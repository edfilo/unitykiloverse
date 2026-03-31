using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Text;

public class FirebaseRestClient : MonoBehaviour
{
    [Header("Configuration")]
    public string databaseUrl = "https://kiloworld-aa8d6-default-rtdb.firebaseio.com";
    public string authSecret = ""; // Optional: Database Secret or Auth Token
    public string firestoreProjectId = "kiloworld-aa8d6";

    // Singleton access
    public static FirebaseRestClient Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        // Ensure URL doesn't end with slash
        if (databaseUrl.EndsWith("/")) databaseUrl = databaseUrl.Substring(0, databaseUrl.Length - 1);
    }

    // --- Public API ---

    public void GetData(string path, Action<string> onSuccess, Action<string> onError = null)
    {
        StartCoroutine(GetRoutine(BuildRtdbUrl(path), onSuccess, onError));
    }

    public void SetData(string path, string json, Action<string> onSuccess = null, Action<string> onError = null)
    {
        StartCoroutine(RequestRoutine(BuildRtdbUrl(path), "PUT", json, onSuccess, onError));
    }

    public void UpdateData(string path, string json, Action<string> onSuccess = null, Action<string> onError = null)
    {
        StartCoroutine(RequestRoutine(BuildRtdbUrl(path), "PATCH", json, onSuccess, onError));
    }

    public void PushData(string path, string json, Action<string> onSuccess = null, Action<string> onError = null)
    {
        StartCoroutine(PostRoutine(BuildRtdbUrl(path), json, onSuccess, onError));
    }

    public void DeleteData(string path, Action<string> onSuccess = null, Action<string> onError = null)
    {
        StartCoroutine(RequestRoutine(BuildRtdbUrl(path), "DELETE", null, onSuccess, onError));
    }
    
    // --- Firestore API ---
    public void GetFirestoreData(string collection, string documentId, Action<string> onSuccess, Action<string> onError = null)
    {
        string url = $"https://firestore.googleapis.com/v1/projects/{firestoreProjectId}/databases/(default)/documents/{collection}/{documentId}";
        StartCoroutine(GetRoutine(url, onSuccess, onError));
    }

    public void SetFirestoreData(string collection, string documentId, System.Collections.Generic.Dictionary<string, object> data, Action<string> onSuccess = null, Action<string> onError = null)
    {
        string url = $"https://firestore.googleapis.com/v1/projects/{firestoreProjectId}/databases/(default)/documents/{collection}/{documentId}";
        string json = ConvertToFirestoreJson(data);
        StartCoroutine(RequestRoutine(url, "PATCH", json, onSuccess, onError));
    }

    public void RunFirestoreQuery(string collection, string jsonQuery, Action<string> onSuccess, Action<string> onError = null)
    {
        string url = $"https://firestore.googleapis.com/v1/projects/{firestoreProjectId}/databases/(default)/documents:runQuery";
        StartCoroutine(PostRoutine(url, jsonQuery, onSuccess, onError));
    }

    // Convert C# dictionary to Firestore JSON format
    private string ConvertToFirestoreJson(System.Collections.Generic.Dictionary<string, object> data)
    {
        var fields = new StringBuilder();
        fields.Append("{\"fields\":{");

        bool first = true;
        foreach (var kvp in data)
        {
            if (!first) fields.Append(",");
            first = false;

            fields.Append($"\"{kvp.Key}\":{{");

            if (kvp.Value is string)
                fields.Append($"\"stringValue\":\"{EscapeJson(kvp.Value.ToString())}\"");
            else if (kvp.Value is int || kvp.Value is long)
                fields.Append($"\"integerValue\":\"{kvp.Value}\"");
            else if (kvp.Value is float || kvp.Value is double)
                fields.Append($"\"doubleValue\":{kvp.Value}");
            else if (kvp.Value is bool)
                fields.Append($"\"booleanValue\":{kvp.Value.ToString().ToLower()}");
            else
                fields.Append($"\"stringValue\":\"{EscapeJson(kvp.Value?.ToString() ?? "")}\"");

            fields.Append("}");
        }

        fields.Append("}}");
        return fields.ToString();
    }

    private string EscapeJson(string str)
    {
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    // --- Coroutines ---

    private IEnumerator GetRoutine(string url, Action<string> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            if (FirebaseAuthManager.Instance != null && !string.IsNullOrEmpty(FirebaseAuthManager.Instance.idToken))
            {
                www.SetRequestHeader("Authorization", $"Bearer {FirebaseAuthManager.Instance.idToken}");
            }

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Firebase] GET Error: {www.error} ({url})");
                Debug.LogError($"[Firebase] GET Response body: {www.downloadHandler?.text}");
                onError?.Invoke(www.error);
            }
            else
            {
                onSuccess?.Invoke(www.downloadHandler.text);
            }
        }
    }

    private IEnumerator RequestRoutine(string url, string method, string json, Action<string> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest www = new UnityWebRequest(url, method))
        {
            if (!string.IsNullOrEmpty(json))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.SetRequestHeader("Content-Type", "application/json");
            }
            
            if (FirebaseAuthManager.Instance != null && !string.IsNullOrEmpty(FirebaseAuthManager.Instance.idToken))
            {
                www.SetRequestHeader("Authorization", $"Bearer {FirebaseAuthManager.Instance.idToken}");
            }

            www.downloadHandler = new DownloadHandlerBuffer();

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                string body = www.downloadHandler?.text ?? "";
                Debug.LogError($"[Firebase] {method} Error: {www.error} ({url})");
                Debug.LogError($"[Firebase] {method} Response body: {body}");
                onError?.Invoke($"{www.error} | {body}");
            }
            else
            {
                onSuccess?.Invoke(www.downloadHandler.text);
            }
        }
    }

    private IEnumerator PostRoutine(string url, string json, Action<string> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            if (FirebaseAuthManager.Instance != null && !string.IsNullOrEmpty(FirebaseAuthManager.Instance.idToken))
            {
                www.SetRequestHeader("Authorization", $"Bearer {FirebaseAuthManager.Instance.idToken}");
            }

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Firebase] POST Error: {www.error} ({url})");
                onError?.Invoke(www.error);
            }
            else
            {
                // Firebase returns {"name": "PUSH_ID"} or Firestore query results
                onSuccess?.Invoke(www.downloadHandler.text);
            }
        }
    }

    private string BuildRtdbUrl(string path)
    {
        if (!path.StartsWith("/")) path = "/" + path;
        string url = $"{databaseUrl}{path}.json";
        if (!string.IsNullOrEmpty(authSecret))
        {
            url += $"?auth={authSecret}";
        }
        return url;
    }

    // --- SSE Streaming ---
    private Coroutine sseCoroutine;
    private bool sseRunning;

    public void StartListening(string path, Action<string> onData)
    {
        StopListening();
        sseCoroutine = StartCoroutine(SSEListenRoutine(path, onData));
    }

    public void StopListening()
    {
        sseRunning = false;
        if (sseCoroutine != null)
        {
            StopCoroutine(sseCoroutine);
            sseCoroutine = null;
        }
    }

    private IEnumerator SSEListenRoutine(string path, Action<string> onData)
    {
        sseRunning = true;
        float retryDelay = 1f;

        while (sseRunning)
        {
            string url = BuildRtdbUrl(path);
            // SSE requires orderBy for streaming
            url += url.Contains("?") ? "&" : "?";

            Debug.Log($"[Firebase SSE] Connecting to {path}...");

            using (UnityWebRequest www = new UnityWebRequest(url, "GET"))
            {
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Accept", "text/event-stream");
                www.SetRequestHeader("Cache-Control", "no-cache");

                var op = www.SendWebRequest();
                float lastDataTime = Time.realtimeSinceStartup;
                int lastLength = 0;

                while (!op.isDone && sseRunning)
                {
                    string text = www.downloadHandler?.text ?? "";
                    if (text.Length > lastLength)
                    {
                        string newData = text.Substring(lastLength);
                        lastLength = text.Length;
                        lastDataTime = Time.realtimeSinceStartup;

                        // Parse SSE events
                        string[] lines = newData.Split('\n');
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].StartsWith("data:"))
                            {
                                string data = lines[i].Substring(5).Trim();
                                if (!string.IsNullOrEmpty(data) && data != "null")
                                {
                                    onData?.Invoke(data);
                                }
                            }
                        }

                        retryDelay = 1f; // Reset on success
                    }

                    // Timeout: reconnect if no data for 60s
                    if (Time.realtimeSinceStartup - lastDataTime > 60f)
                    {
                        Debug.Log("[Firebase SSE] No data for 60s, reconnecting...");
                        break;
                    }

                    yield return null;
                }

                if (www.result != UnityWebRequest.Result.Success && sseRunning)
                {
                    Debug.LogWarning($"[Firebase SSE] Connection lost: {www.error}. Retrying in {retryDelay}s...");
                }
            }

            if (sseRunning)
            {
                yield return new WaitForSeconds(retryDelay);
                retryDelay = Mathf.Min(retryDelay * 2f, 30f);
            }
        }
    }

    void OnDestroy()
    {
        StopListening();
    }
}
