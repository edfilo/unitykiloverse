import SwiftUI
import AppKit
import CoreLocation

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var window: NSWindow?

    func applicationDidFinishLaunching(_ notification: Notification) {
        print("[K1L0MacHUD] applicationDidFinishLaunching")
        let screenFrame = NSScreen.main?.frame ?? NSRect(x: 0, y: 0, width: 1200, height: 900)
        let panel = NSWindow(contentRect: screenFrame, styleMask: [.borderless], backing: .buffered, defer: false)
        panel.level = .floating
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]
        panel.backgroundColor = .clear
        panel.isOpaque = false
        panel.hasShadow = false
        panel.isReleasedWhenClosed = false
        panel.title = "K1L0 Mac HUD"
        panel.contentView = NSHostingView(rootView: K1L0MacHUDView())
        panel.makeKeyAndOrderFront(nil)
        panel.orderFrontRegardless()
        window = panel
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        false
    }

    func applicationWillTerminate(_ notification: Notification) {
        print("[K1L0MacHUD] applicationWillTerminate")
    }
}

private struct K1L0MacHUDView: View {
    @StateObject private var model = K1L0MacHUDModel()

    var body: some View {
        ZStack {
            Color.clear
            ScrollView(.vertical, showsIndicators: false) {
                VStack(spacing: 12) {
                    HStack(alignment: .top) {
                        WeatherPill(text: model.weatherText, glyph: model.weatherGlyph)
                        Spacer()
                        Button("×") { NSApp.terminate(nil) }
                            .font(.system(size: 24, weight: .bold))
                            .buttonStyle(.plain)
                            .foregroundStyle(.white)
                            .frame(width: 42, height: 42)
                            .background(.black.opacity(0.35), in: Circle())
                    }

                    VStack(spacing: 2) {
                        Text("\(model.liveSteps)")
                            .font(.system(size: 82, weight: .bold, design: .default))
                            .foregroundStyle(.white)
                        Text("steps")
                            .font(.system(size: 13, weight: .medium))
                            .foregroundStyle(.white.opacity(0.72))
                            .padding(.top, -10)
                        Text("24h \(model.steps24h)     7d \(model.steps7d)")
                            .font(.system(size: 13, weight: .medium))
                            .monospacedDigit()
                            .foregroundStyle(.white.opacity(0.82))
                        HStack(spacing: 7) {
                            Image(systemName: model.nearestBeam == nil ? "exclamationmark.triangle.fill" : "antenna.radiowaves.left.and.right")
                                .foregroundStyle(model.nearestBeam == nil ? .yellow : Color(red: 0.66, green: 1, blue: 0.76))
                            Text(model.nearestBeam == nil ? "WALK TO BOOST SIGNAL" : "SIGNAL ESTABLISHED")
                                .font(.system(size: 15, weight: .bold))
                                .foregroundStyle(.white)
                        }
                        .padding(.top, 10)
                    }
                    .frame(maxWidth: .infinity)

                    if let beam = model.nearestBeam {
                        GlassCard {
                            HStack(spacing: 12) {
                                Image(systemName: "exclamationmark.triangle.fill")
                                    .font(.system(size: 22, weight: .bold))
                                    .foregroundStyle(.yellow)
                                VStack(alignment: .leading, spacing: 4) {
                                    Text("NEARBY TRANSMISSION FROM \(beam.senderTitle.uppercased())")
                                        .font(.system(size: 15, weight: .black))
                                    Text(beam.title)
                                        .font(.system(size: 14, weight: .semibold))
                                        .foregroundStyle(.white.opacity(0.76))
                                    Text(model.expirationText(for: beam))
                                        .font(.system(size: 12, weight: .bold))
                                        .monospacedDigit()
                                        .foregroundStyle(.yellow.opacity(0.92))
                                }
                                Spacer()
                                DirectionCell(distance: model.distanceText(beam.distanceMeters), relativeBearing: 0)
                            }
                        }
                    }

                    GlassCard {
                        VStack(alignment: .leading, spacing: 12) {
                            Text("Nearby")
                                .font(.system(size: 25, weight: .bold))
                            Text(model.locationStatus)
                                .font(.system(size: 13, weight: .medium))
                                .foregroundStyle(.white.opacity(0.70))
                            ForEach(model.places.prefix(5)) { place in
                                HStack(spacing: 10) {
                                    DirectionCell(distance: model.distanceText(place.distance), relativeBearing: 0)
                                    VStack(alignment: .leading, spacing: 2) {
                                        Text(place.name).font(.system(size: 16, weight: .semibold)).lineLimit(1)
                                        Text(place.type).font(.system(size: 11, weight: .medium)).foregroundStyle(.white.opacity(0.55))
                                    }
                                    Spacer()
                                }
                            }
                        }
                    }

                    GlassCard {
                        VStack(alignment: .leading, spacing: 8) {
                            Text("Transmission")
                                .font(.system(size: 25, weight: .bold))
                            Text(model.apiStatus)
                                .font(.system(size: 13, weight: .medium))
                                .foregroundStyle(.white.opacity(0.62))
                            Text(model.beamStatus)
                                .font(.system(size: 13, weight: .medium))
                                .foregroundStyle(.white.opacity(0.62))
                        }
                    }
                }
                .padding(.horizontal, 26)
                .padding(.top, 22)
                .padding(.bottom, 40)
                .frame(maxWidth: 460)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        }
        .onAppear { model.start() }
    }
}

private struct WeatherPill: View {
    let text: String
    let glyph: String

    var body: some View {
        HStack(spacing: 8) {
            Image(systemName: glyph)
            Text(text)
                .font(.system(size: 14, weight: .semibold))
        }
        .foregroundStyle(.white)
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
        .background(.ultraThinMaterial, in: Capsule())
        .overlay(Capsule().stroke(.white.opacity(0.22), lineWidth: 1))
    }
}

private struct GlassCard<Content: View>: View {
    @ViewBuilder let content: Content

    var body: some View {
        content
            .foregroundStyle(.white)
            .padding(16)
            .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 26, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 26, style: .continuous).stroke(.white.opacity(0.20), lineWidth: 1))
            .shadow(color: .black.opacity(0.20), radius: 18, y: 10)
    }
}

private struct DirectionCell: View {
    let distance: String
    let relativeBearing: Double

    var body: some View {
        VStack(spacing: 2) {
            Text(distance)
                .font(.system(size: 12, weight: .black))
                .foregroundStyle(.white)
            Image(systemName: "location.north.fill")
                .font(.system(size: 18, weight: .bold))
                .foregroundStyle(Color(red: 0.55, green: 1, blue: 0.62))
                .rotationEffect(.degrees(relativeBearing))
        }
        .frame(width: 58)
    }
}

@MainActor
final class K1L0MacHUDModel: ObservableObject {
    @Published var liveSteps = 0
    @Published var steps24h = 0
    @Published var steps7d = 0
    @Published var weatherText = "K1L0"
    @Published var weatherGlyph = "cloud.sun.fill"
    @Published var places: [OverlayPlace] = []
    @Published var beams: [OverlayBeam] = []
    @Published var locationStatus = "loading nearby places…"
    @Published var beamStatus = "scanning transmissions…"
    @Published var apiStatus = "api resolving…"
    @Published private var now = Date()

    private var timer: Timer?
    private var clock: Timer?
    private let latitude = 40.700636
    private let longitude = -80.109190
    private let apiCandidates = [
        "http://localhost:3000",
        "http://127.0.0.1:3000",
        "https://api-tunnel.kilo.gallery",
        "https://api.kilomeme.com"
    ]

    var nearestBeam: OverlayBeam? {
        beams.sorted { $0.distanceMeters < $1.distanceMeters }.first
    }

    func start() {
        print("[K1L0MacHUD] model start")
        refresh()
        timer = Timer.scheduledTimer(withTimeInterval: 30, repeats: true) { [weak self] _ in
            Task { @MainActor in self?.refresh() }
        }
        clock = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in
            Task { @MainActor in self?.now = Date() }
        }
    }

    func distanceText(_ meters: Double) -> String {
        if meters < 528 { return "\(Int((meters * 3.28084).rounded())) ft" }
        return String(format: "%.1f mi", meters / 1609.344)
    }

    func expirationText(for beam: OverlayBeam) -> String {
        guard let expiresAt = beam.expiresAt else { return "expires --" }
        let remaining = max(0, Int(Date(timeIntervalSince1970: expiresAt / 1000).timeIntervalSince(now).rounded()))
        if remaining <= 0 { return "expires now" }
        let minutes = remaining / 60
        let seconds = remaining % 60
        return minutes > 0 ? "expires in \(minutes)m \(seconds)s" : "expires in \(seconds)s"
    }

    private func refresh() {
        Task {
            let api = await resolveAPIBase()
            await fetchWeather()
            await fetchPlaces(apiBase: api)
            await fetchBeams(apiBase: api)
        }
    }

    private func resolveAPIBase() async -> String {
        for candidate in apiCandidates {
            guard let url = URL(string: "\(candidate)/ping") else { continue }
            var request = URLRequest(url: url, timeoutInterval: 2)
            request.httpMethod = "POST"
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            request.httpBody = #"{"userId":"mac-overlay","lastActive":0}"#.data(using: .utf8)
            do {
                let (_, response) = try await URLSession.shared.data(for: request)
                if let http = response as? HTTPURLResponse, (200...299).contains(http.statusCode) {
                    apiStatus = "api \(candidate.replacingOccurrences(of: "https://", with: "").replacingOccurrences(of: "http://", with: ""))"
                    return candidate
                }
            } catch { }
        }
        apiStatus = "api unavailable"
        return "https://api-tunnel.kilo.gallery"
    }

    private func fetchWeather() async {
        guard let url = URL(string: "https://wttr.in/\(latitude),\(longitude)?format=j1") else { return }
        do {
            let (data, _) = try await URLSession.shared.data(from: url)
            guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let current = (json["current_condition"] as? [[String: Any]])?.first else { return }
            let temp = current["temp_F"] as? String ?? "--"
            let desc = ((current["weatherDesc"] as? [[String: Any]])?.first?["value"] as? String ?? "").lowercased()
            weatherText = "\(temp)°"
            weatherGlyph = desc.contains("rain") ? "cloud.rain.fill" : (desc.contains("cloud") ? "cloud.fill" : "sun.max.fill")
        } catch { }
    }

    private func fetchPlaces(apiBase: String) async {
        guard let url = URL(string: "\(apiBase)/places") else { return }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: ["latitude": latitude, "longitude": longitude, "radiusMeters": 5000])
        do {
            let (data, _) = try await URLSession.shared.data(for: request)
            let decoded = try JSONDecoder().decode(OverlayPlacesResponse.self, from: data)
            places = decoded.places.sorted { $0.distance < $1.distance }
            locationStatus = places.isEmpty ? "no open places nearby" : "\(places.count) open places nearby"
        } catch {
            locationStatus = "places unavailable"
        }
    }

    private func fetchBeams(apiBase: String) async {
        guard let url = URL(string: "\(apiBase)/k1l0/beams/nearby") else { return }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: ["latitude": latitude, "longitude": longitude, "maxMiles": 1.1, "stepMeters": 75, "minDistanceMeters": 45, "ttlMinutes": 20])
        do {
            let (data, _) = try await URLSession.shared.data(for: request)
            let decoded = try JSONDecoder().decode(OverlayBeamsResponse.self, from: data)
            beams = decoded.beams
            beamStatus = beams.isEmpty ? "no nearby transmissions" : "\(beams.count) nearby"
        } catch {
            beamStatus = "transmissions unavailable"
        }
    }
}

struct OverlayPlacesResponse: Decodable { let places: [OverlayPlace] }
struct OverlayPlace: Decodable, Identifiable {
    let placeId: String?
    let name: String
    let type: String
    let distance: Double
    var id: String { placeId ?? name }
}
struct OverlayBeamsResponse: Decodable { let beams: [OverlayBeam] }
struct OverlayBeam: Decodable, Identifiable {
    let id: String
    let label: String?
    let material: String?
    let senderName: String?
    let artifactSenderName: String?
    let expiresAt: Double?
    let distanceMeters: Double
    var title: String { material?.capitalized ?? label?.capitalized ?? "Rare Earth" }
    var senderTitle: String { senderName ?? artifactSenderName ?? "Unknown" }
}

let app = NSApplication.shared
let appDelegate = AppDelegate()
app.delegate = appDelegate
app.setActivationPolicy(.regular)
app.run()
