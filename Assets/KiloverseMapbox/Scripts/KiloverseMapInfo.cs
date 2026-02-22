using UnityEngine;

namespace Kiloverse.Mapbox
{
    // Alias Mapbox types INSIDE namespace to avoid conflicts
    using IMapInformation = global::Mapbox.BaseModule.Map.IMapInformation;
    using MapboxVector2d = global::Mapbox.BaseModule.Data.Vector2d.Vector2d;
    using MapboxLatLng = global::Mapbox.BaseModule.Data.Vector2d.LatitudeLongitude;
    using MapboxConversions = global::Mapbox.BaseModule.Utilities.Conversions;
    using CanonicalTileId = global::Mapbox.BaseModule.Data.Tiles.CanonicalTileId;

    /// <summary>
    /// Minimal map information class replacing MapboxMapBehaviour.
    /// Uses Kiloverse coordinate types internally, converts at SDK boundary.
    /// Independent of Mapbox SDK - only uses tile data from api.kilomeme.com
    /// </summary>
    public class KiloverseMapInfo : MonoBehaviour
    {
        [Header("GPS Position")]
        [SerializeField] private double latitude = 0.0;  // Uninitialized - wait for GPS
        [SerializeField] private double longitude = 0.0; // Uninitialized - wait for GPS
        [SerializeField] private int zoomLevel = 16;

        [Header("Fallback Location (if GPS unavailable)")]
        [SerializeField] private double fallbackLatitude = 40.4417;  // Pittsburgh
        [SerializeField] private double fallbackLongitude = -80.0132; // Pittsburgh

        private KiloverseMapInformation _mapInformation;
        private bool _hasGPSPosition = false;

        void Awake()
        {
            _mapInformation = new KiloverseMapInformation(this);
            Debug.Log("[KiloverseMapInfo] Initialized with (0,0) - waiting for GPS or fallback trigger");
        }

        public KiloverseMapInformation MapInformation => _mapInformation;
        public bool HasGPSPosition => _hasGPSPosition;

        // Current GPS center
        public LatitudeLongitude Center
        {
            get => new LatitudeLongitude(latitude, longitude);
            set
            {
                latitude = value.Latitude;
                longitude = value.Longitude;
                _mapInformation?.UpdatePosition(value);
            }
        }

        public int Zoom
        {
            get => zoomLevel;
            set => zoomLevel = Mathf.Clamp(value, 0, 20);
        }

        /// <summary>
        /// Set GPS position from external systems (GPS, teleport, etc.)
        /// </summary>
        public void SetPosition(double lat, double lon)
        {
            latitude = lat;
            longitude = lon;
            _hasGPSPosition = true;
            _mapInformation?.UpdatePosition(new LatitudeLongitude(lat, lon));
        }

        /// <summary>
        /// Use fallback location if GPS unavailable (called after GPS timeout)
        /// </summary>
        public void UseFallbackLocation()
        {
            if (!_hasGPSPosition)
            {
                Debug.Log($"[KiloverseMapInfo] GPS unavailable - using fallback location: ({fallbackLatitude:F6}, {fallbackLongitude:F6})");
                SetPosition(fallbackLatitude, fallbackLongitude);
            }
        }

        public void SetZoom(int zoom)
        {
            zoomLevel = Mathf.Clamp(zoom, 0, 20);
        }

        // Compatibility properties for legacy code that checks MapboxMap initialization
        public KiloverseMapInfo MapboxMap => this; // Return self for property chaining
        public KiloverseMapInfo MapService => this; // Return self for property chaining
        public object FileSource => null; // Dummy - not needed for Kiloverse tiles
        public bool Initialized => true; // Always initialized (no async loading needed)
        public bool InitializeOnStart { get; set; } = true; // Dummy property
        public object InitializationStatus => 3; // Dummy value for compatibility
        public void Initialize(LatitudeLongitude latLng, int zoom) { SetPosition(latLng.Latitude, latLng.Longitude); SetZoom(zoom); }
        public void Initialize() { } // Parameterless overload for compatibility
        public void LoadMapView() { } // Dummy method for compatibility
    }

    /// <summary>
    /// Map information container passed to mesh/GameObject modifiers.
    /// Replaces Mapbox's AbstractMapVisualizer.MapInformation
    /// </summary>
    public class KiloverseMapInformation : IMapInformation
    {
        private readonly KiloverseMapInfo _map;

        public KiloverseMapInformation(KiloverseMapInfo map)
        {
            _map = map;
        }

        public LatitudeLongitude Position => _map.Center;

        public void UpdatePosition(LatitudeLongitude newPosition)
        {
            // Called when GPS position changes
            // Visualizers can react to this if needed
        }

        // Public properties for common access (avoids need to cast to IMapInformation)
        public MapboxLatLng LatitudeLongitude => new MapboxLatLng(_map.Center.Latitude, _map.Center.Longitude);
        public float Zoom => _map.Zoom;
        public MapboxVector2d CenterMercator
        {
            get
            {
                var ourMercator = Conversions.LatitudeLongitudeToWebMercator(Position);
                return new MapboxVector2d(ourMercator.x, ourMercator.y);
            }
        }
        public float Scale => Conversions.GetTileScaleInMeters((float)_map.Center.Latitude, _map.Zoom);
        public float WorldRelativeScale => Scale; // Alias for Scale
        public void SetInformation(MapboxLatLng? latLng, float? pitch, float? bearing, float? zoom, float? scale)
        {
            if (latLng.HasValue) _map.SetPosition(latLng.Value.Latitude, latLng.Value.Longitude);
            if (zoom.HasValue) _map.SetZoom((int)zoom.Value);
        }
        // Overload for backward compatibility (old 2-parameter signature)
        public void SetInformation(MapboxLatLng latLng, float zoom)
        {
            _map.SetPosition(latLng.Latitude, latLng.Longitude);
            _map.SetZoom((int)zoom);
        }

        // IMapInformation implementation
        MapboxLatLng IMapInformation.LatitudeLongitude => new MapboxLatLng(_map.Center.Latitude, _map.Center.Longitude);
        float IMapInformation.Pitch => 0f;
        float IMapInformation.Bearing => 0f;
        float IMapInformation.Scale => Conversions.GetTileScaleInMeters((float)_map.Center.Latitude, _map.Zoom);
        float IMapInformation.Zoom => _map.Zoom;
        int IMapInformation.AbsoluteZoom => _map.Zoom;
        MapboxVector2d IMapInformation.CenterMercator
        {
            get
            {
                var ourMercator = Conversions.LatitudeLongitudeToWebMercator(Position);
                return new MapboxVector2d(ourMercator.x, ourMercator.y);
            }
        }
        float IMapInformation.GetLatitudeCompensationForLocation => (float)System.Math.Cos(_map.Center.Latitude * Mathf.Deg2Rad);

        void IMapInformation.Initialize() { }
        void IMapInformation.Initialize(MapboxLatLng latLng) { _map.SetPosition(latLng.Latitude, latLng.Longitude); }
        void IMapInformation.SetLatitudeLongitude(MapboxLatLng latLng) { _map.SetPosition(latLng.Latitude, latLng.Longitude); }
        void IMapInformation.SetInformation(MapboxLatLng? latLng, float? pitch, float? bearing, float? zoom, float? scale)
        {
            if (latLng.HasValue) _map.SetPosition(latLng.Value.Latitude, latLng.Value.Longitude);
            if (zoom.HasValue) _map.SetZoom((int)zoom.Value);
        }
        float IMapInformation.GetScaleFor(float zoom) => Conversions.GetTileScaleInMeters((float)_map.Center.Latitude, (int)zoom);

        // Additional interface members (events and properties)
        event System.Action<IMapInformation> IMapInformation.SetView { add { } remove { } }
        event System.Action<IMapInformation> IMapInformation.ViewChanged { add { } remove { } }
        event System.Action<IMapInformation> IMapInformation.LatitudeLongitudeChanged { add { } remove { } }
        event System.Action<IMapInformation> IMapInformation.WorldScaleChanged { add { } remove { } }

        System.Func<CanonicalTileId, float, float, float> IMapInformation.QueryElevation { get; set; }
    }
}
