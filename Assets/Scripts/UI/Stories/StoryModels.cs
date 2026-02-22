using System;
using System.Collections.Generic;
using UnityEngine;

namespace KiloWorld.UI.Stories
{
    public enum StoryMediaType
    {
        Image,
        Video
    }

    [Serializable]
    public class StorySlide
    {
        public string id;
        public StoryMediaType mediaType = StoryMediaType.Image;
        public Sprite imageSprite;
        public string mediaUrl;
        public string audioUrl;
        public float durationSeconds = 5f;
        public string text;
        public Color backgroundColor = Color.clear;
        public bool hasBackgroundColor;
    }

    [Serializable]
    public class StoryGroup
    {
        public string id;
        public string title;
        public Sprite thumbnailSprite;
        public string thumbnailUrl;
        public List<StorySlide> slides = new List<StorySlide>();
        public int lastSeenIndex = -1;
        public Color thumbnailColor = Color.clear;
        public bool hasThumbnailColor;
        public long createdAt;

        public bool IsSeen
        {
            get
            {
                if (slides == null || slides.Count == 0) return false;
                return lastSeenIndex >= slides.Count - 1;
            }
        }
    }
}
