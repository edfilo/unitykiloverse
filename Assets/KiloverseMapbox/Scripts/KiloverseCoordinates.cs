using UnityEngine;
using System;

namespace Kiloverse.Mapbox
{
    /// <summary>
    /// GPS coordinate (latitude/longitude in degrees)
    /// </summary>
    [Serializable]
    public struct LatitudeLongitude
    {
        public double Latitude;
        public double Longitude;

        public LatitudeLongitude(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        public override string ToString() => $"({Latitude:F6}, {Longitude:F6})";

        // Mapbox SDK interop operators removed — SDK no longer in project
    }

    /// <summary>
    /// 2D vector for Mercator coordinates
    /// </summary>
    [Serializable]
    public struct Vector2d
    {
        public double x;
        public double y;

        public Vector2d(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vector2d operator +(Vector2d a, Vector2d b) => new Vector2d(a.x + b.x, a.y + b.y);
        public static Vector2d operator -(Vector2d a, Vector2d b) => new Vector2d(a.x - b.x, a.y - b.y);
        public static Vector2d operator *(Vector2d a, double scalar) => new Vector2d(a.x * scalar, a.y * scalar);
        public static Vector2d operator /(Vector2d a, double scalar) => new Vector2d(a.x / scalar, a.y / scalar);

        public double magnitude => Math.Sqrt(x * x + y * y);
        public double sqrMagnitude => x * x + y * y;

        public override string ToString() => $"({x:F6}, {y:F6})";

        // Mapbox SDK interop operators removed
    }

    /// <summary>
    /// Tile coordinate (z/x/y)
    /// </summary>
    [Serializable]
    public struct TileId
    {
        public int Z; // Zoom level
        public int X; // Tile X
        public int Y; // Tile Y

        public TileId(int z, int x, int y)
        {
            Z = z;
            X = x;
            Y = y;
        }

        public override string ToString() => $"{Z}/{X}/{Y}";
        public override int GetHashCode() => HashCode.Combine(Z, X, Y);
        public override bool Equals(object obj) => obj is TileId other && Z == other.Z && X == other.X && Y == other.Y;

        // Mapbox SDK interop operators removed
    }

    /// <summary>
    /// Extension methods for Unity types to work with Kiloverse coordinate system.
    /// </summary>
    public static class KiloverseVectorExtensions
    {
        /// <summary>
        /// Convert a Unity world position to GPS coordinates, given a Mercator reference point and scale.
        /// Replaces Mapbox.BaseModule.Utilities.VectorExtensions.GetGeoPosition.
        /// </summary>
        public static LatitudeLongitude GetGeoPosition(this UnityEngine.Vector3 position, Vector2d refPoint, float scale = 1f)
        {
            var mercX = refPoint.x + (position.x / scale);
            var mercY = refPoint.y + (position.z / scale);
            return Conversions.WebMercatorToLatitudeLongitude(new Vector2d(mercX, mercY));
        }
    }

    /// <summary>
    /// Web Mercator coordinate conversion utilities.
    /// Standard formulas - no Mapbox dependency.
    /// </summary>
    public static class Conversions
    {
        private const double EarthRadius = 6378137.0; // WGS84 Earth radius in meters
        private const double MercatorExtent = 20037508.342789244; // Max Mercator coordinate

        /// <summary>
        /// Convert GPS coordinates to Web Mercator (EPSG:3857)
        /// </summary>
        public static Vector2d LatitudeLongitudeToWebMercator(LatitudeLongitude latLng)
        {
            double lat = Mathf.Clamp((float)latLng.Latitude, -85.0511f, 85.0511f); // Web Mercator limit
            double lon = latLng.Longitude;

            double x = lon * Mathf.Deg2Rad * EarthRadius;
            double y = Math.Log(Math.Tan((90.0 + lat) * Mathf.Deg2Rad / 2.0)) * EarthRadius;

            return new Vector2d(x, y);
        }

        /// <summary>
        /// Convert Web Mercator to GPS coordinates
        /// </summary>
        public static LatitudeLongitude WebMercatorToLatitudeLongitude(Vector2d mercator)
        {
            double lon = (mercator.x / EarthRadius) * Mathf.Rad2Deg;
            double lat = (2.0 * Math.Atan(Math.Exp(mercator.y / EarthRadius)) - Math.PI / 2.0) * Mathf.Rad2Deg;

            return new LatitudeLongitude(lat, lon);
        }

        /// <summary>
        /// Get the size of one tile edge in Mercator coordinates at a given zoom level
        /// </summary>
        public static double TileEdgeSizeInMercator(int zoom)
        {
            // At zoom 0, world is 1 tile (2 * MercatorExtent wide)
            // Each zoom level doubles the number of tiles
            int tilesPerSide = 1 << zoom; // 2^zoom
            return (2.0 * MercatorExtent) / tilesPerSide;
        }

        /// <summary>
        /// Get the size of one tile edge in Mercator coordinates for a specific tile
        /// </summary>
        public static double TileEdgeSizeInMercator(TileId tileId)
        {
            return TileEdgeSizeInMercator(tileId.Z);
        }

        /// <summary>
        /// Get the scale factor (meters per Unity unit) at a given latitude and zoom
        /// </summary>
        public static float GetTileScaleInMeters(float latitude, int zoom)
        {
            // Mercator meters per tile at equator
            double metersPerTileEquator = (2.0 * Math.PI * EarthRadius) / (1 << zoom);

            // Adjust for latitude (Mercator distortion)
            double latRad = latitude * Mathf.Deg2Rad;
            double scale = metersPerTileEquator * Math.Cos(latRad);

            return (float)scale;
        }

        /// <summary>
        /// Convert GPS to tile coordinates at a given zoom level
        /// </summary>
        public static TileId LatitudeLongitudeToTileId(LatitudeLongitude latLng, int zoom)
        {
            double lat = Mathf.Clamp((float)latLng.Latitude, -85.0511f, 85.0511f);
            double lon = latLng.Longitude;

            int tilesPerSide = 1 << zoom;

            double x = (lon + 180.0) / 360.0 * tilesPerSide;
            double latRad = lat * Mathf.Deg2Rad;
            double y = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * tilesPerSide;

            return new TileId(zoom, (int)x, (int)y);
        }

        /// <summary>
        /// Get the GPS center of a tile
        /// </summary>
        public static LatitudeLongitude TileIdToLatLng(TileId tileId)
        {
            int tilesPerSide = 1 << tileId.Z;

            double lon = (tileId.X + 0.5) / tilesPerSide * 360.0 - 180.0;
            double latRad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * (tileId.Y + 0.5) / tilesPerSide)));
            double lat = latRad * Mathf.Rad2Deg;

            return new LatitudeLongitude(lat, lon);
        }

        /// <summary>
        /// Get latitude compensation factor for horizontal scaling
        /// Used to correct Web Mercator distortion for X/Z axis (NOT Y axis!)
        /// </summary>
        public static float GetLatitudeCompensation(double latitude)
        {
            double latRad = latitude * Mathf.Deg2Rad;
            return (float)Math.Cos(latRad);
        }

        /// <summary>
        /// Get the Web Mercator bounding box for a tile
        /// Returns (minX, minY, maxX, maxY) in Web Mercator coordinates
        /// </summary>
        public static (double minX, double minY, double maxX, double maxY) TileBoundsInWebMercator(TileId tileId)
        {
            int tilesPerSide = 1 << tileId.Z; // 2^zoom
            double tileSize = (2.0 * MercatorExtent) / tilesPerSide;

            double minX = -MercatorExtent + (tileId.X * tileSize);
            double maxX = minX + tileSize;

            // Y is inverted in tile coordinates (0 is top)
            double maxY = MercatorExtent - (tileId.Y * tileSize);
            double minY = maxY - tileSize;

            return (minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Calculate distance between two GPS coordinates using Haversine formula
        /// Returns distance in kilometers
        /// </summary>
        public static double GeoDistance(double lon1, double lat1, double lon2, double lat2)
        {
            double dLat = (lat2 - lat1) * Mathf.Deg2Rad;
            double dLon = (lon2 - lon1) * Mathf.Deg2Rad;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                      Math.Cos(lat1 * Mathf.Deg2Rad) * Math.Cos(lat2 * Mathf.Deg2Rad) *
                      Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double distanceKm = (EarthRadius / 1000.0) * c; // Convert meters to km

            return distanceKm;
        }

        /// <summary>
        /// Convert GPS coordinates to Unity world position relative to map center
        /// </summary>
        public static UnityEngine.Vector3 LatitudeLongitudeToWorldPosition(double latitude, double longitude, Vector2d centerMercator, float scale)
        {
            var targetLatLng = new LatitudeLongitude(latitude, longitude);
            var targetMercator = LatitudeLongitudeToWebMercator(targetLatLng);

            double offsetX = targetMercator.x - centerMercator.x;
            double offsetZ = targetMercator.y - centerMercator.y;

            return new UnityEngine.Vector3((float)offsetX, 0, (float)offsetZ);
        }
    }
}
