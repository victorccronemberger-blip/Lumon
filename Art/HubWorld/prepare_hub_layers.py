"""Register the painted cleanup and build individual Unity props, keeping source pixels outside their masks."""
from pathlib import Path
import hashlib
import json
import math

import cv2
import numpy as np
from PIL import Image, ImageDraw

ART = Path(__file__).resolve().parent
PROJECT = ART.parents[1]
ASSETS = PROJECT / "Assets" / "Lumon" / "HubWorld" / "Art"
ORIGINAL = PROJECT / "Assets" / "Lumon" / "GuardianReview" / "Art2D" / "ObservatoryReviewPlate.png"
REPAIR = ART / "source" / "observatory-clean-higgsfield.png"
DETAIL = ART / "source" / "observatory-clean-details-higgsfield.png"
FRAME_TREE = ART / "source" / "observatory-frame-tree-clean-higgsfield.png"

# Polygons trace the original painted layers in their native 1672 × 941 space.
# Suspension points are absolute pixels in that same image.
DETAIL_REPAIRS = {"CampfireHearth", "WestLantern", "ExpeditionLantern", "ObservatoryBanner"}

# Motion amplitudes, read in the 1280 × 720 view (one plate pixel ≈ 0.766 screen pixels).
# Tree: plate pixels of the largest tip travel, gusts included (HubLayerMotion keeps every row within it).
#   It is a hard ceiling: calm sway is ~45 % of it and gust peaks ~85 % (≈ 10 px at the crown tips),
#   while the trunks stay under ~2 px (PlazaTree holds 22 %, FrameTreeWest 55 % of the height).
# Lantern: degrees of the largest swing; chosen from the pivot-to-bottom arm so the lantern
#   bottom travels ~5 plate px in calm air and ~6.5 px in a gust (West arm ≈ 38 px, East arm ≈ 58 px).
# Flame: fraction of height change. Glow: fraction the halo dims, never brighter than painted.
PLAZA_TREE_TIP_PX = 12.0
FRAME_TREE_TIP_PX = 12.5
WEST_LANTERN_DEG = 10.5
EAST_LANTERN_DEG = 7.0
FLAME_HEIGHT = 0.2
# Cut-out padding. Sway layers get extra horizontal transparent margin so the shader, which drops
# everything sampled beyond the cut-out, never clips the silhouette at the peak of a gust.
PAD = 7
SWAY_SAFETY_PX = 3
LAYERS = [
    ("PlazaTree", "Tree", -30, (1089, 415), PLAZA_TREE_TIP_PX, 5.7, 0.5,
     [(866,321),(872,307),(878,297),(893,285),(915,283),(927,271),(937,258),
      (951,249),(960,237),(979,235),(1000,219),(1020,217),(1037,207),(1051,204),
      (1070,202),(1094,214),(1108,232),(1124,239),(1140,256),(1153,266),(1166,274),
      (1180,287),(1191,304),(1180,315),(1161,318),(1157,340),(1142,351),(1141,371),
      (1151,388),(1153,403),(1137,415),(1129,426),(1111,434),(1096,428),(1084,424),
      (1061,414),(1044,408),(1028,395),(1009,391),(1005,373),(1023,356),(1042,345),
      (1021,337),(1002,347),(984,345),(959,354),(946,341),(923,342),(918,351),
      (903,346),(897,333),(878,331)]),
    ("CampfireHearth", "Static", -21, (930, 442), 0, 1, 0,
     [(888,437),(901,429),(910,429),(917,423),(943,425),(951,429),(966,430),
      (976,437),(972,448),(962,451),(952,459),(929,457),(907,459),(890,453)]),
    ("CampfireFlame", "Flame", -19, (931, 437), FLAME_HEIGHT, 0.72, 0.4,
     [(901,432),(911,428),(917,424),(918,418),(922,412),(925,414),(926,408),
      (930,399),(932,402),(933,394),(937,408),(937,416),(940,413),(945,424),
      (953,427),(961,434),(948,439),(934,440),(916,438),(908,438)]),
    ("WestLantern", "Lantern", -15, (178, 461), WEST_LANTERN_DEG, 2.6, 1.2,
     [(171,460),(182,459),(189,460),(194,465),(190,470),(190,484),
      (191,490),(184,498),(179,499),(166,494),(163,487),(163,473),(161,467),(165,462)]),
    ("EastLantern", "Lantern", -15, (1414, 319), EAST_LANTERN_DEG, 2.4, 0.45,
     [(1411,316),(1418,317),(1420,332),(1425,339),(1426,346),(1431,354),
      (1430,367),(1426,373),(1419,377),(1410,372),(1407,367),(1407,351),
      (1411,347),(1411,342),(1416,339),(1411,332)]),
    ("ExpeditionLantern", "Static", -15, (1121, 448), 0, 1, 0,
     [(1106,402),(1114,396),(1124,395),(1133,398),(1140,402),(1143,411),
      (1148,417),(1148,440),(1153,446),(1145,455),(1104,456),(1094,450),
      (1096,441),(1096,419),(1103,413)]),
    ("EastBanner", "Banner", -16, (1467, 318), 5.7, 3.5, 0.8,
     [(1436,316),(1451,317),(1463,321),(1480,322),(1499,326),(1500,343),
      (1499,359),(1501,380),(1496,400),(1488,405),(1483,414),(1470,424),
      (1463,415),(1457,416),(1445,406),(1441,394),(1438,374),(1439,351),(1436,333)]),
    ("ObservatoryBanner", "Banner", -16, (711, 165), 2.1, 4.2, 2.2,
     [(695,161),(720,161),(724,167),(724,185),(726,207),(721,224),(716,225),
      (712,219),(705,224),(698,218),(699,201),(696,184)]),
    ("ExpeditionDeskBanner", "Banner", -16, (1107, 459), 1.9, 4.8, 1.9,
     [(1077,453),(1094,456),(1115,454),(1133,457),(1135,480),(1135,507),
      (1127,520),(1114,530),(1102,539),(1096,528),(1090,526),(1085,520),
      (1082,506),(1080,484)]),
]


def native_mask(points, width, height):
    """Trace at twice resolution then preserve the original edge antialiasing."""
    high = Image.new("L", (width * 2, height * 2))
    ImageDraw.Draw(high).polygon([(x * 2, y * 2) for x, y in points], fill=255)
    return np.asarray(high.resize((width, height), Image.Resampling.LANCZOS)).copy()


def tree_mask(image, rough):
    """Remove sky visible between foliage and branches in the silhouette."""
    y0, y1, x0, x1 = 196, 440, 855, 1200
    rgb = image[y0:y1, x0:x1].astype(np.int16)
    gate = rough[y0:y1, x0:x1] > 127
    sky = ((rgb[:, :, 2] > rgb[:, :, 1] + 13) &
           (rgb[:, :, 2] > rgb[:, :, 0] - 27) & (rgb[:, :, 2] > 132))
    chroma = ((rgb[:, :, 0] > rgb[:, :, 2] + 32) &
              (rgb[:, :, 1] > rgb[:, :, 2] + 16))
    labels = np.full(gate.shape, cv2.GC_BGD, np.uint8)
    labels[gate] = cv2.GC_PR_FGD
    labels[gate & sky] = cv2.GC_PR_BGD
    labels[gate & chroma & ~sky] = cv2.GC_FGD
    trunk = np.s_[173:215, 219:252]
    labels[trunk][gate[trunk]] = cv2.GC_FGD
    # Pendant blossoms also belong to the tree despite their purple hue.
    for px, py in ((914, 331), (1000, 338), (1134, 352)):
        box = np.s_[py - y0 - 7:py - y0 + 9, px - x0 - 5:px - x0 + 6]
        cropped = rgb[box]
        blossom = (cropped[:, :, 0] > cropped[:, :, 1] + 30) & (cropped[:, :, 2] > cropped[:, :, 1] + 16)
        labels[box][blossom & gate[box]] = cv2.GC_FGD
    bg, fg = np.zeros((1, 65), np.float64), np.zeros((1, 65), np.float64)
    cv2.setRNGSeed(0)
    cv2.grabCut(rgb.astype(np.uint8), labels, None, bg, fg, 4, cv2.GC_INIT_WITH_MASK)
    cut = np.isin(labels, (cv2.GC_FGD, cv2.GC_PR_FGD)).astype(np.uint8) * 255
    full = np.zeros_like(rough)
    full[y0:y1, x0:x1] = cv2.GaussianBlur(cut, (3, 3), 0.48)
    return full


def framing_tree_masks(original, cleaned):
    h, w = original.shape[:2]
    # The booth, distant trees and the paper title belong to other planes.
    contour = native_mask([
        (0,0),(590,0),(582,29),(560,47),(541,61),(525,90),(514,138),
        (490,160),(466,149),(446,121),(406,124),(378,137),(357,166),
        (326,150),(302,168),(281,206),(252,205),(233,181),(209,218),
        (176,201),(156,185),(149,206),(137,239),(122,262),(104,275),
        (91,303),(77,320),(62,332),(50,352),(37,374),(25,394),(0,428)
    ], w, h)
    parchment = native_mask([
        (19,75),(23,55),(31,34),(65,22),(124,21),(164,17),(197,23),
        (237,22),(266,23),(300,23),(330,28),(367,26),(401,25),
        (420,25),(434,40),(435,58),(426,77),(417,100),(400,108),
        (373,107),(333,109),(305,114),(264,108),(233,108),(197,114),
        (167,109),(131,111),(92,110),(67,109),(35,113),(23,100)
    ], w, h)
    footprint = np.minimum(contour, 255 - parchment)
    live = footprint[:440, :600] > 120
    p = original[:440, :600].astype(np.int16)
    q = cleaned[:440, :600].astype(np.int16)
    distance = np.max(np.abs(p - q), axis=2)
    blue_sky = (p[:, :, 2] > p[:, :, 0] + 10) & (p[:, :, 2] > p[:, :, 1] + 20)
    warm_or_pink = ((p[:, :, 0] > p[:, :, 2] + 40) |
                    ((p[:, :, 0] > p[:, :, 1] + 28) & (p[:, :, 2] > p[:, :, 1] + 15)))
    labels = np.full(live.shape, cv2.GC_BGD, np.uint8)
    labels[live] = cv2.GC_PR_FGD
    labels[live & ((distance < 29) | blue_sky)] = cv2.GC_PR_BGD
    labels[live & warm_or_pink & (distance > 62)] = cv2.GC_FGD
    labels[114:268, :42][live[114:268, :42]] = cv2.GC_FGD
    bg, fg = np.zeros((1, 65), np.float64), np.zeros((1, 65), np.float64)
    cv2.setRNGSeed(0)
    cv2.grabCut(p.astype(np.uint8), labels, None, bg, fg, 4, cv2.GC_INIT_WITH_MASK)
    foreground = np.isin(labels, (cv2.GC_FGD, cv2.GC_PR_FGD)).astype(np.uint8) * 255
    result = np.zeros_like(footprint)
    result[:440, :600] = cv2.GaussianBlur(foreground, (3, 3), 0.44)
    return result, footprint, parchment


def aligned_cleanup(original, repaired):
    """Register unchanged landmarks before sampling only the cleaned footprints."""
    h, w = original.shape[:2]
    if abs(repaired.shape[1] / repaired.shape[0] - w / h) > 0.035:
        raise ValueError("The cleanup has a different framing.")
    resized = cv2.resize(repaired, (w, h), interpolation=cv2.INTER_CUBIC)
    sift = cv2.SIFT_create(nfeatures=5000)
    k0, d0 = sift.detectAndCompute(cv2.cvtColor(original, cv2.COLOR_RGB2GRAY), None)
    k1, d1 = sift.detectAndCompute(cv2.cvtColor(resized, cv2.COLOR_RGB2GRAY), None)
    pairs = cv2.BFMatcher().knnMatch(d1, d0, k=2)
    good = [a for a, b in pairs if a.distance < 0.74 * b.distance]
    if len(good) < 32:
        raise ValueError("Too few unchanged anchors in the cleanup.")
    src = np.float32([k1[m.queryIdx].pt for m in good])
    dst = np.float32([k0[m.trainIdx].pt for m in good])
    matrix, inliers = cv2.estimateAffinePartial2D(src, dst, method=cv2.RANSAC, ransacReprojThreshold=4)
    if matrix is None or int(inliers.sum()) < 25 or abs(matrix[0, 0] - 1) > 0.10:
        raise ValueError("The cleanup no longer follows the same map.")
    aligned = cv2.warpAffine(resized, matrix, (w, h), flags=cv2.INTER_CUBIC, borderMode=cv2.BORDER_REFLECT)
    return aligned, {"matches": len(good), "inliers": int(inliers.sum()), "transform": matrix.tolist()}


def sway_margin(kind, amplitude):
    """Horizontal transparent margin, in plate pixels, on each side of a cut-out."""
    if kind != "Tree":
        return PAD
    return max(PAD, math.ceil(amplitude) + SWAY_SAFETY_PX)


def write_sprite(image, mask, name, kind, pivot, order, amplitude, period, phase):
    coords = np.column_stack(np.nonzero(mask > 6))
    if not len(coords):
        raise ValueError(f"{name} has no visible pixels")
    margin = sway_margin(kind, amplitude)
    # Vertical padding is unchanged, so held fractions and vertical pivots keep their meaning.
    top, left = coords.min(axis=0) - (PAD, margin)
    bottom, right = coords.max(axis=0) + (PAD + 1, margin + 1)
    h, w = mask.shape
    travel = math.ceil(amplitude) if kind == "Tree" else 0
    left_clearance = travel if left < 0 else 0
    right_clearance = travel if right > w else 0
    left -= left_clearance
    right += right_clearance
    y0, x0 = max(0, top), max(0, left)
    y1, x1 = min(h, bottom), min(w, right)
    padding = ((y0 - top, bottom - y1), (x0 - left, right - x1))
    rgb = np.pad(image[y0:y1, x0:x1], (*padding, (0, 0)), mode="edge")
    # Padding only exists where the cut-out meets the plate border. For a swaying layer that
    # border is the screen edge, so the off-plate margin repeats the edge column (opaque where the
    # art touches the border). A sway toward the inside then pulls in more of the same branch
    # instead of opening a strip of the cleaned plate at the edge of the screen.
    alpha = np.pad(mask[y0:y1, x0:x1], padding, mode="edge" if kind == "Tree" else "constant")
    if left_clearance:
        alpha[:, :left_clearance] = 0
    if right_clearance:
        alpha[:, -right_clearance:] = 0
    if travel and (alpha[:, :travel].max() > 6 or alpha[:, -travel:].max() > 6):
        raise ValueError(f"{name}: sway margin smaller than the {amplitude} px tip travel")
    rgba = np.dstack((rgb, alpha))
    Image.fromarray(rgba).save(ASSETS / f"{name}.png")
    return {"id": name, "image": f"{name}.png", "x": int(left), "y": int(top),
            "pivotX": (pivot[0] - left) / (right - left), "pivotY": (bottom - pivot[1]) / (bottom - top),
            "order": order, "motion": kind, "amplitude": amplitude, "period": period, "phase": phase}


def glow(shape, name, center, size, order, period, phase, opacity, amplitude=0.42):
    h, w = shape
    cx, cy = center
    rx, ry = size
    yy, xx = np.ogrid[:h, :w]
    radius = ((xx - cx) / rx)**2 + ((yy - cy) / ry)**2
    alpha = np.rint(np.clip(1.0 - radius, 0, 1)**2.5 * opacity).astype(np.uint8)
    color = np.empty((h, w, 3), np.uint8)
    color[:] = (255, 169, 55)
    return write_sprite(color, alpha, name, "Glow", center, order, amplitude, period, phase)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    original = np.array(Image.open(ORIGINAL).convert("RGB"))
    painted = np.array(Image.open(REPAIR).convert("RGB"))
    restored, registration = aligned_cleanup(original, painted)
    detail, detail_registration = aligned_cleanup(original, np.array(Image.open(DETAIL).convert("RGB")))
    framing, framing_registration = aligned_cleanup(original, np.array(Image.open(FRAME_TREE).convert("RGB")))
    h, w = original.shape[:2]
    removal = np.zeros((h, w), np.uint8)
    details_mask = np.zeros_like(removal)
    layers = []
    ASSETS.mkdir(parents=True, exist_ok=True)
    for name, kind, order, pivot, amplitude, period, phase, points in LAYERS:
        footprint = native_mask(points, w, h)
        mask = tree_mask(original, footprint) if kind == "Tree" else footprint
        if name in DETAIL_REPAIRS:
            details_mask = np.maximum(details_mask, footprint)
        else:
            removal = np.maximum(removal, footprint)
        layers.append(write_sprite(original, mask, name, kind, pivot, order, amplitude, period, phase))

    # Only the exposed footprints sample the registered painting. The rest stays byte-identical.
    def repair_weight(mask):
        kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (17, 17))
        cover = cv2.dilate((mask > 40).astype(np.uint8) * 255, kernel)
        return cv2.GaussianBlur(cover, (9, 9), 2.0).astype(np.float32)[:, :, None] / 255

    # The fixed west canopy is the mounting fixture. Restore a stray blossom with
    # the second cleanup and cover a pale trace in the east sky using nearby clean sky.
    recolor = native_mask([(990, 348), (1008, 347), (1011, 366), (994, 372)], w, h)
    details_mask = np.maximum(details_mask, recolor)
    stamp = native_mask([(1459, 339), (1480, 339), (1495, 356), (1491, 391),
                         (1483, 408), (1469, 410), (1456, 390), (1449, 362)], w, h)
    sky_mix = cv2.GaussianBlur(stamp, (21, 21), 5.0).astype(np.float32)[:, :, None] / 255
    restored = np.rint(restored * (1 - sky_mix) + np.roll(restored, 52, axis=1) * sky_mix).astype(np.uint8)

    weight, detail_weight = repair_weight(removal), repair_weight(details_mask)
    clean = original * (1 - weight) + restored * weight
    clean = np.rint(clean * (1 - detail_weight) + detail * detail_weight).astype(np.uint8)
    left_tree, framing_footprint, title_paper = framing_tree_masks(original, framing)
    tree_weight = repair_weight(framing_footprint) * (1 - title_paper.astype(np.float32)[:, :, None] / 255)
    clean = np.rint(clean * (1 - tree_weight) + framing * tree_weight).astype(np.uint8)
    layers.append(write_sprite(original, left_tree, "FrameTreeWest", "Tree", (26, 382), 14, FRAME_TREE_TIP_PX, 6.2, 2.3))
    layers.append(write_sprite(original, title_paper, "ObservatoryTitlePaper", "Static", (232, 63), 17, 0, 1, 0))
    untouched = (weight[:, :, 0] == 0) & (detail_weight[:, :, 0] == 0) & (tree_weight[:, :, 0] == 0)
    if not np.array_equal(original[untouched], clean[untouched]):
        raise RuntimeError("The cleanup changed pixels outside the prop footprints.")
    Image.fromarray(clean).save(ASSETS / "ObservatoryClean.png")

    layers.extend([
        # Halos follow their lantern in HubLayerMotion (pairing by id: XGlow -> X, CampfireGlow -> CampfireFlame).
        # The hanging lantern halos were too faint (24) for their flicker to read; 40 keeps them soft.
        glow((h, w), "CampfireGlow", (932, 429), (43, 30), -18, 0.87, 0.68, 30, 0.45),
        glow((h, w), "WestLanternGlow", (178, 478), (20, 25), -14, 2.3, 1.6, 40, 0.5),
        glow((h, w), "EastLanternGlow", (1420, 361), (17, 23), -14, 1.9, 2.1, 40, 0.5),
        glow((h, w), "ExpeditionLanternGlow", (1121, 431), (40, 34), -14, 2.4, 0.45, 124, 0.65),
    ])
    for layer in layers:
        if not all(0 <= layer[p] <= 1 for p in ("pivotX", "pivotY")):
            raise ValueError(f"Pivot outside sprite: {layer['id']}")
    manifest = {"width": w, "height": h, "baseImage": "ObservatoryClean.png", "layers": layers}
    (ART / "hub-layers.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    preview = Image.fromarray(clean).convert("RGBA")
    for layer in sorted(layers, key=lambda item: item["order"]):
        sprite = Image.open(ASSETS / layer["image"]).convert("RGBA")
        preview.alpha_composite(sprite, (layer["x"], layer["y"]))
    preview.save(ART / "review" / "neutral-composite.png")
    details = {"sourceSha256": digest(ORIGINAL), "repairSha256": digest(REPAIR), "registration": registration,
               "detailSha256": digest(DETAIL), "detailRegistration": detail_registration,
               "frameTreeSha256": digest(FRAME_TREE), "frameTreeRegistration": framing_registration,
               "unchangedOutsideRepair": True, "layers": len(layers),
               "changedPlatePixels": int(np.any(original != clean, axis=2).sum())}
    (ART / "review" / "art-preparation.json").write_text(json.dumps(details, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(details))


if __name__ == "__main__":
    main()
