# Coroutines, Yield, and Avoiding Freezes in Unity

## What is a Coroutine?

A **coroutine** is a method that can pause and resume over multiple frames. Unlike a normal method that runs to completion in one frame, a coroutine uses `yield return` to say "pause here, run the rest of the frame, resume me next time."

```csharp
IEnumerator MyCoroutine()
{
    Debug.Log("Frame 1");
    yield return null;  // Pause - resume next frame
    Debug.Log("Frame 2");
    yield return new WaitForSeconds(2f);  // Pause 2 seconds
    Debug.Log("Frame 3 (2 seconds later)");
}
```

## What is Yield?

- `yield return null` — Pause until the next frame
- `yield return new WaitForSeconds(2f)` — Pause for 2 seconds
- `yield return someCoroutine` — Wait for another coroutine to finish

**Critical:** Coroutines run on the **main thread**. They are NOT parallel. When you yield, you're letting other code (including other coroutines) run. When you don't yield, your coroutine blocks the main thread until it hits a yield.

## The Freeze Bug: Tight Loop Without Yield

```csharp
while (true)
{
    if (playerController == null)
    {
        continue;  // ❌ BUG: No yield! Infinite loop, 100% CPU, editor freezes
    }
    // ...
}
```

When `continue` runs, the loop immediately iterates again. There is no yield. The coroutine spins forever in a tight loop, consuming 100% CPU and freezing Unity. You must force quit.

**Fix:** Always yield before `continue` in a loop:

```csharp
if (playerController == null)
{
    yield return new WaitForSeconds(0.5f);  // ✓ Yield! Let other code run
    continue;
}
```

## Other Common Freeze Causes

1. **Heavy synchronous work** — Parsing 1000 features, creating 100 GameObjects in one frame
2. **Cascading layout rebuilds** — `ForceRebuildLayoutImmediate` triggers `OnRectTransformDimensionsChanged` which triggers another rebuild
3. **FindObjectOfType in a loop** — Very slow, blocks main thread
4. **Many coroutines starting at once** — All do work before yielding, pile up in one frame

## LayoutManager + Places

LocationsPanel shows places from TransmitterScanner. When TransmitterScanner's PlacesPollRoutine hits a `continue` without yielding (e.g. player at 0,0 or playerController null), it freezes. LocationsPanel's RefreshList creates many UI items and calls ForceRebuildLayoutImmediate — heavy work that can compound the freeze when combined with the scanner bug.
