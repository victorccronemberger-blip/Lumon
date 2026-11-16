"""Grounding proof for the SouthWest walk: re-plant the support leg of the six v1 frames.

Reads walk/southwest (v1 frames + clip.json, never written) and writes only
walk/southwest-grounding: frame-NN.png, clip.json, preview-*.png, preview-travel.gif.

Only the support leg/boot of each frame is touched. It is isolated with a per-frame
polygon (intersected with the painted alpha), erased from the v1 frame and pasted back
through a localized warp: zero displacement above HIP_Y, a ramp through thigh and shin,
and a rigid translation of the boot below the cuff. Torso, head, arms, hips and the
swing leg stay pixel-identical to v1 (asserted). One fixed pivot for every frame.

    python -B Art/Guardian2D/ground_walk_southwest.py
"""
from pathlib import Path
import hashlib
import json
import math

import cv2
import numpy as np
from PIL import Image, ImageDraw

BASE = Path(__file__).resolve().parent
V1 = BASE / "walk" / "southwest"
OUT = BASE / "walk" / "southwest-grounding"

SCALE = 165.0 / 442.0                       # source px -> 720p screen px
SLOT_SCREEN = (-14.19, 7.09)                # root motion per slot at 720p (1.3709 m / 6 slots)
STEP = (-SLOT_SCREEN[0] / SCALE, -SLOT_SCREEN[1] / SCALE)   # planted sole vs pivot, source px (+38.0, -19.0)
PAD = 32                                    # equal padding on every side of the v1 canvas
HIP_Y = 238                                 # above this row nothing moves (belt / hip roots)
SEAM = 2                                    # px of v1 kept under the warped layer along interior cuts
CONTACT_FRACTION = 0.0                      # contact = heel corner (the point that stays put from heel strike to flat foot)

# Support leg per output frame (planted 3 + 3 by trajectory) and its isolation polygon in
# v1 canvas px. The support boot is always the front layer. CUFF = first rigid boot row.
SUPPORT = ["left", "left", "left", "right", "right", "right"]
STANCES = {"left": [0, 1, 2], "right": [3, 4, 5]}
POLY = {
    0: [(96, 236), (150, 236), (141, 280), (133, 318), (128, 350), (112, 380), (103, 420),
        (103, 448), (14, 448), (14, 384), (66, 352), (70, 300)],
    1: [(95, 236), (156, 236), (150, 290), (142, 320), (137, 360), (125, 384), (122, 450),
        (44, 450), (44, 410), (84, 392), (86, 345), (70, 300)],
    2: [(95, 236), (150, 236), (151, 290), (151, 306), (147, 340), (149, 370), (157, 402), (160, 455),
        (80, 455), (78, 400), (86, 352), (62, 322), (64, 280)],
    3: [(95, 236), (133, 236), (128, 270), (115, 310), (106, 350), (99, 372), (90, 440),
        (4, 440), (4, 380), (50, 338), (52, 300), (74, 256)],
    4: [(98, 236), (155, 236), (150, 280), (132, 315), (126, 345), (124, 372), (109, 386),
        (109, 452), (30, 452), (30, 420), (72, 392), (68, 340), (72, 300), (85, 270)],
    5: [(100, 236), (160, 236), (155, 290), (150, 310), (149, 345), (150, 370), (157, 404), (160, 455),
        (80, 455), (85, 420), (106, 395), (104, 356), (84, 320), (84, 280)],
}
CUFF = {0: 356, 1: 362, 2: 366, 3: 350, 4: 364, 5: 366}


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def load_v1():
    clip = json.loads((V1 / "clip.json").read_text("utf-8"))
    frames = [np.array(Image.open(V1 / f["file"]).convert("RGBA")) for f in clip["frames"]]
    return clip, frames


def poly_mask(shape, pts):
    m = np.zeros(shape[:2], np.uint8)
    cv2.fillPoly(m, [np.array(pts, np.int32)], 1)
    return m.astype(bool)


def contact_of(alpha, boot):
    """Heel corner (bottom-right extreme), toe tip (front-most low pixel) and contact point."""
    ys, xs = np.nonzero(boot & (alpha >= 128))
    h = int(np.argmax(ys + 0.5 * xs))
    heel = (float(xs[h]), float(ys[h]) + 0.5)
    low = ys >= heel[1] - 50
    xmin = xs[low].min()
    sel = low & (xs <= xmin + 1)
    toe = (float(xs[sel].mean()), float(ys[sel].max()) + 0.5)
    c = (heel[0] + CONTACT_FRACTION * (toe[0] - heel[0]), heel[1] + CONTACT_FRACTION * (toe[1] - heel[1]))
    return heel, toe, c


def ramp(y, cuff):
    u = np.clip((y - HIP_Y) / float(cuff - HIP_Y), 0, 1)
    return 0.7 * u + 0.3 * u * u * (3 - 2 * u)


def warp_layer(rgba, mask, d, cuff):
    """Backward remap of the masked layer: row y moves by ramp(y)*d (premultiplied)."""
    h, w = mask.shape
    a = rgba[:, :, 3].astype(np.float32) / 255 * mask
    pre = np.dstack([rgba[:, :, :3].astype(np.float32) * a[:, :, None], a * 255])
    grid = np.arange(h * 8) / 8.0             # exact integer rows -> identity where ramp = 0
    fwd = grid + ramp(grid, cuff) * d[1]
    ys = np.interp(np.arange(h, dtype=np.float64), fwd, grid)   # source row for each dest row
    xs = np.arange(w)[None, :] - (ramp(ys, cuff) * d[0])[:, None]
    mx = xs.astype(np.float32)
    my = np.repeat(ys[:, None], w, 1).astype(np.float32)
    out = cv2.remap(pre, mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=0)
    mout = cv2.remap(mask.astype(np.float32), mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
    return out, mout


def over(base, layer):
    """Premultiplied layer over straight-alpha base -> straight uint8 RGBA."""
    ba = base[:, :, 3:4].astype(np.float32) / 255
    la = layer[:, :, 3:4] / 255
    rgb = layer[:, :, :3] + base[:, :, :3].astype(np.float32) * ba * (1 - la)
    a = la + ba * (1 - la)
    out = np.zeros(base.shape, np.float32)
    out[:, :, :3] = np.where(a > 1e-4, rgb / np.maximum(a, 1e-4), 0)
    out[:, :, 3:4] = a * 255
    out = np.clip(np.rint(out), 0, 255).astype(np.uint8)
    keep = layer[:, :, 3] <= 0
    out[keep] = base[keep]          # exact v1 pixels wherever the leg layer is absent
    return out


def solve_offsets(points):
    """d_k = c + k*STEP - P_k with c the per-axis midrange (minimises max |d|); common ground y."""
    raw = {foot: [np.array(points[k]) - np.array(STEP) * j for j, k in enumerate(ks)]
           for foot, ks in STANCES.items()}
    cx = {f: (max(r[0] for r in v) + min(r[0] for r in v)) / 2 for f, v in raw.items()}
    ally = [r[1] for v in raw.values() for r in v]
    cy = (max(ally) + min(ally)) / 2
    d, target = {}, {}
    for foot, ks in STANCES.items():
        for j, k in enumerate(ks):
            target[k] = (cx[foot] + STEP[0] * j, cy + STEP[1] * j)
            d[k] = (target[k][0] - points[k][0], target[k][1] - points[k][1])
    return d, target, {"cx": cx, "cy": cy}


def match_boot(prev, nxt, box, search=70):
    """Independent cross-check: SQDIFF match of the support boot patch of output k in k+1."""
    x0, y0, x1, y1 = box
    tpl = prev[y0:y1, x0:x1]
    tm = (tpl[:, :, 3] > 128).astype(np.float32)
    tg = cv2.cvtColor(tpl[:, :, :3], cv2.COLOR_RGB2GRAY).astype(np.float32) * tm
    g = cv2.cvtColor(nxt[:, :, :3], cv2.COLOR_RGB2GRAY).astype(np.float32)
    g[nxt[:, :, 3] < 128] = 230
    res = cv2.matchTemplate(g, tg, cv2.TM_SQDIFF, mask=tm)
    yy, xx = np.mgrid[0:res.shape[0], 0:res.shape[1]]
    res[(abs(yy - y0) > search) | (abs(xx - x0) > search)] = np.inf
    y, x = np.unravel_index(int(np.argmin(res)), res.shape)
    return int(x - x0), int(y - y0), round(float(math.sqrt(res[y, x] / tm.sum())), 1)


def resized(rgba, scale):
    img = Image.fromarray(rgba).convert("RGBa")
    return img.resize((max(1, round(img.width * scale)), max(1, round(img.height * scale))),
                      Image.LANCZOS).convert("RGBA")


def main():
    v1_hashes = {p.name: sha(p) for p in V1.iterdir() if p.is_file()}
    clip1, frames = load_v1()
    H, W = frames[0].shape[:2]
    pivot_x_v1 = clip1["frames"][0]["pivotX"] * W

    masks, boots, v1_contacts = [], [], []
    for k, f in enumerate(frames):
        m = poly_mask(f.shape, POLY[k]) & (f[:, :, 3] > 0) & (np.arange(H)[:, None] >= HIP_Y)
        rows = np.arange(H)[:, None]
        masks.append(m)
        boots.append(m & (rows >= CUFF[k]))
        v1_contacts.append(contact_of(f[:, :, 3], boots[k]))
    points = [c[2] for c in v1_contacts]
    d, target, consts = solve_offsets(points)

    # ---- warp + composite ----
    outs, warped_boots, holes = [], [], []
    for k, f in enumerate(frames):
        # Erase the support leg from the base, but keep a SEAM px band along the cuts that run
        # through the painting (polygon edge inside the figure) so the warped layer overlaps it
        # instead of leaving a see-through line.
        poly = poly_mask(f.shape, POLY[k]) & (np.arange(H)[:, None] >= HIP_Y)   # nothing moves above the hip line
        inner = cv2.erode(poly.astype(np.uint8), np.ones((2 * SEAM + 1, 2 * SEAM + 1), np.uint8)).astype(bool)
        others = (f[:, :, 3] > 0) & ~poly
        near_others = cv2.dilate(others.astype(np.uint8), np.ones((2 * SEAM + 3,) * 2, np.uint8)).astype(bool)
        inner |= poly & ~near_others          # band survives only where it borders the rest of the figure
        base = f.copy()
        base[inner | (masks[k] & (np.arange(H)[:, None] >= CUFF[k] - 4)), 3] = 0
        layer, mout = warp_layer(f, masks[k], d[k], CUFF[k])
        res = over(base, layer)
        # The kept seam band is v1 support-leg paint: wherever the moved leg no longer covers it,
        # it would read as a second contour -> remove it.
        band = poly & ~inner & (base[:, :, 3] > 0)
        exposed = band & (layer[:, :, 3] < 8)
        res[exposed, 3] = 0
        # ...but where it sat against the other leg, a transparent crack would open between the
        # legs: extend the neighbouring (other-leg) paint over those 1-3 px instead.
        other_paint = (base[:, :, 3] >= 128) & ~poly
        crack = exposed & cv2.dilate(other_paint.astype(np.uint8), np.ones((7, 7), np.uint8)).astype(bool)
        if crack.any():
            src = res[:, :, :3].copy()
            rgb = cv2.inpaint(np.ascontiguousarray(src), crack.astype(np.uint8), 2, cv2.INPAINT_TELEA)
            res[crack, :3] = rgb[crack]
            res[crack, 3] = f[crack, 3]
        # Holes: support pixels that were opaque and are no longer covered.
        hole = masks[k] & (f[:, :, 3] >= 128) & (res[:, :, 3] < 128)
        n, lab = cv2.connectedComponents(hole.astype(np.uint8), connectivity=8)
        open_bg = (res[:, :, 3] < 128) & ~hole
        fill = np.zeros_like(hole)
        for c in range(1, n):
            comp = lab == c
            ring = cv2.dilate(comp.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool) & ~comp
            # enclosed gap (revealed behind the moved leg) -> inpaint from its surroundings;
            # gap open to the background -> the leg simply left that place: stays transparent
            if not (ring & open_bg).any():
                fill |= comp
        if fill.any():
            rgb = cv2.inpaint(np.ascontiguousarray(res[:, :, :3]), fill.astype(np.uint8), 3, cv2.INPAINT_TELEA)
            res[fill, :3] = rgb[fill]
            res[fill, 3] = 255
        # assert 1: rows above HIP_Y (head, torso, arms, belt, hip roots) are exactly v1
        if not np.array_equal(res[:HIP_Y], f[:HIP_Y]):
            raise SystemExit(f"frame {k}: pixels above the hip line changed")
        # assert 2: outside the polygon nothing changed except where the moved leg now overlaps
        # (counted and reported: hand / back-leg paint covered by the support leg)
        others = (f[:, :, 3] > 0) & ~poly
        changed = others & (res != f).any(2)
        if (changed & (layer[:, :, 3] <= 0) & ~fill & ~crack).any():
            raise SystemExit(f"frame {k}: pixels outside the support leg changed without being overlapped")
        holes.append({"uncoveredPx": int(hole.sum()), "inpaintedPx": int(fill.sum()),
                      "seamBandRemovedPx": int(exposed.sum()), "seamCrackExtendedPx": int(crack.sum()),
                      "otherPaintOverlappedByMovedLegPx": int(changed.sum()),
                      "otherPaintOverlappedAboveCuffPx": int((changed & (np.arange(H)[:, None] < CUFF[k])).sum())})
        outs.append(res)
        warped_boots.append(cv2.remap(boots[k].astype(np.float32),
                                      (np.arange(W)[None, :] - d[k][0]).astype(np.float32).repeat(H, 0),
                                      (np.arange(H)[:, None] - d[k][1]).astype(np.float32).repeat(W, 1),
                                      cv2.INTER_NEAREST) > 0.5)

    # ---- pad equally ----
    Hp, Wp = H + 2 * PAD, W + 2 * PAD
    padded = []
    for r in outs:
        p = np.zeros((Hp, Wp, 4), np.uint8)
        p[PAD:PAD + H, PAD:PAD + W] = r
        a = p[:, :, 3] > 0
        if a[0].any() or a[-1].any() or a[:, 0].any() or a[:, -1].any():
            raise SystemExit("opaque pixel touches the padded border")
        padded.append(p)
    wboots = [np.pad(b, PAD) for b in warped_boots]

    # ---- re-measure on output pixels ----
    out_contacts = [contact_of(p[:, :, 3], b) for p, b in zip(padded, wboots)]
    cy_pivot = consts["cy"] + STEP[1] * 1 + PAD          # ground row under the root at mid-stance
    pivot = (round((pivot_x_v1 + PAD) / Wp, 6), round((Hp - cy_pivot) / Hp, 6))

    checks = []
    for a, b in zip(range(6), [1, 2, 3, 4, 5, 0]):
        ca, cb = out_contacts[a][2], out_contacts[b][2]
        foot = SUPPORT[a]
        same_planted = SUPPORT[b] == foot and b != 0
        ys, xs = np.nonzero(wboots[a])
        box = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)
        checks.append({
            "pair": f"{a:02d}->{b:02d}", "foot": foot,
            "kind": "planted" if same_planted else "handoff (support changes, not planted)",
            "contactStepPx": [round(cb[0] - ca[0], 2), round(cb[1] - ca[1], 2)] if same_planted else None,
            "contactStepScreen720": [round((cb[0] - ca[0]) * SCALE, 2), round((cb[1] - ca[1]) * SCALE, 2)]
            if same_planted else None,
            "residualScreen720": [round((cb[0] - ca[0] - STEP[0]) * SCALE, 2), round((cb[1] - ca[1] - STEP[1]) * SCALE, 2)]
            if same_planted else None,
            # rigid-template cross-check; unreliable here (boot rotates heel->flat), kept with its RMS
            "bootTemplateDxDyRms": list(match_boot(padded[a], padded[b], box)) if same_planted else None,
        })
        if not same_planted:
            # Leaving foot: its toe in a (support, warped) vs its toe in b (trailing boot, v1 pixels).
            trail = (padded[b][:, :, 3] > 0) & ~np.pad(poly_mask(frames[b].shape, POLY[b]), PAD) \
                & (np.arange(Hp)[:, None] >= 380 + PAD)
            n, lab, st, _ = cv2.connectedComponentsWithStats(trail.astype(np.uint8))
            big = lab == 1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA]))
            ta = out_contacts[a][1]
            tb = contact_of(padded[b][:, :, 3], big)[1]
            checks[-1]["leavingToeStepPx"] = [round(tb[0] - ta[0], 2), round(tb[1] - ta[1], 2)]
            checks[-1]["leavingToeResidualScreen720"] = [round((tb[0] - ta[0] - STEP[0]) * SCALE, 2),
                                                        round((tb[1] - ta[1] - STEP[1]) * SCALE, 2)]

    OUT.mkdir(parents=True, exist_ok=True)
    entries = []
    for k, p in enumerate(padded):
        name = f"frame-{k:02d}.png"
        Image.fromarray(p).save(OUT / name)
        heel, toe, c = out_contacts[k]
        entries.append({"file": name, "pivotX": pivot[0], "pivotY": pivot[1], "supportFoot": SUPPORT[k],
                        "contactX": round(c[0] / Wp, 6), "contactY": round((Hp - c[1]) / Hp, 6)})

    # Same fields (and order) as the v1 clip; frames[] gains contactX/contactY; grounding
    # provenance lives in measurements.grounding.
    conventions = dict(clip1["conventions"])
    conventions.update({
        "pivot": "normalized, origin bottom-left, identical for every frame; pivotX = v1 trunk anchor; "
                 "pivotY = ground row under the root at mid-stance (k = 1 of each 3-frame stance)",
        "supportFoot": "foot planted by trajectory, 3 + 3 (left = near leg); frame-03 is right (v1 said left)",
        "contact": "contactX/contactY normalized, origin bottom-left of this canvas; support-boot material point = "
                   f"heel corner + {CONTACT_FRACTION}*(toe tip - heel corner), re-measured on the output alpha",
    })
    measurements = dict(clip1["measurements"])
    measurements["contactFrames"] = [0, 3]
    measurements["grounding"] = {
        "v1Clip": "walk/southwest/clip.json",
        "v1FrameSha256": {e["file"]: v1_hashes[e["file"]] for e in clip1["frames"]},
        "script": "ground_walk_southwest.py",
        "canvasPx": [Wp, Hp], "padFromV1Px": PAD, "hipRowV1": HIP_Y,
        "plantedStepTargetPx": [round(STEP[0], 2), round(STEP[1], 2)],
        "plantedStepTargetScreen720": [-SLOT_SCREEN[0], -SLOT_SCREEN[1]],
        "appliedBootOffsetPx": {f"{k:02d}": [round(v, 2) for v in d[k]] for k in range(6)},
        "v1ContactPx": {f"{k:02d}": [round(v, 2) for v in points[k]] for k in range(6)},
        "pairs": checks, "pixels": holes,
    }
    predictions = dict(clip1["predictions"])
    planted = [c for c in checks if c["kind"] == "planted"]
    predictions["plantedFootHorizontalDriftPx720"] = round(max(abs(c["residualScreen720"][0]) for c in planted), 2)
    predictions["plantedFootVerticalSlidePerStepPx720"] = round(max(abs(c["residualScreen720"][1]) for c in planted), 2)
    predictions["handoffLeavingToeResidualPx720Estimate"] = {c["pair"]: c["leavingToeResidualScreen720"]
                                                            for c in checks if c["kind"] != "planted"}
    clip = {
        "direction": clip1["direction"],
        "referenceHeightPx": clip1["referenceHeightPx"],
        "cadenceMs": clip1["cadenceMs"],
        "suggestedDistanceMeters": clip1["suggestedDistanceMeters"],
        "frames": entries,
        "status": "proof-not-accepted",
        "sourceFrames": clip1["sourceFrames"],
        "source": clip1["source"],
        "conventions": conventions,
        "measurements": measurements,
        "predictions": predictions,
    }
    if list(clip) != list(clip1) or any(set(e) - {"contactX", "contactY"} != set(f) for e, f in
                                        zip(entries, clip1["frames"])):
        raise SystemExit("clip fields differ from v1")
    (OUT / "clip.json").write_text(json.dumps(clip, indent=2) + "\n", encoding="utf-8")
    previews(padded, frames, clip, out_contacts, pivot, masks, d)

    if {p.name: sha(p) for p in V1.iterdir() if p.is_file()} != v1_hashes:
        raise SystemExit("v1 changed")
    print(json.dumps({"pivot": pivot, "d": {k: [round(v, 1) for v in d[k]] for k in d}}, indent=None))
    for c in checks:
        print(c)
    print(holes)


def previews(padded, frames, clip, contacts, pivot, masks, d):
    Hp, Wp = padded[0].shape[:2]
    H, W = frames[0].shape[:2]
    floor, line = (110, 112, 107, 255), (60, 62, 58, 255)
    # 1. 720p strip: v1 (its own per-frame pivots) above, grounded (fixed pivot) below.
    cell, rowh = 92, 230
    strip = Image.new("RGBA", (cell * 6 + 16, rowh * 2 + 10), floor)
    dr = ImageDraw.Draw(strip)
    v1 = json.loads((V1 / "clip.json").read_text("utf-8"))
    for row in range(2):
        ground = rowh * row + 200
        dr.line([(0, ground), (strip.width, ground)], fill=line)
        for k in range(6):
            cx = cell * k + cell // 2 + 8
            if row == 0:
                spr, pv = resized(frames[k], SCALE), (v1["frames"][k]["pivotX"], v1["frames"][k]["pivotY"])
            else:
                spr, pv = resized(padded[k], SCALE), pivot
            x = round(cx - pv[0] * spr.width)
            y = round(ground - (1 - pv[1]) * spr.height)
            strip.alpha_composite(spr, (x, y))
            dr.line([(cx, ground - 2), (cx, ground + 2)], fill=(200, 30, 30, 255))
            if row == 1:
                c = contacts[k][2]
                px, py = x + c[0] * SCALE, y + c[1] * SCALE
                dr.ellipse([px - 2, py - 2, px + 2, py + 2], outline=(40, 220, 240, 255))
            dr.text((cx - 30, ground + 8), f"{k:02d} {clip['frames'][k]['supportFoot'][0].upper()}"
                    + (" v1" if row == 0 else ""), fill=(0, 0, 0, 255))
    strip.save(OUT / "preview-720p-strip.png")

    # 2. 3x leg crops: v1 vs grounded with support-mask outline.
    S, y0 = 3, 200 + PAD
    crops = Image.new("RGBA", (Wp * S * 6 // 2, (Hp - y0) * S * 2 // 2 * 2), (205, 205, 205, 255))
    tile_w, tile_h = Wp * S // 2, (Hp - y0) * S // 2
    crops = Image.new("RGBA", (tile_w * 6, tile_h * 2), (205, 205, 205, 255))
    for k in range(6):
        v1p = np.zeros_like(padded[k]); v1p[PAD:PAD + H, PAD:PAD + W] = frames[k]
        for row, img in enumerate((v1p, padded[k])):
            t = Image.fromarray(img[y0:]).resize((tile_w, tile_h), Image.LANCZOS)
            bg = Image.new("RGBA", t.size, (205, 205, 205, 255)); bg.alpha_composite(t)
            g = ImageDraw.Draw(bg)
            if row == 0:
                pts = [((x + PAD) * S / 2, (y + PAD - y0) * S / 2) for x, y in POLY[k]]
                g.line(pts + [pts[0]], fill=(255, 0, 255, 255), width=1)
            g.text((4, 4), f"{k:02d} {'v1 + mask' if row == 0 else 'grounded d=' + str([round(v) for v in d[k]])}",
                   fill=(0, 0, 0, 255))
            crops.alpha_composite(bg, (k * tile_w, row * tile_h))
    crops.save(OUT / "preview-legs-1.5x.png")

    # 3. Travel at 720p: root steps SLOT_SCREEN per slot; red = where support contact landed
    # at the first frame of its stance, cyan = current support contact.
    sprites = [resized(p, SCALE) for p in padded]
    width, height = 420, 400
    start = (350.0, 200.0)
    imgs, landed = [], None
    for n in range(18):
        k = n % 6
        root = (start[0] + SLOT_SCREEN[0] * n, start[1] + SLOT_SCREEN[1] * n)
        img = Image.new("RGBA", (width, height), floor)
        g = ImageDraw.Draw(img)
        g.line([start, (start[0] + SLOT_SCREEN[0] * 30, start[1] + SLOT_SCREEN[1] * 30)], fill=line)
        spr = sprites[k]
        x = round(root[0] - pivot[0] * spr.width)
        y = round(root[1] - (1 - pivot[1]) * spr.height)
        img.alpha_composite(spr, (x, y))
        c = contacts[k][2]
        foot = (x + c[0] * SCALE, y + c[1] * SCALE)
        if k in (0, 3):
            landed = foot
        g.ellipse([landed[0] - 3, landed[1] - 3, landed[0] + 3, landed[1] + 3], outline=(230, 40, 40, 255))
        g.ellipse([foot[0] - 1, foot[1] - 1, foot[0] + 1, foot[1] + 1], fill=(40, 200, 230, 255))
        g.text((6, 6), f"frame {k:02d} {clip['frames'][k]['supportFoot']}", fill=(0, 0, 0, 255))
        imgs.append(img.convert("RGB"))
    imgs[0].save(OUT / "preview-720p-travel.gif", save_all=True, append_images=imgs[1:],
                 duration=int(clip["cadenceMs"]), loop=0, optimize=False)
    # Onion of one stride (frames 0..5 at their travelled positions) as a still.
    onion = Image.new("RGBA", (width, height), floor)
    g = ImageDraw.Draw(onion)
    for n in range(6):
        root = (start[0] + SLOT_SCREEN[0] * n, start[1] + SLOT_SCREEN[1] * n)
        spr = sprites[n].copy()
        spr.putalpha(spr.getchannel("A").point(lambda v: v * 45 // 100))
        x = round(root[0] - pivot[0] * spr.width)
        y = round(root[1] - (1 - pivot[1]) * spr.height)
        onion.alpha_composite(spr, (x, y))
    for n in range(6):
        root = (start[0] + SLOT_SCREEN[0] * n, start[1] + SLOT_SCREEN[1] * n)
        x = round(root[0] - pivot[0] * sprites[n].width)
        y = round(root[1] - (1 - pivot[1]) * sprites[n].height)
        c = contacts[n][2]
        col = (230, 40, 40, 255) if SUPPORT[n] == "left" else (40, 120, 240, 255)
        fx, fy = x + c[0] * SCALE, y + c[1] * SCALE
        g.ellipse([fx - 2, fy - 2, fx + 2, fy + 2], outline=col)
        g.text((fx + 4, fy - 4), str(n), fill=col)
    g.text((6, 6), "one cycle at 720p, contact dots: red=left, blue=right", fill=(0, 0, 0, 255))
    onion.save(OUT / "preview-720p-onion.png")
    # 4. Planted check: each frame of a stance drawn at its travelled root, cropped around the
    # world spot where that stance landed (crosshair). A planted boot stays under the crosshair.
    Z, R = 5, 22
    check = Image.new("RGBA", (6 * 2 * R * Z, 2 * R * Z + 18), floor)
    g = ImageDraw.Draw(check)
    land = {}
    for n in range(6):
        root = (start[0] + SLOT_SCREEN[0] * n, start[1] + SLOT_SCREEN[1] * n)
        world = Image.new("RGBA", (width, height), floor)
        x = round(root[0] - pivot[0] * sprites[n].width)
        y = round(root[1] - (1 - pivot[1]) * sprites[n].height)
        world.alpha_composite(sprites[n], (x, y))
        c = contacts[n][2]
        spot = (x + c[0] * SCALE, y + c[1] * SCALE)
        land.setdefault(SUPPORT[n], spot)
        lx, ly = land[SUPPORT[n]]
        tile = world.crop((round(lx) - R, round(ly) - R, round(lx) + R, round(ly) + R)).resize(
            (2 * R * Z, 2 * R * Z), Image.NEAREST)
        tg = ImageDraw.Draw(tile)
        cx = (lx - round(lx) + R) * Z
        cy = (ly - round(ly) + R) * Z
        tg.line([(cx - 14, cy), (cx + 14, cy)], fill=(40, 230, 240, 255), width=2)
        tg.line([(cx, cy - 14), (cx, cy + 14)], fill=(40, 230, 240, 255), width=2)
        check.alpha_composite(tile, (n * 2 * R * Z, 18))
        g.text((n * 2 * R * Z + 4, 3), f"{n:02d} {SUPPORT[n]} (cyan + = landing spot)",
               fill=(0, 0, 0, 255))
    check.save(OUT / "preview-720p-planted-check.png")
    onion.crop((150, 20, 400, 290)).resize((750, 810), Image.LANCZOS).save(OUT / "preview-720p-onion-3x.png")


if __name__ == "__main__":
    main()
