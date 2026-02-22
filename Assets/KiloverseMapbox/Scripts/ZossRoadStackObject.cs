using UnityEngine;

namespace Kiloverse.Mapbox
{
    /// <summary>
    /// ScriptableObject wrapper for ZossRoadStack mesh modifier.
    /// Allows configuration in Unity Inspector and asset-based setup.
    /// </summary>
    [CreateAssetMenu(menuName = "Kiloverse/Modifiers/Zoss Road Stack")]
    public class ZossRoadStackObject : ScriptableObject
    {
        [SerializeField] private bool m_Active = true;

        private ZossRoadStack _modifier;

        public bool Active
        {
            get => m_Active;
            set => m_Active = value;
        }

        public ZossRoadStack GetModifier()
        {
            if (_modifier == null)
            {
                _modifier = new ZossRoadStack();
            }
            return _modifier;
        }
    }
}
