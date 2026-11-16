"""Prepare the Higgsfield cutout and record the boot anchor used by Unity."""
from pathlib import Path
import json

import numpy as np
from PIL import Image

PROJECT = Path(__file__).resolve().parents[2]
ART = PROJECT / "Art" / "Guardian2D"
UNITY = PROJECT / "Assets" / "Lumon" / "GuardianReview" / "Art2D"
SOURCE = ART / "source" / "guardian-apprentice-idle-three-quarter.png"


def main():
    sprite = np.asarray(Image.open(SOURCE).convert("RGBA")).copy()
    alpha = sprite[:, :, 3]
    y, x = np.where(alpha > 96)
    content_height = int(y.max() - y.min() + 1)
    # Keep natural antialiasing and clear the faint disconnected specks outside the cutout.
    alpha[:] = np.rint(np.clip((alpha.astype(float) - 8) * 255 / 241, 0, 255)).astype(np.uint8)
    margin = 9
    left, top, right, bottom = (int(x.min()) - margin, int(y.min()) - margin,
                               int(x.max()) + margin + 1, int(y.max()) + margin + 1)
    foot_x, foot_y = (328.75, 994.5)  # Midpoint of the two contact soles in the source.
    Image.fromarray(sprite[top:bottom, left:right]).save(UNITY / "GuardianIdleThreeQuarter.png")

    shadow_x, shadow_y = np.meshgrid(np.linspace(-1, 1, 160), np.linspace(-1, 1, 72))
    mask = np.clip(1 - shadow_x**2 - shadow_y**2, 0, 1)
    shadow = np.zeros((72, 160, 4), dtype=np.uint8)
    shadow[:, :, :3] = (44, 30, 49)
    shadow[:, :, 3] = np.rint(mask**1.8 * 145).astype(np.uint8)
    Image.fromarray(shadow).save(UNITY / "GuardianContactShadow.png")

    layout = {
        "pivotX": (foot_x - left) / (right - left),
        "pivotY": (bottom - foot_y) / (bottom - top),
        "opaqueHeight": content_height,
    }
    (ART / "sprite-layout.json").write_text(json.dumps(layout, indent=2) + "\n", encoding="utf-8")
    print(layout)


if __name__ == "__main__":
    main()
