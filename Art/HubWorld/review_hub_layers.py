"""Measure visible ambient changes in captures from the fixed Unity camera."""
from pathlib import Path
import hashlib
import json
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ART = Path(__file__).resolve().parent
ASSETS = ART.parents[1] / "Assets" / "Lumon" / "HubWorld" / "Art"
REVIEW = ART / "review"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    manifest_file = ART / "hub-layers.json"
    manifest = json.loads(manifest_file.read_text(encoding="utf-8-sig"))
    paths = [REVIEW / f"unity-1280x720-{i:03d}.png" for i in (0, 50, 100)]
    frames = [Image.open(path).convert("RGB") for path in paths]
    if {frame.size for frame in frames} != {(1280, 720)}:
        raise ValueError("Expected three 1280 × 720 captures from Unity.")
    pixels = np.array([np.asarray(frame) for frame in frames], dtype=np.int16)
    animated = np.max(np.ptp(pixels, axis=0), axis=2) > 7
    sx, sy = 1280 / manifest["width"], 720 / manifest["height"]
    dynamic_regions = np.zeros(animated.shape, dtype=bool)
    measurements = {}
    for layer in manifest["layers"]:
        with Image.open(ASSETS / layer["image"]) as image:
            x, y = layer["x"], layer["y"]
            l = max(0, math.floor(x * sx) - 12)
            t = max(0, math.floor(y * sy) - 12)
            r = min(1280, math.ceil((x + image.width) * sx) + 12)
            b = min(720, math.ceil((y + image.height) * sy) + 12)
        kind = layer["motion"]
        region_pixels = int(animated[t:b, l:r].sum())
        measurements[layer["id"]] = {"motion": kind, "region": [l, t, r, b],
                                      "visibleChangedPixels": region_pixels}
        if kind != "Static":
            dynamic_regions[t:b, l:r] = True
            if kind != "Glow" and region_pixels < 12:
                raise ValueError(f"No visible motion for {layer['id']}: {region_pixels} pixels changed.")

    outside = int((animated & ~dynamic_regions).sum())
    if outside:
        raise ValueError(f"{outside} pixels changed outside animated prop regions.")
    proof = {"manifestSha256": sha(manifest_file), "captures": {path.name: sha(path) for path in paths},
             "changedOutsideAnimatedRegions": outside, "layers": measurements}
    (REVIEW / "motion-pixels.json").write_text(json.dumps(proof, indent=2) + "\n", encoding="utf-8")

    sheet = Image.new("RGB", (924, 574), "#272832")
    labels = ImageDraw.Draw(sheet)
    head = ImageFont.truetype("segoeuib.ttf", 23)
    text = ImageFont.truetype("segoeui.ttf", 15)
    labels.text((16, 9), "Observatório da Aurora", font=head, fill="#fff3e7")
    labels.text((16, 39), "Prova de movimento no Unity · 1280 × 720", font=text, fill="#d7cbd4")
    selections = [
        ("Árvore da praça", (853, 198, 1200, 435)),
        ("Bandeira e lanterna leste", (1395, 310, 1514, 428)),
        ("Fogueira e luz", (877, 381, 987, 467)),
    ]
    y = 68
    for label, rectangle in selections:
        labels.text((16, y), label, font=text, fill="#fff3e7")
        crop = tuple(round(p * (sx if k % 2 == 0 else sy)) for k, p in enumerate(rectangle))
        for i, frame in enumerate(frames):
            x = 16 + i * 303
            image = frame.crop(crop)
            sheet.paste(image, (x, y + 23))
            labels.text((x, y + 28 + image.height), ("0,0 s", "0,5 s", "1,0 s")[i], font=text, fill="#d7cbd4")
        y += round((rectangle[3] - rectangle[1]) * sy) + 54
    sheet.save(REVIEW / "unity-motion-review.png")
    print(f"{int(animated.sum())} altered pixels, {outside} outside prop regions.")


if __name__ == "__main__":
    main()
