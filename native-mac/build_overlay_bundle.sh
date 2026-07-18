#!/bin/bash
# Builds the lightweight macOS SwiftUI host with the shared weather presets
# into a universal macOS native plugin bundle: K1L0Overlay.bundle
#
# The large iOS HUD remains in its native file; Mac features migrate into this
# small host incrementally so routine builds never type-check the full HUD.
#
# Usage:
#   native-mac/build_overlay_bundle.sh [OUTPUT_DIR]
# If OUTPUT_DIR is omitted the bundle is written next to this script (native-mac/).
# The Mac build pipeline passes <K1L0.app>/Contents/Plugins as OUTPUT_DIR.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$ROOT/Assets/Plugins/iOS/K1L0WeatherOverlay.swift"
MODE_SRC="$ROOT/Assets/Plugins/iOS/K1L0WeatherModeController.swift"
SOLAR_SRC="$ROOT/Assets/Plugins/iOS/K1L0SolarEnvironmentSync.swift"
SPLITTER="$ROOT/native-mac/split_overlay_for_mac.sh"
OUT_DIR="${1:-$ROOT/native-mac}"
BUNDLE="$OUT_DIR/K1L0Overlay.bundle"
MACOS_DIR="$BUNDLE/Contents/MacOS"
DEPLOY_TARGET="12.0"

if [[ ! -f "$SRC" ]]; then
  echo "[build_overlay_bundle] source not found: $SRC" >&2
  exit 1
fi

echo "[build_overlay_bundle] source : $SRC"
echo "[build_overlay_bundle] bundle : $BUNDLE"

rm -rf "$BUNDLE"
mkdir -p "$MACOS_DIR"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
SPLIT_DIR="$TMP/Sources"
"$SPLITTER" "$SRC" "$SPLIT_DIR" >/dev/null
SWIFT_SOURCES=("$SPLIT_DIR"/*.swift "$MODE_SRC" "$SOLAR_SRC")
echo "[build_overlay_bundle] split sources: ${#SWIFT_SOURCES[@]}"

HOST_ARCH="$(uname -m)"
echo "[build_overlay_bundle] Host architecture detected: $HOST_ARCH"
SLICES=""
for ARCH in $HOST_ARCH; do
  echo "[build_overlay_bundle] compiling $ARCH..."
  if xcrun swiftc -Onone \
      -Xfrontend -solver-expression-time-threshold=60 \
      -target "${ARCH}-apple-macos${DEPLOY_TARGET}" \
      -emit-library -module-name K1L0Overlay \
      -o "$TMP/K1L0Overlay-$ARCH" \
      "${SWIFT_SOURCES[@]}" \
      -framework SwiftUI -framework AppKit -framework CoreLocation; then
    SLICES="$SLICES $TMP/K1L0Overlay-$ARCH"
  else
    echo "[build_overlay_bundle] WARNING: $ARCH slice failed, skipping" >&2
  fi
done

if [[ -z "${SLICES// }" ]]; then
  echo "[build_overlay_bundle] no slices built" >&2
  exit 1
fi

lipo -create $SLICES -output "$MACOS_DIR/K1L0Overlay"

cat > "$BUNDLE/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key><string>en</string>
  <key>CFBundleExecutable</key><string>K1L0Overlay</string>
  <key>CFBundleIdentifier</key><string>com.filowatt.K1L0.overlay</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleName</key><string>K1L0Overlay</string>
  <key>CFBundlePackageType</key><string>BNDL</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>NSPrincipalClass</key><string></string>
</dict>
</plist>
PLIST

# Ad-hoc sign so the bundle loads even before the app's deep re-sign.
codesign --force --sign - "$BUNDLE" >/dev/null 2>&1 || true

echo "[build_overlay_bundle] arches: $(lipo -archs "$MACOS_DIR/K1L0Overlay")"
echo "[build_overlay_bundle] exports K1L0InstallWeatherOverlay: $(nm -gU "$MACOS_DIR/K1L0Overlay" 2>/dev/null | grep -c K1L0InstallWeatherOverlay)"
echo "[build_overlay_bundle] done -> $BUNDLE"
