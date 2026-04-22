#!/usr/bin/env bash
# Rebuild AppleSignIn.bundle for Mac Unity standalone.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
SRC="$HERE/AppleSignIn_mac.mm"
OUT="$HERE/../Assets/Plugins/macOS/AppleSignIn.bundle/Contents/MacOS/AppleSignIn"
mkdir -p "$(dirname "$OUT")"
clang++ -bundle -o "$OUT" "$SRC" \
  -framework Foundation -framework AppKit -framework AuthenticationServices \
  -fobjc-arc \
  -mmacosx-version-min=10.15 \
  -arch arm64 -arch x86_64
echo "Built: $OUT"
file "$OUT"
