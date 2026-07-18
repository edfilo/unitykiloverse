# KiloLab render preset format

KiloLab is K1L0's live visual-tuning and named-preset system. It connects the
native Swift UI and the K1L0 API to Unity's runtime renderer without requiring a
Unity rebuild for value changes.

## Data flow and technology

1. The weather button asks `GET https://api-tunnel.kilo.gallery/api/k1l0/weather-presets`
   for the current catalog.
2. Selecting a mode asks `GET /api/k1l0/weather-presets/<id>` for that preset.
3. Swift applies a neutral render baseline, then the preset's settings. It also
   mirrors values into `UserDefaults` under `k1lo_native_<key>`.
4. Swift calls Unity through `UnitySendMessage("K1L0HUD", "SetNativeSetting",
   "<key>=<value>")`.
5. `K1L0HUD.SetNativeSetting` updates the runtime render profile, specialized
   controllers, and Unity `PlayerPrefs` (`k1lo_<key>`), then reapplies the
   renderer.

The renderer is Unity URP. Its relevant pieces are:

- URP Volume post-processing for color grading, bloom, vignette, chromatic
  aberration, depth of field, motion blur, and film grain.
- Kronnect Volumetric Fog & Mist 2 for true depth-aware, ray-marched fog.
- `GroundHazeController` plus the `K1L0/GroundHaze` shader for the cheaper
  animated ground smoke: three horizontal planes, four camera-facing cloud
  banks, and one horizon curtain. It renders after opaque world geometry and
  before beam thumbnails/HUD.
- `DynamicSkyVideoController` and `K1L0LayeredSky` for procedural sky colors,
  clouds, astronomy, horizon warmth, rain, and aurora.
- URP emissive Zoss building/window materials for glowing façades.
- `RenderManager` for time/weather selection, lighting, terrain/road values,
  and the active URP profile.

## JSON format

The authoritative catalog is the API-server file
`kiloworldapi/k1l0-weather-presets.json`.

```json
{
  "pink_haze": {
    "label": "Pink Haze",
    "revision": 1,
    "updatedAt": 1784277055603,
    "settings": {
      "groundHazeEnabled": "1",
      "groundHazeDensity": "0.92",
      "groundHazeHue": "0.965",
      "bloomEnabled": "1",
      "bloomIntensity": "1.48"
    }
  }
}
```

Rules:

- Preset IDs are lowercase `[a-z0-9_-]`, at most 48 characters.
- `label` is user-facing and at most 80 characters.
- `revision` increases when the preset is saved through the API.
- `updatedAt` is Unix epoch milliseconds.
- Every setting value is encoded as a JSON **string**, including numbers and
  booleans. Use `"1"` and `"0"` for booleans.
- Hue values use HSV turns: `0`/`1` red, `0.083` orange, `0.167` yellow,
  `0.333` green, `0.5` cyan, `0.667` blue, `0.833` magenta.
- Unknown keys are accepted by the file format but have no visual effect unless
  Unity implements a receiver for them.

## Resolution and persistence

- The server preset is canonical whenever its request succeeds.
- On a request failure, Swift uses the last successfully cached server catalog.
- If there is no cache, the app uses bundled Day, Night, Auto, Pink Haze, Haze
  Lab, Coral Haze, Fiery Orange Haze, and Boring descriptors plus bundled
  fallback values for Day/Night/Auto/Pink/Haze Lab/Boring.
- The selected preset ID is stored locally as
  `k1lo_native_weatherLookMode`.
- Applied values persist in Swift `UserDefaults` and Unity `PlayerPrefs`, but
  every preset selection first applies `resetBaseline`; visual toggles and
  atmospheric values therefore do not inherit accidentally from the previous
  preset. Camera, HUD, and gameplay tuning are intentionally outside that reset.

## Live commands

`POST /api/k1l0/render-tuning` accepts temporary live values and an optional
capture request:

```json
{
  "settings": {
    "groundHazeHue": "0.02",
    "groundHazeDensity": "0.8"
  },
  "capture": true
}
```

The device polls `GET /api/k1l0/render-tuning?since=<revision>`. These commands
are held in server memory and are not a named preset until explicitly saved.

## Parameter reference

Ranges below are Unity's clamps where the receiver defines one. Parameters
without a stated range are passed through as floating-point values.

### Mode, astronomy, and weather

| Parameter | Meaning |
|---|---|
| `testSkyOverride` | Bypass live time/weather and use manual sky inputs. Boolean. |
| `solarWorldOverride` | Lock the visual world to preset/manual solar state instead of live astronomy. Boolean. |
| `visualNightOverride` | Force night rendering while leaving the selected mode intact. Boolean. |
| `manualHour` | Manual local solar hour, wrapped into `0..<24`. |
| `manualWeather` | Manual weather index: 0 clear, 1 partly cloudy, 2 cloudy, 3 overcast, 4 rain, 5 snow, 6 fog, 7 storm. Selecting it enables the manual override. |
| `nativeSunAltitude` | Solar altitude supplied by native astronomy, degrees. |
| `nativeSunAzimuth` | Solar azimuth supplied by native astronomy, degrees. |
| `settingsPanelOpen` | Notifies the sky controller that the old settings panel is open. Runtime UI signal, not normally stored in a preset. |

### Layered procedural sky

| Parameter | Meaning |
|---|---|
| `experimentalLayeredSky` | Enables the procedural layered-sky renderer. Boolean. |
| `layeredBypassWeather` | Prevents live weather from replacing the preset's layered sky. Boolean-like float. |
| `layeredSkyEffect` | Weather effect selector used by the layered sky. |
| `layeredRain` | Rain intensity. |
| `layeredAurora` | Aurora intensity. |
| `layeredSkyTopHue` | HSV hue at the zenith. |
| `layeredSkyMidHue` | HSV hue in the middle sky. |
| `layeredSkyHorizonHue` | HSV horizon hue. |
| `layeredNightBlackness` | Night-sky darkening amount. |
| `layeredHorizonHeight` | Vertical position of the layered horizon blend. |
| `layeredCloudOpacity` | Procedural cloud opacity. |
| `layeredCloudSpeed` | Cloud animation speed. |
| `layeredCloudScale` | Cloud noise scale. |
| `layeredCloudContrast` | Cloud density/edge contrast. |
| `skyGoldenHourStart` | Hour/threshold at which golden-hour treatment begins. |
| `skySunriseWarmth` | Warm sunrise contribution. |
| `skySunsetWarmth` | Warm sunset contribution. |
| `skyDayBrightness` | Day-sky luminance multiplier. |
| `skyGoldenBrightness` | Golden-hour luminance multiplier. |
| `skyGoldenCloudWarmth` | Warm tint added to golden-hour clouds. |
| `skyCloudPink` | Pink contribution to clouds. |
| `skyHorizonPink` | Pink contribution to the horizon. |
| `skyNightHorizonGlow` | Night horizon glow intensity. |
| `skyNightHorizonHue` | Night horizon HSV hue. |
| `skyNightHorizonBrightness` | Night horizon luminance. |
| `vaporDayPink` | Global vapor-pink daylight contribution. `0...1`. |
| `skyTargetFps` | Target update rate for animated sky work. |

### Low-cost ground haze and smoke

These parameters drive the custom transparent plane/billboard shader, not the
ray-marched volumetric fog system.

| Parameter | Meaning |
|---|---|
| `groundHazeEnabled` | Shows the three smoke sheets, four cloud banks, and horizon curtain. Boolean. |
| `groundHazeDensity` | Base opacity/density; layers breathe around this value. |
| `groundHazeDetail` | Procedural noise frequency/detail. |
| `groundHazeSpeed` | Shader noise animation speed. Transform billowing also has deliberately slow built-in motion. |
| `groundHazeHeight` | Base haze height relative to the player, metres. |
| `groundHazeSpacing` | Vertical separation between horizontal sheets, metres. |
| `groundHazeHue` | Base smoke HSV hue. |
| `groundHazeSaturation` | Base smoke saturation, normally `0...1`. |
| `groundHazeBrightness` | Base smoke value/brightness. Values above 1 can bloom. |
| `groundHazeExtent` | Horizontal scale/radius of the nearby smoke sheets. |
| `groundHazePinkAmount` | Pink secondary color contribution, `0...1`. |
| `groundHazeWhiteAmount` | White secondary color contribution, `0...1`. |
| `groundHazeBlueAmount` | Blue secondary color contribution, `0...1`. |
| `groundHazeOrangeAmount` | Orange secondary color contribution, `0...1`. |
| `groundHazeHorizonDensity` | Opacity of the distant horizon curtain, clamped `0...1`. |
| `groundHazeHorizonDistance` | Curtain distance in front of the camera/player, metres. |
| `groundHazeHorizonHeight` | Curtain center height, metres. |

### True volumetric fog

These parameters drive Kronnect Volumetric Fog & Mist 2 through the active
master profile. This path is depth-aware and scatters native lights, but is much
more GPU-expensive than ground haze on a phone.

| Parameter | Meaning / Unity clamp |
|---|---|
| `volumetricFogEnabled` | Enables the volumetric fog renderer. Boolean. |
| `fogConstantDensity` | Uses constant rather than height/distance-shaped density. Boolean. |
| `fogDensity` | Day fog density, `0...1`. |
| `fogNoiseStrength` | 3D noise modulation, `0...2`. |
| `fogNoiseScale` | Noise world scale, `0.1...100`. |
| `fogTurbulence` | Noise distortion/turbulence. |
| `fogWindX`, `fogWindY`, `fogWindZ` | Fog-noise motion vector. |
| `fogBrightness` | Fog emission/brightness, `0...2`. |
| `fogScatteringIntensity` | Light scattering strength, `0...4`. |
| `fogOrangeAmount` | Convenience warm/orange color contribution. |
| `fogColorRed`, `fogColorGreen`, `fogColorBlue` | Explicit fog RGB channels. |
| `fogHeight` | Fog layer height, `0...500` metres. |
| `fogVerticalOffset` | Vertical displacement, `-500...500` metres. |
| `fogDistance` | Starting/working distance, `0...12000` metres. |
| `fogDistanceFallOff` | Distance falloff, `0...1`. |
| `fogMaxDistance` | Maximum ray-march distance, `1...12000` metres. |
| `fogMaxDistanceFallOff` | Maximum-distance fade, `0...1`. |
| `fogDistantFog` | Enables the cheaper distant-fog component. Boolean. |
| `fogDistantDensity` | Distant fog density, `0...2`. |
| `fogDistantStart` | Distance where distant fog begins. |
| `fogNativeLights` | Allows registered native/Unity lights to illuminate fog. Boolean. |
| `fogNativeLightsMultiplier` | Light contribution multiplier. |

Night variants are `fogDensity_night`, `fogNoiseStrength_night`,
`fogNoiseScale_night`, `fogBrightness_night`,
`fogScatteringIntensity_night`, `fogHeight_night`,
`fogDistantDensity_night`, and `fogDistantStart_night`. They are stored as the
night counterpart of the corresponding day parameter.

### Post-processing and color grading

| Parameter | Meaning / Unity clamp |
|---|---|
| `saturation` | URP color-adjustment saturation. |
| `contrast` | URP color-adjustment contrast. |
| `mapBrightness` | Fixed exposure/brightness value; also enables exposure. |
| `hueShift` | Global hue shift. |
| `temperature` | White-balance temperature; enables its override. |
| `tint` | White-balance green/magenta tint; enables its override. |
| `bloomEnabled` | Enables URP bloom. Boolean. |
| `bloomIntensity` | Active bloom intensity. |
| `dayBloomIntensity` | Day-specific bloom intensity, `0...8`. |
| `bloomThreshold` | Luminance threshold that begins blooming. |
| `bloomScatter` | Bloom spread, `0...1`. |
| `vignetteEnabled` | Enables vignette. Boolean. |
| `vignetteIntensity` | Vignette strength, `0...1`. |
| `vignetteSmoothness` | Vignette edge softness, `0.01...1`. |
| `chromaticEnabled` | Enables chromatic aberration. Boolean. |
| `chromaticIntensity` | Chromatic aberration amount, `0...1`. |
| `dofEnabled` | Enables depth of field. Boolean. |
| `focusDistance` | Focus distance, `0.1...300` metres. |
| `aperture` | Simulated f-stop, `0.05...32`. |
| `focalLength` | Simulated focal length, `1...300` mm. |
| `motionBlurEnabled` | Enables motion blur. Boolean. |
| `motionBlurIntensity` | Motion blur intensity, `0...1`. |
| `filmGrainEnabled` | Enables film grain. Boolean. |
| `filmGrainIntensity` | Film grain intensity, `0...1`. |
| `lensDistEnabled`, `lensDistIntensity` | Accepted for compatibility but intentionally force lens distortion off and intensity to zero. |

### Lighting

| Parameter | Meaning |
|---|---|
| `daySunIntensity` | Day directional-sun intensity. |
| `moonlightEnabled` | Enables the moonlight directional light. Boolean. |
| `moonlightManualOverride` | Uses manual moonlight orientation/color instead of astronomy. Boolean. |
| `moonlightIntensity` | Moonlight intensity. |
| `moonlightRed`, `moonlightGreen`, `moonlightBlue` | Moonlight RGB channels. |
| `moonlightPitch`, `moonlightYaw`, `moonlightRoll` | Manual moonlight Euler angles, degrees. |
| `ambientEnabled` | Enables ambient/environment light. Boolean. |
| `ambientIntensity` | Ambient-light multiplier. |
| `spotlightEnabled` | Enables the avatar/player spotlight. Boolean. |
| `spotlightIntensity` | Avatar/player spotlight intensity. |

### Building façades and emissive windows

The `zoss*` values drive generated building wall/window materials and the
`K1L0ZossWindows` shader.

| Parameter | Meaning |
|---|---|
| `zossDayWindowIntensity` | Daylight emissive-window intensity. |
| `zossEmissiveIntensity` | Base emissive intensity. |
| `zossEmissiveSmoothness` | Window material smoothness, `0...1`. |
| `zossEmissiveMetallic` | Window material metallic value, `0...1`. |
| `zossEmissiveHue` | Day emissive HSV hue. |
| `zossEmissiveSaturation` | Day emissive saturation. |
| `zossWallValue` | Wall HSV value/brightness. |
| `zossWallSaturation` | Wall saturation. |
| `zossLitFraction` | Fraction of generated windows that are lit, `0...1`. |
| `zossPaletteMix` | Mix between base and varied window palette, `0...1`. |
| `zossPaletteSaturation` | Day window-palette saturation. |
| `zossPaletteSaturation_night` | Night window-palette saturation. |
| `zossWarmth` | Warm/cool balance of generated window colors. |
| `zossAccentFraction` | Fraction of rare cyan/pink/magenta accent windows. |
| `zossWindowBrightness` | Final window brightness multiplier. |
| `zossBrightnessJitter` | Random per-window brightness variation. |
| `zossBrightnessJitterRate` | Temporal flicker/change rate. |
| `zossWallDaylightLift` | Additional wall visibility in daylight. |
| `zossWallVariance` | Per-building/per-wall brightness variation. |

### Ground, roads, and night ground

| Parameter | Meaning |
|---|---|
| `groundHue` | Ground HSV hue. |
| `groundSaturation` | Ground HSV saturation. |
| `groundValue` | Ground HSV value/brightness. |
| `groundBrightness` | Additional ground-light multiplier. |
| `groundHue_night` | Night-specific ground hue. |
| `groundSaturation_night` | Night-specific ground saturation. |
| `roadValue` | Base road brightness/value. |
| `dayRoadValue` | Day-specific road brightness. |
| `roadHue` | Road HSV hue. |
| `roadSaturation` | Road saturation. |
| `roadGlow` | Road emissive/glow contribution. |

### Camera, item thumbnails, and diagnostics

These are valid live commands but should normally be excluded from visual
presets so selecting a sky look does not move the player camera.

| Parameter | Meaning |
|---|---|
| `godPositionY`, `godPositionZ`, `godRotationX` | God-view camera height, distance, and pitch. |
| `farClipPlane` | Camera far clipping distance. |
| `debugGodMode` | Forces debug God view. Boolean. |
| `debugCameraHeading` | Overrides debug camera heading, degrees. |
| `debugFrameNearestBeam` | Frames the nearest beam with the supplied minimum distance. |
| `debugPositionOverride` | Enables a debug world-position override. |
| `debugHernandezPark` | Moves debug position to the Hernandez Park test area. |
| `itemViewportHeight` | Target item-thumbnail screen height. |
| `itemMaxWorldSize` | Maximum world-space size for item billboards. |
| `itemGlitchAmount` | Item billboard glitch intensity. |
| `beamDistanceLabels` | Shows distance labels over beams. Boolean. |
| `projectorLaserBeams` | Selects projector-laser rather than particle beam rendering. Boolean. |
| `beamDebug` | Shows beam diagnostics. Boolean. |
| `perfOverlay` | Shows Unity performance diagnostics. Boolean. |
| `showStoryStrip` | Shows the Unity story strip. Boolean. |
| `panelMapBrightness` | Map dimming/brightness while native panels are open. |
| `skyTargetFps` | Sky animation update target; also useful as a performance control. |

### Gameplay/runtime controls accepted by the same bridge

These are not renderer preset fields: `musicRadioEnabled`,
`transmissionFizzyEdges`, `ambientMinStepsToSpawn` (`0...2000`),
`momentumGraceSteps` (`10...500`), `ambientBeamTtlMinutes` (`1...240`), and
`ambientCollectRadiusMeters` (`1...100`). Keep them out of named weather looks.

## Stored legacy or currently ineffective fields

The current server catalog contains historical fields that are not handled by
`K1L0HUD.SetNativeSetting` in this build. They are retained for round-tripping
old snapshots but should not be relied on:

- `waterHue`, `waterSaturation`, `waterValue`
- `reflectionIntensity`, `reflectionsEnabled`
- `enableShadows`, `shadowDistance`, `shadowStrength`
- `weatherOpenMeteo`
- `skyVideoUrl` (the legacy weather-video resolver is disabled; the procedural
  sky is production)
- `exposureFixedValue` (use `mapBrightness`, which writes the fixed exposure)
- `manualWeatherOverrideEnabled` (selecting `manualWeather` enables it)

## Current Haze Lab

As of revision 5, **Haze Lab is server-backed** and normally loads from
`GET /api/k1l0/weather-presets/haze_lab`. The server record has 168 stored
fields. Its important active choices are:

- manual daylight at hour `13.25`, with live weather bypassed;
- procedural layered sky enabled;
- true volumetric fog enabled with `fogDensity=0.012`, height `320`, pink RGB
  approximately `(0.9, 0.4, 0.5)`, native-light scattering enabled, and slow
  negative-X/Z drift;
- cheap ground haze disabled (`groundHazeEnabled=0`, density 0, horizon density
  0), although its dormant color parameters remain stored;
- bloom enabled but restrained (`0.78`, threshold `0.98`);
- vignette enabled; chromatic aberration, depth of field, motion blur, and film
  grain disabled;
- dark pink ground/roads and bright generated windows.

The phone also contains a local bundled Haze Lab fallback derived from Pink
Haze. It is used only when the server request fails and no valid cached server
copy exists. Therefore the normal phone look is the server revision, not the
bundled override.
