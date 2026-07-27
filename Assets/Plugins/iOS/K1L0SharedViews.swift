import SwiftUI
import AVFoundation
import CoreLocation
import MetalKit
import CoreMedia
import Metal
#if canImport(UIKit)
import UIKit
#elseif canImport(AppKit)
import AppKit
#endif

struct InventoryTile: View {
    let item: OverlayInventoryItem

    var body: some View {
        VStack(spacing: 6) {
            ZStack {
                // Element tiles keep the green-tinted card; non-element items
                // float freely — no card, no border, no black background. The
                // BlackMaskedRemoteImage masks pitch-black bg to alpha so the
                // item silhouette is what shows.
                if item.isElement {
                    RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .fill(Color(red: 0.05, green: 0.25, blue: 0.12).opacity(0.72))
                        .overlay(
                            RoundedRectangle(cornerRadius: 8, style: .continuous)
                                .stroke(Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.42), lineWidth: 1)
                        )
                    Text(item.symbol)
                        .font(.system(size: 24, weight: .black))
                        .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                } else if let url = URL(string: item.avatarUrl), !item.avatarUrl.isEmpty {
                    BlackMaskedRemoteImage(url: url, contentMode: .fill)
                        .scaleEffect(2.0)
                        .frame(width: 58, height: 58)
                        .clipped()
                } else {
                    Text(item.symbol)
                        .font(.system(size: 18, weight: .black))
                        .foregroundStyle(.white.opacity(0.84))
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

enum ElementSymbolLookup {
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

struct WeatherAlertCard<Content: View>: View {
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

struct WeatherGlassCard<Content: View>: View {
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
func k1l0DismissKeyboard() {
    UIApplication.shared.sendAction(#selector(UIResponder.resignFirstResponder), to: nil, from: nil, for: nil)
}

final class K1L0KeyboardObserver: ObservableObject {
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
final class K1L0OverlayWindow: NSWindow {
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

final class K1L0KeyboardObserver: ObservableObject {
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
 
