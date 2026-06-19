import AVFoundation
import AVKit
import CoreLocation
import CoreMotion
import Foundation
import MapKit
import PhotosUI
import SwiftUI
import UIKit

@main
struct K1L0NativeApp: App {
    var body: some Scene {
        WindowGroup {
            K1L0RootView()
                .preferredColorScheme(.dark)
        }
    }
}

struct K1L0RootView: View {
    @StateObject private var steps = StepModel()
    @StateObject private var weather = WeatherModel()
    @StateObject private var nearbyPlaces = NearbyPlacesModel()
    @StateObject private var nearbyBeams = NearbyBeamModel()
    @StateObject private var inventory = RareEarthInventoryModel()
    @StateObject private var auth = FirebaseAuthModel()
    @State private var showingMap = false

    var body: some View {
        ZStack {
            WeatherVideoBackground(videoName: weather.videoName)
                .ignoresSafeArea()

            LinearGradient(
                colors: [.black.opacity(0.10), .black.opacity(0.30), .black.opacity(0.72)],
                startPoint: .top,
                endPoint: .bottom
            )
            .ignoresSafeArea()

            ScrollView(.vertical, showsIndicators: false) {
                VStack(spacing: 16) {
                    HStack {
                        WeatherBadge(weather: weather)
                        Spacer()
                    }
                    .padding(.top, 14)

                    StepHeroView(steps: steps)
                        .padding(.top, 24)

                    ActivityGraphPanel(steps: steps)
                    RareEarthInventoryPanel(model: inventory)
                    NearbyBeamCard(model: nearbyBeams, auth: auth, inventory: inventory)
                    NearbyPlacesPanel(model: nearbyPlaces)
                    TransmissionPanel()
                    TriggerSettingsPanel(auth: auth, inventory: inventory)
                }
                .padding(.horizontal, 18)
                .padding(.bottom, 34)
            }

            VStack {
                Spacer()
                HStack {
                    Button {
                        showingMap = true
                    } label: {
                        Image(systemName: "map.fill")
                            .font(.system(size: 20, weight: .bold))
                            .foregroundStyle(.white)
                            .frame(width: 58, height: 58)
                            .background(.ultraThinMaterial, in: Circle())
                            .overlay(
                                Circle()
                                    .stroke(.white.opacity(0.24), lineWidth: 1)
                            )
                            .shadow(color: .black.opacity(0.28), radius: 16, y: 8)
                    }
                    .buttonStyle(.plain)
                    Spacer()
                }
                .padding(.horizontal, 18)
                .padding(.bottom, 16)
            }
        }
        .sheet(isPresented: $showingMap) {
            K1L0MapSheet()
                .presentationDetents([.fraction(0.72), .large])
                .presentationDragIndicator(.visible)
                .presentationCornerRadius(30)
        }
        .onAppear {
            steps.start()
            weather.start()
            nearbyPlaces.start()
            nearbyBeams.start()
            auth.start()
        }
        .onChange(of: auth.localId) { _, _ in
            inventory.start(auth: auth)
            nearbyBeams.configure(auth: auth, inventory: inventory)
        }
    }
}

struct K1L0MapSheet: View {
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        ZStack(alignment: .topTrailing) {
            K1L0MapPreview()
                .ignoresSafeArea()

            Button {
                dismiss()
            } label: {
                Image(systemName: "xmark")
                    .font(.system(size: 16, weight: .bold))
                    .foregroundStyle(.white)
                    .frame(width: 44, height: 44)
                    .background(.black.opacity(0.42), in: Circle())
                    .overlay(Circle().stroke(.white.opacity(0.18), lineWidth: 1))
                    .padding(18)
            }
            .buttonStyle(.plain)
        }
    }
}

struct WeatherBadge: View {
    @ObservedObject var weather: WeatherModel

    var body: some View {
        HStack(spacing: 8) {
            Image(systemName: weather.glyph)
                .symbolRenderingMode(.hierarchical)
                .font(.system(size: 17, weight: .semibold))
            Text(weather.temperatureText)
                .font(.system(size: 17, weight: .semibold))
                .monospacedDigit()
        }
        .foregroundStyle(.white)
        .padding(.horizontal, 13)
        .padding(.vertical, 9)
        .background(.black.opacity(0.28), in: Capsule())
    }
}

struct StepHeroView: View {
    @ObservedObject var steps: StepModel
    @AppStorage("k1l0_min_trigger_steps") private var minTriggerSteps = 300.0

    var body: some View {
        VStack(spacing: 7) {
            Text("\(steps.liveSteps)")
                .font(.system(size: 88, weight: .bold, design: .default))
                .monospacedDigit()
                .foregroundStyle(.white)
                .contentTransition(.numericText())

            Text("steps")
                .font(.system(size: 13, weight: .medium))
                .foregroundStyle(.white.opacity(0.72))
                .textCase(.lowercase)

            HStack(spacing: 18) {
                Text("24h \(steps.steps24h)")
                Text("7d \(steps.steps7d)")
            }
            .font(.system(size: 13, weight: .medium))
            .monospacedDigit()
            .foregroundStyle(.white.opacity(0.82))

            HStack(spacing: 7) {
                Image(systemName: steps.liveSteps < Int(minTriggerSteps) ? "exclamationmark.triangle.fill" : "checkmark.seal.fill")
                    .font(.system(size: 15, weight: .bold))
                    .foregroundStyle(steps.liveSteps < Int(minTriggerSteps) ? .yellow : Color(red: 0.58, green: 1.0, blue: 0.66))
                Text(steps.liveSteps < Int(minTriggerSteps) ? "WALK TO BOOST SIGNAL" : "SIGNAL READY")
                    .font(.system(size: 15, weight: .bold))
                    .tracking(0.3)
                    .foregroundStyle(.white)
            }
            .padding(.top, 10)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 14)
    }
}

struct ActivityGraphPanel: View {
    @ObservedObject var steps: StepModel

    var body: some View {
        WeatherGlassCard {
            VStack(alignment: .leading, spacing: 12) {
                HStack(alignment: .firstTextBaseline) {
                    VStack(alignment: .leading, spacing: 3) {
                        Text("Activity")
                            .font(.system(size: 24, weight: .bold))
                        Text("last 24 hours")
                            .font(.system(size: 13, weight: .medium))
                            .foregroundStyle(.white.opacity(0.70))
                    }
                    Spacer()
                    Text("\(steps.steps24h)")
                        .font(.system(size: 20, weight: .bold))
                        .monospacedDigit()
                }

                StepLineGraph(values: steps.hourlySteps)
                    .frame(height: 112)

                HStack {
                    Text("24h")
                    Spacer()
                    Text("active \(steps.liveSteps)")
                    Spacer()
                    Text("7d \(steps.steps7d)")
                }
                .font(.system(size: 12, weight: .semibold))
                .monospacedDigit()
                .foregroundStyle(.white.opacity(0.66))
            }
        }
    }
}

struct NearbyBeamCard: View {
    @ObservedObject var model: NearbyBeamModel
    @ObservedObject var auth: FirebaseAuthModel
    @ObservedObject var inventory: RareEarthInventoryModel

    var body: some View {
        Group {
            if let beam = model.nearestBeam {
                WeatherGlassCard {
                    VStack(alignment: .leading, spacing: 14) {
                        HStack(spacing: 14) {
                            VStack(spacing: 3) {
                                Text(model.distanceText(for: beam))
                                    .font(.system(size: 12, weight: .bold))
                                    .monospacedDigit()
                                    .foregroundStyle(.white.opacity(0.70))
                                Image(systemName: "location.north.fill")
                                    .font(.system(size: 30, weight: .bold))
                                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                                    .rotationEffect(.degrees(model.relativeBearingDegrees(to: beam)))
                            }
                            .frame(width: 58)

                            VStack(alignment: .leading, spacing: 5) {
                                Text("Nearby Beam")
                                    .font(.system(size: 24, weight: .bold))
                                Text(beam.title)
                                    .font(.system(size: 15, weight: .semibold))
                                    .foregroundStyle(.white.opacity(0.78))
                                    .lineLimit(1)
                                if let status = model.statusMessage {
                                    Text(status)
                                        .font(.system(size: 12, weight: .medium))
                                        .foregroundStyle(.white.opacity(0.62))
                                }
                            }

                            Spacer()

                            Text(beam.symbol)
                                .font(.system(size: 24, weight: .bold))
                                .monospaced()
                                .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                        }

                        if model.isAcceptable(beam) {
                            Button {
                                model.accept(beam: beam, auth: auth) {
                                    inventory.refresh()
                                }
                            } label: {
                                Text("[ ACCEPT \(beam.title.uppercased()) ]")
                                    .font(.system(size: 14, weight: .bold))
                                    .frame(maxWidth: .infinity)
                                    .padding(.vertical, 13)
                                    .foregroundStyle(.black)
                                    .background(Color(red: 0.66, green: 1.0, blue: 0.76), in: RoundedRectangle(cornerRadius: 16, style: .continuous))
                            }
                            .buttonStyle(.plain)
                        }
                    }
                }
                .transition(.opacity.combined(with: .scale(scale: 0.98)))
            }
        }
    }
}

struct StepLineGraph: View {
    let values: [Int]

    var body: some View {
        Canvas { context, size in
            let samples = values.isEmpty ? Array(repeating: 0, count: 24) : values
            let maxValue = max(samples.max() ?? 1, 1)
            let stepX = size.width / CGFloat(max(samples.count - 1, 1))
            var path = Path()

            for (index, value) in samples.enumerated() {
                let x = CGFloat(index) * stepX
                let normalized = CGFloat(value) / CGFloat(maxValue)
                let y = size.height - (normalized * (size.height - 12)) - 6
                if index == 0 {
                    path.move(to: CGPoint(x: x, y: y))
                } else {
                    path.addLine(to: CGPoint(x: x, y: y))
                }
            }

            for row in 0..<4 {
                let y = CGFloat(row) * size.height / 3
                var grid = Path()
                grid.move(to: CGPoint(x: 0, y: y))
                grid.addLine(to: CGPoint(x: size.width, y: y))
                context.stroke(grid, with: .color(.white.opacity(0.08)), lineWidth: 1)
            }

            context.stroke(path, with: .color(Color(red: 0.66, green: 1.0, blue: 0.76)), lineWidth: 2.5)
        }
    }
}

struct RareEarthInventoryPanel: View {
    @ObservedObject var model: RareEarthInventoryModel

    var body: some View {
        WeatherGlassCard {
            VStack(alignment: .leading, spacing: 13) {
                HStack(alignment: .firstTextBaseline) {
                    VStack(alignment: .leading, spacing: 3) {
                        Text("Rare Earth")
                            .font(.system(size: 24, weight: .bold))
                        Text(model.subtitle)
                            .font(.system(size: 13, weight: .medium))
                            .foregroundStyle(.white.opacity(0.70))
                    }
                    Spacer()
                    if model.isLoading {
                        ProgressView()
                            .tint(.white)
                    } else {
                        Text("\(model.elements.count)")
                            .font(.system(size: 20, weight: .bold))
                            .monospacedDigit()
                    }
                }

                if model.elements.isEmpty {
                    Text(model.isLoading ? "Loading elements…" : "None collected")
                        .font(.system(size: 15, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.78))
                } else {
                    VStack(spacing: 0) {
                        ForEach(Array(model.elements.enumerated()), id: \.element.id) { index, element in
                            RareEarthRow(element: element)
                            if index < model.elements.count - 1 {
                                Rectangle()
                                    .fill(.white.opacity(0.08))
                                    .frame(height: 1)
                                    .padding(.vertical, 8)
                            }
                        }
                    }
                }
            }
        }
    }
}

struct RareEarthRow: View {
    let element: RareEarthElementTotal

    var body: some View {
        HStack(spacing: 12) {
            Text(element.symbol)
                .font(.system(size: 22, weight: .bold))
                .monospaced()
                .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                .frame(width: 42, alignment: .leading)

            Text(element.name.capitalized)
                .font(.system(size: 15, weight: .semibold))
                .lineLimit(1)

            Spacer()

            Text("\(element.grams)g")
                .font(.system(size: 15, weight: .bold))
                .monospacedDigit()
        }
        .foregroundStyle(.white.opacity(0.92))
    }
}

struct TriggerSettingsPanel: View {
    @ObservedObject var auth: FirebaseAuthModel
    @ObservedObject var inventory: RareEarthInventoryModel
    @AppStorage("k1l0_min_trigger_steps") private var minTriggerSteps = 300.0
    @AppStorage("k1l0_reset_grace_minutes") private var resetGraceMinutes = 10.0
    @AppStorage("k1l0_collect_radius_meters") private var collectRadiusMeters = 10.0

    var body: some View {
        WeatherGlassCard {
            VStack(alignment: .leading, spacing: 16) {
                VStack(alignment: .leading, spacing: 3) {
                    Text("Settings")
                        .font(.system(size: 24, weight: .bold))
                    Text("movement triggers")
                        .font(.system(size: 13, weight: .medium))
                        .foregroundStyle(.white.opacity(0.70))
                }

                VStack(alignment: .leading, spacing: 7) {
                    HStack {
                        Text("Min steps gate")
                        Spacer()
                        Text("\(Int(minTriggerSteps))")
                            .monospacedDigit()
                    }
                    .font(.system(size: 14, weight: .semibold))
                    Slider(value: $minTriggerSteps, in: 100...1000, step: 25)
                        .tint(Color(red: 0.66, green: 1.0, blue: 0.76))
                }

                VStack(alignment: .leading, spacing: 7) {
                    HStack {
                        Text("Reset grace")
                        Spacer()
                        Text("\(Int(resetGraceMinutes)) min")
                            .monospacedDigit()
                    }
                    .font(.system(size: 14, weight: .semibold))
                    Slider(value: $resetGraceMinutes, in: 1...30, step: 1)
                        .tint(Color(red: 0.66, green: 1.0, blue: 0.76))
                }

                VStack(alignment: .leading, spacing: 7) {
                    HStack {
                        Text("Collect radius")
                        Spacer()
                        Text("\(Int(collectRadiusMeters)) m")
                            .monospacedDigit()
                    }
                    .font(.system(size: 14, weight: .semibold))
                    Slider(value: $collectRadiusMeters, in: 1...100, step: 1)
                        .tint(Color(red: 0.66, green: 1.0, blue: 0.76))
                }

                Rectangle()
                    .fill(.white.opacity(0.08))
                    .frame(height: 1)

                VStack(alignment: .leading, spacing: 10) {
                    VStack(alignment: .leading, spacing: 3) {
                        Text("Account")
                            .font(.system(size: 15, weight: .bold))
                        Text(auth.localId.map { "signed in · uid \($0.prefix(10))…" } ?? auth.status)
                            .font(.system(size: 12, weight: .medium))
                            .foregroundStyle(.white.opacity(0.62))
                    }

                    HStack(spacing: 10) {
                        Button {
                            auth.login()
                        } label: {
                            AccountActionButton(title: "[ LOGIN ]", isPrimary: auth.localId == nil)
                        }
                        .buttonStyle(.plain)

                        Button {
                            auth.logout()
                            inventory.clear()
                        } label: {
                            AccountActionButton(title: "[ LOGOUT ]", isPrimary: auth.localId != nil)
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
        }
    }
}

struct AccountActionButton: View {
    let title: String
    let isPrimary: Bool

    var body: some View {
        Text(title)
            .font(.system(size: 13, weight: .bold))
            .frame(maxWidth: .infinity)
            .padding(.vertical, 11)
            .foregroundStyle(isPrimary ? .black : .white.opacity(0.78))
            .background(
                RoundedRectangle(cornerRadius: 14, style: .continuous)
                    .fill(isPrimary ? Color(red: 0.66, green: 1.0, blue: 0.76) : .white.opacity(0.08))
            )
            .overlay(
                RoundedRectangle(cornerRadius: 14, style: .continuous)
                    .stroke(.white.opacity(isPrimary ? 0.0 : 0.16), lineWidth: 1)
            )
    }
}

struct WeatherVideoBackground: UIViewRepresentable {
    let videoName: String

    func makeUIView(context: Context) -> UIView {
        let view = WeatherPlayerView()
        view.playerLayer.player = context.coordinator.player
        return view
    }

    func updateUIView(_ uiView: UIView, context: Context) {
        context.coordinator.play(videoName: videoName)
    }

    func makeCoordinator() -> Coordinator {
        Coordinator()
    }

    final class Coordinator {
        let player = AVQueuePlayer()
        var looper: AVPlayerLooper?
        private var currentName = ""

        func play(videoName: String) {
            guard currentName != videoName else { return }
            currentName = videoName

            guard let url = Bundle.main.url(forResource: videoName, withExtension: "mp4", subdirectory: "WeatherVideos") else {
                return
            }

            player.removeAllItems()
            let item = AVPlayerItem(url: url)
            looper = AVPlayerLooper(player: player, templateItem: item)
            player.isMuted = true
            player.play()
        }
    }
}

final class WeatherPlayerView: UIView {
    let playerLayer = AVPlayerLayer()

    override init(frame: CGRect) {
        super.init(frame: frame)
        backgroundColor = .black
        playerLayer.videoGravity = .resizeAspectFill
        layer.addSublayer(playerLayer)
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override func layoutSubviews() {
        super.layoutSubviews()
        playerLayer.frame = bounds
    }
}

struct K1L0MapPreview: View {
    private let region = MKCoordinateRegion(
        center: CLLocationCoordinate2D(latitude: 40.684, longitude: -80.107),
        span: MKCoordinateSpan(latitudeDelta: 0.025, longitudeDelta: 0.025)
    )

    var body: some View {
        Map(initialPosition: .region(region))
            .mapStyle(.standard(elevation: .realistic))
            .overlay(alignment: .topLeading) {
                Text("nearby field")
                    .font(.system(size: 15, weight: .semibold))
                    .padding(.horizontal, 14)
                    .padding(.vertical, 10)
                    .background(.ultraThinMaterial, in: Capsule())
                    .padding(14)
            }
    }
}

struct NearbyPlacesPanel: View {
    @ObservedObject var model: NearbyPlacesModel

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 3) {
                    Text("Nearby")
                        .font(.system(size: 25, weight: .bold))
                    Text(model.subtitle)
                        .font(.system(size: 13, weight: .medium))
                        .foregroundStyle(.white.opacity(0.70))
                }
                Spacer()
                if model.isLoading {
                    ProgressView()
                        .tint(.white)
                } else {
                    Image(systemName: "location.fill")
                        .font(.system(size: 17, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.78))
                }
            }

            WeatherGlassCard {
                VStack(alignment: .leading, spacing: 0) {
                    LocationFilterRow(selectedFilter: $model.selectedFilter)
                        .padding(.bottom, 14)

                    if model.filteredPlaces.isEmpty {
                        Text(model.isLoading ? "Finding open locations…" : "No open locations found")
                            .font(.system(size: 16, weight: .semibold))
                        Text("K1L0 checks nearby cafes, bars, restaurants, stores, and walkable places.")
                            .font(.system(size: 13, weight: .medium))
                            .foregroundStyle(.white.opacity(0.68))
                            .padding(.top, 6)
                            .fixedSize(horizontal: false, vertical: true)
                    } else {
                        ForEach(Array(model.filteredPlaces.prefix(12).enumerated()), id: \.element.id) { index, place in
                            PlaceDataRow(
                                place: place,
                                distance: model.distanceText(for: place),
                                relativeBearing: model.relativeBearingDegrees(to: place)
                            )
                            if index < min(model.filteredPlaces.count, 12) - 1 {
                                Rectangle()
                                    .fill(.white.opacity(0.08))
                                    .frame(height: 1)
                                    .padding(.vertical, 8)
                            }
                        }
                    }
                }
            }
        }
    }
}

struct LocationFilterRow: View {
    @Binding var selectedFilter: LocationFilter

    var body: some View {
        HStack(spacing: 8) {
            ForEach(LocationFilter.allCases) { filter in
                Button {
                    selectedFilter = filter
                } label: {
                    VStack(spacing: 4) {
                        Image(systemName: filter.icon)
                            .font(.system(size: 13, weight: .semibold))
                        Text(filter.title)
                            .font(.system(size: 9, weight: .bold))
                    }
                    .foregroundStyle(selectedFilter == filter ? .black : .white.opacity(0.78))
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 8)
                    .background(
                        RoundedRectangle(cornerRadius: 14, style: .continuous)
                            .fill(selectedFilter == filter ? Color(red: 0.66, green: 1.0, blue: 0.76) : .white.opacity(0.08))
                    )
                }
                .buttonStyle(.plain)
            }
        }
    }
}

enum LocationFilter: String, CaseIterable, Identifiable {
    case all
    case drink
    case coffee
    case food
    case service

    var id: String { rawValue }

    var title: String {
        switch self {
        case .all: "all"
        case .drink: "drink"
        case .coffee: "coffee"
        case .food: "food"
        case .service: "service"
        }
    }

    var icon: String {
        switch self {
        case .all: "circle.grid.2x2.fill"
        case .drink: "wineglass.fill"
        case .coffee: "cup.and.saucer.fill"
        case .food: "fork.knife"
        case .service: "bag.fill"
        }
    }
}

struct TransmissionPanel: View {
    @AppStorage("k1l0_transmission_spirits") private var spirits = 0.52
    @State private var selectedPhotoItem: PhotosPickerItem?
    @State private var selectedImage: UIImage?
    @State private var status = "idle"
    @State private var pulse = false

    var body: some View {
        WeatherGlassCard {
            VStack(alignment: .leading, spacing: 14) {
                HStack(alignment: .firstTextBaseline) {
                    VStack(alignment: .leading, spacing: 3) {
                        Text("Transmission")
                            .font(.system(size: 24, weight: .bold))
                        Text(status.uppercased())
                            .font(.system(size: 13, weight: .medium))
                            .foregroundStyle(.white.opacity(0.70))
                    }
                    Spacer()
                    PulsingSignalView(pulse: pulse)
                }

                if let selectedImage {
                    Image(uiImage: selectedImage)
                        .resizable()
                        .scaledToFill()
                        .frame(height: 150)
                        .clipShape(RoundedRectangle(cornerRadius: 20, style: .continuous))
                        .overlay(
                            RoundedRectangle(cornerRadius: 20, style: .continuous)
                                .stroke(.white.opacity(0.18), lineWidth: 1)
                        )
                }

                VStack(alignment: .leading, spacing: 7) {
                    HStack {
                        Text("Spirits")
                            .font(.system(size: 14, weight: .bold))
                        Spacer()
                        Text(spiritLabel.uppercased())
                            .font(.system(size: 12, weight: .bold))
                            .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                    }

                    Slider(value: $spirits, in: 0...1)
                        .tint(Color(red: 0.66, green: 1.0, blue: 0.76))

                    Text("status: \(transmissionStatus)")
                        .font(.system(size: 12, weight: .medium))
                        .foregroundStyle(.white.opacity(0.64))
                }

                HStack(spacing: 10) {
                    PhotosPicker(selection: $selectedPhotoItem, matching: .images) {
                        Text(selectedImage == nil ? "[ ATTACH PHOTO ]" : "[ CHANGE PHOTO ]")
                            .font(.system(size: 14, weight: .bold))
                            .frame(maxWidth: .infinity)
                            .padding(.vertical, 13)
                            .background(.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 16, style: .continuous))
                            .overlay(
                                RoundedRectangle(cornerRadius: 16, style: .continuous)
                                    .stroke(.white.opacity(0.18), lineWidth: 1)
                            )
                    }
                    .buttonStyle(.plain)

                    Button {
                        status = selectedImage == nil ? "attach photo first" : "transmitting photo"
                        pulse = selectedImage != nil
                    } label: {
                        Text("[ TRANSMIT ]")
                            .font(.system(size: 14, weight: .bold))
                            .frame(maxWidth: .infinity)
                            .padding(.vertical, 13)
                            .background(Color(red: 0.66, green: 1.0, blue: 0.76).opacity(selectedImage == nil ? 0.18 : 0.95), in: RoundedRectangle(cornerRadius: 16, style: .continuous))
                            .foregroundStyle(selectedImage == nil ? .white.opacity(0.55) : .black)
                    }
                    .buttonStyle(.plain)
                }
            }
        }
        .onAppear {
            pulse = true
            loadPersistedPhoto()
        }
        .onChange(of: selectedPhotoItem) { _, newItem in
            guard let newItem else { return }
            status = "loading photo"
            Task {
                if let data = try? await newItem.loadTransferable(type: Data.self),
                   let image = UIImage(data: data) {
                    await MainActor.run {
                        selectedImage = image
                        persistPhoto(image)
                        status = "photo attached"
                    }
                } else {
                    await MainActor.run {
                        status = "photo unavailable"
                    }
                }
            }
        }
    }

    private var spiritLabel: String {
        if spirits < 0.34 { return "low" }
        if spirits > 0.68 { return "high" }
        return "steady"
    }

    private var transmissionStatus: String {
        if selectedImage == nil {
            return "attach a photo · spirits \(spiritLabel)"
        }
        return "photo armed · spirits \(spiritLabel)"
    }

    private func loadPersistedPhoto() {
        guard selectedImage == nil,
              let data = try? Data(contentsOf: Self.photoURL),
              let image = UIImage(data: data)
        else { return }
        selectedImage = image
        status = "photo attached"
    }

    private func persistPhoto(_ image: UIImage) {
        guard let data = image.jpegData(compressionQuality: 0.86) else { return }
        try? data.write(to: Self.photoURL, options: [.atomic])
    }

    private static var photoURL: URL {
        let documents = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first!
        return documents.appendingPathComponent("k1l0-transmission-photo.jpg")
    }
}

struct PulsingSignalView: View {
    let pulse: Bool

    var body: some View {
        ZStack {
            Circle()
                .stroke(Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.35), lineWidth: 2)
                .frame(width: 44, height: 44)
                .scaleEffect(pulse ? 1.12 : 0.82)
                .opacity(pulse ? 0.24 : 0.82)
                .animation(.easeInOut(duration: 1.05).repeatForever(autoreverses: true), value: pulse)
            Image(systemName: "dot.radiowaves.left.and.right")
                .font(.system(size: 19, weight: .bold))
                .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
        }
    }
}

struct PlaceDataRow: View {
    let place: NearbyPlace
    let distance: String
    let relativeBearing: Double

    var body: some View {
        HStack(spacing: 10) {
            VStack(spacing: 2) {
                Text(distance)
                    .font(.system(size: 10, weight: .semibold))
                    .monospacedDigit()
                    .foregroundStyle(.white.opacity(0.62))
                Image(systemName: "location.north.fill")
                    .font(.system(size: 14, weight: .bold))
                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                    .rotationEffect(.degrees(relativeBearing))
                    .frame(width: 18, height: 18)
            }
            .frame(width: 46)

            VStack(alignment: .leading, spacing: 2) {
                Text(place.name)
                    .font(.system(size: 15, weight: .semibold))
                    .lineLimit(1)
                    .minimumScaleFactor(0.78)
                Text(place.displayType)
                    .font(.system(size: 10, weight: .medium))
                    .foregroundStyle(.white.opacity(0.55))
                    .lineLimit(1)
            }

            Spacer(minLength: 8)

            Text(place.rareEarthSymbol)
                .font(.system(size: 18, weight: .bold))
                .monospaced()
                .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                .frame(width: 34, alignment: .trailing)
        }
        .foregroundStyle(.white.opacity(0.92))
    }
}

struct WeatherGlassCard<Content: View>: View {
    @ViewBuilder var content: Content

    var body: some View {
        content
            .foregroundStyle(.white)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(18)
            .background(
                RoundedRectangle(cornerRadius: 26, style: .continuous)
                    .fill(.black.opacity(0.18))
                    .overlay(
                        LinearGradient(
                            colors: [.white.opacity(0.12), .white.opacity(0.025)],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        )
                        .clipShape(RoundedRectangle(cornerRadius: 26, style: .continuous))
                    )
            )
            .overlay(
                RoundedRectangle(cornerRadius: 26, style: .continuous)
                    .stroke(.white.opacity(0.12), lineWidth: 1)
            )
    }
}

final class StepModel: ObservableObject {
    @Published var liveSteps = 0
    @Published var steps24h = 0
    @Published var steps7d = 0
    @Published var hourlySteps = Array(repeating: 0, count: 24)

    private let pedometer = CMPedometer()
    private var lastRefresh = Date.distantPast
    private var activeBaselineSteps = 0
    private var sessionStepsAtRefresh = 0
    private var latestSessionSteps = 0

    func start() {
        guard CMPedometer.isStepCountingAvailable() else { return }

        refreshStepData()

        pedometer.startUpdates(from: Date()) { [weak self] data, _ in
            DispatchQueue.main.async {
                guard let self else { return }
                self.latestSessionSteps = data?.numberOfSteps.intValue ?? self.latestSessionSteps
                let delta = max(0, self.latestSessionSteps - self.sessionStepsAtRefresh)
                self.liveSteps = self.activeBaselineSteps + delta
                if Date().timeIntervalSince(self.lastRefresh) > 25 {
                    self.refreshStepData()
                }
            }
        }
    }

    private func refreshStepData() {
        lastRefresh = Date()
        queryTotals()
        queryHourlyBuckets()
        queryActiveSteps()
    }

    private func queryTotals() {
        let now = Date()
        if let dayStart = Calendar.current.date(byAdding: .hour, value: -24, to: now) {
            pedometer.queryPedometerData(from: dayStart, to: now) { [weak self] data, _ in
                DispatchQueue.main.async {
                    self?.steps24h = data?.numberOfSteps.intValue ?? 0
                }
            }
        }
        if let weekStart = Calendar.current.date(byAdding: .day, value: -7, to: now) {
            pedometer.queryPedometerData(from: weekStart, to: now) { [weak self] data, _ in
                DispatchQueue.main.async {
                    self?.steps7d = data?.numberOfSteps.intValue ?? 0
                }
            }
        }
    }

    private func queryHourlyBuckets() {
        let now = Date()
        let group = DispatchGroup()
        var results = Array(repeating: 0, count: 24)
        let lock = NSLock()

        for index in 0..<24 {
            guard
                let start = Calendar.current.date(byAdding: .hour, value: index - 24, to: now),
                let end = Calendar.current.date(byAdding: .hour, value: index - 23, to: now)
            else { continue }

            group.enter()
            pedometer.queryPedometerData(from: start, to: end) { data, _ in
                lock.lock()
                results[index] = data?.numberOfSteps.intValue ?? 0
                lock.unlock()
                group.leave()
            }
        }

        group.notify(queue: .main) { [weak self] in
            self?.hourlySteps = results
        }
    }

    private func queryActiveSteps() {
        let now = Date()
        let bucketMinutes = 5
        let bucketCount = 288
        let group = DispatchGroup()
        var buckets = Array(repeating: 0, count: bucketCount)
        let lock = NSLock()

        for index in 0..<bucketCount {
            guard
                let start = Calendar.current.date(byAdding: .minute, value: (index - bucketCount) * bucketMinutes, to: now),
                let end = Calendar.current.date(byAdding: .minute, value: (index - bucketCount + 1) * bucketMinutes, to: now)
            else { continue }

            group.enter()
            pedometer.queryPedometerData(from: start, to: end) { data, _ in
                lock.lock()
                buckets[index] = data?.numberOfSteps.intValue ?? 0
                lock.unlock()
                group.leave()
            }
        }

        group.notify(queue: .main) { [weak self] in
            let storedGrace = UserDefaults.standard.object(forKey: "k1l0_reset_grace_minutes") as? Double
            let graceMinutes = max(1, storedGrace ?? 10)
            let inactiveBucketLimit = max(1, Int(ceil(graceMinutes / Double(bucketMinutes))))
            var inactiveRun = 0
            var activeSteps = 0

            for steps in buckets.reversed() {
                if steps < 2 {
                    inactiveRun += 1
                    if inactiveRun >= inactiveBucketLimit {
                        break
                    }
                } else {
                    inactiveRun = 0
                }
                activeSteps += steps
            }

            self?.activeBaselineSteps = activeSteps
            self?.sessionStepsAtRefresh = self?.latestSessionSteps ?? 0
            self?.liveSteps = activeSteps
        }
    }
}

final class WeatherModel: NSObject, ObservableObject, CLLocationManagerDelegate {
    @Published var temperatureText = "--°"
    @Published var glyph = "cloud.sun.fill"
    @Published var videoName = "clear-day"

    private let locationManager = CLLocationManager()
    private var didRequestWeather = false

    func start() {
        locationManager.delegate = self
        locationManager.desiredAccuracy = kCLLocationAccuracyThreeKilometers

        switch locationManager.authorizationStatus {
        case .notDetermined:
            locationManager.requestWhenInUseAuthorization()
        case .authorizedWhenInUse, .authorizedAlways:
            locationManager.requestLocation()
        default:
            fetchWeather(latitude: 40.684, longitude: -80.107)
        }
    }

    func locationManagerDidChangeAuthorization(_ manager: CLLocationManager) {
        switch manager.authorizationStatus {
        case .authorizedWhenInUse, .authorizedAlways:
            manager.requestLocation()
        case .denied, .restricted:
            fetchWeather(latitude: 40.684, longitude: -80.107)
        default:
            break
        }
    }

    func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        guard let location = locations.last, !didRequestWeather else { return }
        didRequestWeather = true
        fetchWeather(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
    }

    func locationManager(_ manager: CLLocationManager, didFailWithError error: Error) {
        fetchWeather(latitude: 40.684, longitude: -80.107)
    }

    private func fetchWeather(latitude: Double, longitude: Double) {
        let urlString = String(format: "https://wttr.in/%.5f,%.5f?format=j1", latitude, longitude)
        guard let url = URL(string: urlString) else { return }

        URLSession.shared.dataTask(with: url) { [weak self] data, _, _ in
            guard
                let data,
                let payload = try? JSONDecoder().decode(WttrResponse.self, from: data),
                let current = payload.currentCondition.first
            else { return }

            DispatchQueue.main.async {
                self?.applyWeather(current)
            }
        }.resume()
    }

    private func applyWeather(_ current: WttrCurrentCondition) {
        let roundedTemp = Int(current.tempF) ?? 0
        let description = current.weatherDesc.first?.value.lowercased() ?? ""
        let isNight = Self.isNightNow()
        temperatureText = "\(roundedTemp)°"
        glyph = Self.glyph(for: description, isNight: isNight)
        videoName = Self.video(for: description, isNight: isNight)
    }

    private static func glyph(for description: String, isNight: Bool) -> String {
        if description.contains("thunder") || description.contains("storm") {
            return "cloud.bolt.rain.fill"
        }
        if description.contains("rain") || description.contains("drizzle") || description.contains("shower") {
            return "cloud.rain.fill"
        }
        if description.contains("snow") || description.contains("sleet") || description.contains("ice") {
            return "snowflake"
        }
        if description.contains("cloud") || description.contains("overcast") || description.contains("mist") || description.contains("fog") {
            return isNight ? "cloud.moon.fill" : "cloud.fill"
        }
        return isNight ? "moon.stars.fill" : "sun.max.fill"
    }

    private static func video(for description: String, isNight: Bool) -> String {
        if description.contains("thunder") || description.contains("storm") {
            return "thunder"
        }
        if description.contains("rain") || description.contains("drizzle") || description.contains("shower") {
            return isNight ? "raining-night" : "raining-day"
        }
        if description.contains("cloud") || description.contains("overcast") || description.contains("mist") || description.contains("fog") {
            return isNight ? "cloud-night-1" : "cloud-day-2"
        }
        return isNight ? "clear-night" : "clear-day"
    }

    private static func isNightNow() -> Bool {
        let hour = Calendar.current.component(.hour, from: Date())
        return hour < 6 || hour >= 20
    }
}

private struct WttrResponse: Decodable {
    let currentCondition: [WttrCurrentCondition]

    enum CodingKeys: String, CodingKey {
        case currentCondition = "current_condition"
    }
}

private struct WttrCurrentCondition: Decodable {
    let tempF: String
    let weatherDesc: [WttrWeatherDescription]

    enum CodingKeys: String, CodingKey {
        case tempF = "temp_F"
        case weatherDesc
    }
}

private struct WttrWeatherDescription: Decodable {
    let value: String
}

final class NearbyPlacesModel: NSObject, ObservableObject, CLLocationManagerDelegate {
    @Published var places: [NearbyPlace] = []
    @Published var selectedFilter: LocationFilter = .all
    @Published var isLoading = true
    @Published var subtitle = "open locations near you"
    @Published private var currentLocation: CLLocation?
    @Published private var headingDegrees: Double = 0

    private let locationManager = CLLocationManager()
    private var didFetch = false

    var filteredPlaces: [NearbyPlace] {
        places.filter { $0.matches(selectedFilter) }
    }

    func start() {
        locationManager.delegate = self
        locationManager.desiredAccuracy = kCLLocationAccuracyBest
        locationManager.distanceFilter = 1
        locationManager.pausesLocationUpdatesAutomatically = false

        switch locationManager.authorizationStatus {
        case .notDetermined:
            locationManager.requestWhenInUseAuthorization()
        case .authorizedWhenInUse, .authorizedAlways:
            beginLiveLocation()
        default:
            useFallbackLocation()
        }
    }

    func locationManagerDidChangeAuthorization(_ manager: CLLocationManager) {
        switch manager.authorizationStatus {
        case .authorizedWhenInUse, .authorizedAlways:
            beginLiveLocation()
        case .denied, .restricted:
            useFallbackLocation()
        default:
            break
        }
    }

    func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        guard let location = locations.last else { return }
        currentLocation = location
        if !didFetch {
            didFetch = true
            fetchPlaces(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
        }
    }

    func locationManager(_ manager: CLLocationManager, didUpdateHeading newHeading: CLHeading) {
        let heading = newHeading.trueHeading >= 0 ? newHeading.trueHeading : newHeading.magneticHeading
        if heading >= 0 {
            headingDegrees = heading
        }
    }

    func locationManager(_ manager: CLLocationManager, didFailWithError error: Error) {
        useFallbackLocation()
    }

    func distanceText(for place: NearbyPlace) -> String {
        let meters = distanceMeters(to: place) ?? place.distance
        guard let meters else { return "nearby" }
        if meters < 528 {
            return "\(Int((meters * 3.28084).rounded())) ft"
        }
        return String(format: "%.1f mi", meters / 1609.344)
    }

    func relativeBearingDegrees(to place: NearbyPlace) -> Double {
        guard let location = currentLocation else { return 0 }
        let bearing = Self.bearingDegrees(
            from: location.coordinate,
            to: CLLocationCoordinate2D(latitude: place.coordinates.lat, longitude: place.coordinates.lng)
        )
        return bearing - headingDegrees
    }

    private func beginLiveLocation() {
        locationManager.startUpdatingLocation()
        if CLLocationManager.headingAvailable() {
            locationManager.startUpdatingHeading()
        }
    }

    private func useFallbackLocation() {
        let fallback = CLLocation(latitude: 40.684, longitude: -80.107)
        currentLocation = fallback
        if !didFetch {
            didFetch = true
            fetchPlaces(latitude: fallback.coordinate.latitude, longitude: fallback.coordinate.longitude)
        }
    }

    private func distanceMeters(to place: NearbyPlace) -> Double? {
        guard let location = currentLocation else { return nil }
        let placeLocation = CLLocation(latitude: place.coordinates.lat, longitude: place.coordinates.lng)
        return location.distance(from: placeLocation)
    }

    private func fetchPlaces(latitude: Double, longitude: Double) {
        guard let url = URL(string: "https://api.kilomeme.com/places") else { return }
        isLoading = true

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        let body: [String: Any] = [
            "latitude": latitude,
            "longitude": longitude,
            "radiusMeters": 1609
        ]
        request.httpBody = try? JSONSerialization.data(withJSONObject: body)

        URLSession.shared.dataTask(with: request) { [weak self] data, _, _ in
            guard
                let data,
                let response = try? JSONDecoder().decode(PlacesAPIResponse.self, from: data)
            else {
                DispatchQueue.main.async {
                    self?.isLoading = false
                    self?.subtitle = "location scan unavailable"
                }
                return
            }

            DispatchQueue.main.async {
                self?.places = response.places.sorted { $0.distanceValue < $1.distanceValue }
                self?.isLoading = false
                self?.subtitle = response.places.isEmpty ? "no open places nearby" : "\(response.places.count) open places nearby"
            }
        }.resume()
    }

    private static func bearingDegrees(from start: CLLocationCoordinate2D, to end: CLLocationCoordinate2D) -> Double {
        let lat1 = start.latitude * .pi / 180
        let lat2 = end.latitude * .pi / 180
        let deltaLon = (end.longitude - start.longitude) * .pi / 180
        let y = sin(deltaLon) * cos(lat2)
        let x = cos(lat1) * sin(lat2) - sin(lat1) * cos(lat2) * cos(deltaLon)
        let degrees = atan2(y, x) * 180 / .pi
        return (degrees + 360).truncatingRemainder(dividingBy: 360)
    }
}

private struct PlacesAPIResponse: Decodable {
    let ok: Bool
    let places: [NearbyPlace]
}

struct NearbyPlace: Decodable, Identifiable {
    let name: String
    let type: String?
    let types: [String]?
    let businessStatus: String?
    let distance: Double?
    let closingTime: String?
    let coordinates: PlaceCoordinate
    let artifactMaterial: String?

    var id: String {
        "\(name)-\(distanceValue)"
    }

    var distanceValue: Double {
        distance ?? Double.greatestFiniteMagnitude
    }

    var displayType: String {
        let raw = type ?? types?.first ?? "place"
        return raw.replacingOccurrences(of: "_", with: " ").capitalized
    }

    var rareEarthSymbol: String {
        Self.symbol(for: artifactMaterial)
    }

    func matches(_ filter: LocationFilter) -> Bool {
        guard filter != .all else { return true }
        let haystack = ([type].compactMap { $0 } + (types ?? [])).joined(separator: " ").lowercased()
        switch filter {
        case .all:
            return true
        case .drink:
            return haystack.contains("bar") || haystack.contains("pub") || haystack.contains("brewery") || haystack.contains("wine") || haystack.contains("liquor")
        case .coffee:
            return haystack.contains("coffee") || haystack.contains("cafe")
        case .food:
            return haystack.contains("restaurant") || haystack.contains("food") || haystack.contains("bakery") || haystack.contains("sandwich")
        case .service:
            return haystack.contains("store") || haystack.contains("service") || haystack.contains("shop") || haystack.contains("convenience")
        }
    }

    static func symbol(for material: String?) -> String {
        let normalized = (material ?? "").lowercased()
        if normalized.contains("scandium") { return "Sc" }
        if normalized.contains("yttrium") { return "Y" }
        if normalized.contains("lanthanum") { return "La" }
        if normalized.contains("cerium") { return "Ce" }
        if normalized.contains("praseodymium") { return "Pr" }
        if normalized.contains("neodymium") { return "Nd" }
        if normalized.contains("promethium") { return "Pm" }
        if normalized.contains("samarium") { return "Sm" }
        if normalized.contains("europium") { return "Eu" }
        if normalized.contains("gadolinium") { return "Gd" }
        if normalized.contains("terbium") { return "Tb" }
        if normalized.contains("dysprosium") { return "Dy" }
        if normalized.contains("holmium") { return "Ho" }
        if normalized.contains("erbium") { return "Er" }
        if normalized.contains("thulium") { return "Tm" }
        if normalized.contains("ytterbium") { return "Yb" }
        if normalized.contains("lutetium") { return "Lu" }
        return "RE"
    }
}

struct PlaceCoordinate: Decodable {
    let lat: Double
    let lng: Double
}

final class NearbyBeamModel: NSObject, ObservableObject, CLLocationManagerDelegate {
    @Published var beams: [NearbyBeam] = []
    @Published var statusMessage: String?
    @Published private var currentLocation: CLLocation?
    @Published private var headingDegrees: Double = 0

    private let locationManager = CLLocationManager()
    private var didFetch = false
    private weak var auth: FirebaseAuthModel?
    private weak var inventory: RareEarthInventoryModel?
    private var acceptingIds = Set<String>()

    var nearestBeam: NearbyBeam? {
        beams.sorted {
            (distanceMeters(to: $0) ?? $0.distanceMeters) < (distanceMeters(to: $1) ?? $1.distanceMeters)
        }.first
    }

    func configure(auth: FirebaseAuthModel, inventory: RareEarthInventoryModel) {
        self.auth = auth
        self.inventory = inventory
        autoAcceptNearestIfNeeded()
    }

    func start() {
        locationManager.delegate = self
        locationManager.desiredAccuracy = kCLLocationAccuracyBest
        locationManager.distanceFilter = 1
        locationManager.pausesLocationUpdatesAutomatically = false

        switch locationManager.authorizationStatus {
        case .notDetermined:
            locationManager.requestWhenInUseAuthorization()
        case .authorizedWhenInUse, .authorizedAlways:
            beginLiveLocation()
        default:
            useFallbackLocation()
        }
    }

    func locationManagerDidChangeAuthorization(_ manager: CLLocationManager) {
        switch manager.authorizationStatus {
        case .authorizedWhenInUse, .authorizedAlways:
            beginLiveLocation()
        case .denied, .restricted:
            useFallbackLocation()
        default:
            break
        }
    }

    func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        guard let location = locations.last else { return }
        currentLocation = location
        if !didFetch {
            didFetch = true
            fetchBeams(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
        }
        updateApproachStatus()
        autoAcceptNearestIfNeeded()
    }

    func locationManager(_ manager: CLLocationManager, didUpdateHeading newHeading: CLHeading) {
        let heading = newHeading.trueHeading >= 0 ? newHeading.trueHeading : newHeading.magneticHeading
        if heading >= 0 {
            headingDegrees = heading
        }
    }

    func locationManager(_ manager: CLLocationManager, didFailWithError error: Error) {
        useFallbackLocation()
    }

    func distanceText(for beam: NearbyBeam) -> String {
        let meters = distanceMeters(to: beam) ?? beam.distanceMeters
        if meters < 160.934 {
            return "\(Int((meters * 3.28084).rounded())) ft"
        }
        return String(format: "%.1f mi", meters / 1609.344)
    }

    func relativeBearingDegrees(to beam: NearbyBeam) -> Double {
        guard let location = currentLocation else { return 0 }
        let bearing = Self.bearingDegrees(
            from: location.coordinate,
            to: CLLocationCoordinate2D(latitude: beam.lat, longitude: beam.lng)
        )
        return bearing - headingDegrees
    }

    func isAcceptable(_ beam: NearbyBeam) -> Bool {
        let radius = UserDefaults.standard.object(forKey: "k1l0_collect_radius_meters") as? Double ?? 10.0
        return (distanceMeters(to: beam) ?? beam.distanceMeters) <= max(1.0, min(100.0, radius))
    }

    func accept(beam: NearbyBeam, auth: FirebaseAuthModel, completion: @escaping () -> Void) {
        guard !acceptingIds.contains(beam.id) else { return }
        guard let userId = auth.localId, let idToken = auth.idToken else {
            statusMessage = "auth pending"
            return
        }
        acceptingIds.insert(beam.id)
        statusMessage = "accepting \(beam.title)"

        visitBeam(beamId: beam.id)
        writeItem(beam: beam, userId: userId, idToken: idToken) { [weak self] ok in
            DispatchQueue.main.async {
                if ok {
                    self?.beams.removeAll { $0.id == beam.id }
                    self?.acceptingIds.remove(beam.id)
                    self?.statusMessage = "accepted \(beam.title)"
                    completion()
                } else {
                    self?.acceptingIds.remove(beam.id)
                    self?.statusMessage = "accept failed"
                }
            }
        }
    }

    private func autoAcceptNearestIfNeeded() {
        guard let beam = nearestBeam, isAcceptable(beam), let auth else { return }
        accept(beam: beam, auth: auth) { [weak self] in
            self?.inventory?.refresh()
        }
    }

    private func updateApproachStatus() {
        guard acceptingIds.isEmpty, let beam = nearestBeam else { return }
        statusMessage = isAcceptable(beam) ? "within range" : "approach · \(distanceText(for: beam))"
    }

    private func beginLiveLocation() {
        locationManager.startUpdatingLocation()
        if CLLocationManager.headingAvailable() {
            locationManager.startUpdatingHeading()
        }
    }

    private func useFallbackLocation() {
        let fallback = CLLocation(latitude: 40.684, longitude: -80.107)
        currentLocation = fallback
        if !didFetch {
            didFetch = true
            fetchBeams(latitude: fallback.coordinate.latitude, longitude: fallback.coordinate.longitude)
        }
    }

    private func distanceMeters(to beam: NearbyBeam) -> Double? {
        guard let location = currentLocation else { return nil }
        return location.distance(from: CLLocation(latitude: beam.lat, longitude: beam.lng))
    }

    private func fetchBeams(latitude: Double, longitude: Double) {
        guard let url = URL(string: "http://192.168.40.34:3000/k1l0/beams/nearby") else { return }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        let body: [String: Any?] = [
            "latitude": latitude,
            "longitude": longitude,
            "maxMiles": 1.1,
            "stepMeters": 75,
            "minDistanceMeters": 45,
            "movementBearing": nil
        ]
        request.httpBody = try? JSONSerialization.data(withJSONObject: body.compactMapValues { $0 })

        URLSession.shared.dataTask(with: request) { [weak self] data, _, _ in
            guard
                let data,
                let response = try? JSONDecoder().decode(NearbyBeamsResponse.self, from: data)
            else { return }

            DispatchQueue.main.async {
                self?.beams = response.beams
                self?.updateApproachStatus()
                self?.autoAcceptNearestIfNeeded()
            }
        }.resume()
    }

    private func visitBeam(beamId: String) {
        guard let url = URL(string: "http://192.168.40.34:3000/k1l0/beams/visit") else { return }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: ["beamId": beamId])
        URLSession.shared.dataTask(with: request).resume()
    }

    private func writeItem(beam: NearbyBeam, userId: String, idToken: String, completion: @escaping (Bool) -> Void) {
        let safeUserId = userId.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? userId
        guard let token = idToken.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed),
              let url = URL(string: "https://kiloworld-aa8d6-default-rtdb.firebaseio.com/users/\(safeUserId)/items.json?auth=\(token)") else {
            completion(false)
            return
        }

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        let material = beam.material.isEmpty ? beam.title : beam.material
        let payload: [String: Any] = [
            "artifact": material,
            "material": material,
            "grams": 1,
            "beamId": beam.id,
            "source": "native",
            "createdAt": Int(Date().timeIntervalSince1970 * 1000)
        ]
        request.httpBody = try? JSONSerialization.data(withJSONObject: payload)

        URLSession.shared.dataTask(with: request) { _, response, error in
            let status = (response as? HTTPURLResponse)?.statusCode ?? 0
            completion(error == nil && status >= 200 && status < 300)
        }.resume()
    }

    private static func bearingDegrees(from start: CLLocationCoordinate2D, to end: CLLocationCoordinate2D) -> Double {
        let lat1 = start.latitude * .pi / 180
        let lat2 = end.latitude * .pi / 180
        let deltaLon = (end.longitude - start.longitude) * .pi / 180
        let y = sin(deltaLon) * cos(lat2)
        let x = cos(lat1) * sin(lat2) - sin(lat1) * cos(lat2) * cos(deltaLon)
        let degrees = atan2(y, x) * 180 / .pi
        return (degrees + 360).truncatingRemainder(dividingBy: 360)
    }
}

private struct NearbyBeamsResponse: Decodable {
    let ok: Bool
    let beams: [NearbyBeam]
}

struct NearbyBeam: Decodable, Identifiable {
    let id: String
    let type: String
    let lat: Double
    let lng: Double
    let label: String
    let material: String
    let senderName: String
    let distanceMeters: Double

    var title: String {
        if !material.isEmpty { return material }
        if !label.isEmpty { return label }
        return type.capitalized
    }

    var symbol: String {
        NearbyPlace.symbol(for: material)
    }
}

final class FirebaseAuthModel: ObservableObject {
    @Published var localId: String?
    @Published var idToken: String?
    @Published var status = "auth pending"

    private let apiKey = "AIzaSyCJYzfGpp9lYBkIHlAyflGJ-vaT1WfpzjU"

    func start() {
        if let uid = UserDefaults.standard.string(forKey: "k1l0_firebase_uid"),
           let token = UserDefaults.standard.string(forKey: "k1l0_firebase_id_token") {
            localId = uid
            idToken = token
            status = "authed"
            return
        }

        signInAnonymously()
    }

    func login() {
        status = "auth pending"
        signInAnonymously()
    }

    func logout() {
        UserDefaults.standard.removeObject(forKey: "k1l0_firebase_uid")
        UserDefaults.standard.removeObject(forKey: "k1l0_firebase_id_token")
        localId = nil
        idToken = nil
        status = "logged out"
    }

    private func signInAnonymously() {
        guard let url = URL(string: "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=\(apiKey)") else { return }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: ["returnSecureToken": true])

        URLSession.shared.dataTask(with: request) { [weak self] data, _, _ in
            guard
                let data,
                let response = try? JSONDecoder().decode(FirebaseAnonymousAuthResponse.self, from: data)
            else {
                DispatchQueue.main.async { self?.status = "auth failed" }
                return
            }

            UserDefaults.standard.set(response.localId, forKey: "k1l0_firebase_uid")
            UserDefaults.standard.set(response.idToken, forKey: "k1l0_firebase_id_token")
            DispatchQueue.main.async {
                self?.localId = response.localId
                self?.idToken = response.idToken
                self?.status = "authed"
            }
        }.resume()
    }
}

private struct FirebaseAnonymousAuthResponse: Decodable {
    let idToken: String
    let localId: String
}

final class RareEarthInventoryModel: ObservableObject {
    @Published var elements: [RareEarthElementTotal] = []
    @Published var isLoading = true
    @Published var subtitle = "collected elements"

    private weak var auth: FirebaseAuthModel?

    func start(auth: FirebaseAuthModel) {
        self.auth = auth
        fetchInventory()
    }

    func refresh() {
        fetchInventory()
    }

    func clear() {
        elements = []
        isLoading = false
        subtitle = "logged out"
    }

    private func fetchInventory() {
        guard let userId = auth?.localId, let idToken = auth?.idToken else {
            isLoading = false
            subtitle = "auth pending"
            return
        }
        let safeUserId = userId.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? userId
        guard let token = idToken.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed),
              let url = URL(string: "https://kiloworld-aa8d6-default-rtdb.firebaseio.com/users/\(safeUserId)/items.json?auth=\(token)") else { return }
        isLoading = true

        URLSession.shared.dataTask(with: url) { [weak self] data, _, _ in
            guard let data else {
                DispatchQueue.main.async {
                    self?.isLoading = false
                    self?.subtitle = "inventory unavailable"
                }
                return
            }

            let decoded = try? JSONDecoder().decode([String: RareEarthInventoryItem].self, from: data)
            let totals = Self.aggregate(decoded ?? [:])

            DispatchQueue.main.async {
                self?.elements = totals
                self?.isLoading = false
                self?.subtitle = totals.isEmpty ? "none collected" : "\(totals.count) element types"
            }
        }.resume()
    }

    private static func aggregate(_ items: [String: RareEarthInventoryItem]) -> [RareEarthElementTotal] {
        var totals: [String: Int] = [:]
        for item in items.values {
            let material = (item.material ?? item.artifact ?? item.artifactMaterial ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
            guard !material.isEmpty else { continue }
            let normalized = material.lowercased()
            totals[normalized, default: 0] += max(0, item.grams ?? 0)
        }

        return totals
            .map { RareEarthElementTotal(name: $0.key, grams: $0.value, symbol: NearbyPlace.symbol(for: $0.key)) }
            .sorted { $0.name < $1.name }
    }
}

private struct RareEarthInventoryItem: Decodable {
    let material: String?
    let artifact: String?
    let artifactMaterial: String?
    let grams: Int?
}

struct RareEarthElementTotal: Identifiable {
    let name: String
    let grams: Int
    let symbol: String

    var id: String { name }
}
