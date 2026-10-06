# NetUMP_Unity

Native Network MIDI 2.0 libraries + C# wrapper for integration with Unity projects.

## Why Network MIDI 2.0?

[**Network MIDI 2.0**](https://midi.org/network-midi-2-0-udp-overview) is the transport layer used by this template to discover compatible endpoints and exchange MIDI messages over a local network. 

This makes it possible to support workflows such as:

- Sending local MIDI data to a remote endpoint.
- Receiving remote MIDI into a local application.
- Building hybrid local + remote performance setups.
- Experimenting with distributed MIDI systems.

## Building the NetUMP Native Plugin Libraries

This project provides native shared libraries to be used as plugins inside Unity. The native code resides in the `native_netump` folder.

1. The script generates build files using CMake with the specified platform and build type.
2. It compiles the native library as a *shared object* or *DLL*.
3. The resulting native plugin library is copied automatically into the appropriate folder inside this project under:

```sh
NetUMP_Unity/unity/Assets/Plugins/NetUMP/{Platform}/{Architecture}/
```

where `{Platform}` is one of `Android`, `Windows`, `Linux`, or `Apple`.

---

## Prerequisites

- **Android NDK** installed and `ANDROID_NDK` environment variable set (for Android).
- **CMake** and **Ninja** installed and available in your system PATH.
- **MSBuild** installed and available in your system PATH (for Windows).
- **C/C++ compiler/toolchain** installed and available in your system (for Linux/Apple).

> Build-script target selection does not, by itself, provide a cross-compilation toolchain. When building for a platform different from your host operating system, ensure that the required toolchain is installed and supported by the build configuration.

---

## Build Instructions

### **Windows**

On Windows, use **Command Prompt** (cmd), **not PowerShell** for running batch scripts.

1. Open a **Command Prompt** (cmd).
2. Navigate to the `native_netump` folder.
3. Run the build script with desired platform and build type:

- `platform` (optional): `android`, `linux`, `windows`, `apple`, or `all` (default: `all`)
- `build_type` (optional): `Debug` or `Release` (default: `Release`)

**Example:**
Build the Linux plugin in Release mode.

```sh
build_native.bat linux Release
```

---

### **Unix (Linux/macOS)**

1. Open a terminal.
2. Navigate to the `native_netump` folder.
3. Run the build script with desired platform and build type.

**Example:**
Build the Android plugin in Debug mode.

```sh
./build_native.sh android Debug
```

## Integrating into a Unity project

### Copy the native plugins

After a successful build, locate the generated libraries under:

```text
NetUMP_Unity/unity/Assets/Plugins/NetUMP/
```

In your destination Unity project, create the following directory if it does not already exist:

```text
Assets/Plugins/
```

Copy the generated `NetUMP` folder into it, preserving the platform and architecture subfolders:

```text
<Your_Unity_Project>/
└── Assets/
    └── Plugins/
        └── NetUMP/
            └── {Platform}/
                └── {Architecture}/
                    └── <generated native plugin files>
```

You can copy only the platform and architecture folders needed by your project.

### Copy the C# wrapper

1. Create a `Scripts` folder inside your Unity project's `Assets` directory, unless it already exists.
2. Locate `NetUMPWrapper.cs` in this repository.
3. Copy it into:

```text
<Your_Unity_Project>/Assets/Scripts/NetUMPWrapper.cs
```

Your project should now contain:

```text
YourUnityProject/
└── Assets/
    ├── Plugins/
    │   └── NetUMP/
    │       └── {Platform}/
    │           └── {Architecture}/
    │               └── <generated native plugin files>
    └── Scripts/
        └── NetUMPWrapper.cs
```

Use the API exposed by `NetUMPWrapper.cs` from your Unity C# code to interact with the native NetUMP library.
