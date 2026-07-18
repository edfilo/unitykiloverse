import Foundation

struct K1L0WeatherPresetDescriptor: Codable, Identifiable, Hashable {
    let id: String
    let label: String
    let revision: Int
}

/// Authoritative Day/Night/Auto visual-mode presets and live tuning boundary.
/// Kept separate from the large SwiftUI overlay so renderer tuning recompiles
/// this small file instead of the entire application HUD implementation.
enum K1L0WeatherModeController {
    typealias Setting = (String, String)

    private static var autoDaySettings: [String: String]?
    private static var autoNightSettings: [String: String]?

    private static let common: [Setting] = [
        ("testSkyOverride", "0"),
        ("layeredBypassWeather", "0"),
        ("solarWorldOverride", "0"),
        ("zossLitFraction", "1.0"),
        ("zossWindowBrightness", "1.0")
    ]

    /// Every visual mode begins from the same neutral renderer state. Presets
    /// then opt effects back in explicitly, so a disabled effect or override
    /// can never leak from the mode that was selected previously.
    private static let resetBaseline: [Setting] = [
        ("testSkyOverride", "0"),
        ("layeredBypassWeather", "0"),
        ("solarWorldOverride", "0"),
        ("visualNightOverride", "0"),
        ("manualWeatherOverrideEnabled", "0"),
        ("volumetricFogEnabled", "0"),
        ("fogConstantDensity", "0"),
        ("fogDensity", "0"),
        ("fogBrightness", "0.045"),
        ("fogScatteringIntensity", "0"),
        ("fogNoiseStrength", "0"),
        ("fogNoiseScale", "8"),
        ("fogTurbulence", "0"),
        ("fogWindX", "0"),
        ("fogWindY", "0"),
        ("fogWindZ", "0"),
        ("fogOrangeAmount", "0"),
        ("fogDistantFog", "0"),
        ("fogDistantDensity", "0"),
        ("fogNativeLights", "0"),
        ("groundHazeEnabled", "0"),
        ("groundHazeDensity", "0"),
        ("groundHazePinkAmount", "0"),
        ("groundHazeWhiteAmount", "0"),
        ("groundHazeBlueAmount", "0"),
        ("groundHazeOrangeAmount", "0"),
        ("groundHazeHorizonDensity", "0"),
        ("groundHazeHorizonDistance", "1120"),
        ("groundHazeHorizonHeight", "0"),
        ("experimentalLayeredSky", "0"),
        ("layeredSkyEffect", "0"),
        ("layeredRain", "0"),
        ("layeredAurora", "0"),
        ("bloomEnabled", "0"),
        ("vignetteEnabled", "0"),
        ("chromaticEnabled", "0"),
        ("dofEnabled", "0"),
        ("motionBlurEnabled", "0"),
        ("filmGrainEnabled", "0"),
        ("saturation", "0"),
        ("contrast", "0"),
        ("temperature", "0"),
        ("tint", "0"),
        ("hueShift", "0"),
        ("skyCloudPink", "0"),
        ("skyHorizonPink", "0"),
        ("vaporDayPink", "0"),
        ("ambientEnabled", "0"),
        ("ambientIntensity", "0"),
        ("spotlightEnabled", "0"),
        ("reflectionsEnabled", "0"),
        ("enableShadows", "0"),
        ("zossEmissiveIntensity", "5.5"),
        ("zossEmissiveHue", "0.90"),
        ("zossEmissiveSaturation", "0.35"),
        ("zossEmissiveSmoothness", "0.34"),
        ("zossEmissiveMetallic", "0"),
        ("zossPaletteMix", "1"),
        ("zossPaletteSaturation", "0.48"),
        ("zossWarmth", "0.32"),
        ("zossAccentFraction", "0.08"),
        ("zossWindowBrightness", "1"),
        ("zossWallHue", "0.33"),
        ("zossWallSaturation", "0.30"),
        ("zossWallValue", "0.16"),
        ("zossWallDaylightLift", "0.28")
    ]

    private static let automatic: [Setting] = [
        ("visualNightOverride", "0"),
        ("fogOrangeAmount", "0"),
        ("fogDensity", "0.00038"),
        ("fogBrightness", "0.045"),
        ("fogScatteringIntensity", "0.10"),
        ("fogNoiseStrength", "0.14"),
        ("fogNoiseScale", "8.0"),
        ("fogTurbulence", "0.35"),
        ("fogWindX", "0.025"),
        ("fogWindY", "0.002"),
        ("fogWindZ", "0.012"),
        ("fogDistantFog", "1"),
        ("fogDistantDensity", "0.00014"),
        ("fogDistantStart", "105"),
        ("mapBrightness", "0.22"),
        ("temperature", "0"),
        ("tint", "0"),
        ("saturation", "0"),
        ("contrast", "0"),
        ("groundHue", "0.33"),
        ("groundSaturation", "0.32"),
        ("groundValue", "0.24"),
        ("groundBrightness", "0.86"),
        ("bloomEnabled", "1"),
        ("dayBloomIntensity", "2.2"),
        ("bloomIntensity", "2.2"),
        ("bloomThreshold", "1.0"),
        ("bloomScatter", "0.68"),
        ("daySunIntensity", "1.35"),
        ("zossDayWindowIntensity", "5.0"),
        ("zossEmissiveIntensity", "5.5"),
        ("zossWindowBrightness", "0.62"),
        ("zossWarmth", "0.32"),
        ("zossEmissiveSaturation", "0.35"),
        ("zossPaletteSaturation", "0.48"),
        ("roadValue", "0.72"),
        ("dayRoadValue", "0.42"),
        ("roadSaturation", "0.08"),
        ("roadGlow", "0.14"),
        ("skyCloudPink", "0"),
        ("skyHorizonPink", "0"),
        ("vaporDayPink", "0"),
        ("groundHazeEnabled", "1"),
        ("groundHazeDensity", "0.12"),
        ("groundHazeDetail", "1.6"),
        ("groundHazeSpeed", "0.025"),
        ("groundHazeHeight", "0.16"),
        ("groundHazeSpacing", "0.22"),
        ("groundHazeHue", "0.60"),
        ("groundHazeSaturation", "0.12"),
        ("groundHazeBrightness", "0.38"),
        ("groundHazeBlueAmount", "0.08"),
        ("groundHazeOrangeAmount", "0")
    ]

    private static let radioactive: [Setting] = [
        ("testSkyOverride", "1"),
        ("layeredBypassWeather", "1"),
        ("solarWorldOverride", "1"),
        ("manualHour", "13.25"),
        ("visualNightOverride", "0"),
        ("fogOrangeAmount", "0.32"),
        // The full-screen volumetric path halves iPhone frame rate. The horizon
        // curtain and tri-color haze carry the Day atmosphere instead.
        ("fogDensity", "0"),
        ("fogBrightness", "0.38"),
        ("fogScatteringIntensity", "0.52"),
        ("fogNoiseStrength", "2.2"),
        ("fogNoiseScale", "5.5"),
        ("fogTurbulence", "2.2"),
        ("fogWindX", "0.15"),
        ("fogWindY", "0.004"),
        ("fogWindZ", "0.07"),
        ("fogHeight", "5.4"),
        ("fogDistantFog", "0"),
        ("fogDistantDensity", "0.0016"),
        ("fogDistantStart", "145"),
        ("mapBrightness", "0.20"),
        ("temperature", "0"),
        ("tint", "5"),
        ("saturation", "18"),
        ("contrast", "16"),
        ("groundHue", "0.35"),
        ("groundSaturation", "1.0"),
        ("groundValue", "0.32"),
        ("groundBrightness", "1.08"),
        ("bloomEnabled", "1"),
        ("dayBloomIntensity", "3.35"),
        ("bloomIntensity", "3.35"),
        ("bloomThreshold", "0.68"),
        ("bloomScatter", "0.91"),
        ("daySunIntensity", "5.0"),
        ("zossDayWindowIntensity", "11.2"),
        ("zossEmissiveIntensity", "11.2"),
        ("zossWindowBrightness", "0.96"),
        ("zossWarmth", "0.0"),
        ("zossEmissiveSaturation", "0.12"),
        ("zossPaletteSaturation", "0.42"),
        ("roadValue", "0.76"),
        ("dayRoadValue", "0.54"),
        ("roadSaturation", "0.10"),
        ("roadGlow", "0.15"),
        ("skyCloudPink", "0.10"),
        ("skyHorizonPink", "0.42"),
        ("vaporDayPink", "0.36"),
        ("skySunsetWarmth", "0.40"),
        ("groundHazeEnabled", "1"),
        ("groundHazeDensity", "0.54"),
        ("groundHazeDetail", "1.58"),
        ("groundHazeSpeed", "0.078"),
        ("groundHazeHeight", "0.12"),
        ("groundHazeSpacing", "0.60"),
        ("groundHazeHue", "0.038"),
        ("groundHazeSaturation", "0.56"),
        ("groundHazeBrightness", "1.04"),
        ("groundHazeExtent", "280"),
        ("groundHazePinkAmount", "0.62"),
        ("groundHazeWhiteAmount", "0.30"),
        ("groundHazeBlueAmount", "0.14"),
        ("groundHazeOrangeAmount", "0.80"),
        ("groundHazeHorizonDensity", "0.54"),
        ("groundHazeHorizonDistance", "255"),
        ("groundHazeHorizonHeight", "27"),
        ("godPositionY", "49.0"),
        ("godPositionZ", "107.0"),
        ("godRotationX", "-2.0")
    ]

    /// Bookmarked July 16 live-tuned look: deep blue daylight with a broad,
    /// animated pink horizon curtain and consistently luminous windows.
    private static let pinkHaze: [Setting] = [
        ("testSkyOverride", "1"),
        ("layeredBypassWeather", "1"),
        ("solarWorldOverride", "1"),
        ("manualHour", "13.25"),
        ("visualNightOverride", "0"),
        ("volumetricFogEnabled", "0"),
        ("fogDensity", "0"),
        ("fogDistantFog", "0"),
        ("groundHazeEnabled", "1"),
        ("groundHazeDensity", "0.92"),
        ("groundHazeDetail", "0.66"),
        ("groundHazeSpeed", "0.11"),
        ("groundHazeHeight", "142"),
        ("groundHazeSpacing", "1.58"),
        ("groundHazeHue", "0.965"),
        ("groundHazeSaturation", "0.36"),
        ("groundHazeBrightness", "1.38"),
        ("groundHazeExtent", "1.0"),
        ("groundHazePinkAmount", "0.76"),
        ("groundHazeWhiteAmount", "0.40"),
        ("groundHazeBlueAmount", "0.06"),
        ("groundHazeOrangeAmount", "0.06"),
        ("groundHazeHorizonDensity", "0.70"),
        ("groundHazeHorizonDistance", "1120"),
        ("groundHazeHorizonHeight", "108"),
        ("skyHorizonPink", "0.58"),
        ("skyDayBrightness", "1.02"),
        ("zossWallHue", "0.91"),
        ("zossWallSaturation", "0.30"),
        ("zossWallValue", "0.16"),
        ("zossWallDaylightLift", "0.28"),
        ("groundHue", "0.91"),
        ("groundSaturation", "0.30"),
        ("groundValue", "0.16"),
        ("roadHue", "0.90"),
        ("roadSaturation", "0.24"),
        ("roadValue", "0.24"),
        ("waterHue", "0.83"),
        ("waterSaturation", "0.28"),
        ("waterValue", "0.24"),
        ("bloomEnabled", "1"),
        ("bloomIntensity", "1.48"),
        ("bloomThreshold", "0.50"),
        ("bloomScatter", "0.88"),
        ("zossLitFraction", "1"),
        ("zossWindowBrightness", "1.78"),
        ("contrast", "8"),
        ("saturation", "10"),
        ("exposureFixedValue", "0.46")
    ]

    // Patchier, lower, rust-pink dust banks for experimentation. These trailing
    // values intentionally override Pink Haze while leaving its bookmark intact.
    private static let hazeLab: [Setting] = pinkHaze + [
        ("volumetricFogEnabled", "0"),
        ("fogDensity", "0"),
        ("groundHazeDensity", "0.80"),
        ("groundHazeDetail", "2.45"),
        ("groundHazeSpeed", "0.12"),
        ("groundHazeHeight", "78"),
        ("groundHazeSpacing", "0.58"),
        ("groundHazeHue", "0.008"),
        ("groundHazeSaturation", "0.60"),
        ("groundHazeBrightness", "1.04"),
        ("groundHazePinkAmount", "0.46"),
        ("groundHazeWhiteAmount", "0.10"),
        ("groundHazeBlueAmount", "0.05"),
        ("groundHazeOrangeAmount", "0.48"),
        ("groundHazeHorizonDensity", "0.44"),
        ("groundHazeHorizonDistance", "1280"),
        ("groundHazeHorizonHeight", "58"),
        ("skyHorizonPink", "0.46"),
        ("bloomIntensity", "1.68"),
        ("bloomThreshold", "0.43"),
        ("zossWindowBrightness", "1.96"),
        ("contrast", "14"),
        ("saturation", "15")
    ]

    /// Deliberately plain locked daylight for judging map colors and geometry
    /// without any atmospheric grading, volumetric fog, horizon curtain, or
    /// procedural dust/haze layers.
    private static let boring: [Setting] = [
        ("testSkyOverride", "1"),
        ("layeredBypassWeather", "1"),
        ("solarWorldOverride", "1"),
        ("manualHour", "13.25"),
        ("visualNightOverride", "0"),
        ("fogOrangeAmount", "0"),
        ("fogDensity", "0"),
        ("fogDistantFog", "0"),
        ("fogDistantDensity", "0"),
        ("saturation", "0"),
        ("contrast", "0"),
        ("temperature", "0"),
        ("tint", "0"),
        ("groundHazeEnabled", "0"),
        ("groundHazeDensity", "0"),
        ("groundHazeHorizonDensity", "0"),
        ("skyCloudPink", "0"),
        ("skyHorizonPink", "0"),
        ("vaporDayPink", "0"),
        ("groundHue", "0.33"),
        ("groundSaturation", "0.42"),
        ("groundValue", "0.30"),
        ("groundBrightness", "0.90"),
        ("roadValue", "0.72"),
        ("dayRoadValue", "0.48"),
        ("roadSaturation", "0.08"),
        ("bloomEnabled", "1"),
        ("dayBloomIntensity", "2.2"),
        ("bloomIntensity", "2.2"),
        ("bloomThreshold", "1.0"),
        ("bloomScatter", "0.68")
    ]

    private struct RemotePreset: Codable {
        let label: String?
        let revision: Int?
        let settings: [String: String]
    }

    private struct RemoteCatalog: Codable {
        let presets: [String: RemotePreset]
    }

    private struct RemoteModeResponse: Codable {
        let preset: RemotePreset
    }

    private static let endpoint = URL(string: "https://api-tunnel.kilo.gallery/api/k1l0/weather-presets")!
    private static let cacheKey = "k1l0_weather_preset_catalog_v1"
    private static let preferredOrder = ["auto", "radioactive", "midnight", "pink_haze", "haze_lab", "coral_haze", "deep_orange", "fire_fog_lab", "boring"]

    static var bundledDescriptors: [K1L0WeatherPresetDescriptor] {
        [
            .init(id: "auto", label: "Live · Weather + Astronomy", revision: 0),
            .init(id: "radioactive", label: "Apocalyptic Day · 2:15 PM · Overcast", revision: 0),
            .init(id: "midnight", label: "Foggy Blue Night · 11:30 PM · Cloudy", revision: 0),
            .init(id: "pink_haze", label: "Pink Haze · 1:15 PM · Broken Clouds", revision: 0),
            .init(id: "haze_lab", label: "Rust Haze · 4:20 PM · Dust", revision: 0),
            .init(id: "coral_haze", label: "Coral Dusk · 6:35 PM · Haze", revision: 0),
            .init(id: "deep_orange", label: "Fire Sunset · 6:50 PM · Smoke", revision: 0),
            .init(id: "fire_fog_lab", label: "Fire Fog · 7:05 PM · Dense Smoke", revision: 0),
            .init(id: "boring", label: "Neutral Reference · 1:00 PM · Clear", revision: 0)
        ]
    }

    static func refreshCatalog(completion: @escaping ([K1L0WeatherPresetDescriptor]) -> Void) {
        var request = URLRequest(url: endpoint)
        request.cachePolicy = .reloadIgnoringLocalAndRemoteCacheData
        request.timeoutInterval = 8
        URLSession.shared.dataTask(with: request) { data, response, _ in
            let status = (response as? HTTPURLResponse)?.statusCode ?? 0
            if (200..<300).contains(status), let data,
               let catalog = try? JSONDecoder().decode(RemoteCatalog.self, from: data) {
                UserDefaults.standard.set(data, forKey: cacheKey)
                DispatchQueue.main.async { completion(descriptors(for: catalog)) }
                return
            }
            let fallback = cachedCatalog().map(descriptors(for:)) ?? bundledDescriptors
            DispatchQueue.main.async { completion(fallback) }
        }.resume()
    }

    private static func cachedCatalog() -> RemoteCatalog? {
        guard let data = UserDefaults.standard.data(forKey: cacheKey) else { return nil }
        return try? JSONDecoder().decode(RemoteCatalog.self, from: data)
    }

    private static func descriptors(for catalog: RemoteCatalog) -> [K1L0WeatherPresetDescriptor] {
        catalog.presets.map { key, preset in
            .init(id: key, label: preset.label ?? key.replacingOccurrences(of: "_", with: " ").capitalized,
                  revision: preset.revision ?? 0)
        }.sorted {
            let lhs = preferredOrder.firstIndex(of: $0.id) ?? Int.max
            let rhs = preferredOrder.firstIndex(of: $1.id) ?? Int.max
            return lhs == rhs ? $0.label < $1.label : lhs < rhs
        }
    }

    /// Workshop looks are deterministic lighting laboratories. Only Auto may
    /// consume the wall clock, GPS weather, or live astronomy. Every other mode
    /// locks the simulation before its authored renderer values are applied.
    private static func workshopContext(for mode: String) -> [Setting] {
        guard mode != "auto" else { return [] }
        return [
            ("testSkyOverride", "1"),
            ("layeredBypassWeather", "1"),
            ("solarWorldOverride", "1"),
            ("manualWeatherOverrideEnabled", "1")
        ]
    }

    private static func applySettings(_ settings: [Setting], mode: String, syncEnvironment: Bool = true) {
        for (key, value) in resetBaseline + common + settings + workshopContext(for: mode) {
            if let number = Double(value) {
                UserDefaults.standard.set(number, forKey: "k1lo_native_\(key)")
            } else {
                UserDefaults.standard.set(value, forKey: "k1lo_native_\(key)")
            }
            K1L0WeatherOverlayInstaller.setUnitySetting(key, value)
        }
        if syncEnvironment && (mode == "auto" || mode == "midnight" || mode == "radioactive") {
            NativeUnityLightingSync.sync()
            NativeUnitySolarSync.sync()
        }
    }

    private static let autoDiscreteKeys: Set<String> = [
        "testSkyOverride", "layeredBypassWeather", "solarWorldOverride",
        "visualNightOverride", "manualWeatherOverrideEnabled", "volumetricFogEnabled",
        "fogConstantDensity", "fogDistantFog", "fogNativeLights", "groundHazeEnabled",
        "experimentalLayeredSky", "bloomEnabled", "vignetteEnabled", "chromaticEnabled",
        "dofEnabled", "motionBlurEnabled", "filmGrainEnabled", "ambientEnabled",
        "spotlightEnabled", "reflectionsEnabled", "enableShadows"
    ]

    private static func interpolatedAutoSettings(day: [String: String], night: [String: String], dayness: Double) -> [Setting] {
        let t = min(1, max(0, dayness))
        let keys = Set(day.keys).union(night.keys)
        return keys.sorted().compactMap { key in
            if key == "manualHour" { return nil }
            let dayValue = day[key] ?? night[key]
            let nightValue = night[key] ?? day[key]
            guard let dayValue, let nightValue else { return nil }
            if autoDiscreteKeys.contains(key) {
                return (key, t >= 0.5 ? dayValue : nightValue)
            }
            if let d = Double(dayValue), let n = Double(nightValue) {
                var value: Double
                if key.lowercased().contains("hue") {
                    var delta = d - n
                    if delta > 0.5 { delta -= 1 }
                    if delta < -0.5 { delta += 1 }
                    value = (n + delta * t).truncatingRemainder(dividingBy: 1)
                    if value < 0 { value += 1 }
                } else {
                    value = n + (d - n) * t
                }
                return (key, String(format: "%.6f", value))
            }
            return (key, t >= 0.5 ? dayValue : nightValue)
        }
    }

    /// Auto is not a third authored look. It continuously blends the canonical
    /// Night and Day presets using the live solar altitude (-6° night, +8° day).
    static func applyAutoForSolarAltitude(_ altitude: Double) {
        guard UserDefaults.standard.string(forKey: "k1lo_native_weatherLookMode") == "auto",
              let day = autoDaySettings, let night = autoNightSettings else { return }
        let dayness = (altitude + 6.0) / 14.0
        let settings = interpolatedAutoSettings(day: day, night: night, dayness: dayness)
        applySettings(settings + [("testSkyOverride", "0"),
                                  ("layeredBypassWeather", "0"),
                                  ("solarWorldOverride", "0"),
                                  ("visualNightOverride", "0"),
                                  ("manualWeatherOverrideEnabled", "0")],
                      mode: "auto", syncEnvironment: false)
    }

    private static func applyAuto() {
        var request = URLRequest(url: endpoint)
        request.cachePolicy = .reloadIgnoringLocalAndRemoteCacheData
        request.timeoutInterval = 8
        URLSession.shared.dataTask(with: request) { data, urlResponse, _ in
            let status = (urlResponse as? HTTPURLResponse)?.statusCode ?? 0
            let remoteCatalog = (200..<300).contains(status) && data != nil
                ? try? JSONDecoder().decode(RemoteCatalog.self, from: data!)
                : nil
            let catalog = remoteCatalog ?? cachedCatalog()
            let day = catalog?.presets["radioactive"]?.settings
                ?? Dictionary(uniqueKeysWithValues: radioactive)
            let night = catalog?.presets["midnight"]?.settings
                ?? Dictionary(uniqueKeysWithValues: automatic)
            DispatchQueue.main.async {
                autoDaySettings = day
                autoNightSettings = night
                let altitude = UserDefaults.standard.object(forKey: "k1lo_native_liveSolarAltitude") as? Double ?? 0
                applyAutoForSolarAltitude(altitude)
                NativeUnityLightingSync.sync()
                NativeUnitySolarSync.sync()
            }
        }.resume()
    }

    static func apply(_ mode: String) {
        if mode == "auto" {
            applyAuto()
            return
        }
        let nightLock: [Setting] = [("visualNightOverride", "1")]
        let selected: [Setting]
        switch mode {
        case "midnight": selected = automatic + nightLock
        case "auto": selected = automatic
        case "boring": selected = boring
        case "pink_haze": selected = pinkHaze
        case "haze_lab": selected = hazeLab
        default: selected = radioactive
        }

        // The API copy is canonical. Only use the last server snapshot (or the
        // bundled preset) if the fresh request actually fails.
        var request = URLRequest(url: endpoint.appendingPathComponent(mode))
        request.cachePolicy = .reloadIgnoringLocalAndRemoteCacheData
        request.timeoutInterval = 8
        URLSession.shared.dataTask(with: request) { data, urlResponse, _ in
            let status = (urlResponse as? HTTPURLResponse)?.statusCode ?? 0
            if (200..<300).contains(status), let data,
               let response = try? JSONDecoder().decode(RemoteModeResponse.self, from: data) {
                DispatchQueue.main.async {
                    applySettings(response.preset.settings.map { ($0.key, $0.value) }, mode: mode)
                }
                return
            }
            let fallback = cachedCatalog()?.presets[mode]?.settings.map { ($0.key, $0.value) } ?? selected
            DispatchQueue.main.async {
                applySettings(fallback, mode: mode)
            }
        }.resume()
    }
}
