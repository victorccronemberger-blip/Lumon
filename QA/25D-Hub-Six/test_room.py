"""Exercise a LAN room with six independent Windows players and one overflow probe."""
import argparse
from pathlib import Path
import socket
import subprocess
import time

ROOT = Path(__file__).resolve().parents[2]
EXE = ROOT / "Builds/HubWorld-Win64/Lumon.exe"
BASE = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument("output", type=Path)
args = parser.parse_args()
out = args.output.resolve()
out.mkdir(parents=True, exist_ok=True)

host = """
button partyButton
button CRIAR SALA
key esc
room-count 6 210
wait 1
room-state
world
shot praca-dos-seis
key v
shot observatorio-completo
key v
button partyButton
shot sala-cheia
key esc
walk-to -2.9 -1.1 25
walk-to -4.23 -4.53 25
key e
button REGISTRAR NO ATLAS
shot carta-da-lira
key esc
room-charts 3 80
room-state
room-count 1 60
wait 8
room-state
"""
guest = """
button partyButton
room-address 127.0.0.1
button ENTRAR NA SALA
room-count 6 210
button MARCAR PRONTO
key esc
"""
eastern_route = """
wait 14
walk-to -0.2 2.4 25
walk-to 2.9 1.6 25
walk-to 3.4 -1.0 25
walk-to 5.95 -2.62 25
key e
button REGISTRAR NO ATLAS
key esc
"""
processes = []
def start(role, steps, visible):
    folder = out / role
    folder.mkdir(exist_ok=True)
    script = folder / "tour.txt"
    script.write_text(steps.strip(), encoding="utf-8")
    flags = ["-screen-width", "1440" if visible else "480", "-screen-height", "900" if visible else "300",
             "-screen-fullscreen", "0", "-qaTimeout", "360", "-qaStepsFile", str(script),
             "-qaOut", str(folder), "-logFile", str(folder / "player.log")]
    proc = subprocess.Popen([str(EXE), *flags], cwd=ROOT, startupinfo=None if visible else hidden_window())
    processes.append(proc)
    return folder

def hidden_window():
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = 0
    return startup

def read(folder):
    try:
        return (folder / "qa.log").read_text(encoding="utf-8")
    except FileNotFoundError:
        return ""

def wait_log(folder, fragment, timeout):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if fragment in read(folder):
            return
        if processes[0].poll() is not None:
            break
        time.sleep(.25)
    raise RuntimeError(f"{folder.name} did not reach {fragment}")

def handshake():
    with socket.create_connection(("127.0.0.1",42636),timeout=12) as sock:
        sock.sendall(b"LM\x01\x01" + bytes(76))
        data = bytearray()
        while len(data) < 80:
            part = sock.recv(80-len(data))
            if not part:
                break
            data.extend(part)
        return data

try:
    hostdir = start("host", host, True)
    wait_log(hostdir, ":: room-count 6", 90)
    clients = []
    for i in range(5):
        route = eastern_route if i == 0 else ""
        folder = start(f"guest-{i+1}", guest + route + "\nroom-charts 3 150\nroom-state\nwait 2", False)
        clients.append(folder)
        wait_log(folder, ":: room-count 6", 60)
    wait_log(hostdir, "value=6 expected=6", 120)
    full = handshake()
    assert full[:4] == b"LM\x01\x04", full[:8]
    print("Seventh connection refused at capacity.", flush=True)
    wait_log(hostdir, "value=1 expected=1", 200)
    reused = handshake()
    assert reused[:4] == b"LM\x01\x03" and 1 <= reused[4] <= 5, reused[:8]
    print("Released slot available again.", flush=True)
    for proc in processes:
        if proc.wait(timeout=90):
            raise RuntimeError("Player reported a QA failure")
    for folder in [hostdir, *clients]:
        log = read(folder)
        assert "QA DONE" in log and "errors=0" in log and "value=3 expected=3" in log, folder
    print("Six Windows players received both shared observations.", flush=True)
finally:
    for proc in processes:
        if proc.poll() is None:
            proc.terminate()
