using System.Collections.Generic;
using UnityEngine;

namespace KiloWorld.UI.Stories
{
    public class StoryProgressBar : MonoBehaviour
    {
        [SerializeField] private RectTransform container;
        [SerializeField] private StoryProgressSegment segmentPrefab;
        [SerializeField] private Color fillColor = Color.white;
        [SerializeField] private Color backgroundColor = new Color(1f, 1f, 1f, 0.35f);

        private readonly List<StoryProgressSegment> segments = new List<StoryProgressSegment>();

        public void Initialize(RectTransform newContainer, StoryProgressSegment newSegmentPrefab)
        {
            container = newContainer;
            segmentPrefab = newSegmentPrefab;
        }

        public void SetColors(Color background, Color fill)
        {
            backgroundColor = background;
            fillColor = fill;
            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].SetColors(backgroundColor, fillColor);
            }
        }

        public void Build(int count)
        {
            if (container == null || segmentPrefab == null || count <= 0) return;

            if (segments.Count != count)
            {
                ClearSegments();
                for (int i = 0; i < count; i++)
                {
                    var segment = Instantiate(segmentPrefab, container);
                    segment.gameObject.SetActive(true);
                    segment.SetColors(backgroundColor, fillColor);
                    segment.SetProgress(0f);
                    segments.Add(segment);
                }
            }
        }

        public void SetProgress(int activeIndex, float progress)
        {
            if (segments.Count == 0) return;

            for (int i = 0; i < segments.Count; i++)
            {
                if (i < activeIndex)
                {
                    segments[i].SetProgress(1f);
                }
                else if (i == activeIndex)
                {
                    segments[i].SetProgress(progress);
                }
                else
                {
                    segments[i].SetProgress(0f);
                }
            }
        }

        public void ResetAll()
        {
            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].SetProgress(0f);
            }
        }

        private void ClearSegments()
        {
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] == null) continue;
                if (Application.isPlaying)
                {
                    Destroy(segments[i].gameObject);
                }
                else
                {
                    DestroyImmediate(segments[i].gameObject);
                }
            }
            segments.Clear();
        }
    }
}
