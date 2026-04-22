using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;

namespace KiloWorld.UI.Stories
{
    public class StoriesUIBootstrapper : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private float stripHeight = 300f;
        [SerializeField] private float topPadding = 60f;
        [SerializeField] private float itemDiameter = 180f;
        [SerializeField] private float ringThickness = 10f;
        [SerializeField] private float itemSpacing = 26f;
        [SerializeField] private float titleFontSize = 28f;
        [SerializeField] private float titleHeight = 40f;
        [SerializeField] private float ringTopPadding = 12f;
        [SerializeField] private float titleSpacing = 8f;

        [Header("Colors")]
        [SerializeField] private Color stripBackground = new Color(0.05f, 0.08f, 0.14f, 0.3f);
        [SerializeField] private Color unplayedRingColor = new Color(0.1f, 0.85f, 1f, 0.85f);
        [SerializeField] private Color playedRingColor = new Color(0.3f, 0.4f, 0.5f, 0.5f);
        [SerializeField] private Color titleColor = new Color(0.82f, 0.88f, 1f, 0.85f);

        [Header("Progress Bar")]
        [SerializeField] private float progressHeight = 6f;
        [SerializeField] private float progressSpacing = 4f;
        [SerializeField] private Color progressFillColor = new Color(0.7f, 0.92f, 1f, 0.9f);
        [SerializeField] private Color progressBackgroundColor = new Color(0.15f, 0.25f, 0.4f, 0.4f);

        [Header("Test Data")]
        [SerializeField] private bool useTestData = false;
        [SerializeField] private bool applyTestDataOnStart = false;

        private Canvas targetCanvas;
        private StoriesStrip storiesStrip;
        private StoryViewer storyViewer;
        private StoryMediaLoader mediaLoader;
        private StoryItemView itemTemplate;
        private StoryProgressSegment progressSegmentTemplate;
        private Sprite circleSprite;
        private Sprite solidSprite;
        private bool built;

        private void Start()
        {
            Build();
        }

        public void Build()
        {
            if (built) return;

            targetCanvas = FindOrCreateCanvas();
            if (targetCanvas == null)
            {
                Debug.LogWarning("[StoriesUIBootstrapper] No canvas available.");
                return;
            }

            mediaLoader = GetOrAddComponent<StoryMediaLoader>(gameObject);
            circleSprite = CreateCircleSprite(128);
            solidSprite = CreateSolidSprite();

            RectTransform stripRoot = CreateStripRoot(targetCanvas.transform);
            RectTransform contentRoot = CreateStripScrollRect(stripRoot);
            itemTemplate = CreateStoryItemTemplate(stripRoot, contentRoot);

            storiesStrip = GetOrAddComponent<StoriesStrip>(stripRoot.gameObject);
            storiesStrip.Initialize(contentRoot, itemTemplate, null, mediaLoader);
            storiesStrip.ConfigureStyle(itemDiameter, ringThickness, unplayedRingColor, playedRingColor);

            storyViewer = CreateStoryViewer(targetCanvas.transform);
            storyViewer.Hide(); // Start hidden — only shown when user taps a story circle
            storiesStrip.Initialize(contentRoot, itemTemplate, storyViewer, mediaLoader);

            if (useTestData)
            {
                var testData = GetOrAddComponent<StoriesTestDataSource>(gameObject);
                testData.Configure(storiesStrip, applyTestDataOnStart);
            }

            built = true;
        }

        private Canvas FindOrCreateCanvas()
        {
            // Always create a dedicated ScreenSpaceOverlay canvas for stories
            // (PedometerCanvas may be WorldSpace/ScreenSpaceCamera and not visible)
            var storiesGO = GameObject.Find("StoriesCanvas");
            if (storiesGO != null)
            {
                var c = storiesGO.GetComponent<Canvas>();
                if (c != null && c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
            }

            var canvasGO = new GameObject("StoriesCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9000;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private RectTransform CreateStripRoot(Transform parent)
        {
            var root = new GameObject("StoriesStripRoot", typeof(RectTransform));
            root.transform.SetParent(parent, false);

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, stripHeight);

            float safeInset = GetSafeAreaTopInset(parent.GetComponent<Canvas>());
            rect.anchoredPosition = new Vector2(0f, -(safeInset + topPadding));

            return rect;
        }

        private RectTransform CreateStripScrollRect(RectTransform stripRoot)
        {
            var scrollObj = new GameObject("StoriesScrollRect", typeof(RectTransform));
            scrollObj.transform.SetParent(stripRoot, false);
            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 25f;

            var scrollRectTransform = scrollObj.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0f, 0f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;

            var viewportObj = new GameObject("Viewport", typeof(RectTransform));
            viewportObj.transform.SetParent(scrollObj.transform, false);
            var viewportRect = viewportObj.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var viewportImage = viewportObj.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = false;
            viewportObj.AddComponent<RectMask2D>();

            var contentObj = new GameObject("Content", typeof(RectTransform));
            contentObj.transform.SetParent(viewportObj.transform, false);
            var contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0.5f);
            contentRect.anchorMax = new Vector2(0f, 0.5f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.anchoredPosition = new Vector2(20f, 0f);
            contentRect.sizeDelta = new Vector2(0f, stripHeight);

            var layout = contentObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = itemSpacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            return contentRect;
        }

        private StoryItemView CreateStoryItemTemplate(RectTransform stripRoot, RectTransform contentRoot)
        {
            var templateObj = new GameObject("StoryItemTemplate", typeof(RectTransform));
            templateObj.transform.SetParent(stripRoot, false);
            templateObj.SetActive(false);

            var itemRect = templateObj.GetComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(itemDiameter + 16f, stripHeight);

            var layout = templateObj.AddComponent<LayoutElement>();
            layout.preferredWidth = itemDiameter + 16f;
            layout.preferredHeight = stripHeight;

            var buttonImage = templateObj.AddComponent<Image>();
            buttonImage.color = new Color(0f, 0f, 0f, 0f);

            var button = templateObj.AddComponent<Button>();
            button.transition = Selectable.Transition.None;

            var ringObj = new GameObject("Ring", typeof(RectTransform));
            ringObj.transform.SetParent(templateObj.transform, false);
            var ringRect = ringObj.GetComponent<RectTransform>();
            ringRect.anchorMin = new Vector2(0.5f, 1f);
            ringRect.anchorMax = new Vector2(0.5f, 1f);
            ringRect.pivot = new Vector2(0.5f, 1f);
            ringRect.sizeDelta = new Vector2(itemDiameter, itemDiameter);
            ringRect.anchoredPosition = new Vector2(0f, -ringTopPadding);

            var ringImage = ringObj.AddComponent<Image>();
            ringImage.sprite = circleSprite;
            ringImage.color = unplayedRingColor;
            ringImage.raycastTarget = false;

            var maskObj = new GameObject("Mask", typeof(RectTransform));
            maskObj.transform.SetParent(ringObj.transform, false);
            var maskRect = maskObj.GetComponent<RectTransform>();
            float innerDiameter = Mathf.Max(0f, itemDiameter - ringThickness * 2f);
            maskRect.sizeDelta = new Vector2(innerDiameter, innerDiameter);
            maskRect.anchorMin = new Vector2(0.5f, 0.5f);
            maskRect.anchorMax = new Vector2(0.5f, 0.5f);
            maskRect.pivot = new Vector2(0.5f, 0.5f);
            maskRect.anchoredPosition = Vector2.zero;

            var maskImage = maskObj.AddComponent<Image>();
            maskImage.sprite = circleSprite;
            maskImage.color = Color.white;
            maskImage.raycastTarget = false;

            var mask = maskObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var thumbObj = new GameObject("Thumbnail", typeof(RectTransform));
            thumbObj.transform.SetParent(maskObj.transform, false);
            var thumbRect = thumbObj.GetComponent<RectTransform>();
            thumbRect.anchorMin = Vector2.zero;
            thumbRect.anchorMax = Vector2.one;
            thumbRect.offsetMin = Vector2.zero;
            thumbRect.offsetMax = Vector2.zero;
            var thumbImage = thumbObj.AddComponent<Image>();
            thumbImage.color = Color.white;
            thumbImage.raycastTarget = false;

            var titleObj = new GameObject("Title", typeof(RectTransform));
            titleObj.transform.SetParent(templateObj.transform, false);
            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, titleHeight);
            float titleY = ringTopPadding + itemDiameter + titleSpacing;
            titleRect.anchoredPosition = new Vector2(0f, -titleY);

            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Story";
            titleText.fontSize = titleFontSize;
            titleText.color = titleColor;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.raycastTarget = false;

            var view = templateObj.AddComponent<StoryItemView>();
            view.InitializeReferences(button, ringRect, ringImage, maskRect, thumbImage, titleText);

            return view;
        }

        private StoryViewer CreateStoryViewer(Transform parent)
        {
            var viewerObj = new GameObject("StoryViewer", typeof(RectTransform));
            viewerObj.transform.SetParent(parent, false);
            var viewerRect = viewerObj.GetComponent<RectTransform>();
            viewerRect.anchorMin = Vector2.zero;
            viewerRect.anchorMax = Vector2.one;
            viewerRect.offsetMin = Vector2.zero;
            viewerRect.offsetMax = Vector2.zero;

            var canvas = viewerObj.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10000;
            viewerObj.AddComponent<GraphicRaycaster>();

            var canvasGroup = viewerObj.AddComponent<CanvasGroup>();

            var background = viewerObj.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 1f);

            var imageObj = new GameObject("ImageView", typeof(RectTransform));
            imageObj.transform.SetParent(viewerObj.transform, false);
            var imageRect = imageObj.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            var imageView = imageObj.AddComponent<Image>();
            imageView.preserveAspect = true;

            var videoObj = new GameObject("VideoView", typeof(RectTransform));
            videoObj.transform.SetParent(viewerObj.transform, false);
            var videoRect = videoObj.GetComponent<RectTransform>();
            videoRect.anchorMin = Vector2.zero;
            videoRect.anchorMax = Vector2.one;
            videoRect.offsetMin = Vector2.zero;
            videoRect.offsetMax = Vector2.zero;
            var videoView = videoObj.AddComponent<RawImage>();

            var videoPlayer = viewerObj.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = VideoRenderMode.APIOnly;

            var progressRoot = new GameObject("ProgressBar", typeof(RectTransform));
            progressRoot.transform.SetParent(viewerObj.transform, false);
            var progressRect = progressRoot.GetComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0f, 1f);
            progressRect.anchorMax = new Vector2(1f, 1f);
            progressRect.pivot = new Vector2(0.5f, 1f);
            progressRect.sizeDelta = new Vector2(0f, progressHeight);
            progressRect.anchoredPosition = new Vector2(0f, -12f);

            var progressLayout = progressRoot.AddComponent<HorizontalLayoutGroup>();
            progressLayout.spacing = progressSpacing;
            progressLayout.childAlignment = TextAnchor.MiddleCenter;
            progressLayout.childControlWidth = true;
            progressLayout.childControlHeight = true;
            progressLayout.childForceExpandWidth = true;
            progressLayout.childForceExpandHeight = true;

            var progressSegment = CreateProgressSegmentTemplate(progressRoot.transform);

            var progressBar = progressRoot.AddComponent<StoryProgressBar>();
            progressBar.Initialize(progressRect, progressSegment);
            progressBar.SetColors(progressBackgroundColor, progressFillColor);

            var titleObj = new GameObject("TitleText", typeof(RectTransform));
            titleObj.transform.SetParent(viewerObj.transform, false);
            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 40f);
            titleRect.anchoredPosition = new Vector2(20f, -40f);

            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = string.Empty;
            titleText.fontSize = 24f;
            titleText.color = new Color(0.82f, 0.88f, 1f, 0.95f);
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.raycastTarget = false;
            titleText.characterSpacing = 3;

            var slideTextObj = new GameObject("SlideText", typeof(RectTransform));
            slideTextObj.transform.SetParent(viewerObj.transform, false);
            var slideRect = slideTextObj.GetComponent<RectTransform>();
            slideRect.anchorMin = new Vector2(0.5f, 0.5f);
            slideRect.anchorMax = new Vector2(0.5f, 0.5f);
            slideRect.pivot = new Vector2(0.5f, 0.5f);
            slideRect.sizeDelta = new Vector2(900f, 300f);
            slideRect.anchoredPosition = Vector2.zero;

            var slideText = slideTextObj.AddComponent<TextMeshProUGUI>();
            slideText.text = string.Empty;
            slideText.fontSize = 44f;
            slideText.color = Color.white;
            slideText.alignment = TextAlignmentOptions.Center;
            slideText.enableWordWrapping = true;
            slideText.raycastTarget = false;

            // Glass-style close button
            var closeObj = new GameObject("CloseButton", typeof(RectTransform));
            closeObj.transform.SetParent(viewerObj.transform, false);
            var closeRect = closeObj.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(44f, 44f);
            closeRect.anchoredPosition = new Vector2(-20f, -60f);

            var glassSprite = K1L0HUD.RoundedRectSprite != null ? K1L0HUD.RoundedRectSprite : solidSprite;

            // Border/glow layer
            var closeBorderImg = closeObj.AddComponent<Image>();
            closeBorderImg.sprite = glassSprite;
            closeBorderImg.type = Image.Type.Sliced;
            closeBorderImg.color = new Color(0.15f, 0.30f, 0.45f, 0.2f);
            closeBorderImg.raycastTarget = true;

            // Inner fill
            var closeFillObj = new GameObject("Fill", typeof(RectTransform));
            closeFillObj.transform.SetParent(closeObj.transform, false);
            var closeFillRect = closeFillObj.GetComponent<RectTransform>();
            closeFillRect.anchorMin = Vector2.zero;
            closeFillRect.anchorMax = Vector2.one;
            closeFillRect.offsetMin = new Vector2(1.5f, 1.5f);
            closeFillRect.offsetMax = new Vector2(-1.5f, -1.5f);
            var closeFillImg = closeFillObj.AddComponent<Image>();
            closeFillImg.sprite = glassSprite;
            closeFillImg.type = Image.Type.Sliced;
            closeFillImg.color = new Color(0.05f, 0.08f, 0.14f, 0.5f);
            closeFillImg.raycastTarget = false;

            var closeButton = closeObj.AddComponent<Button>();
            closeButton.targetGraphic = closeBorderImg;
            closeButton.transition = Selectable.Transition.None;

            var closeLabelObj = new GameObject("Label", typeof(RectTransform));
            closeLabelObj.transform.SetParent(closeFillObj.transform, false);
            var closeLabelRect = closeLabelObj.GetComponent<RectTransform>();
            closeLabelRect.anchorMin = Vector2.zero;
            closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.offsetMin = Vector2.zero;
            closeLabelRect.offsetMax = Vector2.zero;
            var closeLabel = closeLabelObj.AddComponent<TextMeshProUGUI>();
            closeLabel.text = "X";
            closeLabel.fontSize = 24f;
            closeLabel.color = new Color(0.75f, 0.85f, 1f, 0.9f);
            closeLabel.alignment = TextAlignmentOptions.Center;
            closeLabel.raycastTarget = false;

            var viewer = viewerObj.AddComponent<StoryViewer>();
            viewer.Initialize(canvasGroup, viewerRect, background, imageView, videoView, videoPlayer, progressBar, titleText, slideText, closeButton, mediaLoader);

            CreateStoryRecorder(viewerObj.transform, viewer);

            return viewer;
        }

        private void CreateStoryRecorder(Transform parent, StoryViewer viewer)
        {
            var audioManager = FindFirstObjectByType<AudioDropManager>();
            if (audioManager == null)
            {
                audioManager = parent.gameObject.AddComponent<AudioDropManager>();
            }
            audioManager.storyViewer = viewer;

            var recordBtnObj = new GameObject("StoryRecordButton");
            recordBtnObj.transform.SetParent(parent, false);

            var recordBtnImg = recordBtnObj.AddComponent<Image>();
            recordBtnImg.color = new Color(0.1f, 0.6f, 0.9f, 0.9f);

            recordBtnObj.AddComponent<Button>();

            var recordRect = recordBtnObj.GetComponent<RectTransform>();
            recordRect.anchorMin = new Vector2(0.5f, 0.08f);
            recordRect.anchorMax = new Vector2(0.5f, 0.08f);
            recordRect.pivot = new Vector2(0.5f, 0.5f);
            recordRect.sizeDelta = new Vector2(180, 180);
            recordRect.anchoredPosition = Vector2.zero;

            var recTextObj = new GameObject("RecText");
            recTextObj.transform.SetParent(recordBtnObj.transform, false);
            var recText = recTextObj.AddComponent<Text>();
            recText.text = "HOLD\nTALK";
            recText.font = GetLegacyFont();
            recText.fontSize = 38;
            recText.color = Color.white;
            recText.alignment = TextAnchor.MiddleCenter;
            recText.resizeTextForBestFit = true;
            recText.resizeTextMinSize = 18;
            recText.resizeTextMaxSize = 54;

            var recTextRect = recTextObj.GetComponent<RectTransform>();
            recTextRect.anchorMin = Vector2.zero;
            recTextRect.anchorMax = Vector2.one;
            recTextRect.offsetMin = new Vector2(16, 16);
            recTextRect.offsetMax = new Vector2(-16, -16);

            var statusObj = new GameObject("RecordStatus");
            statusObj.transform.SetParent(parent, false);
            var statusText = statusObj.AddComponent<Text>();
            statusText.text = "Ready to record";
            statusText.font = GetLegacyFont();
            statusText.fontSize = 28;
            statusText.color = Color.white;
            statusText.alignment = TextAnchor.MiddleCenter;

            var statusRect = statusObj.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 0.08f);
            statusRect.anchorMax = new Vector2(0.5f, 0.08f);
            statusRect.pivot = new Vector2(0.5f, 0);
            statusRect.sizeDelta = new Vector2(360, 40);
            statusRect.anchoredPosition = new Vector2(0, 120);

            var spinnerObj = new GameObject("Spinner");
            spinnerObj.transform.SetParent(parent, false);
            var spinnerText = spinnerObj.AddComponent<Text>();
            spinnerText.text = "↻";
            spinnerText.font = GetLegacyFont();
            spinnerText.fontSize = 70;
            spinnerText.color = new Color(0.2f, 0.6f, 1f);
            spinnerText.alignment = TextAnchor.MiddleCenter;
            spinnerObj.SetActive(false);

            var spinnerRect = spinnerObj.GetComponent<RectTransform>();
            spinnerRect.anchorMin = new Vector2(0.5f, 0.08f);
            spinnerRect.anchorMax = new Vector2(0.5f, 0.08f);
            spinnerRect.pivot = new Vector2(0.5f, 0.5f);
            spinnerRect.sizeDelta = new Vector2(80, 80);
            spinnerRect.anchoredPosition = new Vector2(150, 120);

            audioManager.recordButtonImage = recordBtnImg;
            audioManager.statusText = statusText;
            audioManager.loadingSpinner = spinnerObj;
            audioManager.recordingColor = new Color(0.1f, 0.9f, 0.5f);
            audioManager.normalColor = recordBtnImg.color;

            var trigger = recordBtnObj.AddComponent<EventTrigger>();
            var entryDown = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerDown
            };
            entryDown.callback.AddListener(_ => { audioManager.StartRecording(); });
            trigger.triggers.Add(entryDown);

            var entryUp = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerUp
            };
            entryUp.callback.AddListener(_ => { audioManager.StopAndUpload(); });
            trigger.triggers.Add(entryUp);
        }

        private Font GetLegacyFont()
        {
            Font font = null;
            try
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (System.ArgumentException)
            {
                // Arial.ttf is no longer valid in Unity 6+
            }

            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return font;
        }

        private StoryProgressSegment CreateProgressSegmentTemplate(Transform parent)
        {
            var segmentObj = new GameObject("ProgressSegmentTemplate", typeof(RectTransform));
            segmentObj.transform.SetParent(parent, false);
            segmentObj.SetActive(false);

            var segmentRect = segmentObj.GetComponent<RectTransform>();
            segmentRect.sizeDelta = new Vector2(40f, progressHeight);

            var background = segmentObj.AddComponent<Image>();
            background.sprite = solidSprite;
            background.color = progressBackgroundColor;
            background.raycastTarget = false;

            var fillObj = new GameObject("Fill", typeof(RectTransform));
            fillObj.transform.SetParent(segmentObj.transform, false);
            var fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillObj.AddComponent<Image>();
            fillImage.sprite = solidSprite;
            fillImage.color = progressFillColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;
            fillImage.raycastTarget = false;

            var segment = segmentObj.AddComponent<StoryProgressSegment>();
            segment.Initialize(fillImage, background);

            return segment;
        }

        private float GetSafeAreaTopInset(Canvas canvas)
        {
            if (canvas == null) return 0f;
            float topInsetPixels = Screen.height - Screen.safeArea.yMax;
            if (topInsetPixels < 0f) topInsetPixels = 0f;

            // Match UILayoutManager's CanvasScaler-aware calculation
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            float scaleFactor = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                float refW = scaler.referenceResolution.x;
                float refH = scaler.referenceResolution.y;
                float match = scaler.matchWidthOrHeight;
                float logWidth = Mathf.Log(Screen.width / refW, 2);
                float logHeight = Mathf.Log(Screen.height / refH, 2);
                float logBlend = Mathf.Lerp(logWidth, logHeight, match);
                scaleFactor = Mathf.Pow(2, logBlend);
            }
            return topInsetPixels / scaleFactor;
        }

        private Sprite CreateCircleSprite(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float radius = size * 0.5f;
            float radiusSq = radius * radius;
            float center = radius - 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distSq = dx * dx + dy * dy;
                    float alpha = distSq <= radiusSq ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite CreateSolidSprite()
        {
            const int size = 2;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }
            return component;
        }
    }
}
