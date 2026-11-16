"""Exercise the Windows player with real key and pointer events in an isolated profile."""
import ctypes
from ctypes import wintypes
from datetime import datetime
from pathlib import Path
import shutil
import subprocess
import time

BASE = Path(__file__).resolve().parents[2]
USER = ctypes.windll.user32
USER.SetProcessDPIAware()
ENUM = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)


def window_for(pid):
    handles = []

    def collect(window, _):
        process = wintypes.DWORD()
        USER.GetWindowThreadProcessId(window, ctypes.byref(process))
        if process.value == pid and USER.IsWindowVisible(window):
            handles.append(window)
        return True

    USER.EnumWindows(ENUM(collect), 0)
    return handles[0] if len(handles) == 1 else None


def key(code, seconds=0.09):
    USER.keybd_event(code, 0, 0, 0)
    try:
        time.sleep(seconds)
    finally:
        USER.keybd_event(code, 0, 2, 0)


def mouse(window, x, y):
    point = wintypes.POINT(int(x), int(y))
    if not USER.ClientToScreen(window, ctypes.byref(point)):
        raise RuntimeError("Unity's client window is unavailable.")
    USER.SetCursorPos(point.x, point.y)
    time.sleep(0.1)
    USER.mouse_event(0x0002, 0, 0, 0, 0)
    time.sleep(0.08)
    USER.mouse_event(0x0004, 0, 0, 0, 0)


def latest_step(path, previous, player):
    timeout = 28 if previous.startswith("incubate ") else 12
    deadline = time.monotonic() + timeout
    while player.poll() is None and time.monotonic() < deadline:
        if path.exists():
            value = path.read_text(encoding="utf-8").strip()
            if value and value != previous:
                return value
        time.sleep(0.05)
    raise RuntimeError(f"Unity did not progress beyond step {previous!r}.")


def main():
    output = BASE / "Logs/HubInput" / datetime.now().strftime("%Y%m%d-%H%M%S-%f")
    output.mkdir(parents=True)
    player = subprocess.Popen([
        str(BASE / "Builds/HubWorld-Win64/Lumon.exe"), "-screen-fullscreen", "0",
        "-screen-width", "1280", "-screen-height", "720", "-force-d3d11",
        "-lumon-hub-review-write", str(output), "-lumon-hub-review-input",
        "-logFile", str(output / "player.log"),
    ], cwd=BASE)
    try:
        step = ""
        for scenario in (
            "walk", "settings", "language", "close", "click", "station", "eggs", "buy",
            "incubate", "hatch", "continue", "fusion", "fusion-back", "close-team",
        ):
            step = latest_step(output / "player-input-step.txt", step, player)
            if step.split()[0] != scenario:
                raise RuntimeError(f"Expected {scenario!r}, received {step!r}.")
            window = window_for(player.pid)
            if not window:
                raise RuntimeError("Expected exactly one visible window from the test player.")
            if USER.GetForegroundWindow() != window:
                USER.SetForegroundWindow(window)
                time.sleep(0.2)
            if USER.GetForegroundWindow() != window:
                raise RuntimeError("The player is not foreground. Input was withheld.")
            if scenario == "walk":
                key(0x41, 0.5)
            elif scenario in ("settings", "close", "close-team"):
                key(0x1B)
            elif scenario in ("language", "fusion"):
                key(0x09)
                time.sleep(0.12)
                key(0x0D)
            elif scenario == "station":
                key(0x45)
            elif scenario == "continue":
                key(0x0D)
            else:
                _, x, y = step.split()
                mouse(window, x, y)

        result = player.wait(timeout=12)
        if result != 0:
            raise RuntimeError((output / "player-input.txt").read_text(encoding="utf-8"))
        for filename in (
            "player-input.txt", "player-11-atlas-mouse.png", "workshop-station-e.png",
            "workshop-hatch-input.png", "workshop-team-focus-input.png",
        ):
            shutil.copyfile(output / filename, BASE / "Art/HubWorld/review" / filename)
        print((output / "player-input.txt").read_text(encoding="utf-8"))
    finally:
        if player.poll() is None:
            player.terminate()
            player.wait(timeout=5)


if __name__ == "__main__":
    main()
