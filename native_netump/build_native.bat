@echo off
setlocal enabledelayedexpansion

REM Set NDK path here if not in environment
if not defined ANDROID_NDK (
    echo Please set the ANDROID_NDK environment variable to your Android NDK path.
    exit /b 1
)

REM Path to source directory (strip trailing backslash)
set "SRC_DIR=%~dp0"
if "%SRC_DIR:~-1%"=="\" set "SRC_DIR=%SRC_DIR:~0,-1%"

REM Resolve Unity root folder (parent of native_netump)
for %%I in ("%SRC_DIR%\..") do set "UNITY_ROOT=%%~fI"

REM Define Unity plugin folders per platform
set "ANDROID_PLUGIN_DIR=%UNITY_ROOT%\unity\Assets\Plugins\NetUMP\Android\arm64-v8a"
set "WINDOWS_PLUGIN_DIR=%UNITY_ROOT%\unity\Assets\Plugins\NetUMP\Windows\x64"
set "LINUX_PLUGIN_DIR=%UNITY_ROOT%\unity\Assets\Plugins\NetUMP\Linux\x64"
set "APPLE_PLUGIN_DIR=%UNITY_ROOT%\unity\Assets\Plugins\NetUMP\Apple"

REM Default platform to all if not provided as first argument
set "PLATFORM=all"
if not "%1"=="" set "PLATFORM=%1"

REM Default build type to Release if not provided as second argument
set "BUILD_TYPE=Release"
if not "%2"=="" set "BUILD_TYPE=%2"

REM Convert PLATFORM to lowercase
for %%A in (%PLATFORM%) do set "PLATFORM=%%A"
set "PLATFORM=%PLATFORM:"=%"
set "PLATFORM=!PLATFORM!"

echo Build platform: !PLATFORM!
echo Build type: !BUILD_TYPE!

REM Initialize all build flags to 0
set BUILD_ANDROID=0
set BUILD_WINDOWS=0
set BUILD_LINUX=0
set BUILD_APPLE=0

if /i "!PLATFORM!"=="all" (
    set BUILD_ANDROID=1
    set BUILD_WINDOWS=1
    set BUILD_LINUX=1
    set BUILD_APPLE=1
) else (
    if /i "!PLATFORM!"=="android" set BUILD_ANDROID=1
    if /i "!PLATFORM!"=="windows" set BUILD_WINDOWS=1
    if /i "!PLATFORM!"=="linux" set BUILD_LINUX=1
    if /i "!PLATFORM!"=="apple" set BUILD_APPLE=1
)

REM === Build Android ===
if !BUILD_ANDROID! == 1 (
    echo Building Android arm64-v8a...

    set "BUILD_DIR=build_android_!BUILD_TYPE!"
    if not exist "!BUILD_DIR!" mkdir "!BUILD_DIR!"
    pushd "!BUILD_DIR!"

    cmake -G Ninja -DCMAKE_BUILD_TYPE=!BUILD_TYPE! ^
          -DCMAKE_TOOLCHAIN_FILE=%ANDROID_NDK%\build\cmake\android.toolchain.cmake ^
          -DANDROID_ABI=arm64-v8a ^
          -DANDROID_NATIVE_API_LEVEL=24 ^
          "%SRC_DIR%"

    ninja netump

    if not exist "%ANDROID_PLUGIN_DIR%" mkdir "%ANDROID_PLUGIN_DIR%"

    if exist libnetump.so (
        copy /Y libnetump.so "%ANDROID_PLUGIN_DIR%\libnetump.so"
    ) else (
        echo ERROR: libnetump.so not found in !BUILD_DIR!
        pause
    )

    popd
)

REM === Build Windows ===
if !BUILD_WINDOWS! == 1 (
    echo Building Windows x64...

    set "BUILD_DIR=build_windows_!BUILD_TYPE!"
    if not exist "!BUILD_DIR!" mkdir "!BUILD_DIR!"
    pushd "!BUILD_DIR!"

    REM Configure with Visual Studio generator for x64
    cmake -G "Visual Studio 17 2022" -A x64 -DCMAKE_BUILD_TYPE=!BUILD_TYPE! "%SRC_DIR%"

    REM Build the netump target (release/debug)
    cmake --build . --config !BUILD_TYPE! --target netump

    REM Create plugin directory if not exists
    if not exist "%WINDOWS_PLUGIN_DIR%" mkdir "%WINDOWS_PLUGIN_DIR%"

    REM Copy built DLL (Visual Studio places output in config directory)
    if exist "!BUILD_TYPE!\netump.dll" (
        copy /Y "!BUILD_TYPE!\netump.dll" "%WINDOWS_PLUGIN_DIR%\netump.dll"
    ) else (
        echo ERROR: netump.dll not found in !BUILD_DIR!\!BUILD_TYPE!
        pause
    )

    popd
)

REM === Build Linux ===
if !BUILD_LINUX! == 1 (
    echo Building Linux x64...

    set "BUILD_DIR=build_linux_!BUILD_TYPE!"
    if not exist "!BUILD_DIR!" mkdir "!BUILD_DIR!"
    pushd "!BUILD_DIR!"

    cmake -G Ninja -DCMAKE_BUILD_TYPE=!BUILD_TYPE! -DCMAKE_POSITION_INDEPENDENT_CODE=ON "%SRC_DIR%"

    ninja netump

    if not exist "%LINUX_PLUGIN_DIR%" mkdir "%LINUX_PLUGIN_DIR%"

    if exist libnetump.so (
        copy /Y libnetump.so "%LINUX_PLUGIN_DIR%\libnetump.so"
    ) else (
        echo ERROR: libnetump.so not found in !BUILD_DIR!
        pause
    )

    popd
)

REM === Build Apple macOS ===
if !BUILD_APPLE! == 1 (
    echo Building Apple (macOS)...

    set "BUILD_DIR=build_apple_!BUILD_TYPE!"
    if not exist "!BUILD_DIR!" mkdir "!BUILD_DIR!"
    pushd "!BUILD_DIR!"

    cmake -G Ninja -DCMAKE_BUILD_TYPE=!BUILD_TYPE! ^
          -DCMAKE_OSX_ARCHITECTURES=arm64 ^
          "%SRC_DIR%"

    ninja netump

    if not exist "%APPLE_PLUGIN_DIR%" mkdir "%APPLE_PLUGIN_DIR%"

    if exist libnetump.dylib (
        copy /Y libnetump.dylib "%APPLE_PLUGIN_DIR%\libnetump.dylib"
    ) else (
        echo ERROR: libnetump.dylib not found in !BUILD_DIR!
        pause
    )

    popd
)

echo Build finished.

endlocal
