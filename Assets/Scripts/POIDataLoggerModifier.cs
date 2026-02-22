using System;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using UnityEngine;

[Serializable]
public class POIDataLoggerModifier : GameObjectModifier
{

public override void Run(VectorEntity ve, IMapInformation mapInformation)
    {
        Debug.Log("[POIDataLoggerModifier] Run() called!");

        if (ve.GameObject == null || ve.Feature == null || ve.Feature.Properties == null)
        {
            return;
        }

        // Get POI data
        string name = ve.Feature.Properties.ContainsKey("name")
            ? ve.Feature.Properties["name"].ToString()
            : "NO NAME";

        string poiClass = ve.Feature.Properties.ContainsKey("class")
            ? ve.Feature.Properties["class"].ToString()
            : "unknown";

        string filterrank = ve.Feature.Properties.ContainsKey("filterrank")
            ? ve.Feature.Properties["filterrank"].ToString()
            : "?";

        // Get GPS coordinates from Points
        string gpsCoords = "N/A";
        try
        {
            if (ve.Feature.Points != null && ve.Feature.Points.Count > 0 && ve.Feature.Points[0].Count > 0)
            {
                var tileId = ve.Feature.TileId;
                var point = ve.Feature.Points[0][0];
                
                // Convert tile coordinates to lat/lon
                double n = System.Math.Pow(2, tileId.Z);
                double lon = (tileId.X + point.x) / n * 360.0 - 180.0;
                double lat_rad = System.Math.Atan(System.Math.Sinh(System.Math.PI * (1 - 2 * (tileId.Y - point.z) / n)));
                double lat = lat_rad * 180.0 / System.Math.PI;
                
                gpsCoords = $"{lat:F6}, {lon:F6}";
            }
        }
        catch (System.Exception ex)
        {
            gpsCoords = $"ERROR: {ex.Message}";
        }

        // Log POI data - this runs for every POI feature in every tile that loads
        Debug.Log($"[POI] '{name}' | class:{poiClass} | filterrank:{filterrank} | GPS:{gpsCoords} | Tile:{ve.Feature.TileId}");
    }
}
