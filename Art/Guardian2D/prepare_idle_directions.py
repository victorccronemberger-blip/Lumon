"""Trim and register the reviewed 2D poses without mirroring artwork."""
from pathlib import Path
import json

import cv2
import numpy as np
from PIL import Image

BASE = Path(__file__).resolve().parent
DEST = BASE.parents[1] / "Assets" / "Lumon" / "GuardianReview" / "Art2D" / "Directions"

# The extra northwest and northeast studies were the only rear diagonals in the original sheet.
# Use the accepted large portrait for southeast and its corrected opposite for southwest.
CARDS = "cardinals"
DIAGS = "diagonals"
DIRECTIONS = (
    ("North", CARDS, 0), ("NorthEast", DIAGS, 3), ("East", CARDS, 1),
    ("SouthEast", "three-quarter", None), ("South", CARDS, 2),
    ("SouthWest", "southwest-corrected", None), ("West", CARDS, 3), ("NorthWest", DIAGS, 2),
)


def extract(name, slot):
    with Image.open(BASE / "source" / f"guardian-apprentice-idle-{name}.png") as source:
        if slot is not None:
            w, h = source.width // 2, source.height // 2
            left, top = w * (slot % 2), h * (slot // 2)
            source = source.crop((left, top, left + w, top + h))
        return np.array(source.convert("RGBA"))


def isolate(rgba, name):
    alpha = rgba[:, :, 3]
    count, labels, stats, _ = cv2.connectedComponentsWithStats((alpha > 105).astype(np.uint8))
    if count < 2:
        raise ValueError(f"No usable silhouette in {name}")
    main = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    x, y, w, h, _ = map(int, stats[main])
    if min(x, y, rgba.shape[1] - x - w, rgba.shape[0] - y - h) < 4 or h < 280:
        raise ValueError(f"{name} needs clear source margins and a height of at least 280 px")
    subject = labels == main
    margin = 10
    alpha = np.rint(np.clip((alpha.astype(float) - 8) * 255 / 241, 0, 255)).astype(np.uint8)
    halo = cv2.dilate(subject.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
    alpha[~halo] = 0

    # Replace stray fringe color with the nearest opaque subject pixel.
    sure = subject & (alpha >= 226)
    _, near = cv2.distanceTransformWithLabels(
        (~sure).astype(np.uint8), cv2.DIST_L2, 5, labelType=cv2.DIST_LABEL_PIXEL
    )
    palette = np.zeros((int(near.max()) + 1, 3), dtype=np.uint8)
    palette[near[sure]] = rgba[:, :, :3][sure]
    fringe = (alpha > 0) & (alpha < 180)
    rgba[:, :, :3][fringe] = palette[near[fringe]]
    rgba[:, :, 3] = alpha

    # Set the ground anchor between the soles. Use the boot bands in diagonal stances.
    anchor_top = y + round(h * 0.91)
    _, feet_x = np.where(subject[anchor_top : y + h])
    anchor_x = (float(feet_x.min()) + float(feet_x.max()) + 1) / 2
    anchor_y = y + h - 0.5
    l, t = max(0, x - margin), max(0, y - margin)
    r, b = min(rgba.shape[1], x + w + margin), min(rgba.shape[0], y + h + margin)
    return rgba[t:b, l:r], {
        "direction": name,
        "pivotX": (anchor_x - l) / (r - l),
        "pivotY": (b - anchor_y) / (b - t),
        "opaqueHeight": h,
    }


def main():
    outputs = []
    for direction, name, slot in DIRECTIONS:
        rgba, metadata = isolate(extract(name, slot), direction)
        outputs.append((direction, rgba, metadata))
    DEST.mkdir(parents=True, exist_ok=True)
    for name, rgba, _ in outputs:
        Image.fromarray(rgba).save(DEST / f"{name}.png")
    metadata = {"poses": [item[2] for item in outputs]}
    (BASE / "idle-directions.json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(metadata))


if __name__ == "__main__":
    main()
