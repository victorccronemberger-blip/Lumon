"""Build a 1:1 review sheet from the eight fixed Unity captures."""
from pathlib import Path
import json

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent / "review" / "idle-directions"
VIEWS = [
    ("North", "Norte"), ("NorthEast", "Nordeste"), ("East", "Leste"), ("SouthEast", "Sudeste"),
    ("South", "Sul"), ("SouthWest", "Sudoeste"), ("West", "Oeste"), ("NorthWest", "Noroeste"),
]


def main():
    sheet = Image.new("RGB", (750, 552), "#282832")
    draw = ImageDraw.Draw(sheet)
    title = ImageFont.truetype("segoeuib.ttf", 24)
    label = ImageFont.truetype("segoeui.ttf", 16)
    draw.text((18, 13), "Guardiã do Observatório", font=title, fill="#fff3e3")
    draw.text((18, 43), "Repouso em oito rumos · Unity 1280 × 720 · recortes em escala real",
              font=label, fill="#d7cbd4")
    reference = Image.open(ROOT.parent / "unity-1280x720-background.png").convert("RGB")
    background = np.asarray(reference, dtype=np.int16)
    metrics = {}
    for i, (name, caption) in enumerate(VIEWS):
        left, top = 18 + (i % 4) * 182, 76 + (i // 4) * 234
        with Image.open(ROOT / f"{name}.png") as frame:
            if frame.size != (1280, 720):
                raise ValueError(f"Invalid authoring resolution for {name}")
            delta = np.abs(np.asarray(frame.convert("RGB"), dtype=np.int16) - background)
            rows = np.nonzero(np.max(delta, axis=2) > 18)[0]
            height = int(rows.max() - rows.min() + 1)
            bootline = int(rows.max() + 1)
            if abs(height - 165) > 2 or abs(bootline - 508) > 2:
                raise ValueError(f"{name}: unexpected height {height} or ground row {bootline}")
            metrics[name] = {"heightPixels": height, "bootline": bootline}
            sheet.paste(frame.crop((502, 325, 663, 515)), (left, top))
        draw.text((left, top + 196), caption, font=label, fill="#fff3e3")
    sheet.save(ROOT / "unity-oito-rumos.png")
    (ROOT / "metrics.json").write_text(json.dumps(metrics, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
