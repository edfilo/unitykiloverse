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

struct NativeUserEditorDraft: Codable, Equatable {
    var name: String = ""
    var callsign: String = ""
    var bio: String = ""
    var url: String = ""
    var cloakDesign: String = ""
    var helmetDesign: String = ""
    var selfiePath: String = ""
    var selfieUrl: String = ""
    var helmetUrl: String = ""
    var cloakUrl: String = ""
    var avatarUrl: String = ""
    var helmetTextureUrl: String = ""
    var cloakTextureUrl: String = ""
    var skinRevision: Int = 0
}

enum NativeUserEditorStore {
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

// Instagram-style 3-column grid of transmission thumbnails.
// Shows sent transmissions only (originals with a still image).
struct TransmissionGridView: View {
    let groups: [NativeTransmissionChainGroup]
    let onOpen: (NativeTransmissionChainGroup) -> Void

    private let columns = Array(repeating: GridItem(.flexible(), spacing: 2), count: 3)

    // Only originals that have a visual (sent, no parent, has thumbUrl or video)
    private var gridGroups: [NativeTransmissionChainGroup] {
        groups.filter { group in
            guard let firstItem = group.orderedItems.first else { return false }
            let isSent = (firstItem.direction ?? "sent").lowercased() == "sent"
            let isOriginal = (firstItem.parentJobId ?? "").trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            let hasVisual = !(firstItem.thumbUrl?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ?? true)
                || firstItem.playbackVideoUrl != nil
            return isSent && isOriginal && hasVisual
        }
    }

    var body: some View {
        if gridGroups.isEmpty { EmptyView() } else {
            LazyVGrid(columns: columns, spacing: 2) {
                ForEach(gridGroups) { group in
                    let displayItem = group.orderedItems.first ?? group.latest
                    TransmissionGridCell(item: displayItem)
                        .onTapGesture { onOpen(group) }
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
        }
    }
}

struct TransmissionGridCell: View {
    let item: NativeUserTransmissionItem

    var body: some View {
        ZStack(alignment: .bottomLeading) {
            Color.white.opacity(0.06)

            if let raw = item.thumbUrl, let url = URL(string: raw) {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let img):
                        img.resizable()
                            .scaledToFill()
                    default:
                        Color.clear
                    }
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .clipped()
            }

            LinearGradient(
                colors: [.clear, .black.opacity(0.52)],
                startPoint: .center, endPoint: .bottom
            )

            if item.playbackVideoUrl != nil {
                Image(systemName: "play.fill")
                    .font(.system(size: 11, weight: .black))
                    .foregroundStyle(.white.opacity(0.82))
                    .padding(6)
            }

            VStack {
                HStack {
                    Spacer()
                    Circle()
                        .fill((item.direction ?? "sent").lowercased() == "sent"
                            ? Color(red: 0.66, green: 1.0, blue: 0.76)
                            : Color(red: 1.0, green: 0.84, blue: 0.38))
                        .frame(width: 6, height: 6)
                        .padding(6)
                }
                Spacer()
            }
        }
        .frame(maxWidth: .infinity)
        .aspectRatio(1, contentMode: .fit)
        .contentShape(Rectangle())
        .clipped()
    }
}

struct NativeUserTransmissionItem: Codable, Identifiable {
    let jobId: String
    let ownerUserId: String?
    let ownerName: String?
    let ownerCallsign: String?
    let ownerDisplayName: String?
    let sourceCity: String?
    let sourceCountry: String?
    let sourceCountryCode: String?
    let direction: String?
    let thumbUrl: String?
    let finalUrl: String?
    let rawVideoUrl: String?
    let videoUrl: String?
    let audioUrl: String?
    let responsePlot: String?
    let responseOptions: [String]?
    let selectedResponse: String?
    let parentJobId: String?
    let rootJobId: String?
    let chainDepth: Int?
    let status: String?
    let createdAt: Double?
    let updatedAt: Double?

    var id: String { "\(direction ?? "sent")-\(ownerUserId ?? "")-\(jobId)" }

    var playbackVideoUrl: String? {
        let raw = rawVideoUrl?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !raw.isEmpty { return raw }
        let video = videoUrl?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !video.isEmpty { return video }
        let final = finalUrl?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return final.isEmpty ? nil : final
    }

    var isPlayableReadyOriginalSentTransmission: Bool {
        let job = jobId.trimmingCharacters(in: .whitespacesAndNewlines)
        return isPlayableReadyOriginalSentTransmissionIgnoringCancel
            && !K1L0ActiveTransmissionStore.shared.isCanceled(jobId: job)
    }

    var isOriginalSentTransmission: Bool {
        let directionValue = (direction ?? "sent").lowercased()
        let job = jobId.trimmingCharacters(in: .whitespacesAndNewlines)
        let parent = (parentJobId ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        let root = (rootJobId ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        return directionValue == "sent"
            && !job.isEmpty
            && parent.isEmpty
            && (root.isEmpty || root == job)
    }

    var isPlayableReadyOriginalSentTransmissionIgnoringCancel: Bool {
        let statusValue = (status ?? "").lowercased()
        return isOriginalSentTransmission
            && (statusValue == "ready" || statusValue == "complete")
            && playbackVideoUrl != nil
    }

    var createdAtMillis: Double {
        let value = createdAt ?? updatedAt ?? 0
        return value > 0 && value < 10_000_000_000 ? value * 1000 : value
    }
}

struct NativeUserTransmissionResponse: Codable {
    let ok: Bool
    let transmissions: [NativeUserTransmissionItem]
}

struct NativeTransmissionChainGroup: Identifiable {
    let id: String
    let items: [NativeUserTransmissionItem]

    var latest: NativeUserTransmissionItem {
        items.max { lhs, rhs in
            (lhs.updatedAt ?? lhs.createdAt ?? 0) < (rhs.updatedAt ?? rhs.createdAt ?? 0)
        } ?? items[0]
    }

    var statusText: String {
        let rootWasSent = items.contains { ($0.direction ?? "sent") == "sent" && (($0.parentJobId ?? "").isEmpty) }
        if rootWasSent { return "TRANSMISSION SENT" }
        let replies = items.filter { ($0.direction ?? "") == "sent" }.count
        return "\(max(1, replies)) REPLIED"
    }

    var orderedItems: [NativeUserTransmissionItem] {
        items.sorted { lhs, rhs in
            let leftDepth = lhs.chainDepth ?? 0
            let rightDepth = rhs.chainDepth ?? 0
            if leftDepth != rightDepth { return leftDepth < rightDepth }
            return (lhs.createdAt ?? lhs.updatedAt ?? 0) < (rhs.createdAt ?? rhs.updatedAt ?? 0)
        }
    }

    /// Creation time of the original (root) transmission in this chain — used to
    /// order the user-screen squares by when the thread started, not by the most
    /// recent reply.
    var originalCreatedAt: Double {
        let original = items.first(where: { $0.isOriginalSentTransmission })
            ?? orderedItems.first
            ?? items.first
        return original?.createdAt
            ?? original?.updatedAt
            ?? latest.createdAt
            ?? latest.updatedAt
            ?? 0
    }
}

struct NativeWalkHistoryPoint: Identifiable {
    let id = UUID()
    let label: String
    let steps: Int
}

struct NativeWalkHistoryCard: View {
    let hourly: [NativeWalkHistoryPoint]
    let daily: [NativeWalkHistoryPoint]
    let status: String

    var body: some View {
        WeatherGlassCard {
            VStack(alignment: .leading, spacing: 13) {
                HStack {
                    Text("Walk History")
                        .font(.system(size: 19, weight: .bold))
                    Spacer()
                    Text(status)
                        .font(.system(size: 10, weight: .black))
                        .foregroundStyle(.white.opacity(0.48))
                }
                NativeWalkLineGraph(title: "24 HOURS", points: hourly, tint: Color(red: 0.66, green: 1.0, blue: 0.76))
                NativeWalkLineGraph(title: "7 DAYS", points: daily, tint: Color(red: 0.54, green: 0.78, blue: 1.0))
            }
        }
    }
}

struct NativeWalkLineGraph: View {
    let title: String
    let points: [NativeWalkHistoryPoint]
    let tint: Color
    private let plottedWidthRatio: CGFloat = 1.0

    private var totalSteps: Int {
        points.reduce(0) { $0 + $1.steps }
    }

    private var maxSteps: Int {
        max(points.map(\.steps).max() ?? 1, 1)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            HStack(alignment: .firstTextBaseline) {
                Text(title)
                    .font(.system(size: 11, weight: .black))
                    .foregroundStyle(.white.opacity(0.58))
                Spacer()
                Text(K1L0StepValueText(totalSteps))
                    .font(.system(size: 17, weight: .black))
                    .foregroundStyle(.white)
                    .monospacedDigit()
                Text("steps")
                    .font(.system(size: 10, weight: .bold))
                    .foregroundStyle(.white.opacity(0.52))
            }
            ZStack {
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .fill(Color.white.opacity(0.045))
                NativeWalkFillPath(points: points, maxSteps: maxSteps, plottedWidthRatio: plottedWidthRatio)
                    .fill(
                        LinearGradient(
                            colors: [tint.opacity(0.22), tint.opacity(0.02)],
                            startPoint: .top,
                            endPoint: .bottom
                        )
                    )
                    .padding(.horizontal, 8)
                    .padding(.vertical, 9)
                NativeWalkTimeGrid(points: points, plottedWidthRatio: plottedWidthRatio)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 9)
                NativeWalkLinePath(points: points, maxSteps: maxSteps, plottedWidthRatio: plottedWidthRatio)
                    .stroke(tint, style: StrokeStyle(lineWidth: 2.4, lineCap: .round, lineJoin: .round))
                    .padding(.horizontal, 8)
                    .padding(.vertical, 9)
            }
            .frame(height: 82)
            .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).stroke(Color.white.opacity(0.10), lineWidth: 1))
        }
    }
}

struct NativeWalkTimeGrid: View {
    let points: [NativeWalkHistoryPoint]
    let plottedWidthRatio: CGFloat

    var body: some View {
        GeometryReader { geometry in
            ZStack(alignment: .leading) {
                ForEach(0...points.count, id: \.self) { index in
                    let denominator = CGFloat(max(points.count, 1))
                    let x = (CGFloat(index) / denominator) * plottedWidthRatio * geometry.size.width
                    let isEdge = index == 0 || index == points.count
                    Rectangle()
                        .fill(Color.white.opacity(isEdge ? 0.18 : 0.08))
                        .frame(width: isEdge ? 1.2 : 0.6, height: geometry.size.height)
                        .position(x: x, y: geometry.size.height * 0.5)
                }
            }
        }
        .allowsHitTesting(false)
    }
}

struct NativeWalkLinePath: Shape {
    let points: [NativeWalkHistoryPoint]
    let maxSteps: Int
    let plottedWidthRatio: CGFloat

    func path(in rect: CGRect) -> Path {
        graphPath(in: rect, closeToBottom: false)
    }

    fileprivate func graphPath(in rect: CGRect, closeToBottom: Bool) -> Path {
        var path = Path()
        guard !points.isEmpty else { return path }
        let denominator = CGFloat(max(points.count, 1))
        var lastPoint = CGPoint(x: rect.minX, y: rect.maxY)
        for index in points.indices {
            let x = rect.minX + ((CGFloat(index) + 0.5) / denominator) * rect.width * plottedWidthRatio
            let ratio = CGFloat(points[index].steps) / CGFloat(max(maxSteps, 1))
            let y = rect.maxY - max(0, min(1, ratio)) * rect.height
            lastPoint = CGPoint(x: x, y: y)
            if index == points.startIndex {
                path.move(to: CGPoint(x: x, y: y))
            } else {
                path.addLine(to: CGPoint(x: x, y: y))
            }
        }
        if closeToBottom {
            path.addLine(to: CGPoint(x: lastPoint.x, y: rect.maxY))
            path.addLine(to: CGPoint(x: rect.minX, y: rect.maxY))
            path.closeSubpath()
        }
        return path
    }
}

struct NativeWalkFillPath: Shape {
    let points: [NativeWalkHistoryPoint]
    let maxSteps: Int
    let plottedWidthRatio: CGFloat

    func path(in rect: CGRect) -> Path {
        NativeWalkLinePath(points: points, maxSteps: maxSteps, plottedWidthRatio: plottedWidthRatio).graphPath(in: rect, closeToBottom: true)
    }
}

struct NativeUserEditorPanel: View {
    @ObservedObject var data: K1L0OverlayDataModel
    var tabsMode: Bool = false
    let onClose: () -> Void

    @ObservedObject private var saveStore = K1L0UserMetadataSaveStore.shared
    @State private var draft = NativeUserEditorStore.load()
    @State private var transmissions: [NativeUserTransmissionItem] = []
    @State private var transmissionsStatus = "loading transmissions…"
    @State private var userPanelTab: String = "transmissions"
    @State private var isEditingProfile = false
    @State private var showingIdentityDetail = false
#if canImport(UIKit)
    @State private var selfie: UIImage?
    @State private var selfiePickerRequest: PhotoPickerRequest? = nil
#elseif canImport(AppKit)
    @State private var selfie: NSImage?
#endif

    @State private var originalProfileDraft: NativeUserEditorDraft? = nil
    @State private var originalAvatarDraft: NativeUserEditorDraft? = nil

    private func isProfileDirty() -> Bool {
        guard let original = originalProfileDraft else { return false }
        return draft.name != original.name ||
               draft.callsign != original.callsign ||
               draft.url != original.url ||
               draft.bio != original.bio
    }

    private func isAvatarDirty() -> Bool {
        guard let original = originalAvatarDraft else { return false }
        return draft.helmetDesign != original.helmetDesign ||
               draft.selfiePath != original.selfiePath
    }

    private var cleanIgHandle: String {
        let raw = draft.url.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !raw.isEmpty else { return "" }
        if raw.contains("instagram.com/") {
            if let lastPart = raw.split(separator: "/").last {
                return String(lastPart).replacingOccurrences(of: "@", with: "")
            }
        }
        return raw.replacingOccurrences(of: "@", with: "")
    }

    var body: some View {
        GeometryReader { geometry in
            let fullScreenEditor = isEditingProfile || showingIdentityDetail
            let panelTop = fullScreenEditor ? 0 : geometry.safeAreaInsets.top
            ZStack(alignment: .top) {
                if fullScreenEditor {
                    Color.black.ignoresSafeArea()
                } else {
                    Color.clear.ignoresSafeArea()
                }

                ZStack(alignment: .top) {
                    if showingIdentityDetail {
                        identityDetailScroll
                            .transition(.move(edge: .trailing).combined(with: .opacity))
                    } else if isEditingProfile {
                        mainUserScroll
                            .transition(.move(edge: .trailing).combined(with: .opacity))
                    } else {
                        profileSummaryScroll
                            .transition(.move(edge: .leading).combined(with: .opacity))
                    }

                    UserPanelHeader(
                        title: showingIdentityDetail ? "Avatar" : (isEditingProfile ? "Edit Profile" : "User"),
                        tabsMode: tabsMode,
                        onClose: onClose,
                        onSave: nil,
                        onBack: (showingIdentityDetail || isEditingProfile) ? {
                            if showingIdentityDetail {
                                if isAvatarDirty() {
                                    save()
                                }
                                withAnimation(.spring(response: 0.32, dampingFraction: 0.90)) {
                                    showingIdentityDetail = false
                                }
                                originalAvatarDraft = nil
                            } else if isEditingProfile {
                                if isProfileDirty() {
                                    save()
                                }
                                withAnimation(.spring(response: 0.32, dampingFraction: 0.90)) {
                                    isEditingProfile = false
                                }
                                originalProfileDraft = nil
                            }
                        } : nil
                    )

                }
                .coordinateSpace(name: "user-panel")
                .frame(width: geometry.size.width)
                .frame(maxHeight: fullScreenEditor ? geometry.size.height - geometry.safeAreaInsets.top : max(520, geometry.size.height - panelTop))
                .background(
                    fullScreenEditor ? Color.black : (tabsMode ? Color.clear : Color.black.opacity(0.18)),
                    in: RoundedRectangle(cornerRadius: fullScreenEditor ? 0 : 28, style: .continuous)
                )
                .padding(.top, fullScreenEditor ? geometry.safeAreaInsets.top : panelTop)
            }
            .ignoresSafeArea(edges: .bottom)
        }
#if canImport(UIKit)
        .sheet(item: $selfiePickerRequest) { request in
            NativePhotoPicker(sourceType: request.source) { image, path in
                if let image, let path {
                    selfie = image
                    draft.selfiePath = path
                    NativeUserEditorStore.save(draft)
                    saveStore.status = "selfie attached."
                }
                selfiePickerRequest = nil
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
        .onChange(of: saveStore.saveSuccessTrigger) { succeeded in
            if succeeded {
                saveStore.saveSuccessTrigger = false
            }
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
        .onReceive(saveStore.$savedHelmetTextureURL) { url in
            guard !url.isEmpty else { return }
            draft.helmetTextureUrl = url
            NativeUserEditorStore.save(draft)
        }
        .onReceive(saveStore.$savedCloakTextureURL) { url in
            guard !url.isEmpty else { return }
            draft.cloakTextureUrl = url
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
        .onReceive(saveStore.$loadedBio) { value in
            guard !value.isEmpty else { return }
            draft.bio = value
            NativeUserEditorStore.save(draft)
        }
        .onReceive(saveStore.$loadedUrl) { value in
            guard !value.isEmpty else { return }
            draft.url = value
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

    private var profileSummaryScroll: some View {
        ScrollView(.vertical, showsIndicators: true) {
            VStack(alignment: .leading, spacing: 14) {
                Color.clear.frame(height: 24)

                WeatherGlassCard {
                    VStack(alignment: .leading, spacing: 13) {
                        HStack(alignment: .top, spacing: 14) {
                            renderedHero
                            VStack(alignment: .leading, spacing: 5) {
                                Text(draft.callsign.isEmpty ? "no callsign" : "@\(draft.callsign)")
                                    .font(.system(size: 24, weight: .black))
                                    .foregroundStyle(.white)
                                    .lineLimit(1)
                                    .minimumScaleFactor(0.68)

                                if !draft.name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                                    Text(draft.name.trimmingCharacters(in: .whitespacesAndNewlines))
                                        .font(.system(size: 14, weight: .bold))
                                        .foregroundStyle(.white.opacity(0.90))
                                        .lineLimit(1)
                                }

                                let cleanIg = cleanIgHandle
                                if !cleanIg.isEmpty {
                                    HStack(spacing: 0) {
                                        Text("ig:")
                                            .foregroundStyle(.white.opacity(0.68))
                                        Link("@\(cleanIg)", destination: URL(string: "https://www.instagram.com/\(cleanIg)/")!)
                                            .foregroundStyle(Color(red: 0.45, green: 0.88, blue: 1.0))
                                    }
                                    .font(.system(size: 13, weight: .bold, design: .monospaced))
                                }

                                if !draft.bio.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                                    Text(draft.bio.trimmingCharacters(in: .whitespacesAndNewlines))
                                        .font(.system(size: 13, weight: .regular))
                                        .foregroundStyle(.white.opacity(0.82))
                                        .lineLimit(5)
                                        .fixedSize(horizontal: false, vertical: true)
                                }
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                        }

                        Button {
                            withAnimation(.spring(response: 0.32, dampingFraction: 0.90)) {
                                isEditingProfile = true
                                originalProfileDraft = draft
                            }
                        } label: {
                            Text("EDIT PROFILE")
                                .font(.system(size: 14, weight: .black, design: .monospaced))
                                .foregroundStyle(.white)
                                .frame(maxWidth: .infinity, minHeight: 46)
                                .background(Color.white.opacity(0.10), in: Capsule())
                        }
                        .buttonStyle(.plain)
                    }
                }

                // Transmissions | Artifacts tabs (internal tag remains "items")
                Picker("", selection: $userPanelTab) {
                    Text("Transmissions").tag("transmissions")
                    Text("Artifacts").tag("items")
                }
                .pickerStyle(.segmented)
                .zIndex(1)

                if userPanelTab == "items" {
                    if data.inventoryItems.isEmpty {
                        Text(data.elementsStatus.isEmpty ? "no collected artifacts" : data.elementsStatus)
                            .font(.system(size: 12, weight: .semibold))
                            .foregroundStyle(.white.opacity(0.40))
                            .frame(maxWidth: .infinity, alignment: .leading)
                    } else {
                        LazyVGrid(columns: [
                            GridItem(.adaptive(minimum: 72, maximum: 86), spacing: 10)
                        ], alignment: .leading, spacing: 10) {
                            ForEach(data.inventoryItems) { item in
                                InventoryTile(item: item)
                                    .onTapGesture {
                                        withAnimation(.spring(response: 0.32, dampingFraction: 0.88)) {
                                            data.selectInventoryArtifact(item)
                                        }
                                    }
                            }
                        }
                    }
                } else {
                    // Transmission grid
                    VStack(alignment: .leading, spacing: 0) {
                        if !transmissions.isEmpty {
                            TransmissionGridView(groups: transmissionGroups, onOpen: openTransmissionChain)
                        } else {
                            Text(transmissionsStatus)
                                .font(.system(size: 12, weight: .semibold))
                                .foregroundStyle(.white.opacity(0.40))
                                .frame(maxWidth: .infinity, alignment: .leading)
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.top, 8)
                }

                Button {
                    K1L0WeatherOverlayInstaller.logoutNativeSession()
                    onClose()
                } label: {
                    Text("[ LOG OUT ]")
                        .font(.system(size: 14, weight: .black, design: .monospaced))
                        .foregroundStyle(Color(red: 1.0, green: 0.36, blue: 0.32))
                        .frame(maxWidth: .infinity, minHeight: 46)
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 20)
            .padding(.top, 24)
            .padding(.bottom, 38)
        }
        .onAppear(perform: loadTransmissions)
    }

    private var mainUserScroll: some View {
        ScrollView(.vertical, showsIndicators: true) {
            VStack(alignment: .leading, spacing: 14) {
                Color.clear.frame(height: 24)

                WeatherGlassCard {
                    VStack(alignment: .leading, spacing: 10) {
                        HStack(alignment: .top, spacing: 14) {
                            Button {
                                withAnimation(.spring(response: 0.32, dampingFraction: 0.90)) {
                                    showingIdentityDetail = true
                                    originalAvatarDraft = draft
                                }
                            } label: {
                                ZStack(alignment: .bottom) {
                                    renderedHero
                                    Text("EDIT AVATAR")
                                        .font(.system(size: 9, weight: .black, design: .rounded))
                                        .foregroundStyle(.white)
                                        .padding(.horizontal, 6)
                                        .padding(.vertical, 3)
                                        .background(Color.black.opacity(0.62), in: Capsule())
                                        .padding(.bottom, 4)
                                }
                            }
                            .buttonStyle(.plain)

                            VStack(alignment: .leading, spacing: 8) {
                                profileTextField("Name", text: $draft.name)
                                profileTextField("Callsign", text: $draft.callsign)
                                profileTextField("Instagram (optional)", text: $draft.url)
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                        }

                        profileTextField("Bio (optional)", text: $draft.bio)
                            .frame(maxWidth: .infinity, minHeight: 72, alignment: .topLeading)
                    }
                }

                Button {
                    K1L0WeatherOverlayInstaller.logoutNativeSession()
                    onClose()
                } label: {
                    Text("[ LOG OUT ]")
                        .font(.system(size: 14, weight: .black, design: .monospaced))
                        .foregroundStyle(Color(red: 1.0, green: 0.36, blue: 0.32))
                        .frame(maxWidth: .infinity, minHeight: 46)
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 20)
            .padding(.top, 24)
            .padding(.bottom, 38)
        }
        .scrollDismissesKeyboardCompat()
    }


    private var identityDetailScroll: some View {
        ScrollView(.vertical, showsIndicators: true) {
            VStack(alignment: .leading, spacing: 14) {
                Color.clear.frame(height: 24)

                WeatherGlassCard {
                    VStack(alignment: .leading, spacing: 12) {
                        renderedIdentityFull
                        let identityReady = !draft.helmetUrl.isEmpty && (!draft.cloakUrl.isEmpty || !draft.avatarUrl.isEmpty)
                        let meshReady = !draft.helmetTextureUrl.isEmpty
                        Text(identityReady && meshReady ? "custom helmet and original foil cloak ready." : "helmet and foil-cloak avatar render after auto-saving.")
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundStyle(identityReady && meshReady ? Color(red: 0.66, green: 1.0, blue: 0.76) : .white.opacity(0.54))
                    }
                }

                WeatherGlassCard {
                    VStack(alignment: .leading, spacing: 10) {
                        Text("Design")
                            .font(.system(size: 19, weight: .bold))
                        // Custom cloak prompts are temporarily disabled. Keep
                        // the saved draft intact so the feature can return
                        // without losing anyone's previous cloak description.
                        HStack {
                            Text("Cloak")
                            Spacer()
                            Text("Original silver foil")
                                .foregroundStyle(.white.opacity(0.62))
                        }
                        .font(.system(size: 14, weight: .semibold))
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

                WeatherGlassCard {
                    VStack(alignment: .leading, spacing: 10) {
                        Text("Parts")
                            .font(.system(size: 19, weight: .bold))
                        HStack(spacing: 12) {
                            identityPreview(title: "HELMET", urlString: draft.helmetUrl)
                            identityPreview(title: "CLOAK", urlString: draft.cloakUrl.isEmpty ? draft.avatarUrl : draft.cloakUrl)
                        }
                    }
                }
            }
            .padding(.horizontal, 20)
            .padding(.top, 24)
            .padding(.bottom, 38)
        }
        .scrollDismissesKeyboardCompat()
    }

    private var transmissionGroups: [NativeTransmissionChainGroup] {
        let visibleTransmissions = transmissions.filter { item in
            let status = (item.status ?? "").trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
            let hasVisual = item.playbackVideoUrl != nil || !(item.thumbUrl?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ?? true)
            return status != "error" || hasVisual
        }
        let grouped = Dictionary(grouping: visibleTransmissions) { item in
            let root = item.rootJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            if !root.isEmpty { return root }
            let parent = item.parentJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            if !parent.isEmpty { return parent }
            return item.jobId
        }
        return grouped.map { key, items in
            NativeTransmissionChainGroup(
                id: key,
                items: items.sorted {
                    ($0.updatedAt ?? $0.createdAt ?? 0) > ($1.updatedAt ?? $1.createdAt ?? 0)
                }
            )
        }
        // Newest original transmission first — the squares reflect when each
        // thread was started, not when the latest reply landed.
        .sorted {
            $0.originalCreatedAt > $1.originalCreatedAt
        }
    }

    private func transmissionGroupRow(_ group: NativeTransmissionChainGroup) -> some View {
        let latest = group.latest
        return HStack(alignment: .center, spacing: 12) {
            transmissionCircleThumb(latest)
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 6) {
                    Text(transmissionDirectionLabel(latest))
                        .font(.system(size: 10, weight: .black))
                        .foregroundStyle((latest.direction ?? "sent").lowercased() == "sent" ? Color(red: 0.66, green: 1.0, blue: 0.76) : Color(red: 1.0, green: 0.84, blue: 0.38))
                    Text(humanTime(latest.updatedAt ?? latest.createdAt ?? 0))
                        .font(.system(size: 11, weight: .bold))
                        .foregroundStyle(.white.opacity(0.50))
                    Spacer(minLength: 0)
                    if group.items.count > 1 {
                        Text("\(group.items.count)")
                            .font(.system(size: 9, weight: .black))
                            .foregroundStyle(.white.opacity(0.42))
                    }
                }
                Text(transmissionTitle(latest))
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(.white)
                    .lineLimit(2)
                    .multilineTextAlignment(.leading)
                    .frame(maxWidth: .infinity, alignment: .leading)
                if let selected = latest.selectedResponse?.trimmingCharacters(in: .whitespacesAndNewlines), !selected.isEmpty {
                    Text("response: \(selected)")
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.48))
                        .lineLimit(1)
                }
            }
            Image(systemName: "play.fill")
                .font(.system(size: 11, weight: .black))
                .foregroundStyle(.white.opacity(0.34))
        }
        .padding(9)
        .background(Color.white.opacity(0.045))
        .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).stroke(Color.white.opacity(0.10), lineWidth: 1))
        .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
    }

    private func transmissionRow(_ item: NativeUserTransmissionItem) -> some View {
        HStack(alignment: .center, spacing: 12) {
            transmissionCircleThumb(item)
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 6) {
                    Text(transmissionDirectionLabel(item))
                        .font(.system(size: 10, weight: .black))
                        .foregroundStyle(item.direction == "received" ? Color(red: 1.0, green: 0.84, blue: 0.38) : Color(red: 0.66, green: 1.0, blue: 0.76))
                    Text(humanTime(item.updatedAt ?? item.createdAt ?? 0))
                        .font(.system(size: 11, weight: .bold))
                        .foregroundStyle(.white.opacity(0.50))
                    Spacer(minLength: 0)
                    if let depth = item.chainDepth, depth > 0 {
                        Text("CHAIN \(depth)")
                            .font(.system(size: 9, weight: .black))
                            .foregroundStyle(.white.opacity(0.42))
                    }
                }
                Text(transmissionTitle(item))
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(.white)
                    .lineLimit(2)
                    .multilineTextAlignment(.leading)
                    .frame(maxWidth: .infinity, alignment: .leading)
                if let selected = item.selectedResponse?.trimmingCharacters(in: .whitespacesAndNewlines), !selected.isEmpty {
                    Text("response: \(selected)")
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.48))
                        .lineLimit(1)
                }
            }
            Image(systemName: "play.fill")
                .font(.system(size: 11, weight: .black))
                .foregroundStyle(.white.opacity(0.34))
        }
        .padding(9)
        .background(Color.white.opacity(0.045))
        .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).stroke(Color.white.opacity(0.10), lineWidth: 1))
        .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
    }

    private func transmissionDirectionLabel(_ item: NativeUserTransmissionItem) -> String {
        let parent = item.parentJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if isOwnTransmission(item) {
            return parent.isEmpty ? "YOU SENT" : "YOU REPLIED"
        }
        let actor = transmissionActorLabel(item).uppercased()
        return parent.isEmpty ? "\(actor) SENT" : "\(actor) REPLIED"
    }

    private func transmissionActorLabel(_ item: NativeUserTransmissionItem) -> String {
        let display = item.ownerDisplayName?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !display.isEmpty { return display }
        let name = item.ownerName?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !name.isEmpty { return name }
        let callsign = item.ownerCallsign?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !callsign.isEmpty { return callsign }
        let owner = item.ownerUserId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if owner.count > 8 { return String(owner.prefix(8)) }
        return owner.isEmpty ? "K1L0" : owner
    }

    private func isOwnTransmission(_ item: NativeUserTransmissionItem) -> Bool {
        let direction = (item.direction ?? "").trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        if direction == "sent" { return true }
        guard let current = currentNativeUserId() else { return false }
        let owner = item.ownerUserId?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() ?? ""
        return !owner.isEmpty && owner == current.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
    }

    private func transmissionCircleThumb(_ item: NativeUserTransmissionItem) -> some View {
        ZStack {
            Circle()
                .fill(Color.white.opacity(0.07))
            if let raw = item.thumbUrl, let url = URL(string: raw) {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image):
                        image.resizable().scaledToFill()
                    case .failure:
                        Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.yellow)
                    default:
                        ProgressView().tint(.white)
                    }
                }
            }
        }
        .frame(width: 54, height: 54)
        .clipShape(Circle())
        .overlay(Circle().stroke(Color.white.opacity(0.24), lineWidth: 1.2))
    }

    private func transmissionTitle(_ item: NativeUserTransmissionItem) -> String {
        let plot = item.responsePlot?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !plot.isEmpty {
            return cleanedTransmissionTitle(plot, item: item)
        }
        let status = item.status?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return status.isEmpty ? "Transmission" : (status.lowercased() == "planning" ? "transmitting..." : status)
    }

    private func cleanedTransmissionTitle(_ raw: String, item: NativeUserTransmissionItem) -> String {
        var title = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        title = title.replacingOccurrences(
            of: #"(?i)^\s*the person(?:\s+responds)?\s*:\s*"#,
            with: "",
            options: .regularExpression
        )
        let actor = transmissionActorLabel(item).trimmingCharacters(in: .whitespacesAndNewlines)
        if !actor.isEmpty {
            let escapedActor = NSRegularExpression.escapedPattern(for: actor)
            title = title.replacingOccurrences(
                of: #"(?i)^\s*\#(escapedActor)(?:\s+responds|\s+replied)?\s*:\s*"#,
                with: "",
                options: .regularExpression
            )
        }
        title = title.replacingOccurrences(
            of: #"(?i)^\s*[A-Z][A-Za-z0-9 ._'’-]{1,42}\s+(?:responds|replied)\s*:\s*"#,
            with: "",
            options: .regularExpression
        )
        return title.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func humanTime(_ raw: Double) -> String {
        guard raw > 0 else { return "now" }
        let seconds = raw > 9_999_999_999 ? raw / 1000.0 : raw
        let elapsed = max(0, Date().timeIntervalSince1970 - seconds)
        if elapsed < 60 { return "now" }
        if elapsed < 3600 { return "\(Int(elapsed / 60))m ago" }
        if elapsed < 86400 { return "\(Int(elapsed / 3600))h ago" }
        if elapsed < 604800 { return "\(Int(elapsed / 86400))d ago" }
        let formatter = DateFormatter()
        formatter.dateFormat = "MMM d"
        return formatter.string(from: Date(timeIntervalSince1970: seconds))
    }

    private func transmissionClip(_ item: NativeUserTransmissionItem) -> K1L0TransmissionClip {
        let videoURL = item.playbackVideoUrl.flatMap { URL(string: $0) }
        let imageURL = item.thumbUrl.flatMap { URL(string: $0) }
        let parentJob = item.parentJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return K1L0TransmissionClip(
            videoURL: videoURL,
            imageURL: imageURL,
            audioURL: item.audioUrl.flatMap { URL(string: $0) },
            responsePlot: item.responsePlot?.trimmingCharacters(in: .whitespacesAndNewlines) ?? "",
            responseOptions: item.responseOptions ?? [],
            selectedResponse: item.selectedResponse?.trimmingCharacters(in: .whitespacesAndNewlines) ?? "",
            sourceJobId: item.jobId,
            sourceUserId: item.ownerUserId ?? "",
            sourceName: {
                if isOwnTransmission(item) {
                    let cs = (item.ownerCallsign ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
                    if !cs.isEmpty { return cs }
                }
                return item.ownerDisplayName ?? item.ownerName ?? item.ownerCallsign ?? ""
            }(),
            sourceCallsign: item.ownerCallsign ?? "",
            sourceCity: item.sourceCity ?? "",
            sourceCountry: item.sourceCountry ?? "",
            sourceCountryCode: item.sourceCountryCode ?? "",
            createdAt: item.createdAt ?? item.updatedAt ?? 0,
            allowsResponse: !parentJob.isEmpty && !isOwnTransmission(item)
        )
    }

    private func openTransmissionChain(_ group: NativeTransmissionChainGroup) {
        var clips = group.orderedItems
            .map { transmissionClip($0) }
            .filter { $0.videoURL != nil || $0.imageURL != nil }
        guard let first = clips.first else { return }
        let latest = group.latest
        let viewerIsOriginalAuthor = group.orderedItems.first.map { isOwnTransmission($0) } ?? false
        let latestResponseOptions = clips.indices.reversed()
            .first(where: { clips[$0].allowsResponse })
            .map { clips[$0].responseOptions } ?? []
        K1L0TransmissionResultStore.shared.current = K1L0TransmissionResult(
            status: latest.status ?? "ready",
            imageURL: first.imageURL,
            videoURL: first.videoURL,
            audioURL: first.audioURL,
            lyrics: "",
            responsePlot: first.responsePlot,
            responseOptions: latestResponseOptions,
            clips: clips,
            allowsResponseOptions: !viewerIsOriginalAuthor && clips.contains { $0.allowsResponse },
            allowsTextResponse: clips.contains { $0.allowsResponse },
            selectedResponse: first.selectedResponse
        )
    }

    private func openTransmission(_ item: NativeUserTransmissionItem) {
        let videoURL = item.playbackVideoUrl.flatMap { URL(string: $0) }
        let imageURL = item.thumbUrl.flatMap { URL(string: $0) }
        guard videoURL != nil || imageURL != nil else { return }
        let responseOptions = item.responseOptions ?? []
        K1L0TransmissionResultStore.shared.current = K1L0TransmissionResult(
            status: item.status ?? "ready",
            imageURL: imageURL,
            videoURL: videoURL,
            audioURL: item.audioUrl.flatMap { URL(string: $0) },
            lyrics: "",
            responsePlot: item.responsePlot?.trimmingCharacters(in: .whitespacesAndNewlines) ?? "",
            responseOptions: responseOptions,
            allowsResponseOptions: false,
            allowsTextResponse: false,
            selectedResponse: item.selectedResponse?.trimmingCharacters(in: .whitespacesAndNewlines)
        )
    }

    private func loadTransmissions() {
        guard let userId = currentNativeUserId(), !userId.isEmpty else {
            transmissions = []
            transmissionsStatus = "not signed in."
            return
        }
        transmissionsStatus = "loading transmissions…"
        fetchTransmissions(userId: userId, apiIndex: 0)
    }

    private func fetchTransmissions(userId: String, apiIndex: Int) {
        let candidates = [
            "https://api-tunnel.kilo.gallery",
            "http://192.168.40.34:3000",
            "http://fred.local:3000",
            "https://api.kilomeme.com"
        ]
        guard apiIndex < candidates.count else {
            DispatchQueue.main.async {
                transmissions = []
                transmissionsStatus = "transmissions unavailable."
            }
            return
        }
        let safeUser = userId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? userId
        guard let url = URL(string: "\(candidates[apiIndex])/api/k1l0/v2/my-transmissions?userId=\(safeUser)") else {
            fetchTransmissions(userId: userId, apiIndex: apiIndex + 1)
            return
        }
        URLSession.shared.dataTask(with: url) { data, response, _ in
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            guard code == 200,
                  let data,
                  let decoded = try? JSONDecoder().decode(NativeUserTransmissionResponse.self, from: data),
                  decoded.ok
            else {
                fetchTransmissions(userId: userId, apiIndex: apiIndex + 1)
                return
            }
            DispatchQueue.main.async {
                transmissions = decoded.transmissions
                transmissionsStatus = decoded.transmissions.isEmpty ? "no transmissions yet." : ""
            }
        }.resume()
    }

    private func currentNativeUserId() -> String? {
        let defaults = UserDefaults.standard
        for key in ["K1L0UserId", "FirebaseUserId", "DeviceID", "deviceID"] {
            let value = defaults.string(forKey: key) ?? ""
            let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
            if !trimmed.isEmpty { return trimmed }
        }
        return nil
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
                .overlay(Circle().stroke(Color.white.opacity(0.24), lineWidth: 1.2))
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
            .overlay(Circle().stroke(Color.white.opacity(0.24), lineWidth: 1.2))
        }
#elseif canImport(AppKit)
        if let selfie {
            Image(nsImage: selfie)
                .resizable()
                .scaledToFill()
                .frame(width: 96, height: 96)
                .clipShape(Circle())
                .overlay(Circle().stroke(Color.white.opacity(0.24), lineWidth: 1.2))
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
            .overlay(Circle().stroke(Color.white.opacity(0.24), lineWidth: 1.2))
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
        .overlay(Rectangle().stroke(Color.white.opacity(0.24), lineWidth: 1.2))
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

    // The user panel's primary avatar is the same helmet-shot identity used
    // throughout nearby users, leaderboards, and transmissions. The full
    // cloak render remains available inside the dedicated Avatar editor.
    @ViewBuilder
    private var renderedHero: some View {
        let heroWidth: CGFloat = 132
        ZStack {
            K1L0UserAvatar(
                urlString: draft.helmetUrl.isEmpty ? nil : draft.helmetUrl,
                size: heroWidth
            )

            if saveStore.isSaving {
                ZStack {
                    Circle().fill(Color.black.opacity(0.68))
                    VStack(spacing: 8) {
                        ProgressView()
                            .tint(.white)
                            .scaleEffect(0.8)
                        Text("regenerating")
                            .font(.system(size: 10, weight: .black, design: .monospaced))
                            .foregroundStyle(.white)
                            .multilineTextAlignment(.center)
                    }
                    .padding(4)
                }
            }
        }
        .frame(width: heroWidth, height: heroWidth)
    }

    @ViewBuilder
    private var renderedIdentityFull: some View {
        let renderedUrl = draft.cloakUrl.isEmpty ? draft.avatarUrl : draft.cloakUrl
        let thumbWidth: CGFloat = 132
        let thumbHeight: CGFloat = 180
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
                .frame(maxWidth: thumbWidth, maxHeight: thumbHeight)
            } else {
                placeholderRenderedHero(width: thumbWidth)
            }

            if saveStore.isSaving {
                ZStack {
                    Color.black.opacity(0.68)
                    VStack(spacing: 8) {
                        ProgressView()
                            .tint(.white)
                            .scaleEffect(0.9)
                        Text("regenerating")
                            .font(.system(size: 11, weight: .black, design: .monospaced))
                            .foregroundStyle(.white)
                            .multilineTextAlignment(.center)
                    }
                    .padding(6)
                }
            }
        }
        .frame(width: thumbWidth, height: thumbHeight)
        .overlay(Rectangle().stroke(Color.white.opacity(renderedUrl.isEmpty ? 0.14 : 0.24), lineWidth: 1.2))
        .background(Color.white.opacity(0.035))
        .frame(maxWidth: .infinity, alignment: .center)
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
            .overlay(Rectangle().stroke(Color.white.opacity(urlString.isEmpty ? 0.14 : 0.24), lineWidth: 1.2))

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
            selfiePickerRequest = PhotoPickerRequest(source: source)
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
            "bio": draft.bio.trimmingCharacters(in: .whitespacesAndNewlines),
            "url": draft.url.trimmingCharacters(in: .whitespacesAndNewlines),
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

struct NativeMessagesPanel: View {
    var tabsMode: Bool = false
    let onClose: () -> Void
    @State private var transmissions: [NativeUserTransmissionItem] = []
    @State private var transmissionsStatus = "loading messages…"

    var body: some View {
        GeometryReader { geometry in
            let panelTop = geometry.safeAreaInsets.top
            ZStack(alignment: .top) {
                Color.clear.ignoresSafeArea()

                ZStack(alignment: .top) {
                    ScrollView(.vertical, showsIndicators: true) {
                        VStack(alignment: .leading, spacing: 14) {
                            Color.clear.frame(height: 54)

                            WeatherGlassCard {
                                VStack(alignment: .leading, spacing: 10) {
                                    HStack {
                                        Text("Messages")
                                            .font(.system(size: 25, weight: .bold))
                                        Spacer()
                                        Button(action: loadTransmissions) {
                                            Text("[ REFRESH ]")
                                                .font(.system(size: 11, weight: .black))
                                                .foregroundStyle(Color(red: 0.66, green: 1.0, blue: 0.76))
                                        }
                                        .buttonStyle(.plain)
                                    }

                                    if transmissions.isEmpty {
                                        Text(transmissionsStatus)
                                            .font(.system(size: 13, weight: .semibold))
                                            .foregroundStyle(.white.opacity(0.62))
                                    } else {
                                        VStack(spacing: 8) {
                                            ForEach(transmissionGroups) { group in
                                                Button {
                                                    openTransmissionChain(group)
                                                } label: {
                                                    transmissionGroupRow(group)
                                                }
                                                .buttonStyle(.plain)
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        .padding(.horizontal, 20)
                        .padding(.top, 24)
                        .padding(.bottom, 42)
                    }

                    MessagesPanelHeader(tabsMode: tabsMode, onClose: onClose, onRefresh: loadTransmissions)
                }
                .frame(width: geometry.size.width)
                .frame(maxHeight: max(520, geometry.size.height - panelTop))
                .background(tabsMode ? Color.clear : Color.black.opacity(0.18), in: RoundedRectangle(cornerRadius: 28, style: .continuous))
                .padding(.top, panelTop)
            }
            .ignoresSafeArea(edges: .bottom)
        }
        .onAppear(perform: loadTransmissions)
    }

    private var transmissionGroups: [NativeTransmissionChainGroup] {
        let visibleTransmissions = transmissions.filter { item in
            let status = (item.status ?? "").trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
            let hasVisual = item.playbackVideoUrl != nil || !(item.thumbUrl?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ?? true)
            return status != "error" || hasVisual
        }
        let grouped = Dictionary(grouping: visibleTransmissions) { item in
            let root = item.rootJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            if !root.isEmpty { return root }
            let parent = item.parentJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            if !parent.isEmpty { return parent }
            return item.jobId
        }
        return grouped.map { key, items in
            NativeTransmissionChainGroup(
                id: key,
                items: items.sorted {
                    ($0.updatedAt ?? $0.createdAt ?? 0) > ($1.updatedAt ?? $1.createdAt ?? 0)
                }
            )
        }
        .sorted {
            ($0.latest.updatedAt ?? $0.latest.createdAt ?? 0) > ($1.latest.updatedAt ?? $1.latest.createdAt ?? 0)
        }
    }

    private func transmissionGroupRow(_ group: NativeTransmissionChainGroup) -> some View {
        let latest = group.latest
        return HStack(alignment: .center, spacing: 12) {
            transmissionCircleThumb(latest)
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 6) {
                    Text(transmissionDirectionLabel(latest))
                        .font(.system(size: 10, weight: .black))
                        .foregroundStyle((latest.direction ?? "sent").lowercased() == "sent" ? Color(red: 0.66, green: 1.0, blue: 0.76) : Color(red: 1.0, green: 0.84, blue: 0.38))
                    Text(humanTime(latest.updatedAt ?? latest.createdAt ?? 0))
                        .font(.system(size: 11, weight: .bold))
                        .foregroundStyle(.white.opacity(0.50))
                    Spacer(minLength: 0)
                    if group.items.count > 1 {
                        Text("\(group.items.count)")
                            .font(.system(size: 9, weight: .black))
                            .foregroundStyle(.white.opacity(0.42))
                    }
                }
                Text(transmissionTitle(latest))
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(.white)
                    .lineLimit(2)
                    .multilineTextAlignment(.leading)
                    .frame(maxWidth: .infinity, alignment: .leading)
                if let selected = latest.selectedResponse?.trimmingCharacters(in: .whitespacesAndNewlines), !selected.isEmpty {
                    Text(selected)
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.48))
                        .lineLimit(1)
                }
            }
            Image(systemName: "play.fill")
                .font(.system(size: 11, weight: .black))
                .foregroundStyle(.white.opacity(0.34))
        }
        .padding(9)
        .background(Color.white.opacity(0.045))
        .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).stroke(Color.white.opacity(0.10), lineWidth: 1))
        .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
    }

    private func transmissionDirectionLabel(_ item: NativeUserTransmissionItem) -> String {
        let parent = item.parentJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if isOwnTransmission(item) {
            return parent.isEmpty ? "YOU SENT" : "YOU REPLIED"
        }
        let actor = transmissionActorLabel(item).uppercased()
        return parent.isEmpty ? "\(actor) SENT" : "\(actor) REPLIED"
    }

    private func transmissionActorLabel(_ item: NativeUserTransmissionItem) -> String {
        let display = item.ownerDisplayName?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !display.isEmpty { return display }
        let name = item.ownerName?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !name.isEmpty { return name }
        let callsign = item.ownerCallsign?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !callsign.isEmpty { return callsign }
        let owner = item.ownerUserId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if owner.count > 8 { return String(owner.prefix(8)) }
        return owner.isEmpty ? "K1L0" : owner
    }

    private func isOwnTransmission(_ item: NativeUserTransmissionItem) -> Bool {
        let direction = (item.direction ?? "").trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        if direction == "sent" { return true }
        guard let current = currentNativeUserId() else { return false }
        let owner = item.ownerUserId?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() ?? ""
        return !owner.isEmpty && owner == current.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
    }

    private func transmissionCircleThumb(_ item: NativeUserTransmissionItem) -> some View {
        ZStack {
            Circle().fill(Color.white.opacity(0.07))
            if let raw = item.thumbUrl, let url = URL(string: raw) {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image): image.resizable().scaledToFill()
                    case .failure: Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.yellow)
                    default: ProgressView().tint(.white)
                    }
                }
            }
        }
        .frame(width: 54, height: 54)
        .clipShape(Circle())
        .overlay(Circle().stroke(Color.white.opacity(0.24), lineWidth: 1.2))
    }

    private func transmissionTitle(_ item: NativeUserTransmissionItem) -> String {
        let plot = item.responsePlot?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if !plot.isEmpty { return cleanedTransmissionTitle(plot, item: item) }
        let status = item.status?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return status.isEmpty ? "Transmission" : (status.lowercased() == "planning" ? "transmitting..." : status)
    }

    private func cleanedTransmissionTitle(_ raw: String, item: NativeUserTransmissionItem) -> String {
        var title = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        title = title.replacingOccurrences(of: #"(?i)^\s*the person(?:\s+responds)?\s*:\s*"#, with: "", options: .regularExpression)
        let actor = transmissionActorLabel(item).trimmingCharacters(in: .whitespacesAndNewlines)
        if !actor.isEmpty {
            let escapedActor = NSRegularExpression.escapedPattern(for: actor)
            title = title.replacingOccurrences(of: #"(?i)^\s*\#(escapedActor)(?:\s+responds|\s+replied)?\s*:\s*"#, with: "", options: .regularExpression)
        }
        title = title.replacingOccurrences(of: #"(?i)^\s*[A-Z][A-Za-z0-9 ._'’-]{1,42}\s+(?:responds|replied)\s*:\s*"#, with: "", options: .regularExpression)
        return title.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func humanTime(_ raw: Double) -> String {
        guard raw > 0 else { return "now" }
        let seconds = raw > 9_999_999_999 ? raw / 1000.0 : raw
        let elapsed = max(0, Date().timeIntervalSince1970 - seconds)
        if elapsed < 60 { return "now" }
        if elapsed < 3600 { return "\(Int(elapsed / 60))m ago" }
        if elapsed < 86400 { return "\(Int(elapsed / 3600))h ago" }
        if elapsed < 604800 { return "\(Int(elapsed / 86400))d ago" }
        let formatter = DateFormatter()
        formatter.dateFormat = "MMM d"
        return formatter.string(from: Date(timeIntervalSince1970: seconds))
    }

    private func transmissionClip(_ item: NativeUserTransmissionItem) -> K1L0TransmissionClip {
        let parentJob = item.parentJobId?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return K1L0TransmissionClip(
            videoURL: item.playbackVideoUrl.flatMap { URL(string: $0) },
            imageURL: item.thumbUrl.flatMap { URL(string: $0) },
            audioURL: item.audioUrl.flatMap { URL(string: $0) },
            responsePlot: item.responsePlot?.trimmingCharacters(in: .whitespacesAndNewlines) ?? "",
            responseOptions: item.responseOptions ?? [],
            selectedResponse: item.selectedResponse?.trimmingCharacters(in: .whitespacesAndNewlines) ?? "",
            sourceJobId: item.jobId,
            sourceUserId: item.ownerUserId ?? "",
            sourceName: {
                if isOwnTransmission(item) {
                    let cs = (item.ownerCallsign ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
                    if !cs.isEmpty { return cs }
                }
                return item.ownerDisplayName ?? item.ownerName ?? item.ownerCallsign ?? ""
            }(),
            sourceCallsign: item.ownerCallsign ?? "",
            sourceCity: item.sourceCity ?? "",
            sourceCountry: item.sourceCountry ?? "",
            sourceCountryCode: item.sourceCountryCode ?? "",
            createdAt: item.createdAt ?? item.updatedAt ?? 0,
            allowsResponse: !parentJob.isEmpty && !isOwnTransmission(item)
        )
    }

    private func openTransmissionChain(_ group: NativeTransmissionChainGroup) {
        var clips = group.orderedItems
            .map { transmissionClip($0) }
            .filter { $0.videoURL != nil || $0.imageURL != nil }
        guard let first = clips.first else { return }
        let latest = group.latest
        let viewerIsOriginalAuthor = group.orderedItems.first.map { isOwnTransmission($0) } ?? false
        let latestResponseOptions = clips.indices.reversed()
            .first(where: { clips[$0].allowsResponse })
            .map { clips[$0].responseOptions } ?? []
        K1L0TransmissionResultStore.shared.current = K1L0TransmissionResult(
            status: latest.status ?? "ready",
            imageURL: first.imageURL,
            videoURL: first.videoURL,
            audioURL: first.audioURL,
            lyrics: "",
            responsePlot: first.responsePlot,
            responseOptions: latestResponseOptions,
            clips: clips,
            allowsResponseOptions: !viewerIsOriginalAuthor && clips.contains { $0.allowsResponse },
            allowsTextResponse: clips.contains { $0.allowsResponse },
            selectedResponse: first.selectedResponse
        )
    }

    private func loadTransmissions() {
        guard let userId = currentNativeUserId(), !userId.isEmpty else {
            transmissions = []
            transmissionsStatus = "not signed in."
            return
        }
        transmissionsStatus = "loading messages…"
        fetchTransmissions(userId: userId, apiIndex: 0)
    }

    private func fetchTransmissions(userId: String, apiIndex: Int) {
        let candidates = [
            "https://api-tunnel.kilo.gallery",
            "http://192.168.40.34:3000",
            "http://fred.local:3000",
            "https://api.kilomeme.com"
        ]
        guard apiIndex < candidates.count else {
            DispatchQueue.main.async {
                transmissions = []
                transmissionsStatus = "messages unavailable."
            }
            return
        }
        let safeUser = userId.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? userId
        guard let url = URL(string: "\(candidates[apiIndex])/api/k1l0/v2/my-transmissions?userId=\(safeUser)") else {
            fetchTransmissions(userId: userId, apiIndex: apiIndex + 1)
            return
        }
        URLSession.shared.dataTask(with: url) { data, response, _ in
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            guard code == 200,
                  let data,
                  let decoded = try? JSONDecoder().decode(NativeUserTransmissionResponse.self, from: data),
                  decoded.ok
            else {
                fetchTransmissions(userId: userId, apiIndex: apiIndex + 1)
                return
            }
            DispatchQueue.main.async {
                transmissions = decoded.transmissions
                transmissionsStatus = decoded.transmissions.isEmpty ? "no messages yet." : ""
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

struct MessagesPanelHeader: View {
    var tabsMode: Bool = false
    let onClose: () -> Void
    let onRefresh: () -> Void

    var body: some View {
        VStack(spacing: 5) {
            if !tabsMode {
                RoundedRectangle(cornerRadius: 3, style: .continuous)
                    .fill(Color.white.opacity(0.34))
                    .frame(width: 44, height: 5)
                    .padding(.top, 8)
            }
            ZStack {
                Text("Messages")
                    .font(.system(size: 20, weight: .black, design: .rounded))
                    .foregroundStyle(.white)
                    .frame(maxWidth: .infinity, alignment: .center)
                HStack {
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
                    Spacer()
                    Button(action: onRefresh) {
                        Image(systemName: "arrow.clockwise")
                            .font(.system(size: 15, weight: .black))
                            .foregroundStyle(.white)
                            .frame(width: 38, height: 38)
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding(.horizontal, 16)
            .padding(.bottom, 10)
        }
        .frame(maxWidth: .infinity)
        .background(Color.black.opacity(0.001))
        .contentShape(Rectangle())
        .overlay(Rectangle().fill(Color.white.opacity(tabsMode ? 0 : 0.08)).frame(height: 1), alignment: .bottom)
        .gesture(
            DragGesture(minimumDistance: 14)
                .onEnded { value in
                    guard !tabsMode else { return }
                    if value.translation.height > 70 && abs(value.translation.width) < value.translation.height {
                        onClose()
                    }
                }
        )
    }
}

struct SweepingGreenBackground: View {
    var body: some View {
        TimelineView(.animation(minimumInterval: 1.0 / 30.0)) { timeline in
            let time = timeline.date.timeIntervalSinceReferenceDate
            let offset = CGFloat(sin(time * 2.5)) * 0.5 + 0.5
            LinearGradient(
                colors: [
                    Color(red: 0.05, green: 0.70, blue: 0.20),
                    Color(red: 0.45, green: 0.98, blue: 0.55),
                    Color(red: 0.05, green: 0.70, blue: 0.20)
                ],
                startPoint: UnitPoint(x: offset - 0.5, y: 0.0),
                endPoint: UnitPoint(x: offset + 0.5, y: 1.0)
            )
        }
    }
}

struct EchoSignalView: View {
    var body: some View {
        TimelineView(.animation(minimumInterval: 1.0 / 30.0)) { timeline in
            let time = timeline.date.timeIntervalSinceReferenceDate
            ZStack {
                ForEach(0..<3, id: \.self) { i in
                    let progress = (time * 0.65 + Double(i) * 0.33).truncatingRemainder(dividingBy: 1.0)
                    Circle()
                        .stroke(Color.white.opacity(0.85 * (1.0 - progress)), lineWidth: 1.5)
                        .frame(width: 8 + progress * 24, height: 8 + progress * 24)
                }
                Circle()
                    .fill(Color(red: 0.2, green: 0.95, blue: 0.4))
                    .frame(width: 8, height: 8)
            }
            .frame(width: 32, height: 32)
        }
    }
}
