"""Split the approved composition into equal-scale reference panels and mattes."""
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

ASSET = Path(__file__).resolve().parents[1]
IMAGE_WIDTH = 360
GROUND = 666
CROWN = 13
PANEL_AXES = {"front": 196, "left": 500, "back": 835}
# Same image scale and ground row as the Blender calibration.
METRES_PER_PIXEL = 1.8 / (GROUND - CROWN)


def prepare_panel(image, axis):
    panel = np.asarray(image.crop((axis - IMAGE_WIDTH // 2, 0, axis + IMAGE_WIDTH // 2, image.height)))
    # The original illustration uses a neutral studio surround and a cast shadow.
    # Exclude the floor, then extract the single connected figure inside the crop.
    rect = (4, 9, IMAGE_WIDTH - 8, GROUND - 9)
    if axis == PANEL_AXES["left"]:
        rect = (106, 9, 149, GROUND - 9)
    cv2.setRNGSeed(0)
    labels = np.zeros(panel.shape[:2], dtype=np.uint8)
    color = cv2.cvtColor(panel, cv2.COLOR_RGB2BGR)
    background = np.zeros((1, 65), dtype=np.float64)
    foreground = np.zeros((1, 65), dtype=np.float64)
    cv2.grabCut(color, labels, rect, background, foreground, 5, cv2.GC_INIT_WITH_RECT)
    candidates = np.isin(labels, [cv2.GC_FGD, cv2.GC_PR_FGD]).astype(np.uint8)
    n, islands, stats, _ = cv2.connectedComponentsWithStats(candidates, connectivity=8)
    if n < 2:
        raise RuntimeError("No connected figure in calibration image")
    subject = islands == (1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA])))
    return panel, subject


def main():
    image = Image.open(ASSET / "guardiao-observatorio-turnaround.png").convert("RGB")
    for view, axis in PANEL_AXES.items():
        panel, subject = prepare_panel(image, axis)
        alpha = subject.astype(np.uint8) * 255
        Image.fromarray(np.dstack((panel, alpha))).save(ASSET / "ref" / f"{view}.png")
        Image.fromarray(alpha).save(ASSET / "ref" / f"{view}-matte.png")
        y, x = np.nonzero(subject)
        print(f"{view}: bbox x={x.min()}..{x.max()} y={y.min()}..{y.max()} | {len(x)} pixels")
        for row in [30, 60, 92, 121, 148, 180, 220, 260, 285, 321, 355, 395, 430, 485, 545, 590, 625, 657]:
            indices = np.flatnonzero(subject[row])
            spans = np.split(indices, np.where(np.diff(indices) > 1)[0] + 1)
            print(row, [(int(run[0]), int(run[-1])) for run in spans if len(run) >= 3])


if __name__ == "__main__":
    main()

