// ags_forwarders.h -- generated from amd_ags_x64.def: every export of the real amd_ags_x64.dll (32 names,
// ordinals 1-32) forwarded to the renamed original, amd_ags_x64_orig.dll. The .def file stays as the readable
// list; it is not passed to the linker because link.exe took its "name = module.name @ordinal" lines as
// exports of internal symbols (LNK2001 on each) instead of forwarders. echovr.exe build 631547 imports
// agsInit, agsDeInit, agsDriverExtensionsDX12_Init and agsDriverExtensionsDX12_DeInit.
#pragma once

#pragma comment(linker, "/export:agsDeInit=amd_ags_x64_orig.agsDeInit,@1")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_BeginUAVOverlap=amd_ags_x64_orig.agsDriverExtensionsDX11_BeginUAVOverlap,@2")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_CreateBuffer=amd_ags_x64_orig.agsDriverExtensionsDX11_CreateBuffer,@3")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_CreateDevice=amd_ags_x64_orig.agsDriverExtensionsDX11_CreateDevice,@4")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_CreateTexture1D=amd_ags_x64_orig.agsDriverExtensionsDX11_CreateTexture1D,@5")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_CreateTexture2D=amd_ags_x64_orig.agsDriverExtensionsDX11_CreateTexture2D,@6")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_CreateTexture3D=amd_ags_x64_orig.agsDriverExtensionsDX11_CreateTexture3D,@7")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_DestroyDevice=amd_ags_x64_orig.agsDriverExtensionsDX11_DestroyDevice,@8")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_EndUAVOverlap=amd_ags_x64_orig.agsDriverExtensionsDX11_EndUAVOverlap,@9")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_GetMaxClipRects=amd_ags_x64_orig.agsDriverExtensionsDX11_GetMaxClipRects,@10")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_IASetPrimitiveTopology=amd_ags_x64_orig.agsDriverExtensionsDX11_IASetPrimitiveTopology,@11")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_MultiDrawIndexedInstancedIndirect=amd_ags_x64_orig.agsDriverExtensionsDX11_MultiDrawIndexedInstancedIndirect,@12")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_MultiDrawIndexedInstancedIndirectCountIndirect=amd_ags_x64_orig.agsDriverExtensionsDX11_MultiDrawIndexedInstancedIndirectCountIndirect,@13")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_MultiDrawInstancedIndirect=amd_ags_x64_orig.agsDriverExtensionsDX11_MultiDrawInstancedIndirect,@14")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_MultiDrawInstancedIndirectCountIndirect=amd_ags_x64_orig.agsDriverExtensionsDX11_MultiDrawInstancedIndirectCountIndirect,@15")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_NotifyResourceBeginAllAccess=amd_ags_x64_orig.agsDriverExtensionsDX11_NotifyResourceBeginAllAccess,@16")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_NotifyResourceEndAllAccess=amd_ags_x64_orig.agsDriverExtensionsDX11_NotifyResourceEndAllAccess,@17")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_NotifyResourceEndWrites=amd_ags_x64_orig.agsDriverExtensionsDX11_NotifyResourceEndWrites,@18")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_NumPendingAsyncCompileJobs=amd_ags_x64_orig.agsDriverExtensionsDX11_NumPendingAsyncCompileJobs,@19")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_SetClipRects=amd_ags_x64_orig.agsDriverExtensionsDX11_SetClipRects,@20")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_SetDepthBounds=amd_ags_x64_orig.agsDriverExtensionsDX11_SetDepthBounds,@21")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_SetDiskShaderCacheEnabled=amd_ags_x64_orig.agsDriverExtensionsDX11_SetDiskShaderCacheEnabled,@22")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_SetMaxAsyncCompileThreadCount=amd_ags_x64_orig.agsDriverExtensionsDX11_SetMaxAsyncCompileThreadCount,@23")
#pragma comment(linker, "/export:agsDriverExtensionsDX11_SetViewBroadcastMasks=amd_ags_x64_orig.agsDriverExtensionsDX11_SetViewBroadcastMasks,@24")
#pragma comment(linker, "/export:agsDriverExtensionsDX12_DeInit=amd_ags_x64_orig.agsDriverExtensionsDX12_DeInit,@25")
#pragma comment(linker, "/export:agsDriverExtensionsDX12_Init=amd_ags_x64_orig.agsDriverExtensionsDX12_Init,@26")
#pragma comment(linker, "/export:agsDriverExtensionsDX12_PopMarker=amd_ags_x64_orig.agsDriverExtensionsDX12_PopMarker,@27")
#pragma comment(linker, "/export:agsDriverExtensionsDX12_PushMarker=amd_ags_x64_orig.agsDriverExtensionsDX12_PushMarker,@28")
#pragma comment(linker, "/export:agsDriverExtensionsDX12_SetMarker=amd_ags_x64_orig.agsDriverExtensionsDX12_SetMarker,@29")
#pragma comment(linker, "/export:agsGetCrossfireGPUCount=amd_ags_x64_orig.agsGetCrossfireGPUCount,@30")
#pragma comment(linker, "/export:agsInit=amd_ags_x64_orig.agsInit,@31")
#pragma comment(linker, "/export:agsSetDisplayMode=amd_ags_x64_orig.agsSetDisplayMode,@32")
