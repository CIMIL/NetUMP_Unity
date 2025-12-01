cmake_minimum_required(VERSION 3.10)
project(netump_unity)

# Set C++ standard
set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Add platform-specific target defines as required by NetUMP
if(ANDROID)
    add_definitions(-DLINUX)      # Keep existing Android Linux socket API define
    add_definitions(-DANDROID)
    add_definitions(-D__TARGET_LINUX__)  # Android treated as Linux platform
elseif(UNIX AND NOT APPLE)
    add_definitions(-DLINUX)
    add_definitions(-D__TARGET_LINUX__)  # Define Linux target
elseif(APPLE)
    add_definitions(-DMACOS)
    add_definitions(-D__TARGET_MAC__)    # Define Mac target
elseif(WIN32)
    add_definitions(-DWINDOWS)
    add_definitions(-DWIN32)
    add_definitions(-D_WINSOCK_DEPRECATED_NO_WARNINGS)
    add_definitions(-D__TARGET_WIN__)    # Define Windows target
endif()

# Paths to dependencies
set(NETUMP_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/NetUMP")
set(WRAPPER_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/wrapper")

# Include directories
include_directories(
    ${NETUMP_ROOT}
    ${WRAPPER_ROOT}
)

# NetUMP source files
set(NETUMP_SOURCES
    ${NETUMP_ROOT}/NetUMP.cpp
    ${NETUMP_ROOT}/NetUMP_SessionProtocol.cpp
    ${NETUMP_ROOT}/network.cpp
    ${NETUMP_ROOT}/SystemSleep.cpp
    )

# Unity wrapper source files
set(WRAPPER_SOURCES
    ${WRAPPER_ROOT}/NetUMPWrapper.cpp
)

# Create shared library
add_library(netump SHARED
    ${NETUMP_SOURCES}
    ${WRAPPER_SOURCES}
)

# Platform-specific settings
if(ANDROID)
    # Android-specific settings
    target_link_libraries(netump
        log        # Android logging
        android    # Android native API
    )
    
    # Enable hidden visibility by default, expose only EXPORT_API symbols
    set_target_properties(netump PROPERTIES
        CXX_VISIBILITY_PRESET hidden
        VISIBILITY_INLINES_HIDDEN ON
    )
    
elseif(UNIX AND NOT APPLE)
    # Linux-specific settings
    target_link_libraries(netump
        pthread
        rt
    )
    
    set_target_properties(netump PROPERTIES
        CXX_VISIBILITY_PRESET hidden
        VISIBILITY_INLINES_HIDDEN ON
    )
    
elseif(APPLE)
    # macOS-specific settings
    target_link_libraries(netump
        "-framework CoreFoundation"
        "-framework IOKit"
    )
    
    set_target_properties(netump PROPERTIES
        CXX_VISIBILITY_PRESET hidden
        VISIBILITY_INLINES_HIDDEN ON
    )
    
elseif(WIN32)
    # Windows-specific settings
    target_link_libraries(netump
        ws2_32     # Winsock2
        winmm      # Multimedia timers
    )
    
    # Set proper Windows DLL export/import
    target_compile_definitions(netump PRIVATE BUILDING_DLL)
endif()

# Compiler warnings
if(MSVC)
    target_compile_options(netump PRIVATE /W4)
else()
    target_compile_options(netump PRIVATE -Wall -Wextra -Wpedantic)
endif()

# Release optimizations
if(CMAKE_BUILD_TYPE STREQUAL "Release")
    if(MSVC)
        target_compile_options(netump PRIVATE /O2 /Ob2)
    else()
        target_compile_options(netump PRIVATE -O3)
    endif()
    
    # Strip symbols for smaller binary size
    if(NOT WIN32)
        set_target_properties(netump PROPERTIES LINK_FLAGS_RELEASE -s)
    endif()
endif()

# Debug symbols
if(CMAKE_BUILD_TYPE STREQUAL "Debug")
    if(NOT MSVC)
        target_compile_options(netump PRIVATE -g)
    endif()
endif()


# Output name (without lib prefix on all platforms for Unity compatibility)
# set_target_properties(netump PROPERTIES
#     PREFIX ""
#     OUTPUT_NAME "netump"
# )

# Install rules (optional)
install(TARGETS netump
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
    RUNTIME DESTINATION bin
)

# Print configuration summary
message(STATUS "")
message(STATUS "NetUMP Native Unity Plugin Configuration:")
message(STATUS "  Build Type:        ${CMAKE_BUILD_TYPE}")
message(STATUS "  System:            ${CMAKE_SYSTEM_NAME}")
message(STATUS "  Compiler:          ${CMAKE_CXX_COMPILER_ID}")
message(STATUS "  C++ Standard:      ${CMAKE_CXX_STANDARD}")
if(ANDROID)
    message(STATUS "  Android ABI:       ${ANDROID_ABI}")
    message(STATUS "  Android Platform:  ${ANDROID_PLATFORM}")
endif()
message(STATUS "")