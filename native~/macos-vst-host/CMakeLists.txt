cmake_minimum_required(VERSION 3.25)
project(VstHostNative VERSION 0.1.0 LANGUAGES CXX OBJCXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_OBJCXX_STANDARD 17)
set(CMAKE_OBJCXX_STANDARD_REQUIRED ON)

# Prefer Universal unless the caller overrides CMAKE_OSX_ARCHITECTURES.
if(NOT DEFINED CMAKE_OSX_ARCHITECTURES OR CMAKE_OSX_ARCHITECTURES STREQUAL "")
    set(CMAKE_OSX_ARCHITECTURES "arm64;x86_64" CACHE STRING "macOS architectures" FORCE)
endif()

# Shared Windows tree: sources + VST3 SDK checkout (Fetch-Vst3Sdk.*)
set(WIN_HOST_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../windows-vst-host")
set(VST3SDK_DIR "${WIN_HOST_DIR}/vst3sdk")
set(WIN_HOST_SRC "${WIN_HOST_DIR}/src")

if(NOT EXISTS "${VST3SDK_DIR}/public.sdk/source/vst/hosting/module_mac.mm")
    message(FATAL_ERROR
        "VST3 SDK missing module_mac.mm. Run:\n"
        "  ./Fetch-Vst3Sdk.sh\n"
        "from native~/windows-vst-host (SDK path: native~/windows-vst-host/vst3sdk).")
endif()

# Avoid SMTG_VST3_SDK / smtg_detect_xcode_version: Command Line Tools builds
# (no full Xcode.app) fail that gate even when AppleClang works fine.
set(SDK_ROOT "${VST3SDK_DIR}")
set(public_sdk_SOURCE_DIR "${SDK_ROOT}/public.sdk")
set(pluginterfaces_SOURCE_DIR "${SDK_ROOT}/pluginterfaces")
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -stdlib=libc++")
set(CMAKE_OBJCXX_FLAGS "${CMAKE_OBJCXX_FLAGS} -stdlib=libc++")

include_directories("${SDK_ROOT}")

# --- SDK libraries (hosting subset) ---
add_library(pluginterfaces STATIC
    "${pluginterfaces_SOURCE_DIR}/base/conststringtable.cpp"
    "${pluginterfaces_SOURCE_DIR}/base/coreiids.cpp"
    "${pluginterfaces_SOURCE_DIR}/base/funknown.cpp"
    "${pluginterfaces_SOURCE_DIR}/base/ustring.cpp"
)
target_include_directories(pluginterfaces PUBLIC "${SDK_ROOT}")
target_compile_definitions(pluginterfaces PUBLIC
    $<$<CONFIG:Debug>:DEVELOPMENT=1>
    $<$<NOT:$<CONFIG:Debug>>:RELEASE=1>
)

file(GLOB_RECURSE BASE_SOURCES "${SDK_ROOT}/base/source/*.cpp")
add_library(base STATIC ${BASE_SOURCES})
target_include_directories(base PUBLIC "${SDK_ROOT}")
target_link_libraries(base PUBLIC pluginterfaces)
target_compile_definitions(base PUBLIC
    $<$<CONFIG:Debug>:DEVELOPMENT=1>
    $<$<NOT:$<CONFIG:Debug>>:RELEASE=1>
)

set(HOSTING_DIR "${public_sdk_SOURCE_DIR}/source/vst/hosting")
add_library(sdk_hosting STATIC
    "${HOSTING_DIR}/connectionproxy.cpp"
    "${HOSTING_DIR}/eventlist.cpp"
    "${HOSTING_DIR}/hostclasses.cpp"
    "${HOSTING_DIR}/module.cpp"
    "${HOSTING_DIR}/module_mac.mm"
    "${HOSTING_DIR}/parameterchanges.cpp"
    "${HOSTING_DIR}/pluginterfacesupport.cpp"
    "${HOSTING_DIR}/plugprovider.cpp"
    "${HOSTING_DIR}/processdata.cpp"
    "${public_sdk_SOURCE_DIR}/source/vst/vstinitiids.cpp"
    "${public_sdk_SOURCE_DIR}/source/vst/utility/stringconvert.cpp"
    "${public_sdk_SOURCE_DIR}/source/common/commonstringconvert.cpp"
    "${public_sdk_SOURCE_DIR}/source/common/memorystream.cpp"
    "${public_sdk_SOURCE_DIR}/source/common/threadchecker_mac.mm"
)
target_include_directories(sdk_hosting PUBLIC "${SDK_ROOT}" "${public_sdk_SOURCE_DIR}")
target_link_libraries(sdk_hosting PUBLIC base pluginterfaces
    "-framework Cocoa"
    "-framework CoreFoundation"
)
set_source_files_properties(
    "${HOSTING_DIR}/module_mac.mm"
    "${public_sdk_SOURCE_DIR}/source/common/threadchecker_mac.mm"
    PROPERTIES
        COMPILE_FLAGS "-fobjc-arc"
        LANGUAGE OBJCXX
)

# --- Our MODULE bundle (Unity Plugins/macOS/VstHostNative.bundle) ---
add_library(VstHostNative MODULE
    "${WIN_HOST_SRC}/VstHostNative.cpp"
)
target_include_directories(VstHostNative PRIVATE
    "${SDK_ROOT}"
    "${public_sdk_SOURCE_DIR}"
    "${WIN_HOST_SRC}"
)
target_link_libraries(VstHostNative PRIVATE
    sdk_hosting
    base
    pluginterfaces
    "-framework Cocoa"
    "-framework CoreFoundation"
)
target_compile_options(VstHostNative PRIVATE
    -fvisibility=hidden
    -fvisibility-inlines-hidden
)

set_target_properties(VstHostNative PROPERTIES
    BUNDLE TRUE
    BUNDLE_EXTENSION bundle
    PREFIX ""
    OUTPUT_NAME "VstHostNative"
    MACOSX_BUNDLE_BUNDLE_NAME "VstHostNative"
    MACOSX_BUNDLE_GUI_IDENTIFIER "jp.kshoji.unity.vst3nativehost"
    MACOSX_BUNDLE_BUNDLE_VERSION "${PROJECT_VERSION}"
    MACOSX_BUNDLE_SHORT_VERSION_STRING "${PROJECT_VERSION}"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)

# --- Smoke test (dev only) ---
# MODULE libraries cannot be linked into executables (CMake); compile the bridge
# sources directly into the smoke binary instead of linking the .bundle.
add_executable(VstHostSmokeTest
    "${WIN_HOST_DIR}/tools/SmokeTest.cpp"
    "${WIN_HOST_SRC}/VstHostNative.cpp"
)
target_include_directories(VstHostSmokeTest PRIVATE
    "${SDK_ROOT}"
    "${public_sdk_SOURCE_DIR}"
    "${WIN_HOST_SRC}"
)
target_link_libraries(VstHostSmokeTest PRIVATE
    sdk_hosting
    base
    pluginterfaces
    "-framework Cocoa"
    "-framework CoreFoundation"
)
set_target_properties(VstHostSmokeTest PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)

# Install copies the .bundle into the UPM Plugins tree
install(DIRECTORY "$<TARGET_BUNDLE_DIR:VstHostNative>"
    DESTINATION "${CMAKE_CURRENT_SOURCE_DIR}/../../Plugins/macOS"
    USE_SOURCE_PERMISSIONS
)
