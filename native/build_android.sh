#!/bin/bash

# Set Android NDK path if not set in environment
if [ -z "$ANDROID_NDK" ]; then
  echo "Please set ANDROID_NDK environment variable to your Android NDK path"
  exit 1
fi

SRC_DIR=$(dirname "$0")

# Default build type to Release or override with first argument
BUILD_TYPE=${1:-Release}

BUILD_DIR="$SRC_DIR/build_$BUILD_TYPE"

mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

cmake -G Ninja \
  -DCMAKE_BUILD_TYPE=$BUILD_TYPE \
  -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK/build/cmake/android.toolchain.cmake" \
  -DANDROID_ABI=arm64-v8a \
  -DANDROID_NATIVE_API_LEVEL=24 \
  "$SRC_DIR"

ninja netump

cp netump.so "$SRC_DIR/../unity/Assets/Plugins/NetUMP/Android/arm64-v8a/netump.so"

echo "Build ($BUILD_TYPE) and copy completed successfully."