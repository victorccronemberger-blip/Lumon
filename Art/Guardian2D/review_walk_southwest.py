"""Review the southwest proof using the captures and anchors recorded by Unity."""
from pathlib import Path
import argparse
import hashlib
import math
import json

import cv2
from PIL import Image, ImageDraw, ImageFont

from prepare_walk_southwest import BOOTS, load_frames, main_mask, sole

BASE = Path(__file__).resolve().parent
ROOT = BASE.parents[1]



def load_record(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def annotated_offsets(clip, prepared_dir):
    offsets = []
    if all("contactX" in frame and "contactY" in frame for frame in clip["frames"]):
        for frame in clip["frames"]:
            cx, cy = float(frame["contactX"]), float(frame["contactY"])
            if not (0 < cx < 1 and 0 < cy < 1):
                raise ValueError(f"Contact outside the canvas in {frame['file']}")
            with Image.open(prepared_dir / frame["file"]) as sprite:
                x, y = round(cx * sprite.width), round((1 - cy) * sprite.height)
                neighbourhood = sprite.getchannel("A").crop((x - 3, y - 3, x + 4, y + 4))
                if neighbourhood.getextrema()[1] < 128:
                    raise ValueError(f"Contact does not touch the sole in {frame['file']}")
                offsets.append(((cx - frame["pivotX"]) * sprite.width,
                                (frame["pivotY"] - cy) * sprite.height))
        return offsets

    source, _ = load_frames()
    first = clip["sourceFrames"]
    masks = {index: main_mask(source[index])[0] for index in first}
    source_left = min(cv2.boundingRect(mask.astype("uint8"))[0] for mask in masks.values()) - 10
    for frame, index in zip(clip["frames"], first, strict=True):
        foot = sole(masks[index], BOOTS[index][frame["supportFoot"]])
        with Image.open(prepared_dir / frame["file"]) as sprite:
            offsets.append((foot["x"] - source_left - frame["pivotX"] * sprite.width, 0.0))
    return offsets


def review(scale, captures, clip, offsets, output):
    resolution = "720" if scale == 1 else "1080"
    font = ImageFont.truetype("segoeui.ttf", 15)
    title = ImageFont.truetype("segoeuib.ttf", 22)
    width, height = round(144 * scale), round(194 * scale)
    rows = math.ceil(len(captures) / 3)
    sheet = Image.new("RGB", (3 * width + 64, rows * (height + 32) + 83), "#262b34")
    draw = ImageDraw.Draw(sheet)
    draw.text((17, 12), "Caminhada sudoeste · prova no Unity", font=title, fill="#fff3e7")
    draw.text((17, 43), f"{resolution}p · recortes sem ampliação", font=font, fill="#d1c6bf")

    details = []
    for i, capture in enumerate(captures):
        silhouette = capture[f"at{resolution}"]
        frame_record = clip["frames"][i]
        path = ROOT / capture[f"file{resolution}"]
        if hashlib.sha256(path.read_bytes()).hexdigest() != silhouette["sha256"].lower():
            raise ValueError(f"Capture hash mismatch: {path}")
        offset_x, offset_y = offsets[i]
        pixels_per_source = 165 * scale / clip["referenceHeightPx"]
        world_foot_x = silhouette["anchorX"] + offset_x * pixels_per_source
        world_foot_y = silhouette["anchorY"] + offset_y * pixels_per_source
        left = round(silhouette["anchorX"] - width / 2)
        bottom = round(silhouette["anchorY"] + height * 0.09)
        x, y = 16 + (i % 3) * (width + 16), 72 + (i // 3) * (height + 32)
        with Image.open(path) as render:
            sheet.paste(render.crop((left, bottom - height, left + width, bottom)), (x, y))
        draw.text((x, y + height + 3), f"Quadro {i + 1} · {capture['strideMeters']:.2f} m",
                  font=font, fill="#fff3e7")
        details.append({"frame": i, "supportFoot": frame_record["supportFoot"],
                        "projectedFoot": [round(world_foot_x, 2), round(world_foot_y, 2)]})

    for previous, current in zip(details, details[1:]):
        if previous["supportFoot"] == current["supportFoot"]:
            current["sameSupportStepPx"] = [round(b - a, 2) for a, b in
                                            zip(previous["projectedFoot"], current["projectedFoot"])]
    sheet.save(output / f"review-{resolution}.png")
    return details


def playback(captures, cadence, output):
    silhouettes = [capture["at720"] for capture in captures]
    padding = 16
    crop = (min(s["left"] for s in silhouettes) - padding,
            min(s["top"] for s in silhouettes) - padding,
            max(s["right"] for s in silhouettes) + padding,
            max(s["bottom"] for s in silhouettes) + padding)
    frames = []
    for capture in captures:
        with Image.open(ROOT / capture["file720"]) as render:
            frames.append(render.crop(crop).convert("RGB"))
    frames[0].save(output / "unity-sudoeste-720.gif", save_all=True,
                   append_images=frames[1:], duration=round(cadence / 10) * 10, loop=0)


def support_over_time(evidence, captures, clip, offsets, scale):
    start = captures[0]["afterTick"]
    stop = next(c["afterTick"] for c in evidence["captures"] if c["label"] == "wrap-frame-00")
    pixel_scale = 165 / clip["referenceHeightPx"]
    stances = []
    for tick in evidence["ticks"]:
        if not start <= tick["tick"] < stop or tick["leg"] != "stride":
            continue
        frame = tick["frame"]
        if frame < 0:
            continue
        foot = clip["frames"][frame]["supportFoot"]
        dx, dy = offsets[frame]
        point = ((tick["screenX"] + dx * pixel_scale) * scale,
                 (tick["screenY"] + dy * pixel_scale) * scale)
        if not stances or stances[-1]["foot"] != foot:
            stances.append({"foot": foot, "first": point, "last": point, "samples": 0,
                            "maxFromFirstPx": 0.0, "maxHeldPoseTravelPx": 0.0,
                            "currentFrame": frame, "frameStart": point})
        stance = stances[-1]
        if stance["currentFrame"] != frame:
            stance["currentFrame"], stance["frameStart"] = frame, point
        stance["samples"] += 1
        stance["last"] = point
        stance["maxFromFirstPx"] = max(stance["maxFromFirstPx"], math.dist(stance["first"], point))
        stance["maxHeldPoseTravelPx"] = max(stance["maxHeldPoseTravelPx"],
                                             math.dist(stance["frameStart"], point))
    for stance in stances:
        stance.pop("currentFrame")
        stance.pop("frameStart")
        for name in ("maxFromFirstPx", "maxHeldPoseTravelPx"):
            stance[name] = round(stance[name], 2)
        for name in ("first", "last"):
            stance[name] = [round(v, 2) for v in stance[name]]
    return stances


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    variant = parser.add_mutually_exclusive_group()
    variant.add_argument("--grounding", dest="suffix", action="store_const", const="-grounding",
                         help="Review the six corrected key poses")
    variant.add_argument("--subframes", dest="suffix", action="store_const", const="-subframes",
                         help="Review the eighteen-pose cycle")
    variant.add_argument("--layered", dest="suffix", action="store_const", const="-layered",
                         help="Review the independently articulated layers")
    suffix = parser.parse_args().suffix or ""
    output = BASE / "review" / ("walk-southwest" + suffix)
    prepared_dir = BASE / "walk" / ("southwest" + suffix)
    evidence = load_record(output / "evidence.json")
    if not evidence["passed"] or evidence["syntheticSelfTest"]:
        raise ValueError("A real, passing Unity playback proof is required.")
    manifest = prepared_dir / "clip.json"
    if hashlib.sha256(manifest.read_bytes()).hexdigest() != evidence["clipSha256"].lower():
        raise ValueError("The clip changed after Unity captured it. Capture the current revision again.")
    clip = load_record(manifest)
    for frame, recorded in zip(clip["frames"], evidence["frames"], strict=True):
        png = prepared_dir / frame["file"]
        if hashlib.sha256(png.read_bytes()).hexdigest() != recorded["sha256"].lower():
            raise ValueError(f"The sprite {png.name} changed after Unity captured it.")
    captures = [c for c in evidence["captures"] if c["label"].startswith("frame-")]
    if len(captures) != len(clip["frames"]) or [c["frame"] for c in captures] != list(range(len(captures))):
        raise ValueError("The captures do not show one complete, ordered loop.")
    offsets = annotated_offsets(clip, prepared_dir)
    result = {"method": "Source support annotations projected through the Unity anchor at each frame change",
              "720": review(1, captures, clip, offsets, output),
              "1080": review(1.5, captures, clip, offsets, output)}
    result["supportDuringTicks"] = {
        "720": support_over_time(evidence, captures, clip, offsets, 1),
        "1080": support_over_time(evidence, captures, clip, offsets, 1.5),
    }
    playback(captures, clip["cadenceMs"], output)
    (output / "support-review.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
