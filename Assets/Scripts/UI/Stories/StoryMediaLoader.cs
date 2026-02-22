using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace KiloWorld.UI.Stories
{
    public class StoryMediaLoader : MonoBehaviour
    {
        [SerializeField] private int maxCachedSprites = 64;

        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private readonly Queue<string> spriteOrder = new Queue<string>();

        public void LoadSprite(string url, Action<Sprite> onComplete)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                onComplete?.Invoke(null);
                return;
            }

            if (spriteCache.TryGetValue(url, out var cachedSprite))
            {
                onComplete?.Invoke(cachedSprite);
                return;
            }

            StartCoroutine(LoadSpriteCoroutine(url, onComplete));
        }

        public void ClearCache()
        {
            spriteCache.Clear();
            spriteOrder.Clear();
        }

        private IEnumerator LoadSpriteCoroutine(string url, Action<Sprite> onComplete)
        {
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[StoryMediaLoader] Failed to load sprite: {request.error}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                var texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                {
                    onComplete?.Invoke(null);
                    yield break;
                }

                var sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );

                CacheSprite(url, sprite);
                onComplete?.Invoke(sprite);
            }
        }

        private void CacheSprite(string url, Sprite sprite)
        {
            if (spriteCache.ContainsKey(url)) return;

            if (maxCachedSprites > 0)
            {
                while (spriteOrder.Count >= maxCachedSprites)
                {
                    var oldestUrl = spriteOrder.Dequeue();
                    spriteCache.Remove(oldestUrl);
                }
            }

            spriteCache[url] = sprite;
            spriteOrder.Enqueue(url);
        }
    }
}
