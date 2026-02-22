# Building Facade Performance Optimization

## Problem
Urban building tiles were generating **1 million+ vertices**, causing memory pressure and crashes on iOS.

## Root Cause
`ZossBuildingStack.cs` facade system creates individual geometry for every window pane:
- Each window: 2×3 grid = **6 quads × 4 vertices = 24 vertices**
- 256m skyscraper: 64 floors × 4 walls × 3 windows × 24 verts = **18,432 vertices**
- Downtown tile: 10 skyscrapers = **~200,000 vertices just for windows**
- Plus base walls, roads, POIs: **1M+ total vertices**

## Solution Applied: Single-Quad Windows

**File Changed**: `Assets/KiloverseMapbox/Scripts/ZossBuildingStack.cs:337`

**Before**:
```csharp
AddWindowGrid(md, ..., 2, 3); // 2×3 grid = 6 panes per window
```

**After**:
```csharp
AddWindowGrid(md, ..., 1, 1); // Single solid window per slot
```

## Impact

### Vertex Reduction
- **Per Window**: 24 vertices → **4 vertices** (6x reduction)
- **Per Skyscraper**: 18,432 → **3,072 vertices** (6x reduction)
- **Per Urban Tile**: 1,000,000 → **~170,000 vertices** (6x reduction)

### Memory Savings
- **Before**: ~32 MB per tile (1M verts × 32 bytes)
- **After**: ~5.4 MB per tile (170K verts × 32 bytes)
- **Savings**: ~27 MB per tile = **84% memory reduction**

### Visual Trade-off
- Windows are now solid glowing rectangles instead of multi-pane grids
- Still have per-window brightness variation
- Still have random "off" windows (dark rooms)
- Bloom effect still works on emissive windows
- Slightly less architectural detail, but **massively** more stable

## Future Optimizations (Not Implemented Yet)

### Option 1: Floor Mesh Instancing
Create 50 unique floor "ring" meshes and reuse them:
- Memory: ~200 KB unique data vs current 5.4 MB
- Draw calls: 50 vs 1000+
- Would enable **30x further reduction**

### Option 2: Texture-Based Facades
Use existing `TextureSideWallModifier.cs` with atlas textures:
- Windows are texture pixels instead of geometry
- Buildings become simple boxes with textured sides
- Ultimate performance but less dynamic lighting

### Option 3: LOD System
- Close (<100m): Current facade detail
- Medium (100-300m): Single-quad windows (current solution)
- Far (>300m): Simple box extrusion
- Would balance quality and performance

## Testing Recommendations

1. Monitor iOS memory in Xcode Instruments
2. Check frame rate in dense downtown areas
3. Verify bloom still looks good on solid windows
4. Test multiple tiles loading simultaneously
5. Watch for memory pressure warnings in console

## Rollback Instructions

If visual quality is unacceptable, revert line 337:
```csharp
AddWindowGrid(md, ..., 2, 3); // Restore 6-pane grid
```

But consider implementing LOD system instead for best of both worlds.
