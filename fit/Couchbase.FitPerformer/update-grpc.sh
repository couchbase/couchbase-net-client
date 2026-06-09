#!/usr/bin/env bash
#
# Updates the FIT proto files. This should be run manually when the protocol changes.
# Clones https://github.com/couchbaselabs/fit-protocol/tree/main/operational and replaces the contents of the gRPC folder.
# Requires `curl` and `tar`.
#
# Usage:
#   ./update-grpc.sh

set -euo pipefail

REPO="couchbaselabs/fit-protocol"
BRANCH="${BRANCH:-main}"
SUBDIR="operational"

# gRPC folder lives next to this script.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEST="$SCRIPT_DIR/gRPC"

# Verify required tooling is available.
for cmd in curl tar; do
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "error: required command '$cmd' was not found on PATH" >&2
    exit 1
  fi
done

# Work in a self-cleaning temp directory.
TMP="$(mktemp -d)"
cleanup() { rm -rf "$TMP"; }
trap cleanup EXIT

echo "Downloading ${REPO}@${BRANCH} ..."
curl -fsSL "https://codeload.github.com/${REPO}/tar.gz/refs/heads/${BRANCH}" -o "$TMP/src.tar.gz"

echo "Extracting archive ..."
tar -xzf "$TMP/src.tar.gz" -C "$TMP"

# Locate the extracted repo root (e.g. fit-protocol-main) without hard-coding it.
SRC_ROOT="$(find "$TMP" -maxdepth 1 -type d -name 'fit-protocol-*' | head -n1)"
SRC="${SRC_ROOT}/${SUBDIR}"
if [ ! -d "$SRC" ]; then
  echo "error: '${SUBDIR}' folder was not found in the downloaded archive" >&2
  exit 1
fi

echo "Mirroring '${SUBDIR}' into ${DEST} ..."
rm -rf "$DEST"
mkdir -p "$DEST"
# Copy the contents of operational/ (the trailing /. matters) into gRPC.
cp -R "$SRC"/. "$DEST"/

count="$(find "$DEST" -type f | wc -l | tr -d ' ')"
echo "Done. ${DEST} now mirrors '${SUBDIR}' (${count} files)."
