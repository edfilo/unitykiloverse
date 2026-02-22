using System.Collections.Generic;
using UnityEngine;

namespace KiloWorld.UI.Stories
{
    public static class StoryFeedService
    {
        public static void ApplyFromPingJson(string json, bool logErrors = false)
        {
            if (!StoriesJsonDataSource.TryParseStories(json, out List<StoryGroup> stories, logErrors))
            {
                return;
            }

            var strip = Object.FindFirstObjectByType<StoriesStrip>();
            if (strip == null)
            {
                if (logErrors)
                {
                    Debug.LogWarning("[StoryFeedService] StoriesStrip not found.");
                }
                return;
            }

            strip.SetStories(stories);
        }
    }
}
