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
        Capture(false);
    }

    public void Capture(bool analyze)
    {
        if (_capturing) return;
        StartCoroutine(CaptureAndUpload(analyze));
    }

    private IEnumerator CaptureAndUpload(bool analyze)
    {
        _capturing = true;
        yield return new WaitForEndOfFrame();

        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        byte[] png = tex.EncodeToPNG();
        Destroy(tex);

        string base64 = Convert.ToBase64String(png);
        string json = "{\"image\":\"" + base64 + "\",\"analyze\":" + (analyze ? "true" : "false") + "}";

        Debug.Log($"[Screenshot] Captured {png.Length} bytes, uploading analyze={analyze}...");

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
