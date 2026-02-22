using UnityEngine;

namespace Kiloverse.Mapbox
{
    /// <summary>
    /// Stores Overture road segment class for runtime filtering.
    /// Attached to road GameObjects during rendering.
    /// </summary>
    public class RoadSegmentMetadata : MonoBehaviour
    {
        public string roadClass;
        public string roadSubclass;
    }
}
