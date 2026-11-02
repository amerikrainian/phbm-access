"""Regenerate the decompiled game reference under game/ (gitignored, never committed).

    uv run python tools/python/decompile.py [--game DIR]

Runs ilspycmd (dotnet tool install -g ilspycmd) over Assembly-CSharp.dll and
Assembly-CSharp-firstpass.dll into game/decompiled and game/firstpass, one file per type, and
records the game's build line (v2015.txt) in game/version.txt. Rerun after a game update, then
rebuild: the module references Assembly-CSharp publicized, so a renamed member is a compile error.
"""
from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path

import gamelib


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--game", type=Path, default=None, help="the game folder (default: found like the build finds it)")
    args = ap.parse_args()
    game = args.game or gamelib.game_dir()
    if game is None or not (game / gamelib.GAME_EXE).exists():
        sys.exit("game not found; pass --game")
    managed = game / gamelib.DATA_DIR / "Managed"
    ilspy = shutil.which("ilspycmd")
    if ilspy is None:
        sys.exit("ilspycmd not on PATH: dotnet tool install -g ilspycmd")
    out_root = gamelib.REPO / "game"
    for asm, folder in (("Assembly-CSharp.dll", "decompiled"), ("Assembly-CSharp-firstpass.dll", "firstpass")):
        out = out_root / folder
        if out.exists():
            shutil.rmtree(out)
        out.mkdir(parents=True)
        print("decompiling", asm, "->", out)
        subprocess.run([ilspy, "-p", "-o", str(out), str(managed / asm), "-r", str(managed)], check=True,
                       stdout=subprocess.DEVNULL)
    version = game / "v2015.txt"
    if version.exists():
        (out_root / "version.txt").write_text(version.read_text(encoding="utf-8", errors="replace"), encoding="utf-8")
    print("done:", sum(1 for _ in (out_root / "decompiled").rglob("*.cs")), "types")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
