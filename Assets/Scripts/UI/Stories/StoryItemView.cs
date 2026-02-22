using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KiloWorld.UI.Stories
{
    public class StoryItemView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button button;
        [SerializeField] private RectTransform ringRect;
        [SerializeField] private Image ringImage;
        [SerializeField] private RectTransform maskRect;
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private TMP_Text titleText;

        [Header("Layout")]
        [SerializeField] private float diameter = 72f;
        [SerializeField] private float ringThickness = 6f;
        [SerializeField] private float seenRingThicknessMultiplier = 0.55f;

        [Header("Colors")]
        [SerializeField] private Color unplayedRingColor = Color.white;
        [SerializeField] private Color playedRingColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        private Action<StoryGroup> clickHandler;
        private bool isSeen;

        public StoryGroup BoundStory { get; private set; }

        public void InitializeReferences(
            Button newButton,
            RectTransform newRingRect,
            Image newRingImage,
            RectTransform newMaskRect,
            Image newThumbnailImage,
            TMP_Text newTitleText)
        {
            button = newButton;
            ringRect = newRingRect;
            ringImage = newRingImage;
            maskRect = newMaskRect;
            thumbnailImage = newThumbnailImage;
            titleText = newTitleText;
            ApplyLayout();
        }

        public void Bind(StoryGroup story, bool seen, Action<StoryGroup> onClick)
        {
            BoundStory = story;
            clickHandler = onClick;
            SetTitle(story != null ? story.title : string.Empty);
            SetThumbnail(story != null ? story.thumbnailSprite : null);
            ConfigureLayout(diameter, ringThickness);
            SetSeen(seen);
            HookButton();
        }

        public void SetSeen(bool seen)
        {
            isSeen = seen;
            if (ringImage != null)
            {
                ringImage.color = isSeen ? playedRingColor : unplayedRingColor;
            }
            float thickness = isSeen ? ringThickness * seenRingThicknessMultiplier : ringThickness;
            ApplyRingThickness(thickness);
        }

        public void SetRingColors(Color unplayed, Color played)
        {
            unplayedRingColor = unplayed;
            playedRingColor = played;
            SetSeen(isSeen);
        }

        public void ConfigureLayout(float newDiameter, float newRingThickness)
        {
            diameter = newDiameter;
            ringThickness = newRingThickness;
            ApplyLayout();
        }

        public void SetTitle(string title)
        {
            if (titleText != null)
            {
                titleText.text = title ?? string.Empty;
            }
        }

        public void SetThumbnail(Sprite sprite)
        {
            if (thumbnailImage != null)
            {
                if (sprite != null)
                {
                    thumbnailImage.sprite = sprite;
                    thumbnailImage.color = Color.white;
                }
                else if (BoundStory != null && BoundStory.hasThumbnailColor)
                {
                    thumbnailImage.sprite = GetSolidSprite();
                    thumbnailImage.color = BoundStory.thumbnailColor;
                }
                else
                {
                    thumbnailImage.sprite = null;
                    thumbnailImage.color = Color.white;
                }
            }
        }

        private void HookButton()
        {
            if (button == null) return;
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }

        private void HandleClick()
        {
            if (BoundStory == null) return;
            clickHandler?.Invoke(BoundStory);
        }

        private void ApplyLayout()
        {
            if (ringRect != null)
            {
                ringRect.sizeDelta = new Vector2(diameter, diameter);
            }

            ApplyRingThickness(ringThickness);
        }

        private void ApplyRingThickness(float thickness)
        {
            if (maskRect != null)
            {
                float innerDiameter = Mathf.Max(0f, diameter - thickness * 2f);
                maskRect.sizeDelta = new Vector2(innerDiameter, innerDiameter);
            }

            if (thumbnailImage != null)
            {
                thumbnailImage.rectTransform.sizeDelta = maskRect != null ? maskRect.sizeDelta : Vector2.zero;
            }
        }

        private static Sprite GetSolidSprite()
        {
            if (s_solidSprite != null) return s_solidSprite;
            var texture = Texture2D.whiteTexture;
            s_solidSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            return s_solidSprite;
        }

        private static Sprite s_solidSprite;
    }
}
