"""Run the managed checks in the installed game's Mono, without starting Unity or a world."""
import ctypes
import os
from pathlib import Path
import sys

if len(sys.argv) not in (4, 5):
    raise SystemExit("Usage: python run_mono.py <game directory> <plugin.dll> <BepInEx core directory> [DropThat.dll]")
game, plugin, core = map(lambda p: Path(p).resolve(), sys.argv[1:4])
managed = game / "valheim_Data" / "Managed"
if not managed.is_dir():
    managed = game / "valheim_server_Data" / "Managed"
runtime = game / "MonoBleedingEdge"
runner = Path(__file__).resolve().parent / "bin" / "Debug" / "net48" / "GameCompatibilityCheck.exe"
# Keep the directory handle alive for Mono's native dependencies.
dll_directory = os.add_dll_directory(str(runtime / "EmbedRuntime"))
mono = ctypes.CDLL(str(runtime / "EmbedRuntime" / "mono-2.0-bdwgc.dll"))
mono.mono_set_dirs.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
mono.mono_set_assemblies_path.argtypes = [ctypes.c_char_p]
mono.mono_jit_init_version.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
mono.mono_jit_init_version.restype = ctypes.c_void_p
mono.mono_domain_assembly_open.argtypes = [ctypes.c_void_p, ctypes.c_char_p]
mono.mono_domain_assembly_open.restype = ctypes.c_void_p
mono.mono_jit_exec.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_int, ctypes.POINTER(ctypes.c_char_p)]
mono.mono_jit_exec.restype = ctypes.c_int
mono.mono_set_dirs(str(managed).encode(), str(runtime / "etc").encode())
mono.mono_set_assemblies_path(os.pathsep.join(map(str, (managed, core, plugin.parent, runner.parent))).encode())
domain = mono.mono_jit_init_version(b"CreatureManagerCompatibilityCheck", b"v4.0.30319")
if not domain:
    raise SystemExit("Could not initialize game Mono")
assembly = mono.mono_domain_assembly_open(domain, str(runner).encode())
if not assembly:
    raise SystemExit("Could not load compatibility check assembly")
args = [str(runner).encode(), str(plugin).encode(), str(managed).encode(), str(core).encode()]
if len(sys.argv) == 5:
    args.append(str(Path(sys.argv[4]).resolve()).encode())
argv = (ctypes.c_char_p * len(args))(*args)
raise SystemExit(mono.mono_jit_exec(domain, assembly, len(args), argv))
