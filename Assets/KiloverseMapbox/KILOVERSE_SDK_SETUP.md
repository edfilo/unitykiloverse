# KiloverseMapbox SDK Setup Guide

## Overview

The KiloverseMapbox SDK is a custom, lightweight map rendering system that **completely replaces** the Mapbox SDK dependency. It provides:

- ✅ **No Mapbox token required** - All tiles from `api.kilomeme.com`
- ✅ **No Mapbox API calls** - Zero external dependencies
- ✅ **Proper road widths** - Uses actual Overture Maps data
- ✅ **Memory pressure monitoring** - Track all layer culling efficiency
- ✅ **Two-tier culling** - Tile-level + per-object renderer culling

## Quick Start

### 1. Add KiloverseMapInfo Component

Replace any `MapboxMapBehaviour` component with `KiloverseMapInfo`:

```
GameObject: KiloMap
Components:
  - KiloverseMapInfo
    - Latitude: 40.4417 (Pittsburgh default)
    - Longitude: -80.0132
    - Zoom Level: 16
```

### 2. Link to OvertureMapManager

```
GameObject: KiloMap
Components:
  - OvertureMapManager
    - Map: (assign KiloverseMapInfo component)
    - Player Camera: (assign Main Camera)
    - Zoom Level: 16

    Visualizers:
      - Building Visualizer: BuildingLayerVisualizer.asset
      - Road Visualizer: RoadLayerVisualizer.asset
      - POI Visualizer: POILayerVisualizer.asset
      - Water Visualizer: WaterLayerVisualizer.asset
```

### 3. (Optional) Add Memory Stats Monitoring

```
GameObject: KiloMap
Components:
  - BuildingMemoryStats
    - Show On Screen GUI: ✓
    - Update Interval: 1.0
```

This displays real-time stats:
- Total objects in memory per layer
- Active objects (not tile-culled)
- Visible objects (actually rendering)
- Culling efficiency percentage

### 4. Update GPS Controller

```
GameObject: Player (or wherever GPSLocationController is attached)
Components:
  - GPSLocationController
    - Map: (assign KiloverseMapInfo component)
```

### 5. Update Teleport Manager

```
GameObject: UIBootstrapper (or wherever TeleportManager is attached)
Components:
  - TeleportManager
    - Map: (assign KiloverseMapInfo component)
```

## Road Width Setup (NEW!)

### Create ZossRoadStack Asset

1. Right-click in `Assets/KiloverseMapbox/ModifierStacks/`
2. Create → Kiloverse → Modifiers → Zoss Road Stack
3. Name it `ZossRoadStack.asset`

### Assign to Road Visualizer

1. Open `RoadLayerVisualizer.asset`
2. Find the Mesh Modifiers list
3. Replace `LineMeshModifier` with your new `ZossRoadStack.asset`

### How It Works

ZossRoadStack reads road width from Overture Maps in this priority:

1. **`width` property** (meters) - Direct width from data
2. **`lanes` property** - Calculates as `lanes × 3.5m`
3. **`class` property** - Falls back to defaults:
   - Motorway: 12m
   - Trunk: 10m
   - Primary: 8m
   - Secondary: 7m
   - Tertiary: 6m
   - Residential: 5m
   - Service: 3.5m
   - Pedestrian: 2m
   - Footway: 1.5m
   - Path: 1m
4. **Ultimate fallback**: 5m (if no data available)

## Architecture

### KiloverseMapInfo (Replaces MapboxMapBehaviour)

**Properties:**
- `Center` - Current GPS position (LatitudeLongitude)
- `Zoom` - Zoom level (int, 0-20)
- `MapInformation` - KiloverseMapInformation instance

**Methods:**
- `SetPosition(double lat, double lon)` - Update GPS position
- `SetZoom(int zoom)` - Update zoom level

**Implements:**
- `IMapInformation` - Required by mesh modifiers (SnapTerrainModifier, etc.)

### KiloverseMapInformation

**Properties:**
- `LatitudeLongitude` - Current GPS position
- `GetLatitudeCompensationForLocation` - Cos(latitude) for horizontal scaling

**Implements:**
- `IMapInformation` - Provides coordinate conversion for modifiers

### OvertureMapManager

**Now uses:**
- `KiloverseMapInfo` instead of `MapboxMapBehaviour`
- Direct visualizer assignment (no VectorLayerModuleScript dependency)
- `StartCoroutine()` from KiloverseMapInfo MonoBehaviour

**Features:**
- Fetches tiles from `api.kilomeme.com/xyz/{layer}/{z}/{x}/{y}.mvt`
- Decodes MVT tiles server-side (gzip/deflate support)
- Renders using assigned visualizers
- Two-tier culling (tile + per-object)

### Memory Pressure Monitoring

**BuildingMemoryStats.cs** tracks all layers:
- Buildings
- Roads
- Water
- Places
- Labels

**Metrics:**
- Total in memory
- Active (not tile-culled)
- Visible (renderer enabled)
- Culling efficiency %

**Display:**
- On-screen GUI (top-left corner)
- Console logs every 5 seconds
- Optional UI Text component

## Migration Checklist

✅ Replace `MapboxMapBehaviour` with `KiloverseMapInfo`
✅ Update all script references (15+ files updated automatically)
✅ Assign visualizers to OvertureMapManager
✅ Create ZossRoadStack asset for proper road widths
✅ Add BuildingMemoryStats for monitoring
✅ Remove any Mapbox token configuration
✅ Test GPS tracking
✅ Test teleport system
✅ Verify road widths in-game

## Troubleshooting

### "KiloverseMapInfo not found"
- Ensure KiloverseMapInfo component is attached to a GameObject in the scene
- Check that OvertureMapManager has the `Map` field assigned

### Roads all same width
- Verify ZossRoadStack.asset is assigned to RoadLayerVisualizer
- Check that LineMeshModifier is NOT in the modifier list
- Enable debug logs to see width values

### Buildings not loading
- Check visualizers are assigned to OvertureMapManager
- Verify `api.kilomeme.com` is accessible
- Check console for tile fetch errors

### High memory pressure
- Enable BuildingMemoryStats to monitor culling
- Verify tile-level culling is active (check console logs)
- Check UpdateFrustumCulling() is running every 10 frames
- Reduce `maxRenderDistance` on TileRendererCuller if attached

## Performance Tips

1. **Enable memory stats monitoring** during development to verify culling
2. **Attach TileRendererCuller** for per-building frustum/distance culling
3. **Adjust zoom level** - Higher zoom = more tiles = more memory
4. **Monitor console logs** for tile counts and culling stats

## What's Removed

- ❌ MapboxMapBehaviour
- ❌ VectorLayerModuleScript
- ❌ Mapbox token requirement
- ❌ Mapbox API calls
- ❌ Mapbox SDK authentication
- ❌ Fixed 5m road widths

## What's Added

- ✅ KiloverseMapInfo (~120 lines)
- ✅ ZossRoadStack (dynamic road widths)
- ✅ BuildingMemoryStats (monitoring)
- ✅ Proper Overture Maps data usage
- ✅ Full SDK independence

---

**Generated with Claude Code**
Last updated: 2026-01-30
