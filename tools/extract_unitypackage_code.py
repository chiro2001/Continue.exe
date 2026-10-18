#!/usr/bin/env python3
"""从 .unitypackage 里抽出纯文本资产（shader / cginc / hlsl / cs）。

.unitypackage 本质是 gzip 压缩的 tar，每个资产一个目录，目录里有：
    pathname  资产在项目里的路径
    asset     资产内容（文本资产即纯文本）

用法:
    python3 tools/extract_unitypackage_code.py <package.unitypackage> <输出目录>
    python3 tools/extract_unitypackage_code.py <package.unitypackage> <输出目录> --list

设计意图：把用户自购特效资产里的着色器取出来**只读参考**。
原资产代码不随本仓库分发；仓库里只保留我们自己的实现与说明，
因为多数素材的授权允许个人使用、不允许再分发源码。
"""

import argparse
import re
import tarfile
import sys
from pathlib import Path

TEXT_ASSET_PATTERNS = (".shader", ".cginc", ".hlsl", ".cs", ".compute", ".asmdef")


def safe_name(asset_path: str) -> str:
    return re.sub(r'[:*?"<>|]', "_", asset_path.lstrip("/"))


def iter_entries(package: Path):
    with tarfile.open(package, "r:gz") as tar:
        members = {m.name: m for m in tar.getmembers()}
        for name, member in members.items():
            if not name.endswith("/pathname"):
                continue
            prefix = name[: -len("pathname")]
            asset_member = members.get(prefix + "asset")
            if asset_member is None:
                continue
            path = tar.extractfile(member).read().decode("utf-8", "replace").strip()
            if not path:
                continue
            blob = tar.extractfile(asset_member).read()
            yield path, blob


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("package", type=Path)
    ap.add_argument("outdir", type=Path)
    ap.add_argument("--list", action="store_true", help="只列出匹配的资产路径，不写文件")
    ap.add_argument("--all-text", action="store_true",
                    help="连 .prefab/.mat/.controller 等 YAML 文本一起列出（只在 --list 时生效）")
    args = ap.parse_args()

    if not args.package.is_file():
        print(f"package not found: {args.package}", file=sys.stderr)
        return 2

    matched = 0
    for path, blob in iter_entries(args.package):
        is_text_asset = path.lower().endswith(TEXT_ASSET_PATTERNS)
        if args.list and args.all_text:
            is_match = path.lower().endswith(TEXT_ASSET_PATTERNS + (".prefab", ".mat", ".controller", ".anim"))
        else:
            is_match = is_text_asset
        if not is_match:
            continue
        matched += 1
        if args.list:
            print(path)
            continue
        target = args.outdir / safe_name(path)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(blob)
        print(f"{len(blob):8d} B  -> {target}")

    if not matched:
        print("no matching text assets found", file=sys.stderr)
        return 1
    if args.list:
        print(f"\n{matched} matching assets", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
