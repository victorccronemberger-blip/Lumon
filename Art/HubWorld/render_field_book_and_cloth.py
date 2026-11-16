"""Prepare the field-book lettering sheet and folded fabric from the painted Hub."""
from pathlib import Path
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFilter
from build_aurora_inscrita import make_glyph_sheet

HERE = Path(__file__).resolve().parent
ART = HERE.parents[1] / "Assets/Lumon/HubWorld/UI/Art"
CHARACTERS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz ÀÁÂÃÄÇÉÊÍÓÔÕÖÚÜÑàáâãäçéêíóôõöúüñ"
RECT = (174, 307, 269, 384)
FRAMES = 12


def main():
    make_glyph_sheet(CHARACTERS, ART.parent / "Fonts/SourceSerif4-Semibold.otf", "field-book-glyphs",
                     size=68, ink_stroke=1, lean=0.085)
    with Image.open(HERE / "review/unity-1280x720-base-only.png") as source:
        paper = np.asarray(source.crop(RECT).convert("RGB"), dtype=np.float32)
    height, width = paper.shape[:2]
    coverage = Image.new("L", (width, height))
    ImageDraw.Draw(coverage).polygon(
        [(3,17), (27,21), (56,17), (80,8), (87,2), (88,23), (89,52),
         (77,59), (59,66), (36,70), (17,68), (4,64)],
        fill=255,
    )
    matte = np.asarray(coverage.filter(ImageFilter.GaussianBlur(1.6)), dtype=np.float32) / 255
    y, x = np.mgrid[0:height, 0:width].astype(np.float32)
    # Pitched creases share the line warp used by HubInscription. Feathering
    # confines every change to the cloth and keeps the painted ties pinned.
    across = (x + RECT[0] - 176) / 83
    angle = 2 * np.pi * (across * 1.48 + (y + RECT[1] - 321) * .010)
    bend = np.sin(np.pi * np.clip(across, 0, 1))
    depth = np.clip((y + RECT[1] - 317) / 44, 0, 1)
    atlas = Image.new("RGBA", ((width + 2) * FRAMES, height + 2))
    for frame in range(FRAMES):
        t = 2 * np.pi * frame / FRAMES
        fold = angle + .38 * np.sin(t)
        warp = (np.sin(fold) * 2.6 + np.sin(fold * 2) * .55) * bend * depth
        coords_y = (y - warp * matte).astype(np.float32)
        shaded = cv2.remap(paper, x, coords_y, cv2.INTER_LINEAR, borderMode=cv2.BORDER_REPLICATE)
        shadow = np.maximum(0, np.cos(fold)) ** 1.45
        crest = np.maximum(0, -np.cos(fold)) ** 2
        shaded = shaded * (1 - .245 * shadow[..., None] * bend[..., None] * depth[..., None])
        shaded += (255 - shaded) * (.115 * crest[..., None] * bend[..., None] * depth[..., None])
        patch = np.dstack((np.clip(shaded, 0, 255).astype("uint8"), (255 * matte).astype("uint8")))
        atlas.paste(Image.fromarray(patch), (frame * (width + 2) + 1, 1))
    atlas.save(ART / "team-cloth.png")


if __name__ == "__main__":
    main()
