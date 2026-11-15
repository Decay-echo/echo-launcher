cmake_minimum_required(VERSION 3.20)
project(EchoLauncherLoader LANGUAGES CXX)

# The client mod loader: an amd_ags_x64.dll stand-in that forwards all 32 of AMD's exports to the game's own
# file (renamed amd_ags_x64_orig.dll) and loads the mods named in <game>\mods\mode.txt. MSVC x64, static CRT,
# so the DLL imports KERNEL32.dll only. Verify after building:
#   dumpbin /exports amd_ags_x64.dll   -> 32 entries "forwarded to amd_ags_x64_orig.*"
#   dumpbin /imports amd_ags_x64.dll   -> KERNEL32.dll only
#
#   cmake -S loader -B build-loader -G "Visual Studio 17 2022" -A x64
#   cmake --build build-loader --config Release
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
if(NOT MSVC)
  message(FATAL_ERROR "build with MSVC (or clang-cl): the forwarders use link.exe's /export syntax")
endif()
if(NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
  message(FATAL_ERROR "echovr.exe is x64; configure with -A x64")
endif()
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

add_library(amd_ags_x64_client SHARED ags_client.cpp ags_forwarders.h)
target_compile_definitions(amd_ags_x64_client PRIVATE
  _WIN32_WINNT=0x0A00 WINVER=0x0A00 _CRT_SECURE_NO_WARNINGS WIN32_LEAN_AND_MEAN)
target_compile_options(amd_ags_x64_client PRIVATE /W4 /utf-8)
set_target_properties(amd_ags_x64_client PROPERTIES OUTPUT_NAME "amd_ags_x64" PREFIX "" SUFFIX ".dll")
