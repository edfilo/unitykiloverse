import AVKit
import CoreLocation
import Foundation
import SwiftUI
#if canImport(UIKit)
import UIKit
#elseif canImport(AppKit)
import AppKit
#endif
#if os(iOS)
import CoreMotion
#endif

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
        K1L0WeatherOverlayInstaller.install()
    }
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

struct K1L0TransmissionResult: Identifiable {
    let id = UUID()
    let status: String
    let imageURL: URL?
    let videoURL: URL?
    let audioURL: URL?
    let lyrics: String
    let responsePlot: String
    let responseOptions: [String]

    var hasMedia: Bool {
        imageURL != nil || videoURL != nil || audioURL != nil
    }
}

private final class K1L0UserMetadataSaveStore: ObservableObject {
    static let shared = K1L0UserMetadataSaveStore()

    @Published var status = "profile metadata is saved to your user record."
    @Published var savedSelfieURL = ""
    @Published var savedHelmetURL = ""
    @Published var savedCloakURL = ""
    @Published var savedAvatarURL = ""
    @Published var loadedName = ""
    @Published var loadedCallsign = ""
    @Published var loadedCloakDesign = ""
    @Published var loadedHelmetDesign = ""

    func beginSaving() {
        status = "saving user metadata..."
    }

    func handle(_ json: String) {
        guard let data = json.data(using: .utf8),
              let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            status = "metadata save response failed."
            return
        }
        let ok = (root["ok"] as? Bool) ?? false
        let error = (root["error"] as? String) ?? ""
        let statusText = (root["status"] as? String) ?? ""
        let selfieURL = (root["selfieUrl"] as? String) ?? ""
        let helmetURL = (root["helmetUrl"] as? String) ?? ""
        let cloakURL = (root["cloakUrl"] as? String) ?? ""
        let avatarURL = (root["avatarUrl"] as? String) ?? ""
        let name = (root["name"] as? String) ?? ""
        let callsign = (root["callsign"] as? String) ?? ""
        let cloakDesign = (root["cloakDesign"] as? String) ?? ""
        let helmetDesign = (root["helmetDesign"] as? String) ?? ""
        if !selfieURL.isEmpty {
            savedSelfieURL = selfieURL
        }
        if !helmetURL.isEmpty { savedHelmetURL = helmetURL }
        if !cloakURL.isEmpty { savedCloakURL = cloakURL }
        if !avatarURL.isEmpty { savedAvatarURL = avatarURL }
        if !name.isEmpty { loadedName = name }
        if !callsign.isEmpty { loadedCallsign = callsign }
        if !cloakDesign.isEmpty { loadedCloakDesign = cloakDesign }
        if !helmetDesign.isEmpty { loadedHelmetDesign = helmetDesign }
        status = ok ? (statusText.isEmpty ? "user metadata saved." : statusText) : "metadata save failed\(error.isEmpty ? "." : ": \(error)")"
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

        // Drop pure-progress pings with no media at all so we only present once anything is ready to show.
        guard !status.isEmpty, imageURL != nil || videoURL != nil || audioURL != nil || !lyrics.isEmpty else { return }
        let result = K1L0TransmissionResult(status: status, imageURL: imageURL, videoURL: videoURL, audioURL: audioURL, lyrics: lyrics, responsePlot: responsePlot, responseOptions: responseOptions)
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
}

private final class K1L0ActiveTransmissionStore: ObservableObject {
    static let shared = K1L0ActiveTransmissionStore()
    private let key = "k1lo_active_transmission_v1"

    @Published private(set) var snapshot: K1L0ActiveTransmissionSnapshot

    private init() {
        if let data = UserDefaults.standard.data(forKey: key),
           let saved = try? JSONDecoder().decode(K1L0ActiveTransmissionSnapshot.self, from: data) {
            snapshot = saved
        } else {
            snapshot = K1L0ActiveTransmissionSnapshot()
        }
    }

    func start(photoPath: String, message: String, mood: String) {
        snapshot = K1L0ActiveTransmissionSnapshot(
            active: true,
            startedAt: Date().timeIntervalSince1970,
            photoPath: photoPath,
            message: message,
            mood: mood,
            responsePlot: "",
            imageUrl: "",
            videoUrl: ""
        )
        persist()
    }

    func apply(_ result: K1L0TransmissionResult) {
        guard snapshot.active else { return }
        snapshot.responsePlot = result.responsePlot
        snapshot.imageUrl = result.imageURL?.absoluteString ?? snapshot.imageUrl
        snapshot.videoUrl = result.videoURL?.absoluteString ?? snapshot.videoUrl
        persist()
    }

    func stop() {
        snapshot = K1L0ActiveTransmissionSnapshot()
        persist()
    }

    private func persist() {
        if let data = try? JSONEncoder().encode(snapshot) {
            UserDefaults.standard.set(data, forKey: key)
        }
    }
}

private final class K1L0WeatherOverlayInstaller {
#if canImport(UIKit)
    private static weak var hostController: UIViewController?
    private static weak var hostView: UIView?

    static func install() {
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
#elseif canImport(AppKit)
    // Host the SwiftUI overlay in a transparent child NSWindow ordered ABOVE the Unity
    // game window. A plain NSHostingView added as a sibling of Unity's CAMetalLayer-backed
    // content view gets composited UNDER the game render, so a child window is the reliable
    // way to guarantee the HUD draws on top (and it tracks the parent's frame).
    private static var overlayWindow: NSWindow?
    private static weak var parentWindow: NSWindow?
    private static var installed = false
    private static var frameObservers: [NSObjectProtocol] = []

    private static func gameWindow() -> NSWindow? {
        // The Unity player window: visible, has a content view, and isn't our overlay.
        if let main = NSApp.mainWindow, main !== overlayWindow, main.contentView != nil { return main }
        return NSApp.windows.first {
            $0 !== overlayWindow && $0.isVisible && $0.contentView != nil && $0.frame.width > 200
        }
    }

    static func install() {
        guard !installed else { keepOverlayInFront(); return }
        guard let parent = gameWindow() else {
            NSLog("[K1L0Overlay] install: no Unity window yet, retrying")
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.4) { install() }
            return
        }

        let panel = NSWindow(contentRect: parent.frame, styleMask: [.borderless], backing: .buffered, defer: false)
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.isReleasedWhenClosed = false
        panel.collectionBehavior = [.fullScreenAuxiliary, .transient]
        panel.contentView = NSHostingView(rootView: K1L0WeatherOverlayRoot())
        panel.setFrame(parent.frame, display: true)

        parent.addChildWindow(panel, ordered: .above)
        panel.orderFront(nil)

        overlayWindow = panel
        parentWindow = parent
        installed = true
        NSLog("[K1L0Overlay] install: overlay child window attached (parent frame \(NSStringFromRect(parent.frame)))")

        let sync: (Notification) -> Void = { _ in syncFrame() }
        for name in [NSWindow.didResizeNotification, NSWindow.didMoveNotification] {
            frameObservers.append(NotificationCenter.default.addObserver(forName: name, object: parent, queue: .main, using: sync))
        }

        keepOverlayInFront()
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
        overlayWindow.setFrame(parentWindow.frame, display: true)
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
        panel.orderFront(nil)
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
        "K1L0HUD".withCString { objectName in
            "SaveNativeUserMetadata".withCString { methodName in
                payload.withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }

    static func loadNativeUserMetadata() {
        "K1L0HUD".withCString { objectName in
            "LoadNativeUserMetadata".withCString { methodName in
                "".withCString { message in
                    UnitySendMessage(objectName, methodName, message)
                }
            }
        }
    }
}

private struct K1L0WeatherOverlayRoot: View {
    @StateObject private var data = K1L0OverlayDataModel()
    @ObservedObject private var transmissionResults = K1L0TransmissionResultStore.shared
    @State private var hudVisible = true
    @State private var showingSettings = false
    @State private var showingTransmission = false
    @State private var showingUserEditor = false
    @State private var selectedDropFilter = "all"
    @State private var liveDropLimit = 5

    var body: some View {
        ZStack {
            VStack {
                FixedTopStatusHUD(data: data)
                    .padding(.horizontal, 18)
                    .padding(.top, -2)
                    .allowsHitTesting(false)
                Spacer()
            }
            .zIndex(4)

            if hudVisible && !showingSettings {
                ScrollView(.vertical, showsIndicators: false) {
                    VStack(spacing: 8) {
                        WeatherGlassCard {
                            VStack(alignment: .leading, spacing: 12) {
                                Text("Live Drops")
                                    .font(.system(size: 25, weight: .bold))
                                Text("walk to collect")
                                    .font(.system(size: 12, weight: .semibold))
                                    .foregroundStyle(.white.opacity(0.72))
                                DropFilterBar(selected: $selectedDropFilter)

                                let visiblePlaces = data.filteredPlaces(for: selectedDropFilter)
                                ForEach(visiblePlaces.prefix(liveDropLimit)) { place in
                                    HStack(spacing: 10) {
                                        DirectionCell(
                                            distance: data.distanceText(to: place),
                                            relativeBearing: data.relativeBearingDegrees(to: place)
                                        )
                                        Text("\(data.emoji(for: place)) \(place.name)")
                                                .font(.system(size: 16, weight: .semibold))
                                                .lineLimit(1)
                                        Spacer()
                                        if let symbol = place.artifactSymbol {
                                            Text(symbol)
                                                .font(.system(size: 15, weight: .bold, design: .rounded))
                                                .foregroundStyle(.white.opacity(0.92))
                                                .frame(minWidth: 32, alignment: .trailing)
                                        }
                                    }
                                    .padding(.top, 2)
                                }
                                if visiblePlaces.count > liveDropLimit {
                                    Button {
                                        liveDropLimit = min(visiblePlaces.count, liveDropLimit + 5)
                                    } label: {
                                        Text("more")
                                            .font(.system(size: 13, weight: .bold))
                                            .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                                            .padding(.top, 2)
                                    }
                                    .buttonStyle(.plain)
                                }
                            }
                        }

                        WeatherGlassCard {
                            VStack(alignment: .leading, spacing: 10) {
                                Text("Wallet")
                                    .font(.system(size: 25, weight: .bold))
                                if data.elements.isEmpty {
                                    Text(data.elementsStatus)
                                        .font(.system(size: 13, weight: .medium))
                                        .foregroundStyle(.white.opacity(0.70))
                                } else {
                                    ForEach(data.elements.prefix(10)) { element in
                                        HStack(spacing: 10) {
                                            Text(element.symbol)
                                                .font(.system(size: 18, weight: .black))
                                                .foregroundStyle(.green.opacity(0.92))
                                                .frame(width: 38, alignment: .leading)
                                            Text(element.name)
                                                .font(.system(size: 15, weight: .semibold))
                                                .lineLimit(1)
                                            Spacer()
                                            Text("\(element.grams)g")
                                                .font(.system(size: 15, weight: .bold))
                                                .monospacedDigit()
                                                .foregroundStyle(.white.opacity(0.82))
                                        }
                                    }
                                }
                            }
                        }
                    }
                    .padding(.horizontal, 18)
                    .padding(.top, 92)
                    .padding(.bottom, 98)
                }
            }

            if showingSettings {
                NativeSettingsPanel {
                    showingSettings = false
                    K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
                    K1L0WeatherOverlayInstaller.suppressUnityHud()
                }
                .transition(.move(edge: .trailing).combined(with: .opacity))
            }

            if showingUserEditor {
                NativeUserEditorPanel {
                    withAnimation(.spring(response: 0.34, dampingFraction: 0.88)) {
                        showingUserEditor = false
                    }
                    K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
                    K1L0WeatherOverlayInstaller.suppressUnityHud()
                }
                .transition(.move(edge: .bottom).combined(with: .opacity))
                .zIndex(20)
            }

            if showingTransmission {
                NativeTransmissionPanel(elements: data.elements) {
                    withAnimation(.spring(response: 0.34, dampingFraction: 0.88)) {
                        showingTransmission = false
                    }
                    K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
                    K1L0WeatherOverlayInstaller.suppressUnityHud()
                }
                .transition(.move(edge: .bottom).combined(with: .opacity))
                .zIndex(20)
            }

            VStack {
                Spacer()
                HStack {
                    Button {
                        K1L0WeatherOverlayInstaller.keepOverlayInFront()
                        hudVisible.toggle()
                        showingSettings = false
                        showingTransmission = false
                        showingUserEditor = false
                        K1L0WeatherOverlayInstaller.suppressUnityHud()
                        K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
                    } label: {
                        Image(systemName: hudVisible ? "eye.fill" : "eye.slash.fill")
                            .font(.system(size: 20, weight: .bold))
                            .foregroundStyle(.white)
                            .frame(width: 58, height: 58)
                            .background(.ultraThinMaterial, in: Circle())
                            .overlay(Circle().stroke(.white.opacity(0.24), lineWidth: 1))
                            .shadow(color: .black.opacity(0.28), radius: 16, y: 8)
                    }
                    .buttonStyle(.plain)
                    Spacer()
                    Button {
                        K1L0WeatherOverlayInstaller.keepOverlayInFront()
                        withAnimation(.spring(response: 0.34, dampingFraction: 0.88)) {
                            showingTransmission.toggle()
                            showingSettings = false
                            showingUserEditor = false
                        }
                        K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
                        K1L0WeatherOverlayInstaller.suppressUnityHud()
                    } label: {
                        Image(systemName: showingTransmission ? "xmark" : "antenna.radiowaves.left.and.right")
                            .font(.system(size: 22, weight: .bold))
                            .foregroundStyle(.white)
                            .frame(width: 66, height: 66)
                            .background(Color.black.opacity(0.46), in: Circle())
                            .overlay(Circle().stroke(Color.green.opacity(0.46), lineWidth: 1.5))
                            .shadow(color: .black.opacity(0.28), radius: 16, y: 8)
                    }
                    .buttonStyle(.plain)
                    Spacer()
                    Button {
                        K1L0WeatherOverlayInstaller.keepOverlayInFront()
                        withAnimation(.spring(response: 0.34, dampingFraction: 0.88)) {
                            showingUserEditor.toggle()
                            showingSettings = false
                            showingTransmission = false
                        }
                        K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
                        K1L0WeatherOverlayInstaller.suppressUnityHud()
                    } label: {
                        Image(systemName: showingUserEditor ? "xmark" : "person.crop.circle.fill")
                            .font(.system(size: 21, weight: .bold))
                            .foregroundStyle(.white)
                            .frame(width: 58, height: 58)
                            .background(.ultraThinMaterial, in: Circle())
                            .overlay(Circle().stroke(.white.opacity(0.24), lineWidth: 1))
                            .shadow(color: .black.opacity(0.28), radius: 16, y: 8)
                    }
                    .buttonStyle(.plain)
                    Spacer()
                    Button {
                        K1L0WeatherOverlayInstaller.keepOverlayInFront()
                        withAnimation(.spring(response: 0.34, dampingFraction: 0.88)) {
                            showingSettings.toggle()
                            showingTransmission = false
                            showingUserEditor = false
                        }
                        K1L0WeatherOverlayInstaller.setNativeMapVisible(true)
                        K1L0WeatherOverlayInstaller.suppressUnityHud()
                    } label: {
                        Image(systemName: showingSettings ? "xmark" : "gearshape.fill")
                            .font(.system(size: 20, weight: .bold))
                            .foregroundStyle(.white)
                            .frame(width: 58, height: 58)
                            .background(.ultraThinMaterial, in: Circle())
                            .overlay(Circle().stroke(.white.opacity(0.24), lineWidth: 1))
                            .shadow(color: .black.opacity(0.28), radius: 16, y: 8)
                    }
                    .buttonStyle(.plain)
                }
                .padding(.horizontal, 18)
                .padding(.bottom, 16)
            }
        }
        .onAppear {
            data.start()
        }
        .onChange(of: selectedDropFilter) { _ in
            liveDropLimit = 5
        }
        .animation(.spring(response: 0.34, dampingFraction: 0.88), value: showingTransmission)
        .animation(.spring(response: 0.34, dampingFraction: 0.88), value: showingUserEditor)
        .overlay(alignment: .bottom) {
            if let result = transmissionResults.current {
                TransmissionResultPanel(result: result) {
                    transmissionResults.dismiss()
                }
                .transition(.move(edge: .bottom).combined(with: .opacity))
                .zIndex(30)
            }
        }
        .animation(.spring(response: 0.34, dampingFraction: 0.88), value: transmissionResults.current?.id)
    }
}

private struct TransmissionResultPanel: View {
    let result: K1L0TransmissionResult
    let onClose: () -> Void

    var body: some View {
        GeometryReader { geometry in
            ZStack(alignment: .bottom) {
                Color.black.opacity(0.62).ignoresSafeArea()
                    .onTapGesture { onClose() }

                ZStack(alignment: .topTrailing) {
                    ScrollView(.vertical, showsIndicators: true) {
                        VStack(alignment: .leading, spacing: 16) {
                            Text("Incoming transmission")
                                .font(.system(size: 26, weight: .bold))
                                .foregroundStyle(.white)
                            Text("status: \(result.status)")
                                .font(.system(size: 12, weight: .semibold, design: .monospaced))
                                .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.88))

                            if let url = result.videoURL {
                                VideoPlayer(player: AVPlayer(url: url))
                                    .aspectRatio(9.0/16.0, contentMode: .fit)
                                    .frame(maxWidth: .infinity)
                                    .overlay(Rectangle().stroke(Color.green.opacity(0.6), lineWidth: 1))
                            } else if let url = result.imageURL {
                                AsyncImage(url: url) { phase in
                                    switch phase {
                                    case .success(let image):
                                        image
                                            .resizable()
                                            .scaledToFit()
                                    case .failure:
                                        Text("image unavailable")
                                            .foregroundStyle(.white.opacity(0.55))
                                            .frame(maxWidth: .infinity, minHeight: 220)
                                    default:
                                        ProgressView()
                                            .progressViewStyle(.circular)
                                            .frame(maxWidth: .infinity, minHeight: 220)
                                    }
                                }
                                .overlay(Rectangle().stroke(Color.green.opacity(0.6), lineWidth: 1))
                            }

                            if result.imageURL != nil, result.videoURL != nil {
                                AsyncImage(url: result.imageURL) { phase in
                                    if case .success(let image) = phase {
                                        image
                                            .resizable()
                                            .scaledToFit()
                                            .frame(maxWidth: .infinity)
                                            .opacity(0.85)
                                    }
                                }
                            }

                            if let url = result.audioURL, result.videoURL == nil {
                                VideoPlayer(player: AVPlayer(url: url))
                                    .frame(height: 80)
                                    .overlay(Rectangle().stroke(Color.green.opacity(0.6), lineWidth: 1))
                            }

                            VStack(alignment: .leading, spacing: 8) {
                                Text("Lyrics")
                                    .font(.system(size: 18, weight: .bold))
                                    .foregroundStyle(.white)
                                if result.lyrics.isEmpty {
                                    Text("(awaiting transcription from the signal…)")
                                        .font(.system(size: 13, weight: .medium))
                                        .foregroundStyle(.white.opacity(0.52))
                                } else {
                                    Text(result.lyrics)
                                        .font(.system(size: 14, weight: .medium))
                                        .foregroundStyle(.white.opacity(0.92))
                                        .fixedSize(horizontal: false, vertical: true)
                                }
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(14)
                            .background(Color.black.opacity(0.4), in: RoundedRectangle(cornerRadius: 18, style: .continuous))
                            .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).stroke(.white.opacity(0.12), lineWidth: 1))

                            if !result.responseOptions.isEmpty {
                                VStack(alignment: .leading, spacing: 10) {
                                    Text("What should he do?")
                                        .font(.system(size: 18, weight: .bold))
                                        .foregroundStyle(.white)
                                    if !result.responsePlot.isEmpty {
                                        Text(result.responsePlot)
                                            .font(.system(size: 14, weight: .semibold))
                                            .foregroundStyle(.white.opacity(0.86))
                                            .fixedSize(horizontal: false, vertical: true)
                                    }
                                    LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 8) {
                                        ForEach(result.responseOptions + ["OTHER"], id: \.self) { option in
                                            Button {
                                            } label: {
                                                Text("[ \(option.uppercased()) ]")
                                                    .font(.system(size: 13, weight: .black, design: .monospaced))
                                                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                                                    .frame(maxWidth: .infinity, minHeight: 38)
                                                    .overlay(Rectangle().stroke(Color.green.opacity(0.58), lineWidth: 1))
                                            }
                                            .buttonStyle(.plain)
                                        }
                                    }
                                }
                                .frame(maxWidth: .infinity, alignment: .leading)
                                .padding(14)
                                .background(Color.black.opacity(0.4), in: RoundedRectangle(cornerRadius: 18, style: .continuous))
                                .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).stroke(.white.opacity(0.12), lineWidth: 1))
                            }

                            Button(action: onClose) {
                                Text("[ CLOSE ]")
                                    .font(.system(size: 15, weight: .black))
                                    .foregroundStyle(.white)
                                    .frame(maxWidth: .infinity, minHeight: 44)
                                    .overlay(Rectangle().stroke(Color.white.opacity(0.42), lineWidth: 1))
                            }
                            .buttonStyle(.plain)
                        }
                        .padding(.horizontal, 20)
                        .padding(.top, 24)
                        .padding(.bottom, 38)
                    }

                    Button(action: onClose) {
                        Image(systemName: "xmark")
                            .font(.system(size: 20, weight: .black))
                            .foregroundStyle(.white)
                            .frame(width: 46, height: 46)
                            .background(Color.black, in: Circle())
                    }
                    .buttonStyle(.plain)
                    .padding(.top, 8)
                    .padding(.trailing, 14)
                }
                .frame(maxWidth: .infinity)
                .frame(maxHeight: max(420, geometry.size.height * 0.7))
                .background(Color.black)
                .clipShape(RoundedRectangle(cornerRadius: 26, style: .continuous))
                .shadow(color: .black.opacity(0.55), radius: 24, x: 0, y: -10)
            }
            .ignoresSafeArea(edges: .bottom)
        }
    }
}

private struct NativeSettingsPanel: View {
    let onClose: () -> Void

    @AppStorage("k1lo_native_saturation") private var saturation = 35.0
    @AppStorage("k1lo_native_contrast") private var contrast = 18.0
    @AppStorage("k1lo_native_mapBrightness") private var mapBrightness = 0.35
    @AppStorage("k1lo_native_hueShift") private var hueShift = 0.0
    @AppStorage("k1lo_native_temperature") private var temperature = 0.0
    @AppStorage("k1lo_native_tint") private var tint = 0.0
    @AppStorage("k1lo_native_bloomEnabled") private var bloomEnabled = true
    @AppStorage("k1lo_native_bloomIntensity") private var bloomIntensity = 2.5
    @AppStorage("k1lo_native_bloomThreshold") private var bloomThreshold = 0.5
    @AppStorage("k1lo_native_bloomScatter") private var bloomScatter = 0.95
    @AppStorage("k1lo_native_vignetteEnabled") private var vignetteEnabled = true
    @AppStorage("k1lo_native_vignetteIntensity") private var vignetteIntensity = 0.3
    @AppStorage("k1lo_native_vignetteSmoothness") private var vignetteSmoothness = 0.2
    @AppStorage("k1lo_native_chromaticEnabled") private var chromaticEnabled = true
    @AppStorage("k1lo_native_chromaticIntensity") private var chromaticIntensity = 0.1
    @AppStorage("k1lo_native_lensDistEnabled") private var lensDistEnabled = true
    @AppStorage("k1lo_native_lensDistIntensity") private var lensDistIntensity = -0.15
    @AppStorage("k1lo_native_dofEnabled") private var dofEnabled = false
    @AppStorage("k1lo_native_focusDistance") private var focusDistance = 10.0
    @AppStorage("k1lo_native_aperture") private var aperture = 5.6
    @AppStorage("k1lo_native_focalLength") private var focalLength = 50.0
    @AppStorage("k1lo_native_motionBlurEnabled") private var motionBlurEnabled = false
    @AppStorage("k1lo_native_motionBlurIntensity") private var motionBlurIntensity = 0.5
    @AppStorage("k1lo_native_filmGrainEnabled") private var filmGrainEnabled = false
    @AppStorage("k1lo_native_filmGrainIntensity") private var filmGrainIntensity = 0.2
    @AppStorage("k1lo_native_godPositionY") private var godPositionY = 100.0
    @AppStorage("k1lo_native_godPositionZ") private var godPositionZ = 100.0
    @AppStorage("k1lo_native_godRotationX") private var godRotationX = 55.0
    @AppStorage("k1lo_native_farClipPlane") private var farClipPlane = 250.0
    @AppStorage("k1lo_native_auroraEnabled") private var auroraEnabled = true
    @AppStorage("k1lo_native_auroraIntensity") private var auroraIntensity = 0.75
    @AppStorage("k1lo_native_auroraHeight") private var auroraHeight = 115.0
    @AppStorage("k1lo_native_auroraDistance") private var auroraDistance = 420.0
    @AppStorage("k1lo_native_auroraWidth") private var auroraWidth = 520.0
    @AppStorage("k1lo_native_auroraVerticalSize") private var auroraVerticalSize = 140.0
    @AppStorage("k1lo_native_auroraDriftSpeed") private var auroraDriftSpeed = 0.28
    @AppStorage("k1lo_native_beamDistanceLabels") private var beamDistanceLabels = true
    @AppStorage("k1lo_native_beamDebug") private var beamDebug = false
    @AppStorage("k1lo_native_perfOverlay") private var perfOverlay = false
    @AppStorage("k1lo_native_showStoryStrip") private var showStoryStrip = false
    @AppStorage("k1lo_native_panelMapBrightness") private var panelMapBrightness = 0.01
    @AppStorage("k1lo_native_weatherOpenMeteo") private var weatherOpenMeteo = true
    @AppStorage("k1lo_native_manualHour") private var manualHour = 13.0
    @AppStorage("k1lo_native_manualWeather") private var manualWeather = 0
    @AppStorage("k1lo_native_ambientMinStepsToSpawn") private var ambientMinStepsToSpawn = 50.0
    @AppStorage("k1lo_native_momentumSessionGraceMinutes") private var momentumSessionGraceMinutes = 1.5
    @AppStorage("k1lo_native_ambientBeamTtlMinutes") private var ambientBeamTtlMinutes = 20.0
    @AppStorage("k1lo_native_ambientCollectRadiusMeters") private var ambientCollectRadiusMeters = 10.0

    var body: some View {
        ZStack {
            ScrollView(.vertical, showsIndicators: true) {
                VStack(alignment: .leading, spacing: 14) {
                    HStack {
                        VStack(alignment: .leading, spacing: 2) {
                            Text("Settings")
                                .font(.system(size: 34, weight: .bold))
                            Text("Unity scene controls")
                                .font(.system(size: 13, weight: .medium))
                                .foregroundStyle(.white.opacity(0.58))
                        }
                        Spacer()
                        Button(action: onClose) {
                            Image(systemName: "xmark")
                                .font(.system(size: 22, weight: .bold))
                                .foregroundStyle(.white)
                                .frame(width: 48, height: 48)
                                .background(.black.opacity(0.45), in: Circle())
                        }
                        .buttonStyle(.plain)
                    }
                    .padding(.top, 18)

                    SettingsSection(title: "Map Color") {
                        SettingSliderRow(title: "Saturation", value: $saturation, range: -100...100, step: 1, key: "saturation")
                        SettingSliderRow(title: "Contrast", value: $contrast, range: -100...100, step: 1, key: "contrast")
                        SettingSliderRow(title: "Map Bright", value: $mapBrightness, range: -2...2, step: 0.05, key: "mapBrightness")
                        SettingSliderRow(title: "Hue Shift", value: $hueShift, range: -100...100, step: 1, key: "hueShift")
                        SettingSliderRow(title: "Temperature", value: $temperature, range: -100...100, step: 1, key: "temperature")
                        SettingSliderRow(title: "Tint", value: $tint, range: -100...100, step: 1, key: "tint")
                    }

                    SettingsSection(title: "Bloom") {
                        SettingToggleRow(title: "Bloom", value: $bloomEnabled, key: "bloomEnabled")
                        SettingSliderRow(title: "Intensity", value: $bloomIntensity, range: 0...8, step: 0.1, key: "bloomIntensity")
                        SettingSliderRow(title: "Threshold", value: $bloomThreshold, range: 0...2, step: 0.05, key: "bloomThreshold")
                        SettingSliderRow(title: "Scatter", value: $bloomScatter, range: 0...1, step: 0.01, key: "bloomScatter")
                    }

                    SettingsSection(title: "Post FX") {
                        SettingToggleRow(title: "Vignette", value: $vignetteEnabled, key: "vignetteEnabled")
                        SettingSliderRow(title: "Vignette Intensity", value: $vignetteIntensity, range: 0...1, step: 0.01, key: "vignetteIntensity")
                        SettingSliderRow(title: "Vignette Smoothness", value: $vignetteSmoothness, range: 0.01...1, step: 0.01, key: "vignetteSmoothness")
                        SettingToggleRow(title: "Chromatic", value: $chromaticEnabled, key: "chromaticEnabled")
                        SettingSliderRow(title: "Chromatic Intensity", value: $chromaticIntensity, range: 0...1, step: 0.01, key: "chromaticIntensity")
                        SettingToggleRow(title: "Lens Distortion", value: $lensDistEnabled, key: "lensDistEnabled")
                        SettingSliderRow(title: "Lens Distortion", value: $lensDistIntensity, range: -1...1, step: 0.01, key: "lensDistIntensity")
                    }

                    SettingsSection(title: "Focus + Motion") {
                        SettingToggleRow(title: "Depth of Field", value: $dofEnabled, key: "dofEnabled")
                        SettingSliderRow(title: "Focus Distance", value: $focusDistance, range: 0.1...300, step: 0.1, key: "focusDistance")
                        SettingSliderRow(title: "Aperture", value: $aperture, range: 0.05...32, step: 0.05, key: "aperture")
                        SettingSliderRow(title: "Focal Length", value: $focalLength, range: 1...300, step: 1, key: "focalLength")
                        SettingToggleRow(title: "Motion Blur", value: $motionBlurEnabled, key: "motionBlurEnabled")
                        SettingSliderRow(title: "Motion Blur Intensity", value: $motionBlurIntensity, range: 0...1, step: 0.01, key: "motionBlurIntensity")
                        SettingToggleRow(title: "Film Grain", value: $filmGrainEnabled, key: "filmGrainEnabled")
                        SettingSliderRow(title: "Film Grain Intensity", value: $filmGrainIntensity, range: 0...1, step: 0.01, key: "filmGrainIntensity")
                    }

                    SettingsSection(title: "God Camera") {
                        SettingSliderRow(title: "Height", value: $godPositionY, range: 10...500, step: 1, key: "godPositionY")
                        SettingSliderRow(title: "Distance", value: $godPositionZ, range: 10...500, step: 1, key: "godPositionZ")
                        SettingSliderRow(title: "Pitch", value: $godRotationX, range: -90...90, step: 1, key: "godRotationX")
                        SettingSliderRow(title: "Far Clip", value: $farClipPlane, range: 100...5000, step: 10, key: "farClipPlane")
                    }

                    SettingsSection(title: "Aurora") {
                        SettingToggleRow(title: "Aurora", value: $auroraEnabled, key: "auroraEnabled")
                        SettingSliderRow(title: "Intensity", value: $auroraIntensity, range: 0...2, step: 0.05, key: "auroraIntensity")
                        SettingSliderRow(title: "Height", value: $auroraHeight, range: 20...300, step: 1, key: "auroraHeight")
                        SettingSliderRow(title: "Distance", value: $auroraDistance, range: 80...900, step: 5, key: "auroraDistance")
                        SettingSliderRow(title: "Width", value: $auroraWidth, range: 80...900, step: 5, key: "auroraWidth")
                        SettingSliderRow(title: "Vertical Size", value: $auroraVerticalSize, range: 20...320, step: 1, key: "auroraVerticalSize")
                        SettingSliderRow(title: "Drift Speed", value: $auroraDriftSpeed, range: 0...2, step: 0.02, key: "auroraDriftSpeed")
                    }

                    SettingsSection(title: "Signals") {
                        SettingSliderRow(title: "Min Steps Gate", value: $ambientMinStepsToSpawn, range: 0...2000, step: 10, key: "ambientMinStepsToSpawn")
                        SettingSliderRow(title: "Reset Grace Min", value: $momentumSessionGraceMinutes, range: 1...30, step: 0.5, key: "momentumSessionGraceMinutes")
                        SettingSliderRow(title: "Portal Expire Min", value: $ambientBeamTtlMinutes, range: 1...240, step: 1, key: "ambientBeamTtlMinutes")
                        SettingSliderRow(title: "Collect Radius", value: $ambientCollectRadiusMeters, range: 1...100, step: 1, key: "ambientCollectRadiusMeters")
                        SettingToggleRow(title: "Distance Labels", value: $beamDistanceLabels, key: "beamDistanceLabels")
                        SettingToggleRow(title: "Beam Debug", value: $beamDebug, key: "beamDebug")
                        SettingToggleRow(title: "Perf Overlay", value: $perfOverlay, key: "perfOverlay")
                        SettingToggleRow(title: "Story Strip", value: $showStoryStrip, key: "showStoryStrip")
                        SettingSliderRow(title: "Panel Map Bright", value: $panelMapBrightness, range: 0...1, step: 0.01, key: "panelMapBrightness")
                    }

                    SettingsSection(title: "Weather") {
                        SettingToggleRow(title: "Open-Meteo Source", value: $weatherOpenMeteo, key: "weatherOpenMeteo")
                        SettingSliderRow(title: "Manual Sky Hour", value: $manualHour, range: 0...24, step: 0.25, key: "manualHour")
                        SettingWeatherSegmentRow(selection: $manualWeather)
                    }
                }
                .foregroundStyle(.white)
                .padding(.horizontal, 18)
                .padding(.bottom, 110)
            }
        }
    }
}

private struct SettingsSection<Content: View>: View {
    let title: String
    @ViewBuilder let content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(title)
                .font(.system(size: 18, weight: .bold))
                .foregroundStyle(.white.opacity(0.92))
            VStack(spacing: 9) {
                content
            }
        }
        .padding(16)
        .background(Color.black.opacity(0.16), in: RoundedRectangle(cornerRadius: 24, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 24, style: .continuous)
                .stroke(.white.opacity(0.10), lineWidth: 1)
        )
    }
}

private struct SettingSliderRow: View {
    let title: String
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
            Slider(value: $value, in: range, step: step)
                .tint(Color(red: 0.66, green: 1.0, blue: 0.76))
                .onChange(of: value) { newValue in
                    K1L0WeatherOverlayInstaller.setUnitySetting(key, String(format: "%.3f", newValue))
                }
        }
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
            }
        }
    }
}

private struct NativeUserEditorDraft: Codable, Equatable {
    var name: String = ""
    var callsign: String = ""
    var cloakDesign: String = ""
    var helmetDesign: String = ""
    var selfiePath: String = ""
    var selfieUrl: String = ""
    var helmetUrl: String = ""
    var cloakUrl: String = ""
    var avatarUrl: String = ""
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

private struct NativeUserEditorPanel: View {
    let onClose: () -> Void

    @ObservedObject private var saveStore = K1L0UserMetadataSaveStore.shared
    @State private var draft = NativeUserEditorStore.load()
#if canImport(UIKit)
    @State private var selfie: UIImage?
    @State private var showingSelfiePicker = false
    @State private var selfieSource: UIImagePickerController.SourceType = .photoLibrary
#elseif canImport(AppKit)
    @State private var selfie: NSImage?
#endif

    var body: some View {
        GeometryReader { geometry in
            ZStack(alignment: .bottom) {
                Color.black.opacity(0.34).ignoresSafeArea()

                ZStack(alignment: .topTrailing) {
                    ScrollView(.vertical, showsIndicators: true) {
                        VStack(alignment: .leading, spacing: 14) {
                            Text("User")
                                .font(.system(size: 30, weight: .bold))
                                .foregroundStyle(.white)
                            Text("design your signal identity.")
                                .font(.system(size: 14, weight: .medium))
                                .foregroundStyle(.white.opacity(0.68))

                            // Hero header: rendered cloak/helmet avatar on the
                            // left (this is the user's K1L0 identity, not the
                            // raw selfie), name + callsign on the right. The
                            // raw selfie has its own card further down.
                            WeatherGlassCard {
                                HStack(alignment: .top, spacing: 14) {
                                    renderedHero
                                    VStack(alignment: .leading, spacing: 10) {
                                        profileTextField("Name", text: $draft.name)
                                        profileTextField("Callsign", text: $draft.callsign)
                                    }
                                    .frame(maxWidth: .infinity, alignment: .leading)
                                }
                            }

                            WeatherGlassCard {
                                VStack(alignment: .leading, spacing: 10) {
                                    Text("Rendered Identity")
                                        .font(.system(size: 19, weight: .bold))
                                    HStack(spacing: 12) {
                                        identityPreview(title: "HELMET", urlString: draft.helmetUrl)
                                        identityPreview(title: "CLOAK", urlString: draft.cloakUrl.isEmpty ? draft.avatarUrl : draft.cloakUrl)
                                    }
                                    Text(draft.helmetUrl.isEmpty || (draft.cloakUrl.isEmpty && draft.avatarUrl.isEmpty) ? "helmet and cloak render after saving." : "helmet and cloak ready.")
                                        .font(.system(size: 13, weight: .semibold))
                                        .foregroundStyle(draft.helmetUrl.isEmpty || (draft.cloakUrl.isEmpty && draft.avatarUrl.isEmpty) ? .white.opacity(0.54) : Color(red: 0.66, green: 1.0, blue: 0.76))
                                }
                            }

                            // Design comes BEFORE Selfie — the user types their
                            // prompts first, then attaches the selfie that gets
                            // rendered against them on save.
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

                            Text(saveStore.status)
                                .font(.system(size: 13, weight: .semibold))
                                .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.88))

                            Button {
                                save()
                            } label: {
                                Text("[ SAVE USER ]")
                                    .font(.system(size: 17, weight: .black))
                                    .foregroundStyle(.green)
                                    .frame(maxWidth: .infinity, minHeight: 54)
                                    .overlay(Rectangle().stroke(Color.green.opacity(0.82), lineWidth: 1.5))
                            }
                            .buttonStyle(.plain)
                        }
                        .padding(.horizontal, 20)
                        .padding(.top, 24)
                        .padding(.bottom, 38)
                    }

                    Button(action: onClose) {
                        Image(systemName: "xmark")
                            .font(.system(size: 22, weight: .black))
                            .foregroundStyle(.white)
                            .frame(width: 52, height: 52)
                            .background(Color.black, in: Circle())
                    }
                    .buttonStyle(.plain)
                    .padding(.top, 8)
                    .padding(.trailing, 14)
                }
                .frame(maxWidth: .infinity)
                .frame(height: min(geometry.size.height - 18, max(560, geometry.size.height * 0.82)))
                .background(Color.black)
                .clipShape(RoundedRectangle(cornerRadius: 24, style: .continuous))
                .shadow(color: .black.opacity(0.55), radius: 24, x: 0, y: -10)
            }
            .ignoresSafeArea(edges: .bottom)
        }
#if canImport(UIKit)
        .sheet(isPresented: $showingSelfiePicker) {
            NativePhotoPicker(sourceType: selfieSource) { image, path in
                if let image, let path {
                    selfie = image
                    draft.selfiePath = path
                    NativeUserEditorStore.save(draft)
                    saveStore.status = "selfie attached."
                }
                showingSelfiePicker = false
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
                .overlay(Circle().stroke(Color.green.opacity(0.85), lineWidth: 1.5))
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
            .overlay(Circle().stroke(Color.green.opacity(0.85), lineWidth: 1.5))
        }
#elseif canImport(AppKit)
        if let selfie {
            Image(nsImage: selfie)
                .resizable()
                .scaledToFill()
                .frame(width: 96, height: 96)
                .clipShape(Circle())
                .overlay(Circle().stroke(Color.green.opacity(0.85), lineWidth: 1.5))
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
            .overlay(Circle().stroke(Color.green.opacity(0.85), lineWidth: 1.5))
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
        .overlay(Rectangle().stroke(Color.green.opacity(0.85), lineWidth: 1.5))
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
        }
        .frame(width: heroWidth)
        .overlay(Rectangle().stroke(Color.green.opacity(renderedUrl.isEmpty ? 0.24 : 0.85), lineWidth: 1.5))
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
            .overlay(Rectangle().stroke(Color.green.opacity(urlString.isEmpty ? 0.24 : 0.85), lineWidth: 1.5))

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
            selfieSource = source
            showingSelfiePicker = true
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

private struct NativeTransmissionPanel: View {
    let elements: [OverlayElement]
    let onClose: () -> Void
    @ObservedObject private var activeTransmission = K1L0ActiveTransmissionStore.shared

#if canImport(UIKit)
    @State private var selectedPhoto: UIImage?
    @State private var showingPhotoPicker = false
    @State private var pickerSource: UIImagePickerController.SourceType = .photoLibrary
#elseif canImport(AppKit)
    @State private var selectedPhoto: NSImage?
#endif
    @State private var selectedPhotoPath = ""
    @State private var message = ""
    @State private var status = "attach a photo and describe what you are doing."
    @State private var selectedMood = "wired"
    private let moods = ["depressed", "excited", "busy", "wired", "silly"]

    var body: some View {
        GeometryReader { geometry in
            ZStack(alignment: .bottom) {
                Color.black.opacity(0.34).ignoresSafeArea()

                ZStack(alignment: .topTrailing) {
                    VStack(alignment: .leading, spacing: 10) {
                            if activeTransmission.snapshot.active {
                                ActiveTransmissionTerminal(
                                    snapshot: activeTransmission.snapshot,
                                    onStop: { activeTransmission.stop() }
                                )
                            } else {
                                Text("Transmission")
                                    .font(.system(size: 28, weight: .bold))
                                    .foregroundStyle(.white)
                                Text("What are you doing?")
                                    .font(.system(size: 14, weight: .medium))
                                    .foregroundStyle(.white.opacity(0.68))

                                WeatherGlassCard {
                                    VStack(alignment: .leading, spacing: 10) {
                                        Text("Photo Payload")
                                            .font(.system(size: 19, weight: .bold))
#if canImport(UIKit)
                                        HStack(spacing: 10) {
                                            transmitterPhotoButton("TAKE PHOTO", source: .camera)
                                            transmitterPhotoButton("SELECT PHOTO", source: .photoLibrary)
                                        }
#elseif canImport(AppKit)
                                        Button {
                                            macSelectPhoto()
                                        } label: {
                                            Text("[ SELECT PHOTO ]")
                                                .font(.system(size: 13, weight: .black))
                                                .foregroundStyle(.white)
                                                .frame(maxWidth: .infinity, minHeight: 42)
                                                .overlay(Rectangle().stroke(Color.white.opacity(0.40), lineWidth: 1))
                                        }
                                        .buttonStyle(.plain)
#endif
                                        if let selectedPhoto {
#if canImport(UIKit)
                                            Image(uiImage: selectedPhoto)
                                                .resizable()
                                                .scaledToFill()
                                                .frame(width: 84, height: 112)
                                                .clipped()
                                                .overlay(Rectangle().stroke(Color.green.opacity(0.85), lineWidth: 1.5))
#elseif canImport(AppKit)
                                            Image(nsImage: selectedPhoto)
                                                .resizable()
                                                .scaledToFill()
                                                .frame(width: 84, height: 112)
                                                .clipped()
                                                .overlay(Rectangle().stroke(Color.green.opacity(0.85), lineWidth: 1.5))
#endif
                                        }
                                        Text(selectedPhotoPath.isEmpty ? "no photo attached" : "photo attached")
                                            .font(.system(size: 13, weight: .semibold))
                                            .foregroundStyle(selectedPhotoPath.isEmpty ? .white.opacity(0.54) : Color(red: 0.66, green: 1.0, blue: 0.76))
                                    }
                                }

                                WeatherGlassCard {
                                    VStack(alignment: .leading, spacing: 10) {
                                        Text("Mood")
                                            .font(.system(size: 19, weight: .bold))
                                        LazyVGrid(columns: [GridItem(.adaptive(minimum: 96), spacing: 8)], alignment: .leading, spacing: 8) {
                                            ForEach(moods, id: \.self) { mood in
                                                Button {
                                                    selectedMood = mood
                                                    status = "mood: \(mood)."
                                                } label: {
                                                    Text(mood.uppercased())
                                                        .font(.system(size: 12, weight: .black))
                                                        .foregroundStyle(selectedMood == mood ? .black : .white)
                                                        .frame(maxWidth: .infinity, minHeight: 34)
                                                        .background(selectedMood == mood ? Color.green.opacity(0.90) : Color.white.opacity(0.06))
                                                        .clipShape(Capsule())
                                                        .overlay(Capsule().stroke(Color.green.opacity(selectedMood == mood ? 0.95 : 0.28), lineWidth: 1))
                                                }
                                                .buttonStyle(.plain)
                                            }
                                        }
                                    }
                                }

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
                                    Text("[ START TRANSMISSION ]")
                                        .font(.system(size: 17, weight: .black))
                                        .foregroundStyle(.green)
                                        .frame(maxWidth: .infinity, minHeight: 54)
                                        .overlay(Rectangle().stroke(Color.green.opacity(0.82), lineWidth: 1.5))
                                }
                                .buttonStyle(.plain)
                                .disabled(!canTransmit)
                                .opacity(canTransmit ? 1 : 0.38)
                            }
                    }
                    .padding(.horizontal, 20)
                    .padding(.top, 24)
                        .padding(.bottom, 38)

                    Button(action: onClose) {
                        Image(systemName: "xmark")
                            .font(.system(size: 22, weight: .black))
                            .foregroundStyle(.white)
                            .frame(width: 52, height: 52)
                            .background(Color.black, in: Circle())
                    }
                    .buttonStyle(.plain)
                    .padding(.top, 8)
                    .padding(.trailing, 14)
                }
                .frame(maxWidth: .infinity)
                .frame(height: min(geometry.size.height - 18, max(560, geometry.size.height * 0.82)))
                .background(Color.black)
                .clipShape(RoundedRectangle(cornerRadius: 24, style: .continuous))
                .shadow(color: .black.opacity(0.55), radius: 24, x: 0, y: -10)
                .gesture(
                    DragGesture(minimumDistance: 18)
                        .onEnded { value in
                            if value.translation.height > 80 {
                                onClose()
                            }
                        }
                )
            }
            .ignoresSafeArea(edges: .bottom)
        }
#if canImport(UIKit)
        .sheet(isPresented: $showingPhotoPicker) {
            NativePhotoPicker(sourceType: pickerSource) { image, path in
                if let image, let path {
                    selectedPhoto = image
                    selectedPhotoPath = path
                    status = "photo attached."
                }
                showingPhotoPicker = false
            }
            .ignoresSafeArea()
        }
#endif
    }

    @ViewBuilder
    private var messageField: some View {
#if canImport(UIKit)
        TextField("What are you doing?", text: $message)
            .textInputAutocapitalization(.sentences)
#else
        TextField("What are you doing?", text: $message)
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
    private func transmitterPhotoButton(_ title: String, source: UIImagePickerController.SourceType) -> some View {
        Button {
            guard UIImagePickerController.isSourceTypeAvailable(source) else {
                status = source == .camera ? "camera unavailable." : "photo library unavailable."
                return
            }
            pickerSource = source
            showingPhotoPicker = true
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
        let payload: [String: String] = [
            "element": "",
            "message": cleanMessage,
            "photoPath": selectedPhotoPath,
            "mood": selectedMood
        ]
        guard let data = try? JSONSerialization.data(withJSONObject: payload),
              let json = String(data: data, encoding: .utf8)
        else {
            status = "transmission payload failed."
            return
        }
        K1L0ActiveTransmissionStore.shared.start(photoPath: selectedPhotoPath, message: cleanMessage, mood: selectedMood)
        K1L0WeatherOverlayInstaller.beginNativeTransmission(json)
        status = "transmitting..."
    }
}

private struct ActiveTransmissionTerminal: View {
    let snapshot: K1L0ActiveTransmissionSnapshot
    let onStop: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text(snapshot.videoUrl.isEmpty ? "building transmission" : "TRANSMITTING")
                .font(.system(size: 30, weight: .black))
                .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))

            ZStack {
                if snapshot.videoUrl.isEmpty {
                    WarblyStaticView()
                } else {
                    InlineTransmissionVideoPlayer(urlString: snapshot.videoUrl)
                    WarblyStaticView()
                        .opacity(0.14)
                        .allowsHitTesting(false)
                }
            }
            .frame(maxWidth: .infinity)
            .frame(height: 330)
            .background(Color.black.opacity(0.86))
            .clipped()
            .overlay(Rectangle().stroke(Color.green.opacity(0.50), lineWidth: 1.4))

            Button(action: onStop) {
                Text(snapshot.videoUrl.isEmpty ? "Cancel" : "[ STOP TRANSMISSION ]")
                    .font(.system(size: 17, weight: .black))
                    .foregroundStyle(.red)
                    .frame(maxWidth: .infinity, minHeight: 56)
                    .overlay(Rectangle().stroke(Color.red.opacity(0.78), lineWidth: 1.5))
            }
            .buttonStyle(.plain)
        }
    }
}

private struct WarblyStaticView: View {
    var body: some View {
        TimelineView(.animation) { timeline in
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

private struct InlineTransmissionVideoPlayer: View {
    let urlString: String
    @State private var player: AVPlayer

    init(urlString: String) {
        self.urlString = urlString
        let url = URL(string: urlString) ?? URL(fileURLWithPath: "/dev/null")
        _player = State(initialValue: AVPlayer(url: url))
    }

    var body: some View {
        VideoPlayer(player: player)
            .onAppear { player.play() }
            .onDisappear { player.pause() }
            .onReceive(NotificationCenter.default.publisher(for: .AVPlayerItemDidPlayToEndTime)) { notification in
                guard let item = notification.object as? AVPlayerItem,
                      item === player.currentItem else { return }
                player.seek(to: .zero)
                player.play()
            }
    }
}

#if canImport(UIKit)
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

private struct FixedTopStatusHUD: View {
    @ObservedObject var data: K1L0OverlayDataModel

    var body: some View {
        HStack(alignment: .top) {
            WeatherPill(model: data)
            Spacer(minLength: 12)
            VStack(alignment: .trailing, spacing: 0) {
                Text(data.heroText)
                    .font(.system(size: 48, weight: .bold, design: .default))
                    .foregroundStyle(.white)
                    .monospacedDigit()
                Text("live steps")
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(.white.opacity(0.72))
                    .padding(.top, -4)
                Text("24h \(data.steps24h)   7d \(data.steps7d)")
                    .font(.system(size: 12, weight: .medium))
                    .monospacedDigit()
                    .foregroundStyle(.white.opacity(0.82))
            }
        }
    }
}

private struct WeatherPill: View {
    @ObservedObject var model: K1L0OverlayDataModel

    var body: some View {
        HStack(spacing: 8) {
            Image(systemName: model.weatherGlyph)
                .font(.system(size: 17, weight: .semibold))
            if !model.cityText.isEmpty {
                Text(model.cityText)
                    .font(.system(size: 17, weight: .semibold))
                    .lineLimit(1)
            }
            Text(model.weatherText)
                .font(.system(size: 17, weight: .semibold))
        }
        .foregroundStyle(.white)
        .padding(.horizontal, 13)
        .padding(.vertical, 9)
        .background(.black.opacity(0.28), in: Capsule())
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

private struct DropFilterBar: View {
    @Binding var selected: String

    private let filters = [
        ("all", "all"),
        ("drink", "🍺"),
        ("coffee", "☕️"),
        ("food", "🍽️"),
        ("service", "🛠️")
    ]

    var body: some View {
        HStack(spacing: 6) {
            ForEach(filters, id: \.0) { filter in
                Button {
                    selected = filter.0
                } label: {
                    Text(filter.1)
                        .font(.system(size: 12, weight: .bold))
                        .foregroundStyle(.white)
                        .padding(.horizontal, 8)
                        .padding(.vertical, 5)
                        .background(selected == filter.0 ? Color.green.opacity(0.42) : Color.white.opacity(0.10), in: Capsule())
                }
                .buttonStyle(.plain)
            }
        }
    }
}

private final class K1L0OverlayDataModel: NSObject, ObservableObject, CLLocationManagerDelegate {
    @Published var liveSteps = 0
    @Published var steps24h = 0
    @Published var steps7d = 0
    @Published var cityText = ""
    @Published var weatherText = "K1L0"
    @Published var weatherGlyph = "cloud.sun.fill"
    @Published var places: [OverlayPlace] = []
    @Published var beams: [OverlayBeam] = []
    @Published var elements: [OverlayElement] = []
    @Published var locationStatus = "loading nearby places…"
    @Published var beamStatus = "scanning transmissions…"
    @Published var elementsStatus = "loading elements…"
    @Published var apiStatus = "api resolving…"
    @Published private var now = Date()
    @Published private var headingDegrees = 0.0

    private let locationManager = CLLocationManager()
#if os(iOS)
    private let pedometer = CMPedometer()
#endif
    private var currentLocation: CLLocation?
    private var didFetchNearby = false
    private var nearbyRefreshTimer: Timer?
    private var clockTimer: Timer?
    private var activeAPIBase: String?
    private var isResolvingAPI = false
    private var lastWeatherFetchAt = Date.distantPast
    private var lastWeatherFetchLocation: CLLocation?
    private var lastBeamDistances: [String: Double] = [:]
    private var walkingTowardUntil: [String: Date] = [:]
    private var collectingBeamIds = Set<String>()
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

        var displayText: String {
            guard let tempF else { return "--°" }
            return "\(Int(tempF.rounded()))°"
        }
    }

    var heroText: String {
        "\(liveSteps)"
    }

    var ctaText: String {
        if nearestBeam != nil { return "SIGNAL ESTABLISHED" }
        return liveSteps > 0 ? "CONTINUE WALKING" : "WALK TO BOOST SIGNAL"
    }

    var ctaIcon: String {
        nearestBeam != nil ? "antenna.radiowaves.left.and.right" : "exclamationmark.triangle.fill"
    }

    var ctaColor: Color {
        nearestBeam != nil ? Color(red: 0.66, green: 1.0, blue: 0.76) : .yellow
    }

    var nearestBeam: OverlayBeam? {
        beams
            .filter { !isExpired($0) }
            .sorted { distanceMeters(to: $0) < distanceMeters(to: $1) }
            .first
    }

    func filteredPlaces(for filter: String) -> [OverlayPlace] {
        let normalized = filter.lowercased()
        guard normalized != "all" else { return places }
        return places.filter { placeCategory($0) == normalized }
    }

    func emoji(for place: OverlayPlace) -> String {
        switch placeCategory(place) {
        case "drink": return "🍺"
        case "coffee": return "☕️"
        case "food": return "🍽️"
        case "service": return "🛠️"
        default: return "📍"
        }
    }

    func start() {
        locationManager.delegate = self
        locationManager.desiredAccuracy = kCLLocationAccuracyBest
        locationManager.distanceFilter = 3
#if os(iOS)
        locationManager.pausesLocationUpdatesAutomatically = false
#endif

        switch locationManager.authorizationStatus {
        case .notDetermined:
            locationManager.requestWhenInUseAuthorization()
        case .authorizedAlways, .authorizedWhenInUse:
            locationManager.startUpdatingLocation()
            startHeadingUpdates()
        default:
            useFallbackLocation()
        }

        startPedometer()
        fetchInventory()
        startNearbyRefreshTimer()
        startClock()
    }

    func locationManagerDidChangeAuthorization(_ manager: CLLocationManager) {
        switch manager.authorizationStatus {
        case .authorizedAlways, .authorizedWhenInUse:
            locationManager.startUpdatingLocation()
            startHeadingUpdates()
        case .denied, .restricted:
            useFallbackLocation()
        default:
            break
        }
    }

    func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        guard let location = locations.last else { return }
        currentLocation = location
        updateBeamApproachState()
        checkForBeamCollection()
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
        useFallbackLocation()
    }

    func distanceText(to place: OverlayPlace) -> String {
        formatDistance(distanceMeters(to: place))
    }

    func distanceText(to beam: OverlayBeam) -> String {
        formatDistance(distanceMeters(to: beam))
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
        let fallback = CLLocation(latitude: 40.684, longitude: -80.107)
        currentLocation = fallback
        fetchWeather(latitude: fallback.coordinate.latitude, longitude: fallback.coordinate.longitude)
        if !didFetchNearby {
            didFetchNearby = true
            fetchNearby(latitude: fallback.coordinate.latitude, longitude: fallback.coordinate.longitude)
        }
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
        guard CMPedometer.isStepCountingAvailable() else { return }
        let now = Date()
        pedometer.startUpdates(from: now) { [weak self] data, _ in
            guard let data else { return }
            DispatchQueue.main.async {
                self?.liveSteps = data.numberOfSteps.intValue
            }
        }
        querySteps(since: Date(timeIntervalSinceNow: -24 * 60 * 60)) { [weak self] value in self?.steps24h = value }
        querySteps(since: Date(timeIntervalSinceNow: -7 * 24 * 60 * 60)) { [weak self] value in self?.steps7d = value }
#endif
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

    private func fetchNearby(latitude: Double, longitude: Double) {
        locationStatus = places.isEmpty ? "loading nearby places…" : locationStatus
        beamStatus = beams.isEmpty ? "scanning transmissions…" : beamStatus
        resolveAPIBase { [weak self] apiBase in
            guard let self else { return }
            self.fetchPlaces(latitude: latitude, longitude: longitude, apiBase: apiBase)
            self.fetchBeams(latitude: latitude, longitude: longitude, apiBase: apiBase)
            self.fetchInventory()
        }
    }

    private func startNearbyRefreshTimer() {
        nearbyRefreshTimer?.invalidate()
        nearbyRefreshTimer = Timer.scheduledTimer(withTimeInterval: 30, repeats: true) { [weak self] _ in
            guard let self, let location = self.currentLocation else { return }
            self.fetchNearby(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
        }
    }

    private func startClock() {
        clockTimer?.invalidate()
        now = Date()
        clockTimer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in
            self?.now = Date()
            self?.expireStaleApproachState()
            self?.checkForBeamCollection()
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
        guard let url = URL(string: "\(candidate)/ping") else {
            testAPIBase(at: index + 1, completion: completion)
            return
        }

        var request = URLRequest(url: url, timeoutInterval: candidate.contains("192.168") || candidate.contains("fred.local") || candidate.contains("172.20") ? 3 : 8)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = #"{"userId":"swift-overlay","lastActive":0}"#.data(using: .utf8)

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
            "radiusMeters": 1609
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
            }
        }.resume()
    }

    private func fetchBeams(latitude: Double, longitude: Double, apiBase: String) {
        guard let url = URL(string: "\(apiBase)/k1l0/beams/nearby") else { return }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: [
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
                let activeBeams = decoded.beams.filter { self?.isExpired($0) == false }
                self?.beams = activeBeams
                self?.beamStatus = activeBeams.isEmpty ? "no nearby transmissions" : "\(activeBeams.count) nearby"
                self?.updateBeamApproachState()
                self?.checkForBeamCollection()
            }
        }.resume()
    }

    private func fetchInventory() {
        guard let userId = currentUserIdForInventory(), !userId.isEmpty else {
            elementsStatus = "sign in to load elements"
            return
        }
        let safeUserId = sanitizeFirebaseKey(userId)
        guard let url = URL(string: "https://kiloworld-aa8d6-default-rtdb.firebaseio.com/users/\(safeUserId).json") else { return }

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
                DispatchQueue.main.async {
                    self?.elements = parsed
                    self?.elementsStatus = parsed.isEmpty ? "no collected elements" : "\(parsed.count) collected"
                }
            } catch {
                let snippet = String(data: data.prefix(180), encoding: .utf8) ?? "non-utf8"
                DispatchQueue.main.async { self?.elementsStatus = "elements decode error" }
                print("[K1L0Overlay] elements decode error: \(error) body=\(snippet)")
            }
        }.resume()
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
    }

    private func fetchBackendWeatherAndCity(latitude: Double, longitude: Double, apiBase: String) {
        guard let url = URL(string: "\(apiBase)/ping") else { return }
        let userId = currentUserIdForInventory() ?? "swift-overlay"
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: [
            "userId": userId,
            "platform": "native-ios",
            "manualPing": false,
            "coordinates": [
                "latitude": latitude,
                "longitude": longitude
            ]
        ])

        URLSession.shared.dataTask(with: request) { [weak self] data, _, _ in
            guard let data,
                  let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let weather = json["weather"] as? [String: Any]
            else { return }

            let city = json["city"] as? String
            let tempF = weather["temperatureF"] as? Double
            let glyph = (weather["glyph"] as? String) ?? (weather["icon"] as? String)
            let snapshot = WeatherSnapshot(
                city: city,
                tempF: tempF,
                glyph: Self.weatherGlyph(forBackendGlyph: glyph)
            )
            DispatchQueue.main.async {
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
                glyph: Self.weatherGlyph(forDescription: desc, isDay: !isNight)
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

        for beam in beams where !isExpired(beam) {
            let distance = distanceMeters(to: beam)
            if let previous = lastBeamDistances[beam.id], previous - distance > 1.2 {
                walkingTowardUntil[beam.id] = now.addingTimeInterval(8)
            }
            lastBeamDistances[beam.id] = distance
        }
    }

    private func checkForBeamCollection() {
        guard currentLocation != nil else { return }
        let radius = collectRadiusMeters()
        guard let beam = beams
            .filter({ !isExpired($0) && !collectingBeamIds.contains($0.id) })
            .sorted(by: { distanceMeters(to: $0) < distanceMeters(to: $1) })
            .first,
            distanceMeters(to: beam) <= radius
        else { return }

        collectBeam(beam)
    }

    private func collectBeam(_ beam: OverlayBeam) {
        collectingBeamIds.insert(beam.id)
        beams.removeAll { $0.id == beam.id }
        lastBeamDistances.removeValue(forKey: beam.id)
        walkingTowardUntil.removeValue(forKey: beam.id)
        beamStatus = beams.isEmpty ? "scanning transmissions…" : "\(beams.count) nearby"
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

    private func collectRadiusMeters() -> Double {
        let stored = UserDefaults.standard.double(forKey: "k1lo_native_ambientCollectRadiusMeters")
        return stored > 0 ? min(100, max(1, stored)) : 10
    }

    private func expireStaleApproachState() {
        walkingTowardUntil = walkingTowardUntil.filter { $0.value > now }
    }

    private func isExpired(_ beam: OverlayBeam) -> Bool {
        guard let expiresAt = beam.expiresAt else { return false }
        return expiresAt <= now.timeIntervalSince1970 * 1000
    }

    private func placeCategory(_ place: OverlayPlace) -> String {
        let type = place.type.lowercased()
        if type.contains("coffee") || type.contains("cafe") || type.contains("bakery") { return "coffee" }
        if type.contains("bar") || type.contains("brew") || type.contains("pub") || type.contains("drink") { return "drink" }
        if type.contains("restaurant") || type.contains("food") || type.contains("pizza") || type.contains("thai") || type.contains("wing") { return "food" }
        if type.contains("service") || type.contains("store") || type.contains("shop") || type.contains("gas") || type.contains("fuel") { return "service" }
        return "food"
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
        if let elementsRoot = root["elements"] as? [String: Any] {
            let aggregate = parseElementCollection(elementsRoot)
            if !aggregate.isEmpty { return aggregate }
        }
        if let itemsRoot = root["items"] as? [String: Any] {
            return parseElementCollection(itemsRoot)
        }
        return parseElementCollection(root)
    }

    private static func parseElementCollection(_ root: [String: Any]) -> [OverlayElement] {
        var totals: [String: Int] = [:]
        for value in root.values {
            guard let item = value as? [String: Any] else { continue }
            let rawName = firstString(item, keys: ["element", "material", "artifactMaterial", "rareEarthMineral", "artifact"])
            let name = canonicalElementName(rawName)
            guard !name.isEmpty else { continue }
            let grams = firstInt(item, keys: ["grams", "quantityGrams", "quantity"])
            guard grams > 0 else { continue }
            totals[name, default: 0] += max(0, grams)
        }
        return totals.map { OverlayElement(name: $0.key, grams: $0.value) }
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

private struct OverlayPlace: Decodable, Identifiable {
    let placeId: String?
    let name: String
    let type: String
    let coordinates: OverlayCoordinate
    let distance: Double
    let artifactMaterial: String?

    var id: String { placeId ?? name }

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

private struct OverlayBeam: Decodable, Identifiable {
    let id: String
    let lat: Double
    let lng: Double
    let label: String?
    let material: String?
    let senderName: String?
    let artifactSenderName: String?
    let grams: Int?
    let expiresAt: Double?
    let distanceMeters: Double

    var title: String {
        material?.capitalized ?? label?.capitalized ?? "Rare Earth"
    }

    var senderTitle: String {
        senderName ?? artifactSenderName ?? "Unknown"
    }
}

private struct OverlayElement: Identifiable {
    let name: String
    let grams: Int

    var id: String { name.lowercased() }

    var symbol: String { ElementSymbolLookup.symbol(for: name) }
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
            .background(Color.black.opacity(0.28), in: RoundedRectangle(cornerRadius: 24, style: .continuous))
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
            .background(Color.black.opacity(0.18), in: RoundedRectangle(cornerRadius: 28, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 28, style: .continuous)
                    .stroke(.white.opacity(0.20), lineWidth: 1)
            )
    }
}
