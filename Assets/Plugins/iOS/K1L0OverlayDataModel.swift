import SwiftUI
import AVFoundation
import CoreLocation
import MapKit
import MetalKit
import CoreMedia
import Metal
#if canImport(UIKit)
import UIKit
import CoreMotion
#elseif canImport(AppKit)
import AppKit
#endif

final class K1L0OverlayDataModel: NSObject, ObservableObject, CLLocationManagerDelegate {
    static weak var activeModel: K1L0OverlayDataModel?
    // Astronomy follows the map location, not the GPS toggle. A selected fixed
    // location is authoritative even if a stale live GPS fix is still cached;
    // Live GPS falls through to the latest Core Location coordinate.
    var solarCoordinate: CLLocationCoordinate2D? {
        fixedLocationForCurrentMode()?.coordinate ?? currentLocation?.coordinate
    }
    // MapKit POI search wants the *player's effective location*: in preset
    // mode this follows sim-steps away from the preset origin, in Live GPS
    // it's the latest Core Location fix. Preset origin is only the fallback
    // for the first frame before currentLocation is seeded.
    var playerCoordinate: CLLocationCoordinate2D? {
        currentLocation?.coordinate ?? fixedLocationForCurrentMode()?.coordinate
    }

    @Published var liveSteps = 0 {
        didSet {
            guard liveSteps != oldValue else { return }
            noteRecentStepChange(from: oldValue, to: liveSteps)
            if !isApplyingUnitySimulatedStepState {
                advanceFixedTestLocation(from: oldValue, to: liveSteps)
            }
            normalizeIncomingBaselinesForLiveSteps()
            handleLiveStepsChanged()
        }
    }
    @Published var steps24h = 0
    @Published var steps7d = 0
    @Published var cityText = ""
    @Published var countryText = ""
    @Published var weatherText = "K1L0"
    @Published var weatherGlyph = "cloud.sun.fill"
    @Published var places: [OverlayPlace] = []
    @Published var placesDatasetStatus = "mapkit"
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
    @Published var artifactDetailSelection: OverlayArtifactDetailSelection?
    var hasArtifactDetailSelection: Bool {
        if case .some = artifactDetailSelection { return true }
        return false
    }
    @Published var receiveProgressSteps = 0
    @Published var receiveSignalStatus = "scanning signals"
    @Published var renderReady = false
    @Published var renderLoadingDetail = "starting map"
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
    private var locationHologramRetryCount = 0

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
    private let motionActivityManager = CMMotionActivityManager()
    private var pedometerSessionStart: Date?
    private var pedometerSessionRefreshInFlight = false
    private var pedometerSessionTimer: Timer?
    private var pedometerStatsTimer: Timer?
    private var motionSaysPedestrian = false
    private var motionSaysVehicle = false
    private var vehicleSpeedUntil = Date.distantPast
    private var lastRecentStepAt = Date.distantPast
#endif
		    private var currentLocation: CLLocation?
    private var isApplyingUnitySimulatedStepState = false
    private var simulatedLocationStepBaseline = 0
    private var lastSimulatedLocationPushAt = Date.distantPast
	    private var lastPlaceTileKeys = Set<String>()
	    private var lastPlacePrimaryTileKey: String?
	    private var lastPlaceHalfHourBucket: Int?
    private var lastPlaceFetchLocation: CLLocation?
    private var didFetchNearby = false
    private var nearbyRefreshTimer: Timer?
    private var lastIdleBeamFetchAt = Date.distantPast
    private var ambientSpawnRequestInFlight = false
    private var lastAmbientSpawnAttemptAt = Date.distantPast
    private var pendingAmbientSpawnAPIBase: String?
    private var pendingAmbientSpawnOrigin: CLLocation?
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
    // Nearby location chips use a wider acquisition cone plus hysteresis. A
    // noisy compass reading near the edge must not make the chip flash or swap
    // rapidly between neighboring places.
    private var retainedLocationMarqueePlaceId: String?
    private var retainedLocationMarqueeLastEligibleAt = Date.distantPast
    private let locationMarqueeDirectEnterDegrees = 60.0
    private let locationMarqueeTowardEnterDegrees = 85.0
    private let locationMarqueeExitDegrees = 105.0
    private let locationMarqueeExitGraceSeconds: TimeInterval = 6.0
    private var walkingTowardUntil: [String: Date] = [:]
    private var walkingAwayStartSteps: [String: Int] = [:]
    private var walkingAwaySampleCounts: [String: Int] = [:]
    private var beamDistanceHistory: [String: [Double]] = [:]
    private var relocatingBeamIds = Set<String>()
    private var dismissedBeamIds = Set<String>()
    private var collectingBeamIds = Set<String>()
    private var collectingPlaceIds = Set<String>()
    private static let locationDwellRecordsKey = "k1lo_native_locationDwellRecords_v2"
    private static let collectedPlaceItemsKey = "k1lo_native_collectedPlaceItems_v2"
    private static let locationDwellResumeWindow: TimeInterval = 30 * 60
    private struct PersistedLocationDwell: Codable {
        let itemKey: String
        let startedAt: TimeInterval
        var lastConfirmedAt: TimeInterval
    }
    private var persistedLocationDwells: [String: PersistedLocationDwell] = {
        guard let data = UserDefaults.standard.data(forKey: K1L0OverlayDataModel.locationDwellRecordsKey),
              let records = try? JSONDecoder().decode([String: PersistedLocationDwell].self, from: data) else { return [:] }
        return records
    }()
    private var collectedPlaceItemKeys: Set<String> = {
        Set(UserDefaults.standard.stringArray(forKey: K1L0OverlayDataModel.collectedPlaceItemsKey) ?? [])
    }()
    private var locationDwellStartedAt: [String: Date] = [:]
    private var didApplyInitialDenseLocationFilter = false
    private var locationFilterGeneration = 0
    // Unity owns location-to-footprint pairing and the enter/exit decision.
    // Swift retains only the display/timer state for the active place.
    private let locationDwellDuration: TimeInterval = 10 * 60
    private var receiveUnlockedIds = Set<String>()
    private var isFetchingIncomingTransmission = false
    private static let incomingWaitBaselineKey = "k1lo_native_incomingWaitBaselineSteps_v1"
    private static let incomingTuneBaselineKey = "k1lo_native_incomingTuneBaselineSteps_v1"
    private static let incomingTuneSignalIdKey = "k1lo_native_incomingTuneSignalId_v1"
    private static let incomingSeedRequestAtKey = "k1lo_native_incomingSeedRequestAt_v1"
    static let locationDropFilterKey = "k1lo_native_locationDropFilter_v1"
    static let locationBeamCategoriesKey = "k1lo_native_locationBeamCategories_v1"
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
        let country: String?
        let tempF: Double?
        let glyph: String
        let isDay: Bool?
        let cloudCover: Double?

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
        K1L0StepValueText(liveSteps)
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

    private func getKeepWalkingString(base: String) -> String {
        if liveSteps < 200 {
            if base == "KEEP WALKING" {
                return "YOU ONLY TOOK \(K1L0StepText(liveSteps).uppercased())... KEEP WALKING"
            } else if base == "Keep walking" {
                return "You only took \(K1L0StepText(liveSteps))... keep walking"
            } else {
                return "you only took \(K1L0StepText(liveSteps))... keep walking"
            }
        } else {
            // The nag is for the sedentary only. Anyone past 200 steps is
            // clearly walking — acknowledge the distance, don't lecture.
            if base == "KEEP WALKING" {
                return "YOU TOOK \(K1L0StepText(liveSteps).uppercased())"
            } else if base == "Keep walking" {
                return "You took \(K1L0StepText(liveSteps))"
            } else {
                return "you took \(K1L0StepText(liveSteps))"
            }
        }
    }

    var ctaText: String {
        if let beam = activePursuedBeam {
            return "AMBIENT · \(distanceText(to: beam).uppercased())"
        }
        guard liveSteps > 0 else { return "WALK" }
        let remaining = signalAcquisitionRemainingSteps()
        let kw = getKeepWalkingString(base: "KEEP WALKING")
        return remaining > 0 ? "\(kw) · SIGNAL IN \(K1L0StepText(remaining).uppercased())" : "\(kw) · SEARCHING"
    }

    var walkingSkyAlertBeam: OverlayBeam? { activePursuedBeam }

    var walkingSkyAlertText: String {
        guard liveSteps > 0 else {
            let duration = liveStepDurationText
                .replacingOccurrences(of: "last ", with: "")
                .trimmingCharacters(in: .whitespacesAndNewlines)
            return "inactive for \(duration).   Please walk."
        }
        let kw = getKeepWalkingString(base: "keep walking")
        if let beam = activePursuedBeam {
            return "\(kw)\n\(beam.teaserText.lowercased())"
        }
        let remaining = signalAcquisitionRemainingSteps()
        if remaining > 0 {
            return "\(kw)\(animatedDots)"
        }
        let status = receiveSignalStatus
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .lowercased()
        if !status.isEmpty && !status.hasPrefix("walk ") {
            return "\(kw)\n\(animatedSignalStatus(status))"
        }
        return "\(kw)\(animatedDots)"
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
            return liveSteps < 200 ? "keep walking\(animatedDots)" : "searching\(animatedDots)"
        }
        if trimmed == "scanning signals" || trimmed.contains("searching") {
            return liveSteps < 200 ? "keep walking\(animatedDots)" : "searching\(animatedDots)"
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

    func handleEnvironmentState(_ json: String) {
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
        // DESIGN: while the player is dwelling at a location, ambient beams are
        // deliberately NOT advertised — the location visit is the activity
        // (hang out, collect the location item, transmit from there).
        guard collectCandidatePlace == nil else { return nil }
        if let beam = activePursuedBeam {
            return beam
        }
        if isPedestrianActivityConfirmed { return nearestBeam }
        // Idle and NOT in a vehicle (sitting at home): heading is meaningless,
        // so advertise the nearest live beam in any direction — bait to get up
        // and go outside. Client-positioned ambient beams live 50–150m away;
        // vehicle rides still hide beams entirely.
        let stationary = max(0, currentLocation?.speed ?? 0) <= 1.0
        if !isLikelyInVehicle && stationary {
            return beams
                .filter { !isExpired($0) && distanceMeters(to: $0) >= 45 }
                .sorted { distanceMeters(to: $0) < distanceMeters(to: $1) }
                .first
        }
        return nil
    }

    var activePursuedBeam: OverlayBeam? {
        guard incomingTransmission == nil else { return nil }
        if let collectCandidateBeam, !isExpired(collectCandidateBeam) {
            return collectCandidateBeam
        }
        // An ambient item remains advertised in every direction. Direction is
        // guidance, not a visibility gate; sustained walking-away relocation
        // below is what eventually replaces a stale behind-the-player item.
        return nearestBeam
    }

    func filteredPlaces(for filter: String) -> [OverlayPlace] {
        let normalized = filter.lowercased()
        let visible = normalized == "all"
            ? places
            : places.filter { placeCategory($0) == normalized }
        return visible.sorted { distanceMeters(to: $0) < distanceMeters(to: $1) }
    }

    func applyLocationFilter(_ encodedCategories: String? = nil) {
        locationFilterGeneration += 1
        let generation = locationFilterGeneration
        let stored = encodedCategories
            ?? UserDefaults.standard.string(forKey: Self.locationBeamCategoriesKey)
            ?? "coffee,drinks"
        let selectedCategories = normalizedLocationCategories(stored)
        let filtered = places
            .filter { selectedCategories.contains(placeCategory($0)) }
            .sorted { distanceMeters(to: $0) < distanceMeters(to: $1) }
        func nativeEntry(_ place: OverlayPlace) -> [String: Any] {
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
                "artifactPoolItemId": place.artifactPoolItemId ?? "",
                "artifactLore": place.artifactTeaser ?? "",
                "artifactContainer": "",
                "artifactSenderName": ""
            ]
            if let imageUrl = place.imageUrl, !imageUrl.isEmpty {
                entry["imageUrl"] = imageUrl
            }
            if let depthMapUrl = place.depthMapUrl, !depthMapUrl.isEmpty {
                entry["depthMapUrl"] = depthMapUrl
            }
            if let placeId = place.placeId {
                entry["placeId"] = placeId
            }
            if let featureId = place.buildingFeatureId, !featureId.isEmpty {
                entry["buildingFeatureId"] = featureId
            }
            if let tileKey = place.buildingTileKey, !tileKey.isEmpty {
                entry["buildingTileKey"] = tileKey
            }
            return entry
        }
        let payloadPlaces = filtered.map(nativeEntry)

        let payload: [String: Any] = [
            "ok": true,
            "includePlaces": true,
            "includeBeams": false,
            "places": payloadPlaces,
            "beams": []
        ]

        guard let data = try? JSONSerialization.data(withJSONObject: payload),
              let json = String(data: data, encoding: .utf8) else { return }
        let locationCatalogPayload: [String: Any] = ["places": places.map(nativeEntry)]
        guard let catalogData = try? JSONSerialization.data(withJSONObject: locationCatalogPayload),
              let catalogJson = String(data: catalogData, encoding: .utf8) else { return }
        // Unity objects can finish initializing after the first nearby response.
        // Replay this idempotent place snapshot a few times so the scanner cannot
        // miss it; this is the same resend a category-filter toggle used to cause.
        [0.0, 0.45, 1.25].forEach { delay in
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) { [weak self] in
                guard self?.locationFilterGeneration == generation else { return }
                K1L0WeatherOverlayInstaller.applyNativeWorldNearby(json)
                K1L0WeatherOverlayInstaller.applyNativeLocationCatalog(catalogJson)
            }
        }
    }

    private func applyInitialDenseLocationFilterIfNeeded(_ incomingPlaces: [OverlayPlace]) {
        guard !didApplyInitialDenseLocationFilter else { return }
        didApplyInitialDenseLocationFilter = true
        guard incomingPlaces.count > 20 else { return }

        // Beta scope: bars + coffee only, no food/restaurant filter option.
        let candidates = ["coffee", "drinks"]
        let counts = Dictionary(grouping: incomingPlaces, by: placeCategory).mapValues(\.count)
        guard let selected = candidates.max(by: { (counts[$0] ?? 0) < (counts[$1] ?? 0) }) else { return }

        UserDefaults.standard.set(selected, forKey: Self.locationDropFilterKey)
        UserDefaults.standard.set(selected, forKey: Self.locationBeamCategoriesKey)
        let coffeeCount = counts["coffee"] ?? 0
        let barCount = counts["drinks"] ?? 0
        print("[K1L0Overlay] dense nearby init: \(incomingPlaces.count) open places; auto-selected \(selected) (coffee=\(coffeeCount), bar=\(barCount))")
    }

    func homeMarqueeItems() -> [K1L0MarqueeItem] {
        var rows: [K1L0MarqueeItem] = []
        rows.append(K1L0MarqueeItem(
            id: "walking-status",
            kind: "status",
            line1: liveSteps > 0 ? "Keep walking" : "Walk",
            line2: liveSteps > 0 ? "\(K1L0StepText(liveSteps)) \(liveStepDurationText)" : "Idle for \(inactiveDurationPlainText)",
            distanceText: nil,
            relativeBearing: nil,
            progress: nil
        ))

        var ambientLocked = false

        let visibleAmbientBeams = beams
            .filter { !isExpired($0) }
            .sorted { distanceMeters(to: $0) < distanceMeters(to: $1) }
        if !visibleAmbientBeams.isEmpty { ambientLocked = true }
        for beam in visibleAmbientBeams {
            let itemId = "beam:\(beam.id)"
            rows.append(K1L0MarqueeItem(
                id: itemId,
                kind: beam.rewardType?.lowercased() == "object" ? "ambientObject" : "ambientElement",
                line1: "Nearby artifact",
                // Match the map notification: thumbnail + title only.
                line2: "",
                distanceText: distanceText(to: beam),
                relativeBearing: relativeBearingDegrees(to: beam),
                progress: nil,
                imageUrl: beam.imageUrl
            ))
        }

        if !ambientLocked, let place = bestLocationMarqueeCandidate() {
            let itemId = "place:\(place.placeId ?? place.id)"
            rows.append(K1L0MarqueeItem(
                id: itemId,
                kind: "location",
                line1: place.name,
                line2: locationMarqueeInstruction(for: place),
                distanceText: distanceText(to: place),
                relativeBearing: relativeBearingDegrees(to: place),
                progress: nil,
                imageUrl: place.imageUrl
            ))
        }

        return rows
    }

    func mapMarqueeItems() -> [K1L0MarqueeItem] {
        var rows: [K1L0MarqueeItem] = []
        rows.append(K1L0MarqueeItem(
            id: "walking-status",
            kind: "status",
            line1: liveSteps > 0 ? "Keep walking" : "Walk",
            line2: liveSteps > 0 ? "\(K1L0StepText(liveSteps)) \(liveStepDurationText)" : "Idle for \(inactiveDurationPlainText)",
            distanceText: nil,
            relativeBearing: nil,
            progress: nil
        ))

        for beam in beams
            .filter({ !isExpired($0) })
            .sorted(by: { distanceMeters(to: $0) < distanceMeters(to: $1) }) {
            let itemId = "beam:\(beam.id)"
            rows.append(K1L0MarqueeItem(
                id: itemId,
                kind: beam.rewardType?.lowercased() == "object" ? "ambientObject" : "ambientElement",
                // Uniform ambient banner: thumbnail + "Nearby artifact" only. The
                // teaser made banners inconsistent (some beams have one, some
                // don't); the teaser still shows on the collect card itself.
                line1: "Nearby artifact",
                line2: "",
                distanceText: distanceText(to: beam),
                relativeBearing: relativeBearingDegrees(to: beam),
                progress: nil,
                imageUrl: beam.imageUrl
            ))
        }

        if let place = bestLocationMarqueeCandidate() {
            let itemId = "place:\(place.placeId ?? place.id)"
            rows.append(K1L0MarqueeItem(
                id: itemId,
                kind: "location",
                line1: place.name,
                line2: locationMarqueeInstruction(for: place),
                distanceText: distanceText(to: place),
                relativeBearing: relativeBearingDegrees(to: place),
                progress: nil,
                imageUrl: place.imageUrl
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
        guard isPedestrianActivityConfirmed else {
            clearRetainedLocationMarqueeCandidate()
            return nil
        }
        // Presence takes precedence over proximity. While any location dwell is
        // active, do not also advertise a second "nearby location" chip.
        guard collectCandidatePlace == nil, locationDwellStartedAt.isEmpty else {
            clearRetainedLocationMarqueeCandidate()
            return nil
        }
        let nearestBeamDistance = nearestForwardBeam.map { distanceMeters(to: $0) } ?? .greatestFiniteMagnitude
        let currentTime = now

        // Keep the selected place through a wider exit cone and slightly wider
        // distance boundary. Once it truly leaves that envelope, decay for a
        // few seconds before removing it. This is deliberately separate from
        // entry eligibility so GPS/compass noise cannot flicker the chip.
        if let retainedId = retainedLocationMarqueePlaceId,
           let retained = places.first(where: { locationMarqueeId(for: $0) == retainedId }) {
            let meters = distanceMeters(to: retained)
            let steps = estimatedSteps(forMeters: meters)
            let bearing = abs(Self.normalizedSignedDegrees(relativeBearingDegrees(to: retained)))
            let remainsEligible = steps <= 550
                && meters < nearestBeamDistance + 20
                && locationDwellStartedAt[retained.id] == nil
                && collectCandidatePlace?.id != retained.id
                && bearing <= locationMarqueeExitDegrees

            if remainsEligible {
                retainedLocationMarqueeLastEligibleAt = currentTime
                return retained
            }
            if currentTime.timeIntervalSince(retainedLocationMarqueeLastEligibleAt) <= locationMarqueeExitGraceSeconds {
                return retained
            }
        }

        let candidate = places
            .filter { place in
                let meters = distanceMeters(to: place)
                let steps = estimatedSteps(forMeters: meters)
                let itemId = "place:\(locationMarqueeId(for: place))"
                return meters < nearestBeamDistance
                    && steps <= 500
                    && locationDwellStartedAt[place.id] == nil
                    && collectCandidatePlace?.id != place.id
                    && isWalkingTowardItem(itemId, relativeBearing: relativeBearingDegrees(to: place))
            }
            .sorted { distanceMeters(to: $0) < distanceMeters(to: $1) }
            .first

        if let candidate {
            retainedLocationMarqueePlaceId = locationMarqueeId(for: candidate)
            retainedLocationMarqueeLastEligibleAt = currentTime
        } else {
            clearRetainedLocationMarqueeCandidate()
        }
        return candidate
    }

    private func locationMarqueeId(for place: OverlayPlace) -> String {
        place.placeId ?? place.id
    }

    private func clearRetainedLocationMarqueeCandidate() {
        retainedLocationMarqueePlaceId = nil
        retainedLocationMarqueeLastEligibleAt = .distantPast
    }

    private func estimatedSteps(forMeters meters: Double) -> Int {
        max(1, Int((max(0, meters) * 1.3).rounded()))
    }

    private func locationMarqueeInstruction(for place: OverlayPlace) -> String {
        "Nearby"
    }

    private func stepsText(toMeters meters: Double) -> String {
        K1L0StepText(estimatedSteps(forMeters: meters))
    }

    private func isWalkingTowardItem(_ itemId: String, relativeBearing: Double) -> Bool {
        guard isPedestrianActivityConfirmed else { return false }
        let normalized = abs(Self.normalizedSignedDegrees(relativeBearing))
        if normalized <= locationMarqueeDirectEnterDegrees { return true }
        return worldItemDistanceTrend[itemId] == "toward"
            && normalized <= locationMarqueeTowardEnterDegrees
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
        case "drinks": return "🍸"
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
        updateVehicleSpeedState(location)
        NativeUnitySolarSync.sync()
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
        // Fixed-location navigation is owned by Unity player yaw. Physical
        // phone orientation must not fight horizontal swipe navigation.
        guard !isUsingFixedTestLocation else { return }
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

    func distanceText(to tap: OverlayFloatingItemTap) -> String {
        guard tap.hasValidCoordinate, let currentLocation else {
            return formatDistance(tap.distanceMeters)
        }
        return formatDistance(currentLocation.distance(from: CLLocation(latitude: tap.latitude, longitude: tap.longitude)))
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

    func relativeBearingDegrees(to tap: OverlayFloatingItemTap) -> Double {
        guard tap.hasValidCoordinate, let currentLocation else { return 0 }
        return Self.bearingDegrees(
            from: currentLocation.coordinate,
            to: CLLocationCoordinate2D(latitude: tap.latitude, longitude: tap.longitude)
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
                refreshPedometerSession(forceRestart: true)
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
        pedometer.stopUpdates()
        pedometerSessionRefreshInFlight = false
        startHeadingUpdates()
    #endif
        let location = CLLocation(latitude: preset.latitude, longitude: preset.longitude)
        currentLocation = location
        // Fixed coordinates are still live astronomical coordinates. Push the
        // new sun/moon state immediately instead of retaining the prior map's
        // sky until the weather request or minute timer completes.
        NativeUnitySolarSync.sync()
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
        retainedLocationMarqueePlaceId = nil
        retainedLocationMarqueeLastEligibleAt = .distantPast
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
        startMotionActivityDetection()
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

    private func noteRecentStepChange(from oldValue: Int, to newValue: Int) {
#if os(iOS)
        // Ignore the initial bulk session restore. Subsequent positive deltas
        // are evidence that GPS displacement came from a person walking.
        if oldValue > 0 && newValue > oldValue {
            lastRecentStepAt = Date()
        }
#endif
    }

#if os(iOS)
    private func startMotionActivityDetection() {
        guard CMMotionActivityManager.isActivityAvailable() else { return }
        motionActivityManager.stopActivityUpdates()
        motionActivityManager.startActivityUpdates(to: .main) { [weak self] activity in
            guard let self, let activity else { return }
            let confident = activity.confidence != .low
            self.motionSaysVehicle = confident && (activity.automotive || activity.cycling)
            self.motionSaysPedestrian = confident && (activity.walking || activity.running)
            if self.motionSaysVehicle {
                self.vehicleSpeedUntil = Date().addingTimeInterval(30)
                self.suppressWalkOnlyPopups()
            }
        }
    }

    private func updateVehicleSpeedState(_ location: CLLocation) {
        // Above 4.5 m/s is beyond the intended walking-game pace and catches a
        // car before Core Motion has confidently classified it as automotive.
        if location.speed >= 4.5 {
            vehicleSpeedUntil = Date().addingTimeInterval(30)
            suppressWalkOnlyPopups()
        }
    }
#else
    private func updateVehicleSpeedState(_ location: CLLocation) {}
#endif

    private var isLikelyInVehicle: Bool {
#if os(iOS)
        guard !isUsingFixedTestLocation else { return false }
        return motionSaysVehicle || vehicleSpeedUntil > Date()
#else
        return false
#endif
    }

    private var isPedestrianActivityConfirmed: Bool {
        if isUsingFixedTestLocation { return liveSteps > 0 }
#if os(iOS)
        if isLikelyInVehicle { return false }
        if motionSaysPedestrian { return true }
        let recentRealStep = Date().timeIntervalSince(lastRecentStepAt) <= 90
        let speed = max(0, currentLocation?.speed ?? 0)
        return recentRealStep && speed <= 3.4
#else
        return liveSteps > 0
#endif
    }

    private func suppressWalkOnlyPopups() {
        collectCandidateBeam = nil
        // Vehicle/activity classification is noisy while somebody is standing
        // still. Never discard an active location transfer here; its separate
        // 50 m exit boundary is the sole authority for abandonment.
        if locationDwellStartedAt.isEmpty {
            collectCandidatePlace = nil
        }
        walkingTowardUntil.removeAll()
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

    func handleUnityStepState(_ json: String) {
        guard let data = json.data(using: .utf8),
              let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return }
#if os(iOS)
        // Real-GPS iOS remains owned by Core Location/Core Motion. In a fixed
        // location, however, Unity's manual walker is authoritative—exactly as
        // it is on Mac—so accept its virtual steps and coordinates.
        guard isUsingFixedTestLocation else { return }
#endif
        let nextLive = max(0, obj["liveSteps"] as? Int ?? liveSteps)
        let next24h = max(0, obj["steps24h"] as? Int ?? steps24h)
        let next7d = max(0, obj["steps7d"] as? Int ?? steps7d)
        let latitude = obj["latitude"] as? Double
        let longitude = obj["longitude"] as? Double
        if let simulatedHeading = obj["heading"] as? Double {
            headingDegrees = simulatedHeading.truncatingRemainder(dividingBy: 360) + (simulatedHeading < 0 ? 360 : 0)
        }

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

        isApplyingUnitySimulatedStepState = true
        liveSteps = nextLive
        isApplyingUnitySimulatedStepState = false
        simulatedLocationStepBaseline = nextLive
        steps24h = next24h
        steps7d = next7d

        if let latitude, let longitude, latitude != 0, longitude != 0 {
            let simulatedLocation = CLLocation(latitude: latitude, longitude: longitude)
            currentLocation = simulatedLocation
            updateBeamApproachState()
            checkForBeamCollection()
            sendNativeSimulatedLocation(location: simulatedLocation)

            if needsFreshPlaces(latitude: latitude, longitude: longitude) {
                fetchWeather(latitude: latitude, longitude: longitude)
                fetchNearby(latitude: latitude, longitude: longitude)
            }
        }
    }

    private func refreshPedometerSession(forceRestart: Bool) {
#if os(iOS)
        // Manual/non-GPS walking is driven by Unity's virtual movement stream.
        // A Core Motion query completing later must not overwrite those steps.
        guard !isUsingFixedTestLocation else {
            pedometer.stopUpdates()
            pedometerSessionRefreshInFlight = false
            return
        }
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
                guard let self, !self.isUsingFixedTestLocation else {
                    self?.pedometerSessionRefreshInFlight = false
                    return
                }
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
                        guard let self, !self.isUsingFixedTestLocation else { return }
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
		        let includeBeams = !isLikelyInVehicle && (forceWorldRefresh || needsFreshBeams(location: location))
	        resolveAPIBase { [weak self] apiBase in
	            guard let self else { return }
                if includePlaces {
                    self.fetchMapKitPlaces(
                        latitude: requestLatitude,
                        longitude: requestLongitude,
                        apiBase: apiBase
                    )
                }
	            if includeBeams {
	                self.fetchWorldNearby(
	                    latitude: requestLatitude,
	                    longitude: requestLongitude,
	                    apiBase: apiBase,
	                    includePlaces: false,
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
	        isPedestrianActivityConfirmed
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
	        if isActivelyWalking(location) {
	            return !hasUsableForwardBeam(from: location)
	        }

	        // Couch mode still needs a quiet world prompt. While the list is empty,
	        // retry on the existing 30-second nearby timer so an early safe-road
	        // miss cannot strand the session for five minutes. Once a beam exists,
	        // the home highlight gate above suppresses redundant refreshes.
	        // Vehicle rides and location dwell sessions are intentionally silent.
	        guard !isLikelyInVehicle,
	              collectCandidatePlace == nil,
	              max(0, location.speed) <= 1.0,
	              homeHighlightBeam == nil,
	              Date().timeIntervalSince(lastIdleBeamFetchAt) >= (beams.isEmpty ? 30 : 5 * 60) else { return false }
	        lastIdleBeamFetchAt = Date()
	        return true
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
            motionActivityManager.stopActivityUpdates()
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
	            // Keep couch-mode bait far enough away that it inspires a walk
	            // and can never appear as an accidental immediate collection.
	            "minDistanceMeters": isPedestrianActivityConfirmed ? 45 : 200,
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
	                // Location beams are sent separately through the active map
	                // category snapshot below. Never flash the unfiltered server
	                // list into Unity before the authoritative toggles are applied.
	                root["includePlaces"] = false
	                root["places"] = []
	                root["includeBeams"] = includeBeams
	                if let payload = try? JSONSerialization.data(withJSONObject: root),
	                   let encoded = String(data: payload, encoding: .utf8) {
	                    worldNearbyJson = encoded
	                }
	            }
	            DispatchQueue.main.async {
	                if includeBeams {
	                    K1L0WeatherOverlayInstaller.applyNativeWorldNearby(worldNearbyJson)
	                }
	                if includePlaces {
	                    self?.places = decoded.places.sorted { $0.distance < $1.distance }
	                    self?.locationStatus = decoded.places.isEmpty ? "no open places nearby" : "\(decoded.places.count) open places nearby"
	                    self?.applyInitialDenseLocationFilterIfNeeded(decoded.places)
	                    self?.applyLocationFilter()
	                    let collectibleLocations = decoded.places
	                        .filter(\.hasCollectibleArtifact)
	                        .sorted { $0.distance < $1.distance }
	                        .prefix(3)
	                    let missingHolograms = collectibleLocations.contains {
	                        ($0.imageUrl ?? "").isEmpty || ($0.depthMapUrl ?? "").isEmpty
	                    }
	                    if missingHolograms, let self, self.locationHologramRetryCount < 6 {
	                        self.locationHologramRetryCount += 1
	                        DispatchQueue.main.asyncAfter(deadline: .now() + 12) { [weak self] in
	                            guard let self else { return }
	                            self.fetchWorldNearby(
	                                latitude: latitude,
	                                longitude: longitude,
	                                apiBase: apiBase,
	                                includePlaces: true,
	                                includeBeams: false
	                            )
	                        }
	                    } else {
	                        self?.locationHologramRetryCount = 0
	                        self?.markPlacesFresh(latitude: latitude, longitude: longitude)
	                    }
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
	                    if activeBeams.isEmpty {
	                        self?.spawnClientAmbientBeam(
	                            apiBase: apiBase,
	                            origin: CLLocation(latitude: latitude, longitude: longitude)
	                        )
	                    }
	                }
	                self?.updateBeamApproachState()
	                self?.checkForBeamCollection()
	            }
        }.resume()
    }

    private func spawnClientAmbientBeam(apiBase: String, origin: CLLocation) {
        guard beams.isEmpty,
              !ambientSpawnRequestInFlight,
              !isLikelyInVehicle,
              incomingTransmission == nil,
              collectCandidatePlace == nil,
              Date().timeIntervalSince(lastAmbientSpawnAttemptAt) >= 20 else { return }

        ambientSpawnRequestInFlight = true
        lastAmbientSpawnAttemptAt = Date()
        pendingAmbientSpawnAPIBase = apiBase
        pendingAmbientSpawnOrigin = origin
        let payload: [String: Any] = [
            "playerLatitude": origin.coordinate.latitude,
            "playerLongitude": origin.coordinate.longitude,
            "minDistance": 50,
            "maxDistance": 150,
            "preferredBearing": walkingBearingDegrees(for: origin),
            "constrainBearing": isPedestrianActivityConfirmed
        ]
        guard let data = try? JSONSerialization.data(withJSONObject: payload),
              let json = String(data: data, encoding: .utf8) else {
            ambientSpawnRequestInFlight = false
            pendingAmbientSpawnAPIBase = nil
            pendingAmbientSpawnOrigin = nil
            lastIdleBeamFetchAt = .distantPast
            return
        }
        beamStatus = "finding a safe road"
        K1L0WeatherOverlayInstaller.requestAmbientSpawnPlacement(json)
        let attemptAt = lastAmbientSpawnAttemptAt
        DispatchQueue.main.asyncAfter(deadline: .now() + 8) { [weak self] in
            guard let self,
                  self.ambientSpawnRequestInFlight,
                  self.lastAmbientSpawnAttemptAt == attemptAt else { return }
            self.ambientSpawnRequestInFlight = false
            self.pendingAmbientSpawnAPIBase = nil
            self.pendingAmbientSpawnOrigin = nil
            self.lastIdleBeamFetchAt = .distantPast
            self.beamStatus = "retrying safe road placement"
        }
    }

    func handleAmbientSpawnPlacement(_ json: String) {
        guard ambientSpawnRequestInFlight,
              let apiBase = pendingAmbientSpawnAPIBase,
              let origin = pendingAmbientSpawnOrigin else { return }
        pendingAmbientSpawnAPIBase = nil
        pendingAmbientSpawnOrigin = nil

        guard let data = json.data(using: .utf8),
              let root = (try? JSONSerialization.jsonObject(with: data)) as? [String: Any],
              root["ok"] as? Bool == true,
              let latitude = (root["latitude"] as? NSNumber)?.doubleValue,
              let longitude = (root["longitude"] as? NSNumber)?.doubleValue,
              let roadClass = root["roadClass"] as? String,
              !roadClass.isEmpty,
        	      let url = URL(string: "\(apiBase)/k1l0/beams/spawn") else {
            ambientSpawnRequestInFlight = false
            lastIdleBeamFetchAt = .distantPast
            beamStatus = "waiting for a safe nearby road"
            return
        }

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: [
            "userId": currentUserIdForInventory() ?? "anon",
            "playerLatitude": origin.coordinate.latitude,
            "playerLongitude": origin.coordinate.longitude,
            "latitude": latitude,
            "longitude": longitude,
            "roadClass": roadClass,
            "ttlMinutes": beamTtlMinutes()
        ])

        URLSession.shared.dataTask(with: request) { [weak self] data, response, error in
            guard let data,
                  let decoded = try? JSONDecoder().decode(OverlayBeamsResponse.self, from: data) else {
                let code = (response as? HTTPURLResponse)?.statusCode ?? 0
                DispatchQueue.main.async {
                    self?.ambientSpawnRequestInFlight = false
                    self?.lastIdleBeamFetchAt = .distantPast
                    self?.beamStatus = "ambient spawn unavailable \(code)"
                }
                if let error { print("[K1L0Overlay] ambient spawn error: \(error.localizedDescription)") }
                return
            }
            DispatchQueue.main.async {
                guard let self else { return }
                self.ambientSpawnRequestInFlight = false
                let activeBeams = decoded.beams.filter { !self.isExpired($0) }
                self.beams = activeBeams
                self.beamStatus = activeBeams.isEmpty ? "no nearby ambient" : "\(activeBeams.count) nearby"
                if activeBeams.isEmpty {
                    self.lastIdleBeamFetchAt = .distantPast
                }

                if var root = (try? JSONSerialization.jsonObject(with: data)) as? [String: Any] {
                    root["includePlaces"] = false
                    root["includeBeams"] = true
                    if let payload = try? JSONSerialization.data(withJSONObject: root),
                       let json = String(data: payload, encoding: .utf8) {
                        K1L0WeatherOverlayInstaller.applyNativeWorldNearby(json)
                    }
                }
                self.updateBeamApproachState()
                self.checkForBeamCollection()
            }
        }.resume()
    }

    func respondToTransmission(_ result: K1L0TransmissionResult, option: String, photoPath: String? = nil, completion: (() -> Void)? = nil) {
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
            let sendRespond: (String?) -> Void = { [weak self] attachedImageUrl in
                guard let url = URL(string: "\(apiBase)/api/k1l0/v2/transmit/respond") else { return }
                var request = URLRequest(url: url)
                request.httpMethod = "POST"
                request.setValue("application/json", forHTTPHeaderField: "Content-Type")
                var body: [String: Any] = [
                    "userId": userId,
                    "parentUserId": parentUserId,
                    "parentJobId": parentJobId,
                    "selectedResponse": option
                ]
                if let attachedImageUrl, !attachedImageUrl.isEmpty {
                    // Responder photo becomes the response slide's background,
                    // like the input image on an original transmission.
                    body["image"] = attachedImageUrl
                }
                request.httpBody = try? JSONSerialization.data(withJSONObject: body)
                URLSession.shared.dataTask(with: request) { [weak self] _, _, _ in
                    DispatchQueue.main.async {
                        self?.setIncomingWaitBaseline(self?.liveSteps ?? 0)
                        self?.clearIncomingTuneBaseline()
                        self?.incomingTransmission = nil
                        self?.receiveProgressSteps = 0
                        self?.receiveSignalStatus = "response transmitting…"
                        self?.fetchIncomingTransmissionIfNeeded()
                        completion?()
                    }
                }.resume()
            }
            if let photoPath, !photoPath.isEmpty {
                self.uploadTransmissionPhoto(photoPath: photoPath, apiBase: apiBase, userId: userId, status: { _ in }) { uploadResult in
                    sendRespond(try? uploadResult.get())
                }
            } else {
                sendRespond(nil)
            }
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

    func declineIncomingTransmission() {
        guard incomingTransmission != nil else { return }
        setIncomingWaitBaseline(liveSteps)
        clearIncomingTuneBaseline()
        receiveProgressSteps = 0
        receiveSignalStatus = "contact declined"
        incomingTransmission = nil
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
            receiveSignalStatus = "walk \(K1L0StepText(wait - walkedSinceLastSignal)) for signal"
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
                        self.receiveSignalStatus = "scanning signals"
                        return
                    }
                    guard let data else {
                        self.receiveSignalStatus = "scanning signals"
                        return
                    }
                    let decoded: OverlayReceiveResponse
                    do {
                        decoded = try JSONDecoder().decode(OverlayReceiveResponse.self, from: data)
                    } catch {
                        self.receiveSignalStatus = "scanning signals"
                        return
                    }
                    guard decoded.ok else {
                        self.receiveSignalStatus = "scanning signals"
                        return
                    }
                    guard let transmission = decoded.transmission else {
                        self.receiveSignalStatus = "scanning signals"
                        self.requestNearbyTestSignal(apiBase: apiBase, userId: userId)
                        return
                    }
                    guard transmission.isOriginalTransmission else {
                        self.receiveSignalStatus = "scanning signals"
                        return
                    }
                    guard !self.isOwnIncomingTransmission(transmission, currentUserId: userId) else {
                        self.receiveSignalStatus = "scanning signals"
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
                sourceCallsign: item.sourceCallsign ?? "",
                sourceCity: item.sourceCity ?? "",
                sourceCountry: item.sourceCountry ?? "",
                sourceCountryCode: item.sourceCountryCode ?? "",
                createdAt: item.createdAt ?? item.updatedAt ?? 0,
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
                sourceCallsign: incoming.sourceCallsign ?? "",
                sourceCity: incoming.sourceCity ?? "",
                sourceCountry: incoming.sourceCountry ?? "",
                sourceCountryCode: incoming.sourceCountryCode ?? "",
                createdAt: incoming.createdAt ?? incoming.updatedAt ?? 0,
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
            createdAt: incoming.createdAt ?? incoming.updatedAt,
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
            self?.advanceLocationDwell()
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
        fetchMapKitPlaces(latitude: latitude, longitude: longitude, apiBase: apiBase)
    }

    private static func stableArtifactHash(_ value: String) -> UInt64 {
        var hash: UInt64 = 0xcbf29ce484222325
        for byte in value.utf8 {
            hash ^= UInt64(byte)
            hash &*= 0x100000001b3
        }
        return hash
    }

    // On-device places via MKLocalSearch. Populates `places` from focused
    // open-now searches within category-specific radii. Place discovery remains entirely
    // MapKit; after
    // discovery, the current shared item pool is fetched once and assigned in
    // memory. Location beams are never read from or written to Firestore.
    private func fetchMapKitPlaces(latitude: Double, longitude: Double, apiBase: String) {
        let radiusMeters: CLLocationDistance = 1609.344
        let drinksRadiusMeters: CLLocationDistance = 2_000
        let center = CLLocationCoordinate2D(latitude: latitude, longitude: longitude)
        let originLoc = CLLocation(latitude: latitude, longitude: longitude)
        let region = MKCoordinateRegion(
            center: center,
            latitudinalMeters: drinksRadiusMeters * 2,
            longitudinalMeters: drinksRadiusMeters * 2
        )
        // Keep each natural-language intent separate and leave the region at
        // MapKit's default/advisory priority. For "open now" searches, a
        // required region causes MapKit to fill the result set with nearby
        // category matches even when they are closed. Advisory search can
        // expand outward to businesses Apple believes are open; the explicit
        // per-intent distance check below then removes those distant matches,
        // correctly leaving no local results when everything nearby is closed.
        // Restaurant stays in the drinks filter because Apple classifies many
        // neighborhood bars (including Marty's) as restaurants.
        let legs: [([MKPointOfInterestCategory]?, String, String, CLLocationDistance)] = [
            ([.cafe], "cafe", "coffee shops open now", radiusMeters),
            // Apple classifies Sheetz primarily as a food market and sometimes
            // exposes its fuel listing separately as a gas station. Keep this
            // as its own intent so coffee-shop text ranking does not omit it,
            // but map the results into the existing coffee/convenience group.
            // This leg gets a narrowly larger 2 km cap: the nearest Sheetz to
            // the Cranberry test origin is 1,757 m away, while the next
            // convenience result is 2,290 m away.
            ([.foodMarket, .gasStation], "cafe", "convenience stores open now", 2_000),
            ([.nightlife, .restaurant, .brewery, .distillery, .winery], "bar", "bars open now", drinksRadiusMeters),
            // MapKit's plural "bars" intent omits some open neighborhood
            // bar-and-grills even when `.restaurant` is included. A focused
            // companion intent restores those results (notably Marty's)
            // without weakening the query to plain "bar", which admits
            // businesses that are currently closed.
            ([.nightlife, .restaurant, .brewery, .distillery, .winery], "bar", "bar and grill open now", drinksRadiusMeters),
        ]
        let group = DispatchGroup()
        var accumulated: [OverlayPlace] = []
        let accLock = NSLock()
        for (categories, typeTag, query, maxDistanceMeters) in legs {
            group.enter()
            let request = MKLocalSearch.Request()
            request.naturalLanguageQuery = query
            request.region = region
            request.resultTypes = .pointOfInterest
            if let categories {
                request.pointOfInterestFilter = MKPointOfInterestFilter(including: categories)
            }
            MKLocalSearch(request: request).start { response, _ in
                defer { group.leave() }
                guard let items = response?.mapItems else { return }
                var mapped: [OverlayPlace] = []
                for item in items {
                    let coord = item.placemark.coordinate
                    let itemLoc = CLLocation(latitude: coord.latitude, longitude: coord.longitude)
                    let meters = itemLoc.distance(from: originLoc)
                    guard meters <= maxDistanceMeters else { continue }
                    let name = item.name ?? item.placemark.name ?? "(unnamed)"
                    let placeId = "mapkit:\(name)@\(String(format: "%.5f,%.5f", coord.latitude, coord.longitude))"
                    mapped.append(OverlayPlace(
                        placeId: placeId,
                        name: name,
                        type: typeTag,
                        types: [typeTag],
                        coordinates: OverlayCoordinate(lat: coord.latitude, lng: coord.longitude),
                        distance: meters,
                        artifactMaterial: nil,
                        artifactLabel: nil,
                        artifactPoolItemId: nil,
                        artifactTeaser: nil,
                        teaser: nil,
                        imageUrl: nil,
                        depthMapUrl: nil,
                        openNow: nil,
                        openingHours: nil,
                        closingTime: nil,
                        buildingFeatureId: nil,
                        buildingTileKey: nil
                    ))
                }
                accLock.lock()
                accumulated.append(contentsOf: mapped)
                accLock.unlock()
            }
        }
        group.notify(queue: .main) { [weak self] in
            guard let self else { return }
            var seen = Set<String>()
            let unique = accumulated
                .sorted { $0.distance < $1.distance }
                .filter { p in
                    let key = "\(p.name)|\(Int(p.coordinates.lat * 100000))|\(Int(p.coordinates.lng * 100000))"
                    if seen.contains(key) { return false }
                    seen.insert(key)
                    return true
                }
            self.enrichMapKitPlaces(unique, apiBase: apiBase) { [weak self] enriched in
                guard let self else { return }
                self.places = enriched
                self.placesDatasetStatus = "mapkit"
                self.locationStatus = enriched.isEmpty ? "no open MapKit places within 1 mi" : "\(enriched.count) open MapKit places within 1 mi"
                self.applyInitialDenseLocationFilterIfNeeded(enriched)
                self.applyLocationFilter()
                self.checkForBeamCollection()
                self.markPlacesFresh(latitude: latitude, longitude: longitude)
            }
        }
    }

    private func enrichMapKitPlaces(
        _ places: [OverlayPlace],
        apiBase: String,
        completion: @escaping ([OverlayPlace]) -> Void
    ) {
        guard !places.isEmpty,
              let url = URL(string: "\(apiBase)/api/k1l0/pool/items") else {
            completion(places)
            return
        }
        URLSession.shared.dataTask(with: url) { data, _, _ in
            guard let data,
                  let response = try? JSONDecoder().decode(K1L0PoolItemsResponse.self, from: data) else {
                DispatchQueue.main.async { completion(places) }
                return
            }
            let pool = response.items
                .filter { $0.active != false && !$0.title.isEmpty && !($0.avatarUrl ?? "").isEmpty }
                .sorted { $0.id < $1.id }
            guard !pool.isEmpty else {
                DispatchQueue.main.async { completion(places) }
                return
            }
            let slot = Int(Date().timeIntervalSince1970 / (4 * 60 * 60))
            var used = Set<String>()
            let enriched = places.map { place -> OverlayPlace in
                let start = Int(Self.stableArtifactHash("\(place.id):\(slot)") % UInt64(pool.count))
                var selected = pool[start]
                for offset in 0..<pool.count {
                    let candidate = pool[(start + offset) % pool.count]
                    if !used.contains(candidate.id) {
                        selected = candidate
                        break
                    }
                }
                used.insert(selected.id)
                return OverlayPlace(
                    placeId: place.placeId,
                    name: place.name,
                    type: place.type,
                    types: place.types,
                    coordinates: place.coordinates,
                    distance: place.distance,
                    artifactMaterial: selected.material ?? selected.title,
                    artifactLabel: selected.title,
                    artifactPoolItemId: selected.id,
                    artifactTeaser: selected.teaser,
                    teaser: place.teaser,
                    imageUrl: selected.avatarUrl,
                    depthMapUrl: selected.depthMapUrl,
                    openNow: place.openNow,
                    openingHours: place.openingHours,
                    closingTime: place.closingTime,
                    buildingFeatureId: place.buildingFeatureId,
                    buildingTileKey: place.buildingTileKey
                )
            }
            DispatchQueue.main.async { completion(enriched) }
        }.resume()
    }

    func refreshPlacesDataset() {
        guard let location = currentLocation else { return }
        resolveAPIBase { [weak self] apiBase in
            self?.fetchPlaces(
                latitude: location.coordinate.latitude,
                longitude: location.coordinate.longitude,
                apiBase: apiBase
            )
        }
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
            "minDistanceMeters": isPedestrianActivityConfirmed ? 45 : 200,
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
                if activeBeams.isEmpty {
                    self?.spawnClientAmbientBeam(
                        apiBase: apiBase,
                        origin: CLLocation(latitude: latitude, longitude: longitude)
                    )
                }

                // Keep Unity's sky renderer on the exact same fallback result
                // as the native Home/map UI. Previously this legacy endpoint
                // updated only Swift, leaving a visible "Nearby artifact" card
                // while Unity had rings=0 and therefore no sky item.
                if var root = (try? JSONSerialization.jsonObject(with: data)) as? [String: Any] {
                    let activeIds = Set(activeBeams.map(\.id))
                    if let rawBeams = root["beams"] as? [[String: Any]] {
                        root["beams"] = rawBeams.filter {
                            guard let id = $0["id"] as? String else { return false }
                            return activeIds.contains(id)
                        }
                    }
                    root["ok"] = true
                    root["includePlaces"] = false
                    root["includeBeams"] = true
                    if let payload = try? JSONSerialization.data(withJSONObject: root),
                       let json = String(data: payload, encoding: .utf8) {
                        K1L0WeatherOverlayInstaller.applyNativeWorldNearby(json)
                    }
                }
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
                        self?.inventoryItems = parsedItems.sorted {
                            ($0.collectedAt ?? .distantPast) > ($1.collectedAt ?? .distantPast)
                        }
                        self?.elementsStatus = parsedItems.isEmpty ? "no collected artifacts" : "\(parsedItems.count) collected"
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
        if let country = snapshot.country?.trimmingCharacters(in: .whitespacesAndNewlines), !country.isEmpty {
            countryText = country
        }
        weatherText = snapshot.displayText
        weatherGlyph = snapshot.glyph
        K1L0WindowGlowResolver.rememberWeatherIsDay(snapshot.isDay)
        if let cloudCover = snapshot.cloudCover {
            UserDefaults.standard.set(cloudCover, forKey: "k1lo_native_liveCloudCover")
            NativeUnitySolarSync.sync()
        }
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
            let country = json["country"] as? String
            let tempF = weather["temperatureF"] as? Double
            let glyph = (weather["glyph"] as? String) ?? (weather["icon"] as? String)
            let weatherCode = weather["weatherCode"] as? Int
            let isDay = weather["isDay"] as? Bool
            let cloudCover = (weather["cloudCover"] as? NSNumber)?.doubleValue
            let snapshot = WeatherSnapshot(
                city: city,
                country: country,
                tempF: tempF,
                glyph: Self.weatherGlyph(forWeatherCode: weatherCode, isDay: isDay, fallbackGlyph: glyph, preferBackendGlyph: true),
                isDay: isDay,
                cloudCover: cloudCover
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
                country: nil,
                tempF: Double(temp),
                glyph: Self.weatherGlyph(forDescription: desc, isDay: !isNight),
                isDay: !isNight,
                cloudCover: Double(current["cloudcover"] as? String ?? "")
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
        walkingAwaySampleCounts = walkingAwaySampleCounts.filter { currentIds.contains($0.key) }
        beamDistanceHistory = beamDistanceHistory.filter { currentIds.contains($0.key) }

        let dismissSteps = beamDismissStepsRequired()
        var beamIdsToDismiss = Set<String>()

        for beam in beams where !isExpired(beam) {
            let distance = distanceMeters(to: beam)
            var history = beamDistanceHistory[beam.id] ?? []
            history.append(distance)
            if history.count > 6 { history.removeFirst(history.count - 6) }
            beamDistanceHistory[beam.id] = history

            if history.count == 6 {
                let previousAverage = history.prefix(3).reduce(0, +) / 3.0
                let recentAverage = history.suffix(3).reduce(0, +) / 3.0
                let averageDelta = recentAverage - previousAverage
                if averageDelta < -2.5 {
                    walkingTowardUntil[beam.id] = now.addingTimeInterval(8)
                    walkingAwayStartSteps.removeValue(forKey: beam.id)
                    walkingAwaySampleCounts.removeValue(forKey: beam.id)
                } else if averageDelta > 2.5, dismissSteps > 0 {
                    let startSteps = walkingAwayStartSteps[beam.id] ?? liveSteps
                    walkingAwayStartSteps[beam.id] = startSteps
                    let samples = (walkingAwaySampleCounts[beam.id] ?? 0) + 1
                    walkingAwaySampleCounts[beam.id] = samples
                    // Two consecutive rolling-average confirmations plus real
                    // steps reject stationary GPS drift and momentary detours.
                    let relocateSteps = min(dismissSteps, 15)
                    if samples >= 2 && liveSteps - startSteps >= relocateSteps {
                        beamIdsToDismiss.insert(beam.id)
                    }
                }
            }
            lastBeamDistances[beam.id] = distance
        }

        relocateBeams(ids: beamIdsToDismiss)
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

    private func relocateBeams(ids: Set<String>) {
        guard !ids.isEmpty, let origin = currentLocation else { return }
        for id in ids where !relocatingBeamIds.contains(id) {
            relocatingBeamIds.insert(id)
            resolveAPIBase { [weak self] apiBase in
                guard let self,
                      let url = URL(string: "\(apiBase)/k1l0/beams/relocate") else { return }
                var request = URLRequest(url: url)
                request.httpMethod = "POST"
                request.setValue("application/json", forHTTPHeaderField: "Content-Type")
                request.httpBody = try? JSONSerialization.data(withJSONObject: [
                    "beamId": id,
                    "userId": self.currentUserIdForInventory() ?? "anon"
                ])
                URLSession.shared.dataTask(with: request) { [weak self] data, response, _ in
                    let status = (response as? HTTPURLResponse)?.statusCode ?? 0
                    let ok = data.flatMap {
                        (try? JSONSerialization.jsonObject(with: $0) as? [String: Any])?["ok"] as? Bool
                    } == true
                    DispatchQueue.main.async {
                        guard let self else { return }
                        self.relocatingBeamIds.remove(id)
                        guard ok else {
                            self.beamStatus = "item relocation unavailable \(status)"
                            return
                        }
                        self.dismissedBeamIds.insert(id)
                        self.beams.removeAll { $0.id == id }
                        if self.collectCandidateBeam?.id == id { self.collectCandidateBeam = nil }
                        self.lastBeamDistances.removeValue(forKey: id)
                        self.walkingTowardUntil.removeValue(forKey: id)
                        self.walkingAwayStartSteps.removeValue(forKey: id)
                        self.walkingAwaySampleCounts.removeValue(forKey: id)
                        self.beamDistanceHistory.removeValue(forKey: id)
                        K1L0WeatherOverlayInstaller.applyNativeWorldNearby(
                            "{\"ok\":true,\"includePlaces\":false,\"includeBeams\":true,\"beams\":[]}"
                        )
                        self.lastAmbientSpawnAttemptAt = .distantPast
                        self.spawnClientAmbientBeam(apiBase: apiBase, origin: self.currentLocation ?? origin)
                    }
                }.resume()
            }
        }
    }

    private func checkForBeamCollection() {
        guard incomingTransmission == nil else { return }
        guard currentLocation != nil else { return }
        let radius = collectRadiusMeters()

        guard isPedestrianActivityConfirmed else { return }

        if let candidate = collectCandidateBeam {
            let stillAvailable = beams.contains(where: { $0.id == candidate.id })
                && !isExpired(candidate)
                && isPedestrianActivityConfirmed
                && distanceMeters(to: candidate) <= radius
            if stillAvailable { return }
            collectCandidateBeam = nil
        }

        if let beam = beams
            .filter({ !isExpired($0) && !collectingBeamIds.contains($0.id) })
            .sorted(by: { distanceMeters(to: $0) < distanceMeters(to: $1) })
            .first,
            distanceMeters(to: beam) <= radius {
            collectCandidateBeam = beam
            K1L0WeatherOverlayInstaller.playBeamCollectSound()
        }
    }

    private func locationItemKey(_ place: OverlayPlace) -> String {
        let artifact = (place.artifactPoolItemId ?? place.artifactLabel ?? place.artifactMaterial ?? "unknown")
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .lowercased()
        return "\(place.id):\(artifact)"
    }

    private func persistLocationDwellState() {
        if let data = try? JSONEncoder().encode(persistedLocationDwells) {
            UserDefaults.standard.set(data, forKey: Self.locationDwellRecordsKey)
        }
        UserDefaults.standard.set(Array(collectedPlaceItemKeys).sorted(), forKey: Self.collectedPlaceItemsKey)
    }

    private func hasCollectedLocationItem(_ place: OverlayPlace) -> Bool {
        collectedPlaceItemKeys.contains(locationItemKey(place))
    }

    func handleUnityLocationPresence(_ json: String) {
        guard let data = json.data(using: .utf8),
              let payload = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return }
        let placeId = (payload["placeId"] as? String ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        let name = (payload["name"] as? String ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        let inside = payload["inside"] as? Bool ?? false
        guard !placeId.isEmpty || !name.isEmpty else { return }

        let place = places.first {
            (!placeId.isEmpty && $0.id == placeId)
                || (!name.isEmpty && $0.name.compare(name, options: [.caseInsensitive, .diacriticInsensitive]) == .orderedSame)
        }

        if inside {
            guard let place else {
                print("[K1L0Overlay] Unity entered unknown place id=\(placeId) name=\(name)")
                return
            }
            let itemKey = locationItemKey(place)
            if locationDwellStartedAt[place.id] == nil {
                if let persisted = persistedLocationDwells[place.id],
                   persisted.itemKey == itemKey,
                   now.timeIntervalSince1970 - persisted.lastConfirmedAt <= Self.locationDwellResumeWindow {
                    locationDwellStartedAt[place.id] = Date(timeIntervalSince1970: persisted.startedAt)
                } else {
                    locationDwellStartedAt[place.id] = now
                }
                K1L0WeatherOverlayInstaller.playBeamCollectSound()
            }
            let startedAt = locationDwellStartedAt[place.id] ?? now
            persistedLocationDwells[place.id] = PersistedLocationDwell(
                itemKey: itemKey,
                startedAt: startedAt.timeIntervalSince1970,
                lastConfirmedAt: now.timeIntervalSince1970
            )
            persistLocationDwellState()
            collectCandidatePlace = place
            advanceLocationDwell()
            return
        }

        let exitingId = place?.id ?? placeId
        guard !exitingId.isEmpty else { return }
        locationDwellStartedAt.removeValue(forKey: exitingId)
        persistedLocationDwells.removeValue(forKey: exitingId)
        persistLocationDwellState()
        if collectCandidatePlace?.id == exitingId { collectCandidatePlace = nil }
    }

    func handleUnityFloatingItemTap(_ json: String) {
        guard let data = json.data(using: .utf8),
              let tap = try? JSONDecoder().decode(OverlayFloatingItemTap.self, from: data) else {
            print("[K1L0Overlay] Could not decode floating-item tap")
            return
        }

        func same(_ lhs: String?, _ rhs: String) -> Bool {
            guard let lhs else { return false }
            let a = lhs.trimmingCharacters(in: .whitespacesAndNewlines)
            let b = rhs.trimmingCharacters(in: .whitespacesAndNewlines)
            return !a.isEmpty && !b.isEmpty &&
                a.compare(b, options: [.caseInsensitive, .diacriticInsensitive]) == .orderedSame
        }

        if tap.kind == "location" {
            let identityKeys = [tap.placeId, tap.externalKey, tap.signalId].filter {
                !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            }
            var matchedPlace = places.first { place in
                identityKeys.contains { same(place.placeId, $0) || same(place.id, $0) }
            }
            if matchedPlace == nil, !tap.locationName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                matchedPlace = places.first { same($0.name, tap.locationName) }
            }
            if matchedPlace == nil, tap.hasValidCoordinate {
                let target = CLLocation(latitude: tap.latitude, longitude: tap.longitude)
                matchedPlace = places
                    .map { ($0, target.distance(from: CLLocation(latitude: $0.coordinates.lat, longitude: $0.coordinates.lng))) }
                    .filter { $0.1 <= 80 }
                    .min(by: { $0.1 < $1.1 })?.0
            }
            if let matchedPlace {
                presentArtifactDetail(.place(matchedPlace))
            } else {
                presentArtifactDetail(.fallback(tap))
            }
        } else {
            var matchedBeam = beams.first {
                same($0.id, tap.externalKey) || same($0.id, tap.signalId)
            }
            if matchedBeam == nil, tap.hasValidCoordinate {
                let target = CLLocation(latitude: tap.latitude, longitude: tap.longitude)
                matchedBeam = beams
                    .map { ($0, target.distance(from: CLLocation(latitude: $0.lat, longitude: $0.lng))) }
                    .filter { $0.1 <= 40 }
                    .min(by: { $0.1 < $1.1 })?.0
            }
            if let matchedBeam {
                presentArtifactDetail(.beam(matchedBeam))
            } else {
                presentArtifactDetail(.fallback(tap))
            }
        }
        print("[K1L0Overlay] Floating \(tap.kind) selected signal=\(tap.signalId) external=\(tap.externalKey)")
    }

    func dismissArtifactDetail() {
        withAnimation(.spring(response: 0.34, dampingFraction: 0.90)) {
            artifactDetailSelection = nil
        }
    }

    func selectLocationArtifact(_ place: OverlayPlace) {
        presentArtifactDetail(.place(place))
    }

    func selectInventoryArtifact(_ item: OverlayInventoryItem) {
        presentArtifactDetail(.inventory(item))
    }

    func selectFloatingArtifact(from marqueeItem: K1L0MarqueeItem) {
        guard marqueeItem.kind == "ambientElement" || marqueeItem.kind == "ambientObject" else { return }
        let prefix = "beam:"
        let signalId = marqueeItem.id.hasPrefix(prefix)
            ? String(marqueeItem.id.dropFirst(prefix.count))
            : marqueeItem.id
        guard let beam = beams.first(where: { $0.id == signalId }) else {
            print("[K1L0Overlay] Nearby artifact chip could not resolve beam id=\(signalId)")
            return
        }
        presentArtifactDetail(.beam(beam))
        print("[K1L0Overlay] Nearby artifact chip selected beam=\(signalId)")
    }

    private func presentArtifactDetail(_ selection: OverlayArtifactDetailSelection) {
        withAnimation(.spring(response: 0.34, dampingFraction: 0.88)) {
            artifactDetailSelection = selection
        }
    }

    private func advanceLocationDwell() {
        guard let place = collectCandidatePlace,
              let startedAt = locationDwellStartedAt[place.id],
              now.timeIntervalSince(startedAt) >= locationDwellDuration,
              !hasCollectedLocationItem(place) else { return }
        collectPlace(place)
        // Collection never clears presence. Only Unity's explicit footprint
        // exit event may remove this chip or abandon the visit.
        collectCandidatePlace = place
    }

    func locationDwellRemainingSeconds(for place: OverlayPlace) -> Int {
        guard let startedAt = locationDwellStartedAt[place.id] else {
            return Int(locationDwellDuration)
        }
        return max(0, Int(ceil(locationDwellDuration - now.timeIntervalSince(startedAt))))
    }

    func locationDwellElapsedSeconds(for place: OverlayPlace) -> Int {
        guard let startedAt = locationDwellStartedAt[place.id] else { return 0 }
        return max(0, Int(now.timeIntervalSince(startedAt)))
    }

    func locationDwellProgress(for place: OverlayPlace) -> Double {
        min(1, max(0, Double(locationDwellElapsedSeconds(for: place)) / locationDwellDuration))
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
        // Location presence is latched by Unity. Closing a detail sheet must
        // never hide the chip; only an explicit footprint exit can clear it.
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
        if !cityText.isEmpty { payload["city"] = cityText }
        if !countryText.isEmpty { payload["country"] = countryText }
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
        collectedPlaceItemKeys.insert(locationItemKey(place))
        persistLocationDwellState()
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
            "artifactPoolItemId": place.artifactPoolItemId ?? "",
            "artifactTeaser": place.artifactTeaser ?? place.teaser ?? "",
            "imageUrl": place.imageUrl ?? "",
            "city": cityText,
            "country": countryText,
            "latitude": place.coordinates.lat,
            "longitude": place.coordinates.lng
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
            "country": countryText,
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
        if !typeSet.isDisjoint(with: drinkTypes) { return "drinks" }
        if !typeSet.isDisjoint(with: snackTypes) { return "snack" }
        if !typeSet.isDisjoint(with: foodTypes) { return "food" }

        if !words.isDisjoint(with: ["coffee", "cafe", "bakery", "donut"]) { return "coffee" }
        if !words.isDisjoint(with: ["bar", "brewery", "brewpub", "taproom", "pub", "beer", "wine", "cocktail"]) { return "drinks" }
        if !words.isDisjoint(with: ["restaurant", "pizza", "thai", "wing", "wings", "sandwich", "primanti"]) { return "food" }
        if !words.isDisjoint(with: ["convenience", "bodega", "market", "mart", "store", "shop", "gas", "fuel", "candy"]) { return "snack" }
        return "food"
    }

    private func normalizedLocationFilter(_ filter: String) -> String {
        switch filter.lowercased().trimmingCharacters(in: .whitespacesAndNewlines) {
        case "drink", "drinks", "bar":
            return "drinks"
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

    private func normalizedLocationCategories(_ encoded: String) -> Set<String> {
        let allowed = Set(["coffee", "drinks", "food"])
        let requested = Set(encoded
            .split(separator: ",")
            .map { normalizedLocationFilter(String($0)) }
            .filter { allowed.contains($0) })
        // Valid map states are 1/3, 2/3, or 3/3 categories. Never let an
        // accidental empty value make every location beam disappear.
        return requested.isEmpty ? allowed : requested
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
        let createdAtMs = firstInt(item, keys: ["createdAt", "lastCollectedAt", "updatedAt"])
        let collectedAt: Date? = createdAtMs > 0 ? Date(timeIntervalSince1970: Double(createdAtMs) / 1000.0) : nil
        let collectedCity = firstString(item, keys: ["collectedCity", "city"])
        let collectedCountry = firstString(item, keys: ["collectedCountry", "country"])
        let travelCountries = (item["travelCountries"] as? [Any] ?? [])
            .compactMap { $0 as? String }
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
        if isObject {
            let name = firstString(item, keys: ["objectName", "name", "artifact", "sourceLabel", "label"])
            guard !name.isEmpty else { return nil }
            return OverlayInventoryItem(
                id: firstString(item, keys: ["id"]).isEmpty ? "object:\(name.lowercased())" : firstString(item, keys: ["id"]),
                kind: rawKind.isEmpty ? "object" : rawKind,
                name: name,
                symbol: objectSymbol(for: name),
                grams: 0,
                count: max(1, firstInt(item, keys: ["count"])),
                avatarUrl: firstString(item, keys: ["avatarUrl", "imageUrl", "iconUrl"]),
                depthMapUrl: firstString(item, keys: ["depthMapUrl", "depthUrl"]),
                senderName: senderName,
                sourceTransmissionJobId: sourceJobId,
                collectedAt: collectedAt,
                collectedCity: collectedCity,
                collectedCountry: collectedCountry,
                sourceKind: firstString(item, keys: ["sourceKind"]),
                sourcePlaceId: firstString(item, keys: ["sourcePlaceId"]),
                sourcePlaceName: firstString(item, keys: ["sourcePlaceName"]),
                poolItemId: firstString(item, keys: ["poolItemId"]),
                discoveryNumber: firstInt(item, keys: ["discoveryNumber"]),
                globalFindCount: firstInt(item, keys: ["globalFindCount", "timesFound"]),
                rarityAtDiscovery: firstString(item, keys: ["rarityAtDiscovery", "rarity"]),
                travelCountries: travelCountries,
                sourceHeadline: firstString(item, keys: ["sourceHeadline"]),
                sourcePublisher: firstString(item, keys: ["sourceName"]),
                sourceUrl: firstString(item, keys: ["sourceUrl"])
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
            collectedAt: collectedAt,
            collectedCity: collectedCity
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
        K1L0StepText(estimatedSteps(forMeters: meters))
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

struct OverlayPlacesResponse: Decodable {
    let places: [OverlayPlace]
    let placesDataset: OverlayPlacesDataset?
}

struct OverlayPlacesDataset: Decodable {
    let requestedMode: String
    let selected: String
    let served: String
    let scraperAvailable: Bool
    let fallback: Bool
    let scraperPlaceCount: Int
    let googlePlaceCount: Int
}

struct OverlayWorldNearbyResponse: Decodable {
    let places: [OverlayPlace]
    let beams: [OverlayBeam]
}

private struct K1L0PoolItemsResponse: Decodable {
    let items: [K1L0PoolArtifact]
}

private struct K1L0PoolArtifact: Decodable {
    let id: String
    let title: String
    let material: String?
    let teaser: String?
    let avatarUrl: String?
    let depthMapUrl: String?
    let active: Bool?
}

struct K1L0MarqueeItem: Identifiable {
    let id: String
    let kind: String
    let line1: String
    let line2: String
    let distanceText: String?
    let relativeBearing: Double?
    let progress: Double?
    let imageUrl: String?

    init(
        id: String,
        kind: String,
        line1: String,
        line2: String,
        distanceText: String?,
        relativeBearing: Double?,
        progress: Double?,
        imageUrl: String? = nil
    ) {
        self.id = id
        self.kind = kind
        self.line1 = line1
        self.line2 = line2
        self.distanceText = distanceText
        self.relativeBearing = relativeBearing
        self.progress = progress
        self.imageUrl = imageUrl
    }

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

struct OverlayPlace: Decodable, Identifiable {
    let placeId: String?
    let name: String
    let type: String
    let types: [String]?
    let coordinates: OverlayCoordinate
    let distance: Double
    let artifactMaterial: String?
    let artifactLabel: String?
    let artifactPoolItemId: String?
    let artifactTeaser: String?
    let teaser: String?
    let imageUrl: String?
    let depthMapUrl: String?
    let openNow: Bool?
    let openingHours: OverlayOpeningHours?
    let closingTime: String?
    let buildingFeatureId: String?
    let buildingTileKey: String?

    var id: String { placeId ?? name }

    var hoursDisplayText: String? {
        if let descriptions = openingHours?.weekdayDescriptions, !descriptions.isEmpty {
            let weekday = DateFormatter().weekdaySymbols[Calendar.current.component(.weekday, from: Date()) - 1]
            if let today = descriptions.first(where: {
                $0.range(of: weekday, options: [.anchored, .caseInsensitive]) != nil
            }) {
                return today
            }
        }

        let closing = (closingTime ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if !closing.isEmpty && closing.lowercased() != "unknown" {
            return openNow == false ? "Closed · \(closing)" : "Open · \(closing)"
        }
        if let openNow { return openNow ? "Open now" : "Closed now" }
        return nil
    }

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
        return "Location Artifact"
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

struct OverlayOpeningHours: Decodable {
    let openNow: Bool?
    let weekdayDescriptions: [String]?
}

struct OverlayCoordinate: Decodable {
    let lat: Double
    let lng: Double
}

struct OverlayFloatingItemTap: Decodable {
    let signalId: String
    let externalKey: String
    let placeId: String
    let kind: String
    let locationName: String
    let itemName: String
    let imageUrl: String
    let latitude: Double
    let longitude: Double
    let distanceMeters: Double

    var hasValidCoordinate: Bool {
        latitude.isFinite && longitude.isFinite &&
            (-90...90).contains(latitude) && (-180...180).contains(longitude) &&
            (abs(latitude) > 0.0001 || abs(longitude) > 0.0001)
    }

    var displayTitle: String {
        let preferred = itemName
        let value = preferred.trimmingCharacters(in: .whitespacesAndNewlines)
        if value.caseInsensitiveCompare("Ambient Item") == .orderedSame { return "Ambient Artifact" }
        return value.isEmpty ? (kind == "location" ? "Location Artifact" : "Ambient Artifact") : value
    }
}

enum OverlayArtifactDetailSelection {
    case place(OverlayPlace)
    case beam(OverlayBeam)
    case fallback(OverlayFloatingItemTap)
    case inventory(OverlayInventoryItem)
}

struct OverlayBeamsResponse: Decodable {
    let beams: [OverlayBeam]
}

struct OverlayUsersResponse: Decodable {
    let users: [OverlayUser]
}

struct OverlayStepLeader: Decodable, Identifiable {
    let userId: String
    let name: String
    let callsign: String?
    let helmetUrl: String
    let steps24h: Int
    let steps7d: Int
    let synthetic: Bool
    var id: String { userId }

    var displayName: String {
        let call = (callsign ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        return call.isEmpty ? String(userId.prefix(10)) : call
    }
}

struct OverlayStepLeaderboardResponse: Decodable {
    let ok: Bool
    let top24h: [OverlayStepLeader]
    let top7d: [OverlayStepLeader]
    let participantCount: Int
}

struct OverlayReceiveResponse: Decodable {
    let ok: Bool
    let transmission: OverlayIncomingTransmission?
}

struct OverlayIncomingTransmission: Decodable, Identifiable {
    let sourceUserId: String
    let sourceName: String?
    let sourceCallsign: String?
    let sourceDisplayName: String?
    let sourceCity: String?
    let sourceCountry: String?
    let sourceCountryCode: String?
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
    let createdAt: Double?
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
        // Transmission surfaces identify people by callsign. Real/display names
        // are optional profile details and belong only in the user modal.
        let candidates = [sourceCallsign, sourceUserId]
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

struct OverlayReceiverSlide: Decodable {
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

struct OverlayUser: Decodable, Identifiable {
    let userId: String
    let name: String?
    let callsign: String?
    let avatarUrl: String?
    let helmetUrl: String?
    let faceUrl: String?
    let city: String?
    var country: String? = nil
    var countryCode: String? = nil
    let lat: Double?
    let lng: Double?
    let lastActive: Double?

    var id: String { userId }

    var displayName: String {
        let call = (callsign ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if !call.isEmpty { return call }
        return String(userId.prefix(10))
    }

    var realName: String {
        let realName = (name ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        return realName.isEmpty ? displayName : realName
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

struct OverlayBeam: Decodable, Identifiable {
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
        return identity.isEmpty ? "Nearby artifact" : "Nearby \(identity.lowercased())"
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

struct OverlayElement: Identifiable {
    let name: String
    let grams: Int
    let count: Int

    var id: String { name.lowercased() }

    var symbol: String { ElementSymbolLookup.symbol(for: name) }
}

struct OverlayInventoryItem: Identifiable {
    let id: String
    let kind: String
    let name: String
    let symbol: String
    let grams: Int
    let count: Int
    let avatarUrl: String
    let depthMapUrl: String
    let senderName: String
    let sourceTransmissionJobId: String
    let collectedAt: Date?
    let collectedCity: String
    let collectedCountry: String
    let sourceKind: String
    let sourcePlaceId: String
    let sourcePlaceName: String
    let poolItemId: String
    let discoveryNumber: Int
    let globalFindCount: Int
    let rarityAtDiscovery: String
    let travelCountries: [String]
    let sourceHeadline: String
    let sourcePublisher: String
    let sourceUrl: String

    init(id: String, kind: String, name: String, symbol: String, grams: Int, count: Int, avatarUrl: String, depthMapUrl: String = "",
         senderName: String = "", sourceTransmissionJobId: String = "", collectedAt: Date? = nil, collectedCity: String = "",
         collectedCountry: String = "", sourceKind: String = "", sourcePlaceId: String = "", sourcePlaceName: String = "",
         poolItemId: String = "", discoveryNumber: Int = 0, globalFindCount: Int = 0, rarityAtDiscovery: String = "",
         travelCountries: [String] = [], sourceHeadline: String = "", sourcePublisher: String = "", sourceUrl: String = "") {
        self.id = id
        self.kind = kind
        self.name = name
        self.symbol = symbol
        self.grams = grams
        self.count = count
        self.avatarUrl = avatarUrl
        self.depthMapUrl = depthMapUrl
        self.senderName = senderName
        self.sourceTransmissionJobId = sourceTransmissionJobId
        self.collectedAt = collectedAt
        self.collectedCity = collectedCity
        self.collectedCountry = collectedCountry
        self.sourceKind = sourceKind
        self.sourcePlaceId = sourcePlaceId
        self.sourcePlaceName = sourcePlaceName
        self.poolItemId = poolItemId
        self.discoveryNumber = discoveryNumber
        self.globalFindCount = globalFindCount
        self.rarityAtDiscovery = rarityAtDiscovery
        self.travelCountries = travelCountries
        self.sourceHeadline = sourceHeadline
        self.sourcePublisher = sourcePublisher
        self.sourceUrl = sourceUrl
    }

    init(element: OverlayElement) {
        self.id = "element:\(element.id)"
        self.kind = "element"
        self.name = element.name
        self.symbol = element.symbol
        self.grams = element.grams
        self.count = element.count
        self.avatarUrl = ""
        self.depthMapUrl = ""
        self.senderName = ""
        self.sourceTransmissionJobId = ""
        self.collectedAt = nil
        self.collectedCity = ""
        self.collectedCountry = ""
        self.sourceKind = ""
        self.sourcePlaceId = ""
        self.sourcePlaceName = ""
        self.poolItemId = ""
        self.discoveryNumber = 0
        self.globalFindCount = 0
        self.rarityAtDiscovery = ""
        self.travelCountries = []
        self.sourceHeadline = ""
        self.sourcePublisher = ""
        self.sourceUrl = ""
    }

    var isElement: Bool { kind.lowercased() == "element" }
    private var assetSlug: String {
        name.lowercased()
            .replacingOccurrences(of: "[^a-z0-9]+", with: "-", options: .regularExpression)
            .trimmingCharacters(in: CharacterSet(charactersIn: "-"))
    }
    var resolvedAvatarUrl: String {
        avatarUrl.isEmpty ? "https://cdn.kilo.gallery/beam-avatars/beam_\(assetSlug).png" : avatarUrl
    }
    var resolvedDepthMapUrl: String {
        depthMapUrl.isEmpty ? "https://cdn.kilo.gallery/beam-avatars/beam_\(assetSlug)_depth.png" : depthMapUrl
    }
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
