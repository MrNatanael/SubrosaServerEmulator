#!/bin/bash
cd "$(dirname "$(realpath "$0")")/../" || exit 1

PROJECT="SubrosaServerEmulator/SubrosaServerEmulator.csproj"
OUT="build/bin/Master"

rm -rf "$OUT"
mkdir -p "$OUT"

for RID in win-x64 linux-x64; do
  dotnet publish "$PROJECT" \
    -c Release \
    -r "$RID" \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$OUT/$RID"
done