"""Compare ambient motion from real Windows-player frames and make detail and full-scene reels."""
from pathlib import Path
import json
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ART = Path(__file__).resolve().parent
REVIEW = ART / "review"
REGIONS = (
    ("copa_da_praca", "Copa da praça", (661, 148, 912, 253), (18, 75), 1),
    ("arvore_da_moldura", "Árvore da moldura", (0, 6, 164, 145), (292, 75), 1),
    ("lanterna_da_barraca", "Lanterna oeste", (113, 327, 159, 395), (19, 241), 2),
    ("lanterna_do_poste", "Lanterna leste", (1064, 237, 1113, 305), (135, 241), 2),
    ("lanterna_da_mesa", "Lanterna da mesa", (825, 299, 893, 355), (255, 241), 2),
    ("fogueira", "Fogueira", (672, 286, 748, 350), (414, 241), 2),
    ("tecido_equipe", "Tecido da equipe", (174, 307, 269, 384), (472, 75), 1),
)
MIN_CHANGED_PIXELS = 30
DELTA = 9


def difference(a, b):
    return int(np.count_nonzero(np.max(np.abs(a - b), axis=2) >= DELTA))


def main():
    input_dir = Path(sys.argv[1])
    files = sorted(input_dir.glob("motion-*.png"))
    if len(files) != 60:
        raise ValueError(f"Expected 60 player frames, received {len(files)}.")
    with Image.open(input_dir / "reduced-a.png") as a, Image.open(input_dir / "reduced-b.png") as b:
        reduced = [(a.crop(bounds), b.crop(bounds)) for _, _, bounds, _, _ in REGIONS]

    measurements = {}
    frames = []
    full_frames = []
    first = []
    title = ImageFont.truetype("segoeuib.ttf", 23)
    body = ImageFont.truetype("segoeui.ttf", 14)
    for index, path in enumerate(files):
        with Image.open(path) as frame:
            if frame.size != (1280, 720):
                raise ValueError(f"Unexpected player frame {frame.size}.")
            full_frames.append(frame.convert("RGB"))
            canvas = Image.new("RGB", (586, 405), "#261e2b")
            draw = ImageDraw.Draw(canvas)
            draw.text((18, 12), "Ambiente do Observatório", fill="#fff1d9", font=title)
            draw.text((18, 45), "Captura do executável · copas em escala real · detalhes a 2×", fill="#e3ccd1", font=body)
            for i, (key, title_text, bounds, position, zoom) in enumerate(REGIONS):
                crop = frame.crop(bounds).convert("RGB")
                current = np.asarray(crop, dtype=np.int16)
                if index == 0:
                    first.append(current)
                    reduced_a, reduced_b = (np.asarray(p.convert("RGB"), dtype=np.int16) for p in reduced[i])
                    measurements[key] = {"resolved_pixels": 0, "reduced_motion_changed_pixels": difference(reduced_a, reduced_b)}
                stats = measurements[key]
                stats["resolved_pixels"] = max(stats["resolved_pixels"], difference(current, first[i]))
                scaled = crop.resize((crop.width * zoom, crop.height * zoom), Image.Resampling.NEAREST)
                canvas.paste(scaled, position)
                draw.text((position[0], position[1] + scaled.height + 5), title_text, fill="#fff1d9", font=body)
            frames.append(canvas)

    failed = [name for name, stats in measurements.items()
              if stats["resolved_pixels"] < MIN_CHANGED_PIXELS or stats["reduced_motion_changed_pixels"] > 0]
    result = {"frames": len(frames), "fps": 10, "pixel_threshold": DELTA, "regions": measurements, "failed": failed}
    (REVIEW / "player-motion.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    frames[0].save(REVIEW / "player-ambiente.gif", save_all=True, append_images=frames[1:], duration=100, loop=0)
    full_frames[0].save(REVIEW / "player-praca-em-movimento.webp", save_all=True, append_images=full_frames[1:],
                        duration=100, loop=0, lossless=True, method=3)
    print(json.dumps(result, ensure_ascii=False))
    if failed:
        raise ValueError(f"Motion is unreadable or not reduced in: {', '.join(failed)}")


if __name__ == "__main__":
    main()
