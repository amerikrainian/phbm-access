"""Play a campaign forward through the mod's own screens, to reach a stage for inspection.

    uv run python tools/python/run_driver.py [--until town|battle|treasure|loot|inventory|talk|rooms]
                                             [--steps N] [--treasure try|leave]

Reads the mod's focused screen from GET /nav each step and answers it the way a player would, through
POST /input (the mod's real key dispatch), never by poking the game: in a fight, Enter on the action
the turn lands on and Enter on its first target; on a reward, Escape (the game's skip); in the
inventory, the room overview's Enter Room; in a talk, Enter (a page) or the first option; a popup the
mod does not cover yet, its first button. It stops at the stage named by --until, at the town, at a
screen it does not know, or after --steps steps, and prints the last lines spoken. A run that reaches
an unknown screen is how a new game screen surfaces.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
import urllib.request
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gamelib  # noqa: E402


def get(path: str) -> str:
    with urllib.request.urlopen(gamelib.DEV_URL + path, timeout=40) as r:
        return r.read().decode("utf-8", errors="replace")


def post(path: str, body: str) -> str:
    req = urllib.request.Request(gamelib.DEV_URL + path, data=body.encode("utf-8"), method="POST")
    with urllib.request.urlopen(req, timeout=40) as r:
        return r.read().decode("utf-8", errors="replace")


def nav() -> dict:
    """The navigator's state: screen key, focused node id, readout, node ids."""
    text = get("/nav")
    state = {"screen": None, "focus": None, "readout": "", "nodes": []}
    for line in text.splitlines():
        if line.startswith("screen: "):
            state["screen"] = line[len("screen: "):].split(" ")[0]
        elif line.startswith("focus: "):
            state["focus"] = line[len("focus: "):]
        elif line.startswith("readout: "):
            state["readout"] = line[len("readout: "):]
        elif line.startswith("    ") or line.startswith("  > "):
            state["nodes"].append(line[4:].split(": ")[0])
    return state


def press(*verbs: str, pause: float = 0.35) -> None:
    for verb in verbs:
        post("/input", verb)
        time.sleep(pause)


def cursor() -> int:
    first = get("/speech").splitlines()[0]
    return int(first.split(":")[1]) if first.startswith("cursor:") else 0


STAGES = {
    "battle": "game.battle", "loot": "game.loot", "inventory": "game.inventory", "town": "game.town",
    "treasure": "game.treasure", "talk": "game.talk", "rooms": "inventory.rooms",
}


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--until", choices=sorted(STAGES), default=None)
    ap.add_argument("--steps", type=int, default=200)
    ap.add_argument("--treasure", choices=("try", "leave"), default="leave")
    args = ap.parse_args()
    start = cursor()
    target = STAGES.get(args.until)
    for step in range(args.steps):
        s = nav()
        screen = s["screen"]
        if target and screen == target:
            print(f"reached {args.until} at step {step}")
            break
        if screen == "game.battle":
            if any(n.startswith("battle:action") for n in s["nodes"]):
                press("confirm")
                press("confirm", pause=6)
            else:
                time.sleep(1.5)
        elif screen == "game.loot":
            press("back", pause=2)
        elif screen == "game.inventory":
            post("/eval", "var inv = ScreenBase.pCurrentInstance as Inventory; inv.v_ButtonRoomOverview.TriggerClick(); 0")
            time.sleep(1.5)
        elif screen == "inventory.rooms":
            press("home", "confirm", pause=3)
        elif screen == "game.treasure":
            if args.treasure == "leave":
                post("/eval", "(ScreenBase.pCurrentInstance as RoomTreasure).EventButtonLeave(); 0")
            else:
                press("confirm")
            time.sleep(4)
        elif screen == "game.talk":
            press("confirm", pause=1)
        elif screen == "game.overland":
            time.sleep(2)
        elif screen == "game.town":
            print(f"town at step {step}")
            break
        elif screen == "game.generic" and s["focus"] and "(none)" not in s["focus"]:
            press("confirm", pause=1.5)
        elif screen in (None, "(none)", "game.generic"):
            time.sleep(1.5)
        else:
            print(f"unknown screen {screen} at step {step}: {s['readout']}")
            break
    print(get(f"/speech?since={max(0, start)}").splitlines()[-1:][0] if True else "")
    lines = get(f"/speech?since={start}").splitlines()[1:]
    for line in lines[-25:]:
        print(line)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
