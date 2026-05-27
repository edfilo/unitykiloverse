using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace KiloWorld.UI.Stories
{
    [Serializable]
    public class StoryFeedDto
    {
        public List<StoryGroupDto> stories = new List<StoryGroupDto>();
    }

    [Serializable]
    public class StoryGroupDto
    {
        public string id;
        public string title;
        public string thumbnailUrl;
        public Color thumbnailColor = Color.clear;
        public bool hasThumbnailColor;
        public long createdAt;
        public int lastSeenIndex = -1;
        public List<StorySlideDto> slides = new List<StorySlideDto>();
    }

    [Serializable]
    public class StorySlideDto
    {
        public string id;
        public string mediaType;
        public string mediaUrl;
        public string audioUrl;
        public float durationSeconds = 5f;
        public string text;
        public Color backgroundColor = Color.clear;
        public bool hasBackgroundColor;
    }

    public class StoriesJsonDataSource : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private StoriesStrip targetStrip;
        [SerializeField] private bool applyOnStart = false;

        [Header("Source")]
        [SerializeField] private string jsonUrl;
        [SerializeField] private TextAsset jsonAsset;
        [SerializeField] private bool preferJsonAsset = true;

        [Header("Debug")]
        [SerializeField] private bool logErrors = true;

        private void Start()
        {
            if (applyOnStart)
            {
                Load();
            }
        }

        public void Configure(StoriesStrip strip, bool applyNow)
        {
            targetStrip = strip;
            if (applyNow)
            {
                Load();
            }
        }

        [ContextMenu("Load Stories JSON")]
        public void Load()
        {
            if (preferJsonAsset && jsonAsset != null)
            {
                ApplyJson(jsonAsset.text);
                return;
            }

            if (!string.IsNullOrWhiteSpace(jsonUrl))
            {
                StartCoroutine(LoadFromUrl(jsonUrl));
                return;
            }

            if (logErrors)
            {
                Debug.LogWarning("[StoriesJsonDataSource] No JSON source provided.");
            }
        }

        public void ApplyJson(string json)
        {
            if (!TryParseStories(json, out var mappedStories, logErrors))
            {
                return;
            }

            if (targetStrip != null)
            {
                targetStrip.SetStories(mappedStories);
            }
            else if (logErrors)
            {
                Debug.LogWarning("[StoriesJsonDataSource] Target strip not assigned.");
            }
        }

        private IEnumerator LoadFromUrl(string url)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (logErrors)
                    {
                        Debug.LogWarning($"[StoriesJsonDataSource] Fetch failed: {request.error}");
                    }
                    yield break;
                }

                ApplyJson(request.downloadHandler.text);
            }
        }

        public static bool TryParseStories(string json, out List<StoryGroup> mappedStories, bool logErrors = false)
        {
            mappedStories = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                if (logErrors)
                {
                    Debug.LogWarning("[StoriesJsonDataSource] Empty JSON payload.");
                }
                return false;
            }

            StoryFeedDto dto = null;
            try
            {
                dto = JsonUtility.FromJson<StoryFeedDto>(json);
            }
            catch (Exception ex)
            {
                if (logErrors)
                {
                    Debug.LogWarning($"[StoriesJsonDataSource] JSON parse error: {ex.Message}");
                }
                return false;
            }

            if (dto == null || dto.stories == null)
            {
                if (logErrors)
                {
                    Debug.LogWarning("[StoriesJsonDataSource] JSON missing stories array.");
                }
                return false;
            }

            mappedStories = MapStories(dto.stories);
            return true;
        }

        private static List<StoryGroup> MapStories(List<StoryGroupDto> source)
        {
            var results = new List<StoryGroup>(source.Count);
            foreach (var groupDto in source)
            {
                if (groupDto == null) continue;

                var group = new StoryGroup
                {
                    id = groupDto.id,
                    title = groupDto.title,
                    thumbnailUrl = groupDto.thumbnailUrl,
                    lastSeenIndex = groupDto.lastSeenIndex,
                    thumbnailColor = groupDto.thumbnailColor,
                    hasThumbnailColor = groupDto.hasThumbnailColor,
                    createdAt = groupDto.createdAt,
                    slides = new List<StorySlide>()
                };

                if (groupDto.slides != null)
                {
                    foreach (var slideDto in groupDto.slides)
                    {
                        if (slideDto == null) continue;
                        if (string.IsNullOrWhiteSpace(slideDto.mediaUrl)) continue;

                        var slide = new StorySlide
                        {
                            id = slideDto.id,
                            mediaType = ParseMediaType(slideDto.mediaType),
                            mediaUrl = slideDto.mediaUrl,
                            audioUrl = slideDto.audioUrl,
                            durationSeconds = slideDto.durationSeconds > 0f ? slideDto.durationSeconds : 5f,
                            text = slideDto.text,
                            backgroundColor = slideDto.backgroundColor,
                            hasBackgroundColor = slideDto.hasBackgroundColor
                        };

                        group.slides.Add(slide);
                    }
                }

                if (group.slides == null || group.slides.Count == 0)
                {
                    continue;
                }

                if (!group.hasThumbnailColor && string.IsNullOrWhiteSpace(group.thumbnailUrl))
                {
                    var firstColoredSlide = group.slides.Find(slide => slide != null && slide.hasBackgroundColor);
                    if (firstColoredSlide != null)
                    {
                        group.thumbnailColor = firstColoredSlide.backgroundColor;
                        group.hasThumbnailColor = true;
                    }
                }

                results.Add(group);
            }

            results.Sort((a, b) =>
            {
                long aCreated = a != null ? a.createdAt : 0;
                long bCreated = b != null ? b.createdAt : 0;
                int createdCompare = bCreated.CompareTo(aCreated);
                if (createdCompare != 0) return createdCompare;

                string aId = a != null ? a.id : null;
                string bId = b != null ? b.id : null;
                return string.CompareOrdinal(bId, aId);
            });

            return results;
        }

        private static StoryMediaType ParseMediaType(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return StoryMediaType.Image;

            var trimmed = value.Trim();
            if (int.TryParse(trimmed, out var numeric))
            {
                return numeric == 1 ? StoryMediaType.Video : StoryMediaType.Image;
            }

            switch (trimmed.ToLowerInvariant())
            {
                case "video":
                    return StoryMediaType.Video;
                default:
                    return StoryMediaType.Image;
            }
        }
    }
}
