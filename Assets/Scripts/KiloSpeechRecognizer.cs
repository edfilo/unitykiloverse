using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class KiloSpeechRecognizer
{
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    private const string Lib = "KiloSpeechRecognizer";
    [DllImport(Lib)] static extern void KiloSpeech_RequestAuthorization();
    [DllImport(Lib)] static extern void KiloSpeech_Start();
    [DllImport(Lib)] static extern void KiloSpeech_Stop();
    [DllImport(Lib)] static extern IntPtr KiloSpeech_GetLatestText();
    [DllImport(Lib)] static extern void KiloSpeech_ClearLatestText();
    [DllImport(Lib)] static extern void KiloSpeech_FreeCString(IntPtr p);

    public static void RequestAuthorization() => KiloSpeech_RequestAuthorization();
    public static void Start() => KiloSpeech_Start();
    public static void Stop() => KiloSpeech_Stop();

    public static string GetLatestText()
    {
        IntPtr p = KiloSpeech_GetLatestText();
        if (p == IntPtr.Zero) return "";
        try { return Marshal.PtrToStringUTF8(p) ?? ""; }
        finally { KiloSpeech_FreeCString(p); }
    }

    public static void ClearLatestText() => KiloSpeech_ClearLatestText();
#else
    public static void RequestAuthorization() { }
    public static void Start() { }
    public static void Stop() { }
    public static string GetLatestText() => "";
    public static void ClearLatestText() { }
#endif
}
