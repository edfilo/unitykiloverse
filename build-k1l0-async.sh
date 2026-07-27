#!/bin/zsh
# Detached K1L0 local-first build for Codex/TedFred.
# The normal build script installs and launches when an iPhone is available,
# otherwise it automatically publishes the K1L0 OTA.
python3 /Users/kiloverse/scripts/ios_build_detach.py \
  "/Users/kiloverse/unitykiloverse/build-k1l0-device.sh" \
  /tmp/k1l0-build-status.txt \
  /tmp/k1l0-build.log
