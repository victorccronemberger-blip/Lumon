"""Prepare the painted SouthWest walk cycle from the Higgsfield GIF.

Reads only Art/Guardian2D/source and writes only Art/Guardian2D/walk/southwest:
frame-NN.png (RGBA, native source resolution, one shared canvas), clip.json and
two local previews at the fixed 720p game scale. Frames are never interpolated,
resized one by one or mirrored.

    python Art/Guardian2D/prepare_walk_southwest.py
"""
from pathlib import Path
import hashlib
import json
import math
import sys

import cv2
import numpy as np
from PIL import Image, ImageDraw

BASE = Path(__file__).resolve().parent
SOURCE = BASE / "source" / "guardian-apprentice-walk-southwest.gif"
SOURCE_SHA256 = "a2833e8a572c9ab975f7b06d629620bce6b26a02d52b6c7795e1baf48b5d82b4"
OUT = BASE / "walk" / "southwest"

# Fixed review camera (GuardianReviewScene / GuardianMotion, read only).
ORTHO_HALF_SIZE = 4.1
SCREEN = (1280, 720)
CAMERA_PITCH_DEG = 30.0
SPRITE_HEIGHT_720 = 165.0
ENGINE_SPEED = 2.8  # m/s, GuardianMotion.Speed
PX_PER_METER_720 = SCREEN[1] / (2 * ORTHO_HALF_SIZE)
# Billboarded sprite, camera pitched 30 deg: ground x maps 1:1, ground z by sin(30 deg).
SW = (-math.sqrt(0.5), -math.sqrt(0.5))
SW_SCREEN_PER_METER = (SW[0] * PX_PER_METER_720,
                       -SW[1] * math.sin(math.radians(CAMERA_PITCH_DEG)) * PX_PER_METER_720)  # +y = down

# --- Annotations from inspecting all eight poses (hypotheses until engine capture) ---
# "left" is the near leg for a SouthWest facing (buckle on its outer side, it occludes
# the other leg). Boxes are the boot column ranges in source pixels.
BOOTS = {
    0: {"left": (141, 224), "right": (247, 306)},   # contact: left heel strikes, right toe-off
    1: {"left": (169, 246), "right": (255, 304)},   # loading: right heel lifts
    2: {"left": (188, 256), "right": (250, 297)},   # right swings behind
    3: {"left": (205, 269), "right": (277, 309)},   # right swings behind, high
    4: {"left": (221, 283), "right": None},         # passing: right knee high, boot hidden
    5: {"left": (268, 321), "right": (129, 204)},   # contact: right heel strikes, left toe-off
    6: {"left": (274, 328), "right": (153, 234)},   # loading: left heel lifts
    7: {"left": (279, 311), "right": (201, 268)},   # left swings behind, high
}
SUPPORT = {0: "left", 1: "left", 2: "left", 3: "left", 4: "left", 5: "left", 6: "right", 7: "right"}
# Foot that stays on (or within a few px of) the ground across each source transition.
GROUND_TRACK = {0: "left", 1: "left", 2: "left", 3: "left", 4: "left", 5: "right", 6: "right", 7: "right"}
CONTACT_FRAMES = (0, 5)
# Chosen cycle (see animation-review.md): pairs (0,5) contact, (1,6) loading, (3,7) swing
# behind. Frames 2 and 4 are dropped so both steps get three equal distance slots.
CHOSEN = (0, 1, 3, 5, 6, 7)
CANDIDATES = [tuple(range(8))] + [
    tuple(sorted((0, 5, 6, 7) + pair))
    for pair in ((1, 2), (1, 3), (1, 4), (2, 3), (2, 4), (3, 4))
]
MARGIN = 10
SOLE_BAND = 420  # rows below this hold the soles that can touch the ground


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def load_frames():
    if sha256(SOURCE) != SOURCE_SHA256:
        raise SystemExit(f"Source hash changed: {SOURCE}")
    frames, durations = [], []
    with Image.open(SOURCE) as gif:
        if gif.n_frames != 8 or gif.size != (480, 480):
            raise SystemExit("Expected eight 480x480 frames")
        for i in range(gif.n_frames):
            gif.seek(i)
            durations.append(int(gif.info.get("duration", 0)))
            frames.append(np.array(gif.convert("RGBA")))
    return frames, durations


def main_mask(rgba):
    alpha = (rgba[:, :, 3] > 0).astype(np.uint8)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(alpha, connectivity=8)
    main = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    stray = int(sum(stats[k, cv2.CC_STAT_AREA] for k in range(1, count) if k != main))
    return labels == main, stray


def soften(rgba, mask):
    """Inward one-pixel feather of the binary GIF edge plus colour bleed under alpha 0."""
    blur = cv2.GaussianBlur(mask.astype(np.float32), (0, 0), 0.7)
    alpha = np.where(mask, np.clip(blur / 0.97, 0, 1), 0)
    out = rgba.copy()
    _, near = cv2.distanceTransformWithLabels((~mask).astype(np.uint8), cv2.DIST_L2, 5,
                                              labelType=cv2.DIST_LABEL_PIXEL)
    palette = np.zeros((int(near.max()) + 1, 3), np.uint8)
    palette[near[mask]] = rgba[:, :, :3][mask]
    out[:, :, :3][~mask] = palette[near[~mask]]
    out[:, :, 3] = np.rint(alpha * 255).astype(np.uint8)
    return out


def brooch(rgba, threshold=0):
    r, g, b, a = (rgba[:, :, k].astype(int) for k in range(4))
    hit = (a > threshold) & (g > 110) & (b > 100) & (r < 120) & (g - r > 40)
    ys, xs = np.nonzero(hit)
    return float(xs.mean()), float(ys.mean())


def sole(mask, box):
    """Lowest opaque row and contact columns inside a boot column range."""
    x0, x1 = box
    rows, cols = np.nonzero(mask[SOLE_BAND:, x0:x1 + 1])
    if not len(rows):
        return None
    low = int(rows.max()) + SOLE_BAND
    contact = cols[rows + SOLE_BAND >= low - 2] + x0
    return {"y": low, "x": float(contact.mean())}


def track(frames, masks, i, j, foot, height=30):
    """Template-match a boot from frame i into frame j; returns (dx, dy, rms)."""
    x0, x1 = BOOTS[i][foot]
    yb = sole(masks[i], BOOTS[i][foot])["y"]
    tpl = frames[i][yb - height:yb + 1, x0:x1 + 1]
    tmask = masks[i][yb - height:yb + 1, x0:x1 + 1].astype(np.float32)
    tgray = cv2.cvtColor(tpl[:, :, :3], cv2.COLOR_RGB2GRAY).astype(np.float32) * tmask
    gray = cv2.cvtColor(frames[j][:, :, :3], cv2.COLOR_RGB2GRAY).astype(np.float32)
    gray[~masks[j]] = 230
    res = cv2.matchTemplate(gray, tgray, cv2.TM_SQDIFF, mask=tmask)
    yy, xx = np.mgrid[0:res.shape[0], 0:res.shape[1]]
    res[(abs(yy - (yb - height)) >= 25) | (abs(xx - x0) >= 70)] = np.inf
    y, x = np.unravel_index(int(np.argmin(res)), res.shape)
    return int(x - x0), int(y - (yb - height)), float(math.sqrt(res[y, x] / tmask.sum()))


def evaluate(order, chain, scale):
    """Per-slot planted-foot slide (720p px) when every frame gets an equal distance slot."""
    total = sum(chain[k][0] for k in range(8))
    per_slot = total / len(order)
    slides, worst_cum, cum = [], 0.0, 0.0
    for a, b in zip(order, order[1:] + order[:1]):
        k, disp = a, 0
        while k != b:
            disp += chain[k][0]
            k = (k + 1) % 8
        if a in CONTACT_FRAMES:
            cum = 0.0
        slide = (disp - per_slot) * scale
        cum += slide
        slides.append(round(slide, 2))
        worst_cum = max(worst_cum, abs(cum))
    return {"order": list(order), "slidePx720": slides,
            "maxSlidePx720": round(max(map(abs, slides)), 2), "maxDriftPx720": round(worst_cum, 2)}


def idle_reference():
    sys.path.insert(0, str(BASE))
    sys.dont_write_bytecode = True
    from prepare_idle_directions import extract, isolate  # pure functions, nothing written
    rgba, meta = isolate(extract("southwest-corrected", None), "SouthWest")
    approved = next(p for p in json.loads((BASE / "idle-directions.json").read_text("utf-8"))["poses"]
                    if p["direction"] == "SouthWest")
    if abs(meta["pivotX"] - approved["pivotX"]) > 1e-9 or abs(meta["pivotY"] - approved["pivotY"]) > 1e-9:
        raise SystemExit("Approved SouthWest idle registration no longer reproduces")
    top = int(np.nonzero((rgba[:, :, 3] > 105).any(1))[0].min())
    bx, by = brooch(rgba, 105)
    return rgba, approved, {"pivotXpx": approved["pivotX"] * rgba.shape[1], "broochX": bx,
                            "headToBrooch": by - top}


def resized(rgba, scale):
    img = Image.fromarray(rgba).convert("RGBa")
    size = (max(1, round(img.width * scale)), max(1, round(img.height * scale)))
    return img.resize(size, Image.LANCZOS).convert("RGBA")


def paste_at_pivot(canvas, sprite, pivot, at):
    x = round(at[0] - pivot[0] * sprite.width)
    y = round(at[1] - (1 - pivot[1]) * sprite.height)
    canvas.alpha_composite(sprite, (x, y))


def write_previews(outputs, clip, idle_rgba, idle_meta, foot_points):
    scale = SPRITE_HEIGHT_720 / clip["referenceHeightPx"]
    idle_scale = SPRITE_HEIGHT_720 / idle_meta["opaqueHeight"]
    floor, line = (110, 112, 107, 255), (60, 62, 58, 255)

    # 1. Strip at the fixed 720p scale: approved idle, then every cycle frame on its pivot.
    cell, ground = 92, 200
    strip = Image.new("RGBA", (cell * (len(outputs) + 1) + 16, 236), floor)
    draw = ImageDraw.Draw(strip)
    draw.line([(0, ground), (strip.width, ground)], fill=line)
    draw.line([(0, ground - 165), (strip.width, ground - 165)], fill=(150, 60, 60, 255))
    paste_at_pivot(strip, resized(idle_rgba, idle_scale),
                   (idle_meta["pivotX"], idle_meta["pivotY"]), (cell // 2 + 8, ground))
    draw.text((8, ground + 8), "idle", fill=(0, 0, 0, 255))
    for k, (rgba, frame) in enumerate(zip(outputs, clip["frames"])):
        cx = cell * (k + 1) + cell // 2 + 8
        paste_at_pivot(strip, resized(rgba, scale), (frame["pivotX"], frame["pivotY"]), (cx, ground))
        draw.line([(cx, ground - 2), (cx, ground + 2)], fill=(200, 30, 30, 255))
        draw.text((cx - 40, ground + 8), f"{k:02d}<{clip['sourceFrames'][k]} {frame['supportFoot'][0].upper()}",
                  fill=(0, 0, 0, 255))
    draw.text((8, 4), "720p scale, red line = 165 px, red tick = pivot", fill=(0, 0, 0, 255))
    strip.save(OUT / "preview-720p-strip.png")

    # 2. Simulated engine travel: 720p crop, root moving SouthWest at 2.8 m/s, frame chosen by
    # distance like GuardianSpriteFacing. Dots mark where each support foot landed.
    tick = 0.04
    distance = clip["suggestedDistanceMeters"]
    sprites = [resized(rgba, scale) for rgba in outputs]
    width, height = 420, 300
    start = (330.0, 90.0)
    images, landed, prev_frame = [], None, None
    for n in range(int(2 * distance / (ENGINE_SPEED * tick)) + 1):
        travelled = n * ENGINE_SPEED * tick
        frame = min(int((travelled / distance % 1) * len(outputs)), len(outputs) - 1)
        root = (start[0] + SW_SCREEN_PER_METER[0] * travelled, start[1] + SW_SCREEN_PER_METER[1] * travelled)
        img = Image.new("RGBA", (width, height), floor)
        draw = ImageDraw.Draw(img)
        draw.line([start, (start[0] + SW_SCREEN_PER_METER[0] * 4, start[1] + SW_SCREEN_PER_METER[1] * 4)],
                  fill=line)
        draw.ellipse([root[0] - 16, root[1] - 5, root[0] + 16, root[1] + 5], fill=(80, 82, 78, 255))
        meta = clip["frames"][frame]
        paste_at_pivot(img, sprites[frame], (meta["pivotX"], meta["pivotY"]), root)
        fx, fy = foot_points[frame]
        foot = (root[0] + fx * scale, root[1] + fy * scale)
        if frame != prev_frame and (prev_frame is None or frame in clip["measurements"]["contactFrames"]):
            landed = foot
        prev_frame = frame
        draw.ellipse([landed[0] - 2, landed[1] - 2, landed[0] + 2, landed[1] + 2], outline=(230, 40, 40, 255))
        draw.ellipse([foot[0] - 1, foot[1] - 1, foot[0] + 1, foot[1] + 1], fill=(40, 200, 230, 255))
        images.append(img.convert("RGB"))
    images[0].save(OUT / "preview-720p-travel.gif", save_all=True, append_images=images[1:],
                   duration=int(tick * 1000), loop=0, optimize=False)


def main():
    frames, durations = load_frames()
    masks, strays = zip(*(main_mask(f) for f in frames))

    # Ground per frame: the support sole must be the lowest sole of that frame.
    soles = {i: {foot: (sole(masks[i], box) if box else None) for foot, box in BOOTS[i].items()} for i in range(8)}
    for i in range(8):
        lowest = max(s["y"] for s in soles[i].values() if s)
        if soles[i][SUPPORT[i]]["y"] != lowest or lowest != int(np.nonzero(masks[i].any(1))[0].max()):
            raise SystemExit(f"Support annotation does not own the lowest sole in frame {i}")

    chain = {i: track(frames, masks, i, (i + 1) % 8, GROUND_TRACK[i]) for i in range(8)}
    tops = {i: int(np.nonzero(masks[i].any(1))[0].min()) for i in range(8)}
    single = [i for i in range(8) if i not in CONTACT_FRAMES]
    reference_height = max(soles[i][SUPPORT[i]]["y"] - tops[i] + 1 for i in single)
    scale = SPRITE_HEIGHT_720 / reference_height

    step_left = sum(chain[k][0] for k in range(0, 5))
    step_right = sum(chain[k][0] for k in range(5, 8))
    stride = step_left + step_right
    centers = {i: {f: (b[0] + b[1]) / 2 for f, b in BOOTS[i].items() if b} for i in CONTACT_FRAMES}
    separation = [centers[0]["right"] - centers[0]["left"], centers[5]["left"] - centers[5]["right"]]
    distance = stride * scale / abs(SW_SCREEN_PER_METER[0])

    evaluations = [evaluate(order, chain, scale) for order in CANDIDATES]
    chosen = next(e for e in evaluations if tuple(e["order"]) == CHOSEN)
    source_order = evaluations[0]
    if chosen["maxDriftPx720"] >= source_order["maxDriftPx720"]:
        raise SystemExit("Dropping frames must reduce the planted-foot drift")

    idle_rgba, idle_meta, idle = idle_reference()
    walk_brooch = [brooch(frames[i]) for i in CHOSEN]
    brooch_x = float(np.mean([b[0] for b in walk_brooch]))
    head_to_brooch = float(np.mean([b[1] - tops[i] for b, i in zip(walk_brooch, CHOSEN)]))
    # Same trunk-to-ground offset as the approved idle, carried into walk pixels.
    anchor_x = brooch_x + (idle["pivotXpx"] - idle["broochX"]) * reference_height / idle_meta["opaqueHeight"]

    # One canvas for every frame, translation only.
    boxes = [cv2.boundingRect(masks[i].astype(np.uint8)) for i in CHOSEN]
    left = min(b[0] for b in boxes) - MARGIN
    top = min(b[1] for b in boxes) - MARGIN
    right = max(b[0] + b[2] for b in boxes) + MARGIN
    bottom = max(b[1] + b[3] for b in boxes) + MARGIN
    width, height = right - left, bottom - top

    OUT.mkdir(parents=True, exist_ok=True)
    for stale in OUT.glob("frame-*.png"):
        stale.unlink()
    outputs, entries, foot_points = [], [], []
    for k, i in enumerate(CHOSEN):
        rgba = soften(frames[i], masks[i])[top:bottom, left:right]
        support = soles[i][SUPPORT[i]]
        ground = support["y"] + 0.5  # centre of the lowest row, as in prepare_idle_directions
        name = f"frame-{k:02d}.png"
        Image.fromarray(rgba).save(OUT / name)
        outputs.append(rgba)
        entries.append({"file": name, "pivotX": round((anchor_x - left) / width, 6),
                        "pivotY": round((bottom - ground) / height, 6), "supportFoot": SUPPORT[i]})
        # The preview follows the foot that lands at contact and carries the following slots.
        landing = soles[i][GROUND_TRACK[i]]
        foot_points.append((landing["x"] - anchor_x, landing["y"] - support["y"]))

    pose_ms = sum(durations) / len(CHOSEN)
    stance_dy = sum(chain[k][1] for k in (1, 2, 3, 6))
    clip = {
        "direction": "SouthWest",
        "referenceHeightPx": reference_height,
        "cadenceMs": round(pose_ms, 2),
        "suggestedDistanceMeters": round(distance, 4),
        "frames": entries,
        "status": "proof-not-accepted",
        "sourceFrames": list(CHOSEN),
        "source": {"file": "source/guardian-apprentice-walk-southwest.gif", "sha256": SOURCE_SHA256,
                   "frameMs": durations, "strayPixelsRemoved": list(strays)},
        "conventions": {
            "pivot": "normalized, origin bottom-left; pivotX = trunk anchor shared by all frames; "
                     "pivotY = lowest row of the support sole in that frame",
            "supportFoot": "anatomical side; left = near leg for SouthWest. Hypothesis until engine capture",
            "cadenceMs": "source cycle (8 x 110 ms) spread over the retained poses",
        },
        "measurements": {
            "sourceToScreen720": round(scale, 5),
            "contactFrames": [CHOSEN.index(i) for i in CONTACT_FRAMES],
            "trackDxDyRmsBySourceTransition": {f"{i}->{(i + 1) % 8}": [chain[i][0], chain[i][1], round(chain[i][2], 1)]
                                               for i in range(8)},
            "stepSourcePx": {"left": step_left, "right": step_right},
            "contactFootSeparationSourcePx": separation,
            "candidates": evaluations,
            "broochSwaySourcePx": round(max(b[0] for b in walk_brooch) - min(b[0] for b in walk_brooch), 2),
            "headToBroochRatioWalkOverIdle": round((head_to_brooch / reference_height)
                                                   / (idle["headToBrooch"] / idle_meta["opaqueHeight"]), 4),
            "referenceHeightMatchingIdleHeadPx": round(head_to_brooch * idle_meta["opaqueHeight"] / idle["headToBrooch"], 1),
        },
        "predictions": {
            "engineSpeedMetersPerSecond": ENGINE_SPEED,
            "engineFrameMs": round(distance / len(CHOSEN) / ENGINE_SPEED * 1000, 1),
            "sourceImpliedSpeedMetersPerSecond": round(distance / (sum(durations) / 1000), 3),
            "screenPxPerMeter720": [round(v, 3) for v in SW_SCREEN_PER_METER],
            "plantedFootHorizontalDriftPx720": chosen["maxDriftPx720"],
            # pivotY sits on the support sole, so the planted foot follows the root down the screen.
            "plantedFootVerticalSlidePerStepPx720": round(SW_SCREEN_PER_METER[1] * distance / 2, 2),
            "stanceTrackDySourcePx": stance_dy,
            "expectedStanceTrackDySourcePx": round(-0.5 * sum(chain[k][0] for k in (1, 2, 3, 6)), 1),
        },
    }
    (OUT / "clip.json").write_text(json.dumps(clip, indent=2) + "\n", encoding="utf-8")
    write_previews(outputs, clip, idle_rgba, idle_meta, foot_points)
    print(json.dumps({k: clip[k] for k in ("referenceHeightPx", "cadenceMs", "suggestedDistanceMeters",
                                            "sourceFrames")}))
    for e in evaluations:
        print(e["order"], "maxSlide", e["maxSlidePx720"], "maxDrift", e["maxDriftPx720"], e["slidePx720"])
    print(json.dumps(clip["predictions"]))
    print(json.dumps({k: clip["measurements"][k] for k in ("stepSourcePx", "contactFootSeparationSourcePx",
                                                            "headToBroochRatioWalkOverIdle",
                                                            "referenceHeightMatchingIdleHeadPx",
                                                            "broochSwaySourcePx")}))


if __name__ == "__main__":
    main()
