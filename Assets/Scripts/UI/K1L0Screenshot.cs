using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

public class K1L0Screenshot : MonoBehaviour
{
    private static K1L0Screenshot _instance;
    public static K1L0Screenshot Instance => _instance;

    private bool _capturing;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    public void Capture()
    {
        if (_capturing) return;
        StartCoroutine(CaptureAndUpload());
    }

    private IEnumerator CaptureAndUpload()
    {
        _capturing = true;
        yield return new WaitForEndOfFrame();

        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        byte[] png = tex.EncodeToPNG();
        Destroy(tex);

        string base64 = Convert.ToBase64String(png);
        string json = "{\"image\":\"" + base64 + "\"}";

        Debug.Log($"[Screenshot] Captured {png.Length} bytes, uploading...");

        yield return APIManager.Instance.Post("/screenshot", json, (success, response) =>
        {
            if (success)
                Debug.Log($"[Screenshot] Uploaded: {response}");
            else
                Debug.LogWarning($"[Screenshot] Upload failed: {response}");
        });

        _capturing = false;
    }
}
