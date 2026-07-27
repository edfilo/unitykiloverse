import Foundation

struct K1L0WeatherPresetDescriptor: Codable, Identifiable, Hashable {
    let id: String
    let label: String
    let revision: Int
}

private final class K1L0WeatherPresetBundleMarker {}

/// Authoritative Day/Night visual presets, their astronomy-driven Auto blend,
/// and per-renderer-section manual-override boundaries.
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
        ("nearFogEnabled", "0"),
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
    // Bump whenever the catalog schema/fallback contract changes so an old
    // cached recipe cannot outrank the clean bundled snapshot while offline.
    private static let cacheKey = "k1l0_weather_preset_catalog_v2"
    private static let preferredOrder = ["day", "night"]
    private static let supportedModes = Set(preferredOrder)

    private static let bundledCatalogSnapshot: RemoteCatalog? = {
        var bundles = [Bundle.main, Bundle(for: K1L0WeatherPresetBundleMarker.self)]
        if let overlayBundle = Bundle(identifier: "com.filowatt.K1L0.overlay") {
            bundles.insert(overlayBundle, at: 0)
        }
        for bundle in bundles {
            guard let url = bundle.url(forResource: "K1L0WeatherPresets", withExtension: "json"),
                  let data = try? Data(contentsOf: url),
                  let catalog = try? JSONDecoder().decode(RemoteCatalog.self, from: data)
            else { continue }
            return catalog
        }
        return nil
    }()

    static var bundledDescriptors: [K1L0WeatherPresetDescriptor] {
        bundledCatalogSnapshot.map(descriptors(for:)) ?? []
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
            let fallback = (cachedCatalog() ?? bundledCatalogSnapshot).map(descriptors(for:)) ?? []
            DispatchQueue.main.async { completion(fallback) }
        }.resume()
    }

    private static func cachedCatalog() -> RemoteCatalog? {
        guard let data = UserDefaults.standard.data(forKey: cacheKey) else { return nil }
        return try? JSONDecoder().decode(RemoteCatalog.self, from: data)
    }

    private static func descriptors(for catalog: RemoteCatalog) -> [K1L0WeatherPresetDescriptor] {
        let authored: [K1L0WeatherPresetDescriptor] = catalog.presets.compactMap { key, preset in
            guard supportedModes.contains(key) else { return nil }
            return .init(id: key, label: preset.label ?? key.replacingOccurrences(of: "_", with: " ").capitalized,
                         revision: preset.revision ?? 0)
        }.sorted {
            let lhs = preferredOrder.firstIndex(of: $0.id) ?? Int.max
            let rhs = preferredOrder.firstIndex(of: $1.id) ?? Int.max
            return lhs == rhs ? $0.label < $1.label : lhs < rhs
        }
        let revision = authored.map { $0.revision }.max() ?? 0
        return [.init(id: "auto", label: "Auto · Weather + Astronomy", revision: revision)] + authored
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

    static let itemFlightSection = "itemFlight"
    static let lightingSection = "lighting"
    static let windowsSection = "windowsBuildings"
    static let groundSection = "ground"
    static let bloomSection = "bloom"
    static let gradeSection = "colorGrade"
    static let postProcessingSection = "postProcessing"
    static let fogSection = "fog"

    private static let sectionSettings: [String: Set<String>] = [
      itemFlightSection: [
        "itemBaseSize", "itemViewportHeight", "itemMaxWorldSize",
        "itemInsectCruiseY", "itemInsectCeilingY", "itemInsectVisitInterval",
        "itemInsectApproachSeconds", "itemInsectApproachMeander",
        "itemInsectCameraClearance", "itemInsectInvestigationLift",
        "itemInsectCuriosityRadius", "itemInsectCuriositySpeed",
        "itemInsectHoverSeconds", "itemInsectReturnSeconds",
        "ambientItemSpotlightEnabled", "ambientItemSpotlightIntensity",
        "ambientItemSpotlightRange", "ambientItemSpotlightAngle"
      ],
      lightingSection: [
        "ambientEnabled", "ambientIntensity", "daySunIntensity",
        "moonlightEnabled", "moonlightManualOverride", "moonlightIntensity",
        "moonlightRed", "moonlightGreen", "moonlightBlue",
        "moonlightPitch", "moonlightYaw", "moonlightRoll",
        "spotlightEnabled", "spotlightIntensity",
        "reflectionsEnabled", "reflectionIntensity",
        "enableShadows", "shadowStrength", "shadowDistance",
      ],
      windowsSection: [
        "zossEmissiveIntensity", "zossDayWindowIntensity",
        "zossEmissiveHue", "zossEmissiveSaturation",
        "zossNightEmissiveHue", "zossNightEmissiveSaturation",
        "zossEmissiveSmoothness", "zossEmissiveMetallic",
        "zossLitFraction", "zossPaletteMix", "zossPaletteSaturation",
        "zossPaletteSaturation_night", "zossWarmth", "zossAccentFraction",
        "zossWindowBrightness", "zossBrightnessJitter",
        "zossBrightnessJitterRate", "zossWallValue",
        "zossWallSaturation", "zossWallDaylightLift", "zossWallVariance",
        "roadValue", "dayRoadValue", "vaporDayPink"
      ],
      groundSection: [
        "groundHue", "groundSaturation", "groundValue", "groundBrightness",
        "groundHue_night", "groundSaturation_night", "roadSaturation", "roadGlow"
      ],
      bloomSection: [
        "bloomEnabled", "bloomIntensity", "dayBloomIntensity",
        "bloomThreshold", "bloomScatter"
      ],
      gradeSection: [
        "mapBrightness", "exposureFixedValue",
        "saturation", "contrast", "temperature", "tint", "hueShift"
      ],
      postProcessingSection: [
        "vignetteEnabled", "vignetteIntensity", "vignetteSmoothness",
        "chromaticEnabled", "chromaticIntensity", "dofEnabled",
        "focusDistance", "aperture", "focalLength", "motionBlurEnabled",
        "motionBlurIntensity", "filmGrainEnabled", "filmGrainIntensity"
      ]
    ]

    private static let fogManualOverrideKey = "k1lo_native_fogManualOverride"

    static var fogManualOverrideActive: Bool {
        UserDefaults.standard.bool(forKey: fogManualOverrideKey)
    }

    static func isFogSetting(_ key: String) -> Bool {
        key == "nearFogEnabled" || key == "volumetricFogEnabled" || key.hasPrefix("fog")
    }

    static func section(forSetting key: String) -> String? {
        if isFogSetting(key) { return fogSection }
        return sectionSettings.first(where: { $0.value.contains(key) })?.key
    }

    private static func overrideKey(for section: String) -> String {
        section == fogSection ? fogManualOverrideKey : "k1lo_native_sectionManualOverride_\(section)"
    }

    static func sectionManualOverrideActive(_ section: String) -> Bool {
        UserDefaults.standard.bool(forKey: overrideKey(for: section))
    }

    static func sectionManualOverrideActive(forSetting key: String) -> Bool {
        guard let section = section(forSetting: key) else { return false }
        return sectionManualOverrideActive(section)
    }

    static func beginSectionManualOverride(forSetting key: String) {
        guard let section = section(forSetting: key) else { return }
        UserDefaults.standard.set(true, forKey: overrideKey(for: section))
    }

    static func clearSectionManualOverride(_ section: String) {
        UserDefaults.standard.set(false, forKey: overrideKey(for: section))
    }

    /// Removes the device's cached values for one renderer family. The caller
    /// then reapplies the API section or active Day/Night/Auto recipe.
    static func resetLocalValues(for section: String) {
        let defaults = UserDefaults.standard
        var keys: Set<String>
        if section == fogSection {
            keys = Set(defaults.dictionaryRepresentation().keys.filter {
                $0.hasPrefix("k1lo_native_fog")
            })
            keys.insert("k1lo_native_nearFogEnabled")
            keys.insert("k1lo_native_volumetricFogEnabled")
        } else {
            keys = Set((sectionSettings[section] ?? []).map { "k1lo_native_\($0)" })
        }
        if section == itemFlightSection {
            keys.formUnion([
                "k1lo_native_itemInsectCameraClearanceV3",
                "k1lo_native_itemInsectHoverSecondsV2",
                "k1lo_native_itemInsectReturnSecondsV3"
            ])
        }
        keys.insert(overrideKey(for: section))
        for key in keys { defaults.removeObject(forKey: key) }
        if section != fogSection && section != itemFlightSection {
            defaults.removeObject(forKey: "k1lo_native_lookManualOverride")
        }
    }

    static func beginFogManualOverride() {
        beginSectionManualOverride(forSetting: "fogDensity")
    }

    static func clearFogManualOverride() {
        clearSectionManualOverride(fogSection)
    }

    // Compatibility for the retired combined Look panel. The visible UI now
    // uses independent section authority, so these wrappers cannot create a
    // second competing override model.
    static var lookManualOverrideActive: Bool {
        [lightingSection, windowsSection, groundSection, bloomSection,
         gradeSection, postProcessingSection].contains { sectionManualOverrideActive($0) }
    }

    static func isLookSetting(_ key: String) -> Bool {
        guard let section = section(forSetting: key) else { return false }
        return section != fogSection && section != itemFlightSection
    }

    static func beginLookManualOverride() {
        for section in [lightingSection, windowsSection, groundSection, bloomSection,
                        gradeSection, postProcessingSection] {
            UserDefaults.standard.set(true, forKey: overrideKey(for: section))
        }
    }

    static func clearLookManualOverride() {
        for section in [lightingSection, windowsSection, groundSection, bloomSection,
                        gradeSection, postProcessingSection] {
            clearSectionManualOverride(section)
        }
    }

    private static func applySettings(_ settings: [Setting], mode: String, syncEnvironment: Bool = true) {
        for (key, value) in resetBaseline + common + settings + workshopContext(for: mode) {
            // An active settings-panel fog workspace remains authoritative
            // across periodic preset/astronomy updates. Publish or Revert
            // clears this flag and reconnects the selected preset.
            if sectionManualOverrideActive(forSetting: key) { continue }
            if let number = Double(value) {
                UserDefaults.standard.set(number, forKey: "k1lo_native_\(key)")
            } else {
                UserDefaults.standard.set(value, forKey: "k1lo_native_\(key)")
            }
            K1L0WeatherOverlayInstaller.setUnitySetting(key, value)
        }
        // Every mode must immediately push its celestial/weather snapshot.
        // Previously only Auto, Night, and Day synchronized here, so workshop
        // sunsets retained the prior mode's black sky despite storing a new sun.
        if syncEnvironment {
            NativeUnityLightingSync.sync()
            NativeUnitySolarSync.sync()
        }
    }

    private static let autoDiscreteKeys: Set<String> = [
        "testSkyOverride", "layeredBypassWeather", "solarWorldOverride",
        "visualNightOverride", "manualWeatherOverrideEnabled", "nearFogEnabled",
        "fogConstantDensity", "fogDistantFog", "fogNativeLights",
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

    /// Enforce real minimum/maximum constraints without replacing the authored
    /// Day/Night interpolation. This keeps Live atmospheric while allowing its
    /// color, lighting, fog and bloom to continue changing with astronomy.
    private static func constrainedLiveSettings(_ settings: [Setting]) -> [Setting] {
        var values = Dictionary(uniqueKeysWithValues: settings)
        func raise(_ key: String, to minimum: Double) {
            values[key] = String(format: "%.6f", max(Double(values[key] ?? "") ?? minimum, minimum))
        }
        func lower(_ key: String, to maximum: Double) {
            values[key] = String(format: "%.6f", min(Double(values[key] ?? "") ?? maximum, maximum))
        }
        values["fogConstantDensity"] = "0"
        values["fogDistantFog"] = "1"
        values["bloomEnabled"] = "1"
        // The API preset owns the fog renderer toggle. In particular, Auto
        // must not silently turn it back on after Day/Night disabled it.
        // Preserve authored fog values so an explicitly attached Fog workspace
        // can still experiment with the renderer without preset interference.
        raise("fogBrightness", to: 0.050)
        raise("fogScatteringIntensity", to: 0.08)
        raise("fogNoiseStrength", to: 0.16)
        raise("fogNoiseScale", to: 10)
        raise("fogDistantDensity", to: 0.00055)
        lower("fogDistantStart", to: 145)
        raise("fogV2DistantDiffusion", to: 0.24)
        raise("bloomIntensity", to: 1.65)
        raise("dayBloomIntensity", to: 1.65)
        lower("bloomThreshold", to: 0.78)
        raise("bloomScatter", to: 0.66)
        raise("zossEmissiveIntensity", to: 8.2)
        raise("zossDayWindowIntensity", to: 8.2)
        raise("zossWindowBrightness", to: 1.38)
        return values.keys.sorted().map { ($0, values[$0]!) }
    }

    /// Auto is not a third authored look. It continuously blends the canonical
    /// Night and Day presets using the live solar altitude (-6° night, +8° day).
    static func applyAutoForSolarAltitude(_ altitude: Double) {
        guard UserDefaults.standard.string(forKey: "k1lo_native_weatherLookMode") == "auto",
              let day = autoDaySettings, let night = autoNightSettings else { return }
        let dayness = (altitude + 6.0) / 14.0
        let settings = constrainedLiveSettings(
            interpolatedAutoSettings(day: day, night: night, dayness: dayness))
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
            if remoteCatalog != nil, let data {
                UserDefaults.standard.set(data, forKey: cacheKey)
            }
            let catalog = remoteCatalog ?? cachedCatalog() ?? bundledCatalogSnapshot
            guard let day = catalog?.presets["day"]?.settings,
                  let night = catalog?.presets["night"]?.settings else { return }
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

    static func apply(_ requestedMode: String) {
        refreshGlobalSettingSections()
        let mode: String
        switch requestedMode {
        case "radioactive": mode = "day"
        case "midnight": mode = "night"
        case "day", "night", "auto": mode = requestedMode
        default: mode = "auto"
        }
        UserDefaults.standard.set(mode, forKey: "k1lo_native_weatherLookMode")
        if mode == "auto" {
            applyAuto()
            return
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
            let fallback = (cachedCatalog() ?? bundledCatalogSnapshot)?
                .presets[mode]?.settings.map { ($0.key, $0.value) }
            guard let fallback else { return }
            DispatchQueue.main.async {
                applySettings(fallback, mode: mode)
            }
        }.resume()
    }

    private struct RemoteSettingSection: Codable {
        let label: String?
        let revision: Int?
        let settings: [String: String]
    }

    private struct RemoteSettingSectionCatalog: Codable {
        let sections: [String: RemoteSettingSection]
    }

    private static let settingSectionsEndpoint = URL(string: "https://api-tunnel.kilo.gallery/api/k1l0/setting-sections")!
    private static let settingSectionsCacheKey = "k1lo_setting_sections_v1"

    static func refreshGlobalSettingSections(_ onlySection: String? = nil) {
        var request = URLRequest(url: settingSectionsEndpoint)
        request.cachePolicy = .reloadIgnoringLocalAndRemoteCacheData
        request.timeoutInterval = 8
        URLSession.shared.dataTask(with: request) { data, response, _ in
            let status = (response as? HTTPURLResponse)?.statusCode ?? 0
            let remote = (200..<300).contains(status) && data != nil
                ? try? JSONDecoder().decode(RemoteSettingSectionCatalog.self, from: data!)
                : nil
            if remote != nil, let data { UserDefaults.standard.set(data, forKey: settingSectionsCacheKey) }
            let cached = UserDefaults.standard.data(forKey: settingSectionsCacheKey)
                .flatMap { try? JSONDecoder().decode(RemoteSettingSectionCatalog.self, from: $0) }
            guard let catalog = remote ?? cached else { return }
            DispatchQueue.main.async {
                for (section, value) in catalog.sections where onlySection == nil || section == onlySection {
                    guard !sectionManualOverrideActive(section) else { continue }
                    for (key, raw) in value.settings { applyGlobalSectionSetting(key, raw) }
                }
            }
        }.resume()
    }

    private static func applyGlobalSectionSetting(_ key: String, _ raw: String) {
        let aliases: [String: String] = [
            "itemInsectCameraClearance": "k1lo_native_itemInsectCameraClearanceV3",
            "itemInsectHoverSeconds": "k1lo_native_itemInsectHoverSecondsV2",
            "itemInsectReturnSeconds": "k1lo_native_itemInsectReturnSecondsV3"
        ]
        let defaultsKey = aliases[key] ?? "k1lo_native_\(key)"
        if let number = Double(raw) { UserDefaults.standard.set(number, forKey: defaultsKey) }
        else { UserDefaults.standard.set(raw, forKey: defaultsKey) }
        K1L0WeatherOverlayInstaller.setUnitySetting(key, raw)
    }
}
