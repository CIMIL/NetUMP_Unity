#!/bin/bash

set -e

# Set Android NDK path if not set in environment
if [ -z "$ANDROID_NDK" ]; then
  echo "Please set ANDROID_NDK environment variable to your Android NDK path"
  exit 1
fi

# Resolve script directory (source directory)
SRC_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

# Default platform to all or override with first argument
PLATFORM="${1:-all}"

# Default build type to Release or override with second argument
BUILD_TYPE="${2:-Release}"

# Convert PLATFORM to lowercase
PLATFORM="${PLATFORM,,}"

echo "Build platform: $PLATFORM"
echo "Build type: $BUILD_TYPE"

# Determine Unity root (parent of native_netump)
UNITY_ROOT="$(dirname "$SRC_DIR")"

# Define plugin output folders
ANDROID_PLUGIN_DIR="$UNITY_ROOT/unity/Assets/Plugins/NetUMP/Android/arm64-v8a"
WINDOWS_PLUGIN_DIR="$UNITY_ROOT/unity/Assets/Plugins/NetUMP/Windows/x64"
LINUX_PLUGIN_DIR="$UNITY_ROOT/unity/Assets/Plugins/NetUMP/Linux/x64"
APPLE_PLUGIN_DIR="$UNITY_ROOT/unity/Assets/Plugins/NetUMP/Apple"

# Function to build Android
build_android() {
  BUILD_DIR="$SRC_DIR/build_android_$BUILD_TYPE"
  mkdir -p "$BUILD_DIR"
  pushd "$BUILD_DIR" > /dev/null

  cmake -G Ninja \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK/build/cmake/android.toolchain.cmake" \
    -DANDROID_ABI=arm64-v8a \
    -DANDROID_NATIVE_API_LEVEL=24 \
    "$SRC_DIR"

  ninja netump

  mkdir -p "$ANDROID_PLUGIN_DIR"
  if [ -f netump.so ]; then
    cp -f netump.so "$ANDROID_PLUGIN_DIR/netump.so"
  else
    echo "ERROR: netump.so not found in $BUILD_DIR"
    exit 1
  fi

  popd > /dev/null
  echo "Android build and copy completed."
}

# Function to build Windows
build_windows() {
  BUILD_DIR="$SRC_DIR/build_windows_$BUILD_TYPE"
  mkdir -p "$BUILD_DIR"
  pushd "$BUILD_DIR" > /dev/null

  cmake -G Ninja -DCMAKE_BUILD_TYPE="$BUILD_TYPE" -A x64 "$SRC_DIR"

  ninja netump

  mkdir -p "$WINDOWS_PLUGIN_DIR"
  if [ -f netump.dll ]; then
    cp -f netump.dll "$WINDOWS_PLUGIN_DIR/netump.dll"
  else
    echo "ERROR: netump.dll not found in $BUILD_DIR"
    exit 1
  fi

  popd > /dev/null
  echo "Windows build and copy completed."
}

# Function to build Linux
build_linux() {
  BUILD_DIR="$SRC_DIR/build_linux_$BUILD_TYPE"
  mkdir -p "$BUILD_DIR"
  pushd "$BUILD_DIR" > /dev/null

  cmake -G Ninja -DCMAKE_BUILD_TYPE="$BUILD_TYPE" -DCMAKE_POSITION_INDEPENDENT_CODE=ON "$SRC_DIR"

  ninja netump

  mkdir -p "$LINUX_PLUGIN_DIR"
  if [ -f netump.so ]; then
    cp -f netump.so "$LINUX_PLUGIN_DIR/netump.so"
  else
    echo "ERROR: netump.so not found in $BUILD_DIR"
    exit 1
  fi

  popd > /dev/null
  echo "Linux build and copy completed."
}

# Function to build Apple (macOS)
build_apple() {
  BUILD_DIR="$SRC_DIR/build_apple_$BUILD_TYPE"
  mkdir -p "$BUILD_DIR"
  pushd "$BUILD_DIR" > /dev/null

  cmake -G Ninja -DCMAKE_BUILD_TYPE="$BUILD_TYPE" -DCMAKE_OSX_ARCHITECTURES="x86_64;arm64" "$SRC_DIR"

  ninja netump

  mkdir -p "$APPLE_PLUGIN_DIR"
  if [ -f netump.dylib ]; then
    cp -f netump.dylib "$APPLE_PLUGIN_DIR/netump.dylib"
  else
    echo "ERROR: netump.dylib not found in $BUILD_DIR"
    exit 1
  fi

  popd > /dev/null
  echo "Apple build and copy completed."
}

# Decide what to build
case "$PLATFORM" in
  android)
    build_android
    ;;
  windows)
    build_windows
    ;;
  linux)
    build_linux
    ;;
  apple)
    build_apple
    ;;
  all)
    build_android
    build_windows
    build_linux
    build_apple
    ;;
  *)
    echo "Unknown platform: $PLATFORM"
    echo "Supported platforms: android, windows, linux, apple, all"
    exit 1
    ;;
esac

echo "Build process finished."
