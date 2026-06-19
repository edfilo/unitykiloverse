import CoreLocation
import CoreMotion
import Foundation
import SwiftUI
import UIKit

@_silgen_name("UnitySendMessage")
private func UnitySendMessage(_ objectName: UnsafePointer<CChar>, _ methodName: UnsafePointer<CChar>, _ message: UnsafePointer<CChar>)

@_cdecl("K1L0InstallWeatherOverlay")
public func K1L0InstallWeatherOverlay() {
    DispatchQueue.main.async {
        K1L0WeatherOverlayInstaller.install()
    }
}

private final class K1L0WeatherOverlayInstaller {
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
}

private struct K1L0WeatherOverlayRoot: View {
    @StateObject private var data = K1L0OverlayDataModel()
    @State private var hudVisible = true
    @State private var showingSettings = false

    var body: some View {
        ZStack {
            if !showingSettings {
                ScrollView(.vertical, showsIndicators: false) {
                    VStack(spacing: 8) {
                        HStack {
                            WeatherPill(model: data)
                            Spacer()
                        }
                        .padding(.top, 0)

                        VStack(spacing: 3) {
                            Text(data.heroText)
                                .font(.system(size: 76, weight: .bold, design: .default))
                                .foregroundStyle(.white)
                            Text("steps")
                                .font(.system(size: 13, weight: .medium))
                                .foregroundStyle(.white.opacity(0.72))
                                .padding(.top, -10)
                            Text("24h \(data.steps24h)     7d \(data.steps7d)")
                                .font(.system(size: 13, weight: .medium))
                                .monospacedDigit()
                                .foregroundStyle(.white.opacity(0.82))
                            HStack(spacing: 7) {
                                Image(systemName: data.ctaIcon)
                                    .foregroundStyle(data.ctaColor)
                                Text(data.ctaText)
                                    .font(.system(size: 15, weight: .bold))
                                    .foregroundStyle(.white)
                            }
                            .padding(.top, 10)
                        }
                        .frame(maxWidth: .infinity)
                        .padding(.top, 0)
                        .padding(.bottom, 20)

                        if let beam = data.nearestBeam {
                            WeatherAlertCard {
                                HStack(spacing: 12) {
                                    Image(systemName: "exclamationmark.triangle.fill")
                                        .font(.system(size: 22, weight: .bold))
                                        .foregroundStyle(.yellow)
                                    VStack(alignment: .leading, spacing: 4) {
                                        Text("NEARBY TRANSMISSION FROM \(beam.senderTitle.uppercased())")
                                            .font(.system(size: 15, weight: .black))
                                            .foregroundStyle(.white)
                                        Text(beam.title)
                                            .font(.system(size: 14, weight: .semibold))
                                            .foregroundStyle(.white.opacity(0.76))
                                        Text(data.expirationText(for: beam))
                                            .font(.system(size: 12, weight: .bold))
                                            .monospacedDigit()
                                            .foregroundStyle(.yellow.opacity(0.92))
                                    }
                                    Spacer()
                                    DirectionCell(
                                        distance: data.distanceText(to: beam),
                                        relativeBearing: data.relativeBearingDegrees(to: beam)
                                    )
                                }
                            }
                        }

                        if hudVisible {
                        WeatherGlassCard {
                            VStack(alignment: .leading, spacing: 12) {
                                Text("Nearby")
                                    .font(.system(size: 25, weight: .bold))
                                Text(data.locationStatus)
                                    .font(.system(size: 13, weight: .medium))
                                    .foregroundStyle(.white.opacity(0.70))
                                Text(data.apiStatus)
                                    .font(.system(size: 11, weight: .medium))
                                    .foregroundStyle(.white.opacity(0.48))

                                ForEach(data.places.prefix(5)) { place in
                                    HStack(spacing: 10) {
                                        DirectionCell(
                                            distance: data.distanceText(to: place),
                                            relativeBearing: data.relativeBearingDegrees(to: place)
                                        )
                                        VStack(alignment: .leading, spacing: 2) {
                                            Text(place.name)
                                                .font(.system(size: 16, weight: .semibold))
                                                .lineLimit(1)
                                            Text(place.type)
                                                .font(.system(size: 11, weight: .medium))
                                                .foregroundStyle(.white.opacity(0.55))
                                        }
                                        Spacer()
                                    }
                                    .padding(.top, 2)
                                }
                            }
                        }

                        WeatherGlassCard {
                            VStack(alignment: .leading, spacing: 10) {
                                Text("Transmission")
                                    .font(.system(size: 25, weight: .bold))
                                if let beam = data.nearestBeam {
                                    HStack(spacing: 10) {
                                        DirectionCell(
                                            distance: data.distanceText(to: beam),
                                            relativeBearing: data.relativeBearingDegrees(to: beam)
                                        )
                                        VStack(alignment: .leading, spacing: 3) {
                                            Text(beam.title)
                                                .font(.system(size: 16, weight: .semibold))
                                            Text(data.beamStatus)
                                                .font(.system(size: 12, weight: .medium))
                                                .foregroundStyle(.white.opacity(0.65))
                                            Text(data.expirationText(for: beam))
                                                .font(.system(size: 12, weight: .bold))
                                                .monospacedDigit()
                                                .foregroundStyle(.yellow.opacity(0.92))
                                        }
                                        Spacer()
                                    }
                                } else {
                                    Text(data.beamStatus)
                                        .font(.system(size: 13, weight: .medium))
                                        .foregroundStyle(.white.opacity(0.70))
                                }
                            }
                        }
                        }
                    }
                    .padding(.horizontal, 18)
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

            VStack {
                Spacer()
                HStack {
                    Button {
                        K1L0WeatherOverlayInstaller.keepOverlayInFront()
                        hudVisible.toggle()
                        showingSettings = false
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
                        showingSettings.toggle()
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

private struct WeatherPill: View {
    @ObservedObject var model: K1L0OverlayDataModel

    var body: some View {
        HStack(spacing: 8) {
            Image(systemName: model.weatherGlyph)
                .font(.system(size: 17, weight: .semibold))
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

private final class K1L0OverlayDataModel: NSObject, ObservableObject, CLLocationManagerDelegate {
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
    @Published private var headingDegrees = 0.0

    private let locationManager = CLLocationManager()
    private let pedometer = CMPedometer()
    private var currentLocation: CLLocation?
    private var didFetchNearby = false
    private var nearbyRefreshTimer: Timer?
    private var clockTimer: Timer?
    private var activeAPIBase: String?
    private var isResolvingAPI = false
    private let apiCandidates = [
        "https://api-tunnel.kilo.gallery",
        "http://192.168.40.34:3000",
        "http://fred.local:3000",
        "http://172.20.10.5:3000",
        "https://api.kilomeme.com"
    ]

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
        beams.sorted { distanceMeters(to: $0) < distanceMeters(to: $1) }.first
    }

    func start() {
        locationManager.delegate = self
        locationManager.desiredAccuracy = kCLLocationAccuracyBest
        locationManager.distanceFilter = 3
        locationManager.pausesLocationUpdatesAutomatically = false

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
        fetchWeather(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
        if !didFetchNearby {
            didFetchNearby = true
            fetchNearby(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
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
        if CLLocationManager.headingAvailable() {
            locationManager.startUpdatingHeading()
        }
    }

    private func startPedometer() {
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
    }

    private func querySteps(since start: Date, assign: @escaping (Int) -> Void) {
        pedometer.queryPedometerData(from: start, to: Date()) { data, _ in
            DispatchQueue.main.async {
                assign(data?.numberOfSteps.intValue ?? 0)
            }
        }
    }

    private func fetchNearby(latitude: Double, longitude: Double) {
        locationStatus = places.isEmpty ? "loading nearby places…" : locationStatus
        beamStatus = beams.isEmpty ? "scanning transmissions…" : beamStatus
        resolveAPIBase { [weak self] apiBase in
            guard let self else { return }
            self.fetchPlaces(latitude: latitude, longitude: longitude, apiBase: apiBase)
            self.fetchBeams(latitude: latitude, longitude: longitude, apiBase: apiBase)
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
                self?.beams = decoded.beams
                self?.beamStatus = decoded.beams.isEmpty ? "no nearby transmissions" : "\(decoded.beams.count) nearby"
            }
        }.resume()
    }

    private func fetchWeather(latitude: Double, longitude: Double) {
        guard let url = URL(string: "https://wttr.in/\(latitude),\(longitude)?format=j1") else { return }
        URLSession.shared.dataTask(with: url) { [weak self] data, _, _ in
            guard let data, let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let current = (json["current_condition"] as? [[String: Any]])?.first
            else { return }
            let temp = current["temp_F"] as? String ?? "--"
            let desc = ((current["weatherDesc"] as? [[String: Any]])?.first?["value"] as? String ?? "").lowercased()
            DispatchQueue.main.async {
                self?.weatherText = "\(temp)°"
                self?.weatherGlyph = desc.contains("rain") ? "cloud.rain.fill" : (desc.contains("cloud") ? "cloud.fill" : "sun.max.fill")
            }
        }.resume()
    }

    private func distanceMeters(to place: OverlayPlace) -> Double {
        guard let currentLocation else { return place.distance }
        return currentLocation.distance(from: CLLocation(latitude: place.coordinates.lat, longitude: place.coordinates.lng))
    }

    private func distanceMeters(to beam: OverlayBeam) -> Double {
        guard let currentLocation else { return beam.distanceMeters }
        return currentLocation.distance(from: CLLocation(latitude: beam.lat, longitude: beam.lng))
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

    var id: String { placeId ?? name }
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
    let expiresAt: Double?
    let distanceMeters: Double

    var title: String {
        material?.capitalized ?? label?.capitalized ?? "Rare Earth"
    }

    var senderTitle: String {
        senderName ?? artifactSenderName ?? "Unknown"
    }
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
