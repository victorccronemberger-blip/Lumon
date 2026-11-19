"""Capture the Observatory art tour from a built Windows checkout."""
import argparse
from datetime import datetime
from pathlib import Path
import re
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--projeto", type=Path, default=Path.cwd())
    parser.add_argument("--saida", type=Path, help="Nova pasta para as capturas")
    args = parser.parse_args()
    root = args.projeto.resolve()
    exe = root / "Builds/HubWorld-Win64/Lumon.exe"
    tour = root / "QA/Arquitetura-do-Observatorio/teste.txt"
    for path in (exe, tour):
        if not path.is_file():
            parser.exit(1, f"Arquivo necessário ausente: {path}\n")
    out = (args.saida or root / "Builds/Revisoes" / datetime.now().strftime("%Y%m%d-%H%M%S-%f")).resolve()
    out.mkdir(parents=True, exist_ok=False)
    subprocess.run([str(exe), "-screen-width", "1920", "-screen-height", "1080",
                    "-screen-fullscreen", "0", "-qaStepsFile", str(tour),
                    "-qaOut", str(out), "-logFile", str(out / "player.log")],
                   cwd=exe.parent, check=True)
    log = (out / "qa.log").read_text(encoding="utf-8")
    panels = set(re.findall(r"^QA TEL .*?panel=(\w+)", log, re.M))
    missing = {"Team", "Nest", "EggShop", "Fusion", "Improve", "Expeditions", "EastScope", "WestScope"} - panels
    shots = [p for p in out.glob("shot_*.png") if p.stat().st_size]
    expected = sum(line.startswith("shot ") for line in tour.read_text().splitlines())
    if (missing or len(shots) != expected or "QA DONE" not in log
            or not re.search(r"^QA PERF .*\berrors=0\s*$", log, re.M)
            or "QA ARRIVAL reached=False" in log):
        parser.exit(1, f"Revisão incompleta em {out}. Painéis ausentes: {sorted(missing)}. Capturas: {len(shots)}/{expected}.\n")
    print(f"{len(shots)} capturas e oito interações verificadas em {out}")


if __name__ == "__main__":
    main()
