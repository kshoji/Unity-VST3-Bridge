cmake_minimum_required(VERSION 3.25)
project(VstHostNative VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# --- VST3 SDK paths ---
set(VST3SDK_DIR "${CMAKE_CURRENT_SOURCE_DIR}/vst3sdk")

# SDK cmake helpers
list(APPEND CMAKE_MODULE_PATH "${VST3SDK_DIR}/cmake/modules")
include(SMTG_VST3_SDK)
smtg_setup_platform_toolset()

set(SDK_ROOT "${VST3SDK_DIR}")
set(public_sdk_SOURCE_DIR "${SDK_ROOT}/public.sdk")
set(pluginterfaces_SOURCE_DIR "${SDK_ROOT}/pluginterfaces")

include_directories("${SDK_ROOT}" "${SDK_ROOT}")

# --- SDK libraries (hosting subset) ---
# pluginterfaces
add_library(pluginterfaces STATIC
    "${pluginterfaces_SOURCE_DIR}/base/conststringtable.cpp"
    "${pluginterfaces_SOURCE_DIR}/base/coreiids.cpp"
    "${pluginterfaces_SOURCE_DIR}/base/funknown.cpp"
    "${pluginterfaces_SOURCE_DIR}/base/ustring.cpp"
)
target_include_directories(pluginterfaces PUBLIC "${SDK_ROOT}")

# base
file(GLOB_RECURSE BASE_SOURCES "${SDK_ROOT}/base/source/*.cpp")
add_library(base STATIC ${BASE_SOURCES})
target_include_directories(base PUBLIC "${SDK_ROOT}")
target_link_libraries(base PUBLIC pluginterfaces)

# sdk_hosting — only the hosting helpers we need
set(HOSTING_DIR "${public_sdk_SOURCE_DIR}/source/vst/hosting")
add_library(sdk_hosting STATIC
    "${HOSTING_DIR}/connectionproxy.cpp"
    "${HOSTING_DIR}/eventlist.cpp"
    "${HOSTING_DIR}/hostclasses.cpp"
    "${HOSTING_DIR}/module.cpp"
    "${HOSTING_DIR}/module_win32.cpp"
    "${HOSTING_DIR}/parameterchanges.cpp"
    "${HOSTING_DIR}/pluginterfacesupport.cpp"
    "${HOSTING_DIR}/plugprovider.cpp"
    "${HOSTING_DIR}/processdata.cpp"
    "${public_sdk_SOURCE_DIR}/source/vst/vstinitiids.cpp"
    "${public_sdk_SOURCE_DIR}/source/vst/utility/stringconvert.cpp"
    "${public_sdk_SOURCE_DIR}/source/common/commonstringconvert.cpp"
    "${public_sdk_SOURCE_DIR}/source/common/threadchecker_win32.cpp"
)
target_include_directories(sdk_hosting PUBLIC "${SDK_ROOT}" "${public_sdk_SOURCE_DIR}")
target_link_libraries(sdk_hosting PUBLIC base pluginterfaces)

# --- Our shared library ---
add_library(VstHostNative SHARED
    src/VstHostNative.cpp
)
target_include_directories(VstHostNative PRIVATE
    "${SDK_ROOT}"
    "${public_sdk_SOURCE_DIR}"
    src
)
target_link_libraries(VstHostNative PRIVATE sdk_hosting base pluginterfaces)

# Windows-specific
if(WIN32)
    target_compile_definitions(VstHostNative PRIVATE
        _UNICODE UNICODE NOMINMAX WIN32_LEAN_AND_MEAN
    )
endif()

# Output name without lib prefix
set_target_properties(VstHostNative PROPERTIES
    PREFIX ""
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)

# Install to Plugins directory
install(TARGETS VstHostNative
    RUNTIME DESTINATION "${CMAKE_CURRENT_SOURCE_DIR}/../../Plugins/Windows/x86_64"
    LIBRARY DESTINATION "${CMAKE_CURRENT_SOURCE_DIR}/../../Plugins/Windows/x86_64"
)

# --- Smoke test (dev only) ---
add_executable(VstHostSmokeTest tools/SmokeTest.cpp)
target_include_directories(VstHostSmokeTest PRIVATE src)
target_link_libraries(VstHostSmokeTest PRIVATE VstHostNative)
set_target_properties(VstHostSmokeTest PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)
if(WIN32)
    target_compile_definitions(VstHostSmokeTest PRIVATE
        _UNICODE UNICODE NOMINMAX WIN32_LEAN_AND_MEAN
    )
endif()
