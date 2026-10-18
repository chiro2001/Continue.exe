#!/usr/bin/env python3
"""Inventory Unity assets on the GamePC workstation through SSH.

Scans the configured roots for .unitypackage/.unity/.fbx/.blend files and, for
each .unitypackage (a plain gzip tar), prints the asset paths stored inside its
`pathname` entries. Nothing is imported into Unity and no executables are run.

Usage:
    python3 tools/scan_gamepc_assets.py list                 # 列出候选包
    python3 tools/scan_gamepc_assets.py peek <path> [<path>] # 查看包内资产路径
"""

import argparse
import base64
import subprocess
import sys

DEFAULT_HOST = "gpu5080"          # ssh alias -> 192.168.101.5 (GamePC)
DEFAULT_ROOTS = [
    r"D:\BaiduNetdiskDownload",
    r"E:\BaiduNetdiskDownload",
]
EXTENSIONS = [".unitypackage", ".unity", ".fbx", ".blend", ".obj"]

PREFIX = (
    "$ErrorActionPreference = 'Continue'; $ProgressPreference = 'SilentlyContinue'; "
    "$PSStyle.OutputRendering = 'PlainText'; "
    "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); "
    "$OutputEncoding = [Console]::OutputEncoding; "
)


def run_pwsh(script: str, host: str) -> int:
    """Remote command lines go through cmd.exe, so keep scripts small."""
    encoded = base64.b64encode((PREFIX + script).encode("utf-16le")).decode("ascii")
    if len(encoded) > 7000:
        print("script too long for the remote cmd.exe command line", file=sys.stderr)
        return 2
    result = subprocess.run(
        ["ssh", "-T", "-o", "BatchMode=yes", "-o", "ConnectTimeout=15",
         "-o", "LogLevel=ERROR", host,
         "pwsh -NoLogo -NoProfile -NonInteractive -EncodedCommand " + encoded],
    )
    return result.returncode


def list_packages(host: str, roots: list[str]) -> int:
    ext_list = ",".join(f"'{e}'" for e in EXTENSIONS)
    roots_list = ",".join(f"'{r}'" for r in roots)
    script = (
        f"$exts = @({ext_list}); $roots = @({roots_list}); "
        "foreach ($root in $roots) { "
        "  if (-not (Test-Path -LiteralPath $root)) { continue } "
        "  Get-ChildItem -LiteralPath $root -Recurse -File -Force -ErrorAction SilentlyContinue | "
        "    Where-Object { $exts -contains $_.Extension.ToLower() } | Sort-Object FullName | "
        "    ForEach-Object { Write-Output ($_.FullName + [char]9 + ('{0:N2}MB' -f ($_.Length / 1MB))) } }"
    )
    return run_pwsh(script, host)


def peek_packages(host: str, packages: list[str]) -> int:
    pkg_list = ",".join("'" + p.replace("'", "''") + "'" for p in packages)
    work = r"C:\Users\Chiro\tmp\vrc-old-web-map\pkg-peek"
    script = (
        f"$pkgs = @({pkg_list}); $work = '{work}'; "
        "New-Item -ItemType Directory -Force -Path $work | Out-Null; "
        "foreach ($pkg in $pkgs) { "
        "  if (-not (Test-Path -LiteralPath $pkg)) { Write-Output \"missing: $pkg\"; continue } "
        "  $dest = Join-Path $work ([guid]::NewGuid().ToString('N')); "
        "  New-Item -ItemType Directory -Force -Path $dest | Out-Null; "
        "  & tar -xzf $pkg -C $dest --include '*/pathname' 2>$null; "
        "  $paths = Get-ChildItem -LiteralPath $dest -Recurse -Force -File | "
        "    Where-Object { $_.Name -eq 'pathname' } | "
        "    ForEach-Object { (Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8).Trim() } | "
        "    Where-Object { $_ } | Sort-Object; "
        "  Write-Output \"=== $pkg ($(@($paths).Count) assets) ===\"; "
        "  $paths | ForEach-Object { Write-Output \"  $_\" }; "
        "  Remove-Item -LiteralPath $dest -Recurse -Force -ErrorAction SilentlyContinue }"
    )
    return run_pwsh(script, host)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host", default=DEFAULT_HOST)
    sub = parser.add_subparsers(dest="command", required=True)
    p_list = sub.add_parser("list", help="列出候选资产文件")
    p_list.add_argument("--root", action="append", dest="roots",
                        help="覆盖默认扫描根目录，可重复")
    p_peek = sub.add_parser("peek", help="打印 .unitypackage 内的资产路径")
    p_peek.add_argument("packages", nargs="+")
    args = parser.parse_args()

    if args.command == "list":
        return list_packages(args.host, args.roots or DEFAULT_ROOTS)
    return peek_packages(args.host, args.packages)


if __name__ == "__main__":
    raise SystemExit(main())
