using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        // RTDB REST auth — idToken takes priority over legacy secret. SSE streams
        // only accept auth via query string (the Authorization header we use for
        // one-shot REST calls doesn't apply here), so rules-protected paths need
        // the token inlined in the URL or the stream delivers empty data.
        string token = FirebaseAuthManager.Instance != null ? FirebaseAuthManager.Instance.idToken : null;
        if (!string.IsNullOrEmpty(token))
        {
            url += $"?auth={token}";
        }
        else if (!string.IsNullOrEmpty(authSecret))
        {
            url += $"?auth={authSecret}";
        }
        return url;
    }

    // --- SSE Streaming (multi-listener, true streaming via HttpClient) ---
    //
    // Unity's DownloadHandlerScript on macOS standalone buffers chunked responses
    // and only delivers ~1 ReceiveData call per SSE connection, so subsequent
    // Firebase RTDB "put" events never surface. We use System.Net.Http.HttpClient
    // on a background Task with HttpCompletionOption.ResponseHeadersRead and a
    // StreamReader that yields one SSE line at a time. Lines are handed off to
    // the main thread via a ConcurrentQueue which a coroutine drains.
    private class ListenerState
    {
        public CancellationTokenSource cts;
        public Task task;
        public Coroutine drainCoroutine;
        public bool running;
        public Action<string> onData;
        public readonly ConcurrentQueue<QueueItem> queue = new ConcurrentQueue<QueueItem>();
    }

    private enum QueueItemKind { Meta, Line, Error, Reconnect }
    private struct QueueItem
    {
        public QueueItemKind kind;
        public string payload;
    }

    private static readonly HttpClient sseHttpClient = CreateSseHttpClient();
    private readonly Dictionary<string, ListenerState> listeners = new Dictionary<string, ListenerState>();

    private static HttpClient CreateSseHttpClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true };
        var client = new HttpClient(handler)
        {
            // SSE connections are long-lived. Effectively no timeout — the
            // server sends keep-alives every 30s and we handle reconnects.
            Timeout = Timeout.InfiniteTimeSpan
        };
        return client;
    }

    public void StartListening(string path, Action<string> onData)
    {
        StopListening(path);
        var state = new ListenerState
        {
            running = true,
            cts = new CancellationTokenSource(),
            onData = onData
        };
        listeners[path] = state;
        state.drainCoroutine = StartCoroutine(DrainQueueRoutine(path, state));
        state.task = Task.Run(() => SSEStreamLoopAsync(path, state));
    }

    public void StopListening(string path)
    {
        if (!listeners.TryGetValue(path, out var state)) return;
        state.running = false;
        try { state.cts?.Cancel(); } catch { }
        if (state.drainCoroutine != null) StopCoroutine(state.drainCoroutine);
        listeners.Remove(path);
    }

    public void StopListening()
    {
        foreach (var kv in listeners)
        {
            kv.Value.running = false;
            try { kv.Value.cts?.Cancel(); } catch { }
            if (kv.Value.drainCoroutine != null) StopCoroutine(kv.Value.drainCoroutine);
        }
        listeners.Clear();
    }

    private async Task SSEStreamLoopAsync(string path, ListenerState state)
    {
        string url = BuildRtdbUrl(path);
        float retryDelaySec = 1f;

        while (state.running && !state.cts.IsCancellationRequested)
        {
            state.queue.Enqueue(new QueueItem { kind = QueueItemKind.Meta, payload = "connecting" });

            try
            {
                using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    req.Headers.TryAddWithoutValidation("Accept", "text/event-stream");
                    req.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");

                    using (var resp = await sseHttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, state.cts.Token).ConfigureAwait(false))
                    {
                        string ct = resp.Content.Headers.ContentType?.ToString() ?? "?";
                        state.queue.Enqueue(new QueueItem
                        {
                            kind = QueueItemKind.Meta,
                            payload = $"HTTP {(int)resp.StatusCode} ct={ct}"
                        });

                        if (!resp.IsSuccessStatusCode)
                        {
                            state.queue.Enqueue(new QueueItem
                            {
                                kind = QueueItemKind.Error,
                                payload = $"status {(int)resp.StatusCode}"
                            });
                        }
                        else
                        {
                            using (var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (var reader = new StreamReader(stream, Encoding.UTF8))
                            {
                                retryDelaySec = 1f;
                                while (state.running && !state.cts.IsCancellationRequested)
                                {
                                    string line;
                                    try
                                    {
                                        line = await reader.ReadLineAsync().ConfigureAwait(false);
                                    }
                                    catch (Exception e)
                                    {
                                        state.queue.Enqueue(new QueueItem { kind = QueueItemKind.Error, payload = "read: " + e.Message });
                                        break;
                                    }

                                    if (line == null)
                                    {
                                        state.queue.Enqueue(new QueueItem { kind = QueueItemKind.Reconnect, payload = "eof" });
                                        break;
                                    }

                                    state.queue.Enqueue(new QueueItem { kind = QueueItemKind.Line, payload = line });
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                state.queue.Enqueue(new QueueItem { kind = QueueItemKind.Error, payload = e.Message });
            }

            if (!state.running || state.cts.IsCancellationRequested) break;
            try { await Task.Delay(TimeSpan.FromSeconds(retryDelaySec), state.cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            retryDelaySec = Math.Min(retryDelaySec * 2f, 30f);
        }
    }

    private IEnumerator DrainQueueRoutine(string path, ListenerState state)
    {
        string currentEvent = null;
        int lineCount = 0;
        int dataCount = 0;

        while (state.running)
        {
            while (state.queue.TryDequeue(out QueueItem item))
            {
                switch (item.kind)
                {
                    case QueueItemKind.Meta:
                        Debug.Log($"[Firebase SSE] {path} {item.payload}");
                        break;
                    case QueueItemKind.Error:
                        Debug.LogWarning($"[Firebase SSE] {path} error: {item.payload}");
                        break;
                    case QueueItemKind.Reconnect:
                        Debug.Log($"[Firebase SSE] {path} reconnect ({item.payload}) — lines={lineCount} data={dataCount}");
                        currentEvent = null;
                        break;
                    case QueueItemKind.Line:
                        lineCount++;
                        string line = item.payload;
                        if (string.IsNullOrEmpty(line))
                        {
                            currentEvent = null;
                            break;
                        }
                        if (line.StartsWith("event:"))
                        {
                            currentEvent = line.Substring(6).Trim();
                        }
                        else if (line.StartsWith("data:"))
                        {
                            string data = line.Substring(5).Trim();
                            if (string.IsNullOrEmpty(data) || data == "null") break;
                            if (currentEvent == "keep-alive") break;
                            dataCount++;
                            try { state.onData?.Invoke(data); }
                            catch (Exception e) { Debug.LogError($"[Firebase SSE] onData handler threw: {e}"); }
                        }
                        break;
                }
            }
            yield return null;
        }
    }

    void OnDestroy()
    {
        StopListening();
    }
}
