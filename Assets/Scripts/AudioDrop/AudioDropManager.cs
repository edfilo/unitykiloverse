using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using System;
using KiloWorld.UI.Stories;

public class AudioDropManager : MonoBehaviour
{
    [Header("Configuration")]
    public string apiUploadUrl = "/api/transcribe";
    public int maxRecordingTime = 60; // Seconds
    public int recordingFrequency = 44100;
    public string contributeEndpoint = "/contribute";

    [Header("Story Context")]
    public StoryViewer storyViewer;

    [Header("UI References")]
    public Image recordButtonImage;
    public Text statusText;
    public GameObject loadingSpinner;

    [Header("Visual Feedback")]
    public Color normalColor = Color.white;
    public Color recordingColor = Color.red;

    private AudioClip recordedClip;
    private float recordingStartTime;
    private bool isRecording = false;
    private string microphoneDevice;

    [Serializable]
    private class TranscribeResponse
    {
        public bool ok;
        public string transcript;
        public string audioUrl;
    }

    [Serializable]
    private class ContributePayload
    {
        public string userId;
        public string prompt;
        public string voiceClip;
        public string voiceTranscript;
        public string storyId;
        public bool generateDialog = true;
        public Coordinates coordinates;
    }

    [Serializable]
    private class Coordinates
    {
        public double latitude;
        public double longitude;
    }

    void Start()
    {
        if (loadingSpinner != null) loadingSpinner.SetActive(false);
        UpdateStatus("Ready to record");
        
        // Request Microphone Permission
        StartCoroutine(RequestMicrophonePermission());
    }

    IEnumerator RequestMicrophonePermission()
    {
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        if (Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            UpdateStatus("Mic ready");
        }
        else
        {
            UpdateStatus("Mic permission denied");
        }
    }

    // Call this from EventTrigger PointerDown
    public void StartRecording()
    {
        if (isRecording) return;
        if (Microphone.devices.Length == 0)
        {
            UpdateStatus("No Mic found");
            return;
        }

        microphoneDevice = Microphone.devices[0];
        recordedClip = Microphone.Start(microphoneDevice, false, maxRecordingTime, recordingFrequency);
        recordingStartTime = Time.time;
        isRecording = true;

        if (recordButtonImage != null) recordButtonImage.color = recordingColor;
        UpdateStatus("Recording...");
    }

    // Call this from EventTrigger PointerUp
    public void StopAndUpload()
    {
        if (!isRecording) return;

        Microphone.End(microphoneDevice);
        isRecording = false;

        float recordingDuration = Time.time - recordingStartTime;
        if (recordingDuration < 0.5f)
        {
            UpdateStatus("Hold longer to record");
            if (recordButtonImage != null) recordButtonImage.color = normalColor;
            return;
        }

        if (recordButtonImage != null) recordButtonImage.color = normalColor;
        UpdateStatus("Processing...");

        StartCoroutine(ProcessAndUpload(recordedClip, recordingDuration));
    }

    IEnumerator ProcessAndUpload(AudioClip clip, float duration)
    {
        if (loadingSpinner != null) loadingSpinner.SetActive(true);

        // 1. Trim and Convert to WAV
        // Note: SavWav.GetWav handles the full clip, but we might want to trim it.
        // For simplicity, we'll send the whole buffer but the server might process it.
        // Better: Create a temporary trimmed clip or let SavWav handle it (SavWav usually dumps the whole buffer).
        // Let's rely on the fact that we stopped recording, but Microphone.Start creates a fixed buffer.
        // We need to trim the AudioClip data before sending.
        
        AudioClip trimmedClip = TrimClip(clip, duration);
        float length;
        byte[] wavData = SavWav.GetWav(trimmedClip, out length);

        // 2. Get Location
        double lat = 0;
        double lon = 0;

        if (Input.location.status == LocationServiceStatus.Running)
        {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
        }

        // 3. Transcribe + Upload
        WWWForm form = new WWWForm();
        form.AddBinaryData("audio", wavData, "recording.wav", "audio/wav");
        form.AddField("latitude", lat.ToString());
        form.AddField("longitude", lon.ToString());
        form.AddField("timestamp", DateTime.UtcNow.ToString());

        // Note: Check if backend expects fields in a specific way.
        // The server.js endpoint uses 'audioInputUpload.single('audio')'.
        // It doesn't seem to explicitly look for lat/lon in the upload body in the snippet I saw,
        // but it's good practice to send it.

        string baseUrl = APIManager.Instance != null ? APIManager.Instance.GetBaseURL() : null;
        string transcribeUrl = apiUploadUrl;
        if (!string.IsNullOrEmpty(baseUrl) && !apiUploadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            transcribeUrl = baseUrl + apiUploadUrl;
        }

        using (UnityWebRequest www = UnityWebRequest.Post(transcribeUrl, form))
        {
            yield return www.SendWebRequest();

            if (loadingSpinner != null) loadingSpinner.SetActive(false);

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Upload failed: {www.error}");
                UpdateStatus("Upload failed");
            }
            else
            {
                Debug.Log($"[AudioDrop] Transcribe response: {www.downloadHandler.text}");
                var response = JsonUtility.FromJson<TranscribeResponse>(www.downloadHandler.text);
                if (response == null || string.IsNullOrEmpty(response.audioUrl))
                {
                    UpdateStatus("Transcribe failed");
                    yield break;
                }

                UpdateStatus("Sending reply...");
                yield return StartCoroutine(SendStoryReply(response, lat, lon));
                UpdateStatus("Sent!");
                yield return new WaitForSeconds(1.5f);
                UpdateStatus("Ready");
            }
        }
    }

    private IEnumerator SendStoryReply(TranscribeResponse response, double lat, double lon)
    {
        string userId = DeviceIDManager.Instance != null
            ? DeviceIDManager.Instance.GetCurrentUserId()
            : SystemInfo.deviceUniqueIdentifier;
        string storyId = storyViewer != null && storyViewer.CurrentStory != null
            ? storyViewer.CurrentStory.id
            : null;

        var payload = new ContributePayload
        {
            userId = userId,
            prompt = response.transcript,
            voiceClip = response.audioUrl,
            voiceTranscript = response.transcript,
            storyId = storyId,
            generateDialog = true,
            coordinates = new Coordinates { latitude = lat, longitude = lon }
        };

        string json = JsonUtility.ToJson(payload);
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

        string baseUrl = APIManager.Instance != null ? APIManager.Instance.GetBaseURL() : null;
        string contributeUrl = contributeEndpoint;
        if (!string.IsNullOrEmpty(baseUrl) && !contributeEndpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            contributeUrl = baseUrl + contributeEndpoint;
        }

        using (UnityWebRequest www = new UnityWebRequest(contributeUrl, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AudioDrop] Contribute failed: {www.error}");
                UpdateStatus("Send failed");
            }
            else
            {
                Debug.Log($"[AudioDrop] Contribute OK: {www.downloadHandler.text}");
            }
        }
    }

    AudioClip TrimClip(AudioClip original, float duration)
    {
        int samples = (int)(recordingFrequency * duration);
        AudioClip newClip = AudioClip.Create(original.name + "_trimmed", samples, original.channels, original.frequency, false);
        
        float[] data = new float[samples * original.channels];
        original.GetData(data, 0);
        newClip.SetData(data, 0);
        
        return newClip;
    }

    void UpdateStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log($"[AudioDrop] {msg}");
    }
}
