#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
BUILD_ROOT="/tmp/k1l0_native_build"
APP_DIR="$BUILD_ROOT/K1L0.app"
PAYLOAD_DIR="$BUILD_ROOT/ota_payload"
OTA_DIR="/tmp/tedfred_ota/k1l0/latest"
BUILD_NUMBER="$(date +%Y%m%d%H%M%S)"
VIDEOS_SRC="/tmp/k1l0_sky_video_refs/videos"
FALLBACK_VIDEOS_SRC="$(cd "$ROOT/.." && pwd)/Assets/StreamingAssets/WeatherVideos"
PROFILE="$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles/67f9ab53-0e20-4359-86f4-41dc044a69e0.mobileprovision"
IDENTITY="Apple Development: Ed Filowat (CYE232ULMR)"
SDK="$(xcrun --sdk iphoneos --show-sdk-path)"

rm -rf "$BUILD_ROOT"
mkdir -p "$APP_DIR/WeatherVideos" "$OTA_DIR"

if compgen -G "$VIDEOS_SRC/*.mp4" > /dev/null; then
  cp "$VIDEOS_SRC"/*.mp4 "$APP_DIR/WeatherVideos/"
elif compgen -G "$FALLBACK_VIDEOS_SRC/*.mp4" > /dev/null; then
  cp "$FALLBACK_VIDEOS_SRC"/*.mp4 "$APP_DIR/WeatherVideos/"
else
  echo "warning: no sky video mp4s found in $VIDEOS_SRC or $FALLBACK_VIDEOS_SRC"
fi

cat > "$APP_DIR/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleDevelopmentRegion</key><string>en</string>
  <key>CFBundleDisplayName</key><string>K1L0</string>
  <key>CFBundleExecutable</key><string>K1L0</string>
  <key>CFBundleIdentifier</key><string>com.filowatt.K1L0</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleName</key><string>K1L0</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>1.1</string>
  <key>CFBundleVersion</key><string>__BUILD_NUMBER__</string>
  <key>LSRequiresIPhoneOS</key><true/>
  <key>MinimumOSVersion</key><string>17.6</string>
  <key>NSMotionUsageDescription</key><string>K1L0 uses steps to measure signal strength.</string>
  <key>NSLocationWhenInUseUsageDescription</key><string>K1L0 uses location to find nearby fields.</string>
  <key>NSPhotoLibraryUsageDescription</key><string>K1L0 uses selected photos for transmissions.</string>
  <key>NSAppTransportSecurity</key><dict>
    <key>NSAllowsArbitraryLoads</key><true/>
  </dict>
  <key>UIApplicationSceneManifest</key><dict>
    <key>UIApplicationSupportsMultipleScenes</key><false/>
  </dict>
  <key>UILaunchScreen</key><dict>
    <key>UIColorName</key><string></string>
  </dict>
  <key>UISupportedInterfaceOrientations</key><array>
    <string>UIInterfaceOrientationPortrait</string>
  </array>
  <key>UIDeviceFamily</key><array><integer>1</integer></array>
</dict></plist>
PLIST
perl -0pi -e "s/__BUILD_NUMBER__/$BUILD_NUMBER/g" "$APP_DIR/Info.plist"

cat > "$BUILD_ROOT/Entitlements.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>application-identifier</key><string>7R2746UPX7.com.filowatt.K1L0</string>
  <key>com.apple.developer.team-identifier</key><string>7R2746UPX7</string>
  <key>get-task-allow</key><true/>
</dict></plist>
PLIST

swiftc \
  -target arm64-apple-ios17.6 \
  -sdk "$SDK" \
  -parse-as-library \
  -O \
  "$ROOT/Sources/K1L0NativeApp.swift" \
  -o "$APP_DIR/K1L0" \
  -framework SwiftUI \
  -framework UIKit \
  -framework AVFoundation \
  -framework AVKit \
  -framework CoreMotion \
  -framework MapKit \
  -framework CoreLocation \
  -framework PhotosUI

cp "$PROFILE" "$APP_DIR/embedded.mobileprovision"
/usr/bin/codesign --force --sign "$IDENTITY" --entitlements "$BUILD_ROOT/Entitlements.plist" --timestamp=none "$APP_DIR"

rm -rf "$PAYLOAD_DIR"
mkdir -p "$PAYLOAD_DIR/Payload"
cp -R "$APP_DIR" "$PAYLOAD_DIR/Payload/"
rm -f "$OTA_DIR/app.ipa"
(cd "$PAYLOAD_DIR" && zip -qr "$OTA_DIR/app.ipa" Payload)

cat > "$OTA_DIR/manifest.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict><key>items</key><array><dict>
  <key>assets</key><array><dict>
    <key>kind</key><string>software-package</string>
    <key>url</key><string>https://tunnel.kilo.gallery/ota/k1l0/latest/app.ipa</string>
  </dict></array>
  <key>metadata</key><dict>
    <key>bundle-identifier</key><string>com.filowatt.K1L0</string>
    <key>bundle-version</key><string>__BUILD_NUMBER__</string>
    <key>kind</key><string>software</string>
    <key>title</key><string>K1L0</string>
  </dict>
</dict></array></dict></plist>
PLIST
perl -0pi -e "s/__BUILD_NUMBER__/$BUILD_NUMBER/g" "$OTA_DIR/manifest.plist"

plutil -lint "$APP_DIR/Info.plist" "$BUILD_ROOT/Entitlements.plist" "$OTA_DIR/manifest.plist"
ls -lh "$APP_DIR/K1L0" "$OTA_DIR/app.ipa" "$OTA_DIR/manifest.plist"
echo "itms-services://?action=download-manifest&url=https://tunnel.kilo.gallery/ota/k1l0/latest/manifest.plist"
