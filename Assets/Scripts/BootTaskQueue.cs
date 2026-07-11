using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Boot tasks run one-by-one; each task's Action runs synchronously on the main thread.
/// Keep task actions FAST (e.g. set flags only). Heavy work must run in coroutines elsewhere.
/// </summary>
public class BootTaskQueue : MonoBehaviour
{
    private static BootTaskQueue _instance;
    private readonly Queue<BootTask> _queue = new Queue<BootTask>();
    private bool _running;

    private class BootTask
    {
        public string Label;
        public Action Action;
        public bool Done;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (_instance != null) return;
        var go = new GameObject("BootTaskQueue");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<BootTaskQueue>();
        // Ensure component stays enabled
        _instance.enabled = true;
        go.SetActive(true);
    }

    private void Awake()
    {
        // Ensure we stay alive
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        enabled = true;
        gameObject.SetActive(true);
    }

    public static IEnumerator Enqueue(string label, Action action)
    {
        EnsureInstance();
#if UNITY_EDITOR
        // Editor: execute task synchronously to avoid coroutine stall when Update() stops
        BootDiagnostics.Mark($"BootTask start {label} (editor sync)");
        try { action?.Invoke(); }
        catch (Exception e) { Debug.LogError($"[BootTaskQueue] Task '{label}' failed: {e.Message}"); }
        BootDiagnostics.Mark($"BootTask completed wait for {label}");
        yield return null;
#else
        var task = new BootTask { Label = label, Action = action, Done = false };
        _instance._queue.Enqueue(task);
        BootDiagnostics.Mark($"BootTask enqueued {label} (queue size: {_instance._queue.Count})");
        _instance.TryStart();
        int waitFrames = 0;
        while (!task.Done)
        {
            waitFrames++;
            if (waitFrames % 60 == 0)
            {
                Debug.LogWarning($"[BootTaskQueue] Waiting for task '{label}' to complete (waited {waitFrames} frames, queue size: {_instance._queue.Count}, running: {_instance._running})");
            }
            yield return null;
        }
        BootDiagnostics.Mark($"BootTask completed wait for {label}");
#endif
    }

    private static void EnsureInstance()
    {
        if (_instance != null) return;
        Init();
    }

    private void TryStart()
    {
        if (_running)
        {
            BootDiagnostics.Mark($"BootTaskQueue.TryStart: Already running (queue size: {_queue.Count})");
            return;
        }
        
        // Double-check: if queue has items but we're not running, something went wrong
        if (_queue.Count > 0 && !_running)
        {
            Debug.LogWarning($"[BootTaskQueue] Queue has {_queue.Count} items but processor not running! Restarting... activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy} enabled={enabled}");
        }
        
        BootDiagnostics.Mark($"BootTaskQueue.TryStart: Starting queue processor (queue size: {_queue.Count})");
        
        // Ensure we're enabled and active before starting coroutine
        if (!enabled) enabled = true;
        if (!gameObject.activeInHierarchy) gameObject.SetActive(true);
        
        StartCoroutine(RunQueue());
    }

    private IEnumerator RunQueue()
    {
        _running = true;
        BootDiagnostics.Mark($"BootTaskQueue.RunQueue: Started (initial queue size: {_queue.Count})");
        
        while (_queue.Count > 0)
        {
            var task = _queue.Dequeue();
            BootDiagnostics.Mark($"BootTask start {task.Label} (remaining in queue: {_queue.Count})");
            float taskStart = Time.realtimeSinceStartup;
            try
            {
                task.Action?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[BootTaskQueue] Task '{task.Label}' failed: {e.Message}\n{e.StackTrace}");
            }
            task.Done = true;
            float taskMs = (Time.realtimeSinceStartup - taskStart) * 1000f;
            if (taskMs > 50f)
                Debug.LogWarning($"[BootTaskQueue] Task '{task.Label}' took {taskMs:F0}ms (should be <50ms to avoid lockups)");
            BootDiagnostics.Mark($"BootTask done {task.Label}");
            
            // Yield to allow other coroutines to run, including BootSequence waiting for this task
            yield return null;
            
            // Check if more tasks were added while we were yielding
            if (_queue.Count > 0)
            {
                BootDiagnostics.Mark($"BootTaskQueue: Continuing with {_queue.Count} more tasks");
            }
        }
        
        _running = false;
        BootDiagnostics.Mark($"BootTaskQueue.RunQueue: Finished (queue empty)");
    }
}
