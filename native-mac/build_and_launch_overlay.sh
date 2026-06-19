#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP="/tmp/K1L0MacHUD.app"
OUT="$APP/Contents/MacOS/K1L0MacHUD"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS"
swiftc "$ROOT/native-mac/Sources/K1L0MacHUD.swift" -o "$OUT" -framework SwiftUI -framework AppKit -framework CoreLocation
cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleExecutable</key><string>K1L0MacHUD</string>
  <key>CFBundleIdentifier</key><string>com.filowatt.k1lo.machud</string>
  <key>CFBundleName</key><string>K1L0 Mac HUD</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST
pkill -f K1L0MacHUD.app/Contents/MacOS/K1L0MacHUD 2>/dev/null || true
open -n "$APP"
sleep 1
pgrep -f K1L0MacHUD.app/Contents/MacOS/K1L0MacHUD | head -1 >/tmp/k1l0_mac_hud.pid
echo "launched $APP pid $(cat /tmp/k1l0_mac_hud.pid)"
