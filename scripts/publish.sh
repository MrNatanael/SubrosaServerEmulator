#!/bin/bash
set -euo pipefail
trap 'echo "Failed to build" >&2' ERR

cd "$(dirname "$(realpath "$0")")" || exit 1

echo "========== [ COMPILING INTERCEPT LIBS ] =========="
echo ""
bash compile_libs.sh
echo ""
echo "========== [ COMPILING MASTER SERVER ] =========="
bash compile_server.sh
echo ""

trap 'echo "Failed to pack archives" >&2' ERR
echo "================== [ PACKING ] =================="
echo ""
mkdir -p "../build/dist"

BIN="../build/bin"
DIST="../dist"
for RID in win-x64 linux-x64; do
    echo "Packing $RID"

    (
        cd "$BIN"
        zip -qr "$DIST/SubrosaServerEmulator-$RID.zip" "Master/$RID"
    )
done

echo "Packing libs"
(
    cd "$BIN"
    zip -qr "$DIST/Intercept.zip" "libs"
)

echo ""
echo "================= [ FINISHED ] ================="