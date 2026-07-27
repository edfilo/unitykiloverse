import Foundation
import CoreLocation

enum NativeUnitySolarSync {
    private static var timer: Timer?

    static func start() {
        sync()
        guard timer == nil else { return }
        timer = Timer.scheduledTimer(withTimeInterval: 60, repeats: true) { _ in sync() }
    }

    static func sync() {
        guard let coordinate = K1L0CurrentSolarCoordinate() else { return }
        let julianDate = Date().timeIntervalSince1970 / 86400.0 + 2440587.5
        let days = julianDate - 2451545.0
        let meanLongitude = (280.460 + 0.9856474 * days).truncatingRemainder(dividingBy: 360)
        let anomaly = (357.528 + 0.9856003 * days) * .pi / 180
        let eclipticLongitude = (meanLongitude + 1.915 * sin(anomaly) + 0.020 * sin(2 * anomaly)) * .pi / 180
        let obliquity = (23.439 - 0.0000004 * days) * .pi / 180
        let rightAscension = atan2(cos(obliquity) * sin(eclipticLongitude), cos(eclipticLongitude))
        let declination = asin(sin(obliquity) * sin(eclipticLongitude))
        let gmst = (280.46061837 + 360.98564736629 * (julianDate - 2451545.0)).truncatingRemainder(dividingBy: 360)
        let hourAngle = (gmst + coordinate.longitude) * .pi / 180 - rightAscension
        let latitude = coordinate.latitude * .pi / 180
        let altitude = asin(sin(latitude) * sin(declination) + cos(latitude) * cos(declination) * cos(hourAngle))
        let azimuth = atan2(-sin(hourAngle), tan(declination) * cos(latitude) - sin(latitude) * cos(hourAngle))
        let defaults = UserDefaults.standard
        let bypass = defaults.bool(forKey: "k1lo_native_layeredBypassWeather")
        let testOverride = defaults.bool(forKey: "k1lo_native_testSkyOverride")
        let lookMode = defaults.string(forKey: "k1lo_native_weatherLookMode") ?? "auto"
        // Workshop presets carry explicit celestial coordinates. Do not derive
        // their lighting from a clock hour: the old sine approximation made
        // authored sunsets cross below the horizon and turn the sky black.
        // Auto always means live astronomy at the active map coordinate. Stale
        // workshop flags must not replace a GPS/fixed-location sunset for even
        // one timer cycle while Auto clears them below.
        let manualPreview = lookMode == "auto" ? false : (bypass || testOverride)
        let fallbackHour = defaults.object(forKey: "k1lo_native_manualHour") as? Double ?? 13.25
        let fallbackAltitude = sin((fallbackHour - 6.0) / 12.0 * .pi) * 62.0
        let fallbackAzimuth = (fallbackHour / 24.0 * 360.0 + 90.0).truncatingRemainder(dividingBy: 360.0)
        let workshopAltitude = defaults.object(forKey: "k1lo_native_workshopSolarAltitude") as? Double ?? fallbackAltitude
        let workshopAzimuth = defaults.object(forKey: "k1lo_native_workshopSolarAzimuth") as? Double ?? fallbackAzimuth
        let effectiveAltitude = manualPreview ? workshopAltitude : altitude * 180 / .pi
        let effectiveAzimuth = manualPreview ? workshopAzimuth : (azimuth * 180 / .pi + 360).truncatingRemainder(dividingBy: 360)
        // Auto blends the canonical Night and Day presets continuously. Night
        // keeps its visual lock but still receives live celestial positioning.
        if lookMode == "auto" {
            defaults.set(false, forKey: "k1lo_native_visualNightOverride")
            K1L0WeatherOverlayInstaller.setUnitySetting("visualNightOverride", "0")
            K1L0WeatherModeController.applyAutoForSolarAltitude(effectiveAltitude)
        } else if lookMode == "night" {
            defaults.set(true, forKey: "k1lo_native_visualNightOverride")
            K1L0WeatherOverlayInstaller.setUnitySetting("visualNightOverride", "1")
        }
        let liveCloudCover = defaults.object(forKey: "k1lo_native_liveCloudCover") as? Double ?? 35
        let liveCloudOpacity = min(0.88, max(0.08, liveCloudCover / 100.0))
        let liveCloudCoverage = min(1.0, max(0.0, liveCloudCover / 100.0))
        // Workshop presets store an explicit cloud-cover percentage too. That
        // value must drive procedural coverage just like live API weather;
        // otherwise an "Overcast" workshop inherits an unrelated local slider.
        defaults.set(altitude * 180 / .pi, forKey: "k1lo_native_liveSolarAltitude")
        defaults.set((azimuth * 180 / .pi + 360).truncatingRemainder(dividingBy: 360), forKey: "k1lo_native_liveSolarAzimuth")
        K1L0ApplyEnvironmentSnapshot([
            "solarAltitude": effectiveAltitude,
            "solarAzimuth": effectiveAzimuth,
            "solarTimestamp": Date().timeIntervalSince1970,
            "bypassWeather": manualPreview,
            "effect": defaults.integer(forKey: "k1lo_native_layeredSkyEffect"),
            "cloudOpacity": liveCloudOpacity,
            "cloudCoverage": liveCloudCoverage,
            "cloudSpeed": defaults.object(forKey: "k1lo_native_layeredCloudSpeed") as? Double ?? 0.08,
            "cloudScale": defaults.object(forKey: "k1lo_native_layeredCloudScale") as? Double ?? 2.2,
            "cloudContrast": defaults.object(forKey: "k1lo_native_layeredCloudContrast") as? Double ?? 1.5,
            "topHue": defaults.object(forKey: "k1lo_native_layeredSkyTopHue") as? Double ?? 0.62,
            "midHue": defaults.object(forKey: "k1lo_native_layeredSkyMidHue") as? Double ?? 0.76,
            "horizonHue": defaults.object(forKey: "k1lo_native_layeredSkyHorizonHue") as? Double ?? 0.94,
            "nightBlackness": defaults.object(forKey: "k1lo_native_layeredNightBlackness") as? Double ?? 0.72,
            "rain": defaults.object(forKey: "k1lo_native_layeredRain") as? Double ?? 0,
            "aurora": defaults.object(forKey: "k1lo_native_layeredAurora") as? Double ?? 0
        ])
    }
}
