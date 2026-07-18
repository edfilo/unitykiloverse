import AppKit
import SwiftUI

public typealias K1L0UnityMessageCallback = @convention(c) (UnsafePointer<CChar>?, UnsafePointer<CChar>?, UnsafePointer<CChar>?) -> Void
private var unityCallback: K1L0UnityMessageCallback?

@_cdecl("K1L0SetUnityCallback")
public func K1L0SetUnityCallback(_ callback: K1L0UnityMessageCallback?) { unityCallback = callback }

enum K1L0WeatherOverlayInstaller {
    static func setUnitySetting(_ key: String, _ value: String) {
        "K1L0HUD".withCString { objectName in
            "SetNativeSetting".withCString { methodName in
                "\(key)=\(value)".withCString { message in unityCallback?(objectName, methodName, message) }
            }
        }
    }
}
enum NativeUnityLightingSync { static func sync() {} }
enum NativeUnitySolarSync { static func sync() {}; static func start() {} }

private enum MacTab: String, CaseIterable, Identifiable {
    case home = "HOME", transmit = "TRANSMIT", messages = "MESSAGES", profile = "PROFILE"
    var id: String { rawValue }
    var symbol: String {
        switch self {
        case .home: return "map.fill"
        case .transmit: return "antenna.radiowaves.left.and.right"
        case .messages: return "bubble.left.and.bubble.right.fill"
        case .profile: return "person.crop.circle.fill"
        }
    }
}

private struct K1L0MacOverlayRoot: View {
    @State private var tab: MacTab = .home
    @State private var showingWeather = false
    @AppStorage("k1lo_native_weatherLookMode") private var weatherMode = "pink_haze"

    var body: some View {
        ZStack(alignment: .bottom) {
            Color.clear.allowsHitTesting(false)
            VStack {
                HStack {
                    Button { showingWeather = true } label: {
                        Label(weatherMode.replacingOccurrences(of: "_", with: " ").uppercased(), systemImage: "cloud.sun.fill")
                            .font(.system(size: 12, weight: .black, design: .rounded))
                            .padding(.horizontal, 12).padding(.vertical, 8)
                            .background(.black.opacity(0.72), in: Capsule())
                    }
                    .buttonStyle(.plain).foregroundStyle(.white)
                    Spacer()
                }.padding(14)
                Spacer()
            }
            HStack(spacing: 2) {
                ForEach(MacTab.allCases) { candidate in
                    Button { tab = candidate } label: {
                        VStack(spacing: 3) {
                            Image(systemName: candidate.symbol).font(.system(size: 17, weight: .bold))
                            Text(candidate.rawValue).font(.system(size: 9, weight: .black, design: .rounded))
                        }
                        .foregroundStyle(tab == candidate ? Color.green : Color.white.opacity(0.68))
                        .frame(maxWidth: .infinity).frame(height: 54)
                    }.buttonStyle(.plain)
                }
            }
            .padding(.horizontal, 8).padding(.bottom, 6)
            .background(.black.opacity(0.82))
            .overlay(alignment: .top) { Rectangle().fill(.white.opacity(0.15)).frame(height: 1) }
        }
        .confirmationDialog("Sky Mode", isPresented: $showingWeather, titleVisibility: .visible) {
            presetButton("Day", "pink_haze"); presetButton("Night", "midnight"); presetButton("Auto", "auto")
            presetButton("Pink Haze", "pink_haze"); presetButton("Haze Lab", "haze_lab"); presetButton("Boring", "boring")
            Button("Cancel", role: .cancel) {}
        }
        .onAppear { K1L0WeatherModeController.apply(weatherMode) }
    }

    private func presetButton(_ title: String, _ mode: String) -> some View {
        Button(title) { weatherMode = mode; K1L0WeatherModeController.apply(mode) }
    }
}

private final class MacOverlayHost {
    static let shared = MacOverlayHost()
    private var panel: NSPanel?
    private weak var parent: NSWindow?
    private var observers: [NSObjectProtocol] = []

    func install() {
        if panel != nil { syncFrame(); panel?.orderFront(nil); return }
        guard let game = NSApp.mainWindow ?? NSApp.windows.first(where: { $0.isVisible && $0.frame.width > 200 }) else {
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.4) { self.install() }; return
        }
        let overlay = NSPanel(contentRect: game.frame, styleMask: [.borderless], backing: .buffered, defer: false)
        overlay.isOpaque = false; overlay.backgroundColor = .clear; overlay.hasShadow = false
        overlay.isReleasedWhenClosed = false; overlay.collectionBehavior = [.fullScreenAuxiliary, .transient]
        overlay.contentView = NSHostingView(rootView: K1L0MacOverlayRoot())
        game.addChildWindow(overlay, ordered: .above)
        parent = game; panel = overlay; syncFrame(); overlay.orderFront(nil)
        for name in [NSWindow.didMoveNotification, NSWindow.didResizeNotification] {
            observers.append(NotificationCenter.default.addObserver(forName: name, object: game, queue: .main) { [weak self] _ in self?.syncFrame() })
        }
        NSLog("[K1L0Overlay] Mac SwiftUI shell installed")
    }
    private func syncFrame() { if let panel, let parent { panel.setFrame(parent.frame, display: true) } }
}

@_cdecl("K1L0InstallWeatherOverlay") public func K1L0InstallWeatherOverlay() { DispatchQueue.main.async { MacOverlayHost.shared.install() } }
@_cdecl("K1L0SetWeatherLookMode") public func K1L0SetWeatherLookMode(_ ptr: UnsafePointer<CChar>?) { if let ptr { DispatchQueue.main.async { K1L0WeatherModeController.apply(String(cString: ptr)) } } }
@_cdecl("K1L0DeliverTransmissionResult") public func K1L0DeliverTransmissionResult(_ ptr: UnsafePointer<CChar>?) {}
@_cdecl("K1L0DeliverUserMetadataSaveResult") public func K1L0DeliverUserMetadataSaveResult(_ ptr: UnsafePointer<CChar>?) {}
@_cdecl("K1L0DeliverNativeAuthState") public func K1L0DeliverNativeAuthState(_ ptr: UnsafePointer<CChar>?) {}
@_cdecl("K1L0DeliverStepState") public func K1L0DeliverStepState(_ ptr: UnsafePointer<CChar>?) {}
@_cdecl("K1L0DeliverRenderReadiness") public func K1L0DeliverRenderReadiness(_ ptr: UnsafePointer<CChar>?) {}
