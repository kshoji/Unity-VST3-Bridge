cmake_minimum_required(VERSION 3.25)
project(VstHostNative VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Shared Windows tree: sources + VST3 SDK submodule
set(WIN_HOST_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../windows-vst-host")
set(VST3SDK_DIR "${WIN_HOST_DIR}/vst3sdk")
set(WIN_HOST_SRC "${WIN_HOST_DIR}/src")

if(NOT EXISTS "${VST3SDK_DIR}/public.sdk/source/vst/hosting/module_linux.cpp")
    message(FATAL_ERROR
        "VST3 SDK missing module_linux.cpp. Run:\n"
        "  git submodule update --init --recursive\n"
        "from the repository root (SDK lives under native~/windows-vst-host/vst3sdk).")
endif()

set(SDK_ROOT "${VST3SDK_DIR}")
set(public_sdk_SOURCE_DIR "${SDK_ROOT}/public.sdk")
set(pluginterfaces_SOURCE_DIR "${SDK_ROOT}/pluginterfaces")

include_directories("${SDK_ROOT}")

find_package(Threads REQUIRED)

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
    "${HOSTING_DIR}/module_linux.cpp"
    "${HOSTING_DIR}/parameterchanges.cpp"
    "${HOSTING_DIR}/pluginterfacesupport.cpp"
    "${HOSTING_DIR}/plugprovider.cpp"
    "${HOSTING_DIR}/processdata.cpp"
    "${public_sdk_SOURCE_DIR}/source/vst/vstinitiids.cpp"
    "${public_sdk_SOURCE_DIR}/source/vst/utility/stringconvert.cpp"
    "${public_sdk_SOURCE_DIR}/source/common/commonstringconvert.cpp"
    "${public_sdk_SOURCE_DIR}/source/common/memorystream.cpp"
    "${public_sdk_SOURCE_DIR}/source/common/threadchecker_linux.cpp"
)
target_include_directories(sdk_hosting PUBLIC "${SDK_ROOT}" "${public_sdk_SOURCE_DIR}")
target_link_libraries(sdk_hosting PUBLIC base pluginterfaces ${CMAKE_DL_LIBS} Threads::Threads)

# --- Shared library for Unity Plugins/Linux/x86_64/VstHostNative.so ---
add_library(VstHostNative SHARED
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
    ${CMAKE_DL_LIBS}
    Threads::Threads
)
target_compile_options(VstHostNative PRIVATE
    -fvisibility=hidden
    -fvisibility-inlines-hidden
)

set_target_properties(VstHostNative PROPERTIES
    PREFIX ""
    OUTPUT_NAME "VstHostNative"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)

# --- Smoke test (dev only) ---
add_executable(VstHostSmokeTest
    "${WIN_HOST_DIR}/tools/SmokeTest.cpp"
)
target_include_directories(VstHostSmokeTest PRIVATE "${WIN_HOST_SRC}")
target_link_libraries(VstHostSmokeTest PRIVATE VstHostNative)
set_target_properties(VstHostSmokeTest PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    BUILD_RPATH "$ORIGIN"
    INSTALL_RPATH "$ORIGIN"
)

# Install copies the .so into the UPM Plugins tree
install(TARGETS VstHostNative
    LIBRARY DESTINATION "${CMAKE_CURRENT_SOURCE_DIR}/../../Plugins/Linux/x86_64"
    RUNTIME DESTINATION "${CMAKE_CURRENT_SOURCE_DIR}/../../Plugins/Linux/x86_64"
)
