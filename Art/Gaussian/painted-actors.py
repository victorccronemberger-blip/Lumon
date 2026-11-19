"""Bake painted characters into camera-facing relief splats. Requires Pillow."""
from pathlib import Path
from math import log, sin, pi
from struct import Struct
from PIL import Image

root = Path(__file__).resolve().parents[2]
source = Image.open(root / "Art/Concepts/Observatorio/forge-duo.png").convert("RGBA")
out = root / "Assets/StreamingAssets/Lumon/Splats"
fields = "x y z scale_0 scale_1 scale_2 rot_1 rot_2 rot_3 rot_0 f_dc_0 f_dc_1 f_dc_2 opacity".split()
pack = Struct("<14f").pack

def bake(name, left, right, height, pivot):
    cut = source.crop((left, 0, right, source.height))
    bounds = cut.getchannel("A").point(lambda a: 255 if a > 32 else 0).getbbox()
    cut = cut.crop(bounds)
    h = 186
    w = round(h * cut.width / cut.height)
    cut = cut.resize((w, h), Image.Resampling.LANCZOS)
    spacing = height / h
    rows = bytearray()
    for j in range(h):
        for i in range(w):
            r,g,b,a = cut.getpixel((i,j))
            if a < 30:
                continue
            u,v=(i+.5)/w,1-(j+.5)/h
            depth=-.035*sin(pi*u)*sin(pi*v)
            opacity=max(.01,min(.999,a/255))
            rows += pack((u-pivot)*spacing*w, v*height, depth,
                         log(spacing*.72),log(spacing*.72),log(spacing*.12),0,0,0,1,
                         *((c/255-.5)/.2820948 for c in (r,g,b)), log(opacity/(1-opacity)))
    header = ["ply","format binary_little_endian 1.0",f"element vertex {len(rows)//56}"]
    header += ["property float "+f for f in fields]+["end_header",""]
    (out/(name+".ply")).write_bytes("\n".join(header).encode("ascii")+rows)
    print(name, w, h, len(rows)//56, 'width',w*spacing)
bake("dawn-pangolin",0,source.width//2,.70,.48)
bake("dawn-smith",source.width//2,source.width,1.32,.66)

# A soft elliptical contact mark underneath each pair of feet.
rows = bytearray()
for i in range(-12,13):
    for j in range(-6,7):
        radius = ((i/12)**2 + (j/6)**2)**0.5
        if radius >= 1: continue
        opacity = .27 * (1-radius**2)
        rows += pack(i*.031,0,j*.031,log(.022),log(.022),log(.003),-.7071068,0,0,.7071068,
                     -.99,-.96,-.80,log(opacity/(1-opacity)))
header=["ply","format binary_little_endian 1.0",f"element vertex {len(rows)//56}"]
header += ["property float "+f for f in fields]+["end_header",""]
(out/"workshop-shadow.ply").write_bytes("\n".join(header).encode("ascii")+rows)
