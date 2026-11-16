"""Compose the eight Unity captures without rescaling the subjects."""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ASSET = Path(__file__).resolve().parents[1]
REVIEW = ASSET / "review" / "02-blockout"
ANGLES = range(0, 360, 45)


def main():
    sheet = Image.new("RGB", (960, 570), "#1f252d")
    draw = ImageDraw.Draw(sheet)
    heading = ImageFont.truetype("segoeuib.ttf", 22)
    caption = ImageFont.truetype("segoeui.ttf", 15)
    draw.text((22, 17), "Guardiã do Observatório · prova de volume no Unity", font=heading, fill="#faf4e8")
    draw.text((22, 47), "Câmera fixa · 1280 × 720 · recortes sem ampliação", font=caption, fill="#bbc7cb")
    for i, angle in enumerate(ANGLES):
        capture = Image.open(REVIEW / f"unity-yaw-{angle:03d}.png")
        if capture.size != (1280, 720):
            raise RuntimeError(f"Unexpected capture size for {angle}°: {capture.size}")
        x, y = 20 + i % 4 * 232, 88 + i // 4 * 233
        sheet.paste(capture.crop((532, 255, 748, 450)), (x, y))
        draw.text((x + 8, y + 201), f"{angle:03d}°", font=caption, fill="#e4ecea")
    sheet.save(REVIEW / "unity-oito-angulos.png")


if __name__ == "__main__":
    main()
