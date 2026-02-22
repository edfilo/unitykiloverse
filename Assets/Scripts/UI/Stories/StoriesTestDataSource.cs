using System.Collections.Generic;
using UnityEngine;

namespace KiloWorld.UI.Stories
{
    public class StoriesTestDataSource : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private StoriesStrip targetStrip;
        [SerializeField] private bool applyOnStart = false;

        [Header("Sample Data")]
        [SerializeField] private int storyCount = 6;
        [SerializeField] private bool varySlidesPerStory = true;
        [SerializeField] private int minSlidesPerStory = 3;
        [SerializeField] private int maxSlidesPerStory = 6;
        [SerializeField] private int slidesPerStory = 3;
        [SerializeField] private float imageSlideDuration = 4f;
        [SerializeField] private bool useRemoteImages = false;

        [Header("Media URLs")]
        [SerializeField] private string[] sampleVideoUrls =
        {
            "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4",
            "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ElephantsDream.mp4",
            "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/Sintel.mp4",
            "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/TearsOfSteel.mp4"
        };

        [SerializeField] private string[] sampleImageUrls =
        {
            "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/images/BigBuckBunny.jpg",
            "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/images/ElephantsDream.jpg",
            "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/images/Sintel.jpg",
            "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/images/TearsOfSteel.jpg"
        };

        private readonly List<Texture2D> runtimeTextures = new List<Texture2D>();
        private readonly List<Sprite> runtimeSprites = new List<Sprite>();

        private void Start()
        {
            if (applyOnStart)
            {
                ApplyTestData();
            }
        }

        public void Configure(StoriesStrip strip, bool applyNow)
        {
            targetStrip = strip;
            if (applyNow)
            {
                ApplyTestData();
            }
        }

        [ContextMenu("Apply Test Data")]
        public void ApplyTestData()
        {
            if (targetStrip == null)
            {
                Debug.LogWarning("[StoriesTestDataSource] No target strip assigned.");
                return;
            }

            var stories = BuildTestStories();
            targetStrip.SetStories(stories);
        }

        public List<StoryGroup> BuildTestStories()
        {
            ClearRuntimeAssets();

            int safeStoryCount = Mathf.Max(1, storyCount);
            int safeSlidesPerStory = Mathf.Max(1, slidesPerStory);
            int minSlides = Mathf.Max(1, minSlidesPerStory);
            int maxSlides = Mathf.Max(minSlides, maxSlidesPerStory);
            int slideRange = Mathf.Max(1, maxSlides - minSlides + 1);

            var stories = new List<StoryGroup>(safeStoryCount);
            for (int i = 0; i < safeStoryCount; i++)
            {
                var story = new StoryGroup
                {
                    id = $"story_{i + 1:00}",
                    title = $"Story {i + 1:00}",
                    lastSeenIndex = -1
                };

                story.thumbnailSprite = CreateGradientSprite(GetPaletteColor(i, 0), GetPaletteColor(i, 1));
                int slidesForStory = varySlidesPerStory
                    ? (minSlides + ((i + 1) % slideRange))
                    : safeSlidesPerStory;
                slidesForStory = Mathf.Max(1, slidesForStory);
                story.slides = new List<StorySlide>(slidesForStory);

                for (int j = 0; j < slidesForStory; j++)
                {
                    bool useVideo = sampleVideoUrls != null && sampleVideoUrls.Length > 0 && j % 2 == 1;
                    var slide = new StorySlide
                    {
                        id = $"{story.id}_slide_{j + 1:00}",
                        mediaType = useVideo ? StoryMediaType.Video : StoryMediaType.Image,
                        durationSeconds = imageSlideDuration
                    };

                    if (useVideo)
                    {
                        slide.mediaUrl = sampleVideoUrls[(i + j) % sampleVideoUrls.Length];
                    }
                    else
                    {
                        if (useRemoteImages && sampleImageUrls != null && sampleImageUrls.Length > 0)
                        {
                            slide.mediaUrl = sampleImageUrls[(i + j) % sampleImageUrls.Length];
                        }
                        else
                        {
                            slide.imageSprite = CreateGradientSprite(GetPaletteColor(i, j + 2), GetPaletteColor(i, j + 3));
                        }
                    }

                    story.slides.Add(slide);
                }

                stories.Add(story);
            }

            return stories;
        }

        private Sprite CreateGradientSprite(Color start, Color end)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < size; y++)
            {
                float t = y / (float)(size - 1);
                Color rowColor = Color.Lerp(start, end, t);
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, rowColor);
                }
            }

            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            runtimeTextures.Add(texture);
            runtimeSprites.Add(sprite);
            return sprite;
        }

        private Color GetPaletteColor(int storyIndex, int offset)
        {
            int seed = (storyIndex + 1) * (offset + 3);
            float r = Mathf.Abs(Mathf.Sin(seed * 0.37f));
            float g = Mathf.Abs(Mathf.Sin(seed * 0.59f));
            float b = Mathf.Abs(Mathf.Sin(seed * 0.83f));
            return new Color(r, g, b, 1f);
        }

        private void ClearRuntimeAssets()
        {
            for (int i = 0; i < runtimeSprites.Count; i++)
            {
                if (runtimeSprites[i] == null) continue;
                if (Application.isPlaying)
                {
                    Destroy(runtimeSprites[i]);
                }
                else
                {
                    DestroyImmediate(runtimeSprites[i]);
                }
            }
            runtimeSprites.Clear();

            for (int i = 0; i < runtimeTextures.Count; i++)
            {
                if (runtimeTextures[i] == null) continue;
                if (Application.isPlaying)
                {
                    Destroy(runtimeTextures[i]);
                }
                else
                {
                    DestroyImmediate(runtimeTextures[i]);
                }
            }
            runtimeTextures.Clear();
        }
    }
}
