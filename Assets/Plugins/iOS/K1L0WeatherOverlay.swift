import AVFoundation
import AVKit
import CoreImage
import CoreLocation
import Darwin
import Foundation
import Metal
import MetalKit
import SwiftUI
#if canImport(UIKit)
import UIKit
#elseif canImport(AppKit)
import AppKit
#endif
#if os(iOS)
import CoreMotion
@_silgen_name("UnityPause")
private func K1L0UnityPause(_ pause: Int32)
#endif

private let K1L0DefaultHelmetIconURL = "https://cdn.kilo.gallery/k1l0/ref/generic_closed_helmet_v2.png"

private enum K1L0MediaCache {
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

private enum K1L0NativeSettingsDefaults {
    static let values: [String: Any] = [
        // "Dystopian daylight" grade: drained color, cold temp, sickly green
        // tint, harsher contrast — the sky stays daytime but reads bleak.
        "k1lo_native_saturation": -28.0,
        "k1lo_native_contrast": 14.0,
        "k1lo_native_mapBrightness": -0.12,
        "k1lo_native_hueShift": -4.0,
        "k1lo_native_temperature": -12.0,
        "k1lo_native_tint": -6.0,
        "k1lo_native_bloomEnabled": true,
        "k1lo_native_bloomIntensity": 2.4,
        "k1lo_native_bloomThreshold": 1.2,
        "k1lo_native_bloomScatter": 0.43,
        "k1lo_native_vignetteEnabled": true,
        "k1lo_native_vignetteIntensity": 0.45,
        "k1lo_native_vignetteSmoothness": 1.0,
        "k1lo_native_chromaticEnabled": true,
        "k1lo_native_chromaticIntensity": 0.16,
        "k1lo_native_lensDistEnabled": true,
        "k1lo_native_lensDistIntensity": -0.5,
        "k1lo_native_dofEnabled": false,
        "k1lo_native_focusDistance": 18.1,
        "k1lo_native_aperture": 8.25,
        "k1lo_native_focalLength": 119.0,
        "k1lo_native_motionBlurEnabled": false,
        "k1lo_native_motionBlurIntensity": 0.02,
        "k1lo_native_filmGrainEnabled": true,
        "k1lo_native_filmGrainIntensity": 0.4,
        "k1lo_native_godPositionY": 51.0,
        "k1lo_native_godPositionZ": 107.0,
        "k1lo_native_godRotationX": -1.0,
        "k1lo_native_farClipPlane": 3600.0,
        "k1lo_native_moonlightEnabled": true,
        "k1lo_native_moonlightManualOverride": false,
        "k1lo_native_moonlightIntensity": 1.0,
        "k1lo_native_moonlightRed": 0.7,
        "k1lo_native_moonlightGreen": 0.8,
        "k1lo_native_moonlightBlue": 1.0,
        "k1lo_native_moonlightPitch": 90.0,
        "k1lo_native_moonlightYaw": 0.0,
        "k1lo_native_moonlightRoll": 0.0,
        "k1lo_native_ambientEnabled": true,
        // Dusty skylight so terrain reads as grey wasteland instead of void.
        // 1.15 keeps grass/roads readable at night without washing out the grade.
        "k1lo_native_ambientIntensity": 1.55,
        "k1lo_native_spotlightEnabled": true,
        "k1lo_native_spotlightIntensity": 3.0,
        "k1lo_native_zossEmissiveIntensity": 1.9,
        "k1lo_native_zossEmissiveSmoothness": 0.34,
        "k1lo_native_zossEmissiveMetallic": 0.0,
        // Window glow: vaporwave magenta-pink (hue 0.90 ≈ 324°) to match the
        // pink day skies; ground carries a faint irradiated-olive tinge so it
        // reads as ash, not paper-white.
        "k1lo_native_zossEmissiveHue": 0.90,
        "k1lo_native_zossEmissiveSaturation": 0.62,
        "k1lo_native_zossNightEmissiveHue": 0.115,
        "k1lo_native_zossNightEmissiveSaturation": 0.82,
        // Green grass (hue 0.33) with real saturation — the old ash-olive
        // (0.23/0.12) read as black under the pink skies.
        "k1lo_native_groundHue": 0.33,
        "k1lo_native_groundSaturation": 0.42,
        "k1lo_native_beamDistanceLabels": false,
        "k1lo_native_beamDebug": false,
        "k1lo_native_perfOverlay": true,
        "k1lo_native_showStoryStrip": false,
        "k1lo_native_panelMapBrightness": 0.34,
        "k1lo_native_weatherOpenMeteo": true,
        "k1lo_native_bottomMenuLayout": "tabs",
        "k1lo_native_manualHour": 13.25,
        // Manual weather is only a fallback for when live weather is missing —
        // it must stay Clear so a sunny day never shows the overcast sky video.
        "k1lo_native_manualWeather": 0,
        "k1lo_native_ambientMinStepsToSpawn": 110.0,
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
        "k1lo_native_musicRadioMode": "final",
        "k1lo_native_fogConstantDensity": false,
        "k1lo_native_fogDensity": 0.37,
        "k1lo_native_fogNoiseStrength": 1.67,
        "k1lo_native_fogNoiseScale": 17.4,
        // Brighter fog = pale radioactive dust instead of black smog.
        "k1lo_native_fogBrightness": 0.34,
        "k1lo_native_fogScatteringIntensity": 1.15,
        "k1lo_native_fogHeight": 77.0,
        "k1lo_native_fogDistantFog": true,
        // Horizon haze: soften the hard sky/city seam so daylight reads
        // polluted rather than postcard-clear.
        "k1lo_native_fogDistantDensity": 0.0,
        "k1lo_native_fogDistantStart": 0.0,
        "k1lo_native_fogNativeLights": false,
        "k1lo_native_fogNativeLightsMultiplier": 0.0,
        "k1lo_native_skyTargetFps": 30.0,
        "k1lo_native_experimentalLayeredSky": false,
        "k1lo_native_layeredSkyTopHue": 0.62,
        "k1lo_native_layeredSkyHorizonHue": 0.94,
        "k1lo_native_layeredCloudOpacity": 0.72,
        "k1lo_native_layeredCloudSpeed": 0.08,
        "k1lo_native_layeredCloudScale": 2.2,
        "k1lo_native_layeredCloudContrast": 1.5,
        "k1lo_native_fogDensity_night": 0.37,
        "k1lo_native_fogNoiseStrength_night": 1.67,
        "k1lo_native_fogNoiseScale_night": 17.4,
        "k1lo_native_fogBrightness_night": 0.34,
        "k1lo_native_fogScatteringIntensity_night": 1.15,
        "k1lo_native_fogHeight_night": 77.0,
        "k1lo_native_fogDistantDensity_night": 0.0,
        "k1lo_native_fogDistantStart_night": 0.0,
        "k1lo_native_groundHue_night": 0.30,
        "k1lo_native_groundSaturation_night": 0.0
    ]

    static func register() {
        UserDefaults.standard.register(defaults: values)
        applyDystopianGradeOnce()
        applyGroundLightFixOnce()
        resetManualWeatherToClearOnce()
        applyGroundLiftOnce()
        applyPinkWindowGlowOnce()
        applyGreenGrassOnce()
        applyGrassVisibilityLightOnce()
        applyHudCameraDefaultsOnce()
        disableThermalHeavyTransmissionEdgesOnce()
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
            "k1lo_native_godPositionY": 51.0,
            "k1lo_native_godPositionZ": 107.0,
            "k1lo_native_godRotationX": -1.0,
            "k1lo_native_farClipPlane": 3600.0,
            "k1lo_native_bottomMenuLayout": "tabs",
            "k1lo_native_fogBrightness": 0.34,
            "k1lo_native_fogDistantDensity": 0.0,
            "k1lo_native_fogDistantStart": 0.0,
            "k1lo_native_zossEmissiveSaturation": 0.62,
            "k1lo_godPositionY": 51.0,
            "k1lo_godPositionZ": 107.0,
            "k1lo_godRotationX": -1.0,
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
        defaults.set(false, forKey: "k1lo_native_beamDistanceLabels")
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
    /// shadow the registered defaults, so stamp the new "dystopian daylight"
    /// grade over them once. Users can still re-tune afterward.
    private static func applyDystopianGradeOnce() {
        let defaults = UserDefaults.standard
        let flag = "k1lo_native_dystopianGrade_v1"
        guard !defaults.bool(forKey: flag) else { return }
        let grade: [String: Any] = [
            "k1lo_native_saturation": -28.0,
            "k1lo_native_contrast": 14.0,
            "k1lo_native_mapBrightness": -0.12,
            "k1lo_native_temperature": -12.0,
            "k1lo_native_tint": -6.0,
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
}

private enum K1L0SkyVideoURLResolver {
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
}

private enum K1L0WindowGlowResolver {
    private static let lastWeatherIsDayKey = "k1lo_native_lastWeatherIsDay"

    static func apply(isDay explicitIsDay: Bool? = nil) {
        let defaults = UserDefaults.standard
        let isDay = explicitIsDay ?? storedOrManualIsDay(defaults)
        let hue: Double
        let saturation: Double
        if isDay {
            hue = defaults.object(forKey: "k1lo_native_zossEmissiveHue") as? Double ?? 0.90
            saturation = defaults.object(forKey: "k1lo_native_zossEmissiveSaturation") as? Double ?? 0.62
        } else {
            hue = defaults.object(forKey: "k1lo_native_zossNightEmissiveHue") as? Double ?? 0.115
            saturation = defaults.object(forKey: "k1lo_native_zossNightEmissiveSaturation") as? Double ?? 0.82
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

private enum K1L0NativeAPI {
    static let candidates = [
        "https://api-tunnel.kilo.gallery",
        "http://192.168.40.34:3000",
    ]

    static func resolve(completion: @escaping (String) -> Void) {
        test(at: 0, completion: completion)
    }

    private static func test(at index: Int, completion: @escaping (String) -> Void) {
        guard index < candidates.count else {
            completion(candidates[0])
            return
        }
        let candidate = candidates[index]
        guard let url = URL(string: "\(candidate)/health") else {
            test(at: index + 1, completion: completion)
            return
        }
        var request = URLRequest(url: url, timeoutInterval: candidate.contains("192.168") ? 3 : 8)
        request.httpMethod = "GET"
        URLSession.shared.dataTask(with: request) { _, response, _ in
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            DispatchQueue.main.async {
                if code == 200 {
                    completion(candidate)
                } else {
                    test(at: index + 1, completion: completion)
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

private final class K1L0AuthGateStore: ObservableObject {
    static let shared = K1L0AuthGateStore()

    @Published var userId = ""
    @Published var displayName = ""
    @Published var email = ""
    @Published var isAuthenticated = false
    @Published var status = "sign in to sync your identity, transmissions, and items."

    private init() {
        loadCached()
    }

    func loadCached() {
        let defaults = UserDefaults.standard
        userId = K1L0NativeAPI.currentUserId() ?? ""
        displayName = defaults.string(forKey: "FirebaseDisplayName")?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        email = defaults.string(forKey: "FirebaseEmail")?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        isAuthenticated = !userId.isEmpty
        status = isAuthenticated ? "signed in." : "sign in to sync your identity, transmissions, and items."
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
private func UnitySendMessage(_ objectName: UnsafePointer<CChar>, _ methodName: UnsafePointer<CChar>, _ message: UnsafePointer<CChar>)
#else
public typealias K1L0UnityMessageCallback = @convention(c) (UnsafePointer<CChar>?, UnsafePointer<CChar>?, UnsafePointer<CChar>?) -> Void
private var k1l0UnityCallback: K1L0UnityMessageCallback?

@_cdecl("K1L0SetUnityCallback")
public func K1L0SetUnityCallback(_ callback: K1L0UnityMessageCallback?) {
    k1l0UnityCallback = callback
}

private func UnitySendMessage(_ objectName: UnsafePointer<CChar>, _ methodName: UnsafePointer<CChar>, _ message: UnsafePointer<CChar>) {
    k1l0UnityCallback?(objectName, methodName, message)
}
#endif

@_cdecl("K1L0InstallWeatherOverlay")
public func K1L0InstallWeatherOverlay() {
    DispatchQueue.main.async {
        K1L0NativeSettingsDefaults.register()
        K1L0SkyVideoURLResolver.fetchConfig()
        K1L0WeatherOverlayInstaller.install()
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

@_cdecl("K1L0DeliverPerfStats")
public func K1L0DeliverPerfStats(_ jsonPtr: UnsafePointer<CChar>?) {
    guard let jsonPtr else { return }
    let json = String(cString: jsonPtr)
    DispatchQueue.main.async {
        K1L0PerfStatsStore.shared.handle(json)
    }
}

private final class K1L0PerfStatsStore: NSObject, ObservableObject {
    static let shared = K1L0PerfStatsStore()

    @Published private(set) var fps: Double = 0
    @Published private(set) var frameMs: Double = 0
    @Published private(set) var allocMB: Int = 0
    @Published private(set) var reservedMB: Int = 0
    @Published private(set) var thermal: String = "..."
    @Published private(set) var batteryPct: Double = -1
    @Published private(set) var batteryDrainPctPerHour: Double = 0
    @Published private(set) var processCpuPct: Double = 0
    @Published private(set) var videoPlaybackActive = false
    @Published private(set) var renderDebug: [String: Any] = [:]
    @Published private(set) var updatedAt: Date?

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
        link.add(to: .main, forMode: .common)
        displayLink = link
#endif
    }

    func handle(_ json: String) {
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

        let sampledFps = Double(nativeFrameCount) / elapsed
        fps = sampledFps
        frameMs = sampledFps > 0 ? 1000.0 / sampledFps : 0
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
        nativeFrameCount = 0
        nativeLastSampleTime = now
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
    var allowsResponse: Bool = false
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

private final class K1L0UserMetadataSaveStore: ObservableObject {
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

private struct K1L0ActiveTransmissionSnapshot: Codable {
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

private func normalizedTransmissionOptions(_ options: [String], includeFallback: Bool = false) -> [String] {
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
    // An explicit empty array means this viewer is the origin of the thread:
    // they type the next raw transmission instead of choosing a canned reply.
    if cleaned.isEmpty { return [] }
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

private func k1l0NumericTimestamp(_ value: Any?) -> Double {
    if let value = value as? Double { return value }
    if let value = value as? Int { return Double(value) }
    if let value = value as? Int64 { return Double(value) }
    if let value = value as? String { return Double(value.trimmingCharacters(in: .whitespacesAndNewlines)) ?? 0 }
    return 0
}

private func k1l0ReadableDateTime(_ raw: Double?) -> String {
    guard let raw, raw > 0 else { return "" }
    let seconds = raw > 9_999_999_999 ? raw / 1000.0 : raw
    let formatter = DateFormatter()
    formatter.dateStyle = .medium
    formatter.timeStyle = .short
    return formatter.string(from: Date(timeIntervalSince1970: seconds))
}

private final class K1L0ActiveTransmissionStore: ObservableObject {
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

private final class K1L0RadioPlayer: ObservableObject {
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
    private var mode = "final"

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

    private func configureAudioSession() {
#if os(iOS)
        do {
            try AVAudioSession.sharedInstance().setCategory(.playback, mode: .default, options: [])
            try AVAudioSession.sharedInstance().setActive(true)
        } catch {
            status = "audio session failed"
            print("[K1L0Radio] audio session failed: \(error.localizedDescription)")
        }
#endif
    }
}

private final class K1L0WeatherOverlayInstaller {
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

    static func applyNativeWorldNearby(_ json: String) {
        guard !json.isEmpty else { return }
        let wasPaused = unityPlaybackPaused
        if wasPaused { K1L0UnityPause(0) }
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
                    K1L0UnityPause(1)
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

    static func playBeamCollectSound() {
        "K1L0HUD".withCString { objectName in
            "PlayNativeBeamCollectSound".withCString { methodName in
                "".withCString { message in
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


private enum NativeUnityLightingSync {
    static func sync() {
        let defaults = UserDefaults.standard
        let moonlightEnabled = defaults.object(forKey: "k1lo_native_moonlightEnabled") as? Bool ?? true
        let moonlightManualOverride = defaults.object(forKey: "k1lo_native_moonlightManualOverride") as? Bool ?? false
        let moonlightIntensity = defaults.object(forKey: "k1lo_native_moonlightIntensity") as? Double ?? 1.0
        let moonlightRed = defaults.object(forKey: "k1lo_native_moonlightRed") as? Double ?? 0.7
        let moonlightGreen = defaults.object(forKey: "k1lo_native_moonlightGreen") as? Double ?? 0.8
        let moonlightBlue = defaults.object(forKey: "k1lo_native_moonlightBlue") as? Double ?? 1.0
        let moonlightPitch = defaults.object(forKey: "k1lo_native_moonlightPitch") as? Double ?? 90.0
        let moonlightYaw = defaults.object(forKey: "k1lo_native_moonlightYaw") as? Double ?? 0.0
        let moonlightRoll = defaults.object(forKey: "k1lo_native_moonlightRoll") as? Double ?? 0.0
        let ambientEnabled = defaults.object(forKey: "k1lo_native_ambientEnabled") as? Bool ?? true
        let ambientIntensity = defaults.object(forKey: "k1lo_native_ambientIntensity") as? Double ?? 1.55
        var spotlightEnabled = defaults.object(forKey: "k1lo_native_spotlightEnabled") as? Bool ?? true
        var spotlightIntensity = defaults.object(forKey: "k1lo_native_spotlightIntensity") as? Double ?? 3.0

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
        let groundHueVal = defaults.object(forKey: "k1lo_native_groundHue") as? Double ?? 0.33
        let groundSatVal = defaults.object(forKey: "k1lo_native_groundSaturation") as? Double ?? 0.42
        K1L0WindowGlowResolver.apply()
        K1L0WeatherOverlayInstaller.setUnitySetting("groundHue", String(format: "%.3f", groundHueVal))
        K1L0WeatherOverlayInstaller.setUnitySetting("groundSaturation", String(format: "%.3f", groundSatVal))

        // Color grade + horizon fog — push the saved (or freshly migrated
        // "dystopian daylight") values so Unity's PlayerPrefs copy can't keep
        // an older look alive across app updates.
        let gradeKeys: [(unity: String, store: String, fallback: Double)] = [
            ("saturation", "k1lo_native_saturation", -28.0),
            ("contrast", "k1lo_native_contrast", 14.0),
            ("mapBrightness", "k1lo_native_mapBrightness", -0.12),
            ("hueShift", "k1lo_native_hueShift", -4.0),
            ("temperature", "k1lo_native_temperature", -12.0),
            ("tint", "k1lo_native_tint", -6.0),
            ("vignetteIntensity", "k1lo_native_vignetteIntensity", 0.45),
            ("chromaticIntensity", "k1lo_native_chromaticIntensity", 0.16),
            ("filmGrainIntensity", "k1lo_native_filmGrainIntensity", 0.4),
            ("fogDistantDensity", "k1lo_native_fogDistantDensity", 0.3),
            ("fogDistantStart", "k1lo_native_fogDistantStart", 400.0),
            ("fogBrightness", "k1lo_native_fogBrightness", 0.55),
        ]
        for entry in gradeKeys {
            let value = defaults.object(forKey: entry.store) as? Double ?? entry.fallback
            K1L0WeatherOverlayInstaller.setUnitySetting(entry.unity, String(format: "%.3f", value))
        }
        let grainOn = defaults.object(forKey: "k1lo_native_filmGrainEnabled") as? Bool ?? true
        let distantFogOn = defaults.object(forKey: "k1lo_native_fogDistantFog") as? Bool ?? true
        K1L0WeatherOverlayInstaller.setUnitySetting("filmGrainEnabled", grainOn ? "1" : "0")
        K1L0WeatherOverlayInstaller.setUnitySetting("fogDistantFog", distantFogOn ? "1" : "0")

        // Sky Target FPS
        let skyTargetFps = defaults.object(forKey: "k1lo_native_skyTargetFps") as? Double ?? 30.0
        K1L0WeatherOverlayInstaller.setUnitySetting("skyTargetFps", String(format: "%.3f", skyTargetFps))

        // Transmission Fizzy Edges
        let transmissionFizzyEdgesVal = defaults.object(forKey: "k1lo_native_transmissionFizzyEdges") as? Bool ?? false
        K1L0WeatherOverlayInstaller.setUnitySetting("transmissionFizzyEdges", transmissionFizzyEdgesVal ? "1" : "0")

        // Night Fog & Ground values
        let fogDensityNight = defaults.object(forKey: "k1lo_native_fogDensity_night") as? Double ?? 0.37
        let fogNoiseStrengthNight = defaults.object(forKey: "k1lo_native_fogNoiseStrength_night") as? Double ?? 1.67
        let fogNoiseScaleNight = defaults.object(forKey: "k1lo_native_fogNoiseScale_night") as? Double ?? 17.4
        let fogBrightnessNight = defaults.object(forKey: "k1lo_native_fogBrightness_night") as? Double ?? 0.34
        let fogScatteringIntensityNight = defaults.object(forKey: "k1lo_native_fogScatteringIntensity_night") as? Double ?? 1.15
        let fogHeightNight = defaults.object(forKey: "k1lo_native_fogHeight_night") as? Double ?? 77.0
        let fogDistantDensityNight = defaults.object(forKey: "k1lo_native_fogDistantDensity_night") as? Double ?? 0.0
        let fogDistantStartNight = defaults.object(forKey: "k1lo_native_fogDistantStart_night") as? Double ?? 0.0
        let groundHueNight = defaults.object(forKey: "k1lo_native_groundHue_night") as? Double ?? 0.30
        let groundSaturationNight = defaults.object(forKey: "k1lo_native_groundSaturation_night") as? Double ?? 0.0

        K1L0WeatherOverlayInstaller.setUnitySetting("fogDensity_night", String(format: "%.3f", fogDensityNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("fogNoiseStrength_night", String(format: "%.3f", fogNoiseStrengthNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("fogNoiseScale_night", String(format: "%.3f", fogNoiseScaleNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("fogBrightness_night", String(format: "%.3f", fogBrightnessNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("fogScatteringIntensity_night", String(format: "%.3f", fogScatteringIntensityNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("fogHeight_night", String(format: "%.3f", fogHeightNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("fogDistantDensity_night", String(format: "%.3f", fogDistantDensityNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("fogDistantStart_night", String(format: "%.3f", fogDistantStartNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("groundHue_night", String(format: "%.3f", groundHueNight))
        K1L0WeatherOverlayInstaller.setUnitySetting("groundSaturation_night", String(format: "%.3f", groundSaturationNight))

        let manualWeather = defaults.object(forKey: "k1lo_native_manualWeather") as? Int ?? 0
        let manualHour = defaults.object(forKey: "k1lo_native_manualHour") as? Double ?? 13.25
        K1L0WeatherOverlayInstaller.setUnitySetting("testSkyOverride", K1L0SkyVideoURLResolver.testOverrideEnabled ? "1" : "0")
        K1L0SkyVideoURLResolver.applyManualSkyVideoIfTesting(manualWeatherIndex: manualWeather, manualHour: manualHour)
    }
}

private struct K1L0LoginPermissionGate: View {
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
                        body: auth.isAuthenticated ? (auth.displayName.isEmpty ? "identity is ready." : auth.displayName) : "sync your avatar, transmissions, items, and profile across devices.",
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

private struct K1L0WeatherOverlayRoot: View {
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
    @State private var liveDropLimit = 5
    @State private var homeLocationsExpanded = false
    @State private var homeNearbyUsersExpanded = false
    @State private var selectedNearbyUser: OverlayUser?
    @State private var selectedInventoryItem: OverlayInventoryItem?
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
    @AppStorage("k1lo_native_musicRadioMode") private var musicRadioMode = "final"
    @AppStorage("k1lo_native_bottomMenuLayout") private var bottomMenuLayout = "tabs"
    @Environment(\.scenePhase) private var scenePhase

    private var isVideoTransmissionPlaying: Bool {
        guard let result = transmissionResults.current else { return false }
        return result.videoURL != nil || !result.clips.filter { $0.videoURL != nil }.isEmpty
    }

    private var anyPanelOpen: Bool {
        hudVisible || showingSettings || showingTransmission || showingUserEditor || showingMessages || transmissionResults.current != nil
    }

    private var bottomCloseVisible: Bool {
        showingTransmission || transmissionResults.current != nil
    }

    private var skyModePanelOpen: Bool {
        hudVisible || showingTransmission || showingUserEditor || showingMessages || transmissionResults.current != nil
    }

    private var radioSuppressed: Bool {
        transmissionResults.current != nil
            || (showingTransmission && activeTransmission.snapshot.active && !activeTransmission.snapshot.videoUrl.isEmpty)
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

            if appHudReady && !skyModePanelOpen {
                IncomingSignalSkyOverlay(data: data)
                    .ignoresSafeArea()
                    .allowsHitTesting(false)
                    .zIndex(1)
            }
            if appHudReady && !skyModePanelOpen && !showingSettings && data.incomingTransmission == nil {
                WalkingSkyAlert(
                    text: data.walkingSkyAlertText,
                    stableText: data.walkingSkyAlertStableText,
                    distanceText: data.walkingSkyAlertBeam.map { data.distanceText(to: $0) },
                    relativeBearing: data.walkingSkyAlertBeam.map { data.relativeBearingDegrees(to: $0) },
                    dotPhase: data.searchDotPhase
                )
                .ignoresSafeArea()
                .allowsHitTesting(false)
                .zIndex(2)
            }

            if appHudReady {
                VStack(spacing: 8) {
                    if !showingMessages && !showingTransmission && !showingUserEditor {
                        FixedTopStatusHUD(data: data, settingsActive: showingSettings, hideSteps: hudVisible, onSettingsTapped: toggleSettings)
                            .padding(.horizontal, 18)
                            .padding(.top, topStatusPadding)
                    }
                    if !skyModePanelOpen && !showingSettings {
                        IncomingSignalHUD(data: data)
                            .padding(.horizontal, 18)
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

                                WorldMarqueeCard(items: data.homeMarqueeItems())

                                WeatherGlassCard {
                                    VStack(alignment: .leading, spacing: 12) {
                                        Text("Live Drops")
                                            .font(.system(size: 25, weight: .bold))
                                        DropFilterBar(selected: $selectedDropFilter)

                                        let visiblePlaces = data.filteredPlaces(for: selectedDropFilter)
                                        let displayedPlaces = homeLocationsExpanded ? visiblePlaces : Array(visiblePlaces.prefix(4))
                                        ForEach(displayedPlaces) { place in
                                            HStack(spacing: 10) {
                                                DirectionCell(
                                                    distance: data.distanceText(to: place),
                                                    relativeBearing: data.relativeBearingDegrees(to: place)
                                                )
                                                VStack(alignment: .leading, spacing: 2) {
                                                    Text("\(data.emoji(for: place)) \(place.name)")
                                                        .font(.system(size: 16, weight: .semibold))
                                                        .lineLimit(1)
                                                    if let teaser = place.bylineTeaser {
                                                        Text(teaser)
                                                            .font(.system(size: 12, weight: .semibold))
                                                            .foregroundStyle(.white.opacity(0.64))
                                                            .lineLimit(1)
                                                    }
                                                }
                                                Spacer()
                                                Image(systemName: "questionmark.diamond.fill")
                                                    .font(.system(size: 15, weight: .bold))
                                                    .foregroundStyle(.white.opacity(0.86))
                                                    .frame(minWidth: 32, alignment: .trailing)
                                            }
                                            .padding(.top, 2)
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
                                                        Text(user.nameAndCallsign)
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
                                        Text("Items")
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
                                                                selectedInventoryItem = item
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
                                            Text("Walking Leaderboard")
                                                .font(.system(size: 25, weight: .bold))
                                            Spacer()
                                            Text(data.stepLeaderboardStatus)
                                                .font(.system(size: 10, weight: .bold))
                                                .foregroundStyle(.white.opacity(0.55))
                                        }

                                        StepLeaderboardSection(title: "PAST 24 HOURS", leaders: data.stepLeaders24h, useWeeklyTotal: false) { user in
                                            withAnimation(.spring(response: 0.32, dampingFraction: 0.88)) {
                                                selectedNearbyUser = user
                                            }
                                        }
                                        StepLeaderboardSection(title: "PAST 7 DAYS", leaders: data.stepLeaders7d, useWeeklyTotal: true) { user in
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
                NativeUserEditorPanel(tabsMode: bottomMenuLayout == "tabs") {
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
               data.incomingTransmission == nil,
               transmissionResults.current == nil,
               data.collectCandidateBeam == nil,
               let place = data.collectCandidatePlace {
                LocationItemCollectPrompt(
                    place: place,
                    distanceText: data.distanceText(to: place),
                    relativeBearing: data.relativeBearingDegrees(to: place),
                    onCollect: { data.confirmCollectPlace(place) },
                    onDismiss: { data.dismissLocationCollectPrompt() }
                )
                .transition(.scale(scale: 0.96).combined(with: .opacity))
                .zIndex(36)
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

            if appHudReady, let selectedItem = selectedInventoryItem {
                InventoryItemDetailCard(
                    item: selectedItem,
                    onDismiss: {
                        withAnimation(.spring(response: 0.32, dampingFraction: 0.88)) {
                            selectedInventoryItem = nil
                        }
                    }
                )
                .transition(.move(edge: .bottom).combined(with: .opacity))
                .zIndex(80)
            }

            // Persistent floating controls. Home/map toggle on the lower-left
            // (swaps icon based on whether the home HUD is up); user shortcut
            // on the lower-right. The transmit button stays centered between
            // them. No more X — the toggle button doubles as the close.
            // Hide the whole bar while a transmission/chain video is playing
            // (e.g. opened from Messages or the user screen) so it never
            // overlaps the playback panel.
            if appHudReady && !isVideoTransmissionPlaying {
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
                                            .font(.system(size: 21, weight: .bold))
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
            data.applyLocationFilter(selectedDropFilter)
        }
        .onChange(of: scenePhase) { phase in
            if phase == .active {
                data.refreshPermissionGateState()
                data.refreshTransmissionState(clearStaleCache: true)
                loadNewsWalkHistory()
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.8) {
                    K1L0RadioPlayer.shared.setSuppressed(radioSuppressed)
                    K1L0RadioPlayer.shared.resumeAfterForeground(apiBase: data.activeAPIBase)
                }
                NativeUnityLightingSync.sync()
            }
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
            if id != nil {
                K1L0WeatherOverlayInstaller.playBeamCollectSound()
            }
        }
        .animation((bottomMenuLayout == "tabs") ? .easeOut(duration: 0.12) : .spring(response: 0.34, dampingFraction: 0.88), value: showingTransmission)
        .animation((bottomMenuLayout == "tabs") ? .easeOut(duration: 0.12) : .spring(response: 0.34, dampingFraction: 0.88), value: showingUserEditor)
        .animation((bottomMenuLayout == "tabs") ? .easeOut(duration: 0.12) : .spring(response: 0.34, dampingFraction: 0.88), value: showingMessages)
        .overlay(alignment: .bottom) {
            if let result = transmissionResults.current {
                ZStack {
                    Color.black.ignoresSafeArea()
                    TransmissionResultPanel(result: result, onSelectOption: { option in
                        data.respondToTransmission(result, option: option)
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

private struct TransmissionResultPanel: View {
    let result: K1L0TransmissionResult
    let onSelectOption: (String) -> Void
    let onClose: () -> Void
    @State private var currentClipIndex = 0
    @State private var currentClipProgress = 0.0
    @State private var textTransform = TransmissionTextTransformStore.load()
    @State private var typedPlotTexts: Set<String> = []
    @State private var videoReadyForText = false
    @State private var showingSettings = false
    @State private var responseDraft = ""
    @State private var isSendingResponse = false
    @State private var showResponderCard = false

    private var currentClipSenderName: String {
        guard currentClipIndex >= 0, currentClipIndex < playableClips.count else { return "" }
        return playableClips[currentClipIndex].sourceName
    }

    // Prefer the live nearby-users record (helmet avatar, city) for the tapped
    // responder; fall back to a minimal card from the clip identity.
    private var responderCardUser: OverlayUser? {
        guard currentClipIndex >= 0, currentClipIndex < playableClips.count else { return nil }
        let clip = playableClips[currentClipIndex]
        guard !clip.sourceUserId.isEmpty else { return nil }
        if let known = K1L0OverlayDataModel.activeModel?.nearbyUsers.first(where: { $0.userId == clip.sourceUserId }) {
            return known
        }
        return OverlayUser(userId: clip.sourceUserId, name: clip.sourceName, callsign: nil,
                           avatarUrl: nil, helmetUrl: nil, faceUrl: nil, city: nil,
                           lat: nil, lng: nil, lastActive: nil)
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
            return overlayTextForClip(plot: clip.responsePlot, selectedResponse: clip.selectedResponse, isResponseClip: safeIndex > 0)
        }
        return overlayTextForClip(plot: result.responsePlot, selectedResponse: result.selectedResponse ?? "", isResponseClip: false)
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
            let buttonRowY = topSafe + 24
            let topReserve = topSafe + 50
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
            // choices grid + composer. composerHeight mirrors the rendered
            // composer (each choice row 28pt, 6pt row gaps, 8pt to the field,
            // 42pt field) so the plot text pins a fixed 12pt above its top.
            let composerRows = responseChoices.isEmpty ? 0 : Int(ceil(Double(responseChoices.count) / 2.0))
            // + 24 for the "please respond." caption row above the choices.
            let composerHeight: CGFloat = canRespond
                ? CGFloat(composerRows) * 28 + CGFloat(max(0, composerRows - 1)) * 6 + 8 + 42 + 24
                : 0
            let composerTopY = screenH - bottomSafe - composerHeight
            let plotBottomInset: CGFloat = canRespond
                ? max(18, videoRect.maxY - composerTopY + 12)
                : 66
            ZStack(alignment: .top) {
                Color.black.ignoresSafeArea()

                VStack(spacing: 12) {
                    ZStack {
                        Color.clear

                        if !playableClips.isEmpty {
                            InlineTransmissionVideoPlayer(clips: playableClips, currentClipIndex: $currentClipIndex, currentClipProgress: $currentClipProgress, isVideoReady: $videoReadyForText, holdAtEndIndex: responseClipIndex)
                                .frame(width: videoWidth, height: videoHeight)
                                .mask(TatteredEdgeMaskCanvas())
                        } else if let url = result.videoURL {
                            InlineTransmissionVideoPlayer(urlString: url.absoluteString, audioUrlString: result.audioURL?.absoluteString, currentClipProgress: $currentClipProgress, isVideoReady: $videoReadyForText)
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
                if canRespond, let responder = responderCardUser {
                    Button {
                        withAnimation(.spring(response: 0.3, dampingFraction: 0.9)) {
                            showResponderCard = true
                        }
                    } label: {
                        HStack(spacing: 8) {
                            K1L0UserAvatar(
                                urlString: responder.avatarDisplayUrl,
                                size: 36,
                                userId: responder.userId
                            )
                            Text(responder.displayName.uppercased())
                                .font(.system(size: 13, weight: .black, design: .rounded))
                                .foregroundStyle(.white)
                                .lineLimit(1)
                                .minimumScaleFactor(0.72)
                        }
                        .padding(.leading, 6)
                        .padding(.trailing, 11)
                        .padding(.vertical, 5)
                        .background(Color.black.opacity(0.58), in: Capsule())
                        .overlay(Capsule().stroke(Color.white.opacity(0.24), lineWidth: 1))
                    }
                    .buttonStyle(.plain)
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
                        .font(.system(size: 16, weight: .black))
                        .foregroundStyle(.white)
                        .frame(width: 44, height: 44)
                        .background(Color.black.opacity(0.38), in: Circle())
                }
                .buttonStyle(.plain)
                .position(x: geometry.size.width - 34, y: buttonRowY)

                Button(action: {
                    withAnimation {
                        showingSettings.toggle()
                    }
                }) {
                    Image(systemName: "gearshape")
                        .font(.system(size: 16, weight: .black))
                        .foregroundStyle(.white)
                        .frame(width: 44, height: 44)
                        .background(Color.black.opacity(0.38), in: Circle())
                }
                .buttonStyle(.plain)
                .position(x: geometry.size.width - 84, y: buttonRowY)

#if canImport(UIKit)
                if !saveMediaItems.isEmpty {
                    CameraRollSaveButton(mediaItems: saveMediaItems, iconOnly: true)
                        .position(x: geometry.size.width - 134, y: buttonRowY)
                }
#endif

                if canRespond {
                    VStack(spacing: 8) {
                        Text("please respond.")
                            .font(.system(size: 13, weight: .bold, design: .monospaced))
                            .foregroundStyle(.white.opacity(0.78))
                            .frame(maxWidth: .infinity)
                        if !responseChoices.isEmpty {
                            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 6) {
                                ForEach(responseChoices, id: \.self) { option in
                                    Button {
                                        guard !isSendingResponse else { return }
                                        responseDraft = option
                                        K1L0WeatherOverlayInstaller.playBeamCollectSound()
                                    } label: {
                                        Text(option.uppercased())
                                            .font(.system(size: 12, weight: .black, design: .rounded))
                                            .foregroundStyle(.white)
                                            .lineLimit(1)
                                            .minimumScaleFactor(0.72)
                                            .frame(maxWidth: .infinity, minHeight: 28)
                                            .padding(.horizontal, 8)
                                            .background(Color.black.opacity(0.50), in: RoundedRectangle(cornerRadius: 7, style: .continuous))
                                            .overlay(RoundedRectangle(cornerRadius: 7, style: .continuous).stroke(Color.white.opacity(0.18), lineWidth: 1))
                                    }
                                    .buttonStyle(.plain)
                                    .disabled(isSendingResponse)
                                    .opacity(isSendingResponse ? 0.45 : 1.0)
                                }
                            }
                        }

                        HStack(spacing: 8) {
                            TextField(isSendingResponse ? "sending..." : "RESPOND", text: $responseDraft)
                                .textInputAutocapitalization(.never)
                                .disableAutocorrection(true)
                                .font(.system(size: 15, weight: .semibold, design: .rounded))
                                .foregroundStyle(.white)
                                .padding(.horizontal, 12)
                                .frame(height: 42)
                                .background(Color.black.opacity(0.70), in: RoundedRectangle(cornerRadius: 8, style: .continuous))
                                .overlay(RoundedRectangle(cornerRadius: 8, style: .continuous).stroke(Color(red: 1.0, green: 0.19, blue: 0.58).opacity(0.9), lineWidth: 1.5))
                                .disabled(isSendingResponse)

                            Button {
                                let text = responseDraft.trimmingCharacters(in: .whitespacesAndNewlines)
                                guard !isSendingResponse, !text.isEmpty else { return }
                                isSendingResponse = true
                                responseDraft = ""
                                K1L0WeatherOverlayInstaller.playBeamCollectSound()
                                onSelectOption(text)
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
                    .padding(.horizontal, 12)
                    .padding(.bottom, bottomSafe)
                    .frame(width: geometry.size.width, height: screenH, alignment: .bottom)
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
            .onChange(of: transmissionFizzyEdges) { val in
                K1L0WeatherOverlayInstaller.setUnitySetting("transmissionFizzyEdges", val ? "1" : "0")
            }
            .onChange(of: transmissionFXEnabled) { val in
                K1L0WeatherOverlayInstaller.setUnitySetting("transmissionFX", val ? "1" : "0")
            }
            .onChange(of: transmissionFXIntensity) { val in
                K1L0WeatherOverlayInstaller.setUnitySetting("transmissionFXIntensity", String(format: "%.2f", val))
            }
        }
    }
}

// Real device safe-area insets, for views that ignore safe areas (their
// GeometryReader reports zero insets because the parent consumed them).
private func k1l0DeviceSafeAreaInsets() -> (top: CGFloat, bottom: CGFloat) {
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
private func k1l0DeviceScreenSize() -> CGSize {
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

private struct TransmissionTextTransform: Codable, Equatable {
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

private func k1l0AspectFitRect(container: CGSize, aspectRatio: CGFloat = 9.0 / 16.0) -> CGRect {
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

private enum TransmissionTextTransformStore {
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

private struct DraggableTransmissionTextOverlay: View {
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
private final class K1L0TuningStaticPlayer {
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

private struct IncomingSignalHUD: View {
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
                Text("\(incoming.senderLabel.uppercased()) IS TRYING TO CONTACT SOMEONE")
                    .font(.system(size: 12, weight: .black, design: .monospaced))
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
                HStack(alignment: .bottom, spacing: 8) {
                    TenBarSignalMeter(strength: progress)
                    Text("WALK")
                        .font(.system(size: 10, weight: .black, design: .monospaced))
                        .foregroundStyle(.white)
                        .padding(.bottom, 2)
                    Text(percentText)
                        .font(.system(size: 14, weight: .black, design: .monospaced))
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

private struct IncomingSignalThumbnailImage: View {
    let image: Image
    let progress: Double

    private var clamped: Double { min(1, max(0, progress)) }
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

private struct SignalTuningShape: Shape {
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

private struct IncomingSignalSkyOverlay: View {
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

private struct WalkingSkyAlert: View {
    let text: String
    var stableText: String? = nil
    var distanceText: String? = nil
    var relativeBearing: Double? = nil
    // 0...2 — how many trailing dots are lit. All three dots are ALWAYS
    // rendered (constant width, symmetric centering); animation is opacity
    // only, so the centered line never shifts or looks lopsided.
    var dotPhase: Int = 2

    var body: some View {
        GeometryReader { geometry in
            HStack(spacing: 8) {
                if let dist = distanceText, let bearing = relativeBearing {
                    DirectionCell(distance: dist, relativeBearing: bearing)
                }
                renderText()
            }
            .padding(.horizontal, 14)
            .padding(.vertical, 6)
            .background(Color.black.opacity(0.08))
            .clipShape(Capsule())
            .fixedSize(horizontal: false, vertical: true)
            .frame(maxWidth: geometry.size.width * 0.82)
            .position(x: geometry.size.width * 0.5, y: geometry.safeAreaInsets.top + 182)
        }
    }

    @ViewBuilder
    private func renderText() -> some View {
        let (baseText, hadDots) = splitText(text)
        if hadDots {
            (
                Text(baseText).foregroundColor(.white.opacity(0.88))
                + Text(".").foregroundColor(.white.opacity(dotPhase >= 0 ? 0.88 : 0.22))
                + Text(".").foregroundColor(.white.opacity(dotPhase >= 1 ? 0.88 : 0.22))
                + Text(".").foregroundColor(.white.opacity(dotPhase >= 2 ? 0.88 : 0.22))
            )
            .font(.system(size: 17, weight: .semibold))
            .tracking(0.6)
            .multilineTextAlignment(distanceText != nil ? .leading : .center)
            .lineLimit(nil)
        } else {
            Text(text)
                .font(.system(size: 17, weight: .semibold))
                .tracking(0.6)
                .foregroundStyle(.white.opacity(0.88))
                .multilineTextAlignment(distanceText != nil ? .leading : .center)
                .lineLimit(nil)
        }
    }

    // Strips any trailing dot/pad run (periods plus the figure/punctuation
    // spaces earlier fixes padded with) — the view re-adds exactly three
    // opacity-animated dots.
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

private struct IncomingSignalSkyImage: View {
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

private struct TenBarSignalMeter: View {
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

private struct WorldMarqueeCard: View {
    let items: [K1L0MarqueeItem]

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
                            Image(systemName: "figure.walk")
                                .font(.system(size: 18, weight: .bold))
                                .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                                .frame(width: 46)
                        }
                        VStack(alignment: .leading, spacing: 3) {
                            Text(item.line1)
                                .font(.system(size: item.kind == "status" ? 19 : 16, weight: .bold))
                                .foregroundStyle(.white)
                                .lineLimit(1)
                                .minimumScaleFactor(0.66)
                            Text(item.line2)
                                .font(.system(size: 12, weight: .semibold))
                                .foregroundStyle(.white.opacity(0.72))
                                .lineLimit(2)
                                .minimumScaleFactor(0.64)
                        }
                        Spacer()
                    }
                }
            }
        }
    }
}

private struct K1L0TabbedBottomMenu: View {
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
            K1L0UserAvatar(urlString: helmetUrl.isEmpty ? nil : helmetUrl, size: 28)
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

private struct NativeLocationPreset: Identifiable {
    let id: String
    let title: String
    let subtitle: String
    let latitude: Double
    let longitude: Double

    static let storageKey = "k1lo_native_locationMode"
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
    static let pointState = NativeLocationPreset(
        id: "point_state_park",
        title: "Point St Park",
        subtitle: "Downtown Pittsburgh",
        latitude: 40.4417359,
        longitude: -80.0119979
    )

    static let all = [domino, hernandez, pointState]

    static func preset(for id: String) -> NativeLocationPreset? {
        all.first { $0.id == id }
    }
}

private struct SettingsSectionInfo: Identifiable {
    let id: String
    let title: String
}

private struct NativeSettingsPanel: View {
    let apiBase: String?
    let onClose: () -> Void
    @ObservedObject private var radio = K1L0RadioPlayer.shared
    @ObservedObject private var perfStats = K1L0PerfStatsStore.shared

    @AppStorage("k1lo_native_saturation") private var saturation = 2.0
    @AppStorage("k1lo_native_contrast") private var contrast = 4.0
    @AppStorage("k1lo_native_mapBrightness") private var mapBrightness = -0.05
    @AppStorage("k1lo_native_hueShift") private var hueShift = -4.0
    @AppStorage("k1lo_native_temperature") private var temperature = 2.0
    @AppStorage("k1lo_native_tint") private var tint = 1.0
    @AppStorage("k1lo_native_bloomEnabled") private var bloomEnabled = true
    @AppStorage("k1lo_native_bloomIntensity") private var bloomIntensity = 2.4
    @AppStorage("k1lo_native_bloomThreshold") private var bloomThreshold = 1.2
    @AppStorage("k1lo_native_bloomScatter") private var bloomScatter = 0.43
    @AppStorage("k1lo_native_vignetteEnabled") private var vignetteEnabled = true
    @AppStorage("k1lo_native_vignetteIntensity") private var vignetteIntensity = 0.3
    @AppStorage("k1lo_native_vignetteSmoothness") private var vignetteSmoothness = 1.0
    @AppStorage("k1lo_native_chromaticEnabled") private var chromaticEnabled = true
    @AppStorage("k1lo_native_chromaticIntensity") private var chromaticIntensity = 0.09
    @AppStorage("k1lo_native_lensDistEnabled") private var lensDistEnabled = true
    @AppStorage("k1lo_native_lensDistIntensity") private var lensDistIntensity = -0.5
    @AppStorage("k1lo_native_dofEnabled") private var dofEnabled = false
    @AppStorage("k1lo_native_focusDistance") private var focusDistance = 18.1
    @AppStorage("k1lo_native_aperture") private var aperture = 8.25
    @AppStorage("k1lo_native_focalLength") private var focalLength = 119.0
    @AppStorage("k1lo_native_motionBlurEnabled") private var motionBlurEnabled = false
    @AppStorage("k1lo_native_motionBlurIntensity") private var motionBlurIntensity = 0.02
    @AppStorage("k1lo_native_filmGrainEnabled") private var filmGrainEnabled = true
    @AppStorage("k1lo_native_filmGrainIntensity") private var filmGrainIntensity = 0.0
    @AppStorage("k1lo_native_godPositionY") private var godPositionY = 51.0
    @AppStorage("k1lo_native_godPositionZ") private var godPositionZ = 107.0
    @AppStorage("k1lo_native_godRotationX") private var godRotationX = -1.0
    @AppStorage("k1lo_native_farClipPlane") private var farClipPlane = 3600.0
    @AppStorage("k1lo_native_moonlightEnabled") private var moonlightEnabled = true
    @AppStorage("k1lo_native_moonlightManualOverride") private var moonlightManualOverride = false
    @AppStorage("k1lo_native_moonlightIntensity") private var moonlightIntensity = 1.0
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
    @AppStorage("k1lo_native_zossEmissiveIntensity") private var zossEmissiveIntensity = 1.9
    @AppStorage("k1lo_native_zossEmissiveSmoothness") private var zossEmissiveSmoothness = 0.34
    @AppStorage("k1lo_native_zossEmissiveMetallic") private var zossEmissiveMetallic = 0.05
    @AppStorage("k1lo_native_zossEmissiveHue") private var zossEmissiveHue = 0.07
    @AppStorage("k1lo_native_zossEmissiveSaturation") private var zossEmissiveSaturation = 0.62
    @AppStorage("k1lo_native_zossNightEmissiveHue") private var zossNightEmissiveHue = 0.115
    @AppStorage("k1lo_native_zossNightEmissiveSaturation") private var zossNightEmissiveSaturation = 0.82
    @AppStorage("k1lo_native_groundHue") private var groundHue = 0.30
    @AppStorage("k1lo_native_groundSaturation") private var groundSaturation = 0.0
    @AppStorage("k1lo_native_beamDistanceLabels") private var beamDistanceLabels = false
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
    @AppStorage("k1lo_native_manualHour") private var manualHour = 13.25
    @AppStorage("k1lo_native_manualWeather") private var manualWeather = 0
    @AppStorage("k1lo_native_ambientMinStepsToSpawn") private var ambientMinStepsToSpawn = 110.0
    @AppStorage("k1lo_native_receiveStepsRequired") private var receiveStepsRequired = 200.0
    @AppStorage("k1lo_native_transmissionWaitSteps") private var transmissionWaitSteps = 500.0
    @AppStorage("k1lo_native_momentumSessionGraceMinutes") private var momentumSessionGraceMinutes = 20.0
    @AppStorage("k1lo_native_ambientBeamTtlMinutes") private var ambientBeamTtlMinutes = 30.0
    @AppStorage("k1lo_native_ambientCollectRadiusMeters") private var ambientCollectRadiusMeters = 16.0
    @AppStorage("k1lo_native_locationCollectRadiusFeet") private var locationCollectRadiusFeet = 50.0
    @AppStorage("k1lo_native_ambientBeamDismissSteps") private var ambientBeamDismissSteps = 80.0
    @AppStorage("k1lo_native_musicRadioEnabled") private var musicRadioEnabled = true
    @AppStorage("k1lo_native_musicRadioVolume") private var musicRadioVolume = 0.5415074229240417
    @AppStorage("k1lo_native_musicRadioMode") private var musicRadioMode = "final"
    @AppStorage("k1lo_native_fogConstantDensity") private var fogConstantDensity = false
    @AppStorage("k1lo_native_fogDensity") private var fogDensity = 0.37
    @AppStorage("k1lo_native_fogNoiseStrength") private var fogNoiseStrength = 1.67
    @AppStorage("k1lo_native_fogNoiseScale") private var fogNoiseScale = 17.4
    @AppStorage("k1lo_native_fogBrightness") private var fogBrightness = 0.34
    @AppStorage("k1lo_native_fogScatteringIntensity") private var fogScatteringIntensity = 1.15
    @AppStorage("k1lo_native_fogHeight") private var fogHeight = 77.0
    @AppStorage("k1lo_native_fogDistantFog") private var fogDistantFog = true
    @AppStorage("k1lo_native_fogDistantDensity") private var fogDistantDensity = 0.0
    @AppStorage("k1lo_native_fogDistantStart") private var fogDistantStart = 0.0
    @AppStorage("k1lo_native_fogNativeLights") private var fogNativeLights = false
    @AppStorage("k1lo_native_fogNativeLightsMultiplier") private var fogNativeLightsMultiplier = 0.0
    @AppStorage(NativeLocationPreset.storageKey) private var locationMode = NativeLocationPreset.liveId
    @AppStorage("k1lo_native_skyTargetFps") private var skyTargetFps = 30.0
    @AppStorage("k1lo_native_experimentalLayeredSky") private var experimentalLayeredSky = false
    @AppStorage("k1lo_native_layeredSkyTopHue") private var layeredSkyTopHue = 0.62
    @AppStorage("k1lo_native_layeredSkyHorizonHue") private var layeredSkyHorizonHue = 0.94
    @AppStorage("k1lo_native_layeredCloudOpacity") private var layeredCloudOpacity = 0.72
    @AppStorage("k1lo_native_layeredCloudSpeed") private var layeredCloudSpeed = 0.08
    @AppStorage("k1lo_native_layeredCloudScale") private var layeredCloudScale = 2.2
    @AppStorage("k1lo_native_layeredCloudContrast") private var layeredCloudContrast = 1.5
    @AppStorage("k1lo_native_fogDensity_night") private var fogDensityNight = 0.37
    @AppStorage("k1lo_native_fogNoiseStrength_night") private var fogNoiseStrengthNight = 1.67
    @AppStorage("k1lo_native_fogNoiseScale_night") private var fogNoiseScaleNight = 17.4
    @AppStorage("k1lo_native_fogBrightness_night") private var fogBrightnessNight = 0.34
    @AppStorage("k1lo_native_fogScatteringIntensity_night") private var fogScatteringIntensityNight = 1.15
    @AppStorage("k1lo_native_fogHeight_night") private var fogHeightNight = 77.0
    @AppStorage("k1lo_native_fogDistantDensity_night") private var fogDistantDensityNight = 0.0
    @AppStorage("k1lo_native_fogDistantStart_night") private var fogDistantStartNight = 0.0
    @AppStorage("k1lo_native_groundHue_night") private var groundHueNight = 0.30
    @AppStorage("k1lo_native_groundSaturation_night") private var groundSaturationNight = 0.0
    @State private var fogTuningMode = "Day"
    @State private var groundTuningMode = "Day"
    @AppStorage("k1lo_native_selectedSettingsSection") private var selectedSection = "Menu"

    private let sectionsList = [
        SettingsSectionInfo(id: "Performance", title: "Perf"),
        SettingsSectionInfo(id: "Location", title: "Loc"),
        SettingsSectionInfo(id: "HUD", title: "HUD"),
        SettingsSectionInfo(id: "God Camera", title: "God Cam"),
        SettingsSectionInfo(id: "Lighting", title: "Light"),
        SettingsSectionInfo(id: "Fog", title: "Fog"),
        SettingsSectionInfo(id: "Window Glow", title: "Windows"),
        SettingsSectionInfo(id: "Ground / Grass", title: "Ground"),
        SettingsSectionInfo(id: "Map Color", title: "Map"),
        SettingsSectionInfo(id: "Bloom", title: "Bloom"),
        SettingsSectionInfo(id: "Post FX", title: "Post FX"),
        SettingsSectionInfo(id: "Focus + Motion", title: "Focus"),
        SettingsSectionInfo(id: "Music", title: "Music"),
        SettingsSectionInfo(id: "Timers", title: "Timers"),
        SettingsSectionInfo(id: "Weather", title: "Weather"),
        SettingsSectionInfo(id: "Layered Sky", title: "Sky Lab"),
        SettingsSectionInfo(id: "Transmission FX", title: "Tx FX")
    ]

    var body: some View {
        GeometryReader { geometry in
            let topClearance = geometry.safeAreaInsets.top + 78
            VStack(spacing: 0) {
                Spacer()
                    .frame(height: topClearance)
                
                VStack(alignment: .leading, spacing: 10) {
                    Button {
                        K1L0WeatherOverlayInstaller.captureSnapshotForAnalysis()
                    } label: {
                        HStack {
                            Text("Snapshot")
                                .font(.system(size: 15, weight: .bold))
                            Spacer()
                            Text("analyze")
                                .font(.system(size: 12, weight: .semibold, design: .monospaced))
                                .foregroundStyle(.white.opacity(0.68))
                        }
                        .foregroundStyle(.white)
                        .padding(.vertical, 10)
                        .padding(.horizontal, 14)
                        .background(Color.white.opacity(0.10), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
                    }
                    .buttonStyle(.plain)

                    // Multirow Segment Controller
                    let columns = Array(repeating: GridItem(.flexible(), spacing: 4), count: 4)
                    LazyVGrid(columns: columns, spacing: 4) {
                        ForEach(sectionsList) { sec in
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

                ScrollView(.vertical, showsIndicators: true) {
                    VStack(alignment: .leading, spacing: 14) {
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
                        }
                    }

                    if selectedSection == "HUD" {
                        SettingsSection(title: "HUD", resetAction: {
                            bottomMenuLayout = "tabs"
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
                        }
                    }

                    if selectedSection == "God Camera" {
                        SettingsSection(title: "God Camera", resetAction: {
                            godPositionY = 51.0
                            godPositionZ = 107.0
                            godRotationX = -1.0
                            farClipPlane = 3600.0
                            K1L0WeatherOverlayInstaller.setUnitySetting("godPositionY", "51.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("godPositionZ", "107.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("godRotationX", "-1.000")
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
                            moonlightIntensity = 1.0
                            moonlightRed = 0.7
                            moonlightGreen = 0.8
                            moonlightBlue = 1.0
                            moonlightPitch = 90.0
                            moonlightYaw = 0.0
                            moonlightRoll = 0.0
                            ambientEnabled = true
                            ambientIntensity = 1.55
                            spotlightEnabled = true
                            spotlightIntensity = 3.0

                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightEnabled", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightManualOverride", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightIntensity", "1.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightRed", "0.700")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightGreen", "0.800")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightBlue", "1.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightPitch", "90.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightYaw", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("moonlightRoll", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("ambientEnabled", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("ambientIntensity", "1.550")
                            K1L0WeatherOverlayInstaller.setUnitySetting("spotlightEnabled", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("spotlightIntensity", "3.000")
                            syncLightingSettings()
                        }) {
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

                    if selectedSection == "Fog" {
                        SettingsSection(title: "Fog", resetAction: {
                            fogConstantDensity = false
                            fogDensity = 0.37
                            fogNoiseStrength = 1.67
                            fogNoiseScale = 17.4
                            fogBrightness = 0.34
                            fogScatteringIntensity = 1.15
                            fogHeight = 77.0
                            fogDistantFog = true
                            fogDistantDensity = 0.0
                            fogDistantStart = 0.0
                            fogDensityNight = 0.37
                            fogNoiseStrengthNight = 1.67
                            fogNoiseScaleNight = 17.4
                            fogBrightnessNight = 0.34
                            fogScatteringIntensityNight = 1.15
                            fogHeightNight = 77.0
                            fogDistantDensityNight = 0.0
                            fogDistantStartNight = 0.0
                            fogNativeLights = false
                            fogNativeLightsMultiplier = 0.0

                            K1L0WeatherOverlayInstaller.setUnitySetting("fogConstantDensity", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogDensity", "0.370")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogNoiseStrength", "1.670")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogNoiseScale", "17.400")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogBrightness", "0.340")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogScatteringIntensity", "1.150")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogHeight", "77.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogDistantFog", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogDistantDensity", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogDistantStart", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogDensity_night", "0.370")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogNoiseStrength_night", "1.670")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogNoiseScale_night", "17.400")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogBrightness_night", "0.340")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogScatteringIntensity_night", "1.150")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogHeight_night", "77.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogDistantDensity_night", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogDistantStart_night", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogNativeLights", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("fogNativeLightsMultiplier", "0.000")
                        }) {
                            SettingsSegmentedRow(
                                items: [("Day Tuning", "Day"), ("Night Tuning", "Night")],
                                selection: $fogTuningMode
                            )
                            .padding(.bottom, 8)

                            SettingToggleRow(title: "Constant Density", value: $fogConstantDensity, key: "fogConstantDensity")
                            
                            if fogTuningMode == "Day" {
                                SettingSliderRow(title: "Density (Day)", value: $fogDensity, range: 0...3, step: 0.01, key: "fogDensity")
                                SettingSliderRow(title: "Noise Strength (Day)", value: $fogNoiseStrength, range: 0...3, step: 0.01, key: "fogNoiseStrength")
                                SettingSliderRow(title: "Noise Scale (Day)", value: $fogNoiseScale, range: 0.1...80, step: 0.1, key: "fogNoiseScale")
                                SettingSliderRow(title: "Brightness (Day)", value: $fogBrightness, range: 0...2, step: 0.01, key: "fogBrightness")
                                SettingSliderRow(title: "Scattering (Day)", value: $fogScatteringIntensity, range: 0...4, step: 0.01, key: "fogScatteringIntensity")
                                SettingSliderRow(title: "Height (Day)", value: $fogHeight, range: 0...500, step: 1, key: "fogHeight")
                                SettingToggleRow(title: "Distant Fog", value: $fogDistantFog, key: "fogDistantFog")
                                SettingSliderRow(title: "Distant Density (Day)", value: $fogDistantDensity, range: 0...2, step: 0.01, key: "fogDistantDensity")
                                SettingSliderRow(title: "Distant Start (Day)", value: $fogDistantStart, range: 0...12000, step: 50, key: "fogDistantStart")
                            } else {
                                SettingSliderRow(title: "Density (Night)", value: $fogDensityNight, range: 0...3, step: 0.01, key: "fogDensity_night")
                                SettingSliderRow(title: "Noise Strength (Night)", value: $fogNoiseStrengthNight, range: 0...3, step: 0.01, key: "fogNoiseStrength_night")
                                SettingSliderRow(title: "Noise Scale (Night)", value: $fogNoiseScaleNight, range: 0.1...80, step: 0.1, key: "fogNoiseScale_night")
                                SettingSliderRow(title: "Brightness (Night)", value: $fogBrightnessNight, range: 0...2, step: 0.01, key: "fogBrightness_night")
                                SettingSliderRow(title: "Scattering (Night)", value: $fogScatteringIntensityNight, range: 0...4, step: 0.01, key: "fogScatteringIntensity_night")
                                SettingSliderRow(title: "Height (Night)", value: $fogHeightNight, range: 0...500, step: 1, key: "fogHeight_night")
                                SettingToggleRow(title: "Distant Fog", value: $fogDistantFog, key: "fogDistantFog")
                                SettingSliderRow(title: "Distant Density (Night)", value: $fogDistantDensityNight, range: 0...2, step: 0.01, key: "fogDistantDensity_night")
                                SettingSliderRow(title: "Distant Start (Night)", value: $fogDistantStartNight, range: 0...12000, step: 50, key: "fogDistantStart_night")
                            }
                            
                            SettingToggleRow(title: "Fog Lights", value: $fogNativeLights, key: "fogNativeLights")
                            SettingSliderRow(title: "Light Multiplier", value: $fogNativeLightsMultiplier, range: 0...10, step: 0.1, key: "fogNativeLightsMultiplier")
                        }
                    }

                    if selectedSection == "Window Glow" {
                        SettingsSection(title: "Window Glow", resetAction: {
                            zossEmissiveIntensity = 1.9
                            zossEmissiveSmoothness = 0.34
                            zossEmissiveMetallic = 0.0
                            zossEmissiveHue = 0.90
                            zossEmissiveSaturation = 0.62
                            zossNightEmissiveHue = 0.115
                            zossNightEmissiveSaturation = 0.82

                            K1L0WeatherOverlayInstaller.setUnitySetting("zossEmissiveIntensity", "1.900")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossEmissiveSmoothness", "0.340")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossEmissiveMetallic", "0.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossEmissiveHue", "0.900")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossEmissiveSaturation", "0.620")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossNightEmissiveHue", "0.115")
                            K1L0WeatherOverlayInstaller.setUnitySetting("zossNightEmissiveSaturation", "0.820")
                            K1L0WindowGlowResolver.apply()
                        }) {
                            SettingSliderRow(title: "Brightness", value: $zossEmissiveIntensity, range: 0...50, step: 0.1, key: "zossEmissiveIntensity")
                            SettingSliderRow(title: "Smoothness", value: $zossEmissiveSmoothness, range: 0...1, step: 0.01, key: "zossEmissiveSmoothness")
                            SettingSliderRow(title: "Metallic", value: $zossEmissiveMetallic, range: 0...1, step: 0.01, key: "zossEmissiveMetallic")
                            SettingSliderRow(title: "Day Hue", value: $zossEmissiveHue, range: 0...1, step: 0.01, key: "zossEmissiveHue")
                            SettingSliderRow(title: "Day Saturation", value: $zossEmissiveSaturation, range: 0...1, step: 0.01, key: "zossEmissiveSaturation")
                            SettingSliderRow(title: "Night Hue", value: $zossNightEmissiveHue, range: 0...1, step: 0.01, key: "zossNightEmissiveHue")
                            SettingSliderRow(title: "Night Saturation", value: $zossNightEmissiveSaturation, range: 0...1, step: 0.01, key: "zossNightEmissiveSaturation")
                        }
                    }

                    if selectedSection == "Ground / Grass" {
                        SettingsSection(title: "Ground / Grass", resetAction: {
                            groundHue = 0.33
                            groundSaturation = 0.42
                            groundHueNight = 0.30
                            groundSaturationNight = 0.0

                            K1L0WeatherOverlayInstaller.setUnitySetting("groundHue", "0.330")
                            K1L0WeatherOverlayInstaller.setUnitySetting("groundSaturation", "0.420")
                            K1L0WeatherOverlayInstaller.setUnitySetting("groundHue_night", "0.300")
                            K1L0WeatherOverlayInstaller.setUnitySetting("groundSaturation_night", "0.000")
                        }) {
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
                            saturation = -28.0
                            contrast = 14.0
                            mapBrightness = -0.12
                            hueShift = -4.0
                            temperature = -12.0
                            tint = -6.0

                            K1L0WeatherOverlayInstaller.setUnitySetting("saturation", "-28.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("contrast", "14.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("mapBrightness", "-0.120")
                            K1L0WeatherOverlayInstaller.setUnitySetting("hueShift", "-4.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("temperature", "-12.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("tint", "-6.000")
                        }) {
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
                            bloomIntensity = 2.4
                            bloomThreshold = 1.2
                            bloomScatter = 0.43

                            K1L0WeatherOverlayInstaller.setUnitySetting("bloomEnabled", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("bloomIntensity", "2.400")
                            K1L0WeatherOverlayInstaller.setUnitySetting("bloomThreshold", "1.200")
                            K1L0WeatherOverlayInstaller.setUnitySetting("bloomScatter", "0.430")
                        }) {
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
                            lensDistEnabled = true
                            lensDistIntensity = -0.5

                            K1L0WeatherOverlayInstaller.setUnitySetting("vignetteEnabled", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("vignetteIntensity", "0.450")
                            K1L0WeatherOverlayInstaller.setUnitySetting("vignetteSmoothness", "1.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("chromaticEnabled", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("chromaticIntensity", "0.160")
                            K1L0WeatherOverlayInstaller.setUnitySetting("lensDistEnabled", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("lensDistIntensity", "-0.500")
                        }) {
                            SettingToggleRow(title: "Vignette", value: $vignetteEnabled, key: "vignetteEnabled")
                            SettingSliderRow(title: "Vignette Intensity", value: $vignetteIntensity, range: 0...1, step: 0.01, key: "vignetteIntensity")
                            SettingSliderRow(title: "Vignette Smoothness", value: $vignetteSmoothness, range: 0.01...1, step: 0.01, key: "vignetteSmoothness")
                            SettingToggleRow(title: "Chromatic", value: $chromaticEnabled, key: "chromaticEnabled")
                            SettingSliderRow(title: "Chromatic Intensity", value: $chromaticIntensity, range: 0...1, step: 0.01, key: "chromaticIntensity")
                            SettingToggleRow(title: "Lens Distortion", value: $lensDistEnabled, key: "lensDistEnabled")
                            SettingSliderRow(title: "Lens Distortion", value: $lensDistIntensity, range: -1...1, step: 0.01, key: "lensDistIntensity")
                        }
                    }

                    if selectedSection == "Focus + Motion" {
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
                            musicRadioMode = "final"
                            musicRadioVolume = 0.5415074229240417

                            K1L0WeatherOverlayInstaller.setUnitySetting("musicRadioEnabled", "1")
                            K1L0RadioPlayer.shared.setEnabled(true, apiBase: apiBase)
                            K1L0RadioPlayer.shared.setMode("final")
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
                            }
                            .padding(.vertical, 7)
                            .padding(.horizontal, 10)
                            .background(Color.white.opacity(0.025), in: RoundedRectangle(cornerRadius: 10))
                        }
                    }

                    if selectedSection == "Timers" {
                        SettingsSection(title: "Timers", resetAction: {
                            ambientMinStepsToSpawn = 110.0
                            receiveStepsRequired = 200.0
                            transmissionWaitSteps = 500.0
                            momentumSessionGraceMinutes = 20.0
                            ambientBeamTtlMinutes = 30.0
                            ambientCollectRadiusMeters = 16.0
                            locationCollectRadiusFeet = 50.0
                            ambientBeamDismissSteps = 80.0
                            beamDistanceLabels = false
                            beamDebug = false
                            perfOverlay = true
                            showStoryStrip = false
                            panelMapBrightness = 0.34

                            K1L0WeatherOverlayInstaller.setUnitySetting("ambientMinStepsToSpawn", "110.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("receiveStepsRequired", "200.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("transmissionWaitSteps", "500.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("momentumSessionGraceMinutes", "20.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("ambientBeamTtlMinutes", "30.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("ambientCollectRadiusMeters", "16.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("locationCollectRadiusFeet", "50.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("ambientBeamDismissSteps", "80.000")
                            K1L0WeatherOverlayInstaller.setUnitySetting("beamDistanceLabels", "0")
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
                            SettingSliderRow(title: "Location Collect Radius", subtitle: "Feet from a place item needed to collect it.", value: $locationCollectRadiusFeet, range: 10...300, step: 5, key: "locationCollectRadiusFeet")
                            SettingSliderRow(title: "Walk-Away Dismiss", subtitle: "Steps away from a pursued object before the alert is dismissed.", value: $ambientBeamDismissSteps, range: 0...1000, step: 10, key: "ambientBeamDismissSteps")
                            SettingToggleRow(title: "Beam Labels", value: $beamDistanceLabels, key: "beamDistanceLabels")
                            SettingToggleRow(title: "Beam Debug", value: $beamDebug, key: "beamDebug")
                            SettingToggleRow(title: "Perf Overlay", value: $perfOverlay, key: "perfOverlay")
                            SettingToggleRow(title: "Story Strip", value: $showStoryStrip, key: "showStoryStrip")
                            SettingSliderRow(title: "Panel Map Bright", value: $panelMapBrightness, range: 0...1, step: 0.01, key: "panelMapBrightness")
                        }
                    }

                    if selectedSection == "Weather" {
                        SettingsSection(title: "Weather", resetAction: {
                            weatherOpenMeteo = true
                            testSkyOverride = false
                            manualHour = 13.25
                            manualWeather = 0
                            skyTargetFps = 30.0

                            K1L0WeatherOverlayInstaller.setUnitySetting("weatherOpenMeteo", "1")
                            K1L0WeatherOverlayInstaller.setUnitySetting("testSkyOverride", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("manualHour", "13.250")
                            K1L0WeatherOverlayInstaller.setUnitySetting("manualWeather", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("skyTargetFps", "30.000")
                            K1L0SkyVideoURLResolver.restoreLastLiveSkyVideoIfAvailable()
                            K1L0WindowGlowResolver.applyManualHour(13.25)
                        }) {
                            SettingToggleRow(title: "Open-Meteo Source", value: $weatherOpenMeteo, key: "weatherOpenMeteo")
                            // Test override: beats live server weather AND the real
                            // clock even with GPS on — the time preset + weather
                            // picker below become the sole source of truth.
                            SettingToggleRow(title: "Test Weather Override", value: $testSkyOverride, key: "testSkyOverride")
                            SettingSkyTimeRow(manualHour: $manualHour)
                            SettingWeatherSegmentRow(selection: $manualWeather)
                            SettingSliderRow(title: "Sky Speed (FPS)", value: $skyTargetFps, range: 1...60, step: 1, key: "skyTargetFps")
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
                            experimentalLayeredSky = false
                            layeredSkyTopHue = 0.62
                            layeredSkyHorizonHue = 0.94
                            layeredCloudOpacity = 0.72
                            layeredCloudSpeed = 0.08
                            layeredCloudScale = 2.2
                            layeredCloudContrast = 1.5
                            K1L0WeatherOverlayInstaller.setUnitySetting("experimentalLayeredSky", "0")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredSkyTopHue", "0.620")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredSkyHorizonHue", "0.940")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredCloudOpacity", "0.720")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredCloudSpeed", "0.080")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredCloudScale", "2.200")
                            K1L0WeatherOverlayInstaller.setUnitySetting("layeredCloudContrast", "1.500")
                        }) {
                            SettingToggleRow(title: "Use Layered Metal Sky", value: $experimentalLayeredSky, key: "experimentalLayeredSky")
                            Text("Experimental renderer. Off returns instantly to the existing weather-video sky.")
                                .font(.system(size: 11, weight: .medium, design: .monospaced))
                                .foregroundStyle(.white.opacity(0.58))
                            SettingSliderRow(title: "Zenith Hue", value: $layeredSkyTopHue, range: 0...1, step: 0.01, key: "layeredSkyTopHue")
                            SettingSliderRow(title: "Horizon Hue", value: $layeredSkyHorizonHue, range: 0...1, step: 0.01, key: "layeredSkyHorizonHue")
                            SettingSliderRow(title: "Cloud Opacity", value: $layeredCloudOpacity, range: 0...1, step: 0.02, key: "layeredCloudOpacity")
                            SettingSliderRow(title: "Cloud Speed", value: $layeredCloudSpeed, range: -0.5...0.5, step: 0.01, key: "layeredCloudSpeed")
                            SettingSliderRow(title: "Cloud Scale", value: $layeredCloudScale, range: 0.5...6, step: 0.1, key: "layeredCloudScale")
                            SettingSliderRow(title: "Cloud Contrast", value: $layeredCloudContrast, range: 0.2...4, step: 0.1, key: "layeredCloudContrast")
                        }
                    }
                    }
                    .foregroundStyle(.white)
                    .padding(.horizontal, 18)
                    .padding(.bottom, 110)
                }
            }
        }
        .onAppear {
            if selectedSection == "Menu" || selectedSection.isEmpty {
                selectedSection = "Performance"
            }
            perfStats.startNativeSampling()
            resetNativeFogDefaultsOnce()
            syncLightingSettings()
            syncLayeredSkySettings()
        }
    }

    private func syncLayeredSkySettings() {
        K1L0WeatherOverlayInstaller.setUnitySetting("experimentalLayeredSky", experimentalLayeredSky ? "1" : "0")
        K1L0WeatherOverlayInstaller.setUnitySetting("layeredSkyTopHue", String(format: "%.3f", layeredSkyTopHue))
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
        fogDensity = 0.37
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
}

private struct LocationModeButton: View {
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

private struct SettingsCategoryButton: View {
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

private struct SettingsMasterMenuButton: View {
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

private struct SettingsSegmentedRow: View {
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

private struct SettingsSection<Content: View>: View {
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

private struct PerfStatCell: View {
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

private struct SettingSliderRow: View {
    let title: String
    var subtitle: String? = nil
    @Binding var value: Double
    let range: ClosedRange<Double>
    let step: Double
    let key: String

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
        if key == "zossEmissiveHue" ||
            key == "zossEmissiveSaturation" ||
            key == "zossNightEmissiveHue" ||
            key == "zossNightEmissiveSaturation" {
            K1L0WindowGlowResolver.apply()
            return
        }
        K1L0WeatherOverlayInstaller.setUnitySetting(key, String(format: "%.3f", newValue))
    }

	    private func syncDependentSkyVideoUrl(newValue: Double) {
	        guard key == "manualHour" else { return }
	        let manualWeather = UserDefaults.standard.integer(forKey: "k1lo_native_manualWeather")
	        K1L0SkyVideoURLResolver.applyManualSkyVideoIfTesting(manualWeatherIndex: manualWeather, manualHour: newValue)
        K1L0WindowGlowResolver.applyManualHour(newValue)
	    }

    private var formattedValue: String {
        abs(value.rounded() - value) < 0.001 ? "\(Int(value.rounded()))" : String(format: "%.2f", value)
    }
}

private struct SettingToggleRow: View {
    let title: String
    @Binding var value: Bool
    let key: String

    var body: some View {
        Button {
            value.toggle()
            K1L0WeatherOverlayInstaller.setUnitySetting(key, value ? "1" : "0")
            if key == "testSkyOverride" {
                if value {
                    let manualWeather = UserDefaults.standard.integer(forKey: "k1lo_native_manualWeather")
                    let manualHour = UserDefaults.standard.double(forKey: "k1lo_native_manualHour")
                    K1L0SkyVideoURLResolver.applyManualSkyVideoIfTesting(manualWeatherIndex: manualWeather, manualHour: manualHour)
                } else {
                    K1L0SkyVideoURLResolver.restoreLastLiveSkyVideoIfAvailable()
                }
            }
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

// Day / Dusk / Night presets instead of a fiddly 0–24 hour slider. The sky
// VIDEO only cares about day vs night, but the hour also drives the sun arc
// (dawn/dusk color, ground dayness, fog), so Dusk earns its slot.
private struct SettingSkyTimeRow: View {
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

private struct SettingWeatherSegmentRow: View {
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
	                K1L0SkyVideoURLResolver.applyManualSkyVideoIfTesting(manualWeatherIndex: newValue, manualHour: manualHour)
                K1L0WindowGlowResolver.applyManualHour(manualHour)
	            }
	        }
	    }
}

private struct NativeUserEditorDraft: Codable, Equatable {
    var name: String = ""
    var callsign: String = ""
    var bio: String = ""
    var url: String = ""
    var cloakDesign: String = ""
    var helmetDesign: String = ""
    var selfiePath: String = ""
    var selfieUrl: String = ""
    var helmetUrl: String = ""
    var cloakUrl: String = ""
    var avatarUrl: String = ""
    var helmetTextureUrl: String = ""
    var cloakTextureUrl: String = ""
    var skinRevision: Int = 0
}

private enum NativeUserEditorStore {
    private static let key = "k1lo_native_user_metadata_v1"

    static func load() -> NativeUserEditorDraft {
        guard let data = UserDefaults.standard.data(forKey: key),
              let draft = try? JSONDecoder().decode(NativeUserEditorDraft.self, from: data) else {
            return NativeUserEditorDraft()
        }
        return draft
    }

    static func save(_ draft: NativeUserEditorDraft) {
        guard let data = try? JSONEncoder().encode(draft) else { return }
        UserDefaults.standard.set(data, forKey: key)
    }
}

// Instagram-style 3-column grid of transmission thumbnails.
// Shows sent transmissions only (originals with a still image).
private struct TransmissionGridView: View {
    let groups: [NativeTransmissionChainGroup]
    let onOpen: (NativeTransmissionChainGroup) -> Void

    private let columns = Array(repeating: GridItem(.flexible(), spacing: 2), count: 3)

    // Only originals that have a visual (sent, no parent, has thumbUrl or video)
    private var gridGroups: [NativeTransmissionChainGroup] {
        groups.filter { group in
            guard let firstItem = group.orderedItems.first else { return false }
            let isSent = (firstItem.direction ?? "sent").lowercased() == "sent"
            let isOriginal = (firstItem.parentJobId ?? "").trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            let hasVisual = !(firstItem.thumbUrl?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ?? true)
                || firstItem.playbackVideoUrl != nil
            return isSent && isOriginal && hasVisual
        }
    }

    var body: some View {
        if gridGroups.isEmpty { EmptyView() } else {
            LazyVGrid(columns: columns, spacing: 2) {
                ForEach(gridGroups) { group in
                    let displayItem = group.orderedItems.first ?? group.latest
                    TransmissionGridCell(item: displayItem)
                        .onTapGesture { onOpen(group) }
                }
            }
            .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
        }
    }
}

private struct TransmissionGridCell: View {
    let item: NativeUserTransmissionItem

    var body: some View {
        GeometryReader { geo in
            ZStack(alignment: .bottomLeading) {
                // Thumb image
                if let raw = item.thumbUrl, let url = URL(string: raw) {
                    AsyncImage(url: url) { phase in
                        switch phase {
                        case .success(let img):
                            img.resizable().scaledToFill()
                        default:
                            Color.white.opacity(0.06)
                        }
                    }
                } else {
                    Color.white.opacity(0.06)
                }

                // Gradient scrim
                LinearGradient(
                    colors: [.clear, .black.opacity(0.52)],
                    startPoint: .center, endPoint: .bottom
                )

                // Play icon (only for items with video)
                if item.playbackVideoUrl != nil {
                    Image(systemName: "play.fill")
                        .font(.system(size: 11, weight: .black))
                        .foregroundStyle(.white.opacity(0.82))
                        .padding(6)
                }

                // Sent/received dot top-right
                VStack {
                    HStack {
                        Spacer()
                        Circle()
                            .fill((item.direction ?? "sent").lowercased() == "sent"
                                ? Color(red: 0.66, green: 1.0, blue: 0.76)
                                : Color(red: 1.0, green: 0.84, blue: 0.38))
                            .frame(width: 6, height: 6)
                            .padding(6)
                    }
                    Spacer()
                }
            }
            .frame(width: geo.size.width, height: geo.size.width)
            .clipped()
        }
        .aspectRatio(1, contentMode: .fit)
    }
}

private struct NativeUserTransmissionItem: Codable, Identifiable {
    let jobId: String
    let ownerUserId: String?
    let ownerName: String?
    let ownerCallsign: String?
    let ownerDisplayName: String?
    let direction: String?
    let thumbUrl: String?
    let finalUrl: String?
    let rawVideoUrl: String?
    let videoUrl: String?
    let audioUrl: String?
    let responsePlot: String?
    let responseOptions: [String]?
    let selectedResponse: String?
    let parentJobId: String?
    let rootJobId: String?
    let chainDepth: Int?
    let status: String?
    let createdAt: Double?
    let updatedAt: Double?

    var id: String { "\(direction ?? "sent")-\(ownerUserId ?? "")-\(jobId)" }

    var playbackVideoUrl: String? {
        let raw = rawVideoUrl?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !raw.isEmpty { return raw }
        let video = videoUrl?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !video.isEmpty { return video }
        let final = finalUrl?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return final.isEmpty ? nil : final
    }

    var isPlayableReadyOriginalSentTransmission: Bool {
        let job = jobId.trimmingCharacters(in: .whitespacesAndNewlines)
        return isPlayableReadyOriginalSentTransmissionIgnoringCancel
            && !K1L0ActiveTransmissionStore.shared.isCanceled(jobId: job)
    }

    var isOriginalSentTransmission: Bool {
        let directionValue = (direction ?? "sent").lowercased()
        let job = jobId.trimmingCharacters(in: .whitespacesAndNewlines)
        let parent = (parentJobId ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        let root = (rootJobId ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        return directionValue == "sent"
            && !job.isEmpty
            && parent.isEmpty
            && (root.isEmpty || root == job)
    }

    var isPlayableReadyOriginalSentTransmissionIgnoringCancel: Bool {
        let statusValue = (status ?? "").lowercased()
        return isOriginalSentTransmission
            && (statusValue == "ready" || statusValue == "complete")
            && playbackVideoUrl != nil
    }

    var createdAtMillis: Double {
        let value = createdAt ?? updatedAt ?? 0
        return value > 0 && value < 10_000_000_000 ? value * 1000 : value
    }
}

private struct NativeUserTransmissionResponse: Codable {
    let ok: Bool
    let transmissions: [NativeUserTransmissionItem]
}

private struct NativeTransmissionChainGroup: Identifiable {
    let id: String
    let items: [NativeUserTransmissionItem]

    var latest: NativeUserTransmissionItem {
        items.max { lhs, rhs in
            (lhs.updatedAt ?? lhs.createdAt ?? 0) < (rhs.updatedAt ?? rhs.createdAt ?? 0)
        } ?? items[0]
    }

    var statusText: String {
        let rootWasSent = items.contains { ($0.direction ?? "sent") == "sent" && (($0.parentJobId ?? "").isEmpty) }
        if rootWasSent { return "TRANSMISSION SENT" }
        let replies = items.filter { ($0.direction ?? "") == "sent" }.count
        return "\(max(1, replies)) REPLIED"
    }

    var orderedItems: [NativeUserTransmissionItem] {
        items.sorted { lhs, rhs in
            let leftDepth = lhs.chainDepth ?? 0
            let rightDepth = rhs.chainDepth ?? 0
            if leftDepth != rightDepth { return leftDepth < rightDepth }
            return (lhs.createdAt ?? lhs.updatedAt ?? 0) < (rhs.createdAt ?? rhs.updatedAt ?? 0)
        }
    }

    /// Creation time of the original (root) transmission in this chain — used to
    /// order the user-screen squares by when the thread started, not by the most
    /// recent reply.
    var originalCreatedAt: Double {
        let original = items.first(where: { $0.isOriginalSentTransmission })
            ?? orderedItems.first
            ?? items.first
        return original?.createdAt
            ?? original?.updatedAt
            ?? latest.createdAt
            ?? latest.updatedAt
            ?? 0
    }
}

private struct NativeWalkHistoryPoint: Identifiable {
    let id = UUID()
    let label: String
    let steps: Int
}

private struct NativeWalkHistoryCard: View {
    let hourly: [NativeWalkHistoryPoint]
    let daily: [NativeWalkHistoryPoint]
    let status: String

    var body: some View {
        WeatherGlassCard {
            VStack(alignment: .leading, spacing: 13) {
                HStack {
                    Text("Walk History")
                        .font(.system(size: 19, weight: .bold))
                    Spacer()
                    Text(status)
                        .font(.system(size: 10, weight: .black))
                        .foregroundStyle(.white.opacity(0.48))
                }
                NativeWalkLineGraph(title: "24 HOURS", points: hourly, tint: Color(red: 0.66, green: 1.0, blue: 0.76))
                NativeWalkLineGraph(title: "7 DAYS", points: daily, tint: Color(red: 0.54, green: 0.78, blue: 1.0))
            }
        }
    }
}

private struct NativeWalkLineGraph: View {
    let title: String
    let points: [NativeWalkHistoryPoint]
    let tint: Color
    private let plottedWidthRatio: CGFloat = 1.0

    private var totalSteps: Int {
        points.reduce(0) { $0 + $1.steps }
    }

    private var maxSteps: Int {
        max(points.map(\.steps).max() ?? 1, 1)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            HStack(alignment: .firstTextBaseline) {
                Text(title)
                    .font(.system(size: 11, weight: .black))
                    .foregroundStyle(.white.opacity(0.58))
                Spacer()
                Text("\(totalSteps)")
                    .font(.system(size: 17, weight: .black))
                    .foregroundStyle(.white)
                    .monospacedDigit()
                Text("steps")
                    .font(.system(size: 10, weight: .bold))
                    .foregroundStyle(.white.opacity(0.52))
            }
            ZStack {
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .fill(Color.white.opacity(0.045))
                NativeWalkFillPath(points: points, maxSteps: maxSteps, plottedWidthRatio: plottedWidthRatio)
                    .fill(
                        LinearGradient(
                            colors: [tint.opacity(0.22), tint.opacity(0.02)],
                            startPoint: .top,
                            endPoint: .bottom
                        )
                    )
                    .padding(.horizontal, 8)
                    .padding(.vertical, 9)
                NativeWalkTimeGrid(points: points, plottedWidthRatio: plottedWidthRatio)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 9)
                NativeWalkLinePath(points: points, maxSteps: maxSteps, plottedWidthRatio: plottedWidthRatio)
                    .stroke(tint, style: StrokeStyle(lineWidth: 2.4, lineCap: .round, lineJoin: .round))
                    .padding(.horizontal, 8)
                    .padding(.vertical, 9)
            }
            .frame(height: 82)
            .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).stroke(Color.white.opacity(0.10), lineWidth: 1))
        }
    }
}

private struct NativeWalkTimeGrid: View {
    let points: [NativeWalkHistoryPoint]
    let plottedWidthRatio: CGFloat

    var body: some View {
        GeometryReader { geometry in
            ZStack(alignment: .leading) {
                ForEach(0...points.count, id: \.self) { index in
                    let denominator = CGFloat(max(points.count, 1))
                    let x = (CGFloat(index) / denominator) * plottedWidthRatio * geometry.size.width
                    let isEdge = index == 0 || index == points.count
                    Rectangle()
                        .fill(Color.white.opacity(isEdge ? 0.18 : 0.08))
                        .frame(width: isEdge ? 1.2 : 0.6, height: geometry.size.height)
                        .position(x: x, y: geometry.size.height * 0.5)
                }
            }
        }
        .allowsHitTesting(false)
    }
}

private struct NativeWalkLinePath: Shape {
    let points: [NativeWalkHistoryPoint]
    let maxSteps: Int
    let plottedWidthRatio: CGFloat

    func path(in rect: CGRect) -> Path {
        graphPath(in: rect, closeToBottom: false)
    }

    fileprivate func graphPath(in rect: CGRect, closeToBottom: Bool) -> Path {
        var path = Path()
        guard !points.isEmpty else { return path }
        let denominator = CGFloat(max(points.count, 1))
        var lastPoint = CGPoint(x: rect.minX, y: rect.maxY)
        for index in points.indices {
            let x = rect.minX + ((CGFloat(index) + 0.5) / denominator) * rect.width * plottedWidthRatio
            let ratio = CGFloat(points[index].steps) / CGFloat(max(maxSteps, 1))
            let y = rect.maxY - max(0, min(1, ratio)) * rect.height
            lastPoint = CGPoint(x: x, y: y)
            if index == points.startIndex {
                path.move(to: CGPoint(x: x, y: y))
            } else {
                path.addLine(to: CGPoint(x: x, y: y))
            }
        }
        if closeToBottom {
            path.addLine(to: CGPoint(x: lastPoint.x, y: rect.maxY))
            path.addLine(to: CGPoint(x: rect.minX, y: rect.maxY))
            path.closeSubpath()
        }
        return path
    }
}

private struct NativeWalkFillPath: Shape {
    let points: [NativeWalkHistoryPoint]
    let maxSteps: Int
    let plottedWidthRatio: CGFloat

    func path(in rect: CGRect) -> Path {
        NativeWalkLinePath(points: points, maxSteps: maxSteps, plottedWidthRatio: plottedWidthRatio).graphPath(in: rect, closeToBottom: true)
    }
}

private struct NativeUserEditorPanel: View {
    var tabsMode: Bool = false
    let onClose: () -> Void

    @ObservedObject private var saveStore = K1L0UserMetadataSaveStore.shared
    @State private var draft = NativeUserEditorStore.load()
    @State private var transmissions: [NativeUserTransmissionItem] = []
    @State private var transmissionsStatus = "loading transmissions…"
    @State private var isEditingProfile = false
    @State private var showingIdentityDetail = false
#if canImport(UIKit)
    @State private var selfie: UIImage?
    @State private var selfiePickerRequest: PhotoPickerRequest? = nil
#elseif canImport(AppKit)
    @State private var selfie: NSImage?
#endif

    @State private var originalProfileDraft: NativeUserEditorDraft? = nil
    @State private var originalAvatarDraft: NativeUserEditorDraft? = nil

    private func isProfileDirty() -> Bool {
        guard let original = originalProfileDraft else { return false }
        return draft.name != original.name ||
               draft.callsign != original.callsign ||
               draft.url != original.url ||
               draft.bio != original.bio
    }

    private func isAvatarDirty() -> Bool {
        guard let original = originalAvatarDraft else { return false }
        return draft.cloakDesign != original.cloakDesign ||
               draft.helmetDesign != original.helmetDesign ||
               draft.selfiePath != original.selfiePath
    }

    var body: some View {
        GeometryReader { geometry in
            let fullScreenEditor = isEditingProfile || showingIdentityDetail
            let panelTop = fullScreenEditor ? 0 : geometry.safeAreaInsets.top
            ZStack(alignment: .top) {
                if fullScreenEditor {
                    Color.black.ignoresSafeArea()
                } else {
                    Color.clear.ignoresSafeArea()
                }

                ZStack(alignment: .top) {
                    if showingIdentityDetail {
                        identityDetailScroll
                            .transition(.move(edge: .trailing).combined(with: .opacity))
                    } else if isEditingProfile {
                        mainUserScroll
                            .transition(.move(edge: .trailing).combined(with: .opacity))
                    } else {
                        profileSummaryScroll
                            .transition(.move(edge: .leading).combined(with: .opacity))
                    }

                    UserPanelHeader(
                        title: showingIdentityDetail ? "Avatar" : (isEditingProfile ? "Edit Profile" : "User"),
                        tabsMode: tabsMode,
                        onClose: onClose,
                        onSave: nil,
                        onBack: (showingIdentityDetail || isEditingProfile) ? {
                            if showingIdentityDetail {
                                if isAvatarDirty() {
                                    save()
                                }
                                withAnimation(.spring(response: 0.32, dampingFraction: 0.90)) {
                                    showingIdentityDetail = false
                                }
                                originalAvatarDraft = nil
                            } else if isEditingProfile {
                                if isProfileDirty() {
                                    save()
                                }
                                withAnimation(.spring(response: 0.32, dampingFraction: 0.90)) {
                                    isEditingProfile = false
                                }
                                originalProfileDraft = nil
                            }
                        } : nil
                    )
                }
                .coordinateSpace(name: "user-panel")
                .frame(width: geometry.size.width)
                .frame(maxHeight: fullScreenEditor ? geometry.size.height - geometry.safeAreaInsets.top : max(520, geometry.size.height - panelTop))
                .background(
                    fullScreenEditor ? Color.black : (tabsMode ? Color.clear : Color.black.opacity(0.18)),
                    in: RoundedRectangle(cornerRadius: fullScreenEditor ? 0 : 28, style: .continuous)
                )
                .padding(.top, fullScreenEditor ? geometry.safeAreaInsets.top : panelTop)
            }
            .ignoresSafeArea(edges: .bottom)
        }
#if canImport(UIKit)
        .sheet(item: $selfiePickerRequest) { request in
            NativePhotoPicker(sourceType: request.source) { image, path in
                if let image, let path {
                    selfie = image
                    draft.selfiePath = path
                    NativeUserEditorStore.save(draft)
                    saveStore.status = "selfie attached."
                }
                selfiePickerRequest = nil
            }
            .ignoresSafeArea()
        }
#endif
        .onAppear {
            loadSelfiePreview()
            K1L0WeatherOverlayInstaller.loadNativeUserMetadata()
        }
        .onChange(of: draft) { newValue in
            NativeUserEditorStore.save(newValue)
        }
        .onChange(of: saveStore.saveSuccessTrigger) { succeeded in
            if succeeded {
                saveStore.saveSuccessTrigger = false
            }
        }
        .onReceive(saveStore.$savedSelfieURL) { url in
            guard !url.isEmpty else { return }
            draft.selfieUrl = url
            NativeUserEditorStore.save(draft)
        }
        .onReceive(saveStore.$savedHelmetURL) { url in
            guard !url.isEmpty else { return }
            draft.helmetUrl = url
            NativeUserEditorStore.save(draft)
        }
        .onReceive(saveStore.$savedCloakURL) { url in
            guard !url.isEmpty else { return }
            draft.cloakUrl = url
            NativeUserEditorStore.save(draft)
        }
        .onReceive(saveStore.$savedAvatarURL) { url in
            guard !url.isEmpty else { return }
            draft.avatarUrl = url
            NativeUserEditorStore.save(draft)
        }
        .onReceive(saveStore.$savedHelmetTextureURL) { url in
            guard !url.isEmpty else { return }
            draft.helmetTextureUrl = url
            NativeUserEditorStore.save(draft)
        }
        .onReceive(saveStore.$savedCloakTextureURL) { url in
            guard !url.isEmpty else { return }
            draft.cloakTextureUrl = url
            NativeUserEditorStore.save(draft)
        }
        .onReceive(saveStore.$loadedName) { value in
            guard !value.isEmpty else { return }
            draft.name = value
            NativeUserEditorStore.save(draft)
        }
        .onReceive(saveStore.$loadedCallsign) { value in
            guard !value.isEmpty else { return }
            draft.callsign = value
            NativeUserEditorStore.save(draft)
        }
        .onReceive(saveStore.$loadedBio) { value in
            guard !value.isEmpty else { return }
            draft.bio = value
            NativeUserEditorStore.save(draft)
        }
        .onReceive(saveStore.$loadedUrl) { value in
            guard !value.isEmpty else { return }
            draft.url = value
            NativeUserEditorStore.save(draft)
        }
        .onReceive(saveStore.$loadedCloakDesign) { value in
            guard !value.isEmpty else { return }
            draft.cloakDesign = value
            NativeUserEditorStore.save(draft)
        }
        .onReceive(saveStore.$loadedHelmetDesign) { value in
            guard !value.isEmpty else { return }
            draft.helmetDesign = value
            NativeUserEditorStore.save(draft)
        }
    }

    private var profileSummaryScroll: some View {
        ScrollView(.vertical, showsIndicators: true) {
            VStack(alignment: .leading, spacing: 14) {
                Color.clear.frame(height: 24)

                WeatherGlassCard {
                    VStack(alignment: .leading, spacing: 13) {
                        HStack(alignment: .top, spacing: 14) {
                            renderedHero
                            VStack(alignment: .leading, spacing: 5) {
                                Text(profileDisplayName)
                                    .font(.system(size: 24, weight: .black))
                                    .foregroundStyle(.white)
                                    .lineLimit(1)
                                    .minimumScaleFactor(0.68)
                                Text(profileSecondaryLine)
                                    .font(.system(size: 13, weight: .bold, design: .monospaced))
                                    .foregroundStyle(.white.opacity(0.68))
                                    .lineLimit(2)
                                    .minimumScaleFactor(0.72)
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                        }

                        if !draft.bio.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                            Text(draft.bio.trimmingCharacters(in: .whitespacesAndNewlines))
                                .font(.system(size: 14, weight: .semibold))
                                .foregroundStyle(.white.opacity(0.82))
                                .lineLimit(4)
                                .frame(maxWidth: .infinity, alignment: .leading)
                        }

                        Button {
                            withAnimation(.spring(response: 0.32, dampingFraction: 0.90)) {
                                isEditingProfile = true
                                originalProfileDraft = draft
                            }
                        } label: {
                            Text("[ EDIT PROFILE ]")
                                .font(.system(size: 14, weight: .black, design: .monospaced))
                                .foregroundStyle(.white)
                                .frame(maxWidth: .infinity, minHeight: 46)
                                .background(Color.white.opacity(0.10), in: Capsule())
                        }
                        .buttonStyle(.plain)
                    }
                }

                if !saveStore.status.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    Text(saveStore.status)
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.88))
                }

                // Transmission grid
                if !transmissions.isEmpty {
                    TransmissionGridView(groups: transmissionGroups, onOpen: openTransmissionChain)
                } else {
                    Text(transmissionsStatus)
                        .font(.system(size: 12, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.40))
                        .frame(maxWidth: .infinity, alignment: .leading)
                }

                Button {
                    K1L0WeatherOverlayInstaller.logoutNativeSession()
                    onClose()
                } label: {
                    Text("[ LOG OUT ]")
                        .font(.system(size: 14, weight: .black, design: .monospaced))
                        .foregroundStyle(Color(red: 1.0, green: 0.36, blue: 0.32))
                        .frame(maxWidth: .infinity, minHeight: 46)
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 20)
            .padding(.top, 24)
            .padding(.bottom, 38)
        }
        .onAppear(perform: loadTransmissions)
    }

    private var mainUserScroll: some View {
        ScrollView(.vertical, showsIndicators: true) {
            VStack(alignment: .leading, spacing: 14) {
                Color.clear.frame(height: 24)

                WeatherGlassCard {
                    VStack(alignment: .leading, spacing: 10) {
                        HStack(alignment: .top, spacing: 14) {
                            Button {
                                withAnimation(.spring(response: 0.32, dampingFraction: 0.90)) {
                                    showingIdentityDetail = true
                                    originalAvatarDraft = draft
                                }
                            } label: {
                                ZStack(alignment: .bottom) {
                                    renderedHero
                                    Text("EDIT AVATAR")
                                        .font(.system(size: 9, weight: .black, design: .rounded))
                                        .foregroundStyle(.white)
                                        .padding(.horizontal, 6)
                                        .padding(.vertical, 3)
                                        .background(Color.black.opacity(0.62), in: Capsule())
                                        .padding(.bottom, 4)
                                }
                            }
                            .buttonStyle(.plain)

                            VStack(alignment: .leading, spacing: 8) {
                                profileTextField("Name", text: $draft.name)
                                profileTextField("Callsign", text: $draft.callsign)
                                profileTextField("Instagram (optional)", text: $draft.url)
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                        }

                        profileTextField("Bio (optional)", text: $draft.bio)
                            .frame(maxWidth: .infinity, minHeight: 72, alignment: .topLeading)
                    }
                }

                Text(saveStore.status)
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.88))

                Button {
                    K1L0WeatherOverlayInstaller.logoutNativeSession()
                    onClose()
                } label: {
                    Text("[ LOG OUT ]")
                        .font(.system(size: 14, weight: .black, design: .monospaced))
                        .foregroundStyle(Color(red: 1.0, green: 0.36, blue: 0.32))
                        .frame(maxWidth: .infinity, minHeight: 46)
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 20)
            .padding(.top, 24)
            .padding(.bottom, 38)
        }
        .scrollDismissesKeyboardCompat()
    }

    private var profileDisplayName: String {
        let name = draft.name.trimmingCharacters(in: .whitespacesAndNewlines)
        return name.isEmpty ? "K1L0 User" : name
    }

    private var profileSecondaryLine: String {
        let callsign = draft.callsign.trimmingCharacters(in: .whitespacesAndNewlines)
        let instagram = draft.url.trimmingCharacters(in: .whitespacesAndNewlines)
        let pieces = [callsign.isEmpty ? "" : "@\(callsign)", instagram].filter { !$0.isEmpty }
        return pieces.isEmpty ? "profile" : pieces.joined(separator: "  ")
    }

    private var identityDetailScroll: some View {
        ScrollView(.vertical, showsIndicators: true) {
            VStack(alignment: .leading, spacing: 14) {
                Color.clear.frame(height: 24)

                WeatherGlassCard {
                    VStack(alignment: .leading, spacing: 12) {
                        renderedIdentityFull
                        let identityReady = !draft.helmetUrl.isEmpty && (!draft.cloakUrl.isEmpty || !draft.avatarUrl.isEmpty)
                        let meshReady = !draft.helmetTextureUrl.isEmpty && !draft.cloakTextureUrl.isEmpty
                        Text(identityReady && meshReady ? "helmet, cloak, and 3D skin ready." : "helmet, cloak, and 3D skin render after auto-saving.")
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundStyle(identityReady && meshReady ? Color(red: 0.66, green: 1.0, blue: 0.76) : .white.opacity(0.54))
                    }
                }

                WeatherGlassCard {
                    VStack(alignment: .leading, spacing: 10) {
                        Text("Design")
                            .font(.system(size: 19, weight: .bold))
                        profileTextField("Cloak design", text: $draft.cloakDesign)
                        profileTextField("Helmet design", text: $draft.helmetDesign)
                    }
                }

                WeatherGlassCard {
                    VStack(alignment: .leading, spacing: 10) {
                        Text("Selfie")
                            .font(.system(size: 19, weight: .bold))
                        HStack(alignment: .top, spacing: 12) {
                            selfiePreview
                            VStack(alignment: .leading, spacing: 8) {
#if canImport(UIKit)
                                HStack(spacing: 10) {
                                    selfieButton("TAKE SELFIE", source: .camera)
                                    selfieButton("SELECT", source: .photoLibrary)
                                }
#elseif canImport(AppKit)
                                Button {
                                    macSelectSelfie()
                                } label: {
                                    Text("[ SELECT SELFIE ]")
                                        .font(.system(size: 13, weight: .black))
                                        .foregroundStyle(.white)
                                        .frame(maxWidth: .infinity, minHeight: 42)
                                        .overlay(Rectangle().stroke(Color.white.opacity(0.40), lineWidth: 1))
                                }
                                .buttonStyle(.plain)
#endif
                                Text(draft.selfiePath.isEmpty && draft.selfieUrl.isEmpty ? "no selfie attached" : "selfie attached")
                                    .font(.system(size: 13, weight: .semibold))
                                    .foregroundStyle(draft.selfiePath.isEmpty && draft.selfieUrl.isEmpty ? .white.opacity(0.54) : Color(red: 0.66, green: 1.0, blue: 0.76))
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                        }
                    }
                }

                WeatherGlassCard {
                    VStack(alignment: .leading, spacing: 10) {
                        Text("Parts")
                            .font(.system(size: 19, weight: .bold))
                        HStack(spacing: 12) {
                            identityPreview(title: "HELMET", urlString: draft.helmetUrl)
                            identityPreview(title: "CLOAK", urlString: draft.cloakUrl.isEmpty ? draft.avatarUrl : draft.cloakUrl)
                        }
                    }
                }

                Text(saveStore.status)
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.88))
            }
            .padding(.horizontal, 20)
            .padding(.top, 24)
            .padding(.bottom, 38)
        }
        .scrollDismissesKeyboardCompat()
    }

    private var transmissionGroups: [NativeTransmissionChainGroup] {
        let visibleTransmissions = transmissions.filter { item in
            let status = (item.status ?? "").trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
            let hasVisual = item.playbackVideoUrl != nil || !(item.thumbUrl?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ?? true)
            return status != "error" || hasVisual
        }
        let grouped = Dictionary(grouping: visibleTransmissions) { item in
            let root = item.rootJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            if !root.isEmpty { return root }
            let parent = item.parentJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            if !parent.isEmpty { return parent }
            return item.jobId
        }
        return grouped.map { key, items in
            NativeTransmissionChainGroup(
                id: key,
                items: items.sorted {
                    ($0.updatedAt ?? $0.createdAt ?? 0) > ($1.updatedAt ?? $1.createdAt ?? 0)
                }
            )
        }
        // Newest original transmission first — the squares reflect when each
        // thread was started, not when the latest reply landed.
        .sorted {
            $0.originalCreatedAt > $1.originalCreatedAt
        }
    }

    private func transmissionGroupRow(_ group: NativeTransmissionChainGroup) -> some View {
        let latest = group.latest
        return HStack(alignment: .center, spacing: 12) {
            transmissionCircleThumb(latest)
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 6) {
                    Text(transmissionDirectionLabel(latest))
                        .font(.system(size: 10, weight: .black))
                        .foregroundStyle((latest.direction ?? "sent").lowercased() == "sent" ? Color(red: 0.66, green: 1.0, blue: 0.76) : Color(red: 1.0, green: 0.84, blue: 0.38))
                    Text(humanTime(latest.updatedAt ?? latest.createdAt ?? 0))
                        .font(.system(size: 11, weight: .bold))
                        .foregroundStyle(.white.opacity(0.50))
                    Spacer(minLength: 0)
                    if group.items.count > 1 {
                        Text("\(group.items.count)")
                            .font(.system(size: 9, weight: .black))
                            .foregroundStyle(.white.opacity(0.42))
                    }
                }
                Text(transmissionTitle(latest))
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(.white)
                    .lineLimit(2)
                    .multilineTextAlignment(.leading)
                    .frame(maxWidth: .infinity, alignment: .leading)
                if let selected = latest.selectedResponse?.trimmingCharacters(in: .whitespacesAndNewlines), !selected.isEmpty {
                    Text("response: \(selected)")
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.48))
                        .lineLimit(1)
                }
            }
            Image(systemName: "play.fill")
                .font(.system(size: 11, weight: .black))
                .foregroundStyle(.white.opacity(0.34))
        }
        .padding(9)
        .background(Color.white.opacity(0.045))
        .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).stroke(Color.white.opacity(0.10), lineWidth: 1))
        .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
    }

    private func transmissionRow(_ item: NativeUserTransmissionItem) -> some View {
        HStack(alignment: .center, spacing: 12) {
            transmissionCircleThumb(item)
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 6) {
                    Text(transmissionDirectionLabel(item))
                        .font(.system(size: 10, weight: .black))
                        .foregroundStyle(item.direction == "received" ? Color(red: 1.0, green: 0.84, blue: 0.38) : Color(red: 0.66, green: 1.0, blue: 0.76))
                    Text(humanTime(item.updatedAt ?? item.createdAt ?? 0))
                        .font(.system(size: 11, weight: .bold))
                        .foregroundStyle(.white.opacity(0.50))
                    Spacer(minLength: 0)
                    if let depth = item.chainDepth, depth > 0 {
                        Text("CHAIN \(depth)")
                            .font(.system(size: 9, weight: .black))
                            .foregroundStyle(.white.opacity(0.42))
                    }
                }
                Text(transmissionTitle(item))
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(.white)
                    .lineLimit(2)
                    .multilineTextAlignment(.leading)
                    .frame(maxWidth: .infinity, alignment: .leading)
                if let selected = item.selectedResponse?.trimmingCharacters(in: .whitespacesAndNewlines), !selected.isEmpty {
                    Text("response: \(selected)")
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.48))
                        .lineLimit(1)
                }
            }
            Image(systemName: "play.fill")
                .font(.system(size: 11, weight: .black))
                .foregroundStyle(.white.opacity(0.34))
        }
        .padding(9)
        .background(Color.white.opacity(0.045))
        .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).stroke(Color.white.opacity(0.10), lineWidth: 1))
        .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
    }

    private func transmissionDirectionLabel(_ item: NativeUserTransmissionItem) -> String {
        let parent = item.parentJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if isOwnTransmission(item) {
            return parent.isEmpty ? "YOU SENT" : "YOU REPLIED"
        }
        let actor = transmissionActorLabel(item).uppercased()
        return parent.isEmpty ? "\(actor) SENT" : "\(actor) REPLIED"
    }

    private func transmissionActorLabel(_ item: NativeUserTransmissionItem) -> String {
        let display = item.ownerDisplayName?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !display.isEmpty { return display }
        let name = item.ownerName?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !name.isEmpty { return name }
        let callsign = item.ownerCallsign?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !callsign.isEmpty { return callsign }
        let owner = item.ownerUserId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if owner.count > 8 { return String(owner.prefix(8)) }
        return owner.isEmpty ? "K1L0" : owner
    }

    private func isOwnTransmission(_ item: NativeUserTransmissionItem) -> Bool {
        let direction = (item.direction ?? "").trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        if direction == "sent" { return true }
        guard let current = currentNativeUserId() else { return false }
        let owner = item.ownerUserId?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() ?? ""
        return !owner.isEmpty && owner == current.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
    }

    private func transmissionCircleThumb(_ item: NativeUserTransmissionItem) -> some View {
        ZStack {
            Circle()
                .fill(Color.white.opacity(0.07))
            if let raw = item.thumbUrl, let url = URL(string: raw) {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image):
                        image.resizable().scaledToFill()
                    case .failure:
                        Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.yellow)
                    default:
                        ProgressView().tint(.white)
                    }
                }
            }
        }
        .frame(width: 54, height: 54)
        .clipShape(Circle())
        .overlay(Circle().stroke(Color.white.opacity(0.24), lineWidth: 1.2))
    }

    private func transmissionTitle(_ item: NativeUserTransmissionItem) -> String {
        let plot = item.responsePlot?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !plot.isEmpty {
            return cleanedTransmissionTitle(plot, item: item)
        }
        let status = item.status?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return status.isEmpty ? "Transmission" : status
    }

    private func cleanedTransmissionTitle(_ raw: String, item: NativeUserTransmissionItem) -> String {
        var title = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        title = title.replacingOccurrences(
            of: #"(?i)^\s*the person(?:\s+responds)?\s*:\s*"#,
            with: "",
            options: .regularExpression
        )
        let actor = transmissionActorLabel(item).trimmingCharacters(in: .whitespacesAndNewlines)
        if !actor.isEmpty {
            let escapedActor = NSRegularExpression.escapedPattern(for: actor)
            title = title.replacingOccurrences(
                of: #"(?i)^\s*\#(escapedActor)(?:\s+responds|\s+replied)?\s*:\s*"#,
                with: "",
                options: .regularExpression
            )
        }
        title = title.replacingOccurrences(
            of: #"(?i)^\s*[A-Z][A-Za-z0-9 ._'’-]{1,42}\s+(?:responds|replied)\s*:\s*"#,
            with: "",
            options: .regularExpression
        )
        return title.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func humanTime(_ raw: Double) -> String {
        guard raw > 0 else { return "now" }
        let seconds = raw > 9_999_999_999 ? raw / 1000.0 : raw
        let elapsed = max(0, Date().timeIntervalSince1970 - seconds)
        if elapsed < 60 { return "now" }
        if elapsed < 3600 { return "\(Int(elapsed / 60))m ago" }
        if elapsed < 86400 { return "\(Int(elapsed / 3600))h ago" }
        if elapsed < 604800 { return "\(Int(elapsed / 86400))d ago" }
        let formatter = DateFormatter()
        formatter.dateFormat = "MMM d"
        return formatter.string(from: Date(timeIntervalSince1970: seconds))
    }

    private func transmissionClip(_ item: NativeUserTransmissionItem) -> K1L0TransmissionClip {
        let videoURL = item.playbackVideoUrl.flatMap { URL(string: $0) }
        let imageURL = item.thumbUrl.flatMap { URL(string: $0) }
        let parentJob = item.parentJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return K1L0TransmissionClip(
            videoURL: videoURL,
            imageURL: imageURL,
            audioURL: item.audioUrl.flatMap { URL(string: $0) },
            responsePlot: item.responsePlot?.trimmingCharacters(in: .whitespacesAndNewlines) ?? "",
            responseOptions: item.responseOptions ?? [],
            selectedResponse: item.selectedResponse?.trimmingCharacters(in: .whitespacesAndNewlines) ?? "",
            sourceJobId: item.jobId,
            sourceUserId: item.ownerUserId ?? "",
            sourceName: item.ownerDisplayName ?? item.ownerName ?? item.ownerCallsign ?? "",
            allowsResponse: !parentJob.isEmpty && !isOwnTransmission(item)
        )
    }

    private func openTransmissionChain(_ group: NativeTransmissionChainGroup) {
        var clips = group.orderedItems
            .map { transmissionClip($0) }
            .filter { $0.videoURL != nil || $0.imageURL != nil }
        guard let first = clips.first else { return }
        let latest = group.latest
        let viewerIsOriginalAuthor = group.orderedItems.first.map { isOwnTransmission($0) } ?? false
        let latestResponseOptions = clips.indices.reversed()
            .first(where: { clips[$0].allowsResponse })
            .map { clips[$0].responseOptions } ?? []
        K1L0TransmissionResultStore.shared.current = K1L0TransmissionResult(
            status: latest.status ?? "ready",
            imageURL: first.imageURL,
            videoURL: first.videoURL,
            audioURL: first.audioURL,
            lyrics: "",
            responsePlot: first.responsePlot,
            responseOptions: latestResponseOptions,
            clips: clips,
            allowsResponseOptions: !viewerIsOriginalAuthor && clips.contains { $0.allowsResponse },
            allowsTextResponse: clips.contains { $0.allowsResponse },
            selectedResponse: first.selectedResponse
        )
    }

    private func openTransmission(_ item: NativeUserTransmissionItem) {
        let videoURL = item.playbackVideoUrl.flatMap { URL(string: $0) }
        let imageURL = item.thumbUrl.flatMap { URL(string: $0) }
        guard videoURL != nil || imageURL != nil else { return }
        let responseOptions = item.responseOptions ?? []
        K1L0TransmissionResultStore.shared.current = K1L0TransmissionResult(
            status: item.status ?? "ready",
            imageURL: imageURL,
            videoURL: videoURL,
            audioURL: item.audioUrl.flatMap { URL(string: $0) },
            lyrics: "",
            responsePlot: item.responsePlot?.trimmingCharacters(in: .whitespacesAndNewlines) ?? "",
            responseOptions: responseOptions,
            allowsResponseOptions: false,
            allowsTextResponse: false,
            selectedResponse: item.selectedResponse?.trimmingCharacters(in: .whitespacesAndNewlines)
        )
    }

    private func loadTransmissions() {
        guard let userId = currentNativeUserId(), !userId.isEmpty else {
            transmissions = []
            transmissionsStatus = "not signed in."
            return
        }
        transmissionsStatus = "loading transmissions…"
        fetchTransmissions(userId: userId, apiIndex: 0)
    }

    private func fetchTransmissions(userId: String, apiIndex: Int) {
        let candidates = [
            "https://api-tunnel.kilo.gallery",
            "http://192.168.40.34:3000",
            "http://fred.local:3000",
            "https://api.kilomeme.com"
        ]
        guard apiIndex < candidates.count else {
            DispatchQueue.main.async {
                transmissions = []
                transmissionsStatus = "transmissions unavailable."
            }
            return
        }
        let safeUser = userId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? userId
        guard let url = URL(string: "\(candidates[apiIndex])/api/k1l0/v2/my-transmissions?userId=\(safeUser)") else {
            fetchTransmissions(userId: userId, apiIndex: apiIndex + 1)
            return
        }
        URLSession.shared.dataTask(with: url) { data, response, _ in
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            guard code == 200,
                  let data,
                  let decoded = try? JSONDecoder().decode(NativeUserTransmissionResponse.self, from: data),
                  decoded.ok
            else {
                fetchTransmissions(userId: userId, apiIndex: apiIndex + 1)
                return
            }
            DispatchQueue.main.async {
                transmissions = decoded.transmissions
                transmissionsStatus = decoded.transmissions.isEmpty ? "no transmissions yet." : ""
            }
        }.resume()
    }

    private func currentNativeUserId() -> String? {
        let defaults = UserDefaults.standard
        for key in ["FirebaseUserId", "K1L0UserId", "DeviceID", "deviceID"] {
            let value = defaults.string(forKey: key) ?? ""
            let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
            if !trimmed.isEmpty { return trimmed }
        }
        return nil
    }

    @ViewBuilder
    private func profileTextField(_ title: String, text: Binding<String>) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(title)
                .font(.system(size: 12, weight: .bold))
                .foregroundStyle(.white.opacity(0.62))
#if canImport(UIKit)
            TextField(title, text: text)
                .textInputAutocapitalization(.words)
                .font(.system(size: 16, weight: .semibold))
                .foregroundStyle(.white)
                .padding(12)
                .background(Color.white.opacity(0.08))
                .overlay(Rectangle().stroke(Color.white.opacity(0.18), lineWidth: 1))
#else
            TextField(title, text: text)
                .textFieldStyle(.plain)
                .font(.system(size: 16, weight: .semibold))
                .foregroundStyle(.white)
                .padding(12)
                .background(Color.white.opacity(0.08))
                .overlay(Rectangle().stroke(Color.white.opacity(0.18), lineWidth: 1))
#endif
        }
    }

    @ViewBuilder
    private var selfiePreview: some View {
#if canImport(UIKit)
        if let selfie {
            Image(uiImage: selfie)
                .resizable()
                .scaledToFill()
                .frame(width: 96, height: 96)
                .clipShape(Circle())
                .overlay(Circle().stroke(Color.white.opacity(0.24), lineWidth: 1.2))
        } else if let url = URL(string: draft.selfieUrl), !draft.selfieUrl.isEmpty {
            AsyncImage(url: url) { phase in
                switch phase {
                case .success(let image):
                    image.resizable().scaledToFill()
                default:
                    Color.white.opacity(0.08)
                }
            }
            .frame(width: 96, height: 96)
            .clipShape(Circle())
            .overlay(Circle().stroke(Color.white.opacity(0.24), lineWidth: 1.2))
        }
#elseif canImport(AppKit)
        if let selfie {
            Image(nsImage: selfie)
                .resizable()
                .scaledToFill()
                .frame(width: 96, height: 96)
                .clipShape(Circle())
                .overlay(Circle().stroke(Color.white.opacity(0.24), lineWidth: 1.2))
        } else if let url = URL(string: draft.selfieUrl), !draft.selfieUrl.isEmpty {
            AsyncImage(url: url) { phase in
                switch phase {
                case .success(let image):
                    image.resizable().scaledToFill()
                default:
                    Color.white.opacity(0.08)
                }
            }
            .frame(width: 96, height: 96)
            .clipShape(Circle())
            .overlay(Circle().stroke(Color.white.opacity(0.24), lineWidth: 1.2))
        }
#endif
    }

    // Full-aspect selfie for the hero header. Unlike `selfiePreview` (small
    // circular thumb), this preserves the image's actual aspect ratio inside
    // a fixed-width column so the photo reads as the person's portrait, not
    // a cropped avatar chip. Falls back to a placeholder when no selfie.
    @ViewBuilder
    private var selfieHero: some View {
        let heroWidth: CGFloat = 132
        let heroMaxHeight: CGFloat = 180
        ZStack {
#if canImport(UIKit)
            if let selfie {
                Image(uiImage: selfie)
                    .resizable()
                    .scaledToFit()
                    .frame(maxWidth: heroWidth, maxHeight: heroMaxHeight)
            } else if let url = URL(string: draft.selfieUrl), !draft.selfieUrl.isEmpty {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image):
                        image.resizable().scaledToFit()
                    case .failure:
                        Color.red.opacity(0.18)
                    default:
                        ProgressView().progressViewStyle(.circular)
                    }
                }
                .frame(maxWidth: heroWidth, maxHeight: heroMaxHeight)
            } else {
                placeholderSelfieHero(width: heroWidth)
            }
#elseif canImport(AppKit)
            if let selfie {
                Image(nsImage: selfie)
                    .resizable()
                    .scaledToFit()
                    .frame(maxWidth: heroWidth, maxHeight: heroMaxHeight)
            } else if let url = URL(string: draft.selfieUrl), !draft.selfieUrl.isEmpty {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image):
                        image.resizable().scaledToFit()
                    case .failure:
                        Color.red.opacity(0.18)
                    default:
                        ProgressView().progressViewStyle(.circular)
                    }
                }
                .frame(maxWidth: heroWidth, maxHeight: heroMaxHeight)
            } else {
                placeholderSelfieHero(width: heroWidth)
            }
#endif
        }
        .frame(width: heroWidth)
        .overlay(Rectangle().stroke(Color.white.opacity(0.24), lineWidth: 1.2))
    }

    private func placeholderSelfieHero(width: CGFloat) -> some View {
        VStack(spacing: 6) {
            Image(systemName: "person.crop.rectangle")
                .font(.system(size: 38, weight: .light))
                .foregroundStyle(.white.opacity(0.32))
            Text("no selfie")
                .font(.system(size: 11, weight: .bold))
                .foregroundStyle(.white.opacity(0.42))
        }
        .frame(width: width, height: width * 4 / 3)
        .background(Color.white.opacity(0.06))
    }

    // Rendered K1L0 identity (cloak+helmet) for the hero header. Prefers
    // cloakUrl since that's the full character render; falls back to avatarUrl.
    // Placeholder shows a helmet glyph until the user saves and the pipeline
    // produces a render.
    @ViewBuilder
    private var renderedHero: some View {
        let heroWidth: CGFloat = 132
        let heroMaxHeight: CGFloat = 180
        let renderedUrl = draft.cloakUrl.isEmpty ? draft.avatarUrl : draft.cloakUrl
        ZStack {
            if let url = URL(string: renderedUrl), !renderedUrl.isEmpty {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image):
                        image.resizable().scaledToFit()
                    case .failure:
                        Color.red.opacity(0.18)
                    default:
                        ProgressView().progressViewStyle(.circular)
                    }
                }
                .frame(maxWidth: heroWidth, maxHeight: heroMaxHeight)
            } else {
                placeholderRenderedHero(width: heroWidth)
            }

            if saveStore.isSaving {
                ZStack {
                    Color.black.opacity(0.68)
                    VStack(spacing: 8) {
                        ProgressView()
                            .tint(.white)
                            .scaleEffect(0.8)
                        Text("regenerating")
                            .font(.system(size: 10, weight: .black, design: .monospaced))
                            .foregroundStyle(.white)
                            .multilineTextAlignment(.center)
                    }
                    .padding(4)
                }
            }
        }
        .frame(width: heroWidth)
        .overlay(Rectangle().stroke(Color.white.opacity(renderedUrl.isEmpty ? 0.14 : 0.24), lineWidth: 1.2))
    }

    @ViewBuilder
    private var renderedIdentityFull: some View {
        let renderedUrl = draft.cloakUrl.isEmpty ? draft.avatarUrl : draft.cloakUrl
        let thumbWidth: CGFloat = 132
        let thumbHeight: CGFloat = 180
        ZStack {
            if let url = URL(string: renderedUrl), !renderedUrl.isEmpty {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image):
                        image.resizable().scaledToFit()
                    case .failure:
                        Color.red.opacity(0.18)
                    default:
                        ProgressView().progressViewStyle(.circular)
                    }
                }
                .frame(maxWidth: thumbWidth, maxHeight: thumbHeight)
            } else {
                placeholderRenderedHero(width: thumbWidth)
            }

            if saveStore.isSaving {
                ZStack {
                    Color.black.opacity(0.68)
                    VStack(spacing: 8) {
                        ProgressView()
                            .tint(.white)
                            .scaleEffect(0.9)
                        Text("regenerating")
                            .font(.system(size: 11, weight: .black, design: .monospaced))
                            .foregroundStyle(.white)
                            .multilineTextAlignment(.center)
                    }
                    .padding(6)
                }
            }
        }
        .frame(width: thumbWidth, height: thumbHeight)
        .overlay(Rectangle().stroke(Color.white.opacity(renderedUrl.isEmpty ? 0.14 : 0.24), lineWidth: 1.2))
        .background(Color.white.opacity(0.035))
        .frame(maxWidth: .infinity, alignment: .center)
    }

    private func placeholderRenderedHero(width: CGFloat) -> some View {
        VStack(spacing: 6) {
            Image(systemName: "person.crop.square.filled.and.at.rectangle")
                .font(.system(size: 38, weight: .light))
                .foregroundStyle(.white.opacity(0.32))
            Text("renders on save")
                .font(.system(size: 11, weight: .bold))
                .foregroundStyle(.white.opacity(0.42))
        }
        .frame(width: width, height: width * 4 / 3)
        .background(Color.white.opacity(0.06))
    }

    private func identityPreview(title: String, urlString: String) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            ZStack {
                if let url = URL(string: urlString), !urlString.isEmpty {
                    AsyncImage(url: url) { phase in
                        switch phase {
                        case .success(let image):
                            image.resizable().scaledToFill()
                        case .failure:
                            Color.red.opacity(0.18)
                        default:
                            ProgressView().progressViewStyle(.circular)
                        }
                    }
                } else {
                    Color.white.opacity(0.08)
                    Text("pending")
                        .font(.system(size: 11, weight: .bold))
                        .foregroundStyle(.white.opacity(0.42))
                }
            }
            .frame(width: 92, height: 112)
            .clipped()
            .overlay(Rectangle().stroke(Color.white.opacity(urlString.isEmpty ? 0.14 : 0.24), lineWidth: 1.2))

            Text(title)
                .font(.system(size: 11, weight: .black))
                .foregroundStyle(.white.opacity(0.72))
        }
    }

#if canImport(UIKit)
    private func selfieButton(_ title: String, source: UIImagePickerController.SourceType) -> some View {
        Button {
            guard UIImagePickerController.isSourceTypeAvailable(source) else {
                saveStore.status = source == .camera ? "camera unavailable." : "photo library unavailable."
                return
            }
            selfiePickerRequest = PhotoPickerRequest(source: source)
        } label: {
            Text("[ \(title) ]")
                .font(.system(size: 13, weight: .black))
                .foregroundStyle(.white)
                .frame(maxWidth: .infinity, minHeight: 42)
                .overlay(Rectangle().stroke(Color.white.opacity(0.40), lineWidth: 1))
        }
        .buttonStyle(.plain)
    }
#endif

#if canImport(AppKit)
    private func macSelectSelfie() {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        panel.canChooseFiles = true
        panel.allowedFileTypes = ["png", "jpg", "jpeg", "heic", "gif", "tiff", "bmp"]
        guard panel.runModal() == .OK, let url = panel.url else { return }
        let image = NSImage(contentsOf: url)
        let dest = URL(fileURLWithPath: NSTemporaryDirectory())
            .appendingPathComponent("k1l0-selfie-\(UUID().uuidString).jpg")
        if let image,
           let tiff = image.tiffRepresentation,
           let rep = NSBitmapImageRep(data: tiff),
           let jpeg = rep.representation(using: .jpeg, properties: [.compressionFactor: 0.86]),
           (try? jpeg.write(to: dest, options: .atomic)) != nil {
            selfie = image
            draft.selfiePath = dest.path
        } else {
            selfie = image
            draft.selfiePath = url.path
        }
        NativeUserEditorStore.save(draft)
        saveStore.status = "selfie attached."
    }
#endif

    private func loadSelfiePreview() {
        guard !draft.selfiePath.isEmpty else { return }
#if canImport(UIKit)
        selfie = UIImage(contentsOfFile: draft.selfiePath)
#elseif canImport(AppKit)
        selfie = NSImage(contentsOfFile: draft.selfiePath)
#endif
    }

    private func save() {
        NativeUserEditorStore.save(draft)
        let payload: [String: String] = [
            "name": draft.name.trimmingCharacters(in: .whitespacesAndNewlines),
            "callsign": draft.callsign.trimmingCharacters(in: .whitespacesAndNewlines),
            "bio": draft.bio.trimmingCharacters(in: .whitespacesAndNewlines),
            "url": draft.url.trimmingCharacters(in: .whitespacesAndNewlines),
            "cloakDesign": draft.cloakDesign.trimmingCharacters(in: .whitespacesAndNewlines),
            "helmetDesign": draft.helmetDesign.trimmingCharacters(in: .whitespacesAndNewlines),
            "selfiePath": draft.selfiePath
        ]
        guard let data = try? JSONSerialization.data(withJSONObject: payload),
              let json = String(data: data, encoding: .utf8) else {
            saveStore.status = "save payload failed."
            return
        }
        saveStore.beginSaving()
        K1L0WeatherOverlayInstaller.saveNativeUserMetadata(json)
    }
}

private struct NativeMessagesPanel: View {
    var tabsMode: Bool = false
    let onClose: () -> Void
    @State private var transmissions: [NativeUserTransmissionItem] = []
    @State private var transmissionsStatus = "loading messages…"

    var body: some View {
        GeometryReader { geometry in
            let panelTop = geometry.safeAreaInsets.top
            ZStack(alignment: .top) {
                Color.clear.ignoresSafeArea()

                ZStack(alignment: .top) {
                    ScrollView(.vertical, showsIndicators: true) {
                        VStack(alignment: .leading, spacing: 14) {
                            Color.clear.frame(height: 54)

                            WeatherGlassCard {
                                VStack(alignment: .leading, spacing: 10) {
                                    HStack {
                                        Text("Messages")
                                            .font(.system(size: 25, weight: .bold))
                                        Spacer()
                                        Button(action: loadTransmissions) {
                                            Text("[ REFRESH ]")
                                                .font(.system(size: 11, weight: .black))
                                                .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                                        }
                                        .buttonStyle(.plain)
                                    }

                                    if transmissions.isEmpty {
                                        Text(transmissionsStatus)
                                            .font(.system(size: 13, weight: .semibold))
                                            .foregroundStyle(.white.opacity(0.62))
                                    } else {
                                        VStack(spacing: 8) {
                                            ForEach(transmissionGroups) { group in
                                                Button {
                                                    openTransmissionChain(group)
                                                } label: {
                                                    transmissionGroupRow(group)
                                                }
                                                .buttonStyle(.plain)
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        .padding(.horizontal, 20)
                        .padding(.top, 24)
                        .padding(.bottom, 42)
                    }

                    MessagesPanelHeader(tabsMode: tabsMode, onClose: onClose, onRefresh: loadTransmissions)
                }
                .frame(width: geometry.size.width)
                .frame(maxHeight: max(520, geometry.size.height - panelTop))
                .background(tabsMode ? Color.clear : Color.black.opacity(0.18), in: RoundedRectangle(cornerRadius: 28, style: .continuous))
                .padding(.top, panelTop)
            }
            .ignoresSafeArea(edges: .bottom)
        }
        .onAppear(perform: loadTransmissions)
    }

    private var transmissionGroups: [NativeTransmissionChainGroup] {
        let visibleTransmissions = transmissions.filter { item in
            let status = (item.status ?? "").trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
            let hasVisual = item.playbackVideoUrl != nil || !(item.thumbUrl?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ?? true)
            return status != "error" || hasVisual
        }
        let grouped = Dictionary(grouping: visibleTransmissions) { item in
            let root = item.rootJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            if !root.isEmpty { return root }
            let parent = item.parentJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            if !parent.isEmpty { return parent }
            return item.jobId
        }
        return grouped.map { key, items in
            NativeTransmissionChainGroup(
                id: key,
                items: items.sorted {
                    ($0.updatedAt ?? $0.createdAt ?? 0) > ($1.updatedAt ?? $1.createdAt ?? 0)
                }
            )
        }
        .sorted {
            ($0.latest.updatedAt ?? $0.latest.createdAt ?? 0) > ($1.latest.updatedAt ?? $1.latest.createdAt ?? 0)
        }
    }

    private func transmissionGroupRow(_ group: NativeTransmissionChainGroup) -> some View {
        let latest = group.latest
        return HStack(alignment: .center, spacing: 12) {
            transmissionCircleThumb(latest)
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 6) {
                    Text(transmissionDirectionLabel(latest))
                        .font(.system(size: 10, weight: .black))
                        .foregroundStyle((latest.direction ?? "sent").lowercased() == "sent" ? Color(red: 0.66, green: 1.0, blue: 0.76) : Color(red: 1.0, green: 0.84, blue: 0.38))
                    Text(humanTime(latest.updatedAt ?? latest.createdAt ?? 0))
                        .font(.system(size: 11, weight: .bold))
                        .foregroundStyle(.white.opacity(0.50))
                    Spacer(minLength: 0)
                    if group.items.count > 1 {
                        Text("\(group.items.count)")
                            .font(.system(size: 9, weight: .black))
                            .foregroundStyle(.white.opacity(0.42))
                    }
                }
                Text(transmissionTitle(latest))
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(.white)
                    .lineLimit(2)
                    .multilineTextAlignment(.leading)
                    .frame(maxWidth: .infinity, alignment: .leading)
                if let selected = latest.selectedResponse?.trimmingCharacters(in: .whitespacesAndNewlines), !selected.isEmpty {
                    Text(selected)
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.48))
                        .lineLimit(1)
                }
            }
            Image(systemName: "play.fill")
                .font(.system(size: 11, weight: .black))
                .foregroundStyle(.white.opacity(0.34))
        }
        .padding(9)
        .background(Color.white.opacity(0.045))
        .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).stroke(Color.white.opacity(0.10), lineWidth: 1))
        .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
    }

    private func transmissionDirectionLabel(_ item: NativeUserTransmissionItem) -> String {
        let parent = item.parentJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if isOwnTransmission(item) {
            return parent.isEmpty ? "YOU SENT" : "YOU REPLIED"
        }
        let actor = transmissionActorLabel(item).uppercased()
        return parent.isEmpty ? "\(actor) SENT" : "\(actor) REPLIED"
    }

    private func transmissionActorLabel(_ item: NativeUserTransmissionItem) -> String {
        let display = item.ownerDisplayName?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !display.isEmpty { return display }
        let name = item.ownerName?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !name.isEmpty { return name }
        let callsign = item.ownerCallsign?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !callsign.isEmpty { return callsign }
        let owner = item.ownerUserId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if owner.count > 8 { return String(owner.prefix(8)) }
        return owner.isEmpty ? "K1L0" : owner
    }

    private func isOwnTransmission(_ item: NativeUserTransmissionItem) -> Bool {
        let direction = (item.direction ?? "").trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        if direction == "sent" { return true }
        guard let current = currentNativeUserId() else { return false }
        let owner = item.ownerUserId?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() ?? ""
        return !owner.isEmpty && owner == current.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
    }

    private func transmissionCircleThumb(_ item: NativeUserTransmissionItem) -> some View {
        ZStack {
            Circle().fill(Color.white.opacity(0.07))
            if let raw = item.thumbUrl, let url = URL(string: raw) {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image): image.resizable().scaledToFill()
                    case .failure: Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.yellow)
                    default: ProgressView().tint(.white)
                    }
                }
            }
        }
        .frame(width: 54, height: 54)
        .clipShape(Circle())
        .overlay(Circle().stroke(Color.white.opacity(0.24), lineWidth: 1.2))
    }

    private func transmissionTitle(_ item: NativeUserTransmissionItem) -> String {
        let plot = item.responsePlot?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !plot.isEmpty { return cleanedTransmissionTitle(plot, item: item) }
        let status = item.status?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return status.isEmpty ? "Transmission" : status
    }

    private func cleanedTransmissionTitle(_ raw: String, item: NativeUserTransmissionItem) -> String {
        var title = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        title = title.replacingOccurrences(of: #"(?i)^\s*the person(?:\s+responds)?\s*:\s*"#, with: "", options: .regularExpression)
        let actor = transmissionActorLabel(item).trimmingCharacters(in: .whitespacesAndNewlines)
        if !actor.isEmpty {
            let escapedActor = NSRegularExpression.escapedPattern(for: actor)
            title = title.replacingOccurrences(of: #"(?i)^\s*\#(escapedActor)(?:\s+responds|\s+replied)?\s*:\s*"#, with: "", options: .regularExpression)
        }
        title = title.replacingOccurrences(of: #"(?i)^\s*[A-Z][A-Za-z0-9 ._'’-]{1,42}\s+(?:responds|replied)\s*:\s*"#, with: "", options: .regularExpression)
        return title.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func humanTime(_ raw: Double) -> String {
        guard raw > 0 else { return "now" }
        let seconds = raw > 9_999_999_999 ? raw / 1000.0 : raw
        let elapsed = max(0, Date().timeIntervalSince1970 - seconds)
        if elapsed < 60 { return "now" }
        if elapsed < 3600 { return "\(Int(elapsed / 60))m ago" }
        if elapsed < 86400 { return "\(Int(elapsed / 3600))h ago" }
        if elapsed < 604800 { return "\(Int(elapsed / 86400))d ago" }
        let formatter = DateFormatter()
        formatter.dateFormat = "MMM d"
        return formatter.string(from: Date(timeIntervalSince1970: seconds))
    }

    private func transmissionClip(_ item: NativeUserTransmissionItem) -> K1L0TransmissionClip {
        let parentJob = item.parentJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return K1L0TransmissionClip(
            videoURL: item.playbackVideoUrl.flatMap { URL(string: $0) },
            imageURL: item.thumbUrl.flatMap { URL(string: $0) },
            audioURL: item.audioUrl.flatMap { URL(string: $0) },
            responsePlot: item.responsePlot?.trimmingCharacters(in: .whitespacesAndNewlines) ?? "",
            responseOptions: item.responseOptions ?? [],
            selectedResponse: item.selectedResponse?.trimmingCharacters(in: .whitespacesAndNewlines) ?? "",
            sourceJobId: item.jobId,
            sourceUserId: item.ownerUserId ?? "",
            sourceName: item.ownerDisplayName ?? item.ownerName ?? item.ownerCallsign ?? "",
            allowsResponse: !parentJob.isEmpty && !isOwnTransmission(item)
        )
    }

    private func openTransmissionChain(_ group: NativeTransmissionChainGroup) {
        var clips = group.orderedItems
            .map { transmissionClip($0) }
            .filter { $0.videoURL != nil || $0.imageURL != nil }
        guard let first = clips.first else { return }
        let latest = group.latest
        let viewerIsOriginalAuthor = group.orderedItems.first.map { isOwnTransmission($0) } ?? false
        let latestResponseOptions = clips.indices.reversed()
            .first(where: { clips[$0].allowsResponse })
            .map { clips[$0].responseOptions } ?? []
        K1L0TransmissionResultStore.shared.current = K1L0TransmissionResult(
            status: latest.status ?? "ready",
            imageURL: first.imageURL,
            videoURL: first.videoURL,
            audioURL: first.audioURL,
            lyrics: "",
            responsePlot: first.responsePlot,
            responseOptions: latestResponseOptions,
            clips: clips,
            allowsResponseOptions: !viewerIsOriginalAuthor && clips.contains { $0.allowsResponse },
            allowsTextResponse: clips.contains { $0.allowsResponse },
            selectedResponse: first.selectedResponse
        )
    }

    private func loadTransmissions() {
        guard let userId = currentNativeUserId(), !userId.isEmpty else {
            transmissions = []
            transmissionsStatus = "not signed in."
            return
        }
        transmissionsStatus = "loading messages…"
        fetchTransmissions(userId: userId, apiIndex: 0)
    }

    private func fetchTransmissions(userId: String, apiIndex: Int) {
        let candidates = [
            "https://api-tunnel.kilo.gallery",
            "http://192.168.40.34:3000",
            "http://fred.local:3000",
            "https://api.kilomeme.com"
        ]
        guard apiIndex < candidates.count else {
            DispatchQueue.main.async {
                transmissions = []
                transmissionsStatus = "messages unavailable."
            }
            return
        }
        let safeUser = userId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? userId
        guard let url = URL(string: "\(candidates[apiIndex])/api/k1l0/v2/my-transmissions?userId=\(safeUser)") else {
            fetchTransmissions(userId: userId, apiIndex: apiIndex + 1)
            return
        }
        URLSession.shared.dataTask(with: url) { data, response, _ in
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            guard code == 200,
                  let data,
                  let decoded = try? JSONDecoder().decode(NativeUserTransmissionResponse.self, from: data),
                  decoded.ok
            else {
                fetchTransmissions(userId: userId, apiIndex: apiIndex + 1)
                return
            }
            DispatchQueue.main.async {
                transmissions = decoded.transmissions
                transmissionsStatus = decoded.transmissions.isEmpty ? "no messages yet." : ""
            }
        }.resume()
    }

    private func currentNativeUserId() -> String? {
        let defaults = UserDefaults.standard
        for key in ["FirebaseUserId", "K1L0UserId", "DeviceID", "deviceID"] {
            let value = defaults.string(forKey: key) ?? ""
            let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
            if !trimmed.isEmpty { return trimmed }
        }
        return nil
    }
}

private struct MessagesPanelHeader: View {
    var tabsMode: Bool = false
    let onClose: () -> Void
    let onRefresh: () -> Void

    var body: some View {
        VStack(spacing: 5) {
            if !tabsMode {
                RoundedRectangle(cornerRadius: 3, style: .continuous)
                    .fill(Color.white.opacity(0.34))
                    .frame(width: 44, height: 5)
                    .padding(.top, 8)
            }
            ZStack {
                Text("Messages")
                    .font(.system(size: 20, weight: .black, design: .rounded))
                    .foregroundStyle(.white)
                    .frame(maxWidth: .infinity, alignment: .center)
                HStack {
                    if !tabsMode {
                        Button(action: onClose) {
                            Image(systemName: "xmark")
                                .font(.system(size: 15, weight: .black))
                                .foregroundStyle(.white)
                                .frame(width: 38, height: 38)
                        }
                        .buttonStyle(.plain)
                    } else {
                        Color.clear.frame(width: 38, height: 38)
                    }
                    Spacer()
                    Button(action: onRefresh) {
                        Image(systemName: "arrow.clockwise")
                            .font(.system(size: 15, weight: .black))
                            .foregroundStyle(.white)
                            .frame(width: 38, height: 38)
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding(.horizontal, 16)
            .padding(.bottom, 10)
        }
        .frame(maxWidth: .infinity)
        .background(Color.black.opacity(0.001))
        .contentShape(Rectangle())
        .overlay(Rectangle().fill(Color.white.opacity(tabsMode ? 0 : 0.08)).frame(height: 1), alignment: .bottom)
        .gesture(
            DragGesture(minimumDistance: 14)
                .onEnded { value in
                    guard !tabsMode else { return }
                    if value.translation.height > 70 && abs(value.translation.width) < value.translation.height {
                        onClose()
                    }
                }
        )
    }
}

private struct NativeTransmissionPanel: View {
    @ObservedObject var data: K1L0OverlayDataModel
    let elements: [OverlayElement]
    var tabsMode: Bool = false
    let onClose: () -> Void
    @ObservedObject private var activeTransmission = K1L0ActiveTransmissionStore.shared

#if canImport(UIKit)
    @State private var selectedPhoto: UIImage?
    @State private var photoPickerRequest: PhotoPickerRequest? = nil
#elseif canImport(AppKit)
    @State private var selectedPhoto: NSImage?
#endif
    @State private var selectedPhotoPath = ""
    @State private var message = ""
    @State private var status = "add an image and say what you are up to."
    private let defaultMood = "live"

    private var transmitterStateText: String {
        let snapshot = activeTransmission.snapshot
        guard snapshot.active else { return "CREATE TRANSMISSION" }
        if !snapshot.videoUrl.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { return "TRANSMITTING" }
        return "BUILDING TRANSMISSION"
    }

    var body: some View {
        GeometryReader { geometry in
            let panelTop = geometry.safeAreaInsets.top
            let panelBottom = max(88, geometry.safeAreaInsets.bottom + 84)
            let panelHeight = max(520, geometry.size.height - panelTop - panelBottom)
            let showingFullscreenTransmission = activeTransmission.snapshot.active
                && !activeTransmission.snapshot.videoUrl.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            ZStack(alignment: .top) {
                Color.clear.ignoresSafeArea()

                if showingFullscreenTransmission {
                    Color.black.ignoresSafeArea()
                    ActiveTransmissionTerminal(
                        snapshot: activeTransmission.snapshot,
                        availableHeight: geometry.size.height,
                        onStop: { activeTransmission.stop() },
                        onFailureReset: { restoreFailedDraft(activeTransmission.snapshot) },
                        fullscreenPlayer: true,
                        onClose: onClose
                    )
                    .frame(width: geometry.size.width, height: geometry.size.height, alignment: .top)
                    .background(Color.black)
                    .ignoresSafeArea()
                } else {
                ZStack(alignment: .top) {
                    // ScrollView so SwiftUI's automatic keyboard-avoidance can
                    // inset content (push the message field above the keyboard
                    // when it slides in). Without a scroll container the field
                    // sits behind the keyboard.
                    ScrollView(.vertical, showsIndicators: false) {
                        VStack(alignment: .leading, spacing: 10) {
                            TransmitterPanelHeader(
                                state: transmitterStateText,
                                isActive: activeTransmission.snapshot.active,
                                tabsMode: tabsMode,
                                onStop: { activeTransmission.stop() },
                                onClose: onClose
                            )

                            if activeTransmission.snapshot.active {
                                ActiveTransmissionTerminal(
                                    snapshot: activeTransmission.snapshot,
                                    availableHeight: max(360, panelHeight - 62),
                                    onStop: { activeTransmission.stop() },
                                    onFailureReset: { restoreFailedDraft(activeTransmission.snapshot) }
                                )
                            } else {
                                WeatherGlassCard {
                                    VStack(alignment: .leading, spacing: 8) {
                                        Text("Add an image to transmit")
                                            .font(.system(size: 17, weight: .bold))
                                            .frame(maxWidth: .infinity, alignment: .center)
#if canImport(UIKit)
                                        HStack(alignment: .center, spacing: 10) {
                                            transmitterPhotoButton("Camera", systemImage: "camera.fill", source: .camera)
                                            transmitterPhotoButton("Photo", systemImage: "photo.on.rectangle.angled", source: .photoLibrary)
                                        }
                                        if let selectedPhoto {
                                            Image(uiImage: selectedPhoto)
                                                .resizable()
                                                .scaledToFill()
                                                .frame(maxWidth: .infinity)
                                                .frame(height: 96)
                                                .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
                                                .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).stroke(Color.green.opacity(0.85), lineWidth: 1.2))
                                        }
#elseif canImport(AppKit)
                                        Button {
                                            macSelectPhoto()
                                        } label: {
                                            Label("Photo", systemImage: "photo.on.rectangle.angled")
                                                .font(.system(size: 14, weight: .black))
                                                .foregroundStyle(.white)
                                                .frame(maxWidth: .infinity, minHeight: 44)
                                                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
                                                .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).stroke(Color.white.opacity(0.30), lineWidth: 1))
                                        }
                                        .buttonStyle(.plain)
                                        if let selectedPhoto {
                                            Image(nsImage: selectedPhoto)
                                                .resizable()
                                                .scaledToFill()
                                                .frame(width: 58, height: 58)
                                                .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                                                .overlay(RoundedRectangle(cornerRadius: 8, style: .continuous).stroke(Color.green.opacity(0.85), lineWidth: 1.2))
                                        }
#endif
                                        Text(selectedPhotoPath.isEmpty ? "no photo attached" : "photo attached")
                                            .font(.system(size: 13, weight: .semibold))
                                            .foregroundStyle(selectedPhotoPath.isEmpty ? .white.opacity(0.54) : Color(red: 0.66, green: 1.0, blue: 0.76))
                                    }
                                }

                                Text("What are you up to?")
                                    .font(.system(size: 17, weight: .bold))
                                    .foregroundStyle(.white)

                                messageField
                                    .font(.system(size: 15, weight: .semibold))
                                    .foregroundStyle(.white)
                                    .padding(12)
                                    .background(Color.white.opacity(0.08))
                                    .overlay(Rectangle().stroke(Color.white.opacity(0.18), lineWidth: 1))

                                Text(status)
                                    .font(.system(size: 13, weight: .semibold))
                                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.88))

                                Button {
                                    transmit()
                                } label: {
                                    HStack(spacing: 10) {
                                        Image(systemName: "antenna.radiowaves.left.and.right")
                                            .font(.system(size: 17, weight: .black))
                                        Text("Start Transmitting")
                                            .font(.system(size: 17, weight: .black))
                                    }
                                    .foregroundStyle(.white)
                                    .frame(maxWidth: .infinity, minHeight: 56)
                                    .background(canTransmit ? Color.green.opacity(0.94) : Color.white.opacity(0.07), in: Capsule())
                                    .overlay(Capsule().stroke(canTransmit ? Color.green.opacity(0.95) : Color.white.opacity(0.18), lineWidth: 1.2))
                                }
                                .buttonStyle(.plain)
                                .disabled(!canTransmit)
                                .opacity(canTransmit ? 1 : 0.72)
                            }
                        }
                        .padding(.horizontal, activeTransmission.snapshot.active ? 0 : 20)
                        .padding(.top, 0)
                        .padding(.bottom, activeTransmission.snapshot.active ? 4 : 38)
                    }
                    // Tap-outside-the-keyboard to dismiss feels right for a
                    // single-field panel; user can also drag to dismiss.
                    .scrollDismissesKeyboardCompat()
                }
                .frame(maxWidth: .infinity)
                .frame(height: panelHeight)
                .background(tabsMode ? Color.clear : Color.black.opacity(activeTransmission.snapshot.active ? 0.02 : 0.14), in: RoundedRectangle(cornerRadius: 28, style: .continuous))
                .overlay(
                    RoundedRectangle(cornerRadius: 28, style: .continuous)
                        .stroke(Color.white.opacity(tabsMode ? 0 : (activeTransmission.snapshot.active ? 0.06 : 0.14)), lineWidth: 1)
                )
                .padding(.top, panelTop)
                .padding(.bottom, panelBottom)
                .gesture(
                    DragGesture(minimumDistance: 18)
                        .onEnded { value in
                            guard !tabsMode else { return }
                            if value.translation.height > 80 {
                                onClose()
                            }
                        }
                )
                }
            }
            // ignoresSafeArea(edges: .bottom) was here — it disables the
            // keyboard inset and hides the message field behind the keyboard.
            // Let the default safe-area behavior push the panel up.
        }
        .onAppear {
            data.refreshTransmissionState(clearStaleCache: true)
        }
#if canImport(UIKit)
        .sheet(item: $photoPickerRequest) { request in
            NativePhotoPicker(sourceType: request.source) { image, path in
                if let image, let path {
                    selectedPhoto = image
                    selectedPhotoPath = path
                    status = "photo attached."
                }
                photoPickerRequest = nil
            }
            .ignoresSafeArea()
        }
#endif
    }

    @ViewBuilder
    private var messageField: some View {
#if canImport(UIKit)
        TextField("What are you up to?", text: $message)
            .textInputAutocapitalization(.sentences)
            .transmitterKeyboardDoneToolbar()
#else
        TextField("What are you up to?", text: $message)
            .textFieldStyle(.plain)
#endif
    }

    private var canTransmit: Bool {
        !selectedPhotoPath.isEmpty && !message.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

#if canImport(AppKit)
    private func macSelectPhoto() {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        panel.canChooseFiles = true
        panel.allowedFileTypes = ["png", "jpg", "jpeg", "heic", "gif", "tiff", "bmp"]
        guard panel.runModal() == .OK, let url = panel.url else { return }
        let image = NSImage(contentsOf: url)
        let dest = URL(fileURLWithPath: NSTemporaryDirectory())
            .appendingPathComponent("k1l0-transmit-\(UUID().uuidString).jpg")
        if let image,
           let tiff = image.tiffRepresentation,
           let rep = NSBitmapImageRep(data: tiff),
           let jpeg = rep.representation(using: .jpeg, properties: [.compressionFactor: 0.86]),
           (try? jpeg.write(to: dest, options: .atomic)) != nil {
            selectedPhoto = image
            selectedPhotoPath = dest.path
        } else {
            selectedPhoto = image
            selectedPhotoPath = url.path
        }
        status = "photo attached."
    }
#endif

#if canImport(UIKit)
    private func transmitterPhotoButton(_ title: String, systemImage: String, source: UIImagePickerController.SourceType) -> some View {
        Button {
            guard UIImagePickerController.isSourceTypeAvailable(source) else {
                status = source == .camera ? "camera unavailable." : "photo library unavailable."
                return
            }
            photoPickerRequest = PhotoPickerRequest(source: source)
        } label: {
            Label(title, systemImage: systemImage)
                .font(.system(size: 14, weight: .black))
                .foregroundStyle(.white)
                .frame(maxWidth: .infinity, minHeight: 44)
                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).stroke(Color.white.opacity(0.30), lineWidth: 1))
        }
        .buttonStyle(.plain)
    }
#endif

    private func transmit() {
        guard !selectedPhotoPath.isEmpty else {
            status = "attach a photo first."
            return
        }
        let cleanMessage = message.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleanMessage.isEmpty else {
            status = "answer what you are doing."
            return
        }
        let locationSummary = data.cityText.trimmingCharacters(in: .whitespacesAndNewlines)
        let weatherSummary = data.weatherText.trimmingCharacters(in: .whitespacesAndNewlines)
        K1L0ActiveTransmissionStore.shared.start(
            photoPath: selectedPhotoPath,
            message: cleanMessage,
            mood: defaultMood,
            locationSummary: locationSummary,
            weatherSummary: weatherSummary
        )
        status = "transmitting..."
        data.submitNativeTransmission(photoPath: selectedPhotoPath, message: cleanMessage, mood: defaultMood) { nextStatus in
            status = nextStatus
        }
    }

    private func restoreFailedDraft(_ snapshot: K1L0ActiveTransmissionSnapshot) {
        message = snapshot.message
        selectedPhotoPath = snapshot.photoPath
#if canImport(UIKit)
        if !snapshot.photoPath.isEmpty {
            selectedPhoto = UIImage(contentsOfFile: snapshot.photoPath)
        }
#elseif canImport(AppKit)
        if !snapshot.photoPath.isEmpty {
            selectedPhoto = NSImage(contentsOfFile: snapshot.photoPath)
        }
#endif
        status = "couldn't establish signal. edit or try again."
        activeTransmission.stop()
    }
}

private struct TransmitterPanelHeader: View {
    let state: String
    let isActive: Bool
    var tabsMode: Bool = false
    let onStop: () -> Void
    let onClose: () -> Void

    var body: some View {
        VStack(spacing: 5) {
            if !tabsMode {
                RoundedRectangle(cornerRadius: 3, style: .continuous)
                    .fill(Color.white.opacity(0.34))
                    .frame(width: 44, height: 5)
                    .padding(.top, 8)
            }
            ZStack {
                VStack(spacing: 2) {
                    Text("TRANSMITTER")
                        .font(.system(size: 18, weight: .black, design: .rounded))
                        .foregroundStyle(.white)
                    Text(state)
                        .font(.system(size: 10, weight: .black, design: .rounded))
                        .foregroundStyle(.white.opacity(0.64))
                        .lineLimit(1)
                        .minimumScaleFactor(0.72)
                }
                .frame(maxWidth: .infinity, alignment: .center)

                HStack {
                    if isActive {
                        Button(action: onStop) {
                            Text("STOP")
                                .font(.system(size: 12, weight: .black, design: .rounded))
                                .foregroundStyle(.white)
                                .padding(.horizontal, 13)
                                .frame(height: 34)
                                .background(
                                    LinearGradient(
                                        colors: [Color.red.opacity(0.86), Color.red.opacity(0.34)],
                                        startPoint: .topLeading,
                                        endPoint: .bottomTrailing
                                    ),
                                    in: Capsule()
                                )
                                .overlay(Capsule().stroke(Color.white.opacity(0.25), lineWidth: 1))
                        }
                        .buttonStyle(.plain)
                    } else {
                        Color.clear.frame(width: 64, height: 34)
                    }
                    Spacer()
                    if !tabsMode {
                        Button(action: onClose) {
                            Image(systemName: "xmark")
                                .font(.system(size: 15, weight: .black))
                                .foregroundStyle(.white)
                                .frame(width: 38, height: 38)
                        }
                        .buttonStyle(.plain)
                    } else {
                        Color.clear.frame(width: 38, height: 38)
                    }
                }
            }
            .padding(.horizontal, 16)
            .padding(.bottom, 10)
        }
        .frame(maxWidth: .infinity)
        .background(Color.clear)
        .contentShape(Rectangle())
    }
}

private struct TransmissionBuildingArtwork: View {
    let imageUrl: String
    @State private var revealProgress = 0.0

    private var resolvedURL: URL? {
        let value = imageUrl.trimmingCharacters(in: .whitespacesAndNewlines)
        return value.isEmpty ? nil : URL(string: value)
    }

    var body: some View {
        GeometryReader { geometry in
            ZStack {
                CyberBuildingPixels()
                if let url = resolvedURL {
                    AsyncImage(url: url) { phase in
                        if case .success(let image) = phase {
                            image
                                .resizable()
                                .scaledToFill()
                                .frame(width: geometry.size.width, height: geometry.size.height)
                                .mask(PixelDiffusionMask(progress: revealProgress))
                                .onAppear {
                                    revealProgress = 0
                                    withAnimation(.linear(duration: 1.65)) { revealProgress = 1 }
                                }
                        }
                    }
                }
                VStack(spacing: 7) {
                    Text(revealProgress > 0 ? "TUNING TRANSMISSION" : "BUILDING TRANSMISSION")
                        .font(.system(size: 20, weight: .black, design: .monospaced))
                    Text("PLEASE STANDBY")
                        .font(.system(size: 12, weight: .black, design: .monospaced))
                        .tracking(3)
                }
                .foregroundStyle(.white)
                .padding(.horizontal, 22)
                .padding(.vertical, 16)
                .background(Color.black.opacity(0.46), in: RoundedRectangle(cornerRadius: 5))
                .overlay(RoundedRectangle(cornerRadius: 5).stroke(Color.white.opacity(0.28), lineWidth: 1))
            }
            .frame(width: geometry.size.width, height: geometry.size.height)
            .clipped()
        }
        .id(imageUrl)
    }
}

private struct CyberBuildingPixels: View {
    private let colors: [Color] = [
        Color(red: 1.0, green: 0.08, blue: 0.48),
        Color(red: 0.04, green: 0.84, blue: 1.0),
        Color(red: 0.55, green: 0.16, blue: 1.0),
        Color(red: 1.0, green: 0.72, blue: 0.05),
        Color(red: 0.08, green: 1.0, blue: 0.58),
        Color(red: 0.06, green: 0.04, blue: 0.18)
    ]

    var body: some View {
        TimelineView(.periodic(from: .now, by: 0.055)) { timeline in
            Canvas { context, size in
                let tick = Int(timeline.date.timeIntervalSinceReferenceDate * 18)
                let cell = max(28.0, min(size.width, size.height) / 8.0)
                let columns = Int(ceil(size.width / cell)) + 2
                let rows = Int(ceil(size.height / cell)) + 2
                context.fill(Path(CGRect(origin: .zero, size: size)), with: .color(Color(red: 0.02, green: 0.01, blue: 0.08)))
                for row in -1..<rows {
                    for column in -1..<columns {
                        let rowSeed: Int = row * 73
                        let columnSeed: Int = column * 131
                        let timeSeed: Int = tick * 47
                        let combinedSeed: Int = rowSeed + columnSeed + timeSeed
                        let seed: Int = abs(combinedSeed % 997)
                        let color = colors[seed % colors.count]
                        let xPhase: Int = (seed / 7) + (tick * (row + 9))
                        let yPhase: Int = (seed / 11) + (tick * (column + 7))
                        let xStep: Int = (xPhase % 5) - 2
                        let yStep: Int = (yPhase % 5) - 2
                        let xJitter: CGFloat = CGFloat(xStep) * cell * CGFloat(0.28)
                        let yJitter: CGFloat = CGFloat(yStep) * cell * CGFloat(0.22)
                        let rect = CGRect(
                            x: CGFloat(column) * cell + xJitter,
                            y: CGFloat(row) * cell + yJitter,
                            width: cell * CGFloat(1 + seed % 3),
                            height: cell * CGFloat(1 + (seed / 3) % 2)
                        )
                        context.fill(Path(rect), with: .color(color.opacity(0.52 + Double(seed % 35) / 100.0)))
                    }
                }
            }
        }
        .overlay {
            LinearGradient(colors: [Color.white.opacity(0.10), .clear, Color.black.opacity(0.22)], startPoint: .topLeading, endPoint: .bottomTrailing)
                .blendMode(.screen)
        }
        .allowsHitTesting(false)
    }
}

private struct PixelDiffusionMask: View {
    let progress: Double

    var body: some View {
        Canvas { context, size in
            let cell = max(7.0, min(size.width, size.height) / 42.0)
            let columns = Int(ceil(size.width / cell))
            let rows = Int(ceil(size.height / cell))
            for row in 0..<rows {
                for column in 0..<columns {
                    let rowHash: Int = row * 92_821
                    let columnHash: Int = column * 68_917
                    let crossHash: Int = (row * column) * 17
                    let combinedHash: Int = rowHash + columnHash + crossHash
                    let hash: Int = abs(combinedHash % 1_000)
                    if Double(hash) / 1000.0 <= progress {
                        let rect = CGRect(x: CGFloat(column) * cell, y: CGFloat(row) * cell, width: cell + 0.75, height: cell + 0.75)
                        context.fill(Path(rect), with: .color(.white))
                    }
                }
            }
        }
    }
}

private struct ActiveTransmissionTerminal: View {
    let snapshot: K1L0ActiveTransmissionSnapshot
    let availableHeight: CGFloat
    let onStop: () -> Void
    let onFailureReset: () -> Void
    // Fullscreen transmitter mode: render the exact same fullscreen chain
    // player used everywhere else (settings gear, camera-roll save, tattered
    // frame) and overlay only the pencil (tweak) and END controls.
    var fullscreenPlayer: Bool = false
    var onClose: () -> Void = {}
    @ObservedObject private var keyboard = K1L0KeyboardObserver.shared
    @State private var showingEndConfirmation = false
    @State private var showingSignalFailure = false
    @State private var showingTweakPanel = false
    @State private var tweakStatus = ""
    @State private var tweakImageUrl = ""
    @State private var tweakPhotoPrompt = ""
    @State private var tweakVideoPrompt = ""
    @State private var tweakMusicPrompt = ""
    @State private var tweakLoadedJobId = ""
    @State private var textTransform = TransmissionTextTransformStore.load()
    @AppStorage("k1lo_native_transmissionFizzyEdges") private var transmissionFizzyEdges = false
    @AppStorage("k1lo_native_transmissionFX") private var transmissionFXEnabled = true
    @AppStorage("k1lo_native_transmissionFXIntensity") private var transmissionFXIntensity = 0.5

    private var saveOverlayText: String {
        snapshot.responsePlot.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    var body: some View {
        Group {
            if fullscreenPlayer {
                fullscreenPlayerBody
            } else {
                panelBody
            }
        }
        .alert("End transmission?", isPresented: $showingEndConfirmation) {
            Button("End", role: .destructive) {
                onStop()
            }
            Button("Keep Transmitting", role: .cancel) { }
        } message: {
            Text("This removes this transmission from the transmitter. It will not come back here.")
        }
        .alert("Couldn't Establish Signal", isPresented: $showingSignalFailure) {
            Button("Try Again") {
                onFailureReset()
            }
        } message: {
            Text("Please try again.")
        }
        .onAppear {
            let buildAge = Date().timeIntervalSince1970 - snapshot.startedAt
            showingSignalFailure = !snapshot.error.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                || (snapshot.videoUrl.isEmpty && buildAge >= 300)
            if showingTweakPanel {
                loadTweakDetails()
            }
        }
        .onChange(of: snapshot.jobId) { _ in
            if showingTweakPanel {
                loadTweakDetails(force: true)
            }
        }
        .onChange(of: snapshot.error) { error in
            if !error.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                showingSignalFailure = true
            }
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
    }

    // The live transmission wrapped as a result so the shared fullscreen chain
    // player renders it exactly like a received transmission. No response
    // UI — the sender doesn't respond to their own live signal.
    private var livePlayerResult: K1L0TransmissionResult {
        let videoURL = URL(string: snapshot.videoUrl)
        let audioURL = snapshot.audioUrl.isEmpty ? nil : URL(string: snapshot.audioUrl)
        let imageURL = snapshot.imageUrl.isEmpty ? nil : URL(string: snapshot.imageUrl)
        let clip = K1L0TransmissionClip(
            videoURL: videoURL,
            imageURL: imageURL,
            audioURL: audioURL,
            responsePlot: snapshot.responsePlot,
            responseOptions: [],
            selectedResponse: "",
            sourceJobId: snapshot.jobId
        )
        return K1L0TransmissionResult(
            status: "live",
            imageURL: imageURL,
            videoURL: videoURL,
            audioURL: audioURL,
            lyrics: "",
            responsePlot: snapshot.responsePlot,
            responseOptions: [],
            jobId: snapshot.jobId,
            clips: [clip]
        )
    }

    private var fullscreenPlayerBody: some View {
        ZStack(alignment: .topLeading) {
            TransmissionResultPanel(
                result: livePlayerResult,
                onSelectOption: { _ in },
                onClose: onClose
            )
            // Rebuild the player when a tweak regenerates the video or music.
            .id("live-\(snapshot.jobId)-\(snapshot.videoUrl)-\(snapshot.audioUrl)")

            // Pencil + END pinned top-left, mirroring the player's own
            // top-right save/settings/close row.
            HStack(spacing: 10) {
                Button {
                    withAnimation(.easeInOut(duration: 0.18)) {
                        showingTweakPanel.toggle()
                    }
                    if showingTweakPanel {
                        loadTweakDetails()
                    }
                } label: {
                    Image(systemName: "pencil")
                        .font(.system(size: 16, weight: .black))
                        .foregroundStyle(.white)
                        .frame(width: 44, height: 44)
                        .background(Color.black.opacity(0.38), in: Circle())
                }
                .buttonStyle(.plain)

                Button {
                    showingEndConfirmation = true
                } label: {
                    Text("END")
                        .font(.system(size: 12, weight: .black, design: .monospaced))
                        .foregroundStyle(.white)
                        .frame(height: 44)
                        .padding(.horizontal, 14)
                        .background(Color.black.opacity(0.38), in: Capsule())
                }
                .buttonStyle(.plain)
            }
            .padding(.leading, 12)
            .padding(.top, k1l0DeviceSafeAreaInsets().top + 2)

            if showingTweakPanel {
                TransmissionTweakPanel(
                    snapshot: snapshot,
                    imageUrl: tweakImageUrl.isEmpty ? snapshot.imageUrl : tweakImageUrl,
                    photoPrompt: $tweakPhotoPrompt,
                    videoPrompt: $tweakVideoPrompt,
                    musicPrompt: $tweakMusicPrompt,
                    status: tweakStatus,
                    onClose: { withAnimation(.easeInOut(duration: 0.18)) { showingTweakPanel = false } },
                    onRefresh: { loadTweakDetails(force: true) },
                    onRegenerateImage: { regenerate(endpoint: "regen-nb", promptKey: "nbPrompt", prompt: tweakPhotoPrompt) },
                    onRegenerateVideo: { regenerate(endpoint: "regen-video", promptKey: "wanPrompt", prompt: tweakVideoPrompt) },
                    onRegenerateMusic: { regenerate(endpoint: "regen-music", promptKey: "musicPrompt", prompt: tweakMusicPrompt) }
                )
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topTrailing)
                .transition(.opacity)
            }
        }
        .ignoresSafeArea(.keyboard, edges: .bottom)
    }

    private var panelBody: some View {
        ZStack(alignment: .topTrailing) {
            VStack(spacing: 8) {
                GeometryReader { proxy in
                    // Match the fullscreen chain player exactly: a full-width
                    // 9:16 transmission frame. Do not compress it to fit the
                    // tab panel; the surrounding ScrollView can accommodate it.
                    let height = proxy.size.width * 16 / 9
                    let width = proxy.size.width
                    HStack {
                        Spacer(minLength: 0)
                        ZStack {
                            if snapshot.videoUrl.isEmpty {
                                TransmissionBuildingArtwork(imageUrl: snapshot.imageUrl)
                            } else {
                                InlineTransmissionVideoPlayer(urlString: snapshot.videoUrl, audioUrlString: snapshot.audioUrl.isEmpty ? nil : snapshot.audioUrl)
                                    .allowsHitTesting(false)
                                    .mask(TatteredEdgeMaskCanvas())
                            }
                            if !snapshot.responsePlot.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                                DraggableTransmissionTextOverlay(
                                    text: snapshot.responsePlot,
                                    transform: $textTransform,
                                    canvasSize: CGSize(width: width, height: height),
                                    allowEditing: true,
                                    useExternalTypewriter: false,
                                    // Hug the frame bottom.
                                    bottomInset: 18
                                )
                            }

                        }
                        .frame(width: width, height: height)
                        .background(Color.black.opacity(0.86))
                        .clipped()
                        Spacer(minLength: 0)
                    }
                }
                .frame(height: k1l0DeviceScreenSize().width * 16 / 9)

                HStack(spacing: 10) {
                    transmitterToolButton(label: nil, systemImage: "slider.horizontal.3") {
                        withAnimation(.easeInOut(duration: 0.18)) {
                            showingTweakPanel.toggle()
                        }
                        if showingTweakPanel {
                            loadTweakDetails()
                        }
                    }
                    Spacer()
#if canImport(UIKit)
                    if !snapshot.videoUrl.isEmpty {
                        CameraRollSaveButton(
                            videoUrlString: snapshot.videoUrl,
                            audioUrlString: snapshot.audioUrl.isEmpty ? nil : snapshot.audioUrl,
                            overlayText: saveOverlayText,
                            overlayTransform: textTransform
                        )
                    }
#endif
                }
                .padding(.horizontal, 18)
                .frame(height: 40)

                if !snapshot.error.isEmpty {
                    Text(snapshot.error)
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(.red)
                        .textSelection(.enabled)
                        .padding(.horizontal, 18)
                }
            }
            .frame(maxWidth: .infinity, maxHeight: availableHeight, alignment: .top)

            if showingTweakPanel {
                TransmissionTweakPanel(
                    snapshot: snapshot,
                    imageUrl: tweakImageUrl.isEmpty ? snapshot.imageUrl : tweakImageUrl,
                    photoPrompt: $tweakPhotoPrompt,
                    videoPrompt: $tweakVideoPrompt,
                    musicPrompt: $tweakMusicPrompt,
                    status: tweakStatus,
                    onClose: { withAnimation(.easeInOut(duration: 0.18)) { showingTweakPanel = false } },
                    onRefresh: { loadTweakDetails(force: true) },
                    onRegenerateImage: { regenerate(endpoint: "regen-nb", promptKey: "nbPrompt", prompt: tweakPhotoPrompt) },
                    onRegenerateVideo: { regenerate(endpoint: "regen-video", promptKey: "wanPrompt", prompt: tweakVideoPrompt) },
                    onRegenerateMusic: { regenerate(endpoint: "regen-music", promptKey: "musicPrompt", prompt: tweakMusicPrompt) }
                )
                .transition(.opacity)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: availableHeight, alignment: .top)
        .ignoresSafeArea(.keyboard, edges: .bottom)
    }

    private func transmitterToolButton(label: String?, systemImage: String?, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Group {
                if let systemImage {
                    Image(systemName: systemImage)
                        .font(.system(size: 15, weight: .black))
                } else {
                    Text(label ?? "")
                        .font(.system(size: 16, weight: .black, design: .monospaced))
                }
            }
            .foregroundStyle(.white)
            .frame(width: 40, height: 38)
            .background(Color.black.opacity(0.52))
            .overlay(Rectangle().stroke(Color.white.opacity(0.34), lineWidth: 1))
        }
        .buttonStyle(.plain)
    }

    private var currentNativeUserId: String {
        let defaults = UserDefaults.standard
        for key in ["FirebaseUserId", "K1L0UserId", "DeviceID", "deviceID"] {
            let value = defaults.string(forKey: key) ?? ""
            let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
            if !trimmed.isEmpty { return trimmed }
        }
        return "anon"
    }

    private var apiCandidates: [String] {
        [
            "https://api-tunnel.kilo.gallery",
            "http://192.168.40.34:3000",
            "http://fred.local:3000",
            "https://api.kilomeme.com"
        ]
    }

    private func loadTweakDetails(force: Bool = false, apiIndex: Int = 0) {
        let jobId = snapshot.jobId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !jobId.isEmpty else {
            tweakStatus = "waiting for job id."
            return
        }
        if !force, tweakLoadedJobId == jobId, !tweakPhotoPrompt.isEmpty {
            return
        }
        guard apiIndex < apiCandidates.count else {
            tweakStatus = "prompt fetch failed."
            return
        }
        let userId = currentNativeUserId
        guard let encodedUser = userId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed),
              let url = URL(string: "\(apiCandidates[apiIndex])/api/k1l0/v2/transmit/\(jobId)?userId=\(encodedUser)") else {
            loadTweakDetails(force: force, apiIndex: apiIndex + 1)
            return
        }
        tweakStatus = "loading prompts..."
        URLSession.shared.dataTask(with: URLRequest(url: url, timeoutInterval: 12)) { data, response, _ in
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            guard (200...299).contains(code),
                  let data,
                  let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  (root["ok"] as? Bool) == true
            else {
                DispatchQueue.main.async {
                    loadTweakDetails(force: force, apiIndex: apiIndex + 1)
                }
                return
            }
            let plan = root["plan"] as? [String: Any] ?? [:]
            let music = plan["music"] as? [String: Any] ?? [:]
            let audit = K1L0OverlayDataModel.transmissionAudit(from: root)
            DispatchQueue.main.async {
                tweakLoadedJobId = jobId
                tweakImageUrl = (root["stillUrl"] as? String) ?? (root["nbUrl"] as? String) ?? snapshot.imageUrl
                tweakPhotoPrompt = (plan["nb_prompt"] as? String) ?? tweakPhotoPrompt
                tweakVideoPrompt = (plan["wan_prompt"] as? String) ?? tweakVideoPrompt
                tweakMusicPrompt = (music["prompt"] as? String) ?? tweakMusicPrompt
                K1L0ActiveTransmissionStore.shared.applyAudit(
                    inputImageUrl: audit.inputImageUrl,
                    locationSummary: audit.location,
                    weatherSummary: audit.weather,
                    photoPrompt: audit.photoPrompt,
                    videoPrompt: audit.videoPrompt,
                    musicPrompt: audit.musicPrompt,
                    lyrics: audit.lyrics,
                    createdAt: k1l0NumericTimestamp(root["createdAt"]) > 0 ? k1l0NumericTimestamp(root["createdAt"]) : k1l0NumericTimestamp(root["updatedAt"])
                )
                tweakStatus = "prompts loaded."
            }
        }.resume()
    }

    private func regenerate(endpoint: String, promptKey: String, prompt: String, apiIndex: Int = 0) {
        let jobId = snapshot.jobId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !jobId.isEmpty else {
            tweakStatus = "waiting for job id."
            return
        }
        guard apiIndex < apiCandidates.count else {
            tweakStatus = "regen request failed."
            return
        }
        guard let url = URL(string: "\(apiCandidates[apiIndex])/api/k1l0/v2/\(endpoint)") else {
            regenerate(endpoint: endpoint, promptKey: promptKey, prompt: prompt, apiIndex: apiIndex + 1)
            return
        }
        var request = URLRequest(url: url, timeoutInterval: 12)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: [
            "userId": currentNativeUserId,
            "jobId": jobId,
            promptKey: prompt.trimmingCharacters(in: .whitespacesAndNewlines)
        ])
        tweakStatus = "\(endpoint) queued..."
        URLSession.shared.dataTask(with: request) { data, response, _ in
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            guard (200...299).contains(code),
                  let data,
                  let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  (root["ok"] as? Bool) == true
            else {
                DispatchQueue.main.async {
                    regenerate(endpoint: endpoint, promptKey: promptKey, prompt: prompt, apiIndex: apiIndex + 1)
                }
                return
            }
            DispatchQueue.main.async {
                tweakStatus = "\(endpoint) running."
                pollRegeneratedJob()
            }
        }.resume()
    }

    private func pollRegeneratedJob(attempt: Int = 0) {
        guard attempt < 90 else {
            tweakStatus = "regen timed out."
            return
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 5) {
            loadTweakDetails(force: true)
            let jobId = snapshot.jobId.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !jobId.isEmpty,
                  let encodedUser = currentNativeUserId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed),
                  let url = URL(string: "\(apiCandidates[0])/api/k1l0/v2/transmit/\(jobId)?userId=\(encodedUser)") else { return }
            URLSession.shared.dataTask(with: URLRequest(url: url, timeoutInterval: 12)) { data, _, _ in
                guard let data,
                      let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
                else {
                    DispatchQueue.main.async { pollRegeneratedJob(attempt: attempt + 1) }
                    return
                }
                let status = (root["status"] as? String) ?? ""
                if status == "ready" || status == "complete" {
                    let audit = K1L0OverlayDataModel.transmissionAudit(from: root)
                    let finalUrl = (root["finalUrl"] as? String) ?? ""
                    let rawVideoUrl = (root["rawVideoUrl"] as? String) ?? (root["videoUrl"] as? String) ?? finalUrl
                    let imageUrl = (root["stillUrl"] as? String) ?? (root["nbUrl"] as? String) ?? ""
                    let audioUrl = (root["audioUrl"] as? String) ?? ""
                    let responsePlot = (root["responsePlot"] as? String) ?? snapshot.responsePlot
                    let responseOptions = (root["responseOptions"] as? [String]) ?? snapshot.responseOptions
                    let payload: [String: Any] = [
                        "jobId": jobId,
                        "status": status,
                        "imageUrl": imageUrl,
                        "videoUrl": rawVideoUrl,
                        "audioUrl": audioUrl,
                        "lyrics": audit.lyrics,
                        "createdAt": k1l0NumericTimestamp(root["createdAt"]) > 0 ? k1l0NumericTimestamp(root["createdAt"]) : k1l0NumericTimestamp(root["updatedAt"]),
                        "responsePlot": responsePlot,
                        "responseOptions": responseOptions
                    ]
                    if let payloadData = try? JSONSerialization.data(withJSONObject: payload),
                       let json = String(data: payloadData, encoding: .utf8) {
                        DispatchQueue.main.async {
                            K1L0TransmissionResultStore.shared.handle(json)
                            K1L0ActiveTransmissionStore.shared.applyAudit(
                                inputImageUrl: audit.inputImageUrl,
                                locationSummary: audit.location,
                                weatherSummary: audit.weather,
                                photoPrompt: audit.photoPrompt,
                                videoPrompt: audit.videoPrompt,
                                musicPrompt: audit.musicPrompt,
                                lyrics: audit.lyrics,
                                createdAt: k1l0NumericTimestamp(root["createdAt"]) > 0 ? k1l0NumericTimestamp(root["createdAt"]) : k1l0NumericTimestamp(root["updatedAt"])
                            )
                            tweakStatus = "regen ready."
                            loadTweakDetails(force: true)
                        }
                    }
                    return
                }
                if status == "error" {
                    let error = (root["error"] as? String) ?? "regen failed"
                    DispatchQueue.main.async { tweakStatus = error }
                    return
                }
                DispatchQueue.main.async {
                    tweakStatus = status.isEmpty ? "regen running..." : status
                    pollRegeneratedJob(attempt: attempt + 1)
                }
            }.resume()
        }
    }
}

private struct TransmissionAuditPanel: View {
    let snapshot: K1L0ActiveTransmissionSnapshot

    private var rows: [(String, String)] {
        [
            ("CREATED", k1l0ReadableDateTime(snapshot.createdAt)),
            ("WEATHER", snapshot.weatherSummary),
            ("LOCATION", snapshot.locationSummary),
            ("USER PROMPT", snapshot.message),
            ("INPUT IMAGE", snapshot.inputImageUrl.isEmpty ? snapshot.photoPath : snapshot.inputImageUrl),
            ("NANO BANANA IMAGE PROMPT", snapshot.photoPrompt),
            ("VIDEO PROMPT", snapshot.videoPrompt),
            ("MUSIC PROMPT", snapshot.musicPrompt),
            ("LYRICS", snapshot.lyrics)
        ]
        .map { ($0.0, $0.1.trimmingCharacters(in: .whitespacesAndNewlines)) }
        .filter { !$0.1.isEmpty }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            HStack(spacing: 8) {
                Image(systemName: "info.circle.fill")
                    .font(.system(size: 11, weight: .black))
                Text("Used variables")
                    .font(.system(size: 11, weight: .black, design: .monospaced))
                Spacer()
                Text(snapshot.status.uppercased())
                    .font(.system(size: 10, weight: .bold, design: .monospaced))
                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.85))
            }
            .foregroundStyle(.white.opacity(0.82))

            VStack(alignment: .leading, spacing: 6) {
                ForEach(Array(rows.enumerated()), id: \.offset) { _, row in
                    VStack(alignment: .leading, spacing: 2) {
                        Text(row.0)
                            .font(.system(size: 9, weight: .black, design: .monospaced))
                            .foregroundStyle(.white.opacity(0.46))
                        Text(row.1)
                            .font(.system(size: 10, weight: .semibold, design: .monospaced))
                            .foregroundStyle(.white.opacity(0.88))
                            .lineLimit(row.0 == "LYRICS" ? 10 : 3)
                            .textSelection(.enabled)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    }
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .frame(maxWidth: .infinity)
    }
}

private struct TransmissionTweakPanel: View {
    let snapshot: K1L0ActiveTransmissionSnapshot
    let imageUrl: String
    @Binding var photoPrompt: String
    @Binding var videoPrompt: String
    @Binding var musicPrompt: String
    let status: String
    let onClose: () -> Void
    let onRefresh: () -> Void
    let onRegenerateImage: () -> Void
    let onRegenerateVideo: () -> Void
    let onRegenerateMusic: () -> Void
    @AppStorage("k1lo_native_transmissionFizzyEdges") private var transmissionFizzyEdges = false
    @AppStorage("k1lo_native_transmissionFX") private var transmissionFXEnabled = true
    @AppStorage("k1lo_native_transmissionFXIntensity") private var transmissionFXIntensity = 0.5

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text("Transmission Lab")
                    .font(.system(size: 16, weight: .black, design: .rounded))
                    .foregroundStyle(.white)
                Spacer()
                Button(action: onRefresh) {
                    Image(systemName: "arrow.clockwise")
                        .font(.system(size: 13, weight: .black))
                        .foregroundStyle(.white)
                        .frame(width: 32, height: 32)
                }
                .buttonStyle(.plain)
                Button(action: onClose) {
                    Image(systemName: "xmark")
                        .font(.system(size: 13, weight: .black))
                        .foregroundStyle(.white)
                        .frame(width: 32, height: 32)
                }
                .buttonStyle(.plain)
            }

            ScrollView(.vertical, showsIndicators: true) {
                VStack(alignment: .leading, spacing: 10) {
                    TransmissionAuditPanel(snapshot: snapshot)
                        .padding(10)
                        .background(Color.white.opacity(0.045))
                        .overlay(Rectangle().stroke(Color.white.opacity(0.12), lineWidth: 1))

                    if !imageUrl.isEmpty, let url = URL(string: imageUrl) {
                        AsyncImage(url: url) { phase in
                            switch phase {
                            case .success(let image):
                                image.resizable().scaledToFit()
                            default:
                                Rectangle().fill(Color.white.opacity(0.08))
                            }
                        }
                        .frame(maxWidth: .infinity)
                        .frame(height: 160)
                        .clipped()
                        .overlay(Rectangle().stroke(Color.white.opacity(0.18), lineWidth: 1))
                    }

                    promptEditor("PHOTO PROMPT", text: $photoPrompt, actionTitle: "REGENERATE IMAGE", action: onRegenerateImage)
                    promptEditor("VIDEO PROMPT", text: $videoPrompt, actionTitle: "REGENERATE VIDEO", action: onRegenerateVideo)
                    promptEditor("MUSIC PROMPT", text: $musicPrompt, actionTitle: "REGENERATE MUSIC", action: onRegenerateMusic)

                    VStack(alignment: .leading, spacing: 6) {
                        Text("PLAYBACK FX")
                            .font(.system(size: 11, weight: .black, design: .monospaced))
                            .foregroundStyle(.white.opacity(0.72))
                        
                        VStack(spacing: 8) {
                            HStack {
                                Toggle("Fizzy Edges", isOn: $transmissionFizzyEdges)
                                    .font(.system(size: 12, weight: .semibold))
                                    .foregroundStyle(.white)
                                    .tint(Color(red: 0.66, green: 1.0, blue: 0.76))
                            }
                            
                            HStack {
                                Toggle("Glitch FX", isOn: $transmissionFXEnabled)
                                    .font(.system(size: 12, weight: .semibold))
                                    .foregroundStyle(.white)
                                    .tint(Color(red: 0.66, green: 1.0, blue: 0.76))
                            }

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
                        .padding(10)
                        .background(Color.white.opacity(0.045))
                        .overlay(Rectangle().stroke(Color.white.opacity(0.12), lineWidth: 1))
                    }

                    Text(status)
                        .font(.system(size: 12, weight: .semibold))
                        .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
        }
        .padding(14)
        .frame(maxWidth: 420)
        .frame(maxHeight: 620)
        .background(Color.black.opacity(0.86), in: RoundedRectangle(cornerRadius: 18, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).stroke(Color.white.opacity(0.22), lineWidth: 1))
        .padding(18)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topTrailing)
    }

    private func promptEditor(_ title: String, text: Binding<String>, actionTitle: String, action: @escaping () -> Void) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(title)
                .font(.system(size: 11, weight: .black, design: .monospaced))
                .foregroundStyle(.white.opacity(0.72))
            TextEditor(text: text)
                .font(.system(size: 11, weight: .semibold, design: .monospaced))
                .foregroundStyle(.white)
                .tint(.white)
                .scrollContentBackgroundCompatHidden()
                .colorScheme(.dark)
                .transmitterKeyboardDoneToolbar()
                .frame(minHeight: 96)
                .padding(6)
                .background(Color.white.opacity(0.06))
                .overlay(Rectangle().stroke(Color.white.opacity(0.16), lineWidth: 1))
            Button(action: action) {
                Text("[ \(actionTitle) ]")
                    .font(.system(size: 11, weight: .black, design: .monospaced))
                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                    .frame(maxWidth: .infinity, minHeight: 34)
                    .overlay(Rectangle().stroke(Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.54), lineWidth: 1))
            }
            .buttonStyle(.plain)
        }
    }
}

private struct TransmissionPlotRibbon: View {
    let text: String
    var allowEditing: Bool = true
    var animateText: Bool = true
    @State private var visibleCharacterCount = 0
    @State private var editing = false
    @State private var draft = ""

    private var cleanText: String {
        text.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private var visibleText: String {
        animateText ? String(cleanText.prefix(visibleCharacterCount)) : cleanText
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            if editing {
                TextEditor(text: $draft)
                    .font(.system(size: 15, weight: .bold, design: .monospaced))
                    .foregroundStyle(.white)
                    .tint(.white)
                    .scrollContentBackgroundCompatHidden()
                    .colorScheme(.dark)
                    .frame(minHeight: 74, maxHeight: 104)
                    .background(Color.black.opacity(0.82))
                    .overlay(Rectangle().stroke(Color.white.opacity(0.16), lineWidth: 1))
            } else {
                Text(visibleText)
                    .font(.system(size: 15, weight: .black, design: .monospaced))
                    .foregroundStyle(.white)
                    .lineLimit(5)
                    .minimumScaleFactor(0.62)
                    .multilineTextAlignment(.center)
                    .frame(maxWidth: .infinity, maxHeight: 96, alignment: .bottom)
            }
        }
        .frame(maxWidth: .infinity, alignment: .center)
        // Corner-overlaid edit button so it doesn't steal text width.
        .overlay(alignment: .topTrailing) {
            if allowEditing {
                Button {
                    if editing {
                        saveEditedPlot()
                    } else {
                        draft = cleanText
                    }
                    editing.toggle()
                } label: {
                    Image(systemName: editing ? "checkmark" : "pencil")
                        .font(.system(size: 12, weight: .black))
                        .foregroundStyle(.white)
                        .frame(width: 28, height: 28)
                        .background(Color.white.opacity(0.12))
                        .overlay(Rectangle().stroke(Color.white.opacity(0.28), lineWidth: 1))
                }
                .buttonStyle(.plain)
            }
        }
        .padding(.horizontal, 6)
        .padding(.vertical, 6)
        .frame(maxHeight: 118, alignment: .bottom)
        .clipped()
        .onAppear {
            draft = cleanText
            restartTypewriter()
        }
            .onChange(of: cleanText) { _ in
                if !editing {
                    draft = cleanText
                }
                restartTypewriter()
            }
    }

    private func restartTypewriter() {
        if !animateText {
            visibleCharacterCount = cleanText.count
            return
        }
        visibleCharacterCount = 0
        let maxCount = cleanText.count
        guard maxCount > 0 else { return }
        for index in 1...maxCount {
            DispatchQueue.main.asyncAfter(deadline: .now() + Double(index) * 0.018) {
                visibleCharacterCount = min(index, maxCount)
            }
        }
    }

    private func saveEditedPlot() {
        K1L0ActiveTransmissionStore.shared.updateResponsePlot(draft)
        let snapshot = K1L0ActiveTransmissionStore.shared.snapshot
        let jobId = snapshot.jobId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !jobId.isEmpty,
              let userId = currentNativeUserId()?.trimmingCharacters(in: .whitespacesAndNewlines),
              !userId.isEmpty else { return }
        persistEditedPlot(jobId: jobId, userId: userId, responsePlot: draft)
    }

    private func persistEditedPlot(jobId: String, userId: String, responsePlot: String, apiIndex: Int = 0) {
        let candidates = [
            "https://api-tunnel.kilo.gallery",
            "http://192.168.40.34:3000",
            "http://fred.local:3000",
            "https://api.kilomeme.com"
        ]
        guard apiIndex < candidates.count else { return }
        guard let url = URL(string: "\(candidates[apiIndex])/api/k1l0/v2/transmit/\(jobId)/plot") else {
            persistEditedPlot(jobId: jobId, userId: userId, responsePlot: responsePlot, apiIndex: apiIndex + 1)
            return
        }
        var request = URLRequest(url: url, timeoutInterval: 12)
        request.httpMethod = "PATCH"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: [
            "userId": userId,
            "responsePlot": responsePlot.trimmingCharacters(in: .whitespacesAndNewlines)
        ])
        URLSession.shared.dataTask(with: request) { data, response, _ in
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            guard (200...299).contains(code),
                  let data,
                  let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  (root["ok"] as? Bool) == true
            else {
                persistEditedPlot(jobId: jobId, userId: userId, responsePlot: responsePlot, apiIndex: apiIndex + 1)
                return
            }
        }.resume()
    }

    private func currentNativeUserId() -> String? {
        let defaults = UserDefaults.standard
        for key in ["FirebaseUserId", "K1L0UserId", "DeviceID", "deviceID"] {
            let value = defaults.string(forKey: key) ?? ""
            let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
            if !trimmed.isEmpty { return trimmed }
        }
        return nil
    }
}

private struct WarblyStaticView: View {
    var body: some View {
        TimelineView(.periodic(from: .now, by: 0.25)) { timeline in
            Canvas { context, size in
                let time = timeline.date.timeIntervalSinceReferenceDate
                context.fill(Path(CGRect(origin: .zero, size: size)), with: .color(.black.opacity(0.78)))

                for index in 0..<42 {
                    let y = (Double(index) / 42.0) * size.height
                    let wave = sin(time * 5.2 + Double(index) * 0.63)
                    let jitter = sin(time * 17.0 + Double(index) * 1.91)
                    let width = size.width * (0.22 + 0.72 * abs(wave))
                    let x = size.width * (0.5 + 0.18 * jitter) - width / 2
                    let alpha = 0.08 + 0.22 * abs(sin(time * 9.0 + Double(index)))
                    let rect = CGRect(x: x, y: y, width: width, height: 1.2)
                    context.fill(Path(rect), with: .color(Color.green.opacity(alpha)))
                }

                for index in 0..<14 {
                    let x = size.width * (0.08 + 0.84 * abs(sin(time * 0.7 + Double(index) * 2.17)))
                    let rect = CGRect(x: x, y: 0, width: 1, height: size.height)
                    context.fill(Path(rect), with: .color(Color.white.opacity(0.04)))
                }
            }
        }
    }
}

private struct PixelBreakupView: View {
    let progress: Double

    var body: some View {
        TimelineView(.periodic(from: .now, by: 0.18)) { timeline in
            Canvas { context, size in
                let time = timeline.date.timeIntervalSinceReferenceDate
                let clamped = min(1, max(0, progress))
                let block = max(8, 54 - clamped * 42)
                let columns = Int(ceil(size.width / block))
                let rows = Int(ceil(size.height / block))
                for row in 0..<rows {
                    for column in 0..<columns {
                        let seed = sin(Double(row * 97 + column * 53) + floor(time * 8.0) * 1.37)
                        let threshold = 0.18 + clamped * 0.62
                        if abs(seed) > threshold {
                            let alpha = (0.16 + 0.38 * abs(seed)) * (1.0 - clamped)
                            let drift = sin(time * 5.0 + Double(row) * 0.81) * block * 1.4 * (1.0 - clamped)
                            let rect = CGRect(
                                x: Double(column) * block + drift,
                                y: Double(row) * block,
                                width: block + 1,
                                height: block + 1
                            )
                            context.fill(Path(rect), with: .color(Color.black.opacity(alpha)))
                            if abs(seed) > threshold + 0.12 {
                                let lightRect = rect.offsetBy(dx: -drift * 0.35, dy: 0).insetBy(dx: block * 0.18, dy: block * 0.18)
                                context.fill(Path(lightRect), with: .color(Color.white.opacity(alpha * 0.42)))
                            }
                        }
                    }
                }
                for band in 0..<9 {
                    let y = size.height * (0.08 + 0.86 * abs(sin(time * 0.37 + Double(band) * 1.73)))
                    let height = max(4, block * (0.28 + 0.22 * abs(sin(time + Double(band)))))
                    let x = sin(time * (2.2 + Double(band) * 0.17) + Double(band)) * size.width * 0.24 * (1.0 - clamped)
                    let rect = CGRect(x: x, y: y, width: size.width, height: height)
                    context.fill(Path(rect), with: .color(Color.white.opacity(0.08 * (1.0 - clamped))))
                }
            }
        }
    }
}

private struct SignalTuningWaveView: View {
    let progress: Double

    var body: some View {
        TimelineView(.periodic(from: .now, by: 0.18)) { timeline in
            Canvas { context, size in
                let time = timeline.date.timeIntervalSinceReferenceDate
                let clamped = min(1, max(0, progress))
                let unresolved = 1.0 - clamped
                let bandCount = max(4, Int(14 - clamped * 10))
                for index in 0..<bandCount {
                    let bandHeight = size.height / CGFloat(bandCount)
                    let y = CGFloat(index) * bandHeight
                    let wave = sin(time * 5.5 + Double(index) * 0.92)
                    let drift = CGFloat(wave) * size.width * CGFloat(0.24 * unresolved)
                    let alpha = 0.08 + 0.18 * unresolved * abs(wave)
                    let rect = CGRect(
                        x: drift,
                        y: y,
                        width: size.width,
                        height: max(1.0, bandHeight * CGFloat(0.72 + 0.18 * abs(wave)))
                    )
                    context.fill(Path(rect), with: .color(Color.white.opacity(alpha)))
                }

                for line in 0..<10 {
                    let y = size.height * CGFloat(Double(line) / 10.0)
                    let rect = CGRect(x: 0, y: y, width: size.width, height: 1)
                    context.fill(Path(rect), with: .color(Color.black.opacity(0.10 * unresolved)))
                }
            }
        }
        .blendMode(.overlay)
        .allowsHitTesting(false)
    }
}

// Semi-transparent "detuned signal" placeholder shown while a transmission is
// still building. Replaces the old flat-black WarblyStaticView square: instead
// of an opaque black hole it renders a luminous detuned signal, and — when an
// image URL is already available (the NanoBanana still generates before the
// video) — ghosts that image band-sheared, green-tinted, blurred and pixelated,
// so you can faintly see the incoming transmission resolving before it lands.
private struct DetunedSignalPreviewView: View {
    var imageUrl: String = ""

    private var resolvedURL: URL? {
        let trimmed = imageUrl.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        return URL(string: trimmed)
    }

    var body: some View {
        ZStack {
            Color.black.opacity(0.45)

            if let url = resolvedURL {
                AsyncImage(url: url) { phase in
                    if case .success(let image) = phase {
                        DetunedGhostImage(image: image)
                    }
                }
            }

            // Coarse pixel breakup (most blocky at progress 0).
            PixelBreakupView(progress: 0)
                .opacity(0.85)
                .allowsHitTesting(false)

            // Horizontal detuned tuning bands.
            SignalTuningWaveView(progress: 0)

            // Reduced grain over the top.
            WarblyStaticView()
                .opacity(0.45)
                .allowsHitTesting(false)
        }
    }
}

// Renders the incoming image as a detuned ghost: sliced into horizontal bands
// that shear side-to-side over time (VHS tracking-skew), tinted green, blurred,
// and dimmed so it reads as a faint signal resolving rather than a clean frame.
private struct DetunedGhostImage: View {
    let image: Image

    var body: some View {
        GeometryReader { proxy in
            let size = proxy.size
                TimelineView(.periodic(from: .now, by: 0.18)) { timeline in
                Canvas { context, _ in
                    let time = timeline.date.timeIntervalSinceReferenceDate
                    let bandCount = 26
                    let bandHeight = size.height / CGFloat(bandCount)
                    let maxShear = size.width * 0.06
                    for index in 0..<bandCount {
                        // Each band drifts on its own phase; faster bands jitter
                        // more to sell the "signal not yet locked" feel.
                        let phase = time * 6.0 + Double(index) * 0.5
                        let jitter = sin(phase) + 0.35 * sin(phase * 3.1)
                        let shear = CGFloat(jitter) * maxShear
                        let y = CGFloat(index) * bandHeight
                        context.drawLayer { layer in
                            layer.clip(to: Path(CGRect(x: 0, y: y, width: size.width, height: bandHeight + 1)))
                            layer.draw(image, in: CGRect(x: shear, y: 0, width: size.width, height: size.height))
                        }
                    }
                }
            }
            .frame(width: size.width, height: size.height)
        }
        .opacity(0.6)
        .colorMultiply(Color(red: 0.5, green: 0.95, blue: 0.62))
        .blur(radius: 1.6)
        .allowsHitTesting(false)
    }
}

// Mask canvas that carves the same tattered-edge silhouette into the video
// itself: white where the video is visible, transparent where it is torn away.
// Apply with .mask { TatteredEdgeMaskCanvas() } on the player view so the
// Unity sky shows through the ragged border instead of a dark overlay.
private struct TatteredEdgeMaskCanvas: View {
    var maxDepth: CGFloat = 4
    var step: CGFloat = 2

    var body: some View {
        // When the Core Image tattered kernel is enabled, tearing is baked into
        // the video frame itself — keep this mask fully opaque so nothing is cut.
        if K1L0TransmissionFX.tatteredShaderActive {
            Color.white
        } else {
        TimelineView(.periodic(from: .now, by: 0.22)) { timeline in
            Canvas { context, size in
                let t = timeline.date.timeIntervalSinceReferenceDate
                let w = size.width
                let h = size.height

                func tear(_ p: CGFloat, _ seed: Double) -> CGFloat {
                    let dp = Double(p)
                    let shape = 0.5 + 0.5 * sin(dp * 0.12 + seed + t * 1.6)
                    let mid   = 0.5 + 0.5 * sin(dp * 0.39 - t * 3.1 + seed * 1.7)
                    let jit   = 0.5 + 0.5 * sin(dp * 1.85 + t * 9.5 + seed * 2.3)
                    let v = shape * 0.55 + mid * 0.3 + jit * 0.15
                    let rip = sin(dp * 0.06 + t * 0.7 + seed) > 0.87 ? 1.8 : 1.0
                    return maxDepth * CGFloat(v) * CGFloat(rip)
                }

                context.withCGContext { cgCtx in
                    // Fully opaque white = every video pixel visible by default.
                    cgCtx.setFillColor(CGColor(red: 1, green: 1, blue: 1, alpha: 1))
                    cgCtx.fill(CGRect(origin: .zero, size: CGSize(width: w, height: h)))

                    // Erase torn border strips to transparent.
                    cgCtx.setBlendMode(.clear)

                    var x: CGFloat = 0
                    while x < w { cgCtx.fill(CGRect(x: x, y: 0, width: step + 1, height: tear(x, 0.0))); x += step }
                    x = 0
                    while x < w { let d = tear(x, 5.5); cgCtx.fill(CGRect(x: x, y: h - d, width: step + 1, height: d)); x += step }
                    var y: CGFloat = 0
                    while y < h { cgCtx.fill(CGRect(x: 0, y: y, width: tear(y, 2.3), height: step + 1)); y += step }
                    y = 0
                    while y < h { let d = tear(y, 7.9); cgCtx.fill(CGRect(x: w - d, y: y, width: d, height: step + 1)); y += step }
                }
            }
            .drawingGroup()
        }
        }
    }
}

// Tattered, fizzy edge decay drawn over the transmission frame. Irregular
// opaque dark "bite" marks reach inward from all four edges so the visible
// boundary reads torn/frayed rather than a clean rectangle, with a faint
// neutral fizz along the frontier and scattered fizzy specks.
// Animated via TimelineView so the edges fizzle and creep over time.
private struct FizzyTatteredEdgeOverlay: View {
    var maxDepth: CGFloat = 4
    var step: CGFloat = 2
    private let fizz = Color.white

    var body: some View {
        // Frontier fizz is baked into the video frame by the Core Image
        // tattered kernel; this Canvas overlay is only the disabled-mode fallback.
        Group {
            if K1L0TransmissionFX.tatteredShaderActive {
                EmptyView()
            } else {
        TimelineView(.periodic(from: .now, by: 0.22)) { timeline in
            Canvas { context, size in
                let t = timeline.date.timeIntervalSinceReferenceDate
                let w = size.width
                let h = size.height

                // Per-position tear depth: a slow ragged silhouette + mid
                // frequency + a fast jitter (the fizz). Occasionally deepens
                // for a rip. `seed` varies per edge so they don't move in lockstep.
                func tear(_ p: CGFloat, _ seed: Double) -> CGFloat {
                    let dp = Double(p)
                    let shape = 0.5 + 0.5 * sin(dp * 0.12 + seed + t * 1.6)
                    let mid = 0.5 + 0.5 * sin(dp * 0.39 - t * 3.1 + seed * 1.7)
                    let jit = 0.5 + 0.5 * sin(dp * 1.85 + t * 9.5 + seed * 2.3)
                    let v = shape * 0.55 + mid * 0.3 + jit * 0.15
                    let rip = sin(dp * 0.06 + t * 0.7 + seed) > 0.87 ? 1.8 : 1.0
                    return maxDepth * CGFloat(v) * CGFloat(rip)
                }

                let frontierAlpha = { (p: CGFloat, seed: Double) -> Double in
                    0.3 + 0.35 * abs(sin(Double(p) * 2.1 + t * 12.0 + seed))
                }

                // TOP: bite downward from y = 0
                var x: CGFloat = 0
                while x < w {
                    let d = tear(x, 0.0)
                    context.fill(Path(CGRect(x: x, y: 0, width: step + 1, height: d)), with: .color(.black.opacity(0.5)))
                    context.fill(Path(CGRect(x: x, y: max(0, d - 2), width: step + 1, height: 2.4)), with: .color(fizz.opacity(frontierAlpha(x, 0.0))))
                    x += step
                }
                // BOTTOM: bite upward from y = h
                x = 0
                while x < w {
                    let d = tear(x, 5.5)
                    context.fill(Path(CGRect(x: x, y: h - d, width: step + 1, height: d)), with: .color(.black.opacity(0.5)))
                    context.fill(Path(CGRect(x: x, y: h - d, width: step + 1, height: 2.4)), with: .color(fizz.opacity(frontierAlpha(x, 5.5))))
                    x += step
                }
                // LEFT: bite rightward from x = 0
                var y: CGFloat = 0
                while y < h {
                    let d = tear(y, 2.3)
                    context.fill(Path(CGRect(x: 0, y: y, width: d, height: step + 1)), with: .color(.black.opacity(0.5)))
                    context.fill(Path(CGRect(x: max(0, d - 2), y: y, width: 2.4, height: step + 1)), with: .color(fizz.opacity(frontierAlpha(y, 2.3))))
                    y += step
                }
                // RIGHT: bite leftward from x = w
                y = 0
                while y < h {
                    let d = tear(y, 7.9)
                    context.fill(Path(CGRect(x: w - d, y: y, width: d, height: step + 1)), with: .color(.black.opacity(0.5)))
                    context.fill(Path(CGRect(x: w - d, y: y, width: 2.4, height: step + 1)), with: .color(fizz.opacity(frontierAlpha(y, 7.9))))
                    y += step
                }

                // Scattered fizzy specks hugging the frontier.
                for i in 0..<70 {
                    let edge = i % 4
                    let n = sin(Double(i) * 12.9898 + t * 4.0)
                    let n2 = sin(Double(i) * 4.1414 - t * 6.0)
                    let along = CGFloat(abs(n))
                    let inset = CGFloat(abs(n2)) * maxDepth * 0.9
                    let px: CGFloat
                    let py: CGFloat
                    switch edge {
                    case 0: px = along * w; py = inset
                    case 1: px = along * w; py = h - inset
                    case 2: px = inset; py = along * h
                    default: px = w - inset; py = along * h
                    }
                    let s = 1 + CGFloat(abs(sin(Double(i) + t * 8.0))) * 1.6
                    context.fill(Path(CGRect(x: px, y: py, width: s, height: s)), with: .color(fizz.opacity(0.2 + 0.45 * abs(n2))))
                }
            }
        }
        }
        }
        .allowsHitTesting(false)
    }
}

private struct TransmissionChainProgressBar: View {
    let total: Int
    let currentIndex: Int
    let currentProgress: Double

    var body: some View {
        HStack(spacing: 4) {
            ForEach(0..<max(total, 0), id: \.self) { index in
                GeometryReader { geometry in
                    ZStack(alignment: .leading) {
                        Capsule()
                            .fill(Color.white.opacity(0.24))
                        Capsule()
                            .fill(Color.white)
                            .frame(width: geometry.size.width * fillRatio(for: index))
                    }
                }
                .frame(height: 3)
            }
        }
        .frame(height: 8)
    }

    private func fillRatio(for index: Int) -> Double {
        if index < currentIndex { return 1 }
        if index == currentIndex { return min(1, max(0, currentProgress)) }
        return 0
    }
}

private struct TransmissionChainTapZones: View {
    let clipCount: Int
    @Binding var currentIndex: Int
    @Binding var currentProgress: Double

    var body: some View {
        HStack(spacing: 0) {
            Color.clear
                .contentShape(Rectangle())
                .onTapGesture {
                    currentProgress = 0
                    currentIndex = max(0, currentIndex - 1)
                }
            Color.clear
                .contentShape(Rectangle())
                .onTapGesture {
                    currentProgress = 0
                    currentIndex = min(max(clipCount - 1, 0), currentIndex + 1)
                }
        }
        .padding(.top, 92)
        .padding(.bottom, 132)
    }
}

// Shared loop counter so the FX know which play-through the video is on.
// The AVVideoComposition clock restarts every loop, so the player bumps this
// externally when playback wraps.
private final class K1L0TransmissionFXLoopState {
    var loopCount = 0
}

// Client-side transmission FX — Swift port of the old server-side ffmpeg
// composite (mirrors Unity's TransmissionFXScheduler): beat-synced cut
// boundaries, random crop/zoom closeups, treatment cycling, flash inserts,
// animated grain. Applied as a Core Image AVVideoComposition so it renders
// on the bare AVPlayerLayer (SwiftUI layer shaders can't touch UIKit-backed
// video layers). Effects run immediately, including the first inbound
// appearance, then re-roll on repeats at a slower, music-paced cut cadence.
private enum K1L0TransmissionFX {
    struct Treatment {
        var chromaR = CGVector.zero
        var chromaB = CGVector.zero
        var invert = false
        var saturation: Double = 1
        var contrast: Double = 1
        var brightness: Double = 0
        var posterize: Double = 0
        var blur: Double = 0
        var noise: Double = 0
        var vignette: Double = 0
    }

    struct Cut {
        let start: Double
        let crop: CGRect      // normalized, bottom-left origin
        let hflip: Bool
        let vflip: Bool
        let fx: Treatment
        let flashIn: Bool
        let flashWhite: Bool
    }

    static var enabled: Bool {
        UserDefaults.standard.object(forKey: "k1lo_native_transmissionFX") as? Bool ?? true
    }

    // 0 = barely-there grade, 1 = full detuned chaos. Scales every treatment
    // parameter and gates the harsh looks (negate, posterize, flashes).
    static var intensity: Double {
        let raw = UserDefaults.standard.object(forKey: "k1lo_native_transmissionFXIntensity") as? Double ?? 0.5
        return min(1, max(0, raw))
    }

    // Tattered edge as a Core Image kernel (GPU, in-pipeline) instead of the
    // CPU SwiftUI Canvas mask/overlay. Bakes the torn silhouette directly into
    // the composited video frame as the last composition step.
    // Toggle off (`k1lo_native_tatteredCIKernel`=0) to force the Canvas fallback.
    static var tatteredKernelEnabled: Bool {
        (UserDefaults.standard.object(forKey: "k1lo_native_tatteredCIKernel") as? Bool) ?? true
    }

    static let tatteredEdgeKernel: CIKernel? = {
        let source = """
        float tearFn(float q, float seed, float time, float maxDepth) {
            float shape = 0.5 + 0.5 * sin(q * 0.14 + seed + time * 1.8);
            float mid   = 0.5 + 0.5 * sin(q * 0.41 - time * 3.4 + seed * 1.7);
            float fast  = 0.5 + 0.5 * sin(q * 0.93 + time * 6.2 + seed * 0.9);
            float jit   = 0.5 + 0.5 * sin(q * 2.10 + time * 11.0 + seed * 2.3);
            float v = shape * 0.48 + mid * 0.26 + fast * 0.16 + jit * 0.10;
            float rip = (sin(q * 0.07 + time * 0.9 + seed) > 0.84) ? 2.1 : 1.0;
            return maxDepth * v * rip;
        }

        kernel vec4 tatteredEdge(__sample src, vec2 size, float time, float maxDepth, vec3 fizzColor) {
            vec2 p = destCoord();
            vec4 c = src;
            float w = size.x;
            float h = size.y;

            float tT = tearFn(p.x, 0.0, time, maxDepth);
            float tB = tearFn(p.x, 5.5, time, maxDepth);
            float tL = tearFn(p.y, 2.3, time, maxDepth);
            float tR = tearFn(p.y, 7.9, time, maxDepth);

            float torn = 0.0;
            if (p.y < tT) torn = 1.0;
            if ((h - p.y) < tB) torn = 1.0;
            if (p.x < tL) torn = 1.0;
            if ((w - p.x) < tR) torn = 1.0;

            float frontier = min(min(abs(p.y - tT), abs((h - p.y) - tB)),
                                 min(abs(p.x - tL), abs((w - p.x) - tR)));
            float band = max(1.0, maxDepth * 0.22);
            float glow = 1.0 - smoothstep(0.0, band, frontier);
            float pulse = 0.3 + 0.5 * abs(sin(p.x * 2.1 + time * 12.0)) * abs(sin(p.y * 1.7 - time * 9.0));

            vec2 cell = floor(p / 3.0);
            float sp = fract(sin(dot(cell, vec2(12.9898, 78.233))) * 43758.5453 + time * 4.0);
            float edgeDist = min(min(p.x, w - p.x), min(p.y, h - p.y));
            float sparkle = step(0.985, sp) * (1.0 - smoothstep(0.0, maxDepth, edgeDist));

            float glowAmt = clamp(glow * pulse + sparkle * 0.8, 0.0, 1.0);
            // Preserve the animated ragged edge and sparkle, but do not tint the
            // perimeter. The old signal-green mix read as a decorative border.
            vec3 outRgb = mix(c.rgb, vec3(1.0), glowAmt * 0.22);
            return vec4(outRgb, c.a * (1.0 - torn));
        }
        """
        return CIKernel(source: source)
    }()

    // Render the kernel once into a throwaway CIContext. If the source failed to
    // compile (or the GPU refuses it) this returns false and we transparently
    // fall back to the Canvas mask/overlay — so a bad kernel can NEVER take the
    // grain/chroma/cut FX (or the tattering itself) down with it.
    static let tatteredKernelValid: Bool = {
        guard let kernel = tatteredEdgeKernel else { return false }
        let testExtent = CGRect(x: 0, y: 0, width: 32, height: 32)
        let src = CIImage(color: CIColor(red: 1, green: 1, blue: 1, alpha: 1)).cropped(to: testExtent)
        guard let out = kernel.apply(extent: testExtent, roiCallback: { _, rect in rect }, arguments: [
            src,
            CIVector(x: 32, y: 32),
            NSNumber(value: Float(1.0)),
            NSNumber(value: Float(2.0)),
            CIColor(red: 1.0, green: 1.0, blue: 1.0),
        ]) else { return false }
        let ctx = CIContext(options: [.useSoftwareRenderer: false])
        return ctx.createCGImage(out, from: testExtent) != nil
    }()

    // Single resolved switch: shader only when enabled AND it actually renders.
    static var tatteredShaderActive: Bool {
        tatteredKernelEnabled && tatteredKernelValid
    }

    static func apply(to item: AVPlayerItem, loopState: K1L0TransmissionFXLoopState) {
#if os(iOS)
        // Live iOS playback is rendered by K1L0MetalVideoPlayer. Keep this
        // composition implementation for offline/export and non-iOS fallback.
        return
#else
        guard enabled else { return }
        let asset = item.asset
        asset.loadValuesAsynchronously(forKeys: ["duration", "tracks"]) {
            guard asset.statusOfValue(forKey: "duration", error: nil) == .loaded,
                  asset.statusOfValue(forKey: "tracks", error: nil) == .loaded,
                  asset.tracks(withMediaType: .video).first != nil else { return }
            let duration = asset.duration.seconds
            guard duration.isFinite, duration > 0.2 else { return }
            let schedule = rollSchedule(duration: duration)
            guard !schedule.isEmpty else { return }
            let composition = makeComposition(asset: asset, schedule: schedule, loopState: loopState)
            DispatchQueue.main.async { item.videoComposition = composition }
        }
#endif
    }

    private static func rollSchedule(duration: Double, bpm: Double = 72) -> [Cut] {
        let beat = 60.0 / max(bpm, 30.0)
        // Keep cuts music-paced and a little uneven so the loop feels detuned
        // instead of strobing.
        let level = intensity
        // Cut cadence follows intensity too: full chaos cuts at 5x beat,
        // low intensity stretches toward 8x (fewer, longer cuts).
        let paceMultiplier = 5.0 + 3.0 * (1 - level)
        let choices: [Double] = [0.5, 0.7, 1.0, 1.5, 2.0, 2.75, 3.5]
        let weights: [Double] = [0.7, 1.4, 3, 3, 2.2, 1.2, 0.6]
        var cuts: [Cut] = []
        var t = 0.0
        var safety = 64
        while t < duration && safety > 0 {
            safety -= 1
            let cutDuration = beat * paceMultiplier * weightedPick(choices, weights)
            cuts.append(makeCut(at: t, cutDuration: cutDuration, level: level))
            t += cutDuration
        }
        return cuts
    }

    private static func makeCut(at t: Double, cutDuration: Double, level: Double) -> Cut {
        // Face sits upper-middle in the portrait comps; the ffmpeg ROI was
        // top-origin (0.40, 0.34) — Core Image is bottom-origin, hence 0.66.
        let faceCenterX = 0.40, faceCenterY = 0.66
        // Closeups follow intensity: at full chaos 60% face zoom / 30% random
        // crop with deep punch-ins; at low intensity most cuts stay full-frame
        // and the zooms that do happen are gentler.
        let faceZoomChance = 0.25 + 0.35 * level
        let randomCropChance = 0.15 + 0.15 * level
        let crop: CGRect
        let r = Double.random(in: 0..<1)
        if r < faceZoomChance {
            let zoomMin = 0.35 + 0.25 * (1 - level)
            let zoomMax = min(0.92, 0.75 + 0.15 * (1 - level))
            let zoom = Double.random(in: zoomMin...zoomMax)
            crop = clampCrop(x: faceCenterX - zoom * 0.5, y: faceCenterY - zoom * 0.5, w: zoom, h: zoom)
        } else if r < faceZoomChance + randomCropChance {
            let zoomMin = 0.5 + 0.2 * (1 - level)
            let zoomMax = min(0.95, 0.85 + 0.1 * (1 - level))
            let zoom = Double.random(in: zoomMin...zoomMax)
            crop = clampCrop(x: Double.random(in: 0...(1 - zoom)), y: Double.random(in: 0...(1 - zoom)), w: zoom, h: zoom)
        } else {
            crop = CGRect(x: 0, y: 0, width: 1, height: 1)
        }
        return Cut(
            start: t,
            crop: crop,
            hflip: Double.random(in: 0..<1) < 0.06 * (0.5 + level),
            vflip: Double.random(in: 0..<1) < 0.015 * level,
            fx: randomTreatment(level: level, cutDuration: cutDuration),
            flashIn: Double.random(in: 0..<1) < 0.10 * level,
            flashWhite: true
        )
    }

    private static func randomTreatment(level: Double, cutDuration: Double) -> Treatment {
        var fx = Treatment()
        // Weighted looks: mild grades are common; harsh ones scale with the
        // FX Intensity slider. Negate is disabled outright — effects must
        // never darken a slide, and inversion turns bright scenes black.
        let lookWeights: [Double] = [
            2.2,                        // 0 clean
            1.8,                        // 1 chroma shift horizontal
            0.9 * level,                // 2 chroma shift diagonal extreme
            0,                          // 3 negate — permanently off (darkens)
            0.9,                        // 4 desat + contrast
            0.5 * level,                // 5 posterize
            0.8,                        // 6 blur
            0.9,                        // 7 noise heavy
            1.0                         // 8 wavy chroma + grain
        ]
        switch weightedIndex(lookWeights) {
        case 0: // clean
            break
        case 1: // chroma shift horizontal
            let px = 3 + 4 * level
            fx.chromaR = CGVector(dx: px, dy: 0); fx.chromaB = CGVector(dx: -px, dy: 0)
        case 2: // chroma shift diagonal extreme
            let px = 6 + 9 * level
            fx.chromaR = CGVector(dx: px, dy: 4 * level); fx.chromaB = CGVector(dx: -px, dy: -4 * level)
            fx.noise = 0.08 * max(0.4, level)
        case 3: // negate — gated above
            fx.invert = true
        case 4: // desat + contrast
            fx.saturation = 1 - 0.9 * level
            fx.contrast = 1 + 0.45 * level
            fx.brightness = 0.02 * level
        case 5: // posterize
            fx.posterize = 8 - 3 * level
            fx.contrast = 1 + 0.2 * level
        case 6: // blur
            fx.blur = 1 + 1.5 * level
            fx.contrast = 1 + 0.3 * level
        case 7: // noise heavy
            fx.noise = 0.08 + 0.10 * level
            fx.contrast = 1 + 0.3 * level
        default: // wavy displacement grade — approximated as chroma + grain
            fx.noise = 0.04 + 0.06 * level
            let px = 2 + 3 * level
            fx.chromaR = CGVector(dx: px, dy: 1); fx.chromaB = CGVector(dx: -px, dy: -1)
        }
        // No vignette: it darkens slide edges, and effects must never darken.
        return fx
    }

    private static func weightedIndex(_ weights: [Double]) -> Int {
        let total = weights.reduce(0, +)
        guard total > 0 else { return 0 }
        let pick = Double.random(in: 0..<total)
        var acc = 0.0
        for (index, weight) in weights.enumerated() {
            acc += weight
            if pick < acc { return index }
        }
        return weights.count - 1
    }

    private static func clampCrop(x: Double, y: Double, w: Double, h: Double) -> CGRect {
        CGRect(x: min(max(x, 0), 1 - w), y: min(max(y, 0), 1 - h), width: w, height: h)
    }

    private static func weightedPick(_ values: [Double], _ weights: [Double]) -> Double {
        let total = weights.reduce(0, +)
        let pick = Double.random(in: 0..<total)
        var acc = 0.0
        for (i, w) in weights.enumerated() {
            acc += w
            if pick <= acc { return values[i] }
        }
        return values[values.count - 1]
    }

    // Bake a full FX pass over an export asset (camera-roll saves). Rolls a
    // schedule spanning the whole export, so cuts land across loop boundaries.
    static func bakedComposition(for asset: AVAsset, durationSeconds: Double) -> AVVideoComposition? {
        guard enabled, durationSeconds > 0.2 else { return nil }
        let schedule = rollSchedule(duration: durationSeconds)
        guard !schedule.isEmpty else { return nil }
        return makeComposition(asset: asset, schedule: schedule, loopState: K1L0TransmissionFXLoopState())
    }

    private static let noiseImage: CIImage =
        CIFilter(name: "CIRandomGenerator")?.outputImage ?? CIImage.empty()

    private static func channel(_ source: CIImage, r: Double, g: Double, b: Double) -> CIImage {
        source.applyingFilter("CIColorMatrix", parameters: [
            "inputRVector": CIVector(x: CGFloat(r), y: 0, z: 0, w: 0),
            "inputGVector": CIVector(x: 0, y: CGFloat(g), z: 0, w: 0),
            "inputBVector": CIVector(x: 0, y: 0, z: CGFloat(b), w: 0),
            "inputAVector": CIVector(x: 0, y: 0, z: 0, w: 1)
        ])
    }

    private static func makeComposition(asset: AVAsset, schedule: [Cut], loopState: K1L0TransmissionFXLoopState) -> AVVideoComposition {
        AVMutableVideoComposition(asset: asset) { request in
            let t = request.compositionTime.seconds
            var cut = schedule[0]
            for c in schedule {
                if c.start <= t { cut = c } else { break }
            }

            var image = request.sourceImage
            let extent = image.extent

            // Crop/zoom closeup — scale the ROI back up to fill the frame.
            if cut.crop.width < 0.999 {
                let cropPx = CGRect(
                    x: extent.minX + cut.crop.minX * extent.width,
                    y: extent.minY + cut.crop.minY * extent.height,
                    width: cut.crop.width * extent.width,
                    height: cut.crop.height * extent.height
                )
                let sx = extent.width / cropPx.width
                let sy = extent.height / cropPx.height
                image = image
                    .cropped(to: cropPx)
                    .transformed(by: CGAffineTransform(translationX: -cropPx.minX, y: -cropPx.minY))
                    .transformed(by: CGAffineTransform(scaleX: sx, y: sy))
            }
            if cut.hflip {
                image = image.transformed(by: CGAffineTransform(a: -1, b: 0, c: 0, d: 1, tx: extent.width, ty: 0))
            }
            if cut.vflip {
                image = image.transformed(by: CGAffineTransform(a: 1, b: 0, c: 0, d: -1, tx: 0, ty: extent.height))
            }

            let fx = cut.fx
            if fx.chromaR != .zero || fx.chromaB != .zero {
                let red = channel(image, r: 1, g: 0, b: 0)
                    .transformed(by: CGAffineTransform(translationX: fx.chromaR.dx, y: fx.chromaR.dy))
                let green = channel(image, r: 0, g: 1, b: 0)
                let blue = channel(image, r: 0, g: 0, b: 1)
                    .transformed(by: CGAffineTransform(translationX: fx.chromaB.dx, y: fx.chromaB.dy))
                image = red.applyingFilter("CIAdditionCompositing", parameters: [kCIInputBackgroundImageKey: green])
                image = blue.applyingFilter("CIAdditionCompositing", parameters: [kCIInputBackgroundImageKey: image])
            }
            if fx.invert {
                image = image.applyingFilter("CIColorInvert")
            }
            if fx.saturation != 1 || fx.contrast != 1 || fx.brightness != 0 {
                image = image.applyingFilter("CIColorControls", parameters: [
                    kCIInputSaturationKey: fx.saturation,
                    kCIInputContrastKey: fx.contrast,
                    kCIInputBrightnessKey: fx.brightness
                ])
            }
            if fx.posterize > 0 {
                image = image.applyingFilter("CIColorPosterize", parameters: ["inputLevels": fx.posterize])
            }
            if fx.blur > 0 {
                image = image.clampedToExtent()
                    .applyingFilter("CIGaussianBlur", parameters: [kCIInputRadiusKey: fx.blur])
            }
            if fx.noise > 0 {
                // Animated grain: slide the random field each frame.
                let jitter = CGAffineTransform(
                    translationX: CGFloat((t * 973.0).truncatingRemainder(dividingBy: 512.0)),
                    y: CGFloat((t * 541.0).truncatingRemainder(dividingBy: 512.0))
                )
                let grain = noiseImage
                    .transformed(by: jitter)
                    .applyingFilter("CIColorMatrix", parameters: [
                        "inputRVector": CIVector(x: 0.7, y: 0, z: 0, w: 0),
                        "inputGVector": CIVector(x: 0.7, y: 0, z: 0, w: 0),
                        "inputBVector": CIVector(x: 0.7, y: 0, z: 0, w: 0),
                        "inputAVector": CIVector(x: 0, y: 0, z: 0, w: CGFloat(fx.noise))
                    ])
                    .cropped(to: extent)
                image = grain.applyingFilter("CISourceOverCompositing", parameters: [kCIInputBackgroundImageKey: image])
            }
            if fx.vignette > 0 {
                image = image.applyingFilter("CIVignette", parameters: [
                    kCIInputIntensityKey: fx.vignette * 2.0,
                    kCIInputRadiusKey: 1.6
                ])
            }

            // 1-frame flash at the cut boundary, decaying over 80ms.
            let flashAge = t - cut.start
            if cut.flashIn, flashAge >= 0, flashAge < 0.08 {
                let alpha = 1.0 - flashAge / 0.08
                let level: CGFloat = cut.flashWhite ? 1 : 0
                let flash = CIImage(color: CIColor(red: level, green: level, blue: level, alpha: CGFloat(alpha)))
                    .cropped(to: extent)
                image = flash.applyingFilter("CISourceOverCompositing", parameters: [kCIInputBackgroundImageKey: image])
            }

            // Tattered edge: bake the torn silhouette + neutral frontier fizz into
            // the frame on the GPU. Torn pixels get alpha 0 so Unity sky shows
            // through (AVPlayerLayer has a clear background). Skipped unless the
            // kernel both compiles and renders (tatteredShaderActive); otherwise
            // the SwiftUI Canvas mask/overlay handles it and FX stay intact.
            image = image.cropped(to: extent)
            if Self.tatteredShaderActive, let kernel = Self.tatteredEdgeKernel {
                let maxDepth = min(6.0, 0.012 * min(extent.width, extent.height))
                if let processed = kernel.apply(extent: extent, roiCallback: { _, rect in rect }, arguments: [
                    image,
                    CIVector(x: extent.width, y: extent.height),
                    NSNumber(value: Float(t)),
                    NSNumber(value: Float(maxDepth)),
                    CIColor(red: 1.0, green: 1.0, blue: 1.0),
                ]) {
                    image = processed
                }
            }

            request.finish(with: image, context: nil)
        }
    }
}

private struct InlineTransmissionVideoPlayer: View {
    let urlString: String
    let audioUrlString: String?
    let clips: [K1L0TransmissionClip]
    @State private var player: AVPlayer
    @State private var audioPlayer: AVPlayer?
    @Binding private var currentClipIndex: Int
    @Binding private var currentClipProgress: Double
    @Binding private var isVideoReady: Bool
    @State private var timeObserver: Any?
    @State private var fxLoopState: K1L0TransmissionFXLoopState
    private let holdAtEndIndex: Int?

    init(urlString: String, audioUrlString: String? = nil, currentClipProgress: Binding<Double> = .constant(0), isVideoReady: Binding<Bool> = .constant(false)) {
        self.urlString = urlString
        self.audioUrlString = audioUrlString
        self.clips = []
        _currentClipIndex = .constant(0)
        _currentClipProgress = currentClipProgress
        _isVideoReady = isVideoReady
        self.holdAtEndIndex = nil
        let loopState = K1L0TransmissionFXLoopState()
        _fxLoopState = State(initialValue: loopState)
        let url = URL(string: urlString) ?? URL(fileURLWithPath: "/dev/null")
        let item = AVPlayerItem(url: url)
        K1L0TransmissionFX.apply(to: item, loopState: loopState)
        _player = State(initialValue: AVPlayer(playerItem: item))
        // Raw WAN clips are silent — TransmissionFX-style audio rides on a
        // separate URL (ACE-Step music/vocals). Play it in parallel and loop
        // it to match the video loop.
        let audio: AVPlayer?
        if let s = audioUrlString, !s.isEmpty, let u = URL(string: s) {
            audio = AVPlayer(url: u)
        } else {
            audio = nil
        }
        _audioPlayer = State(initialValue: audio)
    }

    init(clips: [K1L0TransmissionClip], currentClipIndex: Binding<Int>, currentClipProgress: Binding<Double>, isVideoReady: Binding<Bool> = .constant(false), holdAtEndIndex: Int? = nil) {
        let playable = clips.filter { $0.videoURL != nil }
        self.clips = playable
        self.urlString = playable.first?.videoURL?.absoluteString ?? ""
        self.audioUrlString = playable.first?.audioURL?.absoluteString
        _currentClipIndex = currentClipIndex
        _currentClipProgress = currentClipProgress
        _isVideoReady = isVideoReady
        self.holdAtEndIndex = holdAtEndIndex
        let loopState = K1L0TransmissionFXLoopState()
        _fxLoopState = State(initialValue: loopState)
        let url = playable.first?.videoURL ?? URL(fileURLWithPath: "/dev/null")
        let item = AVPlayerItem(url: url)
        K1L0TransmissionFX.apply(to: item, loopState: loopState)
        _player = State(initialValue: AVPlayer(playerItem: item))
        let audio: AVPlayer?
        if let audioURL = playable.first?.audioURL {
            audio = AVPlayer(url: audioURL)
        } else {
            audio = nil
        }
        _audioPlayer = State(initialValue: audio)
    }

    var body: some View {
        // Keep playback on the bare AVPlayerLayer. SwiftUI layer effects can't
        // sample UIKit-backed video reliably; the tattered edge is overlaid by
        // the result panel instead.
        K1L0BareVideoPlayer(player: player)
            .onAppear {
                isVideoReady = false
                player.isMuted = audioPlayer != nil
                player.play()
                audioPlayer?.play()
                installProgressObserver()
            }
            .onDisappear {
                player.pause()
                audioPlayer?.pause()
                removeProgressObserver()
                isVideoReady = false
            }
            .onReceive(NotificationCenter.default.publisher(for: .AVPlayerItemDidPlayToEndTime)) { notification in
                guard let item = notification.object as? AVPlayerItem else { return }
                if item === player.currentItem {
                    advanceVideo()
                }
                if let ap = audioPlayer, item === ap.currentItem {
                    ap.seek(to: .zero, toleranceBefore: .zero, toleranceAfter: .zero) { _ in
                        ap.play()
                    }
                }
            }
            .onChange(of: currentClipIndex) { index in
                playClip(at: index)
            }
    }

    private func advanceVideo() {
        guard !clips.isEmpty else {
            // Single video wrapped — count the completed play-through so the
            // next loop can re-roll its FX schedule.
            fxLoopState.loopCount += 1
            if let item = player.currentItem {
                K1L0TransmissionFX.apply(to: item, loopState: fxLoopState)
            }
            player.seek(to: .zero, toleranceBefore: .zero, toleranceAfter: .zero) { _ in
                player.play()
            }
            return
        }
        if let holdAtEndIndex, currentClipIndex == holdAtEndIndex {
            // Loop the response clip in place (the choices stay up because the
            // clip index doesn't change) instead of freezing on the last
            // frame — a frozen frame reads as a broken static image.
            fxLoopState.loopCount += 1
            if let item = player.currentItem {
                K1L0TransmissionFX.apply(to: item, loopState: fxLoopState)
            }
            player.seek(to: .zero, toleranceBefore: .zero, toleranceAfter: .zero) { _ in
                player.play()
            }
            return
        }
        let nextIndex = (currentClipIndex + 1) % clips.count
        // A full pass of the chain only completes when we wrap back to clip 0.
        if nextIndex == 0 { fxLoopState.loopCount += 1 }
        currentClipIndex = nextIndex
    }

    private func playClip(at index: Int) {
        guard !clips.isEmpty else { return }
        let safeIndex = min(max(0, index), clips.count - 1)
        if currentClipIndex != safeIndex {
            currentClipIndex = safeIndex
            return
        }
        currentClipProgress = 0
        isVideoReady = false
        let next = clips[safeIndex]
        guard let videoURL = next.videoURL else {
            player.seek(to: .zero)
            player.play()
            return
        }
        let item = AVPlayerItem(url: videoURL)
        K1L0TransmissionFX.apply(to: item, loopState: fxLoopState)
        // Music continuity: a story chain shares one track, so only swap the
        // audio player when the next clip carries a DIFFERENT track. Same
        // track — or a clip with no track of its own — keeps the music
        // playing seamlessly across slide changes.
        if let audioURL = next.audioURL {
            let currentURL = (audioPlayer?.currentItem?.asset as? AVURLAsset)?.url
            if currentURL != audioURL {
                audioPlayer?.pause()
                audioPlayer = AVPlayer(url: audioURL)
            }
        }
        player.replaceCurrentItem(with: item)
        player.isMuted = audioPlayer != nil
        player.play()
        if audioPlayer?.timeControlStatus != .playing {
            audioPlayer?.play()
        }
    }

    private func installProgressObserver() {
        removeProgressObserver()
        // Four progress updates per second are visually smooth enough for the
        // tiny chain bar and avoid driving SwiftUI at ~12.5 Hz over AVPlayer.
        let interval = CMTime(seconds: 0.25, preferredTimescale: 600)
        timeObserver = player.addPeriodicTimeObserver(forInterval: interval, queue: .main) { time in
            markVideoReadyIfPossible()
            guard let duration = player.currentItem?.duration.seconds,
                  duration.isFinite,
                  duration > 0 else {
                currentClipProgress = 0
                return
            }
            currentClipProgress = min(1, max(0, time.seconds / duration))
        }
    }

    private func markVideoReadyIfPossible() {
        guard !isVideoReady,
              let item = player.currentItem,
              item.status == .readyToPlay else { return }
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.18) {
            if player.currentItem === item && item.status == .readyToPlay {
                isVideoReady = true
            }
        }
    }

    private func removeProgressObserver() {
        if let timeObserver {
            player.removeTimeObserver(timeObserver)
            self.timeObserver = nil
        }
    }

}

private struct TransmissionFizzyEdgesModifier: ViewModifier {
    let enabled: Bool
    let size: CGSize

    private static let fizzyShaderAvailable: Bool = {
        guard let device = MTLCreateSystemDefaultDevice() else { return false }
        let bundle = Bundle(for: K1L0TuningStaticPlayer.self)
        guard let lib = try? device.makeDefaultLibrary(bundle: bundle) else { return false }
        return lib.functionNames.contains("k1l0FizzyEdges")
    }()

    func body(content: Content) -> some View {
        if #available(iOS 17.0, macOS 14.0, *), enabled, Self.fizzyShaderAvailable {
            TimelineView(.periodic(from: .now, by: 0.18)) { timeline in
                content
                    .layerEffect(
                        ShaderLibrary.bundle(Bundle(for: K1L0TuningStaticPlayer.self)).k1l0FizzyEdges(
                            .float2(Float(size.width), Float(size.height)),
                            .float(Float(timeline.date.timeIntervalSinceReferenceDate.truncatingRemainder(dividingBy: 3600))),
                            .float(enabled ? 1.0 : 0.0)
                        ),
                        maxSampleOffset: .zero
                    )
            }
        } else {
            content
        }
    }
}

private extension View {
    func transmissionFizzyMask(enabled: Bool, size: CGSize) -> some View {
        modifier(TransmissionFizzyEdgesModifier(enabled: enabled, size: size))
    }
}

private struct K1L0BareVideoPlayer: View {
    let player: AVPlayer

    var body: some View {
#if canImport(UIKit)
        K1L0MetalVideoPlayerView(player: player)
#elseif canImport(AppKit)
        K1L0BareVideoPlayerNSView(player: player)
#else
        VideoPlayer(player: player)
#endif
    }
}

#if canImport(UIKit)
private struct K1L0MetalVideoPlayerView: UIViewRepresentable {
    let player: AVPlayer

    func makeUIView(context: Context) -> K1L0MetalVideoView {
        let view = K1L0MetalVideoView()
        view.player = player
        return view
    }

    func updateUIView(_ view: K1L0MetalVideoView, context: Context) {
        view.player = player
    }
}

private final class K1L0MetalVideoView: MTKView, MTKViewDelegate {
    struct Uniforms {
        var viewport = SIMD2<Float>(1, 1)
        var texture = SIMD2<Float>(1, 1)
        var time: Float = 0
        var intensity: Float = 0.5
    }

    var player: AVPlayer? { didSet { attachOutputIfNeeded() } }
    private var attachedItem: AVPlayerItem?
    private var videoOutput: AVPlayerItemVideoOutput?
    private var commandQueue: MTLCommandQueue?
    private var pipeline: MTLRenderPipelineState?
    private var textureCache: CVMetalTextureCache?
    private let startedAt = CACurrentMediaTime()

    required init(coder: NSCoder) { super.init(coder: coder); configure() }
    override init(frame: CGRect, device: MTLDevice?) {
        super.init(frame: frame, device: device ?? MTLCreateSystemDefaultDevice())
        configure()
    }

    private func configure() {
        guard let device else { return }
        framebufferOnly = false
        isOpaque = false
        backgroundColor = .clear
        colorPixelFormat = .bgra8Unorm
        preferredFramesPerSecond = 30
        enableSetNeedsDisplay = false
        isPaused = false
        delegate = self
        commandQueue = device.makeCommandQueue()
        CVMetalTextureCacheCreate(nil, nil, device, nil, &textureCache)
        let bundle = Bundle(for: K1L0TuningStaticPlayer.self)
        guard let library = try? device.makeDefaultLibrary(bundle: bundle),
              let vertex = library.makeFunction(name: "k1l0VideoVertex"),
              let fragment = library.makeFunction(name: "k1l0VideoFragment") else { return }
        let descriptor = MTLRenderPipelineDescriptor()
        descriptor.vertexFunction = vertex
        descriptor.fragmentFunction = fragment
        descriptor.colorAttachments[0].pixelFormat = colorPixelFormat
        descriptor.colorAttachments[0].isBlendingEnabled = true
        descriptor.colorAttachments[0].sourceRGBBlendFactor = .sourceAlpha
        descriptor.colorAttachments[0].destinationRGBBlendFactor = .oneMinusSourceAlpha
        pipeline = try? device.makeRenderPipelineState(descriptor: descriptor)
    }

    private func attachOutputIfNeeded() {
        guard let item = player?.currentItem, item !== attachedItem else { return }
        if let old = attachedItem, let output = videoOutput { old.remove(output) }
        let attrs: [String: Any] = [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA]
        let output = AVPlayerItemVideoOutput(pixelBufferAttributes: attrs)
        item.add(output)
        attachedItem = item
        videoOutput = output
    }

    func mtkView(_ view: MTKView, drawableSizeWillChange size: CGSize) {}

    func draw(in view: MTKView) {
        attachOutputIfNeeded()
        guard let output = videoOutput, let player, let drawable = currentDrawable,
              let pass = currentRenderPassDescriptor, let pipeline, let commandQueue,
              let cache = textureCache else { return }
        let itemTime = output.itemTime(forHostTime: CACurrentMediaTime())
        guard output.hasNewPixelBuffer(forItemTime: itemTime),
              let pixel = output.copyPixelBuffer(forItemTime: itemTime, itemTimeForDisplay: nil) else { return }
        let width = CVPixelBufferGetWidth(pixel), height = CVPixelBufferGetHeight(pixel)
        var cvTexture: CVMetalTexture?
        guard CVMetalTextureCacheCreateTextureFromImage(nil, cache, pixel, nil, .bgra8Unorm, width, height, 0, &cvTexture) == kCVReturnSuccess,
              let cvTexture, let texture = CVMetalTextureGetTexture(cvTexture),
              let buffer = commandQueue.makeCommandBuffer(), let encoder = buffer.makeRenderCommandEncoder(descriptor: pass) else { return }
        var uniforms = Uniforms(
            viewport: SIMD2(Float(drawableSize.width), Float(drawableSize.height)),
            texture: SIMD2(Float(width), Float(height)),
            time: Float(CACurrentMediaTime() - startedAt),
            intensity: K1L0TransmissionFX.enabled ? Float(K1L0TransmissionFX.intensity) : 0
        )
        encoder.setRenderPipelineState(pipeline)
        encoder.setFragmentTexture(texture, index: 0)
        encoder.setFragmentBytes(&uniforms, length: MemoryLayout<Uniforms>.stride, index: 0)
        encoder.drawPrimitives(type: .triangleStrip, vertexStart: 0, vertexCount: 4)
        encoder.endEncoding()
        buffer.present(drawable)
        buffer.commit()
        _ = player
    }
}

private struct K1L0BareVideoPlayerView: UIViewRepresentable {
    let player: AVPlayer

    func makeUIView(context: Context) -> PlayerView {
        let view = PlayerView()
        view.playerLayer.player = player
        // Aspect-fill so the transmission covers the full-height playback box
        // (minor center crop) instead of letterboxing — the tattered edges
        // then tear real video on all four sides, edge to edge.
        view.playerLayer.videoGravity = .resizeAspectFill
        view.isUserInteractionEnabled = false
        view.backgroundColor = .clear
        return view
    }

    func updateUIView(_ uiView: PlayerView, context: Context) {
        if uiView.playerLayer.player !== player {
            uiView.playerLayer.player = player
        }
    }

    final class PlayerView: UIView {
        override static var layerClass: AnyClass {
            AVPlayerLayer.self
        }

        var playerLayer: AVPlayerLayer {
            layer as! AVPlayerLayer
        }
    }
}
#endif

#if canImport(AppKit) && !canImport(UIKit)
private struct K1L0BareVideoPlayerNSView: NSViewRepresentable {
    let player: AVPlayer

    func makeNSView(context: Context) -> PlayerView {
        let view = PlayerView()
        view.playerLayer.player = player
        view.playerLayer.videoGravity = .resizeAspect
        return view
    }

    func updateNSView(_ nsView: PlayerView, context: Context) {
        if nsView.playerLayer.player !== player {
            nsView.playerLayer.player = player
        }
    }

    final class PlayerView: NSView {
        let playerLayer = AVPlayerLayer()

        override init(frame frameRect: NSRect) {
            super.init(frame: frameRect)
            wantsLayer = true
            layer = CALayer()
            layer?.backgroundColor = NSColor.clear.cgColor
            playerLayer.backgroundColor = NSColor.clear.cgColor
            playerLayer.videoGravity = .resizeAspect
            layer?.addSublayer(playerLayer)
        }

        required init?(coder: NSCoder) {
            nil
        }

        override func layout() {
            super.layout()
            playerLayer.frame = bounds
        }
    }
}
#endif

// Reusable sticky drag handle for full-screen panels. The content scrolls
// behind it; this view stays pinned to the top of the panel and fires onDismiss
// when pulled down past the threshold.
private struct StickyPanelHeader: View {
    let iconName: String
    let title: String
    let onClose: () -> Void

    var body: some View {
        VStack(spacing: 4) {
            RoundedRectangle(cornerRadius: 3, style: .continuous)
                .fill(Color.white.opacity(0.46))
                .frame(width: 44, height: 5)
                .padding(.top, 8)
            HStack(spacing: 10) {
                Image(systemName: iconName)
                    .font(.system(size: 18, weight: .black))
                    .foregroundStyle(.white)
                Text(title)
                    .font(.system(size: 20, weight: .black, design: .rounded))
                    .foregroundStyle(.white)
                Spacer()
                Button(action: onClose) {
                    Image(systemName: "xmark")
                        .font(.system(size: 14, weight: .black))
                        .foregroundStyle(.white)
                        .frame(width: 34, height: 34)
                        .background(Color.black.opacity(0.46), in: Circle())
                        .overlay(Circle().stroke(.white.opacity(0.28), lineWidth: 1))
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 16)
            .padding(.bottom, 10)
        }
        .frame(maxWidth: .infinity)
        .background(Color.black.opacity(0.001)) // near-clear but hit-testable
        .contentShape(Rectangle())                // ensure drag/tap hit the whole header
        .overlay(
            Rectangle().fill(Color.white.opacity(0.10)).frame(height: 1),
            alignment: .bottom
        )
        .gesture(
            DragGesture(minimumDistance: 14)
                .onEnded { value in
                    if value.translation.height > 70 && abs(value.translation.width) < value.translation.height {
                        onClose()
                    }
                }
        )
    }
}

private struct HomePanelHeader: View {
    let onClose: () -> Void

    var body: some View {
        VStack(spacing: 5) {
            RoundedRectangle(cornerRadius: 3, style: .continuous)
                .fill(Color.white.opacity(0.34))
                .frame(width: 44, height: 5)
                .padding(.top, 8)
            ZStack {
                HStack {
                    Button(action: onClose) {
                        Image(systemName: "xmark")
                            .font(.system(size: 15, weight: .black))
                            .foregroundStyle(.white)
                            .frame(width: 38, height: 38)
                    }
                    .buttonStyle(.plain)
                    Spacer()
                }
            }
            .padding(.horizontal, 16)
            .padding(.bottom, 10)
        }
        .frame(maxWidth: .infinity)
        .background(Color.black.opacity(0.001))
        .contentShape(Rectangle())
        .overlay(
            Rectangle().fill(Color.white.opacity(0.08)).frame(height: 1),
            alignment: .bottom
        )
    }
}

private struct UserPanelHeader: View {
    @ObservedObject private var saveStore = K1L0UserMetadataSaveStore.shared
    let title: String
    var tabsMode: Bool = false
    let onClose: () -> Void
    var onSave: (() -> Void)? = nil
    var onBack: (() -> Void)? = nil

    var body: some View {
        VStack(spacing: 5) {
            if !tabsMode && onBack == nil {
                RoundedRectangle(cornerRadius: 3, style: .continuous)
                    .fill(Color.white.opacity(0.34))
                    .frame(width: 44, height: 5)
                    .padding(.top, 8)
            }
            ZStack {
                Text(title)
                    .font(.system(size: 20, weight: .black, design: .rounded))
                    .foregroundStyle(.white)
                    .frame(maxWidth: .infinity, alignment: .center)
                HStack {
                    if let onBack {
                        Button(action: onBack) {
                            HStack(spacing: 4) {
                                Image(systemName: "chevron.left")
                                    .font(.system(size: 16, weight: .black))
                                Text("Back")
                                    .font(.system(size: 15, weight: .bold))
                            }
                            .foregroundStyle(.white)
                            .frame(height: 38)
                        }
                        .buttonStyle(.plain)
                        .disabled(saveStore.isSaving)
                    } else if !tabsMode {
                        Button(action: onClose) {
                            Image(systemName: "xmark")
                                .font(.system(size: 15, weight: .black))
                                .foregroundStyle(.white)
                                .frame(width: 38, height: 38)
                        }
                        .buttonStyle(.plain)
                        .disabled(saveStore.isSaving)
                    } else {
                        Color.clear.frame(width: 38, height: 38)
                    }
                    Spacer()
                    if let onSave {
                        Button(action: {
                            if !saveStore.isSaving {
                                onSave()
                            }
                        }) {
                            HStack(spacing: 6) {
                                if saveStore.isSaving {
                                    ProgressView()
                                        .progressViewStyle(.circular)
                                        .tint(.white)
                                        .scaleEffect(0.8)
                                    Text("Saving...")
                                } else {
                                    Text("Save")
                                }
                            }
                            .font(.system(size: 15, weight: .black))
                            .foregroundStyle(.white)
                            .padding(.horizontal, 12)
                            .frame(height: 38)
                        }
                        .buttonStyle(.plain)
                        .disabled(saveStore.isSaving)
                    } else {
                        Color.clear.frame(width: 54, height: 38)
                    }
                }
            }
            .padding(.horizontal, 16)
            .padding(.bottom, 10)
        }
        .frame(maxWidth: .infinity)
        .background(Color.black.opacity(0.001))
        .contentShape(Rectangle())
        .overlay(
            Rectangle().fill(Color.white.opacity(tabsMode ? 0 : 0.08)).frame(height: 1),
            alignment: .bottom
        )
    }
}

// iOS-26 Liquid Glass when available, ultraThinMaterial fallback otherwise.
// Used by the persistent floating home/map and user buttons in the corners.
private struct LiquidGlassCircle: ViewModifier {
    func body(content: Content) -> some View {
        if #available(iOS 26.0, macOS 26.0, *) {
            content
                .glassEffect(.regular.interactive(), in: Circle())
        } else {
            content
                .background(.ultraThinMaterial, in: Circle())
                .overlay(Circle().stroke(.white.opacity(0.24), lineWidth: 1))
        }
    }
}

// Detects bounce-overscroll at the top of a ScrollView. Place as the first
// child inside the ScrollView. Reports the scroll-content top position into
// the parent's named coordinate space; fires onDismiss when the user pulls
// past `threshold` (rubber-band sheet-dismiss gesture).
private struct PullToDismissTopAnchor: View {
    let panelCoordinateSpace: String
    let onDismiss: () -> Void
    let threshold: CGFloat
    @State private var baseline: CGFloat? = nil
    @State private var fired = false

    var body: some View {
        GeometryReader { proxy in
            let y = proxy.frame(in: .named(panelCoordinateSpace)).minY
            Color.clear
                .preference(key: PullOffsetKey.self, value: y)
        }
        .frame(height: 0)
    }
}

private struct PullOffsetKey: PreferenceKey {
    static var defaultValue: CGFloat = 0
    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) { value = nextValue() }
}

#if canImport(UIKit)
private struct CameraRollSaveMediaItem {
    let videoUrlString: String
    let audioUrlString: String?
    var overlayText: String = ""
    var overlayTransform: TransmissionTextTransform = TransmissionTextTransform()
}

private struct LocalCameraRollSaveMediaItem {
    let videoURL: URL
    let audioURL: URL?
    let overlayText: String
    let overlayTransform: TransmissionTextTransform
}

private func cameraRollSaveError(_ message: String) -> NSError {
    NSError(domain: "K1L0CameraRollSave", code: 1, userInfo: [NSLocalizedDescriptionKey: message])
}

private func k1l0SaveLog(_ message: String) {
    let line = "\(ISO8601DateFormatter().string(from: Date())) \(message)\n"
    NSLog("K1L0Save %@", message)
    guard let documents = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first else { return }
    let url = documents.appendingPathComponent("k1l0-save.log")
    guard let data = line.data(using: .utf8) else { return }
    if FileManager.default.fileExists(atPath: url.path),
       let handle = try? FileHandle(forWritingTo: url) {
        defer { try? handle.close() }
        try? handle.seekToEnd()
        try? handle.write(contentsOf: data)
    } else {
        try? data.write(to: url, options: .atomic)
    }
}

private func k1l0SaveErrorDescription(_ error: Error) -> String {
    let nsError = error as NSError
    return "\(nsError.domain) code=\(nsError.code) \(nsError.localizedDescription) userInfo=\(nsError.userInfo)"
}

private final class K1L0CameraRollSaveDelegate: NSObject {
    let completion: (String) -> Void

    init(completion: @escaping (String) -> Void) {
        self.completion = completion
    }

    @objc func video(_ videoPath: String, didFinishSavingWithError error: Error?, contextInfo: UnsafeMutableRawPointer) {
        if let error {
            completion("save failed: \(error.localizedDescription)")
        } else {
            completion("saved to camera roll")
        }
    }
}

private final class K1L0CameraRollSaveStatusOverlay {
    private static var window: UIWindow?
    private static var label: UILabel?
    private static var spinner: UIActivityIndicatorView?

    static func show(_ status: String) {
        DispatchQueue.main.async {
            let currentWindow = window ?? makeWindow()
            window = currentWindow
            label?.text = status
            spinner?.startAnimating()
            currentWindow.isHidden = false
            currentWindow.alpha = 1
        }
    }

    static func hide(after delay: TimeInterval = 0.55) {
        DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
            UIView.animate(withDuration: 0.18, animations: {
                window?.alpha = 0
            }, completion: { _ in
                spinner?.stopAnimating()
                window?.isHidden = true
            })
        }
    }

    private static func makeWindow() -> UIWindow {
        let scene = UIApplication.shared.connectedScenes
            .compactMap { $0 as? UIWindowScene }
            .first { $0.activationState == .foregroundActive }
            ?? UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first
        let nextWindow = scene.map { UIWindow(windowScene: $0) } ?? UIWindow(frame: UIScreen.main.bounds)
        nextWindow.windowLevel = .alert + 4
        nextWindow.backgroundColor = .clear

        let root = UIViewController()
        root.view.backgroundColor = .clear
        let panel = UIVisualEffectView(effect: UIBlurEffect(style: .systemUltraThinMaterialDark))
        panel.translatesAutoresizingMaskIntoConstraints = false
        panel.layer.cornerRadius = 18
        panel.clipsToBounds = true

        let indicator = UIActivityIndicatorView(style: .large)
        indicator.translatesAutoresizingMaskIntoConstraints = false
        indicator.color = .white

        let text = UILabel()
        text.translatesAutoresizingMaskIntoConstraints = false
        text.textColor = .white
        text.font = .monospacedSystemFont(ofSize: 13, weight: .black)
        text.textAlignment = .center
        text.numberOfLines = 2

        panel.contentView.addSubview(indicator)
        panel.contentView.addSubview(text)
        root.view.addSubview(panel)
        nextWindow.rootViewController = root

        NSLayoutConstraint.activate([
            panel.centerXAnchor.constraint(equalTo: root.view.centerXAnchor),
            panel.centerYAnchor.constraint(equalTo: root.view.centerYAnchor),
            panel.widthAnchor.constraint(equalToConstant: 230),
            panel.heightAnchor.constraint(equalToConstant: 132),
            indicator.centerXAnchor.constraint(equalTo: panel.contentView.centerXAnchor),
            indicator.topAnchor.constraint(equalTo: panel.contentView.topAnchor, constant: 28),
            text.leadingAnchor.constraint(equalTo: panel.contentView.leadingAnchor, constant: 14),
            text.trailingAnchor.constraint(equalTo: panel.contentView.trailingAnchor, constant: -14),
            text.topAnchor.constraint(equalTo: indicator.bottomAnchor, constant: 14)
        ])

        label = text
        spinner = indicator
        return nextWindow
    }
}

private struct CameraRollSaveButton: View {
    let mediaItems: [CameraRollSaveMediaItem]
    let iconOnly: Bool
    @State private var status = ""
    @State private var saving = false
    @State private var saveDelegate: K1L0CameraRollSaveDelegate?

    init(videoUrlString: String, audioUrlString: String? = nil, overlayText: String = "", overlayTransform: TransmissionTextTransform = TransmissionTextTransform()) {
        self.mediaItems = [CameraRollSaveMediaItem(videoUrlString: videoUrlString, audioUrlString: audioUrlString, overlayText: overlayText, overlayTransform: overlayTransform)]
        self.iconOnly = false
    }

    init(mediaItems: [CameraRollSaveMediaItem], iconOnly: Bool = false) {
        self.mediaItems = mediaItems.filter { !$0.videoUrlString.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
        self.iconOnly = iconOnly
    }

    var body: some View {
        if iconOnly {
            Button {
                saveVideo()
            } label: {
                Image(systemName: saving ? "hourglass" : "square.and.arrow.down")
                    .font(.system(size: 16, weight: .black))
                    .foregroundStyle(.white)
                    .frame(width: 44, height: 44)
                    .background(Color.black.opacity(0.38), in: Circle())
            }
            .buttonStyle(.plain)
            .disabled(saving)
        } else {
        VStack(alignment: .trailing, spacing: 4) {
            Button {
                saveVideo()
            } label: {
                Text(saving ? "[ SAVING… ]" : "[ SAVE TO CAMERA ROLL ]")
                    .font(.system(size: 13, weight: .black, design: .monospaced))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 14)
                    .frame(minHeight: 42)
                    .background(Color.black.opacity(0.58))
            }
            .buttonStyle(.plain)
            .disabled(saving)

            if !status.isEmpty {
                Text(status)
                    .font(.system(size: 10, weight: .bold, design: .monospaced))
                    .foregroundStyle(.white.opacity(0.72))
                    .lineLimit(1)
            }
        }
        }
    }

    private func saveVideo() {
        guard !saving, !mediaItems.isEmpty else {
            status = "bad video url"
            return
        }
        k1l0SaveLog("save tapped items=\(mediaItems.count)")
        for (index, item) in mediaItems.enumerated() {
            let hasOverlay = !item.overlayText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            let hasAudio = !(item.audioUrlString?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ?? true)
            k1l0SaveLog("item \(index) video=\(item.videoUrlString) audio=\(hasAudio ? item.audioUrlString ?? "" : "none") overlay=\(hasOverlay ? "yes" : "no") overlayText=\(item.overlayText)")
        }
        saving = true
        updateSavingStatus(mediaItems.count > 1 ? "downloading chain…" : "downloading…")
        downloadMediaItems(mediaItems) { result in
            switch result {
            case .failure(let error):
                DispatchQueue.main.async {
                    saving = false
                    finishSavingStatus(error.localizedDescription)
                }
            case .success(let localItems):
                let hasOverlay = localItems.contains { !$0.overlayText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
                let hasAudio = localItems.contains { $0.audioURL != nil }
                k1l0SaveLog("downloaded items=\(localItems.count) overlay=\(hasOverlay ? "yes" : "no") audio=\(hasAudio ? "yes" : "no")")
                if localItems.count == 1,
                   localItems[0].audioURL == nil,
                   localItems[0].overlayText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    k1l0SaveLog("saving raw video; no overlay/audio requested")
                    saveLocalVideo(localItems[0].videoURL)
                    return
                }
                if localItems.count == 1 {
                    DispatchQueue.main.async {
                        updateSavingStatus("exporting text \(hasOverlay ? "yes" : "no") audio \(hasAudio ? "yes" : "no")…")
                    }
                    exportSingleVideo(localItems[0]) { exportResult in
                        switch exportResult {
                        case .failure(let error):
                            DispatchQueue.main.async {
                                saving = false
                                finishSavingStatus(error.localizedDescription)
                                k1l0SaveLog("single export failed \(k1l0SaveErrorDescription(error))")
                            }
                        case .success(let outputURL):
                            saveLocalVideo(outputURL)
                        }
                    }
                    return
                }
                DispatchQueue.main.async {
                    updateSavingStatus("exporting chain text \(hasOverlay ? "yes" : "no") audio \(hasAudio ? "yes" : "no")…")
                }
                exportStitchedVideo(localItems) { exportResult in
                    switch exportResult {
                    case .failure(let error):
                        DispatchQueue.main.async {
                            saving = false
                            finishSavingStatus(error.localizedDescription)
                        }
                    case .success(let outputURL):
                        saveLocalVideo(outputURL)
                    }
                }
            }
        }
    }

    private func updateSavingStatus(_ nextStatus: String) {
        status = nextStatus
        K1L0CameraRollSaveStatusOverlay.show(nextStatus)
    }

    private func finishSavingStatus(_ nextStatus: String) {
        status = nextStatus
        K1L0CameraRollSaveStatusOverlay.show(nextStatus)
        K1L0CameraRollSaveStatusOverlay.hide()
    }

    private func downloadMediaItems(_ items: [CameraRollSaveMediaItem], completion: @escaping (Result<[LocalCameraRollSaveMediaItem], Error>) -> Void) {
        let group = DispatchGroup()
        let lock = NSLock()
        var videoURLs = Array<URL?>(repeating: nil, count: items.count)
        var audioURLs = Array<URL?>(repeating: nil, count: items.count)
        var firstError: String?

        func download(_ urlString: String, fallbackExtension: String, completion: @escaping (URL?) -> Void) {
            guard let remoteURL = URL(string: urlString) else {
                completion(nil)
                return
            }
            URLSession.shared.downloadTask(with: remoteURL) { localURL, _, error in
                guard error == nil, let localURL else {
                    completion(nil)
                    return
                }
                let pathExtension = remoteURL.pathExtension.isEmpty ? fallbackExtension : remoteURL.pathExtension
                let target = URL(fileURLWithPath: NSTemporaryDirectory())
                    .appendingPathComponent("k1l0-save-\(UUID().uuidString).\(pathExtension)")
                do {
                    try? FileManager.default.removeItem(at: target)
                    try FileManager.default.copyItem(at: localURL, to: target)
                    completion(target)
                } catch {
                    completion(nil)
                }
            }.resume()
        }

        for (index, item) in items.enumerated() {
            group.enter()
                download(item.videoUrlString, fallbackExtension: "mp4") { localURL in
                    lock.lock()
                    if let localURL {
                        videoURLs[index] = localURL
                        k1l0SaveLog("video download ok index=\(index) local=\(localURL.path)")
                    } else if firstError == nil {
                        k1l0SaveLog("video download failed index=\(index) url=\(item.videoUrlString)")
                        firstError = "video download failed"
                    }
                lock.unlock()
                group.leave()
            }

            if let audioUrlString = item.audioUrlString?.trimmingCharacters(in: .whitespacesAndNewlines), !audioUrlString.isEmpty {
                group.enter()
                download(audioUrlString, fallbackExtension: "m4a") { localURL in
                    if let localURL {
                        k1l0SaveLog("audio download ok index=\(index) local=\(localURL.path)")
                        prepareAudioForMux(localURL) { preparedURL in
                            lock.lock()
                            if let preparedURL {
                                audioURLs[index] = preparedURL
                                k1l0SaveLog("audio prepared ok index=\(index) local=\(preparedURL.path)")
                            } else if firstError == nil {
                                k1l0SaveLog("audio prepare failed index=\(index) url=\(audioUrlString)")
                                firstError = "audio prepare failed"
                            }
                            lock.unlock()
                            group.leave()
                        }
                    } else {
                        lock.lock()
                        if firstError == nil {
                            k1l0SaveLog("audio download failed index=\(index) url=\(audioUrlString)")
                            firstError = "audio download failed"
                        }
                        lock.unlock()
                        group.leave()
                    }
                }
            }
        }

        group.notify(queue: .main) {
            if let firstError {
                completion(.failure(cameraRollSaveError(firstError)))
                return
            }
            let localItems = videoURLs.enumerated().compactMap { index, videoURL -> LocalCameraRollSaveMediaItem? in
                guard let videoURL else { return nil }
                return LocalCameraRollSaveMediaItem(videoURL: videoURL, audioURL: audioURLs[index], overlayText: items[index].overlayText, overlayTransform: items[index].overlayTransform)
            }
            guard localItems.count == items.count else {
                completion(.failure(cameraRollSaveError("video download failed")))
                return
            }
            completion(.success(localItems))
        }
    }

    private func prepareAudioForMux(_ localURL: URL, completion: @escaping (URL?) -> Void) {
        let pathExtension = localURL.pathExtension.lowercased()
        guard pathExtension == "mp3" else {
            completion(localURL)
            return
        }
        let asset = AVURLAsset(url: localURL)
        let outputURL = URL(fileURLWithPath: NSTemporaryDirectory())
            .appendingPathComponent("k1l0-audio-\(UUID().uuidString).m4a")
        try? FileManager.default.removeItem(at: outputURL)
        guard let exporter = AVAssetExportSession(asset: asset, presetName: AVAssetExportPresetAppleM4A) else {
            k1l0SaveLog("audio transcode unavailable input=\(localURL.path)")
            completion(nil)
            return
        }
        exporter.outputURL = outputURL
        exporter.outputFileType = .m4a
        exporter.exportAsynchronously {
            DispatchQueue.main.async {
                switch exporter.status {
                case .completed:
                    k1l0SaveLog("audio transcode completed output=\(outputURL.path)")
                    completion(outputURL)
                case .failed, .cancelled:
                    let exportError = exporter.error.map { k1l0SaveErrorDescription($0) } ?? "unknown"
                    k1l0SaveLog("audio transcode failed \(exportError)")
                    completion(nil)
                default:
                    k1l0SaveLog("audio transcode failed status=\(exporter.status.rawValue)")
                    completion(nil)
                }
            }
        }
    }

    private func exportSingleVideo(_ item: LocalCameraRollSaveMediaItem, completion: @escaping (Result<URL, Error>) -> Void) {
        DispatchQueue.global(qos: .userInitiated).async {
            // Saved transmissions mirror the in-app player: the clip loops to
            // fill 15 seconds, the music rides the whole way, and the glitch
            // FX are baked in (pass 1), then the plot/options text is burned
            // on top (pass 2 — the CI filter handler and the Core Animation
            // overlay can't share one AVVideoComposition).
            let targetDuration = CMTime(seconds: 15, preferredTimescale: 600)
            let composition = AVMutableComposition()
            let videoAsset = AVURLAsset(url: item.videoURL)
            guard let sourceVideoTrack = videoAsset.tracks(withMediaType: .video).first,
                  let compositionVideoTrack = composition.addMutableTrack(
                    withMediaType: .video,
                    preferredTrackID: kCMPersistentTrackID_Invalid
                  ) else {
                DispatchQueue.main.async { completion(.failure(cameraRollSaveError("missing video track"))) }
                return
            }
            compositionVideoTrack.preferredTransform = sourceVideoTrack.preferredTransform

            do {
                let videoRange = sourceVideoTrack.timeRange
                guard videoRange.duration.seconds > 0.05 else {
                    throw NSError(domain: "K1L0Save", code: 3, userInfo: [NSLocalizedDescriptionKey: "empty video track"])
                }
                var cursor = CMTime.zero
                while cursor < targetDuration {
                    let insert = CMTimeMinimum(videoRange.duration, targetDuration - cursor)
                    try compositionVideoTrack.insertTimeRange(
                        CMTimeRange(start: videoRange.start, duration: insert),
                        of: sourceVideoTrack,
                        at: cursor
                    )
                    cursor = cursor + insert
                }
                k1l0SaveLog("single video looped to \(cursor.seconds)s from \(videoRange.duration.seconds)s source")

                if let audioURL = item.audioURL,
                   let compositionAudioTrack = composition.addMutableTrack(withMediaType: .audio, preferredTrackID: kCMPersistentTrackID_Invalid) {
                    let sourceAudioAsset = AVURLAsset(url: audioURL)
                    guard let sourceAudioTrack = sourceAudioAsset.tracks(withMediaType: .audio).first else {
                        throw NSError(domain: "K1L0Save", code: 2, userInfo: [NSLocalizedDescriptionKey: "missing audio track"])
                    }
                    let audioRange = sourceAudioTrack.timeRange
                    var audioCursor = CMTime.zero
                    while audioCursor < cursor, audioRange.duration.seconds > 0.05 {
                        let insert = CMTimeMinimum(audioRange.duration, cursor - audioCursor)
                        try compositionAudioTrack.insertTimeRange(
                            CMTimeRange(start: audioRange.start, duration: insert),
                            of: sourceAudioTrack,
                            at: audioCursor
                        )
                        audioCursor = audioCursor + insert
                    }
                    k1l0SaveLog("single audio looped to \(audioCursor.seconds)s")
                }
            } catch {
                k1l0SaveLog("mux failed \(k1l0SaveErrorDescription(error))")
                DispatchQueue.main.async { completion(.failure(cameraRollSaveError("mux failed: \(k1l0SaveErrorDescription(error))"))) }
                return
            }

            let totalRange = CMTimeRange(start: .zero, duration: compositionVideoTrack.timeRange.duration)

            if let fxComposition = K1L0TransmissionFX.bakedComposition(for: composition, durationSeconds: totalRange.duration.seconds) {
                self.exportComposition(
                    composition,
                    filenamePrefix: "k1l0-fxbake",
                    videoComposition: fxComposition,
                    timeRange: totalRange
                ) { fxResult in
                    switch fxResult {
                    case .success(let fxURL):
                        self.exportTextOverlayPass(sourceURL: fxURL, item: item, deleteSourceWhenDone: true, completion: completion)
                    case .failure(let error):
                        // FX bake failed — fall back to the clean text-only export.
                        k1l0SaveLog("fx bake failed, exporting clean: \(k1l0SaveErrorDescription(error))")
                        self.exportPlainTextOverlay(composition: composition,
                                                    compositionVideoTrack: compositionVideoTrack,
                                                    sourceVideoTrack: sourceVideoTrack,
                                                    item: item,
                                                    completion: completion)
                    }
                }
            } else {
                self.exportPlainTextOverlay(composition: composition,
                                            compositionVideoTrack: compositionVideoTrack,
                                            sourceVideoTrack: sourceVideoTrack,
                                            item: item,
                                            completion: completion)
            }
        }
    }

    // Pass 2 when FX baking succeeded: wrap the FX'd temp file in a fresh
    // composition and burn the plot/options overlay onto it.
    private func exportTextOverlayPass(sourceURL: URL, item: LocalCameraRollSaveMediaItem, deleteSourceWhenDone: Bool, completion: @escaping (Result<URL, Error>) -> Void) {
        let asset = AVURLAsset(url: sourceURL)
        let composition = AVMutableComposition()
        guard let sourceVideoTrack = asset.tracks(withMediaType: .video).first,
              let compositionVideoTrack = composition.addMutableTrack(withMediaType: .video, preferredTrackID: kCMPersistentTrackID_Invalid) else {
            if deleteSourceWhenDone { try? FileManager.default.removeItem(at: sourceURL) }
            DispatchQueue.main.async { completion(.failure(cameraRollSaveError("fx pass missing video track"))) }
            return
        }
        do {
            try compositionVideoTrack.insertTimeRange(sourceVideoTrack.timeRange, of: sourceVideoTrack, at: .zero)
            compositionVideoTrack.preferredTransform = sourceVideoTrack.preferredTransform
            if let sourceAudioTrack = asset.tracks(withMediaType: .audio).first,
               let compositionAudioTrack = composition.addMutableTrack(withMediaType: .audio, preferredTrackID: kCMPersistentTrackID_Invalid) {
                try compositionAudioTrack.insertTimeRange(sourceAudioTrack.timeRange, of: sourceAudioTrack, at: .zero)
            }
        } catch {
            if deleteSourceWhenDone { try? FileManager.default.removeItem(at: sourceURL) }
            DispatchQueue.main.async { completion(.failure(cameraRollSaveError("fx pass mux failed: \(k1l0SaveErrorDescription(error))"))) }
            return
        }
        let videoComposition = makeVideoComposition(
            compositionTrack: compositionVideoTrack,
            sourceTrack: sourceVideoTrack,
            duration: compositionVideoTrack.timeRange.duration,
            renderSize: normalizedRenderSize(for: sourceVideoTrack),
            overlayText: item.overlayText,
            overlayTransform: item.overlayTransform
        )
        exportComposition(
            composition,
            filenamePrefix: "k1l0-single",
            videoComposition: videoComposition,
            timeRange: CMTimeRange(start: .zero, duration: compositionVideoTrack.timeRange.duration)
        ) { result in
            if deleteSourceWhenDone { try? FileManager.default.removeItem(at: sourceURL) }
            completion(result)
        }
    }

    // Single-pass fallback: looped video + text overlay, no FX bake.
    private func exportPlainTextOverlay(composition: AVMutableComposition,
                                        compositionVideoTrack: AVMutableCompositionTrack,
                                        sourceVideoTrack: AVAssetTrack,
                                        item: LocalCameraRollSaveMediaItem,
                                        completion: @escaping (Result<URL, Error>) -> Void) {
        let videoComposition = makeVideoComposition(
            compositionTrack: compositionVideoTrack,
            sourceTrack: sourceVideoTrack,
            duration: compositionVideoTrack.timeRange.duration,
            renderSize: normalizedRenderSize(for: sourceVideoTrack),
            overlayText: item.overlayText,
            overlayTransform: item.overlayTransform
        )
        exportComposition(
            composition,
            filenamePrefix: "k1l0-single",
            videoComposition: videoComposition,
            timeRange: CMTimeRange(start: .zero, duration: compositionVideoTrack.timeRange.duration),
            completion: completion
        )
    }

    private func exportStitchedVideo(_ items: [LocalCameraRollSaveMediaItem], completion: @escaping (Result<URL, Error>) -> Void) {
        DispatchQueue.global(qos: .userInitiated).async {
            let composition = AVMutableComposition()
            guard let compositionVideoTrack = composition.addMutableTrack(
                withMediaType: .video,
                preferredTrackID: kCMPersistentTrackID_Invalid
            ) else {
                DispatchQueue.main.async { completion(.failure(cameraRollSaveError("stitch failed"))) }
                return
            }
            let compositionAudioTrack = composition.addMutableTrack(
                withMediaType: .audio,
                preferredTrackID: kCMPersistentTrackID_Invalid
            )
            var cursor = CMTime.zero
            var didSetTransform = false
            var firstSourceTrack: AVAssetTrack?
            var renderSize = CGSize(width: 1080, height: 1920)
            var overlaySegments: [(text: String, start: CMTime, duration: CMTime, transform: TransmissionTextTransform)] = []
            let chainAudioURL = items.first?.audioURL

            do {
                for item in items {
                    let videoAsset = AVURLAsset(url: item.videoURL)
                    guard let sourceVideoTrack = videoAsset.tracks(withMediaType: .video).first else {
                        throw NSError(domain: "K1L0Save", code: 1, userInfo: [NSLocalizedDescriptionKey: "missing video track"])
                    }
                    let videoRange = sourceVideoTrack.timeRange
                    let duration = videoRange.duration
                    k1l0SaveLog("chain video insert start=\(videoRange.start.seconds) duration=\(duration.seconds) assetDuration=\(videoAsset.duration.seconds)")
                    try compositionVideoTrack.insertTimeRange(
                        videoRange,
                        of: sourceVideoTrack,
                        at: cursor
                    )
                    if !didSetTransform {
                        compositionVideoTrack.preferredTransform = sourceVideoTrack.preferredTransform
                        firstSourceTrack = sourceVideoTrack
                        renderSize = normalizedRenderSize(for: sourceVideoTrack)
                        didSetTransform = true
                    }
                    let cleanOverlay = item.overlayText.trimmingCharacters(in: .whitespacesAndNewlines)
                    if !cleanOverlay.isEmpty {
                        overlaySegments.append((text: cleanOverlay, start: cursor, duration: duration, transform: item.overlayTransform))
                    }

                    if chainAudioURL == nil, let audioURL = item.audioURL {
                        let sourceAudioAsset = AVURLAsset(url: audioURL)
                        guard let sourceAudioTrack = sourceAudioAsset.tracks(withMediaType: .audio).first else {
                            throw NSError(domain: "K1L0Save", code: 2, userInfo: [NSLocalizedDescriptionKey: "missing audio track"])
                        }
                        let sourceAudioRange = sourceAudioTrack.timeRange
                        let muxDuration = CMTimeMinimum(duration, sourceAudioRange.duration)
                        k1l0SaveLog("chain audio insert start=\(sourceAudioRange.start.seconds) duration=\(muxDuration.seconds) trackDuration=\(sourceAudioRange.duration.seconds)")
                        try compositionAudioTrack?.insertTimeRange(
                            CMTimeRange(start: sourceAudioRange.start, duration: muxDuration),
                            of: sourceAudioTrack,
                            at: cursor
                        )
                    } else if chainAudioURL == nil, let embeddedAudioTrack = videoAsset.tracks(withMediaType: .audio).first {
                        let embeddedRange = embeddedAudioTrack.timeRange
                        try compositionAudioTrack?.insertTimeRange(
                            CMTimeRange(start: embeddedRange.start, duration: CMTimeMinimum(duration, embeddedRange.duration)),
                            of: embeddedAudioTrack,
                            at: cursor
                        )
                    }
                    cursor = cursor + duration
                }
                if let chainAudioURL,
                   let compositionAudioTrack {
                    let sourceAudioAsset = AVURLAsset(url: chainAudioURL)
                    guard let sourceAudioTrack = sourceAudioAsset.tracks(withMediaType: .audio).first else {
                        throw NSError(domain: "K1L0Save", code: 2, userInfo: [NSLocalizedDescriptionKey: "missing original audio track"])
                    }
                    let sourceAudioRange = sourceAudioTrack.timeRange
                    guard sourceAudioRange.duration.seconds.isFinite, sourceAudioRange.duration > .zero else {
                        throw NSError(domain: "K1L0Save", code: 2, userInfo: [NSLocalizedDescriptionKey: "bad original audio duration"])
                    }
                    var audioCursor = CMTime.zero
                    while audioCursor < cursor {
                        let remaining = cursor - audioCursor
                        let muxDuration = CMTimeMinimum(sourceAudioRange.duration, remaining)
                        k1l0SaveLog("chain original audio insert start=\(sourceAudioRange.start.seconds) duration=\(muxDuration.seconds) at=\(audioCursor.seconds)")
                        try compositionAudioTrack.insertTimeRange(
                            CMTimeRange(start: sourceAudioRange.start, duration: muxDuration),
                            of: sourceAudioTrack,
                            at: audioCursor
                        )
                        audioCursor = audioCursor + muxDuration
                    }
                }
            } catch {
                DispatchQueue.main.async { completion(.failure(cameraRollSaveError("stitch failed: \(error.localizedDescription)"))) }
                return
            }

            let videoComposition: AVVideoComposition?
            if let firstSourceTrack {
                videoComposition = makeVideoComposition(
                    compositionTrack: compositionVideoTrack,
                    sourceTrack: firstSourceTrack,
                    duration: cursor,
                    renderSize: renderSize,
                    overlaySegments: overlaySegments
                )
            } else {
                videoComposition = nil
            }
            exportComposition(composition, filenamePrefix: "k1l0-chain", videoComposition: videoComposition, timeRange: nil, completion: completion)
        }
    }

    private func normalizedRenderSize(for track: AVAssetTrack) -> CGSize {
        let transformed = track.naturalSize.applying(track.preferredTransform)
        let width = abs(transformed.width)
        let height = abs(transformed.height)
        if width > 0, height > 0 {
            return CGSize(width: width, height: height)
        }
        return track.naturalSize
    }

    private func makeVideoComposition(
        compositionTrack: AVCompositionTrack,
        sourceTrack: AVAssetTrack,
        duration: CMTime,
        renderSize: CGSize,
        overlayText: String,
        overlayTransform: TransmissionTextTransform = TransmissionTextTransform()
    ) -> AVMutableVideoComposition? {
        let cleanText = overlayText.trimmingCharacters(in: .whitespacesAndNewlines)
        return makeVideoComposition(
            compositionTrack: compositionTrack,
            sourceTrack: sourceTrack,
            duration: duration,
            renderSize: renderSize,
            overlaySegments: cleanText.isEmpty ? [] : [(text: cleanText, start: .zero, duration: duration, transform: overlayTransform)]
        )
    }

    private func makeVideoComposition(
        compositionTrack: AVCompositionTrack,
        sourceTrack: AVAssetTrack,
        duration: CMTime,
        renderSize: CGSize,
        overlaySegments: [(text: String, start: CMTime, duration: CMTime, transform: TransmissionTextTransform)]
    ) -> AVMutableVideoComposition? {
        let videoComposition = AVMutableVideoComposition()
        videoComposition.renderSize = renderSize
        videoComposition.frameDuration = CMTime(value: 1, timescale: 30)

        let instruction = AVMutableVideoCompositionInstruction()
        instruction.timeRange = CMTimeRange(start: .zero, duration: duration)
        let layerInstruction = AVMutableVideoCompositionLayerInstruction(assetTrack: compositionTrack)
        layerInstruction.setTransform(sourceTrack.preferredTransform, at: .zero)
        instruction.layerInstructions = [layerInstruction]
        videoComposition.instructions = [instruction]

        let cleanSegments = overlaySegments
            .map { (text: $0.text.trimmingCharacters(in: .whitespacesAndNewlines), start: $0.start, duration: $0.duration, transform: $0.transform.clamped()) }
            .filter { !$0.text.isEmpty }
        guard !cleanSegments.isEmpty else { return videoComposition }

        let parentLayer = CALayer()
        parentLayer.frame = CGRect(origin: .zero, size: renderSize)
        let videoLayer = CALayer()
        videoLayer.frame = parentLayer.frame
        parentLayer.addSublayer(videoLayer)

        for segment in cleanSegments {
            let segmentLayer = CALayer()
            segmentLayer.frame = parentLayer.frame
            applyTimedOpacity(to: segmentLayer, start: segment.start, duration: segment.duration)
            parentLayer.addSublayer(segmentLayer)

            let (plotText, optionText) = splitOverlayText(segment.text)
            let fontSize = max(17, min(34, renderSize.width * 0.030 * CGFloat(segment.transform.scale)))
            let optionsFrame = optionText.isEmpty ? nil : optionsOverlayFrame(text: optionText, renderSize: renderSize)
            // Core Animation layers in the export composition are bottom-left
            // origin — the text block anchors just above the pill row.
            let textFrame = overlayFrame(
                text: plotText,
                transform: segment.transform,
                renderSize: renderSize,
                fontSize: fontSize,
                bottomAnchorY: optionsFrame.map { $0.maxY + max(8, renderSize.width * 0.018) }
            )
            if !plotText.isEmpty {
                addTypewriterTextLayers(
                    text: plotText,
                    frame: textFrame,
                    fontSize: fontSize,
                    rotationDegrees: segment.transform.rotationDegrees,
                    start: segment.start,
                    duration: segment.duration,
                    parentLayer: segmentLayer
                )
            }
            if !optionText.isEmpty {
                addOptionsGridLayer(
                    text: optionText,
                    frame: optionsFrame ?? optionsOverlayFrame(text: optionText, renderSize: renderSize),
                    renderSize: renderSize,
                    start: segment.start,
                    duration: segment.duration,
                    parentLayer: segmentLayer
                )
            }
        }

        videoComposition.animationTool = AVVideoCompositionCoreAnimationTool(
            postProcessingAsVideoLayer: videoLayer,
            in: parentLayer
        )
        return videoComposition
    }

    private func overlayFrame(
        text: String,
        transform: TransmissionTextTransform,
        renderSize: CGSize,
        fontSize: CGFloat,
        bottomAnchorY: CGFloat? = nil
    ) -> CGRect {
        // Bottom-left origin (Core Animation export space): y is the frame's
        // BOTTOM edge. Anchored just above the pills, or near the video's
        // bottom when there are no options.
        let scale = max(0.6, renderSize.width / 390.0)
        let width = min(renderSize.width * 0.94, max(renderSize.width * 0.60, renderSize.width - (24 * scale)))
        let attributes: [NSAttributedString.Key: Any] = [
            .font: UIFont.monospacedSystemFont(ofSize: fontSize, weight: .black)
        ]
        let maxHeight = min(renderSize.height * 0.28, 166 * scale)
        let measured = (text as NSString).boundingRect(
            with: CGSize(width: width - 28, height: renderSize.height * 0.82),
            options: [.usesLineFragmentOrigin, .usesFontLeading],
            attributes: attributes,
            context: nil
        )
        let height = min(maxHeight, max(fontSize * 2.4, measured.height + 32))
        let anchorY = bottomAnchorY ?? (36 * scale)
        return CGRect(
            x: (renderSize.width - width) * 0.5,
            y: min(renderSize.height - height - 10, anchorY),
            width: width,
            height: height
        )
    }

    private func splitOverlayText(_ text: String) -> (plot: String, options: String) {
        let parts = text
            .components(separatedBy: .newlines)
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
        var plotLines: [String] = []
        var optionLines: [String] = []
        for part in parts {
            if part.hasPrefix("[") || part.contains("]  [") {
                optionLines.append(part)
            } else {
                plotLines.append(part)
            }
        }
        return (plotLines.joined(separator: "\n"), optionLines.joined(separator: "  "))
    }

    private struct OptionsPillLayout {
        let options: [String]
        let widths: [CGFloat]
        let rows: [[Int]]
        let fontSize: CGFloat
        let pillHeight: CGFloat
        let gap: CGFloat
        let totalHeight: CGFloat
    }

    // Parse "[ SMASH THE GRATE ]  [ LEAVE ]" into individual options and lay
    // them out as centered pill rows that wrap within maxWidth.
    private func optionsPillLayout(text: String, renderSize: CGSize) -> OptionsPillLayout? {
        var options: [String] = []
        var remainder = text[...]
        while let open = remainder.firstIndex(of: "["), let close = remainder[open...].firstIndex(of: "]") {
            let inner = remainder[remainder.index(after: open)..<close].trimmingCharacters(in: .whitespacesAndNewlines)
            if !inner.isEmpty { options.append(inner) }
            remainder = remainder[remainder.index(after: close)...]
        }
        if options.isEmpty {
            let flat = text.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !flat.isEmpty else { return nil }
            options = [flat]
        }
        let fontSize = max(13, min(22, renderSize.width * 0.019))
        let font = UIFont.monospacedSystemFont(ofSize: fontSize, weight: .black)
        let maxWidth = renderSize.width * 0.88
        let padH = fontSize * 0.9
        let pillHeight = fontSize * 2.1
        let gap = fontSize * 0.55
        let widths = options.map { min(maxWidth, ($0 as NSString).size(withAttributes: [.font: font]).width + padH * 2) }
        var rows: [[Int]] = [[]]
        var rowWidth: CGFloat = 0
        for (index, width) in widths.enumerated() {
            let needed = rows[rows.count - 1].isEmpty ? width : rowWidth + gap + width
            if needed > maxWidth && !rows[rows.count - 1].isEmpty {
                rows.append([index])
                rowWidth = width
            } else {
                rows[rows.count - 1].append(index)
                rowWidth = needed
            }
        }
        let totalHeight = CGFloat(rows.count) * pillHeight + CGFloat(max(0, rows.count - 1)) * gap
        return OptionsPillLayout(options: options, widths: widths, rows: rows,
                                 fontSize: fontSize, pillHeight: pillHeight, gap: gap, totalHeight: totalHeight)
    }

    private func addOptionsGridLayer(
        text: String,
        frame: CGRect,
        renderSize: CGSize,
        start: CMTime,
        duration: CMTime,
        parentLayer: CALayer
    ) {
        guard let layout = optionsPillLayout(text: text, renderSize: renderSize) else { return }
        let font = UIFont.monospacedSystemFont(ofSize: layout.fontSize, weight: .black)
        // Bottom-left origin: row 0 sits at the bottom of the frame.
        for (rowIndex, row) in layout.rows.enumerated() {
            let rowWidth = row.reduce(CGFloat(0)) { $0 + layout.widths[$1] } + layout.gap * CGFloat(max(0, row.count - 1))
            var x = frame.midX - rowWidth / 2
            let y = frame.minY + CGFloat(rowIndex) * (layout.pillHeight + layout.gap)
            for index in row {
                let pill = CALayer()
                pill.frame = CGRect(x: x, y: y, width: layout.widths[index], height: layout.pillHeight)
                pill.backgroundColor = UIColor(white: 0.20, alpha: 0.85).cgColor
                pill.cornerRadius = layout.pillHeight / 2
                pill.borderColor = UIColor(white: 1.0, alpha: 0.16).cgColor
                pill.borderWidth = 1
                applyTimedOpacity(to: pill, start: start, duration: duration)
                parentLayer.addSublayer(pill)

                let label = CATextLayer()
                label.string = layout.options[index]
                label.contentsScale = UIScreen.main.scale
                label.alignmentMode = .center
                label.truncationMode = .end
                label.foregroundColor = UIColor.white.withAlphaComponent(0.92).cgColor
                label.font = font
                label.fontSize = layout.fontSize
                let textHeight = layout.fontSize * 1.35
                label.frame = CGRect(
                    x: x,
                    y: y + (layout.pillHeight - textHeight) / 2,
                    width: layout.widths[index],
                    height: textHeight
                )
                applyTimedOpacity(to: label, start: start, duration: duration)
                parentLayer.addSublayer(label)

                x += layout.widths[index] + layout.gap
            }
        }
    }

    private func optionsOverlayFrame(text: String, renderSize: CGSize) -> CGRect {
        // Bottom-left origin: the pill rows hug the very bottom of the video.
        let scale = max(0.6, renderSize.width / 390.0)
        let width = renderSize.width * 0.88
        let height = optionsPillLayout(text: text, renderSize: renderSize)?.totalHeight ?? 0
        let bottomInset = 36 * scale
        return CGRect(
            x: (renderSize.width - width) * 0.5,
            y: bottomInset,
            width: width,
            height: height
        )
    }

    private func addTypewriterTextLayers(
        text: String,
        frame: CGRect,
        fontSize: CGFloat,
        rotationDegrees: Double,
        start: CMTime,
        duration: CMTime,
        parentLayer: CALayer
    ) {
        let characters = Array(text)
        guard !characters.isEmpty else { return }
        let revealDuration = min(max(Double(characters.count) * 0.045, 0.65), max(0.65, duration.seconds * 0.55))
        let characterInterval = revealDuration / Double(characters.count)

        for characterIndex in characters.indices {
            let textLayer = CATextLayer()
            textLayer.string = String(characters[...characterIndex])
            textLayer.contentsScale = UIScreen.main.scale
            textLayer.alignmentMode = .left
            textLayer.isWrapped = true
            textLayer.truncationMode = .end
            textLayer.foregroundColor = UIColor.white.cgColor
            textLayer.shadowColor = UIColor.black.cgColor
            textLayer.shadowOpacity = 0.95
            textLayer.shadowRadius = 10
            textLayer.shadowOffset = CGSize(width: 0, height: 2)
            textLayer.font = UIFont.monospacedSystemFont(ofSize: fontSize, weight: .black)
            textLayer.fontSize = fontSize
            textLayer.frame = frame
            if abs(rotationDegrees) > 0.1 {
                textLayer.setAffineTransform(CGAffineTransform(rotationAngle: CGFloat(rotationDegrees * .pi / 180)))
            }

            let visibleStart = start.seconds + Double(characterIndex) * characterInterval
            let visibleEnd: Double
            if characterIndex == characters.index(before: characters.endIndex) {
                visibleEnd = start.seconds + max(revealDuration, duration.seconds - 0.15)
            } else {
                visibleEnd = start.seconds + Double(characterIndex + 1) * characterInterval
            }
            applyTimedOpacity(
                to: textLayer,
                startSeconds: visibleStart,
                endSeconds: visibleEnd,
                totalDuration: duration.seconds
            )
            parentLayer.addSublayer(textLayer)
        }
    }

    private func applyTimedOpacity(to layer: CALayer, start: CMTime, duration: CMTime) {
        layer.opacity = 0
        let animation = CAKeyframeAnimation(keyPath: "opacity")
        animation.values = [0, 1, 1, 0]
        animation.keyTimes = [0, 0.001, 0.999, 1]
        animation.beginTime = AVCoreAnimationBeginTimeAtZero + max(0, start.seconds)
        animation.duration = max(0.1, duration.seconds)
        animation.fillMode = .both
        animation.isRemovedOnCompletion = false
        layer.add(animation, forKey: "k1l0TimedOpacity")
    }

    private func applyTimedOpacity(to layer: CALayer, startSeconds: Double, endSeconds: Double, totalDuration: Double) {
        layer.opacity = 0
        let animation = CAKeyframeAnimation(keyPath: "opacity")
        animation.values = [0, 1, 1, 0]
        animation.keyTimes = [0, 0.001, 0.999, 1]
        animation.beginTime = AVCoreAnimationBeginTimeAtZero + max(0, startSeconds)
        animation.duration = max(0.04, endSeconds - startSeconds)
        animation.fillMode = .both
        animation.isRemovedOnCompletion = false
        layer.add(animation, forKey: "k1l0TypewriterOpacity")
    }

    private func exportComposition(
        _ composition: AVMutableComposition,
        filenamePrefix: String,
        videoComposition: AVVideoComposition?,
        timeRange: CMTimeRange?,
        completion: @escaping (Result<URL, Error>) -> Void
    ) {
        let outputURL = URL(fileURLWithPath: NSTemporaryDirectory())
            .appendingPathComponent("\(filenamePrefix)-\(UUID().uuidString).mp4")
        try? FileManager.default.removeItem(at: outputURL)
        guard let exporter = AVAssetExportSession(asset: composition, presetName: AVAssetExportPresetHighestQuality) else {
            DispatchQueue.main.async { completion(.failure(cameraRollSaveError("export unavailable"))) }
            return
        }
        exporter.outputURL = outputURL
        exporter.outputFileType = .mp4
        exporter.shouldOptimizeForNetworkUse = true
        exporter.videoComposition = videoComposition
        if let timeRange {
            exporter.timeRange = timeRange
        }
        let durationSeconds = composition.duration.seconds
        NSLog("K1L0Save export start prefix=%@ videoComposition=%@", filenamePrefix, videoComposition == nil ? "no" : "yes")
        k1l0SaveLog("export start prefix=\(filenamePrefix) videoComposition=\(videoComposition == nil ? "no" : "yes") duration=\(durationSeconds.isFinite ? durationSeconds : -1)")
        var didFinish = false
        let timeout = DispatchWorkItem {
            guard !didFinish else { return }
            didFinish = true
            let progress = exporter.progress
            exporter.cancelExport()
            let message = "export timeout prefix=\(filenamePrefix) progress=\(progress)"
            NSLog("K1L0Save %@", message)
            k1l0SaveLog(message)
            completion(.failure(cameraRollSaveError("export timed out")))
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 90, execute: timeout)
        exporter.exportAsynchronously {
            DispatchQueue.main.async {
                guard !didFinish else { return }
                didFinish = true
                timeout.cancel()
                switch exporter.status {
                case .completed:
                    NSLog("K1L0Save export completed %@", outputURL.absoluteString)
                    k1l0SaveLog("export completed output=\(outputURL.path)")
                    completion(.success(outputURL))
                case .failed, .cancelled:
                    let exportError = exporter.error.map { k1l0SaveErrorDescription($0) } ?? "unknown"
                    NSLog("K1L0Save export failed %@", exportError)
                    k1l0SaveLog("export failed \(exportError)")
                    completion(.failure(cameraRollSaveError("export failed: \(exportError)")))
                default:
                    NSLog("K1L0Save export failed status=%ld", exporter.status.rawValue)
                    completion(.failure(cameraRollSaveError("export failed")))
                }
            }
        }
    }

    private func saveLocalVideo(_ localURL: URL) {
        DispatchQueue.main.async {
            updateSavingStatus("saving…")
            NSLog("K1L0Save photos save start %@", localURL.absoluteString)
            let delegate = K1L0CameraRollSaveDelegate { nextStatus in
                saving = false
                finishSavingStatus(nextStatus)
                NSLog("K1L0Save photos save status %@", nextStatus)
                saveDelegate = nil
            }
            saveDelegate = delegate
            UISaveVideoAtPathToSavedPhotosAlbum(
                localURL.path,
                delegate,
                #selector(K1L0CameraRollSaveDelegate.video(_:didFinishSavingWithError:contextInfo:)),
                nil
            )
        }
    }
}

// Wraps the source type so sheet(item:) can carry it — eliminates the
// sheet(isPresented:) race where the source mutation and the show flag fire
// in the same SwiftUI batch and the old source value gets captured.
private struct PhotoPickerRequest: Identifiable {
    let id = UUID()
    let source: UIImagePickerController.SourceType
}

private struct NativePhotoPicker: UIViewControllerRepresentable {
    let sourceType: UIImagePickerController.SourceType
    let onComplete: (UIImage?, String?) -> Void

    func makeCoordinator() -> Coordinator {
        Coordinator(onComplete: onComplete)
    }

    func makeUIViewController(context: Context) -> UIImagePickerController {
        let picker = UIImagePickerController()
        picker.sourceType = sourceType
        picker.delegate = context.coordinator
        picker.allowsEditing = false
        return picker
    }

    func updateUIViewController(_ uiViewController: UIImagePickerController, context: Context) {}

    final class Coordinator: NSObject, UINavigationControllerDelegate, UIImagePickerControllerDelegate {
        let onComplete: (UIImage?, String?) -> Void

        init(onComplete: @escaping (UIImage?, String?) -> Void) {
            self.onComplete = onComplete
        }

        func imagePickerController(_ picker: UIImagePickerController, didFinishPickingMediaWithInfo info: [UIImagePickerController.InfoKey : Any]) {
            let image = info[.originalImage] as? UIImage
            let path = image.flatMap { Self.writeJPEG($0) }
            picker.dismiss(animated: true) {
                self.onComplete(image, path)
            }
        }

        func imagePickerControllerDidCancel(_ picker: UIImagePickerController) {
            picker.dismiss(animated: true) {
                self.onComplete(nil, nil)
            }
        }

        private static func writeJPEG(_ image: UIImage) -> String? {
            guard let data = image.jpegData(compressionQuality: 0.86) else { return nil }
            let url = URL(fileURLWithPath: NSTemporaryDirectory())
                .appendingPathComponent("k1l0-transmit-\(UUID().uuidString).jpg")
            do {
                try data.write(to: url, options: .atomic)
                return url.path
            } catch {
                return nil
            }
        }
    }
}
#endif

private struct StepStatBlock: View {
    let label: String
    let value: Int

    var body: some View {
        VStack(alignment: .leading, spacing: 1) {
            Text("\(value)")
                .font(.system(size: 24, weight: .bold))
                .monospacedDigit()
                .foregroundStyle(.white)
            Text(label)
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(.white.opacity(0.58))
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

private struct StepLeaderboardSection: View {
    let title: String
    let leaders: [OverlayStepLeader]
    let useWeeklyTotal: Bool
    let onSelectUser: (OverlayUser) -> Void

    @State private var isExpanded: Bool = false

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            Text(title)
                .font(.system(size: 11, weight: .black, design: .rounded))
                .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
            
            let displayCount = isExpanded ? 10 : 5
            ForEach(Array(leaders.prefix(displayCount).enumerated()), id: \.element.id) { index, leader in
                HStack(spacing: 9) {
                    Text("\(index + 1)")
                        .font(.system(size: 12, weight: .black, design: .monospaced))
                        .foregroundStyle(.white.opacity(0.58))
                        .frame(width: 20, alignment: .trailing)
                    K1L0UserAvatar(urlString: leader.helmetUrl, size: 28, userId: leader.userId)
                    Text(leader.name)
                        .font(.system(size: 13, weight: .semibold))
                        .lineLimit(1)
                    Spacer()
                    Text((useWeeklyTotal ? leader.steps7d : leader.steps24h).formatted())
                        .font(.system(size: 13, weight: .black, design: .monospaced))
                }
                .contentShape(Rectangle())
                .onTapGesture {
                    let user = OverlayUser(
                        userId: leader.userId,
                        name: leader.name,
                        callsign: nil,
                        avatarUrl: nil,
                        helmetUrl: leader.helmetUrl,
                        faceUrl: nil,
                        city: nil,
                        lat: nil,
                        lng: nil,
                        lastActive: nil
                    )
                    onSelectUser(user)
                }
            }

            if leaders.count > 5 {
                Button(action: {
                    withAnimation(.easeInOut(duration: 0.2)) {
                        isExpanded.toggle()
                    }
                }) {
                    HStack(spacing: 6) {
                        Text(isExpanded ? "SHOW LESS" : "SHOW MORE")
                        Image(systemName: isExpanded ? "chevron.up" : "chevron.down")
                    }
                    .font(.system(size: 11, weight: .black))
                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                    .frame(maxWidth: .infinity, minHeight: 28)
                }
                .buttonStyle(.plain)
            }
        }
    }
}

private struct LiveStepStatBlock: View {
    let value: Int
    let durationText: String

    var body: some View {
        VStack(alignment: .center, spacing: 3) {
            Text("\(value)")
                .font(.system(size: 24, weight: .bold))
                .monospacedDigit()
                .foregroundStyle(.white)
                .lineLimit(1)
                .minimumScaleFactor(0.5)
                .allowsTightening(true)
                .fixedSize(horizontal: true, vertical: false)

            Text("steps")
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(.white.opacity(0.68))
                .frame(maxWidth: .infinity, alignment: .center)
            Text(durationText)
                .font(.system(size: 10, weight: .semibold))
                .foregroundStyle(.white.opacity(0.52))
                .lineLimit(1)
                .minimumScaleFactor(0.78)
                .frame(maxWidth: .infinity, alignment: .center)
        }
        .frame(minWidth: 82, alignment: .center)
    }
}

private struct NativeNewsWalkGraph: View {
    let points: [NativeWalkHistoryPoint]
    let tint: Color
    let gridDivisions: Int   // number of interior intervals (e.g. 24 hour buckets, 7 day buckets)
    let majorEvery: Int      // every N divisions draw a brighter major rule (e.g. every 6 hours)

    private var maxSteps: Int {
        max(points.map(\.steps).max() ?? 1, 1)
    }

    var body: some View {
        ZStack {
            RoundedRectangle(cornerRadius: 8, style: .continuous)
                .fill(Color.white.opacity(0.045))
            GridVerticalRules(divisions: gridDivisions, majorEvery: majorEvery)
                .stroke(Color.white.opacity(0.22), lineWidth: 0.5)
                .padding(.horizontal, 7)
                .padding(.vertical, 4)
            GridVerticalRules(divisions: gridDivisions, majorEvery: majorEvery, majorOnly: true)
                .stroke(Color.white.opacity(0.55), lineWidth: 1.0)
                .padding(.horizontal, 7)
                .padding(.vertical, 4)
            NativeWalkLinePath(points: points, maxSteps: maxSteps, plottedWidthRatio: 1.0)
                .stroke(tint, style: StrokeStyle(lineWidth: 1.8, lineCap: .round, lineJoin: .round))
                .padding(.horizontal, 7)
                .padding(.vertical, 8)
        }
        .frame(height: 48)
        .overlay(RoundedRectangle(cornerRadius: 8, style: .continuous).stroke(Color.white.opacity(0.08), lineWidth: 1))
        .frame(maxWidth: .infinity)
    }
}

private struct GridVerticalRules: Shape {
    let divisions: Int
    let majorEvery: Int
    var majorOnly: Bool = false

    func path(in rect: CGRect) -> Path {
        var path = Path()
        guard divisions > 1 else { return path }
        for i in 1..<divisions {
            let isMajor = majorEvery > 0 && (i % majorEvery == 0)
            if majorOnly && !isMajor { continue }
            if !majorOnly && isMajor { continue } // skip; major pass draws these
            let x = rect.minX + (CGFloat(i) / CGFloat(divisions)) * rect.width
            path.move(to: CGPoint(x: x, y: rect.minY))
            path.addLine(to: CGPoint(x: x, y: rect.maxY))
        }
        return path
    }
}

private struct FixedTopStatusHUD: View {
    @ObservedObject var data: K1L0OverlayDataModel
    let settingsActive: Bool
    let hideSteps: Bool
    let onSettingsTapped: () -> Void

    var body: some View {
        ZStack(alignment: .topLeading) {
            HStack(alignment: .top) {
                WeatherPill(model: data, onSettingsTapped: onSettingsTapped)
                Spacer()
                if !hideSteps {
                    TopLiveStepsPill(model: data)
                }
            }
        }
    }
}

private struct TopLiveStepsPill: View {
    @ObservedObject var model: K1L0OverlayDataModel

    var body: some View {
        VStack(alignment: .trailing, spacing: 1) {
            Text("\(model.liveSteps)")
                .font(.system(size: 22, weight: .black, design: .rounded))
                .monospacedDigit()
            Text("steps")
                .font(.system(size: 10, weight: .black, design: .rounded))
                .textCase(.uppercase)
                .foregroundStyle(.white.opacity(0.72))
        }
        .foregroundStyle(.white)
        .padding(.horizontal, 13)
        .padding(.vertical, 10)
        .frame(minWidth: 72, alignment: .trailing)
    }
}

private struct WeatherPill: View {
    @ObservedObject var model: K1L0OverlayDataModel
    let onSettingsTapped: () -> Void

    var body: some View {
        Button(action: onSettingsTapped) {
            VStack(alignment: .leading, spacing: 2) {
                if !model.cityText.isEmpty {
                    Text(model.cityText)
                        .font(.system(size: 16, weight: .semibold))
                        .lineLimit(2)
                        .fixedSize(horizontal: false, vertical: true)
                }
                HStack(spacing: 7) {
                    Image(systemName: model.weatherGlyph)
                        .font(.system(size: 16, weight: .semibold))
                    Text(model.weatherText)
                        .font(.system(size: 16, weight: .semibold))
                }
            }
        }
        .buttonStyle(.plain)
        .foregroundStyle(.white)
        .padding(.leading, 13)
        .padding(.trailing, 13)
        .padding(.vertical, 10)
        .frame(maxWidth: 270, alignment: .leading)
    }
}

private struct DirectionCell: View {
    let distance: String
    let relativeBearing: Double

    var body: some View {
        VStack(spacing: 2) {
            Text(distance)
                .font(.system(size: 10, weight: .bold))
                .monospacedDigit()
                .foregroundStyle(.white)
            Image(systemName: "location.north.fill")
                .font(.system(size: 14, weight: .bold))
                .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                .rotationEffect(.degrees(relativeBearing))
                .frame(width: 18, height: 18)
        }
        .frame(width: 46)
    }
}

private struct K1L0UserAvatar: View {
    let urlString: String?
    let size: CGFloat
    var userId: String? = nil
    @State private var resolvedURL: String?

    var body: some View {
        let customHelmet = (resolvedURL ?? urlString ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        let resolvedHelmet = customHelmet.isEmpty ? K1L0DefaultHelmetIconURL : customHelmet
        ZStack {
            Circle()
                .fill(Color.white.opacity(0.10))
            if let url = URL(string: resolvedHelmet) {
                AsyncImage(url: url) { phase in
                    if case .success(let image) = phase {
                        image
                            .resizable()
                            .scaledToFill()
                    } else {
                        Image(systemName: "shield.fill")
                            .font(.system(size: size * 0.42, weight: .bold))
                            .foregroundStyle(.white.opacity(0.72))
                    }
                }
            } else {
                Image(systemName: "shield.fill")
                    .font(.system(size: size * 0.42, weight: .bold))
                    .foregroundStyle(.white.opacity(0.72))
            }
        }
        .frame(width: size, height: size)
        .clipShape(Circle())
        .overlay(Circle().stroke(Color.white.opacity(0.22), lineWidth: 1))
        .onAppear {
            K1L0UserHelmetResolver.resolve(userId: userId, fallbackURL: urlString) { url in
                resolvedURL = url
            }
        }
    }
}

// Shared identity lookup for every compact player portrait. Most lists already
// receive helmetUrl inline; response clips only carry a user ID, so this fills
// that gap through the same metadata endpoint and caches it for the session.
private enum K1L0UserHelmetResolver {
    private static var cache: [String: String] = [:]

    static func resolve(userId: String?, fallbackURL: String?, completion: @escaping (String?) -> Void) {
        let id = (userId ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        let fallback = (fallbackURL ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if !fallback.isEmpty && fallback != K1L0DefaultHelmetIconURL {
            if !id.isEmpty { cache[id] = fallback }
            completion(fallback)
            return
        }
        if let cached = cache[id], !cached.isEmpty {
            completion(cached)
            return
        }
        guard !id.isEmpty else {
            completion(fallback.isEmpty ? nil : fallback)
            return
        }

        K1L0NativeAPI.resolve { apiBase in
            let encoded = id.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? id
            guard let url = URL(string: "\(apiBase)/api/k1l0/user/metadata?userId=\(encoded)") else {
                DispatchQueue.main.async { completion(fallback.isEmpty ? nil : fallback) }
                return
            }
            URLSession.shared.dataTask(with: url) { data, _, _ in
                var helmet = ""
                if let data,
                   let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
                    helmet = (root["helmetUrl"] as? String ?? "")
                        .trimmingCharacters(in: .whitespacesAndNewlines)
                }
                DispatchQueue.main.async {
                    if !helmet.isEmpty { cache[id] = helmet }
                    completion(helmet.isEmpty ? (fallback.isEmpty ? nil : fallback) : helmet)
                }
            }.resume()
        }
    }
}

private struct SignalStrengthMeter: View {
    let strength: Double

    var body: some View {
        HStack(spacing: 3) {
            ForEach(0..<5, id: \.self) { index in
                Capsule()
                    .fill(index < activeBars ? Color(red: 0.66, green: 1.0, blue: 0.76) : Color.white.opacity(0.22))
                    .frame(width: 13, height: CGFloat(4 + index * 3))
            }
        }
        .frame(height: 17, alignment: .bottom)
        .accessibilityLabel("signal strength")
    }

    private var activeBars: Int {
        min(5, max(1, Int((strength * 5).rounded(.up))))
    }
}

private struct MysteryObjectCollectPrompt: View {
    let beam: OverlayBeam
    let distanceText: String
    let relativeBearing: Double
    let onCollect: () -> Void
    let onDismiss: () -> Void

    @State private var spin = false

    var body: some View {
        ZStack {
            Color.black.opacity(0.22)
                .ignoresSafeArea()

            VStack(spacing: 18) {
                HStack {
                    DirectionCell(distance: distanceText, relativeBearing: relativeBearing)
                    Spacer()
                    Button(action: onDismiss) {
                        Image(systemName: "xmark")
                            .font(.system(size: 16, weight: .black))
                            .foregroundStyle(.white)
                            .frame(width: 38, height: 38)
                            .background(Color.black.opacity(0.46), in: Circle())
                    }
                    .buttonStyle(.plain)
                }

                ZStack {
                    ForEach(0..<4, id: \.self) { index in
                        RoundedRectangle(cornerRadius: 18, style: .continuous)
                            .stroke(
                                AngularGradient(
                                    colors: [
                                        Color(red: 0.70, green: 1.0, blue: 0.50).opacity(0.95),
                                        .white.opacity(0.08),
                                        Color(red: 0.35, green: 0.85, blue: 1.0).opacity(0.85),
                                        .white.opacity(0.05),
                                        Color(red: 0.70, green: 1.0, blue: 0.50).opacity(0.95)
                                    ],
                                    center: .center
                                ),
                                lineWidth: index == 0 ? 3 : 1.6
                            )
                            .frame(width: CGFloat(122 + index * 26), height: CGFloat(122 + index * 26))
                            .rotationEffect(.degrees(spin ? Double(360 + index * 45) : Double(index * 45)))
                            .opacity(0.82 - Double(index) * 0.13)
                            .animation(
                                .linear(duration: Double(2.4 + Double(index) * 0.42)).repeatForever(autoreverses: false),
                                value: spin
                            )
                    }

                    RoundedRectangle(cornerRadius: 18, style: .continuous)
                        .fill(Color.black.opacity(0.82))
                        .frame(width: 116, height: 116)
                        .overlay(
                            RoundedRectangle(cornerRadius: 18, style: .continuous)
                                .stroke(Color.white.opacity(0.22), lineWidth: 1)
                        )

                    if let imageUrlString = beam.imageUrl,
                       !imageUrlString.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
                       let imageUrl = URL(string: imageUrlString) {
                        AsyncImage(url: imageUrl) { phase in
                            switch phase {
                            case .success(let image):
                                image
                                    .resizable()
                                    .aspectRatio(contentMode: .fill)
                                    .frame(width: 116, height: 116)
                                    .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
                            case .failure(_), .empty:
                                Image(systemName: beam.collectIconName)
                                    .font(.system(size: 46, weight: .black))
                                    .foregroundStyle(Color(red: 0.70, green: 1.0, blue: 0.50))
                            @unknown default:
                                EmptyView()
                            }
                        }
                    } else {
                        Image(systemName: beam.collectIconName)
                            .font(.system(size: 46, weight: .black))
                            .foregroundStyle(Color(red: 0.70, green: 1.0, blue: 0.50))
                    }
                }
                .frame(height: 204)

                VStack(spacing: 8) {
                    Text("Object Ready")
                        .font(.system(size: 13, weight: .black))
                        .foregroundStyle(.white.opacity(0.68))
                        .textCase(.uppercase)
                    Text(beam.title)
                        .font(.system(size: 30, weight: .heavy))
                        .foregroundStyle(.white)
                        .lineLimit(2)
                        .multilineTextAlignment(.center)
                        .minimumScaleFactor(0.62)
                }

                Button(action: onCollect) {
                    Text("collect")
                        .font(.system(size: 18, weight: .black, design: .monospaced))
                        .foregroundStyle(.white)
                        .lineLimit(1)
                        .minimumScaleFactor(0.54)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 14)
                        .background(Color(red: 0.15, green: 0.45, blue: 0.22))
                        .clipShape(Capsule())
                }
                .buttonStyle(.plain)
            }
            .padding(22)
            .frame(maxWidth: 390, minHeight: 430)
            .background(Color.black.opacity(0.72), in: RoundedRectangle(cornerRadius: 24, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 24, style: .continuous)
                    .stroke(Color.white.opacity(0.18), lineWidth: 1)
            )
            .padding(.horizontal, 18)
        }
        .ignoresSafeArea(.keyboard, edges: .bottom)
        .onAppear { spin = true }
    }
}

private struct LocationItemCollectPrompt: View {
    let place: OverlayPlace
    let distanceText: String
    let relativeBearing: Double
    let onCollect: () -> Void
    let onDismiss: () -> Void

    @State private var pulse = false

    var body: some View {
        ZStack {
            Color.black.opacity(0.22)
                .ignoresSafeArea()

            VStack(spacing: 18) {
                HStack {
                    DirectionCell(distance: distanceText, relativeBearing: relativeBearing)
                    Spacer()
                    Button(action: onDismiss) {
                        Image(systemName: "xmark")
                            .font(.system(size: 16, weight: .black))
                            .foregroundStyle(.white)
                            .frame(width: 38, height: 38)
                            .background(Color.black.opacity(0.46), in: Circle())
                    }
                    .buttonStyle(.plain)
                }

                ZStack {
                    Circle()
                        .stroke(Color(red: 0.70, green: 1.0, blue: 0.50).opacity(pulse ? 0.18 : 0.75), lineWidth: pulse ? 16 : 3)
                        .frame(width: pulse ? 190 : 128, height: pulse ? 190 : 128)
                        .animation(.easeOut(duration: 1.25).repeatForever(autoreverses: false), value: pulse)

                    RoundedRectangle(cornerRadius: 18, style: .continuous)
                        .fill(Color.black.opacity(0.82))
                        .frame(width: 116, height: 116)
                        .overlay(
                            RoundedRectangle(cornerRadius: 18, style: .continuous)
                                .stroke(Color.white.opacity(0.22), lineWidth: 1)
                        )

                    Image(systemName: place.collectIconName)
                        .font(.system(size: 46, weight: .black))
                        .foregroundStyle(Color(red: 0.70, green: 1.0, blue: 0.50))
                }
                .frame(height: 204)

                VStack(spacing: 8) {
                    Text("Location Item Ready")
                        .font(.system(size: 13, weight: .black))
                        .foregroundStyle(.white.opacity(0.68))
                        .textCase(.uppercase)
                    Text(place.collectTitle)
                        .font(.system(size: 30, weight: .heavy))
                        .foregroundStyle(.white)
                        .lineLimit(2)
                        .multilineTextAlignment(.center)
                        .minimumScaleFactor(0.62)
                    Text(place.name)
                        .font(.system(size: 15, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.74))
                        .lineLimit(1)
                        .minimumScaleFactor(0.7)
                }

                Button(action: onCollect) {
                    Text("collect")
                        .font(.system(size: 18, weight: .black, design: .monospaced))
                        .foregroundStyle(.white)
                        .lineLimit(1)
                        .minimumScaleFactor(0.54)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 14)
                        .background(Color(red: 0.15, green: 0.45, blue: 0.22))
                        .clipShape(Capsule())
                }
                .buttonStyle(.plain)
            }
            .padding(22)
            .frame(maxWidth: 390, minHeight: 430)
            .background(Color.black.opacity(0.72), in: RoundedRectangle(cornerRadius: 24, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 24, style: .continuous)
                    .stroke(Color.white.opacity(0.18), lineWidth: 1)
            )
            .padding(.horizontal, 18)
        }
        .ignoresSafeArea(.keyboard, edges: .bottom)
        .onAppear { pulse = true }
    }
}

// Bottom card for a tapped nearby user — same chrome as MysteryObjectCollectPrompt.
// No last-seen; inline message compose stored in Firebase via /api/k1l0/message.
private struct NearbyProfileDTO: Decodable {
    let avatarUrl: String?
    let helmetUrl: String?
    let faceUrl: String?
    let bio: String?
    let url: String?
}

private struct RoundedCorners: Shape {
    let corners: UIRectCorner
    let radius: CGFloat

    func path(in rect: CGRect) -> Path {
        let path = UIBezierPath(roundedRect: rect, byRoundingCorners: corners, cornerRadii: CGSize(width: radius, height: radius))
        return Path(path.cgPath)
    }
}

private struct NearbyUserInfoCard: View {
    let user: OverlayUser
    let locationText: String
    let onDismiss: () -> Void

    @State private var composing = false
    @State private var messageText = ""
    @State private var isSending = false
    @State private var sent = false
    @FocusState private var fieldFocused: Bool
    // Fetched on open from /api/k1l0/user/metadata so the hero can use the full
    // avatar render and we can surface bio + instagram handle.
    @State private var heroUrl: String?
    @State private var bioText: String?
    @State private var instagramUrl: String?
    @State private var profileLoaded = false

    private var placeText: String {
        let city = (user.city ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if city.isEmpty { return locationText }
        return "\(locationText) · \(city)"
    }

    var body: some View {
        VStack {
            Spacer()
            VStack(alignment: .leading, spacing: 0) {
                // Hero: full-bleed avatar render with identity overlaid at the bottom.
                ZStack(alignment: .bottomLeading) {
                    heroImage
                        .frame(height: 232)
                        .frame(maxWidth: .infinity)
                        .clipped()
                    LinearGradient(colors: [.clear, .black.opacity(0.30), .black.opacity(0.92)], startPoint: .top, endPoint: .bottom)
                    VStack(alignment: .leading, spacing: 5) {
                        Text("Nearby")
                            .font(.system(size: 12, weight: .black))
                            .foregroundStyle(.white.opacity(0.72))
                            .textCase(.uppercase)
                        Text(user.displayName)
                            .font(.system(size: 28, weight: .heavy))
                            .foregroundStyle(.white)
                            .lineLimit(1)
                            .minimumScaleFactor(0.7)
                        if user.nameAndCallsign != user.displayName {
                            Text(user.nameAndCallsign)
                                .font(.system(size: 14, weight: .semibold))
                                .foregroundStyle(.white.opacity(0.82))
                                .lineLimit(1)
                        }
                        if let handle = instagramDisplay, !handle.isEmpty {
                            Label(handle, systemImage: "camera.fill")
                                .font(.system(size: 13, weight: .semibold))
                                .foregroundStyle(.white.opacity(0.9))
                                .lineLimit(1)
                        }
                    }
                    .padding(18)
                    // Close button, top-right over the hero.
                    VStack {
                        HStack {
                            Spacer()
                            Button(action: onDismiss) {
                                Image(systemName: "xmark")
                                    .font(.system(size: 16, weight: .black))
                                    .foregroundStyle(.white)
                                    .frame(width: 34, height: 34)
                                    .background(Color.black.opacity(0.42), in: Circle())
                            }
                            .buttonStyle(.plain)
                        }
                        Spacer()
                    }
                    .padding(14)
                }
                .frame(height: 232)

                VStack(alignment: .leading, spacing: 12) {
                    if let bio = bioText?.trimmingCharacters(in: .whitespacesAndNewlines), !bio.isEmpty {
                        Text(bio)
                            .font(.system(size: 14, weight: .medium))
                            .foregroundStyle(.white.opacity(0.85))
                            .lineLimit(4)
                            .fixedSize(horizontal: false, vertical: true)
                    }

                    Label(placeText, systemImage: "location.fill")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.72))
                        .lineLimit(1)
                        .minimumScaleFactor(0.75)

                    if sent {
                        Text("[ message sent ]")
                            .font(.system(size: 18, weight: .black, design: .monospaced))
                            .foregroundStyle(.white.opacity(0.55))
                            .frame(maxWidth: .infinity)
                            .padding(.vertical, 14)
                    } else if composing {
                        HStack(spacing: 10) {
                            TextField("say something…", text: $messageText)
                                .font(.system(size: 15, weight: .medium))
                                .foregroundStyle(.white)
                                .tint(.white)
                                .focused($fieldFocused)
                                .submitLabel(.send)
                                .onSubmit { sendMessage() }
                            Button(action: sendMessage) {
                                if isSending {
                                    ProgressView().tint(.black).scaleEffect(0.8)
                                        .frame(width: 44, height: 20)
                                } else {
                                    Text("send")
                                        .font(.system(size: 15, weight: .black))
                                        .foregroundStyle(.black)
                                        .padding(.horizontal, 14)
                                        .padding(.vertical, 10)
                                }
                            }
                            .disabled(messageText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || isSending)
                            .buttonStyle(.plain)
                            .background(Color(red: 0.70, green: 1.0, blue: 0.50))
                        }
                        .padding(.horizontal, 12)
                        .padding(.vertical, 8)
                        .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 10))
                    } else {
                        Button(action: { composing = true; fieldFocused = true }) {
                            Text("[ message ]")
                                .font(.system(size: 18, weight: .black, design: .monospaced))
                                .foregroundStyle(.black)
                                .lineLimit(1)
                                .minimumScaleFactor(0.54)
                                .frame(maxWidth: .infinity)
                                .padding(.vertical, 14)
                                .background(Color(red: 0.70, green: 1.0, blue: 0.50))
                        }
                        .buttonStyle(.plain)
                    }
                }
                .padding(.horizontal, 18)
                .padding(.top, 18)
                .padding(.bottom, 38)
            }
            .background(Color.black)
            .clipShape(RoundedCorners(corners: [.topLeft, .topRight], radius: 26))
            .overlay(
                RoundedCorners(corners: [.topLeft, .topRight], radius: 26)
                    .stroke(Color.white.opacity(0.18), lineWidth: 1)
            )
            .padding(.horizontal, 0)
            .padding(.bottom, 0)
        }
        .ignoresSafeArea(edges: .bottom)
        .ignoresSafeArea(.keyboard, edges: .bottom)
        .onAppear { loadProfile() }
    }

    @ViewBuilder
    private var heroImage: some View {
        let resolved = (heroUrl ?? user.avatarDisplayUrl) ?? K1L0DefaultHelmetIconURL
        if let u = URL(string: resolved), !resolved.isEmpty {
            AsyncImage(url: u) { phase in
                switch phase {
                case .success(let image):
                    // Anchor the fill to the TOP so tall 9:16 cloak renders show
                    // the head/helmet instead of cropping to the midsection.
                    GeometryReader { proxy in
                        image
                            .resizable()
                            .scaledToFill()
                            .frame(width: proxy.size.width, height: proxy.size.height, alignment: .top)
                    }
                case .failure:
                    WarblyStaticView()
                default:
                    WarblyStaticView()
                }
            }
        } else {
            WarblyStaticView()
        }
    }

    private var instagramDisplay: String? {
        let raw = (instagramUrl ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        guard !raw.isEmpty else { return nil }
        if let last = raw.split(separator: "/").last, !last.isEmpty {
            let piece = last.hasPrefix("@") ? String(last.dropFirst()) : String(last)
            return "@\(piece)"
        }
        return raw.hasPrefix("@") ? raw : "@\(raw)"
    }

    private func loadProfile() {
        guard !profileLoaded else { return }
        profileLoaded = true
        K1L0NativeAPI.resolve { apiBase in
            let encoded = user.userId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? user.userId
            guard let url = URL(string: "\(apiBase)/api/k1l0/user/metadata?userId=\(encoded)") else { return }
            URLSession.shared.dataTask(with: url) { data, _, _ in
                guard let data = data,
                      let dto = try? JSONDecoder().decode(NearbyProfileDTO.self, from: data) else { return }
                DispatchQueue.main.async {
                    let helmet = (dto.helmetUrl ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
                    heroUrl = helmet.isEmpty ? K1L0DefaultHelmetIconURL : helmet
                    bioText = dto.bio
                    instagramUrl = dto.url
                }
            }.resume()
        }
    }

    private func sendMessage() {
        let text = messageText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty, !isSending else { return }
        guard let fromId = K1L0NativeAPI.currentUserId() else { return }
        isSending = true
        K1L0NativeAPI.resolve { apiBase in
            guard let url = URL(string: "\(apiBase)/api/k1l0/message") else {
                DispatchQueue.main.async { isSending = false }
                return
            }
            var req = URLRequest(url: url)
            req.httpMethod = "POST"
            req.setValue("application/json", forHTTPHeaderField: "Content-Type")
            let body: [String: Any] = ["from": fromId, "to": user.userId, "text": text]
            req.httpBody = try? JSONSerialization.data(withJSONObject: body)
            URLSession.shared.dataTask(with: req) { _, response, _ in
                DispatchQueue.main.async {
                    isSending = false
                    if (response as? HTTPURLResponse)?.statusCode == 200 {
                        sent = true
                    }
                }
            }.resume()
        }
    }
}

private struct InventoryItemDetailCard: View {
    let item: OverlayInventoryItem
    let onDismiss: () -> Void

    private static let dateFormatter: DateFormatter = {
        let f = DateFormatter()
        f.dateStyle = .medium
        f.timeStyle = .short
        return f
    }()

    var body: some View {
        VStack {
            Spacer()
            VStack(alignment: .leading, spacing: 16) {
                HStack(alignment: .top, spacing: 14) {
                    ZStack {
                        RoundedRectangle(cornerRadius: 14, style: .continuous)
                            .fill(item.isElement
                                ? Color(red: 0.05, green: 0.25, blue: 0.12).opacity(0.82)
                                : Color.white.opacity(0.10))
                            .overlay(
                                RoundedRectangle(cornerRadius: 14, style: .continuous)
                                    .stroke(item.isElement
                                        ? Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.42)
                                        : Color.white.opacity(0.18), lineWidth: 1)
                            )
                        if !item.isElement, let url = URL(string: item.avatarUrl), !item.avatarUrl.isEmpty {
                            AsyncImage(url: url) { phase in
                                switch phase {
                                case .success(let image):
                                    image.resizable().scaledToFill()
                                default:
                                    Text(item.symbol)
                                        .font(.system(size: 22, weight: .black))
                                        .foregroundStyle(.white.opacity(0.78))
                                }
                            }
                            .frame(width: 56, height: 56)
                            .clipShape(RoundedRectangle(cornerRadius: 13, style: .continuous))
                        } else {
                            Text(item.symbol)
                                .font(.system(size: item.isElement ? 26 : 22, weight: .black))
                                .foregroundStyle(item.isElement ? Color(red: 0.66, green: 1.0, blue: 0.76) : .white.opacity(0.84))
                        }
                    }
                    .frame(width: 60, height: 60)

                    VStack(alignment: .leading, spacing: 4) {
                        Text(item.isElement ? "Element" : "Object")
                            .font(.system(size: 12, weight: .black))
                            .foregroundStyle(.white.opacity(0.55))
                            .textCase(.uppercase)
                        Text(item.name)
                            .font(.system(size: 22, weight: .heavy))
                            .foregroundStyle(.white)
                            .lineLimit(2)
                            .minimumScaleFactor(0.72)
                        Text(item.amountText)
                            .font(.system(size: 13, weight: .bold).monospacedDigit())
                            .foregroundStyle(.white.opacity(0.60))
                    }

                    Spacer()

                    Button(action: onDismiss) {
                        Image(systemName: "xmark")
                            .font(.system(size: 15, weight: .black))
                            .foregroundStyle(.white)
                            .frame(width: 32, height: 32)
                            .background(Color.black.opacity(0.42), in: Circle())
                    }
                    .buttonStyle(.plain)
                }

                Divider().background(Color.white.opacity(0.14))

                VStack(alignment: .leading, spacing: 10) {
                    if !item.senderName.isEmpty {
                        Label("From \(item.senderName)", systemImage: "arrow.down.circle.fill")
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundStyle(.white.opacity(0.72))
                    }
                    if let date = item.collectedAt {
                        Label(Self.dateFormatter.string(from: date), systemImage: "calendar")
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundStyle(.white.opacity(0.72))
                    }
                }

                Text(item.detailDescription)
                    .font(.system(size: 14, weight: .medium))
                    .foregroundStyle(.white.opacity(0.68))
                    .lineSpacing(3)
                    .fixedSize(horizontal: false, vertical: true)
            }
            .padding(20)
            .background(Color.black.opacity(0.82), in: RoundedRectangle(cornerRadius: 24, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 24, style: .continuous)
                    .stroke(Color.white.opacity(0.18), lineWidth: 1)
            )
            .padding(.horizontal, 18)
            .padding(.bottom, 96)
        }
        .ignoresSafeArea(.keyboard, edges: .bottom)
    }
}

private struct DropFilterBar: View {
    @Binding var selected: String

    private let filters = [
        ("all", "all"),
        ("drink", "🍺"),
        ("coffee", "☕️"),
        ("food", "🍽️"),
        ("snack", "🍬")
    ]

    var body: some View {
        HStack(spacing: 12) {
            ForEach(filters, id: \.0) { filter in
                Button {
                    selected = filter.0
                } label: {
                    Text(filter.1)
                        .font(.system(size: filter.0 == "all" ? 20 : 24, weight: .bold))
                        .foregroundStyle(.white)
                        .frame(width: 54, height: 54)
                        .background(selected == filter.0 ? Color.green.opacity(0.48) : Color.white.opacity(0.12), in: Circle())
                }
                .buttonStyle(.plain)
            }
        }
    }
}

private final class K1L0OverlayDataModel: NSObject, ObservableObject, CLLocationManagerDelegate {
    fileprivate static weak var activeModel: K1L0OverlayDataModel?

    @Published var liveSteps = 0 {
        didSet {
            guard liveSteps != oldValue else { return }
            advanceFixedTestLocation(from: oldValue, to: liveSteps)
            normalizeIncomingBaselinesForLiveSteps()
            handleLiveStepsChanged()
        }
    }
    @Published var steps24h = 0
    @Published var steps7d = 0
    @Published var cityText = ""
    @Published var weatherText = "K1L0"
    @Published var weatherGlyph = "cloud.sun.fill"
    @Published var places: [OverlayPlace] = []
    @Published var beams: [OverlayBeam] = []
    @Published var elements: [OverlayElement] = []
    @Published var inventoryItems: [OverlayInventoryItem] = []
    @Published var nearbyUsers: [OverlayUser] = []
    @Published var stepLeaders24h: [OverlayStepLeader] = []
    @Published var stepLeaders7d: [OverlayStepLeader] = []
    @Published var stepLeaderboardStatus = "loading walkers…"
    @Published var incomingTransmission: OverlayIncomingTransmission?
    @Published var collectCandidateBeam: OverlayBeam?
    @Published var collectCandidatePlace: OverlayPlace?
    @Published var receiveProgressSteps = 0
    @Published var receiveSignalStatus = "scanning signals"
    @Published var locationStatus = "loading nearby places…"
    @Published var beamStatus = "scanning ambient…"
    @Published var elementsStatus = "loading elements…"
    @Published var nearbyUsersStatus = "loading users…"
    @Published var apiStatus = "api resolving…"
    @Published var locationPermissionReady = K1L0OverlayDataModel.initialLocationPermissionReady()
    @Published var motionPermissionReady = K1L0OverlayDataModel.initialMotionPermissionReady()
    @Published var locationPermissionDenied = false
    @Published var motionPermissionDenied = false
    @Published var locationPermissionText = "K1L0 anchors beams, signals, and nearby walkers to where you really are. without GPS the world can't load around you."
    @Published var motionPermissionText = "your real steps charge signals and collect beams. step counts stay on this phone."
    @Published var liveStepSessionStart: Date?
    @Published var liveStepIdleStart: Date?
    @Published private var environmentKnown = false
    @Published private var likelyIndoors = false
    @Published private var now = Date()
    @Published private var headingDegrees = 0.0
    @Published private(set) var activeAPIBase: String?

    private let locationManager = CLLocationManager()
#if os(macOS)
    private static let macDebugLocation = CLLocation(latitude: NativeLocationPreset.fallback.latitude, longitude: NativeLocationPreset.fallback.longitude)
#endif

    // Synchronous first-frame permission truth so the gate doesn't flash for players
    // whose permissions are already granted (or reappear wrongly hidden when revoked).
    private static func initialLocationPermissionReady() -> Bool {
#if os(iOS)
        switch CLLocationManager().authorizationStatus {
        case .authorizedAlways, .authorizedWhenInUse: return true
        default: return false
        }
#else
        return true
#endif
    }

    private static func initialMotionPermissionReady() -> Bool {
#if os(iOS)
        guard CMPedometer.isStepCountingAvailable() else { return true }
        return CMPedometer.authorizationStatus() == .authorized
#else
        return true
#endif
    }
#if os(iOS)
    private let pedometer = CMPedometer()
    private var pedometerSessionStart: Date?
    private var pedometerSessionRefreshInFlight = false
    private var pedometerSessionTimer: Timer?
    private var pedometerStatsTimer: Timer?
#endif
		    private var currentLocation: CLLocation?
    private var simulatedLocationStepBaseline = 0
    private var lastSimulatedLocationPushAt = Date.distantPast
	    private var lastPlaceTileKeys = Set<String>()
	    private var lastPlacePrimaryTileKey: String?
	    private var lastPlaceHalfHourBucket: Int?
	    private var lastPlaceFetchLocation: CLLocation?
	    private var didFetchNearby = false
    private var nearbyRefreshTimer: Timer?
    private var clockTimer: Timer?
    private var videoPlaybackActive = false
    private var isResolvingAPI = false
    private var lastWeatherFetchAt = Date.distantPast
    private var lastWeatherFetchLocation: CLLocation?
    private var lastIncomingScanAt = Date.distantPast
    private var didRequestLocationAuthorization = false
    private var lastBeamDistances: [String: Double] = [:]
    private var lastWorldItemDistances: [String: Double] = [:]
    private var worldItemDistanceTrend: [String: String] = [:]
    private var walkingTowardUntil: [String: Date] = [:]
    private var walkingAwayStartSteps: [String: Int] = [:]
    private var dismissedBeamIds = Set<String>()
    private var collectingBeamIds = Set<String>()
    private var collectingPlaceIds = Set<String>()
    private var collectedPlaceIds = Set<String>()
    private var receiveUnlockedIds = Set<String>()
    private var isFetchingIncomingTransmission = false
    private static let incomingWaitBaselineKey = "k1lo_native_incomingWaitBaselineSteps_v1"
    private static let incomingTuneBaselineKey = "k1lo_native_incomingTuneBaselineSteps_v1"
    private static let incomingTuneSignalIdKey = "k1lo_native_incomingTuneSignalId_v1"
    private static let incomingSeedRequestAtKey = "k1lo_native_incomingSeedRequestAt_v1"
    fileprivate static let locationDropFilterKey = "k1lo_native_locationDropFilter_v1"
    private var incomingWaitBaselineSteps = UserDefaults.standard.integer(forKey: K1L0OverlayDataModel.incomingWaitBaselineKey)
    private var incomingWaitBaselineInitialized = UserDefaults.standard.object(forKey: K1L0OverlayDataModel.incomingWaitBaselineKey) != nil
    private var incomingTuneBaselineSteps = UserDefaults.standard.integer(forKey: K1L0OverlayDataModel.incomingTuneBaselineKey)
    private var incomingTuneSignalId = UserDefaults.standard.string(forKey: K1L0OverlayDataModel.incomingTuneSignalIdKey) ?? ""
    private var lastIncomingSeedRequestAt = UserDefaults.standard.double(forKey: K1L0OverlayDataModel.incomingSeedRequestAtKey)
    private let apiCandidates = [
        "https://api-tunnel.kilo.gallery",
        "http://192.168.40.34:3000",
        "http://fred.local:3000",
        "http://172.20.10.5:3000",
        "https://api.kilomeme.com"
    ]

    private struct WeatherSnapshot {
        let city: String?
        let tempF: Double?
        let glyph: String
        let isDay: Bool?

        var displayText: String {
            guard let tempF else { return "--°" }
            return "\(Int(tempF.rounded()))°"
        }
    }

    override init() {
        K1L0MediaCache.configure()
        super.init()
        Self.activeModel = self
    }

    var heroText: String {
        "\(liveSteps)"
    }

    var liveStepDurationText: String {
        let start = liveSteps > 0 ? liveStepSessionStart : liveStepIdleStart
        guard let start else { return "last 0 min" }
        let seconds = max(60, Int(now.timeIntervalSince(start).rounded()))
        let minutes = max(1, seconds / 60)
        if minutes < 90 {
            return "last \(minutes) \(minutes == 1 ? "min" : "mins")"
        }
        let hours = max(1, Int((Double(minutes) / 60.0).rounded()))
        return "last \(hours) \(hours == 1 ? "hour" : "hours")"
    }

    var ctaText: String {
        if let beam = activePursuedBeam {
            return "AMBIENT · \(distanceText(to: beam).uppercased())"
        }
        guard liveSteps > 0 else { return "WALK" }
        let remaining = signalAcquisitionRemainingSteps()
        return remaining > 0 ? "KEEP WALKING · SIGNAL IN \(remaining) STEPS" : "KEEP WALKING · SEARCHING"
    }

    var walkingSkyAlertBeam: OverlayBeam? { activePursuedBeam }

    var walkingSkyAlertText: String {
        guard liveSteps > 0 else {
            let action = environmentKnown && likelyIndoors ? "go outside and explore the kiloverse" : "walk"
            let duration = liveStepDurationText
                .replacingOccurrences(of: "last ", with: "")
                .trimmingCharacters(in: .whitespacesAndNewlines)
            return "you have been inactive for \(duration)\n\(action)"
        }
        if let beam = activePursuedBeam {
            return "keep walking\n\(beam.teaserText.lowercased())"
        }
        let remaining = signalAcquisitionRemainingSteps()
        if remaining > 0 {
            return "keep walking\nsearching for signals from other users\(animatedDots)"
        }
        let status = receiveSignalStatus
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .lowercased()
        if !status.isEmpty && !status.hasPrefix("walk ") {
            return "keep walking\n\(animatedSignalStatus(status))"
        }
        return "keep walking\nsearching for signals from other users\(animatedDots)"
    }

    // The string always carries exactly three dots (constant width, no
    // wobble); WalkingSkyAlert animates them by opacity using searchDotPhase.
    private var animatedDots: String { "..." }

    var searchDotPhase: Int {
        Int(now.timeIntervalSince1970 / 0.35) % 3
    }

    private func animatedSignalStatus(_ status: String) -> String {
        let trimmed = status
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .trimmingCharacters(in: CharacterSet(charactersIn: ".…"))
        if trimmed.hasPrefix("signal unavailable")
            || trimmed.hasPrefix("signal request")
            || trimmed.hasPrefix("signal empty")
            || trimmed.hasPrefix("signal decode")
            || trimmed.hasPrefix("skipped ") {
            return "searching for signals from other users\(animatedDots)"
        }
        if trimmed == "scanning signals" || trimmed == "searching for signals" || trimmed == "searching" {
            return "searching for signals from other users\(animatedDots)"
        }
        return status
    }

    // Returns a stable max-width version of walkingSkyAlertText to prevent capsule width jitter
    // during dot animation. Figure space (\u{2007}, digit-width) is wider than a period, so
    // the 1-dot+2-figure-space phase is always the widest — we lock the layout to that.
    var walkingSkyAlertStableText: String? {
        let text = walkingSkyAlertText
        guard text.contains("\u{2007}") else { return nil }
        let base = String(text.dropLast(3))
        return base + ".\u{2007}\u{2007}"
    }

    fileprivate func handleEnvironmentState(_ json: String) {
        guard let data = json.data(using: .utf8),
              let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return }
        environmentKnown = (root["known"] as? Bool) ?? false
        likelyIndoors = (root["indoors"] as? Bool) ?? false
    }

    var ctaIcon: String {
        activePursuedBeam != nil ? "antenna.radiowaves.left.and.right" : "exclamationmark.triangle.fill"
    }

    var ctaColor: Color {
        activePursuedBeam != nil ? Color(red: 0.66, green: 1.0, blue: 0.76) : .yellow
    }

    var nearestBeam: OverlayBeam? {
        beams
            .filter { !isExpired($0) }
            .sorted { distanceMeters(to: $0) < distanceMeters(to: $1) }
            .first
    }

    var nearestForwardBeam: OverlayBeam? {
        beams
            .filter { !isExpired($0) && isBeamReasonablyAhead($0) }
            .sorted { distanceMeters(to: $0) < distanceMeters(to: $1) }
            .first
    }

    private var homeHighlightBeam: OverlayBeam? {
        guard liveSteps > 0 else { return nil }
        if let beam = activePursuedBeam {
            return beam
        }
        return nearestForwardBeam
    }

    var activePursuedBeam: OverlayBeam? {
        guard incomingTransmission == nil else { return nil }
        if let collectCandidateBeam, !isExpired(collectCandidateBeam) {
            return collectCandidateBeam
        }
        return beams
            .filter { !isExpired($0) && isWalkingToward($0) && isBeamReasonablyAhead($0) }
            .sorted { distanceMeters(to: $0) < distanceMeters(to: $1) }
            .first
    }

    func filteredPlaces(for filter: String) -> [OverlayPlace] {
        let normalized = filter.lowercased()
        let visible = normalized == "all"
            ? places
            : places.filter { placeCategory($0) == normalized }
        return visible.sorted { distanceMeters(to: $0) < distanceMeters(to: $1) }
    }

    func applyLocationFilter(_ filter: String? = nil) {
        let normalized = normalizedLocationFilter(filter ?? UserDefaults.standard.string(forKey: Self.locationDropFilterKey) ?? "all")
        let filtered = filteredPlaces(for: normalized)
        var payloadPlaces: [[String: Any]] = []

        for place in filtered {
            var entry: [String: Any] = [
                "name": place.name,
                "type": place.type,
                "types": place.types ?? [],
                "businessStatus": "OPERATIONAL",
                "coordinates": [
                    "lat": place.coordinates.lat,
                    "lng": place.coordinates.lng
                ],
                "lore": place.bylineTeaser ?? "",
                "artifactMaterial": place.artifactMaterial ?? "",
                "artifactLabel": place.artifactLabel ?? "",
                "artifactLore": place.artifactTeaser ?? "",
                "artifactContainer": "",
                "artifactSenderName": ""
            ]
            if let placeId = place.placeId {
                entry["placeId"] = placeId
            }
            payloadPlaces.append(entry)
        }

        let payload: [String: Any] = [
            "ok": true,
            "includePlaces": true,
            "includeBeams": false,
            "places": payloadPlaces,
            "beams": []
        ]

        guard let data = try? JSONSerialization.data(withJSONObject: payload),
              let json = String(data: data, encoding: .utf8) else { return }
        // Unity objects can finish initializing after the first nearby response.
        // Replay this idempotent place snapshot a few times so the scanner cannot
        // miss it; this is the same resend a category-filter toggle used to cause.
        [0.0, 0.45, 1.25].forEach { delay in
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                K1L0WeatherOverlayInstaller.applyNativeWorldNearby(json)
            }
        }
    }

    func homeMarqueeItems() -> [K1L0MarqueeItem] {
        var rows: [K1L0MarqueeItem] = []
        rows.append(K1L0MarqueeItem(
            id: "walking-status",
            kind: "status",
            line1: liveSteps > 0 ? "Keep walking" : "Walk",
            line2: liveSteps > 0 ? "you took \(liveSteps) steps \(liveStepDurationText)" : "Idle for \(inactiveDurationPlainText)",
            distanceText: nil,
            relativeBearing: nil,
            progress: nil
        ))

        var incomingLocked = false
        var ambientLocked = false

        if let incoming = incomingTransmission {
            if liveSteps > 0 {
                incomingLocked = true
                rows.append(K1L0MarqueeItem(
                    id: "incoming:\(incoming.id)",
                    kind: "incomingTransmission",
                    line1: "searching for signals from other users\(animatedDots)",
                    line2: "walk",
                    distanceText: nil,
                    relativeBearing: nil,
                    progress: nil
                ))
            }
        }

        if !incomingLocked, let beam = homeHighlightBeam {
            ambientLocked = true
            let itemId = "beam:\(beam.id)"
            rows.append(K1L0MarqueeItem(
                id: itemId,
                kind: beam.rewardType?.lowercased() == "object" ? "ambientObject" : "ambientElement",
                line1: "Nearby item",
                line2: beam.teaserText,
                distanceText: distanceText(to: beam),
                relativeBearing: relativeBearingDegrees(to: beam),
                progress: nil
            ))
        }

        if !incomingLocked, !ambientLocked, let place = bestLocationMarqueeCandidate() {
            let itemId = "place:\(place.placeId ?? place.id)"
            rows.append(K1L0MarqueeItem(
                id: itemId,
                kind: "location",
                line1: place.name,
                line2: "Stop in to collect something.",
                distanceText: distanceText(to: place),
                relativeBearing: relativeBearingDegrees(to: place),
                progress: nil
            ))
        }

        return rows
    }

    private var inactiveDurationPlainText: String {
        liveStepDurationText
            .replacingOccurrences(of: "last ", with: "")
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func bestLocationMarqueeCandidate() -> OverlayPlace? {
        let nearestBeamDistance = nearestForwardBeam.map { distanceMeters(to: $0) } ?? .greatestFiniteMagnitude
        return places
            .filter { place in
                let meters = distanceMeters(to: place)
                let steps = estimatedSteps(forMeters: meters)
                let itemId = "place:\(place.placeId ?? place.id)"
                return meters < nearestBeamDistance
                    && steps <= 500
                    && isWalkingTowardItem(itemId, relativeBearing: relativeBearingDegrees(to: place))
            }
            .sorted { distanceMeters(to: $0) < distanceMeters(to: $1) }
            .first
    }

    private func estimatedSteps(forMeters meters: Double) -> Int {
        max(1, Int((max(0, meters) * 1.3).rounded()))
    }

    private func stepsText(toMeters meters: Double) -> String {
        "\(estimatedSteps(forMeters: meters)) steps"
    }

    private func isWalkingTowardItem(_ itemId: String, relativeBearing: Double) -> Bool {
        guard liveSteps > 0 else { return false }
        let normalized = abs(Self.normalizedSignedDegrees(relativeBearing))
        if normalized <= 45 { return true }
        return worldItemDistanceTrend[itemId] == "toward" && normalized <= 70
    }

    private func isBeamReasonablyAhead(_ beam: OverlayBeam) -> Bool {
        guard currentLocation != nil else { return false }
        return abs(Self.normalizedSignedDegrees(relativeBearingDegrees(to: beam))) <= 70
    }

    private func relativeDirectionPhrase(to place: OverlayPlace) -> String {
        let bearing = Self.normalizedSignedDegrees(relativeBearingDegrees(to: place))
        let absBearing = abs(bearing)
        if absBearing <= 18 { return "straight ahead" }
        if bearing > 0 {
            return absBearing < 95 ? "ahead and on your right" : "on your right"
        }
        return absBearing < 95 ? "ahead and on your left" : "on your left"
    }

    private static func normalizedSignedDegrees(_ degrees: Double) -> Double {
        var value = degrees.truncatingRemainder(dividingBy: 360)
        if value > 180 { value -= 360 }
        if value < -180 { value += 360 }
        return value
    }

    private func appendMovementTrend(to line: String, itemId: String) -> String {
        guard let trend = worldItemDistanceTrend[itemId], !trend.isEmpty else { return line }
        return "\(line) · \(trend)"
    }

    func emoji(for place: OverlayPlace) -> String {
        switch placeCategory(place) {
        case "drink": return "🍺"
        case "coffee": return "☕️"
        case "food": return "🍽️"
        case "snack": return "🍬"
        default: return "📍"
        }
    }

    func start() {
        locationManager.delegate = self
        locationManager.desiredAccuracy = kCLLocationAccuracyBest
        locationManager.distanceFilter = 3
        if applyStoredLocationModeIfNeeded(forceRefresh: true) {
            updatePermissionState()
            startPedometer()
            fetchInventory()
            startNearbyRefreshTimer()
            startClock()
            fetchIncomingTransmissionIfNeeded()
            return
        }
#if os(iOS)
        locationManager.pausesLocationUpdatesAutomatically = false
#endif

        let locationStatus = locationManager.authorizationStatus
        switch locationStatus {
        case .notDetermined:
            #if os(iOS)
            locationPermissionReady = false
            locationPermissionText = "location permission lets K1L0 show places and ambient objects near you."
            #else
            locationPermissionReady = true
            locationPermissionText = "desktop debug location ready."
            #endif
            requestLocationAuthorizationOnce()
        case .authorizedAlways, .authorizedWhenInUse:
            locationManager.startUpdatingLocation()
            #if os(iOS)
            startHeadingUpdates()
            #endif
        default:
            useFallbackLocation()
        }
        if locationStatus != .notDetermined {
            updatePermissionState(status: locationStatus)
        }

        startPedometer()
        fetchInventory()
        startNearbyRefreshTimer()
        startClock()
        fetchIncomingTransmissionIfNeeded()
    }

    func locationManagerDidChangeAuthorization(_ manager: CLLocationManager) {
        if isUsingFixedTestLocation { return }
        switch manager.authorizationStatus {
        case .authorizedAlways, .authorizedWhenInUse:
            updatePermissionState(status: manager.authorizationStatus)
            locationManager.startUpdatingLocation()
            #if os(iOS)
            startHeadingUpdates()
            #endif
        case .denied, .restricted:
            updatePermissionState(status: manager.authorizationStatus)
            useFallbackLocation()
        default:
            updatePermissionState(status: manager.authorizationStatus)
            break
        }
    }

    func locationManager(_ manager: CLLocationManager, didChangeAuthorization status: CLAuthorizationStatus) {
        locationManagerDidChangeAuthorization(manager)
    }

    func requestRequiredPermissions() {
        requestLocationPermissionFromGate()
        requestMotionPermissionFromGate()
    }

    // Re-reads both permission truths without prompting. Called on foreground and by the
    // gate so a permission revoked in Settings drops the app back to the lock screen.
    func refreshPermissionGateState() {
        updatePermissionState()
#if os(iOS)
        refreshMotionPermissionState()
#endif
    }

    func requestLocationPermissionFromGate() {
        if isUsingFixedTestLocation {
            applyStoredLocationModeIfNeeded(forceRefresh: true)
            updatePermissionState()
            return
        }
        let locationStatus = locationManager.authorizationStatus
        switch locationStatus {
        case .notDetermined:
            requestLocationAuthorizationOnce()
        case .authorizedAlways, .authorizedWhenInUse:
            locationManager.startUpdatingLocation()
            #if os(iOS)
            startHeadingUpdates()
            #endif
            updatePermissionState(status: locationStatus)
        default:
            #if os(iOS)
            openAppSettings()
            #else
            useFallbackLocation()
            #endif
            updatePermissionState(status: locationStatus)
        }
    }

    func requestMotionPermissionFromGate() {
#if os(iOS)
        guard CMPedometer.isStepCountingAvailable() else {
            refreshMotionPermissionState()
            return
        }
        if CMPedometer.authorizationStatus() == .denied || CMPedometer.authorizationStatus() == .restricted {
            openAppSettings()
            return
        }
        // notDetermined: the first pedometer query pops the system Motion & Fitness prompt;
        // the callback lands after the player answers it.
        let now = Date()
        pedometer.queryPedometerData(from: now.addingTimeInterval(-60), to: now) { [weak self] _, _ in
            DispatchQueue.main.async { self?.refreshPermissionGateState() }
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 2.0) { [weak self] in
            self?.refreshMotionPermissionState()
        }
#else
        motionPermissionReady = true
        motionPermissionDenied = false
        motionPermissionText = "desktop movement simulates motion."
#endif
    }

#if os(iOS)
    private func refreshMotionPermissionState() {
        guard CMPedometer.isStepCountingAvailable() else {
            motionPermissionReady = true
            motionPermissionDenied = false
            motionPermissionText = "motion not available on this device."
            return
        }
        switch CMPedometer.authorizationStatus() {
        case .authorized:
            motionPermissionReady = true
            motionPermissionDenied = false
            motionPermissionText = "motion ready."
        case .denied, .restricted:
            motionPermissionReady = false
            motionPermissionDenied = true
            motionPermissionText = "motion access is off. your real steps are how K1L0 charges signals and collects beams — nothing can be earned without the pedometer. step counts stay on this phone."
        case .notDetermined:
            motionPermissionReady = false
            motionPermissionDenied = false
            motionPermissionText = "your real steps charge signals and collect beams. step counts stay on this phone."
        @unknown default:
            motionPermissionReady = true
            motionPermissionDenied = false
            motionPermissionText = "motion permission status unknown."
        }
    }

    private func openAppSettings() {
        guard let url = URL(string: UIApplication.openSettingsURLString) else { return }
        UIApplication.shared.open(url)
    }
#endif

    private func requestLocationAuthorizationOnce() {
        guard !didRequestLocationAuthorization else { return }
        didRequestLocationAuthorization = true
        locationManager.requestWhenInUseAuthorization()
    }

    private func updatePermissionState(status: CLAuthorizationStatus? = nil) {
        if isUsingFixedTestLocation {
            locationPermissionReady = true
            locationPermissionDenied = false
            locationPermissionText = "test location active."
        } else {
            switch status ?? locationManager.authorizationStatus {
            case .authorizedAlways, .authorizedWhenInUse:
                locationPermissionReady = true
                locationPermissionDenied = false
                locationPermissionText = "location ready."
            case .denied, .restricted:
                #if os(iOS)
                locationPermissionReady = false
                locationPermissionDenied = true
                locationPermissionText = "location access is off. K1L0 anchors beams, signals, and nearby walkers to where you really are — the world can't load without GPS."
                #else
                locationPermissionReady = true
                locationPermissionDenied = false
                locationPermissionText = "location denied. using offline/simulated location."
                #endif
            case .notDetermined:
                #if os(iOS)
                locationPermissionReady = false
                locationPermissionDenied = false
                locationPermissionText = "K1L0 anchors beams, signals, and nearby walkers to where you really are. without GPS the world can't load around you."
                #else
                locationPermissionReady = true
                locationPermissionDenied = false
                locationPermissionText = "desktop debug location ready."
                #endif
            @unknown default:
                locationPermissionReady = true
                locationPermissionDenied = false
                locationPermissionText = "location permission status unknown."
            }
        }
#if !os(iOS)
        motionPermissionReady = true
        motionPermissionDenied = false
        motionPermissionText = "desktop movement simulates motion."
#endif
    }

    func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        if isUsingFixedTestLocation { return }
        guard let location = locations.last else { return }
        currentLocation = location
        updateBeamApproachState()
        checkForBeamCollection()
        
        #if os(macOS)
        // On macOS Standalone, send the real location updates to Unity as "simulated" location
        // because Unity's built-in GPS Location Service is not active/supported on macOS.
        let mode = UserDefaults.standard.string(forKey: NativeLocationPreset.storageKey) ?? NativeLocationPreset.liveId
        let payload: [String: Any] = [
            "mode": mode,
            "liveGps": false,
            "latitude": location.coordinate.latitude,
            "longitude": location.coordinate.longitude,
            "name": "Live GPS"
        ]
        if let data = try? JSONSerialization.data(withJSONObject: payload),
           let json = String(data: data, encoding: .utf8) {
            K1L0WeatherOverlayInstaller.applyNativeSimulatedLocation(json)
        }
        #endif
        
        fetchWeather(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
        if !didFetchNearby {
            didFetchNearby = true
            fetchNearby(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
        }
    }

#if os(iOS)
    func locationManager(_ manager: CLLocationManager, didUpdateHeading newHeading: CLHeading) {
        let heading = newHeading.trueHeading >= 0 ? newHeading.trueHeading : newHeading.magneticHeading
        if heading >= 0 {
            headingDegrees = heading
        }
    }
#endif

    func locationManager(_ manager: CLLocationManager, didFailWithError error: Error) {
        if isUsingFixedTestLocation { return }
        useFallbackLocation()
    }

    func distanceText(to place: OverlayPlace) -> String {
        formatDistance(distanceMeters(to: place))
    }

    func distanceText(to beam: OverlayBeam) -> String {
        formatDistance(distanceMeters(to: beam))
    }

    func userLocationText(_ user: OverlayUser) -> String {
        guard let lat = user.lat, let lng = user.lng else { return "no live location" }
        guard let currentLocation else { return "live" }
        return formatDistance(currentLocation.distance(from: CLLocation(latitude: lat, longitude: lng)))
    }

    func relativeBearingDegrees(to place: OverlayPlace) -> Double {
        guard let currentLocation else { return 0 }
        return Self.bearingDegrees(
            from: currentLocation.coordinate,
            to: CLLocationCoordinate2D(latitude: place.coordinates.lat, longitude: place.coordinates.lng)
        ) - headingDegrees
    }

    func relativeBearingDegrees(to beam: OverlayBeam) -> Double {
        guard let currentLocation else { return 0 }
        return Self.bearingDegrees(
            from: currentLocation.coordinate,
            to: CLLocationCoordinate2D(latitude: beam.lat, longitude: beam.lng)
        ) - headingDegrees
    }

    func expirationText(for beam: OverlayBeam) -> String {
        guard let expiresAt = beam.expiresAt else { return "expires --" }
        let expiresDate = Date(timeIntervalSince1970: expiresAt / 1000)
        let remainingSeconds = max(0, Int(expiresDate.timeIntervalSince(now).rounded()))
        if remainingSeconds <= 0 { return "expires now" }
        let days = remainingSeconds / 86_400
        let hours = (remainingSeconds % 86_400) / 3_600
        let minutes = (remainingSeconds % 3_600) / 60
        let seconds = remainingSeconds % 60
        if days > 0 { return "expires in \(days)d \(hours)h" }
        if hours > 0 { return "expires in \(hours)h \(minutes)m" }
        if minutes > 0 { return "expires in \(minutes)m \(seconds)s" }
        return "expires in \(seconds)s"
    }

    func expirationCountdown(for beam: OverlayBeam) -> String {
        guard let expiresAt = beam.expiresAt else { return "--" }
        let expiresDate = Date(timeIntervalSince1970: expiresAt / 1000)
        let remainingSeconds = max(0, Int(expiresDate.timeIntervalSince(now).rounded()))
        if remainingSeconds <= 0 { return "0s" }
        let minutes = remainingSeconds / 60
        let seconds = remainingSeconds % 60
        if minutes > 0 { return "\(minutes)m \(seconds)s" }
        return "\(seconds)s"
    }

    func transmissionUrgencyText(for beam: OverlayBeam) -> String {
        if isWalkingToward(beam) { return "WALK" }
        return "expires in \(expirationCountdown(for: beam))"
    }

    func isWalkingToward(_ beam: OverlayBeam) -> Bool {
        guard liveSteps > 0, let until = walkingTowardUntil[beam.id] else { return false }
        return until > now
    }

    func signalStrength(for beam: OverlayBeam) -> Double {
        let distance = max(0, distanceMeters(to: beam))
        let maxDistance = 500.0
        return min(1, max(0.08, 1 - (distance / maxDistance)))
    }

    private func useFallbackLocation() {
#if os(macOS)
        let fallback = fixedLocationForCurrentMode() ?? Self.macDebugLocation
#else
        let fallback = fixedLocationForCurrentMode() ?? CLLocation(latitude: NativeLocationPreset.fallback.latitude, longitude: NativeLocationPreset.fallback.longitude)
#endif
        currentLocation = fallback
        fetchWeather(latitude: fallback.coordinate.latitude, longitude: fallback.coordinate.longitude)
        if !didFetchNearby {
            didFetchNearby = true
            fetchNearby(latitude: fallback.coordinate.latitude, longitude: fallback.coordinate.longitude)
        }
    }

    private var isUsingFixedTestLocation: Bool {
        NativeLocationPreset.preset(for: UserDefaults.standard.string(forKey: NativeLocationPreset.storageKey) ?? NativeLocationPreset.liveId) != nil
    }

    private func fixedLocationForCurrentMode() -> CLLocation? {
        let mode = UserDefaults.standard.string(forKey: NativeLocationPreset.storageKey) ?? NativeLocationPreset.liveId
        guard let preset = NativeLocationPreset.preset(for: mode) else { return nil }
        return CLLocation(latitude: preset.latitude, longitude: preset.longitude)
    }

    private func destinationCoordinate(from start: CLLocationCoordinate2D, meters: Double, bearingDegrees: Double) -> CLLocationCoordinate2D {
        let earthRadiusMeters = 6_371_000.0
        let angularDistance = meters / earthRadiusMeters
        let bearing = bearingDegrees * .pi / 180
        let lat1 = start.latitude * .pi / 180
        let lon1 = start.longitude * .pi / 180
        let lat2 = asin(sin(lat1) * cos(angularDistance) + cos(lat1) * sin(angularDistance) * cos(bearing))
        let lon2 = lon1 + atan2(
            sin(bearing) * sin(angularDistance) * cos(lat1),
            cos(angularDistance) - sin(lat1) * sin(lat2)
        )
        return CLLocationCoordinate2D(latitude: lat2 * 180 / .pi, longitude: lon2 * 180 / .pi)
    }

    private func advanceFixedTestLocation(from oldSteps: Int, to newSteps: Int) {
        guard isUsingFixedTestLocation else { return }
        guard newSteps > oldSteps else {
            simulatedLocationStepBaseline = newSteps
            return
        }
        let deltaSteps = min(40, max(0, newSteps - max(oldSteps, simulatedLocationStepBaseline)))
        guard deltaSteps > 0 else { return }
        let start = currentLocation ?? fixedLocationForCurrentMode()
        guard let start else { return }
        let meters = Double(deltaSteps) * 0.76
        let nextCoordinate = destinationCoordinate(
            from: start.coordinate,
            meters: meters,
            bearingDegrees: headingDegrees
        )
        let nextLocation = CLLocation(latitude: nextCoordinate.latitude, longitude: nextCoordinate.longitude)
        currentLocation = nextLocation
        simulatedLocationStepBaseline = newSteps
        updateBeamApproachState()
        checkForBeamCollection()
        sendNativeSimulatedLocation(location: nextLocation)
        if needsFreshPlaces(latitude: nextCoordinate.latitude, longitude: nextCoordinate.longitude) {
            fetchWeather(latitude: nextCoordinate.latitude, longitude: nextCoordinate.longitude)
            fetchNearby(latitude: nextCoordinate.latitude, longitude: nextCoordinate.longitude)
        }
    }

    @discardableResult
    private func applyStoredLocationModeIfNeeded(forceRefresh: Bool) -> Bool {
        let mode = UserDefaults.standard.string(forKey: NativeLocationPreset.storageKey) ?? NativeLocationPreset.liveId
        guard NativeLocationPreset.preset(for: mode) != nil else {
            sendNativeLocationMode(mode)
            return false
        }
        setLocationMode(mode, forceRefresh: forceRefresh)
        return true
    }

    func setLocationMode(_ mode: String, forceRefresh: Bool = true) {
        UserDefaults.standard.set(mode, forKey: NativeLocationPreset.storageKey)
        sendNativeLocationMode(mode)

        guard let preset = NativeLocationPreset.preset(for: mode) else {
            switch locationManager.authorizationStatus {
            case .authorizedAlways, .authorizedWhenInUse:
                locationManager.startUpdatingLocation()
                #if os(iOS)
                startHeadingUpdates()
                #endif
                locationPermissionReady = true
                locationPermissionText = "location ready."
            case .notDetermined:
                requestLocationAuthorizationOnce()
            default:
                useFallbackLocation()
                #if !os(iOS)
                locationPermissionReady = true
                locationPermissionText = "desktop debug location ready."
                #endif
            }
            return
        }

    #if os(iOS)
        locationManager.stopUpdatingLocation()
        startHeadingUpdates()
    #endif
        let location = CLLocation(latitude: preset.latitude, longitude: preset.longitude)
        currentLocation = location
        simulatedLocationStepBaseline = liveSteps
        lastSimulatedLocationPushAt = Date.distantPast
        locationPermissionReady = true
        locationPermissionText = "test location: \(preset.title)."
        resetWorldMemory()
        fetchWeather(latitude: preset.latitude, longitude: preset.longitude)
        fetchNearby(latitude: preset.latitude, longitude: preset.longitude, forceWorldRefresh: forceRefresh)
    }

    private func resetWorldMemory() {
        places = []
        beams = []
        collectCandidateBeam = nil
        locationStatus = "loading nearby places..."
	        beamStatus = "scanning ambient..."
		        lastPlaceTileKeys.removeAll()
		        lastPlacePrimaryTileKey = nil
		        lastPlaceHalfHourBucket = nil
		        lastPlaceFetchLocation = nil
        didFetchNearby = false
        lastWeatherFetchAt = Date.distantPast
        lastWeatherFetchLocation = nil
        lastBeamDistances.removeAll()
        lastWorldItemDistances.removeAll()
        worldItemDistanceTrend.removeAll()
        walkingTowardUntil.removeAll()
        walkingAwayStartSteps.removeAll()
        dismissedBeamIds.removeAll()
        collectingBeamIds.removeAll()
        let emptyWorld = #"{"ok":true,"includePlaces":true,"includeBeams":true,"places":[],"beams":[]}"#
        K1L0WeatherOverlayInstaller.applyNativeWorldNearby(emptyWorld)
    }

    private func sendNativeLocationMode(_ mode: String, location overrideLocation: CLLocation? = nil) {
        var payload: [String: Any] = ["mode": mode, "liveGps": mode == NativeLocationPreset.liveId]
        if let preset = NativeLocationPreset.preset(for: mode) {
            payload["name"] = preset.title
            payload["latitude"] = overrideLocation?.coordinate.latitude ?? preset.latitude
            payload["longitude"] = overrideLocation?.coordinate.longitude ?? preset.longitude
        }
        guard let data = try? JSONSerialization.data(withJSONObject: payload),
              let json = String(data: data, encoding: .utf8) else { return }
        K1L0WeatherOverlayInstaller.applyNativeLocationMode(json)
    }

    private func sendNativeSimulatedLocation(location: CLLocation) {
        guard Date().timeIntervalSince(lastSimulatedLocationPushAt) >= 0.35 else { return }
        let mode = UserDefaults.standard.string(forKey: NativeLocationPreset.storageKey) ?? NativeLocationPreset.liveId
        guard NativeLocationPreset.preset(for: mode) != nil else { return }
        var payload: [String: Any] = [
            "mode": mode,
            "liveGps": false,
            "latitude": location.coordinate.latitude,
            "longitude": location.coordinate.longitude
        ]
        if let preset = NativeLocationPreset.preset(for: mode) {
            payload["name"] = preset.title
        }
        guard let data = try? JSONSerialization.data(withJSONObject: payload),
              let json = String(data: data, encoding: .utf8) else { return }
        lastSimulatedLocationPushAt = Date()
        K1L0WeatherOverlayInstaller.applyNativeSimulatedLocation(json)
    }

    private func startHeadingUpdates() {
#if os(iOS)
        if CLLocationManager.headingAvailable() {
            locationManager.startUpdatingHeading()
        }
#endif
    }

    private func startPedometer() {
#if os(iOS)
        refreshMotionPermissionState()
        guard CMPedometer.isStepCountingAvailable() else { return }
        refreshPedometerSession(forceRestart: true)
        refreshPedometerTotals()

        pedometerSessionTimer?.invalidate()
        pedometerSessionTimer = Timer.scheduledTimer(withTimeInterval: 30, repeats: true) { [weak self] _ in
            self?.refreshPedometerSession(forceRestart: true)
        }

        pedometerStatsTimer?.invalidate()
        pedometerStatsTimer = Timer.scheduledTimer(withTimeInterval: 60, repeats: true) { [weak self] _ in
            self?.refreshPedometerTotals()
        }
#endif
    }

    private func refreshPedometerTotals() {
#if os(iOS)
        let group = DispatchGroup()
        var dayTotal = 0
        var weekTotal = 0
        group.enter()
        querySteps(since: Date(timeIntervalSinceNow: -24 * 60 * 60)) { value in
            dayTotal = value
            group.leave()
        }
        group.enter()
        querySteps(since: Date(timeIntervalSinceNow: -7 * 24 * 60 * 60)) { value in
            weekTotal = value
            group.leave()
        }
        group.notify(queue: .main) { [weak self] in
            self?.steps24h = dayTotal
            self?.steps7d = weekTotal
            self?.publishStepTotals()
        }
#endif
    }

    private func publishStepTotals() {
        guard let userId = currentUserIdForInventory(), !userId.isEmpty else { return }
        resolveAPIBase { [weak self] apiBase in
            guard let self, let url = URL(string: "\(apiBase)/api/k1l0/steps") else { return }
            var request = URLRequest(url: url)
            request.httpMethod = "POST"
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            request.httpBody = try? JSONSerialization.data(withJSONObject: [
                "userId": userId,
                "steps24h": self.steps24h,
                "steps7d": self.steps7d
            ])
            URLSession.shared.dataTask(with: request).resume()
        }
    }

    fileprivate func handleUnityStepState(_ json: String) {
#if os(macOS)
        guard let data = json.data(using: .utf8),
              let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return }
        let nextLive = max(0, obj["liveSteps"] as? Int ?? liveSteps)
        let next24h = max(0, obj["steps24h"] as? Int ?? steps24h)
        let next7d = max(0, obj["steps7d"] as? Int ?? steps7d)
        let latitude = obj["latitude"] as? Double
        let longitude = obj["longitude"] as? Double

        if nextLive > 0 {
            if liveSteps <= 0 || nextLive < liveSteps {
                liveStepSessionStart = Date()
            }
            liveStepIdleStart = nil
        } else {
            if liveSteps > 0 || liveStepIdleStart == nil {
                liveStepIdleStart = Date()
            }
            liveStepSessionStart = nil
        }

        liveSteps = nextLive
        steps24h = next24h
        steps7d = next7d

        if let latitude, let longitude, latitude != 0, longitude != 0 {
            let simulatedLocation = CLLocation(latitude: latitude, longitude: longitude)
            currentLocation = simulatedLocation
            updateBeamApproachState()
            checkForBeamCollection()

            if needsFreshPlaces(latitude: latitude, longitude: longitude) {
                fetchWeather(latitude: latitude, longitude: longitude)
                fetchNearby(latitude: latitude, longitude: longitude)
            }
        }
#endif
    }

    private func refreshPedometerSession(forceRestart: Bool) {
#if os(iOS)
        guard CMPedometer.isStepCountingAvailable(), !pedometerSessionRefreshInFlight else { return }
        pedometerSessionRefreshInFlight = true

        let now = Date()
        let bucketSeconds = pedometerSessionBucketSeconds()
        let oldest = now.addingTimeInterval(-bucketSeconds * 48)
        let inactiveStepThreshold = pedometerInactiveStepThreshold(bucketSeconds: bucketSeconds)
        let inactiveMeterThreshold = pedometerInactiveMeterThreshold(bucketSeconds: bucketSeconds)

        crawlPedometerSession(
            cursor: now,
            oldest: oldest,
            bucketSeconds: bucketSeconds,
            sessionStart: now,
            sessionSteps: 0,
            idleStart: nil,
            inactiveStepThreshold: inactiveStepThreshold,
            inactiveMeterThreshold: inactiveMeterThreshold
        ) { [weak self] start, steps in
            DispatchQueue.main.async {
                guard let self else { return }
                self.pedometerSessionRefreshInFlight = false
                self.liveSteps = max(0, steps)
                self.liveStepSessionStart = steps > 0 ? start : nil
                self.liveStepIdleStart = steps > 0 ? nil : start

                let previous = self.pedometerSessionStart
                let shouldRestart = forceRestart && (previous == nil || abs((previous ?? start).timeIntervalSince(start)) > 1)
                guard shouldRestart else { return }

                self.pedometerSessionStart = start
                self.pedometer.stopUpdates()
                self.pedometer.startUpdates(from: start) { [weak self] data, _ in
                    guard let data else { return }
                    DispatchQueue.main.async {
                        guard let self else { return }
                        let steps = max(0, data.numberOfSteps.intValue)
                        self.liveSteps = steps
                        self.liveStepSessionStart = steps > 0 ? start : nil
                        self.liveStepIdleStart = steps > 0 ? nil : start
                    }
                }
            }
        }
#endif
    }

    private func crawlPedometerSession(
        cursor: Date,
        oldest: Date,
        bucketSeconds: TimeInterval,
        sessionStart: Date,
        sessionSteps: Int,
        idleStart: Date?,
        inactiveStepThreshold: Int,
        inactiveMeterThreshold: Double,
        completion: @escaping (Date, Int) -> Void
    ) {
#if os(iOS)
        let bucketStart = maxDate(cursor.addingTimeInterval(-bucketSeconds), oldest)
        pedometer.queryPedometerData(from: bucketStart, to: cursor) { [weak self] data, _ in
            let steps = max(0, data?.numberOfSteps.intValue ?? 0)
            let meters = max(0, data?.distance?.doubleValue ?? 0)
            let inactive = steps < inactiveStepThreshold || (meters > 0 && meters < inactiveMeterThreshold)

            if inactive || bucketStart <= oldest {
                if sessionSteps == 0 && bucketStart > oldest {
                    self?.crawlPedometerSession(
                        cursor: bucketStart,
                        oldest: oldest,
                        bucketSeconds: bucketSeconds,
                        sessionStart: sessionStart,
                        sessionSteps: 0,
                        idleStart: bucketStart,
                        inactiveStepThreshold: inactiveStepThreshold,
                        inactiveMeterThreshold: inactiveMeterThreshold,
                        completion: completion
                    )
                    return
                }
                if sessionSteps == 0 {
                    completion(idleStart ?? bucketStart, 0)
                    return
                }
                completion(sessionStart, sessionSteps)
                return
            }

            if let idleStart {
                completion(idleStart, 0)
                return
            }

            self?.crawlPedometerSession(
                cursor: bucketStart,
                oldest: oldest,
                bucketSeconds: bucketSeconds,
                sessionStart: bucketStart,
                sessionSteps: sessionSteps + steps,
                idleStart: nil,
                inactiveStepThreshold: inactiveStepThreshold,
                inactiveMeterThreshold: inactiveMeterThreshold,
                completion: completion
            )
        }
#endif
    }

    private func pedometerSessionBucketSeconds() -> TimeInterval {
        let stored = UserDefaults.standard.double(forKey: "k1lo_native_momentumSessionGraceMinutes")
        let minutes = stored > 0 ? stored : 1.5
        return min(240, max(10, minutes)) * 60
    }

    private func pedometerInactiveStepThreshold(bucketSeconds: TimeInterval) -> Int {
        let minutes = bucketSeconds / 60
        let minSteps = Int(UserDefaults.standard.double(forKey: "k1lo_native_ambientMinStepsToSpawn"))
        let scaled = Int((minutes * 4).rounded())
        return min(max(3, scaled), max(3, max(0, minSteps) - 1))
    }

    private func pedometerInactiveMeterThreshold(bucketSeconds: TimeInterval) -> Double {
        max(2, (bucketSeconds / 60) * 3)
    }

    private func maxDate(_ lhs: Date, _ rhs: Date) -> Date {
        lhs > rhs ? lhs : rhs
    }

    private func querySteps(since start: Date, assign: @escaping (Int) -> Void) {
#if os(iOS)
        pedometer.queryPedometerData(from: start, to: Date()) { data, _ in
            DispatchQueue.main.async {
                assign(data?.numberOfSteps.intValue ?? 0)
            }
        }
#endif
    }

	    private func fetchNearby(latitude: Double, longitude: Double, forceWorldRefresh: Bool = false) {
		        locationStatus = places.isEmpty ? "loading nearby places…" : locationStatus
		        beamStatus = beams.isEmpty ? "scanning ambient…" : beamStatus
		        let location = currentLocation ?? CLLocation(latitude: latitude, longitude: longitude)
		        let requestLatitude = latitude
		        let requestLongitude = longitude
		        let includePlaces = forceWorldRefresh || needsFreshPlaces(latitude: requestLatitude, longitude: requestLongitude)
		        let includeBeams = forceWorldRefresh || needsFreshBeams(location: location)
	        resolveAPIBase { [weak self] apiBase in
	            guard let self else { return }
	            if includePlaces || includeBeams {
	                self.fetchWorldNearby(
	                    latitude: requestLatitude,
	                    longitude: requestLongitude,
	                    apiBase: apiBase,
	                    includePlaces: includePlaces,
	                    includeBeams: includeBeams
	                )
	            }
	            self.fetchNearbyUsers(apiBase: apiBase)
	            self.fetchStepLeaderboard(apiBase: apiBase)
	            self.fetchInventory()
	        }
	    }

	    private func currentHalfHourBucket() -> Int {
	        Int(floor(Date().timeIntervalSince1970 / (30 * 60)))
	    }

	    private func placeCellKey(latitude: Double, longitude: Double) -> String {
	        let cellMeters = 1500.0
	        let earthRadius = 6378137.0
	        let x = earthRadius * longitude * .pi / 180
	        let clampedLat = min(85.05112878, max(-85.05112878, latitude))
	        let latRad = clampedLat * .pi / 180
	        let y = earthRadius * log(tan(.pi / 4 + latRad / 2))
	        let cx = Int((x / cellMeters).rounded())
	        let cy = Int((y / cellMeters).rounded())
	        return "p1500_\(cx)_\(cy)"
	    }

	    private func placeTileKeys(latitude: Double, longitude: Double) -> Set<String> {
	        let cellMeters = 1500.0
	        let earthRadius = 6378137.0
	        let x = earthRadius * longitude * .pi / 180
	        let clampedLat = min(85.05112878, max(-85.05112878, latitude))
	        let latRad = clampedLat * .pi / 180
	        let y = earthRadius * log(tan(.pi / 4 + latRad / 2))
	        let cx = Int((x / cellMeters).rounded())
	        let cy = Int((y / cellMeters).rounded())
	        var keys = Set<String>()
	        for dx in -1...1 {
	            for dy in -1...1 {
	                keys.insert("p1500_\(cx + dx)_\(cy + dy)")
	            }
	        }
	        return keys
	    }

	    private func needsFreshPlaces(latitude: Double, longitude: Double) -> Bool {
	        let bucket = currentHalfHourBucket()
	        let primaryKey = placeCellKey(latitude: latitude, longitude: longitude)
	        let keys = placeTileKeys(latitude: latitude, longitude: longitude)
	        let location = CLLocation(latitude: latitude, longitude: longitude)
	        let movedSinceLastPlaceFetch = lastPlaceFetchLocation.map {
	            location.distance(from: $0) > 100
	        } ?? true
	        return places.isEmpty
	            || lastPlaceHalfHourBucket != bucket
	            || lastPlacePrimaryTileKey != primaryKey
	            || lastPlaceTileKeys != keys
	            || movedSinceLastPlaceFetch
	    }

	    private func markPlacesFresh(latitude: Double, longitude: Double) {
	        lastPlaceHalfHourBucket = currentHalfHourBucket()
	        lastPlacePrimaryTileKey = placeCellKey(latitude: latitude, longitude: longitude)
	        lastPlaceTileKeys = placeTileKeys(latitude: latitude, longitude: longitude)
	        lastPlaceFetchLocation = CLLocation(latitude: latitude, longitude: longitude)
	    }

	    private func normalizedBearingDelta(_ degrees: Double) -> Double {
	        var delta = degrees.truncatingRemainder(dividingBy: 360)
	        if delta > 180 { delta -= 360 }
	        if delta < -180 { delta += 360 }
	        return delta
	    }

	    private func walkingBearingDegrees(for location: CLLocation) -> Double {
	        if location.course >= 0 {
	            return location.course
	        }
	        return headingDegrees
	    }

	    private func isActivelyWalking(_ location: CLLocation) -> Bool {
	        if location.speed >= 0.45 { return true }
	        return liveSteps > 0
	    }

	    private func hasUsableForwardBeam(from location: CLLocation) -> Bool {
	        let walkingBearing = walkingBearingDegrees(for: location)
	        let usableForwardMaxDistance = max(120.0, collectRadiusMeters() * 4.0)
	        return beams.contains { beam in
	            guard !isExpired(beam), !dismissedBeamIds.contains(beam.id) else { return false }
	            let beamLocation = CLLocation(latitude: beam.lat, longitude: beam.lng)
	            let bearing = Self.bearingDegrees(
	                from: location.coordinate,
	                to: beamLocation.coordinate
	            )
	            return location.distance(from: beamLocation) <= usableForwardMaxDistance
	                && abs(normalizedBearingDelta(bearing - walkingBearing)) <= 70
	        }
	    }

	    private func needsFreshBeams(location: CLLocation) -> Bool {
	        guard incomingTransmission == nil else { return false }
	        guard isActivelyWalking(location) else { return false }
	        return !hasUsableForwardBeam(from: location)
	    }

    private func startNearbyRefreshTimer() {
        nearbyRefreshTimer?.invalidate()
        nearbyRefreshTimer = Timer.scheduledTimer(withTimeInterval: 30, repeats: true) { [weak self] _ in
            guard let self, let location = self.currentLocation else { return }
            self.fetchNearby(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
        }
    }

    func setVideoPlaybackActive(_ active: Bool) {
        guard videoPlaybackActive != active else { return }
        videoPlaybackActive = active
        if active {
            nearbyRefreshTimer?.invalidate()
            nearbyRefreshTimer = nil
            clockTimer?.invalidate()
            clockTimer = nil
#if os(iOS)
            pedometerSessionTimer?.invalidate()
            pedometerSessionTimer = nil
            pedometerStatsTimer?.invalidate()
            pedometerStatsTimer = nil
            pedometer.stopUpdates()
            locationManager.stopUpdatingHeading()
            locationManager.stopUpdatingLocation()
#endif
        } else {
            startClock()
            startNearbyRefreshTimer()
#if os(iOS)
            startPedometer()
            locationManager.startUpdatingLocation()
            startHeadingUpdates()
#endif
            refreshTransmissionState()
        }
    }

    func refreshTransmissionState(clearStaleCache: Bool = false) {
        if clearStaleCache {
            K1L0ActiveTransmissionStore.shared.clearFinishedCached()
        }
        fetchInventory()
        fetchLatestTransmission(clearStaleCache: clearStaleCache)
        fetchIncomingTransmissionIfNeeded()
        if let location = currentLocation {
            fetchNearby(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
        }
    }

	    private func fetchWorldNearby(latitude: Double, longitude: Double, apiBase: String, includePlaces: Bool = true, includeBeams: Bool = true) {
	        guard let url = URL(string: "\(apiBase)/api/k1l0/world/nearby") else { return }
	        let userId = currentUserIdForInventory() ?? "anon"
	        let movementBearing = currentLocation.map { walkingBearingDegrees(for: $0) } ?? headingDegrees
	        let walkingSpeed = max(0, currentLocation?.speed ?? 0)
	        var request = URLRequest(url: url)
	        request.httpMethod = "POST"
	        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: [
            "userId": userId,
            "latitude": latitude,
            "longitude": longitude,
	            "radiusMeters": 3500,
	            "maxMiles": 1.1,
	            "stepMeters": 75,
	            "minDistanceMeters": 45,
	            "ttlMinutes": beamTtlMinutes(),
	            "movementBearing": movementBearing,
	            "walkingSpeedMetersPerSecond": walkingSpeed,
	            "includePlaces": includePlaces,
	            "includeBeams": includeBeams
	        ])

        URLSession.shared.dataTask(with: request) { [weak self] data, response, error in
            guard let data else {
                let code = (response as? HTTPURLResponse)?.statusCode ?? 0
	                DispatchQueue.main.async {
	                    self?.locationStatus = "world unavailable \(code)"
	                    self?.beamStatus = "world unavailable \(code)"
	                    if includePlaces {
	                        self?.fetchPlaces(latitude: latitude, longitude: longitude, apiBase: apiBase)
	                    }
	                    if includeBeams {
	                        self?.fetchBeams(latitude: latitude, longitude: longitude, apiBase: apiBase)
	                    }
	                }
                if let error { print("[K1L0Overlay] world fetch error: \(error.localizedDescription)") }
                return
            }
            let decoded: OverlayWorldNearbyResponse
            do {
                decoded = try JSONDecoder().decode(OverlayWorldNearbyResponse.self, from: data)
            } catch {
                let snippet = String(data: data.prefix(240), encoding: .utf8) ?? "non-utf8"
	                DispatchQueue.main.async {
	                    self?.locationStatus = "world decode error"
	                    self?.beamStatus = "world decode error"
	                    if includePlaces {
	                        self?.fetchPlaces(latitude: latitude, longitude: longitude, apiBase: apiBase)
	                    }
	                    if includeBeams {
	                        self?.fetchBeams(latitude: latitude, longitude: longitude, apiBase: apiBase)
	                    }
	                }
                print("[K1L0Overlay] world decode error: \(error) body=\(snippet)")
                return
	            }
	            var worldNearbyJson = String(data: data, encoding: .utf8) ?? ""
	            if var root = (try? JSONSerialization.jsonObject(with: data)) as? [String: Any] {
	                root["includePlaces"] = includePlaces
	                root["includeBeams"] = includeBeams
	                if let payload = try? JSONSerialization.data(withJSONObject: root),
	                   let encoded = String(data: payload, encoding: .utf8) {
	                    worldNearbyJson = encoded
	                }
	            }
	            DispatchQueue.main.async {
	                if includePlaces || includeBeams {
	                    K1L0WeatherOverlayInstaller.applyNativeWorldNearby(worldNearbyJson)
	                }
	                if includePlaces {
	                    self?.places = decoded.places.sorted { $0.distance < $1.distance }
	                    self?.locationStatus = decoded.places.isEmpty ? "no open places nearby" : "\(decoded.places.count) open places nearby"
	                    self?.markPlacesFresh(latitude: latitude, longitude: longitude)
	                    self?.applyLocationFilter()
	                }
	                if includeBeams {
	                    let fetchedIds = Set(decoded.beams.map { $0.id })
	                    self?.dismissedBeamIds = self?.dismissedBeamIds.filter { fetchedIds.contains($0) } ?? []
	                    let activeBeams = decoded.beams.filter {
	                        self?.isExpired($0) == false && self?.dismissedBeamIds.contains($0.id) == false
	                    }
	                    self?.beams = activeBeams
	                    self?.beamStatus = activeBeams.isEmpty ? "no nearby ambient" : "\(activeBeams.count) nearby"
	                }
	                self?.updateBeamApproachState()
	                self?.checkForBeamCollection()
	            }
        }.resume()
    }

    func respondToTransmission(_ result: K1L0TransmissionResult, option: String) {
        guard let userId = currentUserIdForInventory() else {
            print("[K1L0Overlay] respond dropped: no user id")
            return
        }
        // Reply to the DEEPEST slide in the chain that carries a job identity —
        // that's the response being answered. Fall back to the result's own
        // source (plain incoming transmissions), then to the viewer's own job
        // (own-thread view, where sourceUserId is nil). Previously this
        // guard-failed silently in the own-thread view and always targeted the
        // root, which is how a reply could clobber the response slide.
        var parentUserId = ""
        var parentJobId = ""
        if let deepest = result.clips.last(where: { !$0.sourceJobId.isEmpty }) {
            parentJobId = deepest.sourceJobId
            parentUserId = deepest.sourceUserId.isEmpty ? (result.sourceUserId ?? userId) : deepest.sourceUserId
        } else if let sourceUserId = result.sourceUserId, let jobId = result.jobId {
            parentUserId = sourceUserId
            parentJobId = jobId
        } else if let jobId = result.jobId {
            parentUserId = userId
            parentJobId = jobId
        }
        guard !parentUserId.isEmpty, !parentJobId.isEmpty else {
            print("[K1L0Overlay] respond dropped: no parent identity on result \(result.jobId ?? "nil")")
            return
        }
        resolveAPIBase { apiBase in
            guard let url = URL(string: "\(apiBase)/api/k1l0/v2/transmit/respond") else { return }
            var request = URLRequest(url: url)
            request.httpMethod = "POST"
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            let body: [String: Any] = [
                "userId": userId,
                "parentUserId": parentUserId,
                "parentJobId": parentJobId,
                "selectedResponse": option
            ]
            request.httpBody = try? JSONSerialization.data(withJSONObject: body)
            URLSession.shared.dataTask(with: request) { [weak self] _, _, _ in
                DispatchQueue.main.async {
                    self?.setIncomingWaitBaseline(self?.liveSteps ?? 0)
                    self?.clearIncomingTuneBaseline()
                    self?.incomingTransmission = nil
                    self?.receiveProgressSteps = 0
                    self?.receiveSignalStatus = "response transmitting…"
                    self?.fetchIncomingTransmissionIfNeeded()
                }
            }.resume()
        }
    }

    private func handleLiveStepsChanged() {
        if incomingTransmission != nil {
            updateReceiveProgress()
        } else {
            fetchIncomingTransmissionIfNeeded()
        }
    }

    private func normalizeIncomingBaselinesForLiveSteps() {
        if !incomingWaitBaselineInitialized {
            setIncomingWaitBaseline(liveSteps)
        } else if liveSteps == 0 && incomingWaitBaselineSteps > 0 {
            setIncomingWaitBaseline(liveSteps)
        }
    }

    private func setIncomingWaitBaseline(_ steps: Int) {
        incomingWaitBaselineSteps = max(0, steps)
        incomingWaitBaselineInitialized = true
        UserDefaults.standard.set(incomingWaitBaselineSteps, forKey: K1L0OverlayDataModel.incomingWaitBaselineKey)
    }

    private func setIncomingTuneBaseline(_ steps: Int, signalId: String) {
        incomingTuneBaselineSteps = max(0, steps)
        incomingTuneSignalId = signalId
        UserDefaults.standard.set(incomingTuneBaselineSteps, forKey: K1L0OverlayDataModel.incomingTuneBaselineKey)
        UserDefaults.standard.set(incomingTuneSignalId, forKey: K1L0OverlayDataModel.incomingTuneSignalIdKey)
    }

    private func clearIncomingTuneBaseline() {
        incomingTuneBaselineSteps = 0
        incomingTuneSignalId = ""
        UserDefaults.standard.removeObject(forKey: K1L0OverlayDataModel.incomingTuneBaselineKey)
        UserDefaults.standard.removeObject(forKey: K1L0OverlayDataModel.incomingTuneSignalIdKey)
    }

    private func fetchIncomingTransmissionIfNeeded() {
        guard incomingTransmission == nil, !isFetchingIncomingTransmission else { return }
        guard activePursuedBeam == nil else { return }
        guard let userId = currentUserIdForInventory(), !userId.isEmpty else { return }
        guard liveSteps > 0 else {
            receiveSignalStatus = "walk for signal"
            return
        }
        let wait = transmissionWaitStepsRequired()
        let walkedSinceLastSignal = max(0, liveSteps - incomingWaitBaselineSteps)
        guard walkedSinceLastSignal >= wait else {
            receiveSignalStatus = "walk \(wait - walkedSinceLastSignal) steps for signal"
            return
        }
        guard Date().timeIntervalSince(lastIncomingScanAt) >= 15 else { return }
        lastIncomingScanAt = Date()
        isFetchingIncomingTransmission = true
        receiveSignalStatus = "scanning signals"
        resolveAPIBase { [weak self] apiBase in
            guard let self else { return }
            let safeUser = userId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? userId
            let excludeIds = self.localUserIdAliases()
                .map { self.sanitizeFirebaseKey($0) }
                .filter { !$0.isEmpty }
                .joined(separator: ",")
                .addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? ""
            guard let url = URL(string: "\(apiBase)/api/k1l0/v2/receive/random?userId=\(safeUser)&excludeUserIds=\(excludeIds)&originalOnly=1") else {
                self.receiveSignalStatus = "signal request failed"
                self.isFetchingIncomingTransmission = false
                return
            }
            URLSession.shared.dataTask(with: url) { [weak self] data, response, _ in
                DispatchQueue.main.async {
                    guard let self else { return }
                    self.isFetchingIncomingTransmission = false
                    let code = (response as? HTTPURLResponse)?.statusCode ?? 0
                    guard code == 200 else {
                        self.receiveSignalStatus = "searching for signals"
                        return
                    }
                    guard let data else {
                        self.receiveSignalStatus = "searching for signals"
                        return
                    }
                    let decoded: OverlayReceiveResponse
                    do {
                        decoded = try JSONDecoder().decode(OverlayReceiveResponse.self, from: data)
                    } catch {
                        self.receiveSignalStatus = "searching for signals"
                        return
                    }
                    guard decoded.ok else {
                        self.receiveSignalStatus = "searching for signals"
                        return
                    }
                    guard let transmission = decoded.transmission else {
                        self.receiveSignalStatus = "searching for signals"
                        self.requestNearbyTestSignal(apiBase: apiBase, userId: userId)
                        return
                    }
                    guard transmission.isOriginalTransmission else {
                        self.receiveSignalStatus = "searching for signals"
                        return
                    }
                    guard !self.isOwnIncomingTransmission(transmission, currentUserId: userId) else {
                        self.receiveSignalStatus = "searching for signals"
                        return
                    }
                    self.setIncomingTuneBaseline(self.liveSteps, signalId: transmission.id)
                    self.incomingTransmission = transmission
                    self.updateReceiveProgress()
                }
            }.resume()
        }
    }

    private func requestNearbyTestSignal(apiBase: String, userId: String) {
        let nowSeconds = Date().timeIntervalSince1970
        guard nowSeconds - lastIncomingSeedRequestAt >= 10 * 60 else { return }
        lastIncomingSeedRequestAt = nowSeconds
        UserDefaults.standard.set(nowSeconds, forKey: K1L0OverlayDataModel.incomingSeedRequestAtKey)

        var payload: [String: Any] = [
            "userId": userId,
            "city": cityText
        ]
        if let currentLocation {
            payload["latitude"] = currentLocation.coordinate.latitude
            payload["longitude"] = currentLocation.coordinate.longitude
        }
        guard let url = URL(string: "\(apiBase)/api/k1l0/v2/receive/seed-test") else { return }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: payload)
        URLSession.shared.dataTask(with: request) { data, response, _ in
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            let body = data.flatMap { String(data: $0, encoding: .utf8) } ?? ""
            print("[K1L0Overlay] seed-test signal status=\(code) body=\(body.prefix(240))")
        }.resume()
    }

    private func signalAcquisitionRemainingSteps() -> Int {
        if incomingTransmission != nil {
            return max(0, receiveStepsRequired() - receiveProgressSteps)
        }
        let wait = transmissionWaitStepsRequired()
        let walkedSinceLastSignal = max(0, liveSteps - incomingWaitBaselineSteps)
        return max(0, wait - walkedSinceLastSignal)
    }

    private func localUserIdAliases() -> [String] {
        var ids = Set<String>()
        if let current = currentUserIdForInventory() {
            ids.insert(current.trimmingCharacters(in: .whitespacesAndNewlines))
        }
        let defaults = UserDefaults.standard
        for key in ["FirebaseUserId", "K1L0UserId", "DeviceID", "deviceID"] {
            let value = defaults.string(forKey: key)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            if !value.isEmpty {
                ids.insert(value)
            }
        }
        return Array(ids)
    }

    private func isOwnIncomingTransmission(_ transmission: OverlayIncomingTransmission, currentUserId: String) -> Bool {
        let source = sanitizeFirebaseKey(transmission.sourceUserId).lowercased()
        let aliases = Set((localUserIdAliases() + [currentUserId]).map { sanitizeFirebaseKey($0).lowercased() })
        if aliases.contains(source) {
            return true
        }
        let activeJobId = K1L0ActiveTransmissionStore.shared.snapshot.jobId.trimmingCharacters(in: .whitespacesAndNewlines)
        return !activeJobId.isEmpty && activeJobId == transmission.jobId
    }

    private func updateReceiveProgress() {
        guard let incoming = incomingTransmission else {
            fetchIncomingTransmissionIfNeeded()
            return
        }
        let required = receiveStepsRequired()
        if incomingTuneSignalId != incoming.id {
            setIncomingTuneBaseline(liveSteps, signalId: incoming.id)
        }
        receiveProgressSteps = min(required, max(0, liveSteps - incomingTuneBaselineSteps))
        let pct = min(100, max(0, Int((Double(receiveProgressSteps) / Double(max(1, required))) * 100)))
        if receiveProgressSteps <= 0 {
            receiveSignalStatus = "faint signal detected"
        } else if receiveProgressSteps < required {
            receiveSignalStatus = "signal strength \(pct)%"
        } else if !receiveUnlockedIds.contains(incoming.id) {
            receiveSignalStatus = "signal locked"
            receiveUnlockedIds.insert(incoming.id)
            presentIncomingTransmission(incoming)
            setIncomingWaitBaseline(liveSteps)
            clearIncomingTuneBaseline()
            incomingTransmission = nil
        }
    }

    private func presentIncomingTransmission(_ incoming: OverlayIncomingTransmission) {
        var clips: [K1L0TransmissionClip] = []
        let localAliases = Set(localUserIdAliases().map { sanitizeFirebaseKey($0).lowercased() })
        func isLocalSource(_ sourceUserId: String) -> Bool {
            let source = sanitizeFirebaseKey(sourceUserId).lowercased()
            return !source.isEmpty && localAliases.contains(source)
        }
        let chainItems = (incoming.chain ?? []).sorted { lhs, rhs in
            let leftDepth = lhs.chainDepth ?? 0
            let rightDepth = rhs.chainDepth ?? 0
            if leftDepth != rightDepth { return leftDepth < rightDepth }
            return (lhs.updatedAt ?? 0) < (rhs.updatedAt ?? 0)
        }
        let sourceItems = chainItems.isEmpty ? [incoming] : chainItems
        let viewerIsOriginalAuthor = sourceItems.first.map { isLocalSource($0.sourceUserId) } ?? false
        for (index, item) in sourceItems.enumerated() {
            let isLatest = index == sourceItems.count - 1
            let canAnswer = viewerIsOriginalAuthor
                ? (isLatest && !isLocalSource(item.sourceUserId))
                : (sourceItems.count == 1 && index == 0 && !isLocalSource(item.sourceUserId))
            let clip = K1L0TransmissionClip(
                videoURL: item.playbackVideoUrl.flatMap { URL(string: $0) },
                imageURL: item.thumbUrl.flatMap { URL(string: $0) },
                audioURL: item.audioUrl.flatMap { $0.isEmpty ? nil : URL(string: $0) },
                responsePlot: item.responsePlot ?? "",
                responseOptions: canAnswer && !viewerIsOriginalAuthor ? (incoming.responseOptions ?? []) : [],
                selectedResponse: "",
                sourceJobId: item.jobId,
                sourceUserId: item.sourceUserId,
                sourceName: item.senderLabel,
                allowsResponse: canAnswer
            )
            if clip.videoURL != nil || clip.imageURL != nil {
                clips.append(clip)
            }
        }
        if chainItems.isEmpty, let slide = incoming.receiverSlide {
            let receiverClip = K1L0TransmissionClip(
                videoURL: slide.playbackVideoUrl.flatMap { URL(string: $0) },
                imageURL: slide.imageUrl.flatMap { URL(string: $0) },
                audioURL: slide.audioUrl.flatMap { $0.isEmpty ? nil : URL(string: $0) },
                responsePlot: "",
                responseOptions: incoming.responseOptions ?? [],
                selectedResponse: "",
                sourceJobId: incoming.jobId,
                sourceUserId: incoming.sourceUserId,
                sourceName: incoming.senderLabel,
                allowsResponse: !isLocalSource(incoming.sourceUserId)
            )
            if receiverClip.videoURL != nil || receiverClip.imageURL != nil {
                clips.append(receiverClip)
            }
        }
        let latestResponseOptions = clips.indices.reversed()
            .first(where: { clips[$0].allowsResponse })
            .map { clips[$0].responseOptions } ?? []
        K1L0TransmissionResultStore.shared.current = K1L0TransmissionResult(
            status: "ready",
            imageURL: incoming.thumbUrl.flatMap { URL(string: $0) },
            videoURL: incoming.playbackVideoUrl.flatMap { URL(string: $0) },
            audioURL: incoming.audioUrl.flatMap { $0.isEmpty ? nil : URL(string: $0) },
            lyrics: "",
            responsePlot: incoming.responsePlot ?? "",
            responseOptions: latestResponseOptions,
            sourceUserId: incoming.sourceUserId,
            jobId: incoming.jobId,
            rootJobId: incoming.rootJobId,
            clips: clips,
            allowsResponseOptions: !viewerIsOriginalAuthor && clips.contains { $0.allowsResponse },
            allowsTextResponse: clips.contains { $0.allowsResponse }
        )
    }

    func receiveStepsRequired() -> Int {
        let stored = UserDefaults.standard.double(forKey: "k1lo_native_receiveStepsRequired")
        return max(1, Int(stored > 0 ? stored : 500))
    }

    func transmissionWaitStepsRequired() -> Int {
        let waitStored = UserDefaults.standard.double(forKey: "k1lo_native_transmissionWaitSteps")
        return max(0, Int(waitStored))
    }

    private func fetchLatestTransmission(clearStaleCache: Bool = false) {
        guard let userId = currentUserIdForInventory(), !userId.isEmpty else { return }
        let todayStartMillis = Calendar.current.startOfDay(for: Date()).timeIntervalSince1970 * 1000
        resolveAPIBase { apiBase in
            let safeUser = userId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? userId
            guard let url = URL(string: "\(apiBase)/api/k1l0/v2/my-transmissions?userId=\(safeUser)&createdSince=\(Int(todayStartMillis))") else { return }
            URLSession.shared.dataTask(with: url) { data, _, _ in
                guard let data,
                      let decoded = try? JSONDecoder().decode(NativeUserTransmissionResponse.self, from: data),
                      decoded.ok
                else { return }
                let originals = decoded.transmissions.filter {
                    $0.isOriginalSentTransmission && $0.createdAtMillis >= todayStartMillis
                }
                guard let newestOriginal = originals.max(by: {
                    $0.createdAtMillis < $1.createdAtMillis
                }) else {
                    if clearStaleCache && !K1L0ActiveTransmissionStore.shared.snapshot.active {
                        DispatchQueue.main.async {
                            K1L0ActiveTransmissionStore.shared.clearCached()
                        }
                    }
                    return
                }
                let newestJobId = newestOriginal.jobId.trimmingCharacters(in: .whitespacesAndNewlines)
                let activeSnapshot = K1L0ActiveTransmissionStore.shared.snapshot
                let activeJobId = activeSnapshot.jobId.trimmingCharacters(in: .whitespacesAndNewlines)
                if activeSnapshot.active && (activeJobId.isEmpty || activeJobId != newestJobId) {
                    return
                }
                if K1L0ActiveTransmissionStore.shared.isCanceled(jobId: newestJobId) {
                    DispatchQueue.main.async {
                        K1L0ActiveTransmissionStore.shared.clearCached()
                    }
                    return
                }
                let transmission = newestOriginal
                let responsePlot = transmission.responsePlot ?? ""
                let imageUrl = transmission.thumbUrl ?? ""
                let videoUrl = transmission.playbackVideoUrl ?? ""
                let audioUrl = transmission.audioUrl ?? ""
                let status = transmission.status ?? (videoUrl.isEmpty ? "building" : "ready")
                DispatchQueue.main.async {
                    K1L0ActiveTransmissionStore.shared.showLatest(
                        message: "",
                        mood: "wired",
                        responsePlot: responsePlot,
                        imageUrl: imageUrl,
                        videoUrl: videoUrl,
                        audioUrl: audioUrl,
                        status: status,
                        jobId: transmission.jobId,
                        responseOptions: transmission.responseOptions ?? [],
                        createdAt: transmission.createdAt ?? transmission.updatedAt
                    )
                }
            }.resume()
        }
    }

    private func startClock() {
        clockTimer?.invalidate()
        now = Date()
        clockTimer = Timer.scheduledTimer(withTimeInterval: 0.35, repeats: true) { [weak self] _ in
            self?.now = Date()
            self?.expireStaleApproachState()
            self?.checkForBeamCollection()
            self?.updateReceiveProgress()
        }
    }

    private func resolveAPIBase(completion: @escaping (String) -> Void) {
        if let activeAPIBase {
            completion(activeAPIBase)
            return
        }
        if isResolvingAPI {
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.25) { [weak self] in
                self?.resolveAPIBase(completion: completion)
            }
            return
        }

        isResolvingAPI = true
        testAPIBase(at: 0) { [weak self] apiBase in
            DispatchQueue.main.async {
                self?.activeAPIBase = apiBase
                self?.isResolvingAPI = false
                self?.apiStatus = "api \(apiBase.replacingOccurrences(of: "https://", with: "").replacingOccurrences(of: "http://", with: ""))"
                completion(apiBase)
            }
        }
    }

    private func testAPIBase(at index: Int, completion: @escaping (String) -> Void) {
        guard index < apiCandidates.count else {
            completion("https://api-tunnel.kilo.gallery")
            return
        }

        let candidate = apiCandidates[index]
        guard let url = URL(string: "\(candidate)/health") else {
            testAPIBase(at: index + 1, completion: completion)
            return
        }

        var request = URLRequest(url: url, timeoutInterval: candidate.contains("192.168") || candidate.contains("fred.local") || candidate.contains("172.20") ? 3 : 8)
        request.httpMethod = "GET"

        URLSession.shared.dataTask(with: request) { [weak self] _, response, error in
            if error == nil, let http = response as? HTTPURLResponse, (200...299).contains(http.statusCode) {
                completion(candidate)
            } else {
                self?.testAPIBase(at: index + 1, completion: completion)
            }
        }.resume()
    }

    private func fetchPlaces(latitude: Double, longitude: Double, apiBase: String) {
        guard let url = URL(string: "\(apiBase)/places") else { return }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: [
            "latitude": latitude,
            "longitude": longitude,
            "radiusMeters": 3500
        ])

        URLSession.shared.dataTask(with: request) { [weak self] data, response, error in
            guard let data else {
                let code = (response as? HTTPURLResponse)?.statusCode ?? 0
                DispatchQueue.main.async { self?.locationStatus = "nearby places unavailable \(code)" }
                if let error { print("[K1L0Overlay] places fetch error: \(error.localizedDescription)") }
                return
            }
            let decoded: OverlayPlacesResponse
            do {
                decoded = try JSONDecoder().decode(OverlayPlacesResponse.self, from: data)
            } catch {
                let snippet = String(data: data.prefix(180), encoding: .utf8) ?? "non-utf8"
                DispatchQueue.main.async { self?.locationStatus = "places decode error" }
                print("[K1L0Overlay] places decode error: \(error) body=\(snippet)")
                return
            }
            DispatchQueue.main.async {
                self?.places = decoded.places.sorted { $0.distance < $1.distance }
                self?.locationStatus = decoded.places.isEmpty ? "no open places nearby" : "\(decoded.places.count) open places nearby"
                self?.checkForBeamCollection()
            }
        }.resume()
    }

    private func fetchBeams(latitude: Double, longitude: Double, apiBase: String) {
        guard let url = URL(string: "\(apiBase)/k1l0/beams/nearby") else { return }
        let userId = currentUserIdForInventory() ?? "anon"
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: [
            "userId": userId,
            "latitude": latitude,
            "longitude": longitude,
            "maxMiles": 1.1,
            "stepMeters": 75,
            "minDistanceMeters": 45,
            "ttlMinutes": beamTtlMinutes()
        ])

        URLSession.shared.dataTask(with: request) { [weak self] data, response, error in
            guard let data else {
                let code = (response as? HTTPURLResponse)?.statusCode ?? 0
                DispatchQueue.main.async { self?.beamStatus = "transmissions unavailable \(code)" }
                if let error { print("[K1L0Overlay] beams fetch error: \(error.localizedDescription)") }
                return
            }
            let decoded: OverlayBeamsResponse
            do {
                decoded = try JSONDecoder().decode(OverlayBeamsResponse.self, from: data)
            } catch {
                let snippet = String(data: data.prefix(180), encoding: .utf8) ?? "non-utf8"
                DispatchQueue.main.async { self?.beamStatus = "transmissions decode error" }
                print("[K1L0Overlay] beams decode error: \(error) body=\(snippet)")
                return
            }
            DispatchQueue.main.async {
                let fetchedIds = Set(decoded.beams.map { $0.id })
                self?.dismissedBeamIds = self?.dismissedBeamIds.filter { fetchedIds.contains($0) } ?? []
                let activeBeams = decoded.beams.filter {
                    self?.isExpired($0) == false && self?.dismissedBeamIds.contains($0.id) == false
                }
                self?.beams = activeBeams
                self?.beamStatus = activeBeams.isEmpty ? "no nearby ambient" : "\(activeBeams.count) nearby"
                self?.updateBeamApproachState()
                self?.checkForBeamCollection()
            }
        }.resume()
    }

    private func fetchNearbyUsers(apiBase: String) {
        guard let url = URL(string: "\(apiBase)/api/k1l0/users/nearby?limit=50") else { return }
        URLSession.shared.dataTask(with: url) { [weak self] data, response, error in
            guard let data else {
                let code = (response as? HTTPURLResponse)?.statusCode ?? 0
                DispatchQueue.main.async { self?.nearbyUsersStatus = "users unavailable \(code)" }
                if let error { print("[K1L0Overlay] users fetch error: \(error.localizedDescription)") }
                return
            }
            do {
                let decoded = try JSONDecoder().decode(OverlayUsersResponse.self, from: data)
                DispatchQueue.main.async {
                    self?.nearbyUsers = decoded.users
                    self?.nearbyUsersStatus = decoded.users.isEmpty ? "no users found" : "\(decoded.users.count) users"
                }
            } catch {
                let snippet = String(data: data.prefix(180), encoding: .utf8) ?? "non-utf8"
                DispatchQueue.main.async { self?.nearbyUsersStatus = "users decode error" }
                print("[K1L0Overlay] users decode error: \(error) body=\(snippet)")
            }
        }.resume()
    }

    private func fetchStepLeaderboard(apiBase: String) {
        guard let url = URL(string: "\(apiBase)/api/k1l0/steps/leaderboard") else { return }
        URLSession.shared.dataTask(with: url) { [weak self] data, response, _ in
            guard let data else {
                let code = (response as? HTTPURLResponse)?.statusCode ?? 0
                DispatchQueue.main.async { self?.stepLeaderboardStatus = "walkers unavailable \(code)" }
                return
            }
            do {
                let decoded = try JSONDecoder().decode(OverlayStepLeaderboardResponse.self, from: data)
                DispatchQueue.main.async {
                    self?.stepLeaders24h = decoded.top24h
                    self?.stepLeaders7d = decoded.top7d
                    self?.stepLeaderboardStatus = decoded.participantCount == 0 ? "no walkers yet" : "\(decoded.participantCount) walkers"
                }
            } catch {
                // Transient garbage (tunnel HTML error page during an API
                // restart) shouldn't wipe a working board — keep the last good
                // data and let the next refresh cycle heal the status.
                let snippet = String(data: data.prefix(160), encoding: .utf8) ?? "non-utf8"
                print("[K1L0Overlay] walkers decode error: \(error) body=\(snippet)")
                DispatchQueue.main.async {
                    let hasData = !(self?.stepLeaders24h.isEmpty ?? true) || !(self?.stepLeaders7d.isEmpty ?? true)
                    self?.stepLeaderboardStatus = hasData ? "walkers reconnecting…" : "walkers decode error"
                }
            }
        }.resume()
    }

    private func fetchInventory() {
        guard let userId = currentUserIdForInventory(), !userId.isEmpty else {
            elementsStatus = "sign in to load elements"
            return
        }
        resolveAPIBase { [weak self] apiBase in
            guard let self else { return }
            let encodedUserId = userId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? userId
            guard let url = URL(string: "\(apiBase)/api/k1l0/user/elements?userId=\(encodedUserId)") else { return }

            URLSession.shared.dataTask(with: url) { [weak self] data, response, error in
                guard let data else {
                    let code = (response as? HTTPURLResponse)?.statusCode ?? 0
                    DispatchQueue.main.async { self?.elementsStatus = "elements unavailable \(code)" }
                    if let error { print("[K1L0Overlay] elements fetch error: \(error.localizedDescription)") }
                    return
                }
                do {
                    let json = try JSONSerialization.jsonObject(with: data)
                    let parsed = Self.parseElements(from: json)
                    let parsedItems = Self.parseInventoryItems(from: json, fallbackElements: parsed)
                    DispatchQueue.main.async {
                        self?.elements = parsed
                        self?.inventoryItems = parsedItems
                        self?.elementsStatus = parsedItems.isEmpty ? "no collected items" : "\(parsedItems.count) collected"
                    }
                } catch {
                    let snippet = String(data: data.prefix(180), encoding: .utf8) ?? "non-utf8"
                    DispatchQueue.main.async { self?.elementsStatus = "elements decode error" }
                    print("[K1L0Overlay] elements decode error: \(error) body=\(snippet)")
                }
            }.resume()
        }
    }

    private func fetchWeather(latitude: Double, longitude: Double) {
        let location = CLLocation(latitude: latitude, longitude: longitude)
        if Date().timeIntervalSince(lastWeatherFetchAt) < 120,
           let lastWeatherFetchLocation,
           location.distance(from: lastWeatherFetchLocation) < 80 {
            return
        }
        lastWeatherFetchAt = Date()
        lastWeatherFetchLocation = location

        let useOpenMeteo = UserDefaults.standard.object(forKey: "k1lo_native_weatherOpenMeteo") as? Bool ?? true
        if useOpenMeteo {
            resolveAPIBase { [weak self] apiBase in
                self?.fetchBackendWeatherAndCity(latitude: latitude, longitude: longitude, apiBase: apiBase)
            }
        } else {
            fetchWttrWeather(latitude: latitude, longitude: longitude)
        }
    }

    private func applyWeather(_ snapshot: WeatherSnapshot) {
        if let city = snapshot.city?.trimmingCharacters(in: .whitespacesAndNewlines), !city.isEmpty {
            cityText = city
        }
        weatherText = snapshot.displayText
        weatherGlyph = snapshot.glyph
        let skyVideoUrl = K1L0SkyVideoURLResolver.url(glyph: snapshot.glyph, isDay: snapshot.isDay)
        if !skyVideoUrl.isEmpty {
            K1L0SkyVideoURLResolver.rememberLiveSkyVideoUrl(skyVideoUrl)
        }
        if !skyVideoUrl.isEmpty && !K1L0SkyVideoURLResolver.testOverrideEnabled {
            K1L0WeatherOverlayInstaller.setUnitySetting("skyVideoUrl", skyVideoUrl)
        }
        K1L0WindowGlowResolver.rememberWeatherIsDay(snapshot.isDay)
    }

    private func fetchBackendWeatherAndCity(latitude: Double, longitude: Double, apiBase: String) {
        guard let url = URL(string: "\(apiBase)/ping") else { return }
        let userId = currentUserIdForInventory() ?? "swift-overlay"
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        var payload: [String: Any] = [
            "userId": userId,
            "platform": "native-ios",
            "manualPing": false,
            "coordinates": [
                "latitude": latitude,
                "longitude": longitude
            ]
        ]
        let renderDebug = K1L0PerfStatsStore.shared.renderDebug
        if !renderDebug.isEmpty {
            payload["renderDebug"] = renderDebug
        }
        request.httpBody = try? JSONSerialization.data(withJSONObject: payload)

        URLSession.shared.dataTask(with: request) { [weak self] data, _, _ in
            guard let data,
                  let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let weather = json["weather"] as? [String: Any]
            else { return }

            let city = json["city"] as? String
            let tempF = weather["temperatureF"] as? Double
            let glyph = (weather["glyph"] as? String) ?? (weather["icon"] as? String)
            let weatherCode = weather["weatherCode"] as? Int
            let isDay = weather["isDay"] as? Bool
            let snapshot = WeatherSnapshot(
                city: city,
                tempF: tempF,
                glyph: Self.weatherGlyph(forWeatherCode: weatherCode, isDay: isDay, fallbackGlyph: glyph, preferBackendGlyph: true),
                isDay: isDay
            )
            DispatchQueue.main.async {
                print("[K1L0Overlay] weather city=\(city ?? "nil") tempF=\(tempF.map { String(format: "%.1f", $0) } ?? "nil") code=\(weatherCode.map(String.init) ?? "nil") isDay=\(isDay.map(String.init) ?? "nil") glyphRaw=\(glyph ?? "nil") glyphApplied=\(snapshot.glyph)")
                self?.applyWeather(snapshot)
            }
        }.resume()
    }

    private func fetchWttrWeather(latitude: Double, longitude: Double) {
        guard let url = URL(string: "https://wttr.in/\(latitude),\(longitude)?format=j1") else { return }
        URLSession.shared.dataTask(with: url) { [weak self] data, _, _ in
            guard let data, let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let current = (json["current_condition"] as? [[String: Any]])?.first
            else { return }
            let temp = current["temp_F"] as? String ?? "--"
            let desc = ((current["weatherDesc"] as? [[String: Any]])?.first?["value"] as? String ?? "").lowercased()
            let isNight = Calendar.current.component(.hour, from: Date()) < 6 || Calendar.current.component(.hour, from: Date()) >= 19
            let snapshot = WeatherSnapshot(
                city: nil,
                tempF: Double(temp),
                glyph: Self.weatherGlyph(forDescription: desc, isDay: !isNight),
                isDay: !isNight
            )
            DispatchQueue.main.async {
                self?.applyWeather(snapshot)
            }
        }.resume()
    }

    private static func weatherGlyph(forBackendGlyph glyph: String?) -> String {
        switch (glyph ?? "").trimmingCharacters(in: .whitespacesAndNewlines).lowercased() {
        case "sun", "sunny", "clear":
            return "sun.max.fill"
        case "moon", "night", "clear-night":
            return "moon.stars.fill"
        case "cloud", "cloudy", "overcast":
            return "cloud.fill"
        case "partly cloudy", "partlycloudy":
            return "cloud.sun.fill"
        case "rain", "rainy", "drizzle":
            return "cloud.rain.fill"
        case "snow", "snowy":
            return "cloud.snow.fill"
        case "storm", "thunder", "thunderstorm":
            return "cloud.bolt.rain.fill"
        case "fog", "foggy", "mist":
            return "cloud.fog.fill"
        default:
            return "cloud.fill"
        }
    }

    private static func weatherGlyph(forWeatherCode code: Int?, isDay: Bool?, fallbackGlyph: String?, preferBackendGlyph: Bool = false) -> String {
        if preferBackendGlyph, let fallbackGlyph, !fallbackGlyph.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return weatherGlyph(forBackendGlyph: fallbackGlyph)
        }
        guard let code else { return weatherGlyph(forBackendGlyph: fallbackGlyph) }
        let day = isDay ?? true
        switch code {
        case 0:
            return day ? "sun.max.fill" : "moon.stars.fill"
        case 1, 2:
            return day ? "cloud.sun.fill" : "cloud.moon.fill"
        case 3:
            return "cloud.fill"
        case 45, 48:
            return "cloud.fog.fill"
        case 51...67, 80...82:
            return "cloud.rain.fill"
        case 71...77, 85...86:
            return "cloud.snow.fill"
        case 95...99:
            return "cloud.bolt.rain.fill"
        default:
            return weatherGlyph(forBackendGlyph: fallbackGlyph)
        }
    }

    private static func weatherGlyph(forDescription desc: String, isDay: Bool) -> String {
        if desc.contains("thunder") || desc.contains("storm") { return "cloud.bolt.rain.fill" }
        if desc.contains("snow") || desc.contains("sleet") { return "cloud.snow.fill" }
        if desc.contains("rain") || desc.contains("drizzle") || desc.contains("shower") { return "cloud.rain.fill" }
        if desc.contains("fog") || desc.contains("mist") || desc.contains("haze") { return "cloud.fog.fill" }
        if desc.contains("partly") { return isDay ? "cloud.sun.fill" : "cloud.moon.fill" }
        if desc.contains("cloud") || desc.contains("overcast") { return "cloud.fill" }
        return isDay ? "sun.max.fill" : "moon.stars.fill"
    }

    private func distanceMeters(to place: OverlayPlace) -> Double {
        guard let currentLocation else { return place.distance }
        return currentLocation.distance(from: CLLocation(latitude: place.coordinates.lat, longitude: place.coordinates.lng))
    }

    private func distanceMeters(to beam: OverlayBeam) -> Double {
        guard let currentLocation else { return beam.distanceMeters }
        return currentLocation.distance(from: CLLocation(latitude: beam.lat, longitude: beam.lng))
    }

    private func updateBeamApproachState() {
        let currentIds = Set(beams.map { $0.id })
        lastBeamDistances = lastBeamDistances.filter { currentIds.contains($0.key) }
        walkingTowardUntil = walkingTowardUntil.filter { currentIds.contains($0.key) }
        walkingAwayStartSteps = walkingAwayStartSteps.filter { currentIds.contains($0.key) }

        let dismissSteps = beamDismissStepsRequired()
        var beamIdsToDismiss = Set<String>()

        for beam in beams where !isExpired(beam) {
            let distance = distanceMeters(to: beam)
            if let previous = lastBeamDistances[beam.id] {
                if previous - distance > 1.2 {
                    walkingTowardUntil[beam.id] = now.addingTimeInterval(8)
                    walkingAwayStartSteps.removeValue(forKey: beam.id)
                } else if distance - previous > 1.2, dismissSteps > 0 {
                    let startSteps = walkingAwayStartSteps[beam.id] ?? liveSteps
                    walkingAwayStartSteps[beam.id] = startSteps
                    if liveSteps - startSteps >= dismissSteps {
                        beamIdsToDismiss.insert(beam.id)
                    }
                }
            }
            lastBeamDistances[beam.id] = distance
        }

        dismissBeams(ids: beamIdsToDismiss)
        updateWorldItemDistanceTrends()
    }

    private func updateWorldItemDistanceTrends() {
        var currentDistances: [String: Double] = [:]
        for beam in beams where !isExpired(beam) {
            currentDistances["beam:\(beam.id)"] = distanceMeters(to: beam)
        }
        for place in places {
            currentDistances["place:\(place.placeId ?? place.id)"] = distanceMeters(to: place)
        }

        worldItemDistanceTrend = worldItemDistanceTrend.filter { currentDistances[$0.key] != nil }
        lastWorldItemDistances = lastWorldItemDistances.filter { currentDistances[$0.key] != nil }
        for (itemId, distance) in currentDistances {
            if let previous = lastWorldItemDistances[itemId] {
                if previous - distance > 1.2 {
                    worldItemDistanceTrend[itemId] = "toward"
                } else if distance - previous > 1.2 {
                    worldItemDistanceTrend[itemId] = "away"
                }
            }
            lastWorldItemDistances[itemId] = distance
        }
    }

    private func dismissBeams(ids: Set<String>) {
        guard !ids.isEmpty else { return }
        dismissedBeamIds.formUnion(ids)
        beams.removeAll { ids.contains($0.id) }
        if let candidate = collectCandidateBeam, ids.contains(candidate.id) {
            collectCandidateBeam = nil
        }
        for id in ids {
            lastBeamDistances.removeValue(forKey: id)
            walkingTowardUntil.removeValue(forKey: id)
            walkingAwayStartSteps.removeValue(forKey: id)
        }
        beamStatus = beams.isEmpty ? "scanning ambient…" : "\(beams.count) nearby"
    }

    private func checkForBeamCollection() {
        guard incomingTransmission == nil else { return }
        guard currentLocation != nil else { return }
        let radius = collectRadiusMeters()

        if let candidate = collectCandidatePlace {
            let stillAvailable = places.contains(where: { $0.id == candidate.id })
                && candidate.hasCollectibleArtifact
                && !collectedPlaceIds.contains(candidate.id)
                && distanceMeters(to: candidate) <= locationCollectRadiusMeters()
            if !stillAvailable {
                collectCandidatePlace = nil
            }
        }

        if let candidate = collectCandidateBeam {
            let stillAvailable = beams.contains(where: { $0.id == candidate.id })
                && !isExpired(candidate)
                && liveSteps > 0
                && distanceMeters(to: candidate) <= radius
            if stillAvailable { return }
            collectCandidateBeam = nil
        }

        guard liveSteps > 0 else {
            checkForLocationCollection()
            return
        }

        if let beam = beams
            .filter({ !isExpired($0) && !collectingBeamIds.contains($0.id) })
            .sorted(by: { distanceMeters(to: $0) < distanceMeters(to: $1) })
            .first,
            distanceMeters(to: beam) <= radius {
            collectCandidateBeam = beam
            K1L0WeatherOverlayInstaller.playBeamCollectSound()
            return
        }

        checkForLocationCollection()
    }

    private func checkForLocationCollection() {
        guard incomingTransmission == nil else { return }
        guard currentLocation != nil else { return }
        guard collectCandidateBeam == nil else { return }
        if collectCandidatePlace != nil { return }
        let radius = locationCollectRadiusMeters()
        guard let place = places
            .filter({
                $0.hasCollectibleArtifact
                    && !collectingPlaceIds.contains($0.id)
                    && !collectedPlaceIds.contains($0.id)
                    && distanceMeters(to: $0) <= radius
            })
            .sorted(by: { distanceMeters(to: $0) < distanceMeters(to: $1) })
            .first
        else { return }

        collectCandidatePlace = place
        K1L0WeatherOverlayInstaller.playBeamCollectSound()
    }

    func confirmCollectBeam(_ beam: OverlayBeam) {
        collectBeam(beam)
    }

    func dismissCollectPrompt() {
        collectCandidateBeam = nil
    }

    func confirmCollectPlace(_ place: OverlayPlace) {
        collectPlace(place)
    }

    func dismissLocationCollectPrompt() {
        if let place = collectCandidatePlace {
            collectedPlaceIds.insert(place.id)
        }
        collectCandidatePlace = nil
    }

    private func collectBeam(_ beam: OverlayBeam) {
        collectingBeamIds.insert(beam.id)
        collectCandidateBeam = nil
        beams.removeAll { $0.id == beam.id }
        lastBeamDistances.removeValue(forKey: beam.id)
        walkingTowardUntil.removeValue(forKey: beam.id)
        walkingAwayStartSteps.removeValue(forKey: beam.id)
        beamStatus = beams.isEmpty ? "scanning ambient…" : "\(beams.count) nearby"
        K1L0WeatherOverlayInstaller.playBeamCollectSound()

        resolveAPIBase { [weak self] apiBase in
            self?.postBeamVisit(beam.id, apiBase: apiBase, userId: self?.currentUserIdForInventory())
        }
    }

    private func postBeamVisit(_ beamId: String, apiBase: String, userId: String?) {
        guard let url = URL(string: "\(apiBase)/k1l0/beams/visit") else { return }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        var payload: [String: Any] = ["beamId": beamId]
        if let userId, !userId.isEmpty {
            payload["userId"] = userId
        }
        request.httpBody = try? JSONSerialization.data(withJSONObject: payload)
        URLSession.shared.dataTask(with: request) { [weak self] _, response, error in
            if let error {
                print("[K1L0Overlay] beam visit error: \(error.localizedDescription)")
            } else if let http = response as? HTTPURLResponse, !(200...299).contains(http.statusCode) {
                print("[K1L0Overlay] beam visit failed status=\(http.statusCode)")
            }
            DispatchQueue.main.async {
                self?.collectingBeamIds.remove(beamId)
                self?.fetchInventory()
            }
        }.resume()
    }

    private func collectPlace(_ place: OverlayPlace) {
        collectingPlaceIds.insert(place.id)
        collectedPlaceIds.insert(place.id)
        collectCandidatePlace = nil
        K1L0WeatherOverlayInstaller.playBeamCollectSound()

        resolveAPIBase { [weak self] apiBase in
            self?.postPlaceVisit(place, apiBase: apiBase, userId: self?.currentUserIdForInventory())
        }
    }

    private func postPlaceVisit(_ place: OverlayPlace, apiBase: String, userId: String?) {
        guard let url = URL(string: "\(apiBase)/api/k1l0/places/visit") else { return }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        var payload: [String: Any] = [
            "placeId": place.id,
            "placeName": place.name,
            "artifactMaterial": place.artifactMaterial ?? "",
            "artifactLabel": place.artifactLabel ?? "",
            "artifactTeaser": place.artifactTeaser ?? place.teaser ?? ""
        ]
        if let userId, !userId.isEmpty {
            payload["userId"] = userId
        }
        request.httpBody = try? JSONSerialization.data(withJSONObject: payload)
        URLSession.shared.dataTask(with: request) { [weak self] _, response, error in
            if let error {
                print("[K1L0Overlay] place visit error: \(error.localizedDescription)")
            } else if let http = response as? HTTPURLResponse, !(200...299).contains(http.statusCode) {
                print("[K1L0Overlay] place visit failed status=\(http.statusCode)")
            }
            DispatchQueue.main.async {
                self?.collectingPlaceIds.remove(place.id)
                self?.fetchInventory()
            }
        }.resume()
    }

    func submitNativeTransmission(photoPath: String, message: String, mood: String, status: @escaping (String) -> Void) {
        let userId = currentUserIdForInventory() ?? "anon"
        resolveAPIBase { [weak self] apiBase in
            guard let self else { return }
            DispatchQueue.main.async { status("uploading photo...") }
            self.uploadTransmissionPhoto(photoPath: photoPath, apiBase: apiBase, userId: userId, status: status) { result in
                switch result {
                case .failure(let error):
                    DispatchQueue.main.async {
                        K1L0ActiveTransmissionStore.shared.apply(K1L0TransmissionResult(status: "error", imageURL: nil, videoURL: nil, audioURL: nil, lyrics: "", responsePlot: error.localizedDescription, responseOptions: []))
                        status("photo upload failed: \(error.localizedDescription)")
                    }
                case .success(let imageUrl):
                    DispatchQueue.main.async { status("creating transmission...") }
                    self.createTransmissionJob(apiBase: apiBase, userId: userId, imageUrl: imageUrl, message: message, mood: mood, status: status)
                }
            }
        }
    }

    private func uploadTransmissionPhoto(photoPath: String, apiBase: String, userId: String, status: @escaping (String) -> Void, completion: @escaping (Result<String, Error>) -> Void) {
        guard let url = URL(string: "\(apiBase)/api/k1l0/upload-image") ?? URL(string: "\(apiBase)/k1l0/upload-image") else {
            completion(.failure(NSError(domain: "K1L0", code: 0, userInfo: [NSLocalizedDescriptionKey: "invalid upload URL"])))
            return
        }
        DispatchQueue.global(qos: .userInitiated).async {
            do {
                let fileUrl = URL(fileURLWithPath: photoPath)
                let originalData = try Data(contentsOf: fileUrl)
                let data = self.normalizedUploadImageData(originalData, sourceUrl: fileUrl)
                let contentType = "image/jpeg"
                let uploadBytes = data.count
                let uploadKB = max(1, Int((Double(uploadBytes) / 1024.0).rounded()))
                print("[K1L0Overlay] upload image url=\(url.absoluteString) source=\(fileUrl.lastPathComponent) originalBytes=\(originalData.count) uploadBytes=\(uploadBytes)")
                DispatchQueue.main.async { status("uploading photo \(uploadKB)KB...") }
                var request = URLRequest(url: url, timeoutInterval: 45)
                request.httpMethod = "POST"
                request.setValue("application/json", forHTTPHeaderField: "Content-Type")
                request.httpBody = try JSONSerialization.data(withJSONObject: [
                    "userId": userId,
                    "filename": fileUrl.deletingPathExtension().lastPathComponent + ".jpg",
                    "contentType": contentType,
                    "imageBase64": data.base64EncodedString()
                ])
                URLSession.shared.dataTask(with: request) { data, response, error in
                    if let error {
                        print("[K1L0Overlay] upload image transport error=\(error.localizedDescription)")
                        completion(.failure(error))
                        return
                    }
                    let code = (response as? HTTPURLResponse)?.statusCode ?? 0
                    let body = data.flatMap { String(data: $0.prefix(240), encoding: .utf8) } ?? ""
                    print("[K1L0Overlay] upload image response status=\(code) body=\(body)")
                    guard let data,
                          let http = response as? HTTPURLResponse,
                          (200...299).contains(http.statusCode),
                          let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                          let ok = root["ok"] as? Bool,
                          ok,
                          let imageUrl = root["url"] as? String,
                          !imageUrl.isEmpty
                    else {
                        completion(.failure(NSError(domain: "K1L0", code: code, userInfo: [NSLocalizedDescriptionKey: "upload failed \(code) \(body)"])))
                        return
                    }
                    completion(.success(imageUrl))
                }.resume()
            } catch {
                completion(.failure(error))
            }
        }
    }

    private func normalizedUploadImageData(_ data: Data, sourceUrl: URL) -> Data {
#if canImport(UIKit)
        guard let image = UIImage(data: data) else { return data }
        let maxEdge: CGFloat = 1440
        let largestEdge = max(image.size.width, image.size.height)
        let scale = largestEdge > maxEdge ? maxEdge / largestEdge : 1
        let targetSize = CGSize(width: image.size.width * scale, height: image.size.height * scale)
        let renderer = UIGraphicsImageRenderer(size: targetSize)
        let rendered = renderer.image { _ in
            image.draw(in: CGRect(origin: .zero, size: targetSize))
        }
        return rendered.jpegData(compressionQuality: 0.74) ?? data
#elseif canImport(AppKit)
        guard let image = NSImage(data: data) else { return data }
        let maxEdge: CGFloat = 1440
        let largestEdge = max(image.size.width, image.size.height)
        let scale = largestEdge > maxEdge ? maxEdge / largestEdge : 1
        let targetSize = NSSize(width: image.size.width * scale, height: image.size.height * scale)
        let resized = NSImage(size: targetSize)
        resized.lockFocus()
        image.draw(in: NSRect(origin: .zero, size: targetSize), from: .zero, operation: .copy, fraction: 1)
        resized.unlockFocus()
        guard let tiff = resized.tiffRepresentation,
              let rep = NSBitmapImageRep(data: tiff),
              let jpeg = rep.representation(using: .jpeg, properties: [.compressionFactor: 0.74])
        else { return data }
        return jpeg
#else
        return data
#endif
    }

    private func createTransmissionJob(apiBase: String, userId: String, imageUrl: String, message: String, mood: String, status: @escaping (String) -> Void) {
        guard let url = URL(string: "\(apiBase)/api/k1l0/v2/transmit") ?? URL(string: "\(apiBase)/k1l0/v2/transmit") else {
            status("invalid transmit URL")
            return
        }
        let atLocationMaxMeters = 50.0 * 0.3048
        let atPlace = places
            .map { place in (place: place, meters: distanceMeters(to: place)) }
            .filter { $0.meters <= atLocationMaxMeters }
            .sorted { $0.meters < $1.meters }
            .first
        var locationPayload: [String: Any] = [
            "city": cityText.isEmpty ? "Cranberry Township, PA" : cityText,
            "name": atPlace?.place.name ?? "",
        ]
        if let atPlace {
            locationPayload["locationName"] = atPlace.place.name
            locationPayload["distance"] = atPlace.meters
        }
        if let currentLocation {
            locationPayload["lat"] = currentLocation.coordinate.latitude
            locationPayload["lng"] = currentLocation.coordinate.longitude
        }
        var weatherPayload: [String: Any] = [
            "city": cityText.isEmpty ? "Cranberry Township, PA" : cityText,
            "summary": weatherGlyph,
            "displayText": weatherText,
            "glyph": weatherGlyph
        ]
        let numericTemp = weatherText
            .replacingOccurrences(of: "°", with: "")
            .trimmingCharacters(in: .whitespacesAndNewlines)
        if let tempF = Double(numericTemp) {
            weatherPayload["temperatureF"] = tempF
            weatherPayload["tempF"] = tempF
        }
        var request = URLRequest(url: url, timeoutInterval: 45)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: [
            "userId": userId,
            "image": imageUrl,
            "message": message,
            "mood": mood,
            "activity": [
                "type": "walk",
                "timestamp": ISO8601DateFormatter().string(from: Date()),
                "location": locationPayload,
                "weather": weatherPayload,
                "stats": ["steps": liveSteps],
                "mood_prompt": message
            ]
        ])
        URLSession.shared.dataTask(with: request) { [weak self] data, response, error in
            if let error {
                DispatchQueue.main.async {
                    K1L0ActiveTransmissionStore.shared.apply(K1L0TransmissionResult(status: "error", imageURL: nil, videoURL: nil, audioURL: nil, lyrics: "", responsePlot: error.localizedDescription, responseOptions: []))
                    status("transmit failed: \(error.localizedDescription)")
                }
                return
            }
            guard let data,
                  let http = response as? HTTPURLResponse,
                  (200...299).contains(http.statusCode),
                  let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let ok = root["ok"] as? Bool,
                  ok,
                  let jobId = root["jobId"] as? String
            else {
                let code = (response as? HTTPURLResponse)?.statusCode ?? 0
                let body = data.flatMap { String(data: $0.prefix(160), encoding: .utf8) } ?? ""
                DispatchQueue.main.async {
                    K1L0ActiveTransmissionStore.shared.apply(K1L0TransmissionResult(status: "error", imageURL: nil, videoURL: nil, audioURL: nil, lyrics: "", responsePlot: "transmit failed \(code) \(body)", responseOptions: []))
                    status("transmit failed \(code)")
                }
                return
            }
            DispatchQueue.main.async {
                K1L0ActiveTransmissionStore.shared.setJobId(jobId)
                status("queued \(jobId)")
            }
            self?.streamTransmissionJob(apiBase: apiBase, userId: userId, jobId: jobId, status: status)
        }.resume()
    }

    private func applyTransmissionResult(root: [String: Any], jobId: String, status: @escaping (String) -> Void) {
        let jobStatus = (root["status"] as? String) ?? ""
        if jobStatus == "ready" || jobStatus == "complete" {
            let audit = Self.transmissionAudit(from: root)
            let finalUrl = (root["finalUrl"] as? String) ?? ""
            let rawVideoUrl = (root["rawVideoUrl"] as? String) ?? (root["videoUrl"] as? String) ?? finalUrl
            let imageUrl = (root["stillUrl"] as? String) ?? (root["nbUrl"] as? String) ?? ""
            let musicVariants = root["musicVariants"] as? [[String: Any]]
            let audioUrl = (root["audioUrl"] as? String) ?? (musicVariants?.first?["url"] as? String) ?? ""
            let responsePlot = (root["responsePlot"] as? String) ?? ""
            let responseOptions = (root["responseOptions"] as? [String]) ?? []
            let createdAt = k1l0NumericTimestamp(root["createdAt"]) > 0 ? k1l0NumericTimestamp(root["createdAt"]) : k1l0NumericTimestamp(root["updatedAt"])
            let payload: [String: Any] = [
                "jobId": jobId, "status": jobStatus, "imageUrl": imageUrl,
                "videoUrl": rawVideoUrl, "audioUrl": audioUrl, "lyrics": audit.lyrics,
                "createdAt": createdAt, "responsePlot": responsePlot, "responseOptions": responseOptions
            ]
            if let payloadData = try? JSONSerialization.data(withJSONObject: payload),
               let json = String(data: payloadData, encoding: .utf8) {
                DispatchQueue.main.async {
                    K1L0ActiveTransmissionStore.shared.applyAudit(
                        inputImageUrl: audit.inputImageUrl, locationSummary: audit.location,
                        weatherSummary: audit.weather, photoPrompt: audit.photoPrompt,
                        videoPrompt: audit.videoPrompt, musicPrompt: audit.musicPrompt,
                        lyrics: audit.lyrics, createdAt: createdAt
                    )
                    K1L0TransmissionResultStore.shared.handle(json)
                    status("transmission ready")
                }
            }
        } else if jobStatus == "error" {
            let error = (root["error"] as? String) ?? "transmission failed"
            DispatchQueue.main.async {
                K1L0ActiveTransmissionStore.shared.apply(K1L0TransmissionResult(status: "error", imageURL: nil, videoURL: nil, audioURL: nil, lyrics: "", responsePlot: error, responseOptions: []))
                status(error)
            }
        }
    }

    private func streamTransmissionJob(apiBase: String, userId: String, jobId: String, status: @escaping (String) -> Void) {
        guard let url = URL(string: "\(apiBase)/api/k1l0/v2/transmit/\(jobId)/events?userId=\(userId)") else {
            DispatchQueue.main.async { status("stream url error") }
            return
        }
        var request = URLRequest(url: url)
        request.timeoutInterval = 720
        Task { [weak self] in
            do {
                let (bytes, response) = try await URLSession.shared.bytes(for: request)
                guard (response as? HTTPURLResponse)?.statusCode == 200 else {
                    DispatchQueue.main.async { status("stream error \((response as? HTTPURLResponse)?.statusCode ?? 0)") }
                    self?.pollTransmissionJob(apiBase: apiBase, userId: userId, jobId: jobId, status: status)
                    return
                }
                for try await line in bytes.lines {
                    guard line.hasPrefix("data: ") else { continue }
                    let jsonStr = String(line.dropFirst(6))
                    guard let data = jsonStr.data(using: .utf8),
                          let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
                    else { continue }
                    let jobStatus = (root["status"] as? String) ?? ""
                    if !jobStatus.isEmpty && jobStatus != "ready" && jobStatus != "complete" {
                        DispatchQueue.main.async { status(jobStatus) }
                    }
                    if jobStatus == "ready" || jobStatus == "complete" || jobStatus == "error" || jobStatus == "ended" {
                        self?.applyTransmissionResult(root: root, jobId: jobId, status: status)
                        return
                    }
                }
            } catch {
                DispatchQueue.main.async { status("stream closed: \(error.localizedDescription)") }
                self?.pollTransmissionJob(apiBase: apiBase, userId: userId, jobId: jobId, status: status)
            }
        }
    }

    private func pollTransmissionJob(apiBase: String, userId: String, jobId: String, status: @escaping (String) -> Void, attempt: Int = 0) {
        guard attempt < 90 else {
            DispatchQueue.main.async {
                K1L0ActiveTransmissionStore.shared.apply(K1L0TransmissionResult(status: "error", imageURL: nil, videoURL: nil, audioURL: nil, lyrics: "", responsePlot: "transmission timed out", responseOptions: []))
                status("transmission timed out")
            }
            return
        }
        guard let url = URL(string: "\(apiBase)/api/k1l0/v2/transmit/\(jobId)?userId=\(userId)") ?? URL(string: "\(apiBase)/k1l0/v2/transmit/\(jobId)?userId=\(userId)") else { return }
        URLSession.shared.dataTask(with: url) { [weak self] data, _, _ in
            guard let data,
                  let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
            else {
                DispatchQueue.main.asyncAfter(deadline: .now() + 5) {
                    self?.pollTransmissionJob(apiBase: apiBase, userId: userId, jobId: jobId, status: status, attempt: attempt + 1)
                }
                return
            }
            let jobStatus = (root["status"] as? String) ?? ""
            if jobStatus == "ready" || jobStatus == "complete" || jobStatus == "error" {
                self?.applyTransmissionResult(root: root, jobId: jobId, status: status)
                return
            }
            DispatchQueue.main.async { status(jobStatus.isEmpty ? "building transmission..." : jobStatus) }
            DispatchQueue.main.asyncAfter(deadline: .now() + 5) {
                self?.pollTransmissionJob(apiBase: apiBase, userId: userId, jobId: jobId, status: status, attempt: attempt + 1)
            }
        }.resume()
    }

    static func transmissionAudit(from root: [String: Any]) -> (inputImageUrl: String, location: String, weather: String, photoPrompt: String, videoPrompt: String, musicPrompt: String, lyrics: String) {
        let plan = root["plan"] as? [String: Any] ?? [:]
        let music = plan["music"] as? [String: Any] ?? [:]
        let musicVariants = root["musicVariants"] as? [[String: Any]] ?? []
        let hud = plan["hud"] as? [String: Any] ?? [:]
        let header = hud["header"] as? [String: Any] ?? [:]
        let activity = root["activity"] as? [String: Any] ?? [:]
        let activityLocation = activity["location"] as? [String: Any] ?? [:]
        let activityWeather = activity["weather"] as? [String: Any] ?? [:]

        let inputImageUrl = [
            root["inputImageUrl"],
            root["sceneImageUrl"],
            root["stillUrl"],
            root["nbUrl"]
        ].compactMap { $0 as? String }
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .first { !$0.isEmpty } ?? ""

        let location = [
            header["location"],
            activityLocation["city"],
            activityLocation["locationName"],
            activityLocation["name"]
        ].compactMap { $0 as? String }
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .first { !$0.isEmpty } ?? ""

        let weather = [
            header["weather"],
            activityWeather["displayText"],
            activityWeather["summary"],
            activityWeather["glyph"]
        ].compactMap { $0 as? String }
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .first { !$0.isEmpty } ?? ""

        return (
            inputImageUrl: inputImageUrl,
            location: location,
            weather: weather,
            photoPrompt: ((plan["nb_prompt"] as? String) ?? "").trimmingCharacters(in: .whitespacesAndNewlines),
            videoPrompt: ((plan["wan_prompt"] as? String) ?? "").trimmingCharacters(in: .whitespacesAndNewlines),
            musicPrompt: ((music["prompt"] as? String) ?? "").trimmingCharacters(in: .whitespacesAndNewlines),
            lyrics: [
                root["lyrics"],
                root["songLyrics"],
                music["lyrics"],
                music["songLyrics"],
                musicVariants.first?["lyrics"],
                musicVariants.first?["songLyrics"]
            ].compactMap { $0 as? String }
                .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
                .first { !$0.isEmpty } ?? ""
        )
    }

    private func collectRadiusMeters() -> Double {
        let stored = UserDefaults.standard.double(forKey: "k1lo_native_ambientCollectRadiusMeters")
        return stored > 0 ? min(100, max(1, stored)) : 10
    }

    private func locationCollectRadiusMeters() -> Double {
        let storedFeet = UserDefaults.standard.double(forKey: "k1lo_native_locationCollectRadiusFeet")
        let feet = storedFeet > 0 ? min(300, max(10, storedFeet)) : 50
        return feet * 0.3048
    }

    private func beamDismissStepsRequired() -> Int {
        let stored = UserDefaults.standard.double(forKey: "k1lo_native_ambientBeamDismissSteps")
        return max(0, Int(stored))
    }

    private func expireStaleApproachState() {
        walkingTowardUntil = walkingTowardUntil.filter { $0.value > now }
    }

    private func isExpired(_ beam: OverlayBeam) -> Bool {
        guard let expiresAt = beam.expiresAt else { return false }
        return expiresAt <= now.timeIntervalSince1970 * 1000
    }

    private func placeCategory(_ place: OverlayPlace) -> String {
        let types = ([place.type] + (place.types ?? []))
            .map { $0.lowercased().replacingOccurrences(of: " ", with: "_") }
        let typeSet = Set(types)
        let words = Set(place.name.lowercased().components(separatedBy: CharacterSet.alphanumerics.inverted).filter { !$0.isEmpty })

        let coffeeTypes: Set<String> = ["cafe", "coffee_shop", "bakery", "donut_shop"]
        let foodTypes: Set<String> = [
            "restaurant", "food", "meal_delivery", "meal_takeaway", "fast_food_restaurant",
            "pizza_restaurant", "burger_restaurant", "chinese_restaurant", "mexican_restaurant",
            "italian_restaurant", "japanese_restaurant", "thai_restaurant", "indian_restaurant",
            "sushi_restaurant", "american_restaurant", "diner", "breakfast_restaurant",
            "brunch_restaurant", "steakhouse", "sandwich_shop", "seafood_restaurant", "deli"
        ]
        let drinkTypes: Set<String> = [
            "bar", "bar_and_grill", "pub", "wine_bar", "night_club", "brewery",
            "beer_bar", "cocktail_bar", "dive_bar", "sports_bar", "gastropub",
            "irish_pub", "tiki_bar", "sake_bar", "speakeasy", "lounge", "liquor_store"
        ]
        let snackTypes: Set<String> = [
            "convenience_store", "gas_station", "grocery_store", "market",
            "ice_cream_shop", "dessert_shop", "candy_store"
        ]

        if !typeSet.isDisjoint(with: coffeeTypes) { return "coffee" }
        if !typeSet.isDisjoint(with: drinkTypes) { return "drink" }
        if !typeSet.isDisjoint(with: snackTypes) { return "snack" }
        if !typeSet.isDisjoint(with: foodTypes) { return "food" }

        if !words.isDisjoint(with: ["coffee", "cafe", "bakery", "donut"]) { return "coffee" }
        if !words.isDisjoint(with: ["bar", "brewery", "brewpub", "taproom", "pub", "beer", "wine", "cocktail"]) { return "drink" }
        if !words.isDisjoint(with: ["restaurant", "pizza", "thai", "wing", "wings", "sandwich", "primanti"]) { return "food" }
        if !words.isDisjoint(with: ["convenience", "bodega", "market", "mart", "store", "shop", "gas", "fuel", "candy"]) { return "snack" }
        return "food"
    }

    private func normalizedLocationFilter(_ filter: String) -> String {
        switch filter.lowercased().trimmingCharacters(in: .whitespacesAndNewlines) {
        case "drink", "drinks", "bar":
            return "drink"
        case "coffee", "cafe":
            return "coffee"
        case "food", "restaurant":
            return "food"
        case "snack", "snacks", "convenience":
            return "snack"
        default:
            return "all"
        }
    }

    private func beamTtlMinutes() -> Double {
        let storedMinutes = UserDefaults.standard.double(forKey: "k1lo_native_ambientBeamTtlMinutes")
        if storedMinutes > 0 {
            return min(240, max(1, storedMinutes))
        }
        let legacyHours = UserDefaults.standard.double(forKey: "k1lo_native_ambientBeamTtlHours")
        if legacyHours > 0 {
            return min(240, max(1, legacyHours * 60))
        }
        return 20
    }


    private func currentUserIdForInventory() -> String? {
        let defaults = UserDefaults.standard
        for key in ["FirebaseUserId", "K1L0UserId", "DeviceID", "deviceID"] {
            let value = defaults.string(forKey: key) ?? ""
            if !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                return value
            }
        }
        return nil
    }

    private func sanitizeFirebaseKey(_ raw: String) -> String {
        raw.replacingOccurrences(of: ".", with: "_")
            .replacingOccurrences(of: "#", with: "_")
            .replacingOccurrences(of: "$", with: "_")
            .replacingOccurrences(of: "[", with: "_")
            .replacingOccurrences(of: "]", with: "_")
            .replacingOccurrences(of: "/", with: "_")
    }

    private static func parseElements(from json: Any) -> [OverlayElement] {
        guard let root = json as? [String: Any] else { return [] }
        if let elementsArray = root["elements"] as? [[String: Any]] {
            let parsed = parseElementArray(elementsArray)
            if !parsed.isEmpty { return parsed }
        }
        if let elementsRoot = root["elements"] as? [String: Any] {
            let aggregate = parseElementCollection(elementsRoot)
            if !aggregate.isEmpty { return aggregate }
        }
        if let itemsRoot = root["items"] as? [String: Any] {
            return parseElementCollection(itemsRoot)
        }
        return parseElementCollection(root)
    }

    private static func parseInventoryItems(from json: Any, fallbackElements: [OverlayElement]) -> [OverlayInventoryItem] {
        guard let root = json as? [String: Any] else {
            return fallbackElements.map { OverlayInventoryItem(element: $0) }
        }
        if let values = root["items"] as? [[String: Any]] {
            let parsed = values.compactMap(parseInventoryItem)
            if !parsed.isEmpty { return parsed }
        }
        return fallbackElements.map { OverlayInventoryItem(element: $0) }
    }

    private static func parseInventoryItem(_ item: [String: Any]) -> OverlayInventoryItem? {
        let rawKind = firstString(item, keys: ["kind", "type"]).lowercased()
        let isObject = rawKind == "object" || !firstString(item, keys: ["objectName"]).isEmpty
        let senderName = firstString(item, keys: ["senderName", "sourceName"])
        let sourceJobId = firstString(item, keys: ["sourceTransmissionJobId", "jobId"])
        let createdAtMs = firstInt(item, keys: ["createdAt"])
        let collectedAt: Date? = createdAtMs > 0 ? Date(timeIntervalSince1970: Double(createdAtMs) / 1000.0) : nil
        if isObject {
            let name = firstString(item, keys: ["objectName", "name", "artifact", "sourceLabel", "label"])
            guard !name.isEmpty else { return nil }
            return OverlayInventoryItem(
                id: firstString(item, keys: ["id"]).isEmpty ? "object:\(name.lowercased())" : firstString(item, keys: ["id"]),
                kind: "object",
                name: name,
                symbol: objectSymbol(for: name),
                grams: 0,
                count: max(1, firstInt(item, keys: ["count"])),
                avatarUrl: firstString(item, keys: ["avatarUrl", "imageUrl", "iconUrl"]),
                senderName: senderName,
                sourceTransmissionJobId: sourceJobId,
                collectedAt: collectedAt
            )
        }
        let rawName = firstString(item, keys: ["material", "element", "name", "artifact"])
        let name = canonicalElementName(rawName)
        guard !name.isEmpty else { return nil }
        return OverlayInventoryItem(
            id: firstString(item, keys: ["id"]).isEmpty ? "element:\(name.lowercased())" : firstString(item, keys: ["id"]),
            kind: "element",
            name: name,
            symbol: ElementSymbolLookup.symbol(for: name),
            grams: firstInt(item, keys: ["grams", "quantityGrams", "quantity"]),
            count: max(1, firstInt(item, keys: ["count"])),
            avatarUrl: "",
            senderName: senderName,
            sourceTransmissionJobId: sourceJobId,
            collectedAt: collectedAt
        )
    }

    private static func objectSymbol(for name: String) -> String {
        let words = name
            .split(whereSeparator: { !$0.isLetter && !$0.isNumber })
            .map(String.init)
            .filter { !$0.isEmpty }
        if words.count >= 2 {
            return (String(words[0].prefix(1)) + String(words[1].prefix(1))).uppercased()
        }
        return String(name.prefix(2)).uppercased()
    }

    private static func parseElementArray(_ values: [[String: Any]]) -> [OverlayElement] {
        var gramTotals: [String: Int] = [:]
        var countTotals: [String: Int] = [:]
        for item in values {
            let rawName = firstString(item, keys: ["element", "material", "artifactMaterial", "rareEarthMineral", "artifact"])
            let name = canonicalElementName(rawName)
            guard !name.isEmpty else { continue }
            let grams = firstInt(item, keys: ["grams", "quantityGrams", "quantity"])
            gramTotals[name, default: 0] += max(0, grams)
            countTotals[name, default: 0] += 1
        }
        return gramTotals.map { OverlayElement(name: $0.key, grams: $0.value, count: countTotals[$0.key] ?? 1) }
            .sorted { $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending }
    }

    private static func parseElementCollection(_ root: [String: Any]) -> [OverlayElement] {
        var gramTotals: [String: Int] = [:]
        var countTotals: [String: Int] = [:]
        for value in root.values {
            guard let item = value as? [String: Any] else { continue }
            let rawName = firstString(item, keys: ["element", "material", "artifactMaterial", "rareEarthMineral", "artifact"])
            let name = canonicalElementName(rawName)
            guard !name.isEmpty else { continue }
            let grams = firstInt(item, keys: ["grams", "quantityGrams", "quantity"])
            let count = firstInt(item, keys: ["count"])
            gramTotals[name, default: 0] += max(0, grams)
            countTotals[name, default: 0] += max(1, count)
        }
        return gramTotals.map { OverlayElement(name: $0.key, grams: $0.value, count: countTotals[$0.key] ?? 1) }
            .sorted { $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending }
    }

    private static func firstString(_ dict: [String: Any], keys: [String]) -> String {
        for key in keys {
            if let value = dict[key] as? String {
                let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
                if !trimmed.isEmpty { return trimmed }
            }
        }
        return ""
    }

    private static func firstInt(_ dict: [String: Any], keys: [String]) -> Int {
        for key in keys {
            if let value = dict[key] as? Int { return value }
            if let value = dict[key] as? Double { return Int(value.rounded()) }
            if let value = dict[key] as? String, let parsed = Int(value) { return parsed }
        }
        return 0
    }

    private static func canonicalElementName(_ raw: String) -> String {
        let trimmed = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmed.isEmpty { return "" }
        let lower = trimmed.lowercased()
        for element in knownElementNames {
            let needle = element.lowercased()
            if lower == needle || lower.hasPrefix("\(needle) ") || lower.contains(" \(needle) ") {
                return element
            }
        }
        if let first = lower.components(separatedBy: " in ").first, !first.isEmpty {
            return first.capitalized
        }
        return trimmed.capitalized
    }

    private static let knownElementNames = [
        "Scandium", "Yttrium", "Lanthanum", "Cerium", "Praseodymium", "Neodymium",
        "Promethium", "Samarium", "Europium", "Gadolinium", "Terbium", "Dysprosium",
        "Holmium", "Erbium", "Thulium", "Ytterbium", "Lutetium", "Indium", "Gallium",
        "Bismuth", "Cobalt", "Molybdenum", "Selenium", "Tungsten", "Titanium",
        "Vanadium", "Niobium", "Tantalum", "Nickel", "Tellurium", "Hematite",
        "Magnetite", "Monazite", "Bastnäsite"
    ]

    private func formatDistance(_ meters: Double) -> String {
        if meters < 528 {
            return "\(Int((meters * 3.28084).rounded())) ft"
        }
        return String(format: "%.1f mi", meters / 1609.344)
    }

    private static func bearingDegrees(from start: CLLocationCoordinate2D, to end: CLLocationCoordinate2D) -> Double {
        let lat1 = start.latitude * .pi / 180
        let lat2 = end.latitude * .pi / 180
        let deltaLon = (end.longitude - start.longitude) * .pi / 180
        let y = sin(deltaLon) * cos(lat2)
        let x = cos(lat1) * sin(lat2) - sin(lat1) * cos(lat2) * cos(deltaLon)
        let bearing = atan2(y, x) * 180 / .pi
        return bearing >= 0 ? bearing : bearing + 360
    }
}

private struct OverlayPlacesResponse: Decodable {
    let places: [OverlayPlace]
}

private struct OverlayWorldNearbyResponse: Decodable {
    let places: [OverlayPlace]
    let beams: [OverlayBeam]
}

private struct K1L0MarqueeItem: Identifiable {
    let id: String
    let kind: String
    let line1: String
    let line2: String
    let distanceText: String?
    let relativeBearing: Double?
    let progress: Double?

    var kindDisplay: String {
        switch kind {
        case "incomingTransmission": return "incoming"
        case "ambientElement": return "ambient"
        case "ambientObject": return "ambient"
        case "location": return "location"
        default: return kind
        }
    }
}

private struct OverlayPlace: Decodable, Identifiable {
    let placeId: String?
    let name: String
    let type: String
    let types: [String]?
    let coordinates: OverlayCoordinate
    let distance: Double
    let artifactMaterial: String?
    let artifactLabel: String?
    let artifactTeaser: String?
    let teaser: String?

    var id: String { placeId ?? name }

    var bylineTeaser: String? {
        let candidates = [teaser, artifactTeaser]
        for candidate in candidates {
            let value = (candidate ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
            if !value.isEmpty { return value }
        }
        return nil
    }

    var hasCollectibleArtifact: Bool {
        let candidates = [artifactMaterial, artifactLabel, artifactTeaser, teaser]
        return candidates.contains { value in
            !(value ?? "").trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        }
    }

    var collectTitle: String {
        let candidates = [artifactLabel, artifactMaterial, artifactTeaser, teaser]
        for candidate in candidates {
            let value = (candidate ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
            if !value.isEmpty { return value }
        }
        return "Location Item"
    }

    var collectIconName: String {
        let title = collectTitle.lowercased()
        if title.contains("coffee") || title.contains("cup") || title.contains("tea") {
            return "cup.and.saucer.fill"
        }
        if title.contains("key") {
            return "key.fill"
        }
        if title.contains("book") || title.contains("paper") {
            return "book.closed.fill"
        }
        return artifactSymbol == nil ? "shippingbox.fill" : "atom"
    }

    var artifactSymbol: String? {
        guard let artifactMaterial else { return nil }
        return ElementSymbolLookup.symbol(for: artifactMaterial)
    }
}

private struct OverlayCoordinate: Decodable {
    let lat: Double
    let lng: Double
}

private struct OverlayBeamsResponse: Decodable {
    let beams: [OverlayBeam]
}

private struct OverlayUsersResponse: Decodable {
    let users: [OverlayUser]
}

private struct OverlayStepLeader: Decodable, Identifiable {
    let userId: String
    let name: String
    let helmetUrl: String
    let steps24h: Int
    let steps7d: Int
    let synthetic: Bool
    var id: String { userId }
}

private struct OverlayStepLeaderboardResponse: Decodable {
    let ok: Bool
    let top24h: [OverlayStepLeader]
    let top7d: [OverlayStepLeader]
    let participantCount: Int
}

private struct OverlayReceiveResponse: Decodable {
    let ok: Bool
    let transmission: OverlayIncomingTransmission?
}

private struct OverlayIncomingTransmission: Decodable, Identifiable {
    let sourceUserId: String
    let sourceName: String?
    let sourceCallsign: String?
    let sourceDisplayName: String?
    let jobId: String
    let thumbUrl: String?
    let finalUrl: String?
    let rawVideoUrl: String?
    let videoUrl: String?
    let audioUrl: String?
    let responsePlot: String?
    let responseOptions: [String]?
    let receiverSlide: OverlayReceiverSlide?
    let rootJobId: String?
    let parentJobId: String?
    let chainDepth: Int?
    let updatedAt: Double?
    let chain: [OverlayIncomingTransmission]?

    var id: String { "\(sourceUserId)_\(jobId)" }

    var isOriginalTransmission: Bool {
        let job = jobId.trimmingCharacters(in: .whitespacesAndNewlines)
        let parent = (parentJobId ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        let root = (rootJobId ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        return parent.isEmpty && (root.isEmpty || root == job)
    }

    var senderLabel: String {
        let candidates = [sourceDisplayName, sourceName, sourceCallsign, sourceUserId]
        for candidate in candidates {
            let value = (candidate ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
            if !value.isEmpty { return value }
        }
        return "UNKNOWN"
    }

    var playbackVideoUrl: String? {
        let raw = rawVideoUrl?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !raw.isEmpty { return raw }
        let video = videoUrl?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !video.isEmpty { return video }
        let final = finalUrl?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return final.isEmpty ? nil : final
    }
}

private struct OverlayReceiverSlide: Decodable {
    let type: String?
    let imageUrl: String?
    let videoUrl: String?
    let rawVideoUrl: String?
    let audioUrl: String?

    var playbackVideoUrl: String? {
        let raw = rawVideoUrl?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !raw.isEmpty { return raw }
        let video = videoUrl?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return video.isEmpty ? nil : video
    }
}

private struct OverlayUser: Decodable, Identifiable {
    let userId: String
    let name: String?
    let callsign: String?
    let avatarUrl: String?
    let helmetUrl: String?
    let faceUrl: String?
    let city: String?
    let lat: Double?
    let lng: Double?
    let lastActive: Double?

    var id: String { userId }

    var displayName: String {
        let call = (callsign ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if !call.isEmpty { return call }
        let realName = (name ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if !realName.isEmpty { return realName }
        return String(userId.prefix(10))
    }

    var nameAndCallsign: String {
        let realName = (name ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        let call = (callsign ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if !realName.isEmpty && !call.isEmpty { return "\(realName) (\(call))" }
        if !realName.isEmpty { return realName }
        if !call.isEmpty { return "(\(call))" }
        return String(userId.prefix(10))
    }

	    var avatarDisplayUrl: String? {
	        let helmet = (helmetUrl ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
	        return helmet.isEmpty ? K1L0DefaultHelmetIconURL : helmet
	    }
}

private struct OverlayBeam: Decodable, Identifiable {
    let id: String
    let lat: Double
    let lng: Double
    let label: String?
    let rewardType: String?
    let objectName: String?
    let material: String?
    let container: String?
    let artifactLabel: String?
    let artifactMaterial: String?
    let artifactContainer: String?
    let senderName: String?
    let artifactSenderName: String?
    let teaser: String?
    let grams: Int?
    let expiresAt: Double?
    let distanceMeters: Double
    let imageUrl: String?

    var title: String {
        firstDisplayName(includeTeaser: true) ?? "Ambient Object"
    }

    var collectLabel: String {
        firstDisplayName(includeTeaser: false) ?? title
    }

    var teaserText: String {
        let t = (teaser ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if !t.isEmpty { return t }
        let identity = collectLabel.trimmingCharacters(in: .whitespacesAndNewlines)
        return identity.isEmpty ? "Nearby item" : "Nearby \(identity.lowercased())"
    }

    var collectIconName: String {
        let name = title.lowercased()
        if name.contains("coffee") || name.contains("cup") || name.contains("tea") {
            return "cup.and.saucer.fill"
        }
        if name.contains("shoe") || name.contains("sneaker") {
            return "shoeprints.fill"
        }
        if name.contains("key") {
            return "key.fill"
        }
        if name.contains("book") || name.contains("paper") {
            return "book.closed.fill"
        }
        if name.contains("candy") || name.contains("gum") {
            return "takeoutbag.and.cup.and.straw.fill"
        }
        return "shippingbox.fill"
    }

    var senderTitle: String {
        senderName ?? artifactSenderName ?? "Unknown"
    }

    private func firstDisplayName(includeTeaser: Bool) -> String? {
        let primaryMaterial = clean(material)
        let primaryContainer = clean(container)
        let artifactMaterialValue = clean(artifactMaterial)
        let artifactContainerValue = clean(artifactContainer)
        let materialObject = Self.joinMaterial(primaryMaterial, primaryContainer)
        let artifactObject = Self.joinMaterial(artifactMaterialValue, artifactContainerValue)

        var candidates = [
            clean(objectName),
            clean(label),
            clean(artifactLabel),
            materialObject,
            artifactObject,
            primaryMaterial,
            artifactMaterialValue
        ]
        if includeTeaser {
            candidates.append(clean(teaser))
        }

        return candidates.compactMap { $0 }.first { value in
            return !Self.isGenericDisplayName(value)
        }
    }

    private func clean(_ value: String?) -> String? {
        guard let value else { return nil }
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }

    private static func joinMaterial(_ material: String?, _ container: String?) -> String? {
        guard let material else { return nil }
        guard let container, !container.isEmpty else { return material }
        return "\(material) in \(container)"
    }

    private static func isGenericDisplayName(_ value: String) -> Bool {
        let normalized = value
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .lowercased()
        return normalized == "object"
            || normalized == "object found"
            || normalized == "ambient object"
            || normalized == "mystery object"
            || normalized == "metal"
            || normalized == "collectible"
            || normalized == "unknown"
    }
}

private struct OverlayElement: Identifiable {
    let name: String
    let grams: Int
    let count: Int

    var id: String { name.lowercased() }

    var symbol: String { ElementSymbolLookup.symbol(for: name) }
}

private struct OverlayInventoryItem: Identifiable {
    let id: String
    let kind: String
    let name: String
    let symbol: String
    let grams: Int
    let count: Int
    let avatarUrl: String
    let senderName: String
    let sourceTransmissionJobId: String
    let collectedAt: Date?

    init(id: String, kind: String, name: String, symbol: String, grams: Int, count: Int, avatarUrl: String,
         senderName: String = "", sourceTransmissionJobId: String = "", collectedAt: Date? = nil) {
        self.id = id
        self.kind = kind
        self.name = name
        self.symbol = symbol
        self.grams = grams
        self.count = count
        self.avatarUrl = avatarUrl
        self.senderName = senderName
        self.sourceTransmissionJobId = sourceTransmissionJobId
        self.collectedAt = collectedAt
    }

    init(element: OverlayElement) {
        self.id = "element:\(element.id)"
        self.kind = "element"
        self.name = element.name
        self.symbol = element.symbol
        self.grams = element.grams
        self.count = element.count
        self.avatarUrl = ""
        self.senderName = ""
        self.sourceTransmissionJobId = ""
        self.collectedAt = nil
    }

    var isElement: Bool { kind.lowercased() == "element" }
    var amountText: String { grams > 0 ? "\(grams)g" : "×\(count)" }

    var detailDescription: String {
        if isElement {
            return "Rare material extracted from transmissions. Elements accumulate over time and will unlock future upgrades and crafting."
        }
        let lower = name.lowercased()
        if lower.contains("relay") || lower.contains("signal") {
            return "A signal artifact collected from the kiloverse. Prototype hardware that may power future transmission capabilities."
        }
        if lower.contains("key") || lower.contains("access") {
            return "An access artifact. May unlock restricted channels or special transmission modes."
        }
        return "An object collected from a transmission. Its purpose will become clear as the kiloverse expands."
    }
}

private struct InventoryTile: View {
    let item: OverlayInventoryItem

    var body: some View {
        VStack(spacing: 6) {
            ZStack {
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .fill(item.isElement ? Color(red: 0.05, green: 0.25, blue: 0.12).opacity(0.72) : Color.white.opacity(0.10))
                    .overlay(
                        RoundedRectangle(cornerRadius: 8, style: .continuous)
                            .stroke(item.isElement ? Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.42) : Color.white.opacity(0.16), lineWidth: 1)
                    )
                if !item.isElement, let url = URL(string: item.avatarUrl), !item.avatarUrl.isEmpty {
                    AsyncImage(url: url) { phase in
                        switch phase {
                        case .success(let image):
                            image
                                .resizable()
                                .scaledToFill()
                        default:
                            Text(item.symbol)
                                .font(.system(size: 18, weight: .black))
                                .foregroundStyle(.white.opacity(0.78))
                        }
                    }
                    .frame(width: 58, height: 58)
                    .clipShape(RoundedRectangle(cornerRadius: 7, style: .continuous))
                } else {
                    Text(item.symbol)
                        .font(.system(size: item.isElement ? 24 : 18, weight: .black))
                        .foregroundStyle(item.isElement ? Color(red: 0.66, green: 1.0, blue: 0.76) : .white.opacity(0.84))
                }
            }
            .frame(width: 64, height: 64)

            Text(item.name)
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(.white.opacity(0.90))
                .lineLimit(2)
                .multilineTextAlignment(.center)
                .frame(width: 72, height: 28, alignment: .top)

            Text(item.amountText)
                .font(.system(size: 11, weight: .black))
                .monospacedDigit()
                .foregroundStyle(.white.opacity(0.70))
        }
        .frame(width: 76, height: 112, alignment: .top)
    }
}

private enum ElementSymbolLookup {
    static func symbol(for rawName: String) -> String {
        let key = rawName.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        return symbols[key] ?? String(rawName.prefix(2)).capitalized
    }

    private static let symbols: [String: String] = [
        "scandium": "Sc",
        "yttrium": "Y",
        "lanthanum": "La",
        "cerium": "Ce",
        "praseodymium": "Pr",
        "neodymium": "Nd",
        "promethium": "Pm",
        "samarium": "Sm",
        "europium": "Eu",
        "gadolinium": "Gd",
        "terbium": "Tb",
        "dysprosium": "Dy",
        "holmium": "Ho",
        "erbium": "Er",
        "thulium": "Tm",
        "ytterbium": "Yb",
        "lutetium": "Lu"
    ]
}

private struct WeatherAlertCard<Content: View>: View {
    @ViewBuilder let content: Content

    var body: some View {
        content
            .foregroundStyle(.white)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(16)
            .background(Color.black.opacity(0.12), in: RoundedRectangle(cornerRadius: 24, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 24, style: .continuous)
                    .stroke(Color.yellow.opacity(0.58), lineWidth: 1.5)
            )
    }
}

private struct WeatherGlassCard<Content: View>: View {
    @ViewBuilder let content: Content

    var body: some View {
        content
            .foregroundStyle(.white)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(18)
            .background(Color.white.opacity(0.035), in: RoundedRectangle(cornerRadius: 28, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 28, style: .continuous)
                    .stroke(.white.opacity(0.12), lineWidth: 1)
            )
    }
}

// MARK: - Keyboard helpers
//
// Panels use ScrollView plus SwiftUI's native keyboard safe-area behavior.
// Do not add manual keyboard-height padding here; on bottom-pinned sheets it
// creates a giant spacer instead of simply lifting the active field.

#if canImport(UIKit)
private func k1l0DismissKeyboard() {
    UIApplication.shared.sendAction(#selector(UIResponder.resignFirstResponder), to: nil, from: nil, for: nil)
}

private final class K1L0KeyboardObserver: ObservableObject {
    static let shared = K1L0KeyboardObserver()

    @Published var height: CGFloat = 0

    private init() {
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(keyboardWillChangeFrame(_:)),
            name: UIResponder.keyboardWillChangeFrameNotification,
            object: nil
        )
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(keyboardWillHide(_:)),
            name: UIResponder.keyboardWillHideNotification,
            object: nil
        )
    }

    @objc private func keyboardWillChangeFrame(_ notification: Notification) {
        guard let frame = notification.userInfo?[UIResponder.keyboardFrameEndUserInfoKey] as? CGRect,
              let window = UIApplication.shared.connectedScenes
                .compactMap({ $0 as? UIWindowScene })
                .flatMap({ $0.windows })
                .first(where: { $0.isKeyWindow })
        else { return }
        let overlap = max(0, window.bounds.maxY - frame.minY - window.safeAreaInsets.bottom)
        height = overlap
    }

    @objc private func keyboardWillHide(_ notification: Notification) {
        height = 0
    }
}

extension View {
    /// Compat shim for `.scrollDismissesKeyboard(.interactively)` — only
    /// available on iOS 16+. On older OS this is a no-op.
    @ViewBuilder
    func scrollDismissesKeyboardCompat() -> some View {
        if #available(iOS 16.0, *) {
            scrollDismissesKeyboard(.interactively)
        } else {
            self
        }
    }

    @ViewBuilder
    func scrollContentBackgroundCompatHidden() -> some View {
        if #available(iOS 16.0, *) {
            scrollContentBackground(.hidden)
        } else {
            self
        }
    }

    func transmitterKeyboardDoneToolbar() -> some View {
        toolbar {
            ToolbarItemGroup(placement: .keyboard) {
                Spacer()
                Button("Done") {
                    k1l0DismissKeyboard()
                }
                .font(.system(size: 15, weight: .bold))
            }
        }
    }
}
#else
private final class K1L0OverlayWindow: NSWindow {
    var lockedFrame: NSRect?

    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }

    override func keyDown(with event: NSEvent) {
        if forwardMovementKey(event) { return }
        super.keyDown(with: event)
    }

    override func keyUp(with event: NSEvent) {
        if forwardMovementKey(event) { return }
        super.keyUp(with: event)
    }

    override func constrainFrameRect(_ frameRect: NSRect, to screen: NSScreen?) -> NSRect {
        lockedFrame ?? super.constrainFrameRect(frameRect, to: screen)
    }

    private func forwardMovementKey(_ event: NSEvent) -> Bool {
        switch event.keyCode {
        case 0, 1, 2, 13, 123, 124, 125, 126:
            parent?.sendEvent(event)
            return true
        default:
            return false
        }
    }
}

private final class K1L0KeyboardObserver: ObservableObject {
    static let shared = K1L0KeyboardObserver()
    @Published var height: CGFloat = 0
}

internal final class K1L0StatusTarget: NSObject {
    static let shared = K1L0StatusTarget()
    
    @objc func showApp() {
        if let main = NSApp.windows.first(where: { $0.className.contains("PlayerWindow") || $0.title == "K1L0" }) {
            main.makeKeyAndOrderFront(nil)
            NSApp.activate(ignoringOtherApps: true)
        }
    }
    
    @objc func hideApp() {
        NSApp.hide(nil)
    }
    
    @objc func quitApp() {
        NSApp.terminate(nil)
    }
}

struct SegmentedRow: View {
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
                        .clipShape(RoundedRectangle(cornerRadius: 6))
                }
                .buttonStyle(.plain)

                if index < items.count - 1 && !isSelected && selection != items[index + 1].tag {
                    Color.white.opacity(0.15)
                        .frame(width: 1, height: 16)
                }
            }
        }
        .padding(2)
        .background(Color.white.opacity(0.08))
        .clipShape(RoundedRectangle(cornerRadius: 8))
        .overlay(
            RoundedRectangle(cornerRadius: 8)
                .stroke(Color.white.opacity(0.12), lineWidth: 1)
        )
    }
}

struct CategoryButton: View {
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

extension View {
    func keyboardAdaptive() -> some View { self }
    func scrollDismissesKeyboardCompat() -> some View { self }
    func scrollContentBackgroundCompatHidden() -> some View { self }
    func transmitterKeyboardDoneToolbar() -> some View { self }
}
#endif
