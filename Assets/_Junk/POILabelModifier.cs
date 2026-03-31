using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Data;
using Mapbox.BaseModule.Data.Vector2d; // For LatitudeLongitude
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using UnityEngine;
using TMPro;

[Serializable]
public class POILabelModifier : GameObjectModifier
{
    // Schema for Overture z15+ names property
    [Serializable]
    public class OvertureNames
    {
        public string primary;
        public string common;
        public string rules;
    }

    // Schema for Overture z15+ categories property
    [Serializable]
    public class OvertureCategories
    {
        public string primary;
        public string alternate;
    }

    private Dictionary<VectorEntity, GameObject> _labels;
    private static GameObject _anchorContainer;

    private static readonly HashSet<string> _tileDumpLogged = new HashSet<string>();
    private static bool _tileDumpHeaderLogged = false;
    private const int TileDumpZoom = 16;
    private const int TileDumpX = 12567;
    private const int TileDumpY = 26570;

    // Track label anchors created during current tile processing (for tile-based culling)
    private static readonly List<GameObject> _currentTileLabels = new List<GameObject>();

    public static List<GameObject> GetAndClearCurrentTileLabels()
    {
        var labels = new List<GameObject>(_currentTileLabels);
        _currentTileLabels.Clear();
        return labels;
    }

    

public POILabelModifier()
    {
        if (_labels == null)
        {
            _labels = new Dictionary<VectorEntity, GameObject>();
        }
    }

    // Map Overture primary category to one of 4 main category groups (from locations.html)
    private static string GetMainCategoryGroup(string primaryCategory)
    {
        if (string.IsNullOrEmpty(primaryCategory)) return "other";

        // Coffee/Cafe - Green
        if (primaryCategory == "cafe" || primaryCategory == "coffee_shop" ||
            primaryCategory == "smoothie_juice_bar" || primaryCategory == "juice_bar" ||
            primaryCategory == "tea_house")
        {
            return "coffee";
        }

        // Bars/Pubs - Red
        if (primaryCategory == "bar" || primaryCategory == "pub" || primaryCategory == "brewery" ||
            primaryCategory == "beer_bar" || primaryCategory == "cocktail_bar" || primaryCategory == "wine_bar" ||
            primaryCategory == "dive_bar" || primaryCategory == "sports_bar" || primaryCategory == "gastropub" ||
            primaryCategory == "irish_pub" || primaryCategory == "tiki_bar" || primaryCategory == "sake_bar")
        {
            return "bar";
        }

        // Restaurants/Food - Blue (all food-related categories)
        if (primaryCategory == "restaurant" || primaryCategory == "fast_food_restaurant" ||
            primaryCategory == "pizza_restaurant" || primaryCategory == "burger_restaurant" ||
            primaryCategory == "chinese_restaurant" || primaryCategory == "mexican_restaurant" ||
            primaryCategory == "italian_restaurant" || primaryCategory == "japanese_restaurant" ||
            primaryCategory == "thai_restaurant" || primaryCategory == "indian_restaurant" ||
            primaryCategory == "sushi_restaurant" || primaryCategory == "american_restaurant" ||
            primaryCategory == "diner" || primaryCategory == "breakfast_and_brunch_restaurant" ||
            primaryCategory == "steakhouse" || primaryCategory == "sandwich_shop" ||
            primaryCategory == "bakery" || primaryCategory == "food_stand" ||
            primaryCategory == "seafood_restaurant" || primaryCategory == "ethiopian_restaurant" ||
            primaryCategory == "chicken_restaurant" || primaryCategory == "hot_dog_restaurant" ||
            primaryCategory == "buffet_restaurant")
        {
            return "food";
        }

        // Convenience - Orange
        if (primaryCategory == "convenience_store" || primaryCategory == "gas_station")
        {
            return "convenience";
        }

        return "other";
    }

    public override void Run(VectorEntity ve, IMapInformation mapInformation)
    {
        // Lazy initialize anchor container (cannot create GameObjects in constructor during serialization)
        if (_anchorContainer == null)
        {
            _anchorContainer = new GameObject("POI_Anchors");
            Debug.Log("[POILabelModifier] Created POI_Anchors container for scene organization");
        }

        try
        {
            // Debug.Log("[POILabelModifier] Run() called - starting POI processing");
            // Debug.Log($"[POILabelModifier] VectorEntity: GameObject={ve.GameObject?.name}, Feature={ve.Feature != null}, MeshFilter={ve.MeshFilter != null}");

            // 1. Get Name - Updated for Overture z15 schema
            string poiName = "Unknown POI";

            // New schema (z15+): names = {"primary":"...","common":null,"rules":null}
            if (ve.Feature.Properties.ContainsKey("names"))
            {
                try
                {
                    string namesJson = ve.Feature.Properties["names"].ToString();
                    var namesObj = JsonUtility.FromJson<OvertureNames>(namesJson);
                    if (namesObj != null && !string.IsNullOrEmpty(namesObj.primary))
                    {
                        poiName = namesObj.primary;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[POILabelModifier] Failed to parse names JSON: {ex.Message}");
                }
            }
            // Fallback to old schema for backwards compatibility
            else if (ve.Feature.Properties.ContainsKey("name"))
            {
                poiName = ve.Feature.Properties["name"].ToString();
            }
            else if (ve.Feature.Properties.ContainsKey("name_en"))
            {
                poiName = ve.Feature.Properties["name_en"].ToString();
            }

            // 2. Extract Categories - Parse Overture z15 schema
            string primaryCategory = "";
            string mainCategoryGroup = "other";

            // New schema (z15+): categories = {"primary":"bar","alternate":null}
            if (ve.Feature.Properties.ContainsKey("categories"))
            {
                try
                {
                    string categoriesJson = ve.Feature.Properties["categories"].ToString();
                    var categoriesObj = JsonUtility.FromJson<OvertureCategories>(categoriesJson);
                    if (categoriesObj != null && !string.IsNullOrEmpty(categoriesObj.primary))
                    {
                        primaryCategory = categoriesObj.primary;
                        mainCategoryGroup = GetMainCategoryGroup(primaryCategory);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[POILabelModifier] Failed to parse categories JSON: {ex.Message}");
                }
            }

            // Fallback: Extract legacy properties (for backwards compatibility)
            string className = ve.Feature.Properties.ContainsKey("class") ? ve.Feature.Properties["class"].ToString() : "";
            string typeName = ve.Feature.Properties.ContainsKey("type") ? ve.Feature.Properties["type"].ToString() : "";
            string makiName = ve.Feature.Properties.ContainsKey("maki") ? ve.Feature.Properties["maki"].ToString() : "";

            // If category parsing failed, try to infer from legacy fields
            if (string.IsNullOrEmpty(primaryCategory))
            {
                primaryCategory = !string.IsNullOrEmpty(makiName) ? makiName :
                                  !string.IsNullOrEmpty(typeName) ? typeName :
                                  className;
                mainCategoryGroup = GetMainCategoryGroup(primaryCategory);
            }

            // 3. Determine GPS Location
            // Prefer feature point coordinates (more accurate than tile center).
            Vector2d latLon = new Vector2d();
            bool hasPointLocation = false;

            // DEBUG: Check what geometry data exists
            // Debug.Log($"[POILabelModifier] '{poiName}' Points={ve.Feature.Points?.Count ?? 0}");

            try
            {
                if (ve.Feature.Points != null && ve.Feature.Points.Count > 0 && ve.Feature.Points[0].Count > 0)
                {
                    var tileId = ve.Feature.TileId;
                    var point = ve.Feature.Points[0][0];

                    // COORDINATE SYSTEM: Mapbox vector tiles have point.z in 0-1 range where:
                    // - point.z = 0 at NORTH edge of tile
                    // - point.z = 1 at SOUTH edge of tile
                    // Tile Y coordinate increases southward, so: absoluteY = tileId.Y + point.z
                    double n = System.Math.Pow(2, tileId.Z);
                    double lon = (tileId.X + point.x) / n * 360.0 - 180.0;
                    double latRad = System.Math.Atan(System.Math.Sinh(System.Math.PI * (1 - 2 * (tileId.Y + point.z) / n)));
                    double lat = latRad * 180.0 / System.Math.PI;

                    latLon = new Vector2d(lat, lon);
                    hasPointLocation = true;
                }
                else
                {
                    // No point geometry - will fall back to tile center
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[POILabelModifier] Failed to compute point GPS, falling back to tile center. {ex.Message}");
            }

            if (!hasPointLocation)
            {
                var tileId = ve.Feature.TileId;
                var rect = Mapbox.BaseModule.Utilities.Conversions.TileBoundsInWebMercator(tileId);
                var tileCenterMercator = rect.Center;
                var latLonStruct = Mapbox.BaseModule.Utilities.Conversions.WebMercatorToLatLon(tileCenterMercator);
                latLon = new Vector2d(latLonStruct.Latitude, latLonStruct.Longitude);
            }

            // 4. Calculate World Position from GPS coordinates
            // Convert lat/lon to Unity world position (same approach as GPSLocationController)
            Vector2d mapCenterMercator = Conversions.LatitudeLongitudeToWebMercator(mapInformation.LatitudeLongitude);
            Vector2d poiMercator = Conversions.LatitudeLongitudeToWebMercator(new LatitudeLongitude(latLon.x, latLon.y));
            Vector2d diff = poiMercator - mapCenterMercator;

            Vector3 worldPos = new Vector3((float)diff.x, 20f, (float)diff.y);

            // 5. Create/Update Label Anchor
            GameObject labelObj;
            if (_labels.ContainsKey(ve))
            {
                labelObj = _labels[ve];
                if (labelObj != null)
                {
                    labelObj.transform.position = worldPos;
                }
                else
                {
                    _labels.Remove(ve);
                    labelObj = new GameObject($"LabelAnchor_{poiName}");
                    labelObj.transform.SetParent(_anchorContainer.transform, false);
                    labelObj.transform.position = worldPos;
                    _labels.Add(ve, labelObj);
                }
            }
            else
            {
                labelObj = new GameObject($"LabelAnchor_{poiName}");
                labelObj.transform.SetParent(_anchorContainer.transform, false);
                labelObj.transform.position = worldPos;
                _labels.Add(ve, labelObj);
            }
            
            // Track this label for tile-based culling
            _currentTileLabels.Add(labelObj);

            // Register UI
            if (POICanvasManager.Instance == null)
            {
                Debug.Log("[POILabelModifier] Creating POICanvasManager instance");
                new GameObject("POICanvasManager").AddComponent<POICanvasManager>();
            }
            // Debug.Log($"[POILabelModifier] Registering POI '{poiName}' at world pos {worldPos} with POICanvasManager");
            POICanvasManager.Instance.RegisterPOI(labelObj.transform, poiName);

            // Register Scanner
            if (TransmitterScanner.Instance == null)
            {
                new GameObject("TransmitterScanner").AddComponent<TransmitterScanner>();
            }
            TransmitterScanner.Instance.RegisterTransmitter(poiName, primaryCategory, mainCategoryGroup, latLon);

            LogTileDump(ve, poiName, className, typeName, makiName, latLon);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[POILabelModifier] FAILED: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public override void Finalize(VectorEntity entity)
    {
        base.Finalize(entity);
        if (_labels.TryGetValue(entity, out var label))
        {
            if (label != null)
            {
                if (POICanvasManager.Instance != null)
                {
                    POICanvasManager.Instance.UnregisterPOI(label.transform);
                }
                GameObject.Destroy(label);
            }
            _labels.Remove(entity);
        }
    }



    private static void LogTileDump(VectorEntity ve, string poiName, string className, string typeName, string makiName, Vector2d latLon)
    {
        if (ve == null || ve.Feature == null) return;
        var tileId = ve.Feature.TileId;
        if (tileId.Z != TileDumpZoom || tileId.X != TileDumpX || tileId.Y != TileDumpY) return;

        if (!_tileDumpHeaderLogged)
        {
            Debug.Log($"[TileDump {TileDumpZoom}/{TileDumpX}/{TileDumpY}] Begin POI dump");
            _tileDumpHeaderLogged = true;
        }

        string filterRank = ve.Feature.Properties != null && ve.Feature.Properties.ContainsKey("filterrank")
            ? ve.Feature.Properties["filterrank"].ToString()
            : "";
        string key = $"{poiName}|{className}|{typeName}|{makiName}|{latLon.x:F6},{latLon.y:F6}";
        if (_tileDumpLogged.Contains(key)) return;

        _tileDumpLogged.Add(key);
        Debug.Log($"[TileDump {TileDumpZoom}/{TileDumpX}/{TileDumpY}] '{poiName}' class={className} type={typeName} maki={makiName} filterrank={filterRank} gps={latLon.x:F6},{latLon.y:F6}");
    }
}