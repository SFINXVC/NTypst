#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
NATIVE_DIR="$REPO_ROOT/native"
RUNTIMES_DIR="$REPO_ROOT/runtimes"
CONFIG="${1:-debug}"

if [ "$CONFIG" = "release" ]; then
    CARGO_FLAGS="--release"
    CARGO_OUT="release"
else
    CARGO_FLAGS=""
    CARGO_OUT="debug"
fi

echo "Building native library ($CONFIG)..."
cd "$NATIVE_DIR"
cargo build $CARGO_FLAGS

OS="$(uname -s)"
ARCH="$(uname -m)"

case "$OS-$ARCH" in
    Linux-x86_64)   RID="linux-x64";  LIB="libtypst_native.so" ;;
    Linux-aarch64)   RID="linux-arm64"; LIB="libtypst_native.so" ;;
    Darwin-x86_64)   RID="osx-x64";    LIB="libtypst_native.dylib" ;;
    Darwin-arm64)    RID="osx-arm64";   LIB="libtypst_native.dylib" ;;
    *)               echo "Unsupported platform: $OS-$ARCH"; exit 1 ;;
esac

mkdir -p "$RUNTIMES_DIR/$RID/native"
cp "$NATIVE_DIR/target/$CARGO_OUT/$LIB" "$RUNTIMES_DIR/$RID/native/"
echo "Copied $LIB → runtimes/$RID/native/"

cd "$REPO_ROOT"
dotnet build -c "$([ "$CONFIG" = "release" ] && echo Release || echo Debug)"
echo "Done."
