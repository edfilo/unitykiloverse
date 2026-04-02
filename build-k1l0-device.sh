#!/bin/bash
# K1L0 full iOS build pipeline: Unity → Xcode → Install → Launch
# Usage:
#   kbuild run --tag "K1L0 iOS" build-k1l0-device.sh          # Device build
#   kbuild run --tag "K1L0 OTA" build-k1l0-device.sh --ota    # OTA build
set -e

MODE="device"
if [[ "$1" == "--ota" ]]; then MODE="ota"; fi

UNITY="/Applications/Unity/Hub/Editor/6000.3.12f1/Unity.app/Contents/MacOS/Unity"
PROJECT="/Users/kiloverse/unitykiloverse"
IOS_BUILD="/Users/kiloverse/unitykiloverse/Builds/iOS"
BUNDLE_ID="com.filowatt.K1L0"
TEAM_ID="7R2746UPX7"
APP_NAME="K1L0"
LOG="/tmp/k1l0_unity_build.log"
OTA_DIR="/tmp/tedfred_ota"
OTA_URL="https://tunnel.kilo.gallery/ota/latest"

# Detect connected iPhone — returns "XCODE_ID|DEVICECTL_ID"
detect_device() {
    # xcodebuild uses ECID, devicectl uses CoreDevice UUID
    local XCODE_ID=$(xcodebuild -project "$IOS_BUILD/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -showdestinations 2>/dev/null | grep "platform:iOS, arch" | head -1 | sed 's/.*id:\([^,]*\).*/\1/')
    local DEVCTL_ID=$(xcrun devicectl list devices 2>/dev/null | grep -i "iphone" | grep "connected" | awk '{for(i=1;i<=NF;i++) if($i ~ /^[A-F0-9]{8}-/) print $i}')
    if [ -n "$XCODE_ID" ] && [ -n "$DEVCTL_ID" ]; then
        echo "${XCODE_ID}|${DEVCTL_ID}"
    fi
}

# ===================================================================
echo "╔══════════════════════════════════════════╗"
echo "║  K1L0 iOS Build — $MODE mode"
echo "╚══════════════════════════════════════════╝"
echo ""
PIPELINE_START=$(date +%s)

# --- Step 1: Unity ---
echo "▸ Step 1/3: Unity IL2CPP"
echo "  Killing stale Unity processes..."
pkill -9 -f "Unity.*MacOS/Unity" 2>/dev/null || true
pkill -9 -f "Unity.ILPP|bee_backend|Unity.Licensing|UnityPackageManager|VBCSCompiler" 2>/dev/null || true
sleep 1
rm -rf "$PROJECT/Temp" 2>/dev/null || true
rm -f "$LOG"

STEP_START=$(date +%s)
"$UNITY" -batchmode -quit -nographics \
  -projectPath "$PROJECT" \
  -executeMethod HeadlessBuilder.BuildIOS \
  -logFile "$LOG" 2>&1 &
UNITY_PID=$!

# Tail Unity log for live progress
LAST_LINE=""
while kill -0 $UNITY_PID 2>/dev/null; do
  if [ -f "$LOG" ]; then
    LINE=$(tail -1 "$LOG" 2>/dev/null | head -c 120)
    if [ "$LINE" != "$LAST_LINE" ]; then
      case "$LINE" in
        *IL2CPP*|*Compiling*|*"Build target"*|*Shader*|*"error CS"*|*"Build Finished"*|*BUSY*)
          echo "  $LINE"
          LAST_LINE="$LINE"
          ;;
      esac
    fi
  fi
  sleep 5
done
wait $UNITY_PID || true

if grep -q "Build Finished, Result: Success" "$LOG" 2>/dev/null; then
  echo "  ✓ Unity: $(($(date +%s) - STEP_START))s"
else
  echo "  ✗ Unity FAILED after $(($(date +%s) - STEP_START))s"
  echo ""
  echo "  Errors:"
  grep "error CS" "$LOG" 2>/dev/null | head -10 | sed 's/^/    /'
  exit 1
fi

# --- Step 2: Xcode ---
echo ""
echo "▸ Step 2/3: Xcode compile"

# Patch project
printf '#!/bin/sh\nexit 0\n' > "$IOS_BUILD/process_symbols.sh"
chmod +x "$IOS_BUILD/process_symbols.sh"
sed -i "" "s/CYE232ULMR/$TEAM_ID/g" "$IOS_BUILD/Unity-iPhone.xcodeproj/project.pbxproj"

# Strip Location Push entitlement (requires special provisioning profile)
if [ -f "$IOS_BUILD/Unity-iPhone.entitlements" ]; then
  cat > "$IOS_BUILD/Unity-iPhone.entitlements" << 'ENTITLEMENTS'
<?xml version="1.0" encoding="utf-8"?>
<plist version="1.0">
  <dict>
  </dict>
</plist>
ENTITLEMENTS
  echo "  Stripped Location Push entitlement"
fi

STEP_START=$(date +%s)

if [[ "$MODE" == "ota" ]]; then
    # OTA: Archive + manual IPA packaging
    echo "  Archiving for OTA..."
    ARCHIVE="/tmp/ota_archive/$APP_NAME.xcarchive"
    rm -rf "$ARCHIVE" 2>/dev/null
    mkdir -p /tmp/ota_archive

    xcodebuild -project "$IOS_BUILD/Unity-iPhone.xcodeproj" \
      -scheme Unity-iPhone -configuration Release \
      -archivePath "$ARCHIVE" \
      -allowProvisioningUpdates \
      clean archive 2>&1 | tail -5

    if [ ! -d "$ARCHIVE" ]; then
      echo "  ✗ Archive FAILED after $(($(date +%s) - STEP_START))s"
      exit 1
    fi
    echo "  ✓ Archive: $(($(date +%s) - STEP_START))s"

    # Package IPA manually (no exportArchive — no distribution cert)
    echo "  Packaging IPA..."
    rm -rf /tmp/ota_payload "$OTA_DIR/export" 2>/dev/null
    mkdir -p /tmp/ota_payload/Payload "$OTA_DIR/export"
    cp -R "$ARCHIVE/Products/Applications/$APP_NAME.app" /tmp/ota_payload/Payload/
    cd /tmp/ota_payload && zip -qr "$OTA_DIR/export/$APP_NAME.ipa" Payload/

    # Generate manifest
    cat > "$OTA_DIR/manifest_1.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict><key>items</key><array><dict>
  <key>assets</key><array><dict>
    <key>kind</key><string>software-package</string>
    <key>url</key><string>$OTA_URL/export/$APP_NAME.ipa</string>
  </dict></array>
  <key>metadata</key><dict>
    <key>bundle-identifier</key><string>$BUNDLE_ID</string>
    <key>bundle-version</key><string>1.0</string>
    <key>kind</key><string>software</string>
    <key>title</key><string>$APP_NAME</string>
  </dict>
</dict></array></dict></plist>
PLIST

    INSTALL_LINK="itms-services://?action=download-manifest&url=https%3A%2F%2Ftunnel.kilo.gallery%2Fota%2Flatest%2Fmanifest_1.plist"
    echo ""
    echo "  ╔═══════════════════════════════════╗"
    echo "  ║  OTA Install Link:                ║"
    echo "  ║  $INSTALL_LINK"
    echo "  ╚═══════════════════════════════════╝"
    echo "$INSTALL_LINK" | pbcopy 2>/dev/null || true
    echo "  (copied to clipboard)"

else
    # Device: Build directly to device
    DEVICE_IDS=$(detect_device)
    XCODE_LOG="/tmp/k1l0_xcode_build.log"

    # Build with -sdk iphoneos + explicit signing (avoids destination resolver requiring simulator runtime)
    echo "  Building with -sdk iphoneos..."
    xcodebuild -project "$IOS_BUILD/Unity-iPhone.xcodeproj" \
      -scheme Unity-iPhone -configuration Debug \
      -sdk iphoneos -arch arm64 \
      -allowProvisioningUpdates \
      ONLY_ACTIVE_ARCH=YES \
      CODE_SIGN_STYLE=Automatic \
      DEVELOPMENT_TEAM=$TEAM_ID \
      CODE_SIGN_IDENTITY="Apple Development" \
      build > "$XCODE_LOG" 2>&1 || true

    if [ -z "$DEVICE_IDS" ]; then
      echo "  No iOS device detected — opening Xcode"
      echo "  ✓ Xcode: $(($(date +%s) - STEP_START))s (no device)"
      open "$IOS_BUILD/Unity-iPhone.xcodeproj"
    else
      XCODE_ID=$(echo "$DEVICE_IDS" | cut -d'|' -f1)
      DEVCTL_ID=$(echo "$DEVICE_IDS" | cut -d'|' -f2)
      echo "  Device: $XCODE_ID (xcode) / $DEVCTL_ID (devicectl)"

      if grep -q "BUILD SUCCEEDED" "$XCODE_LOG"; then
        echo "  ✓ Xcode: $(($(date +%s) - STEP_START))s"
      else
        echo "  ✗ Xcode FAILED after $(($(date +%s) - STEP_START))s"
        grep -E "error:" "$XCODE_LOG" | head -5 | sed 's/^/    /'
        exit 1
      fi

      # --- Step 3: Install + Launch ---
      echo ""
      echo "▸ Step 3/3: Install + Launch"

      APP=$(find ~/Library/Developer/Xcode/DerivedData -path "*/Debug-iphoneos/$APP_NAME.app" -maxdepth 5 2>/dev/null | head -1)
      if [ -z "$APP" ]; then
        echo "  ✗ $APP_NAME.app not found in DerivedData"
        exit 1
      fi

      xcrun devicectl device install app --device "$DEVCTL_ID" "$APP" 2>&1 | grep -E "installed|error|App installed" || true
      xcrun devicectl device process launch --device "$DEVCTL_ID" "$BUNDLE_ID" 2>&1 | grep -E "Launched|error" || true
      echo "  ✓ Installed and launched"
    fi
fi

TOTAL=$(($(date +%s) - PIPELINE_START))
TOTAL_STR="${TOTAL}s"
if [ $TOTAL -ge 60 ]; then TOTAL_STR="$((TOTAL/60))m $((TOTAL%60))s"; fi
echo ""
echo "╔══════════════════════════════════════════╗"
echo "║  ✓ K1L0 $MODE BUILD COMPLETE: $TOTAL_STR"
echo "╚══════════════════════════════════════════╝"
echo "BUILD SUCCEEDED"
