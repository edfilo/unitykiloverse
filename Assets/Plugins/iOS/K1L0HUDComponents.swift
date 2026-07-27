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

struct StickyPanelHeader: View {
    let iconName: String
    let title: String
    let onClose: () -> Void

    var body: some View {
        VStack(spacing: 4) {
            RoundedRectangle(cornerRadius: 3, style: .continuous)
                .fill(Color.white.opacity(0.46))
                .frame(width: 44, height: 5)
                .padding(.top, 8)
            HStack(spacing: 10) {
                Image(systemName: iconName)
                    .font(.system(size: 18, weight: .black))
                    .foregroundStyle(.white)
                Text(title)
                    .font(.system(size: 20, weight: .black, design: .rounded))
                    .foregroundStyle(.white)
                Spacer()
                Button(action: onClose) {
                    Image(systemName: "xmark")
                        .font(.system(size: 14, weight: .black))
                        .foregroundStyle(.white)
                        .frame(width: 34, height: 34)
                        .background(Color.black.opacity(0.46), in: Circle())
                        .overlay(Circle().stroke(.white.opacity(0.28), lineWidth: 1))
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 16)
            .padding(.bottom, 10)
        }
        .frame(maxWidth: .infinity)
        .background(Color.black.opacity(0.001)) // near-clear but hit-testable
        .contentShape(Rectangle())                // ensure drag/tap hit the whole header
        .overlay(
            Rectangle().fill(Color.white.opacity(0.10)).frame(height: 1),
            alignment: .bottom
        )
        .gesture(
            DragGesture(minimumDistance: 14)
                .onEnded { value in
                    if value.translation.height > 70 && abs(value.translation.width) < value.translation.height {
                        onClose()
                    }
                }
        )
    }
}

struct HomePanelHeader: View {
    let onClose: () -> Void

    var body: some View {
        VStack(spacing: 5) {
            RoundedRectangle(cornerRadius: 3, style: .continuous)
                .fill(Color.white.opacity(0.34))
                .frame(width: 44, height: 5)
                .padding(.top, 8)
            ZStack {
                HStack {
                    Button(action: onClose) {
                        Image(systemName: "xmark")
                            .font(.system(size: 15, weight: .black))
                            .foregroundStyle(.white)
                            .frame(width: 38, height: 38)
                    }
                    .buttonStyle(.plain)
                    Spacer()
                }
            }
            .padding(.horizontal, 16)
            .padding(.bottom, 10)
        }
        .frame(maxWidth: .infinity)
        .background(Color.black.opacity(0.001))
        .contentShape(Rectangle())
        .overlay(
            Rectangle().fill(Color.white.opacity(0.08)).frame(height: 1),
            alignment: .bottom
        )
    }
}

struct UserPanelHeader: View {
    @ObservedObject private var saveStore = K1L0UserMetadataSaveStore.shared
    let title: String
    var tabsMode: Bool = false
    let onClose: () -> Void
    var onSave: (() -> Void)? = nil
    var onBack: (() -> Void)? = nil

    var body: some View {
        VStack(spacing: 5) {
            if !tabsMode && onBack == nil {
                RoundedRectangle(cornerRadius: 3, style: .continuous)
                    .fill(Color.white.opacity(0.34))
                    .frame(width: 44, height: 5)
                    .padding(.top, 8)
            }
            ZStack {
                Text(title)
                    .font(.system(size: 20, weight: .black, design: .rounded))
                    .foregroundStyle(.white)
                    .frame(maxWidth: .infinity, alignment: .center)
                HStack {
                    if let onBack {
                        Button(action: onBack) {
                            HStack(spacing: 4) {
                                Image(systemName: "chevron.left")
                                    .font(.system(size: 16, weight: .black))
                                Text("Back")
                                    .font(.system(size: 15, weight: .bold))
                            }
                            .foregroundStyle(.white)
                            .frame(height: 38)
                        }
                        .buttonStyle(.plain)
                        .disabled(saveStore.isSaving)
                    } else if !tabsMode {
                        Button(action: onClose) {
                            Image(systemName: "xmark")
                                .font(.system(size: 15, weight: .black))
                                .foregroundStyle(.white)
                                .frame(width: 38, height: 38)
                        }
                        .buttonStyle(.plain)
                        .disabled(saveStore.isSaving)
                    } else {
                        Color.clear.frame(width: 38, height: 38)
                    }
                    Spacer()
                    if let onSave {
                        Button(action: {
                            if !saveStore.isSaving {
                                onSave()
                            }
                        }) {
                            HStack(spacing: 6) {
                                if saveStore.isSaving {
                                    ProgressView()
                                        .progressViewStyle(.circular)
                                        .tint(.white)
                                        .scaleEffect(0.8)
                                    Text("Saving...")
                                } else {
                                    Text("Save")
                                }
                            }
                            .font(.system(size: 15, weight: .black))
                            .foregroundStyle(.white)
                            .padding(.horizontal, 12)
                            .frame(height: 38)
                        }
                        .buttonStyle(.plain)
                        .disabled(saveStore.isSaving)
                    } else {
                        Color.clear.frame(width: 54, height: 38)
                    }
                }
            }
            .padding(.horizontal, 16)
            .padding(.bottom, 10)
        }
        .frame(maxWidth: .infinity)
        .background(Color.black.opacity(0.001))
        .contentShape(Rectangle())
        .overlay(
            Rectangle().fill(Color.white.opacity(tabsMode ? 0 : 0.08)).frame(height: 1),
            alignment: .bottom
        )
    }
}

// iOS-26 Liquid Glass when available, ultraThinMaterial fallback otherwise.
// Used by the persistent floating home/map and user buttons in the corners.
struct LiquidGlassCircle: ViewModifier {
    func body(content: Content) -> some View {
        if #available(iOS 26.0, macOS 26.0, *) {
            content
                .glassEffect(.regular.interactive(), in: Circle())
        } else {
            content
                .background(.ultraThinMaterial, in: Circle())
                .overlay(Circle().stroke(.white.opacity(0.24), lineWidth: 1))
        }
    }
}

// Detects bounce-overscroll at the top of a ScrollView. Place as the first
// child inside the ScrollView. Reports the scroll-content top position into
// the parent's named coordinate space; fires onDismiss when the user pulls
// past `threshold` (rubber-band sheet-dismiss gesture).
struct PullToDismissTopAnchor: View {
    let panelCoordinateSpace: String
    let onDismiss: () -> Void
    let threshold: CGFloat
    @State private var baseline: CGFloat? = nil
    @State private var fired = false

    var body: some View {
        GeometryReader { proxy in
            let y = proxy.frame(in: .named(panelCoordinateSpace)).minY
            Color.clear
                .preference(key: PullOffsetKey.self, value: y)
        }
        .frame(height: 0)
    }
}

struct PullOffsetKey: PreferenceKey {
    static var defaultValue: CGFloat = 0
    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) { value = nextValue() }
}

#if canImport(UIKit)
struct CameraRollSaveMediaItem {
    let videoUrlString: String
    let audioUrlString: String?
    var overlayText: String = ""
    var overlayTransform: TransmissionTextTransform = TransmissionTextTransform()
}

struct LocalCameraRollSaveMediaItem {
    let videoURL: URL
    let audioURL: URL?
    let overlayText: String
    let overlayTransform: TransmissionTextTransform
}

func cameraRollSaveError(_ message: String) -> NSError {
    NSError(domain: "K1L0CameraRollSave", code: 1, userInfo: [NSLocalizedDescriptionKey: message])
}

func k1l0SaveLog(_ message: String) {
    let line = "\(ISO8601DateFormatter().string(from: Date())) \(message)\n"
    NSLog("K1L0Save %@", message)
    guard let documents = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first else { return }
    let url = documents.appendingPathComponent("k1l0-save.log")
    guard let data = line.data(using: .utf8) else { return }
    if FileManager.default.fileExists(atPath: url.path),
       let handle = try? FileHandle(forWritingTo: url) {
        defer { try? handle.close() }
        try? handle.seekToEnd()
        try? handle.write(contentsOf: data)
    } else {
        try? data.write(to: url, options: .atomic)
    }
}

func k1l0SaveErrorDescription(_ error: Error) -> String {
    let nsError = error as NSError
    return "\(nsError.domain) code=\(nsError.code) \(nsError.localizedDescription) userInfo=\(nsError.userInfo)"
}

final class K1L0CameraRollSaveDelegate: NSObject {
    let completion: (String) -> Void

    init(completion: @escaping (String) -> Void) {
        self.completion = completion
    }

    @objc func video(_ videoPath: String, didFinishSavingWithError error: Error?, contextInfo: UnsafeMutableRawPointer) {
        if let error {
            completion("save failed: \(error.localizedDescription)")
        } else {
            completion("saved to camera roll")
        }
    }
}

final class K1L0CameraRollSaveStatusOverlay {
    private static var window: UIWindow?
    private static var label: UILabel?
    private static var spinner: UIActivityIndicatorView?

    static func show(_ status: String) {
        DispatchQueue.main.async {
            let currentWindow = window ?? makeWindow()
            window = currentWindow
            label?.text = status
            spinner?.startAnimating()
            currentWindow.isHidden = false
            currentWindow.alpha = 1
        }
    }

    static func hide(after delay: TimeInterval = 0.55) {
        DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
            UIView.animate(withDuration: 0.18, animations: {
                window?.alpha = 0
            }, completion: { _ in
                spinner?.stopAnimating()
                window?.isHidden = true
            })
        }
    }

    private static func makeWindow() -> UIWindow {
        let scene = UIApplication.shared.connectedScenes
            .compactMap { $0 as? UIWindowScene }
            .first { $0.activationState == .foregroundActive }
            ?? UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first
        let nextWindow = scene.map { UIWindow(windowScene: $0) } ?? UIWindow(frame: UIScreen.main.bounds)
        nextWindow.windowLevel = .alert + 4
        nextWindow.backgroundColor = .clear

        let root = UIViewController()
        root.view.backgroundColor = .clear
        let panel = UIVisualEffectView(effect: UIBlurEffect(style: .systemUltraThinMaterialDark))
        panel.translatesAutoresizingMaskIntoConstraints = false
        panel.layer.cornerRadius = 18
        panel.clipsToBounds = true

        let indicator = UIActivityIndicatorView(style: .large)
        indicator.translatesAutoresizingMaskIntoConstraints = false
        indicator.color = .white

        let text = UILabel()
        text.translatesAutoresizingMaskIntoConstraints = false
        text.textColor = .white
        text.font = .monospacedSystemFont(ofSize: 13, weight: .black)
        text.textAlignment = .center
        text.numberOfLines = 2

        panel.contentView.addSubview(indicator)
        panel.contentView.addSubview(text)
        root.view.addSubview(panel)
        nextWindow.rootViewController = root

        NSLayoutConstraint.activate([
            panel.centerXAnchor.constraint(equalTo: root.view.centerXAnchor),
            panel.centerYAnchor.constraint(equalTo: root.view.centerYAnchor),
            panel.widthAnchor.constraint(equalToConstant: 230),
            panel.heightAnchor.constraint(equalToConstant: 132),
            indicator.centerXAnchor.constraint(equalTo: panel.contentView.centerXAnchor),
            indicator.topAnchor.constraint(equalTo: panel.contentView.topAnchor, constant: 28),
            text.leadingAnchor.constraint(equalTo: panel.contentView.leadingAnchor, constant: 14),
            text.trailingAnchor.constraint(equalTo: panel.contentView.trailingAnchor, constant: -14),
            text.topAnchor.constraint(equalTo: indicator.bottomAnchor, constant: 14)
        ])

        label = text
        spinner = indicator
        return nextWindow
    }
}

struct CameraRollSaveButton: View {
    let mediaItems: [CameraRollSaveMediaItem]
    let iconOnly: Bool
    @State private var status = ""
    @State private var saving = false
    @State private var saveDelegate: K1L0CameraRollSaveDelegate?

    init(videoUrlString: String, audioUrlString: String? = nil, overlayText: String = "", overlayTransform: TransmissionTextTransform = TransmissionTextTransform()) {
        self.mediaItems = [CameraRollSaveMediaItem(videoUrlString: videoUrlString, audioUrlString: audioUrlString, overlayText: overlayText, overlayTransform: overlayTransform)]
        self.iconOnly = false
    }

    init(mediaItems: [CameraRollSaveMediaItem], iconOnly: Bool = false) {
        self.mediaItems = mediaItems.filter { !$0.videoUrlString.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
        self.iconOnly = iconOnly
    }

    var body: some View {
        if iconOnly {
            Button {
                saveVideo()
            } label: {
                Image(systemName: saving ? "hourglass" : "square.and.arrow.down")
                    .font(.system(size: 15, weight: .regular))
                    .foregroundStyle(.white.opacity(0.94))
                    .frame(width: 34, height: 34)
                    .background(.ultraThinMaterial, in: Circle())
                    .overlay(Circle().stroke(Color.white.opacity(0.20), lineWidth: 0.5))
                    .frame(width: 44, height: 44)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .disabled(saving)
        } else {
        VStack(alignment: .trailing, spacing: 4) {
            Button {
                saveVideo()
            } label: {
                Text(saving ? "[ SAVING… ]" : "[ SAVE TO CAMERA ROLL ]")
                    .font(.system(size: 13, weight: .black, design: .monospaced))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 14)
                    .frame(minHeight: 42)
                    .background(Color.black.opacity(0.58))
            }
            .buttonStyle(.plain)
            .disabled(saving)

            if !status.isEmpty {
                Text(status)
                    .font(.system(size: 10, weight: .bold, design: .monospaced))
                    .foregroundStyle(.white.opacity(0.72))
                    .lineLimit(1)
            }
        }
        }
    }

    private func saveVideo() {
        guard !saving, !mediaItems.isEmpty else {
            status = "bad video url"
            return
        }
        k1l0SaveLog("save tapped items=\(mediaItems.count)")
        for (index, item) in mediaItems.enumerated() {
            let hasOverlay = !item.overlayText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            let hasAudio = !(item.audioUrlString?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ?? true)
            k1l0SaveLog("item \(index) video=\(item.videoUrlString) audio=\(hasAudio ? item.audioUrlString ?? "" : "none") overlay=\(hasOverlay ? "yes" : "no") overlayText=\(item.overlayText)")
        }
        saving = true
        updateSavingStatus(mediaItems.count > 1 ? "downloading chain…" : "downloading…")
        downloadMediaItems(mediaItems) { result in
            switch result {
            case .failure(let error):
                DispatchQueue.main.async {
                    saving = false
                    finishSavingStatus(error.localizedDescription)
                }
            case .success(let localItems):
                let hasOverlay = localItems.contains { !$0.overlayText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
                let hasAudio = localItems.contains { $0.audioURL != nil }
                k1l0SaveLog("downloaded items=\(localItems.count) overlay=\(hasOverlay ? "yes" : "no") audio=\(hasAudio ? "yes" : "no")")
                if localItems.count == 1,
                   localItems[0].audioURL == nil,
                   localItems[0].overlayText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    k1l0SaveLog("saving raw video; no overlay/audio requested")
                    saveLocalVideo(localItems[0].videoURL)
                    return
                }
                if localItems.count == 1 {
                    DispatchQueue.main.async {
                        updateSavingStatus("exporting text \(hasOverlay ? "yes" : "no") audio \(hasAudio ? "yes" : "no")…")
                    }
                    exportSingleVideo(localItems[0]) { exportResult in
                        switch exportResult {
                        case .failure(let error):
                            DispatchQueue.main.async {
                                saving = false
                                finishSavingStatus(error.localizedDescription)
                                k1l0SaveLog("single export failed \(k1l0SaveErrorDescription(error))")
                            }
                        case .success(let outputURL):
                            saveLocalVideo(outputURL)
                        }
                    }
                    return
                }
                DispatchQueue.main.async {
                    updateSavingStatus("exporting chain text \(hasOverlay ? "yes" : "no") audio \(hasAudio ? "yes" : "no")…")
                }
                exportStitchedVideo(localItems) { exportResult in
                    switch exportResult {
                    case .failure(let error):
                        DispatchQueue.main.async {
                            saving = false
                            finishSavingStatus(error.localizedDescription)
                        }
                    case .success(let outputURL):
                        saveLocalVideo(outputURL)
                    }
                }
            }
        }
    }

    private func updateSavingStatus(_ nextStatus: String) {
        status = nextStatus
        K1L0CameraRollSaveStatusOverlay.show(nextStatus)
    }

    private func finishSavingStatus(_ nextStatus: String) {
        status = nextStatus
        K1L0CameraRollSaveStatusOverlay.show(nextStatus)
        K1L0CameraRollSaveStatusOverlay.hide()
    }

    private func downloadMediaItems(_ items: [CameraRollSaveMediaItem], completion: @escaping (Result<[LocalCameraRollSaveMediaItem], Error>) -> Void) {
        let group = DispatchGroup()
        let lock = NSLock()
        var videoURLs = Array<URL?>(repeating: nil, count: items.count)
        var audioURLs = Array<URL?>(repeating: nil, count: items.count)
        var firstError: String?

        func download(_ urlString: String, fallbackExtension: String, completion: @escaping (URL?) -> Void) {
            guard let remoteURL = URL(string: urlString) else {
                completion(nil)
                return
            }
            URLSession.shared.downloadTask(with: remoteURL) { localURL, _, error in
                guard error == nil, let localURL else {
                    completion(nil)
                    return
                }
                let pathExtension = remoteURL.pathExtension.isEmpty ? fallbackExtension : remoteURL.pathExtension
                let target = URL(fileURLWithPath: NSTemporaryDirectory())
                    .appendingPathComponent("k1l0-save-\(UUID().uuidString).\(pathExtension)")
                do {
                    try? FileManager.default.removeItem(at: target)
                    try FileManager.default.copyItem(at: localURL, to: target)
                    completion(target)
                } catch {
                    completion(nil)
                }
            }.resume()
        }

        for (index, item) in items.enumerated() {
            group.enter()
                download(item.videoUrlString, fallbackExtension: "mp4") { localURL in
                    lock.lock()
                    if let localURL {
                        videoURLs[index] = localURL
                        k1l0SaveLog("video download ok index=\(index) local=\(localURL.path)")
                    } else if firstError == nil {
                        k1l0SaveLog("video download failed index=\(index) url=\(item.videoUrlString)")
                        firstError = "video download failed"
                    }
                lock.unlock()
                group.leave()
            }

            if let audioUrlString = item.audioUrlString?.trimmingCharacters(in: .whitespacesAndNewlines), !audioUrlString.isEmpty {
                group.enter()
                download(audioUrlString, fallbackExtension: "m4a") { localURL in
                    if let localURL {
                        k1l0SaveLog("audio download ok index=\(index) local=\(localURL.path)")
                        prepareAudioForMux(localURL) { preparedURL in
                            lock.lock()
                            if let preparedURL {
                                audioURLs[index] = preparedURL
                                k1l0SaveLog("audio prepared ok index=\(index) local=\(preparedURL.path)")
                            } else if firstError == nil {
                                k1l0SaveLog("audio prepare failed index=\(index) url=\(audioUrlString)")
                                firstError = "audio prepare failed"
                            }
                            lock.unlock()
                            group.leave()
                        }
                    } else {
                        lock.lock()
                        if firstError == nil {
                            k1l0SaveLog("audio download failed index=\(index) url=\(audioUrlString)")
                            firstError = "audio download failed"
                        }
                        lock.unlock()
                        group.leave()
                    }
                }
            }
        }

        group.notify(queue: .main) {
            if let firstError {
                completion(.failure(cameraRollSaveError(firstError)))
                return
            }
            let localItems = videoURLs.enumerated().compactMap { index, videoURL -> LocalCameraRollSaveMediaItem? in
                guard let videoURL else { return nil }
                return LocalCameraRollSaveMediaItem(videoURL: videoURL, audioURL: audioURLs[index], overlayText: items[index].overlayText, overlayTransform: items[index].overlayTransform)
            }
            guard localItems.count == items.count else {
                completion(.failure(cameraRollSaveError("video download failed")))
                return
            }
            completion(.success(localItems))
        }
    }

    private func prepareAudioForMux(_ localURL: URL, completion: @escaping (URL?) -> Void) {
        let pathExtension = localURL.pathExtension.lowercased()
        guard pathExtension == "mp3" else {
            completion(localURL)
            return
        }
        let asset = AVURLAsset(url: localURL)
        let outputURL = URL(fileURLWithPath: NSTemporaryDirectory())
            .appendingPathComponent("k1l0-audio-\(UUID().uuidString).m4a")
        try? FileManager.default.removeItem(at: outputURL)
        guard let exporter = AVAssetExportSession(asset: asset, presetName: AVAssetExportPresetAppleM4A) else {
            k1l0SaveLog("audio transcode unavailable input=\(localURL.path)")
            completion(nil)
            return
        }
        exporter.outputURL = outputURL
        exporter.outputFileType = .m4a
        exporter.exportAsynchronously {
            DispatchQueue.main.async {
                switch exporter.status {
                case .completed:
                    k1l0SaveLog("audio transcode completed output=\(outputURL.path)")
                    completion(outputURL)
                case .failed, .cancelled:
                    let exportError = exporter.error.map { k1l0SaveErrorDescription($0) } ?? "unknown"
                    k1l0SaveLog("audio transcode failed \(exportError)")
                    completion(nil)
                default:
                    k1l0SaveLog("audio transcode failed status=\(exporter.status.rawValue)")
                    completion(nil)
                }
            }
        }
    }

    private func exportSingleVideo(_ item: LocalCameraRollSaveMediaItem, completion: @escaping (Result<URL, Error>) -> Void) {
        DispatchQueue.global(qos: .userInitiated).async {
            // Saved transmissions mirror the in-app player: the clip loops to
            // fill 15 seconds, the music rides the whole way, and the glitch
            // FX are baked in (pass 1), then the plot/options text is burned
            // on top (pass 2 — the CI filter handler and the Core Animation
            // overlay can't share one AVVideoComposition).
            let targetDuration = CMTime(seconds: 15, preferredTimescale: 600)
            let composition = AVMutableComposition()
            let videoAsset = AVURLAsset(url: item.videoURL)
            guard let sourceVideoTrack = videoAsset.tracks(withMediaType: .video).first,
                  let compositionVideoTrack = composition.addMutableTrack(
                    withMediaType: .video,
                    preferredTrackID: kCMPersistentTrackID_Invalid
                  ) else {
                DispatchQueue.main.async { completion(.failure(cameraRollSaveError("missing video track"))) }
                return
            }
            compositionVideoTrack.preferredTransform = sourceVideoTrack.preferredTransform

            do {
                let videoRange = sourceVideoTrack.timeRange
                guard videoRange.duration.seconds > 0.05 else {
                    throw NSError(domain: "K1L0Save", code: 3, userInfo: [NSLocalizedDescriptionKey: "empty video track"])
                }
                var cursor = CMTime.zero
                while cursor < targetDuration {
                    let insert = CMTimeMinimum(videoRange.duration, targetDuration - cursor)
                    try compositionVideoTrack.insertTimeRange(
                        CMTimeRange(start: videoRange.start, duration: insert),
                        of: sourceVideoTrack,
                        at: cursor
                    )
                    cursor = cursor + insert
                }
                k1l0SaveLog("single video looped to \(cursor.seconds)s from \(videoRange.duration.seconds)s source")

                if let audioURL = item.audioURL,
                   let compositionAudioTrack = composition.addMutableTrack(withMediaType: .audio, preferredTrackID: kCMPersistentTrackID_Invalid) {
                    let sourceAudioAsset = AVURLAsset(url: audioURL)
                    guard let sourceAudioTrack = sourceAudioAsset.tracks(withMediaType: .audio).first else {
                        throw NSError(domain: "K1L0Save", code: 2, userInfo: [NSLocalizedDescriptionKey: "missing audio track"])
                    }
                    let audioRange = sourceAudioTrack.timeRange
                    var audioCursor = CMTime.zero
                    while audioCursor < cursor, audioRange.duration.seconds > 0.05 {
                        let insert = CMTimeMinimum(audioRange.duration, cursor - audioCursor)
                        try compositionAudioTrack.insertTimeRange(
                            CMTimeRange(start: audioRange.start, duration: insert),
                            of: sourceAudioTrack,
                            at: audioCursor
                        )
                        audioCursor = audioCursor + insert
                    }
                    k1l0SaveLog("single audio looped to \(audioCursor.seconds)s")
                }
            } catch {
                k1l0SaveLog("mux failed \(k1l0SaveErrorDescription(error))")
                DispatchQueue.main.async { completion(.failure(cameraRollSaveError("mux failed: \(k1l0SaveErrorDescription(error))"))) }
                return
            }

            let totalRange = CMTimeRange(start: .zero, duration: compositionVideoTrack.timeRange.duration)

            if let fxComposition = K1L0TransmissionFX.bakedComposition(for: composition, durationSeconds: totalRange.duration.seconds) {
                self.exportComposition(
                    composition,
                    filenamePrefix: "k1l0-fxbake",
                    videoComposition: fxComposition,
                    timeRange: totalRange
                ) { fxResult in
                    switch fxResult {
                    case .success(let fxURL):
                        self.exportTextOverlayPass(sourceURL: fxURL, item: item, deleteSourceWhenDone: true, completion: completion)
                    case .failure(let error):
                        // FX bake failed — fall back to the clean text-only export.
                        k1l0SaveLog("fx bake failed, exporting clean: \(k1l0SaveErrorDescription(error))")
                        self.exportPlainTextOverlay(composition: composition,
                                                    compositionVideoTrack: compositionVideoTrack,
                                                    sourceVideoTrack: sourceVideoTrack,
                                                    item: item,
                                                    completion: completion)
                    }
                }
            } else {
                self.exportPlainTextOverlay(composition: composition,
                                            compositionVideoTrack: compositionVideoTrack,
                                            sourceVideoTrack: sourceVideoTrack,
                                            item: item,
                                            completion: completion)
            }
        }
    }

    // Pass 2 when FX baking succeeded: wrap the FX'd temp file in a fresh
    // composition and burn the plot/options overlay onto it.
    private func exportTextOverlayPass(sourceURL: URL, item: LocalCameraRollSaveMediaItem, deleteSourceWhenDone: Bool, completion: @escaping (Result<URL, Error>) -> Void) {
        let asset = AVURLAsset(url: sourceURL)
        let composition = AVMutableComposition()
        guard let sourceVideoTrack = asset.tracks(withMediaType: .video).first,
              let compositionVideoTrack = composition.addMutableTrack(withMediaType: .video, preferredTrackID: kCMPersistentTrackID_Invalid) else {
            if deleteSourceWhenDone { try? FileManager.default.removeItem(at: sourceURL) }
            DispatchQueue.main.async { completion(.failure(cameraRollSaveError("fx pass missing video track"))) }
            return
        }
        do {
            try compositionVideoTrack.insertTimeRange(sourceVideoTrack.timeRange, of: sourceVideoTrack, at: .zero)
            compositionVideoTrack.preferredTransform = sourceVideoTrack.preferredTransform
            if let sourceAudioTrack = asset.tracks(withMediaType: .audio).first,
               let compositionAudioTrack = composition.addMutableTrack(withMediaType: .audio, preferredTrackID: kCMPersistentTrackID_Invalid) {
                try compositionAudioTrack.insertTimeRange(sourceAudioTrack.timeRange, of: sourceAudioTrack, at: .zero)
            }
        } catch {
            if deleteSourceWhenDone { try? FileManager.default.removeItem(at: sourceURL) }
            DispatchQueue.main.async { completion(.failure(cameraRollSaveError("fx pass mux failed: \(k1l0SaveErrorDescription(error))"))) }
            return
        }
        let videoComposition = makeVideoComposition(
            compositionTrack: compositionVideoTrack,
            sourceTrack: sourceVideoTrack,
            duration: compositionVideoTrack.timeRange.duration,
            renderSize: normalizedRenderSize(for: sourceVideoTrack),
            overlayText: item.overlayText,
            overlayTransform: item.overlayTransform
        )
        exportComposition(
            composition,
            filenamePrefix: "k1l0-single",
            videoComposition: videoComposition,
            timeRange: CMTimeRange(start: .zero, duration: compositionVideoTrack.timeRange.duration)
        ) { result in
            if deleteSourceWhenDone { try? FileManager.default.removeItem(at: sourceURL) }
            completion(result)
        }
    }

    // Single-pass fallback: looped video + text overlay, no FX bake.
    private func exportPlainTextOverlay(composition: AVMutableComposition,
                                        compositionVideoTrack: AVMutableCompositionTrack,
                                        sourceVideoTrack: AVAssetTrack,
                                        item: LocalCameraRollSaveMediaItem,
                                        completion: @escaping (Result<URL, Error>) -> Void) {
        let videoComposition = makeVideoComposition(
            compositionTrack: compositionVideoTrack,
            sourceTrack: sourceVideoTrack,
            duration: compositionVideoTrack.timeRange.duration,
            renderSize: normalizedRenderSize(for: sourceVideoTrack),
            overlayText: item.overlayText,
            overlayTransform: item.overlayTransform
        )
        exportComposition(
            composition,
            filenamePrefix: "k1l0-single",
            videoComposition: videoComposition,
            timeRange: CMTimeRange(start: .zero, duration: compositionVideoTrack.timeRange.duration),
            completion: completion
        )
    }

    private func exportStitchedVideo(_ items: [LocalCameraRollSaveMediaItem], completion: @escaping (Result<URL, Error>) -> Void) {
        DispatchQueue.global(qos: .userInitiated).async {
            let composition = AVMutableComposition()
            guard let compositionVideoTrack = composition.addMutableTrack(
                withMediaType: .video,
                preferredTrackID: kCMPersistentTrackID_Invalid
            ) else {
                DispatchQueue.main.async { completion(.failure(cameraRollSaveError("stitch failed"))) }
                return
            }
            let compositionAudioTrack = composition.addMutableTrack(
                withMediaType: .audio,
                preferredTrackID: kCMPersistentTrackID_Invalid
            )
            var cursor = CMTime.zero
            var didSetTransform = false
            var firstSourceTrack: AVAssetTrack?
            var renderSize = CGSize(width: 1080, height: 1920)
            var overlaySegments: [(text: String, start: CMTime, duration: CMTime, transform: TransmissionTextTransform)] = []
            let chainAudioURL = items.first?.audioURL

            do {
                for item in items {
                    let videoAsset = AVURLAsset(url: item.videoURL)
                    guard let sourceVideoTrack = videoAsset.tracks(withMediaType: .video).first else {
                        throw NSError(domain: "K1L0Save", code: 1, userInfo: [NSLocalizedDescriptionKey: "missing video track"])
                    }
                    let videoRange = sourceVideoTrack.timeRange
                    let duration = videoRange.duration
                    k1l0SaveLog("chain video insert start=\(videoRange.start.seconds) duration=\(duration.seconds) assetDuration=\(videoAsset.duration.seconds)")
                    try compositionVideoTrack.insertTimeRange(
                        videoRange,
                        of: sourceVideoTrack,
                        at: cursor
                    )
                    if !didSetTransform {
                        compositionVideoTrack.preferredTransform = sourceVideoTrack.preferredTransform
                        firstSourceTrack = sourceVideoTrack
                        renderSize = normalizedRenderSize(for: sourceVideoTrack)
                        didSetTransform = true
                    }
                    let cleanOverlay = item.overlayText.trimmingCharacters(in: .whitespacesAndNewlines)
                    if !cleanOverlay.isEmpty {
                        overlaySegments.append((text: cleanOverlay, start: cursor, duration: duration, transform: item.overlayTransform))
                    }

                    if chainAudioURL == nil, let audioURL = item.audioURL {
                        let sourceAudioAsset = AVURLAsset(url: audioURL)
                        guard let sourceAudioTrack = sourceAudioAsset.tracks(withMediaType: .audio).first else {
                            throw NSError(domain: "K1L0Save", code: 2, userInfo: [NSLocalizedDescriptionKey: "missing audio track"])
                        }
                        let sourceAudioRange = sourceAudioTrack.timeRange
                        let muxDuration = CMTimeMinimum(duration, sourceAudioRange.duration)
                        k1l0SaveLog("chain audio insert start=\(sourceAudioRange.start.seconds) duration=\(muxDuration.seconds) trackDuration=\(sourceAudioRange.duration.seconds)")
                        try compositionAudioTrack?.insertTimeRange(
                            CMTimeRange(start: sourceAudioRange.start, duration: muxDuration),
                            of: sourceAudioTrack,
                            at: cursor
                        )
                    } else if chainAudioURL == nil, let embeddedAudioTrack = videoAsset.tracks(withMediaType: .audio).first {
                        let embeddedRange = embeddedAudioTrack.timeRange
                        try compositionAudioTrack?.insertTimeRange(
                            CMTimeRange(start: embeddedRange.start, duration: CMTimeMinimum(duration, embeddedRange.duration)),
                            of: embeddedAudioTrack,
                            at: cursor
                        )
                    }
                    cursor = cursor + duration
                }
                if let chainAudioURL,
                   let compositionAudioTrack {
                    let sourceAudioAsset = AVURLAsset(url: chainAudioURL)
                    guard let sourceAudioTrack = sourceAudioAsset.tracks(withMediaType: .audio).first else {
                        throw NSError(domain: "K1L0Save", code: 2, userInfo: [NSLocalizedDescriptionKey: "missing original audio track"])
                    }
                    let sourceAudioRange = sourceAudioTrack.timeRange
                    guard sourceAudioRange.duration.seconds.isFinite, sourceAudioRange.duration > .zero else {
                        throw NSError(domain: "K1L0Save", code: 2, userInfo: [NSLocalizedDescriptionKey: "bad original audio duration"])
                    }
                    var audioCursor = CMTime.zero
                    while audioCursor < cursor {
                        let remaining = cursor - audioCursor
                        let muxDuration = CMTimeMinimum(sourceAudioRange.duration, remaining)
                        k1l0SaveLog("chain original audio insert start=\(sourceAudioRange.start.seconds) duration=\(muxDuration.seconds) at=\(audioCursor.seconds)")
                        try compositionAudioTrack.insertTimeRange(
                            CMTimeRange(start: sourceAudioRange.start, duration: muxDuration),
                            of: sourceAudioTrack,
                            at: audioCursor
                        )
                        audioCursor = audioCursor + muxDuration
                    }
                }
            } catch {
                DispatchQueue.main.async { completion(.failure(cameraRollSaveError("stitch failed: \(error.localizedDescription)"))) }
                return
            }

            let videoComposition: AVVideoComposition?
            if let firstSourceTrack {
                videoComposition = makeVideoComposition(
                    compositionTrack: compositionVideoTrack,
                    sourceTrack: firstSourceTrack,
                    duration: cursor,
                    renderSize: renderSize,
                    overlaySegments: overlaySegments
                )
            } else {
                videoComposition = nil
            }
            exportComposition(composition, filenamePrefix: "k1l0-chain", videoComposition: videoComposition, timeRange: nil, completion: completion)
        }
    }

    private func normalizedRenderSize(for track: AVAssetTrack) -> CGSize {
        let transformed = track.naturalSize.applying(track.preferredTransform)
        let width = abs(transformed.width)
        let height = abs(transformed.height)
        if width > 0, height > 0 {
            return CGSize(width: width, height: height)
        }
        return track.naturalSize
    }

    private func makeVideoComposition(
        compositionTrack: AVCompositionTrack,
        sourceTrack: AVAssetTrack,
        duration: CMTime,
        renderSize: CGSize,
        overlayText: String,
        overlayTransform: TransmissionTextTransform = TransmissionTextTransform()
    ) -> AVMutableVideoComposition? {
        let cleanText = overlayText.trimmingCharacters(in: .whitespacesAndNewlines)
        return makeVideoComposition(
            compositionTrack: compositionTrack,
            sourceTrack: sourceTrack,
            duration: duration,
            renderSize: renderSize,
            overlaySegments: cleanText.isEmpty ? [] : [(text: cleanText, start: .zero, duration: duration, transform: overlayTransform)]
        )
    }

    private func makeVideoComposition(
        compositionTrack: AVCompositionTrack,
        sourceTrack: AVAssetTrack,
        duration: CMTime,
        renderSize: CGSize,
        overlaySegments: [(text: String, start: CMTime, duration: CMTime, transform: TransmissionTextTransform)]
    ) -> AVMutableVideoComposition? {
        let videoComposition = AVMutableVideoComposition()
        videoComposition.renderSize = renderSize
        videoComposition.frameDuration = CMTime(value: 1, timescale: 30)

        let instruction = AVMutableVideoCompositionInstruction()
        instruction.timeRange = CMTimeRange(start: .zero, duration: duration)
        let layerInstruction = AVMutableVideoCompositionLayerInstruction(assetTrack: compositionTrack)
        layerInstruction.setTransform(sourceTrack.preferredTransform, at: .zero)
        instruction.layerInstructions = [layerInstruction]
        videoComposition.instructions = [instruction]

        let cleanSegments = overlaySegments
            .map { (text: $0.text.trimmingCharacters(in: .whitespacesAndNewlines), start: $0.start, duration: $0.duration, transform: $0.transform.clamped()) }
            .filter { !$0.text.isEmpty }
        guard !cleanSegments.isEmpty else { return videoComposition }

        let parentLayer = CALayer()
        parentLayer.frame = CGRect(origin: .zero, size: renderSize)
        let videoLayer = CALayer()
        videoLayer.frame = parentLayer.frame
        parentLayer.addSublayer(videoLayer)

        for segment in cleanSegments {
            let segmentLayer = CALayer()
            segmentLayer.frame = parentLayer.frame
            applyTimedOpacity(to: segmentLayer, start: segment.start, duration: segment.duration)
            parentLayer.addSublayer(segmentLayer)

            let (plotText, optionText) = splitOverlayText(segment.text)
            let fontSize = max(17, min(34, renderSize.width * 0.030 * CGFloat(segment.transform.scale)))
            let optionsFrame = optionText.isEmpty ? nil : optionsOverlayFrame(text: optionText, renderSize: renderSize)
            // Core Animation layers in the export composition are bottom-left
            // origin — the text block anchors just above the pill row.
            let textFrame = overlayFrame(
                text: plotText,
                transform: segment.transform,
                renderSize: renderSize,
                fontSize: fontSize,
                bottomAnchorY: optionsFrame.map { $0.maxY + max(8, renderSize.width * 0.018) }
            )
            if !plotText.isEmpty {
                addTypewriterTextLayers(
                    text: plotText,
                    frame: textFrame,
                    fontSize: fontSize,
                    rotationDegrees: segment.transform.rotationDegrees,
                    start: segment.start,
                    duration: segment.duration,
                    parentLayer: segmentLayer
                )
            }
            if !optionText.isEmpty {
                addOptionsGridLayer(
                    text: optionText,
                    frame: optionsFrame ?? optionsOverlayFrame(text: optionText, renderSize: renderSize),
                    renderSize: renderSize,
                    start: segment.start,
                    duration: segment.duration,
                    parentLayer: segmentLayer
                )
            }
        }

        videoComposition.animationTool = AVVideoCompositionCoreAnimationTool(
            postProcessingAsVideoLayer: videoLayer,
            in: parentLayer
        )
        return videoComposition
    }

    private func overlayFrame(
        text: String,
        transform: TransmissionTextTransform,
        renderSize: CGSize,
        fontSize: CGFloat,
        bottomAnchorY: CGFloat? = nil
    ) -> CGRect {
        // Bottom-left origin (Core Animation export space): y is the frame's
        // BOTTOM edge. Anchored just above the pills, or near the video's
        // bottom when there are no options.
        let scale = max(0.6, renderSize.width / 390.0)
        let width = min(renderSize.width * 0.94, max(renderSize.width * 0.60, renderSize.width - (24 * scale)))
        let attributes: [NSAttributedString.Key: Any] = [
            .font: UIFont.monospacedSystemFont(ofSize: fontSize, weight: .black)
        ]
        let maxHeight = min(renderSize.height * 0.28, 166 * scale)
        let measured = (text as NSString).boundingRect(
            with: CGSize(width: width - 28, height: renderSize.height * 0.82),
            options: [.usesLineFragmentOrigin, .usesFontLeading],
            attributes: attributes,
            context: nil
        )
        let height = min(maxHeight, max(fontSize * 2.4, measured.height + 32))
        let anchorY = bottomAnchorY ?? (36 * scale)
        return CGRect(
            x: (renderSize.width - width) * 0.5,
            y: min(renderSize.height - height - 10, anchorY),
            width: width,
            height: height
        )
    }

    private func splitOverlayText(_ text: String) -> (plot: String, options: String) {
        let parts = text
            .components(separatedBy: .newlines)
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
        var plotLines: [String] = []
        var optionLines: [String] = []
        for part in parts {
            if part.hasPrefix("[") || part.contains("]  [") {
                optionLines.append(part)
            } else {
                plotLines.append(part)
            }
        }
        return (plotLines.joined(separator: "\n"), optionLines.joined(separator: "  "))
    }

    private struct OptionsPillLayout {
        let options: [String]
        let widths: [CGFloat]
        let rows: [[Int]]
        let fontSize: CGFloat
        let pillHeight: CGFloat
        let gap: CGFloat
        let totalHeight: CGFloat
    }

    // Parse "[ SMASH THE GRATE ]  [ LEAVE ]" into individual options and lay
    // them out as centered pill rows that wrap within maxWidth.
    private func optionsPillLayout(text: String, renderSize: CGSize) -> OptionsPillLayout? {
        var options: [String] = []
        var remainder = text[...]
        while let open = remainder.firstIndex(of: "["), let close = remainder[open...].firstIndex(of: "]") {
            let inner = remainder[remainder.index(after: open)..<close].trimmingCharacters(in: .whitespacesAndNewlines)
            if !inner.isEmpty { options.append(inner) }
            remainder = remainder[remainder.index(after: close)...]
        }
        if options.isEmpty {
            let flat = text.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !flat.isEmpty else { return nil }
            options = [flat]
        }
        let fontSize = max(13, min(22, renderSize.width * 0.019))
        let font = UIFont.monospacedSystemFont(ofSize: fontSize, weight: .black)
        let maxWidth = renderSize.width * 0.88
        let padH = fontSize * 0.9
        let pillHeight = fontSize * 2.1
        let gap = fontSize * 0.55
        let widths = options.map { min(maxWidth, ($0 as NSString).size(withAttributes: [.font: font]).width + padH * 2) }
        var rows: [[Int]] = [[]]
        var rowWidth: CGFloat = 0
        for (index, width) in widths.enumerated() {
            let needed = rows[rows.count - 1].isEmpty ? width : rowWidth + gap + width
            if needed > maxWidth && !rows[rows.count - 1].isEmpty {
                rows.append([index])
                rowWidth = width
            } else {
                rows[rows.count - 1].append(index)
                rowWidth = needed
            }
        }
        let totalHeight = CGFloat(rows.count) * pillHeight + CGFloat(max(0, rows.count - 1)) * gap
        return OptionsPillLayout(options: options, widths: widths, rows: rows,
                                 fontSize: fontSize, pillHeight: pillHeight, gap: gap, totalHeight: totalHeight)
    }

    private func addOptionsGridLayer(
        text: String,
        frame: CGRect,
        renderSize: CGSize,
        start: CMTime,
        duration: CMTime,
        parentLayer: CALayer
    ) {
        guard let layout = optionsPillLayout(text: text, renderSize: renderSize) else { return }
        let font = UIFont.monospacedSystemFont(ofSize: layout.fontSize, weight: .black)
        // Bottom-left origin: row 0 sits at the bottom of the frame.
        for (rowIndex, row) in layout.rows.enumerated() {
            let rowWidth = row.reduce(CGFloat(0)) { $0 + layout.widths[$1] } + layout.gap * CGFloat(max(0, row.count - 1))
            var x = frame.midX - rowWidth / 2
            let y = frame.minY + CGFloat(rowIndex) * (layout.pillHeight + layout.gap)
            for index in row {
                let pill = CALayer()
                pill.frame = CGRect(x: x, y: y, width: layout.widths[index], height: layout.pillHeight)
                pill.backgroundColor = UIColor(white: 0.20, alpha: 0.85).cgColor
                pill.cornerRadius = layout.pillHeight / 2
                pill.borderColor = UIColor(white: 1.0, alpha: 0.16).cgColor
                pill.borderWidth = 1
                applyTimedOpacity(to: pill, start: start, duration: duration)
                parentLayer.addSublayer(pill)

                let label = CATextLayer()
                label.string = layout.options[index]
                label.contentsScale = UIScreen.main.scale
                label.alignmentMode = .center
                label.truncationMode = .end
                label.foregroundColor = UIColor.white.withAlphaComponent(0.92).cgColor
                label.font = font
                label.fontSize = layout.fontSize
                let textHeight = layout.fontSize * 1.35
                label.frame = CGRect(
                    x: x,
                    y: y + (layout.pillHeight - textHeight) / 2,
                    width: layout.widths[index],
                    height: textHeight
                )
                applyTimedOpacity(to: label, start: start, duration: duration)
                parentLayer.addSublayer(label)

                x += layout.widths[index] + layout.gap
            }
        }
    }

    private func optionsOverlayFrame(text: String, renderSize: CGSize) -> CGRect {
        // Bottom-left origin: the pill rows hug the very bottom of the video.
        let scale = max(0.6, renderSize.width / 390.0)
        let width = renderSize.width * 0.88
        let height = optionsPillLayout(text: text, renderSize: renderSize)?.totalHeight ?? 0
        let bottomInset = 36 * scale
        return CGRect(
            x: (renderSize.width - width) * 0.5,
            y: bottomInset,
            width: width,
            height: height
        )
    }

    private func addTypewriterTextLayers(
        text: String,
        frame: CGRect,
        fontSize: CGFloat,
        rotationDegrees: Double,
        start: CMTime,
        duration: CMTime,
        parentLayer: CALayer
    ) {
        let characters = Array(text)
        guard !characters.isEmpty else { return }
        let revealDuration = min(max(Double(characters.count) * 0.045, 0.65), max(0.65, duration.seconds * 0.55))
        let characterInterval = revealDuration / Double(characters.count)

        for characterIndex in characters.indices {
            let textLayer = CATextLayer()
            textLayer.string = String(characters[...characterIndex])
            textLayer.contentsScale = UIScreen.main.scale
            textLayer.alignmentMode = .left
            textLayer.isWrapped = true
            textLayer.truncationMode = .end
            textLayer.foregroundColor = UIColor.white.cgColor
            textLayer.shadowColor = UIColor.black.cgColor
            textLayer.shadowOpacity = 0.95
            textLayer.shadowRadius = 10
            textLayer.shadowOffset = CGSize(width: 0, height: 2)
            textLayer.font = UIFont.monospacedSystemFont(ofSize: fontSize, weight: .black)
            textLayer.fontSize = fontSize
            textLayer.frame = frame
            if abs(rotationDegrees) > 0.1 {
                textLayer.setAffineTransform(CGAffineTransform(rotationAngle: CGFloat(rotationDegrees * .pi / 180)))
            }

            let visibleStart = start.seconds + Double(characterIndex) * characterInterval
            let visibleEnd: Double
            if characterIndex == characters.index(before: characters.endIndex) {
                visibleEnd = start.seconds + max(revealDuration, duration.seconds - 0.15)
            } else {
                visibleEnd = start.seconds + Double(characterIndex + 1) * characterInterval
            }
            applyTimedOpacity(
                to: textLayer,
                startSeconds: visibleStart,
                endSeconds: visibleEnd,
                totalDuration: duration.seconds
            )
            parentLayer.addSublayer(textLayer)
        }
    }

    private func applyTimedOpacity(to layer: CALayer, start: CMTime, duration: CMTime) {
        layer.opacity = 0
        let animation = CAKeyframeAnimation(keyPath: "opacity")
        animation.values = [0, 1, 1, 0]
        animation.keyTimes = [0, 0.001, 0.999, 1]
        animation.beginTime = AVCoreAnimationBeginTimeAtZero + max(0, start.seconds)
        animation.duration = max(0.1, duration.seconds)
        animation.fillMode = .both
        animation.isRemovedOnCompletion = false
        layer.add(animation, forKey: "k1l0TimedOpacity")
    }

    private func applyTimedOpacity(to layer: CALayer, startSeconds: Double, endSeconds: Double, totalDuration: Double) {
        layer.opacity = 0
        let animation = CAKeyframeAnimation(keyPath: "opacity")
        animation.values = [0, 1, 1, 0]
        animation.keyTimes = [0, 0.001, 0.999, 1]
        animation.beginTime = AVCoreAnimationBeginTimeAtZero + max(0, startSeconds)
        animation.duration = max(0.04, endSeconds - startSeconds)
        animation.fillMode = .both
        animation.isRemovedOnCompletion = false
        layer.add(animation, forKey: "k1l0TypewriterOpacity")
    }

    private func exportComposition(
        _ composition: AVMutableComposition,
        filenamePrefix: String,
        videoComposition: AVVideoComposition?,
        timeRange: CMTimeRange?,
        completion: @escaping (Result<URL, Error>) -> Void
    ) {
        let outputURL = URL(fileURLWithPath: NSTemporaryDirectory())
            .appendingPathComponent("\(filenamePrefix)-\(UUID().uuidString).mp4")
        try? FileManager.default.removeItem(at: outputURL)
        guard let exporter = AVAssetExportSession(asset: composition, presetName: AVAssetExportPresetHighestQuality) else {
            DispatchQueue.main.async { completion(.failure(cameraRollSaveError("export unavailable"))) }
            return
        }
        exporter.outputURL = outputURL
        exporter.outputFileType = .mp4
        exporter.shouldOptimizeForNetworkUse = true
        exporter.videoComposition = videoComposition
        if let timeRange {
            exporter.timeRange = timeRange
        }
        let durationSeconds = composition.duration.seconds
        NSLog("K1L0Save export start prefix=%@ videoComposition=%@", filenamePrefix, videoComposition == nil ? "no" : "yes")
        k1l0SaveLog("export start prefix=\(filenamePrefix) videoComposition=\(videoComposition == nil ? "no" : "yes") duration=\(durationSeconds.isFinite ? durationSeconds : -1)")
        var didFinish = false
        let timeout = DispatchWorkItem {
            guard !didFinish else { return }
            didFinish = true
            let progress = exporter.progress
            exporter.cancelExport()
            let message = "export timeout prefix=\(filenamePrefix) progress=\(progress)"
            NSLog("K1L0Save %@", message)
            k1l0SaveLog(message)
            completion(.failure(cameraRollSaveError("export timed out")))
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 90, execute: timeout)
        exporter.exportAsynchronously {
            DispatchQueue.main.async {
                guard !didFinish else { return }
                didFinish = true
                timeout.cancel()
                switch exporter.status {
                case .completed:
                    NSLog("K1L0Save export completed %@", outputURL.absoluteString)
                    k1l0SaveLog("export completed output=\(outputURL.path)")
                    completion(.success(outputURL))
                case .failed, .cancelled:
                    let exportError = exporter.error.map { k1l0SaveErrorDescription($0) } ?? "unknown"
                    NSLog("K1L0Save export failed %@", exportError)
                    k1l0SaveLog("export failed \(exportError)")
                    completion(.failure(cameraRollSaveError("export failed: \(exportError)")))
                default:
                    NSLog("K1L0Save export failed status=%ld", exporter.status.rawValue)
                    completion(.failure(cameraRollSaveError("export failed")))
                }
            }
        }
    }

    private func saveLocalVideo(_ localURL: URL) {
        DispatchQueue.main.async {
            updateSavingStatus("saving…")
            NSLog("K1L0Save photos save start %@", localURL.absoluteString)
            let delegate = K1L0CameraRollSaveDelegate { nextStatus in
                saving = false
                finishSavingStatus(nextStatus)
                NSLog("K1L0Save photos save status %@", nextStatus)
                saveDelegate = nil
            }
            saveDelegate = delegate
            UISaveVideoAtPathToSavedPhotosAlbum(
                localURL.path,
                delegate,
                #selector(K1L0CameraRollSaveDelegate.video(_:didFinishSavingWithError:contextInfo:)),
                nil
            )
        }
    }
}

// Wraps the source type so sheet(item:) can carry it — eliminates the
// sheet(isPresented:) race where the source mutation and the show flag fire
// in the same SwiftUI batch and the old source value gets captured.
struct PhotoPickerRequest: Identifiable {
    let id = UUID()
    let source: UIImagePickerController.SourceType
}

struct NativePhotoPicker: UIViewControllerRepresentable {
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

#if canImport(AppKit)
struct PhotoPickerRequest: Identifiable {
    let id = UUID()
    let source: UIImagePickerController.SourceType
}

/// Mac placeholder for the iOS camera sheet. The existing AppKit file-picker
/// flows remain available elsewhere in the overlay; this preserves the shared
/// response composer without pulling UIKit into the native Mac bundle.
struct NativePhotoPicker: View {
    let sourceType: UIImagePickerController.SourceType
    let onComplete: (NSImage?, String?) -> Void

    var body: some View {
        Color.clear
            .frame(width: 1, height: 1)
            .onAppear { onComplete(nil, nil) }
    }
}
#endif

struct StepStatBlock: View {
    let label: String
    let value: Int

    var body: some View {
        VStack(alignment: .leading, spacing: 1) {
            Text(K1L0StepValueText(value))
                .font(.system(size: 24, weight: .bold))
                .monospacedDigit()
                .foregroundStyle(.white)
            Text(label)
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(.white.opacity(0.58))
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

struct StepLeaderboardSection: View {
    let title: String
    let leaders: [OverlayStepLeader]
    let useWeeklyTotal: Bool
    let onSelectUser: (OverlayUser) -> Void

    @State private var isExpanded: Bool = false

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            if !title.isEmpty {
                HStack {
                    Text(title)
                        .font(.system(size: 11, weight: .black, design: .rounded))
                        .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                    Spacer()
                    Text("steps")
                        .font(.system(size: 11, weight: .light, design: .rounded))
                        .foregroundStyle(.white.opacity(0.4))
                }
            }
            
            let displayCount = isExpanded ? 10 : 5
            ForEach(Array(leaders.prefix(displayCount).enumerated()), id: \.element.id) { index, leader in
                HStack(spacing: 9) {
                    Text("\(index + 1)")
                        .font(.system(size: 12, weight: .black, design: .monospaced))
                        .foregroundStyle(.white.opacity(0.58))
                        .frame(width: 20, alignment: .trailing)
                    K1L0UserAvatar(urlString: leader.helmetUrl, size: 28, userId: leader.userId)
                    Text(leader.displayName)
                        .font(.system(size: 13, weight: .semibold))
                        .lineLimit(1)
                    Spacer()
                    Text(K1L0StepValueText(useWeeklyTotal ? leader.steps7d : leader.steps24h))
                        .font(.system(size: 13, weight: .black, design: .monospaced))
                }
                .contentShape(Rectangle())
                .onTapGesture {
                    let user = OverlayUser(
                        userId: leader.userId,
                        name: leader.name,
                        callsign: leader.callsign,
                        avatarUrl: nil,
                        helmetUrl: leader.helmetUrl,
                        faceUrl: nil,
                        city: nil,
                        lat: nil,
                        lng: nil,
                        lastActive: nil
                    )
                    onSelectUser(user)
                }
            }

            if leaders.count > 5 {
                Button(action: {
                    withAnimation(.easeInOut(duration: 0.2)) {
                        isExpanded.toggle()
                    }
                }) {
                    HStack(spacing: 6) {
                        Text(isExpanded ? "SHOW LESS" : "SHOW MORE")
                        Image(systemName: isExpanded ? "chevron.up" : "chevron.down")
                    }
                    .font(.system(size: 11, weight: .black))
                    .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                    .frame(maxWidth: .infinity, minHeight: 28)
                }
                .buttonStyle(.plain)
            }
        }
    }
}

struct LiveStepStatBlock: View {
    let value: Int
    let durationText: String

    var body: some View {
        VStack(alignment: .center, spacing: 3) {
            Text(K1L0StepValueText(value))
                .font(.system(size: 24, weight: .bold))
                .monospacedDigit()
                .foregroundStyle(.white)
                .lineLimit(1)
                .minimumScaleFactor(0.5)
                .allowsTightening(true)
                .fixedSize(horizontal: true, vertical: false)

            Text("steps")
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(.white.opacity(0.68))
                .frame(maxWidth: .infinity, alignment: .center)
            Text(durationText)
                .font(.system(size: 10, weight: .semibold))
                .foregroundStyle(.white.opacity(0.52))
                .lineLimit(1)
                .minimumScaleFactor(0.78)
                .frame(maxWidth: .infinity, alignment: .center)
        }
        .frame(minWidth: 82, alignment: .center)
    }
}

struct NativeNewsWalkGraph: View {
    let points: [NativeWalkHistoryPoint]
    let tint: Color
    let gridDivisions: Int   // number of interior intervals (e.g. 24 hour buckets, 7 day buckets)
    let majorEvery: Int      // every N divisions draw a brighter major rule (e.g. every 6 hours)

    private var maxSteps: Int {
        max(points.map(\.steps).max() ?? 1, 1)
    }

    var body: some View {
        ZStack {
            RoundedRectangle(cornerRadius: 8, style: .continuous)
                .fill(Color.white.opacity(0.045))
            GridVerticalRules(divisions: gridDivisions, majorEvery: majorEvery)
                .stroke(Color.white.opacity(0.22), lineWidth: 0.5)
                .padding(.horizontal, 7)
                .padding(.vertical, 4)
            GridVerticalRules(divisions: gridDivisions, majorEvery: majorEvery, majorOnly: true)
                .stroke(Color.white.opacity(0.55), lineWidth: 1.0)
                .padding(.horizontal, 7)
                .padding(.vertical, 4)
            NativeWalkLinePath(points: points, maxSteps: maxSteps, plottedWidthRatio: 1.0)
                .stroke(tint, style: StrokeStyle(lineWidth: 1.8, lineCap: .round, lineJoin: .round))
                .padding(.horizontal, 7)
                .padding(.vertical, 8)
        }
        .frame(height: 48)
        .overlay(RoundedRectangle(cornerRadius: 8, style: .continuous).stroke(Color.white.opacity(0.08), lineWidth: 1))
        .frame(maxWidth: .infinity)
    }
}

struct GridVerticalRules: Shape {
    let divisions: Int
    let majorEvery: Int
    var majorOnly: Bool = false

    func path(in rect: CGRect) -> Path {
        var path = Path()
        guard divisions > 1 else { return path }
        for i in 1..<divisions {
            let isMajor = majorEvery > 0 && (i % majorEvery == 0)
            if majorOnly && !isMajor { continue }
            if !majorOnly && isMajor { continue } // skip; major pass draws these
            let x = rect.minX + (CGFloat(i) / CGFloat(divisions)) * rect.width
            path.move(to: CGPoint(x: x, y: rect.minY))
            path.addLine(to: CGPoint(x: x, y: rect.maxY))
        }
        return path
    }
}

struct TopFilterHUDIcons: View {
    @AppStorage(K1L0OverlayDataModel.locationBeamCategoriesKey) private var selectedCategories = "coffee,drinks"

    private let items = [
        ("coffee", "cup.and.saucer.fill"),
        ("drinks", "wineglass.fill"),
    ]

    var body: some View {
        HStack(spacing: 6) {
            ForEach(items, id: \.0) { item in
                filterButton(id: item.0, systemImage: item.1)
            }
        }
        .padding(.horizontal, 7)
        .padding(.vertical, 6)
        .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 25, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 25, style: .continuous)
                .stroke(Color.white.opacity(0.08), lineWidth: 1)
        )
    }

    private func filterButton(id: String, systemImage: String) -> some View {
        let selected = categorySet.contains(id)
        let accent = Color(red: 0.66, green: 1.0, blue: 0.76)
        return Button {
            var updated = categorySet
            if selected {
                if updated.count > 1 { updated.remove(id) }
            } else {
                updated.insert(id)
            }
            selectedCategories = encoded(updated)
        } label: {
            Image(systemName: systemImage)
                .font(.system(size: 16, weight: .bold))
                .foregroundStyle(selected ? accent : Color.white)
                .frame(width: 38, height: 38)
                .background(selected ? accent.opacity(0.18) : Color.white.opacity(0.06), in: Circle())
                .overlay(Circle().stroke(selected ? accent.opacity(0.35) : Color.white.opacity(0.12), lineWidth: 1))
        }
        .buttonStyle(.plain)
    }

    private var categorySet: Set<String> {
        let allowed = Set(items.map { $0.0 })
        let stored = Set(selectedCategories.split(separator: ",").map(String.init))
        let valid = stored.intersection(allowed)
        return valid.isEmpty ? allowed : valid
    }

    private func encoded(_ categories: Set<String>) -> String {
        items.map { $0.0 }.filter { categories.contains($0) }.joined(separator: ",")
    }
}

struct FixedTopStatusHUD: View {
    @ObservedObject var data: K1L0OverlayDataModel
    let settingsActive: Bool
    let hideSteps: Bool
    let weatherLookMode: String
    let onSettingsTapped: () -> Void

    var body: some View {
        ZStack(alignment: .topLeading) {
            HStack(alignment: .top) {
                WeatherPill(model: data, weatherLookMode: weatherLookMode, onSettingsTapped: onSettingsTapped)
                Spacer(minLength: 4)
                VStack(alignment: .trailing, spacing: 2) {
                    if !hideSteps {
                        TopFilterHUDIcons()
                        TopLiveStepsPill(model: data)
                    }
                }
                .padding(.top, 4)
            }
        }
    }
}

struct TopLiveStepsPill: View {
    @ObservedObject var model: K1L0OverlayDataModel

    var body: some View {
        VStack(alignment: .trailing, spacing: 1) {
            Text("\(max(0, model.liveSteps))")
                .font(.system(size: 24, weight: .black, design: .rounded))
                .monospacedDigit()
            Text("steps")
                .font(.system(size: 10, weight: .black, design: .rounded))
                .textCase(.uppercase)
                .foregroundStyle(.white.opacity(0.72))
        }
        .foregroundStyle(.white)
        .padding(.horizontal, 13)
        .padding(.top, 2)
        .padding(.bottom, 10)
        .frame(minWidth: 72, alignment: .trailing)
    }
}

struct WeatherPill: View {
    @ObservedObject var model: K1L0OverlayDataModel
    let weatherLookMode: String
    let onSettingsTapped: () -> Void

    var body: some View {
        Button(action: onSettingsTapped) {
            VStack(alignment: .leading, spacing: 2) {
                if !model.cityText.isEmpty {
                    Text(model.cityText)
                        .font(.system(size: 16, weight: .semibold))
                        .lineLimit(1)
                        .minimumScaleFactor(0.5)
                }
                HStack(spacing: 7) {
                    Image(systemName: model.weatherGlyph)
                        .font(.system(size: 16, weight: .semibold))
                    Text(model.weatherText)
                        .font(.system(size: 16, weight: .semibold))
                }
            }
        }
        .buttonStyle(.plain)
        .foregroundStyle(.white)
        .padding(.leading, 13)
        .padding(.trailing, 4)
        .padding(.vertical, 10)
        .layoutPriority(1)
    }
}

struct DirectionCell: View {
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

struct K1L0UserAvatar: View {
    let urlString: String?
    let size: CGFloat
    var userId: String? = nil
    @State private var resolvedURL: String?

    var body: some View {
        let customHelmet = (resolvedURL ?? urlString ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        let resolvedHelmet = customHelmet.isEmpty ? K1L0DefaultHelmetIconURL : customHelmet
        ZStack {
            Circle()
                .fill(Color.white.opacity(0.10))
            if let url = URL(string: resolvedHelmet) {
                AsyncImage(url: url) { phase in
                    if case .success(let image) = phase {
                        image
                            .resizable()
                            .scaledToFill()
                    } else {
                        Image(systemName: "shield.fill")
                            .font(.system(size: size * 0.42, weight: .bold))
                            .foregroundStyle(.white.opacity(0.72))
                    }
                }
            } else {
                Image(systemName: "shield.fill")
                    .font(.system(size: size * 0.42, weight: .bold))
                    .foregroundStyle(.white.opacity(0.72))
            }
        }
        .frame(width: size, height: size)
        .clipShape(Circle())
        .overlay(Circle().stroke(Color.white.opacity(0.22), lineWidth: 1))
        .onAppear {
            K1L0UserHelmetResolver.resolve(userId: userId, fallbackURL: urlString) { url in
                resolvedURL = url
            }
        }
    }
}

// Shared identity lookup for every compact player portrait. Most lists already
// receive helmetUrl inline; response clips only carry a user ID, so this fills
// that gap through the same metadata endpoint and caches it for the session.
enum K1L0UserHelmetResolver {
    private static var cache: [String: String] = [:]

    static func resolve(userId: String?, fallbackURL: String?, completion: @escaping (String?) -> Void) {
        let id = (userId ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        let fallback = (fallbackURL ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if !fallback.isEmpty && fallback != K1L0DefaultHelmetIconURL {
            if !id.isEmpty { cache[id] = fallback }
            completion(fallback)
            return
        }
        if let cached = cache[id], !cached.isEmpty {
            completion(cached)
            return
        }
        guard !id.isEmpty else {
            completion(fallback.isEmpty ? nil : fallback)
            return
        }

        K1L0NativeAPI.resolve { apiBase in
            let encoded = id.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? id
            guard let url = URL(string: "\(apiBase)/api/k1l0/user/metadata?userId=\(encoded)") else {
                DispatchQueue.main.async { completion(fallback.isEmpty ? nil : fallback) }
                return
            }
            URLSession.shared.dataTask(with: url) { data, _, _ in
                var helmet = ""
                if let data,
                   let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
                    helmet = (root["helmetUrl"] as? String ?? "")
                        .trimmingCharacters(in: .whitespacesAndNewlines)
                }
                DispatchQueue.main.async {
                    if !helmet.isEmpty { cache[id] = helmet }
                    completion(helmet.isEmpty ? (fallback.isEmpty ? nil : fallback) : helmet)
                }
            }.resume()
        }
    }
}

struct SignalStrengthMeter: View {
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

// Luminance → alpha masker. Item avatars are guaranteed pitch-black
// backgrounds by the beam-avatar prompt spec, so mapping brightness to
// alpha via CIColorMatrix makes the black bg transparent while preserving
// the item's color. Cached by URL so scroll never re-processes.
enum K1L0BlackMasker {
    private static func hasEmbeddedAlpha(_ image: CGImage) -> Bool {
        switch image.alphaInfo {
        case .first, .last, .premultipliedFirst, .premultipliedLast, .alphaOnly:
            return true
        case .none, .noneSkipFirst, .noneSkipLast:
            return false
        @unknown default:
            return false
        }
    }

#if canImport(UIKit)
    static let cache: NSCache<NSString, UIImage> = {
        let c = NSCache<NSString, UIImage>()
        c.countLimit = 256
        return c
    }()

    static func mask(_ input: UIImage) -> UIImage? {
        guard let cg = input.cgImage else { return nil }
        // New spawned-item artwork carries a semantic BiRefNet matte in the
        // PNG alpha channel. Preserve it exactly so dark foreground details do
        // not get mistaken for the legacy pitch-black background.
        if hasEmbeddedAlpha(cg) { return input }
        let ci = CIImage(cgImage: cg)
        guard let filter = CIFilter(name: "CIColorMatrix") else { return nil }
        filter.setValue(ci, forKey: kCIInputImageKey)
        filter.setValue(CIVector(x: 1, y: 0, z: 0, w: 0), forKey: "inputRVector")
        filter.setValue(CIVector(x: 0, y: 1, z: 0, w: 0), forKey: "inputGVector")
        filter.setValue(CIVector(x: 0, y: 0, z: 1, w: 0), forKey: "inputBVector")
        // alpha = clamped luma with a bias so pitch-black (0,0,0) → 0 and
        // any non-trivially-dark pixel saturates to 1.0 quickly. Without the
        // coefficient scale-up, plain luma-to-alpha (0.299/0.587/0.114) made
        // even saturated reds/blues render at ~30% opacity ("faint items").
        // 6.0 * standard-luma weights ≈ full opacity by ~17% brightness; the
        // Core Image pipeline auto-clamps output to [0,1].
        filter.setValue(CIVector(x: 6.0 * 0.299, y: 6.0 * 0.587, z: 6.0 * 0.114, w: 0), forKey: "inputAVector")
        filter.setValue(CIVector(x: 0, y: 0, z: 0, w: 0), forKey: "inputBiasVector")
        guard let out = filter.outputImage else { return nil }
        let ctx = CIContext(options: [.workingColorSpace: NSNull()])
        guard let cgOut = ctx.createCGImage(out, from: out.extent) else { return nil }
        return UIImage(cgImage: cgOut, scale: input.scale, orientation: input.imageOrientation)
    }
#elseif canImport(AppKit)
    static let cache: NSCache<NSString, NSImage> = {
        let c = NSCache<NSString, NSImage>()
        c.countLimit = 256
        return c
    }()

    static func mask(_ input: NSImage) -> NSImage? {
        var rect = CGRect(origin: .zero, size: input.size)
        guard let cg = input.cgImage(forProposedRect: &rect, context: nil, hints: nil) else { return nil }
        if hasEmbeddedAlpha(cg) { return input }
        let ci = CIImage(cgImage: cg)
        guard let filter = CIFilter(name: "CIColorMatrix") else { return nil }
        filter.setValue(ci, forKey: kCIInputImageKey)
        filter.setValue(CIVector(x: 1, y: 0, z: 0, w: 0), forKey: "inputRVector")
        filter.setValue(CIVector(x: 0, y: 1, z: 0, w: 0), forKey: "inputGVector")
        filter.setValue(CIVector(x: 0, y: 0, z: 1, w: 0), forKey: "inputBVector")
        filter.setValue(CIVector(x: 6 * 0.299, y: 6 * 0.587, z: 6 * 0.114, w: 0), forKey: "inputAVector")
        filter.setValue(CIVector(x: 0, y: 0, z: 0, w: 0), forKey: "inputBiasVector")
        guard let out = filter.outputImage,
              let cgOut = CIContext(options: [.workingColorSpace: NSNull()]).createCGImage(out, from: out.extent)
        else { return nil }
        return NSImage(cgImage: cgOut, size: input.size)
    }
#endif
}

struct BlackMaskedRemoteImage: View {
    let url: URL?
    var contentMode: ContentMode = .fill
#if canImport(UIKit)
    @State private var maskedImage: UIImage?
#else
    @State private var maskedImage: NSImage?
#endif

    var body: some View {
        Group {
            if let img = maskedImage {
#if canImport(UIKit)
                Image(uiImage: img)
                    .resizable()
                    .aspectRatio(contentMode: contentMode)
#else
                Image(nsImage: img)
                    .resizable()
                    .aspectRatio(contentMode: contentMode)
#endif
            } else {
                Color.clear
            }
        }
        .task(id: url?.absoluteString) {
            guard let url else {
                await MainActor.run { maskedImage = nil }
                return
            }
            let key = url.absoluteString as NSString
            if let cached = K1L0BlackMasker.cache.object(forKey: key) {
                await MainActor.run { maskedImage = cached }
                return
            }
            do {
                let (data, _) = try await URLSession.shared.data(from: url)
#if canImport(UIKit)
                guard let raw = UIImage(data: data) else { return }
#else
                guard let raw = NSImage(data: data) else { return }
#endif
                guard let masked = K1L0BlackMasker.mask(raw) else {
                    await MainActor.run { maskedImage = raw }
                    return
                }
                K1L0BlackMasker.cache.setObject(masked, forKey: key)
                await MainActor.run { maskedImage = masked }
            } catch {
                // Silent — Color.clear placeholder stays visible.
            }
        }
    }
}

struct NearbyItemThumbnail: View {
    let imageUrl: String?
    // Optional glyph rendered when the URL is missing — MapKit places have
    // no imageUrl but do carry a deterministic artifact material, and the
    // periodic symbol reads as a proper collectible rather than a stub.
    var fallbackGlyph: String? = nil

    private var url: URL? {
        let raw = (imageUrl ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        return raw.isEmpty ? nil : URL(string: raw)
    }

    var body: some View {
        Group {
            if let url {
                // Render at twice the viewport size for a center-focused crop
                // matching the item grid; black bg is masked to alpha so no
                // square box surrounds the item.
                BlackMaskedRemoteImage(url: url, contentMode: .fill)
                    .frame(width: 60, height: 60)
                    .frame(width: 30, height: 30)
                    .clipped()
            } else if let glyph = fallbackGlyph, !glyph.isEmpty {
                Text(glyph)
                    .font(.system(size: 13, weight: .black, design: .monospaced))
                    .foregroundStyle(.white.opacity(0.92))
                    .frame(width: 30, height: 30)
                    .background(
                        Circle().stroke(.white.opacity(0.35), lineWidth: 1)
                    )
            } else {
                fallback
            }
        }
        .frame(width: 30, height: 30)
        .frame(minWidth: 32, alignment: .trailing)
    }

    private var fallback: some View {
        Image(systemName: "questionmark.diamond.fill")
            .font(.system(size: 15, weight: .bold))
            .foregroundStyle(.white.opacity(0.86))
    }
}

/// Shared presentation chrome for every artifact/location detail surface.
/// The panel is pinned to the bottom edge, enters/exits from that edge, and
/// can be pulled down to dismiss without each caller maintaining its own
/// modal state or gesture implementation.
private struct K1L0ArtifactContentHeightKey: PreferenceKey {
    static var defaultValue: CGFloat = 0

    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) {
        value = max(value, nextValue())
    }
}

struct K1L0ArtifactBottomSheet<Content: View>: View {
    let onDismiss: () -> Void
    private let content: Content
    @State private var dragOffset: CGFloat = 0
    @State private var contentHeight: CGFloat = 0
    @Environment(\.horizontalSizeClass) private var horizontalSizeClass

    init(onDismiss: @escaping () -> Void, @ViewBuilder content: () -> Content) {
        self.onDismiss = onDismiss
        self.content = content()
    }

    private func dismissAnimated() {
        withAnimation(.spring(response: 0.34, dampingFraction: 0.90)) {
            onDismiss()
        }
    }

    var body: some View {
        GeometryReader { geometry in
            let bottomSafe = max(geometry.safeAreaInsets.bottom, k1l0DeviceSafeAreaInsets().bottom)
            let maxSheetHeight = geometry.size.height * (horizontalSizeClass == .compact ? 0.70 : 0.76)
            let grabberHeight: CGFloat = 13
            let measuredSheetHeight = contentHeight > 0
                ? min(maxSheetHeight, contentHeight + grabberHeight)
                : maxSheetHeight
            ZStack(alignment: .bottom) {
                Color.black.opacity(0.30)
                    .ignoresSafeArea()
                    .contentShape(Rectangle())
                    .onTapGesture(perform: dismissAnimated)

                VStack(spacing: 0) {
                    Capsule()
                        .fill(Color.white.opacity(0.32))
                        .frame(width: 42, height: 5)
                        .padding(.top, 3)
                        .padding(.bottom, 5)

                    ScrollView(.vertical, showsIndicators: false) {
                        content
                            .frame(maxWidth: .infinity)
                            .background {
                                GeometryReader { contentGeometry in
                                    Color.clear.preference(
                                        key: K1L0ArtifactContentHeightKey.self,
                                        value: contentGeometry.size.height
                                    )
                                }
                            }
                    }
                    .scrollBounceBehavior(.basedOnSize)
                }
                .frame(
                    maxWidth: horizontalSizeClass == .compact ? .infinity : 520,
                    minHeight: min(120, measuredSheetHeight),
                    maxHeight: measuredSheetHeight,
                    alignment: .top
                )
                .onPreferenceChange(K1L0ArtifactContentHeightKey.self) { measuredHeight in
                    guard measuredHeight > 0, abs(contentHeight - measuredHeight) > 0.5 else {
                        return
                    }
                    contentHeight = measuredHeight
                }
                .padding(.bottom, bottomSafe)
                .background(Color.black.opacity(0.91))
                .background(.ultraThinMaterial)
                .environment(\.colorScheme, .dark)
                .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))
                .overlay(
                    RoundedRectangle(cornerRadius: 22, style: .continuous)
                        .stroke(Color.white.opacity(0.18), lineWidth: 1)
                )
                .shadow(color: .black.opacity(0.52), radius: 28, y: 12)
                .offset(y: max(0, dragOffset))
                .simultaneousGesture(
                    DragGesture(minimumDistance: 8)
                        .onChanged { value in
                            dragOffset = max(0, value.translation.height)
                        }
                        .onEnded { value in
                            let shouldDismiss = value.translation.height > 90 ||
                                value.predictedEndTranslation.height > 170
                            if shouldDismiss {
                                dismissAnimated()
                            } else {
                                withAnimation(.spring(response: 0.28, dampingFraction: 0.84)) {
                                    dragOffset = 0
                                }
                            }
                        }
                )
            }
            .ignoresSafeArea(edges: .bottom)
        }
    }
}

/// One detail destination shared by Unity floating-item taps, map/home chips,
/// home locations, and collected inventory artifacts.
struct UnifiedArtifactDetailSheet: View {
    let selection: OverlayArtifactDetailSelection
    @ObservedObject var data: K1L0OverlayDataModel
    let onDismiss: () -> Void

    @ViewBuilder
    var body: some View {
        switch selection {
        case .place(let place):
            HomeLocationDetailCard(
                place: place,
                distanceText: data.distanceText(to: place),
                relativeBearing: data.relativeBearingDegrees(to: place),
                onDismiss: onDismiss
            )
        case .beam(let beam):
            FloatingWorldItemDetailCard(
                title: beam.title,
                detailText: beam.teaserText,
                imageUrl: beam.imageUrl,
                distanceText: data.distanceText(to: beam),
                relativeBearing: data.relativeBearingDegrees(to: beam),
                onDismiss: onDismiss
            )
        case .fallback(let tap):
            FloatingWorldItemDetailCard(
                title: tap.displayTitle,
                detailText: tap.kind == "location"
                    ? "LAST SPOTTED NEAR \(tap.locationName.uppercased())"
                    : "Nearby artifact",
                imageUrl: tap.imageUrl.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? nil : tap.imageUrl,
                distanceText: data.distanceText(to: tap),
                relativeBearing: data.relativeBearingDegrees(to: tap),
                onDismiss: onDismiss
            )
        case .inventory(let item):
            InventoryItemDetailCard(item: item, onDismiss: onDismiss)
        }
    }
}

struct HomeLocationDetailCard: View {
    let place: OverlayPlace
    let distanceText: String
    let relativeBearing: Double
    let onDismiss: () -> Void

    private var isOpen: Bool { (place.openNow ?? place.openingHours?.openNow) != false }

    private var siteStatusText: String { isOpen ? "SITE ACTIVE" : "SITE DORMANT" }

    private var lastSpottedText: String {
        "LAST SPOTTED NEAR \(place.name.uppercased())"
    }

    private var categoryText: String? {
        let ignored: Set<String> = [
            "establishment", "point_of_interest", "food", "store", "premise"
        ]
        var seen = Set<String>()
        var labels: [String] = []
        for raw in [place.type] + (place.types ?? []) {
            let normalized = raw
                .trimmingCharacters(in: .whitespacesAndNewlines)
                .lowercased()
                .replacingOccurrences(of: "-", with: "_")
                .replacingOccurrences(of: " ", with: "_")
            guard !normalized.isEmpty, !ignored.contains(normalized), seen.insert(normalized).inserted else {
                continue
            }
            labels.append(normalized.replacingOccurrences(of: "_", with: " ").capitalized)
            if labels.count == 3 { break }
        }
        return labels.isEmpty ? nil : labels.joined(separator: " · ")
    }

    var body: some View {
        K1L0ArtifactBottomSheet(onDismiss: onDismiss) {
            VStack(spacing: 14) {
                HStack {
                    Label("ARTIFACT SIGNAL", systemImage: "wave.3.right.circle.fill")
                        .font(.system(size: 11, weight: .black, design: .monospaced))
                        .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                    Spacer(minLength: 8)
                    Text(siteStatusText)
                        .font(.system(size: 9, weight: .black, design: .monospaced))
                        .foregroundStyle(isOpen ? Color.green : Color.red.opacity(0.9))
                        .padding(.horizontal, 8)
                        .padding(.vertical, 5)
                        .background(Color.white.opacity(0.07), in: Capsule())
                    Button(action: onDismiss) {
                        Image(systemName: "xmark")
                            .font(.system(size: 12, weight: .black))
                            .foregroundStyle(.white.opacity(0.86))
                            .frame(width: 30, height: 30)
                            .background(Color.white.opacity(0.10), in: Circle())
                    }
                    .buttonStyle(.plain)
                }

                Group {
                    if let raw = place.imageUrl,
                       !raw.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
                       let url = URL(string: raw) {
                        BlackMaskedRemoteImage(url: url, contentMode: .fit)
                            .opacity(0.78)
                            .blendMode(.screen)
                    } else {
                        Image(systemName: place.collectIconName)
                            .font(.system(size: 62, weight: .black))
                            .foregroundStyle(Color(red: 0.70, green: 1.0, blue: 0.50).opacity(0.82))
                    }
                }
                .frame(width: 190, height: 190)

                VStack(spacing: 5) {
                    Text(place.collectTitle)
                        .font(.system(size: 27, weight: .heavy))
                        .foregroundStyle(.white)
                        .lineLimit(2)
                        .multilineTextAlignment(.center)
                        .minimumScaleFactor(0.68)

                    Text(lastSpottedText)
                        .font(.system(size: 11, weight: .black, design: .monospaced))
                        .foregroundStyle(.white.opacity(0.66))
                        .lineLimit(2)
                        .multilineTextAlignment(.center)

                    if let categoryText {
                        Text("SITE CLASS  ·  \(categoryText.uppercased())")
                            .font(.system(size: 10, weight: .bold, design: .monospaced))
                            .foregroundStyle(.white.opacity(0.48))
                            .lineLimit(2)
                            .multilineTextAlignment(.center)
                    }
                }

                if let hours = place.hoursDisplayText {
                    HStack(alignment: .top, spacing: 8) {
                        Image(systemName: "dot.radiowaves.left.and.right")
                            .foregroundStyle(.white.opacity(0.62))
                        VStack(alignment: .leading, spacing: 2) {
                            Text("OBSERVATION WINDOW")
                                .font(.system(size: 9, weight: .black, design: .monospaced))
                                .foregroundStyle(.white.opacity(0.44))
                            Text(hours)
                                .frame(maxWidth: .infinity, alignment: .leading)
                        }
                    }
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundStyle(.white.opacity(0.86))
                }

                HStack(spacing: 13) {
                    Image(systemName: "location.north.fill")
                        .font(.system(size: 25, weight: .black))
                        .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                        .rotationEffect(.degrees(relativeBearing))
                        .frame(width: 36, height: 36)
                    VStack(alignment: .leading, spacing: 1) {
                        Text(distanceText)
                            .font(.system(size: 21, weight: .black, design: .rounded))
                            .monospacedDigit()
                        Text("RANGE ON FOOT")
                            .font(.system(size: 10, weight: .bold, design: .monospaced))
                            .foregroundStyle(.white.opacity(0.56))
                    }
                    Spacer()
                }
                .foregroundStyle(.white)
                .padding(.horizontal, 13)
                .padding(.vertical, 10)
                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
            }
            .padding(17)
            .frame(maxWidth: 430)
            .frame(maxWidth: .infinity)
        }
    }
}

struct FloatingWorldItemDetailCard: View {
    let title: String
    let detailText: String
    let imageUrl: String?
    let distanceText: String
    let relativeBearing: Double
    let onDismiss: () -> Void

    private var detail: String? {
        let value = detailText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !value.isEmpty,
              value.compare(title, options: [.caseInsensitive, .diacriticInsensitive]) != .orderedSame else {
            return nil
        }
        return value
    }

    var body: some View {
        K1L0ArtifactBottomSheet(onDismiss: onDismiss) {
            VStack(spacing: 15) {
                HStack {
                    Text("AMBIENT ARTIFACT")
                        .font(.system(size: 12, weight: .black, design: .rounded))
                        .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                    Spacer()
                    Button(action: onDismiss) {
                        Image(systemName: "xmark")
                            .font(.system(size: 12, weight: .black))
                            .foregroundStyle(.white.opacity(0.86))
                            .frame(width: 30, height: 30)
                            .background(Color.white.opacity(0.10), in: Circle())
                    }
                    .buttonStyle(.plain)
                }

                Group {
                    if let raw = imageUrl,
                       !raw.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
                       let url = URL(string: raw) {
                        BlackMaskedRemoteImage(url: url, contentMode: .fit)
                            // Zoom past the art's black mat so the object
                            // fills the square (matches the inventory modal).
                            .scaleEffect(1.55)
                            .opacity(0.78)
                            .blendMode(.screen)
                    } else {
                        Image(systemName: "shippingbox.fill")
                            .font(.system(size: 62, weight: .black))
                            .foregroundStyle(.white.opacity(0.76))
                    }
                }
                .frame(width: 190, height: 190)
                .clipShape(RoundedRectangle(cornerRadius: 6, style: .continuous))
                // Bright diagnostic border for layout tweaking: shows the true
                // bounds of the item graphic square.
                .overlay(
                    RoundedRectangle(cornerRadius: 6, style: .continuous)
                        .stroke(Color.white.opacity(0.92), lineWidth: 1.5)
                )

                VStack(spacing: 5) {
                    Text(title)
                        .font(.system(size: 25, weight: .heavy))
                        .foregroundStyle(.white)
                        .lineLimit(2)
                        .multilineTextAlignment(.center)
                        .minimumScaleFactor(0.68)
                    if let detail {
                        Text(detail)
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundStyle(.white.opacity(0.60))
                            .lineLimit(2)
                            .multilineTextAlignment(.center)
                    }
                }

                HStack(spacing: 13) {
                    Image(systemName: "location.north.fill")
                        .font(.system(size: 25, weight: .black))
                        .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                        .rotationEffect(.degrees(relativeBearing))
                        .frame(width: 36, height: 36)
                    VStack(alignment: .leading, spacing: 1) {
                        Text(distanceText)
                            .font(.system(size: 21, weight: .black, design: .rounded))
                            .monospacedDigit()
                        Text("walking distance")
                            .font(.system(size: 11, weight: .bold))
                            .foregroundStyle(.white.opacity(0.56))
                    }
                    Spacer()
                }
                .foregroundStyle(.white)
                .padding(.horizontal, 13)
                .padding(.vertical, 10)
                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
            }
            .padding(18)
            .frame(maxWidth: 430)
            .frame(maxWidth: .infinity)
        }
    }
}

struct MysteryObjectCollectPrompt: View {
    let beam: OverlayBeam
    let distanceText: String
    let relativeBearing: Double
    let onCollect: () -> Void
    let onDismiss: () -> Void

    var body: some View {
        ZStack {
            Color.black.opacity(0.22)
                .ignoresSafeArea()

            VStack(spacing: 12) {
                HStack {
                    DirectionCell(distance: distanceText, relativeBearing: relativeBearing)
                    Spacer()
                    Button(action: onDismiss) {
                        Image(systemName: "xmark")
                            .font(.system(size: 16, weight: .black))
                            .foregroundStyle(.white)
                            .frame(width: 34, height: 34)
                            .background(Color.black.opacity(0.46), in: Circle())
                    }
                    .buttonStyle(.plain)
                }

                ZStack {
                    if let imageUrlString = beam.imageUrl,
                       !imageUrlString.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
                       let imageUrl = URL(string: imageUrlString) {
                        BlackMaskedRemoteImage(url: imageUrl, contentMode: .fit)
                            .frame(width: 140, height: 140)
                            .opacity(0.72)
                            .blendMode(.screen)
                    } else {
                        Image(systemName: beam.collectIconName)
                            .font(.system(size: 40, weight: .black))
                            .foregroundStyle(Color(red: 0.70, green: 1.0, blue: 0.50))
                            .opacity(0.72)
                    }
                }
                .frame(height: 148)

                VStack(spacing: 8) {
                    Text("Artifact Ready")
                        .font(.system(size: 13, weight: .black))
                        .foregroundStyle(.white.opacity(0.68))
                        .textCase(.uppercase)
                    Text(beam.title)
                        .font(.system(size: 30, weight: .heavy))
                        .foregroundStyle(.white)
                        .lineLimit(2)
                        .multilineTextAlignment(.center)
                        .minimumScaleFactor(0.62)
                }

                Button(action: onCollect) {
                    Text("collect")
                        .font(.system(size: 18, weight: .black, design: .monospaced))
                        .foregroundStyle(.white)
                        .lineLimit(1)
                        .minimumScaleFactor(0.54)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 14)
                        .background(Color(red: 0.15, green: 0.45, blue: 0.22))
                        .clipShape(Capsule())
                }
                .buttonStyle(.plain)
            }
            .padding(16)
            .frame(maxWidth: 390)
            .background(Color.black.opacity(0.72), in: RoundedRectangle(cornerRadius: 24, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 24, style: .continuous)
                    .stroke(Color.white.opacity(0.18), lineWidth: 1)
            )
            .padding(.horizontal, 18)
        }
        .ignoresSafeArea(.keyboard, edges: .bottom)
    }
}

struct LocationDwellStatusChip: View {
    let place: OverlayPlace
    let progress: Double
    let onTap: () -> Void

    private var transferPercentage: Int {
        Int((min(1, max(0, progress)) * 100).rounded())
    }

    var body: some View {
        HStack {
            Button(action: onTap) {
                HStack(spacing: 10) {
                        Image(systemName: "dot.radiowaves.left.and.right")
                            .font(.system(size: 18, weight: .black))
                            .foregroundStyle(Color.green)
                        VStack(alignment: .leading, spacing: 3) {
                            Text("AT \(place.name.uppercased())")
                                .font(.system(size: 13, weight: .black, design: .rounded))
                                .foregroundStyle(.white)
                                .lineLimit(1)
                            Text("TRANSFERRING... \(transferPercentage)%")
                                .font(.system(size: 11, weight: .bold, design: .rounded))
                                .foregroundStyle(.white.opacity(0.72))
                            GeometryReader { proxy in
                                ZStack(alignment: .leading) {
                                    Capsule().fill(Color.white.opacity(0.13))
                                    Capsule().fill(Color.green)
                                        .frame(width: proxy.size.width * progress)
                                }
                            }
                            .frame(height: 5)
                        }
                        Image(systemName: "chevron.right")
                            .font(.system(size: 12, weight: .black))
                            .foregroundStyle(.white.opacity(0.58))
                }
                .padding(.horizontal, 13)
                .padding(.vertical, 10)
                .frame(maxWidth: 320)
                .background(Color.black.opacity(0.78), in: RoundedRectangle(cornerRadius: 11, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: 11, style: .continuous).stroke(Color.green.opacity(0.55), lineWidth: 1))
            }
            .buttonStyle(.plain)
            Spacer()
        }
        .allowsHitTesting(true)
    }
}

struct LocationItemCollectPrompt: View {
    let place: OverlayPlace
    let distanceText: String
    let relativeBearing: Double
    let secondsRemaining: Int
    let progress: Double
    let onDismiss: () -> Void

    private var transferPercentage: Int {
        Int((min(1, max(0, progress)) * 100).rounded())
    }

    var body: some View {
        ZStack {
            Color.black.opacity(0.22)
                .ignoresSafeArea()

            VStack(spacing: 12) {
                HStack {
                    DirectionCell(distance: distanceText, relativeBearing: relativeBearing)
                    Spacer()
                    Button(action: onDismiss) {
                        Image(systemName: "xmark")
                            .font(.system(size: 16, weight: .black))
                            .foregroundStyle(.white)
                            .frame(width: 34, height: 34)
                            .background(Color.black.opacity(0.46), in: Circle())
                    }
                    .buttonStyle(.plain)
                }

                Group {
                    if let raw = place.imageUrl, !raw.isEmpty, let url = URL(string: raw) {
                        BlackMaskedRemoteImage(url: url, contentMode: .fit)
                            .frame(width: 132, height: 132)
                            .opacity(0.72)
                            .blendMode(.screen)
                    } else {
                        Image(systemName: place.collectIconName)
                            .font(.system(size: 58, weight: .black))
                            .foregroundStyle(Color(red: 0.70, green: 1.0, blue: 0.50))
                            .frame(width: 132, height: 132)
                    }
                }
                .frame(height: 140)

                VStack(spacing: 6) {
                    Text("TRANSFERRING ARTIFACT")
                        .font(.system(size: 12, weight: .black))
                        .foregroundStyle(.white.opacity(0.68))
                        .textCase(.uppercase)
                    Text(place.collectTitle)
                        .font(.system(size: 24, weight: .heavy))
                        .foregroundStyle(.white)
                        .lineLimit(2)
                        .multilineTextAlignment(.center)
                        .minimumScaleFactor(0.62)
                    Text("Stay at \(place.name) until the signal completes.")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.74))
                        .lineLimit(2)
                        .multilineTextAlignment(.center)
                        .minimumScaleFactor(0.7)
                    if let hours = place.hoursDisplayText {
                        HStack(spacing: 6) {
                            Image(systemName: "clock.fill")
                            Text(hours)
                                .lineLimit(1)
                        }
                        .font(.system(size: 12, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.78))
                    }
                }

                // Signal meter + percent + timer live on one row now.
                HStack(spacing: 10) {
                    TenBarSignalMeter(strength: progress)
                        .frame(height: 20)
                        .frame(maxWidth: .infinity)
                    Text("\(transferPercentage)%")
                        .font(.system(size: 14, weight: .black, design: .monospaced))
                        .foregroundStyle(.white.opacity(0.85))
                        .frame(width: 44, alignment: .trailing)
                }

                HStack(spacing: 8) {
                    Image(systemName: "timer")
                    Text(String(format: "%d:%02d remaining", secondsRemaining / 60, secondsRemaining % 60))
                        .font(.system(size: 15, weight: .black, design: .monospaced))
                }
                .foregroundStyle(.white)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 10)
                .background(Color(red: 0.15, green: 0.45, blue: 0.22))
                .clipShape(Capsule())
            }
            .padding(16)
            .frame(maxWidth: 390)
            .background(Color.black.opacity(0.72), in: RoundedRectangle(cornerRadius: 24, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 24, style: .continuous)
                    .stroke(Color.white.opacity(0.18), lineWidth: 1)
            )
            .padding(.horizontal, 18)
        }
        .ignoresSafeArea(.keyboard, edges: .bottom)
    }
}

// Bottom card for a tapped nearby user — same chrome as MysteryObjectCollectPrompt.
// No last-seen; inline message compose stored in Firebase via /api/k1l0/message.
struct NearbyProfileDTO: Decodable {
    let avatarUrl: String?
    let helmetUrl: String?
    let faceUrl: String?
    let bio: String?
    let url: String?
}

struct RoundedCorners: Shape {
#if canImport(UIKit)
    let corners: UIRectCorner
#else
    struct Corners: OptionSet {
        let rawValue: Int
        static let topLeft = Corners(rawValue: 1 << 0)
        static let topRight = Corners(rawValue: 1 << 1)
    }
    let corners: Corners
#endif
    let radius: CGFloat

    func path(in rect: CGRect) -> Path {
#if canImport(UIKit)
        let path = UIBezierPath(roundedRect: rect, byRoundingCorners: corners, cornerRadii: CGSize(width: radius, height: radius))
        return Path(path.cgPath)
#else
        // AppKit has no UIRectCorner equivalent. Current call sites request the
        // two top corners; a continuous rounded rectangle is the closest native
        // Mac treatment and keeps the shared card geometry intact.
        return Path(roundedRect: rect, cornerRadius: radius)
#endif
    }
}

struct NearbyUserInfoCard: View {
    let user: OverlayUser
    let locationText: String
    let onDismiss: () -> Void

    @State private var composing = false
    @State private var messageText = ""
    @State private var isSending = false
    @State private var sent = false
    @FocusState private var fieldFocused: Bool
    // Fetched on open from /api/k1l0/user/metadata so the hero can use the full
    // avatar render and we can surface bio + instagram handle.
    @State private var heroUrl: String?
    @State private var bioText: String?
    @State private var instagramUrl: String?
    @State private var profileLoaded = false

    private var placeText: String {
        let city = (user.city ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if city.isEmpty { return locationText }
        return "\(locationText) · \(city)"
    }

    var body: some View {
        VStack {
            Spacer()
            VStack(alignment: .leading, spacing: 0) {
                // Hero: full-bleed avatar render with identity overlaid at the bottom.
                ZStack(alignment: .bottomLeading) {
                    heroImage
                        .frame(height: 232)
                        .frame(maxWidth: .infinity)
                        .clipped()
                    LinearGradient(colors: [.clear, .black.opacity(0.30), .black.opacity(0.92)], startPoint: .top, endPoint: .bottom)
                    VStack(alignment: .leading, spacing: 5) {
                        Text(user.displayName.uppercased())
                            .font(.system(size: 28, weight: .heavy))
                            .foregroundStyle(.white)
                            .lineLimit(1)
                            .minimumScaleFactor(0.7)
                        if !user.realName.isEmpty && user.realName.caseInsensitiveCompare(user.displayName) != .orderedSame {
                            Text(user.realName)
                                .font(.system(size: 14, weight: .semibold))
                                .foregroundStyle(.white.opacity(0.82))
                                .lineLimit(1)
                        }
                        if let handle = instagramDisplay,
                           !handle.isEmpty,
                           let destination = instagramDestination {
                            HStack(spacing: 0) {
                                Text("ig: ")
                                    .foregroundStyle(.white.opacity(0.68))
                                Link(handle, destination: destination)
                                    .foregroundStyle(Color(red: 0.45, green: 0.88, blue: 1.0))
                            }
                                .font(.system(size: 13, weight: .semibold))
                                .lineLimit(1)
                        }
                    }
                    .padding(18)
                    // Close button, top-right over the hero.
                    VStack {
                        HStack {
                            Spacer()
                            Button(action: onDismiss) {
                                Image(systemName: "xmark")
                                    .font(.system(size: 16, weight: .black))
                                    .foregroundStyle(.white)
                                    .frame(width: 34, height: 34)
                                    .background(Color.black.opacity(0.42), in: Circle())
                            }
                            .buttonStyle(.plain)
                        }
                        Spacer()
                    }
                    .padding(14)
                }
                .frame(height: 232)

                VStack(alignment: .leading, spacing: 12) {
                    if let bio = bioText?.trimmingCharacters(in: .whitespacesAndNewlines), !bio.isEmpty {
                        Text(bio)
                            .font(.system(size: 14, weight: .medium))
                            .foregroundStyle(.white.opacity(0.85))
                            .lineLimit(4)
                            .fixedSize(horizontal: false, vertical: true)
                    }

                    Label(placeText, systemImage: "location.fill")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.72))
                        .lineLimit(1)
                        .minimumScaleFactor(0.75)

                    if sent {
                        Text("[ message sent ]")
                            .font(.system(size: 18, weight: .black, design: .monospaced))
                            .foregroundStyle(.white.opacity(0.55))
                            .frame(maxWidth: .infinity)
                            .padding(.vertical, 14)
                    } else if composing {
                        HStack(spacing: 10) {
                            TextField("say something…", text: $messageText)
                                .font(.system(size: 15, weight: .medium))
                                .foregroundStyle(.white)
                                .tint(.white)
                                .focused($fieldFocused)
                                .submitLabel(.send)
                                .onSubmit { sendMessage() }
                            Button(action: sendMessage) {
                                if isSending {
                                    ProgressView().tint(.black).scaleEffect(0.8)
                                        .frame(width: 44, height: 20)
                                } else {
                                    Text("send")
                                        .font(.system(size: 15, weight: .black))
                                        .foregroundStyle(.black)
                                        .padding(.horizontal, 14)
                                        .padding(.vertical, 10)
                                }
                            }
                            .disabled(messageText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || isSending)
                            .buttonStyle(.plain)
                            .background(Color(red: 0.70, green: 1.0, blue: 0.50))
                        }
                        .padding(.horizontal, 12)
                        .padding(.vertical, 8)
                        .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 10))
                    } else {
                        Button(action: { composing = true; fieldFocused = true }) {
                            Text("[ message ]")
                                .font(.system(size: 18, weight: .black, design: .monospaced))
                                .foregroundStyle(.black)
                                .lineLimit(1)
                                .minimumScaleFactor(0.54)
                                .frame(maxWidth: .infinity)
                                .padding(.vertical, 14)
                                .background(Color(red: 0.70, green: 1.0, blue: 0.50))
                        }
                        .buttonStyle(.plain)
                    }
                }
                .padding(.horizontal, 18)
                .padding(.top, 18)
                .padding(.bottom, 38)
            }
            .background(Color.black)
            .clipShape(RoundedCorners(corners: [.topLeft, .topRight], radius: 26))
            .overlay(
                RoundedCorners(corners: [.topLeft, .topRight], radius: 26)
                    .stroke(Color.white.opacity(0.18), lineWidth: 1)
            )
            .padding(.horizontal, 0)
            .padding(.bottom, 0)
        }
        .ignoresSafeArea(edges: .bottom)
        .ignoresSafeArea(.keyboard, edges: .bottom)
        .onAppear { loadProfile() }
    }

    @ViewBuilder
    private var heroImage: some View {
        let resolved = (heroUrl ?? user.avatarDisplayUrl) ?? K1L0DefaultHelmetIconURL
        if let u = URL(string: resolved), !resolved.isEmpty {
            AsyncImage(url: u) { phase in
                switch phase {
                case .success(let image):
                    // Anchor the fill to the TOP so tall 9:16 cloak renders show
                    // the head/helmet instead of cropping to the midsection.
                    GeometryReader { proxy in
                        image
                            .resizable()
                            .scaledToFill()
                            .frame(width: proxy.size.width, height: proxy.size.height, alignment: .top)
                    }
                case .failure:
                    WarblyStaticView()
                default:
                    WarblyStaticView()
                }
            }
        } else {
            WarblyStaticView()
        }
    }

    private var instagramDisplay: String? {
        let raw = (instagramUrl ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        guard !raw.isEmpty else { return nil }
        if let last = raw.split(separator: "/").last, !last.isEmpty {
            let piece = last.hasPrefix("@") ? String(last.dropFirst()) : String(last)
            return "@\(piece)"
        }
        return raw.hasPrefix("@") ? raw : "@\(raw)"
    }

    private var instagramDestination: URL? {
        guard let handle = instagramDisplay else { return nil }
        let clean = handle.trimmingCharacters(in: CharacterSet(charactersIn: "@"))
        guard !clean.isEmpty else { return nil }
        return URL(string: "https://www.instagram.com/\(clean)/")
    }

    private func loadProfile() {
        guard !profileLoaded else { return }
        profileLoaded = true
        K1L0NativeAPI.resolve { apiBase in
            let encoded = user.userId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? user.userId
            guard let url = URL(string: "\(apiBase)/api/k1l0/user/metadata?userId=\(encoded)") else { return }
            URLSession.shared.dataTask(with: url) { data, _, _ in
                guard let data = data,
                      let dto = try? JSONDecoder().decode(NearbyProfileDTO.self, from: data) else { return }
                DispatchQueue.main.async {
                    let helmet = (dto.helmetUrl ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
                    heroUrl = helmet.isEmpty ? K1L0DefaultHelmetIconURL : helmet
                    bioText = dto.bio
                    instagramUrl = dto.url
                }
            }.resume()
        }
    }

    private func sendMessage() {
        let text = messageText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty, !isSending else { return }
        guard let fromId = K1L0NativeAPI.currentUserId() else { return }
        isSending = true
        K1L0NativeAPI.resolve { apiBase in
            guard let url = URL(string: "\(apiBase)/api/k1l0/message") else {
                DispatchQueue.main.async { isSending = false }
                return
            }
            var req = URLRequest(url: url)
            req.httpMethod = "POST"
            req.setValue("application/json", forHTTPHeaderField: "Content-Type")
            let body: [String: Any] = ["from": fromId, "to": user.userId, "text": text]
            req.httpBody = try? JSONSerialization.data(withJSONObject: body)
            URLSession.shared.dataTask(with: req) { _, response, _ in
                DispatchQueue.main.async {
                    isSending = false
                    if (response as? HTTPURLResponse)?.statusCode == 200 {
                        sent = true
                    }
                }
            }.resume()
        }
    }
}

struct InventoryItemDetailCard: View {
    let item: OverlayInventoryItem
    let onDismiss: () -> Void

    private static let dateFormatter: DateFormatter = {
        let f = DateFormatter()
        f.dateFormat = "EEEE, MMMM d, yyyy 'at' h:mm a"
        return f
    }()

    private var foundLocation: String {
        var parts: [String] = []
        for value in [item.sourcePlaceName, item.collectedCity, item.collectedCountry] {
            let clean = value.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !clean.isEmpty,
                  !parts.contains(where: { $0.caseInsensitiveCompare(clean) == .orderedSame }) else { continue }
            parts.append(clean)
        }
        return parts.joined(separator: ", ")
    }

    private var travelCountries: [String] {
        var values = item.travelCountries
        if !item.collectedCountry.isEmpty { values.append(item.collectedCountry) }
        var result: [String] = []
        for value in values {
            let clean = value.trimmingCharacters(in: .whitespacesAndNewlines)
            if !clean.isEmpty && !result.contains(where: { $0.caseInsensitiveCompare(clean) == .orderedSame }) {
                result.append(clean)
            }
        }
        return result
    }

    private func ordinal(_ number: Int) -> String {
        let mod100 = number % 100
        let suffix: String
        if (11...13).contains(mod100) {
            suffix = "th"
        } else {
            switch number % 10 {
            case 1: suffix = "st"
            case 2: suffix = "nd"
            case 3: suffix = "rd"
            default: suffix = "th"
            }
        }
        return "\(number)\(suffix)"
    }

    var body: some View {
        K1L0ArtifactBottomSheet(onDismiss: onDismiss) {
            VStack(alignment: .leading, spacing: 16) {
                HStack(alignment: .top, spacing: 14) {
                    ZStack {
                        RoundedRectangle(cornerRadius: 14, style: .continuous)
                            .fill(item.isElement
                                ? Color(red: 0.05, green: 0.25, blue: 0.12).opacity(0.82)
                                : Color.white.opacity(0.10))
                            .overlay(
                                RoundedRectangle(cornerRadius: 14, style: .continuous)
                                    .stroke(item.isElement
                                        ? Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.42)
                                        : Color.white.opacity(0.18), lineWidth: 1)
                            )
                        if !item.isElement, let url = URL(string: item.avatarUrl), !item.avatarUrl.isEmpty {
                            AsyncImage(url: url) { phase in
                                switch phase {
                                case .success(let image):
                                    image.resizable().scaledToFill().scaleEffect(2.0)
                                default:
                                    Text(item.symbol)
                                        .font(.system(size: 22, weight: .black))
                                        .foregroundStyle(.white.opacity(0.78))
                                }
                            }
                            .frame(width: 56, height: 56)
                            .background(Color.black)
                            .clipShape(RoundedRectangle(cornerRadius: 13, style: .continuous))
                        } else {
                            Text(item.symbol)
                                .font(.system(size: item.isElement ? 26 : 22, weight: .black))
                                .foregroundStyle(item.isElement ? Color(red: 0.66, green: 1.0, blue: 0.76) : .white.opacity(0.84))
                        }
                    }
                    .frame(width: 60, height: 60)

                    VStack(alignment: .leading, spacing: 4) {
                        Text(item.isElement ? "Element" : "Object")
                            .font(.system(size: 12, weight: .black))
                            .foregroundStyle(.white.opacity(0.55))
                            .textCase(.uppercase)
                        Text(item.name)
                            .font(.system(size: 22, weight: .heavy))
                            .foregroundStyle(.white)
                            .lineLimit(2)
                            .minimumScaleFactor(0.72)
                        Text(item.amountText)
                            .font(.system(size: 13, weight: .bold).monospacedDigit())
                            .foregroundStyle(.white.opacity(0.60))
                    }

                    Spacer()

                    Button(action: onDismiss) {
                        Image(systemName: "xmark")
                            .font(.system(size: 15, weight: .black))
                            .foregroundStyle(.white)
                            .frame(width: 32, height: 32)
                            .background(Color.black.opacity(0.42), in: Circle())
                    }
                    .buttonStyle(.plain)
                }

                if let imageURL = URL(string: item.resolvedAvatarUrl) {
                    BlackMaskedRemoteImage(url: imageURL, contentMode: .fit)
                        // The pool art keeps the object small on a large black
                        // mat; zoom past the mat so the object fills the square.
                        .scaleEffect(1.55)
                        .frame(width: 230, height: 230)
                        .clipShape(RoundedRectangle(cornerRadius: 6, style: .continuous))
                        .opacity(0.72)
                        .blendMode(.screen)
                        // Diagnostic border: shows the true bounds of the image
                        // square so art-mat padding is visibly distinct from
                        // modal layout padding.
                        .overlay(
                            RoundedRectangle(cornerRadius: 6, style: .continuous)
                                .stroke(Color.white.opacity(0.45), lineWidth: 1)
                        )
                        .frame(maxWidth: .infinity, alignment: .center)
                        .accessibilityLabel(item.name)
                }

                if !item.rarityAtDiscovery.isEmpty || !item.sourceKind.isEmpty {
                    HStack(spacing: 8) {
                        if !item.rarityAtDiscovery.isEmpty {
                            Text(item.rarityAtDiscovery.uppercased())
                                .foregroundStyle(Color(red: 1.0, green: 0.48, blue: 0.72))
                        }
                        if !item.sourceKind.isEmpty {
                            Text(item.sourceKind.uppercased())
                                .foregroundStyle(.white.opacity(0.64))
                        }
                    }
                    .font(.system(size: 11, weight: .black, design: .monospaced))
                    .padding(.horizontal, 11)
                    .padding(.vertical, 6)
                    .background(Color.white.opacity(0.08), in: Capsule())
                    .overlay(Capsule().stroke(Color.white.opacity(0.16), lineWidth: 1))
                    .frame(maxWidth: .infinity, alignment: .center)
                }

                Divider().background(Color.white.opacity(0.14))

                VStack(alignment: .leading, spacing: 10) {
                    if let date = item.collectedAt, !foundLocation.isEmpty {
                        Label("Found at \(foundLocation) · \(Self.dateFormatter.string(from: date))", systemImage: "location.fill")
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundStyle(.white.opacity(0.72))
                    } else if let date = item.collectedAt {
                        Label("Found \(Self.dateFormatter.string(from: date))", systemImage: "calendar")
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundStyle(.white.opacity(0.72))
                    } else if !foundLocation.isEmpty {
                        Label("Found at \(foundLocation)", systemImage: "location.fill")
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundStyle(.white.opacity(0.72))
                    }
                    if item.discoveryNumber > 0 {
                        Label("\(ordinal(item.discoveryNumber)) known specimen", systemImage: "sparkles")
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundStyle(.white.opacity(0.78))
                    }
                    if item.globalFindCount > item.discoveryNumber && item.globalFindCount > 0 {
                        Label("\(item.globalFindCount) known now", systemImage: "globe.americas.fill")
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundStyle(.white.opacity(0.68))
                    }
                    if !travelCountries.isEmpty {
                        let noun = travelCountries.count == 1 ? "country" : "countries"
                        Label("Traveled through \(travelCountries.count) \(noun)", systemImage: "point.topleft.down.to.point.bottomright.curvepath")
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundStyle(.white.opacity(0.72))
                    }
                }

                Text(item.detailDescription)
                    .font(.system(size: 14, weight: .medium))
                    .foregroundStyle(.white.opacity(0.68))
                    .lineSpacing(3)
                    .fixedSize(horizontal: false, vertical: true)
            }
            .padding(.horizontal, 20)
            .padding(.top, 20)
            .padding(.bottom, 28)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}

#if os(iOS)
struct K1L0ItemPointCloudView: UIViewRepresentable {
    let imageURL: URL
    let depthURL: URL?
    let particleSize: Double
    let spacing: Double
    let zSpread: Double
    let brightness: Double
    let rotationDegrees: Double
    let rotationSpeed: Double
    let perspective: Double
    let cameraDistance: Double
    let sparkle: Double

    func makeUIView(context: Context) -> K1L0ItemPointCloudMetalView {
        // Pass the device explicitly: a bare (frame:) call can resolve to
        // UIView's inherited init and silently skip the whole Metal setup.
        NSLog("[K1L0PointCloud] makeUIView image=\(imageURL) depth=\(depthURL?.absoluteString ?? "none")")
        let view = K1L0ItemPointCloudMetalView(frame: .zero, device: MTLCreateSystemDefaultDevice())
        view.configure(
            particleSize: particleSize,
            spacing: spacing,
            zSpread: zSpread,
            brightness: brightness,
            rotationDegrees: rotationDegrees,
            rotationSpeed: rotationSpeed,
            perspective: perspective,
            cameraDistance: cameraDistance,
            sparkle: sparkle
        )
        view.load(imageURL: imageURL, depthURL: depthURL)
        return view
    }

    func updateUIView(_ view: K1L0ItemPointCloudMetalView, context: Context) {
        view.configure(
            particleSize: particleSize,
            spacing: spacing,
            zSpread: zSpread,
            brightness: brightness,
            rotationDegrees: rotationDegrees,
            rotationSpeed: rotationSpeed,
            perspective: perspective,
            cameraDistance: cameraDistance,
            sparkle: sparkle
        )
        view.load(imageURL: imageURL, depthURL: depthURL)
    }
}

final class K1L0ItemParticleCanvasView: UIView {
    private struct Point { let x: CGFloat; let y: CGFloat; let z: CGFloat; let seed: CGFloat }
    private var points: [Point] = []
    private var displayLink: CADisplayLink?
    private var started = CACurrentMediaTime()
    private var loadedKey = ""

    override init(frame: CGRect) {
        super.init(frame: frame)
        backgroundColor = .black
        isOpaque = true
        let link = CADisplayLink(target: self, selector: #selector(tick))
        link.preferredFrameRateRange = CAFrameRateRange(minimum: 20, maximum: 30, preferred: 30)
        link.add(to: .main, forMode: .common)
        displayLink = link
    }

    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    deinit { displayLink?.invalidate() }

    func load(imageURL: URL, depthURL: URL?) {
        let url = depthURL ?? imageURL
        let key = url.absoluteString
        guard key != loadedKey else { return }
        loadedKey = key
        URLSession.shared.dataTask(with: url) { [weak self] data, _, _ in
            guard let data, let image = UIImage(data: data), let cg = image.cgImage else { return }
            let width = 48, height = 48
            var pixels = [UInt8](repeating: 0, count: width * height)
            guard let context = CGContext(
                data: &pixels, width: width, height: height,
                bitsPerComponent: 8, bytesPerRow: width,
                space: CGColorSpaceCreateDeviceGray(),
                bitmapInfo: CGImageAlphaInfo.none.rawValue
            ) else { return }
            context.interpolationQuality = .medium
            context.draw(cg, in: CGRect(x: 0, y: 0, width: width, height: height))
            var generated: [Point] = []
            generated.reserveCapacity(900)
            for row in 0..<height {
                for column in 0..<width {
                    let value = CGFloat(pixels[row * width + column]) / 255.0
                    guard value > 0.055 else { continue }
                    let seed = CGFloat((column * 73 + row * 151) % 997) / 997.0
                    generated.append(Point(
                        x: (CGFloat(column) / CGFloat(width - 1) - 0.5) * 1.55,
                        y: (0.5 - CGFloat(row) / CGFloat(height - 1)) * 1.55,
                        z: max(-0.45, min(0.45, (value - 0.20) * 0.9)),
                        seed: seed
                    ))
                }
            }
            DispatchQueue.main.async {
                self?.points = generated
                self?.started = CACurrentMediaTime()
                self?.setNeedsDisplay()
                NSLog("[K1L0PointCloud] native canvas ready points=\(generated.count)")
            }
        }.resume()
    }

    @objc private func tick() { setNeedsDisplay() }

    override func draw(_ rect: CGRect) {
        guard let context = UIGraphicsGetCurrentContext(), !points.isEmpty else { return }
        context.setFillColor(UIColor.white.cgColor)
        let time = CGFloat(CACurrentMediaTime() - started)
        let yaw = sin(time * 0.55) * 0.48
        let cosine = cos(yaw), sine = sin(yaw)
        let scale = min(bounds.width, bounds.height) * 0.48
        let center = CGPoint(x: bounds.midX, y: bounds.midY)
        for point in points {
            let x = cosine * point.x - sine * point.z
            let z = sine * point.x + cosine * point.z
            let perspective = 1.0 / max(0.65, 1.65 - z)
            let shimmer = 0.86 + 0.14 * sin(time * 2.0 + point.seed * 20.0)
            let size: CGFloat = 8.0 + 7.0 * shimmer
            let px = center.x + x * scale * perspective
            let py = center.y + point.y * scale * perspective + sin(time + point.seed * 9.0) * 2.0
            context.fillEllipse(in: CGRect(x: px - size / 2, y: py - size / 2, width: size, height: size))
        }
    }
}

final class K1L0ItemPointCloudMetalView: MTKView, MTKViewDelegate {
    private struct Uniforms {
        var time: Float
        var aspect: Float
        var pointSize: Float
        var hasTextures: Float = 0
        var textureAspect: Float = 1
        var particleScale: Float = 1
        var spacing: Float = 1
        var zSpread: Float = 0.1
        var brightness: Float = 1
        var rotationDegrees: Float = 60
        var rotationSpeed: Float = 1
        var perspective: Float = 1
        var cameraDistance: Float = 1.2
        var sparkle: Float = 1
        var foregroundCenter = SIMD2<Float>(repeating: 0.5)
    }
    private static let pointGrid = 192 // must match K1L0_POINT_GRID in K1L0TuningShader.metal
    private static let starCount = 56 // must match K1L0_STAR_COUNT in K1L0TuningShader.metal
    private var pipeline: MTLRenderPipelineState?
    private var commandQueue: MTLCommandQueue?
    private var colorTexture: MTLTexture?
    private var depthTexture: MTLTexture?
    private var placeholderTexture: MTLTexture?
    private var started = CACurrentMediaTime()
    private var loadedKey = ""
    private var particleScale: Float = 1
    private var particleSpacing: Float = 1
    private var depthSpread: Float = 0.1
    private var particleBrightness: Float = 1
    private var rotationDegrees: Float = 60
    private var rotationSpeed: Float = 1
    private var perspective: Float = 1
    private var cameraDistance: Float = 1.2
    private var sparkle: Float = 1
    private var foregroundCenter = SIMD2<Float>(repeating: 0.5)
    private var loggedFirstDraw = false
    private var loggedBlockedDraw = false

    override init(frame: CGRect, device: MTLDevice? = MTLCreateSystemDefaultDevice()) {
        super.init(frame: frame, device: device)
        let deviceName = device?.name ?? "none"
        NSLog("[K1L0PointCloud] Metal view init device=\(deviceName) bundle=\(Bundle.main.bundlePath)")
        framebufferOnly = true
        colorPixelFormat = .bgra8Unorm
        clearColor = MTLClearColorMake(0, 0, 0, 1)
        preferredFramesPerSecond = 30
        isPaused = false
        enableSetNeedsDisplay = false
        delegate = self
        guard let device else {
            NSLog("[K1L0PointCloud] Metal device unavailable")
            return
        }
        let shaderBundles = [Bundle.main, Bundle(for: K1L0TuningStaticPlayer.self)]
        var shaderLibrary: MTLLibrary?
        for bundle in shaderBundles where shaderLibrary == nil {
            let candidates: [MTLLibrary?] = [
                try? device.makeDefaultLibrary(bundle: bundle),
                try? device.makeLibrary(URL: bundle.bundleURL.appendingPathComponent("default.metallib"))
            ]
            if let library = candidates.compactMap({ $0 }).first(where: {
                $0.makeFunction(name: "k1l0ItemGlitchVertex") != nil &&
                $0.makeFunction(name: "k1l0ItemGlitchFragment") != nil
            }) {
                shaderLibrary = library
                NSLog("[K1L0PointCloud] shader library ready bundle=\(bundle.bundlePath)")
            }
        }
        guard let library = shaderLibrary,
              let vertex = library.makeFunction(name: "k1l0ItemGlitchVertex"),
              let fragment = library.makeFunction(name: "k1l0ItemGlitchFragment") else {
            NSLog("[K1L0PointCloud] shader functions unavailable in app and UnityFramework bundles")
            return
        }
        let descriptor = MTLRenderPipelineDescriptor()
        descriptor.vertexFunction = vertex
        descriptor.fragmentFunction = fragment
        descriptor.colorAttachments[0].pixelFormat = colorPixelFormat
        descriptor.colorAttachments[0].isBlendingEnabled = true
        descriptor.colorAttachments[0].sourceRGBBlendFactor = .sourceAlpha
        descriptor.colorAttachments[0].destinationRGBBlendFactor = .oneMinusSourceAlpha
        descriptor.colorAttachments[0].sourceAlphaBlendFactor = .sourceAlpha
        descriptor.colorAttachments[0].destinationAlphaBlendFactor = .oneMinusSourceAlpha
        do {
            pipeline = try device.makeRenderPipelineState(descriptor: descriptor)
            NSLog("[K1L0PointCloud] pipeline ready")
        } catch {
            NSLog("[K1L0PointCloud] pipeline failed: \(error)")
        }
        commandQueue = device.makeCommandQueue()
        // 1×1 white stand-in so the texture slots are always bound; the shader
        // switches to the diagnostic grid while hasTextures == 0.
        let placeholderDescriptor = MTLTextureDescriptor.texture2DDescriptor(
            pixelFormat: .bgra8Unorm, width: 1, height: 1, mipmapped: false)
        placeholderDescriptor.usage = [.shaderRead]
        if let texture = device.makeTexture(descriptor: placeholderDescriptor) {
            var white: UInt32 = 0xFFFFFFFF
            texture.replace(region: MTLRegionMake2D(0, 0, 1, 1), mipmapLevel: 0, withBytes: &white, bytesPerRow: 4)
            placeholderTexture = texture
        }
    }

    required init(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }

    private struct DecodedTexture {
        let texture: MTLTexture
        let foregroundCenter: SIMD2<Float>
    }

    private static func decodeTexture(_ data: Data, using device: MTLDevice) throws -> DecodedTexture {
        // Rasterize into a predictable RGBA8 buffer. MTKTextureLoader rejects
        // some otherwise valid CDN RGB and palette-indexed PNG representations.
        guard let image = UIImage(data: data), let cgImage = image.cgImage else {
            throw NSError(
                domain: "K1L0PointCloud",
                code: 1,
                userInfo: [NSLocalizedDescriptionKey: "UIKit could not decode image data"]
            )
        }
        let width = cgImage.width
        let height = cgImage.height
        let bytesPerRow = width * 4
        var pixels = [UInt8](repeating: 0, count: bytesPerRow * height)
        return try pixels.withUnsafeMutableBytes { bytes in
            guard let context = CGContext(
                data: bytes.baseAddress,
                width: width,
                height: height,
                bitsPerComponent: 8,
                bytesPerRow: bytesPerRow,
                space: CGColorSpaceCreateDeviceRGB(),
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue | CGBitmapInfo.byteOrder32Big.rawValue
            ) else {
                throw NSError(
                    domain: "K1L0PointCloud",
                    code: 2,
                    userInfo: [NSLocalizedDescriptionKey: "Could not create RGBA bitmap context"]
                )
            }
            context.translateBy(x: 0, y: CGFloat(height))
            context.scaleBy(x: 1, y: -1)
            context.draw(cgImage, in: CGRect(x: 0, y: 0, width: width, height: height))

            // Match the shader's alpha/luma background rejection and measure
            // the actual visible subject rather than the full square texture.
            let rgba = bytes.bindMemory(to: UInt8.self)
            var minX = width, minY = height, maxX = -1, maxY = -1
            for y in 0..<height {
                for x in 0..<width {
                    let offset = y * bytesPerRow + x * 4
                    let r = Int(rgba[offset])
                    let g = Int(rgba[offset + 1])
                    let b = Int(rgba[offset + 2])
                    let a = Int(rgba[offset + 3])
                    let luma1000 = 299 * r + 587 * g + 114 * b
                    if a >= 39 && luma1000 >= 11_475 {
                        minX = min(minX, x); maxX = max(maxX, x)
                        minY = min(minY, y); maxY = max(maxY, y)
                    }
                }
            }
            let center: SIMD2<Float>
            if maxX >= minX, maxY >= minY {
                center = SIMD2(
                    Float(minX + maxX + 1) / Float(width * 2),
                    Float(minY + maxY + 1) / Float(height * 2)
                )
            } else {
                center = SIMD2(repeating: 0.5)
            }

            let descriptor = MTLTextureDescriptor.texture2DDescriptor(
                pixelFormat: .rgba8Unorm,
                width: width,
                height: height,
                mipmapped: false
            )
            descriptor.usage = [.shaderRead]
            descriptor.storageMode = .shared
            guard let texture = device.makeTexture(descriptor: descriptor) else {
                throw NSError(
                    domain: "K1L0PointCloud",
                    code: 3,
                    userInfo: [NSLocalizedDescriptionKey: "Metal could not allocate RGBA texture"]
                )
            }
            texture.replace(
                region: MTLRegionMake2D(0, 0, width, height),
                mipmapLevel: 0,
                withBytes: bytes.baseAddress!,
                bytesPerRow: bytesPerRow
            )
            return DecodedTexture(texture: texture, foregroundCenter: center)
        }
    }

    func configure(
        particleSize: Double,
        spacing: Double,
        zSpread: Double,
        brightness: Double,
        rotationDegrees: Double,
        rotationSpeed: Double,
        perspective: Double,
        cameraDistance: Double,
        sparkle: Double
    ) {
        particleScale = Float(particleSize)
        particleSpacing = Float(spacing)
        depthSpread = Float(zSpread)
        particleBrightness = Float(brightness)
        self.rotationDegrees = Float(rotationDegrees)
        self.rotationSpeed = Float(rotationSpeed)
        self.perspective = Float(perspective)
        self.cameraDistance = Float(cameraDistance)
        self.sparkle = Float(sparkle)
    }

    func load(imageURL: URL, depthURL: URL?) {
        let key = imageURL.absoluteString + "|" + (depthURL?.absoluteString ?? "")
        guard key != loadedKey, let device else { return }
        loadedKey = key
        let depthAddress = depthURL?.absoluteString ?? "none"
        NSLog("[K1L0PointCloud] load requested image=\(imageURL.absoluteString) depth=\(depthAddress)")
        Task.detached(priority: .utility) { [weak self] in
            do {
                let imageData = try Data(contentsOf: imageURL, options: .mappedIfSafe)
                NSLog("[K1L0PointCloud] image data ready bytes=\(imageData.count)")
                let decodedColor = try Self.decodeTexture(imageData, using: device)
                let color = decodedColor.texture
                var depth = color
                if let depthURL {
                    do {
                        let depthData = try Data(contentsOf: depthURL, options: .mappedIfSafe)
                        NSLog("[K1L0PointCloud] depth data ready bytes=\(depthData.count)")
                        depth = try Self.decodeTexture(depthData, using: device).texture
                    } catch {
                        NSLog("[K1L0PointCloud] depth load failed \(depthURL.absoluteString) error=\(error.localizedDescription); using color texture")
                    }
                }
                await MainActor.run {
                    self?.colorTexture = color
                    self?.depthTexture = depth
                    self?.foregroundCenter = decodedColor.foregroundCenter
                    self?.started = CACurrentMediaTime()
                    NSLog("[K1L0PointCloud] textures ready color=\(color.width)x\(color.height) depth=\(depth.width)x\(depth.height) foregroundCenter=\(decodedColor.foregroundCenter)")
                }
            } catch {
                NSLog("[K1L0PointCloud] color load failed \(imageURL.absoluteString) error=\(error.localizedDescription)")
            }
        }
    }

    func mtkView(_ view: MTKView, drawableSizeWillChange size: CGSize) {}

    func draw(in view: MTKView) {
        guard let drawable = currentDrawable, let pass = currentRenderPassDescriptor,
              let pipeline, let colorTexture, let depthTexture,
              let queue = commandQueue, let command = queue.makeCommandBuffer(),
              let encoder = command.makeRenderCommandEncoder(descriptor: pass) else {
            if !loggedBlockedDraw {
                loggedBlockedDraw = true
                NSLog("[K1L0PointCloud] draw blocked drawable=\(currentDrawable != nil) pass=\(currentRenderPassDescriptor != nil) pipeline=\(pipeline != nil) color=\(colorTexture != nil) depth=\(depthTexture != nil) queue=\(commandQueue != nil) size=\(drawableSize.width)x\(drawableSize.height)")
            }
            return
        }
        if !loggedFirstDraw {
            loggedFirstDraw = true
            NSLog("[K1L0PointCloud] first draw size=\(drawableSize.width)x\(drawableSize.height) color=\(colorTexture.width)x\(colorTexture.height) depth=\(depthTexture.width)x\(depthTexture.height)")
        }
        let hasTextures = true
        let grid = Self.pointGrid
        var uniforms = Uniforms(
            time: Float(CACurrentMediaTime() - started),
            aspect: Float(max(1, drawableSize.width) / max(1, drawableSize.height)),
            // Real cloud: points a touch wider than one grid cell so the item
            // reads solid with additive glow. Diagnostic grid: big fat dots.
            pointSize: hasTextures
                ? Float(max(2.25, drawableSize.width / CGFloat(grid) * 1.38))
                : Float(max(44, drawableSize.width / 300.0 * 56.0)),
            hasTextures: hasTextures ? 1 : 0,
            textureAspect: Float(colorTexture.width) / Float(max(1, colorTexture.height)),
            particleScale: particleScale,
            spacing: particleSpacing,
            zSpread: depthSpread,
            brightness: particleBrightness,
            rotationDegrees: rotationDegrees,
            rotationSpeed: rotationSpeed,
            perspective: perspective,
            cameraDistance: cameraDistance,
            sparkle: sparkle,
            foregroundCenter: foregroundCenter
        )
        encoder.setRenderPipelineState(pipeline)
        encoder.setVertexBytes(&uniforms, length: MemoryLayout<Uniforms>.stride, index: 0)
        encoder.setVertexTexture(colorTexture, index: 0)
        encoder.setVertexTexture(depthTexture, index: 1)
        encoder.drawPrimitives(type: .triangleStrip, vertexStart: 0, vertexCount: 4)
        encoder.endEncoding()
        command.present(drawable)
        command.commit()
    }
}
#endif

struct DropFilterBar: View {
    @Binding var selected: String

    private let filters = [
        ("all", "📍"),
        ("coffee", "☕️"),
        ("drinks", "🍸")
    ]

    var body: some View {
        HStack(spacing: 3) {
            ForEach(filters, id: \.0) { filter in
                Button {
                    selected = filter.0
                } label: {
                    Text(filter.1)
                        .font(.system(size: 21, weight: .bold))
                        .foregroundStyle(.white)
                        .frame(maxWidth: .infinity, minHeight: 42)
                        .background(
                            selected == filter.0
                            ? Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.28)
                            : Color.clear,
                            in: RoundedRectangle(cornerRadius: 11, style: .continuous)
                        )
                        .overlay {
                            if selected == filter.0 {
                                RoundedRectangle(cornerRadius: 11, style: .continuous)
                                    .stroke(Color(red: 0.66, green: 1.0, blue: 0.76).opacity(0.42), lineWidth: 1)
                            }
                        }
                }
                .buttonStyle(.plain)
            }
        }
        .padding(4)
        .frame(maxWidth: .infinity)
        .background(Color.white.opacity(0.075), in: RoundedRectangle(cornerRadius: 15, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 15, style: .continuous)
                .stroke(Color.white.opacity(0.12), lineWidth: 1)
        )
    }
}
