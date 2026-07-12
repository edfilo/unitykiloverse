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
        K1L0ApplyEnvironmentSnapshot([
            "solarAltitude": altitude * 180 / .pi,
            "solarAzimuth": (azimuth * 180 / .pi + 360).truncatingRemainder(dividingBy: 360),
            "bypassWeather": defaults.bool(forKey: "k1lo_native_layeredBypassWeather"),
            "effect": defaults.integer(forKey: "k1lo_native_layeredSkyEffect"),
            "cloudOpacity": defaults.object(forKey: "k1lo_native_layeredCloudOpacity") as? Double ?? 0.72,
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
