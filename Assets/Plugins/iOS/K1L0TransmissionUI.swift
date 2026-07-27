import SwiftUI
import AVFoundation
import CoreLocation
import MetalKit
import CoreMedia
import Metal
import CryptoKit
#if canImport(UIKit)
import UIKit
#elseif canImport(AppKit)
import AppKit
#endif

struct NativeTransmissionPanel: View {
    @ObservedObject var data: K1L0OverlayDataModel
    let elements: [OverlayElement]
    var tabsMode: Bool = false
    let onClose: () -> Void
    @ObservedObject private var activeTransmission = K1L0ActiveTransmissionStore.shared

#if canImport(UIKit)
    @State private var selectedPhoto: UIImage?
    @State private var photoPickerRequest: PhotoPickerRequest? = nil
#elseif canImport(AppKit)
    @State private var selectedPhoto: NSImage?
#endif
    @State private var selectedPhotoPath = ""
    @State private var message = ""
    @State private var status = "add an image and say what you are up to."
    private let defaultMood = "live"

    private var transmitterStateText: String {
        let snapshot = activeTransmission.snapshot
        guard snapshot.active else { return "CREATE TRANSMISSION" }
        if !snapshot.videoUrl.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { return "TRANSMITTING" }
        return "BUILDING TRANSMISSION"
    }

    private var isUnderway: Bool {
        activeTransmission.snapshot.active && activeTransmission.snapshot.videoUrl.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    var body: some View {
        GeometryReader { geometry in
            let panelTop = geometry.safeAreaInsets.top
            let panelBottom = max(88, geometry.safeAreaInsets.bottom + 84)
            let panelHeight = max(520, geometry.size.height - panelTop - panelBottom)
            let showingFullscreenTransmission = activeTransmission.snapshot.active
            ZStack(alignment: .top) {
                Color.clear.ignoresSafeArea()

                if showingFullscreenTransmission {
                    Color.black.ignoresSafeArea()
                    ActiveTransmissionTerminal(
                        snapshot: activeTransmission.snapshot,
                        walkSteps: data.liveSteps,
                        availableHeight: geometry.size.height,
                        onStop: { activeTransmission.stop() },
                        onFailureReset: { restoreFailedDraft(activeTransmission.snapshot) },
                        onNewTransmission: createNewTransmission,
                        fullscreenPlayer: true,
                        onClose: onClose
                    )
                    .frame(width: geometry.size.width, height: geometry.size.height, alignment: .top)
                    .background(Color.black)
                    .ignoresSafeArea()
                } else {
                ZStack(alignment: .top) {
                    // ScrollView so SwiftUI's automatic keyboard-avoidance can
                    // inset content (push the message field above the keyboard
                    // when it slides in). Without a scroll container the field
                    // sits behind the keyboard.
                    ScrollView(.vertical, showsIndicators: false) {
                        VStack(alignment: .leading, spacing: 10) {
                            TransmitterPanelHeader(
                                state: transmitterStateText,
                                isTransmitting: isUnderway,
                                tabsMode: tabsMode,
                                onStop: { activeTransmission.stop() },
                                onClose: onClose
                            )

                            if activeTransmission.snapshot.active {
                                Button(action: createNewTransmission) {
                                    HStack(spacing: 10) {
                                        if !isUnderway {
                                            Image(systemName: "plus.circle.fill")
                                                .font(.system(size: 20, weight: .black))
                                        }
                                        Text(isUnderway ? "Creating Transmission" : "Create New Transmission")
                                            .font(.system(size: 17, weight: .black))
                                    }
                                    .foregroundStyle(.black.opacity(0.88))
                                    .frame(maxWidth: .infinity, minHeight: 58)
                                    .background {
                                        Group {
                                            if isUnderway {
                                                SweepingGreenBackground()
                                            } else {
                                                Color.green
                                            }
                                        }
                                        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
                                    }
                                    .overlay(
                                        RoundedRectangle(cornerRadius: 16, style: .continuous)
                                            .stroke(Color.white.opacity(0.48), lineWidth: 1.2)
                                    )
                                    .shadow(color: Color.green.opacity(0.38), radius: 14, y: 4)
                                }
                                .buttonStyle(.plain)
                                .padding(.horizontal, 20)
                            }

                            if activeTransmission.snapshot.active {
                                ActiveTransmissionTerminal(
                                    snapshot: activeTransmission.snapshot,
                                    walkSteps: data.liveSteps,
                                    availableHeight: max(360, panelHeight - 62),
                                    onStop: { activeTransmission.stop() },
                                    onFailureReset: { restoreFailedDraft(activeTransmission.snapshot) },
                                    onNewTransmission: createNewTransmission
                                )
                            } else {
                                WeatherGlassCard {
                                    VStack(alignment: .leading, spacing: 8) {
                                        Text("Take a photo to transmit")
                                            .font(.system(size: 17, weight: .bold))
                                            .frame(maxWidth: .infinity, alignment: .center)
#if canImport(UIKit)
                                        // Live shots only — a transmission starts
                                        // from what you're looking at right now.
                                        transmitterPhotoButton("Camera", systemImage: "camera.fill", source: .camera)
                                        if let selectedPhoto {
                                            Image(uiImage: selectedPhoto)
                                                .resizable()
                                                .scaledToFill()
                                                .frame(maxWidth: .infinity)
                                                .frame(height: 96)
                                                .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
                                                .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).stroke(Color.green.opacity(0.85), lineWidth: 1.2))
                                        }
#elseif canImport(AppKit)
                                        Button {
                                            macSelectPhoto()
                                        } label: {
                                            Label("Photo", systemImage: "photo.on.rectangle.angled")
                                                .font(.system(size: 14, weight: .black))
                                                .foregroundStyle(.white)
                                                .frame(maxWidth: .infinity, minHeight: 44)
                                                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
                                                .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).stroke(Color.white.opacity(0.30), lineWidth: 1))
                                        }
                                        .buttonStyle(.plain)
                                        if let selectedPhoto {
                                            Image(nsImage: selectedPhoto)
                                                .resizable()
                                                .scaledToFill()
                                                .frame(width: 58, height: 58)
                                                .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                                                .overlay(RoundedRectangle(cornerRadius: 8, style: .continuous).stroke(Color.green.opacity(0.85), lineWidth: 1.2))
                                        }
#endif
                                        Text(selectedPhotoPath.isEmpty ? "no photo attached" : "photo attached")
                                            .font(.system(size: 13, weight: .semibold))
                                            .foregroundStyle(selectedPhotoPath.isEmpty ? .white.opacity(0.54) : Color(red: 0.66, green: 1.0, blue: 0.76))
                                    }
                                }

                                Text("What are you up to?")
                                    .font(.system(size: 17, weight: .bold))
                                    .foregroundStyle(.white)

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
                                    HStack(spacing: 10) {
                                        Image(systemName: "antenna.radiowaves.left.and.right")
                                            .font(.system(size: 17, weight: .black))
                                        Text("Start Transmitting")
                                            .font(.system(size: 17, weight: .black))
                                    }
                                    .foregroundStyle(.white)
                                    .frame(maxWidth: .infinity, minHeight: 56)
                                    .background(canTransmit ? Color.green.opacity(0.94) : Color.white.opacity(0.07), in: Capsule())
                                    .overlay(Capsule().stroke(canTransmit ? Color.green.opacity(0.95) : Color.white.opacity(0.18), lineWidth: 1.2))
                                }
                                .buttonStyle(.plain)
                                .disabled(!canTransmit)
                                .opacity(canTransmit ? 1 : 0.72)
                            }
                        }
                        .padding(.horizontal, activeTransmission.snapshot.active ? 0 : 20)
                        .padding(.top, 0)
                        .padding(.bottom, activeTransmission.snapshot.active ? 4 : 38)
                    }
                    // Tap-outside-the-keyboard to dismiss feels right for a
                    // single-field panel; user can also drag to dismiss.
                    .scrollDismissesKeyboardCompat()
                }
                .frame(maxWidth: .infinity)
                .frame(height: panelHeight)
                .background(tabsMode ? Color.clear : Color.black.opacity(activeTransmission.snapshot.active ? 0.02 : 0.14), in: RoundedRectangle(cornerRadius: 28, style: .continuous))
                .overlay(
                    RoundedRectangle(cornerRadius: 28, style: .continuous)
                        .stroke(Color.white.opacity(tabsMode ? 0 : (activeTransmission.snapshot.active ? 0.06 : 0.14)), lineWidth: 1)
                )
                .padding(.top, panelTop)
                .padding(.bottom, panelBottom)
                .gesture(
                    DragGesture(minimumDistance: 18)
                        .onEnded { value in
                            guard !tabsMode else { return }
                            if value.translation.height > 80 {
                                onClose()
                            }
                        }
                )
                }
            }
            // ignoresSafeArea(edges: .bottom) was here — it disables the
            // keyboard inset and hides the message field behind the keyboard.
            // Let the default safe-area behavior push the panel up.
        }
        .onAppear {
            data.refreshTransmissionState(clearStaleCache: true)
        }
#if canImport(UIKit)
        .sheet(item: $photoPickerRequest) { request in
            NativePhotoPicker(sourceType: request.source) { image, path in
                if let image, let path {
                    selectedPhoto = image
                    selectedPhotoPath = path
                    status = "photo attached."
                }
                photoPickerRequest = nil
            }
            .ignoresSafeArea()
        }
#endif
    }

    @ViewBuilder
    private var messageField: some View {
#if canImport(UIKit)
        TextField("What are you up to?", text: $message)
            .textInputAutocapitalization(.sentences)
            .transmitterKeyboardDoneToolbar()
#else
        TextField("What are you up to?", text: $message)
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
    private func transmitterPhotoButton(_ title: String, systemImage: String, source: UIImagePickerController.SourceType) -> some View {
        Button {
            guard UIImagePickerController.isSourceTypeAvailable(source) else {
                status = source == .camera ? "camera unavailable." : "photo library unavailable."
                return
            }
            photoPickerRequest = PhotoPickerRequest(source: source)
        } label: {
            Label(title, systemImage: systemImage)
                .font(.system(size: 14, weight: .black))
                .foregroundStyle(.white)
                .frame(maxWidth: .infinity, minHeight: 44)
                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).stroke(Color.white.opacity(0.30), lineWidth: 1))
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
        let locationSummary = data.cityText.trimmingCharacters(in: .whitespacesAndNewlines)
        let weatherSummary = data.weatherText.trimmingCharacters(in: .whitespacesAndNewlines)
        K1L0ActiveTransmissionStore.shared.start(
            photoPath: selectedPhotoPath,
            message: cleanMessage,
            mood: defaultMood,
            locationSummary: locationSummary,
            weatherSummary: weatherSummary
        )
        status = "transmitting..."
        data.submitNativeTransmission(photoPath: selectedPhotoPath, message: cleanMessage, mood: defaultMood) { nextStatus in
            status = nextStatus
        }
    }

    private func createNewTransmission() {
        activeTransmission.stop()
        selectedPhotoPath = ""
        selectedPhoto = nil
        message = ""
        status = "take a new photo to begin."
    }

    private func restoreFailedDraft(_ snapshot: K1L0ActiveTransmissionSnapshot) {
        message = snapshot.message
        selectedPhotoPath = snapshot.photoPath
#if canImport(UIKit)
        if !snapshot.photoPath.isEmpty {
            selectedPhoto = UIImage(contentsOfFile: snapshot.photoPath)
        }
#elseif canImport(AppKit)
        if !snapshot.photoPath.isEmpty {
            selectedPhoto = NSImage(contentsOfFile: snapshot.photoPath)
        }
#endif
        status = "couldn't establish signal. edit or try again."
        activeTransmission.stop()
    }
}

struct TransmitterPanelHeader: View {
    let state: String
    let isTransmitting: Bool
    var tabsMode: Bool = false
    let onStop: () -> Void
    let onClose: () -> Void

    var body: some View {
        VStack(spacing: 5) {
            if !tabsMode {
                RoundedRectangle(cornerRadius: 3, style: .continuous)
                    .fill(Color.white.opacity(0.34))
                    .frame(width: 44, height: 5)
                    .padding(.top, 8)
            }
            ZStack {
                VStack(spacing: 2) {
                    Text("TRANSMITTER")
                        .font(.system(size: 18, weight: .black, design: .monospaced))
                        .foregroundStyle(.white)
                    Text(state)
                        .font(.system(size: 10, weight: .black, design: .monospaced))
                        .foregroundStyle(.white.opacity(0.64))
                        .lineLimit(1)
                        .minimumScaleFactor(0.72)
                }
                .frame(maxWidth: .infinity, alignment: .center)

                HStack {
                    if isTransmitting {
                        Button(action: onStop) {
                            Text("CANCEL")
                                .font(.system(size: 12, weight: .black, design: .monospaced))
                                .foregroundStyle(.white)
                                .padding(.horizontal, 13)
                                .frame(height: 34)
                                .background(
                                    LinearGradient(
                                        colors: [Color.red.opacity(0.86), Color.red.opacity(0.34)],
                                        startPoint: .topLeading,
                                        endPoint: .bottomTrailing
                                    ),
                                    in: Capsule()
                                )
                                .overlay(Capsule().stroke(Color.white.opacity(0.25), lineWidth: 1))
                        }
                        .buttonStyle(.plain)
                    } else {
                        Color.clear.frame(width: 64, height: 34)
                    }
                    Spacer()
                    if !tabsMode {
                        Button(action: onClose) {
                            Image(systemName: "xmark")
                                .font(.system(size: 15, weight: .black))
                                .foregroundStyle(.white)
                                .frame(width: 38, height: 38)
                        }
                        .buttonStyle(.plain)
                    } else {
                        Color.clear.frame(width: 38, height: 38)
                    }
                }
            }
            .padding(.horizontal, 16)
            .padding(.bottom, 10)
        }
        .frame(maxWidth: .infinity)
        .background(Color.clear)
        .contentShape(Rectangle())
    }
}

struct TransmissionBuildingArtwork: View {
    let imageUrl: String
    let photoPath: String
    let signalStrength: Double

    // SSTV-style build: the outgoing frame arrives one coarse pixel at a time
    // in scan order, with static below the scan line, row shears, and chroma
    // ghosts. Pixels sample the real image — when the NanoBanana still lands,
    // the buffer re-samples and traces of the generated frame appear mid-scan.
    private static let gridCols = 27
    private static let gridRows = 48
    private static let scanSeconds = 20.0

    @State private var cellColors: [Color] = []
    @State private var scanStart = Date()

    private var resolvedURL: URL? {
        let value = imageUrl.trimmingCharacters(in: .whitespacesAndNewlines)
        return value.isEmpty ? nil : URL(string: value)
    }

    var body: some View {
        GeometryReader { geometry in
            TimelineView(.animation(minimumInterval: 1.0 / 24.0)) { timeline in
                let time = timeline.date.timeIntervalSinceReferenceDate
                let elapsed = timeline.date.timeIntervalSince(scanStart)
                ZStack {
                    Color.black
                    Canvas { context, size in
                        drawScan(context: context, size: size, time: time, elapsed: elapsed)
                    }
                    VStack(spacing: 14) {
                        Spacer()
                        Text("ESTABLISHING OUTGOING SIGNAL")
                            .font(.system(size: 18, weight: .black, design: .monospaced))
                            .foregroundStyle(.white)
                        Text("WALK TO STRENGTHEN SIGNAL")
                            .font(.system(size: 24, weight: .black, design: .monospaced))
                            .foregroundStyle(Color.green)
                            .multilineTextAlignment(.center)
                        TenBarSignalMeter(strength: signalStrength)
                            .scaleEffect(x: 2.25, y: 2.0)
                            .frame(height: 44)
                        Text("\(Int((signalStrength * 100).rounded()))%")
                            .font(.system(size: 18, weight: .black, design: .monospaced))
                            .foregroundStyle(.white)
                        Spacer().frame(height: 44)
                    }
                    .padding(.horizontal, 28)
                }
                .frame(width: geometry.size.width, height: geometry.size.height)
                .clipped()
            }
        }
        .task(id: "\(imageUrl)|\(photoPath)") { await loadPixels() }
    }

    private func cellHash(_ a: Int, _ b: Int) -> Double {
        var h = UInt64(bitPattern: Int64(a &* 374761393 &+ b &* 668265263))
        h = (h ^ (h >> 13)) &* 1274126177
        return Double(h % 1000) / 1000.0
    }

    private func drawScan(context: GraphicsContext, size: CGSize, time: Double, elapsed: Double) {
        let cols = Self.gridCols, rows = Self.gridRows
        let cw = size.width / CGFloat(cols)
        let ch = size.height / CGFloat(rows)
        let total = cols * rows
        let revealed = cellColors.isEmpty
            ? 0
            : min(total, Int(max(0, elapsed) * Double(total) / Self.scanSeconds))
        let frameTick = Int(time * 3)
        let flicker = 0.94 + 0.06 * sin(time * 13.7)

        for index in 0..<revealed {
            let row = index / cols
            let col = index % cols
            var x = CGFloat(col) * cw
            let y = CGFloat(row) * ch
            // Vintage transmission shear: occasional rows slip sideways.
            let g = cellHash(row, frameTick)
            let sheared = g > 0.93
            if sheared {
                x += cw * CGFloat((g - 0.93) * 40.0) * (cellHash(row, frameTick + 7) > 0.5 ? 1 : -1)
            }
            let color = cellColors.indices.contains(index) ? cellColors[index] : Color(white: 0.08)
            let cellFlicker = flicker * (0.90 + 0.10 * cellHash(index, frameTick))
            let rect = CGRect(x: x, y: y, width: cw + 0.5, height: ch + 0.5)
            context.fill(Path(rect), with: .color(color.opacity(cellFlicker)))
            if sheared, col == 0 {
                // Chroma ghosts hug sheared rows.
                let rowRect = CGRect(x: x + 3, y: y, width: size.width, height: ch)
                context.fill(Path(rowRect), with: .color(Color.red.opacity(0.10)))
                let rowRect2 = CGRect(x: x - 3, y: y, width: size.width, height: ch)
                context.fill(Path(rowRect2), with: .color(Color.cyan.opacity(0.10)))
            }
        }

        if revealed < total {
            let headRow = revealed / cols
            let headCol = revealed % cols
            // Glowing scan line + hot leading cell.
            context.fill(Path(CGRect(x: 0, y: CGFloat(headRow) * ch, width: size.width, height: ch)),
                         with: .color(Color.green.opacity(0.10)))
            context.fill(Path(CGRect(x: CGFloat(headCol) * cw - cw * 0.5, y: CGFloat(headRow) * ch, width: cw * 1.8, height: ch)),
                         with: .color(Color.white.opacity(0.85)))
            // Static in the not-yet-received region.
            let noiseRows = rows - headRow - 1
            if noiseRows > 0 {
                for n in 0..<130 {
                    let nx = cellHash(n, frameTick) * Double(cols)
                    let ny = Double(headRow + 1) + cellHash(n, frameTick + 31) * Double(noiseRows)
                    let v = cellHash(n, frameTick + 77)
                    let c = v > 0.86 ? Color.green.opacity(0.35) : Color(white: v * 0.30)
                    context.fill(Path(CGRect(x: nx * cw, y: ny * ch, width: cw * 0.7, height: ch * 0.7)),
                                 with: .color(c))
                }
            }
        }

        // CRT scanlines.
        var line = 0
        while line < rows {
            context.fill(Path(CGRect(x: 0, y: CGFloat(line) * ch, width: size.width, height: 1)),
                         with: .color(.black.opacity(0.22)))
            line += 2
        }
    }

    private func loadPixels() async {
        var cg: CGImage? = nil
#if canImport(UIKit)
        if let url = resolvedURL, let (data, _) = try? await URLSession.shared.data(from: url) {
            cg = UIImage(data: data)?.cgImage
        }
        if cg == nil, !photoPath.isEmpty {
            cg = UIImage(contentsOfFile: photoPath)?.cgImage
        }
#elseif canImport(AppKit)
        if let url = resolvedURL, let (data, _) = try? await URLSession.shared.data(from: url),
           let ns = NSImage(data: data) {
            cg = ns.cgImage(forProposedRect: nil, context: nil, hints: nil)
        }
        if cg == nil, !photoPath.isEmpty, let ns = NSImage(contentsOfFile: photoPath) {
            cg = ns.cgImage(forProposedRect: nil, context: nil, hints: nil)
        }
#endif
        guard let cgImage = cg else { return }
        let cols = Self.gridCols, rows = Self.gridRows
        var pixels = [UInt8](repeating: 0, count: cols * rows * 4)
        let drawn: Bool = pixels.withUnsafeMutableBytes { buffer in
            guard let ctx = CGContext(
                data: buffer.baseAddress, width: cols, height: rows,
                bitsPerComponent: 8, bytesPerRow: cols * 4,
                space: CGColorSpaceCreateDeviceRGB(),
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
            ) else { return false }
            ctx.interpolationQuality = .low
            let iw = CGFloat(cgImage.width), ih = CGFloat(cgImage.height)
            let targetAspect = CGFloat(cols) / CGFloat(rows)
            var drawW = CGFloat(cols), drawH = CGFloat(rows)
            var dx: CGFloat = 0, dy: CGFloat = 0
            if iw / ih > targetAspect {
                drawW = CGFloat(rows) * (iw / ih); dx = -(drawW - CGFloat(cols)) / 2
            } else {
                drawH = CGFloat(cols) * (ih / iw); dy = -(drawH - CGFloat(rows)) / 2
            }
            ctx.draw(cgImage, in: CGRect(x: dx, y: dy, width: drawW, height: drawH))
            return true
        }
        guard drawn else { return }
        var colors: [Color] = []
        colors.reserveCapacity(cols * rows)
        for row in 0..<rows {
            let bufferRow = rows - 1 - row // CG origin is bottom-left
            for col in 0..<cols {
                let i = (bufferRow * cols + col) * 4
                colors.append(Color(
                    red: Double(pixels[i]) / 255.0,
                    green: Double(pixels[i + 1]) / 255.0,
                    blue: Double(pixels[i + 2]) / 255.0
                ))
            }
        }
        let sampled = colors
        await MainActor.run {
            let firstLoad = cellColors.isEmpty
            cellColors = sampled
            // First image starts the scan; a later swap (NanoBanana ready)
            // keeps scan progress so its traces appear in already-built rows.
            if firstLoad { scanStart = Date() }
        }
    }
}

struct CyberBuildingPixels: View {
    private let colors: [Color] = [
        Color(red: 1.0, green: 0.08, blue: 0.48),
        Color(red: 0.04, green: 0.84, blue: 1.0),
        Color(red: 0.55, green: 0.16, blue: 1.0),
        Color(red: 1.0, green: 0.72, blue: 0.05),
        Color(red: 0.08, green: 1.0, blue: 0.58),
        Color(red: 0.06, green: 0.04, blue: 0.18)
    ]

    var body: some View {
        TimelineView(.periodic(from: .now, by: 0.055)) { timeline in
            Canvas { context, size in
                let tick = Int(timeline.date.timeIntervalSinceReferenceDate * 18)
                let cell = max(28.0, min(size.width, size.height) / 8.0)
                let columns = Int(ceil(size.width / cell)) + 2
                let rows = Int(ceil(size.height / cell)) + 2
                context.fill(Path(CGRect(origin: .zero, size: size)), with: .color(Color(red: 0.02, green: 0.01, blue: 0.08)))
                for row in -1..<rows {
                    for column in -1..<columns {
                        let rowSeed: Int = row * 73
                        let columnSeed: Int = column * 131
                        let timeSeed: Int = tick * 47
                        let combinedSeed: Int = rowSeed + columnSeed + timeSeed
                        let seed: Int = abs(combinedSeed % 997)
                        let color = colors[seed % colors.count]
                        let xPhase: Int = (seed / 7) + (tick * (row + 9))
                        let yPhase: Int = (seed / 11) + (tick * (column + 7))
                        let xStep: Int = (xPhase % 5) - 2
                        let yStep: Int = (yPhase % 5) - 2
                        let xJitter: CGFloat = CGFloat(xStep) * cell * CGFloat(0.28)
                        let yJitter: CGFloat = CGFloat(yStep) * cell * CGFloat(0.22)
                        let rect = CGRect(
                            x: CGFloat(column) * cell + xJitter,
                            y: CGFloat(row) * cell + yJitter,
                            width: cell * CGFloat(1 + seed % 3),
                            height: cell * CGFloat(1 + (seed / 3) % 2)
                        )
                        context.fill(Path(rect), with: .color(color.opacity(0.52 + Double(seed % 35) / 100.0)))
                    }
                }
            }
        }
        .overlay {
            LinearGradient(colors: [Color.white.opacity(0.10), .clear, Color.black.opacity(0.22)], startPoint: .topLeading, endPoint: .bottomTrailing)
                .blendMode(.screen)
        }
        .allowsHitTesting(false)
    }
}

struct PixelDiffusionMask: View {
    let progress: Double

    var body: some View {
        Canvas { context, size in
            let cell = max(7.0, min(size.width, size.height) / 42.0)
            let columns = Int(ceil(size.width / cell))
            let rows = Int(ceil(size.height / cell))
            for row in 0..<rows {
                for column in 0..<columns {
                    let rowHash: Int = row * 92_821
                    let columnHash: Int = column * 68_917
                    let crossHash: Int = (row * column) * 17
                    let combinedHash: Int = rowHash + columnHash + crossHash
                    let hash: Int = abs(combinedHash % 1_000)
                    if Double(hash) / 1000.0 <= progress {
                        let rect = CGRect(x: CGFloat(column) * cell, y: CGFloat(row) * cell, width: cell + 0.75, height: cell + 0.75)
                        context.fill(Path(rect), with: .color(.white))
                    }
                }
            }
        }
    }
}

struct ActiveTransmissionTerminal: View {
    let snapshot: K1L0ActiveTransmissionSnapshot
    let walkSteps: Int
    let availableHeight: CGFloat
    let onStop: () -> Void
    let onFailureReset: () -> Void
    let onNewTransmission: () -> Void
    // Fullscreen transmitter mode: render the exact same fullscreen chain
    // player used everywhere else (settings gear, camera-roll save, tattered
    // frame) and overlay only the pencil (tweak) and END controls.
    var fullscreenPlayer: Bool = false
    var onClose: () -> Void = {}
    @ObservedObject private var keyboard = K1L0KeyboardObserver.shared
    @State private var showingEndConfirmation = false
    @State private var showingSignalFailure = false
    @State private var showingTweakPanel = false
    @State private var tweakStatus = ""
    @State private var tweakImageUrl = ""
    @State private var tweakPhotoPrompt = ""
    @State private var tweakVideoPrompt = ""
    @State private var tweakMusicPrompt = ""
    @State private var tweakLoadedJobId = ""
    @State private var textTransform = TransmissionTextTransformStore.load()
    @State private var outgoingWalkBaseline: Int?
    @StateObject private var chainObserver = K1L0ActiveChainObserver()
    @AppStorage("k1lo_native_transmissionFizzyEdges") private var transmissionFizzyEdges = false
    @AppStorage("k1lo_native_transmissionFX") private var transmissionFXEnabled = true
    @AppStorage("k1lo_native_transmissionFXIntensity") private var transmissionFXIntensity = 0.5

    private var saveOverlayText: String {
        snapshot.responsePlot.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private var outgoingSignalStrength: Double {
        guard let baseline = outgoingWalkBaseline else { return 0 }
        return min(1, max(0, Double(walkSteps - baseline) / 200.0))
    }

    var body: some View {
        Group {
            if fullscreenPlayer {
                fullscreenPlayerBody
            } else {
                panelBody
            }
        }
        .alert("End transmission?", isPresented: $showingEndConfirmation) {
            Button("End", role: .destructive) {
                onStop()
            }
            Button("Keep Transmitting", role: .cancel) { }
        } message: {
            Text("This removes this transmission from the transmitter. It will not come back here.")
        }
        .alert("Couldn't Establish Signal", isPresented: $showingSignalFailure) {
            Button("Try Again") {
                onFailureReset()
            }
        } message: {
            Text("Please try again.")
        }
        .onAppear {
            let buildAge = Date().timeIntervalSince1970 - snapshot.startedAt
            showingSignalFailure = !snapshot.error.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                || (snapshot.videoUrl.isEmpty && buildAge >= 300)
            if showingTweakPanel {
                loadTweakDetails()
            }
            chainObserver.start(rootJobId: snapshot.jobId)
        }
        .onChange(of: snapshot.jobId) { _ in
            chainObserver.start(rootJobId: snapshot.jobId)
            if showingTweakPanel {
                loadTweakDetails(force: true)
            }
        }
        .onDisappear { chainObserver.stop() }
        .onChange(of: snapshot.error) { error in
            if !error.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                showingSignalFailure = true
            }
        }
        .onChange(of: transmissionFizzyEdges) { val in
            K1L0WeatherOverlayInstaller.setUnitySetting("transmissionFizzyEdges", val ? "1" : "0")
        }
        .onChange(of: transmissionFXEnabled) { val in
            K1L0WeatherOverlayInstaller.setUnitySetting("transmissionFX", val ? "1" : "0")
        }
        .onChange(of: transmissionFXIntensity) { val in
            K1L0WeatherOverlayInstaller.setUnitySetting("transmissionFXIntensity", String(format: "%.2f", val))
        }
    }

    // The live transmission wrapped as a result so the shared fullscreen chain
    // player renders it exactly like a received transmission — including the
    // respond composer when the deepest slide is someone else's response
    // (the same alternation rule the server enforces on /respond).
    private var livePlayerResult: K1L0TransmissionResult {
        let videoURL = URL(string: snapshot.videoUrl)
        let audioURL = snapshot.audioUrl.isEmpty ? nil : URL(string: snapshot.audioUrl)
        let imageURL = snapshot.imageUrl.isEmpty ? nil : URL(string: snapshot.imageUrl)
        let clip = K1L0TransmissionClip(
            videoURL: videoURL,
            imageURL: imageURL,
            audioURL: audioURL,
            responsePlot: snapshot.responsePlot,
            responseOptions: [],
            selectedResponse: "",
            sourceJobId: snapshot.jobId,
            sourceUserId: currentNativeUserId
        )
        var clips = chainObserver.clips.isEmpty ? [clip] : chainObserver.clips
        let ownKey = currentNativeUserId.lowercased()
        var canAnswer = false
        if var deepest = clips.last {
            let deepestOwner = deepest.sourceUserId.lowercased()
            canAnswer = !deepestOwner.isEmpty && !ownKey.isEmpty && deepestOwner != ownKey
            if canAnswer {
                deepest.allowsResponse = true
                clips[clips.count - 1] = deepest
            }
        }
        let first = clips.first ?? clip
        return K1L0TransmissionResult(
            status: "live",
            imageURL: first.imageURL,
            videoURL: first.videoURL,
            audioURL: first.audioURL,
            lyrics: "",
            responsePlot: snapshot.responsePlot,
            responseOptions: clips.last?.responseOptions ?? [],
            jobId: snapshot.jobId,
            clips: clips,
            allowsResponseOptions: canAnswer,
            allowsTextResponse: canAnswer
        )
    }

    private var fullscreenPlayerBody: some View {
        ZStack(alignment: .topLeading) {
            if snapshot.videoUrl.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                // Building: the SSTV pixel scan owns the whole screen, exactly
                // like the video player it hands off to.
                TransmissionBuildingArtwork(
                    imageUrl: snapshot.imageUrl,
                    photoPath: snapshot.photoPath,
                    signalStrength: outgoingSignalStrength
                )
                .ignoresSafeArea()
            } else {
            TransmissionResultPanel(
                result: livePlayerResult,
                onSelectOption: { option, photoPath in
                    K1L0OverlayDataModel.activeModel?.respondToTransmission(
                        livePlayerResult, option: option, photoPath: photoPath)
                },
                composerBottomObstruction: 72,
                onClose: onClose,
                onNewTransmission: onNewTransmission
            )
            // Rebuild the player when a tweak regenerates the video or music.
            .id("live-\(snapshot.jobId)-\(snapshot.videoUrl)-\(snapshot.audioUrl)-\(chainObserver.clips.map(\.sourceJobId).joined(separator: ":"))")
            }

            // END button removed — the transmission player has its own close
            // affordances (pull-to-dismiss + tap-outside) and the END overlay
            // was crowding the top-left of the video.

            if showingTweakPanel {
                TransmissionTweakPanel(
                    snapshot: snapshot,
                    imageUrl: tweakImageUrl.isEmpty ? snapshot.imageUrl : tweakImageUrl,
                    photoPrompt: $tweakPhotoPrompt,
                    videoPrompt: $tweakVideoPrompt,
                    musicPrompt: $tweakMusicPrompt,
                    status: tweakStatus,
                    onClose: { withAnimation(.easeInOut(duration: 0.18)) { showingTweakPanel = false } },
                    onRefresh: { loadTweakDetails(force: true) },
                    onRegenerateImage: { regenerate(endpoint: "regen-nb", promptKey: "nbPrompt", prompt: tweakPhotoPrompt) },
                    onRegenerateVideo: { regenerate(endpoint: "regen-video", promptKey: "wanPrompt", prompt: tweakVideoPrompt) },
                    onRegenerateMusic: { regenerate(endpoint: "regen-music", promptKey: "musicPrompt", prompt: tweakMusicPrompt) }
                )
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topTrailing)
                .transition(.opacity)
            }
        }
        .ignoresSafeArea(.keyboard, edges: .bottom)
    }

    private var panelBody: some View {
        ZStack(alignment: .topTrailing) {
            VStack(spacing: 8) {
                GeometryReader { proxy in
                    // Match the fullscreen chain player exactly: a full-width
                    // 9:16 transmission frame. Do not compress it to fit the
                    // tab panel; the surrounding ScrollView can accommodate it.
                    let height = proxy.size.width * 16 / 9
                    let width = proxy.size.width
                    HStack {
                        Spacer(minLength: 0)
                        ZStack {
                            if snapshot.videoUrl.isEmpty {
                                TransmissionBuildingArtwork(
                                    imageUrl: snapshot.imageUrl,
                                    photoPath: snapshot.photoPath,
                                    signalStrength: outgoingSignalStrength
                                )
                            } else {
                                InlineTransmissionVideoPlayer(urlString: snapshot.videoUrl, audioUrlString: snapshot.audioUrl.isEmpty ? nil : snapshot.audioUrl)
                                    .allowsHitTesting(false)
                                    .mask(TatteredEdgeMaskCanvas())
                            }
                            if !snapshot.responsePlot.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                                DraggableTransmissionTextOverlay(
                                    text: snapshot.responsePlot,
                                    transform: $textTransform,
                                    canvasSize: CGSize(width: width, height: height),
                                    allowEditing: true,
                                    useExternalTypewriter: false,
                                    // Hug the frame bottom.
                                    bottomInset: 18
                                )
                            }

                        }
                        .frame(width: width, height: height)
                        .background(Color.black.opacity(0.86))
                        .clipped()
                        Spacer(minLength: 0)
                    }
                }
                .frame(height: k1l0DeviceScreenSize().width * 16 / 9)

                HStack(spacing: 10) {
                    transmitterToolButton(label: nil, systemImage: "slider.horizontal.3") {
                        withAnimation(.easeInOut(duration: 0.18)) {
                            showingTweakPanel.toggle()
                        }
                        if showingTweakPanel {
                            loadTweakDetails()
                        }
                    }
                    let isUnderway = snapshot.active && snapshot.videoUrl.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                    Button(action: onNewTransmission) {
                        Text(isUnderway ? "Creating Transmission" : "NEW TRANSMISSION")
                            .font(.system(size: 15, weight: .black, design: .rounded))
                            .foregroundStyle(.black.opacity(0.88))
                            .frame(maxWidth: .infinity, minHeight: 44)
                            .background {
                                Group {
                                    if isUnderway {
                                        SweepingGreenBackground()
                                    } else {
                                        Color.green
                                    }
                                }
                                .clipShape(Capsule())
                            }
                            .overlay(Capsule().stroke(Color.white.opacity(0.48), lineWidth: 1.2))
                    }
                    .buttonStyle(.plain)
#if canImport(UIKit)
                    if !snapshot.videoUrl.isEmpty {
                        CameraRollSaveButton(
                            videoUrlString: snapshot.videoUrl,
                            audioUrlString: snapshot.audioUrl.isEmpty ? nil : snapshot.audioUrl,
                            overlayText: saveOverlayText,
                            overlayTransform: textTransform
                        )
                    }
#endif
                }
                .padding(.horizontal, 18)
                .frame(height: 40)

                if !snapshot.error.isEmpty {
                    Text(snapshot.error)
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(.red)
                        .textSelection(.enabled)
                        .padding(.horizontal, 18)
                }
            }
            .frame(maxWidth: .infinity, maxHeight: availableHeight, alignment: .top)

            if showingTweakPanel {
                TransmissionTweakPanel(
                    snapshot: snapshot,
                    imageUrl: tweakImageUrl.isEmpty ? snapshot.imageUrl : tweakImageUrl,
                    photoPrompt: $tweakPhotoPrompt,
                    videoPrompt: $tweakVideoPrompt,
                    musicPrompt: $tweakMusicPrompt,
                    status: tweakStatus,
                    onClose: { withAnimation(.easeInOut(duration: 0.18)) { showingTweakPanel = false } },
                    onRefresh: { loadTweakDetails(force: true) },
                    onRegenerateImage: { regenerate(endpoint: "regen-nb", promptKey: "nbPrompt", prompt: tweakPhotoPrompt) },
                    onRegenerateVideo: { regenerate(endpoint: "regen-video", promptKey: "wanPrompt", prompt: tweakVideoPrompt) },
                    onRegenerateMusic: { regenerate(endpoint: "regen-music", promptKey: "musicPrompt", prompt: tweakMusicPrompt) }
                )
                .transition(.opacity)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: availableHeight, alignment: .top)
        .ignoresSafeArea(.keyboard, edges: .bottom)
        .onAppear {
            if outgoingWalkBaseline == nil { outgoingWalkBaseline = walkSteps }
        }
    }

    private func transmitterToolButton(label: String?, systemImage: String?, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Group {
                if let systemImage {
                    Image(systemName: systemImage)
                        .font(.system(size: 15, weight: .black))
                } else {
                    Text(label ?? "")
                        .font(.system(size: 16, weight: .black, design: .monospaced))
                }
            }
            .foregroundStyle(.white)
            .frame(width: 40, height: 38)
            .background(Color.black.opacity(0.52))
            .overlay(Rectangle().stroke(Color.white.opacity(0.34), lineWidth: 1))
        }
        .buttonStyle(.plain)
    }

    private var currentNativeUserId: String {
        let defaults = UserDefaults.standard
        for key in ["FirebaseUserId", "K1L0UserId", "DeviceID", "deviceID"] {
            let value = defaults.string(forKey: key) ?? ""
            let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
            if !trimmed.isEmpty { return trimmed }
        }
        return "anon"
    }

    private var apiCandidates: [String] {
        [
            "https://api-tunnel.kilo.gallery",
            "http://192.168.40.34:3000",
            "http://fred.local:3000",
            "https://api.kilomeme.com"
        ]
    }

    private func loadTweakDetails(force: Bool = false, apiIndex: Int = 0) {
        let jobId = snapshot.jobId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !jobId.isEmpty else {
            tweakStatus = "waiting for job id."
            return
        }
        if !force, tweakLoadedJobId == jobId, !tweakPhotoPrompt.isEmpty {
            return
        }
        guard apiIndex < apiCandidates.count else {
            tweakStatus = "prompt fetch failed."
            return
        }
        let userId = currentNativeUserId
        guard let encodedUser = userId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed),
              let url = URL(string: "\(apiCandidates[apiIndex])/api/k1l0/v2/transmit/\(jobId)?userId=\(encodedUser)") else {
            loadTweakDetails(force: force, apiIndex: apiIndex + 1)
            return
        }
        tweakStatus = "loading prompts..."
        URLSession.shared.dataTask(with: URLRequest(url: url, timeoutInterval: 12)) { data, response, _ in
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            guard (200...299).contains(code),
                  let data,
                  let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  (root["ok"] as? Bool) == true
            else {
                DispatchQueue.main.async {
                    loadTweakDetails(force: force, apiIndex: apiIndex + 1)
                }
                return
            }
            let plan = root["plan"] as? [String: Any] ?? [:]
            let music = plan["music"] as? [String: Any] ?? [:]
            let audit = K1L0OverlayDataModel.transmissionAudit(from: root)
            DispatchQueue.main.async {
                tweakLoadedJobId = jobId
                tweakImageUrl = (root["stillUrl"] as? String) ?? (root["nbUrl"] as? String) ?? snapshot.imageUrl
                tweakPhotoPrompt = (plan["nb_prompt"] as? String) ?? tweakPhotoPrompt
                tweakVideoPrompt = (plan["wan_prompt"] as? String) ?? tweakVideoPrompt
                tweakMusicPrompt = (music["prompt"] as? String) ?? tweakMusicPrompt
                K1L0ActiveTransmissionStore.shared.applyAudit(
                    inputImageUrl: audit.inputImageUrl,
                    locationSummary: audit.location,
                    weatherSummary: audit.weather,
                    photoPrompt: audit.photoPrompt,
                    videoPrompt: audit.videoPrompt,
                    musicPrompt: audit.musicPrompt,
                    lyrics: audit.lyrics,
                    createdAt: k1l0NumericTimestamp(root["createdAt"]) > 0 ? k1l0NumericTimestamp(root["createdAt"]) : k1l0NumericTimestamp(root["updatedAt"])
                )
                tweakStatus = "prompts loaded."
            }
        }.resume()
    }

    private func regenerate(endpoint: String, promptKey: String, prompt: String, apiIndex: Int = 0) {
        let jobId = snapshot.jobId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !jobId.isEmpty else {
            tweakStatus = "waiting for job id."
            return
        }
        guard apiIndex < apiCandidates.count else {
            tweakStatus = "regen request failed."
            return
        }
        guard let url = URL(string: "\(apiCandidates[apiIndex])/api/k1l0/v2/\(endpoint)") else {
            regenerate(endpoint: endpoint, promptKey: promptKey, prompt: prompt, apiIndex: apiIndex + 1)
            return
        }
        var request = URLRequest(url: url, timeoutInterval: 12)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: [
            "userId": currentNativeUserId,
            "jobId": jobId,
            promptKey: prompt.trimmingCharacters(in: .whitespacesAndNewlines)
        ])
        tweakStatus = "\(endpoint) queued..."
        URLSession.shared.dataTask(with: request) { data, response, _ in
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            guard (200...299).contains(code),
                  let data,
                  let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  (root["ok"] as? Bool) == true
            else {
                DispatchQueue.main.async {
                    regenerate(endpoint: endpoint, promptKey: promptKey, prompt: prompt, apiIndex: apiIndex + 1)
                }
                return
            }
            DispatchQueue.main.async {
                tweakStatus = "\(endpoint) running."
                pollRegeneratedJob()
            }
        }.resume()
    }

    private func pollRegeneratedJob(attempt: Int = 0) {
        guard attempt < 90 else {
            tweakStatus = "regen timed out."
            return
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 5) {
            loadTweakDetails(force: true)
            let jobId = snapshot.jobId.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !jobId.isEmpty,
                  let encodedUser = currentNativeUserId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed),
                  let url = URL(string: "\(apiCandidates[0])/api/k1l0/v2/transmit/\(jobId)?userId=\(encodedUser)") else { return }
            URLSession.shared.dataTask(with: URLRequest(url: url, timeoutInterval: 12)) { data, _, _ in
                guard let data,
                      let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
                else {
                    DispatchQueue.main.async { pollRegeneratedJob(attempt: attempt + 1) }
                    return
                }
                let status = (root["status"] as? String) ?? ""
                if status == "ready" || status == "complete" {
                    let audit = K1L0OverlayDataModel.transmissionAudit(from: root)
                    let finalUrl = (root["finalUrl"] as? String) ?? ""
                    let rawVideoUrl = (root["rawVideoUrl"] as? String) ?? (root["videoUrl"] as? String) ?? finalUrl
                    let imageUrl = (root["stillUrl"] as? String) ?? (root["nbUrl"] as? String) ?? ""
                    let audioUrl = (root["audioUrl"] as? String) ?? ""
                    let responsePlot = (root["responsePlot"] as? String) ?? snapshot.responsePlot
                    let responseOptions = (root["responseOptions"] as? [String]) ?? snapshot.responseOptions
                    let payload: [String: Any] = [
                        "jobId": jobId,
                        "status": status,
                        "imageUrl": imageUrl,
                        "videoUrl": rawVideoUrl,
                        "audioUrl": audioUrl,
                        "lyrics": audit.lyrics,
                        "createdAt": k1l0NumericTimestamp(root["createdAt"]) > 0 ? k1l0NumericTimestamp(root["createdAt"]) : k1l0NumericTimestamp(root["updatedAt"]),
                        "responsePlot": responsePlot,
                        "responseOptions": responseOptions
                    ]
                    if let payloadData = try? JSONSerialization.data(withJSONObject: payload),
                       let json = String(data: payloadData, encoding: .utf8) {
                        DispatchQueue.main.async {
                            K1L0TransmissionResultStore.shared.handle(json)
                            K1L0ActiveTransmissionStore.shared.applyAudit(
                                inputImageUrl: audit.inputImageUrl,
                                locationSummary: audit.location,
                                weatherSummary: audit.weather,
                                photoPrompt: audit.photoPrompt,
                                videoPrompt: audit.videoPrompt,
                                musicPrompt: audit.musicPrompt,
                                lyrics: audit.lyrics,
                                createdAt: k1l0NumericTimestamp(root["createdAt"]) > 0 ? k1l0NumericTimestamp(root["createdAt"]) : k1l0NumericTimestamp(root["updatedAt"])
                            )
                            tweakStatus = "regen ready."
                            loadTweakDetails(force: true)
                        }
                    }
                    return
                }
                if status == "error" {
                    let error = (root["error"] as? String) ?? "regen failed"
                    DispatchQueue.main.async { tweakStatus = error }
                    return
                }
                DispatchQueue.main.async {
                    tweakStatus = status.isEmpty ? "regen running..." : status
                    pollRegeneratedJob(attempt: attempt + 1)
                }
            }.resume()
        }
    }
}

struct TransmissionAuditPanel: View {
    let snapshot: K1L0ActiveTransmissionSnapshot

    private var rows: [(String, String)] {
        [
            ("CREATED", k1l0ReadableDateTime(snapshot.createdAt)),
            ("WEATHER", snapshot.weatherSummary),
            ("LOCATION", snapshot.locationSummary),
            ("USER PROMPT", snapshot.message),
            ("INPUT IMAGE", snapshot.inputImageUrl.isEmpty ? snapshot.photoPath : snapshot.inputImageUrl),
            ("NANO BANANA IMAGE PROMPT", snapshot.photoPrompt),
            ("VIDEO PROMPT", snapshot.videoPrompt),
            ("MUSIC PROMPT", snapshot.musicPrompt),
            ("LYRICS", snapshot.lyrics)
        ]
        .map { ($0.0, $0.1.trimmingCharacters(in: .whitespacesAndNewlines)) }
        .filter { !$0.1.isEmpty }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            HStack(spacing: 8) {
                Image(systemName: "info.circle.fill")
                    .font(.system(size: 11, weight: .black))
                Text("Used variables")
                    .font(.system(size: 11, weight: .black, design: .monospaced))
                Spacer()
                Text(snapshot.status.uppercased())
                    .font(.system(size: 10, weight: .bold, design: .monospaced))
                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.85))
            }
            .foregroundStyle(.white.opacity(0.82))

            VStack(alignment: .leading, spacing: 6) {
                ForEach(Array(rows.enumerated()), id: \.offset) { _, row in
                    VStack(alignment: .leading, spacing: 2) {
                        Text(row.0)
                            .font(.system(size: 9, weight: .black, design: .monospaced))
                            .foregroundStyle(.white.opacity(0.46))
                        Text(row.1)
                            .font(.system(size: 10, weight: .semibold, design: .monospaced))
                            .foregroundStyle(.white.opacity(0.88))
                            .lineLimit(row.0 == "LYRICS" ? 10 : 3)
                            .textSelection(.enabled)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    }
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .frame(maxWidth: .infinity)
    }
}

struct TransmissionTweakPanel: View {
    let snapshot: K1L0ActiveTransmissionSnapshot
    let imageUrl: String
    @Binding var photoPrompt: String
    @Binding var videoPrompt: String
    @Binding var musicPrompt: String
    let status: String
    let onClose: () -> Void
    let onRefresh: () -> Void
    let onRegenerateImage: () -> Void
    let onRegenerateVideo: () -> Void
    let onRegenerateMusic: () -> Void
    @AppStorage("k1lo_native_transmissionFizzyEdges") private var transmissionFizzyEdges = false
    @AppStorage("k1lo_native_transmissionFX") private var transmissionFXEnabled = true
    @AppStorage("k1lo_native_transmissionFXIntensity") private var transmissionFXIntensity = 0.5

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text("Transmission Lab")
                    .font(.system(size: 16, weight: .black, design: .rounded))
                    .foregroundStyle(.white)
                Spacer()
                Button(action: onRefresh) {
                    Image(systemName: "arrow.clockwise")
                        .font(.system(size: 13, weight: .black))
                        .foregroundStyle(.white)
                        .frame(width: 32, height: 32)
                }
                .buttonStyle(.plain)
                Button(action: onClose) {
                    Image(systemName: "xmark")
                        .font(.system(size: 13, weight: .black))
                        .foregroundStyle(.white)
                        .frame(width: 32, height: 32)
                }
                .buttonStyle(.plain)
            }

            ScrollView(.vertical, showsIndicators: true) {
                VStack(alignment: .leading, spacing: 10) {
                    TransmissionAuditPanel(snapshot: snapshot)
                        .padding(10)
                        .background(Color.white.opacity(0.045))
                        .overlay(Rectangle().stroke(Color.white.opacity(0.12), lineWidth: 1))

                    if !imageUrl.isEmpty, let url = URL(string: imageUrl) {
                        AsyncImage(url: url) { phase in
                            switch phase {
                            case .success(let image):
                                image.resizable().scaledToFit()
                            default:
                                Rectangle().fill(Color.white.opacity(0.08))
                            }
                        }
                        .frame(maxWidth: .infinity)
                        .frame(height: 160)
                        .clipped()
                        .overlay(Rectangle().stroke(Color.white.opacity(0.18), lineWidth: 1))
                    }

                    promptEditor("PHOTO PROMPT", text: $photoPrompt, actionTitle: "REGENERATE IMAGE", action: onRegenerateImage)
                    promptEditor("VIDEO PROMPT", text: $videoPrompt, actionTitle: "REGENERATE VIDEO", action: onRegenerateVideo)
                    promptEditor("MUSIC PROMPT", text: $musicPrompt, actionTitle: "REGENERATE MUSIC", action: onRegenerateMusic)

                    VStack(alignment: .leading, spacing: 6) {
                        Text("PLAYBACK FX")
                            .font(.system(size: 11, weight: .black, design: .monospaced))
                            .foregroundStyle(.white.opacity(0.72))
                        
                        VStack(spacing: 8) {
                            HStack {
                                Toggle("Fizzy Edges", isOn: $transmissionFizzyEdges)
                                    .font(.system(size: 12, weight: .semibold))
                                    .foregroundStyle(.white)
                                    .tint(Color(red: 0.66, green: 1.0, blue: 0.76))
                            }
                            
                            HStack {
                                Toggle("Glitch FX", isOn: $transmissionFXEnabled)
                                    .font(.system(size: 12, weight: .semibold))
                                    .foregroundStyle(.white)
                                    .tint(Color(red: 0.66, green: 1.0, blue: 0.76))
                            }

                            VStack(alignment: .leading, spacing: 2) {
                                HStack {
                                    Text("FX Intensity")
                                        .font(.system(size: 11, weight: .semibold))
                                        .foregroundStyle(.white)
                                    Spacer()
                                    Text(String(format: "%.2f", transmissionFXIntensity))
                                        .font(.system(size: 11, weight: .semibold, design: .monospaced))
                                        .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                                }
                                Slider(value: $transmissionFXIntensity, in: 0...1, step: 0.05)
                                    .tint(Color(red: 0.66, green: 1.0, blue: 0.76))
                            }
                        }
                        .padding(10)
                        .background(Color.white.opacity(0.045))
                        .overlay(Rectangle().stroke(Color.white.opacity(0.12), lineWidth: 1))
                    }

                    Text(status)
                        .font(.system(size: 12, weight: .semibold))
                        .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
        }
        .padding(14)
        .frame(maxWidth: 420)
        .frame(maxHeight: 620)
        .background(Color.black.opacity(0.86), in: RoundedRectangle(cornerRadius: 18, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).stroke(Color.white.opacity(0.22), lineWidth: 1))
        .padding(18)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topTrailing)
    }

    private func promptEditor(_ title: String, text: Binding<String>, actionTitle: String, action: @escaping () -> Void) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(title)
                .font(.system(size: 11, weight: .black, design: .monospaced))
                .foregroundStyle(.white.opacity(0.72))
            TextEditor(text: text)
                .font(.system(size: 11, weight: .semibold, design: .monospaced))
                .foregroundStyle(.white)
                .tint(.white)
                .scrollContentBackgroundCompatHidden()
                .colorScheme(.dark)
                .transmitterKeyboardDoneToolbar()
                .frame(minHeight: 96)
                .padding(6)
                .background(Color.white.opacity(0.06))
                .overlay(Rectangle().stroke(Color.white.opacity(0.16), lineWidth: 1))
            Button(action: action) {
                Text("[ \(actionTitle) ]")
                    .font(.system(size: 11, weight: .black, design: .monospaced))
                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                    .frame(maxWidth: .infinity, minHeight: 34)
                    .overlay(Rectangle().stroke(Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.54), lineWidth: 1))
            }
            .buttonStyle(.plain)
        }
    }
}

func k1l0PersistTransmissionPlot(jobId: String, userId: String, responsePlot: String, apiIndex: Int = 0) {
    let candidates = [
        "https://api-tunnel.kilo.gallery",
        "http://192.168.40.34:3000",
        "http://fred.local:3000",
        "https://api.kilomeme.com"
    ]
    guard apiIndex < candidates.count,
          let url = URL(string: "\(candidates[apiIndex])/api/k1l0/v2/transmit/\(jobId)/plot") else { return }
    var request = URLRequest(url: url, timeoutInterval: 12)
    request.httpMethod = "PATCH"
    request.setValue("application/json", forHTTPHeaderField: "Content-Type")
    request.httpBody = try? JSONSerialization.data(withJSONObject: [
        "userId": userId,
        "responsePlot": responsePlot.trimmingCharacters(in: .whitespacesAndNewlines)
    ])
    URLSession.shared.dataTask(with: request) { data, response, _ in
        let code = (response as? HTTPURLResponse)?.statusCode ?? 0
        guard (200...299).contains(code),
              let data,
              let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              (root["ok"] as? Bool) == true else {
            k1l0PersistTransmissionPlot(jobId: jobId, userId: userId, responsePlot: responsePlot, apiIndex: apiIndex + 1)
            return
        }
    }.resume()
}

struct TransmissionPlotRibbon: View {
    let text: String
    var allowEditing: Bool = true
    var animateText: Bool = true
    @State private var visibleCharacterCount = 0
    @State private var editing = false
    @State private var draft = ""

    private var cleanText: String {
        text.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private var visibleText: String {
        animateText ? String(cleanText.prefix(visibleCharacterCount)) : cleanText
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            if editing {
                TextEditor(text: $draft)
                    .font(.system(size: 15, weight: .bold, design: .monospaced))
                    .foregroundStyle(.white)
                    .tint(.white)
                    .scrollContentBackgroundCompatHidden()
                    .colorScheme(.dark)
                    .frame(minHeight: 74, maxHeight: 104)
                    .background(Color.black.opacity(0.82))
                    .overlay(Rectangle().stroke(Color.white.opacity(0.16), lineWidth: 1))
            } else {
                Text(visibleText)
                    .font(.system(size: 15, weight: .black, design: .monospaced))
                    .foregroundStyle(.white)
                    .lineLimit(5)
                    .minimumScaleFactor(0.62)
                    .multilineTextAlignment(.center)
                    .frame(maxWidth: .infinity, maxHeight: 96, alignment: .bottom)
            }
        }
        .frame(maxWidth: .infinity, alignment: .center)
        // Corner-overlaid edit button so it doesn't steal text width.
        .overlay(alignment: .topTrailing) {
            if allowEditing {
                Button {
                    if editing {
                        saveEditedPlot()
                    } else {
                        draft = cleanText
                    }
                    editing.toggle()
                } label: {
                    Image(systemName: editing ? "checkmark" : "pencil")
                        .font(.system(size: 12, weight: .black))
                        .foregroundStyle(.white)
                        .frame(width: 28, height: 28)
                        .background(Color.white.opacity(0.12))
                        .overlay(Rectangle().stroke(Color.white.opacity(0.28), lineWidth: 1))
                }
                .buttonStyle(.plain)
            }
        }
        .padding(.horizontal, 6)
        .padding(.vertical, 6)
        .frame(maxHeight: 118, alignment: .bottom)
        .clipped()
        .onAppear {
            draft = cleanText
            restartTypewriter()
        }
            .onChange(of: cleanText) { _ in
                if !editing {
                    draft = cleanText
                }
                restartTypewriter()
            }
    }

    private func restartTypewriter() {
        if !animateText {
            visibleCharacterCount = cleanText.count
            return
        }
        visibleCharacterCount = 0
        let maxCount = cleanText.count
        guard maxCount > 0 else { return }
        for index in 1...maxCount {
            DispatchQueue.main.asyncAfter(deadline: .now() + Double(index) * 0.018) {
                visibleCharacterCount = min(index, maxCount)
            }
        }
    }

    private func saveEditedPlot() {
        K1L0ActiveTransmissionStore.shared.updateResponsePlot(draft)
        let snapshot = K1L0ActiveTransmissionStore.shared.snapshot
        let jobId = snapshot.jobId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !jobId.isEmpty,
              let userId = currentNativeUserId()?.trimmingCharacters(in: .whitespacesAndNewlines),
              !userId.isEmpty else { return }
        persistEditedPlot(jobId: jobId, userId: userId, responsePlot: draft)
    }

    private func persistEditedPlot(jobId: String, userId: String, responsePlot: String, apiIndex: Int = 0) {
        let candidates = [
            "https://api-tunnel.kilo.gallery",
            "http://192.168.40.34:3000",
            "http://fred.local:3000",
            "https://api.kilomeme.com"
        ]
        guard apiIndex < candidates.count else { return }
        guard let url = URL(string: "\(candidates[apiIndex])/api/k1l0/v2/transmit/\(jobId)/plot") else {
            persistEditedPlot(jobId: jobId, userId: userId, responsePlot: responsePlot, apiIndex: apiIndex + 1)
            return
        }
        var request = URLRequest(url: url, timeoutInterval: 12)
        request.httpMethod = "PATCH"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: [
            "userId": userId,
            "responsePlot": responsePlot.trimmingCharacters(in: .whitespacesAndNewlines)
        ])
        URLSession.shared.dataTask(with: request) { data, response, _ in
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            guard (200...299).contains(code),
                  let data,
                  let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  (root["ok"] as? Bool) == true
            else {
                persistEditedPlot(jobId: jobId, userId: userId, responsePlot: responsePlot, apiIndex: apiIndex + 1)
                return
            }
        }.resume()
    }

    private func currentNativeUserId() -> String? {
        let defaults = UserDefaults.standard
        for key in ["FirebaseUserId", "K1L0UserId", "DeviceID", "deviceID"] {
            let value = defaults.string(forKey: key) ?? ""
            let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
            if !trimmed.isEmpty { return trimmed }
        }
        return nil
    }
}

struct WarblyStaticView: View {
    var body: some View {
        TimelineView(.periodic(from: .now, by: 0.25)) { timeline in
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

struct PixelBreakupView: View {
    let progress: Double

    var body: some View {
        TimelineView(.periodic(from: .now, by: 0.18)) { timeline in
            Canvas { context, size in
                let time = timeline.date.timeIntervalSinceReferenceDate
                let clamped = min(1, max(0, progress))
                let block = max(8, 54 - clamped * 42)
                let columns = Int(ceil(size.width / block))
                let rows = Int(ceil(size.height / block))
                for row in 0..<rows {
                    for column in 0..<columns {
                        let seed = sin(Double(row * 97 + column * 53) + floor(time * 8.0) * 1.37)
                        let threshold = 0.18 + clamped * 0.62
                        if abs(seed) > threshold {
                            let alpha = (0.16 + 0.38 * abs(seed)) * (1.0 - clamped)
                            let drift = sin(time * 5.0 + Double(row) * 0.81) * block * 1.4 * (1.0 - clamped)
                            let rect = CGRect(
                                x: Double(column) * block + drift,
                                y: Double(row) * block,
                                width: block + 1,
                                height: block + 1
                            )
                            context.fill(Path(rect), with: .color(Color.black.opacity(alpha)))
                            if abs(seed) > threshold + 0.12 {
                                let lightRect = rect.offsetBy(dx: -drift * 0.35, dy: 0).insetBy(dx: block * 0.18, dy: block * 0.18)
                                context.fill(Path(lightRect), with: .color(Color.white.opacity(alpha * 0.42)))
                            }
                        }
                    }
                }
                for band in 0..<9 {
                    let y = size.height * (0.08 + 0.86 * abs(sin(time * 0.37 + Double(band) * 1.73)))
                    let height = max(4, block * (0.28 + 0.22 * abs(sin(time + Double(band)))))
                    let x = sin(time * (2.2 + Double(band) * 0.17) + Double(band)) * size.width * 0.24 * (1.0 - clamped)
                    let rect = CGRect(x: x, y: y, width: size.width, height: height)
                    context.fill(Path(rect), with: .color(Color.white.opacity(0.08 * (1.0 - clamped))))
                }
            }
        }
    }
}

struct SignalTuningWaveView: View {
    let progress: Double

    var body: some View {
        TimelineView(.periodic(from: .now, by: 0.18)) { timeline in
            Canvas { context, size in
                let time = timeline.date.timeIntervalSinceReferenceDate
                let clamped = min(1, max(0, progress))
                let unresolved = 1.0 - clamped
                let bandCount = max(4, Int(14 - clamped * 10))
                for index in 0..<bandCount {
                    let bandHeight = size.height / CGFloat(bandCount)
                    let y = CGFloat(index) * bandHeight
                    let wave = sin(time * 5.5 + Double(index) * 0.92)
                    let drift = CGFloat(wave) * size.width * CGFloat(0.24 * unresolved)
                    let alpha = 0.08 + 0.18 * unresolved * abs(wave)
                    let rect = CGRect(
                        x: drift,
                        y: y,
                        width: size.width,
                        height: max(1.0, bandHeight * CGFloat(0.72 + 0.18 * abs(wave)))
                    )
                    context.fill(Path(rect), with: .color(Color.white.opacity(alpha)))
                }

                for line in 0..<10 {
                    let y = size.height * CGFloat(Double(line) / 10.0)
                    let rect = CGRect(x: 0, y: y, width: size.width, height: 1)
                    context.fill(Path(rect), with: .color(Color.black.opacity(0.10 * unresolved)))
                }
            }
        }
        .blendMode(.overlay)
        .allowsHitTesting(false)
    }
}

// Semi-transparent "detuned signal" placeholder shown while a transmission is
// still building. Replaces the old flat-black WarblyStaticView square: instead
// of an opaque black hole it renders a luminous detuned signal, and — when an
// image URL is already available (the NanoBanana still generates before the
// video) — ghosts that image band-sheared, green-tinted, blurred and pixelated,
// so you can faintly see the incoming transmission resolving before it lands.
struct DetunedSignalPreviewView: View {
    var imageUrl: String = ""

    private var resolvedURL: URL? {
        let trimmed = imageUrl.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        return URL(string: trimmed)
    }

    var body: some View {
        ZStack {
            Color.black.opacity(0.45)

            if let url = resolvedURL {
                AsyncImage(url: url) { phase in
                    if case .success(let image) = phase {
                        DetunedGhostImage(image: image)
                    }
                }
            }

            // Coarse pixel breakup (most blocky at progress 0).
            PixelBreakupView(progress: 0)
                .opacity(0.85)
                .allowsHitTesting(false)

            // Horizontal detuned tuning bands.
            SignalTuningWaveView(progress: 0)

            // Reduced grain over the top.
            WarblyStaticView()
                .opacity(0.45)
                .allowsHitTesting(false)
        }
    }
}

// Renders the incoming image as a detuned ghost: sliced into horizontal bands
// that shear side-to-side over time (VHS tracking-skew), tinted green, blurred,
// and dimmed so it reads as a faint signal resolving rather than a clean frame.
struct DetunedGhostImage: View {
    let image: Image

    var body: some View {
        GeometryReader { proxy in
            let size = proxy.size
                TimelineView(.periodic(from: .now, by: 0.18)) { timeline in
                Canvas { context, _ in
                    let time = timeline.date.timeIntervalSinceReferenceDate
                    let bandCount = 26
                    let bandHeight = size.height / CGFloat(bandCount)
                    let maxShear = size.width * 0.06
                    for index in 0..<bandCount {
                        // Each band drifts on its own phase; faster bands jitter
                        // more to sell the "signal not yet locked" feel.
                        let phase = time * 6.0 + Double(index) * 0.5
                        let jitter = sin(phase) + 0.35 * sin(phase * 3.1)
                        let shear = CGFloat(jitter) * maxShear
                        let y = CGFloat(index) * bandHeight
                        context.drawLayer { layer in
                            layer.clip(to: Path(CGRect(x: 0, y: y, width: size.width, height: bandHeight + 1)))
                            layer.draw(image, in: CGRect(x: shear, y: 0, width: size.width, height: size.height))
                        }
                    }
                }
            }
            .frame(width: size.width, height: size.height)
        }
        .opacity(0.6)
        .colorMultiply(Color(red: 0.5, green: 0.95, blue: 0.62))
        .blur(radius: 1.6)
        .allowsHitTesting(false)
    }
}

// Mask canvas that carves the same tattered-edge silhouette into the video
// itself: white where the video is visible, transparent where it is torn away.
// Apply with .mask { TatteredEdgeMaskCanvas() } on the player view so the
// Unity sky shows through the ragged border instead of a dark overlay.
struct TatteredEdgeMaskCanvas: View {
    var maxDepth: CGFloat = 4
    var step: CGFloat = 2

    var body: some View {
        // When the Core Image tattered kernel is enabled, tearing is baked into
        // the video frame itself — keep this mask fully opaque so nothing is cut.
        if K1L0TransmissionFX.tatteredShaderActive {
            Color.white
        } else {
        TimelineView(.periodic(from: .now, by: 0.22)) { timeline in
            Canvas { context, size in
                let t = timeline.date.timeIntervalSinceReferenceDate
                let w = size.width
                let h = size.height

                func tear(_ p: CGFloat, _ seed: Double) -> CGFloat {
                    let dp = Double(p)
                    let shape = 0.5 + 0.5 * sin(dp * 0.12 + seed + t * 1.6)
                    let mid   = 0.5 + 0.5 * sin(dp * 0.39 - t * 3.1 + seed * 1.7)
                    let jit   = 0.5 + 0.5 * sin(dp * 1.85 + t * 9.5 + seed * 2.3)
                    let v = shape * 0.55 + mid * 0.3 + jit * 0.15
                    let rip = sin(dp * 0.06 + t * 0.7 + seed) > 0.87 ? 1.8 : 1.0
                    return maxDepth * CGFloat(v) * CGFloat(rip)
                }

                context.withCGContext { cgCtx in
                    // Fully opaque white = every video pixel visible by default.
                    cgCtx.setFillColor(CGColor(red: 1, green: 1, blue: 1, alpha: 1))
                    cgCtx.fill(CGRect(origin: .zero, size: CGSize(width: w, height: h)))

                    // Erase torn border strips to transparent.
                    cgCtx.setBlendMode(.clear)

                    var x: CGFloat = 0
                    while x < w { cgCtx.fill(CGRect(x: x, y: 0, width: step + 1, height: tear(x, 0.0))); x += step }
                    x = 0
                    while x < w { let d = tear(x, 5.5); cgCtx.fill(CGRect(x: x, y: h - d, width: step + 1, height: d)); x += step }
                    var y: CGFloat = 0
                    while y < h { cgCtx.fill(CGRect(x: 0, y: y, width: tear(y, 2.3), height: step + 1)); y += step }
                    y = 0
                    while y < h { let d = tear(y, 7.9); cgCtx.fill(CGRect(x: w - d, y: y, width: d, height: step + 1)); y += step }
                }
            }
            .drawingGroup()
        }
        }
    }
}

// Tattered, fizzy edge decay drawn over the transmission frame. Irregular
// opaque dark "bite" marks reach inward from all four edges so the visible
// boundary reads torn/frayed rather than a clean rectangle, with a faint
// neutral fizz along the frontier and scattered fizzy specks.
// Animated via TimelineView so the edges fizzle and creep over time.
struct FizzyTatteredEdgeOverlay: View {
    var maxDepth: CGFloat = 4
    var step: CGFloat = 2
    private let fizz = Color.white

    var body: some View {
        // Frontier fizz is baked into the video frame by the Core Image
        // tattered kernel; this Canvas overlay is only the disabled-mode fallback.
        Group {
            if K1L0TransmissionFX.tatteredShaderActive {
                EmptyView()
            } else {
        TimelineView(.periodic(from: .now, by: 0.22)) { timeline in
            Canvas { context, size in
                let t = timeline.date.timeIntervalSinceReferenceDate
                let w = size.width
                let h = size.height

                // Per-position tear depth: a slow ragged silhouette + mid
                // frequency + a fast jitter (the fizz). Occasionally deepens
                // for a rip. `seed` varies per edge so they don't move in lockstep.
                func tear(_ p: CGFloat, _ seed: Double) -> CGFloat {
                    let dp = Double(p)
                    let shape = 0.5 + 0.5 * sin(dp * 0.12 + seed + t * 1.6)
                    let mid = 0.5 + 0.5 * sin(dp * 0.39 - t * 3.1 + seed * 1.7)
                    let jit = 0.5 + 0.5 * sin(dp * 1.85 + t * 9.5 + seed * 2.3)
                    let v = shape * 0.55 + mid * 0.3 + jit * 0.15
                    let rip = sin(dp * 0.06 + t * 0.7 + seed) > 0.87 ? 1.8 : 1.0
                    return maxDepth * CGFloat(v) * CGFloat(rip)
                }

                let frontierAlpha = { (p: CGFloat, seed: Double) -> Double in
                    0.3 + 0.35 * abs(sin(Double(p) * 2.1 + t * 12.0 + seed))
                }

                // TOP: bite downward from y = 0
                var x: CGFloat = 0
                while x < w {
                    let d = tear(x, 0.0)
                    context.fill(Path(CGRect(x: x, y: 0, width: step + 1, height: d)), with: .color(.black.opacity(0.5)))
                    context.fill(Path(CGRect(x: x, y: max(0, d - 2), width: step + 1, height: 2.4)), with: .color(fizz.opacity(frontierAlpha(x, 0.0))))
                    x += step
                }
                // BOTTOM: bite upward from y = h
                x = 0
                while x < w {
                    let d = tear(x, 5.5)
                    context.fill(Path(CGRect(x: x, y: h - d, width: step + 1, height: d)), with: .color(.black.opacity(0.5)))
                    context.fill(Path(CGRect(x: x, y: h - d, width: step + 1, height: 2.4)), with: .color(fizz.opacity(frontierAlpha(x, 5.5))))
                    x += step
                }
                // LEFT: bite rightward from x = 0
                var y: CGFloat = 0
                while y < h {
                    let d = tear(y, 2.3)
                    context.fill(Path(CGRect(x: 0, y: y, width: d, height: step + 1)), with: .color(.black.opacity(0.5)))
                    context.fill(Path(CGRect(x: max(0, d - 2), y: y, width: 2.4, height: step + 1)), with: .color(fizz.opacity(frontierAlpha(y, 2.3))))
                    y += step
                }
                // RIGHT: bite leftward from x = w
                y = 0
                while y < h {
                    let d = tear(y, 7.9)
                    context.fill(Path(CGRect(x: w - d, y: y, width: d, height: step + 1)), with: .color(.black.opacity(0.5)))
                    context.fill(Path(CGRect(x: w - d, y: y, width: 2.4, height: step + 1)), with: .color(fizz.opacity(frontierAlpha(y, 7.9))))
                    y += step
                }

                // Scattered fizzy specks hugging the frontier.
                for i in 0..<70 {
                    let edge = i % 4
                    let n = sin(Double(i) * 12.9898 + t * 4.0)
                    let n2 = sin(Double(i) * 4.1414 - t * 6.0)
                    let along = CGFloat(abs(n))
                    let inset = CGFloat(abs(n2)) * maxDepth * 0.9
                    let px: CGFloat
                    let py: CGFloat
                    switch edge {
                    case 0: px = along * w; py = inset
                    case 1: px = along * w; py = h - inset
                    case 2: px = inset; py = along * h
                    default: px = w - inset; py = along * h
                    }
                    let s = 1 + CGFloat(abs(sin(Double(i) + t * 8.0))) * 1.6
                    context.fill(Path(CGRect(x: px, y: py, width: s, height: s)), with: .color(fizz.opacity(0.2 + 0.45 * abs(n2))))
                }
            }
        }
        }
        }
        .allowsHitTesting(false)
    }
}

struct TransmissionChainProgressBar: View {
    let total: Int
    let currentIndex: Int
    let currentProgress: Double

    var body: some View {
        HStack(spacing: 4) {
            ForEach(0..<max(total, 0), id: \.self) { index in
                GeometryReader { geometry in
                    ZStack(alignment: .leading) {
                        Capsule()
                            .fill(Color.white.opacity(0.24))
                        Capsule()
                            .fill(Color.white)
                            .frame(width: geometry.size.width * fillRatio(for: index))
                    }
                }
                .frame(height: 3)
            }
        }
        .frame(height: 8)
    }

    private func fillRatio(for index: Int) -> Double {
        if index < currentIndex { return 1 }
        if index == currentIndex { return min(1, max(0, currentProgress)) }
        return 0
    }
}

struct TransmissionChainTapZones: View {
    let clipCount: Int
    @Binding var currentIndex: Int
    @Binding var currentProgress: Double

    var body: some View {
        HStack(spacing: 0) {
            Color.clear
                .contentShape(Rectangle())
                .onTapGesture {
                    currentProgress = 0
                    currentIndex = max(0, currentIndex - 1)
                }
            Color.clear
                .contentShape(Rectangle())
                .onTapGesture {
                    currentProgress = 0
                    currentIndex = min(max(clipCount - 1, 0), currentIndex + 1)
                }
        }
        .padding(.top, 92)
        .padding(.bottom, 132)
    }
}

// Shared loop counter so the FX know which play-through the video is on.
// The AVVideoComposition clock restarts every loop, so the player bumps this
// externally when playback wraps.
final class K1L0TransmissionFXLoopState {
    var loopCount = 0
}

// Client-side transmission FX — Swift port of the old server-side ffmpeg
// composite (mirrors Unity's TransmissionFXScheduler): beat-synced cut
// boundaries, random crop/zoom closeups, treatment cycling, flash inserts,
// animated grain. Applied as a Core Image AVVideoComposition so it renders
// on the bare AVPlayerLayer (SwiftUI layer shaders can't touch UIKit-backed
// video layers). Effects run immediately, including the first inbound
// appearance, then re-roll on repeats at a slower, music-paced cut cadence.
enum K1L0TransmissionFX {
    struct Treatment {
        var chromaR = CGVector.zero
        var chromaB = CGVector.zero
        var invert = false
        var saturation: Double = 1
        var contrast: Double = 1
        var brightness: Double = 0
        var posterize: Double = 0
        var blur: Double = 0
        var noise: Double = 0
        var vignette: Double = 0
    }

    struct Cut {
        let start: Double
        let crop: CGRect      // normalized, bottom-left origin
        let hflip: Bool
        let vflip: Bool
        let fx: Treatment
        let flashIn: Bool
        let flashWhite: Bool
    }

    static var enabled: Bool {
        UserDefaults.standard.object(forKey: "k1lo_native_transmissionFX") as? Bool ?? true
    }

    // 0 = barely-there grade, 1 = full detuned chaos. Scales every treatment
    // parameter and gates the harsh looks (negate, posterize, flashes).
    static var intensity: Double {
        let raw = UserDefaults.standard.object(forKey: "k1lo_native_transmissionFXIntensity") as? Double ?? 0.5
        return min(1, max(0, raw))
    }

    // Tattered edge as a Core Image kernel (GPU, in-pipeline) instead of the
    // CPU SwiftUI Canvas mask/overlay. Bakes the torn silhouette directly into
    // the composited video frame as the last composition step.
    // Toggle off (`k1lo_native_tatteredCIKernel`=0) to force the Canvas fallback.
    static var tatteredKernelEnabled: Bool {
        (UserDefaults.standard.object(forKey: "k1lo_native_tatteredCIKernel") as? Bool) ?? true
    }

    static let tatteredEdgeKernel: CIKernel? = {
        let source = """
        float tearFn(float q, float seed, float time, float maxDepth) {
            float shape = 0.5 + 0.5 * sin(q * 0.14 + seed + time * 1.8);
            float mid   = 0.5 + 0.5 * sin(q * 0.41 - time * 3.4 + seed * 1.7);
            float fast  = 0.5 + 0.5 * sin(q * 0.93 + time * 6.2 + seed * 0.9);
            float jit   = 0.5 + 0.5 * sin(q * 2.10 + time * 11.0 + seed * 2.3);
            float v = shape * 0.48 + mid * 0.26 + fast * 0.16 + jit * 0.10;
            float rip = (sin(q * 0.07 + time * 0.9 + seed) > 0.84) ? 2.1 : 1.0;
            return maxDepth * v * rip;
        }

        kernel vec4 tatteredEdge(__sample src, vec2 size, float time, float maxDepth, vec3 fizzColor) {
            vec2 p = destCoord();
            vec4 c = src;
            float w = size.x;
            float h = size.y;

            float tT = tearFn(p.x, 0.0, time, maxDepth);
            float tB = tearFn(p.x, 5.5, time, maxDepth);
            float tL = tearFn(p.y, 2.3, time, maxDepth);
            float tR = tearFn(p.y, 7.9, time, maxDepth);

            float torn = 0.0;
            if (p.y < tT) torn = 1.0;
            if ((h - p.y) < tB) torn = 1.0;
            if (p.x < tL) torn = 1.0;
            if ((w - p.x) < tR) torn = 1.0;

            float frontier = min(min(abs(p.y - tT), abs((h - p.y) - tB)),
                                 min(abs(p.x - tL), abs((w - p.x) - tR)));
            float band = max(1.0, maxDepth * 0.22);
            float glow = 1.0 - smoothstep(0.0, band, frontier);
            float pulse = 0.3 + 0.5 * abs(sin(p.x * 2.1 + time * 12.0)) * abs(sin(p.y * 1.7 - time * 9.0));

            vec2 cell = floor(p / 3.0);
            float sp = fract(sin(dot(cell, vec2(12.9898, 78.233))) * 43758.5453 + time * 4.0);
            float edgeDist = min(min(p.x, w - p.x), min(p.y, h - p.y));
            float sparkle = step(0.985, sp) * (1.0 - smoothstep(0.0, maxDepth, edgeDist));

            float glowAmt = clamp(glow * pulse + sparkle * 0.8, 0.0, 1.0);
            // Preserve the animated ragged edge and sparkle, but do not tint the
            // perimeter. The old signal-green mix read as a decorative border.
            vec3 outRgb = mix(c.rgb, vec3(1.0), glowAmt * 0.22);
            return vec4(outRgb, c.a * (1.0 - torn));
        }
        """
        return CIKernel(source: source)
    }()

    // Render the kernel once into a throwaway CIContext. If the source failed to
    // compile (or the GPU refuses it) this returns false and we transparently
    // fall back to the Canvas mask/overlay — so a bad kernel can NEVER take the
    // grain/chroma/cut FX (or the tattering itself) down with it.
    static let tatteredKernelValid: Bool = {
        guard let kernel = tatteredEdgeKernel else { return false }
        let testExtent = CGRect(x: 0, y: 0, width: 32, height: 32)
        let src = CIImage(color: CIColor(red: 1, green: 1, blue: 1, alpha: 1)).cropped(to: testExtent)
        guard let out = kernel.apply(extent: testExtent, roiCallback: { _, rect in rect }, arguments: [
            src,
            CIVector(x: 32, y: 32),
            NSNumber(value: Float(1.0)),
            NSNumber(value: Float(2.0)),
            CIColor(red: 1.0, green: 1.0, blue: 1.0),
        ]) else { return false }
        let ctx = CIContext(options: [.useSoftwareRenderer: false])
        return ctx.createCGImage(out, from: testExtent) != nil
    }()

    // Single resolved switch: shader only when enabled AND it actually renders.
    static var tatteredShaderActive: Bool {
        tatteredKernelEnabled && tatteredKernelValid
    }

    static func apply(to item: AVPlayerItem, loopState: K1L0TransmissionFXLoopState) {
#if os(iOS)
        // Live iOS playback is rendered by K1L0MetalVideoPlayer. Keep this
        // composition implementation for offline/export and non-iOS fallback.
        return
#else
        guard enabled else { return }
        let asset = item.asset
        asset.loadValuesAsynchronously(forKeys: ["duration", "tracks"]) {
            guard asset.statusOfValue(forKey: "duration", error: nil) == .loaded,
                  asset.statusOfValue(forKey: "tracks", error: nil) == .loaded,
                  asset.tracks(withMediaType: .video).first != nil else { return }
            let duration = asset.duration.seconds
            guard duration.isFinite, duration > 0.2 else { return }
            let schedule = rollSchedule(duration: duration)
            guard !schedule.isEmpty else { return }
            let composition = makeComposition(asset: asset, schedule: schedule, loopState: loopState)
            DispatchQueue.main.async { item.videoComposition = composition }
        }
#endif
    }

    private static func rollSchedule(duration: Double, bpm: Double = 72) -> [Cut] {
        let beat = 60.0 / max(bpm, 30.0)
        // Keep cuts music-paced and a little uneven so the loop feels detuned
        // instead of strobing.
        let level = intensity
        // Cut cadence follows intensity too: full chaos cuts at 5x beat,
        // low intensity stretches toward 8x (fewer, longer cuts).
        let paceMultiplier = 5.0 + 3.0 * (1 - level)
        let choices: [Double] = [0.5, 0.7, 1.0, 1.5, 2.0, 2.75, 3.5]
        let weights: [Double] = [0.7, 1.4, 3, 3, 2.2, 1.2, 0.6]
        var cuts: [Cut] = []
        var t = 0.0
        var safety = 64
        while t < duration && safety > 0 {
            safety -= 1
            let cutDuration = beat * paceMultiplier * weightedPick(choices, weights)
            cuts.append(makeCut(at: t, cutDuration: cutDuration, level: level))
            t += cutDuration
        }
        return cuts
    }

    private static func makeCut(at t: Double, cutDuration: Double, level: Double) -> Cut {
        // Face sits upper-middle in the portrait comps; the ffmpeg ROI was
        // top-origin (0.40, 0.34) — Core Image is bottom-origin, hence 0.66.
        let faceCenterX = 0.40, faceCenterY = 0.66
        // Closeups follow intensity: at full chaos 60% face zoom / 30% random
        // crop with deep punch-ins; at low intensity most cuts stay full-frame
        // and the zooms that do happen are gentler.
        let faceZoomChance = 0.25 + 0.35 * level
        let randomCropChance = 0.15 + 0.15 * level
        let crop: CGRect
        let r = Double.random(in: 0..<1)
        if r < faceZoomChance {
            let zoomMin = 0.35 + 0.25 * (1 - level)
            let zoomMax = min(0.92, 0.75 + 0.15 * (1 - level))
            let zoom = Double.random(in: zoomMin...zoomMax)
            crop = clampCrop(x: faceCenterX - zoom * 0.5, y: faceCenterY - zoom * 0.5, w: zoom, h: zoom)
        } else if r < faceZoomChance + randomCropChance {
            let zoomMin = 0.5 + 0.2 * (1 - level)
            let zoomMax = min(0.95, 0.85 + 0.1 * (1 - level))
            let zoom = Double.random(in: zoomMin...zoomMax)
            crop = clampCrop(x: Double.random(in: 0...(1 - zoom)), y: Double.random(in: 0...(1 - zoom)), w: zoom, h: zoom)
        } else {
            crop = CGRect(x: 0, y: 0, width: 1, height: 1)
        }
        return Cut(
            start: t,
            crop: crop,
            hflip: Double.random(in: 0..<1) < 0.06 * (0.5 + level),
            vflip: Double.random(in: 0..<1) < 0.015 * level,
            fx: randomTreatment(level: level, cutDuration: cutDuration),
            flashIn: Double.random(in: 0..<1) < 0.10 * level,
            flashWhite: true
        )
    }

    private static func randomTreatment(level: Double, cutDuration: Double) -> Treatment {
        var fx = Treatment()
        // Weighted looks: mild grades are common; harsh ones scale with the
        // FX Intensity slider. Negate is disabled outright — effects must
        // never darken a slide, and inversion turns bright scenes black.
        let lookWeights: [Double] = [
            2.2,                        // 0 clean
            1.8,                        // 1 chroma shift horizontal
            0.9 * level,                // 2 chroma shift diagonal extreme
            0,                          // 3 negate — permanently off (darkens)
            0.9,                        // 4 desat + contrast
            0.5 * level,                // 5 posterize
            0.8,                        // 6 blur
            0.9,                        // 7 noise heavy
            1.0                         // 8 wavy chroma + grain
        ]
        switch weightedIndex(lookWeights) {
        case 0: // clean
            break
        case 1: // chroma shift horizontal
            let px = 3 + 4 * level
            fx.chromaR = CGVector(dx: px, dy: 0); fx.chromaB = CGVector(dx: -px, dy: 0)
        case 2: // chroma shift diagonal extreme
            let px = 6 + 9 * level
            fx.chromaR = CGVector(dx: px, dy: 4 * level); fx.chromaB = CGVector(dx: -px, dy: -4 * level)
            fx.noise = 0.08 * max(0.4, level)
        case 3: // negate — gated above
            fx.invert = true
        case 4: // desat + contrast
            fx.saturation = 1 - 0.9 * level
            fx.contrast = 1 + 0.45 * level
            fx.brightness = 0.02 * level
        case 5: // posterize
            fx.posterize = 8 - 3 * level
            fx.contrast = 1 + 0.2 * level
        case 6: // blur
            fx.blur = 1 + 1.5 * level
            fx.contrast = 1 + 0.3 * level
        case 7: // noise heavy
            fx.noise = 0.08 + 0.10 * level
            fx.contrast = 1 + 0.3 * level
        default: // wavy displacement grade — approximated as chroma + grain
            fx.noise = 0.04 + 0.06 * level
            let px = 2 + 3 * level
            fx.chromaR = CGVector(dx: px, dy: 1); fx.chromaB = CGVector(dx: -px, dy: -1)
        }
        // No vignette: it darkens slide edges, and effects must never darken.
        return fx
    }

    private static func weightedIndex(_ weights: [Double]) -> Int {
        let total = weights.reduce(0, +)
        guard total > 0 else { return 0 }
        let pick = Double.random(in: 0..<total)
        var acc = 0.0
        for (index, weight) in weights.enumerated() {
            acc += weight
            if pick < acc { return index }
        }
        return weights.count - 1
    }

    private static func clampCrop(x: Double, y: Double, w: Double, h: Double) -> CGRect {
        CGRect(x: min(max(x, 0), 1 - w), y: min(max(y, 0), 1 - h), width: w, height: h)
    }

    private static func weightedPick(_ values: [Double], _ weights: [Double]) -> Double {
        let total = weights.reduce(0, +)
        let pick = Double.random(in: 0..<total)
        var acc = 0.0
        for (i, w) in weights.enumerated() {
            acc += w
            if pick <= acc { return values[i] }
        }
        return values[values.count - 1]
    }

    // Bake a full FX pass over an export asset (camera-roll saves). Rolls a
    // schedule spanning the whole export, so cuts land across loop boundaries.
    static func bakedComposition(for asset: AVAsset, durationSeconds: Double) -> AVVideoComposition? {
        guard enabled, durationSeconds > 0.2 else { return nil }
        let schedule = rollSchedule(duration: durationSeconds)
        guard !schedule.isEmpty else { return nil }
        return makeComposition(asset: asset, schedule: schedule, loopState: K1L0TransmissionFXLoopState())
    }

    private static let noiseImage: CIImage =
        CIFilter(name: "CIRandomGenerator")?.outputImage ?? CIImage.empty()

    private static func channel(_ source: CIImage, r: Double, g: Double, b: Double) -> CIImage {
        source.applyingFilter("CIColorMatrix", parameters: [
            "inputRVector": CIVector(x: CGFloat(r), y: 0, z: 0, w: 0),
            "inputGVector": CIVector(x: 0, y: CGFloat(g), z: 0, w: 0),
            "inputBVector": CIVector(x: 0, y: 0, z: CGFloat(b), w: 0),
            "inputAVector": CIVector(x: 0, y: 0, z: 0, w: 1)
        ])
    }

    private static func makeComposition(asset: AVAsset, schedule: [Cut], loopState: K1L0TransmissionFXLoopState) -> AVVideoComposition {
        AVMutableVideoComposition(asset: asset) { request in
            let t = request.compositionTime.seconds
            var cut = schedule[0]
            for c in schedule {
                if c.start <= t { cut = c } else { break }
            }

            var image = request.sourceImage
            let extent = image.extent

            // Crop/zoom closeup — scale the ROI back up to fill the frame.
            if cut.crop.width < 0.999 {
                let cropPx = CGRect(
                    x: extent.minX + cut.crop.minX * extent.width,
                    y: extent.minY + cut.crop.minY * extent.height,
                    width: cut.crop.width * extent.width,
                    height: cut.crop.height * extent.height
                )
                let sx = extent.width / cropPx.width
                let sy = extent.height / cropPx.height
                image = image
                    .cropped(to: cropPx)
                    .transformed(by: CGAffineTransform(translationX: -cropPx.minX, y: -cropPx.minY))
                    .transformed(by: CGAffineTransform(scaleX: sx, y: sy))
            }
            if cut.hflip {
                image = image.transformed(by: CGAffineTransform(a: -1, b: 0, c: 0, d: 1, tx: extent.width, ty: 0))
            }
            if cut.vflip {
                image = image.transformed(by: CGAffineTransform(a: 1, b: 0, c: 0, d: -1, tx: 0, ty: extent.height))
            }

            let fx = cut.fx
            if fx.chromaR != .zero || fx.chromaB != .zero {
                let red = channel(image, r: 1, g: 0, b: 0)
                    .transformed(by: CGAffineTransform(translationX: fx.chromaR.dx, y: fx.chromaR.dy))
                let green = channel(image, r: 0, g: 1, b: 0)
                let blue = channel(image, r: 0, g: 0, b: 1)
                    .transformed(by: CGAffineTransform(translationX: fx.chromaB.dx, y: fx.chromaB.dy))
                image = red.applyingFilter("CIAdditionCompositing", parameters: [kCIInputBackgroundImageKey: green])
                image = blue.applyingFilter("CIAdditionCompositing", parameters: [kCIInputBackgroundImageKey: image])
            }
            if fx.invert {
                image = image.applyingFilter("CIColorInvert")
            }
            if fx.saturation != 1 || fx.contrast != 1 || fx.brightness != 0 {
                image = image.applyingFilter("CIColorControls", parameters: [
                    kCIInputSaturationKey: fx.saturation,
                    kCIInputContrastKey: fx.contrast,
                    kCIInputBrightnessKey: fx.brightness
                ])
            }
            if fx.posterize > 0 {
                image = image.applyingFilter("CIColorPosterize", parameters: ["inputLevels": fx.posterize])
            }
            if fx.blur > 0 {
                image = image.clampedToExtent()
                    .applyingFilter("CIGaussianBlur", parameters: [kCIInputRadiusKey: fx.blur])
            }
            if fx.noise > 0 {
                // Animated grain: slide the random field each frame.
                let jitter = CGAffineTransform(
                    translationX: CGFloat((t * 973.0).truncatingRemainder(dividingBy: 512.0)),
                    y: CGFloat((t * 541.0).truncatingRemainder(dividingBy: 512.0))
                )
                let grain = noiseImage
                    .transformed(by: jitter)
                    .applyingFilter("CIColorMatrix", parameters: [
                        "inputRVector": CIVector(x: 0.7, y: 0, z: 0, w: 0),
                        "inputGVector": CIVector(x: 0.7, y: 0, z: 0, w: 0),
                        "inputBVector": CIVector(x: 0.7, y: 0, z: 0, w: 0),
                        "inputAVector": CIVector(x: 0, y: 0, z: 0, w: CGFloat(fx.noise))
                    ])
                    .cropped(to: extent)
                image = grain.applyingFilter("CISourceOverCompositing", parameters: [kCIInputBackgroundImageKey: image])
            }
            if fx.vignette > 0 {
                image = image.applyingFilter("CIVignette", parameters: [
                    kCIInputIntensityKey: fx.vignette * 2.0,
                    kCIInputRadiusKey: 1.6
                ])
            }

            // 1-frame flash at the cut boundary, decaying over 80ms.
            let flashAge = t - cut.start
            if cut.flashIn, flashAge >= 0, flashAge < 0.08 {
                let alpha = 1.0 - flashAge / 0.08
                let level: CGFloat = cut.flashWhite ? 1 : 0
                let flash = CIImage(color: CIColor(red: level, green: level, blue: level, alpha: CGFloat(alpha)))
                    .cropped(to: extent)
                image = flash.applyingFilter("CISourceOverCompositing", parameters: [kCIInputBackgroundImageKey: image])
            }

            // Tattered edge: bake the torn silhouette + neutral frontier fizz into
            // the frame on the GPU. Torn pixels get alpha 0 so Unity sky shows
            // through (AVPlayerLayer has a clear background). Skipped unless the
            // kernel both compiles and renders (tatteredShaderActive); otherwise
            // the SwiftUI Canvas mask/overlay handles it and FX stay intact.
            image = image.cropped(to: extent)
            if Self.tatteredShaderActive, let kernel = Self.tatteredEdgeKernel {
                let maxDepth = min(6.0, 0.012 * min(extent.width, extent.height))
                if let processed = kernel.apply(extent: extent, roiCallback: { _, rect in rect }, arguments: [
                    image,
                    CIVector(x: extent.width, y: extent.height),
                    NSNumber(value: Float(t)),
                    NSNumber(value: Float(maxDepth)),
                    CIColor(red: 1.0, green: 1.0, blue: 1.0),
                ]) {
                    image = processed
                }
            }

            request.finish(with: image, context: nil)
        }
    }
}

actor K1L0TransmissionMediaDiskCache {
    static let shared = K1L0TransmissionMediaDiskCache()

    private let fileManager = FileManager.default
    private let directory: URL
    private let byteLimit: Int64 = 512 * 1024 * 1024

    private init() {
        let root = fileManager.urls(for: .cachesDirectory, in: .userDomainMask).first
            ?? fileManager.temporaryDirectory
        directory = root.appendingPathComponent("K1L0TransmissionMedia", isDirectory: true)
        try? fileManager.createDirectory(at: directory, withIntermediateDirectories: true)
    }

    func localURL(for remoteURL: URL) async throws -> URL {
        guard !remoteURL.isFileURL else { return remoteURL }
        let destination = cachedURL(for: remoteURL)
        if isUsableFile(destination) {
            try? fileManager.setAttributes([.modificationDate: Date()], ofItemAtPath: destination.path)
            return destination
        }

        let (temporaryURL, response) = try await URLSession.shared.download(from: remoteURL)
        if let http = response as? HTTPURLResponse, !(200...299).contains(http.statusCode) {
            throw URLError(.badServerResponse)
        }
        guard isUsableFile(temporaryURL) else { throw URLError(.zeroByteResource) }

        try? fileManager.removeItem(at: destination)
        try fileManager.moveItem(at: temporaryURL, to: destination)
        try? fileManager.setAttributes([.modificationDate: Date()], ofItemAtPath: destination.path)
        pruneIfNeeded()
        return destination
    }

    private func cachedURL(for remoteURL: URL) -> URL {
        let digest = SHA256.hash(data: Data(remoteURL.absoluteString.utf8))
            .map { String(format: "%02x", $0) }
            .joined()
        let ext = remoteURL.pathExtension.isEmpty ? "media" : remoteURL.pathExtension
        return directory.appendingPathComponent("\(digest).\(ext)")
    }

    private func isUsableFile(_ url: URL) -> Bool {
        guard let attributes = try? fileManager.attributesOfItem(atPath: url.path),
              let size = attributes[.size] as? NSNumber else { return false }
        return size.int64Value > 0
    }

    private func pruneIfNeeded() {
        let keys: Set<URLResourceKey> = [.isRegularFileKey, .fileSizeKey, .contentModificationDateKey]
        guard let files = try? fileManager.contentsOfDirectory(
            at: directory,
            includingPropertiesForKeys: Array(keys),
            options: [.skipsHiddenFiles]
        ) else { return }

        let entries = files.compactMap { url -> (URL, Int64, Date)? in
            guard let values = try? url.resourceValues(forKeys: keys),
                  values.isRegularFile == true else { return nil }
            return (url, Int64(values.fileSize ?? 0), values.contentModificationDate ?? .distantPast)
        }
        var total = entries.reduce(Int64(0)) { $0 + $1.1 }
        guard total > byteLimit else { return }
        for entry in entries.sorted(by: { $0.2 < $1.2 }) {
            try? fileManager.removeItem(at: entry.0)
            total -= entry.1
            if total <= byteLimit { break }
        }
    }
}

@MainActor
final class K1L0TransmissionMediaPreloader: ObservableObject {
    @Published private(set) var localURLs: [String: URL] = [:]
    @Published private(set) var completedCount = 0
    @Published private(set) var totalCount = 0
    @Published private(set) var isReady = false
    @Published private(set) var errorMessage = ""

    private var loadTask: Task<Void, Never>?
    private var activeKey = ""

    var progress: Double {
        guard totalCount > 0 else { return isReady ? 1 : 0 }
        return Double(completedCount) / Double(totalCount)
    }

    func prepare(urls: [URL], force: Bool = false) {
        let unique = Array(Dictionary(grouping: urls.filter { !$0.isFileURL }, by: \.absoluteString).values.compactMap(\.first))
        let key = unique.map(\.absoluteString).sorted().joined(separator: "\n")
        if !force, key == activeKey, isReady { return }

        loadTask?.cancel()
        activeKey = key
        localURLs = Dictionary(uniqueKeysWithValues: urls.filter(\.isFileURL).map { ($0.absoluteString, $0) })
        completedCount = 0
        totalCount = unique.count
        isReady = unique.isEmpty
        errorMessage = ""
        guard !unique.isEmpty else { return }

        loadTask = Task {
            do {
                try await withThrowingTaskGroup(of: (String, URL).self) { group in
                    for remoteURL in unique {
                        group.addTask {
                            let localURL = try await K1L0TransmissionMediaDiskCache.shared.localURL(for: remoteURL)
                            return (remoteURL.absoluteString, localURL)
                        }
                    }
                    for try await (remoteKey, localURL) in group {
                        guard !Task.isCancelled else { return }
                        localURLs[remoteKey] = localURL
                        completedCount += 1
                    }
                }
                guard !Task.isCancelled else { return }
                isReady = true
            } catch {
                guard !Task.isCancelled else { return }
                errorMessage = error.localizedDescription
            }
        }
    }

    func retry(urls: [URL]) {
        prepare(urls: urls, force: true)
    }

    func resolve(_ url: URL?) -> URL? {
        guard let url else { return nil }
        return localURLs[url.absoluteString] ?? url
    }

    deinit {
        loadTask?.cancel()
    }
}

extension K1L0TransmissionClip {
    func replacingMedia(videoURL: URL?, audioURL: URL?) -> K1L0TransmissionClip {
        var copy = K1L0TransmissionClip(
            videoURL: videoURL,
            imageURL: imageURL,
            audioURL: audioURL,
            responsePlot: responsePlot,
            responseOptions: responseOptions,
            selectedResponse: selectedResponse
        )
        copy.sourceJobId = sourceJobId
        copy.sourceUserId = sourceUserId
        copy.sourceName = sourceName
        copy.sourceCallsign = sourceCallsign
        copy.sourceCity = sourceCity
        copy.sourceCountry = sourceCountry
        copy.sourceCountryCode = sourceCountryCode
        copy.createdAt = createdAt
        copy.allowsResponse = allowsResponse
        return copy
    }
}

struct TransmissionMediaLoadingView: View {
    let completed: Int
    let total: Int
    let progress: Double
    let errorMessage: String
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: 16) {
            if errorMessage.isEmpty {
                ProgressView(value: progress)
                    .progressViewStyle(.linear)
                    .tint(Color(red: 0.35, green: 1.0, blue: 0.62))
                    .frame(maxWidth: 230)

                Text("Loading transmission…")
                    .font(.system(size: 18, weight: .black, design: .monospaced))
                    .foregroundStyle(.white)

                if total > 0 {
                    Text("\(completed) of \(total) media files")
                        .font(.system(size: 12, weight: .bold, design: .monospaced))
                        .foregroundStyle(.white.opacity(0.62))
                }
            } else {
                Text("Transmission failed to load")
                    .font(.system(size: 17, weight: .black, design: .monospaced))
                    .foregroundStyle(.white)

                Button("Try Again", action: onRetry)
                    .font(.system(size: 15, weight: .black, design: .rounded))
                    .foregroundStyle(.black)
                    .padding(.horizontal, 22)
                    .frame(height: 42)
                    .background(Color.green, in: Capsule())
            }
        }
        .padding(24)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(Color.black)
    }
}

struct InlineTransmissionVideoPlayer: View {
    let urlString: String
    let audioUrlString: String?
    let clips: [K1L0TransmissionClip]
    @State private var player: AVPlayer
    @State private var audioPlayer: AVPlayer?
    @Binding private var currentClipIndex: Int
    @Binding private var currentClipProgress: Double
    @Binding private var isVideoReady: Bool
    // When true, the player loops the CURRENT clip in place instead of
    // advancing to the next in the chain. Wired to the pencil-edit state
    // in TransmissionResultPanel so the frame being edited stays put.
    @Binding private var freezeCurrent: Bool
    @State private var timeObserver: Any?
    @State private var fxLoopState: K1L0TransmissionFXLoopState
    private let holdAtEndIndex: Int?

    init(urlString: String, audioUrlString: String? = nil, currentClipProgress: Binding<Double> = .constant(0), isVideoReady: Binding<Bool> = .constant(false), freezeCurrent: Binding<Bool> = .constant(false)) {
        self.urlString = urlString
        self.audioUrlString = audioUrlString
        self.clips = []
        _currentClipIndex = .constant(0)
        _currentClipProgress = currentClipProgress
        _isVideoReady = isVideoReady
        _freezeCurrent = freezeCurrent
        self.holdAtEndIndex = nil
        let loopState = K1L0TransmissionFXLoopState()
        _fxLoopState = State(initialValue: loopState)
        let url = URL(string: urlString) ?? URL(fileURLWithPath: "/dev/null")
        let item = AVPlayerItem(url: url)
        K1L0TransmissionFX.apply(to: item, loopState: loopState)
        _player = State(initialValue: AVPlayer(playerItem: item))
        // Raw WAN clips are silent — TransmissionFX-style audio rides on a
        // separate URL (ACE-Step music/vocals). Play it in parallel and loop
        // it to match the video loop.
        let audio: AVPlayer?
        if let s = audioUrlString, !s.isEmpty, let u = URL(string: s) {
            audio = AVPlayer(url: u)
        } else {
            audio = nil
        }
        _audioPlayer = State(initialValue: audio)
    }

    init(clips: [K1L0TransmissionClip], currentClipIndex: Binding<Int>, currentClipProgress: Binding<Double>, isVideoReady: Binding<Bool> = .constant(false), holdAtEndIndex: Int? = nil, freezeCurrent: Binding<Bool> = .constant(false)) {
        let playable = clips.filter { $0.videoURL != nil }
        self.clips = playable
        self.urlString = playable.first?.videoURL?.absoluteString ?? ""
        self.audioUrlString = playable.first?.audioURL?.absoluteString
        _currentClipIndex = currentClipIndex
        _currentClipProgress = currentClipProgress
        _isVideoReady = isVideoReady
        _freezeCurrent = freezeCurrent
        self.holdAtEndIndex = holdAtEndIndex
        let loopState = K1L0TransmissionFXLoopState()
        _fxLoopState = State(initialValue: loopState)
        let url = playable.first?.videoURL ?? URL(fileURLWithPath: "/dev/null")
        let item = AVPlayerItem(url: url)
        K1L0TransmissionFX.apply(to: item, loopState: loopState)
        _player = State(initialValue: AVPlayer(playerItem: item))
        let audio: AVPlayer?
        if let audioURL = playable.first?.audioURL {
            audio = AVPlayer(url: audioURL)
        } else {
            audio = nil
        }
        _audioPlayer = State(initialValue: audio)
    }

    var body: some View {
        // Keep playback on the bare AVPlayerLayer. SwiftUI layer effects can't
        // sample UIKit-backed video reliably; the tattered edge is overlaid by
        // the result panel instead.
        K1L0BareVideoPlayer(player: player)
            .onAppear {
                isVideoReady = false
                player.isMuted = audioPlayer != nil
                player.play()
                audioPlayer?.play()
                installProgressObserver()
            }
            .onDisappear {
                player.pause()
                audioPlayer?.pause()
                removeProgressObserver()
                isVideoReady = false
            }
            .onReceive(NotificationCenter.default.publisher(for: .AVPlayerItemDidPlayToEndTime)) { notification in
                guard let item = notification.object as? AVPlayerItem else { return }
                if item === player.currentItem {
                    advanceVideo()
                }
                if let ap = audioPlayer, item === ap.currentItem {
                    ap.seek(to: .zero, toleranceBefore: .zero, toleranceAfter: .zero) { _ in
                        ap.play()
                    }
                }
            }
            .onChange(of: currentClipIndex) { index in
                playClip(at: index)
            }
    }

    private func advanceVideo() {
        guard !clips.isEmpty else {
            // Single video wrapped — count the completed play-through so the
            // next loop can re-roll its FX schedule.
            fxLoopState.loopCount += 1
            if let item = player.currentItem {
                K1L0TransmissionFX.apply(to: item, loopState: fxLoopState)
            }
            player.seek(to: .zero, toleranceBefore: .zero, toleranceAfter: .zero) { _ in
                player.play()
            }
            return
        }
        // Pencil-edit takes precedence over chain advance and holdAtEndIndex:
        // loop the current clip in place until the user closes the editor.
        if freezeCurrent {
            fxLoopState.loopCount += 1
            if let item = player.currentItem {
                K1L0TransmissionFX.apply(to: item, loopState: fxLoopState)
            }
            player.seek(to: .zero, toleranceBefore: .zero, toleranceAfter: .zero) { _ in
                player.play()
            }
            return
        }
        if let holdAtEndIndex, currentClipIndex == holdAtEndIndex {
            // Loop the response clip in place (the choices stay up because the
            // clip index doesn't change) instead of freezing on the last
            // frame — a frozen frame reads as a broken static image.
            fxLoopState.loopCount += 1
            if let item = player.currentItem {
                K1L0TransmissionFX.apply(to: item, loopState: fxLoopState)
            }
            player.seek(to: .zero, toleranceBefore: .zero, toleranceAfter: .zero) { _ in
                player.play()
            }
            return
        }
        let nextIndex = (currentClipIndex + 1) % clips.count
        // A full pass of the chain only completes when we wrap back to clip 0.
        if nextIndex == 0 { fxLoopState.loopCount += 1 }
        if nextIndex == currentClipIndex {
            // Single-clip chain: 0 → 0 isn't an index change, so onChange never
            // refires playClip — restart directly or the video freezes at end.
            if let item = player.currentItem {
                K1L0TransmissionFX.apply(to: item, loopState: fxLoopState)
            }
            player.seek(to: .zero, toleranceBefore: .zero, toleranceAfter: .zero) { _ in
                player.play()
            }
            return
        }
        currentClipIndex = nextIndex
    }

    private func playClip(at index: Int) {
        guard !clips.isEmpty else { return }
        let safeIndex = min(max(0, index), clips.count - 1)
        if currentClipIndex != safeIndex {
            currentClipIndex = safeIndex
            return
        }
        currentClipProgress = 0
        isVideoReady = false
        let next = clips[safeIndex]
        guard let videoURL = next.videoURL else {
            player.seek(to: .zero)
            player.play()
            return
        }
        let item = AVPlayerItem(url: videoURL)
        K1L0TransmissionFX.apply(to: item, loopState: fxLoopState)
        // Music continuity: a story chain shares one track, so only swap the
        // audio player when the next clip carries a DIFFERENT track. Same
        // track — or a clip with no track of its own — keeps the music
        // playing seamlessly across slide changes.
        if let audioURL = next.audioURL {
            let currentURL = (audioPlayer?.currentItem?.asset as? AVURLAsset)?.url
            if currentURL != audioURL {
                audioPlayer?.pause()
                audioPlayer = AVPlayer(url: audioURL)
            }
        }
        player.replaceCurrentItem(with: item)
        player.isMuted = audioPlayer != nil
        player.play()
        if audioPlayer?.timeControlStatus != .playing {
            audioPlayer?.play()
        }
    }

    private func installProgressObserver() {
        removeProgressObserver()
        // Four progress updates per second are visually smooth enough for the
        // tiny chain bar and avoid driving SwiftUI at ~12.5 Hz over AVPlayer.
        let interval = CMTime(seconds: 0.25, preferredTimescale: 600)
        timeObserver = player.addPeriodicTimeObserver(forInterval: interval, queue: .main) { time in
            markVideoReadyIfPossible()
            guard let duration = player.currentItem?.duration.seconds,
                  duration.isFinite,
                  duration > 0 else {
                currentClipProgress = 0
                return
            }
            currentClipProgress = min(1, max(0, time.seconds / duration))
        }
    }

    private func markVideoReadyIfPossible() {
        guard !isVideoReady,
              let item = player.currentItem,
              item.status == .readyToPlay else { return }
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.18) {
            if player.currentItem === item && item.status == .readyToPlay {
                isVideoReady = true
            }
        }
    }

    private func removeProgressObserver() {
        if let timeObserver {
            player.removeTimeObserver(timeObserver)
            self.timeObserver = nil
        }
    }

}

struct TransmissionFizzyEdgesModifier: ViewModifier {
    let enabled: Bool
    let size: CGSize

    private static let fizzyShaderAvailable: Bool = {
        guard let device = MTLCreateSystemDefaultDevice() else { return false }
        let bundle = Bundle(for: K1L0TuningStaticPlayer.self)
        guard let lib = try? device.makeDefaultLibrary(bundle: bundle) else { return false }
        return lib.functionNames.contains("k1l0FizzyEdges")
    }()

    func body(content: Content) -> some View {
        if #available(iOS 17.0, macOS 14.0, *), enabled, Self.fizzyShaderAvailable {
            TimelineView(.periodic(from: .now, by: 0.18)) { timeline in
                content
                    .layerEffect(
                        ShaderLibrary.bundle(Bundle(for: K1L0TuningStaticPlayer.self)).k1l0FizzyEdges(
                            .float2(Float(size.width), Float(size.height)),
                            .float(Float(timeline.date.timeIntervalSinceReferenceDate.truncatingRemainder(dividingBy: 3600))),
                            .float(enabled ? 1.0 : 0.0)
                        ),
                        maxSampleOffset: .zero
                    )
            }
        } else {
            content
        }
    }
}

extension View {
    func transmissionFizzyMask(enabled: Bool, size: CGSize) -> some View {
        modifier(TransmissionFizzyEdgesModifier(enabled: enabled, size: size))
    }
}

struct K1L0BareVideoPlayer: View {
    let player: AVPlayer

    var body: some View {
#if canImport(UIKit)
        K1L0MetalVideoPlayerView(player: player)
#elseif canImport(AppKit)
        K1L0BareVideoPlayerNSView(player: player)
#else
        VideoPlayer(player: player)
#endif
    }
}

#if canImport(UIKit)
struct K1L0MetalVideoPlayerView: UIViewRepresentable {
    let player: AVPlayer

    func makeUIView(context: Context) -> K1L0MetalVideoView {
        let view = K1L0MetalVideoView()
        view.player = player
        return view
    }

    func updateUIView(_ view: K1L0MetalVideoView, context: Context) {
        view.player = player
    }
}

final class K1L0MetalVideoView: MTKView, MTKViewDelegate {
    struct Uniforms {
        var viewport = SIMD2<Float>(1, 1)
        var texture = SIMD2<Float>(1, 1)
        var time: Float = 0
        var intensity: Float = 0.5
    }

    var player: AVPlayer? {
        didSet {
            fallbackPlayerLayer?.player = player
            attachOutputIfNeeded()
        }
    }
    private var attachedItem: AVPlayerItem?
    private var videoOutput: AVPlayerItemVideoOutput?
    private var commandQueue: MTLCommandQueue?
    private var pipeline: MTLRenderPipelineState?
    private var textureCache: CVMetalTextureCache?
    private var fallbackPlayerLayer: AVPlayerLayer?
    private let startedAt = CACurrentMediaTime()

    required init(coder: NSCoder) { super.init(coder: coder); configure() }
    override init(frame: CGRect, device: MTLDevice?) {
        super.init(frame: frame, device: device ?? MTLCreateSystemDefaultDevice())
        configure()
    }

    private func configure() {
        guard let device else { return }
        framebufferOnly = false
        isOpaque = false
        backgroundColor = .clear
        colorPixelFormat = .bgra8Unorm
        preferredFramesPerSecond = 30
        enableSetNeedsDisplay = false
        isPaused = false
        delegate = self
        commandQueue = device.makeCommandQueue()
        CVMetalTextureCacheCreate(nil, nil, device, nil, &textureCache)
        // K1L0TuningShader.metal is compiled into the main app target. Loading
        // from Bundle(for:) here used to resolve UnityFramework and left the
        // pipeline nil after the shader ownership moved to K1L0.app.
        let shaderBundle = Bundle.main
        let bundledLibrary = try? device.makeDefaultLibrary(bundle: shaderBundle)
        let explicitLibrary: MTLLibrary? = {
            let url = URL(fileURLWithPath: shaderBundle.bundlePath)
                .appendingPathComponent("default.metallib")
            return try? device.makeLibrary(URL: url)
        }()
        guard let library = bundledLibrary ?? explicitLibrary,
              let vertex = library.makeFunction(name: "k1l0VideoVertex"),
              let fragment = library.makeFunction(name: "k1l0VideoFragment") else {
            NSLog("[K1L0VideoMetal] app shader unavailable; using AVPlayerLayer fallback")
            enablePlayerLayerFallback()
            return
        }
        let descriptor = MTLRenderPipelineDescriptor()
        descriptor.vertexFunction = vertex
        descriptor.fragmentFunction = fragment
        descriptor.colorAttachments[0].pixelFormat = colorPixelFormat
        descriptor.colorAttachments[0].isBlendingEnabled = true
        // The point fragment already carries its shaped alpha. With additive blending,
        // multiplying RGB by alpha again made the hologram nearly invisible on-device.
        descriptor.colorAttachments[0].sourceRGBBlendFactor = .one
        descriptor.colorAttachments[0].destinationRGBBlendFactor = .oneMinusSourceAlpha
        do {
            pipeline = try device.makeRenderPipelineState(descriptor: descriptor)
            NSLog("[K1L0VideoMetal] app-owned pipeline ready")
        } catch {
            NSLog("[K1L0VideoMetal] pipeline failed: \(error); using AVPlayerLayer fallback")
            enablePlayerLayerFallback()
        }
    }

    private func enablePlayerLayerFallback() {
        guard fallbackPlayerLayer == nil else { return }
        let layer = AVPlayerLayer(player: player)
        layer.videoGravity = .resizeAspectFill
        self.layer.addSublayer(layer)
        fallbackPlayerLayer = layer
        setNeedsLayout()
    }

    override func layoutSubviews() {
        super.layoutSubviews()
        fallbackPlayerLayer?.frame = bounds
    }

    private func attachOutputIfNeeded() {
        guard let item = player?.currentItem, item !== attachedItem else { return }
        if let old = attachedItem, let output = videoOutput { old.remove(output) }
        let attrs: [String: Any] = [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA]
        let output = AVPlayerItemVideoOutput(pixelBufferAttributes: attrs)
        item.add(output)
        attachedItem = item
        videoOutput = output
    }

    func mtkView(_ view: MTKView, drawableSizeWillChange size: CGSize) {}

    func draw(in view: MTKView) {
        attachOutputIfNeeded()
        guard let output = videoOutput, let player, let drawable = currentDrawable,
              let pass = currentRenderPassDescriptor, let pipeline, let commandQueue,
              let cache = textureCache else { return }
        let itemTime = output.itemTime(forHostTime: CACurrentMediaTime())
        guard output.hasNewPixelBuffer(forItemTime: itemTime),
              let pixel = output.copyPixelBuffer(forItemTime: itemTime, itemTimeForDisplay: nil) else { return }
        let width = CVPixelBufferGetWidth(pixel), height = CVPixelBufferGetHeight(pixel)
        var cvTexture: CVMetalTexture?
        guard CVMetalTextureCacheCreateTextureFromImage(nil, cache, pixel, nil, .bgra8Unorm, width, height, 0, &cvTexture) == kCVReturnSuccess,
              let cvTexture, let texture = CVMetalTextureGetTexture(cvTexture),
              let buffer = commandQueue.makeCommandBuffer(), let encoder = buffer.makeRenderCommandEncoder(descriptor: pass) else { return }
        var uniforms = Uniforms(
            viewport: SIMD2(Float(drawableSize.width), Float(drawableSize.height)),
            texture: SIMD2(Float(width), Float(height)),
            time: Float(CACurrentMediaTime() - startedAt),
            intensity: K1L0TransmissionFX.enabled ? Float(K1L0TransmissionFX.intensity) : 0
        )
        encoder.setRenderPipelineState(pipeline)
        encoder.setFragmentTexture(texture, index: 0)
        encoder.setFragmentBytes(&uniforms, length: MemoryLayout<Uniforms>.stride, index: 0)
        encoder.drawPrimitives(type: .triangleStrip, vertexStart: 0, vertexCount: 4)
        encoder.endEncoding()
        buffer.present(drawable)
        buffer.commit()
        _ = player
    }
}

struct K1L0BareVideoPlayerView: UIViewRepresentable {
    let player: AVPlayer

    func makeUIView(context: Context) -> PlayerView {
        let view = PlayerView()
        view.playerLayer.player = player
        // Aspect-fill so the transmission covers the full-height playback box
        // (minor center crop) instead of letterboxing — the tattered edges
        // then tear real video on all four sides, edge to edge.
        view.playerLayer.videoGravity = .resizeAspectFill
        view.isUserInteractionEnabled = false
        view.backgroundColor = .clear
        return view
    }

    func updateUIView(_ uiView: PlayerView, context: Context) {
        if uiView.playerLayer.player !== player {
            uiView.playerLayer.player = player
        }
    }

    final class PlayerView: UIView {
        override static var layerClass: AnyClass {
            AVPlayerLayer.self
        }

        var playerLayer: AVPlayerLayer {
            layer as! AVPlayerLayer
        }
    }
}
#endif

#if canImport(AppKit) && !canImport(UIKit)
struct K1L0BareVideoPlayerNSView: NSViewRepresentable {
    let player: AVPlayer

    func makeNSView(context: Context) -> PlayerView {
        let view = PlayerView()
        view.playerLayer.player = player
        view.playerLayer.videoGravity = .resizeAspect
        return view
    }

    func updateNSView(_ nsView: PlayerView, context: Context) {
        if nsView.playerLayer.player !== player {
            nsView.playerLayer.player = player
        }
    }

    final class PlayerView: NSView {
        let playerLayer = AVPlayerLayer()

        override init(frame frameRect: NSRect) {
            super.init(frame: frameRect)
            wantsLayer = true
            layer = CALayer()
            layer?.backgroundColor = NSColor.clear.cgColor
            playerLayer.backgroundColor = NSColor.clear.cgColor
            playerLayer.videoGravity = .resizeAspect
            layer?.addSublayer(playerLayer)
        }

        required init?(coder: NSCoder) {
            nil
        }

        override func layout() {
            super.layout()
            playerLayer.frame = bounds
        }
    }
}
#endif

// Reusable sticky drag handle for full-screen panels. The content scrolls
// behind it; this view stays pinned to the top of the panel and fires onDismiss
// when pulled down past the threshold.
