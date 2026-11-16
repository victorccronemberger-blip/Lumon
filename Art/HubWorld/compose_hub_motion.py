"""Assemble the detail animation from consecutive, unscaled Unity renders."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ART = Path(__file__).resolve().parent
ROOT = ART.parents[1]
OUTPUT = ART / "review"


def main():
    head = ImageFont.truetype("segoeuib.ttf", 19)
    font = ImageFont.truetype("segoeui.ttf", 14)
    scenes = [
        ("Vento na árvore", (654, 147, 927, 336), (14, 68)),
        ("Bandeira e lanterna", (1062, 231, 1160, 329), (303, 68)),
        ("Fogueira e luz", (670, 289, 751, 357), (428, 68)),
        ("Luz da equipe", (115, 327, 158, 392), (535, 68)),
        ("Luz da mesa", (822, 298, 892, 355), (428, 182)),
    ]
    frames = []
    for i in range(48):
        source = ROOT / "Logs" / "HubAmbientFrames" / f"frame-{i:02d}.png"
        with Image.open(source) as capture:
            if capture.size != (1280, 720):
                raise ValueError(f"Unexpected Unity capture size {capture.size}.")
            canvas = Image.new("RGB", (624, 280), "#272832")
            draw = ImageDraw.Draw(canvas)
            draw.text((14, 9), "Observatório da Aurora", fill="#fff3e7", font=head)
            draw.text((14, 35), "Detalhes animados em escala real · câmera do Unity", fill="#d7cbd4", font=font)
            for label, rectangle, (x, y) in scenes:
                crop = capture.crop(rectangle)
                canvas.paste(crop, (x, y))
                draw.text((x, y + crop.height + 4), label, fill="#fff3e7", font=font)
            frames.append(canvas)
    frames[0].save(OUTPUT / "unity-ambiente.gif", save_all=True, append_images=frames[1:],
                   duration=100, loop=0, optimize=True)


if __name__ == "__main__":
    main()
