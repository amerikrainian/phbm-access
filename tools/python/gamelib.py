"""Shared constants for the dev tools: where the game is, how to launch it, where the dev server listens."""
from __future__ import annotations

import os
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
STEAM_APP_ID = "338320"
GAME_EXE = "Pixel Heroes - Byte and Magic.exe"
WINDOW_TITLE = "Pixel Heroes - Byte and Magic"
DEV_URL = os.environ.get("PHA_DEV_URL", "http://127.0.0.1:" + os.environ.get("PHA_DEV_PORT", "8775"))
DATA_DIR = "Pixel Heroes - Byte and Magic_Data"


def game_dir() -> Path | None:
    """The game folder: PIXEL_HEROES_PATH, else Steam's main library, else None."""
    env = os.environ.get("PIXEL_HEROES_PATH")
    if env:
        return Path(env)
    try:
        import winreg

        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam") as key:
            steam = Path(winreg.QueryValueEx(key, "SteamPath")[0])
        candidate = steam / "steamapps" / "common" / "Pixel Heroes"
        if candidate.exists():
            return candidate
    except OSError:
        pass
    default = Path(r"C:\Program Files (x86)\Steam\steamapps\common\Pixel Heroes")
    return default if default.exists() else None
