"""SouthWest walk, 18-pose subframe proof: 3 subposes per grounded phase, support leg re-planted.

Reads walk/southwest (v1 frames + clip.json) and walk/southwest-grounding (clip.json + PNGs, for
pivot and regression only). Writes ONLY walk/southwest-subframes. Never calls the grounding
script's main()/previews() (they write to southwest-grounding); only its pure helpers and
constants are imported, with bytecode writing disabled.

For base frame k (0..5) and subpose j (0..2) the support leg of the v1 frame is isolated with the
grounding polygon (POLY & painted alpha) and re-warped with

    D(y) = ramp(y) * d_k  +  knee(y) * (j - 1) * STEP / 3

d_k is the grounding boot offset (so j = 1 reproduces the grounding frame), ramp() is the
grounding hip->cuff ramp, knee() is an ease-out from the knee row to the cuff (slope is highest at
the knee, zero at the cuff). Below the cuff the boot is a rigid translation. Per subpose the planted
contact therefore recedes STEP/3 = (+12.67, -6.33) source px = (+4.73, -2.36) px at 720p relative to
the trunk, uniformly over all 9 subposes of a stance. Torso, head, arms, hips and swing leg keep
the v1 pixels (asserted). One fixed pivot, equal to grounding.

    python -B Art/Guardian2D/ground_walk_southwest_subframes.py
"""
import sys

sys.dont_write_bytecode = True

from pathlib import Path
import hashlib
import json

import cv2
import numpy as np
from PIL import Image, ImageDraw

BASE = Path(__file__).resolve().parent
sys.path.insert(0, str(BASE))
import ground_walk_southwest as G  # noqa: E402  (helpers + constants only)

V1 = BASE / "walk" / "southwest"
GROUND = BASE / "walk" / "southwest-grounding"
OUT = BASE / "walk" / "southwest-subframes"

SUB = 3
N = 6 * SUB
CYCLE_MS = 880.0
PAD = G.PAD
SCALE = G.SCALE
STEP = np.array(G.STEP)                       # planted sole vs pivot per slot, source px
DELTA_EXACT = STEP / SUB                      # per subpose, source px (+12.67, -6.33)
# Snapped to integer source px so subposes 0 and 2 keep the same sub-pixel phase as subpose 1
# (= grounding): with the fractional delta the rigid boot came out visibly softer on 0/2 than on 1.
DELTA = np.round(DELTA_EXACT)                 # (+13, -6)
SLOT_SUB = (G.SLOT_SCREEN[0] / SUB, G.SLOT_SCREEN[1] / SUB)   # root motion per subpose at 720p
KNEE_FRACTION = 0.45                          # knee row = hip + 0.45 * (cuff - hip)
SCALE_1080 = SCALE * 1.5


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def dir_hashes(d):
    return {p.name: sha(p) for p in sorted(d.iterdir()) if p.is_file()}


def ramp(y, hip, cuff):
    u = np.clip((y - hip) / float(cuff - hip), 0, 1)
    return 0.7 * u + 0.3 * u * u * (3 - 2 * u)


def knee_ramp(y, knee, cuff):
    u = np.clip((y - knee) / float(cuff - knee), 0, 1)
    return 1 - (1 - u) ** 2


def warp_layer(rgba, mask, d, e, hip, knee, cuff):
    """Backward remap of the masked layer, row y moves by ramp(y)*d + knee(y)*e (premultiplied)."""
    h, w = mask.shape
    a = rgba[:, :, 3].astype(np.float32) / 255 * mask
    pre = np.dstack([rgba[:, :, :3].astype(np.float32) * a[:, :, None], a * 255])
    disp = lambda y, i: ramp(y, hip, cuff) * d[i] + knee_ramp(y, knee, cuff) * e[i]
    grid = np.linspace(0, h - 1, h * 8)
    fwd = grid + disp(grid, 1)
    if not np.all(np.diff(fwd) > 0):
        raise SystemExit("row map folds (fwd not strictly increasing)")
    ys = np.interp(np.arange(h, dtype=np.float64), fwd, grid)
    xs = np.arange(w)[None, :] - disp(ys, 0)[:, None]
    mx = xs.astype(np.float32)
    my = np.repeat(ys[:, None], w, 1).astype(np.float32)
    out = cv2.remap(pre, mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=0)
    return out


def compose(f, poly, mask, d, e, hip, knee, cuff):
    """Same compositing as the grounding proof (seam band, erase, warp, enclosed-hole inpaint)."""
    H = f.shape[0]
    SEAM = G.SEAM
    inner = cv2.erode(poly.astype(np.uint8), np.ones((2 * SEAM + 1, 2 * SEAM + 1), np.uint8)).astype(bool)
    others = (f[:, :, 3] > 0) & ~poly
    near = cv2.dilate(others.astype(np.uint8), np.ones((2 * SEAM + 3,) * 2, np.uint8)).astype(bool)
    inner |= poly & ~near
    base = f.copy()
    base[inner | (mask & (np.arange(H)[:, None] >= cuff - 4)), 3] = 0
    layer = warp_layer(f, mask, d, e, hip, knee, cuff)
    res = G.over(base, layer)
    hole = mask & (f[:, :, 3] >= 128) & (res[:, :, 3] < 128)
    n, lab = cv2.connectedComponents(hole.astype(np.uint8), connectivity=8)
    open_bg = (res[:, :, 3] < 128) & ~hole
    fill = np.zeros_like(hole)
    for c in range(1, n):
        comp = lab == c
        ring = cv2.dilate(comp.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool) & ~comp
        if not (ring & open_bg).any():
            fill |= comp
    if fill.any():
        rgb = cv2.inpaint(np.ascontiguousarray(res[:, :, :3]), fill.astype(np.uint8), 3, cv2.INPAINT_TELEA)
        res[fill, :3] = rgb[fill]
        res[fill, 3] = 255
    untouched = ~mask & ~fill & (layer[:, :, 3] <= 0)
    if not np.array_equal(res[untouched], f[untouched]):
        raise SystemExit("pixels outside the support leg changed")
    return res, {"uncoveredPx": int(hole.sum()), "inpaintedPx": int(fill.sum()),
                 "leftTransparentPx": int(hole.sum() - fill.sum())}


def shift_mask(m, t):
    H, W = m.shape
    return cv2.remap(m.astype(np.float32),
                     (np.arange(W)[None, :] - t[0]).astype(np.float32).repeat(H, 0),
                     (np.arange(H)[:, None] - t[1]).astype(np.float32).repeat(W, 1),
                     cv2.INTER_NEAREST) > 0.5


def sharpness(rgba, m):
    g = cv2.cvtColor(rgba[:, :, :3], cv2.COLOR_RGB2GRAY).astype(np.float32)
    lap = cv2.Laplacian(g, cv2.CV_32F)
    core = cv2.erode(m.astype(np.uint8), np.ones((5, 5), np.uint8)).astype(bool)
    return round(float(lap[core].var()), 1)


def main():
    v1_before, gr_before = dir_hashes(V1), dir_hashes(GROUND)
    clip1, frames = G.load_v1()
    gclip = json.loads((GROUND / "clip.json").read_text("utf-8"))
    H, W = frames[0].shape[:2]
    Hp, Wp = H + 2 * PAD, W + 2 * PAD

    # Grounding offsets, recomputed exactly as the grounding script does (unpadded v1 canvas).
    masks0, points = [], []
    for k, f in enumerate(frames):
        m = G.poly_mask(f.shape, G.POLY[k]) & (f[:, :, 3] > 0)
        masks0.append(m)
        points.append(G.contact_of(f[:, :, 3], m & (np.arange(H)[:, None] >= G.CUFF[k]))[2])
    d, target, consts = G.solve_offsets(points)
    gd = gclip["measurements"]["grounding"]["appliedBootOffsetPx"]
    for k in range(6):
        if max(abs(d[k][i] - gd[f"{k:02d}"][i]) for i in range(2)) > 0.01:
            raise SystemExit(f"grounding offset mismatch on {k}")

    pivot_x_v1 = clip1["frames"][0]["pivotX"] * W
    pivot = (round((pivot_x_v1 + PAD) / Wp, 6), round((Hp - (consts["cy"] + STEP[1] + PAD)) / Hp, 6))
    gpivot = (gclip["frames"][0]["pivotX"], gclip["frames"][0]["pivotY"])
    if pivot != gpivot:
        raise SystemExit(f"pivot {pivot} != grounding {gpivot}")

    hip = G.HIP_Y + PAD
    rows = np.arange(Hp)[:, None]
    outs, meta, wboots = [], [], []
    for k, f in enumerate(frames):
        fp = np.pad(f, ((PAD, PAD), (PAD, PAD), (0, 0)))
        poly = G.poly_mask(fp.shape, [(x + PAD, y + PAD) for x, y in G.POLY[k]])
        mask = poly & (fp[:, :, 3] > 0)
        cuff = G.CUFF[k] + PAD
        knee = hip + KNEE_FRACTION * (cuff - hip)
        boot = mask & (rows >= cuff)
        for j in range(SUB):
            e = (j - 1) * DELTA
            res, px = compose(fp, poly, mask, d[k], e, hip, knee, cuff)
            a = res[:, :, 3] > 0
            if a[0].any() or a[-1].any() or a[:, 0].any() or a[:, -1].any():
                raise SystemExit("opaque pixel touches the canvas border")
            top = min(y for _, y in G.POLY[k]) + PAD          # support polygon top (v1 row 236)
            if not np.array_equal(res[:top], fp[:top]):
                raise SystemExit("rows above the support polygon changed")
            px["changedPxAboveHip"] = int((np.abs(res[:hip].astype(int) - fp[:hip].astype(int)).max(2) > 0).sum())
            t = (d[k][0] + e[0], d[k][1] + e[1])
            wb = shift_mask(boot, t)
            # boot stays rigid: opaque count of the moved boot vs the v1 boot
            px["bootOpaqueV1"] = int((boot & (fp[:, :, 3] >= 128)).sum())
            px["bootOpaqueOut"] = int((wb & (res[:, :, 3] >= 128)).sum())
            px["bootSharpnessLapVar"] = sharpness(res, wb & (res[:, :, 3] >= 250))
            outs.append(res)
            wboots.append(wb)
            meta.append({"base": k, "sub": j, "offsetPx": [round(float(t[0]), 2), round(float(t[1]), 2)],
                         "kneeDeltaPx": [round(float(e[0]), 2), round(float(e[1]), 2)], **px})

    contacts = [G.contact_of(p[:, :, 3], b) for p, b in zip(outs, wboots)]

    # Regression: subpose 1 of each base should reproduce the grounding PNG.
    regress = {}
    for k in range(6):
        g = np.array(Image.open(GROUND / f"frame-{k:02d}.png").convert("RGBA")).astype(int)
        o = outs[k * SUB + 1].astype(int)
        diff = np.abs(g - o).max(axis=2)
        regress[f"{k:02d}"] = {"pxDifferent": int((diff > 0).sum()), "maxLevelDiff": int(diff.max())}

    support = [G.SUPPORT[i // SUB] for i in range(N)]
    pairs, swing_pop = [], []
    for a in range(N):
        b = (a + 1) % N
        ca, cb = contacts[a][2], contacts[b][2]
        planted = support[a] == support[b] and b != 0
        step = (cb[0] - ca[0], cb[1] - ca[1])
        changed = np.abs(outs[a].astype(int) - outs[b].astype(int)).max(axis=2) > 0
        pairs.append({
            "pair": f"{a:02d}->{b:02d}", "foot": support[a],
            "kind": "planted" if planted else "handoff (support changes)",
            "contactStepPx": [round(step[0], 2), round(step[1], 2)] if planted else None,
            "residualVsExactScreen720": [round(float(step[0] - DELTA_EXACT[0]) * SCALE, 2),
                                         round(float(step[1] - DELTA_EXACT[1]) * SCALE, 2)] if planted else None,
            # trunk/arms/hips (rows above the hip) and everything else that changed between the two poses
            "changedPxAboveHip": int(changed[:G.HIP_Y + PAD].sum()),
            "changedPxTotal": int(changed.sum()),
        })

    OUT.mkdir(parents=True, exist_ok=True)
    cad = round(CYCLE_MS / N, 2)
    entries = []
    for i, p in enumerate(outs):
        name = f"frame-{i:02d}.png"
        Image.fromarray(p).save(OUT / name)
        c = contacts[i][2]
        entries.append({"file": name, "pivotX": pivot[0], "pivotY": pivot[1], "supportFoot": support[i],
                        "contactX": round(c[0] / Wp, 6), "contactY": round((Hp - c[1]) / Hp, 6)})
    png_sha = {e["file"]: sha(OUT / e["file"]) for e in entries}

    # Stance drift in world (720p, root steps SLOT_SUB per subpose): distance of the support contact
    # from where that stance landed, for this proof and for grounding held 3 ticks each.
    def drift(cs):
        res, land = [], None
        for i in range(N):
            w = (SLOT_SUB[0] * i + (cs[i][0] - pivot[0] * Wp) * SCALE,
                 SLOT_SUB[1] * i + (cs[i][1] - (1 - pivot[1]) * Hp) * SCALE)
            if i % (3 * SUB) == 0:
                land = w
            res.append(round(float(np.hypot(w[0] - land[0], w[1] - land[1])), 2))
        return res
    gcont = [(f["contactX"] * Wp, (1 - f["contactY"]) * Hp) for f in gclip["frames"]]
    drift_sub = drift([c[2] for c in contacts])
    drift_gr = drift([gcont[i // SUB] for i in range(N)])

    clip = {
        "direction": "SouthWest",
        "referenceHeightPx": gclip["referenceHeightPx"],
        "cadenceMs": cad,
        "suggestedDistanceMeters": gclip["suggestedDistanceMeters"],
        "frames": entries,
        "status": "proof-not-accepted",
        "sourceFrames": [gclip["sourceFrames"][i // SUB] for i in range(N)],
        "source": {"v1Clip": "walk/southwest/clip.json", "groundingClip": "walk/southwest-grounding/clip.json",
                   "script": "ground_walk_southwest_subframes.py",
                   "v1FrameSha256": {e["file"]: v1_before[e["file"]] for e in clip1["frames"]},
                   "groundingSha256": gr_before},
        "conventions": {
            "pivot": "normalized, origin bottom-left, identical for every frame and equal to grounding",
            "contact": "normalized, origin bottom-left of this canvas; support-boot heel corner (opaque pixel "
                       "maximising y + 0.5*x in the moved boot), re-measured on the output alpha; contactX is "
                       "the left edge of that pixel column, contactY its bottom edge",
            "supportFoot": "foot planted by trajectory, 9 + 9 subposes (left = near leg)",
            "cadenceMs": "880 ms cycle / 18 subposes",
            "sourceFrames": "GIF frame of the v1 base painting used by each subpose",
        },
        "measurements": {
            "canvasPx": [Wp, Hp], "sourceToScreen720": round(SCALE, 5),
            "subposeStepExactPx": [round(float(DELTA_EXACT[0]), 2), round(float(DELTA_EXACT[1]), 2)],
            "subposeStepExactScreen720": [round(float(DELTA_EXACT[0]) * SCALE, 2),
                                          round(float(DELTA_EXACT[1]) * SCALE, 2)],
            "kneeDeltaAppliedPx": [float(DELTA[0]), float(DELTA[1])],
            "rootStepPerSubposeScreen720": [round(SLOT_SUB[0], 3), round(SLOT_SUB[1], 3)],
            "kneeFraction": KNEE_FRACTION,
            "subposes": meta,
            "regressionSub1VsGrounding": regress,
            "pairs": pairs,
            "stanceDriftFromLandingScreen720": {"subframes": drift_sub, "groundingHeld3": drift_gr},
            "frameSha256": png_sha,
        },
    }
    (OUT / "clip.json").write_text(json.dumps(clip, indent=2) + "\n", encoding="utf-8")

    # Independent read-back: PNGs from disk, contact de-normalised from the written JSON.
    back = json.loads((OUT / "clip.json").read_text("utf-8"))
    names = [f["file"] for f in back["frames"]]
    if names != [f"frame-{i:02d}.png" for i in range(N)]:
        raise SystemExit("frame order")
    if len(set(png_sha.values())) != N:
        raise SystemExit("duplicate PNGs")
    for f in back["frames"]:
        img = np.array(Image.open(OUT / f["file"]).convert("RGBA"))
        if img.shape != (Hp, Wp, 4):
            raise SystemExit("canvas size")
        cx, cy = f["contactX"] * Wp, (1 - f["contactY"]) * Hp
        col, row = int(np.floor(cx + 1e-3)), int(np.ceil(cy - 1e-3)) - 1
        if img[row, col, 3] < 128:
            raise SystemExit(f"{f['file']}: contact ({col},{row}) not on a visible pixel")

    previews(outs, contacts, pivot, support, gclip, meta)

    if dir_hashes(V1) != v1_before or dir_hashes(GROUND) != gr_before:
        raise SystemExit("v1 or grounding changed")
    if list((BASE / "__pycache__").glob("ground_walk_southwest*")) if (BASE / "__pycache__").exists() else []:
        raise SystemExit("bytecode for the grounding modules was written")
    print(json.dumps({"pivot": pivot, "cadenceMs": cad, "regression": regress}))
    for p in pairs:
        print(p)
    print("drift subframes", drift_sub)
    print("drift grounding", drift_gr)
    for m in meta:
        print(m)


def place(spr, root, pivot):
    return round(root[0] - pivot[0] * spr.width), round(root[1] - (1 - pivot[1]) * spr.height)


def previews(outs, contacts, pivot, support, gclip, meta):
    Hp, Wp = outs[0].shape[:2]
    floor, line = (110, 112, 107, 255), (60, 62, 58, 255)
    gr = [np.array(Image.open(GROUND / f["file"]).convert("RGBA")) for f in gclip["frames"]]

    # 1. Strip at real scale: 720p and 1080p rows, fixed pivot; grounding held (top) vs subposes.
    for tag, sc, cell, rowh in (("720p", SCALE, 62, 220), ("1080p", SCALE_1080, 92, 320)):
        strip = Image.new("RGBA", (cell * N + 16, rowh * 2 + 10), floor)
        dr = ImageDraw.Draw(strip)
        for row in range(2):
            ground = rowh * row + rowh - 30
            dr.line([(0, ground), (strip.width, ground)], fill=line)
            for i in range(N):
                cx = cell * i + cell // 2 + 8
                src = gr[i // SUB] if row == 0 else outs[i]
                spr = G.resized(src, sc)
                x, y = place(spr, (cx, ground), pivot)
                strip.alpha_composite(spr, (x, y))
                dr.line([(cx, ground - 2), (cx, ground + 2)], fill=(200, 30, 30, 255))
                if row == 1:
                    c = contacts[i][2]
                    px, py = x + c[0] * sc, y + c[1] * sc
                    dr.ellipse([px - 2, py - 2, px + 2, py + 2], outline=(40, 220, 240, 255))
                dr.text((cx - 26, ground + 6), f"{i:02d}{support[i][0].upper()}" + (" gr" if row == 0 else ""),
                        fill=(0, 0, 0, 255))
        strip.save(OUT / f"preview-{tag}-strip.png")

    # 2. Leg crops at 1.5x: rows = subpose 0/1/2, columns = base 0..5.
    y0 = 225 + PAD
    tw, th = round(Wp * 1.5), round((Hp - y0) * 1.5)
    sheet = Image.new("RGBA", (tw * 6, th * SUB), (205, 205, 205, 255))
    for i, p in enumerate(outs):
        k, j = divmod(i, SUB)
        t = Image.fromarray(p[y0:]).resize((tw, th), Image.LANCZOS)
        bg = Image.new("RGBA", t.size, (205, 205, 205, 255))
        bg.alpha_composite(t)
        ImageDraw.Draw(bg).text((4, 4), f"{i:02d} base{k} sub{j} off={meta[i]['offsetPx']}", fill=(0, 0, 0, 255))
        sheet.alpha_composite(bg, (k * tw, j * th))
    sheet.save(OUT / "preview-legs-1.5x.png")

    # 2b. 3x detail of knee/shin region (canvas x 90..230, y 270..420) on the frames flagged in the note.
    box = (90, 270, 230, 420)
    pick = [3, 4, 6, 7, 8, 12, 15, 16, 17]
    dw, dh = (box[2] - box[0]) * 3, (box[3] - box[1]) * 3
    det = Image.new("RGBA", (dw * len(pick), dh), (205, 205, 205, 255))
    for n, i in enumerate(pick):
        t = Image.fromarray(outs[i][box[1]:box[3], box[0]:box[2]]).resize((dw, dh), Image.NEAREST)
        bg = Image.new("RGBA", t.size, (205, 205, 205, 255))
        bg.alpha_composite(t)
        g = ImageDraw.Draw(bg)
        for yy in range(0, dh, 30 * 3):
            g.text((2, yy), str(box[1] + yy // 3), fill=(0, 0, 160, 255))
        g.text((dw - 60, 4), f"frame {i:02d}", fill=(0, 0, 0, 255))
        det.alpha_composite(bg, (n * dw, 0))
    det.save(OUT / "preview-knee-3x.png")

    # 3. Comparison GIF at 720p: grounding held 3 ticks (left) vs 18 subposes (right), same root
    # step per tick. Red ring = landing spot of the current stance, cyan dot = current contact.
    width, height = 300, 330
    durs = [50] * N
    durs[SUB * 3 - 1] = durs[N - 1] = 40               # 16 x 50 + 2 x 40 = 880 ms per cycle
    gcont = [(f["contactX"] * Wp, (1 - f["contactY"]) * Hp) for f in gclip["frames"]]
    spr_s = [G.resized(p, SCALE) for p in outs]
    spr_g = [G.resized(p, SCALE) for p in gr]
    imgs, landed = [], [None, None]
    start = (250.0, 150.0)
    for n in range(N * 2):
        i = n % N
        root = (start[0] + SLOT_SUB[0] * n, start[1] + SLOT_SUB[1] * n)
        img = Image.new("RGBA", (width * 2, height), floor)
        g = ImageDraw.Draw(img)
        for side, (spr, c) in enumerate(((spr_g[i // SUB], gcont[i // SUB]), (spr_s[i], contacts[i][2]))):
            ox = side * width
            r = (root[0] + ox, root[1])
            g.line([(start[0] + ox, start[1]), (start[0] + ox + SLOT_SUB[0] * 60, start[1] + SLOT_SUB[1] * 60)],
                   fill=line)
            x, y = place(spr, r, pivot)
            img.alpha_composite(spr, (x, y))
            foot = (x + c[0] * SCALE, y + c[1] * SCALE)
            if i % (3 * SUB) == 0:
                landed[side] = foot
            lx, ly = landed[side]
            g.ellipse([lx - 3, ly - 3, lx + 3, ly + 3], outline=(230, 40, 40, 255))
            g.ellipse([foot[0] - 1, foot[1] - 1, foot[0] + 1, foot[1] + 1], fill=(40, 200, 230, 255))
            g.text((ox + 6, 6), ("grounding 6 (held x3)" if side == 0 else "subframes 18") + f"  {i:02d} {support[i]}",
                   fill=(0, 0, 0, 255))
        g.line([(width, 0), (width, height)], fill=(0, 0, 0, 255))
        imgs.append(img.convert("RGB"))
    imgs[0].save(OUT / "preview-720p-compare.gif", save_all=True, append_images=imgs[1:],
                 duration=durs * 2, loop=0, optimize=False, disposal=1)

    # 4. Planted check at 720p, 5x zoom around each stance's landing spot (cyan cross).
    # Rows: grounding left, subframes left, grounding right, subframes right.
    Z, R = 4, 20
    T = 2 * R * Z
    check = Image.new("RGBA", (9 * T, 4 * (T + 16)), floor)
    cg = ImageDraw.Draw(check)
    for side, (srcs, cs) in enumerate(((spr_g, gcont), (spr_s, None))):
        land = {}
        for i in range(N):
            spr = srcs[i // SUB] if side == 0 else srcs[i]
            c = cs[i // SUB] if side == 0 else contacts[i][2]
            root = (start[0] + SLOT_SUB[0] * i, start[1] + SLOT_SUB[1] * i)
            world = Image.new("RGBA", (width, height), floor)
            x, y = place(spr, root, pivot)
            world.alpha_composite(spr, (x, y))
            spot = (x + c[0] * SCALE, y + c[1] * SCALE)
            land.setdefault(support[i], spot)
            lx, ly = land[support[i]]
            tile = world.crop((round(lx) - R, round(ly) - R, round(lx) + R, round(ly) + R)).resize((T, T), Image.NEAREST)
            tg = ImageDraw.Draw(tile)
            cx, cy = (lx - round(lx) + R) * Z, (ly - round(ly) + R) * Z
            tg.line([(cx - 12, cy), (cx + 12, cy)], fill=(40, 230, 240, 255), width=2)
            tg.line([(cx, cy - 12), (cx, cy + 12)], fill=(40, 230, 240, 255), width=2)
            sx, sy = spot[0] - lx, spot[1] - ly
            tg.ellipse([cx + sx * Z - 3, cy + sy * Z - 3, cx + sx * Z + 3, cy + sy * Z + 3], outline=(255, 60, 60, 255))
            row = (0 if support[i] == "left" else 2) + side
            col = i % 9
            check.alpha_composite(tile, (col * T, row * (T + 16) + 16))
            cg.text((col * T + 3, row * (T + 16) + 2), f"{i:02d} {'gr' if side == 0 else 'sub'} {support[i][0].upper()}",
                    fill=(0, 0, 0, 255))
    check.save(OUT / "preview-720p-planted-check.png")


if __name__ == "__main__":
    main()
