import SwiftUI
import AVFoundation
import CoreLocation
import MapKit
import MetalKit
import CoreMedia
import Metal
#if canImport(UIKit)
import UIKit
#elseif canImport(AppKit)
import AppKit

// Small compatibility surface for shared SwiftUI views. These types let the
// macOS overlay compile the same view hierarchy while iPhone-only services
// (camera picker and software-keyboard notifications) remain inert on Mac.
enum UIImagePickerController {
    enum SourceType { case camera, photoLibrary }
    static func isSourceTypeAvailable(_ source: SourceType) -> Bool { source == .photoLibrary }
}
enum UIResponder {
    static let keyboardWillChangeFrameNotification = Notification.Name("K1L0MacKeyboardWillChangeFrame")
    static let keyboardWillHideNotification = Notification.Name("K1L0MacKeyboardWillHide")
    static let keyboardFrameEndUserInfoKey = "K1L0MacKeyboardFrameEnd"
}
struct UIScreen {
    static let main = UIScreen()
    var bounds: CGRect { NSScreen.main?.frame ?? .zero }
}
#endif
#if os(iOS)
import CoreMotion
@_silgen_name("UnityPause")
func K1L0UnityPause(_ pause: Int32)
#endif

let K1L0DefaultHelmetIconURL = "https://cdn.kilo.gallery/k1l0/ref/generic_closed_helmet_v2.png"

func K1L0StepValueText(_ steps: Int) -> String {
    let safeSteps = max(0, steps)
    guard safeSteps > 1_000 else { return "\(safeSteps)" }
    return String(format: "%.1fk", Double(safeSteps) / 1_000.0)
        .replacingOccurrences(of: ".0k", with: "k")
}

func K1L0StepText(_ steps: Int) -> String {
    "\(K1L0StepValueText(steps)) steps"
}

enum K1L0MediaCache {
    private static var configured = false

    static func configure() {
        guard !configured else { return }
        configured = true
        // Avatar PNGs can exceed 1 MB and transmission thumbnails are reused
        // across Home, Profile, tuning, and chain views. CDN URLs are immutable
        // and revisioned, so a generous shared disk cache is safe and avoids
        // downloading the same art every time a SwiftUI view is recreated.
        URLCache.shared = URLCache(
            memoryCapacity: 128 * 1024 * 1024,
            diskCapacity: 768 * 1024 * 1024,
            diskPath: "k1l0-media-cache"
        )
    }
}

enum K1L0NativeSettingsDefaults {
    static let values: [String: Any] = [
        // "Dystopian daylight" grade: drained color, cold temp, sickly green
        // tint, harsher contrast — the sky stays daytime but reads bleak.
        "k1lo_native_saturation": 0.0,
        "k1lo_native_contrast": 0.0,
        "k1lo_native_mapBrightness": 0.0,
        "k1lo_native_hueShift": 0.0,
        "k1lo_native_temperature": 0.0,
        "k1lo_native_tint": 0.0,
        "k1lo_native_bloomEnabled": true,
        "k1lo_native_bloomIntensity": 1.10,
        "k1lo_native_dayBloomIntensity": 2.0,
        "k1lo_native_bloomThreshold": 1.06,
        "k1lo_native_bloomScatter": 0.24,
        "k1lo_native_vignetteEnabled": true,
        "k1lo_native_vignetteIntensity": 0.45,
        "k1lo_native_vignetteSmoothness": 1.0,
        "k1lo_native_chromaticEnabled": true,
        "k1lo_native_chromaticIntensity": 0.16,
        "k1lo_native_dofEnabled": false,
        "k1lo_native_focusDistance": 18.1,
        "k1lo_native_aperture": 8.25,
        "k1lo_native_focalLength": 119.0,
        "k1lo_native_motionBlurEnabled": false,
        "k1lo_native_motionBlurIntensity": 0.02,
        "k1lo_native_filmGrainEnabled": true,
        "k1lo_native_filmGrainIntensity": 0.4,
        "k1lo_native_godPositionY": 49.0,
        "k1lo_native_godPositionZ": 107.0,
        "k1lo_native_godRotationX": -2.0,
        "k1lo_native_farClipPlane": 3600.0,
        "k1lo_native_moonlightEnabled": true,
        "k1lo_native_moonlightManualOverride": false,
        "k1lo_native_moonlightIntensity": 0.55,
        "k1lo_native_moonlightRed": 0.7,
        "k1lo_native_moonlightGreen": 0.8,
        "k1lo_native_moonlightBlue": 1.0,
        "k1lo_native_moonlightPitch": 90.0,
        "k1lo_native_moonlightYaw": 0.0,
        "k1lo_native_moonlightRoll": 0.0,
        "k1lo_native_ambientEnabled": false,
        // Dusty skylight so terrain reads as grey wasteland instead of void.
        // 1.15 keeps grass/roads readable at night without washing out the grade.
        "k1lo_native_ambientIntensity": 0.0,
        "k1lo_native_spotlightEnabled": true,
        "k1lo_native_spotlightIntensity": 3.0,
        "k1lo_native_zossEmissiveIntensity": 19.0,
        "k1lo_native_zossEmissiveSmoothness": 0.34,
        "k1lo_native_zossEmissiveMetallic": 0.0,
        // Window glow: vaporwave magenta-pink (hue 0.90 ≈ 324°) to match the
        // pink day skies; ground carries a faint irradiated-olive tinge so it
        // reads as ash, not paper-white.
        "k1lo_native_zossEmissiveHue": 0.90,
        "k1lo_native_zossEmissiveSaturation": 0.62,
        // Vaporwave city: near-black wall bodies, most windows lit with the
        // full per-window palette, day sky blushed pink.
        "k1lo_native_zossWallValue": 0.10,
        "k1lo_native_zossWallSaturation": 0.30,
        "k1lo_native_zossLitFraction": 1.0,
        "k1lo_native_zossPaletteMix": 1.0,
        "k1lo_native_zossPaletteSaturation": 1.35,
        "k1lo_native_zossPaletteSaturation_night": 1.22,
        "k1lo_native_zossWarmth": 1.0,
        "k1lo_native_zossAccentFraction": 0.08,
        "k1lo_native_zossWindowBrightness": 1.0,
        "k1lo_native_vaporDayPink": 0.65,
        "k1lo_native_zossNightEmissiveHue": 0.115,
        "k1lo_native_zossNightEmissiveSaturation": 0.82,
        // Green grass (hue 0.33) with real saturation — the old ash-olive
        // (0.23/0.12) read as black under the pink skies.
        "k1lo_native_groundHue": 0.33,
        "k1lo_native_groundSaturation": 0.42,
        "k1lo_native_beamDistanceLabels": true,
        "k1lo_native_projectorLaserBeams": true,
        "k1lo_native_beamDebug": false,
        "k1lo_native_perfOverlay": true,
        "k1lo_native_showStoryStrip": false,
        "k1lo_native_panelMapBrightness": 0.34,
        "k1lo_native_weatherOpenMeteo": true,
        "k1lo_native_weatherLookMode": "auto",
        "k1lo_native_bottomMenuLayout": "tabs",
        "k1lo_native_manualHour": 13.25,
        // Manual weather is only a fallback for when live weather is missing —
        // it must stay Clear so a sunny day never shows the overcast sky video.
        "k1lo_native_manualWeather": 0,
        "k1lo_native_ambientMinStepsToSpawn": 0.0,
        "k1lo_native_receiveStepsRequired": 200.0,
        "k1lo_native_transmissionWaitSteps": 500.0,
        "k1lo_native_momentumSessionGraceMinutes": 20.0,
        "k1lo_native_ambientBeamTtlMinutes": 30.0,
        "k1lo_native_ambientCollectRadiusMeters": 16.0,
        "k1lo_native_locationCollectRadiusFeet": 50.0,
        "k1lo_native_ambientBeamDismissSteps": 80.0,
        "k1lo_native_transmissionFX": true,
        "k1lo_native_transmissionFXIntensity": 0.5,
        "k1lo_native_transmissionFizzyEdges": false,
        "k1lo_native_musicRadioEnabled": true,
        "k1lo_native_musicRadioVolume": 0.5415074229240417,
        "k1lo_native_musicRadioMode": "instrumental",
        "k1lo_native_nearFogEnabled": false,
        "k1lo_native_fogConstantDensity": false,
        // The API Day/Night presets keep the fog package fully disabled unless
        // the user explicitly attaches the Fog workspace for experimentation.
        "k1lo_native_fogDensity": 0.0,
        "k1lo_native_fogNoiseStrength": 1.8,
        "k1lo_native_fogNoiseScale": 21.0,
        // Brighter fog = pale radioactive dust instead of black smog.
        "k1lo_native_fogBrightness": 0.81,
        "k1lo_native_fogScatteringIntensity": 0.85,
        "k1lo_native_fogHeight": 120.0,
        "k1lo_native_fogDistantFog": true,
        // Horizon haze: soften the hard sky/city seam so daylight reads
        // polluted rather than postcard-clear.
        "k1lo_native_fogDistantDensity": 0.0018,
        "k1lo_native_fogDistantStart": 25.0,
        "k1lo_native_fogV2DistantBaseAltitude": -88.0,
        "k1lo_native_fogV2DistantMaxHeight": 1800.0,
        "k1lo_native_fogV2DistantHeightDensity": 0.35,
        "k1lo_native_fogV2DistantColorRed": 0.98,
        "k1lo_native_fogV2DistantColorGreen": 1.0,
        "k1lo_native_fogV2DistantColorBlue": 0.46,
        "k1lo_native_fogV2DistantBrightness": 0.9,
        "k1lo_native_fogV2DiffusionIntensity": 0.0,
        "k1lo_native_fogV2DistantDiffusion": 3.32,
        "k1lo_native_fogV2DistantSymmetrical": false,
        "k1lo_native_fogV2DistantTransparency": true,
        "k1lo_native_fogV2DistantNoise": false,
        "k1lo_native_fogV2DistantNoiseScale": 0.01,
        "k1lo_native_fogV2DistantNoiseStrength": 0.36,
        "k1lo_native_fogV2DistantNoiseMaxDistance": 12000.0,
        "k1lo_native_fogV2DistantWindX": 0.008,
        "k1lo_native_fogV2DistantWindY": -0.01,
        "k1lo_native_fogV2DistantWindZ": 0.004,
        "k1lo_native_fogNativeLights": false,
        "k1lo_native_fogNativeLightsMultiplier": 0.0,
        "k1lo_native_skyTargetFps": 30.0,
        "k1lo_native_experimentalLayeredSky": true,
        "k1lo_native_layeredBypassWeather": false,
        "k1lo_native_layeredSkyEffect": 0,
        "k1lo_native_layeredRain": 0.0,
        "k1lo_native_layeredAurora": 0.0,
        "k1lo_native_layeredSkyTopHue": 0.80,
        "k1lo_native_layeredSkyMidHue": 0.62,
        "k1lo_native_layeredNightBlackness": 0.60,
        "k1lo_native_layeredSkyHorizonHue": 0.73,
        "k1lo_native_layeredHorizonHeight": 0.0,
        "k1lo_native_layeredCloudOpacity": 0.58,
        "k1lo_native_layeredCloudSpeed": 0.07,
        "k1lo_native_liveCloudCover": 35.0,
        "k1lo_native_layeredCloudScale": 2.6,
        "k1lo_native_layeredCloudContrast": 0.95,
        "k1lo_native_solarWorldOverride": false,
        "k1lo_native_liveSolarAltitude": 12.0,
        "k1lo_native_liveSolarAzimuth": 180.0,
        "k1lo_native_fogDensity_night": 0.025,
        "k1lo_native_fogNoiseStrength_night": 0.22,
        "k1lo_native_fogNoiseScale_night": 17.4,
        "k1lo_native_fogBrightness_night": 0.24,
        "k1lo_native_fogScatteringIntensity_night": 0.55,
        "k1lo_native_fogHeight_night": 48.0,
        "k1lo_native_fogDistantDensity_night": 0.1,
        "k1lo_native_fogDistantStart_night": 0.0,
        "k1lo_native_groundHue_night": 0.30,
        "k1lo_native_groundSaturation_night": 0.0
    ]

    private static func purgeRetiredGroundHazeSettings() {
        let suffixes = [
            "groundHazeEnabled", "groundHazeDensity", "groundHazeDetail",
            "groundHazeSpeed", "groundHazeHeight", "groundHazeSpacing",
            "groundHazeHue", "groundHazeSaturation", "groundHazeBrightness",
            "groundHazeExtent", "groundHazePinkAmount", "groundHazeWhiteAmount",
            "groundHazeBlueAmount", "groundHazeOrangeAmount",
            "groundHazeHorizonDensity", "groundHazeHorizonDistance",
            "groundHazeHorizonHeight"
        ]
        for suffix in suffixes {
            UserDefaults.standard.removeObject(forKey: "k1lo_native_\(suffix)")
            UserDefaults.standard.removeObject(forKey: "k1lo_\(suffix)")
        }
    }

    static func register() {
        purgeRetiredGroundHazeSettings()
        UserDefaults.standard.register(defaults: values)
        migrateWeatherLookMode()
        applyDystopianGradeOnce()
        applyGroundLightFixOnce()
        resetManualWeatherToClearOnce()
        applyGroundLiftOnce()
        applyPinkWindowGlowOnce()
        applyGreenGrassOnce()
        applyVaporCityOnce()
        applyWindowSaturationSplitOnce()
        applyZeroSpawnGateOnce()
        mergeWeatherOverrideTogglesOnce()
        applyGrassVisibilityLightOnce()
        applyHudCameraDefaultsOnce()
        disableThermalHeavyTransmissionEdgesOnce()
        resetLiveAstronomySkyOnce()
        applySafeFogBootState()
        repairInvalidLegacyFogOverrideOnce()
        applyAmbientZeroOnce()
        applyMoonlight055Once()
        resetOverridesOnLaunch()
    }

    private static func migrateWeatherLookMode() {
        let defaults = UserDefaults.standard
        let current = defaults.string(forKey: "k1lo_native_weatherLookMode") ?? "auto"
        let migrated: String
        switch current {
        case "radioactive": migrated = "day"
        case "midnight": migrated = "night"
        case "auto", "day", "night": migrated = current
        default: migrated = "auto"
        }
        defaults.set(migrated, forKey: "k1lo_native_weatherLookMode")
    }

    private static func applyMoonlight055Once() {
        let d = UserDefaults.standard
        let flag = "k1lo_native_moonlight055_v1"
        guard !d.bool(forKey: flag) else { return }
        d.set(0.55, forKey: "k1lo_native_moonlightIntensity")
        d.set(true, forKey: flag)
    }

    // Overrides are debug/test toggles — reset every launch so they never
    // persist across sessions accidentally.
    private static func resetOverridesOnLaunch() {
        let d = UserDefaults.standard
        d.set(false, forKey: "k1lo_native_testSkyOverride")
        d.set(false, forKey: "k1lo_native_moonlightManualOverride")
        d.set(false, forKey: "k1lo_native_solarWorldOverride")
        d.set(false, forKey: "k1lo_native_layeredBypassWeather")
    }

    /// Begin every preset-controlled launch in the inexpensive far-only state.
    /// The API Day/Night preset replaces these values as soon as it arrives.
    /// An intentionally attached Fog workspace remains untouched.
    private static func applySafeFogBootState() {
        let d = UserDefaults.standard
        if !d.bool(forKey: "k1lo_native_fogManualOverride") {
            d.set(false, forKey: "k1lo_native_nearFogEnabled")
            d.set(0.0, forKey: "k1lo_native_fogDensity")
            d.set(false, forKey: "k1lo_native_fogConstantDensity")
            d.set(true, forKey: "k1lo_native_fogDistantFog")
            d.set(0.0018, forKey: "k1lo_native_fogDistantDensity")
            d.set(25.0, forKey: "k1lo_native_fogDistantStart")
            d.set(0.9, forKey: "k1lo_native_fogV2DistantBrightness")
        }
    }

    /// Some installs retained an old fog experiment with the package disabled
    /// but an extreme distant density (the affected phone had 0.3805). That
    /// impossible pairing blocks every API Day/Auto update and then reasserts
    /// the blown-out value after live tuning. Repair only that legacy state;
    /// sane attached fog workspaces continue to persist exactly as designed.
    private static func repairInvalidLegacyFogOverrideOnce() {
        let d = UserDefaults.standard
        let flag = "k1lo_native_repairInvalidLegacyFogOverride_v1"
        guard !d.bool(forKey: flag) else { return }
        let overrideActive = d.bool(forKey: "k1lo_native_fogManualOverride")
        let distantDensity = d.object(forKey: "k1lo_native_fogDistantDensity") as? NSNumber
        let invalidLegacyState = overrideActive
            && d.object(forKey: "k1lo_native_volumetricFogEnabled") != nil
            && (distantDensity?.doubleValue ?? 0) > 0.5
        if invalidLegacyState {
            d.set(false, forKey: "k1lo_native_fogManualOverride")
            d.set(false, forKey: "k1lo_native_nearFogEnabled")
            d.set(false, forKey: "k1lo_native_fogConstantDensity")
            d.set(0.0, forKey: "k1lo_native_fogDensity")
            d.set(true, forKey: "k1lo_native_fogDistantFog")
            d.set(0.0018, forKey: "k1lo_native_fogDistantDensity")
            d.set(25.0, forKey: "k1lo_native_fogDistantStart")
            d.set(0.9, forKey: "k1lo_native_fogV2DistantBrightness")
        }
        d.set(true, forKey: flag)
    }

    private static func resetLiveAstronomySkyOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_liveAstronomySky_v5"
        guard !defaults.bool(forKey: flag) else { return }
        let cleanSky: [String: Any] = [
            "k1lo_native_experimentalLayeredSky": true,
            "k1lo_native_layeredBypassWeather": false,
            "k1lo_native_layeredSkyEffect": 0,
            "k1lo_native_layeredRain": 0.0,
            "k1lo_native_layeredAurora": 0.0,
            "k1lo_native_layeredCloudOpacity": 0.35,
            "k1lo_native_layeredCloudSpeed": 0.07,
            "k1lo_native_layeredCloudScale": 1.35,
            "k1lo_native_layeredCloudContrast": 1.1,
            "k1lo_native_layeredNightBlackness": 0.82,
            "k1lo_native_testSkyOverride": false,
            "k1lo_native_manualWeather": 0,
            "k1lo_native_manualHour": 13.25,
            "k1lo_native_solarWorldOverride": false,
            "k1lo_native_moonlightManualOverride": false,
            "k1lo_native_ambientEnabled": false,
            "k1lo_native_ambientIntensity": 0.0,
            "k1lo_native_groundHue": 0.28,
            "k1lo_native_groundSaturation": 0.28,
            "k1lo_native_fogBrightness": 0.55,
            "k1lo_native_fogScatteringIntensity": 1.25
        ]
        for (key, value) in cleanSky { defaults.set(value, forKey: key) }
        defaults.set(true, forKey: flag)
    }

    private static func disableThermalHeavyTransmissionEdgesOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_transmissionThermalGuard_v1"
        guard !defaults.bool(forKey: flag) else { return }
        defaults.set(false, forKey: "k1lo_native_transmissionFizzyEdges")
        defaults.set(false, forKey: "k1lo_transmissionFizzyEdges")
        defaults.set(true, forKey: flag)
    }

    private static func applyHudCameraDefaultsOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_hudCameraDefaults_v8"
        guard !defaults.bool(forKey: flag) else { return }
        let tuned: [String: Any] = [
            "k1lo_native_godPositionY": 49.0,
            "k1lo_native_godPositionZ": 107.0,
            "k1lo_native_godRotationX": -2.0,
            "k1lo_native_farClipPlane": 3600.0,
            "k1lo_native_bottomMenuLayout": "tabs",
            "k1lo_native_fogBrightness": 0.55,
            "k1lo_native_fogDistantDensity": 0.0,
            "k1lo_native_fogDistantStart": 0.0,
            "k1lo_native_zossEmissiveSaturation": 0.62,
            "k1lo_godPositionY": 49.0,
            "k1lo_godPositionZ": 107.0,
            "k1lo_godRotationX": -2.0,
            "k1lo_farClipPlane": 3600.0,
            "k1lo_fogBrightness": 0.34,
            "k1lo_fogDistantDensity": 0.0,
            "k1lo_fogDistantStart": 0.0,
            "k1lo_zossEmissiveSaturation": 0.62
        ]
        for (key, value) in tuned { defaults.set(value, forKey: key) }
        defaults.set(true, forKey: flag)
    }

    /// v7 stamp: the video sky path uses flat ambient for terrain; lift it so
    /// grass/road texture detail survives the heavy grade.
    private static func applyGrassVisibilityLightOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_grassVisibilityLight_v7"
        guard !defaults.bool(forKey: flag) else { return }
        defaults.set(true, forKey: "k1lo_native_ambientEnabled")
        defaults.set(1.55, forKey: "k1lo_native_ambientIntensity")
        defaults.set(true, forKey: "k1lo_native_beamDistanceLabels")
        defaults.set(true, forKey: flag)
    }

    /// v5 stamp: shift the stored window-glow color to vaporwave magenta-pink
    /// to match the new pink day skies. One-shot so users can re-tune after.
    private static func applyPinkWindowGlowOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_pinkWindowGlow_v5"
        guard !defaults.bool(forKey: flag) else { return }
        defaults.set(0.90, forKey: "k1lo_native_zossEmissiveHue")
        defaults.set(0.62, forKey: "k1lo_native_zossEmissiveSaturation")
        defaults.set(true, forKey: flag)
    }

    /// v6 stamp: green grass — the stored ash-olive ground color reads black
    /// under the pink day skies. One-shot so users can re-tune after.
    private static func applyGreenGrassOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_greenGrass_v6"
        guard !defaults.bool(forKey: flag) else { return }
        defaults.set(0.33, forKey: "k1lo_native_groundHue")
        defaults.set(0.42, forKey: "k1lo_native_groundSaturation")
        defaults.set(true, forKey: flag)
    }

    /// v9 stamp: Test Weather Override merged into Bypass Live Weather — the
    /// legacy flag now mirrors the Sky Lab master; clear any stale solo state.
    private static func mergeWeatherOverrideTogglesOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_weatherOverrideMerge_v9"
        guard !defaults.bool(forKey: flag) else { return }
        let bypass = defaults.bool(forKey: "k1lo_native_layeredBypassWeather")
        defaults.set(bypass, forKey: "k1lo_native_testSkyOverride")
        defaults.set(true, forKey: flag)
    }

    /// v10 stamp: ambient spawn gate to zero — beams appear without a step
    /// quota. Spawn spacing is enforced server-side (150 m minimum from the
    /// player), so nothing can pop uncomfortably close.
    private static func applyZeroSpawnGateOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_zeroSpawnGate_v10"
        guard !defaults.bool(forKey: flag) else { return }
        defaults.set(0.0, forKey: "k1lo_native_ambientMinStepsToSpawn")
        defaults.set(true, forKey: flag)
    }

    /// v8 stamp: window saturation day/night split — juiced neon palette in
    /// daylight, monochrome glow after dark. One-shot so users can re-tune.
    private static func applyWindowSaturationSplitOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_windowSaturationSplit_v8"
        guard !defaults.bool(forKey: flag) else { return }
        defaults.set(1.35, forKey: "k1lo_native_zossPaletteSaturation")
        defaults.set(1.22, forKey: "k1lo_native_zossPaletteSaturation_night")
        defaults.set(true, forKey: flag)
    }

    /// v7 stamp: vaporwave city — crushed dark building walls, per-window
    /// palette variety, pink-blushed day sky. One-shot so users can re-tune.
    private static func applyVaporCityOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_vaporCity_v7"
        guard !defaults.bool(forKey: flag) else { return }
        defaults.set(0.10, forKey: "k1lo_native_zossWallValue")
        defaults.set(0.30, forKey: "k1lo_native_zossWallSaturation")
        defaults.set(1.0, forKey: "k1lo_native_zossLitFraction")
        defaults.set(1.0, forKey: "k1lo_native_zossPaletteMix")
        defaults.set(0.65, forKey: "k1lo_native_vaporDayPink")
        defaults.set(true, forKey: flag)
    }

    /// v4 stamp: ground/roads still read too dark, especially at night — lift
    /// the stored ambient skylight over any previously saved value once.
    private static func applyGroundLiftOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_dystopianGrade_v4_groundLift"
        guard !defaults.bool(forKey: flag) else { return }
        defaults.set(true, forKey: "k1lo_native_ambientEnabled")
        defaults.set(1.15, forKey: "k1lo_native_ambientIntensity")
        defaults.set(true, forKey: flag)
    }

    /// v3 stamp: the dystopian grade briefly shipped with manual weather
    /// defaulted to Overcast, which hijacked the sky video whenever live
    /// weather was unavailable. Reset any stored Overcast back to Clear once.
    private static func resetManualWeatherToClearOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_dystopianGrade_v3_manualWeatherClear"
        guard !defaults.bool(forKey: flag) else { return }
        if (defaults.object(forKey: "k1lo_native_manualWeather") as? Int ?? 0) == 3 {
            defaults.set(0, forKey: "k1lo_native_manualWeather")
        }
        defaults.set(true, forKey: flag)
    }

    /// One-shot migration: existing installs have stored slider values that
    /// shadow the registered defaults, so stamp the new grade over them once.
    /// Users can still re-tune afterward.
    private static func applyDystopianGradeOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_dystopianGrade_v2"
        guard !defaults.bool(forKey: flag) else { return }
        let grade: [String: Any] = [
            "k1lo_native_saturation": 0.0,
            "k1lo_native_contrast": 0.0,
            "k1lo_native_mapBrightness": 0.0,
            "k1lo_native_temperature": 0.0,
            "k1lo_native_tint": 0.0,
            "k1lo_native_hueShift": 0.0,
            "k1lo_native_vignetteIntensity": 0.45,
            "k1lo_native_chromaticIntensity": 0.16,
            "k1lo_native_filmGrainEnabled": true,
            "k1lo_native_filmGrainIntensity": 0.4,
            "k1lo_native_fogDistantFog": true,
            "k1lo_native_fogDistantDensity": 0.3,
            "k1lo_native_fogDistantStart": 400.0,
        ]
        for (key, value) in grade { defaults.set(value, forKey: key) }
        defaults.set(true, forKey: flag)
    }

    /// v2 stamp: the v1 grade never touched ambient/ground, so installs that
    /// predate it kept night-tuned values and the daytime ground rendered
    /// black. Push the hazy-daylight ambient and ash-olive ground once.
    private static func applyGroundLightFixOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_dystopianGrade_v2_groundLight"
        guard !defaults.bool(forKey: flag) else { return }
        let fix: [String: Any] = [
            "k1lo_native_ambientEnabled": true,
            "k1lo_native_ambientIntensity": 0.9,
            "k1lo_native_fogBrightness": 0.55,
            "k1lo_native_groundHue": 0.23,
            "k1lo_native_groundSaturation": 0.12,
        ]
        for (key, value) in fix { defaults.set(value, forKey: key) }
        defaults.set(true, forKey: flag)
    }

    /// v10 stamp: set ambient light default to 0.0.
    private static func applyAmbientZeroOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_ambientZero_v10"
        guard !defaults.bool(forKey: flag) else { return }
        defaults.set(false, forKey: "k1lo_native_ambientEnabled")
        defaults.set(0.0, forKey: "k1lo_native_ambientIntensity")
        defaults.set(true, forKey: flag)
    }
}

/* Legacy weather-video resolver removed: the production sky is procedural.
enum K1L0SkyVideoURLResolver {
    private static let manualGlyphs = ["clear", "partly", "cloud", "overcast", "rain", "snow", "fog", "storm"]
    private static let lastLiveSkyVideoUrlKey = "k1lo_native_lastLiveSkyVideoUrl"

    // Slot assignments loaded from RTDB / admin config.
    // Keys match SKY_VIDEO_SLOTS in server.js: clear-day, clear-night, cloud-day,
    // cloud-night, raining-day, raining-night, thunder.
    private static var slotConfig: [String: String] = [:]

    // Call once on app launch (and whenever weather config changes in admin).
    static func fetchConfig() {
        guard let url = URL(string: "https://api-tunnel.kilo.gallery/api/k1l0/sky/config") else { return }
        URLSession.shared.dataTask(with: url) { data, _, _ in
            guard let data, let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let config = json["config"] as? [String: String] else { return }
            DispatchQueue.main.async { slotConfig = config }
        }.resume()
    }

    static func url(glyph: String, isDay: Bool?) -> String {
        let clip = clipName(glyph: glyph, isDay: isDay)
        return bundleUrl(for: clip)
    }

    static func url(manualWeatherIndex: Int, manualHour: Double? = nil) -> String {
        let index = max(0, min(manualGlyphs.count - 1, manualWeatherIndex))
        let hour = manualHour ?? UserDefaults.standard.double(forKey: "k1lo_native_manualHour")
        let normalizedHour = hour > 0 ? hour : 13.25
        let isDay = normalizedHour >= 6 && normalizedHour < 19
        return url(glyph: manualGlyphs[index], isDay: isDay)
    }

    static var testOverrideEnabled: Bool {
        UserDefaults.standard.object(forKey: "k1lo_native_testSkyOverride") as? Bool ?? false
    }

    static func rememberLiveSkyVideoUrl(_ url: String) {
        let trimmed = url.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        UserDefaults.standard.set(trimmed, forKey: lastLiveSkyVideoUrlKey)
    }

    static func applyManualSkyVideoIfTesting(manualWeatherIndex: Int, manualHour: Double? = nil) {
        guard testOverrideEnabled else { return }
        let skyVideoUrl = url(manualWeatherIndex: manualWeatherIndex, manualHour: manualHour)
        if !skyVideoUrl.isEmpty {
            K1L0WeatherOverlayInstaller.setUnitySetting("skyVideoUrl", skyVideoUrl)
        }
    }

    static func restoreLastLiveSkyVideoIfAvailable() {
        guard let skyVideoUrl = UserDefaults.standard.string(forKey: lastLiveSkyVideoUrlKey),
              !skyVideoUrl.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        else { return }
        K1L0WeatherOverlayInstaller.setUnitySetting("skyVideoUrl", skyVideoUrl)
    }

    private static func clipName(glyph: String, isDay: Bool?) -> String {
        let value = glyph.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        let day = isDay ?? true
        // Check admin config first; fall back to hardcoded defaults so existing
        // bundles work even if config hasn't been set yet.
        if value.contains("thunder") || value.contains("storm") || value.contains("bolt") {
            return slotConfig["thunder"] ?? "thunder.mp4"
        }
        if value.contains("rain") || value.contains("drizzle") || value.contains("shower") {
            return day ? (slotConfig["raining-day"] ?? "raining-day.mp4") : (slotConfig["raining-night"] ?? "raining-night.mp4")
        }
        if value.contains("cloud") || value.contains("fog") || value.contains("haze") || value.contains("overcast") || value.contains("partly") {
            return day ? (slotConfig["cloud-day"] ?? "cloud-day-1.mp4") : (slotConfig["cloud-night"] ?? "cloud-night-1.mp4")
        }
        return day ? (slotConfig["clear-day"] ?? "clear-day.mp4") : (slotConfig["clear-night"] ?? "clear-night.mp4")
    }

    private static func bundleUrl(for clip: String) -> String {
        let base = (clip as NSString).deletingPathExtension
        let ext = (clip as NSString).pathExtension
        let bundle = Bundle.main
        let candidates = [
            bundle.url(forResource: base, withExtension: ext, subdirectory: "Data/Raw/WeatherVideos"),
            bundle.url(forResource: base, withExtension: ext, subdirectory: "WeatherVideos"),
            bundle.url(forResource: base, withExtension: ext),
            fileUrlIfExists(bundle.bundleURL.appendingPathComponent("Data/Raw/WeatherVideos/\(clip)")),
            fileUrlIfExists(bundle.bundleURL.appendingPathComponent("WeatherVideos/\(clip)"))
        ]
        return candidates.compactMap { $0 }.first?.absoluteString ?? ""
    }

    private static func fileUrlIfExists(_ url: URL) -> URL? {
        FileManager.default.fileExists(atPath: url.path) ? url : nil
    }
} */

enum K1L0WindowGlowResolver {
    private static let lastWeatherIsDayKey = "k1lo_native_lastWeatherIsDay"

    static func apply(isDay explicitIsDay: Bool? = nil) {
        let defaults = UserDefaults.standard
        let isDay = explicitIsDay ?? storedOrManualIsDay(defaults)
        let hue: Double
        let saturation: Double
        if isDay {
            hue = K1L0WeatherLook.double("k1lo_native_zossEmissiveHue", 0.90)
            saturation = K1L0WeatherLook.double("k1lo_native_zossEmissiveSaturation", 0.62)
        } else {
            hue = K1L0WeatherLook.double("k1lo_native_zossNightEmissiveHue", 0.115)
            saturation = K1L0WeatherLook.double("k1lo_native_zossNightEmissiveSaturation", 0.82)
        }
        K1L0WeatherOverlayInstaller.setUnitySetting("zossEmissiveHue", String(format: "%.3f", hue))
        K1L0WeatherOverlayInstaller.setUnitySetting("zossEmissiveSaturation", String(format: "%.3f", saturation))
    }

    static func rememberWeatherIsDay(_ isDay: Bool?) {
        let defaults = UserDefaults.standard
        if let isDay {
            defaults.set(isDay, forKey: lastWeatherIsDayKey)
        } else {
            defaults.removeObject(forKey: lastWeatherIsDayKey)
        }
        apply(isDay: isDay)
    }

    static func applyManualHour(_ hour: Double) {
        let normalizedHour = hour.truncatingRemainder(dividingBy: 24)
        let wrappedHour = normalizedHour < 0 ? normalizedHour + 24 : normalizedHour
        apply(isDay: wrappedHour >= 6 && wrappedHour < 19)
    }

    private static func storedOrManualIsDay(_ defaults: UserDefaults) -> Bool {
        if defaults.object(forKey: lastWeatherIsDayKey) != nil {
            return defaults.bool(forKey: lastWeatherIsDayKey)
        }
        let hour = defaults.object(forKey: "k1lo_native_manualHour") as? Double ?? 13.25
        let normalizedHour = hour.truncatingRemainder(dividingBy: 24)
        let wrappedHour = normalizedHour < 0 ? normalizedHour + 24 : normalizedHour
        return wrappedHour >= 6 && wrappedHour < 19
    }
}

enum K1L0NativeAPI {
    static let candidates = [
        "https://api-tunnel.kilo.gallery",
        "http://192.168.40.34:3000",
        "https://api.kilomeme.com",
    ]

    // Preset files are an authoring surface, not ordinary gameplay data.
    // Publish only through a direct private-LAN/localhost connection; the API
    // deliberately refuses unauthenticated writes arriving through Cloudflare.
    private static let presetWriterCandidates = [
        "http://fred.local:3000",
        "http://192.168.40.34:3000",
        "http://127.0.0.1:3000",
    ]

    static func resolve(completion: @escaping (String) -> Void) {
        test(candidates, at: 0, fallback: candidates[0]) { resolved in
            completion(resolved ?? candidates[0])
        }
    }

    static func resolvePresetWriter(completion: @escaping (String?) -> Void) {
        test(presetWriterCandidates, at: 0, fallback: nil, completion: completion)
    }

    private static func test(_ options: [String], at index: Int, fallback: String?, completion: @escaping (String?) -> Void) {
        guard index < options.count else {
            completion(fallback)
            return
        }
        let candidate = options[index]
        guard let url = URL(string: "\(candidate)/health") else {
            test(options, at: index + 1, fallback: fallback, completion: completion)
            return
        }
        var request = URLRequest(url: url, timeoutInterval: candidate.hasPrefix("http://") ? 3 : 8)
        request.httpMethod = "GET"
        URLSession.shared.dataTask(with: request) { _, response, _ in
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            DispatchQueue.main.async {
                if code == 200 {
                    completion(candidate)
                } else {
                    test(options, at: index + 1, fallback: fallback, completion: completion)
                }
            }
        }.resume()
    }

    static func currentUserId() -> String? {
        let defaults = UserDefaults.standard
        for key in ["FirebaseUserId", "K1L0UserId", "DeviceID", "deviceID"] {
            let value = defaults.string(forKey: key)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            if !value.isEmpty { return value }
        }
        return nil
    }
}

final class K1L0AuthGateStore: ObservableObject {
    static let shared = K1L0AuthGateStore()

    @Published var userId = ""
    @Published var displayName = ""
    @Published var email = ""
    @Published var isAuthenticated = false
    @Published var status = "sign in to sync your identity, transmissions, and artifacts."

    private init() {
        loadCached()
    }

    func loadCached() {
        let defaults = UserDefaults.standard
        userId = K1L0NativeAPI.currentUserId() ?? ""
        displayName = defaults.string(forKey: "FirebaseDisplayName")?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        email = defaults.string(forKey: "FirebaseEmail")?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        isAuthenticated = !userId.isEmpty
        status = isAuthenticated ? "signed in." : "sign in to sync your identity, transmissions, and artifacts."
    }

    func handle(_ json: String) {
        guard let data = json.data(using: .utf8),
              let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            status = json.isEmpty ? "auth update failed." : json
            return
        }
        let nextUserId = (obj["userId"] as? String)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let nextDisplayName = (obj["displayName"] as? String)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let nextEmail = (obj["email"] as? String)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let nextAuthenticated = (obj["isAuthenticated"] as? Bool) ?? !nextUserId.isEmpty
        let nextStatus = (obj["status"] as? String)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""

        userId = nextUserId
        displayName = nextDisplayName
        email = nextEmail
        isAuthenticated = nextAuthenticated && !nextUserId.isEmpty
        status = nextStatus.isEmpty ? (isAuthenticated ? "signed in." : "signed out.") : nextStatus

        let defaults = UserDefaults.standard
        if isAuthenticated {
            defaults.set(nextUserId, forKey: "FirebaseUserId")
            defaults.set(nextUserId, forKey: "K1L0UserId")
            if !nextDisplayName.isEmpty { defaults.set(nextDisplayName, forKey: "FirebaseDisplayName") }
            if !nextEmail.isEmpty { defaults.set(nextEmail, forKey: "FirebaseEmail") }
        } else {
            defaults.removeObject(forKey: "FirebaseUserId")
            defaults.removeObject(forKey: "K1L0UserId")
            defaults.removeObject(forKey: "FirebaseDisplayName")
            defaults.removeObject(forKey: "FirebaseEmail")
        }
    }

    func useLocalDevSession(isMac: Bool) {
        let defaults = UserDefaults.standard
        userId = "8dbw"
        displayName = "Fred"
        email = isMac ? "dev@k1l0.local" : "ios-dev@k1l0.local"
        isAuthenticated = true
        status = isMac ? "local mac dev session." : "local ios dev session."
        defaults.set(userId, forKey: "FirebaseUserId")
        defaults.set(userId, forKey: "K1L0UserId")
        defaults.set(displayName, forKey: "FirebaseDisplayName")
        defaults.set(email, forKey: "FirebaseEmail")
        if let model = K1L0OverlayDataModel.activeModel {
            model.locationPermissionReady = true
            model.motionPermissionReady = true
        }
    }

    #if os(macOS)
    func useLocalMacDevSession() {
        useLocalDevSession(isMac: true)
    }
    #else
    func useLocalIOSDevSession() {
        useLocalDevSession(isMac: false)
    }
    #endif
}

// Swift -> Unity bridge.
//
// iOS: the player exports the C symbol `UnitySendMessage`, so we bind it directly.
// macOS standalone: that symbol is NOT exported by the player, so instead C# registers
// a callback function pointer (the standard desktop-plugin pattern) via
// K1L0SetUnityCallback, and our UnitySendMessage shim forwards to it. Every call site
// below stays identical across platforms.
#if os(iOS)
@_silgen_name("UnitySendMessage")
func UnitySendMessage(_ objectName: UnsafePointer<CChar>, _ methodName: UnsafePointer<CChar>, _ message: UnsafePointer<CChar>)
#else
public typealias K1L0UnityMessageCallback = @convention(c) (UnsafePointer<CChar>?, UnsafePointer<CChar>?, UnsafePointer<CChar>?) -> Void
var k1l0UnityCallback: K1L0UnityMessageCallback?

@_cdecl("K1L0SetUnityCallback")
public func K1L0SetUnityCallback(_ callback: K1L0UnityMessageCallback?) {
    k1l0UnityCallback = callback
}

func UnitySendMessage(_ objectName: UnsafePointer<CChar>, _ methodName: UnsafePointer<CChar>, _ message: UnsafePointer<CChar>) {
    k1l0UnityCallback?(objectName, methodName, message)
}
#endif

@_cdecl("K1L0InstallWeatherOverlay")
public func K1L0InstallWeatherOverlay() {
    DispatchQueue.main.async {
        K1L0NativeSettingsDefaults.register()
        K1L0WeatherOverlayInstaller.install()
    }
}

extension Notification.Name {
    static let k1l0RemoteWeatherLook = Notification.Name("K1L0RemoteWeatherLook")
}

// The live render-tuning bridge calls this with the same identifiers used by
// the weather action sheet.
// Posting into SwiftUI deliberately exercises the same selectWeatherLook path as the
// weather action sheet instead of maintaining a second remote-only preset path.
@_cdecl("K1L0SetWeatherLookMode")
public func K1L0SetWeatherLookMode(_ modePtr: UnsafePointer<CChar>?) {
    guard let modePtr else { return }
    let mode = String(cString: modePtr)
    guard ["auto", "day", "night"].contains(mode) else { return }
    DispatchQueue.main.async {
        NotificationCenter.default.post(name: .k1l0RemoteWeatherLook, object: mode)
    }
}

@_cdecl("K1L0CurrentNativeLocationModeJson")
public func K1L0CurrentNativeLocationModeJson() -> UnsafeMutablePointer<CChar>? {
    let mode = UserDefaults.standard.string(forKey: NativeLocationPreset.storageKey) ?? NativeLocationPreset.liveId
    var payload: [String: Any] = ["mode": mode, "liveGps": mode == NativeLocationPreset.liveId]
    if let preset = NativeLocationPreset.preset(for: mode) {
        payload["name"] = preset.title
        payload["latitude"] = preset.latitude
        payload["longitude"] = preset.longitude
    }
    guard let data = try? JSONSerialization.data(withJSONObject: payload),
          let json = String(data: data, encoding: .utf8)
    else { return nil }
    return strdup(json)
}

// Receives a finished transmission result from Unity (TransmissionManager.OnTransmissionReady).
// Payload is JSON: {"status":"...", "imageUrl":"...", "videoUrl":"...", "audioUrl":"...", "lyrics":"...", "responseOptions":[...]}.
// The first three already come back from /api/k1l0/v2/transmit today; `lyrics` is forwards-compatible
// for when the backend starts emitting it. Empty strings render as "not yet available" in the sheet.
@_cdecl("K1L0DeliverTransmissionResult")
public func K1L0DeliverTransmissionResult(_ jsonPtr: UnsafePointer<CChar>?) {
    guard let jsonPtr else { return }
    let json = String(cString: jsonPtr)
    DispatchQueue.main.async {
        K1L0TransmissionResultStore.shared.handle(json)
    }
}

@_cdecl("K1L0DeliverUserMetadataSaveResult")
public func K1L0DeliverUserMetadataSaveResult(_ jsonPtr: UnsafePointer<CChar>?) {
    guard let jsonPtr else { return }
    let json = String(cString: jsonPtr)
    DispatchQueue.main.async {
        K1L0UserMetadataSaveStore.shared.handle(json)
    }
}

@_cdecl("K1L0DeliverNativeAuthState")
public func K1L0DeliverNativeAuthState(_ jsonPtr: UnsafePointer<CChar>?) {
    let json = jsonPtr.map { String(cString: $0) } ?? ""
    DispatchQueue.main.async {
        K1L0AuthGateStore.shared.handle(json)
    }
}

@_cdecl("K1L0SetEnvironmentState")
public func K1L0SetEnvironmentState(_ jsonPtr: UnsafePointer<CChar>?) {
    guard let jsonPtr else { return }
    let json = String(cString: jsonPtr)
    DispatchQueue.main.async {
        K1L0OverlayDataModel.activeModel?.handleEnvironmentState(json)
    }
}

@_cdecl("K1L0DeliverStepState")
public func K1L0DeliverStepState(_ jsonPtr: UnsafePointer<CChar>?) {
    guard let jsonPtr else { return }
    let json = String(cString: jsonPtr)
    DispatchQueue.main.async {
        K1L0OverlayDataModel.activeModel?.handleUnityStepState(json)
    }
}

@_cdecl("K1L0DeliverLocationPresence")
public func K1L0DeliverLocationPresence(_ jsonPtr: UnsafePointer<CChar>?) {
    guard let jsonPtr else { return }
    let json = String(cString: jsonPtr)
    DispatchQueue.main.async {
        K1L0OverlayDataModel.activeModel?.handleUnityLocationPresence(json)
    }
}

@_cdecl("K1L0DeliverFloatingItemTap")
public func K1L0DeliverFloatingItemTap(_ jsonPtr: UnsafePointer<CChar>?) {
    guard let jsonPtr else { return }
    let json = String(cString: jsonPtr)
    DispatchQueue.main.async {
        K1L0OverlayDataModel.activeModel?.handleUnityFloatingItemTap(json)
    }
}

@_cdecl("K1L0DeliverAmbientSpawnPlacement")
public func K1L0DeliverAmbientSpawnPlacement(_ jsonPtr: UnsafePointer<CChar>?) {
    guard let jsonPtr else { return }
    let json = String(cString: jsonPtr)
    DispatchQueue.main.async {
        K1L0OverlayDataModel.activeModel?.handleAmbientSpawnPlacement(json)
    }
}

@_cdecl("K1L0DeliverPerfStats")
public func K1L0DeliverPerfStats(_ jsonPtr: UnsafePointer<CChar>?) {
    guard let jsonPtr else { return }
    let json = String(cString: jsonPtr)
    DispatchQueue.main.async {
        K1L0PerfStatsStore.shared.handle(json)
    }
}

final class K1L0PerfStatsStore: NSObject, ObservableObject {
    static let shared = K1L0PerfStatsStore()

    @Published private(set) var fps: Double = 0
    @Published private(set) var frameMs: Double = 0
    @Published private(set) var nativeFps: Double = 0
    @Published private(set) var allocMB: Int = 0
    @Published private(set) var reservedMB: Int = 0
    @Published private(set) var thermal: String = "..."
    @Published private(set) var batteryPct: Double = -1
    @Published private(set) var batteryDrainPctPerHour: Double = 0
    @Published private(set) var processCpuPct: Double = 0
    @Published private(set) var videoPlaybackActive = false
    @Published private(set) var renderDebug: [String: Any] = [:]
    @Published private(set) var updatedAt: Date?
    private var lastRemoteMetricsUpload = Date.distantPast

#if canImport(UIKit)
    private var displayLink: CADisplayLink?
    private var nativeFrameCount = 0
    private var nativeLastSampleTime = CACurrentMediaTime()
    private var nativeBatteryStartPct: Double = -1
    private var nativeBatteryStartTime = CACurrentMediaTime()
    private var nativeLastCPUSeconds: Double = 0
    private var nativeLastCPUTime = CACurrentMediaTime()
#endif

    override private init() {
        super.init()
    }

    var isFresh: Bool {
        guard let updatedAt else { return false }
        return Date().timeIntervalSince(updatedAt) < 4
    }

    var drainDisplayText: String {
#if canImport(UIKit)
        if UIDevice.current.batteryState == .charging || UIDevice.current.batteryState == .full { return "CHARGING" }
        let elapsed = max(0, CACurrentMediaTime() - nativeBatteryStartTime)
        if elapsed <= 120 { return "MEASURING \(Int(elapsed))s" }
#endif
        return batteryDrainPctPerHour > 0 ? String(format: "%.1f%%/hr", batteryDrainPctPerHour) : "WAIT 1%"
    }

    func setVideoPlaybackActive(_ active: Bool) {
        videoPlaybackActive = active
#if canImport(UIKit)
        if #available(iOS 15.0, *) {
            displayLink?.preferredFrameRateRange = CAFrameRateRange(minimum: active ? 1 : 30, maximum: active ? 1 : 30, preferred: active ? 1 : 30)
        } else {
            displayLink?.preferredFramesPerSecond = active ? 1 : 30
        }
#endif
    }

    func startNativeSampling() {
#if canImport(UIKit)
        UIDevice.current.isBatteryMonitoringEnabled = true
        if nativeBatteryStartPct < 0 {
            nativeBatteryStartPct = Double(UIDevice.current.batteryLevel) * 100.0
            nativeBatteryStartTime = CACurrentMediaTime()
        }
        guard displayLink == nil else { return }
        nativeLastSampleTime = CACurrentMediaTime()
        nativeFrameCount = 0
        let link = CADisplayLink(target: self, selector: #selector(nativeDisplayTick(_:)))
        if #available(iOS 15.0, *) {
            link.preferredFrameRateRange = CAFrameRateRange(minimum: 1, maximum: 10, preferred: 10)
        } else {
            link.preferredFramesPerSecond = 10
        }
        link.add(to: .main, forMode: .common)
        displayLink = link
#endif
    }

    func handle(_ json: String) {
#if canImport(UIKit)
        // Perf stats arrive even when the settings sheet has never been opened.
        // Start native CPU/thermal sampling here so remote diagnostics do not
        // depend on presenting that UI.
        startNativeSampling()
#endif
        guard let data = json.data(using: .utf8),
              let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return
        }
        fps = (root["fps"] as? Double) ?? fps
        frameMs = (root["frameMs"] as? Double) ?? frameMs
        allocMB = (root["allocMB"] as? Int) ?? Int((root["allocMB"] as? Double) ?? Double(allocMB))
        reservedMB = (root["reservedMB"] as? Int) ?? Int((root["reservedMB"] as? Double) ?? Double(reservedMB))
        thermal = (root["thermal"] as? String) ?? thermal
        batteryPct = (root["batteryPct"] as? Double) ?? batteryPct
        batteryDrainPctPerHour = (root["batteryDrainPctPerHour"] as? Double) ?? batteryDrainPctPerHour
        renderDebug = (root["render"] as? [String: Any]) ?? renderDebug
        updatedAt = Date()
    }

    var renderDebugSummary: String {
        guard !renderDebug.isEmpty else { return "..." }
        let center = (renderDebug["buildingCenter"] as? String) ?? "?"
        let loaded = renderDebug["buildingLoaded"] as? Int ?? Int((renderDebug["buildingLoaded"] as? Double) ?? 0)
        let requesting = renderDebug["buildingRequesting"] as? Int ?? Int((renderDebug["buildingRequesting"] as? Double) ?? 0)
        let last = (renderDebug["lastBuilding"] as? String) ?? ""
        return "bldg \(center) \(loaded)L/\(requesting)Q \(last)"
    }

#if canImport(UIKit)
    @objc private func nativeDisplayTick(_ link: CADisplayLink) {
        nativeFrameCount += 1
        let now = CACurrentMediaTime()
        let elapsed = now - nativeLastSampleTime
        guard elapsed >= 1.0 else { return }

        nativeFps = Double(nativeFrameCount) / elapsed
        allocMB = Self.currentResidentMemoryMB()
        reservedMB = allocMB
        thermal = Self.currentThermalLabel()
        let cpuSeconds = Self.currentProcessCPUSeconds()
        let cpuElapsed = max(0.001, now - nativeLastCPUTime)
        if nativeLastCPUSeconds > 0 {
            processCpuPct = max(0, (cpuSeconds - nativeLastCPUSeconds) / cpuElapsed * 100.0)
        }
        nativeLastCPUSeconds = cpuSeconds
        nativeLastCPUTime = now

        let rawBattery = UIDevice.current.batteryLevel
        if rawBattery >= 0 {
            let pct = Double(rawBattery) * 100.0
            batteryPct = pct
            let batteryElapsed = max(0, now - nativeBatteryStartTime)
            if nativeBatteryStartPct >= 0, batteryElapsed > 120 {
                batteryDrainPctPerHour = max(0, (nativeBatteryStartPct - pct) / batteryElapsed * 3600.0)
            }
        }

        updatedAt = Date()
        uploadRemoteMetricsIfNeeded()
        nativeFrameCount = 0
        nativeLastSampleTime = now
    }

    private func uploadRemoteMetricsIfNeeded() {
        let now = Date()
        guard now.timeIntervalSince(lastRemoteMetricsUpload) >= 5 else { return }
        lastRemoteMetricsUpload = now
        guard let url = URL(string: "https://api-tunnel.kilo.gallery/api/k1l0/render-metrics") else { return }
        var payload: [String: Any] = [
            "fps": fps,
            "frameMs": frameMs,
            "nativeFps": nativeFps,
            "allocMB": allocMB,
            "reservedMB": reservedMB,
            "thermal": thermal,
            "batteryPct": batteryPct,
            "batteryDrainPctPerHour": batteryDrainPctPerHour,
            "processCpuPct": processCpuPct,
            "videoPlaybackActive": videoPlaybackActive
        ]
        if !renderDebug.isEmpty { payload["render"] = renderDebug }
        guard let body = try? JSONSerialization.data(withJSONObject: payload) else { return }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = body
        request.timeoutInterval = 4
        URLSession.shared.dataTask(with: request).resume()
    }

    private static func currentThermalLabel() -> String {
        switch ProcessInfo.processInfo.thermalState {
        case .nominal: return "NOMINAL"
        case .fair: return "FAIR"
        case .serious: return "SERIOUS"
        case .critical: return "CRITICAL"
        @unknown default: return "UNKNOWN"
        }
    }

    private static func currentProcessCPUSeconds() -> Double {
        var usage = rusage()
        guard getrusage(RUSAGE_SELF, &usage) == 0 else { return 0 }
        let user = Double(usage.ru_utime.tv_sec) + Double(usage.ru_utime.tv_usec) / 1_000_000
        let system = Double(usage.ru_stime.tv_sec) + Double(usage.ru_stime.tv_usec) / 1_000_000
        return user + system
    }

    private static func currentResidentMemoryMB() -> Int {
        var info = mach_task_basic_info()
        var count = mach_msg_type_number_t(MemoryLayout<mach_task_basic_info>.size / MemoryLayout<natural_t>.size)
        let result = withUnsafeMutablePointer(to: &info) { pointer in
            pointer.withMemoryRebound(to: integer_t.self, capacity: Int(count)) { rebound in
                task_info(mach_task_self_, task_flavor_t(MACH_TASK_BASIC_INFO), rebound, &count)
            }
        }
        guard result == KERN_SUCCESS else { return 0 }
        return Int(info.resident_size / 1024 / 1024)
    }
#endif
}

struct K1L0TransmissionClip: Identifiable {
    let id = UUID()
    let videoURL: URL?
    let imageURL: URL?
    let audioURL: URL?
    let responsePlot: String
    let responseOptions: [String]
    let selectedResponse: String
    // Job identity of the slide, so replying targets THIS slide in the chain
    // instead of always the root transmission.
    var sourceJobId: String = ""
    var sourceUserId: String = ""
    var sourceName: String = ""
    var sourceCallsign: String = ""
    var sourceCity: String = ""
    var sourceCountry: String = ""
    var sourceCountryCode: String = ""
    var createdAt: Double = 0
    var allowsResponse: Bool = false
}

final class K1L0ActiveChainObserver: ObservableObject {
    // Polls the server thread endpoint. The earlier Firebase-SDK observer was
    // silently dead: the app bundles no GoogleService-Info.plist, so
    // Database.database() had no configuration to resolve.
    @Published private(set) var clips: [K1L0TransmissionClip] = []
    private var timer: Timer?
    private var rootJobId = ""
    private var inFlight = false

    func start(rootJobId: String) {
        let root = rootJobId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !root.isEmpty else { return }
        if self.rootJobId == root, timer != nil { return }
        stop()
        self.rootJobId = root
        fetchThread()
        timer = Timer.scheduledTimer(withTimeInterval: 5, repeats: true) { [weak self] _ in
            self?.fetchThread()
        }
    }

    private func fetchThread() {
        guard !inFlight, !rootJobId.isEmpty else { return }
        guard let userId = K1L0NativeAPI.currentUserId(), !userId.isEmpty else { return }
        inFlight = true
        let root = rootJobId
        K1L0NativeAPI.resolve { [weak self] apiBase in
            guard let self else { return }
            let encoded = userId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? userId
            guard let url = URL(string: "\(apiBase)/api/k1l0/v2/transmit/\(root)/thread?userId=\(encoded)") else {
                self.inFlight = false
                return
            }
            URLSession.shared.dataTask(with: URLRequest(url: url, timeoutInterval: 15)) { [weak self] data, _, _ in
                defer { self?.inFlight = false }
                guard let self, self.rootJobId == root,
                      let data,
                      let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                      (json["ok"] as? Bool) == true,
                      let items = json["items"] as? [[String: Any]] else { return }
                var rows: [K1L0TransmissionClip] = []
                for item in items {
                    func str(_ key: String) -> String { (item[key] as? String) ?? "" }
                    let clip = K1L0TransmissionClip(
                        videoURL: str("videoUrl").isEmpty ? nil : URL(string: str("videoUrl")),
                        imageURL: str("stillUrl").isEmpty ? nil : URL(string: str("stillUrl")),
                        audioURL: str("audioUrl").isEmpty ? nil : URL(string: str("audioUrl")),
                        responsePlot: str("responsePlot"),
                        responseOptions: (item["responseOptions"] as? [String]) ?? [],
                        selectedResponse: str("selectedResponse"),
                        sourceJobId: str("jobId"),
                        sourceUserId: str("userId"),
                        sourceName: str("sourceName"),
                        sourceCallsign: str("sourceCallsign"),
                        sourceCity: str("sourceCity"),
                        sourceCountry: str("sourceCountry"),
                        sourceCountryCode: str("sourceCountryCode"),
                        createdAt: k1l0NumericTimestamp(item["createdAt"]),
                        allowsResponse: false
                    )
                    if clip.videoURL != nil || clip.imageURL != nil { rows.append(clip) }
                }
                DispatchQueue.main.async {
                    guard self.rootJobId == root else { return }
                    let old = self.clips.map(\.sourceJobId)
                    if old != rows.map(\.sourceJobId) || self.clips.count != rows.count {
                        self.clips = rows
                    }
                }
            }.resume()
        }
    }

    func stop() {
        timer?.invalidate()
        timer = nil
        rootJobId = ""
        inFlight = false
        clips = []
    }

    deinit { timer?.invalidate() }
}

struct K1L0TransmissionResult: Identifiable {
    let id = UUID()
    let status: String
    let imageURL: URL?
    let videoURL: URL?
    let audioURL: URL?
    let lyrics: String
    let responsePlot: String
    let responseOptions: [String]
    var createdAt: Double? = nil
    var sourceUserId: String? = nil
    var jobId: String? = nil
    var rootJobId: String? = nil
    var clips: [K1L0TransmissionClip] = []
    var allowsResponseOptions = false
    var allowsTextResponse = false
    var selectedResponse: String? = nil

    var hasMedia: Bool {
        imageURL != nil || videoURL != nil || audioURL != nil || !clips.isEmpty
    }
}

final class K1L0UserMetadataSaveStore: ObservableObject {
    static let shared = K1L0UserMetadataSaveStore()

    @Published var status = "profile metadata is saved to your user record."
    @Published var savedSelfieURL = ""
    @Published var savedHelmetURL = ""
    @Published var savedCloakURL = ""
    @Published var savedAvatarURL = ""
    @Published var savedHelmetTextureURL = ""
    @Published var savedCloakTextureURL = ""
    @Published var loadedName = ""
    @Published var loadedCallsign = ""
    @Published var loadedBio = ""
    @Published var loadedUrl = ""
    @Published var loadedCloakDesign = ""
    @Published var loadedHelmetDesign = ""

    @Published var isSaving = false
    @Published var saveSuccessTrigger = false

    func beginSaving() {
        status = "saving user metadata..."
        isSaving = true
        saveSuccessTrigger = false
    }

    func loadFromBackend() {
        guard let userId = K1L0NativeAPI.currentUserId(), !userId.isEmpty else {
            status = "metadata load skipped: no user id."
            return
        }
        K1L0WeatherOverlayInstaller.sendNativeSessionState()
        K1L0NativeAPI.resolve { [weak self] apiBase in
            let safeUser = userId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? userId
            guard let url = URL(string: "\(apiBase)/api/k1l0/user/metadata?userId=\(safeUser)") else { return }
            URLSession.shared.dataTask(with: url) { data, _, _ in
                DispatchQueue.main.async {
                    guard let data, let json = String(data: data, encoding: .utf8) else {
                        self?.status = "metadata load failed."
                        return
                    }
                    self?.handle(json)
                }
            }.resume()
        }
    }

    func saveToBackend(json payload: String) {
        beginSaving()
        guard let userId = K1L0NativeAPI.currentUserId(), !userId.isEmpty else {
            status = "metadata save skipped: no user id."
            return
        }
        guard let data = payload.data(using: .utf8),
              let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            status = "metadata save payload failed."
            return
        }
        K1L0WeatherOverlayInstaller.sendNativeSessionState()
        K1L0NativeAPI.resolve { [weak self] apiBase in
            self?.saveParsedMetadata(root, userId: userId, apiBase: apiBase)
        }
    }

    private func saveParsedMetadata(_ root: [String: Any], userId: String, apiBase: String) {
        let selfiePath = (root["selfiePath"] as? String) ?? ""
        if !selfiePath.isEmpty {
            uploadSelfie(path: selfiePath, userId: userId, apiBase: apiBase) { [weak self] selfieUrl in
                self?.postMetadata(root, userId: userId, apiBase: apiBase, selfieUrl: selfieUrl)
            }
        } else {
            postMetadata(root, userId: userId, apiBase: apiBase, selfieUrl: nil)
        }
    }

    private func uploadSelfie(path: String, userId: String, apiBase: String, completion: @escaping (String?) -> Void) {
        guard let fileData = try? Data(contentsOf: URL(fileURLWithPath: path)) else {
            DispatchQueue.main.async {
                self.status = "selfie upload failed: file unreadable."
                completion(nil)
            }
            return
        }
        guard let url = URL(string: "\(apiBase)/api/k1l0/upload-image") else {
            completion(nil)
            return
        }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        let lower = path.lowercased()
        let contentType = lower.hasSuffix(".png") ? "image/png" : "image/jpeg"
        let body: [String: Any] = [
            "userId": userId,
            "filename": URL(fileURLWithPath: path).lastPathComponent,
            "contentType": contentType,
            "imageBase64": fileData.base64EncodedString()
        ]
        request.httpBody = try? JSONSerialization.data(withJSONObject: body)
        URLSession.shared.dataTask(with: request) { data, _, _ in
            let urlString: String? = {
                guard let data,
                      let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                      (obj["ok"] as? Bool) == true
                else { return nil }
                return obj["url"] as? String
            }()
            DispatchQueue.main.async {
                if urlString == nil { self.status = "selfie upload failed." }
                completion(urlString)
            }
        }.resume()
    }

    private func postMetadata(_ root: [String: Any], userId: String, apiBase: String, selfieUrl: String?) {
        guard let url = URL(string: "\(apiBase)/api/k1l0/user/metadata") else { return }
        var body: [String: Any] = [
            "userId": userId,
            "name": (root["name"] as? String) ?? "",
            "callsign": (root["callsign"] as? String) ?? "",
            "cloakDesign": (root["cloakDesign"] as? String) ?? "",
            "helmetDesign": (root["helmetDesign"] as? String) ?? ""
        ]
        if let selfieUrl, !selfieUrl.isEmpty {
            body["selfieUrl"] = selfieUrl
        }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: body)
        URLSession.shared.dataTask(with: request) { [weak self] data, _, _ in
            DispatchQueue.main.async {
                guard let self, let data, let json = String(data: data, encoding: .utf8) else {
                    self?.status = "metadata save failed."
                    return
                }
                self.handle(json)
                let effectiveSelfie = selfieUrl ?? self.savedSelfieURL
                self.renderIdentity(root, userId: userId, apiBase: apiBase, selfieUrl: effectiveSelfie)
            }
        }.resume()
    }

    private func renderIdentity(_ root: [String: Any], userId: String, apiBase: String, selfieUrl: String) {
        guard let url = URL(string: "\(apiBase)/api/k1l0/user/identity/render") else { return }
        status = "building identity..."
        let body: [String: Any] = [
            "userId": userId,
            "selfieUrl": selfieUrl,
            "name": (root["name"] as? String) ?? loadedName,
            "callsign": (root["callsign"] as? String) ?? loadedCallsign,
            "cloakDesign": (root["cloakDesign"] as? String) ?? loadedCloakDesign,
            "helmetDesign": (root["helmetDesign"] as? String) ?? loadedHelmetDesign
        ]
        if let textureURL = URL(string: "\(apiBase)/api/k1l0/user/identity/textures") {
            var textureRequest = URLRequest(url: textureURL)
            textureRequest.httpMethod = "POST"
            textureRequest.setValue("application/json", forHTTPHeaderField: "Content-Type")
            textureRequest.httpBody = try? JSONSerialization.data(withJSONObject: body)
            URLSession.shared.dataTask(with: textureRequest) { [weak self] data, _, _ in
                DispatchQueue.main.async {
                    guard let self, let data, let json = String(data: data, encoding: .utf8) else { return }
                    self.handle(json)
                }
            }.resume()
        }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: body)
        URLSession.shared.dataTask(with: request) { [weak self] data, _, _ in
            DispatchQueue.main.async {
                guard let self, let data, let json = String(data: data, encoding: .utf8) else {
                    self?.status = "identity render failed."
                    return
                }
                self.handle(json)
            }
        }.resume()
    }

    func handle(_ json: String) {
        guard let data = json.data(using: .utf8),
              let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            status = "metadata save response failed."
            isSaving = false
            saveSuccessTrigger = false
            return
        }
        let ok = (root["ok"] as? Bool) ?? false
        let error = (root["error"] as? String) ?? ""
        let statusText = (root["status"] as? String) ?? ""
        let selfieURL = (root["selfieUrl"] as? String) ?? ""
        let helmetURL = (root["helmetUrl"] as? String) ?? ""
        let cloakURL = (root["cloakUrl"] as? String) ?? ""
        let avatarURL = (root["avatarUrl"] as? String) ?? ""
        let helmetTextureURL = (root["helmetTextureUrl"] as? String) ?? ""
        let cloakTextureURL = (root["cloakTextureUrl"] as? String) ?? ""
        let skinRevision = (root["skinRevision"] as? Int) ?? 0
        let name = (root["name"] as? String) ?? ""
        let callsign = (root["callsign"] as? String) ?? ""
        let bio = (root["bio"] as? String) ?? ""
        let url = (root["url"] as? String) ?? ""
        let cloakDesign = (root["cloakDesign"] as? String) ?? ""
        let helmetDesign = (root["helmetDesign"] as? String) ?? ""
        if !selfieURL.isEmpty {
            savedSelfieURL = selfieURL
        }
        if !helmetURL.isEmpty { savedHelmetURL = helmetURL }
        if !cloakURL.isEmpty { savedCloakURL = cloakURL }
        if !avatarURL.isEmpty { savedAvatarURL = avatarURL }
        if !helmetTextureURL.isEmpty { savedHelmetTextureURL = helmetTextureURL }
        if !cloakTextureURL.isEmpty { savedCloakTextureURL = cloakTextureURL }
        if !name.isEmpty { loadedName = name }
        if !callsign.isEmpty { loadedCallsign = callsign }
        if !bio.isEmpty { loadedBio = bio }
        if !url.isEmpty { loadedUrl = url }
        if !cloakDesign.isEmpty { loadedCloakDesign = cloakDesign }
        if !helmetDesign.isEmpty { loadedHelmetDesign = helmetDesign }
        status = ok ? (statusText.isEmpty ? "user metadata saved." : statusText) : "metadata save failed\(error.isEmpty ? "." : ": \(error)")"

        if !ok {
            isSaving = false
            saveSuccessTrigger = false
        } else if statusText != "building identity..." {
            isSaving = false
            saveSuccessTrigger = true
        }

        // Bridge the resolved cloak/helmet textures to Unity so the 3D avatar
        // mirrors the user-panel design. Covers metadata load, save, and the
        // identity render response — whenever URLs are present, repaint meshes.
        // avatarURL is the cloak fallback in K1L0PlayerIdentitySkinApplier, so
        // include it in the trigger condition.
        if ok, !helmetURL.isEmpty || !cloakURL.isEmpty || !avatarURL.isEmpty || !helmetTextureURL.isEmpty || !cloakTextureURL.isEmpty {
            let skinPayload: [String: Any] = [
                "helmetUrl": helmetURL,
                "cloakUrl": cloakURL,
                "avatarUrl": avatarURL,
                "helmetTextureUrl": helmetTextureURL,
                "cloakTextureUrl": cloakTextureURL,
                "skinRevision": skinRevision,
                "helmetDesign": helmetDesign,
                "cloakDesign": cloakDesign
            ]
            if let skinData = try? JSONSerialization.data(withJSONObject: skinPayload),
               let skinJson = String(data: skinData, encoding: .utf8) {
                "K1L0HUD".withCString { objectName in
                    "ApplyNativeIdentitySkin".withCString { methodName in
                        skinJson.withCString { message in
                            UnitySendMessage(objectName, methodName, message)
                        }
                    }
                }
            }
        }
    }
}

final class K1L0TransmissionResultStore: ObservableObject {
    static let shared = K1L0TransmissionResultStore()

    @Published var current: K1L0TransmissionResult?

    func handle(_ json: String) {
        guard let data = json.data(using: .utf8),
              let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return }
        let status = (root["status"] as? String) ?? ""
        let imageURL = (root["imageUrl"] as? String).flatMap { URL(string: $0) }
        let videoURL = (root["videoUrl"] as? String).flatMap { URL(string: $0) }
        let audioURL = (root["audioUrl"] as? String).flatMap { URL(string: $0) }
        let lyrics = (root["lyrics"] as? String) ?? ""
        let responsePlot = ((root["responsePlot"] as? String) ?? "")
            .trimmingCharacters(in: .whitespacesAndNewlines)
        let responseOptions = (root["responseOptions"] as? [String] ?? [])
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
        let jobId = (root["jobId"] as? String) ?? ""
        let createdAt = [
            root["createdAt"],
            root["startedAt"],
            root["updatedAt"]
        ].map { k1l0NumericTimestamp($0) }.first { $0 > 0 }
        let isFailure = status.lowercased().contains("error") || status.lowercased().contains("failed")
        let isProgress = status.lowercased() == "queued" || status.lowercased().contains("building")

        // Drop pure-progress pings with no media at all so we only present once anything is ready to show.
        guard !status.isEmpty, isFailure || isProgress || imageURL != nil || videoURL != nil || audioURL != nil || !lyrics.isEmpty else { return }
        var result = K1L0TransmissionResult(status: status, imageURL: imageURL, videoURL: videoURL, audioURL: audioURL, lyrics: lyrics, responsePlot: responsePlot, responseOptions: responseOptions)
        result.createdAt = createdAt
        if !jobId.isEmpty {
            result.jobId = jobId
        }
        K1L0ActiveTransmissionStore.shared.apply(result)
        if !K1L0ActiveTransmissionStore.shared.snapshot.active {
            current = result
        }
    }

    func dismiss() { current = nil }
}

struct K1L0ActiveTransmissionSnapshot: Codable {
    var active: Bool = false
    var startedAt: TimeInterval = 0
    var photoPath: String = ""
    var message: String = ""
    var mood: String = "wired"
    var responsePlot: String = ""
    var imageUrl: String = ""
    var videoUrl: String = ""
    var audioUrl: String = ""
    var status: String = ""
    var error: String = ""
    var jobId: String = ""
    var createdAt: Double? = nil
    var responseOptions: [String] = []
    var inputImageUrl: String = ""
    var locationSummary: String = ""
    var weatherSummary: String = ""
    var photoPrompt: String = ""
    var videoPrompt: String = ""
    var musicPrompt: String = ""
    var lyrics: String = ""
}

func normalizedTransmissionOptions(_ options: [String], includeFallback: Bool = false) -> [String] {
    let fallbackCommands = ["follow the trail", "check the door", "bring the map", "leave a marker"]
    let forbiddenFragments = ["copy", "roger", "ten-four", "10-4", "signal", "stand by", "twenty", "coordinates", "scanning"]
    var cleaned = options
        .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
        .filter { !$0.isEmpty }
        .filter { option in
            let lower = option.lowercased()
            return !forbiddenFragments.contains(where: { lower.contains($0) })
        }
        // Collapse every "other" synonym to one canonical phrase so legacy
        // jobs carrying "do something else" don't render next to the client's
        // own "something else" fallback (the double-"other" bug).
        .map { ["other", "do something else", "something else"].contains($0.lowercased()) ? "something else" : $0 }
    var seen = Set<String>()
    cleaned = cleaned.filter { option in
        let key = option.lowercased()
        guard !seen.contains(key) else { return false }
        seen.insert(key)
        return true
    }
    // An explicit empty array means this viewer is the origin of the thread
    // unless the response player explicitly requests fallbacks. Legacy jobs
    // can contain four options that are ALL removed by the CB-language filter;
    // receivers must still get four usable actions in that case.
    if cleaned.isEmpty { return includeFallback ? fallbackCommands : [] }
    if includeFallback {
        let primary = cleaned.filter { $0.lowercased() != "something else" }
        var merged = Array(primary.prefix(4))
        for fallback in fallbackCommands where merged.count < 4 && !merged.contains(where: { $0.caseInsensitiveCompare(fallback) == .orderedSame }) {
            merged.append(fallback)
        }
        return Array(merged.prefix(4))
    }
    return Array(cleaned.prefix(4))
}

func k1l0NumericTimestamp(_ value: Any?) -> Double {
    if let value = value as? Double { return value }
    if let value = value as? Int { return Double(value) }
    if let value = value as? Int64 { return Double(value) }
    if let value = value as? String { return Double(value.trimmingCharacters(in: .whitespacesAndNewlines)) ?? 0 }
    return 0
}

func k1l0ReadableDateTime(_ raw: Double?) -> String {
    guard let raw, raw > 0 else { return "" }
    let seconds = raw > 9_999_999_999 ? raw / 1000.0 : raw
    let formatter = DateFormatter()
    formatter.dateStyle = .medium
    formatter.timeStyle = .short
    return formatter.string(from: Date(timeIntervalSince1970: seconds))
}

final class K1L0ActiveTransmissionStore: ObservableObject {
    static let shared = K1L0ActiveTransmissionStore()
    private let key = "k1lo_active_transmission_v1"
    private let canceledKey = "k1lo_canceled_transmission_job_ids_v1"

    @Published private(set) var snapshot: K1L0ActiveTransmissionSnapshot

    private init() {
        if let data = UserDefaults.standard.data(forKey: key),
           let saved = try? JSONDecoder().decode(K1L0ActiveTransmissionSnapshot.self, from: data) {
            snapshot = saved
        } else {
            snapshot = K1L0ActiveTransmissionSnapshot()
        }
    }

    func start(photoPath: String, message: String, mood: String, locationSummary: String = "", weatherSummary: String = "") {
        snapshot = K1L0ActiveTransmissionSnapshot(
            active: true,
            startedAt: Date().timeIntervalSince1970,
            photoPath: photoPath,
            message: message,
            mood: mood,
            responsePlot: "",
            imageUrl: "",
            videoUrl: "",
            status: "building",
            error: "",
            jobId: "",
            createdAt: Date().timeIntervalSince1970,
            responseOptions: [],
            locationSummary: locationSummary,
            weatherSummary: weatherSummary
        )
        persist()
    }

    func apply(_ result: K1L0TransmissionResult) {
        guard snapshot.active else { return }
        if let jobId = result.jobId, !jobId.isEmpty {
            snapshot.jobId = jobId
        }
        if let createdAt = result.createdAt, createdAt > 0 {
            snapshot.createdAt = createdAt
        }
        snapshot.status = result.status
        snapshot.responsePlot = result.responsePlot
        snapshot.imageUrl = result.imageURL?.absoluteString ?? snapshot.imageUrl
        snapshot.videoUrl = result.videoURL?.absoluteString ?? snapshot.videoUrl
        snapshot.audioUrl = result.audioURL?.absoluteString ?? snapshot.audioUrl
        if !result.lyrics.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            snapshot.lyrics = result.lyrics
        }
        snapshot.responseOptions = normalizedTransmissionOptions(result.responseOptions)
        if result.status.lowercased().contains("error") || result.status.lowercased().contains("failed") {
            snapshot.error = result.responsePlot.isEmpty ? result.status : result.responsePlot
        }
        persist()
    }

    func applyAudit(inputImageUrl: String = "", locationSummary: String = "", weatherSummary: String = "", photoPrompt: String = "", videoPrompt: String = "", musicPrompt: String = "", lyrics: String = "", createdAt: Double? = nil) {
        guard snapshot.active else { return }
        if let createdAt, createdAt > 0 { snapshot.createdAt = createdAt }
        if !inputImageUrl.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { snapshot.inputImageUrl = inputImageUrl }
        if !locationSummary.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { snapshot.locationSummary = locationSummary }
        if !weatherSummary.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { snapshot.weatherSummary = weatherSummary }
        if !photoPrompt.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { snapshot.photoPrompt = photoPrompt }
        if !videoPrompt.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { snapshot.videoPrompt = videoPrompt }
        if !musicPrompt.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { snapshot.musicPrompt = musicPrompt }
        if !lyrics.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { snapshot.lyrics = lyrics }
        persist()
    }

    func showLatest(message: String, mood: String, responsePlot: String, imageUrl: String, videoUrl: String, audioUrl: String, status: String, jobId: String, responseOptions: [String] = [], createdAt: Double? = nil) {
        snapshot = K1L0ActiveTransmissionSnapshot(
            active: true,
            startedAt: createdAt ?? Date().timeIntervalSince1970,
            photoPath: "",
            message: message,
            mood: mood,
            responsePlot: responsePlot,
            imageUrl: imageUrl,
            videoUrl: videoUrl,
            audioUrl: audioUrl,
            status: status,
            error: "",
            jobId: jobId,
            createdAt: createdAt,
            responseOptions: normalizedTransmissionOptions(responseOptions)
        )
        persist()
    }

    func setJobId(_ jobId: String) {
        guard snapshot.active else { return }
        snapshot.jobId = jobId
        persist()
    }

    func stop() {
        if !snapshot.jobId.isEmpty {
            var ids = canceledIds()
            ids.insert(snapshot.jobId)
            UserDefaults.standard.set(Array(ids), forKey: canceledKey)
        }
        snapshot = K1L0ActiveTransmissionSnapshot()
        persist()
    }

    func isCanceled(jobId: String) -> Bool {
        guard !jobId.isEmpty else { return false }
        return canceledIds().contains(jobId)
    }

    func updateResponsePlot(_ responsePlot: String) {
        guard snapshot.active else { return }
        snapshot.responsePlot = responsePlot.trimmingCharacters(in: .whitespacesAndNewlines)
        persist()
    }

    func clearCached() {
        snapshot = K1L0ActiveTransmissionSnapshot()
        UserDefaults.standard.removeObject(forKey: key)
    }

    func clearFinishedCached() {
        let status = snapshot.status.lowercased()
        let age = Date().timeIntervalSince1970 - snapshot.startedAt
        if snapshot.active && snapshot.jobId.isEmpty && (status.contains("error") || status.contains("failed") || age > 30 * 60) {
            clearCached()
        }
    }

    private func persist() {
        if let data = try? JSONEncoder().encode(snapshot) {
            UserDefaults.standard.set(data, forKey: key)
        }
    }

    private func canceledIds() -> Set<String> {
        Set(UserDefaults.standard.stringArray(forKey: canceledKey) ?? [])
    }
}

final class K1L0RadioPlayer: ObservableObject {
    static let shared = K1L0RadioPlayer()

    @Published private(set) var currentTrackURL = ""
    @Published private(set) var currentTrackPlot = ""
    @Published private(set) var status = "idle"

    private var player: AVPlayer?
    private var enabled = false
    private var suppressed = false
    private var apiBase: String?
    private var loading = false
    private var volume: Float = 0.55
    private var mode = "instrumental"

    private init() {
        registerLifecycleObservers()
    }

    private func registerLifecycleObservers() {
#if os(iOS)
        let nc = NotificationCenter.default
        nc.addObserver(forName: UIApplication.didBecomeActiveNotification, object: nil, queue: .main) { [weak self] _ in
            self?.reclaimSession()
        }
        nc.addObserver(forName: UIApplication.willEnterForegroundNotification, object: nil, queue: .main) { [weak self] _ in
            self?.reclaimSession()
        }
        nc.addObserver(forName: AVAudioSession.interruptionNotification, object: nil, queue: .main) { [weak self] note in
            guard let raw = note.userInfo?[AVAudioSessionInterruptionTypeKey] as? UInt,
                  let type = AVAudioSession.InterruptionType(rawValue: raw),
                  type == .ended else { return }
            self?.reclaimSession()
        }
        nc.addObserver(forName: AVAudioSession.mediaServicesWereResetNotification, object: nil, queue: .main) { [weak self] _ in
            guard let self else { return }
            self.player = nil
            self.currentTrackURL = ""
            self.currentTrackPlot = ""
            if self.enabled, !self.suppressed {
                self.configureAudioSession()
                self.loadNextTrack()
            }
        }
        nc.addObserver(forName: AVAudioSession.routeChangeNotification, object: nil, queue: .main) { [weak self] _ in
            self?.reclaimSession()
        }
#endif
    }

    private func reclaimSession() {
        guard enabled, !suppressed else { return }
        configureAudioSession()
        refreshOrResume()
    }

    // Called on foreground/interruption end/route change. If the player is
    // still healthy, resume; if it went to .failed while we were away, discard
    // it and pull a fresh track.
    private func refreshOrResume() {
        if let p = player, let item = p.currentItem, item.status != .failed {
            p.volume = volume
            p.play()
            status = "playing"
        } else {
            player = nil
            currentTrackURL = ""
            currentTrackPlot = ""
            loadNextTrack()
        }
    }

    func setEnabled(_ value: Bool, apiBase: String?) {
        enabled = value
        if let apiBase { self.apiBase = apiBase }
        if value { startIfNeeded() } else { stop(status: "off") }
    }

    func setSuppressed(_ value: Bool) {
        suppressed = value
        if value {
            player?.pause()
            status = "paused for transmission"
        } else if enabled {
            startIfNeeded()
        }
    }

    func setVolume(_ value: Double) {
        volume = Float(max(0, min(1, value)))
        player?.volume = volume
    }

    func setMode(_ value: String) {
        let nextMode = value == "instrumental" ? "instrumental" : "final"
        guard nextMode != mode else { return }
        mode = nextMode
        currentTrackURL = ""
        currentTrackPlot = ""
        if enabled {
            player?.pause()
            player = nil
            loadNextTrack()
        }
    }

    func resumeAfterForeground(apiBase: String?) {
        if let apiBase { self.apiBase = apiBase }
        guard enabled else { return }
        startIfNeeded()
    }

    private func startIfNeeded() {
        guard enabled, !suppressed else { return }
        configureAudioSession()
        if let player {
            player.volume = volume
            player.play()
            status = currentTrackURL.isEmpty ? "playing" : "playing"
            return
        }
        loadNextTrack()
    }

    private func stop(status newStatus: String = "idle") {
        player?.pause()
        player = nil
        currentTrackURL = ""
        currentTrackPlot = ""
        status = newStatus
    }

    private func loadNextTrack() {
        guard enabled, !suppressed, !loading else { return }
        loading = true
        let base = apiBase ?? "https://api-tunnel.kilo.gallery"
        guard let url = URL(string: "\(base)/api/k1l0/v2/transmit/radio?mode=\(mode)") else {
            loading = false
            status = "bad radio endpoint"
            return
        }
        DispatchQueue.main.async {
            self.status = "loading \(url.absoluteString)"
        }
        URLSession.shared.dataTask(with: url) { [weak self] data, _, _ in
            guard let self else { return }
            defer { self.loading = false }
            guard let data,
                  let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let ok = root["ok"] as? Bool,
                  ok,
                  let track = root["track"] as? [String: Any],
                  let raw = (track["radioUrl"] as? String)
                    ?? (self.mode == "instrumental" ? (track["instrumentalUrl"] as? String) : nil)
                    ?? (track["musicUrl"] as? String)
                    ?? (track["audioUrl"] as? String)
                    ?? (track["instrumentalUrl"] as? String),
                  let audioURL = URL(string: raw)
            else {
                DispatchQueue.main.async { self.status = "no radio track" }
                return
            }
            DispatchQueue.main.async {
                guard self.enabled, !self.suppressed else { return }
                let item = AVPlayerItem(url: audioURL)
                self.player = AVPlayer(playerItem: item)
                self.player?.volume = self.volume
                self.currentTrackURL = raw
                self.currentTrackPlot = ((track["responsePlot"] as? String) ?? "")
                    .trimmingCharacters(in: .whitespacesAndNewlines)
                self.status = "playing"
                NotificationCenter.default.addObserver(
                    forName: .AVPlayerItemDidPlayToEndTime,
                    object: item,
                    queue: .main
                ) { [weak self] _ in
                    self?.player = nil
                    self?.loadNextTrack()
                }
                self.player?.play()
            }
        }.resume()
    }

    // Returns a bulleted diagnostic string explaining why audio might not
    // be playing right now (or empty string if nothing looks wrong). Called
    // from the Music settings tab so the user isn't guessing.
    func diagnose() -> String {
        var lines: [String] = []
        if !enabled { lines.append("• Radio toggle is OFF (enable it above).") }
        if suppressed { lines.append("• Paused because a transmission is playing.") }
        if volume <= 0.001 { lines.append("• Radio volume slider is 0%.") }
        if enabled && !suppressed {
            if loading { lines.append("• Still loading track from the server…") }
            if apiBase == nil { lines.append("• No API base — waiting for connection.") }
            if let p = player {
                if let item = p.currentItem {
                    switch item.status {
                    case .failed:
                        let msg = item.error?.localizedDescription ?? "unknown"
                        lines.append("• Track failed to load: \(msg)")
                    case .unknown:
                        lines.append("• Track has not finished loading yet.")
                    case .readyToPlay:
                        if p.rate == 0 && p.timeControlStatus != .playing {
                            switch p.timeControlStatus {
                            case .paused:
                                lines.append("• Player is paused (nothing pressing it forward).")
                            case .waitingToPlayAtSpecifiedRate:
                                if let reason = p.reasonForWaitingToPlay {
                                    lines.append("• Waiting to play: \(reason.rawValue).")
                                } else {
                                    lines.append("• Waiting to play — likely buffering.")
                                }
                            default: break
                            }
                        }
                    @unknown default: break
                    }
                } else {
                    lines.append("• Player has no track item yet.")
                }
                if p.volume <= 0.001 { lines.append("• Player volume is 0.") }
            } else {
                lines.append("• No AVPlayer instance — track never got constructed.")
            }
        }
#if os(iOS)
        let session = AVAudioSession.sharedInstance()
        if session.outputVolume <= 0.001 {
            lines.append("• Device output volume is 0.")
        }
        if session.secondaryAudioShouldBeSilencedHint {
            lines.append("• Another app is playing audio; iOS is asking us to stay silent.")
        }
        if !session.isOtherAudioPlaying && !enabled {
            // just informational — already covered above.
        }
#endif
        return lines.joined(separator: "\n")
    }

    private func configureAudioSession() {
#if os(iOS)
        do {
            try AVAudioSession.sharedInstance().setCategory(.playback, mode: .default, options: [.mixWithOthers])
            try AVAudioSession.sharedInstance().setActive(true, options: [])
        } catch {
            status = "audio session failed"
            print("[K1L0Radio] audio session failed: \(error.localizedDescription)")
        }
#endif
    }
}

func K1L0CurrentSolarCoordinate() -> CLLocationCoordinate2D? {
    K1L0OverlayDataModel.activeModel?.solarCoordinate
}

func K1L0ApplyEnvironmentSnapshot(_ payload: [String: Any]) {
    K1L0WeatherOverlayInstaller.applyEnvironmentSnapshot(payload)
}

@_cdecl("K1L0DeliverRenderReadiness")
public func K1L0DeliverRenderReadiness(_ jsonPtr: UnsafePointer<CChar>?) {
    guard let jsonPtr else { return }
    let json = String(cString: jsonPtr)
    guard let data = json.data(using: .utf8),
          let payload = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return }
    DispatchQueue.main.async {
        let model = K1L0OverlayDataModel.activeModel
        model?.renderReady = payload["ready"] as? Bool ?? false
        let buildings = payload["buildings"] as? Int ?? 0
        let roads = payload["roads"] as? Int ?? 0
        let beamsReady = payload["beamsReady"] as? Bool ?? false
        model?.renderLoadingDetail = "buildings \(buildings) · roads \(roads) · beams \(beamsReady ? "ready" : "loading")"
        // The native map path does not currently call Unity's legacy road-tile
        // completion hook. Keep the useful readiness message during startup,
        // but never let that stale counter pin it onscreen indefinitely.
        if model?.renderReady == false {
            DispatchQueue.main.asyncAfter(deadline: .now() + 15) { [weak model] in
                if model?.renderReady == false { model?.renderReady = true }
            }
        }
    }
}

final class K1L0WeatherOverlayInstaller {
    private static var unityPlaybackPaused = false

    static func setUnityPlaybackPaused(_ paused: Bool) {
#if os(iOS)
        unityPlaybackPaused = paused
        K1L0UnityPause(paused ? 1 : 0)
#endif
    }

#if canImport(UIKit)
    private static weak var hostController: UIViewController?
    private static weak var hostView: UIView?
    private static weak var videoBackdropView: UIView?

    static func install() {
        K1L0NativeSettingsDefaults.register()
        guard hostController == nil else { return }
        guard let root = UIApplication.shared.connectedScenes
            .compactMap({ $0 as? UIWindowScene })
            .flatMap({ $0.windows })
            .first(where: { $0.isKeyWindow })?.rootViewController
        else {
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.4) { install() }
            return
        }

        let host = UIHostingController(rootView: K1L0WeatherOverlayRoot())
        host.view.backgroundColor = .clear
        host.view.isUserInteractionEnabled = true
        host.view.layer.zPosition = 9999
        host.view.translatesAutoresizingMaskIntoConstraints = false
        root.addChild(host)
        root.view.addSubview(host.view)
        NSLayoutConstraint.activate([
            host.view.leadingAnchor.constraint(equalTo: root.view.leadingAnchor),
            host.view.trailingAnchor.constraint(equalTo: root.view.trailingAnchor),
            host.view.topAnchor.constraint(equalTo: root.view.topAnchor),
            host.view.bottomAnchor.constraint(equalTo: root.view.bottomAnchor)
        ])
        host.didMove(toParent: root)
        hostController = host
        hostView = host.view
        keepOverlayInFront()
        sendNativeSessionState()
        suppressUnityHud()
        setNativeMapVisible(true)
        [0.5, 1.25, 2.5].forEach { delay in
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                setNativeMapVisible(true)
            }
        }
    }

    static func keepOverlayInFront() {
        guard let view = hostView, let superview = view.superview else { return }
        view.layer.zPosition = 9999
        superview.bringSubviewToFront(view)
        [0.25, 0.75, 1.5, 3.0].forEach { delay in
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                guard let view = hostView, let superview = view.superview else { return }
                view.layer.zPosition = 9999
                superview.bringSubviewToFront(view)
            }
        }
    }

    static func setVideoBackdropActive(_ active: Bool) {
#if canImport(UIKit)
        guard let host = hostView, let container = host.superview else { return }
        if active {
            let backdrop: UIView
            if let existing = videoBackdropView {
                backdrop = existing
            } else {
                backdrop = UIView(frame: .zero)
                backdrop.backgroundColor = .black
                backdrop.isUserInteractionEnabled = false
                backdrop.translatesAutoresizingMaskIntoConstraints = false
                container.insertSubview(backdrop, belowSubview: host)
                NSLayoutConstraint.activate([
                    backdrop.leadingAnchor.constraint(equalTo: container.leadingAnchor),
                    backdrop.trailingAnchor.constraint(equalTo: container.trailingAnchor),
                    backdrop.topAnchor.constraint(equalTo: container.topAnchor),
                    backdrop.bottomAnchor.constraint(equalTo: container.bottomAnchor)
                ])
                videoBackdropView = backdrop
            }
            backdrop.isHidden = false
            backdrop.backgroundColor = .black
            backdrop.layer.zPosition = 9998
            host.layer.zPosition = 9999
            container.bringSubviewToFront(host)
        } else {
            videoBackdropView?.isHidden = true
        }
#endif
    }
#elseif canImport(AppKit)
    static func setVideoBackdropActive(_ active: Bool) {
        // Unity remains the backdrop of the transparent child window on macOS.
        // Ordering the overlay forward is sufficient when video state changes.
        if active { keepOverlayInFront() }
    }

    // Host the SwiftUI overlay in a transparent child NSWindow ordered ABOVE the Unity
    // game window. A plain NSHostingView added as a sibling of Unity's CAMetalLayer-backed
    // content view gets composited UNDER the game render, so a child window is the reliable
    // way to guarantee the HUD draws on top (and it tracks the parent's frame).
    private static var overlayWindow: NSWindow?
    private static weak var parentWindow: NSWindow?
    private static var installed = false
    private static var frameObservers: [NSObjectProtocol] = []

    private static var statusItem: NSStatusItem?

    private static func setupStatusItem() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        if let button = item.button {
            button.image = NSImage(systemSymbolName: "antenna.radiowaves.left.and.right", accessibilityDescription: "K1L0")
        }

        let menu = NSMenu()
        menu.addItem(withTitle: "Show K1L0", action: #selector(K1L0StatusTarget.showApp), keyEquivalent: "s").target = K1L0StatusTarget.shared
        menu.addItem(withTitle: "Hide K1L0", action: #selector(K1L0StatusTarget.hideApp), keyEquivalent: "h").target = K1L0StatusTarget.shared
        menu.addItem(NSMenuItem.separator())
        menu.addItem(withTitle: "Quit K1L0", action: #selector(K1L0StatusTarget.quitApp), keyEquivalent: "q").target = K1L0StatusTarget.shared
        
        item.menu = menu
        statusItem = item
    }

    private static func gameWindow() -> NSWindow? {
        // The Unity player window: visible, has a content view, and isn't our overlay.
        if let main = NSApp.mainWindow, main !== overlayWindow, main.contentView != nil { return main }
        return NSApp.windows.first {
            $0 !== overlayWindow && $0.isVisible && $0.contentView != nil && $0.frame.width > 200
        }
    }

    static func install() {
        K1L0NativeSettingsDefaults.register()
        if let iconURL = Bundle.main.url(forResource: "PlayerIcon", withExtension: "icns"),
           let icon = NSImage(contentsOf: iconURL) {
            // Unity assigns its generic cube at runtime, overriding Info.plist.
            // Reassert K1L0's shared application art for the Dock and app switcher.
            NSApp.applicationIconImage = icon
        }
        guard !installed else { keepOverlayInFront(); return }
        guard let parent = gameWindow() else {
            NSLog("[K1L0Overlay] install: no Unity window yet, retrying")
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.4) { install() }
            return
        }

        let panel = K1L0OverlayWindow(
            contentRect: NSRect(origin: parent.frame.origin, size: parent.frame.size),
            styleMask: [.borderless],
            backing: .buffered,
            defer: false
        )
        panel.lockedFrame = parent.frame
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.isReleasedWhenClosed = false
        panel.collectionBehavior = [.fullScreenAuxiliary, .transient]
        let host = NSHostingView(rootView: K1L0WeatherOverlayRoot())
        host.frame = NSRect(origin: .zero, size: parent.frame.size)
        panel.contentView = host
        clampOverlayFrame(panel, to: parent)

        parent.addChildWindow(panel, ordered: .above)
        NSApp.activate(ignoringOtherApps: true)
        parent.makeKeyAndOrderFront(nil)
        panel.makeKeyAndOrderFront(nil)

        overlayWindow = panel
        parentWindow = parent
        installed = true
        NSLog("[K1L0Overlay] install: overlay child window attached (parent frame \(NSStringFromRect(parent.frame)))")
        setupStatusItem()

        let sync: (Notification) -> Void = { _ in syncFrame() }
        for name in [NSWindow.didResizeNotification, NSWindow.didMoveNotification] {
            frameObservers.append(NotificationCenter.default.addObserver(forName: name, object: parent, queue: .main, using: sync))
        }

        keepOverlayInFront()
        [0.1, 0.25, 0.5, 1.0, 2.0, 4.0, 8.0].forEach { delay in
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                syncFrame()
            }
        }
        sendNativeSessionState()
        suppressUnityHud()
        setNativeMapVisible(true)
        [0.5, 1.25, 2.5].forEach { delay in
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                setNativeMapVisible(true)
            }
        }
    }

    private static func syncFrame() {
        guard let overlayWindow, let parentWindow else { return }
        clampOverlayFrame(overlayWindow, to: parentWindow)
    }

    private static func clampOverlayFrame(_ overlay: NSWindow, to parent: NSWindow) {
        let frame = parent.frame
        if let locked = overlay as? K1L0OverlayWindow {
            locked.lockedFrame = frame
        }
        overlay.contentMinSize = frame.size
        overlay.contentMaxSize = frame.size
        overlay.setFrame(frame, display: true)
        overlay.contentView?.frame = NSRect(origin: .zero, size: frame.size)
    }

    static func keepOverlayInFront() {
        reattach()
        [0.25, 0.75, 1.5, 3.0].forEach { delay in
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) { reattach() }
        }
    }

    private static func reattach() {
        guard let panel = overlayWindow, let parent = parentWindow else { return }
        syncFrame()
        parent.addChildWindow(panel, ordered: .above)
        panel.makeKeyAndOrderFront(nil)
    }
#endif

    static func suppressUnityHud() {
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.0) {
            "K1L0HUD".withCString { objectName in
                "SetNativeOverlayMode".withCString { methodName in
                    "1".withCString { message in
                        UnitySendMessage(objectName, methodName, message)
                    }
                }
            }
        }
    }

    static func setNativeMapVisible(_ visible: Bool) {
        "K1L0HUD".withCString { objectName in
            "SetNativeMapVisible".withCString { methodName in
                (visible ? "1" : "0").withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func setUnitySetting(_ key: String, _ value: String) {
        "K1L0HUD".withCString { objectName in
            "SetNativeSetting".withCString { methodName in
                "\(key)=\(value)".withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func focusNativeBeam(_ signalId: String) {
        guard !signalId.isEmpty else { return }
        "K1L0HUD".withCString { objectName in
            "FocusNativeBeam".withCString { methodName in
                signalId.withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func applyNativeWorldNearby(_ json: String) {
        guard !json.isEmpty else { return }
        let wasPaused = unityPlaybackPaused
#if os(iOS)
        if wasPaused { K1L0UnityPause(0) }
#endif
        DispatchQueue.main.async {
            "K1L0HUD".withCString { objectName in
                "ApplyNativeWorldNearby".withCString { methodName in
                    json.withCString { message in
                        UnitySendMessage(objectName, methodName, message)
                    }
                }
            }
            if wasPaused {
                // Give Unity enough time to consume the native message and draw
                // the refreshed static place/beam frame, then sleep again.
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.12) {
                    guard unityPlaybackPaused else { return }
#if os(iOS)
                    K1L0UnityPause(1)
#endif
                }
            }
        }
    }

    static func applyNativeLocationCatalog(_ json: String) {
        guard !json.isEmpty else { return }
        "K1L0HUD".withCString { objectName in
            "ApplyNativeLocationCatalog".withCString { methodName in
                json.withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func requestAmbientSpawnPlacement(_ json: String) {
        guard !json.isEmpty else { return }
        "K1L0HUD".withCString { objectName in
            "RequestAmbientSpawnPlacement".withCString { methodName in
                json.withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func applyNativeLocationMode(_ json: String) {
        guard !json.isEmpty else { return }
        "K1L0HUD".withCString { objectName in
            "ApplyNativeLocationMode".withCString { methodName in
                json.withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func applyNativeSimulatedLocation(_ json: String) {
        guard !json.isEmpty else { return }
        "K1L0HUD".withCString { objectName in
            "ApplyNativeSimulatedLocation".withCString { methodName in
                json.withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func captureSnapshotForAnalysis() {
        dumpSettingsForAnalysis()
        "K1L0Screenshot".withCString { objectName in
            "Capture".withCString { methodName in
                "".withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func dumpSettingsForAnalysis() {
        let defaults = UserDefaults.standard
        var liveSettings: [String: Any] = [:]
        for (key, value) in defaults.dictionaryRepresentation() where key.hasPrefix("k1lo_native_") {
            liveSettings[key] = value
        }
        for (key, value) in K1L0NativeSettingsDefaults.values where liveSettings[key] == nil {
            liveSettings[key] = value
        }

        let payload: [String: Any] = [
            "kind": "native-settings-dump",
            "source": "snapshot-button",
            "timestamp": ISO8601DateFormatter().string(from: Date()),
            "settings": liveSettings,
            "compiledDefaults": K1L0NativeSettingsDefaults.values
        ]
        guard JSONSerialization.isValidJSONObject(payload),
              let body = try? JSONSerialization.data(withJSONObject: payload, options: [.prettyPrinted]) else {
            print("[K1L0Overlay] settings dump failed: invalid JSON")
            return
        }

        K1L0NativeAPI.resolve { base in
            guard let url = URL(string: "\(base)/beam-debug") else { return }
            var request = URLRequest(url: url, timeoutInterval: 8)
            request.httpMethod = "POST"
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            request.httpBody = body
            URLSession.shared.dataTask(with: request) { data, response, error in
                let code = (response as? HTTPURLResponse)?.statusCode ?? 0
                let text = data.flatMap { String(data: $0, encoding: .utf8) } ?? ""
                if let error {
                    print("[K1L0Overlay] settings dump upload failed: \(error.localizedDescription)")
                } else {
                    print("[K1L0Overlay] settings dump upload status=\(code) \(text)")
                }
            }.resume()
        }
    }

    static func sendNativeSessionState() {
        let defaults = UserDefaults.standard
        var userId = ""
        var email = ""
        var displayName = ""
        if userId.isEmpty {
            for key in ["FirebaseUserId", "K1L0UserId", "DeviceID", "deviceID"] {
                let value = defaults.string(forKey: key)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
                if !value.isEmpty {
                    userId = value
                    break
                }
            }
        }
        if email.isEmpty { email = defaults.string(forKey: "FirebaseEmail") ?? "" }
        if displayName.isEmpty { displayName = defaults.string(forKey: "FirebaseDisplayName") ?? "" }
        let deviceId = defaults.string(forKey: "DeviceID") ?? defaults.string(forKey: "deviceID") ?? ""
        let isAuthenticated = K1L0AuthGateStore.shared.isAuthenticated || !userId.isEmpty
        let payload: [String: Any] = [
            "userId": userId,
            "deviceId": deviceId,
            "email": email,
            "displayName": displayName,
            "isAuthenticated": isAuthenticated
        ]
        guard let data = try? JSONSerialization.data(withJSONObject: payload),
              let json = String(data: data, encoding: .utf8)
        else { return }

        "K1L0NativeSessionBridge".withCString { objectName in
            "ApplyNativeSessionState".withCString { methodName in
                json.withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func setNativePanelOpen(_ open: Bool) {
        "K1L0HUD".withCString { objectName in
            "SetNativePanelOpen".withCString { methodName in
                (open ? "1" : "0").withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func setSettingsPanelOpen(_ open: Bool) {
        "K1L0HUD".withCString { objectName in
            "SetSettingsPanelOpen".withCString { methodName in
                (open ? "1" : "0").withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func applyEnvironmentSnapshot(_ payload: [String: Any]) {
        guard let data = try? JSONSerialization.data(withJSONObject: payload),
              let json = String(data: data, encoding: .utf8) else { return }
        "K1L0HUD".withCString { objectName in
            "ApplyNativeEnvironment".withCString { methodName in
                json.withCString { UnitySendMessage(objectName, methodName, $0) }
            }
        }
    }

    static func playBeamCollectSound() {
        "K1L0HUD".withCString { objectName in
            "PlayNativeBeamCollectSound".withCString { methodName in
                "".withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func playSfxSlot(_ slot: String) {
        "K1L0HUD".withCString { objectName in
            "PlayNativeSfxSlot".withCString { methodName in
                slot.withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func beginNativeTransmission(_ payload: String) {
        "K1L0HUD".withCString { objectName in
            "BeginNativeTransmission".withCString { methodName in
                payload.withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func saveNativeUserMetadata(_ payload: String) {
        K1L0UserMetadataSaveStore.shared.saveToBackend(json: payload)
    }

    static func loadNativeUserMetadata() {
        K1L0UserMetadataSaveStore.shared.loadFromBackend()
    }

    static func beginNativeAppleSignIn() {
        K1L0AuthGateStore.shared.status = "opening apple sign in..."
        "K1L0HUD".withCString { objectName in
            "BeginNativeAppleSignIn".withCString { methodName in
                "".withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func logoutNativeSession() {
        K1L0AuthGateStore.shared.status = "signing out..."
        "K1L0HUD".withCString { objectName in
            "LogoutNativeSession".withCString { methodName in
                "".withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }
}


// Weather-automated look: unless Test Weather Override or Bypass Live Weather
// is on, the hand-tuned look sliders are ignored (and hidden in settings) —
// the curated defaults drive lighting/fog/buildings/ground/windows and the
// weather + sun system animates on top of them.
enum K1L0WeatherLook {
    static var manualLookActive: Bool {
        let defaults = UserDefaults.standard
        return (defaults.object(forKey: "k1lo_native_testSkyOverride") as? Bool ?? false)
            || defaults.bool(forKey: "k1lo_native_layeredBypassWeather")
    }

    static func double(_ key: String, _ fallback: Double) -> Double {
        // Fog values in UserDefaults are the last values stamped by the active
        // API preset (or the attached Fog panel). Always use those for fog so
        // this legacy environment sync cannot replace them with obsolete
        // compiled look defaults after Auto has already applied its recipe.
        if (manualLookActive || K1L0WeatherModeController.isFogSetting(
                key.hasPrefix("k1lo_native_") ? String(key.dropFirst("k1lo_native_".count)) : key
            )),
           let value = UserDefaults.standard.object(forKey: key) as? Double { return value }
        return (K1L0NativeSettingsDefaults.values[key] as? Double) ?? fallback
    }

    static func bool(_ key: String, _ fallback: Bool) -> Bool {
        if (manualLookActive || K1L0WeatherModeController.isFogSetting(
                key.hasPrefix("k1lo_native_") ? String(key.dropFirst("k1lo_native_".count)) : key
            )), let value = UserDefaults.standard.object(forKey: key) {
            if let boolValue = value as? Bool { return boolValue }
            if let numberValue = value as? NSNumber { return numberValue.boolValue }
        }
        return (K1L0NativeSettingsDefaults.values[key] as? Bool) ?? fallback
    }
}

enum NativeUnityLightingSync {
    static func sync() {
        let defaults = UserDefaults.standard
        let moonlightEnabled = K1L0WeatherLook.bool("k1lo_native_moonlightEnabled", true)
        let moonlightManualOverride = K1L0WeatherLook.bool("k1lo_native_moonlightManualOverride", false)
        let moonlightIntensity = K1L0WeatherLook.double("k1lo_native_moonlightIntensity", 0.55)
        let moonlightRed = K1L0WeatherLook.double("k1lo_native_moonlightRed", 0.7)
        let moonlightGreen = K1L0WeatherLook.double("k1lo_native_moonlightGreen", 0.8)
        let moonlightBlue = K1L0WeatherLook.double("k1lo_native_moonlightBlue", 1.0)
        let moonlightPitch = K1L0WeatherLook.double("k1lo_native_moonlightPitch", 90.0)
        let moonlightYaw = K1L0WeatherLook.double("k1lo_native_moonlightYaw", 0.0)
        let moonlightRoll = K1L0WeatherLook.double("k1lo_native_moonlightRoll", 0.0)
        let ambientEnabled = K1L0WeatherLook.bool("k1lo_native_ambientEnabled", false)
        let ambientIntensity = K1L0WeatherLook.double("k1lo_native_ambientIntensity", 0.0)
        var spotlightEnabled = K1L0WeatherLook.bool("k1lo_native_spotlightEnabled", true)
        var spotlightIntensity = K1L0WeatherLook.double("k1lo_native_spotlightIntensity", 3.0)

        if spotlightEnabled && spotlightIntensity <= 0.01 {
            spotlightIntensity = 1.0
            defaults.set(spotlightIntensity, forKey: "k1lo_native_spotlightIntensity")
        }
        if !spotlightEnabled {
            spotlightIntensity = max(0.0, spotlightIntensity)
        }

        K1L0WeatherOverlayInstaller.setUnitySetting("moonlightEnabled", moonlightEnabled ? "1" : "0")
        K1L0WeatherOverlayInstaller.setUnitySetting("moonlightManualOverride", moonlightManualOverride ? "1" : "0")
        K1L0WeatherOverlayInstaller.setUnitySetting("moonlightIntensity", String(format: "%.3f", moonlightIntensity))
        K1L0WeatherOverlayInstaller.setUnitySetting("moonlightRed", String(format: "%.3f", moonlightRed))
        K1L0WeatherOverlayInstaller.setUnitySetting("moonlightGreen", String(format: "%.3f", moonlightGreen))
        K1L0WeatherOverlayInstaller.setUnitySetting("moonlightBlue", String(format: "%.3f", moonlightBlue))
        K1L0WeatherOverlayInstaller.setUnitySetting("moonlightPitch", String(format: "%.3f", moonlightPitch))
        K1L0WeatherOverlayInstaller.setUnitySetting("moonlightYaw", String(format: "%.3f", moonlightYaw))
        K1L0WeatherOverlayInstaller.setUnitySetting("moonlightRoll", String(format: "%.3f", moonlightRoll))
        K1L0WeatherOverlayInstaller.setUnitySetting("ambientEnabled", ambientEnabled ? "1" : "0")
        K1L0WeatherOverlayInstaller.setUnitySetting("ambientIntensity", String(format: "%.3f", ambientIntensity))
        K1L0WeatherOverlayInstaller.setUnitySetting("spotlightEnabled", spotlightEnabled ? "1" : "0")
        K1L0WeatherOverlayInstaller.setUnitySetting("spotlightIntensity", String(format: "%.3f", spotlightIntensity))

        // Window glow is time-aware: saved sliders tune the daytime pink, while
        // nighttime gets a fixed warm gold so windows read like traditional light.
        let groundHueVal = K1L0WeatherLook.double("k1lo_native_groundHue", 0.33)
        let groundSatVal = K1L0WeatherLook.double("k1lo_native_groundSaturation", 0.42)
        K1L0WindowGlowResolver.apply()
        K1L0WeatherOverlayInstaller.setUnitySetting("groundHue", String(format: "%.3f", groundHueVal))
        K1L0WeatherOverlayInstaller.setUnitySetting("groundSaturation", String(format: "%.3f", groundSatVal))

        // Color grade + horizon fog — push the saved (or freshly migrated
        // "dystopian daylight") values so Unity's PlayerPrefs copy can't keep
        // an older look alive across app updates.
        let gradeKeys: [(unity: String, store: String, fallback: Double)] = [
            ("saturation", "k1lo_native_saturation", 0.0),
            ("contrast", "k1lo_native_contrast", 0.0),
            ("mapBrightness", "k1lo_native_mapBrightness", 0.0),
            ("hueShift", "k1lo_native_hueShift", 0.0),
            ("temperature", "k1lo_native_temperature", 0.0),
            ("tint", "k1lo_native_tint", 0.0),
            ("vignetteIntensity", "k1lo_native_vignetteIntensity", 0.45),
            ("chromaticIntensity", "k1lo_native_chromaticIntensity", 0.16),
            ("filmGrainIntensity", "k1lo_native_filmGrainIntensity", 0.4),
            ("dayBloomIntensity", "k1lo_native_dayBloomIntensity", 2.0),
        ]
        for entry in gradeKeys {
            let value = defaults.object(forKey: entry.store) as? Double ?? entry.fallback
            K1L0WeatherOverlayInstaller.setUnitySetting(entry.unity, String(format: "%.3f", value))
        }

        // Weather-look keys: hand-tuned values apply only in Test Override /
        // Bypass mode; automated mode force-pushes the curated defaults so a
        // previously saved manual look can't linger in Unity's PlayerPrefs.
        let lookKeys: [(unity: String, store: String, fallback: Double)] = [
            ("fogDensity", "k1lo_native_fogDensity", 0.01),
            ("fogNoiseStrength", "k1lo_native_fogNoiseStrength", 1.8),
            ("fogNoiseScale", "k1lo_native_fogNoiseScale", 21.0),
            ("fogScatteringIntensity", "k1lo_native_fogScatteringIntensity", 1.25),
            ("fogHeight", "k1lo_native_fogHeight", 86.0),
            ("fogDistantDensity", "k1lo_native_fogDistantDensity", 0.0045),
            ("fogDistantStart", "k1lo_native_fogDistantStart", 200.0),
            ("fogBrightness", "k1lo_native_fogBrightness", 0.81),
            ("fogNativeLightsMultiplier", "k1lo_native_fogNativeLightsMultiplier", 0.0),
            ("zossEmissiveIntensity", "k1lo_native_zossEmissiveIntensity", 19.0),
            ("zossEmissiveSmoothness", "k1lo_native_zossEmissiveSmoothness", 0.34),
            ("zossEmissiveMetallic", "k1lo_native_zossEmissiveMetallic", 0.0),
            ("zossWallValue", "k1lo_native_zossWallValue", 0.10),
            ("zossWallSaturation", "k1lo_native_zossWallSaturation", 0.30),
            ("zossLitFraction", "k1lo_native_zossLitFraction", 1.0),
            ("zossPaletteMix", "k1lo_native_zossPaletteMix", 1.0),
            ("zossPaletteSaturation", "k1lo_native_zossPaletteSaturation", 1.35),
            ("zossPaletteSaturation_night", "k1lo_native_zossPaletteSaturation_night", 1.22),
            ("zossWarmth", "k1lo_native_zossWarmth", 1.0),
            ("zossAccentFraction", "k1lo_native_zossAccentFraction", 0.08),
            ("zossWindowBrightness", "k1lo_native_zossWindowBrightness", 1.0),
            ("zossBrightnessJitter", "k1lo_native_zossBrightnessJitter", 0.5),
            ("zossBrightnessJitterRate", "k1lo_native_zossBrightnessJitterRate", 0.6),
            ("zossWallDaylightLift", "k1lo_native_zossWallDaylightLift", 0.55),
            ("zossWallVariance", "k1lo_native_zossWallVariance", 0.6),
            ("roadValue", "k1lo_native_roadValue", 0.88),
            ("vaporDayPink", "k1lo_native_vaporDayPink", 0.65),
            ("layeredNightBlackness", "k1lo_native_layeredNightBlackness", 0.60),
            ("layeredHorizonHeight", "k1lo_native_layeredHorizonHeight", 0.0),
        ]
        for entry in lookKeys {
            let value = K1L0WeatherLook.double(entry.store, entry.fallback)
            let precision = entry.unity.hasPrefix("fog") ? "%.6f" : "%.3f"
            K1L0WeatherOverlayInstaller.setUnitySetting(entry.unity, String(format: precision, value))
        }
        let grainOn = defaults.object(forKey: "k1lo_native_filmGrainEnabled") as? Bool ?? true
        K1L0WeatherOverlayInstaller.setUnitySetting("filmGrainEnabled", grainOn ? "1" : "0")
        K1L0WeatherOverlayInstaller.setUnitySetting("fogDistantFog", K1L0WeatherLook.bool("k1lo_native_fogDistantFog", true) ? "1" : "0")
        K1L0WeatherOverlayInstaller.setUnitySetting("fogNativeLights", K1L0WeatherLook.bool("k1lo_native_fogNativeLights", false) ? "1" : "0")

        // Sky Target FPS
        let skyTargetFps = defaults.object(forKey: "k1lo_native_skyTargetFps") as? Double ?? 30.0
        K1L0WeatherOverlayInstaller.setUnitySetting("skyTargetFps", String(format: "%.3f", skyTargetFps))

        // Transmission Fizzy Edges
        let transmissionFizzyEdgesVal = defaults.object(forKey: "k1lo_native_transmissionFizzyEdges") as? Bool ?? false
        K1L0WeatherOverlayInstaller.setUnitySetting("transmissionFizzyEdges", transmissionFizzyEdgesVal ? "1" : "0")

        // Night Fog & Ground values
        let fogDensityNight = K1L0WeatherLook.double("k1lo_native_fogDensity_night", 0.025)
        let fogNoiseStrengthNight = K1L0WeatherLook.double("k1lo_native_fogNoiseStrength_night", 0.22)
        let fogNoiseScaleNight = K1L0WeatherLook.double("k1lo_native_fogNoiseScale_night", 17.4)
        let fogBrightnessNight = K1L0WeatherLook.double("k1lo_native_fogBrightness_night", 0.24)
        let fogScatteringIntensityNight = K1L0WeatherLook.double("k1lo_native_fogScatteringIntensity_night", 0.55)
        let fogHeightNight = K1L0WeatherLook.double("k1lo_native_fogHeight_night", 48.0)
        let fogDistantDensityNight = K1L0WeatherLook.double("k1lo_native_fogDistantDensity_night", 0.0025)
        let fogDistantStartNight = K1L0WeatherLook.double("k1lo_native_fogDistantStart_night", 100.0)
        let groundHueNight = K1L0WeatherLook.double("k1lo_native_groundHue_night", 0.30)
        let groundSaturationNight = K1L0WeatherLook.double("k1lo_native_groundSaturation_night", 0.0)

        K1L0WeatherOverlayInstaller.setUnitySetting("fogDensity_night", String(format: "%.6f", fogDensityNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("fogNoiseStrength_night", String(format: "%.3f", fogNoiseStrengthNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("fogNoiseScale_night", String(format: "%.3f", fogNoiseScaleNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("fogBrightness_night", String(format: "%.3f", fogBrightnessNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("fogScatteringIntensity_night", String(format: "%.3f", fogScatteringIntensityNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("fogHeight_night", String(format: "%.3f", fogHeightNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("fogDistantDensity_night", String(format: "%.6f", fogDistantDensityNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("fogDistantStart_night", String(format: "%.6f", fogDistantStartNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("groundHue_night", String(format: "%.3f", groundHueNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("groundSaturation_night", String(format: "%.3f", groundSaturationNight))

        let manualWeather = defaults.object(forKey: "k1lo_native_manualWeather") as? Int ?? 0
        let manualHour = defaults.object(forKey: "k1lo_native_manualHour") as? Double ?? 13.25
        let testOverride = defaults.object(forKey: "k1lo_native_testSkyOverride") as? Bool ?? false
        K1L0WeatherOverlayInstaller.setUnitySetting("testSkyOverride", testOverride ? "1" : "0")
    }
}

/* Moved to K1L0SolarEnvironmentSync.swift to keep astronomy/sky iteration
   out of this large UI compilation unit.
enum NativeUnitySolarSync {
    private static var timer: Timer?

    static func start() {
        sync()
        guard timer == nil else { return }
        timer = Timer.scheduledTimer(withTimeInterval: 60, repeats: true) { _ in sync() }
    }

    static func sync() {
        guard let coordinate = K1L0OverlayDataModel.activeModel?.solarCoordinate else { return }
        let now = Date().timeIntervalSince1970 / 86400.0 + 2440587.5
        let n = now - 2451545.0
        let meanLongitude = (280.460 + 0.9856474 * n).truncatingRemainder(dividingBy: 360)
        let anomaly = (357.528 + 0.9856003 * n) * .pi / 180
        let eclipticLongitude = (meanLongitude + 1.915 * sin(anomaly) + 0.020 * sin(2 * anomaly)) * .pi / 180
        let obliquity = (23.439 - 0.0000004 * n) * .pi / 180
        let rightAscension = atan2(cos(obliquity) * sin(eclipticLongitude), cos(eclipticLongitude))
        let declination = asin(sin(obliquity) * sin(eclipticLongitude))
        let gmst = (280.46061837 + 360.98564736629 * (now - 2451545.0)).truncatingRemainder(dividingBy: 360)
        let hourAngle = (gmst + coordinate.longitude) * .pi / 180 - rightAscension
        let latitude = coordinate.latitude * .pi / 180
        let altitude = asin(sin(latitude) * sin(declination) + cos(latitude) * cos(declination) * cos(hourAngle))
        let azimuth = atan2(-sin(hourAngle), tan(declination) * cos(latitude) - sin(latitude) * cos(hourAngle))
        let altitudeDegrees = altitude * 180 / .pi
        let azimuthDegrees = (azimuth * 180 / .pi + 360).truncatingRemainder(dividingBy: 360)
        let defaults = UserDefaults.standard
        K1L0WeatherOverlayInstaller.applyEnvironmentSnapshot([
            "solarAltitude": altitudeDegrees,
            "solarAzimuth": azimuthDegrees,
            "bypassWeather": defaults.bool(forKey: "k1lo_native_layeredBypassWeather"),
            "effect": defaults.integer(forKey: "k1lo_native_layeredSkyEffect"),
            "cloudOpacity": defaults.object(forKey: "k1lo_native_layeredCloudOpacity") as? Double ?? 0.72,
            "cloudCoverage": defaults.object(forKey: "k1lo_native_layeredCloudCoverage") as? Double ?? 0.35,
            "cloudSpeed": defaults.object(forKey: "k1lo_native_layeredCloudSpeed") as? Double ?? 0.07,
            "cloudScale": defaults.object(forKey: "k1lo_native_layeredCloudScale") as? Double ?? 1.35,
            "cloudContrast": defaults.object(forKey: "k1lo_native_layeredCloudContrast") as? Double ?? 1.1,
            "topHue": defaults.object(forKey: "k1lo_native_layeredSkyTopHue") as? Double ?? 0.80,
            "midHue": defaults.object(forKey: "k1lo_native_layeredSkyMidHue") as? Double ?? 0.62,
            "horizonHue": defaults.object(forKey: "k1lo_native_layeredSkyHorizonHue") as? Double ?? 0.73,
            "nightBlackness": defaults.object(forKey: "k1lo_native_layeredNightBlackness") as? Double ?? 0.60,
            "rain": defaults.object(forKey: "k1lo_native_layeredRain") as? Double ?? 0,
            "aurora": defaults.object(forKey: "k1lo_native_layeredAurora") as? Double ?? 0
        ])
    }
} */

struct K1L0LoginPermissionGate: View {
    @ObservedObject var auth: K1L0AuthGateStore
    @ObservedObject var data: K1L0OverlayDataModel

    var body: some View {
        ZStack {
            Color.black
                .ignoresSafeArea()
            VStack(spacing: 18) {
                VStack(spacing: 8) {
                    Text("K1L0")
                        .font(.system(size: 56, weight: .black))
                        .foregroundStyle(.white)
                    Text("SIGNAL ACCESS")
                        .font(.system(size: 14, weight: .black, design: .monospaced))
                        .foregroundStyle(Color(red: 0.72, green: 1.0, blue: 0.68))
                }

                VStack(alignment: .leading, spacing: 12) {
                    gateRow(
                        title: auth.isAuthenticated ? "Apple ID connected" : "Sign in with Apple ID",
                        body: auth.isAuthenticated
                            ? {
                                let cs = K1L0UserMetadataSaveStore.shared.loadedCallsign
                                    .trimmingCharacters(in: .whitespacesAndNewlines)
                                if !cs.isEmpty { return cs }
                                return auth.displayName.isEmpty ? "identity is ready." : auth.displayName
                            }()
                            : "sync your avatar, transmissions, artifacts, and profile across devices.",
                        ready: auth.isAuthenticated
                    )
                    gateRow(
                        title: data.locationPermissionReady ? "GPS ready" : "Allow GPS",
                        body: data.locationPermissionText,
                        ready: data.locationPermissionReady
                    )
                    gateRow(
                        title: data.motionPermissionReady ? "Motion ready" : "Allow motion",
                        body: data.motionPermissionText,
                        ready: data.motionPermissionReady
                    )
                }
                .frame(maxWidth: .infinity, alignment: .leading)

                VStack(spacing: 10) {
                    if !auth.isAuthenticated {
                        Button {
                            K1L0WeatherOverlayInstaller.beginNativeAppleSignIn()
                        } label: {
                            Text("[ SIGN IN WITH APPLE ]")
                                .font(.system(size: 16, weight: .black, design: .monospaced))
                                .frame(maxWidth: .infinity, minHeight: 50)
                        }
                        .buttonStyle(.plain)
                        .foregroundStyle(.black)
                        .background(Color.white)
                        Button {
                            #if os(macOS)
                            auth.useLocalMacDevSession()
                            #else
                            auth.useLocalIOSDevSession()
                            #endif
                        } label: {
                            #if os(macOS)
                            Text("[ CONTINUE AS MAC DEV ]")
                                .font(.system(size: 16, weight: .black, design: .monospaced))
                                .frame(maxWidth: .infinity, minHeight: 50)
                            #else
                            Text("[ CONTINUE AS iOS DEV ]")
                                .font(.system(size: 16, weight: .black, design: .monospaced))
                                .frame(maxWidth: .infinity, minHeight: 50)
                            #endif
                        }
                        .buttonStyle(.plain)
                        .foregroundStyle(.white)
                        .overlay(Rectangle().stroke(Color.white.opacity(0.55), lineWidth: 1))
                    }

                    if !data.locationPermissionReady {
                        Button {
                            data.requestLocationPermissionFromGate()
                        } label: {
                            Text(data.locationPermissionDenied ? "[ ENABLE GPS IN SETTINGS ]" : "[ ALLOW GPS ]")
                                .font(.system(size: 16, weight: .black, design: .monospaced))
                                .frame(maxWidth: .infinity, minHeight: 50)
                        }
                        .buttonStyle(.plain)
                        .foregroundStyle(.white)
                        .overlay(Rectangle().stroke(Color.white.opacity(0.55), lineWidth: 1))
                    }

                    if !data.motionPermissionReady {
                        Button {
                            data.requestMotionPermissionFromGate()
                        } label: {
                            Text(data.motionPermissionDenied ? "[ ENABLE MOTION IN SETTINGS ]" : "[ ALLOW MOTION ]")
                                .font(.system(size: 16, weight: .black, design: .monospaced))
                                .frame(maxWidth: .infinity, minHeight: 50)
                        }
                        .buttonStyle(.plain)
                        .foregroundStyle(.white)
                        .overlay(Rectangle().stroke(Color.white.opacity(0.55), lineWidth: 1))
                    }
                }

                Text(auth.status)
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(.white.opacity(0.68))
                    .multilineTextAlignment(.center)
                    .frame(maxWidth: .infinity)
            }
            .padding(22)
            .frame(maxWidth: 430)
            .background(Color.black.opacity(0.56), in: RoundedRectangle(cornerRadius: 28, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 28, style: .continuous).stroke(Color.white.opacity(0.18), lineWidth: 1))
            .padding(.horizontal, 22)
        }
    }

    private func gateRow(title: String, body: String, ready: Bool) -> some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: ready ? "checkmark.circle.fill" : "exclamationmark.triangle.fill")
                .font(.system(size: 18, weight: .black))
                .foregroundStyle(ready ? Color(red: 0.72, green: 1.0, blue: 0.68) : Color(red: 1.0, green: 0.84, blue: 0.32))
                .frame(width: 24)
            VStack(alignment: .leading, spacing: 4) {
                Text(title)
                    .font(.system(size: 15, weight: .black))
                    .foregroundStyle(.white)
                Text(body)
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(.white.opacity(0.66))
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }
}

extension View {
    /// Break very long SwiftUI generic chains at feature boundaries. Besides
    /// making the shared overlay compile reliably as a standalone Mac module,
    /// this keeps later modifiers from re-type-checking the entire HUD tree.
    func k1l0TypeErased() -> AnyView { AnyView(self) }
}

struct K1L0WeatherPresetPickerModifier: ViewModifier {
    @Binding var isPresented: Bool
    let presets: [K1L0WeatherPresetDescriptor]
    let onSelect: (String) -> Void
    let onSettings: () -> Void

    @ViewBuilder
    func body(content: Content) -> some View {
        content.overlay {
            if isPresented {
                ZStack {
                    Color.black.opacity(0.38)
                        .ignoresSafeArea()
                        .onTapGesture { isPresented = false }
                    VStack(alignment: .leading, spacing: 10) {
                        Text("Weather Look")
                            .font(.system(size: 21, weight: .black))
                            .foregroundStyle(.white)
                        ScrollView {
                            VStack(spacing: 7) {
                                ForEach(presets) { preset in
                                    Button {
                                        isPresented = false
                                        onSelect(preset.id)
                                    } label: {
                                        HStack {
                                            Text(preset.label)
                                            Spacer()
                                            Image(systemName: "chevron.right")
                                                .opacity(0.45)
                                        }
                                        .font(.system(size: 16, weight: .bold))
                                        .foregroundStyle(.white)
                                        .padding(.horizontal, 14)
                                        .frame(height: 42)
                                        .background(Color.white.opacity(0.10), in: RoundedRectangle(cornerRadius: 10))
                                    }
                                    .buttonStyle(.plain)
                                }
                            }
                        }
                        .frame(maxHeight: 310)
                        Button("Settings") {
                            isPresented = false
                            onSettings()
                        }
                        .buttonStyle(.borderedProminent)
                    }
                    .padding(18)
                    .frame(width: 300)
                    .background(Color(red: 0.035, green: 0.055, blue: 0.10).opacity(0.97),
                                in: RoundedRectangle(cornerRadius: 18, style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: 18).stroke(Color.white.opacity(0.16)))
                    .shadow(color: .black.opacity(0.55), radius: 24, y: 10)
                }
                .zIndex(10000)
            }
        }
    }
}

struct K1L0WeatherOverlayRoot: View {
    @StateObject private var data = K1L0OverlayDataModel()
    @ObservedObject private var authGate = K1L0AuthGateStore.shared
    @ObservedObject private var transmissionResults = K1L0TransmissionResultStore.shared
    @ObservedObject private var activeTransmission = K1L0ActiveTransmissionStore.shared
    @State private var hudVisible = false
    @State private var showingSettings = false
    @State private var showingTransmission = false
    @State private var showingUserEditor = false
    @State private var showingMessages = false
    @AppStorage(K1L0OverlayDataModel.locationDropFilterKey) private var selectedDropFilter = "all"
    @AppStorage(K1L0OverlayDataModel.locationBeamCategoriesKey) private var selectedBeamCategories = "coffee,drinks,food"
    @State private var liveDropLimit = 5
    @State private var homeLocationsExpanded = false
    @State private var homeNearbyUsersExpanded = false
    @State private var leaderboardRange = "24h"
    @State private var selectedNearbyUser: OverlayUser?
    @State private var newsPullBaseline: CGFloat? = nil
    @State private var newsPullFired = false
    @State private var newsHourlyWalkHistory: [NativeWalkHistoryPoint] = []
    @State private var newsDailyWalkHistory: [NativeWalkHistoryPoint] = []
    @State private var unityPauseWorkItem: DispatchWorkItem?
#if os(iOS)
    @State private var newsWalkHistoryPedometer = CMPedometer()
#endif
    @AppStorage("k1lo_native_musicRadioEnabled") private var musicRadioEnabled = true
    @AppStorage("k1lo_native_musicRadioVolume") private var musicRadioVolume = 0.5415074229240417
    @AppStorage("k1lo_native_musicRadioMode") private var musicRadioMode = "instrumental"
    @AppStorage("k1lo_native_bottomMenuLayout") private var bottomMenuLayout = "tabs"
    @AppStorage("k1lo_native_statusHUD") private var statusHUD = false
    @AppStorage("k1lo_native_weatherLookMode") private var weatherLookMode = "auto"
    @State private var showingWeatherLookPicker = false
    @State private var weatherPresetCatalog = K1L0WeatherModeController.bundledDescriptors
    @State private var acceptedContactSignalId = ""
    @State private var showingLocationDwellDetail = false
    @Environment(\.scenePhase) private var scenePhase

    private var isVideoTransmissionPlaying: Bool {
        guard let result = transmissionResults.current else { return false }
        return result.videoURL != nil || !result.clips.filter { $0.videoURL != nil }.isEmpty
    }

    // Mirrors TransmitterPanel's fullscreen condition: the live transmission
    // player owns the whole screen, so the bottom menu must yield to its
    // respond composer exactly like the other players.
    private var transmitterFullscreenPlaying: Bool {
        showingTransmission
            && activeTransmission.snapshot.active
            && !activeTransmission.snapshot.videoUrl.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    private var anyPanelOpen: Bool {
        hudVisible || showingSettings || showingTransmission || showingUserEditor || showingMessages || transmissionResults.current != nil || incomingReceiverActive
    }

    private var bottomCloseVisible: Bool {
        showingTransmission || transmissionResults.current != nil
    }

    private var skyModePanelOpen: Bool {
        hudVisible || showingTransmission || showingUserEditor || showingMessages || transmissionResults.current != nil || incomingReceiverActive
    }

    private var radioSuppressed: Bool {
        transmissionResults.current != nil
            || incomingReceiverActive
            || (showingTransmission && activeTransmission.snapshot.active && !activeTransmission.snapshot.videoUrl.isEmpty)
    }

    private var incomingReceiverActive: Bool {
        guard !acceptedContactSignalId.isEmpty,
              let incomingId = data.incomingTransmission?.id else { return false }
        return incomingId == acceptedContactSignalId
    }

    private var transmitterButtonText: String {
        let snapshot = activeTransmission.snapshot
        guard snapshot.active else { return "TRANSMIT" }
        if !snapshot.error.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { return "ERROR" }
        if !snapshot.videoUrl.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { return "LIVE" }
        let status = snapshot.status.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        if status.contains("error") || status.contains("failed") { return "ERROR" }
        return status.isEmpty ? "BUILDING" : status.uppercased()
    }

    private var activeBottomTab: String {
        if showingTransmission { return "transmitter" }
        if showingUserEditor { return "user" }
        if showingMessages { return "inbox" }
        if hudVisible { return "home" }
        return "map"
    }

    private var requiresLoginGate: Bool {
        // Lock back to the gate any time GPS or motion permission disappears,
        // so the player sees the re-enable buttons and why each one is needed.
        return !authGate.isAuthenticated || !data.locationPermissionReady || !data.motionPermissionReady
    }

    private var appHudReady: Bool {
        !requiresLoginGate
    }

    private var topStatusPadding: CGFloat {
#if os(macOS)
        return 34
#else
        return -2
#endif
    }

    private func homeMoreButton(
        expanded: Bool,
        count: Int,
        expandedTitle: String,
        collapsedTitle: String,
        action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            HStack(spacing: 6) {
                Text(expanded ? expandedTitle : "\(collapsedTitle) \(count)")
                Image(systemName: expanded ? "chevron.up" : "chevron.down")
            }
            .font(.system(size: 12, weight: .black))
            .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
            .frame(maxWidth: .infinity, minHeight: 34)
        }
        .buttonStyle(.plain)
    }

    private func closeAllHuds() {
        updateUnityPlayback(panelOpen: false, videoPlaying: false)
        withAnimation(.easeInOut(duration: 0.24)) {
            hudVisible = false
            showingSettings = false
            showingTransmission = false
            showingUserEditor = false
            showingMessages = false
        }
        transmissionResults.dismiss()
        K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
        K1L0WeatherOverlayInstaller.suppressUnityHud()
        K1L0WeatherOverlayInstaller.setNativePanelOpen(false)
    }

    private func updateUnityPlayback(panelOpen: Bool, videoPlaying: Bool) {
        unityPauseWorkItem?.cancel()
        unityPauseWorkItem = nil

        if videoPlaying {
            K1L0WeatherOverlayInstaller.setUnityPlaybackPaused(true)
        } else {
            // Sky Mode keeps Unity running so its layered sky remains alive.
            // World geometry and particle fountains are suppressed in Unity.
            K1L0WeatherOverlayInstaller.setUnityPlaybackPaused(false)
        }
    }

    private func syncItemFloatSettingsToUnity() {
        let defaults = UserDefaults.standard
        let settings: [(unityKey: String, storageKey: String, fallback: Double)] = [
            ("itemBaseSize", "k1lo_native_itemBaseSize", 10.0),
            ("itemViewportHeight", "k1lo_native_itemViewportHeight", 0.045),
            ("itemMaxWorldSize", "k1lo_native_itemMaxWorldSize", 180.0),
            ("itemInsectCruiseY", "k1lo_native_itemInsectCruiseY", 20.0),
            ("itemInsectCeilingY", "k1lo_native_itemInsectCeilingY", 40.0),
            ("itemInsectCameraClearance", "k1lo_native_itemInsectCameraClearanceV3", 24.0),
            ("itemInsectCuriosityRadius", "k1lo_native_itemInsectCuriosityRadius", 5.0),
            ("itemInsectCuriositySpeed", "k1lo_native_itemInsectCuriositySpeed", 0.18),
            ("itemInsectApproachMeander", "k1lo_native_itemInsectApproachMeander", 6.0),
            ("itemInsectInvestigationLift", "k1lo_native_itemInsectInvestigationLift", 7.5),
            ("itemInsectVisitInterval", "k1lo_native_itemInsectVisitInterval", 20.0),
            ("itemInsectApproachSeconds", "k1lo_native_itemInsectApproachSeconds", 6.5),
            ("itemInsectHoverSeconds", "k1lo_native_itemInsectHoverSecondsV2", 4.0),
            ("itemInsectReturnSeconds", "k1lo_native_itemInsectReturnSecondsV3", 4.5),
            ("ambientItemSpotlightIntensity", "k1lo_native_ambientItemSpotlightIntensity", 7.0),
            ("ambientItemSpotlightRange", "k1lo_native_ambientItemSpotlightRange", 65.0),
            ("ambientItemSpotlightAngle", "k1lo_native_ambientItemSpotlightAngle", 17.0)
        ]
        for setting in settings {
            let value = defaults.object(forKey: setting.storageKey) == nil
                ? setting.fallback
                : defaults.double(forKey: setting.storageKey)
            K1L0WeatherOverlayInstaller.setUnitySetting(
                setting.unityKey,
                String(format: "%.4f", value)
            )
        }
        let uplinkEnabled = defaults.object(forKey: "k1lo_native_ambientItemSpotlightEnabled") == nil
            ? true
            : defaults.bool(forKey: "k1lo_native_ambientItemSpotlightEnabled")
        K1L0WeatherOverlayInstaller.setUnitySetting("ambientItemSpotlightEnabled", uplinkEnabled ? "1" : "0")
    }

    private func loadNewsWalkHistory() {
#if os(iOS)
        guard CMPedometer.isStepCountingAvailable() else { return }
        let now = Date()
        let calendar = Calendar.current
        let currentHour = calendar.dateInterval(of: .hour, for: now)?.start ?? now
        let todayStart = calendar.startOfDay(for: now)
        let group = DispatchGroup()
        let lock = NSLock()
        var hourly = Array(repeating: 0, count: 24)
        var daily = Array(repeating: 0, count: 7)

        for index in 0..<24 {
            guard let start = calendar.date(byAdding: .hour, value: index - 23, to: currentHour) else { continue }
            let end = index == 23 ? now : (calendar.date(byAdding: .hour, value: 1, to: start) ?? now)
            group.enter()
            newsWalkHistoryPedometer.queryPedometerData(from: start, to: end) { data, _ in
                lock.lock()
                hourly[index] = data?.numberOfSteps.intValue ?? 0
                lock.unlock()
                group.leave()
            }
        }

        for index in 0..<7 {
            guard let start = calendar.date(byAdding: .day, value: index - 6, to: todayStart) else { continue }
            let end = index == 6 ? now : (calendar.date(byAdding: .day, value: 1, to: start) ?? now)
            group.enter()
            newsWalkHistoryPedometer.queryPedometerData(from: start, to: end) { data, _ in
                lock.lock()
                daily[index] = data?.numberOfSteps.intValue ?? 0
                lock.unlock()
                group.leave()
            }
        }

        group.notify(queue: .main) {
            newsHourlyWalkHistory = (0..<24).map { NativeWalkHistoryPoint(label: "\($0)", steps: hourly[$0]) }
            newsDailyWalkHistory = (0..<7).map { NativeWalkHistoryPoint(label: "\($0)", steps: daily[$0]) }
        }
#endif
    }

    private func toggleSettings() {
        K1L0WeatherOverlayInstaller.keepOverlayInFront()
        let willOpen = !showingSettings
        withAnimation(.easeInOut(duration: 0.24)) {
            showingSettings = willOpen
            hudVisible = false
            showingTransmission = false
            showingUserEditor = false
            showingMessages = false
        }
        K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
        K1L0WeatherOverlayInstaller.suppressUnityHud()
        K1L0WeatherOverlayInstaller.setNativePanelOpen(false)
        // Settings pull-down can race with Unity re-initialising the shared
        // AVAudioSession; nudge the radio to reclaim it after the animation.
        let apiBase = data.activeAPIBase
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.35) {
            K1L0RadioPlayer.shared.resumeAfterForeground(apiBase: apiBase)
        }
    }

    private func toggleWeatherLook() {
        K1L0WeatherOverlayInstaller.keepOverlayInFront()
        K1L0WeatherModeController.refreshCatalog { catalog in
            weatherPresetCatalog = catalog
            showingWeatherLookPicker = true
        }
    }

    private func selectWeatherLook(_ mode: String) {
        weatherLookMode = mode
        K1L0WeatherModeController.apply(mode)
    }


    private func showHomeHud() {
        K1L0WeatherOverlayInstaller.keepOverlayInFront()
        withAnimation(bottomMenuLayout == "tabs" ? .easeOut(duration: 0.12) : .easeInOut(duration: 0.24)) {
            hudVisible = true
            showingSettings = false
            showingTransmission = false
            showingUserEditor = false
            showingMessages = false
        }
        K1L0WeatherOverlayInstaller.suppressUnityHud()
        K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
        K1L0WeatherOverlayInstaller.setNativePanelOpen(true)
    }

    private func showMapOnly() {
        closeAllHuds()
        // Unity and Mapbox can finish their first streamed frame after the
        // native tab transition. Reassert the map state after both the Swift
        // animation and the first tile-render window so a stale sky-panel
        // message cannot leave the world culled/hidden.
        for delay in [0.15, 0.8] {
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                K1L0WeatherOverlayInstaller.setNativePanelOpen(false)
                K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
            }
        }
    }

    private func toggleHomeMap() {
        hudVisible ? showMapOnly() : showHomeHud()
    }

    private func showTransmitter() {
        K1L0WeatherOverlayInstaller.keepOverlayInFront()
        withAnimation(bottomMenuLayout == "tabs" ? .easeOut(duration: 0.12) : .easeInOut(duration: 0.24)) {
            showingTransmission = true
            hudVisible = false
            showingSettings = false
            showingUserEditor = false
            showingMessages = false
        }
        data.refreshTransmissionState()
        K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
        K1L0WeatherOverlayInstaller.suppressUnityHud()
        K1L0WeatherOverlayInstaller.setNativePanelOpen(true)
    }

    private func toggleTransmitter() {
        showingTransmission ? showMapOnly() : showTransmitter()
    }

    private func showInbox() {
        K1L0WeatherOverlayInstaller.keepOverlayInFront()
        data.refreshTransmissionState()
        withAnimation(bottomMenuLayout == "tabs" ? .easeOut(duration: 0.12) : .easeInOut(duration: 0.24)) {
            showingMessages = true
            hudVisible = false
            showingSettings = false
            showingTransmission = false
            showingUserEditor = false
        }
        K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
        K1L0WeatherOverlayInstaller.suppressUnityHud()
        K1L0WeatherOverlayInstaller.setNativePanelOpen(true)
    }

    private func showUserEditor() {
        K1L0WeatherOverlayInstaller.keepOverlayInFront()
        withAnimation(bottomMenuLayout == "tabs" ? .easeOut(duration: 0.12) : .easeInOut(duration: 0.24)) {
            showingUserEditor = true
            hudVisible = false
            showingSettings = false
            showingTransmission = false
            showingMessages = false
        }
        K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
        K1L0WeatherOverlayInstaller.suppressUnityHud()
        K1L0WeatherOverlayInstaller.setNativePanelOpen(true)
    }

    var body: some View {
        ZStack {
            // Persistent full-screen layer so the ZStack always claims the full
            // bounds even when every other child is hidden (e.g. during video
            // transmission playback, where the bottom menu is gated off).
            // Without this the overlay panel inherits a tiny content-sized
            // geometry and its layout collapses.
            Color.clear.ignoresSafeArea()

            if appHudReady && incomingReceiverActive {
                IncomingMessageReceiverView(data: data) {
                    acceptedContactSignalId = ""
                    data.declineIncomingTransmission()
                }
                    .ignoresSafeArea()
                    .transition(.opacity)
                    .zIndex(150)
            }
            if appHudReady && !skyModePanelOpen && !showingSettings {
                WalkingSkyAlert(
                    items: data.mapMarqueeItems(),
                    dotPhase: data.searchDotPhase,
                    statusHUD: statusHUD,
                    contactRequest: data.incomingTransmission?.id == acceptedContactSignalId ? nil : data.incomingTransmission,
                    dwellPlace: data.collectCandidatePlace,
                    dwellProgress: data.collectCandidatePlace.map { data.locationDwellProgress(for: $0) } ?? 0,
                    onItemTap: { item in
                        guard item.kind == "ambientElement" || item.kind == "ambientObject" else { return }
                        let prefix = "beam:"
                        let signalId = item.id.hasPrefix(prefix) ? String(item.id.dropFirst(prefix.count)) : item.id
                        K1L0WeatherOverlayInstaller.focusNativeBeam(signalId)
                        withAnimation(.spring(response: 0.32, dampingFraction: 0.88)) {
                            data.selectFloatingArtifact(from: item)
                        }
                    },
                    onAcceptContact: {
                        guard let incoming = data.incomingTransmission else { return }
                        acceptedContactSignalId = incoming.id
                        K1L0RadioPlayer.shared.setSuppressed(true)
                        // Stay on the current tab while the player walks to tune
                        // the signal. When tuning completes, the data model puts
                        // the result into the shared full-screen player used by
                        // Messages and Profile, without navigating to Transmitter.
                    },
                    onDwellTap: {
                        showingLocationDwellDetail = true
                    },
                    onDeclineContact: data.declineIncomingTransmission
                )
                .ignoresSafeArea()
                .zIndex(2)
            }

            if appHudReady {
                VStack(spacing: 8) {
                    if !incomingReceiverActive && !showingMessages && !showingTransmission && !showingUserEditor {
                        FixedTopStatusHUD(data: data, settingsActive: false, hideSteps: hudVisible, weatherLookMode: weatherLookMode, onSettingsTapped: toggleSettings)
                            .padding(.horizontal, 18)
                            .padding(.top, topStatusPadding)
                    }
                    Spacer()
                }
                .zIndex(60)
            }

            if appHudReady && hudVisible && !showingSettings && !showingTransmission && !showingUserEditor && !showingMessages {
                GeometryReader { geometry in
                    let tabMenuMode = bottomMenuLayout == "tabs"
                    let panelTop = max(0, geometry.safeAreaInsets.top + (tabMenuMode ? 0 : 4))
                    VStack(spacing: 0) {
                        Color.clear.frame(height: panelTop)

                        ZStack(alignment: .top) {
                            ScrollView(.vertical, showsIndicators: false) {
                                VStack(spacing: 8) {
                                if !tabMenuMode {
                                    PullToDismissTopAnchor(panelCoordinateSpace: "news-panel", onDismiss: closeAllHuds, threshold: 90)
                                }
                                WorldMarqueeCard(
                                    items: data.homeMarqueeItems(),
                                    onItemTap: { item in
                                        withAnimation(.spring(response: 0.32, dampingFraction: 0.88)) {
                                            data.selectFloatingArtifact(from: item)
                                        }
                                    }
                                )

                                WeatherGlassCard {
                                    VStack(alignment: .leading, spacing: 8) {
                                        Text("Steps")
                                            .font(.system(size: 25, weight: .bold))
                                            HStack(alignment: .center, spacing: 14) {
                                            VStack(alignment: .center, spacing: 4) {
                                                LiveStepStatBlock(value: data.liveSteps, durationText: data.liveStepDurationText)
                                            }
                                            VStack(alignment: .leading, spacing: 4) {
                                                NativeNewsWalkGraph(points: newsHourlyWalkHistory, tint: Color(red: 0.66, green: 1.0, blue: 0.76), gridDivisions: 24, majorEvery: 6)
                                                StepStatBlock(label: "Last 24h", value: data.steps24h)
                                            }
                                            VStack(alignment: .leading, spacing: 4) {
                                                NativeNewsWalkGraph(points: newsDailyWalkHistory, tint: Color(red: 0.54, green: 0.78, blue: 1.0), gridDivisions: 7, majorEvery: 1)
                                                StepStatBlock(label: "Last 7d", value: data.steps7d)
                                            }
                                        }
                                    }
                                }

                                WeatherGlassCard {
                                    VStack(alignment: .leading, spacing: 12) {
                                        Text("Open Now")
                                            .font(.system(size: 25, weight: .bold))
                                        DropFilterBar(selected: $selectedDropFilter)

                                        let visiblePlaces = data.filteredPlaces(for: selectedDropFilter)
                                        let displayedPlaces = homeLocationsExpanded ? visiblePlaces : Array(visiblePlaces.prefix(4))
                                        ForEach(displayedPlaces) { place in
                                            Button {
                                                withAnimation(.spring(response: 0.30, dampingFraction: 0.88)) {
                                                    data.selectLocationArtifact(place)
                                                }
                                            } label: {
                                                HStack(spacing: 10) {
                                                    DirectionCell(
                                                        distance: data.distanceText(to: place),
                                                        relativeBearing: data.relativeBearingDegrees(to: place)
                                                    )
                                                    Text("\(data.emoji(for: place)) \(place.name)")
                                                        .font(.system(size: 16, weight: .semibold))
                                                        .lineLimit(1)
                                                    Spacer()
                                                    NearbyItemThumbnail(
                                                        imageUrl: place.imageUrl,
                                                        fallbackGlyph: place.artifactMaterial.map(ElementSymbolLookup.symbol(for:))
                                                    )
                                                }
                                                .contentShape(Rectangle())
                                                .padding(.top, 2)
                                            }
                                            .buttonStyle(.plain)
                                        }
                                        if visiblePlaces.count > 4 {
                                            homeMoreButton(
                                                expanded: homeLocationsExpanded,
                                                count: visiblePlaces.count - 4,
                                                expandedTitle: "show fewer drops",
                                                collapsedTitle: "more drops"
                                            ) {
                                                withAnimation(.spring(response: 0.26, dampingFraction: 0.9)) {
                                                    homeLocationsExpanded.toggle()
                                                }
                                            }
                                        }
                                    }
                                }

                                WeatherGlassCard {
                                    VStack(alignment: .leading, spacing: 10) {
                                        Text("Nearby Users")
                                            .font(.system(size: 25, weight: .bold))
                                        if data.nearbyUsers.isEmpty {
                                            Text(data.nearbyUsersStatus)
                                                .font(.system(size: 13, weight: .medium))
                                                .foregroundStyle(.white.opacity(0.70))
                                        } else {
                                            let visibleUsers = homeNearbyUsersExpanded ? data.nearbyUsers : Array(data.nearbyUsers.prefix(4))
                                            ForEach(visibleUsers) { user in
                                                Button {
                                                    withAnimation(.spring(response: 0.32, dampingFraction: 0.88)) {
                                                        selectedNearbyUser = user
                                                    }
                                                } label: {
                                                    HStack(spacing: 10) {
                                                        K1L0UserAvatar(urlString: user.avatarDisplayUrl, size: 34, userId: user.userId)
                                                        Text(user.displayName)
                                                            .font(.system(size: 15, weight: .semibold))
                                                            .lineLimit(1)
                                                            .minimumScaleFactor(0.72)
                                                        Spacer()
                                                        Text(data.userLocationText(user))
                                                            .font(.system(size: 12, weight: .bold))
                                                            .foregroundStyle(.white.opacity(0.72))
                                                            .lineLimit(1)
                                                    }
                                                    .contentShape(Rectangle())
                                                }
                                                .buttonStyle(.plain)
                                            }
                                            if data.nearbyUsers.count > 4 {
                                                homeMoreButton(
                                                    expanded: homeNearbyUsersExpanded,
                                                    count: data.nearbyUsers.count - 4,
                                                    expandedTitle: "show fewer users",
                                                    collapsedTitle: "more nearby"
                                                ) {
                                                    withAnimation(.spring(response: 0.26, dampingFraction: 0.9)) {
                                                        homeNearbyUsersExpanded.toggle()
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                WeatherGlassCard {
                                    VStack(alignment: .leading, spacing: 10) {
                                        Text("Artifacts")
                                            .font(.system(size: 25, weight: .bold))
                                        if data.inventoryItems.isEmpty {
                                            Text(data.elementsStatus)
                                                .font(.system(size: 13, weight: .medium))
                                                .foregroundStyle(.white.opacity(0.70))
                                        } else {
                                            LazyVGrid(columns: [
                                                GridItem(.adaptive(minimum: 72, maximum: 86), spacing: 10)
                                            ], alignment: .leading, spacing: 10) {
                                                ForEach(data.inventoryItems) { item in
                                                    InventoryTile(item: item)
                                                        .onTapGesture {
                                                            withAnimation(.spring(response: 0.32, dampingFraction: 0.88)) {
                                                                data.selectInventoryArtifact(item)
                                                            }
                                                        }
                                                }
                                            }
                                        }
                                    }
                                }

                                WeatherGlassCard {
                                    VStack(alignment: .leading, spacing: 12) {
                                        HStack {
                                            Text("Leaderboard")
                                                .font(.system(size: 25, weight: .bold))
                                            Spacer()
                                            Picker("Range", selection: $leaderboardRange) {
                                                Text("24 Hours").tag("24h")
                                                Text("7 Days").tag("7d")
                                            }
                                            .pickerStyle(.segmented)
                                            .labelsHidden()
                                            .frame(width: 168)
                                        }

                                        if !data.stepLeaderboardStatus.hasSuffix(" walkers") {
                                            Text(data.stepLeaderboardStatus)
                                                .font(.system(size: 10, weight: .bold))
                                                .foregroundStyle(.white.opacity(0.55))
                                        }

                                        StepLeaderboardSection(
                                            title: "",
                                            leaders: leaderboardRange == "7d" ? data.stepLeaders7d : data.stepLeaders24h,
                                            useWeeklyTotal: leaderboardRange == "7d"
                                        ) { user in
                                            withAnimation(.spring(response: 0.32, dampingFraction: 0.88)) {
                                                selectedNearbyUser = user
                                            }
                                        }
                                    }
                                }
                            }
                            .padding(.horizontal, 18)
                            .padding(.top, tabMenuMode ? 0 : 18)
                            .padding(.bottom, 120)
                            }
                        }
                        .coordinateSpace(name: "news-panel")
                        .onPreferenceChange(PullOffsetKey.self) { y in
                            guard !tabMenuMode else { return }
                            if newsPullBaseline == nil { newsPullBaseline = y }
                            guard let base = newsPullBaseline else { return }
                            if !newsPullFired && y - base > 90 {
                                newsPullFired = true
                                closeAllHuds()
                            }
                        }
                        .onAppear {
                            newsPullBaseline = nil
                            newsPullFired = false
                        }
                        .simultaneousGesture(
                            DragGesture(minimumDistance: 18)
                                .onEnded { value in
                                    guard !tabMenuMode else { return }
                                    if value.translation.height > 78,
                                       abs(value.translation.width) < value.translation.height {
                                        closeAllHuds()
                                    }
                                }
                        )
                        .frame(width: geometry.size.width)
                        .frame(maxHeight: max(420, geometry.size.height - panelTop))
                        .background(Color.clear)
                        Spacer(minLength: 0)
                    }
                    .ignoresSafeArea(edges: .bottom)
                }
                .transition(.move(edge: .bottom).combined(with: .opacity))
                .zIndex(20)
            }

            if showingSettings {
                NativeSettingsPanel(apiBase: data.activeAPIBase) {
                    showingSettings = false
                    K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
                    K1L0WeatherOverlayInstaller.suppressUnityHud()
                    K1L0WeatherOverlayInstaller.setNativePanelOpen(false)
                }
                .transition(.opacity)
            }

            if showingUserEditor && !isVideoTransmissionPlaying {
                NativeUserEditorPanel(data: data, tabsMode: bottomMenuLayout == "tabs") {
                    withAnimation(.spring(response: 0.34, dampingFraction: 0.88)) {
                        showingUserEditor = false
                    }
                    K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
                    K1L0WeatherOverlayInstaller.suppressUnityHud()
                    K1L0WeatherOverlayInstaller.setNativePanelOpen(false)
                }
                .transition((bottomMenuLayout == "tabs") ? .opacity : .move(edge: .bottom).combined(with: .opacity))
                .zIndex(20)
            }

            if showingMessages && !isVideoTransmissionPlaying {
                NativeMessagesPanel(tabsMode: bottomMenuLayout == "tabs") {
                    withAnimation(.spring(response: 0.34, dampingFraction: 0.88)) {
                        showingMessages = false
                    }
                    K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
                    K1L0WeatherOverlayInstaller.suppressUnityHud()
                    K1L0WeatherOverlayInstaller.setNativePanelOpen(false)
                }
                .transition((bottomMenuLayout == "tabs") ? .opacity : .move(edge: .bottom).combined(with: .opacity))
                .zIndex(20)
            }

            if showingTransmission {
                NativeTransmissionPanel(data: data, elements: data.elements, tabsMode: bottomMenuLayout == "tabs") {
                    withAnimation(.spring(response: 0.34, dampingFraction: 0.88)) {
                        showingTransmission = false
                    }
                    K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
                    K1L0WeatherOverlayInstaller.suppressUnityHud()
                    K1L0WeatherOverlayInstaller.setNativePanelOpen(false)
                }
                .transition(.opacity)
                .zIndex(20)
            }

            if appHudReady,
               data.incomingTransmission == nil,
               transmissionResults.current == nil,
               !data.hasArtifactDetailSelection,
               let beam = data.collectCandidateBeam {
                MysteryObjectCollectPrompt(
                    beam: beam,
                    distanceText: data.distanceText(to: beam),
                    relativeBearing: data.relativeBearingDegrees(to: beam),
                    onCollect: { data.confirmCollectBeam(beam) },
                    onDismiss: { data.dismissCollectPrompt() }
                )
                .transition(.scale(scale: 0.96).combined(with: .opacity))
                .zIndex(36)
            }

            if appHudReady,
               showingLocationDwellDetail,
               let place = data.collectCandidatePlace {
                LocationItemCollectPrompt(
                    place: place,
                    distanceText: data.distanceText(to: place),
                    relativeBearing: data.relativeBearingDegrees(to: place),
                    secondsRemaining: data.locationDwellRemainingSeconds(for: place),
                    progress: data.locationDwellProgress(for: place),
                    onDismiss: { showingLocationDwellDetail = false }
                )
                .transition(.scale(scale: 0.96).combined(with: .opacity))
                .zIndex(82)
            }

            if appHudReady, let selection = data.artifactDetailSelection {
                UnifiedArtifactDetailSheet(
                    selection: selection,
                    data: data,
                    onDismiss: data.dismissArtifactDetail
                )
                .transition(.move(edge: .bottom).combined(with: .opacity))
                .zIndex(84)
            }

            if appHudReady, let selectedUser = selectedNearbyUser {
                NearbyUserInfoCard(
                    user: selectedUser,
                    locationText: data.userLocationText(selectedUser),
                    onDismiss: {
                        withAnimation(.spring(response: 0.32, dampingFraction: 0.88)) {
                            selectedNearbyUser = nil
                        }
                    }
                )
                .transition(.move(edge: .bottom).combined(with: .opacity))
                // Above the home panel (40) and top HUD (60) — the nearby-users
                // list lives inside the panel, so the card must outstack it.
                .zIndex(80)
            }

            // Persistent floating controls. Home/map toggle on the lower-left
            // (swaps icon based on whether the home HUD is up); user shortcut
            // on the lower-right. The transmit button stays centered between
            // them. No more X — the toggle button doubles as the close.
            // Hide the whole bar while a transmission/chain video is playing
            // (e.g. opened from Messages or the user screen) so it never
            // overlaps the playback panel.
            if appHudReady && !incomingReceiverActive && !isVideoTransmissionPlaying && !transmitterFullscreenPlaying {
                VStack {
                    Spacer()
                    Group {
                        if bottomMenuLayout == "tabs" {
                            K1L0TabbedBottomMenu(
                                activeTab: activeBottomTab,
                                transmitterText: transmitterButtonText,
                                transmitterActive: activeTransmission.snapshot.active,
                                hideUserAndInbox: isVideoTransmissionPlaying,
                                onHome: showHomeHud,
                                onMap: showMapOnly,
                                onTransmitter: showTransmitter,
                                onInbox: showInbox,
                                onUser: showUserEditor
                            )
                            .padding(.horizontal, 10)
                        } else {
                            HStack {
                                Button(action: toggleHomeMap) {
                                    Image(systemName: hudVisible ? "map.fill" : "house.fill")
                                        .font(.system(size: 21, weight: .bold))
                                        .foregroundStyle(.white)
                                        .frame(width: 58, height: 58)
                                        .modifier(LiquidGlassCircle())
                                }
                                .buttonStyle(.plain)
                                Spacer()
                                Button(action: toggleTransmitter) {
                                    VStack(spacing: 3) {
                                        Image(systemName: "antenna.radiowaves.left.and.right")
                                            .font(.system(size: 20, weight: .bold))
                                        Text(transmitterButtonText)
                                            .font(.system(size: 10, weight: .black, design: .rounded))
                                            .lineLimit(1)
                                            .minimumScaleFactor(0.58)
                                    }
                                    .foregroundStyle(.white)
                                    .frame(width: 112, height: 64)
                                    .background(
                                        LinearGradient(
                                            colors: [Color.black.opacity(0.58), Color.red.opacity(activeTransmission.snapshot.active ? 0.48 : 0.18)],
                                            startPoint: .topLeading,
                                            endPoint: .bottomTrailing
                                        ),
                                        in: Capsule()
                                    )
                                    .overlay(Capsule().stroke(Color.white.opacity(0.24), lineWidth: 1))
                                }
                                .buttonStyle(.plain)
                                Spacer()
                                if !isVideoTransmissionPlaying {
                                    Button(action: showUserEditor) {
                                        Image(systemName: "person.crop.circle.fill")
                                            .font(.system(size: 28, weight: .bold))
                                            .foregroundStyle(.white)
                                            .frame(width: 58, height: 58)
                                            .modifier(LiquidGlassCircle())
                                    }
                                    .buttonStyle(.plain)
                                }
                            }
                            .padding(.horizontal, 18)
                        }
                    }
                    .padding(.bottom, bottomMenuLayout == "tabs" ? 0 : 16)
                }
                .zIndex(40)
                .ignoresSafeArea(.keyboard, edges: .bottom)
            }

            if requiresLoginGate {
                K1L0LoginPermissionGate(auth: authGate, data: data)
                    .transition(.opacity)
                    .zIndex(200)
            }
        }
        .onAppear {
            #if os(iOS)
            UISegmentedControl.appearance().setTitleTextAttributes([.foregroundColor: UIColor.white.withAlphaComponent(0.60)], for: .normal)
            UISegmentedControl.appearance().setTitleTextAttributes([.foregroundColor: UIColor.black], for: .selected)
            #endif
            if selectedDropFilter == "snack" {
                selectedDropFilter = "all"
            }
            authGate.loadCached()
            if UserDefaults.standard.object(forKey: "k1lo_native_musicRadioDefaultedV2") == nil {
                musicRadioEnabled = true
                UserDefaults.standard.set(true, forKey: "k1lo_native_musicRadioDefaultedV2")
            }
            data.start()
            data.requestRequiredPermissions()
            data.refreshTransmissionState(clearStaleCache: true)
            loadNewsWalkHistory()
            K1L0RadioPlayer.shared.setVolume(musicRadioVolume)
            K1L0RadioPlayer.shared.setMode(musicRadioMode)
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) {
                K1L0RadioPlayer.shared.setEnabled(musicRadioEnabled, apiBase: data.activeAPIBase)
                K1L0RadioPlayer.shared.setSuppressed(radioSuppressed)
            }
            NativeUnityLightingSync.sync()
            NativeUnitySolarSync.start()
            // @AppStorage and Unity PlayerPrefs are separate stores. Push the
            // complete item-float state at launch so size/motion never depends
            // on opening Settings and nudging a slider first.
            syncItemFloatSettingsToUnity()
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.0) {
                syncItemFloatSettingsToUnity()
                K1L0WeatherModeController.apply(weatherLookMode)
            }
            // Sync sky mode with whichever modal is up at launch (news HUD is
            // visible by default). The .onChange(skyModePanelOpen) below won't
            // fire for the initial value, so we have to drive it explicitly here.
            K1L0WeatherOverlayInstaller.setNativePanelOpen(skyModePanelOpen || requiresLoginGate)
            updateUnityPlayback(
                panelOpen: skyModePanelOpen || requiresLoginGate,
                videoPlaying: isVideoTransmissionPlaying
            )

            // Sync custom character textures (cloak, helmet) on startup
            K1L0WeatherOverlayInstaller.loadNativeUserMetadata()
        }
        .onChange(of: authGate.isAuthenticated) { authenticated in
            // Fire a second skin load once auth settles — the .onAppear call above
            // runs before Firebase returns a userId, so it bails early. This catches
            // the moment a userId becomes available and applies the cloak/helmet.
            if authenticated {
                K1L0WeatherOverlayInstaller.loadNativeUserMetadata()
            }
        }
        .onChange(of: selectedDropFilter) { _ in
            liveDropLimit = 5
            homeLocationsExpanded = false
            // Home remains a single-choice list filter. Choosing there resets
            // the independent map beam switches to one category, or all three
            // when the home list returns to All.
            let resetCategories = selectedDropFilter == "all"
                ? "coffee,drinks,food"
                : selectedDropFilter
            selectedBeamCategories = resetCategories
            data.applyLocationFilter(resetCategories)
        }
        .onChange(of: selectedBeamCategories) { categories in
            data.applyLocationFilter(categories)
        }
        .k1l0TypeErased()
        .modifier(K1L0SceneActivationModifier(scenePhase: scenePhase) {
                data.refreshPermissionGateState()
                data.refreshTransmissionState(clearStaleCache: true)
                loadNewsWalkHistory()
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.8) {
                    K1L0RadioPlayer.shared.setSuppressed(radioSuppressed)
                    K1L0RadioPlayer.shared.resumeAfterForeground(apiBase: data.activeAPIBase)
                }
                NativeUnityLightingSync.sync()
                NativeUnitySolarSync.sync()
        })
        .onReceive(NotificationCenter.default.publisher(for: .k1l0RemoteWeatherLook)) { note in
            guard let mode = note.object as? String else { return }
            selectWeatherLook(mode)
        }
        .onChange(of: musicRadioEnabled) { enabled in
            K1L0RadioPlayer.shared.setEnabled(enabled, apiBase: data.activeAPIBase)
        }
        .onChange(of: musicRadioVolume) { volume in
            K1L0RadioPlayer.shared.setVolume(volume)
        }
        .onChange(of: musicRadioMode) { mode in
            K1L0RadioPlayer.shared.setMode(mode)
        }
        .onChange(of: data.activeAPIBase) { apiBase in
            K1L0RadioPlayer.shared.setEnabled(musicRadioEnabled, apiBase: apiBase)
        }
        .onChange(of: skyModePanelOpen) { open in
            K1L0WeatherOverlayInstaller.setNativePanelOpen(open || requiresLoginGate)
            updateUnityPlayback(
                panelOpen: open || requiresLoginGate,
                videoPlaying: isVideoTransmissionPlaying
            )
        }
        .onChange(of: requiresLoginGate) { required in
            K1L0WeatherOverlayInstaller.setNativePanelOpen(required || skyModePanelOpen)
            updateUnityPlayback(
                panelOpen: required || skyModePanelOpen,
                videoPlaying: isVideoTransmissionPlaying
            )
            if !required {
                // User logged in; sync custom character textures (cloak, helmet)
                K1L0WeatherOverlayInstaller.loadNativeUserMetadata()
            }
        }
        .onChange(of: activeTransmission.snapshot.active) { active in
            K1L0RadioPlayer.shared.setSuppressed(radioSuppressed)
        }
        .onChange(of: activeTransmission.snapshot.videoUrl) { videoUrl in
            K1L0RadioPlayer.shared.setSuppressed(radioSuppressed)
        }
        .onChange(of: showingTransmission) { open in
            K1L0RadioPlayer.shared.setSuppressed(radioSuppressed)
        }
        .k1l0TypeErased()
        .onChange(of: transmissionResults.current?.id) { _ in
            K1L0RadioPlayer.shared.setSuppressed(radioSuppressed)
            K1L0WeatherOverlayInstaller.setVideoBackdropActive(transmissionResults.current != nil)
        }
        .onChange(of: isVideoTransmissionPlaying) { playing in
            updateUnityPlayback(
                panelOpen: skyModePanelOpen || requiresLoginGate,
                videoPlaying: playing
            )
            K1L0PerfStatsStore.shared.setVideoPlaybackActive(playing)
            data.setVideoPlaybackActive(playing)
            if playing { K1L0WeatherOverlayInstaller.keepOverlayInFront() }
        }
        .onChange(of: data.incomingTransmission?.id) { id in
            acceptedContactSignalId = ""
            K1L0RadioPlayer.shared.setSuppressed(radioSuppressed)
            if id != nil { K1L0WeatherOverlayInstaller.playBeamCollectSound() }
        }
        .onChange(of: acceptedContactSignalId) { _ in
            K1L0RadioPlayer.shared.setSuppressed(radioSuppressed)
        }
        .animation((bottomMenuLayout == "tabs") ? .easeOut(duration: 0.12) : .spring(response: 0.34, dampingFraction: 0.88), value: showingTransmission)
        .animation((bottomMenuLayout == "tabs") ? .easeOut(duration: 0.12) : .spring(response: 0.34, dampingFraction: 0.88), value: showingUserEditor)
        .animation((bottomMenuLayout == "tabs") ? .easeOut(duration: 0.12) : .spring(response: 0.34, dampingFraction: 0.88), value: showingMessages)
        .modifier(K1L0WeatherPresetPickerModifier(
            isPresented: $showingWeatherLookPicker,
            presets: weatherPresetCatalog,
            onSelect: selectWeatherLook,
            onSettings: toggleSettings
        ))
        .overlay(alignment: .bottom) {
            if let result = transmissionResults.current {
                ZStack {
                    Color.black.ignoresSafeArea()
                    TransmissionResultPanel(result: result, onSelectOption: { option, photoPath in
                        K1L0WeatherOverlayInstaller.playSfxSlot("response_tap")
                        data.respondToTransmission(result, option: option, photoPath: photoPath)
                        transmissionResults.dismiss()
                    }) {
                        transmissionResults.dismiss()
                    }
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(Color.black.ignoresSafeArea())
                .id(result.id)
                .transition(.opacity)
                .zIndex(30)
            }
        }
        .animation(.spring(response: 0.34, dampingFraction: 0.88), value: transmissionResults.current?.id)
    }
}

struct K1L0SceneActivationModifier: ViewModifier {
    let scenePhase: ScenePhase
    let onActive: () -> Void

    func body(content: Content) -> some View {
        content.onChange(of: scenePhase) { phase in
            if phase == .active { onActive() }
        }
    }
}

struct TransmissionPhotoAttachmentButton: View {
    @Binding var photoPath: String?
    @Binding var showingSourceDialog: Bool
    @Binding var pickerRequest: PhotoPickerRequest?

    var body: some View {
        Button {
            if photoPath != nil {
                photoPath = nil
            } else {
                showingSourceDialog = true
            }
        } label: {
            Image(systemName: photoPath == nil ? "camera" : "camera.fill")
                .font(.system(size: 15, weight: .black))
                .foregroundStyle(photoPath == nil ? .white : Color(red: 1.0, green: 0.19, blue: 0.58))
                .frame(width: 42, height: 42)
                .background(Color.black.opacity(0.70), in: RoundedRectangle(cornerRadius: 8, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .stroke(photoPath == nil ? Color.white.opacity(0.25) : Color(red: 1.0, green: 0.19, blue: 0.58).opacity(0.9), lineWidth: 1.2))
        }
        .buttonStyle(.plain)
        .confirmationDialog("Response photo", isPresented: $showingSourceDialog, titleVisibility: .visible) {
            if UIImagePickerController.isSourceTypeAvailable(.camera) {
                Button("Take Photo") { pickerRequest = PhotoPickerRequest(source: .camera) }
            }
            Button("Photo Library") { pickerRequest = PhotoPickerRequest(source: .photoLibrary) }
            Button("Cancel", role: .cancel) {}
        }
    }
}

struct TransmissionResponseTextField: View {
    @Binding var text: String
    let sending: Bool

    var body: some View {
        Group {
#if canImport(UIKit)
            TextField(sending ? "sending..." : "RESPOND", text: $text)
            .textInputAutocapitalization(.never)
            .disableAutocorrection(true)
#else
            TextField(sending ? "sending..." : "RESPOND", text: $text)
#endif
        }
            .font(.system(size: 15, weight: .semibold, design: .rounded))
            .foregroundStyle(.white)
            .padding(.horizontal, 12)
            .frame(height: 42)
            .background(Color.black.opacity(0.70), in: RoundedRectangle(cornerRadius: 8, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 8, style: .continuous)
                .stroke(Color(red: 1.0, green: 0.19, blue: 0.58).opacity(0.9), lineWidth: 1.5))
            .disabled(sending)
    }
}

struct TransmissionResponseChoiceButton: View {
    let option: String
    let disabled: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Text(option.uppercased())
                .font(.system(size: 11, weight: .semibold, design: .rounded))
                .foregroundStyle(.white)
                .lineLimit(1)
                .minimumScaleFactor(0.72)
                .frame(minHeight: 28, alignment: .leading)
                .padding(.horizontal, 8)
                .background(Color.white.opacity(0.055), in: RoundedRectangle(cornerRadius: 7, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: 7, style: .continuous).stroke(Color.white.opacity(0.14), lineWidth: 0.75))
        }
        .buttonStyle(.plain)
        .fixedSize(horizontal: true, vertical: false)
        .disabled(disabled)
        .opacity(disabled ? 0.45 : 1.0)
    }
}

struct TransmissionResponderIdentityButton: View {
    let responder: OverlayUser
    let createdAt: Double
    let action: () -> Void

    private static let timestampFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.dateFormat = "MMM d · h:mm a"
        return formatter
    }()

    private var primaryLine: String {
        let callsign = (responder.callsign ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        let identity = callsign.isEmpty ? responder.displayName.uppercased() : "@\(callsign.uppercased())"
        let country = (responder.country ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        return country.isEmpty ? identity : "\(identity)  ·  \(country.uppercased())"
    }

    private var secondaryLine: String {
        var parts: [String] = []
        let city = (responder.city ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if !city.isEmpty { parts.append(city.uppercased()) }
        if createdAt > 0 {
            let seconds = createdAt > 9_999_999_999 ? createdAt / 1000.0 : createdAt
            parts.append(Self.timestampFormatter.string(from: Date(timeIntervalSince1970: seconds)).uppercased())
        }
        return parts.joined(separator: "  ·  ")
    }

    var body: some View {
        Button(action: action) {
            HStack(spacing: 8) {
                K1L0UserAvatar(
                    urlString: responder.avatarDisplayUrl,
                    size: 36,
                    userId: responder.userId
                )
                VStack(alignment: .leading, spacing: 2) {
                    Text(primaryLine)
                        .font(.system(size: 13, weight: .black, design: .rounded))
                        .foregroundStyle(.white)
                    if !secondaryLine.isEmpty {
                        Text(secondaryLine)
                            .font(.system(size: 10, weight: .bold, design: .monospaced))
                            .foregroundStyle(.white.opacity(0.68))
                    }
                }
                .lineLimit(1)
                .minimumScaleFactor(0.68)
            }
            .padding(.leading, 6)
            .padding(.trailing, 11)
            .padding(.vertical, 5)
            .background(Color.black.opacity(0.58), in: Capsule())
        }
        .buttonStyle(.plain)
    }
}

struct TransmissionResultPanel: View {
    let result: K1L0TransmissionResult
    let onSelectOption: (String, String?) -> Void
    var composerBottomObstruction: CGFloat = 0
    let onClose: () -> Void
    var onNewTransmission: (() -> Void)? = nil
    @State private var currentClipIndex = 0
    @State private var currentClipProgress = 0.0
    @State private var textTransform = TransmissionTextTransformStore.load()
    @State private var typedPlotTexts: Set<String> = []
    @State private var videoReadyForText = false
    @State private var showingSettings = false
    @State private var responseDraft = ""
    @State private var isSendingResponse = false
    // Panel-scope keyboard height — used to lift the response composer row
    // and its adjoining buttons above the iOS keyboard when the text field
    // is focused. The panel's outer .ignoresSafeArea() defeats SwiftUI's
    // built-in keyboard avoidance, so we do it explicitly.
    @State private var keyboardHeight: CGFloat = 0
    @State private var showResponderCard = false
    @State private var responsePhotoPath: String? = nil
    @State private var responsePhotoPickerRequest: PhotoPickerRequest? = nil
    @State private var showResponsePhotoSourceDialog = false
    @State private var editingPlot = false
    @State private var plotEditDraft = ""
    @State private var editedPlots: [String: String] = [:]
    @FocusState private var plotEditorFocused: Bool
    // Music (SING) editor — pencil-adjacent button. Own-transmission only.
    @State private var showingMusicEditor = false
    @State private var musicLyricsDraft = ""
    @State private var musicSections: [MusicLyricSection] = []
    @State private var musicSubmitting = false
    @StateObject private var mediaPreloader = K1L0TransmissionMediaPreloader()

    struct MusicLyricSection: Identifiable {
        let id = UUID()
        var label: String   // verse | chorus | bridge
        var text: String
    }
    @State private var musicStatus = ""
    @FocusState private var musicEditorFocused: Bool
    @ObservedObject private var keyboard = K1L0KeyboardObserver.shared

    private var currentClipSenderName: String {
        guard currentClipIndex >= 0, currentClipIndex < playableClips.count else { return "" }
        return playableClips[currentClipIndex].sourceName
    }

    // The nearby-user record is useful for current artwork, but never for the
    // location line: city/country belong to the immutable transmission clip.
    // Falling back to a live profile location makes old transmissions appear
    // to move whenever their sender travels.
    private var responderCardUser: OverlayUser? {
        guard currentClipIndex >= 0, currentClipIndex < playableClips.count else { return nil }
        let clip = playableClips[currentClipIndex]
        guard !clip.sourceUserId.isEmpty else { return nil }
        if let known = K1L0OverlayDataModel.activeModel?.nearbyUsers.first(where: { $0.userId == clip.sourceUserId }) {
            return OverlayUser(
                userId: known.userId,
                name: clip.sourceName.isEmpty ? known.name : clip.sourceName,
                callsign: clip.sourceCallsign.isEmpty ? known.callsign : clip.sourceCallsign,
                avatarUrl: known.avatarUrl,
                helmetUrl: known.helmetUrl,
                faceUrl: known.faceUrl,
                city: clip.sourceCity.isEmpty ? nil : clip.sourceCity,
                country: clip.sourceCountry.isEmpty ? nil : clip.sourceCountry,
                countryCode: clip.sourceCountryCode.isEmpty ? nil : clip.sourceCountryCode,
                lat: known.lat,
                lng: known.lng,
                lastActive: known.lastActive
            )
        }
        return OverlayUser(userId: clip.sourceUserId, name: clip.sourceName, callsign: clip.sourceCallsign,
                           avatarUrl: nil, helmetUrl: nil, faceUrl: nil, city: clip.sourceCity,
                           country: clip.sourceCountry, countryCode: clip.sourceCountryCode,
                           lat: nil, lng: nil, lastActive: nil)
    }

    private var currentClipCreatedAt: Double {
        guard currentClipIndex >= 0, currentClipIndex < playableClips.count else { return result.createdAt ?? 0 }
        return playableClips[currentClipIndex].createdAt
    }
    @AppStorage("k1lo_native_transmissionFizzyEdges") private var transmissionFizzyEdges = false
    @AppStorage("k1lo_native_transmissionFX") private var transmissionFXEnabled = true
    @AppStorage("k1lo_native_transmissionFXIntensity") private var transmissionFXIntensity = 0.5

#if canImport(UIKit)
    private var saveMediaItems: [CameraRollSaveMediaItem] {
        let clipItems = result.clips.enumerated().compactMap { index, clip -> CameraRollSaveMediaItem? in
            guard let videoURL = clip.videoURL?.absoluteString else { return nil }
            let baseOverlay = overlayTextForClip(plot: clip.responsePlot, selectedResponse: clip.selectedResponse, isResponseClip: index > 0)
            let overlay = baseOverlay.trimmingCharacters(in: .whitespacesAndNewlines)
            return CameraRollSaveMediaItem(
                videoUrlString: videoURL,
                audioUrlString: index == 0 ? clip.audioURL?.absoluteString : nil,
                overlayText: overlay,
                overlayTransform: textTransform
            )
        }
        if !clipItems.isEmpty { return clipItems }
        guard let videoURL = result.videoURL?.absoluteString else { return [] }
        let baseOverlay = overlayTextForClip(plot: result.responsePlot, selectedResponse: result.selectedResponse ?? "", isResponseClip: false)
        let overlay = baseOverlay.trimmingCharacters(in: .whitespacesAndNewlines)
        return [
            CameraRollSaveMediaItem(
                videoUrlString: videoURL,
                audioUrlString: result.audioURL?.absoluteString,
                overlayText: overlay,
                overlayTransform: textTransform
            )
        ]
    }
#endif

    private var playableClips: [K1L0TransmissionClip] {
        result.clips.filter { $0.videoURL != nil }
    }

    private var transmissionMediaURLs: [URL] {
        var urls = playableClips.flatMap { clip in
            [clip.videoURL, clip.audioURL].compactMap { $0 }
        }
        if playableClips.isEmpty {
            if let videoURL = result.videoURL { urls.append(videoURL) }
            if let audioURL = result.audioURL { urls.append(audioURL) }
        }
        return urls
    }

    private var transmissionMediaKey: String {
        transmissionMediaURLs.map(\.absoluteString).sorted().joined(separator: "\n")
    }

    private var preparedPlayableClips: [K1L0TransmissionClip] {
        playableClips.map { clip in
            clip.replacingMedia(
                videoURL: mediaPreloader.resolve(clip.videoURL),
                audioURL: mediaPreloader.resolve(clip.audioURL)
            )
        }
    }

    private func overlayTextForClip(plot: String, selectedResponse: String, isResponseClip: Bool) -> String {
        let cleanPlot = plot.trimmingCharacters(in: .whitespacesAndNewlines)
        let cleanResponse = selectedResponse.trimmingCharacters(in: .whitespacesAndNewlines)
        if isResponseClip && !cleanResponse.isEmpty { return cleanResponse }
        return cleanPlot
    }

    private var visiblePlotText: String {
        if !playableClips.isEmpty {
            let safeIndex = min(max(0, currentClipIndex), playableClips.count - 1)
            let clip = playableClips[safeIndex]
            if let edited = editedPlots[clip.sourceJobId] { return edited }
            return overlayTextForClip(plot: clip.responsePlot, selectedResponse: clip.selectedResponse, isResponseClip: safeIndex > 0)
        }
        if let jobId = result.jobId, let edited = editedPlots[jobId] { return edited }
        return overlayTextForClip(plot: result.responsePlot, selectedResponse: result.selectedResponse ?? "", isResponseClip: false)
    }

    private var currentEditableIdentity: (jobId: String, ownerUserId: String)? {
        guard !playableClips.isEmpty,
              currentClipIndex >= 0,
              currentClipIndex < playableClips.count else { return nil }
        let clip = playableClips[currentClipIndex]
        let jobId = clip.sourceJobId.trimmingCharacters(in: .whitespacesAndNewlines)
        let owner = clip.sourceUserId.trimmingCharacters(in: .whitespacesAndNewlines)
        let aliases = currentK1L0UserIds
        guard !jobId.isEmpty, !owner.isEmpty,
              aliases.contains(where: { owner.caseInsensitiveCompare($0) == .orderedSame }) else { return nil }
        return (jobId, owner)
    }

    private var currentNativeUserId: String {
        let defaults = UserDefaults.standard
        return (defaults.string(forKey: "K1L0UserId") ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
    }

    // The chain's one song lives on the ROOT slide's job — responses are
    // generated without music. Singing/lyrics must target the root, and only
    // when the root transmission is the player's own.
    private var musicRootIdentity: (jobId: String, ownerUserId: String)? {
        guard let first = playableClips.first else { return nil }
        let jobId = first.sourceJobId.trimmingCharacters(in: .whitespacesAndNewlines)
        let owner = first.sourceUserId.trimmingCharacters(in: .whitespacesAndNewlines)
        let aliases = currentK1L0UserIds
        guard !jobId.isEmpty, !owner.isEmpty,
              aliases.contains(where: { owner.caseInsensitiveCompare($0) == .orderedSame }) else { return nil }
        return (jobId, owner)
    }

    private var playbackStatusText: String? {
        guard !playableClips.isEmpty else { return nil }
        let latestClip = playableClips.last!
        let latestOwner = latestClip.sourceUserId.lowercased()
        let ownKey = currentNativeUserId.lowercased()
        let aliases = currentK1L0UserIds.map { $0.lowercased() }
        let latestIsFromMe = !latestOwner.isEmpty && (latestOwner == ownKey || aliases.contains(latestOwner))
        if latestIsFromMe {
            // 5 slides = full story (depths 0-4); the author just had the
            // final word, so nothing further is coming.
            return playableClips.count >= 5 ? "Story complete." : "Awaiting response…"
        } else {
            let isFinalSlide = (currentClipIndex == playableClips.count - 1)
            if result.allowsTextResponse && !isFinalSlide {
                return "Please respond…"
            }
        }
        return nil
    }

    private var currentK1L0UserIds: [String] {
        let defaults = UserDefaults.standard
        return ["K1L0UserId", "FirebaseUserId", "DeviceID", "deviceID"].compactMap { key in
            let value = (defaults.string(forKey: key) ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
            return value.isEmpty ? nil : value
        }
    }

    private var responseClipIndex: Int? {
        guard !playableClips.isEmpty else { return nil }
        return playableClips.indices.reversed().first { playableClips[$0].allowsResponse }
    }

    private var isShowingResponseClip: Bool {
        guard let responseClipIndex else { return false }
        return currentClipIndex == responseClipIndex
    }

    private var responseChoicesForCurrentClip: [String] {
        guard result.allowsResponseOptions,
              isShowingResponseClip,
              currentClipIndex >= 0,
              currentClipIndex < playableClips.count else { return [] }
        let supplied = playableClips[currentClipIndex].responseOptions
        if supplied.isEmpty {
            return ["follow the trail", "check the door", "bring the map", "leave a marker"]
        }
        return normalizedTransmissionOptions(supplied, includeFallback: true)
    }

    var body: some View {
        GeometryReader { geometry in
            // 9:16 portrait box sitting below the close/settings button row so it
            // no longer covers the top story/chain progress bar. The fizzy tattered
            // border and the player are constrained to this rect so the border wraps
            // the video (not the whole phone screen); the save-to-camera-roll button
            // sits directly underneath. Background is clear so the Unity sky shows
            // through, and the tattered edges are themselves semitransparent.
            // Size the video as a true 9:16 rect — the largest one that fits
            // below the close/settings button row and above the save button — so
            // it fills the frame edge-to-edge with no letterbox. That lets the
            // tattered-edge shader tear all four sides (a letterboxed video
            // leaves the side tears hanging in empty space). With the bottom
            // menu hidden during playback we reclaim vertical room and only
            // reserve ~48pt above the bottom safe area for the save button.
            // The ZStack below uses .ignoresSafeArea(), which makes the content
            // span the physical screen while geometry.safeAreaInsets reads 0
            // (the parent consumed them) — so pull the real device insets from
            // the key window or the response composer lands under the home bar.
            let topSafe = max(geometry.safeAreaInsets.top, k1l0DeviceSafeAreaInsets().top)
            // Composer bottom edge sits exactly on the bottom safe-area line.
            let bottomSafe = max(geometry.safeAreaInsets.bottom, k1l0DeviceSafeAreaInsets().bottom)
            // Real screen height — geometry.size.height can be a safe-area-inset
            // (non-fullscreen) value in this overlay host, which would float the
            // composer above the bottom. Anchor against the key-window bounds so
            // the RESPOND field always pins to the bottom safe-area line.
            let screenH = max(k1l0DeviceScreenSize().height, geometry.size.height)
            // Top stack lives entirely above the video so nothing overlaps the
            // playback area: a tiny chain-progress bar flush with the top safe
            // area, then the settings + close buttons in a right-aligned row
            // directly beneath it. The video starts below that row.
            let progressBarY = topSafe + 5
            let buttonRowY = topSafe + 32
            let topReserve = topSafe + 64
            let responseChoices = responseChoicesForCurrentClip
            let canRespond = result.allowsTextResponse && isShowingResponseClip
            let availWidth = geometry.size.width
            let videoAspect: CGFloat = 9.0 / 16.0
            let videoWidth = availWidth
            let videoHeight = videoWidth / videoAspect
            let videoTopInset = topReserve
            let videoRect = CGRect(
                x: (geometry.size.width - videoWidth) / 2,
                y: videoTopInset,
                width: videoWidth,
                height: videoHeight
            )
            // When the respond UI is up, lift the plot text clear of the
            // stacked choices + composer. composerHeight mirrors the rendered
            // composer (each choice row 28pt, 6pt row gaps, 8pt to the field,
            // 42pt field) so the plot text pins a fixed 12pt above its top.
            let composerRows = responseChoices.count
            // + 24 for the "please respond." caption row above the choices.
            let composerHeight: CGFloat = canRespond
                ? CGFloat(composerRows) * 28 + CGFloat(max(0, composerRows - 1)) * 6 + 8 + 42 + 24
                : 0
            let composerTopY = screenH - bottomSafe - composerBottomObstruction - composerHeight
            let plotBottomInset: CGFloat = canRespond
                ? max(18, videoRect.maxY - composerTopY + 12)
                : 66
            ZStack(alignment: .top) {
                Color.black.ignoresSafeArea()

                VStack(spacing: 12) {
                    ZStack {
                        Color.clear

                        if !mediaPreloader.isReady {
                            TransmissionMediaLoadingView(
                                completed: mediaPreloader.completedCount,
                                total: mediaPreloader.totalCount,
                                progress: mediaPreloader.progress,
                                errorMessage: mediaPreloader.errorMessage,
                                onRetry: { mediaPreloader.retry(urls: transmissionMediaURLs) }
                            )
                            .frame(width: videoWidth, height: videoHeight)
                        } else if !preparedPlayableClips.isEmpty {
                            InlineTransmissionVideoPlayer(clips: preparedPlayableClips, currentClipIndex: $currentClipIndex, currentClipProgress: $currentClipProgress, isVideoReady: $videoReadyForText, holdAtEndIndex: responseClipIndex, freezeCurrent: $editingPlot)
                                .frame(width: videoWidth, height: videoHeight)
                                .mask(TatteredEdgeMaskCanvas())
                        } else if let url = result.videoURL {
                            InlineTransmissionVideoPlayer(
                                urlString: mediaPreloader.resolve(url)?.absoluteString ?? url.absoluteString,
                                audioUrlString: mediaPreloader.resolve(result.audioURL)?.absoluteString,
                                currentClipProgress: $currentClipProgress,
                                isVideoReady: $videoReadyForText
                            )
                                .frame(width: videoWidth, height: videoHeight)
                                .mask(TatteredEdgeMaskCanvas())
                        } else if let url = result.imageURL {
                            AsyncImage(url: url) { phase in
                                switch phase {
                                case .success(let image):
                                    image
                                        .resizable()
                                        .scaledToFit()
                                case .failure:
                                    Text("image unavailable")
                                        .font(.system(size: 16, weight: .bold, design: .monospaced))
                                        .foregroundStyle(.white.opacity(0.72))
                                default:
                                    Color.clear
                                }
                            }
                            .frame(width: videoWidth, height: videoHeight)
                            .transmissionFizzyMask(enabled: transmissionFizzyEdges, size: CGSize(width: videoWidth, height: videoHeight))
                        }

                        if playableClips.count > 1 {
                            TransmissionChainTapZones(
                                clipCount: playableClips.count,
                                currentIndex: $currentClipIndex,
                                currentProgress: $currentClipProgress
                            )
                        }

                    }
                    .frame(width: videoWidth, height: videoHeight)
                    .clipped()

                    Spacer(minLength: 0)
                }
                .frame(width: geometry.size.width, height: geometry.size.height)
                .padding(.top, videoTopInset)

                if (videoReadyForText || (playableClips.isEmpty && result.videoURL == nil)) && !visiblePlotText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    let plotTextKey = visiblePlotText.trimmingCharacters(in: .whitespacesAndNewlines)
                    DraggableTransmissionTextOverlay(
                        text: visiblePlotText,
                        transform: $textTransform,
                        canvasSize: videoRect.size,
                        canvasOrigin: videoRect.origin,
                        animateTypewriter: !typedPlotTexts.contains(plotTextKey),
                        onTypewriterFinished: { typedPlotTexts.insert($0) },
                        bottomInset: plotBottomInset
                    )
                    .id("plot-\(currentClipIndex)-\(visiblePlotText)")
                }

                // The responder identity belongs to the video, not the plot
                // ribbon. Use the same helmet resolver/avatar renderer as
                // Nearby and leaderboards, pinned inside the video's top-left.
                if let responder = responderCardUser {
                    TransmissionResponderIdentityButton(responder: responder, createdAt: currentClipCreatedAt) {
                        withAnimation(.spring(response: 0.3, dampingFraction: 0.9)) {
                            showResponderCard = true
                        }
                    }
                    .frame(width: videoRect.width, height: videoRect.height, alignment: .topLeading)
                    .padding(.leading, 12)
                    .padding(.top, 12)
                    .position(x: videoRect.midX, y: videoRect.midY)
                    .zIndex(15)
                }

                // Tiny chain-progress bar flush with the top safe area.
                if playableClips.count > 1 {
                    TransmissionChainProgressBar(
                        total: playableClips.count,
                        currentIndex: currentClipIndex,
                        currentProgress: currentClipProgress
                    )
                    .frame(maxWidth: geometry.size.width - 28, minHeight: 5, maxHeight: 8)
                    .position(x: geometry.size.width / 2, y: progressBarY)
                    .allowsHitTesting(false)
                }

                // Settings + close buttons: right-aligned row directly beneath
                // the progress bar.
                Button(action: onClose) {
                    Image(systemName: "xmark")
                        .font(.system(size: 14, weight: .medium))
                        .foregroundStyle(.white.opacity(0.94))
                        .frame(width: 34, height: 34)
                        .background(.ultraThinMaterial, in: Circle())
                        .overlay(Circle().stroke(Color.white.opacity(0.20), lineWidth: 0.5))
                        .frame(width: 44, height: 44)
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .position(x: geometry.size.width - 28, y: buttonRowY)

                Button(action: {
                    withAnimation {
                        showingSettings.toggle()
                    }
                }) {
                    Image(systemName: "gearshape")
                        .font(.system(size: 15, weight: .regular))
                        .foregroundStyle(.white.opacity(0.94))
                        .frame(width: 34, height: 34)
                        .background(.ultraThinMaterial, in: Circle())
                        .overlay(Circle().stroke(Color.white.opacity(0.20), lineWidth: 0.5))
                        .frame(width: 44, height: 44)
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .position(x: geometry.size.width - 74, y: buttonRowY)

#if canImport(UIKit)
                if !saveMediaItems.isEmpty {
                    CameraRollSaveButton(mediaItems: saveMediaItems, iconOnly: true)
                        .position(x: geometry.size.width - 120, y: buttonRowY)
                }
#endif

                if let statusText = playbackStatusText {
                    HStack(spacing: 8) {
                        EchoSignalView()
                        Text(statusText)
                            .font(.system(size: 14, weight: .black, design: .rounded))
                            .foregroundStyle(.white)
                            .shadow(color: .black.opacity(0.85), radius: 3, x: 0, y: 1.5)
                    }
                    .padding(.horizontal, 16)
                    .frame(maxWidth: geometry.size.width - 170, alignment: .leading)
                    .position(x: (geometry.size.width - 170) / 2 + 16, y: buttonRowY)
                }

                if canRespond {
                    VStack(alignment: .leading, spacing: 8) {
                        Text(isSendingResponse ? "Response sent…" : "Respond ASAP")
                            .font(.system(size: 13, weight: .bold, design: .monospaced))
                            .foregroundStyle(isSendingResponse ? Color.white.opacity(0.78) : Color(red: 0.90, green: 0.35, blue: 0.98))
                            .frame(maxWidth: .infinity, alignment: .leading)
                        if !isSendingResponse && !responseChoices.isEmpty {
                            VStack(alignment: .leading, spacing: 6) {
                                ForEach(responseChoices, id: \.self) { option in
                                    TransmissionResponseChoiceButton(
                                        option: option,
                                        disabled: isSendingResponse
                                    ) {
                                        responseDraft = option
                                    }
                                }
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                        }

                        if !isSendingResponse {
                        HStack(spacing: 8) {
                            TransmissionPhotoAttachmentButton(
                                photoPath: $responsePhotoPath,
                                showingSourceDialog: $showResponsePhotoSourceDialog,
                                pickerRequest: $responsePhotoPickerRequest
                            )

                            TransmissionResponseTextField(
                                text: $responseDraft,
                                sending: isSendingResponse
                            )

                            Button {
                                let text = responseDraft.trimmingCharacters(in: .whitespacesAndNewlines)
                                guard !isSendingResponse, !text.isEmpty else { return }
                                isSendingResponse = true
                                responseDraft = ""
                                onSelectOption(text, responsePhotoPath)
                                responsePhotoPath = nil
                            } label: {
                                Text(isSendingResponse ? "Sending…" : "Send")
                                    .font(.system(size: 15, weight: .black, design: .rounded))
                                    .foregroundStyle(.white)
                                    .frame(width: 76, height: 42)
                                    .background(Color(red: 1.0, green: 0.19, blue: 0.58), in: RoundedRectangle(cornerRadius: 7, style: .continuous))
                                    .overlay(RoundedRectangle(cornerRadius: 7, style: .continuous).stroke(Color.white.opacity(0.20), lineWidth: 1))
                            }
                            .buttonStyle(.plain)
                            .disabled(isSendingResponse)
                            .opacity(isSendingResponse || responseDraft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? 0.55 : 1.0)
                        }
                        }
                    }
                    .padding(.horizontal, 12)
                    .padding(.bottom, max(bottomSafe + composerBottomObstruction, keyboardHeight))
                    .frame(width: geometry.size.width, height: screenH, alignment: .bottom)
                    .animation(.easeOut(duration: 0.24), value: keyboardHeight)
                    .sheet(item: $responsePhotoPickerRequest) { request in
                        NativePhotoPicker(sourceType: request.source) { image, path in
                            if image != nil, let path {
                                responsePhotoPath = path
                            }
                            responsePhotoPickerRequest = nil
                        }
                        .ignoresSafeArea()
                    }
                }

                if currentEditableIdentity != nil && !editingPlot {
                    HStack(spacing: 10) {
                        Button(action: beginPlotEditing) {
                            Image(systemName: "pencil")
                                .font(.system(size: 18, weight: .black))
                                .foregroundStyle(.white)
                                .frame(width: 50, height: 50)
                                .background(.ultraThinMaterial, in: Circle())
                                .overlay(Circle().stroke(Color.white.opacity(0.28), lineWidth: 1))
                        }
                        .buttonStyle(.plain)

                        Button(action: beginMusicEditing) {
                            Image(systemName: "music.note")
                                .font(.system(size: 20, weight: .black))
                                .foregroundStyle(.white)
                                .frame(width: 50, height: 50)
                                .background(.ultraThinMaterial, in: Circle())
                                .overlay(Circle().stroke(Color(red: 1.0, green: 0.35, blue: 0.75).opacity(0.55), lineWidth: 1.2))
                        }
                        .buttonStyle(.plain)

                        if let onNewTransmission {
                            Button(action: onNewTransmission) {
                                Text("NEW TRANSMISSION")
                                    .font(.system(size: 16, weight: .black, design: .rounded))
                                    .foregroundStyle(.black.opacity(0.88))
                                    .frame(maxWidth: .infinity, minHeight: 50)
                                    .background(Color.green, in: Capsule())
                                    .overlay(Capsule().stroke(Color.white.opacity(0.48), lineWidth: 1.2))
                                    .shadow(color: Color.green.opacity(0.35), radius: 12, y: 3)
                            }
                            .buttonStyle(.plain)
                        }
                    }
                    .padding(.horizontal, 13)
                    .padding(.bottom, bottomSafe + 13)
                    .frame(width: geometry.size.width, height: screenH, alignment: .bottomLeading)
                    .zIndex(44)
                }

                if showingMusicEditor {
                    Color.black.opacity(0.34)
                        .ignoresSafeArea()
                        .contentShape(Rectangle())
                        .onTapGesture { if !musicSubmitting { closeMusicEditor() } }
                        .zIndex(47)

                    VStack(alignment: .leading, spacing: 14) {
                        HStack {
                            HStack(spacing: 6) {
                                Image(systemName: "music.note")
                                    .foregroundStyle(Color(red: 1.0, green: 0.35, blue: 0.75))
                                Text("SING YOUR TRANSMISSION")
                                    .font(.system(size: 13, weight: .black, design: .monospaced))
                                    .foregroundStyle(.white.opacity(0.85))
                            }
                            Spacer()
                            Button("Close") { if !musicSubmitting { closeMusicEditor() } }
                                .font(.system(size: 15, weight: .black))
                                .foregroundStyle(.white.opacity(0.72))
                                .disabled(musicSubmitting)
                        }

                        Text("Edit the lyrics — SING regenerates the vocal track over your existing instrumental using your voice model, then swaps the transmission's audio to the new lego mix.")
                            .font(.system(size: 12, weight: .semibold))
                            .foregroundStyle(.white.opacity(0.55))
                            .fixedSize(horizontal: false, vertical: true)

                        ScrollView(.vertical, showsIndicators: true) {
                            VStack(alignment: .leading, spacing: 10) {
                                ForEach($musicSections) { $section in
                                    VStack(alignment: .leading, spacing: 4) {
                                        Text(section.label.uppercased())
                                            .font(.system(size: 10, weight: .black, design: .monospaced))
                                            .foregroundStyle(section.label == "chorus"
                                                ? Color(red: 1.0, green: 0.35, blue: 0.75)
                                                : Color(red: 0.66, green: 1.0, blue: 0.76))
                                        TextEditor(text: $section.text)
                                            .font(.system(size: 15, weight: .semibold, design: .rounded))
                                            .foregroundStyle(.white)
                                            .tint(.white)
                                            .scrollContentBackgroundCompatHidden()
                                            .colorScheme(.dark)
                                            .frame(minHeight: 58, maxHeight: 120)
                                            .padding(6)
                                            .background(Color.white.opacity(0.07), in: RoundedRectangle(cornerRadius: 10))
                                            .overlay(RoundedRectangle(cornerRadius: 10).stroke(Color.white.opacity(0.16), lineWidth: 1))
                                            .disabled(musicSubmitting)
                                    }
                                }
                                HStack(spacing: 8) {
                                    ForEach(["verse", "chorus", "bridge"], id: \.self) { label in
                                        Button {
                                            musicSections.append(MusicLyricSection(label: label, text: ""))
                                        } label: {
                                            Text("+ \(label)")
                                                .font(.system(size: 11, weight: .black, design: .monospaced))
                                                .foregroundStyle(.white.opacity(0.75))
                                                .padding(.horizontal, 10)
                                                .padding(.vertical, 5)
                                                .background(Color.white.opacity(0.09), in: Capsule())
                                        }
                                        .buttonStyle(.plain)
                                        .disabled(musicSubmitting)
                                    }
                                }
                            }
                        }
                        .frame(minHeight: 160, maxHeight: 300)

                        if !musicStatus.isEmpty {
                            Text(musicStatus)
                                .font(.system(size: 12, weight: .semibold, design: .monospaced))
                                .foregroundStyle(.white.opacity(0.72))
                                .lineLimit(3)
                        }

                        Button(action: submitSing) {
                            HStack(spacing: 8) {
                                if musicSubmitting {
                                    ProgressView().tint(.black)
                                } else {
                                    Image(systemName: "waveform")
                                        .font(.system(size: 18, weight: .black))
                                }
                                Text(musicSubmitting ? "sending…" : "SING")
                                    .font(.system(size: 18, weight: .black, design: .rounded))
                            }
                            .foregroundStyle(.black)
                            .frame(maxWidth: .infinity, minHeight: 52)
                            .background(Color(red: 1.0, green: 0.35, blue: 0.75), in: Capsule())
                            .overlay(Capsule().stroke(Color.white.opacity(0.55), lineWidth: 1.2))
                        }
                        .buttonStyle(.plain)
                        .disabled(musicSubmitting)

                        Button(action: submitRemoveVocals) {
                            HStack(spacing: 8) {
                                Image(systemName: "speaker.slash")
                                    .font(.system(size: 14, weight: .black))
                                Text("REMOVE VOCALS")
                                    .font(.system(size: 13, weight: .black, design: .rounded))
                            }
                            .foregroundStyle(.white.opacity(0.82))
                            .frame(maxWidth: .infinity, minHeight: 40)
                            .background(Color.white.opacity(0.08), in: Capsule())
                            .overlay(Capsule().stroke(Color.white.opacity(0.22), lineWidth: 1))
                        }
                        .buttonStyle(.plain)
                        .disabled(musicSubmitting)
                    }
                    .padding(16)
                    .background(Color.black.opacity(0.96), in: RoundedRectangle(cornerRadius: 20, style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: 20).stroke(Color(red: 1.0, green: 0.35, blue: 0.75).opacity(0.30), lineWidth: 1))
                    .padding(.horizontal, 12)
                    .padding(.bottom, max(bottomSafe + 10, keyboard.height + 10))
                    .frame(width: geometry.size.width, height: screenH, alignment: .bottom)
                    .zIndex(48)
                }

                if editingPlot {
                    Color.black.opacity(0.28)
                        .ignoresSafeArea()
                        .contentShape(Rectangle())
                        .onTapGesture { savePlotEditing() }
                        .zIndex(45)

                    VStack(alignment: .leading, spacing: 12) {
                        HStack {
                            Text("EDIT TRANSMISSION")
                                .font(.system(size: 13, weight: .black, design: .monospaced))
                                .foregroundStyle(.white.opacity(0.76))
                            Spacer()
                            Button("Done") { savePlotEditing() }
                                .font(.system(size: 17, weight: .black))
                                .foregroundStyle(Color(red: 0.45, green: 0.88, blue: 1.0))
                        }
                        TextEditor(text: $plotEditDraft)
                            .focused($plotEditorFocused)
                            .font(.system(size: 18, weight: .bold, design: .rounded))
                            .foregroundStyle(.white)
                            .tint(.white)
                            .scrollContentBackgroundCompatHidden()
                            .colorScheme(.dark)
                            .frame(minHeight: 120, maxHeight: 180)
                            .padding(8)
                            .background(Color.white.opacity(0.07), in: RoundedRectangle(cornerRadius: 12))
                            .overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.white.opacity(0.18), lineWidth: 1))
                    }
                    .padding(16)
                    .background(Color.black.opacity(0.96), in: RoundedRectangle(cornerRadius: 20, style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: 20).stroke(Color.white.opacity(0.20), lineWidth: 1))
                    .padding(.horizontal, 12)
                    .padding(.bottom, max(bottomSafe + 10, keyboard.height + 10))
                    .frame(width: geometry.size.width, height: screenH, alignment: .bottom)
                    .zIndex(46)
                }

                if showingSettings {
                    VStack(alignment: .leading, spacing: 12) {
                        HStack {
                            Text("Playback Settings")
                                .font(.system(size: 13, weight: .bold, design: .rounded))
                                .foregroundStyle(.white)
                            Spacer()
                            Button(action: {
                                transmissionFizzyEdges = false
                                transmissionFXEnabled = true
                                transmissionFXIntensity = 0.5
                            }) {
                                Image(systemName: "arrow.counterclockwise")
                                    .font(.system(size: 10, weight: .bold))
                                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                            }
                            .buttonStyle(.plain)
                            .padding(.trailing, 8)
                            
                            Button(action: {
                                withAnimation { showingSettings = false }
                            }) {
                                Image(systemName: "xmark")
                                    .font(.system(size: 12, weight: .bold))
                                    .foregroundStyle(.white.opacity(0.7))
                            }
                            .buttonStyle(.plain)
                        }
                        .padding(.bottom, 4)

                        Toggle("Fizzy Edges", isOn: $transmissionFizzyEdges)
                            .font(.system(size: 12, weight: .semibold))
                            .foregroundStyle(.white)
                            .tint(Color(red: 0.66, green: 1.0, blue: 0.76))

                        Toggle("Glitch FX", isOn: $transmissionFXEnabled)
                            .font(.system(size: 12, weight: .semibold))
                            .foregroundStyle(.white)
                            .tint(Color(red: 0.66, green: 1.0, blue: 0.76))

                        VStack(alignment: .leading, spacing: 2) {
                            HStack {
                                Text("FX Intensity")
                                    .font(.system(size: 11, weight: .semibold))
                                    .foregroundStyle(.white)
                                Spacer()
                                Text(String(format: "%.2f", transmissionFXIntensity))
                                    .font(.system(size: 11, weight: .semibold, design: .monospaced))
                                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                            }
                            Slider(value: $transmissionFXIntensity, in: 0...1, step: 0.05)
                                .tint(Color(red: 0.66, green: 1.0, blue: 0.76))
                        }
                    }
                    .padding(14)
                    .frame(width: 240)
                    .background(Color.black.opacity(0.88))
                    .overlay(RoundedRectangle(cornerRadius: 14).stroke(Color.white.opacity(0.24), lineWidth: 1))
                    .cornerRadius(14)
                    .position(x: geometry.size.width - 130, y: buttonRowY + 120)
                    .transition(.asymmetric(insertion: .scale.combined(with: .opacity), removal: .opacity))
                    .zIndex(40)
                }

                if showResponderCard, let responder = responderCardUser {
                    NearbyUserInfoCard(
                        user: responder,
                        locationText: K1L0OverlayDataModel.activeModel?.userLocationText(responder) ?? "signal origin unknown",
                        onDismiss: {
                            withAnimation(.spring(response: 0.3, dampingFraction: 0.9)) { showResponderCard = false }
                        }
                    )
                    .transition(.move(edge: .bottom).combined(with: .opacity))
                    .zIndex(60)
                }

            }
            .ignoresSafeArea()
            // Belt and braces: never let the Unity map peek through below the
            // safe area — the whole player sits on opaque black.
            .background(Color.black.ignoresSafeArea())
            .onAppear {
                K1L0WeatherOverlayInstaller.setVideoBackdropActive(true)
            }
            .onDisappear {
                K1L0WeatherOverlayInstaller.setVideoBackdropActive(false)
            }
            // SwiftUI-native keyboard tracking so the composer row + adjoining
            // buttons rise above the keyboard when RESPOND is focused. The
            // panel's outer .ignoresSafeArea() defeats built-in avoidance.
            .onReceive(NotificationCenter.default.publisher(for: UIResponder.keyboardWillChangeFrameNotification)) { notif in
                guard let frame = notif.userInfo?[UIResponder.keyboardFrameEndUserInfoKey] as? CGRect else { return }
                let screen = UIScreen.main.bounds
                keyboardHeight = max(0, screen.maxY - frame.minY)
            }
            .onReceive(NotificationCenter.default.publisher(for: UIResponder.keyboardWillHideNotification)) { _ in
                keyboardHeight = 0
            }
            .onChange(of: transmissionFizzyEdges) { val in
                K1L0WeatherOverlayInstaller.setUnitySetting("transmissionFizzyEdges", val ? "1" : "0")
            }
            .onChange(of: transmissionFXEnabled) { val in
                K1L0WeatherOverlayInstaller.setUnitySetting("transmissionFX", val ? "1" : "0")
            }
            .onChange(of: transmissionFXIntensity) { val in
                K1L0WeatherOverlayInstaller.setUnitySetting("transmissionFXIntensity", String(format: "%.2f", val))
            }
            .onChange(of: currentClipIndex) { _ in
                if editingPlot { savePlotEditing() }
            }
            .task(id: transmissionMediaKey) {
                videoReadyForText = false
                mediaPreloader.prepare(urls: transmissionMediaURLs)
            }
        }
    }

    private func beginPlotEditing() {
        guard currentEditableIdentity != nil else { return }
        plotEditDraft = visiblePlotText
        editingPlot = true
        DispatchQueue.main.async { plotEditorFocused = true }
    }

    private func savePlotEditing() {
        guard let identity = currentEditableIdentity else {
            editingPlot = false
            plotEditorFocused = false
            return
        }
        let clean = plotEditDraft.trimmingCharacters(in: .whitespacesAndNewlines)
        editedPlots[identity.jobId] = clean
        if K1L0ActiveTransmissionStore.shared.snapshot.jobId == identity.jobId {
            K1L0ActiveTransmissionStore.shared.updateResponsePlot(clean)
        }
        editingPlot = false
        plotEditorFocused = false
        k1l0PersistTransmissionPlot(jobId: identity.jobId, userId: identity.ownerUserId, responsePlot: clean)
    }

    // MARK: — Music (SING) editor
    private func beginMusicEditing() {
        guard musicRootIdentity != nil else { return }
        // Seed lyrics from the transmission's plan (or an empty draft if not
        // present). We store per-jobId edits in memory; the server persists on
        // submit anyway.
        // Prefer any edits the user made in this session; otherwise seed from
        // the transmission's own baked lyrics.
        let seed = musicLyricsDraft.isEmpty ? result.lyrics : musicLyricsDraft
        musicLyricsDraft = seed
        musicSections = Self.parseLyricSections(seed)
        musicStatus = ""
        showingMusicEditor = true
        DispatchQueue.main.async { musicEditorFocused = true }
        // Player results are often built with empty lyrics (chain/thread
        // builders don't carry them) — pull the real ones off the root job.
        if seed.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            fetchLyricsForEditor()
        }
    }

    private func fetchLyricsForEditor() {
        guard let identity = musicRootIdentity else { return }
        musicStatus = "loading lyrics…"
        let apiBase = K1L0OverlayDataModel.activeModel?.activeAPIBase ?? "https://api-tunnel.kilo.gallery"
        let encoded = identity.ownerUserId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? identity.ownerUserId
        guard let url = URL(string: "\(apiBase)/api/k1l0/v2/transmit/\(identity.jobId)?userId=\(encoded)") else {
            musicStatus = ""
            return
        }
        URLSession.shared.dataTask(with: URLRequest(url: url, timeoutInterval: 15)) { data, _, _ in
            var lyrics = ""
            if let data,
               let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
                let plan = root["plan"] as? [String: Any]
                let music = plan?["music"] as? [String: Any]
                let variants = root["musicVariants"] as? [[String: Any]]
                lyrics = (music?["lyrics"] as? String).flatMap { $0.isEmpty ? nil : $0 }
                    ?? variants?.compactMap({ $0["lyrics"] as? String }).first(where: { !$0.isEmpty })
                    ?? ""
            }
            DispatchQueue.main.async {
                if musicLyricsDraft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, !lyrics.isEmpty {
                    musicLyricsDraft = lyrics
                    musicSections = Self.parseLyricSections(lyrics)
                }
                if musicStatus == "loading lyrics…" {
                    musicStatus = lyrics.isEmpty ? "no lyrics on this transmission yet." : ""
                }
            }
        }.resume()
    }

    private func closeMusicEditor() {
        showingMusicEditor = false
        musicEditorFocused = false
    }

    // Standard lyric format: [verse] / [chorus] / [bridge] section markers,
    // each followed by its lines — the same convention ACE-Step consumes.
    static func parseLyricSections(_ raw: String) -> [MusicLyricSection] {
        // Markers appear both on their own lines and inline ("[verse] text/
        // [chorus] text"), so split on the marker tokens wherever they occur.
        let pattern = "\\[(verse|chorus|bridge|intro|outro|hook|pre-chorus|prechorus)\\]"
        guard let regex = try? NSRegularExpression(pattern: pattern, options: [.caseInsensitive]) else {
            return [MusicLyricSection(label: "verse", text: raw)]
        }
        let ns = raw as NSString
        let matches = regex.matches(in: raw, range: NSRange(location: 0, length: ns.length))
        let junk = CharacterSet(charactersIn: " \n/")
        if matches.isEmpty {
            let text = raw.trimmingCharacters(in: .whitespacesAndNewlines)
            return [MusicLyricSection(label: "verse", text: text)]
        }
        var sections: [MusicLyricSection] = []
        if matches[0].range.location > 0 {
            let lead = ns.substring(to: matches[0].range.location).trimmingCharacters(in: junk)
            if !lead.isEmpty { sections.append(MusicLyricSection(label: "verse", text: lead)) }
        }
        for (index, match) in matches.enumerated() {
            let label = ns.substring(with: match.range(at: 1)).lowercased()
            let start = match.range.location + match.range.length
            let end = index + 1 < matches.count ? matches[index + 1].range.location : ns.length
            let text = ns.substring(with: NSRange(location: start, length: end - start))
                .trimmingCharacters(in: junk)
            sections.append(MusicLyricSection(label: label, text: text))
        }
        if sections.isEmpty { sections = [MusicLyricSection(label: "verse", text: "")] }
        return sections
    }

    private func assembledLyrics() -> String {
        musicSections
            .filter { !$0.text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
            .map { "[\($0.label)]\n\($0.text.trimmingCharacters(in: .whitespacesAndNewlines))" }
            .joined(separator: "\n")
    }

    private func submitRemoveVocals() {
        guard let identity = musicRootIdentity else { return }
        musicSubmitting = true
        musicStatus = "removing vocals…"
        let apiBase = K1L0OverlayDataModel.activeModel?.activeAPIBase ?? "https://api-tunnel.kilo.gallery"
        guard let url = URL(string: "\(apiBase)/api/k1l0/v2/remove-vocals") else {
            musicSubmitting = false
            musicStatus = "bad api base"
            return
        }
        var req = URLRequest(url: url)
        req.httpMethod = "POST"
        req.setValue("application/json", forHTTPHeaderField: "Content-Type")
        req.httpBody = try? JSONSerialization.data(withJSONObject: [
            "userId": identity.ownerUserId,
            "jobId": identity.jobId,
        ])
        URLSession.shared.dataTask(with: req) { data, response, error in
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            DispatchQueue.main.async {
                musicSubmitting = false
                if let err = error {
                    musicStatus = "error: \(err.localizedDescription)"
                } else if code >= 200 && code < 300 {
                    musicStatus = "vocals removed — instrumental restores on next load."
                    DispatchQueue.main.asyncAfter(deadline: .now() + 2.0) {
                        if musicStatus.hasPrefix("vocals removed") { closeMusicEditor() }
                    }
                } else {
                    let body = data.flatMap { String(data: $0, encoding: .utf8) } ?? ""
                    musicStatus = "server said \(code): \(body.prefix(120))"
                }
            }
        }.resume()
    }

    private func submitSing() {
        guard let identity = musicRootIdentity else { return }
        musicLyricsDraft = assembledLyrics()
        let cleanLyrics = musicLyricsDraft.trimmingCharacters(in: .whitespacesAndNewlines)
        if cleanLyrics.isEmpty {
            musicStatus = "Lyrics can't be empty."
            return
        }
        musicSubmitting = true
        musicStatus = "sending to lego pipeline…"
        let apiBase = K1L0OverlayDataModel.activeModel?.activeAPIBase ?? "https://api-tunnel.kilo.gallery"
        guard let url = URL(string: "\(apiBase)/api/k1l0/v2/gen-lego") else {
            musicSubmitting = false
            musicStatus = "bad api base"
            return
        }
        var req = URLRequest(url: url)
        req.httpMethod = "POST"
        req.setValue("application/json", forHTTPHeaderField: "Content-Type")
        req.httpBody = try? JSONSerialization.data(withJSONObject: [
            "userId": identity.ownerUserId,
            "jobId": identity.jobId,
            "lyrics": cleanLyrics,
            "loraScale": 0.75,
        ])
        URLSession.shared.dataTask(with: req) { data, response, error in
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            DispatchQueue.main.async {
                musicSubmitting = false
                if let err = error {
                    musicStatus = "error: \(err.localizedDescription)"
                    return
                }
                if code >= 200 && code < 300 {
                    musicStatus = "lego started — song will swap on next load (~2-3 min)."
                    // Auto-close after a moment so the user can see status.
                    DispatchQueue.main.asyncAfter(deadline: .now() + 2.4) {
                        if musicStatus.hasPrefix("lego started") { closeMusicEditor() }
                    }
                } else {
                    let body = data.flatMap { String(data: $0, encoding: .utf8) } ?? ""
                    musicStatus = "server said \(code): \(body.prefix(120))"
                }
            }
        }.resume()
    }
}

// Real device safe-area insets, for views that ignore safe areas (their
// GeometryReader reports zero insets because the parent consumed them).
func k1l0DeviceSafeAreaInsets() -> (top: CGFloat, bottom: CGFloat) {
#if canImport(UIKit)
    let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
    for scene in scenes {
        if let window = scene.windows.first(where: { $0.isKeyWindow }) ?? scene.windows.first {
            return (window.safeAreaInsets.top, window.safeAreaInsets.bottom)
        }
    }
#endif
    return (0, 0)
}

// True physical screen size from the key window. The transmission panel's
// GeometryReader can report a safe-area-inset (non-fullscreen) height depending
// on how the .overlay(alignment: .bottom) host consumes insets — so the response
// composer is bottom-anchored against this real height, not geometry.size.height,
// or the RESPOND field floats above the bottom safe-area line.
func k1l0DeviceScreenSize() -> CGSize {
#if canImport(UIKit)
    let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
    for scene in scenes {
        if let window = scene.windows.first(where: { $0.isKeyWindow }) ?? scene.windows.first {
            return window.bounds.size
        }
    }
    return UIScreen.main.bounds.size
#else
    return .zero
#endif
}

struct TransmissionTextTransform: Codable, Equatable {
    var xRatio: Double = 0.50
    var yRatio: Double = 0.18
    var scale: Double = 1.0
    var rotationDegrees: Double = 0

    func clamped() -> TransmissionTextTransform {
        TransmissionTextTransform(
            xRatio: min(0.92, max(0.08, xRatio)),
            yRatio: min(0.82, max(0.08, yRatio)),
            scale: min(1.9, max(0.62, scale)),
            rotationDegrees: 0
        )
    }
}

func k1l0AspectFitRect(container: CGSize, aspectRatio: CGFloat = 9.0 / 16.0) -> CGRect {
    guard container.width > 1, container.height > 1, aspectRatio > 0 else {
        return CGRect(origin: .zero, size: container)
    }
    let containerRatio = container.width / container.height
    if containerRatio > aspectRatio {
        let height = container.height
        let width = height * aspectRatio
        return CGRect(x: (container.width - width) * 0.5, y: 0, width: width, height: height)
    }
    let width = container.width
    let height = width / aspectRatio
    return CGRect(x: 0, y: (container.height - height) * 0.5, width: width, height: height)
}

enum TransmissionTextTransformStore {
    private static let key = "k1lo_transmission_text_transform_v1"

    static func load() -> TransmissionTextTransform {
        guard let data = UserDefaults.standard.data(forKey: key),
              let decoded = try? JSONDecoder().decode(TransmissionTextTransform.self, from: data) else {
            return TransmissionTextTransform()
        }
        return decoded.clamped()
    }

    static func save(_ transform: TransmissionTextTransform) {
        guard let data = try? JSONEncoder().encode(transform.clamped()) else { return }
        UserDefaults.standard.set(data, forKey: key)
    }
}

struct DraggableTransmissionTextOverlay: View {
    let text: String
    @Binding var transform: TransmissionTextTransform
    let canvasSize: CGSize
    var canvasOrigin: CGPoint = .zero
    var allowEditing: Bool = false
    var useExternalTypewriter: Bool = true
    var animateTypewriter: Bool = true
    var onTypewriterFinished: (String) -> Void = { _ in }
    var bottomInset: CGFloat = 66
    @State private var visibleCharacterCount = 0
    private let maxTextHeight: CGFloat = 118

    private var cleanText: String {
        text.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private var visibleText: String {
        String(cleanText.prefix(visibleCharacterCount))
    }

    private var displayedText: String {
        useExternalTypewriter && animateTypewriter ? visibleText : cleanText
    }

    private var boxWidth: CGFloat {
        max(190, canvasSize.width - 16)
    }

    var body: some View {
        VStack(spacing: 4) {
            TransmissionPlotRibbon(text: displayedText, allowEditing: allowEditing, animateText: !useExternalTypewriter)
                .frame(width: boxWidth)
                .frame(height: maxTextHeight, alignment: .bottom)
        }
        .position(
            x: canvasOrigin.x + canvasSize.width * 0.5,
            y: canvasOrigin.y + canvasSize.height - bottomInset - (maxTextHeight * 0.5)
        )
        .transaction { transaction in
            transaction.animation = nil
        }
        .onAppear { restartTypewriter() }
        .onChange(of: cleanText) { _ in restartTypewriter() }
    }

    private func restartTypewriter() {
        if !useExternalTypewriter {
            visibleCharacterCount = cleanText.count
            onTypewriterFinished(cleanText)
            return
        }
        if !animateTypewriter {
            visibleCharacterCount = cleanText.count
            onTypewriterFinished(cleanText)
            return
        }
        visibleCharacterCount = 0
        let maxCount = cleanText.count
        guard maxCount > 0 else { return }
        let interval = max(0.012, min(0.045, 1.7 / Double(maxCount)))
        for index in 1...maxCount {
            DispatchQueue.main.asyncAfter(deadline: .now() + interval * Double(index)) {
                if cleanText == text.trimmingCharacters(in: .whitespacesAndNewlines) {
                    visibleCharacterCount = max(visibleCharacterCount, index)
                    if index == maxCount {
                        onTypewriterFinished(cleanText)
                    }
                }
            }
        }
    }
}

/// Radio-static hiss played while an incoming transmission is tuning in.
/// Pure synthesis (no asset): band-limited white noise + crackle pops, gain
/// tied to how detuned the signal is (1 = full static, 0 = silent).
final class K1L0TuningStaticPlayer {
    static let shared = K1L0TuningStaticPlayer()

    private let engine = AVAudioEngine()
    private var sourceNode: AVAudioSourceNode?
    private var running = false
    // Written from SwiftUI, read on the render thread — atomic enough for a gain knob.
    private var targetLevel: Float = 0

    private init() {}

    func setDetune(_ detune: Double) {
        targetLevel = Float(min(1.0, max(0.0, detune)))
    }

    func start() {
        guard !running else { return }
        let sampleRate = 44_100.0
        guard let format = AVAudioFormat(standardFormatWithSampleRate: sampleRate, channels: 1) else { return }

        var smoothedLevel: Float = 0
        var lowpass: Float = 0
        var crackle: Float = 0
        var seed: UInt32 = 22_222

        let node = AVAudioSourceNode(format: format) { [weak self] _, _, frameCount, audioBufferList -> OSStatus in
            let target = self?.targetLevel ?? 0
            let buffers = UnsafeMutableAudioBufferListPointer(audioBufferList)
            guard let out = buffers.first?.mData?.assumingMemoryBound(to: Float.self) else { return noErr }
            for frame in 0..<Int(frameCount) {
                // xorshift white noise
                seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5
                let white = Float(bitPattern: 0x3F800000 | (seed >> 9)) - 1.5 // ~[-0.5, 0.5]
                // one-pole lowpass so it reads as radio hiss, not digital fizz
                lowpass += 0.18 * (white - lowpass)
                // sparse crackle pops
                if seed % 100_003 < 2 { crackle = white * 3.0 }
                crackle *= 0.988
                // smooth the gain to avoid zipper noise
                smoothedLevel += 0.0008 * (target - smoothedLevel)
                out[frame] = (lowpass * 0.9 + crackle * 0.5) * smoothedLevel * 0.30
            }
            return noErr
        }

        engine.attach(node)
        engine.connect(node, to: engine.mainMixerNode, format: format)
        sourceNode = node
        do {
            try engine.start()
            running = true
        } catch {
            engine.detach(node)
            sourceNode = nil
        }
    }

    func stop() {
        guard running else { return }
        engine.stop()
        if let node = sourceNode {
            engine.detach(node)
            sourceNode = nil
        }
        running = false
    }
}

struct IncomingMessageReceiverView: View {
    @ObservedObject var data: K1L0OverlayDataModel
    let onCancel: () -> Void

    @State private var showingCancelConfirmation = false

    private var progress: Double {
        min(1, max(0, Double(data.receiveProgressSteps) / Double(max(1, data.receiveStepsRequired()))))
    }

    // Compressed progress for the glitch layers only: still visibly untuned
    // at 99%, snapping clean only at a true 100%.
    private var visualProgress: Double { progress >= 1.0 ? 1.0 : progress * 0.85 }

    private var remainingSteps: Int {
        max(0, data.receiveStepsRequired() - data.receiveProgressSteps)
    }

    var body: some View {
        Group {
            if let incoming = data.incomingTransmission {
                GeometryReader { geometry in
                    ZStack {
                        Color.black

                        if let raw = incoming.thumbUrl,
                           let url = URL(string: raw) {
                            AsyncImage(url: url) { phase in
                                if case .success(let image) = phase {
                                    IncomingSignalThumbnailImage(image: image, progress: progress)
                                } else {
                                    WarblyStaticView()
                                }
                            }
                            .frame(width: geometry.size.width, height: geometry.size.height)
                            .clipped()
                        } else {
                            WarblyStaticView()
                        }

                        // The same tuning components used by the transmission
                        // player, expanded over the entire receiver screen.
                        // visualProgress holds these effects visibly detuned
                        // at 99%; only a true 100% clears them.
                        SignalTuningWaveView(progress: visualProgress)
                            .blendMode(.screen)
                            .opacity(0.92)
                            .allowsHitTesting(false)
                        PixelBreakupView(progress: visualProgress)
                            .opacity(0.72 * (1.0 - visualProgress))
                            .allowsHitTesting(false)
                        WarblyStaticView()
                            .blendMode(.screen)
                            .opacity(0.24 + 0.58 * (1.0 - visualProgress))
                            .allowsHitTesting(false)

                        LinearGradient(
                            colors: [
                                Color.black.opacity(0.56),
                                Color.clear,
                                Color.black.opacity(0.30),
                                Color.black.opacity(0.82)
                            ],
                            startPoint: .top,
                            endPoint: .bottom
                        )
                        .allowsHitTesting(false)

                        VStack(spacing: 0) {
                            HStack {
                                Spacer()
                                Button {
                                    showingCancelConfirmation = true
                                } label: {
                                    Image(systemName: "xmark")
                                        .font(.system(size: 18, weight: .black))
                                        .foregroundStyle(.white)
                                        .frame(width: 46, height: 46)
                                        .background(Color.black.opacity(0.54), in: Circle())
                                        .overlay(Circle().stroke(Color.white.opacity(0.32), lineWidth: 1))
                                }
                                .buttonStyle(.plain)
                                .accessibilityLabel("Cancel incoming message")
                            }
                            .padding(.horizontal, 18)
                            // The receiver is presented with .ignoresSafeArea(),
                            // which zeroes geometry.safeAreaInsets — fall back to
                            // the device inset so the close button clears the
                            // status bar.
                            .padding(.top, max(geometry.safeAreaInsets.top, k1l0DeviceSafeAreaInsets().top) + 6)

                            Spacer()

                            VStack(spacing: 17) {
                                Text("Incoming message from \(incoming.senderLabel)…")
                                    .font(.system(size: 26, weight: .black, design: .monospaced))
                                    .foregroundStyle(.white)
                                    .multilineTextAlignment(.center)
                                    .shadow(color: .black, radius: 8)

                                Text("Walk \(K1L0StepText(remainingSteps)) to receive message")
                                    .font(.system(size: 19, weight: .black, design: .monospaced))
                                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                                    .multilineTextAlignment(.center)
                                    .shadow(color: .black, radius: 7)

                                TenBarSignalMeter(strength: progress)
                                    .scaleEffect(x: 2.15, y: 1.9)
                                    .frame(height: 44)

                                Text("\(Int((progress * 100).rounded()))%")
                                    .font(.system(size: 17, weight: .black, design: .monospaced))
                                    .foregroundStyle(.white)
                                    .shadow(color: .black, radius: 5)
                            }
                            .padding(.horizontal, 28)
                            .padding(.bottom, max(38, geometry.safeAreaInsets.bottom + 34))
                        }
                    }
                    .frame(width: geometry.size.width, height: geometry.size.height)
                    .clipped()
                }
                .background(Color.black)
                .onAppear {
                    K1L0TuningStaticPlayer.shared.setDetune(1.0 - progress)
                    if progress < 1.0 { K1L0TuningStaticPlayer.shared.start() }
                }
                .onDisappear {
                    K1L0TuningStaticPlayer.shared.stop()
                }
                .onChange(of: progress) { newValue in
                    K1L0TuningStaticPlayer.shared.setDetune(1.0 - newValue)
                    if newValue >= 1.0 {
                        K1L0TuningStaticPlayer.shared.stop()
                    } else {
                        K1L0TuningStaticPlayer.shared.start()
                    }
                }
                .alert("Cancel incoming message?", isPresented: $showingCancelConfirmation) {
                    Button("Keep Receiving", role: .cancel) { }
                    Button("Cancel Incoming Message", role: .destructive, action: onCancel)
                } message: {
                    Text("This incoming signal will be discarded.")
                }
            }
        }
    }
}

struct IncomingSignalHUD: View {
    @ObservedObject var data: K1L0OverlayDataModel

    private var progress: Double {
        min(1, max(0, Double(data.receiveProgressSteps) / Double(max(1, data.receiveStepsRequired()))))
    }

    private var percentText: String {
        "\(Int((progress * 100).rounded()))%"
    }

    var body: some View {
        if let incoming = data.incomingTransmission {
            VStack(alignment: .center, spacing: 6) {
                Text("TUNING INTO \(incoming.senderLabel.uppercased())")
                    .font(.system(size: 15, weight: .black, design: .monospaced))
                    .foregroundStyle(.white)
                    .multilineTextAlignment(.center)
                    .lineLimit(2)
                    .minimumScaleFactor(0.72)
                    .frame(maxWidth: 320)
                if let raw = incoming.thumbUrl, let url = URL(string: raw) {
                    AsyncImage(url: url) { phase in
                        switch phase {
                        case .success(let image):
                            IncomingSignalThumbnailImage(image: image, progress: progress)
                        case .failure:
                            Text("image unavailable")
                                .font(.system(size: 10, weight: .bold, design: .monospaced))
                                .foregroundStyle(.white.opacity(0.62))
                        default:
                            ProgressView()
                                .tint(.white)
                        }
                    }
                    .frame(width: 150, height: 150)
                    .clipped()
                }
                VStack(spacing: 5) {
                    Text("WALK TO TUNE THE SIGNAL")
                        .font(.system(size: 16, weight: .black, design: .monospaced))
                        .foregroundStyle(.white)
                    TenBarSignalMeter(strength: progress)
                        .scaleEffect(x: 1.75, y: 1.55)
                        .frame(height: 32)
                    Text(percentText)
                        .font(.system(size: 16, weight: .black, design: .monospaced))
                        .foregroundStyle(.white)
                }
            }
            .fixedSize(horizontal: false, vertical: true)
            .frame(maxWidth: 340)
            .padding(.horizontal, 10)
            .padding(.vertical, 9)
            .onAppear {
                K1L0TuningStaticPlayer.shared.setDetune(1.0 - progress)
                if progress < 1.0 { K1L0TuningStaticPlayer.shared.start() }
            }
            .onDisappear { K1L0TuningStaticPlayer.shared.stop() }
            .onChange(of: progress) { newValue in
                K1L0TuningStaticPlayer.shared.setDetune(1.0 - newValue)
                if newValue >= 1.0 {
                    K1L0TuningStaticPlayer.shared.stop()
                } else {
                    K1L0TuningStaticPlayer.shared.start()
                }
            }
        }
    }
}

struct IncomingSignalThumbnailImage: View {
    let image: Image
    let progress: Double

    // Hold the glitch until the signal is truly complete: the image forms and
    // grows legible through the climb, but at 99% it is still visibly
    // untuned — only a real 100% snaps fully clean.
    private var clamped: Double {
        let p = min(1, max(0, progress))
        return p >= 1.0 ? 1.0 : p * 0.85
    }
    private var unresolved: Double { 1.0 - clamped }
    private var imageReveal: Double { min(1.0, max(0.0, (clamped - 0.12) / 0.70)) }
    private var lowResolutionSize: CGFloat {
        CGFloat(3.0 + clamped * 95.0)
    }

    var body: some View {
        GeometryReader { geometry in
            TimelineView(.periodic(from: .now, by: 0.12)) { timeline in
                thumbnailContent(size: geometry.size, time: timeline.date.timeIntervalSinceReferenceDate)
            }
        }
    }

    // True when the compiled metallib actually contains our tuning shader.
    // If the .metal file didn't make it into the build, fall back to the
    // legacy Canvas effect instead of silently showing a clean image.
    private static let tuningShaderAvailable: Bool = {
        guard let device = MTLCreateSystemDefaultDevice() else { return false }
        let bundle = Bundle(for: K1L0TuningStaticPlayer.self)
        guard let lib = try? device.makeDefaultLibrary(bundle: bundle) else { return false }
        return lib.functionNames.contains("k1l0TuningStatic")
    }()

    @ViewBuilder
    private func thumbnailContent(size: CGSize, time: TimeInterval) -> some View {
        if #available(iOS 17.0, macOS 14.0, *), Self.tuningShaderAvailable {
            image
                .resizable()
                .scaledToFill()
                .frame(width: size.width, height: size.height)
                .clipped()
                .layerEffect(
                    // Bundle-scoped lookup: the .metal compiles into whichever
                    // binary hosts this Swift file (UnityFramework), not the
                    // main app bundle where ShaderLibrary.default looks.
                    ShaderLibrary.bundle(Bundle(for: K1L0TuningStaticPlayer.self)).k1l0TuningStatic(
                        .float2(Float(size.width), Float(size.height)),
                        .float(Float(time.truncatingRemainder(dividingBy: 3600))),
                        .float(Float(clamped))
                    ),
                    maxSampleOffset: CGSize(width: size.width, height: size.height)
                )
        } else {
            legacyThumbnailContent(size: size, time: time)
        }
    }

    private func legacyThumbnailContent(size: CGSize, time: TimeInterval) -> some View {
        let pixelScale = Swift.max(CGFloat(1), size.width / Swift.max(CGFloat(1), lowResolutionSize))
        return ZStack {
            scrambledBlocks(size: size, time: time)
                .opacity(0.98 * unresolved)
            slicedPixelImage(size: size, time: time, pixelScale: pixelScale)
                .opacity(0.10 + imageReveal * 0.90)
            slicedPixelImage(size: size, time: time + 0.37, pixelScale: pixelScale)
                .blendMode(.screen)
                .opacity(0.20 * unresolved * imageReveal)
            SignalTuningWaveView(progress: progress)
                .opacity(0.90)
            PixelBreakupView(progress: progress)
                .opacity(0.62 * unresolved)
        }
        .frame(width: size.width, height: size.height)
        .mask(SignalTuningShape(progress: progress, phase: time))
        .clipped()
    }

    private func slicedPixelImage(size: CGSize, time: TimeInterval, pixelScale: CGFloat) -> some View {
        let sliceCount = 22
        let sliceHeight = size.height / CGFloat(sliceCount)
        return ZStack(alignment: .topLeading) {
            ForEach(0..<sliceCount, id: \.self) { index in
                let rowY = CGFloat(index) * sliceHeight
                let phase = time * 6.4 + Double(index) * 0.58
                let wave = sin(phase) * Double(size.width) * (0.22 * unresolved)
                let ripple = sin(time * 17.0 + Double(index) * 1.91) * Double(size.width) * (0.035 * unresolved)
                pixelatedImage
                    .scaleEffect(pixelScale)
                    .frame(width: size.width, height: size.height)
                    .offset(x: CGFloat(wave + ripple), y: -rowY)
                    .frame(width: size.width, height: sliceHeight, alignment: .top)
                    .clipped()
                    .offset(y: rowY)
            }
        }
        .frame(width: size.width, height: size.height)
    }

    private func scrambledBlocks(size: CGSize, time: TimeInterval) -> some View {
        Canvas { context, canvasSize in
            let block = max(10.0, 34.0 - clamped * 24.0)
            let columns = Int(ceil(canvasSize.width / block))
            let rows = Int(ceil(canvasSize.height / block))
            let tick = floor(time * (7.0 + unresolved * 11.0))
            context.fill(Path(CGRect(origin: .zero, size: canvasSize)), with: .color(.black.opacity(0.48 + 0.30 * unresolved)))
            for row in 0..<rows {
                for column in 0..<columns {
                    let seed = sin(Double(row * 113 + column * 47) + tick * 2.31)
                    let seed2 = cos(Double(row * 37 + column * 131) + tick * 1.73)
                    let swapX = Double((column + Int(abs(seed) * Double(columns))) % max(1, columns)) * block
                    let swapY = Double((row + Int(abs(seed2) * Double(rows))) % max(1, rows)) * block
                    let driftX = sin(time * 8.0 + Double(row) * 0.77) * block * 1.6 * unresolved
                    let rect = CGRect(
                        x: swapX + driftX,
                        y: swapY,
                        width: block + 1,
                        height: block + 1
                    )
                    let green = 0.18 + 0.44 * abs(seed)
                    let alpha = (0.22 + 0.50 * abs(seed2)) * unresolved
                    context.fill(Path(rect), with: .color(Color(red: 0.02, green: green, blue: 0.10).opacity(alpha)))
                    if abs(seed) > 0.72 {
                        context.fill(Path(rect.insetBy(dx: block * 0.22, dy: block * 0.22)), with: .color(.white.opacity(0.18 * unresolved)))
                    }
                }
            }
        }
    }

    private var pixelatedImage: some View {
        image
            .resizable()
            .interpolation(.none)
            .scaledToFill()
            .frame(width: lowResolutionSize, height: lowResolutionSize)
            .clipped()
    }
}

struct SignalTuningShape: Shape {
    let progress: Double
    let phase: TimeInterval

    func path(in rect: CGRect) -> Path {
        let clamped = min(1.0, max(0.0, progress))
        let unresolved = 1.0 - clamped
        let amplitude = rect.width * CGFloat(0.20 * unresolved)
        let sliceCount = 28
        var left: [CGPoint] = []
        var right: [CGPoint] = []

        for index in 0...sliceCount {
            let t = CGFloat(index) / CGFloat(sliceCount)
            let y = rect.minY + rect.height * t
            let coarse = sin(phase * 5.6 + Double(index) * 0.84)
            let fine = sin(phase * 17.0 + Double(index) * 1.97)
            let leftOffset = amplitude * CGFloat(0.62 * coarse + 0.30 * fine)
            let rightOffset = amplitude * CGFloat(0.58 * sin(phase * 4.9 + Double(index) * 0.71 + 1.7) + 0.34 * fine)
            left.append(CGPoint(x: rect.minX + max(0, leftOffset), y: y))
            right.append(CGPoint(x: rect.maxX + min(0, rightOffset), y: y))
        }

        var path = Path()
        path.move(to: left.first ?? CGPoint(x: rect.minX, y: rect.minY))
        for point in left.dropFirst() {
            path.addLine(to: point)
        }
        for point in right.reversed() {
            path.addLine(to: point)
        }
        path.closeSubpath()
        return path
    }
}

struct IncomingSignalSkyOverlay: View {
    @ObservedObject var data: K1L0OverlayDataModel

    private var progress: Double {
        min(1, max(0, Double(data.receiveProgressSteps) / Double(max(1, data.receiveStepsRequired()))))
    }

    var body: some View {
        if let incoming = data.incomingTransmission,
           progress >= 1.0,
           let raw = incoming.thumbUrl,
           let url = URL(string: raw) {
            GeometryReader { geometry in
                TimelineView(.periodic(from: .now, by: 0.18)) { timeline in
                    let time = timeline.date.timeIntervalSinceReferenceDate
                    ZStack {
                        AsyncImage(url: url) { phase in
                            if case .success(let image) = phase {
                                IncomingSignalSkyImage(
                                    image: image,
                                    progress: progress,
                                    size: geometry.size,
                                    time: time
                                )
                            } else {
                                WarblyStaticView()
                                    .opacity(0.30)
                            }
                        }
                        PixelBreakupView(progress: progress)
                            .opacity(0.46 * (1.0 - progress))
                        WarblyStaticView()
                            .opacity(0.34 * (1.0 - progress))
                    }
                    .frame(width: geometry.size.width, height: geometry.size.height, alignment: .top)
                    .mask(
                        LinearGradient(
                            stops: [
                                .init(color: .white, location: 0.0),
                                .init(color: .white, location: 0.56),
                                .init(color: .clear, location: 0.86)
                            ],
                            startPoint: .top,
                            endPoint: .bottom
                        )
                    )
                }
            }
        }
    }
}

struct WalkingSkyAlert: View {
    let items: [K1L0MarqueeItem]
    let dotPhase: Int
    let statusHUD: Bool
    let contactRequest: OverlayIncomingTransmission?
    let dwellPlace: OverlayPlace?
    let dwellProgress: Double
    let onItemTap: (K1L0MarqueeItem) -> Void
    let onAcceptContact: () -> Void
    let onDwellTap: () -> Void
    let onDeclineContact: () -> Void

    @State private var currentIndex = 0
    private let timer = Timer.publish(every: 4.0, on: .main, in: .common).autoconnect()

    var body: some View {
        GeometryReader { geometry in
            Group {
                if contactRequest != nil || !items.isEmpty {
                    if statusHUD {
                        VStack(alignment: .leading, spacing: 6) {
                            if let contactRequest {
                                contactRow(contactRequest)
                                    .frame(maxWidth: geometry.size.width - 36, alignment: .leading)
                            }
                            if let dwellPlace {
                                LocationDwellStatusChip(
                                    place: dwellPlace,
                                    progress: dwellProgress,
                                    onTap: onDwellTap
                                )
                                .frame(maxWidth: geometry.size.width - 36, alignment: .leading)
                                .opacity(contactRequest == nil ? 1 : 0)
                                .saturation(contactRequest == nil ? 1 : 0)
                                .allowsHitTesting(contactRequest == nil)
                            }
                            ForEach(items) { item in
                                alertRow(item)
                                    .frame(maxWidth: geometry.size.width - 36, alignment: .leading)
                                    .opacity(contactRequest == nil ? 1 : 0)
                                    .saturation(contactRequest == nil ? 1 : 0)
                                    .allowsHitTesting(contactRequest == nil && isTappableItem(item))
                            }
                        }
                        .padding(.leading, 18)
                        // Clear the complete weather block, including a wrapped
                        // two-line city name, before beginning the status stack.
                        .padding(.top, geometry.safeAreaInsets.top + 128)
                        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
                    } else {
                        VStack(spacing: 6) {
                            if let contactRequest { contactRow(contactRequest) }
                            if let dwellPlace {
                                LocationDwellStatusChip(
                                    place: dwellPlace,
                                    progress: dwellProgress,
                                    onTap: onDwellTap
                                )
                                .opacity(contactRequest == nil ? 1 : 0)
                                .saturation(contactRequest == nil ? 1 : 0)
                                .allowsHitTesting(contactRequest == nil)
                            }
                            if !items.isEmpty {
                                let activeItem = items[currentIndex % items.count]
                                alertRow(activeItem)
                                    .opacity(contactRequest == nil ? 1 : 0)
                                    .saturation(contactRequest == nil ? 1 : 0)
                                    .allowsHitTesting(contactRequest == nil && isTappableItem(activeItem))
                                    .id(activeItem.id)
                            }
                        }
                        .frame(maxWidth: geometry.size.width * 0.88)
                        .position(x: geometry.size.width * 0.5, y: geometry.safeAreaInsets.top + 190)
                    }
                }
            }
            .animation(.easeInOut(duration: 0.35), value: currentIndex)
            .onReceive(timer) { _ in
                if items.count > 1 {
                    currentIndex = (currentIndex + 1) % items.count
                }
            }
            .onAppear {
                if currentIndex >= items.count {
                    currentIndex = 0
                }
            }
            .onChange(of: items.count) { newCount in
                if currentIndex >= newCount {
                    currentIndex = 0
                }
            }
        }
    }

    private func contactRow(_ incoming: OverlayIncomingTransmission) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 9) {
                Image(systemName: "antenna.radiowaves.left.and.right")
                    .font(.system(size: 19, weight: .black))
                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                Text("\(incoming.senderLabel) is trying to make contact")
                    .font(.system(size: 16, weight: .black))
                    .foregroundStyle(.white)
            }
            HStack(spacing: 8) {
                Button("ACCEPT", action: onAcceptContact)
                    .buttonStyle(K1L0ContactChipButtonStyle(tint: Color(red: 0.66, green: 1.0, blue: 0.76), foreground: .black))
                Button("DECLINE", action: onDeclineContact)
                    .buttonStyle(K1L0ContactChipButtonStyle(tint: Color.white.opacity(0.14), foreground: .white))
            }
        }
        .padding(.horizontal, 15)
        .padding(.vertical, 12)
        .background(Color.black.opacity(0.84), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 14).stroke(Color.white.opacity(0.22), lineWidth: 1))
        .fixedSize(horizontal: false, vertical: true)
    }

    private func isTappableItem(_ item: K1L0MarqueeItem) -> Bool {
        item.kind == "ambientElement" || item.kind == "ambientObject"
    }

    @ViewBuilder
    private func alertRow(_ item: K1L0MarqueeItem) -> some View {
        if isTappableItem(item) {
            Button { onItemTap(item) } label: { alertRowContent(item) }
                .buttonStyle(.plain)
        } else {
            alertRowContent(item)
        }
    }

    private func alertRowContent(_ item: K1L0MarqueeItem) -> some View {
        HStack(spacing: 8) {
            if let dist = item.distanceText, let bearing = item.relativeBearing {
                DirectionCell(distance: dist, relativeBearing: bearing)
            } else {
                let isIdle = item.id == "walking-status" && (item.line1 == "Walk" || item.line2.contains("Idle"))
                let sysImg = isIdle ? "exclamationmark.triangle.fill" : (item.kind == "incomingTransmission" ? "antenna.radiowaves.left.and.right" : (item.id == "render-loading" ? "circle.dashed" : "figure.walk"))
                Image(systemName: sysImg)
                    .font(.system(size: 16, weight: .bold))
                    .foregroundStyle(isIdle ? Color.red : Color(red: 0.66, green: 1.0, blue: 0.76))
                    .frame(width: 32)
            }
            // One layout for every banner: thumbnail (when there is one) on
            // the left, text after. The old location-only right-side thumb
            // made ambient and location rows look like two different systems.
            if item.kind == "ambientElement" || item.kind == "ambientObject" || item.kind == "location" {
                NearbyItemThumbnail(imageUrl: item.imageUrl)
                renderItemText(item)
            } else {
                renderItemText(item)
            }
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 6)
        .background(
            Color.black.opacity(0.55),
            in: RoundedRectangle(cornerRadius: statusHUD ? 10 : 22, style: .continuous)
        )
        .overlay(
            RoundedRectangle(cornerRadius: statusHUD ? 10 : 22, style: .continuous)
                .stroke(Color.white.opacity(0.18), lineWidth: 1)
        )
        .fixedSize(horizontal: false, vertical: true)
    }

    @ViewBuilder
    private func renderItemText(_ item: K1L0MarqueeItem) -> some View {
        let (baseText, hadDots) = splitText(item.line1)
        
        VStack(alignment: item.distanceText != nil ? .leading : .center, spacing: 2) {
            if hadDots {
                (
                    Text(baseText).foregroundColor(.white.opacity(0.88))
                    + Text(".").foregroundColor(.white.opacity(dotPhase >= 0 ? 0.88 : 0.22))
                    + Text(".").foregroundColor(.white.opacity(dotPhase >= 1 ? 0.88 : 0.22))
                    + Text(".").foregroundColor(.white.opacity(dotPhase >= 2 ? 0.88 : 0.22))
                )
                .font(.system(size: 15, weight: .bold))
                .tracking(0.6)
                .multilineTextAlignment(item.distanceText != nil ? .leading : .center)
                .lineLimit(1)
            } else {
                Text(item.line1)
                    .font(.system(size: 15, weight: .bold))
                    .tracking(0.6)
                    .foregroundStyle(.white.opacity(0.88))
                    .multilineTextAlignment(item.distanceText != nil ? .leading : .center)
                    .lineLimit(1)
            }
            
            if !item.line2.isEmpty {
                Text(item.line2)
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(.white.opacity(0.72))
                    .multilineTextAlignment(item.distanceText != nil ? .leading : .center)
                    .lineLimit(1)
            }
        }
    }

    private func splitText(_ fullText: String) -> (base: String, hadDots: Bool) {
        var base = fullText
        var sawDot = false
        while let last = base.last, last == "." || last == "\u{2007}" || last == "\u{2008}" || last == " " {
            if last == "." { sawDot = true }
            base.removeLast()
        }
        return sawDot ? (base, true) : (fullText, false)
    }
}

struct K1L0ContactChipButtonStyle: ButtonStyle {
    let tint: Color
    let foreground: Color

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 13, weight: .black, design: .monospaced))
            .foregroundStyle(foreground)
            .frame(maxWidth: .infinity, minHeight: 36)
            .background(tint.opacity(configuration.isPressed ? 0.65 : 1), in: RoundedRectangle(cornerRadius: 8))
    }
}

struct IncomingSignalSkyImage: View {
    let image: Image
    let progress: Double
    let size: CGSize
    let time: TimeInterval

    private var unresolved: Double { 1.0 - min(1, max(0, progress)) }
    private var viewWidth: CGFloat { size.width * 1.12 }
    private var viewHeight: CGFloat { size.height * 0.78 }
    private var blurRadius: CGFloat { CGFloat(max(0, 7.0 * unresolved)) }
    private var imageScale: CGFloat { CGFloat(1.08 + unresolved * 0.10) }
    private var xOffset: CGFloat { CGFloat(sin(time * 2.1) * 18.0 * unresolved) }
    private var yOffset: CGFloat { -size.height * 0.06 }
    private var imageOpacity: Double { 0.16 + progress * 0.66 }

    var body: some View {
        let resolvedImage = image
            .resizable()
            .scaledToFill()
            .frame(width: viewWidth, height: viewHeight)
        return resolvedImage
            .blur(radius: blurRadius)
            .scaleEffect(imageScale)
            .offset(x: xOffset, y: yOffset)
            .opacity(imageOpacity)
    }
}

struct TenBarSignalMeter: View {
    let strength: Double
    private let barCount = 7

    private var activeBars: Int {
        min(barCount, max(0, Int(ceil(min(1, max(0, strength)) * Double(barCount)))))
    }

    var body: some View {
        HStack(alignment: .bottom, spacing: 2) {
            ForEach(0..<barCount, id: \.self) { index in
                Rectangle()
                    .fill(index < activeBars ? Color.white : Color.white.opacity(0.52))
                    .frame(width: 4, height: CGFloat(4 + index * 4))
            }
        }
        .frame(height: 34, alignment: .bottom)
        .accessibilityLabel("signal strength")
    }
}

struct WorldMarqueeCard: View {
    let items: [K1L0MarqueeItem]
    let onItemTap: ((K1L0MarqueeItem) -> Void)?

    init(items: [K1L0MarqueeItem], onItemTap: ((K1L0MarqueeItem) -> Void)? = nil) {
        self.items = items
        self.onItemTap = onItemTap
    }

    var body: some View {
        WeatherGlassCard {
            VStack(alignment: .leading, spacing: 10) {
                ForEach(items) { item in
                    HStack(alignment: .center, spacing: 12) {
                        if let distance = item.distanceText, let bearing = item.relativeBearing {
                            DirectionCell(distance: distance, relativeBearing: bearing)
                        } else if let progress = item.progress {
                            VStack(alignment: .leading, spacing: 4) {
                                Text("\(Int((progress * 100).rounded()))%")
                                    .font(.system(size: 12, weight: .black, design: .monospaced))
                                    .foregroundStyle(.white)
                                    .offset(y: 5)
                                TenBarSignalMeter(strength: progress)
                            }
                            .frame(width: 46, alignment: .leading)
                        } else {
                            let isIdle = item.id == "walking-status" && (item.line1 == "Walk" || item.line2.contains("Idle"))
                            let sysImg = isIdle ? "exclamationmark.triangle.fill" : "figure.walk"
                            Image(systemName: sysImg)
                                .font(.system(size: 18, weight: .bold))
                                .foregroundStyle(isIdle ? Color.red : Color(red: 0.66, green: 1.0, blue: 0.76))
                                .frame(width: 46)
                        }
                        if item.kind == "ambientElement" || item.kind == "ambientObject" {
                            NearbyItemThumbnail(imageUrl: item.imageUrl)
                            VStack(alignment: .leading, spacing: 3) {
                                Text("Nearby artifact")
                                    .font(.system(size: 16, weight: .bold))
                                    .foregroundStyle(.white)
                                    .lineLimit(1)
                                if !item.line2.isEmpty {
                                    Text(item.line2)
                                        .font(.system(size: 12, weight: .semibold))
                                        .foregroundStyle(.white.opacity(0.72))
                                        .lineLimit(2)
                                }
                            }
                        } else {
                            VStack(alignment: .leading, spacing: 3) {
                                Text(item.line1)
                                    .font(.system(size: item.kind == "status" ? 19 : 16, weight: .bold))
                                    .foregroundStyle(.white)
                                    .lineLimit(1)
                                    .minimumScaleFactor(0.66)
                                if !item.line2.isEmpty {
                                    Text(item.line2)
                                        .font(.system(size: 12, weight: .semibold))
                                        .foregroundStyle(.white.opacity(0.72))
                                        .lineLimit(2)
                                        .minimumScaleFactor(0.64)
                                }
                            }
                            if item.kind == "location" {
                                NearbyItemThumbnail(imageUrl: item.imageUrl)
                            }
                        }
                        Spacer()
                    }
                    .contentShape(Rectangle())
                    .onTapGesture {
                        guard item.kind == "ambientElement" || item.kind == "ambientObject" else { return }
                        onItemTap?(item)
                    }
                }
            }
        }
    }
}

struct K1L0TabbedBottomMenu: View {
    let activeTab: String
    let transmitterText: String
    let transmitterActive: Bool
    let hideUserAndInbox: Bool
    let onHome: () -> Void
    let onMap: () -> Void
    let onTransmitter: () -> Void
    let onInbox: () -> Void
    let onUser: () -> Void

    @ObservedObject private var saveStore = K1L0UserMetadataSaveStore.shared

    var body: some View {
        HStack(spacing: 0) {
            tabButton(id: "home", systemImage: "house.fill", action: onHome)
            tabButton(id: "map", systemImage: "map.fill", action: onMap)
            tabButton(id: "transmitter", systemImage: "antenna.radiowaves.left.and.right", action: onTransmitter, red: transmitterActive)
            if !hideUserAndInbox {
                tabButton(id: "inbox", systemImage: "paperplane.fill", action: onInbox)
                userTabButton
            }
        }
        .padding(.horizontal, 8)
        .padding(.vertical, 8)
        .background(Color.black.opacity(0.58), in: Capsule())
        .overlay(Capsule().stroke(Color.white.opacity(0.18), lineWidth: 1))
    }

    private var userTabButton: some View {
        let active = activeTab == "user"
        let helmetUrl = saveStore.savedHelmetURL.trimmingCharacters(in: .whitespacesAndNewlines)
        return Button(action: onUser) {
            K1L0UserAvatar(urlString: helmetUrl.isEmpty ? nil : helmetUrl, size: 34)
                .overlay(active ? Circle().stroke(Color.white.opacity(0.72), lineWidth: 1.5) : nil)
                .frame(maxWidth: .infinity)
                .frame(height: 48)
                .background(
                    active ? Capsule().fill(Color.white.opacity(0.18)) : Capsule().fill(Color.white.opacity(0.001))
                )
        }
        .buttonStyle(.plain)
    }

    private func tabButton(id: String, systemImage: String, action: @escaping () -> Void, red: Bool = false) -> some View {
        let active = activeTab == id
        return Button(action: action) {
            Image(systemName: systemImage)
                .font(.system(size: 20, weight: .bold))
                .foregroundStyle(red && active ? Color.red : .white)
                .frame(maxWidth: .infinity)
                .frame(height: 48)
                .background(
                    active ? Capsule().fill(red ? Color.red.opacity(0.42) : Color.white.opacity(0.18))
                           : Capsule().fill(Color.white.opacity(0.001))
                )
        }
        .buttonStyle(.plain)
    }
}
