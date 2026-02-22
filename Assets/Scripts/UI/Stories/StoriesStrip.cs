using System;
using System.Collections.Generic;
using UnityEngine;

namespace KiloWorld.UI.Stories
{
    public class StoriesStrip : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private StoryItemView itemPrefab;
        [SerializeField] private StoryViewer storyViewer;
        [SerializeField] private StoryMediaLoader mediaLoader;

        [Header("Style")]
        [SerializeField] private Color unplayedRingColor = Color.white;
        [SerializeField] private Color playedRingColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private float itemDiameter = 72f;
        [SerializeField] private float ringThickness = 6f;

        [Header("Behavior")]
        [SerializeField] private bool autoRebuildOnEnable = false;

        public event Action<StoryGroup> StorySelected;
        public event Action<StoryGroup> StoryCompleted;

        private readonly List<StoryGroup> stories = new List<StoryGroup>();
        private readonly List<StoryItemView> itemViews = new List<StoryItemView>();

        private void OnEnable()
        {
            AttachViewerEvents();

            if (autoRebuildOnEnable)
            {
                Rebuild();
            }
        }

        private void OnDisable()
        {
            DetachViewerEvents();
        }

        public void Initialize(RectTransform newContentRoot, StoryItemView newItemPrefab, StoryViewer newViewer, StoryMediaLoader newMediaLoader)
        {
            contentRoot = newContentRoot;
            itemPrefab = newItemPrefab;
            mediaLoader = newMediaLoader;
            if (storyViewer != newViewer)
            {
                DetachViewerEvents();
                storyViewer = newViewer;
                AttachViewerEvents();
            }
        }

        public void ConfigureStyle(float diameter, float thickness, Color unplayedColor, Color playedColor)
        {
            itemDiameter = diameter;
            ringThickness = thickness;
            unplayedRingColor = unplayedColor;
            playedRingColor = playedColor;
        }

        public void SetStories(IEnumerable<StoryGroup> newStories)
        {
            stories.Clear();
            if (newStories != null)
            {
                stories.AddRange(newStories);
            }

            Rebuild();
        }

        public void Rebuild()
        {
            ClearViews();

            if (contentRoot == null || itemPrefab == null)
            {
                Debug.LogWarning("[StoriesStrip] Missing contentRoot or itemPrefab.");
                return;
            }

            for (int i = 0; i < stories.Count; i++)
            {
                var story = stories[i];
                var view = Instantiate(itemPrefab, contentRoot);
                view.gameObject.SetActive(true);
                view.ConfigureLayout(itemDiameter, ringThickness);
                view.SetRingColors(unplayedRingColor, playedRingColor);
                view.Bind(story, story != null && story.IsSeen, HandleStoryClicked);
                itemViews.Add(view);

                if (story != null && story.thumbnailSprite == null && !string.IsNullOrWhiteSpace(story.thumbnailUrl) && mediaLoader != null)
                {
                    mediaLoader.LoadSprite(story.thumbnailUrl, sprite =>
                    {
                        if (sprite == null || view == null) return;
                        story.thumbnailSprite = sprite;
                        view.SetThumbnail(sprite);
                    });
                }
            }
        }

        public void ClearViews()
        {
            for (int i = 0; i < itemViews.Count; i++)
            {
                if (itemViews[i] == null) continue;
                if (Application.isPlaying)
                {
                    Destroy(itemViews[i].gameObject);
                }
                else
                {
                    DestroyImmediate(itemViews[i].gameObject);
                }
            }
            itemViews.Clear();
        }

        private void HandleStoryClicked(StoryGroup story)
        {
            if (story == null) return;

            StorySelected?.Invoke(story);

            if (storyViewer != null)
            {
                int startIndex = Mathf.Clamp(story.lastSeenIndex + 1, 0, story.slides.Count - 1);
                storyViewer.Show(story, startIndex);
            }
        }

        private void HandleSlideChanged(StoryGroup story, int slideIndex)
        {
            UpdateStoryView(story);
        }

        private void HandleStoryCompleted(StoryGroup story)
        {
            UpdateStoryView(story);
            StoryCompleted?.Invoke(story);
        }

        private void UpdateStoryView(StoryGroup story)
        {
            if (story == null) return;

            for (int i = 0; i < itemViews.Count; i++)
            {
                var view = itemViews[i];
                if (view == null) continue;

                if (view.BoundStory == story || (view.BoundStory != null && view.BoundStory.id == story.id))
                {
                    view.SetSeen(story.IsSeen);
                    break;
                }
            }
        }

        private void AttachViewerEvents()
        {
            if (storyViewer == null) return;
            storyViewer.SlideChanged += HandleSlideChanged;
            storyViewer.StoryCompleted += HandleStoryCompleted;
        }

        private void DetachViewerEvents()
        {
            if (storyViewer == null) return;
            storyViewer.SlideChanged -= HandleSlideChanged;
            storyViewer.StoryCompleted -= HandleStoryCompleted;
        }
    }
}
