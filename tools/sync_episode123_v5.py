#!/usr/bin/env python3
"""Copy V5-owned text sources/data/exports as one batch; Pipeline imports, configures and builds."""
import importlib.util, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("unity_remote", ROOT / "tools/unity_remote.py")
remote = importlib.util.module_from_spec(spec); spec.loader.exec_module(remote)
BASE = "D:/Projects/VRChatProjects2/vrc-old-web-map"
EXPORTS = {"atmosphere": "Atmosphere/Textures", "signal": "Signal/Textures", "identity": "Identity/Textures", "foley": "Foley/Audio"}
TARGET = "Assets/OldWebMap/Episode123/Revision5/"


def copy(files, target):
    files = [str(p) for p in files if p.is_file()]
    for at in range(0, len(files), 60):
        if subprocess.run(["scp", "-q", *files[at:at + 60], "gpu5080:" + BASE + "/" + target + "/"]).returncode:
            raise SystemExit(f"scp failed for {target}")
    return len(files)


def main():
    folders = ["AgentScripts/data", "Assets/OldWebMap/Scripts", "Assets/OldWebMap/Shaders"] + [TARGET + v for v in EXPORTS.values()]
    cmd = "; ".join("New-Item -ItemType Directory -Force -Path " + remote.pwsh_literal(BASE + "/" + p) + " | Out-Null" for p in folders)
    if remote.run_pwsh(cmd).returncode:
        raise SystemExit("remote folders")
    n = {}
    if "--exports-only" not in sys.argv:
        n["builders"] = copy(sorted((ROOT / "unity/AgentScripts").glob("OWEpisodeV5*.cs")), "AgentScripts")
        n["data"] = copy(sorted((ROOT / "data").glob("episode123-v5-*.json")), "AgentScripts/data")
        n["shaders"] = copy(sorted((ROOT / "unity/Assets/OldWebMap/Shaders").glob("OWV5*.shader")), "Assets/OldWebMap/Shaders")
        n["runtime"] = copy(sorted((ROOT / "unity/Assets/OldWebMap/Scripts").glob("OWV5*.cs")), "Assets/OldWebMap/Scripts")
    if "--scripts-only" not in sys.argv:
        for module, folder in EXPORTS.items():
            src = ROOT / "art/export/episode123-v5" / module
            n[module] = copy(sorted(p for p in src.glob("*") if p.suffix.lower() in (".png", ".wav", ".exr")), TARGET + folder) if src.exists() else 0
    print("V5 batch copied:", n, "— run PrepareImport before and ResumeImport after; Pipeline builds remain required.", flush=True)


if __name__ == "__main__":
    main()
