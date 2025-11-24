@echo off
setlocal

REM Set NDK path here if not in environment
if not defined ANDROID_NDK (
    echo Please set the ANDROID_NDK environment variable to your Android NDK path.
    exit /b 1
)

REM Path to source directory
set SRC_DIR=%~dp0

REM Default build type to Release if not provided as first argument
set BUILD_TYPE=Release
if not "%1"=="" set BUILD_TYPE=%1

REM Create build directory based on build type
set BUILD_DIR=build_%BUILD_TYPE%
if not exist %BUILD_DIR% mkdir %BUILD_DIR%
cd %BUILD_DIR%

REM Run CMake for Android arm64-v8a
cmake -G Ninja -DCMAKE_BUILD_TYPE=%BUILD_TYPE% ^
      -DCMAKE_TOOLCHAIN_FILE=%ANDROID_NDK%\build\cmake\android.toolchain.cmake ^
      -DANDROID_ABI=arm64-v8a ^
      -DANDROID_NATIVE_API_LEVEL=24 ^
      ..

REM Build target
ninja netump

REM Copy .so to Unity plugin folder
copy netump.so ..\..\unity\Assets\Plugins\NetUMP\Android\arm64-v8a\netump.so

endlocal
