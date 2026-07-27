import SwiftUI
import AVFoundation
import CoreLocation
#if canImport(UIKit)
import UIKit
#elseif canImport(AppKit)
import AppKit
#endif

struct NativeLocationPreset: Identifiable, Codable {
    let id: String
    let title: String
    let subtitle: String
    let latitude: Double
    let longitude: Double

    static let storageKey = "k1lo_native_locationMode"
    static let savedStorageKey = "k1lo_native_savedLocationPresets"
    static let liveId = "live"
    static let fallback = Self.hernandez

    static let domino = NativeLocationPreset(
        id: "zorb",
        title: "Zorb",
        subtitle: "Dunham Pl + Broadway",
        latitude: 40.7109861,
        longitude: -73.9682690
    )
    static let hernandez = NativeLocationPreset(
        id: "hernandez_park",
        title: "Hernandez Park",
        subtitle: "Bushwick, Brooklyn",
        latitude: 40.7028806,
        longitude: -73.9240261
    )
    static let meeder = NativeLocationPreset(
        id: "meeder_green",
        title: "Meeder Green",
        subtitle: "Cranberry Township, PA",
        latitude: 40.70063561266829,
        longitude: -80.10918961729158
    )
    static let pointState = NativeLocationPreset(
        id: "point_state_park",
        title: "Point St Park",
        subtitle: "Downtown Pittsburgh",
        latitude: 40.4417359,
        longitude: -80.0119979
    )

    static let builtIns = [domino, meeder, hernandez, pointState]

    static var all: [NativeLocationPreset] {
        builtIns + saved
    }

    static var saved: [NativeLocationPreset] {
        guard let data = UserDefaults.standard.data(forKey: savedStorageKey),
              let presets = try? JSONDecoder().decode([NativeLocationPreset].self, from: data) else {
            return []
        }
        return presets
    }

    static func saveSearchResult(title: String, subtitle: String, latitude: Double, longitude: Double) -> NativeLocationPreset {
        let id = String(format: "custom_%.5f_%.5f", latitude, longitude)
            .replacingOccurrences(of: "-", with: "m")
            .replacingOccurrences(of: ".", with: "p")
        let preset = NativeLocationPreset(
            id: id,
            title: title,
            subtitle: subtitle,
            latitude: latitude,
            longitude: longitude
        )
        var presets = saved.filter { $0.id != id }
        presets.insert(preset, at: 0)
        if let data = try? JSONEncoder().encode(Array(presets.prefix(20))) {
            UserDefaults.standard.set(data, forKey: savedStorageKey)
        }
        return preset
    }

    static func preset(for id: String) -> NativeLocationPreset? {
        all.first { $0.id == id }
    }
}

struct NativeLocationSearchResult: Identifiable {
    let id: String
    let title: String
    let subtitle: String
    let latitude: Double
    let longitude: Double
}

struct SettingsSectionInfo: Identifiable {
    let id: String
    let title: String
}

struct NativeSettingsPanel: View {
    // Kept in its own compilation unit so visual tuning controls do not rebuild the full HUD.
    let apiBase: String?
    let onClose: () -> Void
    @ObservedObject private var radio = K1L0RadioPlayer.shared
    @ObservedObject private var perfStats = K1L0PerfStatsStore.shared

    @AppStorage("k1lo_native_saturation") private var saturation = 0.0
    @AppStorage("k1lo_native_contrast") private var contrast = 0.0
    @AppStorage("k1lo_native_mapBrightness") private var mapBrightness = 0.0
    @AppStorage("k1lo_native_hueShift") private var hueShift = 0.0
    @AppStorage("k1lo_native_temperature") private var temperature = 0.0
    @AppStorage("k1lo_native_tint") private var tint = 0.0
    @AppStorage("k1lo_native_bloomEnabled") private var bloomEnabled = true
    @AppStorage("k1lo_native_bloomIntensity") private var bloomIntensity = 1.10
    @AppStorage("k1lo_native_bloomThreshold") private var bloomThreshold = 1.06
    @AppStorage("k1lo_native_bloomScatter") private var bloomScatter = 0.24
    @AppStorage("k1lo_native_vignetteEnabled") private var vignetteEnabled = true
    @AppStorage("k1lo_native_vignetteIntensity") private var vignetteIntensity = 0.3
    @AppStorage("k1lo_native_vignetteSmoothness") private var vignetteSmoothness = 1.0
    @AppStorage("k1lo_native_chromaticEnabled") private var chromaticEnabled = true
    @AppStorage("k1lo_native_chromaticIntensity") private var chromaticIntensity = 0.09
    @AppStorage("k1lo_native_dofEnabled") private var dofEnabled = false
    @AppStorage("k1lo_native_focusDistance") private var focusDistance = 18.1
    @AppStorage("k1lo_native_aperture") private var aperture = 8.25
    @AppStorage("k1lo_native_focalLength") private var focalLength = 119.0
    @AppStorage("k1lo_native_motionBlurEnabled") private var motionBlurEnabled = false
    @AppStorage("k1lo_native_motionBlurIntensity") private var motionBlurIntensity = 0.02
    @AppStorage("k1lo_native_filmGrainEnabled") private var filmGrainEnabled = true
    @AppStorage("k1lo_native_filmGrainIntensity") private var filmGrainIntensity = 0.0
    @AppStorage("k1lo_native_godPositionY") private var godPositionY = 49.0
    @AppStorage("k1lo_native_godPositionZ") private var godPositionZ = 107.0
    @AppStorage("k1lo_native_godRotationX") private var godRotationX = -2.0
    @AppStorage("k1lo_native_farClipPlane") private var farClipPlane = 3600.0
    @AppStorage("k1lo_native_moonlightEnabled") private var moonlightEnabled = true
    @AppStorage("k1lo_native_moonlightManualOverride") private var moonlightManualOverride = false
    @AppStorage("k1lo_native_moonlightIntensity") private var moonlightIntensity = 0.55
    @AppStorage("k1lo_native_moonlightRed") private var moonlightRed = 0.7
    @AppStorage("k1lo_native_moonlightGreen") private var moonlightGreen = 0.8
    @AppStorage("k1lo_native_moonlightBlue") private var moonlightBlue = 1.0
    @AppStorage("k1lo_native_moonlightPitch") private var moonlightPitch = 90.0
    @AppStorage("k1lo_native_moonlightYaw") private var moonlightYaw = 0.0
    @AppStorage("k1lo_native_moonlightRoll") private var moonlightRoll = 0.0
    @AppStorage("k1lo_native_ambientEnabled") private var ambientEnabled = true
    @AppStorage("k1lo_native_ambientIntensity") private var ambientIntensity = 0.0
    @AppStorage("k1lo_native_spotlightEnabled") private var spotlightEnabled = true
    @AppStorage("k1lo_native_spotlightIntensity") private var spotlightIntensity = 3.0
    @AppStorage("k1lo_native_daySunIntensity") private var daySunIntensity = 1.35
    @AppStorage("k1lo_native_reflectionsEnabled") private var reflectionsEnabled = true
    @AppStorage("k1lo_native_reflectionIntensity") private var reflectionIntensity = 1.34
    @AppStorage("k1lo_native_shadowStrength") private var shadowStrength = 0.0
    @AppStorage("k1lo_native_shadowDistance") private var shadowDistance = 227.0
    @AppStorage("k1lo_native_sectionManualOverride_itemFlight") private var itemFlightOverride = false
    @AppStorage("k1lo_native_sectionManualOverride_lighting") private var lightingOverride = false
    @AppStorage("k1lo_native_sectionManualOverride_windowsBuildings") private var windowsOverride = false
    @AppStorage("k1lo_native_sectionManualOverride_ground") private var groundOverride = false
    @AppStorage("k1lo_native_sectionManualOverride_bloom") private var bloomOverride = false
    @AppStorage("k1lo_native_sectionManualOverride_colorGrade") private var gradeOverride = false
    @AppStorage("k1lo_native_sectionManualOverride_postProcessing") private var postProcessingOverride = false
    @AppStorage("k1lo_native_lookManualOverride") private var lookManualOverride = false
    @AppStorage("k1lo_native_zossEmissiveIntensity") private var zossEmissiveIntensity = 19.0
    @AppStorage("k1lo_native_zossEmissiveSmoothness") private var zossEmissiveSmoothness = 0.34
    @AppStorage("k1lo_native_zossEmissiveMetallic") private var zossEmissiveMetallic = 0.05
    @AppStorage("k1lo_native_zossEmissiveHue") private var zossEmissiveHue = 0.07
    @AppStorage("k1lo_native_zossEmissiveSaturation") private var zossEmissiveSaturation = 0.62
    @AppStorage("k1lo_native_zossNightEmissiveHue") private var zossNightEmissiveHue = 0.115
    @AppStorage("k1lo_native_zossNightEmissiveSaturation") private var zossNightEmissiveSaturation = 0.82
    @AppStorage("k1lo_native_zossWallValue") private var zossWallValue = 0.10
    @AppStorage("k1lo_native_zossWallSaturation") private var zossWallSaturation = 0.30
    @AppStorage("k1lo_native_zossLitFraction") private var zossLitFraction = 1.0
    @AppStorage("k1lo_native_zossPaletteMix") private var zossPaletteMix = 1.0
    @AppStorage("k1lo_native_zossWindowBrightness") private var zossWindowBrightness = 1.0
    @AppStorage("k1lo_native_zossWarmth") private var zossWarmth = 0.32
    @AppStorage("k1lo_native_zossAccentFraction") private var zossAccentFraction = 0.08
    @AppStorage("k1lo_native_zossPaletteSaturation") private var zossPaletteSaturation = 1.35
    @AppStorage("k1lo_native_zossPaletteSaturation_night") private var zossPaletteSaturationNight = 1.22
    @AppStorage("k1lo_native_zossBrightnessJitter") private var zossBrightnessJitter = 0.5
    @AppStorage("k1lo_native_zossBrightnessJitterRate") private var zossBrightnessJitterRate = 0.6
    @AppStorage("k1lo_native_zossWallDaylightLift") private var zossWallDaylightLift = 0.55
    @AppStorage("k1lo_native_zossWallVariance") private var zossWallVariance = 0.6
    @AppStorage("k1lo_native_roadValue") private var roadValue = 0.88
    @State private var musicDiagnostic: String = ""
    @State private var panelPreviewSnapshot: [String: Any] = [:]
    @State private var publishedFogSnapshot: [String: Any] = [:]

    private func snapshotLookPreview() {
        let ud = UserDefaults.standard
        var snap: [String: Any] = [:]
        for k in Self.previewLookKeys {
            if let v = ud.object(forKey: "k1lo_native_\(k)") {
                snap[k] = v
            } else {
                snap[k] = NSNull()   // marker: key was unset
            }
        }
        panelPreviewSnapshot = snap
    }

    private func restoreLookPreview() {
        guard !panelPreviewSnapshot.isEmpty else { return }
        let ud = UserDefaults.standard
        for (unityKey, raw) in panelPreviewSnapshot {
            // Fog has its own explicit temporary/server authority model. Never
            // restore the panel-open snapshot here: RESET TO SERVER applies
            // asynchronously, and restoring this stale snapshot on close can
            // otherwise turn the renderer back off after the server wins.
            if K1L0WeatherModeController.isFogSetting(unityKey) {
                continue
            }
            if K1L0WeatherModeController.sectionManualOverrideActive(forSetting: unityKey) {
                continue
            }
            let defaultsKey = "k1lo_native_\(unityKey)"
            if raw is NSNull {
                ud.removeObject(forKey: defaultsKey)
                continue
            }
            ud.set(raw, forKey: defaultsKey)
            let payload: String
            if let b = raw as? Bool {
                payload = b ? "1" : "0"
            } else if let d = raw as? Double {
                payload = String(format: "%.4f", d)
            } else if let i = raw as? Int {
                payload = String(i)
            } else if let f = raw as? Float {
                payload = String(format: "%.4f", Double(f))
            } else if let s = raw as? String {
                payload = s
            } else {
                payload = String(describing: raw)
            }
            K1L0WeatherOverlayInstaller.setUnitySetting(unityKey, payload)
        }
        panelPreviewSnapshot = [:]
        // Reapply the authoritative preset as well. This restores renderer
        // defaults for advanced controls that did not previously have a local
        // UserDefaults key and therefore cannot be restored by deletion alone.
        K1L0WeatherModeController.apply(weatherLookMode)

        // Publishing means "keep this fog experiment". Reapply the published
        // raw values after the authoritative preset so closing the panel does
        // not immediately erase the snapshot that was just sent to the API.
        for (unityKey, raw) in publishedFogSnapshot {
            ud.set(raw, forKey: "k1lo_native_\(unityKey)")
            let payload: String
            if let b = raw as? Bool {
                payload = b ? "1" : "0"
            } else if let n = raw as? NSNumber {
                payload = String(format: "%.6f", n.doubleValue)
            } else {
                payload = String(describing: raw)
            }
            K1L0WeatherOverlayInstaller.setUnitySetting(unityKey, payload)
        }
        publishedFogSnapshot = [:]
    }
    @AppStorage("k1lo_native_vaporDayPink") private var vaporDayPink = 0.65
    @AppStorage("k1lo_native_groundHue") private var groundHue = 0.30
    @AppStorage("k1lo_native_groundSaturation") private var groundSaturation = 0.0
    @AppStorage("k1lo_native_beamDistanceLabels") private var beamDistanceLabels = false
    @AppStorage("k1lo_native_projectorLaserBeams") private var projectorLaserBeams = true
    @AppStorage("k1lo_native_beamDebug") private var beamDebug = false
    @AppStorage("k1lo_native_perfOverlay") private var perfOverlay = true
    @AppStorage("k1lo_native_showStoryStrip") private var showStoryStrip = false
    @AppStorage("k1lo_native_panelMapBrightness") private var panelMapBrightness = 0.34
    @AppStorage("k1lo_native_weatherOpenMeteo") private var weatherOpenMeteo = true
    @AppStorage("k1lo_native_testSkyOverride") private var testSkyOverride = false
    @AppStorage("k1lo_native_transmissionFX") private var transmissionFXEnabled = true
    @AppStorage("k1lo_native_transmissionFXIntensity") private var transmissionFXIntensity = 0.5
    @AppStorage("k1lo_native_transmissionFizzyEdges") private var transmissionFizzyEdges = false
    @AppStorage("k1lo_native_bottomMenuLayout") private var bottomMenuLayout = "tabs"
    @AppStorage("k1lo_native_statusHUD") private var statusHUD = false
    @AppStorage("k1lo_native_manualHour") private var manualHour = 13.25
    @AppStorage("k1lo_native_manualWeather") private var manualWeather = 0
    @AppStorage("k1lo_native_ambientMinStepsToSpawn") private var ambientMinStepsToSpawn = 0.0
    @AppStorage("k1lo_native_receiveStepsRequired") private var receiveStepsRequired = 200.0
    @AppStorage("k1lo_native_transmissionWaitSteps") private var transmissionWaitSteps = 500.0
    @AppStorage("k1lo_native_momentumSessionGraceMinutes") private var momentumSessionGraceMinutes = 20.0
    @AppStorage("k1lo_native_ambientBeamTtlMinutes") private var ambientBeamTtlMinutes = 30.0
    @AppStorage("k1lo_native_ambientCollectRadiusMeters") private var ambientCollectRadiusMeters = 16.0
    @AppStorage("k1lo_native_locationCollectRadiusFeet") private var locationCollectRadiusFeet = 50.0
    @AppStorage("k1lo_native_ambientBeamDismissSteps") private var ambientBeamDismissSteps = 80.0
    @AppStorage("k1lo_native_musicRadioEnabled") private var musicRadioEnabled = true
    @AppStorage("k1lo_native_musicRadioVolume") private var musicRadioVolume = 0.5415074229240417
    @AppStorage("k1lo_native_musicRadioMode") private var musicRadioMode = "instrumental"
    @AppStorage("k1lo_native_weatherLookMode") private var weatherLookMode = "auto"
    @AppStorage("k1lo_native_fogManualOverride") private var fogManualOverride = false
    @AppStorage("k1lo_native_nearFogEnabled") private var nearFogEnabled = false
    @AppStorage("k1lo_native_fogConstantDensity") private var fogConstantDensity = false
    @AppStorage("k1lo_native_fogDensity") private var fogDensity = 0.01
    @AppStorage("k1lo_native_fogNoiseStrength") private var fogNoiseStrength = 1.8
    @AppStorage("k1lo_native_fogNoiseScale") private var fogNoiseScale = 21.0
    @AppStorage("k1lo_native_fogBrightness") private var fogBrightness = 0.81
    @AppStorage("k1lo_native_fogScatteringIntensity") private var fogScatteringIntensity = 1.25
    @AppStorage("k1lo_native_fogHeight") private var fogHeight = 86.0
    @AppStorage("k1lo_native_fogDistantFog") private var fogDistantFog = true
    @AppStorage("k1lo_native_fogDistantDensity") private var fogDistantDensity = 0.0
    @AppStorage("k1lo_native_fogDistantStart") private var fogDistantStart = 0.0
    @AppStorage("k1lo_native_fogNativeLights") private var fogNativeLights = false
    @AppStorage("k1lo_native_fogNativeLightsMultiplier") private var fogNativeLightsMultiplier = 0.0
    @AppStorage("k1lo_native_itemInsectCruiseY") private var itemInsectCruiseY = 20.0
    @AppStorage("k1lo_native_itemInsectCeilingY") private var itemInsectCeilingY = 40.0
    @AppStorage("k1lo_native_itemInsectCameraClearanceV3") private var itemInsectCameraClearance = 24.0
    @AppStorage("k1lo_native_itemInsectCuriosityRadius") private var itemInsectCuriosityRadius = 5.0
    @AppStorage("k1lo_native_itemInsectCuriositySpeed") private var itemInsectCuriositySpeed = 0.18
    @AppStorage("k1lo_native_itemInsectApproachMeander") private var itemInsectApproachMeander = 6.0
    @AppStorage("k1lo_native_itemInsectInvestigationLift") private var itemInsectInvestigationLift = 7.5
    @AppStorage("k1lo_native_itemInsectVisitInterval") private var itemInsectVisitInterval = 20.0
    @AppStorage("k1lo_native_itemInsectApproachSeconds") private var itemInsectApproachSeconds = 6.5
    @AppStorage("k1lo_native_itemInsectHoverSecondsV2") private var itemInsectHoverSeconds = 4.0
    @AppStorage("k1lo_native_itemInsectReturnSecondsV3") private var itemInsectReturnSeconds = 4.5
    @AppStorage("k1lo_native_itemBaseSize") private var itemBaseSize = 10.0
    @AppStorage("k1lo_native_itemViewportHeight") private var itemViewportHeight = 0.045
    @AppStorage("k1lo_native_itemMaxWorldSize") private var itemMaxWorldSize = 180.0
    @AppStorage("k1lo_native_ambientItemSpotlightEnabled") private var ambientItemSpotlightEnabled = true
    @AppStorage("k1lo_native_ambientItemSpotlightIntensity") private var ambientItemSpotlightIntensity = 7.0
    @AppStorage("k1lo_native_ambientItemSpotlightRange") private var ambientItemSpotlightRange = 65.0
    @AppStorage("k1lo_native_ambientItemSpotlightAngle") private var ambientItemSpotlightAngle = 17.0
    @AppStorage("k1lo_native_fogRaymarchQuality") private var fogRaymarchQuality = 4.0
    @AppStorage("k1lo_native_fogColorRed") private var fogColorRed = 1.0
    @AppStorage("k1lo_native_fogColorGreen") private var fogColorGreen = 0.42
    @AppStorage("k1lo_native_fogColorBlue") private var fogColorBlue = 0.16
    @AppStorage("k1lo_native_fogOrangeAmount") private var fogOrangeAmount = 0.0
    @AppStorage("k1lo_native_fogDistance") private var fogDistance = 0.0
    @AppStorage("k1lo_native_fogDistanceFallOff") private var fogDistanceFallOff = 0.5
    @AppStorage("k1lo_native_fogMaxDistance") private var fogMaxDistance = 1200.0
    @AppStorage("k1lo_native_fogMaxDistanceFallOff") private var fogMaxDistanceFallOff = 0.5
    @AppStorage("k1lo_native_fogVerticalOffset") private var fogVerticalOffset = 0.0
    @AppStorage("k1lo_native_fogWindX") private var fogWindX = 0.0
    @AppStorage("k1lo_native_fogWindY") private var fogWindY = 0.0
    @AppStorage("k1lo_native_fogWindZ") private var fogWindZ = 0.0
    @AppStorage("k1lo_native_fogTurbulence") private var fogTurbulence = 0.0
    @AppStorage("k1lo_native_fogV2ScatteringHighQuality") private var fogV2ScatteringHighQuality = false
    @AppStorage("k1lo_native_fogV2Downscaling") private var fogV2Downscaling = 2.0
    @AppStorage("k1lo_native_fogV2DownscalingEdgeThreshold") private var fogV2DownscalingEdgeThreshold = 0.01
    @AppStorage("k1lo_native_fogV2BlurPasses") private var fogV2BlurPasses = 2.0
    @AppStorage("k1lo_native_fogV2BlurDownscaling") private var fogV2BlurDownscaling = 1.0
    @AppStorage("k1lo_native_fogV2BlurSpread") private var fogV2BlurSpread = 1.0
    @AppStorage("k1lo_native_fogV2BlurHDR") private var fogV2BlurHDR = false
    @AppStorage("k1lo_native_fogV2BlurEdgePreserve") private var fogV2BlurEdgePreserve = true
    @AppStorage("k1lo_native_fogV2BlurEdgeThreshold") private var fogV2BlurEdgeThreshold = 0.01
    @AppStorage("k1lo_native_fogV2ManagerDither") private var fogV2ManagerDither = 0.02
    @AppStorage("k1lo_native_fogV2NearStepping") private var fogV2NearStepping = 1.0
    @AppStorage("k1lo_native_fogV2MinStep") private var fogV2MinStep = 0.1
    @AppStorage("k1lo_native_fogV2Jittering") private var fogV2Jittering = 0.5
    @AppStorage("k1lo_native_fogV2Dithering") private var fogV2Dithering = 0.5
    @AppStorage("k1lo_native_fogV2NoiseMultiplier") private var fogV2NoiseMultiplier = 1.0
    @AppStorage("k1lo_native_fogV2DetailNoise") private var fogV2DetailNoise = false
    @AppStorage("k1lo_native_fogV2DetailScale") private var fogV2DetailScale = 1.0
    @AppStorage("k1lo_native_fogV2DetailStrength") private var fogV2DetailStrength = 0.5
    @AppStorage("k1lo_native_fogV2DetailOffset") private var fogV2DetailOffset = 0.0
    @AppStorage("k1lo_native_fogV2DeepObscurance") private var fogV2DeepObscurance = 0.0
    @AppStorage("k1lo_native_fogV2SpecularThreshold") private var fogV2SpecularThreshold = 0.5
    @AppStorage("k1lo_native_fogV2SpecularIntensity") private var fogV2SpecularIntensity = 0.0
    @AppStorage("k1lo_native_fogV2DiffusionPower") private var fogV2DiffusionPower = 32.0
    @AppStorage("k1lo_native_fogV2DiffusionIntensity") private var fogV2DiffusionIntensity = 0.0
    @AppStorage("k1lo_native_fogV2DiffusionNearAtten") private var fogV2DiffusionNearAtten = 0.0
    @AppStorage("k1lo_native_fogV2BackScatter") private var fogV2BackScatter = 0.0
    @AppStorage("k1lo_native_fogV2DiffusionFloor") private var fogV2DiffusionFloor = 0.0
    @AppStorage("k1lo_native_fogV2ReceiveShadows") private var fogV2ReceiveShadows = false
    @AppStorage("k1lo_native_fogV2ShadowIntensity") private var fogV2ShadowIntensity = 0.0
    @AppStorage("k1lo_native_fogV2ShadowCancellation") private var fogV2ShadowCancellation = 0.0
    @AppStorage("k1lo_native_fogV2ShadowMaxDistance") private var fogV2ShadowMaxDistance = 100.0
    @AppStorage("k1lo_native_fogV2Border") private var fogV2Border = 0.0
    @AppStorage("k1lo_native_fogV2ScaleNoiseWithHeight") private var fogV2ScaleNoiseWithHeight = 0.0
    @AppStorage("k1lo_native_fogV2DistantMaxHeight") private var fogV2DistantMaxHeight = 1000.0
    @AppStorage("k1lo_native_fogV2DistantHeightDensity") private var fogV2DistantHeightDensity = 0.1
    @AppStorage("k1lo_native_fogV2DistantDiffusion") private var fogV2DistantDiffusion = 0.0
    @AppStorage("k1lo_native_fogV2DistantColorRed") private var fogV2DistantColorRed = 0.90
    @AppStorage("k1lo_native_fogV2DistantColorGreen") private var fogV2DistantColorGreen = 0.60
    @AppStorage("k1lo_native_fogV2DistantColorBlue") private var fogV2DistantColorBlue = 0.50
    @AppStorage("k1lo_native_fogV2DistantBrightness") private var fogV2DistantBrightness = 1.0
    @AppStorage("k1lo_native_fogV2DistantBaseAltitude") private var fogV2DistantBaseAltitude = 0.0
    @AppStorage("k1lo_native_fogV2DistantSymmetrical") private var fogV2DistantSymmetrical = false
    @AppStorage("k1lo_native_fogV2DistantTransparency") private var fogV2DistantTransparency = false
    @AppStorage("k1lo_native_fogV2DistantNoise") private var fogV2DistantNoise = true
    @AppStorage("k1lo_native_fogV2DistantNoiseScale") private var fogV2DistantNoiseScale = 1.0
    @AppStorage("k1lo_native_fogV2DistantNoiseStrength") private var fogV2DistantNoiseStrength = 0.5
    @AppStorage("k1lo_native_fogV2DistantNoiseMaxDistance") private var fogV2DistantNoiseMaxDistance = 1000.0
    @AppStorage("k1lo_native_fogV2DistantWindX") private var fogV2DistantWindX = 0.0
    @AppStorage("k1lo_native_fogV2DistantWindY") private var fogV2DistantWindY = 0.0
    @AppStorage("k1lo_native_fogV2DistantWindZ") private var fogV2DistantWindZ = 0.0
    @State private var fogSnapshotStatus = ""
    @State private var fogPublishInFlight = false
    @State private var showingFogPublishTargetPicker = false
    @AppStorage(NativeLocationPreset.storageKey) private var locationMode = NativeLocationPreset.liveId
    @State private var locationSearchText = ""
    @State private var locationSearchResults: [NativeLocationSearchResult] = []
    @State private var locationSearchStatus = ""
    @State private var locationSearchInFlight = false
    @AppStorage("k1lo_native_skyTargetFps") private var skyTargetFps = 30.0
    @AppStorage("k1lo_native_experimentalLayeredSky") private var experimentalLayeredSky = true
    @AppStorage("k1lo_native_layeredBypassWeather") private var layeredBypassWeather = false
    @AppStorage("k1lo_native_layeredSkyEffect") private var layeredSkyEffect = 0
    @AppStorage("k1lo_native_layeredRain") private var layeredRain = 0.0
    @AppStorage("k1lo_native_layeredAurora") private var layeredAurora = 0.0
    @AppStorage("k1lo_native_layeredSkyTopHue") private var layeredSkyTopHue = 0.80
    @AppStorage("k1lo_native_layeredSkyMidHue") private var layeredSkyMidHue = 0.62
    @AppStorage("k1lo_native_layeredNightBlackness") private var layeredNightBlackness = 0.60
    @AppStorage("k1lo_native_layeredSkyHorizonHue") private var layeredSkyHorizonHue = 0.73
    @AppStorage("k1lo_native_layeredCloudOpacity") private var layeredCloudOpacity = 0.35
    @AppStorage("k1lo_native_layeredCloudSpeed") private var layeredCloudSpeed = 0.07
    @AppStorage("k1lo_native_layeredCloudScale") private var layeredCloudScale = 1.35
    @AppStorage("k1lo_native_layeredHorizonHeight") private var layeredHorizonHeight = 0.0
    @AppStorage("k1lo_native_layeredCloudContrast") private var layeredCloudContrast = 1.1
    @AppStorage("k1lo_native_liveCloudCover") private var liveCloudCover = 35.0
    @AppStorage("k1lo_native_solarWorldOverride") private var solarWorldOverride = false
    @AppStorage("k1lo_native_liveSolarAltitude") private var liveSolarAltitude = 12.0
    @AppStorage("k1lo_native_liveSolarAzimuth") private var liveSolarAzimuth = 180.0
    @AppStorage("k1lo_native_fogDensity_night") private var fogDensityNight = 0.025
    @AppStorage("k1lo_native_fogNoiseStrength_night") private var fogNoiseStrengthNight = 0.22
    @AppStorage("k1lo_native_fogNoiseScale_night") private var fogNoiseScaleNight = 17.4
    @AppStorage("k1lo_native_fogBrightness_night") private var fogBrightnessNight = 0.24
    @AppStorage("k1lo_native_fogScatteringIntensity_night") private var fogScatteringIntensityNight = 0.55
    @AppStorage("k1lo_native_fogHeight_night") private var fogHeightNight = 48.0
    @AppStorage("k1lo_native_fogDistantDensity_night") private var fogDistantDensityNight = 0.0025
    @AppStorage("k1lo_native_fogDistantStart_night") private var fogDistantStartNight = 100.0
    @AppStorage("k1lo_native_groundHue_night") private var groundHueNight = 0.30
    @AppStorage("k1lo_native_groundSaturation_night") private var groundSaturationNight = 0.0
    @State private var fogTuningMode = "Day"
    @State private var fogPass = "Near"
    @State private var groundTuningMode = "Day"
    @AppStorage("k1lo_native_selectedSettingsSection") private var selectedSection = "Menu"

    // Look-tuning sections only exist in Test Override / Bypass mode; live
    // weather mode hides them and runs the curated automated look.
    private var manualLookActive: Bool { testSkyOverride || layeredBypassWeather }

    // Volumetric Fog 2 uses Max Height and Height Density only as a product
    // in its distant-fog shader. Present that effective value as one control;
    // the first edit normalizes the package pair to reach × 1 without changing
    // the rendered result. Base altitude remains independent world-space Y.
    private var distantFogVerticalReach: Binding<Double> {
        Binding(
            get: { fogV2DistantMaxHeight * fogV2DistantHeightDensity },
            set: { newValue in
                fogV2DistantMaxHeight = newValue
                fogV2DistantHeightDensity = 1.0
            }
        )
    }

    // These sections used to be hidden unless override was on. Now they're
    // always visible; the panel-scope snapshot below reverts any tuning done
    // to their sliders when the settings panel is closed.
    private static let weatherLookSections: Set<String> = []
    // Keys whose values are temporarily editable while the settings panel is
    // open, then reverted on close. Sliders write to these normally (live
    // preview); we snapshot on open and restore on close.
    private static let previewLookKeys: [String] = [
        // Lighting
        "moonlightEnabled", "moonlightManualOverride", "moonlightIntensity",
        "moonlightRed", "moonlightGreen", "moonlightBlue",
        "moonlightPitch", "moonlightYaw", "moonlightRoll",
        "ambientEnabled", "ambientIntensity",
        "spotlightEnabled", "spotlightIntensity",
        "daySunIntensity", "reflectionsEnabled", "reflectionIntensity",
        "shadowStrength", "shadowDistance",
        // Fog (day + night)
        "nearFogEnabled", "fogConstantDensity", "fogRaymarchQuality",
        "fogDensity", "fogNoiseStrength", "fogNoiseScale", "fogBrightness",
        "fogScatteringIntensity", "fogHeight",
        "fogDistantFog", "fogDistantDensity", "fogDistantStart",
        "fogDensity_night", "fogNoiseStrength_night", "fogNoiseScale_night",
        "fogBrightness_night", "fogScatteringIntensity_night", "fogHeight_night",
        "fogDistantDensity_night", "fogDistantStart_night",
        "fogNativeLights", "fogNativeLightsMultiplier",
        "fogColorRed", "fogColorGreen", "fogColorBlue", "fogOrangeAmount",
        "fogDistance", "fogDistanceFallOff", "fogMaxDistance", "fogMaxDistanceFallOff",
        "fogVerticalOffset", "fogWindX", "fogWindY", "fogWindZ", "fogTurbulence",
        "fogV2ScatteringHighQuality", "fogV2Downscaling", "fogV2DownscalingEdgeThreshold",
        "fogV2BlurPasses", "fogV2BlurDownscaling", "fogV2BlurSpread", "fogV2BlurHDR",
        "fogV2BlurEdgePreserve", "fogV2BlurEdgeThreshold", "fogV2ManagerDither",
        "fogV2NearStepping", "fogV2MinStep", "fogV2Jittering", "fogV2Dithering",
        "fogV2NoiseMultiplier", "fogV2DetailNoise", "fogV2DetailScale",
        "fogV2DetailStrength", "fogV2DetailOffset", "fogV2DeepObscurance",
        "fogV2SpecularThreshold", "fogV2SpecularIntensity", "fogV2DiffusionPower",
        "fogV2DiffusionIntensity", "fogV2DiffusionNearAtten", "fogV2BackScatter",
        "fogV2DiffusionFloor", "fogV2ReceiveShadows", "fogV2ShadowIntensity",
        "fogV2ShadowCancellation", "fogV2ShadowMaxDistance", "fogV2Border",
        "fogV2ScaleNoiseWithHeight", "fogV2DistantMaxHeight", "fogV2DistantHeightDensity",
        "fogV2DistantDiffusion", "fogV2DistantBaseAltitude", "fogV2DistantSymmetrical",
        "fogV2DistantColorRed", "fogV2DistantColorGreen", "fogV2DistantColorBlue",
        "fogV2DistantBrightness",
        "fogV2DistantTransparency", "fogV2DistantNoise", "fogV2DistantNoiseScale",
        "fogV2DistantNoiseStrength", "fogV2DistantNoiseMaxDistance",
        "fogV2DistantWindX", "fogV2DistantWindY", "fogV2DistantWindZ",
        // Window Glow / walls / road
        "zossEmissiveIntensity", "zossEmissiveSmoothness", "zossEmissiveMetallic",
        "zossEmissiveHue", "zossEmissiveSaturation",
        "zossNightEmissiveHue", "zossNightEmissiveSaturation",
        "zossLitFraction", "zossPaletteMix", "zossWindowBrightness",
        "zossWarmth", "zossAccentFraction",
        "zossPaletteSaturation", "zossPaletteSaturation_night",
        "zossBrightnessJitter", "zossBrightnessJitterRate",
        "zossWallDaylightLift", "zossWallVariance",
        "roadValue", "zossWallValue", "zossWallSaturation",
        "vaporDayPink",
        // Ground / Grass
        "groundHue", "groundSaturation",
        "groundHue_night", "groundSaturation_night",
        // Sky Lab (Layered sky) — reverted on panel close so experimentation
        // doesn't clobber the persisted look. Bypass itself is reverted by
        // Unity's settingsPanelOpen flag while the panel is up.
        "layeredSkyTopHue", "layeredSkyMidHue", "layeredSkyHorizonHue",
        "layeredCloudOpacity", "layeredCloudSpeed", "layeredCloudScale",
        "layeredCloudContrast", "layeredNightBlackness",
        "layeredHorizonHeight",
        "layeredRain", "layeredAurora", "layeredSkyEffect",
        "manualHour", "manualWeather",
    ]

    private func weatherLookModeChanged() {
        // Bypass Live Weather (Sky Lab) is the single manual switch. The old
        // testSkyOverride flag lives on internally (Unity reads it in several
        // places) but always mirrors the master so the two can never disagree.
        if testSkyOverride != layeredBypassWeather {
            testSkyOverride = layeredBypassWeather
        }
        K1L0WeatherOverlayInstaller.setUnitySetting("testSkyOverride", layeredBypassWeather ? "1" : "0")
        // Re-push all look values under the new mode (saved sliders vs curated
        // defaults) and step off a section that just got hidden.
        NativeUnityLightingSync.sync()
        if !manualLookActive && Self.weatherLookSections.contains(selectedSection) {
            selectedSection = "Layered Sky"
        }
    }

    // Renderer tuning moved to the remote live-render channel. Keep this
    // screen focused on controls a player may reasonably change themselves.
    private let sectionsList = [
        SettingsSectionInfo(id: "Location", title: "Loc"),
        SettingsSectionInfo(id: "HUD", title: "HUD"),
        SettingsSectionInfo(id: "Item Float", title: "Artifact Flight"),
        SettingsSectionInfo(id: "Fog", title: "Fog"),
        SettingsSectionInfo(id: "Lighting", title: "Lights"),
        SettingsSectionInfo(id: "Window Glow", title: "Buildings"),
        SettingsSectionInfo(id: "Ground / Grass", title: "Ground"),
        SettingsSectionInfo(id: "Map Color", title: "Grade"),
        SettingsSectionInfo(id: "Bloom", title: "Bloom"),
        SettingsSectionInfo(id: "Post FX", title: "Post FX"),
        SettingsSectionInfo(id: "Music", title: "Music"),
        SettingsSectionInfo(id: "Timers", title: "Gameplay"),
        SettingsSectionInfo(id: "Transmission FX", title: "Effects")
    ]

    var body: some View {
        GeometryReader { geometry in
            let topClearance = geometry.safeAreaInsets.top + 78
            VStack(spacing: 0) {
                Spacer()
                    .frame(height: topClearance)
                
                VStack(alignment: .leading, spacing: 10) {
                    // Multirow Segment Controller
                    let columns = Array(repeating: GridItem(.flexible(), spacing: 4), count: 4)
                    LazyVGrid(columns: columns, spacing: 4) {
                        ForEach(sectionsList.filter { manualLookActive || !Self.weatherLookSections.contains($0.id) }) { sec in
                            let isSelected = selectedSection == sec.id
                            Button {
                                selectedSection = sec.id
                            } label: {
                                Text(sec.title)
                                    .font(.system(size: 11, weight: .bold, design: .monospaced))
                                    .frame(maxWidth: .infinity)
                                    .frame(height: 28)
                                    .background(isSelected ? Color(red: 0.66, green: 1.0, blue: 0.76) : Color.white.opacity(0.08))
                                    .foregroundStyle(isSelected ? .black : .white)
                                    .cornerRadius(6)
                            }
                            .buttonStyle(.plain)
                        }
                    }
                }
                .padding(.horizontal, 18)
                .padding(.bottom, 10)
                .onChange(of: testSkyOverride) { _ in weatherLookModeChanged() }
                .onChange(of: layeredBypassWeather) { _ in weatherLookModeChanged() }

                ScrollView(.vertical, showsIndicators: true) {
                    VStack(alignment: .leading, spacing: 14) {
                    if selectedSection == "Look" {
                        SettingsSection(title: "Lighting + Windows + Grade") {
                            lookOverrideControls

                            Button("LOAD MAC REFERENCE") {
                                applyMacReferenceLook()
                            }
                            .buttonStyle(K1L0FogAuthorityButtonStyle(active: false))

                            fogGroupTitle("Lighting")
                            SettingToggleRow(title: "Ambient", value: $ambientEnabled, key: "ambientEnabled")
                            SettingSliderRow(title: "Ambient Intensity", value: $ambientIntensity, range: 0...8, step: 0.01, key: "ambientIntensity")
                            SettingToggleRow(title: "Moonlight", value: $moonlightEnabled, key: "moonlightEnabled")
                            SettingSliderRow(title: "Moonlight Intensity", value: $moonlightIntensity, range: 0...8, step: 0.01, key: "moonlightIntensity")
                            SettingToggleRow(title: "Avatar Spotlight", value: $spotlightEnabled, key: "spotlightEnabled")
                            SettingSliderRow(title: "Avatar Spotlight Intensity", value: $spotlightIntensity, range: 0...12, step: 0.01, key: "spotlightIntensity")
                            SettingSliderRow(title: "Sun Intensity", value: $daySunIntensity, range: 0...8, step: 0.01, key: "daySunIntensity")
                            SettingToggleRow(title: "Reflections", value: $reflectionsEnabled, key: "reflectionsEnabled")
                            SettingSliderRow(title: "Reflection Intensity", value: $reflectionIntensity, range: 0...4, step: 0.01, key: "reflectionIntensity")
                            SettingSliderRow(title: "Shadow Strength", value: $shadowStrength, range: 0...1, step: 0.01, key: "shadowStrength")
                            SettingSliderRow(title: "Shadow Distance", value: $shadowDistance, range: 0...1000, step: 1, key: "shadowDistance")

                            fogGroupTitle("Windows")
                            SettingSliderRow(title: "Emission", value: $zossEmissiveIntensity, range: 0...50, step: 0.1, key: "zossEmissiveIntensity")
                            SettingSliderRow(title: "Window Brightness", value: $zossWindowBrightness, range: 0...4, step: 0.01, key: "zossWindowBrightness")
                            SettingSliderRow(title: "Window Variety", value: $zossPaletteMix, range: 0...1, step: 0.01, key: "zossPaletteMix")
                            SettingSliderRow(title: "Day Saturation", value: $zossPaletteSaturation, range: 0...1.5, step: 0.01, key: "zossPaletteSaturation")
                            SettingSliderRow(title: "Night Saturation", value: $zossPaletteSaturationNight, range: 0...1.5, step: 0.01, key: "zossPaletteSaturation_night")
                            SettingSliderRow(title: "Warmth", value: $zossWarmth, range: 0...1, step: 0.01, key: "zossWarmth")
                            SettingSliderRow(title: "Accent Windows", value: $zossAccentFraction, range: 0...1, step: 0.01, key: "zossAccentFraction")
                            SettingSliderRow(title: "Brightness Jitter", value: $zossBrightnessJitter, range: 0...1, step: 0.01, key: "zossBrightnessJitter")
                            SettingSliderRow(title: "Jitter Rate", value: $zossBrightnessJitterRate, range: 0.05...4, step: 0.05, key: "zossBrightnessJitterRate")

                            fogGroupTitle("Bloom + Grade")
                            SettingToggleRow(title: "Bloom", value: $bloomEnabled, key: "bloomEnabled")
                            SettingSliderRow(title: "Bloom Intensity", value: $bloomIntensity, range: 0...8, step: 0.1, key: "bloomIntensity")
                            SettingSliderRow(title: "Bloom Threshold", value: $bloomThreshold, range: 0...2, step: 0.05, key: "bloomThreshold")
                            SettingSliderRow(title: "Bloom Scatter", value: $bloomScatter, range: 0...1, step: 0.01, key: "bloomScatter")
                            SettingSliderRow(title: "Exposure", value: $mapBrightness, range: -2...2, step: 0.05, key: "mapBrightness")
                            SettingSliderRow(title: "Contrast", value: $contrast, range: -100...100, step: 1, key: "contrast")
                            SettingSliderRow(title: "Saturation", value: $saturation, range: -100...100, step: 1, key: "saturation")
                            SettingSliderRow(title: "Temperature", value: $temperature, range: -100...100, step: 1, key: "temperature")
                            SettingSliderRow(title: "Tint", value: $tint, range: -100...100, step: 1, key: "tint")
                        }
                    }

                        if selectedSection == "Performance" {
                        SettingsSection(title: "Performance") {
                            VStack(alignment: .leading, spacing: 8) {
                                HStack(alignment: .firstTextBaseline) {
                                    PerfStatCell(title: "FPS", value: perfStats.isFresh ? String(format: "%.0f", perfStats.fps) : "...")
                                    PerfStatCell(title: "FRAME", value: perfStats.isFresh ? String(format: "%.0f ms", perfStats.frameMs) : "...")
                                    PerfStatCell(title: "MEM", value: perfStats.isFresh ? "\(perfStats.allocMB)/\(perfStats.reservedMB) MB" : "...")
                                }
                                HStack(alignment: .firstTextBaseline) {
                                    PerfStatCell(title: "THERMAL", value: perfStats.isFresh ? perfStats.thermal : "...")
                                    PerfStatCell(title: "BAT", value: perfStats.isFresh && perfStats.batteryPct >= 0 ? String(format: "%.0f%%", perfStats.batteryPct) : "...")
                                    PerfStatCell(title: "DRAIN", value: perfStats.isFresh ? perfStats.drainDisplayText : "...")
                                }
                                HStack(alignment: .firstTextBaseline) {
                                    PerfStatCell(title: "PROCESS CPU", value: perfStats.isFresh ? String(format: "%.0f%%", perfStats.processCpuPct) : "...")
                                    PerfStatCell(title: "MODE", value: perfStats.videoPlaybackActive ? "VIDEO" : "WORLD")
                                    PerfStatCell(title: "UNITY", value: perfStats.videoPlaybackActive ? "PAUSED" : "RUNNING")
                                }
                                Text(perfStats.isFresh ? perfStats.renderDebugSummary : "render ...")
                                    .font(.system(size: 11, weight: .semibold, design: .monospaced))
                                    .foregroundStyle(.white.opacity(0.62))
                                    .lineLimit(2)
                            }
                        }
                    }

                    if selectedSection == "Location" {
                        SettingsSection(title: "Location", resetAction: {
                            setLocationMode(NativeLocationPreset.liveId)
                        }) {
                            LocationModeButton(
                                title: "Live GPS",
                                subtitle: "phone location",
                                selected: locationMode == NativeLocationPreset.liveId
                            ) {
                                setLocationMode(NativeLocationPreset.liveId)
                            }
                            ForEach(NativeLocationPreset.all) { preset in
                                LocationModeButton(
                                    title: preset.title,
                                    subtitle: preset.subtitle,
                                    selected: locationMode == preset.id
                                ) {
                                    setLocationMode(preset.id)
                                }
                            }
                            Divider()
                                .background(Color.white.opacity(0.16))
                                .padding(.vertical, 4)
                            VStack(alignment: .leading, spacing: 8) {
                                Text("SEARCH WORLD")
                                    .font(.system(size: 11, weight: .black, design: .monospaced))
                                    .foregroundStyle(.white.opacity(0.64))
                                HStack(spacing: 8) {
                                    TextField("City, park, neighborhood, or address", text: $locationSearchText)
                                        .textFieldStyle(.plain)
                                        .font(.system(size: 14, weight: .medium))
                                        .padding(.horizontal, 11)
                                        .frame(minHeight: 38)
                                        .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 9))
                                        .onSubmit(searchLocations)
                                    Button(action: searchLocations) {
                                        Group {
                                            if locationSearchInFlight {
                                                ProgressView().controlSize(.small)
                                            } else {
                                                Image(systemName: "magnifyingglass")
                                                    .font(.system(size: 15, weight: .black))
                                            }
                                        }
                                        .frame(width: 38, height: 38)
                                        .foregroundStyle(.black)
                                        .background(Color(red: 0.66, green: 1.0, blue: 0.76), in: RoundedRectangle(cornerRadius: 9))
                                    }
                                    .buttonStyle(.plain)
                                    .disabled(locationSearchInFlight || locationSearchText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                                }
                                if !locationSearchStatus.isEmpty {
                                    Text(locationSearchStatus)
                                        .font(.system(size: 11, weight: .semibold))
                                        .foregroundStyle(.white.opacity(0.58))
                                }
                                ForEach(locationSearchResults) { result in
                                    LocationModeButton(
                                        title: result.title,
                                        subtitle: result.subtitle,
                                        selected: false
                                    ) {
                                        selectSearchResult(result)
                                    }
                                }
                            }
                        }
                    }

                    if selectedSection == "HUD" {
                        SettingsSection(title: "HUD", resetAction: {
                            bottomMenuLayout = "tabs"
                            statusHUD = false
                        }) {
                            VStack(alignment: .leading, spacing: 6) {
                                Text("Bottom Menu")
                                    .font(.system(size: 14, weight: .medium))
                                Picker("Bottom Menu", selection: $bottomMenuLayout) {
                                    Text("Floating").tag("floating")
                                    Text("Tabs").tag("tabs")
                                }
                                .pickerStyle(.segmented)
                            }
                            .padding(.vertical, 7)
                            .padding(.horizontal, 10)
                            .background(Color.white.opacity(0.025), in: RoundedRectangle(cornerRadius: 10))

                            Toggle(isOn: $statusHUD) {
                                VStack(alignment: .leading, spacing: 2) {
                                    Text("Status HUD")
                                        .font(.system(size: 14, weight: .medium))
                                    Text("Compact square alerts below weather, aligned to the top left.")
                                        .font(.system(size: 11))
                                        .foregroundStyle(.white.opacity(0.58))
                                }
                            }
                            .tint(Color(red: 0.66, green: 1.0, blue: 0.76))
                            .padding(.vertical, 7)
                            .padding(.horizontal, 10)
                            .background(Color.white.opacity(0.025), in: RoundedRectangle(cornerRadius: 10))
                        }
                    }

                    if selectedSection == "God Camera" {
                        SettingsSection(title: "God Camera", resetAction: {
                            godPositionY = 49.0
                            godPositionZ = 107.0
                            godRotationX = -2.0
                            farClipPlane = 3600.0
                            K1L0WeatherOverlayInstaller.setUnitySetting("godPositionY", "49.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("godPositionZ", "107.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("godRotationX", "-2.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("farClipPlane", "3600.000")
                        }) {
                            SettingSliderRow(title: "Height", value: $godPositionY, range: 10...500, step: 1, key: "godPositionY")
                            SettingSliderRow(title: "Distance", value: $godPositionZ, range: 10...500, step: 1, key: "godPositionZ")
                            SettingSliderRow(title: "Pitch", value: $godRotationX, range: -90...90, step: 1, key: "godRotationX")
                            SettingSliderRow(title: "Far Clip", value: $farClipPlane, range: 100...5000, step: 10, key: "farClipPlane")
                        }
                    }

                    if selectedSection == "Lighting" {
                        SettingsSection(title: "Lighting", resetAction: {
                            moonlightEnabled = true
                            moonlightManualOverride = false
                            moonlightIntensity = 0.55
                            moonlightRed = 0.7
                            moonlightGreen = 0.8
                            moonlightBlue = 1.0
                            moonlightPitch = 90.0
                            moonlightYaw = 0.0
                            moonlightRoll = 0.0
                            ambientEnabled = false
                            ambientIntensity = 0.0
                            spotlightEnabled = true
                            spotlightIntensity = 3.0
                            solarWorldOverride = false

                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightEnabled", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightManualOverride", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightIntensity", "0.550")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightRed", "0.700")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightGreen", "0.800")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightBlue", "1.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightPitch", "90.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightYaw", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightRoll", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("ambientEnabled", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("ambientIntensity", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("spotlightEnabled", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("spotlightIntensity", "3.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("solarWorldOverride", "0")
                            syncLightingSettings()
                        }) {
                            sectionOverrideControls(section: K1L0WeatherModeController.lightingSection,
                                                    active: $lightingOverride)
                            solarLiveReadout("Sun/moon direction and daylight blend are astronomical")
                            SettingToggleRow(title: "Moonlight", value: $moonlightEnabled, key: "moonlightEnabled")
                            SettingToggleRow(title: "Manual Moon/Sun", value: $moonlightManualOverride, key: "moonlightManualOverride")
                            SettingSliderRow(title: "Moon Intensity", value: $moonlightIntensity, range: 0...8, step: 0.01, key: "moonlightIntensity")
                            SettingSliderRow(title: "Moon Red", value: $moonlightRed, range: 0...2, step: 0.01, key: "moonlightRed")
                            SettingSliderRow(title: "Moon Green", value: $moonlightGreen, range: 0...2, step: 0.01, key: "moonlightGreen")
                            SettingSliderRow(title: "Moon Blue", value: $moonlightBlue, range: 0...2, step: 0.01, key: "moonlightBlue")
                            SettingSliderRow(title: "Moon Pitch", value: $moonlightPitch, range: -180...180, step: 1, key: "moonlightPitch")
                            SettingSliderRow(title: "Moon Yaw", value: $moonlightYaw, range: -180...180, step: 1, key: "moonlightYaw")
                            SettingSliderRow(title: "Moon Roll", value: $moonlightRoll, range: -180...180, step: 1, key: "moonlightRoll")
                            SettingToggleRow(title: "Ambient", value: $ambientEnabled, key: "ambientEnabled")
                            SettingSliderRow(title: "Ambient", value: $ambientIntensity, range: 0...8, step: 0.01, key: "ambientIntensity")
                            SettingToggleRow(title: "Spotlight", value: $spotlightEnabled, key: "spotlightEnabled")
                                .onChange(of: spotlightEnabled) { _ in
                                    syncLightingSettings()
                                }
                            SettingSliderRow(title: "Spotlight", value: $spotlightIntensity, range: 0...12, step: 0.01, key: "spotlightIntensity")
                        }
                    }

                    if selectedSection == "Fog" && fogPass == "Near" {
                        SettingsSection(title: "Fog") {
                            SettingsSegmentedRow(
                                items: [("Near", "Near"), ("Far", "Far")],
                                selection: $fogPass
                            )
                            fogOverrideControls
                            Text("Moving a slider starts a temporary fog experiment. Publish saves it to the server; Revert restores the selected preset / Auto astronomy.")
                                .font(.system(size: 11, weight: .medium, design: .monospaced))
                                .foregroundStyle(.white.opacity(0.62))
                                .fixedSize(horizontal: false, vertical: true)
                            SettingsSegmentedRow(
                                items: [("Day Tuning", "Day"), ("Night Tuning", "Night")],
                                selection: $fogTuningMode
                            )
                            SettingToggleRow(title: "Near Fog", value: $nearFogEnabled, key: "nearFogEnabled")
                            SettingToggleRow(title: "Constant Density", value: $fogConstantDensity, key: "fogConstantDensity")

                            fogGroupTitle("Density & Color")
                            if fogTuningMode == "Day" {
                                SettingSliderRow(title: "Density", value: $fogDensity, range: 0...0.35, step: 0.001, key: "fogDensity")
                                SettingSliderRow(title: "Noise Strength", value: $fogNoiseStrength, range: 0...3, step: 0.01, key: "fogNoiseStrength")
                                SettingSliderRow(title: "Noise Scale", value: $fogNoiseScale, range: 0.1...80, step: 0.1, key: "fogNoiseScale")
                                SettingSliderRow(title: "Brightness", value: $fogBrightness, range: 0...2, step: 0.01, key: "fogBrightness")
                                SettingSliderRow(title: "Scattering", value: $fogScatteringIntensity, range: 0...4, step: 0.01, key: "fogScatteringIntensity")
                                SettingSliderRow(title: "Height", value: $fogHeight, range: 0...500, step: 1, key: "fogHeight")
                            } else {
                                SettingSliderRow(title: "Density", value: $fogDensityNight, range: 0...3, step: 0.001, key: "fogDensity_night")
                                SettingSliderRow(title: "Noise Strength", value: $fogNoiseStrengthNight, range: 0...3, step: 0.01, key: "fogNoiseStrength_night")
                                SettingSliderRow(title: "Noise Scale", value: $fogNoiseScaleNight, range: 0.1...80, step: 0.1, key: "fogNoiseScale_night")
                                SettingSliderRow(title: "Brightness", value: $fogBrightnessNight, range: 0...3, step: 0.01, key: "fogBrightness_night")
                                SettingSliderRow(title: "Scattering", value: $fogScatteringIntensityNight, range: 0...6, step: 0.01, key: "fogScatteringIntensity_night")
                                SettingSliderRow(title: "Height", value: $fogHeightNight, range: 0...2000, step: 1, key: "fogHeight_night")
                            }
                            SettingSliderRow(title: "Red", value: $fogColorRed, range: 0...1, step: 0.01, key: "fogColorRed")
                            SettingSliderRow(title: "Green", value: $fogColorGreen, range: 0...1, step: 0.01, key: "fogColorGreen")
                            SettingSliderRow(title: "Blue", value: $fogColorBlue, range: 0...1, step: 0.01, key: "fogColorBlue")
                            SettingSliderRow(title: "Orange Mix", value: $fogOrangeAmount, range: 0...1, step: 0.01, key: "fogOrangeAmount")

                            fogGroupTitle("Shape & Motion")
                            SettingSliderRow(title: "Vertical Offset", value: $fogVerticalOffset, range: -500...500, step: 1, key: "fogVerticalOffset")
                            SettingSliderRow(title: "Near Distance", value: $fogDistance, range: 0...5000, step: 5, key: "fogDistance")
                            SettingSliderRow(title: "Near Falloff", value: $fogDistanceFallOff, range: 0...1, step: 0.01, key: "fogDistanceFallOff")
                            SettingSliderRow(title: "Max Distance", value: $fogMaxDistance, range: 1...12000, step: 10, key: "fogMaxDistance")
                            SettingSliderRow(title: "Max Falloff", value: $fogMaxDistanceFallOff, range: 0...1, step: 0.01, key: "fogMaxDistanceFallOff")
                            SettingSliderRow(title: "Wind X", value: $fogWindX, range: -0.03...0.03, step: 0.0005, key: "fogWindX")
                            SettingSliderRow(title: "Wind Y", value: $fogWindY, range: -0.03...0.03, step: 0.0005, key: "fogWindY")
                            SettingSliderRow(title: "Wind Z", value: $fogWindZ, range: -0.03...0.03, step: 0.0005, key: "fogWindZ")
                            SettingSliderRow(title: "Turbulence", value: $fogTurbulence, range: 0...10, step: 0.05, key: "fogTurbulence")
                            SettingSliderRow(title: "Noise Multiplier", value: $fogV2NoiseMultiplier, range: 0...5, step: 0.01, key: "fogV2NoiseMultiplier")
                            SettingToggleRow(title: "Detail Noise", value: $fogV2DetailNoise, key: "fogV2DetailNoise")
                            SettingSliderRow(title: "Detail Scale", value: $fogV2DetailScale, range: 0.001...20, step: 0.01, key: "fogV2DetailScale")
                            SettingSliderRow(title: "Detail Strength", value: $fogV2DetailStrength, range: 0...1, step: 0.01, key: "fogV2DetailStrength")
                            SettingSliderRow(title: "Detail Offset", value: $fogV2DetailOffset, range: -20...20, step: 0.05, key: "fogV2DetailOffset")
                            SettingSliderRow(title: "Scale Noise With Height", value: $fogV2ScaleNoiseWithHeight, range: 0...1, step: 0.01, key: "fogV2ScaleNoiseWithHeight")
                            SettingSliderRow(title: "Border", value: $fogV2Border, range: 0...2, step: 0.01, key: "fogV2Border")

                            fogGroupTitle("Raymarch & Blur")
                            SettingSliderRow(title: "Raymarch Quality", value: $fogRaymarchQuality, range: 1...16, step: 1, key: "fogRaymarchQuality")
                            SettingSliderRow(title: "Near Stepping", value: $fogV2NearStepping, range: 0...50, step: 0.1, key: "fogV2NearStepping")
                            SettingSliderRow(title: "Minimum Step", value: $fogV2MinStep, range: 0...10, step: 0.01, key: "fogV2MinStep")
                            SettingSliderRow(title: "Jittering", value: $fogV2Jittering, range: 0...5, step: 0.01, key: "fogV2Jittering")
                            SettingSliderRow(title: "Dithering", value: $fogV2Dithering, range: 0...2, step: 0.01, key: "fogV2Dithering")
                            SettingToggleRow(title: "High Quality Scattering", value: $fogV2ScatteringHighQuality, key: "fogV2ScatteringHighQuality")
                            SettingSliderRow(title: "Render Downscale", value: $fogV2Downscaling, range: 1...8, step: 1, key: "fogV2Downscaling")
                            SettingSliderRow(title: "Downscale Edge Threshold", value: $fogV2DownscalingEdgeThreshold, range: 0.0001...1, step: 0.001, key: "fogV2DownscalingEdgeThreshold")
                            SettingSliderRow(title: "Blur Passes", value: $fogV2BlurPasses, range: 0...6, step: 1, key: "fogV2BlurPasses")
                            SettingSliderRow(title: "Blur Downscale", value: $fogV2BlurDownscaling, range: 1...8, step: 1, key: "fogV2BlurDownscaling")
                            SettingSliderRow(title: "Blur Spread", value: $fogV2BlurSpread, range: 0.1...4, step: 0.05, key: "fogV2BlurSpread")
                            SettingToggleRow(title: "HDR Blur", value: $fogV2BlurHDR, key: "fogV2BlurHDR")
                            SettingToggleRow(title: "Preserve Blur Edges", value: $fogV2BlurEdgePreserve, key: "fogV2BlurEdgePreserve")
                            SettingSliderRow(title: "Blur Edge Threshold", value: $fogV2BlurEdgeThreshold, range: 0...1, step: 0.001, key: "fogV2BlurEdgeThreshold")
                            SettingSliderRow(title: "Manager Dither", value: $fogV2ManagerDither, range: 0...0.2, step: 0.001, key: "fogV2ManagerDither")

                            fogGroupTitle("Light & Shadows")
                            SettingToggleRow(title: "Native Fog Lights", value: $fogNativeLights, key: "fogNativeLights")
                            SettingSliderRow(title: "Native Light Multiplier", value: $fogNativeLightsMultiplier, range: 0...10, step: 0.05, key: "fogNativeLightsMultiplier")
                            SettingSliderRow(title: "Deep Obscurance", value: $fogV2DeepObscurance, range: 0...2, step: 0.01, key: "fogV2DeepObscurance")
                            SettingSliderRow(title: "Specular Threshold", value: $fogV2SpecularThreshold, range: 0...1, step: 0.01, key: "fogV2SpecularThreshold")
                            SettingSliderRow(title: "Specular Intensity", value: $fogV2SpecularIntensity, range: 0...1, step: 0.01, key: "fogV2SpecularIntensity")
                            SettingSliderRow(title: "Diffusion Power", value: $fogV2DiffusionPower, range: 1...256, step: 1, key: "fogV2DiffusionPower")
                            SettingSliderRow(title: "Diffusion Intensity", value: $fogV2DiffusionIntensity, range: 0...10, step: 0.01, key: "fogV2DiffusionIntensity")
                            SettingSliderRow(title: "Near Depth Attenuation", value: $fogV2DiffusionNearAtten, range: 0...10, step: 0.01, key: "fogV2DiffusionNearAtten")
                            SettingSliderRow(title: "Back Scatter", value: $fogV2BackScatter, range: 0...1, step: 0.01, key: "fogV2BackScatter")
                            SettingSliderRow(title: "Diffusion Floor", value: $fogV2DiffusionFloor, range: 0...1, step: 0.01, key: "fogV2DiffusionFloor")
                            SettingToggleRow(title: "Receive Shadows", value: $fogV2ReceiveShadows, key: "fogV2ReceiveShadows")
                            SettingSliderRow(title: "Shadow Intensity", value: $fogV2ShadowIntensity, range: 0...1, step: 0.01, key: "fogV2ShadowIntensity")
                            SettingSliderRow(title: "Shadow Cancellation", value: $fogV2ShadowCancellation, range: 0...1, step: 0.01, key: "fogV2ShadowCancellation")
                            SettingSliderRow(title: "Shadow Max Distance", value: $fogV2ShadowMaxDistance, range: 0...5000, step: 10, key: "fogV2ShadowMaxDistance")
                            fogSnapshotButton
                        }
                    }

                    if selectedSection == "Item Float" {
                        SettingsSection(title: "Artifact Flight") {
                            sectionOverrideControls(section: K1L0WeatherModeController.itemFlightSection,
                                                    active: $itemFlightOverride,
                                                    remoteBaseline: true)
                            Text("Every interval, one visible horizon artifact takes a curved timed flight toward the camera, explores a three-dimensional curiosity volume, then races back to its home anchor.")
                                .font(.system(size: 11, weight: .medium, design: .monospaced))
                                .foregroundStyle(.white.opacity(0.62))
                                .fixedSize(horizontal: false, vertical: true)
                            SettingSliderRow(title: "Base Artifact Size", subtitle: "Exact world-space quad size; screen size follows natural perspective.", value: $itemBaseSize, range: 2...40, step: 0.5, key: "itemBaseSize")
                            SettingSliderRow(title: "Far Screen Size", subtitle: "Minimum screen-height fraction for distant artifacts. Close artifacts keep their natural authored size.", value: $itemViewportHeight, range: 0.015...0.12, step: 0.005, key: "itemViewportHeight")
                            SettingSliderRow(title: "Far World Size Cap", subtitle: "Maximum metre size used to keep horizon artifacts readable.", value: $itemMaxWorldSize, range: 16...180, step: 2, key: "itemMaxWorldSize")
                            SettingSliderRow(title: "Visit Interval", subtitle: "Seconds between one-at-a-time horizon visits.", value: $itemInsectVisitInterval, range: 5...120, step: 1, key: "itemInsectVisitInterval")
                            SettingSliderRow(title: "Approach Time", subtitle: "Time to reach the camera; speed is calculated from starting distance.", value: $itemInsectApproachSeconds, range: 1...30, step: 0.25, key: "itemInsectApproachSeconds")
                            SettingSliderRow(title: "Approach Meander", subtitle: "Metres of organic side-to-side, vertical, and depth wandering during the inbound flight.", value: $itemInsectApproachMeander, range: 0...18, step: 0.25, key: "itemInsectApproachMeander")
                            SettingSliderRow(title: "Viewing Distance", subtitle: "Closest preferred distance; large items automatically stay at least 2.2× their width away.", value: $itemInsectCameraClearance, range: 10...80, step: 0.5, key: "itemInsectCameraClearance")
                            SettingSliderRow(title: "Exploration Height", subtitle: "Metres above the camera's centerline while investigating.", value: $itemInsectInvestigationLift, range: 0...24, step: 0.5, key: "itemInsectInvestigationLift")
                            SettingSliderRow(title: "Curiosity Range", subtitle: "Metres explored left/right, up/down, and in depth near the camera.", value: $itemInsectCuriosityRadius, range: 1...16, step: 0.25, key: "itemInsectCuriosityRadius")
                            SettingSliderRow(title: "Investigation Time", subtitle: "Seconds spent moving curiously near the camera.", value: $itemInsectHoverSeconds, range: 0.5...20, step: 0.25, key: "itemInsectHoverSeconds")
                            SettingSliderRow(title: "Return Time", subtitle: "Smooth eased trip back to the artifact's home anchor.", value: $itemInsectReturnSeconds, range: 0.5...20, step: 0.25, key: "itemInsectReturnSeconds")
                            SettingToggleRow(title: "Ambient Uplink Light", value: $ambientItemSpotlightEnabled, key: "ambientItemSpotlightEnabled")
                            SettingSliderRow(title: "Uplink Intensity", value: $ambientItemSpotlightIntensity, range: 0...15, step: 0.25, key: "ambientItemSpotlightIntensity")
                            SettingSliderRow(title: "Uplink Range", value: $ambientItemSpotlightRange, range: 20...120, step: 2, key: "ambientItemSpotlightRange")
                            SettingSliderRow(title: "Uplink Cone", value: $ambientItemSpotlightAngle, range: 5...35, step: 1, key: "ambientItemSpotlightAngle")
                        }
                    }

                    if selectedSection == "Fog" && fogPass == "Far" {
                        SettingsSection(title: "Fog") {
                            SettingsSegmentedRow(
                                items: [("Near", "Near"), ("Far", "Far")],
                                selection: $fogPass
                            )
                            fogOverrideControls
                            Text("Near and Far share the same Custom Fog Mode authority. This is the cheaper horizon/distance pass.")
                                .font(.system(size: 11, weight: .medium, design: .monospaced))
                                .foregroundStyle(.white.opacity(0.62))
                                .fixedSize(horizontal: false, vertical: true)
                            SettingsSegmentedRow(
                                items: [("Day Tuning", "Day"), ("Night Tuning", "Night")],
                                selection: $fogTuningMode
                            )
                            SettingToggleRow(title: "Distance Fog", value: $fogDistantFog, key: "fogDistantFog")
                            if fogTuningMode == "Day" {
                                SettingSliderRow(title: "Density", subtitle: "Per-meter extinction; very small changes are visible.", value: $fogDistantDensity, range: 0...0.5, step: 0.0001, key: "fogDistantDensity")
                                SettingSliderRow(title: "Start Distance", value: $fogDistantStart, range: 0...12000, step: 5, key: "fogDistantStart")
                            } else {
                                SettingSliderRow(title: "Density", subtitle: "Per-meter extinction; very small changes are visible.", value: $fogDistantDensityNight, range: 0...0.5, step: 0.0001, key: "fogDistantDensity_night")
                                SettingSliderRow(title: "Start Distance", value: $fogDistantStartNight, range: 0...12000, step: 5, key: "fogDistantStart_night")
                            }
                            SettingSliderRow(
                                title: "Vertical Reach",
                                subtitle: "Combined package height × height-density value.",
                                value: distantFogVerticalReach,
                                range: 0...10000,
                                step: 5,
                                key: "fogV2DistantMaxHeight",
                                syncOverride: { newValue in
                                    K1L0WeatherOverlayInstaller.setUnitySetting("fogV2DistantMaxHeight", String(format: "%.3f", newValue))
                                    K1L0WeatherOverlayInstaller.setUnitySetting("fogV2DistantHeightDensity", "1.000")
                                }
                            )
                            SettingSliderRow(title: "Layer Altitude", subtitle: "World-space Y reference; not screen position.", value: $fogV2DistantBaseAltitude, range: -2000...2000, step: 1, key: "fogV2DistantBaseAltitude")
                            fogGroupTitle("Color & Light")
                            SettingSliderRow(title: "Red", value: $fogV2DistantColorRed, range: 0...1, step: 0.01, key: "fogV2DistantColorRed")
                            SettingSliderRow(title: "Green", value: $fogV2DistantColorGreen, range: 0...1, step: 0.01, key: "fogV2DistantColorGreen")
                            SettingSliderRow(title: "Blue", value: $fogV2DistantColorBlue, range: 0...1, step: 0.01, key: "fogV2DistantColorBlue")
                            SettingSliderRow(title: "Brightness", value: $fogV2DistantBrightness, range: 0...4, step: 0.01, key: "fogV2DistantBrightness")
                            SettingSliderRow(title: "Global Diffusion", subtitle: "Required for the distance multiplier below to have an effect.", value: $fogV2DiffusionIntensity, range: 0...10, step: 0.01, key: "fogV2DiffusionIntensity")
                            SettingSliderRow(title: "Distance Diffusion Multiplier", value: $fogV2DistantDiffusion, range: 0...10, step: 0.01, key: "fogV2DistantDiffusion")
                            SettingToggleRow(title: "Symmetrical", value: $fogV2DistantSymmetrical, key: "fogV2DistantSymmetrical")
                            SettingToggleRow(title: "Transparency Support", value: $fogV2DistantTransparency, key: "fogV2DistantTransparency")

                            fogGroupTitle("Distance Noise & Motion")
                            SettingToggleRow(title: "Distance Noise", value: $fogV2DistantNoise, key: "fogV2DistantNoise")
                            SettingSliderRow(title: "Noise Scale", value: $fogV2DistantNoiseScale, range: 0.01...100, step: 0.01, key: "fogV2DistantNoiseScale")
                            SettingSliderRow(title: "Noise Strength", value: $fogV2DistantNoiseStrength, range: 0...1, step: 0.01, key: "fogV2DistantNoiseStrength")
                            SettingSliderRow(title: "Noise Max Distance", value: $fogV2DistantNoiseMaxDistance, range: 0...12000, step: 10, key: "fogV2DistantNoiseMaxDistance")
                            SettingSliderRow(title: "Wind X", value: $fogV2DistantWindX, range: -0.03...0.03, step: 0.0005, key: "fogV2DistantWindX")
                            SettingSliderRow(title: "Wind Y", value: $fogV2DistantWindY, range: -0.03...0.03, step: 0.0005, key: "fogV2DistantWindY")
                            SettingSliderRow(title: "Wind Z", value: $fogV2DistantWindZ, range: -0.03...0.03, step: 0.0005, key: "fogV2DistantWindZ")
                            fogSnapshotButton
                        }
                    }

                    if selectedSection == "Window Glow" {
                        SettingsSection(title: "Window Glow", resetAction: {
                            zossEmissiveIntensity = 19.0
                            zossEmissiveSmoothness = 0.34
                            zossEmissiveMetallic = 0.0
                            zossEmissiveHue = 0.90
                            zossEmissiveSaturation = 0.62
                            zossNightEmissiveHue = 0.115
                            zossNightEmissiveSaturation = 0.82
                            zossWallValue = 0.10
                            zossWallSaturation = 0.30
                            zossLitFraction = 1.0
                            zossPaletteMix = 1.0
                            zossPaletteSaturation = 1.35
                            zossPaletteSaturationNight = 0.0
                            zossBrightnessJitter = 0.5
                            zossBrightnessJitterRate = 0.6
                            vaporDayPink = 0.65

                            K1L0WeatherOverlayInstaller.setUnitySetting("zossEmissiveIntensity", "19.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossEmissiveSmoothness", "0.340")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossEmissiveMetallic", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossEmissiveHue", "0.900")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossEmissiveSaturation", "0.620")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossNightEmissiveHue", "0.115")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossNightEmissiveSaturation", "0.820")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossWallValue", "0.100")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossWallSaturation", "0.300")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossLitFraction", "1.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossPaletteMix", "1.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossPaletteSaturation", "1.350")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossPaletteSaturation_night", "1.220")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossBrightnessJitter", "0.500")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossBrightnessJitterRate", "0.600")
                            K1L0WeatherOverlayInstaller.setUnitySetting("vaporDayPink", "0.650")
                            K1L0WindowGlowResolver.apply()
                        }) {
                            sectionOverrideControls(section: K1L0WeatherModeController.windowsSection,
                                                    active: $windowsOverride)
                            solarLiveReadout(String(format: "Live window intensity %.2f", liveWindowIntensity))
                            SettingSliderRow(title: "Brightness", value: $zossEmissiveIntensity, range: 0...50, step: 0.1, key: "zossEmissiveIntensity")
                            SettingSliderRow(title: "Smoothness", value: $zossEmissiveSmoothness, range: 0...1, step: 0.01, key: "zossEmissiveSmoothness")
                            SettingSliderRow(title: "Metallic", value: $zossEmissiveMetallic, range: 0...1, step: 0.01, key: "zossEmissiveMetallic")
                            SettingSliderRow(title: "Day Hue", value: $zossEmissiveHue, range: 0...1, step: 0.01, key: "zossEmissiveHue")
                            SettingSliderRow(title: "Day Saturation", value: $zossEmissiveSaturation, range: 0...1, step: 0.01, key: "zossEmissiveSaturation")
                            SettingSliderRow(title: "Night Hue", value: $zossNightEmissiveHue, range: 0...1, step: 0.01, key: "zossNightEmissiveHue")
                            SettingSliderRow(title: "Night Saturation", value: $zossNightEmissiveSaturation, range: 0...1, step: 0.01, key: "zossNightEmissiveSaturation")
                            SettingSliderRow(title: "Lit Windows", value: $zossLitFraction, range: 0...1, step: 0.01, key: "zossLitFraction")
                            SettingSliderRow(title: "Window Variety", value: $zossPaletteMix, range: 0...1, step: 0.01, key: "zossPaletteMix")
                            SettingSliderRow(title: "Window Saturation (Day)", value: $zossPaletteSaturation, range: 0...1.5, step: 0.01, key: "zossPaletteSaturation")
                            SettingSliderRow(title: "Window Saturation (Night)", value: $zossPaletteSaturationNight, range: 0...1.5, step: 0.01, key: "zossPaletteSaturation_night")
                            SettingSliderRow(title: "Window Brightness Jitter", value: $zossBrightnessJitter, range: 0...1, step: 0.01, key: "zossBrightnessJitter")
                            SettingSliderRow(title: "Window Jitter Rate", value: $zossBrightnessJitterRate, range: 0.05...4, step: 0.05, key: "zossBrightnessJitterRate")
                            SettingSliderRow(title: "Wall Daylight Lift", value: $zossWallDaylightLift, range: 0...1, step: 0.01, key: "zossWallDaylightLift")
                            SettingSliderRow(title: "Wall Variance (Per Building)", value: $zossWallVariance, range: 0...1, step: 0.01, key: "zossWallVariance")
                            SettingSliderRow(title: "Road Brightness", value: $roadValue, range: 0...1, step: 0.01, key: "roadValue")
                            SettingSliderRow(title: "Wall Brightness", value: $zossWallValue, range: 0...1, step: 0.01, key: "zossWallValue")
                            SettingSliderRow(title: "Wall Saturation", value: $zossWallSaturation, range: 0...1, step: 0.01, key: "zossWallSaturation")
                            SettingSliderRow(title: "Day Sky Pink", value: $vaporDayPink, range: 0...1, step: 0.01, key: "vaporDayPink")
                        }
                    }

                    if selectedSection == "Ground / Grass" {
                        SettingsSection(title: "Ground / Grass", resetAction: {
                            groundHue = 0.28
                            groundSaturation = 0.28
                            groundHueNight = 0.30
                            groundSaturationNight = 0.0

                            K1L0WeatherOverlayInstaller.setUnitySetting("groundHue", "0.280")
                            K1L0WeatherOverlayInstaller.setUnitySetting("groundSaturation", "0.280")
                            K1L0WeatherOverlayInstaller.setUnitySetting("groundHue_night", "0.300")
                            K1L0WeatherOverlayInstaller.setUnitySetting("groundSaturation_night", "0.000")
                        }) {
                            sectionOverrideControls(section: K1L0WeatherModeController.groundSection,
                                                    active: $groundOverride)
                            solarLiveReadout("Live ground blends Day/Night color and brightness")
                            SettingsSegmentedRow(
                                items: [("Day Tuning", "Day"), ("Night Tuning", "Night")],
                                selection: $groundTuningMode
                            )
                            .padding(.bottom, 8)

                            if groundTuningMode == "Day" {
                                SettingSliderRow(title: "Hue (Day)", value: $groundHue, range: 0...1, step: 0.01, key: "groundHue")
                                SettingSliderRow(title: "Saturation (Day)", value: $groundSaturation, range: 0...1, step: 0.01, key: "groundSaturation")
                            } else {
                                SettingSliderRow(title: "Hue (Night)", value: $groundHueNight, range: 0...1, step: 0.01, key: "groundHue_night")
                                SettingSliderRow(title: "Saturation (Night)", value: $groundSaturationNight, range: 0...1, step: 0.01, key: "groundSaturation_night")
                            }
                        }
                    }

                    if selectedSection == "Map Color" {
                        SettingsSection(title: "Map Color", resetAction: {
                            saturation = 0.0
                            contrast = 0.0
                            mapBrightness = 0.0
                            hueShift = 0.0
                            temperature = 0.0
                            tint = 0.0

                            K1L0WeatherOverlayInstaller.setUnitySetting("saturation", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("contrast", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("mapBrightness", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("hueShift", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("temperature", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("tint", "0.000")
                        }) {
                            sectionOverrideControls(section: K1L0WeatherModeController.gradeSection,
                                                    active: $gradeOverride)
                            SettingSliderRow(title: "Saturation", value: $saturation, range: -100...100, step: 1, key: "saturation")
                            SettingSliderRow(title: "Contrast", value: $contrast, range: -100...100, step: 1, key: "contrast")
                            SettingSliderRow(title: "Map Bright", value: $mapBrightness, range: -2...2, step: 0.05, key: "mapBrightness")
                            SettingSliderRow(title: "Hue Shift", value: $hueShift, range: -100...100, step: 1, key: "hueShift")
                            SettingSliderRow(title: "Temperature", value: $temperature, range: -100...100, step: 1, key: "temperature")
                            SettingSliderRow(title: "Tint", value: $tint, range: -100...100, step: 1, key: "tint")
                        }
                    }

                    if selectedSection == "Bloom" {
                        SettingsSection(title: "Bloom", resetAction: {
                            bloomEnabled = true
                            bloomIntensity = 1.10
                            bloomThreshold = 1.06
                            bloomScatter = 0.24

                            K1L0WeatherOverlayInstaller.setUnitySetting("bloomEnabled", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("bloomIntensity", "1.100")
                            K1L0WeatherOverlayInstaller.setUnitySetting("bloomThreshold", "1.060")
                            K1L0WeatherOverlayInstaller.setUnitySetting("bloomScatter", "0.240")
                        }) {
                            sectionOverrideControls(section: K1L0WeatherModeController.bloomSection,
                                                    active: $bloomOverride)
                            solarLiveReadout(String(format: "Live bloom intensity %.2f", liveBloomIntensity))
                            SettingToggleRow(title: "Bloom", value: $bloomEnabled, key: "bloomEnabled")
                            SettingSliderRow(title: "Intensity", value: $bloomIntensity, range: 0...8, step: 0.1, key: "bloomIntensity")
                            SettingSliderRow(title: "Threshold", value: $bloomThreshold, range: 0...2, step: 0.05, key: "bloomThreshold")
                            SettingSliderRow(title: "Scatter", value: $bloomScatter, range: 0...1, step: 0.01, key: "bloomScatter")
                        }
                    }

                    if selectedSection == "Post FX" {
                        SettingsSection(title: "Post FX", resetAction: {
                            vignetteEnabled = true
                            vignetteIntensity = 0.45
                            vignetteSmoothness = 1.0
                            chromaticEnabled = true
                            chromaticIntensity = 0.16

                            K1L0WeatherOverlayInstaller.setUnitySetting("vignetteEnabled", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("vignetteIntensity", "0.450")
                            K1L0WeatherOverlayInstaller.setUnitySetting("vignetteSmoothness", "1.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("chromaticEnabled", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("chromaticIntensity", "0.160")
                        }) {
                            sectionOverrideControls(section: K1L0WeatherModeController.postProcessingSection,
                                                    active: $postProcessingOverride)
                            SettingToggleRow(title: "Vignette", value: $vignetteEnabled, key: "vignetteEnabled")
                            SettingSliderRow(title: "Vignette Intensity", value: $vignetteIntensity, range: 0...1, step: 0.01, key: "vignetteIntensity")
                            SettingSliderRow(title: "Vignette Smoothness", value: $vignetteSmoothness, range: 0.01...1, step: 0.01, key: "vignetteSmoothness")
                            SettingToggleRow(title: "Chromatic", value: $chromaticEnabled, key: "chromaticEnabled")
                            SettingSliderRow(title: "Chromatic Intensity", value: $chromaticIntensity, range: 0...1, step: 0.01, key: "chromaticIntensity")
                        }
                    }

                    if selectedSection == "Post FX" {
                        SettingsSection(title: "Focus + Motion", resetAction: {
                            dofEnabled = false
                            focusDistance = 18.1
                            aperture = 8.25
                            focalLength = 119.0
                            motionBlurEnabled = false
                            motionBlurIntensity = 0.02
                            filmGrainEnabled = true
                            filmGrainIntensity = 0.4

                            K1L0WeatherOverlayInstaller.setUnitySetting("dofEnabled", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("focusDistance", "18.100")
                            K1L0WeatherOverlayInstaller.setUnitySetting("aperture", "8.250")
                            K1L0WeatherOverlayInstaller.setUnitySetting("focalLength", "119.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("motionBlurEnabled", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("motionBlurIntensity", "0.020")
                            K1L0WeatherOverlayInstaller.setUnitySetting("filmGrainEnabled", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("filmGrainIntensity", "0.400")
                        }) {
                            SettingToggleRow(title: "Depth of Field", value: $dofEnabled, key: "dofEnabled")
                            SettingSliderRow(title: "Focus Distance", value: $focusDistance, range: 0.1...300, step: 0.1, key: "focusDistance")
                            SettingSliderRow(title: "Aperture", value: $aperture, range: 0.05...32, step: 0.05, key: "aperture")
                            SettingSliderRow(title: "Focal Length", value: $focalLength, range: 1...300, step: 1, key: "focalLength")
                            SettingToggleRow(title: "Motion Blur", value: $motionBlurEnabled, key: "motionBlurEnabled")
                            SettingSliderRow(title: "Motion Blur Intensity", value: $motionBlurIntensity, range: 0...1, step: 0.01, key: "motionBlurIntensity")
                            SettingToggleRow(title: "Film Grain", value: $filmGrainEnabled, key: "filmGrainEnabled")
                            SettingSliderRow(title: "Film Grain Intensity", value: $filmGrainIntensity, range: 0...1, step: 0.01, key: "filmGrainIntensity")
                        }
                    }

                    if selectedSection == "Music" {
                        SettingsSection(title: "Music", resetAction: {
                            musicRadioEnabled = true
                            musicRadioMode = "instrumental"
                            musicRadioVolume = 0.5415074229240417

                            K1L0WeatherOverlayInstaller.setUnitySetting("musicRadioEnabled", "1")
                            K1L0RadioPlayer.shared.setEnabled(true, apiBase: apiBase)
                            K1L0RadioPlayer.shared.setMode("instrumental")
                            K1L0RadioPlayer.shared.setVolume(0.5415074229240417)
                        }) {
                            SettingToggleRow(title: "Radio", value: $musicRadioEnabled, key: "musicRadioEnabled")
                                .onChange(of: musicRadioEnabled) { enabled in
                                    K1L0RadioPlayer.shared.setEnabled(enabled, apiBase: apiBase)
                                }
                            VStack(alignment: .leading, spacing: 6) {
                                Text("Source")
                                    .font(.system(size: 14, weight: .medium))
                                Picker("Source", selection: $musicRadioMode) {
                                    Text("Final Mix").tag("final")
                                    Text("Instrumental").tag("instrumental")
                                }
                                .pickerStyle(.segmented)
                                .onChange(of: musicRadioMode) { mode in
                                    K1L0RadioPlayer.shared.setMode(mode)
                                }
                            }
                            .padding(.vertical, 7)
                            .padding(.horizontal, 10)
                            .background(Color.white.opacity(0.025), in: RoundedRectangle(cornerRadius: 10))
                            VStack(alignment: .leading, spacing: 7) {
                                HStack {
                                    Text("Volume")
                                        .font(.system(size: 14, weight: .medium))
                                    Spacer()
                                    Text("\(Int(musicRadioVolume * 100))%")
                                        .font(.system(size: 13, weight: .bold, design: .monospaced))
                                        .foregroundStyle(.white.opacity(0.72))
                                }
                                Slider(value: $musicRadioVolume, in: 0...1)
                                    .tint(Color(red: 0.66, green: 1.0, blue: 0.76))
                            }
                            .padding(.vertical, 7)
                            .padding(.horizontal, 10)
                            .background(Color.white.opacity(0.025), in: RoundedRectangle(cornerRadius: 10))

                            VStack(alignment: .leading, spacing: 6) {
                                Text("Status")
                                    .font(.system(size: 12, weight: .bold))
                                    .foregroundStyle(.white.opacity(0.55))
                                Text(radio.status)
                                    .font(.system(size: 12, weight: .medium, design: .monospaced))
                                    .foregroundStyle(.white.opacity(0.82))
                                    .textSelection(.enabled)
                                if !radio.currentTrackPlot.isEmpty {
                                    Text(radio.currentTrackPlot)
                                        .font(.system(size: 12, weight: .medium))
                                        .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                                        .lineLimit(4)
                                        .textSelection(.enabled)
                                } else if !radio.currentTrackURL.isEmpty {
                                    Text("track loaded")
                                        .font(.system(size: 12, weight: .medium, design: .monospaced))
                                        .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                                } else {
                                    Text("no track loaded")
                                        .font(.system(size: 12, weight: .medium, design: .monospaced))
                                        .foregroundStyle(.white.opacity(0.55))
                                }
                                if !musicDiagnostic.isEmpty {
                                    Divider().background(Color.white.opacity(0.15))
                                    Text("Why isn't audio playing?")
                                        .font(.system(size: 11, weight: .bold))
                                        .foregroundStyle(Color(red: 1.0, green: 0.72, blue: 0.36))
                                    Text(musicDiagnostic)
                                        .font(.system(size: 12, weight: .medium, design: .monospaced))
                                        .foregroundStyle(Color(red: 1.0, green: 0.84, blue: 0.62))
                                        .fixedSize(horizontal: false, vertical: true)
                                        .textSelection(.enabled)
                                }
                                Button(action: { musicDiagnostic = K1L0RadioPlayer.shared.diagnose() }) {
                                    Text("Re-check playback")
                                        .font(.system(size: 12, weight: .semibold, design: .monospaced))
                                        .padding(.horizontal, 10).padding(.vertical, 5)
                                        .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 6))
                                        .foregroundStyle(.white.opacity(0.82))
                                }
                                .buttonStyle(.plain)
                            }
                            .padding(.vertical, 7)
                            .padding(.horizontal, 10)
                            .background(Color.white.opacity(0.025), in: RoundedRectangle(cornerRadius: 10))
                            .onAppear {
                                musicDiagnostic = K1L0RadioPlayer.shared.diagnose()
                            }
                        }
                    }

                    if selectedSection == "Timers" {
                        SettingsSection(title: "Timers", resetAction: {
                            ambientMinStepsToSpawn = 0.0
                            receiveStepsRequired = 200.0
                            transmissionWaitSteps = 500.0
                            momentumSessionGraceMinutes = 20.0
                            ambientBeamTtlMinutes = 30.0
                            ambientCollectRadiusMeters = 16.0
                            locationCollectRadiusFeet = 50.0
                            ambientBeamDismissSteps = 80.0
                            beamDistanceLabels = true
                            projectorLaserBeams = true
                            beamDebug = false
                            perfOverlay = true
                            showStoryStrip = false
                            panelMapBrightness = 0.34

                            K1L0WeatherOverlayInstaller.setUnitySetting("ambientMinStepsToSpawn", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("receiveStepsRequired", "200.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("transmissionWaitSteps", "500.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("momentumSessionGraceMinutes", "20.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("ambientBeamTtlMinutes", "30.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("ambientCollectRadiusMeters", "16.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("locationCollectRadiusFeet", "50.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("ambientBeamDismissSteps", "80.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("beamDistanceLabels", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("projectorLaserBeams", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("beamDebug", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("perfOverlay", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("showStoryStrip", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("panelMapBrightness", "0.340")
                        }) {
                            SettingSliderRow(title: "Ambient Spawn Gate", subtitle: "Steps required before mystery objects can appear.", value: $ambientMinStepsToSpawn, range: 0...2000, step: 10, key: "ambientMinStepsToSpawn")
                            SettingSliderRow(title: "Tune Signal Steps", subtitle: "Steps needed to tune an incoming transmission from 0% to 100%.", value: $receiveStepsRequired, range: 50...2000, step: 50, key: "receiveStepsRequired")
                            SettingSliderRow(title: "Next Signal Wait", subtitle: "Steps after one signal before another incoming signal can appear.", value: $transmissionWaitSteps, range: 0...5000, step: 50, key: "transmissionWaitSteps")
                            SettingSliderRow(title: "Walk Session Reset", subtitle: "Idle minutes before live steps reset into a new walking session.", value: $momentumSessionGraceMinutes, range: 10...240, step: 5, key: "momentumSessionGraceMinutes")
                            SettingSliderRow(title: "Mystery Object Expiry", subtitle: "Max minutes a spawned mystery object stays available.", value: $ambientBeamTtlMinutes, range: 1...240, step: 1, key: "ambientBeamTtlMinutes")
                            SettingSliderRow(title: "Collect Radius", subtitle: "Meters from a mystery object needed to collect it.", value: $ambientCollectRadiusMeters, range: 1...100, step: 1, key: "ambientCollectRadiusMeters")
                            SettingSliderRow(title: "Location Collect Radius", subtitle: "Feet from a place artifact needed to collect it.", value: $locationCollectRadiusFeet, range: 10...300, step: 5, key: "locationCollectRadiusFeet")
                            SettingSliderRow(title: "Walk-Away Dismiss", subtitle: "Steps away from a pursued object before the alert is dismissed.", value: $ambientBeamDismissSteps, range: 0...1000, step: 10, key: "ambientBeamDismissSteps")
                            SettingToggleRow(title: "Beam Labels", value: $beamDistanceLabels, key: "beamDistanceLabels")
                            SettingToggleRow(title: "Projector Laser Beams", value: $projectorLaserBeams, key: "projectorLaserBeams")
                            SettingToggleRow(title: "Beam Debug", value: $beamDebug, key: "beamDebug")
                            SettingToggleRow(title: "Perf Overlay", value: $perfOverlay, key: "perfOverlay")
                            SettingToggleRow(title: "Story Strip", value: $showStoryStrip, key: "showStoryStrip")
                            SettingSliderRow(title: "Panel Map Bright", value: $panelMapBrightness, range: 0...1, step: 0.01, key: "panelMapBrightness")
                        }
                    }

                    if selectedSection == "Transmission FX" {
                        SettingsSection(title: "Transmission FX", resetAction: {
                            transmissionFXEnabled = true
                            transmissionFXIntensity = 0.5
                            transmissionFizzyEdges = false

                            K1L0WeatherOverlayInstaller.setUnitySetting("transmissionFX", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("transmissionFXIntensity", "0.500")
                            K1L0WeatherOverlayInstaller.setUnitySetting("transmissionFizzyEdges", "0")
                        }) {
                            SettingToggleRow(title: "Glitch FX", value: $transmissionFXEnabled, key: "transmissionFX")
                            SettingSliderRow(title: "FX Intensity", value: $transmissionFXIntensity, range: 0...1, step: 0.05, key: "transmissionFXIntensity")
                            SettingToggleRow(title: "Fizzy Edges", value: $transmissionFizzyEdges, key: "transmissionFizzyEdges")
                        }
                    }

                    if selectedSection == "Layered Sky" {
                        SettingsSection(title: "Experimental Layered Sky", resetAction: {
                            experimentalLayeredSky = true
                            layeredBypassWeather = false
                            testSkyOverride = false
                            weatherOpenMeteo = true
                            manualHour = 13.25
                            manualWeather = 0
                            skyTargetFps = 30.0
                            layeredSkyEffect = 0
                            layeredRain = 0
                            layeredAurora = 0
                            layeredSkyTopHue = 0.80
                            layeredSkyMidHue = 0.62
                            layeredNightBlackness = 0.60
                            layeredSkyHorizonHue = 0.73
                            layeredCloudOpacity = 0.35
                            layeredCloudSpeed = 0.07
                            layeredCloudScale = 1.35
                            layeredCloudContrast = 1.1
                            layeredHorizonHeight = 0.0
                            solarWorldOverride = false
                            K1L0WeatherOverlayInstaller.setUnitySetting("experimentalLayeredSky", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredBypassWeather", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("testSkyOverride", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("weatherOpenMeteo", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("manualHour", "13.250")
                            K1L0WeatherOverlayInstaller.setUnitySetting("manualWeather", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("skyTargetFps", "30.000")
                            K1L0WindowGlowResolver.applyManualHour(13.25)
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredSkyEffect", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredRain", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredAurora", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredSkyTopHue", "0.800")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredSkyMidHue", "0.620")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredNightBlackness", "0.600")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredSkyHorizonHue", "0.730")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredCloudOpacity", "0.350")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredCloudSpeed", "0.070")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredCloudScale", "1.350")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredCloudContrast", "1.100")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredHorizonHeight", "0.0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("solarWorldOverride", "0")
                        }) {
                            solarLiveReadout("Sun, moon, palette and world lighting follow GPS + UTC")
                            Text("Layered Metal Sky · production renderer")
                                .font(.system(size: 11, weight: .medium, design: .monospaced))
                                .foregroundStyle(.white.opacity(0.58))
                            SettingToggleRow(title: "Open-Meteo Source", value: $weatherOpenMeteo, key: "weatherOpenMeteo")
                            SettingSliderRow(title: "Sky Speed (FPS)", value: $skyTargetFps, range: 1...60, step: 1, key: "skyTargetFps")
                            SettingToggleRow(title: "Bypass Live Weather", value: $layeredBypassWeather, key: "layeredBypassWeather")
                            Text(manualLookActive
                                 ? "manual mode — sliders are live and persist."
                                 : "auto-preview: bypass turned on while this panel is open so you can tune. Closing the section restores live weather.")
                                .font(.system(size: 11, weight: .semibold))
                                .foregroundStyle(.white.opacity(0.55))
                            if !layeredBypassWeather {
                                Text(String(format: "Live cloud cover %.0f%% · opacity %.2f", liveCloudCover, min(0.88, max(0.08, liveCloudCover / 100))))
                                    .font(.system(size: 12, weight: .bold, design: .monospaced))
                                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                            }
                            SettingSkyTimeRow(manualHour: $manualHour)
                            SettingWeatherSegmentRow(selection: $manualWeather)
                            SettingLayeredSkyEffectRow(selection: $layeredSkyEffect)
                            if layeredSkyEffect == 1 || layeredSkyEffect == 4 {
                                SettingSliderRow(title: "Rain Intensity", value: $layeredRain, range: 0...1, step: 0.05, key: "layeredRain")
                            }
                            if layeredSkyEffect == 3 {
                                SettingSliderRow(title: "Aurora Intensity", value: $layeredAurora, range: 0...1, step: 0.05, key: "layeredAurora")
                            }
                            SettingSliderRow(title: "Zenith Hue", value: $layeredSkyTopHue, range: 0...1, step: 0.01, key: "layeredSkyTopHue")
                            SettingSliderRow(title: "Mid-Sky Hue", value: $layeredSkyMidHue, range: 0...1, step: 0.01, key: "layeredSkyMidHue")
                            SettingSliderRow(title: "Horizon Hue", value: $layeredSkyHorizonHue, range: 0...1, step: 0.01, key: "layeredSkyHorizonHue")
                            // Horizon Height slider removed — pinned to 0 so
                            // horizon color never covers cloud bodies.
                            SettingSliderRow(title: "Night Blackness", value: $layeredNightBlackness, range: 0...1, step: 0.02, key: "layeredNightBlackness")
                            SettingSliderRow(title: "Cloud Opacity", value: $layeredCloudOpacity, range: 0...1, step: 0.02, key: "layeredCloudOpacity")
                            SettingSliderRow(title: "Cloud Speed", value: $layeredCloudSpeed, range: -0.5...0.5, step: 0.01, key: "layeredCloudSpeed")
                            SettingSliderRow(title: "Cloud Scale", value: $layeredCloudScale, range: 0.5...6, step: 0.1, key: "layeredCloudScale")
                            SettingSliderRow(title: "Cloud Contrast", value: $layeredCloudContrast, range: 0.2...4, step: 0.1, key: "layeredCloudContrast")
                            SettingSliderRow(title: "Window Brightness Jitter", value: $zossBrightnessJitter, range: 0...1, step: 0.01, key: "zossBrightnessJitter")
                            SettingSliderRow(title: "Window Jitter Rate", value: $zossBrightnessJitterRate, range: 0.05...4, step: 0.05, key: "zossBrightnessJitterRate")
                            SettingSliderRow(title: "Wall Daylight Lift", value: $zossWallDaylightLift, range: 0...1, step: 0.01, key: "zossWallDaylightLift")
                            SettingSliderRow(title: "Wall Variance (Per Building)", value: $zossWallVariance, range: 0...1, step: 0.01, key: "zossWallVariance")
                            SettingSliderRow(title: "Road Brightness", value: $roadValue, range: 0...1, step: 0.01, key: "roadValue")
                        }
                        // Bypass no longer toggles when Sky Lab appears —
                        // Unity's settingsPanelOpen flag freezes the live
                        // palette so slider tweaks take effect directly.
                        // Seeding of hues/cloud opacity/manual hour happens
                        // in the panel-scope .onAppear below.
                    }
                    }
                    .foregroundStyle(.white)
                    .padding(.horizontal, 18)
                    .padding(.bottom, 110)
                }
            }
        }
        .onAppear {
            if selectedSection == "Near Fog" {
                selectedSection = "Fog"
                fogPass = "Near"
            } else if selectedSection == "Distance Fog" {
                selectedSection = "Fog"
                fogPass = "Far"
            } else if selectedSection == "Menu" || selectedSection.isEmpty {
                selectedSection = "Location"
            }
            snapshotLookPreview()
            perfStats.startNativeSampling()
        }
        .onDisappear {
            restoreLookPreview()
        }
    }

    // Populate Sky Lab sliders with the sky's current live-computed values
    // (Unity writes these each frame from ApplyLiveSolarPalette) plus cloud
    // opacity from live cloud cover and manualHour from the wall clock.
    // Called on panel open so the sliders reflect current state as their
    // starting point.
    private func seedSkyLabFromLive() {
        let ud = UserDefaults.standard
        if let live = ud.object(forKey: "k1lo_liveSkyTopHue") as? Double {
            layeredSkyTopHue = live
            K1L0WeatherOverlayInstaller.setUnitySetting("layeredSkyTopHue", String(format: "%.4f", live))
        }
        if let live = ud.object(forKey: "k1lo_liveSkyMidHue") as? Double {
            layeredSkyMidHue = live
            K1L0WeatherOverlayInstaller.setUnitySetting("layeredSkyMidHue", String(format: "%.4f", live))
        }
        if let live = ud.object(forKey: "k1lo_liveSkyHorizonHue") as? Double {
            layeredSkyHorizonHue = live
            K1L0WeatherOverlayInstaller.setUnitySetting("layeredSkyHorizonHue", String(format: "%.4f", live))
        }
        let liveOpacity = min(0.88, max(0.08, liveCloudCover / 100))
        layeredCloudOpacity = liveOpacity
        K1L0WeatherOverlayInstaller.setUnitySetting("layeredCloudOpacity", String(format: "%.4f", liveOpacity))
        let cal = Calendar.current
        let comps = cal.dateComponents([.hour, .minute, .second], from: Date())
        let hourNow = Double(comps.hour ?? 12) + Double(comps.minute ?? 0) / 60.0 + Double(comps.second ?? 0) / 3600.0
        manualHour = hourNow
        K1L0WeatherOverlayInstaller.setUnitySetting("manualHour", String(format: "%.3f", hourNow))
        K1L0WindowGlowResolver.applyManualHour(hourNow)
    }

    private var solarDayness: Double {
        min(1, max(0, (liveSolarAltitude + 4) / 14))
    }

    private var liveWindowIntensity: Double {
        solarWorldOverride ? zossEmissiveIntensity : zossEmissiveIntensity * (1 - solarDayness) + 0.12 * solarDayness
    }

    private var liveBloomIntensity: Double {
        solarWorldOverride ? bloomIntensity : bloomIntensity * (1 - solarDayness) + 0.65 * solarDayness
    }

    @ViewBuilder
    private func solarLiveReadout(_ detail: String) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(String(format: "LIVE SUN  alt %.1f°  az %.1f°  day %.0f%%", liveSolarAltitude, liveSolarAzimuth, solarDayness * 100))
                .font(.system(size: 11, weight: .bold, design: .monospaced))
                .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
            Text(detail)
                .font(.system(size: 11, weight: .medium, design: .monospaced))
                .foregroundStyle(.white.opacity(0.58))
            SettingToggleRow(title: "Override Solar World", value: $solarWorldOverride, key: "solarWorldOverride")
        }
        .padding(.bottom, 8)
    }

    private func syncLayeredSkySettings() {
        K1L0WeatherOverlayInstaller.setUnitySetting("experimentalLayeredSky", experimentalLayeredSky ? "1" : "0")
        K1L0WeatherOverlayInstaller.setUnitySetting("layeredBypassWeather", layeredBypassWeather ? "1" : "0")
        K1L0WeatherOverlayInstaller.setUnitySetting("layeredSkyEffect", "\(layeredSkyEffect)")
        K1L0WeatherOverlayInstaller.setUnitySetting("layeredRain", String(format: "%.3f", layeredRain))
        K1L0WeatherOverlayInstaller.setUnitySetting("layeredAurora", String(format: "%.3f", layeredAurora))
        K1L0WeatherOverlayInstaller.setUnitySetting("layeredSkyTopHue", String(format: "%.3f", layeredSkyTopHue))
        K1L0WeatherOverlayInstaller.setUnitySetting("layeredSkyMidHue", String(format: "%.3f", layeredSkyMidHue))
        K1L0WeatherOverlayInstaller.setUnitySetting("layeredNightBlackness", String(format: "%.3f", layeredNightBlackness))
        K1L0WeatherOverlayInstaller.setUnitySetting("layeredSkyHorizonHue", String(format: "%.3f", layeredSkyHorizonHue))
        K1L0WeatherOverlayInstaller.setUnitySetting("layeredCloudOpacity", String(format: "%.3f", layeredCloudOpacity))
        K1L0WeatherOverlayInstaller.setUnitySetting("layeredCloudSpeed", String(format: "%.3f", layeredCloudSpeed))
        K1L0WeatherOverlayInstaller.setUnitySetting("layeredCloudScale", String(format: "%.3f", layeredCloudScale))
        K1L0WeatherOverlayInstaller.setUnitySetting("layeredCloudContrast", String(format: "%.3f", layeredCloudContrast))
    }

    private func resetNativeFogDefaultsOnce() {
        let defaults = UserDefaults.standard
        let key = "k1lo_native_clearBadFogDefaults_v2"
        guard !defaults.bool(forKey: key) else { return }

        fogConstantDensity = false
        fogDensity = 0.01
        fogNoiseStrength = 1.67
        fogNoiseScale = 17.4
        fogBrightness = 0.34
        fogScatteringIntensity = 1.15
        fogHeight = 77.0
        fogDistantFog = true
        fogDistantDensity = 0.0
        fogDistantStart = 0.0
        fogNativeLights = false
        fogNativeLightsMultiplier = 0.0
        defaults.set(true, forKey: key)
    }

    @ViewBuilder
    private func fogGroupTitle(_ title: String) -> some View {
        Text(title.uppercased())
            .font(.system(size: 11, weight: .bold, design: .monospaced))
            .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.82))
            .padding(.top, 8)
            .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var fogSnapshotButton: some View {
        VStack(alignment: .leading, spacing: 6) {
            Button {
                if weatherLookMode == "day" || weatherLookMode == "night" {
                    publishFogSnapshot(to: weatherLookMode)
                } else {
                    showingFogPublishTargetPicker = true
                }
            } label: {
                Label(
                    fogPublishInFlight ? "Publishing…" : "Publish Fog Server Default",
                    systemImage: "arrow.up.doc"
                )
                    .font(.system(size: 13, weight: .bold, design: .monospaced))
                    .frame(maxWidth: .infinity, minHeight: 38)
                    .foregroundStyle(.black)
                    .background(Color(red: 0.66, green: 1.0, blue: 0.76), in: RoundedRectangle(cornerRadius: 8))
            }
            .buttonStyle(.plain)
            .disabled(fogPublishInFlight)
            if !fogSnapshotStatus.isEmpty {
                Text(fogSnapshotStatus)
                    .font(.system(size: 10, weight: .semibold, design: .monospaced))
                    .foregroundStyle(.white.opacity(0.62))
            }
        }
        .padding(.top, 8)
        .confirmationDialog(
            "Which server fog profile should Auto update?",
            isPresented: $showingFogPublishTargetPicker,
            titleVisibility: .visible
        ) {
            Button("Update Day Fog") { publishFogSnapshot(to: "day") }
            Button("Update Night Fog") { publishFogSnapshot(to: "night") }
            Button("Cancel", role: .cancel) {}
        }
    }

    private var fogOverrideControls: some View {
        VStack(alignment: .leading, spacing: 7) {
            if fogManualOverride {
                Button("REVERT TO SERVER") {
                    K1L0WeatherModeController.resetLocalValues(for: K1L0WeatherModeController.fogSection)
                    fogManualOverride = false
                    K1L0WeatherModeController.apply(weatherLookMode)
                }
                .buttonStyle(K1L0FogAuthorityButtonStyle(active: false))
            }
            Text(fogManualOverride ? "TEMPORARY FOG — Publish locks this into the server preset; Revert restores the server." : "SERVER FOG — moving any control starts a temporary local experiment.")
                .font(.system(size: 10, weight: .bold, design: .monospaced))
                .foregroundStyle(fogManualOverride ? Color.orange : Color(red: 0.66, green: 1.0, blue: 0.76))
        }
        .padding(.bottom, 5)
    }

    private func sectionOverrideControls(section: String,
                                         active: Binding<Bool>,
                                         remoteBaseline: Bool = false) -> some View {
        VStack(alignment: .leading, spacing: 7) {
            if active.wrappedValue {
                HStack(spacing: 8) {
                    Button("DETACH") {
                        K1L0WeatherModeController.clearSectionManualOverride(section)
                        active.wrappedValue = false
                        if remoteBaseline {
                            K1L0WeatherModeController.refreshGlobalSettingSections(section)
                        } else {
                            K1L0WeatherModeController.apply(weatherLookMode)
                        }
                    }
                    .buttonStyle(K1L0FogAuthorityButtonStyle(active: true))
                    Button("RESET TO SERVER") {
                        K1L0WeatherModeController.resetLocalValues(for: section)
                        active.wrappedValue = false
                        if remoteBaseline {
                            K1L0WeatherModeController.refreshGlobalSettingSections(section)
                        } else {
                            K1L0WeatherModeController.apply(weatherLookMode)
                        }
                    }
                    .buttonStyle(K1L0FogAuthorityButtonStyle(active: false))
                }
            }
            Text(active.wrappedValue
                 ? "CUSTOM SECTION — these controls ignore preset updates until DETACH."
                 : "SERVER / PRESET CONTROL — moving any control attaches this section locally.")
                .font(.system(size: 10, weight: .bold, design: .monospaced))
                .foregroundStyle(active.wrappedValue ? Color.orange : Color(red: 0.66, green: 1.0, blue: 0.76))
        }
        .padding(.bottom, 5)
    }

    private var lookOverrideControls: some View {
        VStack(alignment: .leading, spacing: 7) {
            if lookManualOverride {
                Button("DETACH") {
                    K1L0WeatherModeController.clearLookManualOverride()
                    lookManualOverride = false
                    K1L0WeatherModeController.apply(weatherLookMode)
                }
                .buttonStyle(K1L0FogAuthorityButtonStyle(active: true))
            }
            Text(lookManualOverride
                 ? "CUSTOM LOOK — lighting, windows, bloom and grade ignore preset updates until DETACH."
                 : "PRESET / AUTO LOOK — move any control to detach these values, or load the captured Mac reference.")
                .font(.system(size: 10, weight: .bold, design: .monospaced))
                .foregroundStyle(lookManualOverride ? Color.orange : Color(red: 0.66, green: 1.0, blue: 0.76))
        }
        .padding(.bottom, 5)
    }

    private func applyMacReferenceLook() {
        K1L0WeatherModeController.beginLookManualOverride()
        lookManualOverride = true

        ambientEnabled = false
        ambientIntensity = 0
        moonlightEnabled = true
        moonlightIntensity = 0.55
        spotlightEnabled = false
        spotlightIntensity = 3
        daySunIntensity = 1.35
        reflectionsEnabled = true
        reflectionIntensity = 1.34
        shadowStrength = 0
        shadowDistance = 227
        zossEmissiveIntensity = 8.2
        zossWindowBrightness = 1.38
        zossPaletteMix = 1
        zossPaletteSaturation = 0.48
        zossPaletteSaturationNight = 1.22
        zossWarmth = 0.32
        zossAccentFraction = 0.08
        zossBrightnessJitter = 0.50
        zossBrightnessJitterRate = 0.60
        bloomEnabled = true
        bloomIntensity = 2.20
        bloomThreshold = 0.78
        bloomScatter = 0.68
        mapBrightness = 0.22
        contrast = 0
        saturation = 0
        temperature = 0
        tint = 0

        let values: [String: String] = [
            "ambientEnabled": "0", "ambientIntensity": "0",
            "moonlightEnabled": "1", "moonlightIntensity": "0.55",
            "spotlightEnabled": "0", "spotlightIntensity": "3",
            "daySunIntensity": "1.35", "reflectionsEnabled": "1",
            "reflectionIntensity": "1.34", "shadowStrength": "0",
            "shadowDistance": "227",
            "zossEmissiveIntensity": "8.2", "zossWindowBrightness": "1.38",
            "zossPaletteMix": "1", "zossPaletteSaturation": "0.48",
            "zossPaletteSaturation_night": "1.22", "zossWarmth": "0.32",
            "zossAccentFraction": "0.08", "zossBrightnessJitter": "0.50",
            "zossBrightnessJitterRate": "0.60", "bloomEnabled": "1",
            "bloomIntensity": "2.20", "bloomThreshold": "0.78",
            "bloomScatter": "0.68", "mapBrightness": "0.22",
            "contrast": "0", "saturation": "0", "temperature": "0", "tint": "0"
        ]
        for (key, value) in values {
            K1L0WeatherOverlayInstaller.setUnitySetting(key, value)
        }
        K1L0WindowGlowResolver.apply()
        syncLightingSettings()
    }

    private func publishFogSnapshot(to requestedMode: String) {
        let mode = requestedMode == "night" ? "night" : "day"
        let defaults = UserDefaults.standard
        var settings: [String: String] = [:]
        var rawSettings: [String: Any] = [:]
        for key in Self.previewLookKeys where key == "nearFogEnabled" || key.hasPrefix("fog") {
            guard let raw = defaults.object(forKey: "k1lo_native_\(key)") else { continue }
            rawSettings[key] = raw
            if let value = raw as? Bool {
                settings[key] = value ? "1" : "0"
            } else if let value = raw as? NSNumber {
                settings[key] = String(format: "%.6f", value.doubleValue)
            } else {
                settings[key] = String(describing: raw)
            }
        }
        guard !settings.isEmpty else {
            fogSnapshotStatus = "No fog values to publish yet."
            return
        }
        fogPublishInFlight = true
        fogSnapshotStatus = "Publishing \(settings.count) values to \(mode.capitalized)…"
        K1L0NativeAPI.resolvePresetWriter { apiBase in
            guard let apiBase,
                  let url = URL(string: "\(apiBase)/api/k1l0/weather-presets/\(mode)/fog") else {
                fogPublishInFlight = false
                fogSnapshotStatus = "Publish requires a direct LAN connection to the K1L0 server."
                return
            }
            var request = URLRequest(url: url)
            request.httpMethod = "PATCH"
            request.timeoutInterval = 10
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            request.httpBody = try? JSONSerialization.data(withJSONObject: ["settings": settings])
            URLSession.shared.dataTask(with: request) { data, response, error in
                let status = (response as? HTTPURLResponse)?.statusCode ?? 0
                let payload: [String: Any]? = data.flatMap { raw in
                    (try? JSONSerialization.jsonObject(with: raw)) as? [String: Any]
                }
                let preset = payload?["preset"] as? [String: Any]
                let revision = preset?["revision"] as? Int
                DispatchQueue.main.async {
                    fogPublishInFlight = false
                    if error == nil && (200..<300).contains(status) {
                        publishedFogSnapshot = rawSettings
                        let suffix = revision.map { " · revision \($0)" } ?? ""
                        fogSnapshotStatus = "Saved \(settings.count) fog values to \(mode.capitalized)\(suffix)."
                        K1L0WeatherModeController.clearFogManualOverride()
                        fogManualOverride = false
                        K1L0WeatherModeController.apply(weatherLookMode)
                    } else {
                        let message = payload?["error"] as? String
                        fogSnapshotStatus = message ?? "Publish failed (HTTP \(status))."
                    }
                }
            }.resume()
        }
    }

    private func syncLightingSettings() {
        if spotlightEnabled && spotlightIntensity <= 0.01 {
            spotlightIntensity = 1.0
        }
        NativeUnityLightingSync.sync()
    }

    private func setLocationMode(_ mode: String) {
        locationMode = mode
        K1L0OverlayDataModel.activeModel?.setLocationMode(mode)
    }

    private func searchLocations() {
        let query = locationSearchText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !query.isEmpty, !locationSearchInFlight else { return }
        locationSearchInFlight = true
        locationSearchStatus = "Searching…"
        locationSearchResults = []

        let geocoder = CLGeocoder()
        geocoder.geocodeAddressString(query) { placemarks, error in
            DispatchQueue.main.async {
                locationSearchInFlight = false
                guard error == nil else {
                    locationSearchStatus = "Search failed. Try a city and state or country."
                    return
                }
                locationSearchResults = (placemarks ?? []).compactMap { placemark in
                    guard let coordinate = placemark.location?.coordinate else { return nil }
                    let title = placemark.name?.trimmingCharacters(in: .whitespacesAndNewlines)
                    let displayTitle = (title?.isEmpty == false ? title! : query)
                    let subtitleParts = [
                        placemark.locality,
                        placemark.administrativeArea,
                        placemark.country
                    ]
                    .compactMap { $0?.trimmingCharacters(in: .whitespacesAndNewlines) }
                    .filter { !$0.isEmpty && $0.caseInsensitiveCompare(displayTitle) != .orderedSame }
                    var seenSubtitleParts = Set<String>()
                    let subtitle = subtitleParts
                        .filter { seenSubtitleParts.insert($0.lowercased()).inserted }
                        .joined(separator: ", ")
                    return NativeLocationSearchResult(
                        id: String(format: "%.6f,%.6f", coordinate.latitude, coordinate.longitude),
                        title: displayTitle,
                        subtitle: subtitle.isEmpty ? String(format: "%.5f, %.5f", coordinate.latitude, coordinate.longitude) : subtitle,
                        latitude: coordinate.latitude,
                        longitude: coordinate.longitude
                    )
                }
                locationSearchStatus = locationSearchResults.isEmpty
                    ? "No matching locations."
                    : "Tap a result to save it and explore there."
            }
        }
    }

    private func selectSearchResult(_ result: NativeLocationSearchResult) {
        let preset = NativeLocationPreset.saveSearchResult(
            title: result.title,
            subtitle: result.subtitle,
            latitude: result.latitude,
            longitude: result.longitude
        )
        locationSearchResults = []
        locationSearchText = ""
        locationSearchStatus = "Saved \(preset.title). Loading its world…"
        setLocationMode(preset.id)
    }
}

struct LocationModeButton: View {
    let title: String
    let subtitle: String
    let selected: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: 10) {
                Image(systemName: selected ? "largecircle.fill.circle" : "circle")
                    .font(.system(size: 16, weight: .bold))
                    .foregroundStyle(selected ? Color(red: 0.66, green: 1.0, blue: 0.76) : .white.opacity(0.58))
                VStack(alignment: .leading, spacing: 2) {
                    Text(title)
                        .font(.system(size: 14, weight: .semibold))
                    Text(subtitle)
                        .font(.system(size: 11, weight: .medium))
                        .foregroundStyle(.white.opacity(0.58))
                }
                Spacer()
            }
            .padding(.vertical, 9)
            .padding(.horizontal, 10)
            .background(selected ? Color.white.opacity(0.08) : Color.white.opacity(0.025), in: RoundedRectangle(cornerRadius: 10))
        }
        .buttonStyle(.plain)
    }
}

struct SettingsCategoryButton: View {
    let title: String
    let tag: String
    @Binding var selection: String

    var body: some View {
        Button {
            selection = tag
        } label: {
            Text(title)
                .font(.system(size: 11, weight: .bold, design: .monospaced))
                .frame(maxWidth: .infinity, minHeight: 32)
                .foregroundStyle(selection == tag ? .black : .white)
                .background(selection == tag ? Color(red: 0.72, green: 1.0, blue: 0.68) : Color.white.opacity(0.12))
                .clipShape(RoundedRectangle(cornerRadius: 6, style: .continuous))
                .overlay(
                    RoundedRectangle(cornerRadius: 6, style: .continuous)
                        .stroke(selection == tag ? Color.clear : Color.white.opacity(0.15), lineWidth: 1)
                )
        }
        .buttonStyle(.plain)
    }
}

struct SettingsMasterMenuButton: View {
    let title: String
    let subtitle: String
    let systemImage: String
    @Binding var selection: String

    var body: some View {
        Button {
            selection = title
        } label: {
            HStack(spacing: 11) {
                Image(systemName: systemImage)
                    .font(.system(size: 16, weight: .semibold))
                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                    .frame(width: 24, height: 24)
                VStack(alignment: .leading, spacing: 2) {
                    Text(title)
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(.white)
                    Text(subtitle)
                        .font(.system(size: 11, weight: .medium))
                        .foregroundStyle(.white.opacity(0.56))
                        .lineLimit(1)
                }
                Spacer(minLength: 8)
                Image(systemName: "chevron.right")
                    .font(.system(size: 12, weight: .bold))
                    .foregroundStyle(.white.opacity(0.42))
            }
            .padding(.vertical, 9)
            .padding(.horizontal, 10)
            .background(Color.white.opacity(0.035), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
        }
        .buttonStyle(.plain)
    }
}

struct SettingsSegmentedRow: View {
    let items: [(title: String, tag: String)]
    @Binding var selection: String

    var body: some View {
        HStack(spacing: 0) {
            ForEach(0..<items.count, id: \.self) { index in
                let item = items[index]
                let isSelected = selection == item.tag
                Button {
                    selection = item.tag
                } label: {
                    Text(item.title)
                        .font(.system(size: 11, weight: .bold, design: .monospaced))
                        .frame(maxWidth: .infinity, minHeight: 32)
                        .foregroundStyle(isSelected ? .black : .white)
                        .background(isSelected ? Color(red: 0.72, green: 1.0, blue: 0.68) : Color.clear)
                        .clipShape(RoundedRectangle(cornerRadius: 6, style: .continuous))
                }
                .buttonStyle(.plain)

                if index < items.count - 1 && !isSelected && selection != items[index + 1].tag {
                    Color.white.opacity(0.15)
                        .frame(width: 1, height: 16)
                }
            }
        }
        .padding(2)
        .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 8, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 8, style: .continuous)
                .stroke(Color.white.opacity(0.12), lineWidth: 1)
        )
    }
}

struct SettingsSection<Content: View>: View {
    let title: String
    var resetAction: (() -> Void)?
    let content: Content

    init(title: String, resetAction: (() -> Void)? = nil, @ViewBuilder content: () -> Content) {
        self.title = title
        self.resetAction = resetAction
        self.content = content()
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text(title)
                    .font(.system(size: 18, weight: .bold))
                    .foregroundStyle(.white.opacity(0.92))
                Spacer()
                if let resetAction {
                    Button(action: resetAction) {
                        HStack(spacing: 4) {
                            Image(systemName: "arrow.counterclockwise")
                                .font(.system(size: 11, weight: .bold))
                            Text("Reset")
                                .font(.system(size: 11, weight: .bold, design: .monospaced))
                        }
                        .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                        .padding(.vertical, 4)
                        .padding(.horizontal, 8)
                        .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 6))
                    }
                    .buttonStyle(.plain)
                }
            }
            VStack(spacing: 9) {
                content
            }
        }
        .padding(16)
        .background(Color.black.opacity(0.06), in: RoundedRectangle(cornerRadius: 24, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 24, style: .continuous)
                .stroke(.white.opacity(0.10), lineWidth: 1)
        )
    }
}

struct PerfStatCell: View {
    let title: String
    let value: String

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(title)
                .font(.system(size: 10, weight: .bold, design: .monospaced))
                .foregroundStyle(.white.opacity(0.45))
            Text(value)
                .font(.system(size: 13, weight: .bold, design: .monospaced))
                .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                .lineLimit(1)
                .minimumScaleFactor(0.72)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.vertical, 8)
        .padding(.horizontal, 10)
        .background(Color.white.opacity(0.025), in: RoundedRectangle(cornerRadius: 10))
    }
}

struct SettingSliderRow: View {
    let title: String
    var subtitle: String? = nil
    @Binding var value: Double
    let range: ClosedRange<Double>
    let step: Double
    let key: String
    var syncOverride: ((Double) -> Void)? = nil

    var body: some View {
        VStack(alignment: .leading, spacing: 5) {
            HStack {
                Text(title)
                    .font(.system(size: 14, weight: .medium))
                Spacer()
                Text(formattedValue)
                    .font(.system(size: 13, weight: .semibold))
                    .monospacedDigit()
                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
            }
            if let subtitle, !subtitle.isEmpty {
                Text(subtitle)
                    .font(.system(size: 11, weight: .medium))
                    .foregroundStyle(.white.opacity(0.58))
                    .fixedSize(horizontal: false, vertical: true)
            }
#if os(macOS)
            Slider(value: $value, in: range)
                .tint(Color(red: 0.66, green: 1.0, blue: 0.76))
	                .onChange(of: value) { newValue in
	                    let snapped = (newValue / step).rounded() * step
	                    if abs(snapped - value) > 0.0001 {
	                        value = min(max(snapped, range.lowerBound), range.upperBound)
	                        return
	                    }
	                    syncSettingValue(newValue)
	                    syncDependentSkyVideoUrl(newValue: newValue)
	                }
#else
	            Slider(value: $value, in: range, step: step)
	                .tint(Color(red: 0.66, green: 1.0, blue: 0.76))
	                .onChange(of: value) { newValue in
	                    syncSettingValue(newValue)
	                    syncDependentSkyVideoUrl(newValue: newValue)
	                }
#endif
	        }
	    }

    private func syncSettingValue(_ newValue: Double) {
        // Any direct fog edit becomes authoritative immediately. Auto astronomy
        // reapplies its blended preset every minute; without this flag that
        // periodic sync can overwrite an in-progress fog experiment (commonly
        // restoring a preset density of zero). Publish or Revert explicitly
        // clears the flag and reconnects preset / Auto control.
        K1L0WeatherModeController.beginSectionManualOverride(forSetting: key)
        let isFarFogControl = key == "fogDistantDensity"
            || key == "fogDistantDensity_night"
            || key == "fogDistantStart"
            || key == "fogDistantStart_night"
            || key.hasPrefix("fogV2Distant")
        if isFarFogControl {
            // A far-fog experiment must be visible even if a previous preset
            // left the independent far pass disabled.
            UserDefaults.standard.set(true, forKey: "k1lo_native_fogDistantFog")
            K1L0WeatherOverlayInstaller.setUnitySetting("fogDistantFog", "1")
        }
        if key == "zossEmissiveHue" ||
            key == "zossEmissiveSaturation" ||
            key == "zossNightEmissiveHue" ||
            key == "zossNightEmissiveSaturation" {
            K1L0WindowGlowResolver.apply()
            return
        }
        if let syncOverride {
            syncOverride(newValue)
        } else {
            K1L0WeatherOverlayInstaller.setUnitySetting(key, String(format: "%.3f", newValue))
        }
    }

	    private func syncDependentSkyVideoUrl(newValue: Double) {
	        guard key == "manualHour" else { return }
	        let manualWeather = UserDefaults.standard.integer(forKey: "k1lo_native_manualWeather")
        K1L0WindowGlowResolver.applyManualHour(newValue)
	    }

    private var formattedValue: String {
        if step < 0.001 { return String(format: "%.5f", value) }
        if step < 0.01 { return String(format: "%.3f", value) }
        return abs(value.rounded() - value) < 0.001 ? "\(Int(value.rounded()))" : String(format: "%.2f", value)
    }
}

struct SettingToggleRow: View {
    let title: String
    @Binding var value: Bool
    let key: String

    var body: some View {
        Button {
            value.toggle()
            K1L0WeatherModeController.beginSectionManualOverride(forSetting: key)
            K1L0WeatherOverlayInstaller.setUnitySetting(key, value ? "1" : "0")
        } label: {
            HStack {
                Text(title)
                    .font(.system(size: 14, weight: .medium))
                Spacer()
                Text(value ? "[ON]" : "[OFF]")
                    .font(.system(size: 13, weight: .bold, design: .monospaced))
                    .foregroundStyle(value ? Color(red: 0.66, green: 1.0, blue: 0.76) : .white.opacity(0.55))
            }
            .padding(.vertical, 7)
            .padding(.horizontal, 10)
            .background(value ? Color.white.opacity(0.08) : Color.white.opacity(0.025), in: RoundedRectangle(cornerRadius: 10))
        }
        .buttonStyle(.plain)
    }
}

struct K1L0FogAuthorityButtonStyle: ButtonStyle {
    let active: Bool

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 12, weight: .black, design: .monospaced))
            .foregroundStyle(active ? .black : .white)
            .frame(maxWidth: .infinity, minHeight: 36)
            .background(
                active ? Color(red: 0.66, green: 1.0, blue: 0.76) : Color.white.opacity(0.10),
                in: RoundedRectangle(cornerRadius: 8)
            )
            .opacity(configuration.isPressed ? 0.7 : 1)
    }
}

// Day / Dusk / Night presets instead of a fiddly 0–24 hour slider. The sky
// VIDEO only cares about day vs night, but the hour also drives the sun arc
// (dawn/dusk color, ground dayness, fog), so Dusk earns its slot.
struct SettingSkyTimeRow: View {
    @Binding var manualHour: Double

    private static let presets: [(label: String, hour: Double)] = [
        ("Day", 13.0), ("Dusk", 18.5), ("Night", 22.0)
    ]

    private var selectedIndex: Int {
        if manualHour >= 6, manualHour < 17.5 { return 0 }
        if manualHour >= 17.5, manualHour < 19.0 { return 1 }
        return 2
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("Sky Time")
                .font(.system(size: 14, weight: .medium))
            Picker("Sky Time", selection: Binding(
                get: { selectedIndex },
                set: { index in
                    let preset = Self.presets[min(max(0, index), Self.presets.count - 1)]
                    manualHour = preset.hour
                    K1L0WeatherOverlayInstaller.setUnitySetting("manualHour", String(format: "%.2f", preset.hour))
                }
            )) {
                ForEach(Self.presets.indices, id: \.self) { index in
                    Text(Self.presets[index].label).tag(index)
                }
            }
            .pickerStyle(.segmented)
        }
    }
}

struct SettingWeatherSegmentRow: View {
    @Binding var selection: Int

    private let options = ["Clear", "Partly", "Cloud", "Overcast", "Rain", "Snow", "Fog", "Storm"]

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("Manual Weather")
                .font(.system(size: 14, weight: .medium))
            Picker("Manual Weather", selection: $selection) {
                ForEach(options.indices, id: \.self) { index in
                    Text(options[index]).tag(index)
                }
            }
            .pickerStyle(.segmented)
	            .onChange(of: selection) { newValue in
	                K1L0WeatherOverlayInstaller.setUnitySetting("manualWeather", "\(newValue)")
	                let manualHour = UserDefaults.standard.double(forKey: "k1lo_native_manualHour")
                K1L0WindowGlowResolver.applyManualHour(manualHour)
	            }
	        }
	    }
}

struct SettingLayeredSkyEffectRow: View {
    @Binding var selection: Int
    private let options = ["Clear", "Rain", "Snow", "Aurora", "Storm"]

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("Sky Effect")
                .font(.system(size: 14, weight: .medium))
            Picker("Sky Effect", selection: $selection) {
                ForEach(options.indices, id: \.self) { index in
                    Text(options[index]).tag(index)
                }
            }
            .pickerStyle(.segmented)
            .onChange(of: selection) { value in
                K1L0WeatherOverlayInstaller.setUnitySetting("layeredSkyEffect", "\(value)")
            }
        }
    }
}
