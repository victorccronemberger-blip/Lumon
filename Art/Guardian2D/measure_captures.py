"""Measure what the actual fixed Unity camera rendered, with and without the sprite."""
from pathlib import Path
import json

import numpy as np
from PIL import Image

REVIEW = Path(__file__).resolve().parent / "review"


def main():
    result = []
    for width, height in [(1280, 720), (1920, 1080)]:
        stem = f"unity-{width}x{height}"
        frame = Image.open(REVIEW / f"{stem}.png").convert("RGB")
        base = Image.open(REVIEW / f"{stem}-background.png").convert("RGB")
        if frame.size != (width, height) or base.size != frame.size:
            raise ValueError(f"Unexpected capture size for {stem}")
        delta = np.abs(np.asarray(frame, dtype=np.int16) - np.asarray(base, dtype=np.int16))
        y, x = np.where(np.max(delta, axis=2) > 18)
        left, top, right, bottom = int(x.min()), int(y.min()), int(x.max()) + 1, int(y.max()) + 1
        height_px = bottom - top
        expected_height = 165 * height / 720
        if abs(height_px - expected_height) > 4 or abs(bottom - height * (1 - 0.295)) > 4:
            raise ValueError(f"{stem}: unexpected height or boot anchor at {(left, top, right, bottom)}")
        result.append({
            "resolution": [width, height],
            "spriteBounds": [left, top, right, bottom],
            "spriteHeightPixels": height_px,
        })
        padding = round(height * 0.025)
        frame.crop((left - padding, top - padding, right + padding, bottom + padding)
                   ).save(REVIEW / f"{stem}-detail.png")
    print(json.dumps(result, indent=2))
    (REVIEW / "capture-metrics.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
