// amd_ags_x64.dll CLIENT loader: the same static-import forwarder as ags_proxy.cpp (echovr.exe imports four
// functions from amd_ags_x64.dll, so a DLL of that name in bin\win10 is mapped before WinMain), carrying no
// hooks of its own. All it does is read <game>\mods\mode.txt and LoadLibrary the DLLs named there from
// <game>\mods\. That replaces the old switcher's inject.exe + hidden PowerShell watcher + Startup entry: no
// injector (Defender flagged it as Behavior:Win32/DefenseEvasion.A!ml), no second process, and it works
// however Echo is started. The launcher (EchoLauncher.exe) installs this file, keeps mods\ current and writes
// mode.txt; "Back to official" renames the original back.
//
// mode.txt, next to the mods: one DLL file name per line; '#' starts a comment; "none" (or an empty file)
// loads nothing; "!allow-server" lets a headless/dedicated server load them too (tests only: a player mod in
// a server pollutes it, so by default a process started with -headless or -server loads nothing).
// Log: eg_loader.txt next to echovr.exe, truncated at every start.
//
// The mods themselves already cope with being loaded this early (before the game's window and before the
// Oculus runtime DLL exists in the process): each starts a thread from its DllMain and polls for
// LibOVRRT64_1.dll, which is what they did under the old 1-second watcher too.

#include <windows.h>

#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <string>

#include "ags_forwarders.h"  // the 32 forwarded exports (see amd_ags_x64.def)

namespace {

FILE* g_log = nullptr;

void Log(const char* fmt, ...) {
  if (!g_log) return;
  SYSTEMTIME st;
  GetLocalTime(&st);
  fprintf(g_log, "%02u:%02u:%02u.%03u ", unsigned(st.wHour), unsigned(st.wMinute), unsigned(st.wSecond),
          unsigned(st.wMilliseconds));
  va_list args;
  va_start(args, fmt);
  vfprintf(g_log, fmt, args);
  va_end(args);
  fputc('\n', g_log);
  fflush(g_log);
}

// Directory of echovr.exe with its trailing backslash.
std::string ExeDir() {
  char path[MAX_PATH] = {};
  if (!GetModuleFileNameA(nullptr, path, MAX_PATH)) return std::string();
  if (char* slash = strrchr(path, '\\')) *(slash + 1) = '\0';
  return path;
}

// <exe dir>\..\..\mods\ resolved, i.e. <game root>\mods\.
std::string ModsDir() {
  const std::string joined = ExeDir() + "..\\..\\mods\\";
  char full[MAX_PATH] = {};
  const DWORD n = GetFullPathNameA(joined.c_str(), MAX_PATH, full, nullptr);
  if (n == 0 || n >= MAX_PATH) return joined;
  std::string dir = full;
  if (dir.empty() || dir.back() != '\\') dir += '\\';
  return dir;
}

// A dedicated server: started with -headless or -server (word, any case).
bool IsServerProcess() {
  const wchar_t* cl = GetCommandLineW();
  if (!cl) return false;
  std::wstring s = cl;
  for (wchar_t& c : s) c = wchar_t(towlower(c));
  for (const wchar_t* flag : {L" -headless", L" -server"}) {
    size_t pos = 0;
    while ((pos = s.find(flag, pos)) != std::wstring::npos) {
      const size_t end = pos + wcslen(flag);
      if (end == s.size() || s[end] == L' ' || s[end] == L'\t' || s[end] == L'"') return true;
      pos = end;
    }
  }
  return false;
}

// A bare file name ending in .dll: no path separators, no drive, nothing odd.
bool ValidDllName(const std::string& n) {
  if (n.size() < 5 || n.size() > 80) return false;
  const std::string tail = n.substr(n.size() - 4);
  if (_stricmp(tail.c_str(), ".dll") != 0) return false;
  if (n[0] == '.') return false;
  for (const char c : n) {
    const bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';
    if (!ok) return false;
  }
  return true;
}

DWORD WINAPI LoadThread(LPVOID) {
  const std::string exe = ExeDir();
  g_log = fopen((exe + "eg_loader.txt").c_str(), "w");
  const std::string mods = ModsDir();
  const std::string mode_path = mods + "mode.txt";
  Log("amd_ags_x64 client loader; mods dir %s", mods.c_str());

  FILE* f = fopen(mode_path.c_str(), "r");
  if (!f) {
    Log("no mode.txt: nothing to load (stock game)");
    return 0;
  }
  std::string names[32];
  int count = 0;
  bool allow_server = false;
  char line[256];
  while (fgets(line, sizeof(line), f) && count < 32) {
    line[strcspn(line, "\r\n")] = '\0';
    char* s = line;
    while (*s == ' ' || *s == '\t') ++s;
    for (char* e = s + strlen(s); e > s && (e[-1] == ' ' || e[-1] == '\t'); --e) e[-1] = '\0';
    if (!s[0] || s[0] == '#') continue;
    if (_stricmp(s, "none") == 0) {
      count = 0;
      break;
    }
    if (_stricmp(s, "!allow-server") == 0) {
      allow_server = true;
      continue;
    }
    if (!ValidDllName(s)) {
      Log("ignored line (not a plain .dll name): %s", s);
      continue;
    }
    names[count++] = s;
  }
  fclose(f);

  if (count == 0) {
    Log("mode.txt names no mod: stock game");
    return 0;
  }
  if (IsServerProcess() && !allow_server) {
    Log("this is a dedicated server (-headless/-server): player mods are not loaded here");
    return 0;
  }
  for (int i = 0; i < count; ++i) {
    const std::string full = mods + names[i];
    HMODULE h = LoadLibraryA(full.c_str());
    if (h) Log("loaded %s", names[i].c_str());
    else Log("FAILED to load %s (error %lu): missing file, or a DLL it needs (VC++ runtime?) is not installed", names[i].c_str(), GetLastError());
  }
  return 0;
}

}  // namespace

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID) {
  if (reason == DLL_PROCESS_ATTACH) {
    DisableThreadLibraryCalls(module);
    // Off the loader thread: LoadLibrary inside DllMain is not allowed, and the forwarded exports need no
    // code of ours at all (the loader resolves them against amd_ags_x64_orig.dll on its own).
    if (HANDLE t = CreateThread(nullptr, 0, LoadThread, nullptr, 0, nullptr)) CloseHandle(t);
  }
  return TRUE;
}
