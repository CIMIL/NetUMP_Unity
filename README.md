# NetUMP_Unity

## TODOs

- [ ] Add the option to modify the localPort, destPort, etc. NetUMP parameters directly from the Unity editor.
- [ ] Examples and tests.



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

- **Android NDK** installed and `ANDROID_NDK` environment variable set.
- **CMake** and **Ninja** installed and available in your system PATH.

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
