using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Video;

namespace KiloWorld.UI.Stories
{
    public class StoryViewer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [Header("UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image imageView;
        [SerializeField] private RawImage videoView;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private StoryProgressBar progressBar;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text slideText;
        [SerializeField] private Button closeButton;
        [SerializeField] private AudioSource audioSource;

        [Header("Loading")]
        [SerializeField] private StoryMediaLoader mediaLoader;

        [Header("Playback")]
        [SerializeField] private float defaultImageDuration = 5f;
        [SerializeField] private float minimumSlideDuration = 5f;
        [SerializeField] private float tapMaxDuration = 0.25f;
        [SerializeField] private float tapMaxDistance = 20f;
        [SerializeField] private float dismissDragThreshold = 120f;
        [SerializeField] private bool pauseOnHold = true;
        [SerializeField] private float progressTopPadding = 12f;
        [SerializeField] private float titleTopPadding = 40f;
        [SerializeField] private float closeTopPadding = 12f;
        [SerializeField] private Color fallbackVideoBackground = new Color(0.1f, 0.4f, 0.85f, 1f);
        [SerializeField] private float videoPrepareFallbackDuration = 4f;

        public event Action<StoryGroup, int> SlideChanged;
        public event Action<StoryGroup> StoryCompleted;
        public event Action<StoryGroup> StoryClosed;

        private StoryGroup currentStory;
        private int currentIndex = -1;
        private Coroutine progressRoutine;
        private Coroutine audioRoutine;
        private bool isPaused;
        private bool isVisible;
        private Vector2 pointerDownPos;
        private float pointerDownTime;
        private string pendingVideoUrl;
        private int slideToken;
        private bool videoCallbacksBound;
        private Sprite fallbackSprite;
        private Texture2D fallbackTexture;
        private Color defaultBackgroundColor = new Color(0f, 0f, 0f, 0.9f);
        private float prepareElapsed;
        private float prepareDuration;

        public StoryGroup CurrentStory => currentStory;

        public void Initialize(
            CanvasGroup newCanvasGroup,
            RectTransform newRootRect,
            Image newBackgroundImage,
            Image newImageView,
            RawImage newVideoView,
            VideoPlayer newVideoPlayer,
            StoryProgressBar newProgressBar,
            TMP_Text newTitleText,
            TMP_Text newSlideText,
            Button newCloseButton,
            StoryMediaLoader newMediaLoader)
        {
            if (videoPlayer != null && videoCallbacksBound)
            {
                videoPlayer.prepareCompleted -= HandleVideoPrepared;
                videoPlayer.loopPointReached -= HandleVideoFinished;
                videoPlayer.errorReceived -= HandleVideoError;
                videoCallbacksBound = false;
            }

            canvasGroup = newCanvasGroup;
            rootRect = newRootRect;
            backgroundImage = newBackgroundImage;
            imageView = newImageView;
            videoView = newVideoView;
            videoPlayer = newVideoPlayer;
            progressBar = newProgressBar;
            titleText = newTitleText;
            slideText = newSlideText;
            closeButton = newCloseButton;
            mediaLoader = newMediaLoader;

            CacheBackgroundColor();
            HookCloseButton();
            BindVideoCallbacks();
            EnsureAudioSource();
        }

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (rootRect == null)
            {
                rootRect = transform as RectTransform;
            }

            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            CacheBackgroundColor();
            HookCloseButton();
            BindVideoCallbacks();
            EnsureAudioSource();

            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= HandleVideoPrepared;
                videoPlayer.loopPointReached -= HandleVideoFinished;
                videoPlayer.errorReceived -= HandleVideoError;
            }
        }

        private void BindVideoCallbacks()
        {
            if (videoPlayer == null || videoCallbacksBound) return;
            videoPlayer.prepareCompleted += HandleVideoPrepared;
            videoPlayer.loopPointReached += HandleVideoFinished;
            videoPlayer.errorReceived += HandleVideoError;
            videoCallbacksBound = true;
        }

        private void LateUpdate()
        {
            TryShowVideoTexture();
        }

        public void Show(StoryGroup story, int startIndex = 0)
        {
            if (story == null || story.slides == null || story.slides.Count == 0) return;

            currentStory = story;
            currentIndex = -1;
            isPaused = false;
            slideToken++;

            if (titleText != null)
            {
                titleText.text = story.title ?? string.Empty;
            }

            if (progressBar != null)
            {
                progressBar.Build(story.slides.Count);
                progressBar.ResetAll();
            }

            ApplySafeAreaOffsets();
            SetVisible(true);
            DisplaySlide(Mathf.Clamp(startIndex, 0, story.slides.Count - 1));
        }

        public void Hide()
        {
            StopPlayback();
            SetVisible(false);
            ApplySlideText(null);

            if (currentStory != null)
            {
                StoryClosed?.Invoke(currentStory);
            }

            currentStory = null;
            currentIndex = -1;
        }

        public void Next()
        {
            if (currentStory == null) return;
            DisplaySlide(currentIndex + 1);
        }

        public void Previous()
        {
            if (currentStory == null) return;

            if (currentIndex <= 0)
            {
                Hide();
                return;
            }

            DisplaySlide(currentIndex - 1);
        }

        public void Pause()
        {
            isPaused = true;
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
            }
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Pause();
            }
        }

        public void Resume()
        {
            isPaused = false;
            if (videoPlayer != null && videoPlayer.isPrepared && !videoPlayer.isPlaying && isVisible)
            {
                videoPlayer.Play();
            }
            if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying && isVisible)
            {
                audioSource.Play();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!isVisible) return;

            pointerDownPos = eventData.position;
            pointerDownTime = Time.unscaledTime;

            if (pauseOnHold)
            {
                Pause();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isVisible) return;

            if (pauseOnHold)
            {
                Resume();
            }

            float duration = Time.unscaledTime - pointerDownTime;
            float distance = Vector2.Distance(pointerDownPos, eventData.position);

            if (duration <= tapMaxDuration && distance <= tapMaxDistance)
            {
                HandleTap(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isVisible) return;

            float deltaY = eventData.position.y - pointerDownPos.y;
            if (deltaY < -dismissDragThreshold)
            {
                Hide();
            }
        }

        private void HandleTap(PointerEventData eventData)
        {
            if (rootRect == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rootRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPos))
            {
                return;
            }

            float normalizedX = (localPos.x / rootRect.rect.width) + 0.5f;
            if (normalizedX < 0.33f)
            {
                Previous();
            }
            else if (normalizedX > 0.66f)
            {
                Next();
            }
            else
            {
                if (isPaused)
                {
                    Resume();
                }
                else
                {
                    Pause();
                }
            }
        }

        private void DisplaySlide(int index)
        {
            if (currentStory == null || currentStory.slides == null) return;

            if (index >= currentStory.slides.Count)
            {
                CompleteStory();
                return;
            }

            if (index < 0)
            {
                index = 0;
            }

            StopPlayback();
            currentIndex = index;
            slideToken++;

            currentStory.lastSeenIndex = Mathf.Max(currentStory.lastSeenIndex, currentIndex);
            SlideChanged?.Invoke(currentStory, currentIndex);

            var slide = currentStory.slides[currentIndex];
            ApplySlideText(slide);
            if (progressBar != null)
            {
                progressBar.SetProgress(currentIndex, 0f);
            }

            if (slide.mediaType == StoryMediaType.Video)
            {
                ShowVideo(slide, slideToken);
            }
            else
            {
                ShowImage(slide, slideToken);
            }

            PlaySlideAudio(slide, slideToken);
        }

        private void ShowImage(StorySlide slide, int token)
        {
            pendingVideoUrl = null;
            ShowFallback(GetSlideBackgroundColor(slide));

            if (slide.imageSprite != null && imageView != null)
            {
                ApplyImageSprite(slide.imageSprite);
            }
            else if (!string.IsNullOrWhiteSpace(slide.mediaUrl) && mediaLoader != null)
            {
                mediaLoader.LoadSprite(slide.mediaUrl, sprite =>
                {
                    if (token != slideToken || imageView == null) return;
                    ApplyImageSprite(sprite);
                });
            }

            float duration = GetSlideDuration(slide);
            progressRoutine = StartCoroutine(RunSlideTimer(duration, token));
        }

        private void ShowVideo(StorySlide slide, int token)
        {
            if (videoPlayer == null || string.IsNullOrWhiteSpace(slide.mediaUrl))
            {
                ShowFallback(fallbackVideoBackground);
                float duration = GetSlideDuration(slide);
                progressRoutine = StartCoroutine(RunSlideTimer(duration, token));
                return;
            }

            pendingVideoUrl = slide.mediaUrl;
            ShowFallback(GetSlideBackgroundColor(slide));

            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = slide.mediaUrl;
            videoPlayer.isLooping = true;
            videoPlayer.Prepare();

            prepareElapsed = 0f;
            prepareDuration = Mathf.Max(0.01f, GetSlideDuration(slide));
            if (videoPrepareFallbackDuration > 0f)
            {
                prepareDuration = Mathf.Max(prepareDuration, videoPrepareFallbackDuration);
            }
            progressRoutine = StartCoroutine(RunVideoPrepare(token));
        }

        private IEnumerator RunSlideTimer(float duration, int token)
        {
            float elapsed = 0f;

            while (elapsed < duration && token == slideToken)
            {
                if (!isPaused)
                {
                    elapsed += Time.unscaledDeltaTime;
                    if (progressBar != null)
                    {
                        progressBar.SetProgress(currentIndex, Mathf.Clamp01(elapsed / duration));
                    }
                }
                yield return null;
            }

            if (token == slideToken && !isPaused)
            {
                Next();
            }
        }

        private IEnumerator RunVideoProgress(float duration, int token)
        {
            float elapsed = 0f;
            float safeDuration = Mathf.Max(minimumSlideDuration, duration > 0.01f ? duration : defaultImageDuration);

            while (token == slideToken && videoPlayer != null && videoPlayer.isPrepared)
            {
                if (!isPaused)
                {
                    elapsed += Time.unscaledDeltaTime;
                    if (safeDuration > 0.01f)
                    {
                        if (progressBar != null)
                        {
                            progressBar.SetProgress(currentIndex, Mathf.Clamp01(elapsed / safeDuration));
                        }
                    }

                    if (safeDuration > 0.01f && elapsed >= safeDuration)
                    {
                        Next();
                        yield break;
                    }
                }

                yield return null;
            }
        }

        private IEnumerator RunVideoPrepare(int token)
        {
            while (token == slideToken && videoPlayer != null && !videoPlayer.isPrepared && prepareElapsed < prepareDuration)
            {
                if (!isPaused)
                {
                    prepareElapsed += Time.unscaledDeltaTime;
                    if (progressBar != null)
                    {
                        progressBar.SetProgress(currentIndex, Mathf.Clamp01(prepareElapsed / prepareDuration));
                    }
                }
                yield return null;
            }

            if (token != slideToken) yield break;

            if (videoPlayer != null && videoPlayer.isPrepared)
            {
                yield break;
            }

            pendingVideoUrl = null;
            ShowFallback(fallbackVideoBackground);

            if (!isPaused)
            {
                Next();
            }
        }

        private void HandleVideoPrepared(VideoPlayer source)
        {
            if (!isVisible || source.url != pendingVideoUrl) return;

            if (progressRoutine != null)
            {
                StopCoroutine(progressRoutine);
                progressRoutine = null;
            }

            if (!isPaused)
            {
                source.Play();
            }

            TryShowVideoTexture(source);
            SetBackgroundColor(defaultBackgroundColor);

            float duration = source.length > 0.01 ? Mathf.Max(minimumSlideDuration, (float)source.length) : minimumSlideDuration;
            progressRoutine = StartCoroutine(RunVideoProgress(duration, slideToken));
        }

        private void HandleVideoFinished(VideoPlayer source)
        {
            if (!isVisible || source.url != pendingVideoUrl) return;
        }

        private void HandleVideoError(VideoPlayer source, string message)
        {
            if (!isVisible || source.url != pendingVideoUrl) return;
            pendingVideoUrl = null;

            if (progressRoutine != null)
            {
                StopCoroutine(progressRoutine);
                progressRoutine = null;
            }

            ShowFallback(fallbackVideoBackground);
            float duration = GetCurrentSlideDuration();
            progressRoutine = StartCoroutine(RunSlideTimer(duration, slideToken));
        }

        private void CompleteStory()
        {
            if (currentStory != null && currentStory.slides != null)
            {
                currentStory.lastSeenIndex = currentStory.slides.Count - 1;
            }

            if (currentStory != null)
            {
                StoryCompleted?.Invoke(currentStory);
            }

            Hide();
        }

        private float GetSlideDuration(StorySlide slide)
        {
            float duration = slide != null && slide.durationSeconds > 0f ? slide.durationSeconds : defaultImageDuration;
            return Mathf.Max(minimumSlideDuration, duration);
        }

        private void StopPlayback()
        {
            if (progressRoutine != null)
            {
                StopCoroutine(progressRoutine);
                progressRoutine = null;
            }

            if (audioRoutine != null)
            {
                StopCoroutine(audioRoutine);
                audioRoutine = null;
            }

            if (videoPlayer != null)
            {
                if (videoPlayer.isPlaying)
                {
                    videoPlayer.Stop();
                }
                videoPlayer.time = 0;
            }

            if (videoView != null)
            {
                videoView.texture = null;
                videoView.enabled = false;
            }

            if (audioSource != null)
            {
                if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                }
                audioSource.clip = null;
            }

            pendingVideoUrl = null;
        }

        private void EnsureAudioSource()
        {
            if (audioSource != null) return;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
        }

        private void PlaySlideAudio(StorySlide slide, int token)
        {
            if (slide == null || audioSource == null) return;
            if (string.IsNullOrWhiteSpace(slide.audioUrl)) return;

            if (audioRoutine != null)
            {
                StopCoroutine(audioRoutine);
                audioRoutine = null;
            }
            audioRoutine = StartCoroutine(LoadAndPlayAudio(slide.audioUrl, token));
        }

        private IEnumerator LoadAndPlayAudio(string url, int token)
        {
            if (string.IsNullOrWhiteSpace(url)) yield break;

            var audioType = GuessAudioType(url);
            using (var request = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                yield return request.SendWebRequest();

                if (token != slideToken) yield break;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[StoryViewer] Audio load failed: {request.error}");
                    yield break;
                }

                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null)
                {
                    Debug.LogWarning("[StoryViewer] Audio clip missing.");
                    yield break;
                }

                audioSource.clip = clip;
                audioSource.Play();
            }
        }

        private AudioType GuessAudioType(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return AudioType.UNKNOWN;
            var lower = url.ToLowerInvariant();
            if (lower.Contains(".wav")) return AudioType.WAV;
            if (lower.Contains(".ogg")) return AudioType.OGGVORBIS;
            if (lower.Contains(".mp3") || lower.Contains(".m4a")) return AudioType.MPEG;
            return AudioType.UNKNOWN;
        }

        private void SetVisible(bool visible)
        {
            isVisible = visible;
            if (canvasGroup == null) return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void HookCloseButton()
        {
            if (closeButton == null) return;
            closeButton.onClick.RemoveListener(HandleCloseClicked);
            closeButton.onClick.AddListener(HandleCloseClicked);
        }

        private void HandleCloseClicked()
        {
            Hide();
        }

        private void CacheBackgroundColor()
        {
            if (backgroundImage != null)
            {
                defaultBackgroundColor = backgroundImage.color;
            }
        }

        private void SetBackgroundColor(Color color)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = color;
            }
        }

        private void EnsureFallbackAssets()
        {
            if (fallbackTexture != null && fallbackSprite != null) return;
            fallbackTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            fallbackTexture.wrapMode = TextureWrapMode.Clamp;
            fallbackTexture.filterMode = FilterMode.Point;
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    fallbackTexture.SetPixel(x, y, Color.white);
                }
            }
            fallbackTexture.Apply();
            fallbackSprite = Sprite.Create(fallbackTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        }

        private void ApplyImageSprite(Sprite sprite)
        {
            if (imageView == null || sprite == null) return;
            imageView.sprite = sprite;
            imageView.color = Color.white;
            imageView.preserveAspect = true;
            ApplyAspectFit(imageView.rectTransform, sprite.rect.width, sprite.rect.height);
        }

        private void ApplySlideText(StorySlide slide)
        {
            if (slideText == null) return;

            if (slide != null && !string.IsNullOrWhiteSpace(slide.text))
            {
                slideText.text = slide.text;
                slideText.enabled = true;
            }
            else
            {
                slideText.text = string.Empty;
                slideText.enabled = false;
            }
        }

        private void ShowFallback(Color color)
        {
            EnsureFallbackAssets();
            if (imageView != null)
            {
                imageView.enabled = true;
                imageView.sprite = fallbackSprite;
                imageView.color = color;
                imageView.preserveAspect = false;
                StretchToFill(imageView.rectTransform);
            }
            if (videoView != null)
            {
                videoView.enabled = false;
                videoView.texture = null;
                StretchToFill(videoView.rectTransform);
            }
            SetBackgroundColor(color);
        }

        private Color GetSlideBackgroundColor(StorySlide slide)
        {
            if (slide != null && slide.hasBackgroundColor)
            {
                return slide.backgroundColor;
            }
            return defaultBackgroundColor;
        }

        private void TryShowVideoTexture(VideoPlayer source = null)
        {
            if (videoView == null || videoPlayer == null) return;
            if (string.IsNullOrEmpty(pendingVideoUrl)) return;
            if (videoPlayer.url != pendingVideoUrl) return;
            if (!videoPlayer.isPlaying && videoPlayer.time <= 0.01) return;

            Texture texture = source != null ? source.texture : videoPlayer.texture;
            if (texture == null) return;

            if (videoView.texture != texture)
            {
                videoView.texture = texture;
            }
            ApplyAspectFit(videoView.rectTransform, texture.width, texture.height);

            if (!videoView.enabled)
            {
                videoView.enabled = true;
                videoView.color = Color.white;
            }

            if (imageView != null)
            {
                imageView.enabled = false;
            }
        }

        private void ApplyAspectFit(RectTransform target, float mediaWidth, float mediaHeight)
        {
            if (target == null || mediaWidth <= 0f || mediaHeight <= 0f) return;

            RectTransform boundsRect = target.parent as RectTransform;
            if (boundsRect == null) boundsRect = rootRect;
            if (boundsRect == null) return;

            Rect bounds = boundsRect.rect;
            if (bounds.width <= 0f || bounds.height <= 0f) return;

            float mediaAspect = mediaWidth / mediaHeight;
            float boundsAspect = bounds.width / bounds.height;
            float width;
            float height;
            if (mediaAspect > boundsAspect)
            {
                width = bounds.width;
                height = width / mediaAspect;
            }
            else
            {
                height = bounds.height;
                width = height * mediaAspect;
            }

            target.anchorMin = new Vector2(0.5f, 0.5f);
            target.anchorMax = new Vector2(0.5f, 0.5f);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = Vector2.zero;
            target.sizeDelta = new Vector2(width, height);
        }

        private void StretchToFill(RectTransform target)
        {
            if (target == null) return;
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.pivot = new Vector2(0.5f, 0.5f);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        private float GetCurrentSlideDuration()
        {
            if (currentStory == null || currentStory.slides == null) return defaultImageDuration;
            if (currentIndex < 0 || currentIndex >= currentStory.slides.Count) return defaultImageDuration;
            return GetSlideDuration(currentStory.slides[currentIndex]);
        }

        private void ApplySafeAreaOffsets()
        {
            if (rootRect == null) return;

            Canvas canvas = rootRect.GetComponentInParent<Canvas>();
            float scaleFactor = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            float topInsetPixels = Screen.height - Screen.safeArea.yMax;
            if (topInsetPixels < 0f) topInsetPixels = 0f;
            float topInset = topInsetPixels / scaleFactor;

            if (progressBar != null)
            {
                RectTransform progressRect = progressBar.transform as RectTransform;
                if (progressRect != null)
                {
                    progressRect.anchoredPosition = new Vector2(0f, -(topInset + progressTopPadding));
                }
            }

            if (titleText != null)
            {
                RectTransform titleRect = titleText.rectTransform;
                if (titleRect != null)
                {
                    titleRect.anchoredPosition = new Vector2(titleRect.anchoredPosition.x, -(topInset + titleTopPadding));
                }
            }

            if (closeButton != null)
            {
                RectTransform closeRect = closeButton.transform as RectTransform;
                if (closeRect != null)
                {
                    float belowProgress = progressTopPadding + closeTopPadding;
                    if (progressBar != null)
                    {
                        RectTransform progressRect = progressBar.transform as RectTransform;
                        if (progressRect != null)
                        {
                            belowProgress = progressTopPadding + progressRect.sizeDelta.y + closeTopPadding;
                        }
                    }
                    closeRect.anchoredPosition = new Vector2(closeRect.anchoredPosition.x, -(topInset + belowProgress));
                }
            }
        }
    }
}
