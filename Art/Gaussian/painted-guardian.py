"""Bake the Gaussian relief from the editable eight-direction guardian sheet. Requires Pillow."""
import argparse
from math import log, sin, pi
from itertools import groupby
from pathlib import Path
from struct import Struct
from PIL import Image

root = Path(__file__).resolve().parents[2]
concepts = root / "Art/Concepts/Observatorio"
atlas_path = root / "Assets/Resources/Lumon/GuardianPainted.png"
width, height = 182, 250
paint_height = 244
spacing = 1.55 / paint_height


def gutter_edges(projection, count):
    # Use the actual transparent gutters: illustrated rows can extend across the
    # mathematical midpoint, especially the hair in the lower half of a sheet.
    size = len(projection)
    gutters = []
    for opaque, items in groupby(enumerate(projection), key=lambda sample: bool(sample[1])):
        if not opaque:
            indices = [index for index, _ in items]
            if len(indices) >= 3:
                gutters.append((indices[0] + indices[-1] + 1) // 2)
    edges = [0]
    for i in range(1, count):
        nominal = i * size / count
        cut = min(gutters, key=lambda value: abs(value - nominal))
        if abs(cut - nominal) > .2 * size / count:
            raise ValueError(f"No clear sprite gutter near {nominal}")
        edges.append(cut)
    return edges + [size]


def read_poses(name, columns, rows, body_height=paint_height):
    with Image.open(concepts / f"guardian-{name}.png") as sheet:
        sheet = sheet.convert("RGBA")
    x, y = sheet.getchannel("A").point(lambda a: 255 if a > 96 else 0).getprojection()
    x_edges, y_edges = gutter_edges(x, columns), gutter_edges(y, rows)
    poses = {}
    for row in range(rows):
        for col in range(columns):
            cell = sheet.crop((x_edges[col], y_edges[row], x_edges[col + 1], y_edges[row + 1]))
            alpha = cell.getchannel("A").point(lambda a: 255 if a > 24 else 0)
            cut = cell.crop(alpha.getbbox())
            # Align by the head and pelvis, so an extended hand or boot cannot move the torso.
            head = cut.getchannel("A").crop((0, 0, cut.width, round(cut.height * .19)))
            head_bounds = head.point(lambda a: 255 if a > 96 else 0).getbbox()
            anchor = (head_bounds[0] + head_bounds[2]) / 2
            scale = body_height / cut.height
            cut = cut.resize((round(cut.width * scale), body_height), Image.Resampling.LANCZOS)
            left = round(width / 2 - anchor * scale)
            if cut.width > width - 2:
                raise ValueError(f"Cropped silhouette in {name} {row} {col}")
            left = max(1, min(left, width - 1 - cut.width))
            tile = Image.new("RGBA", (width, height))
            tile.paste(cut, (left, height - body_height - 2))
            poses[col, row] = tile
    return poses


def assemble_from_concepts():
    from guardian_walk import bake
    axial = read_poses("axial", 2, 3)
    east = read_poses("oblique", 3, 3)
    west = read_poses("west-contact", 3, 3)
    views = [axial[0, 0], east[0, 0], east[1, 0], east[2, 0],
             axial[1, 0], west[2, 0], west[1, 0], west[0, 0]]
    return bake(views)


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--export-from-concepts", type=Path, metavar="OUTPUT.png",
                    help="Export the original drawings as a new sheet, instead of baking the relief.")
args = parser.parse_args()
if args.export_from_concepts:
    assemble_from_concepts().save(args.export_from_concepts)
    raise SystemExit

# Twenty evenly sampled walk poses and three gathering poses for each foot.
# Pack the sequence into two banks to stay within a 4096-pixel texture.
width, height = 196, 284
bank_rows = 14
with Image.open(atlas_path) as atlas:
    if atlas.mode != "RGBA" or atlas.size != (16 * width, bank_rows * height):
        raise ValueError(f"Expected a 3136 × 3976 RGBA guardian sheet: {atlas_path}")
    for pose in range(27):
        for direction in range(8):
            x = (direction + 8 * (pose // bank_rows)) * width
            y = (pose % bank_rows) * height
            cell = atlas.crop((x, y, x + width, y + height))
            bounds = cell.getchannel("A").point(lambda a: 255 if a > 32 else 0).getbbox()
            if not bounds or min(bounds[:2]) < 1 or bounds[2] >= width or bounds[3] >= height:
                raise ValueError(f"Empty or clipped pose {pose} in view {direction}")
    idle = atlas.crop((width, 0, 2 * width, height))

# A shared grid retains vertex identity between poses. Each player selects their own
# texel window. The resting pigments and coverage also make the PLY viewable on its own.
vertex = Struct("<14f")
points = bytearray()
for y in range(height):
    for x in range(width):
        r, g, b, a = idle.getpixel((x, height - 1 - y))
        alpha = min(.999, max(.00001, a / 255))
        relief = -.035 * sin(pi * (x + .5) / width) * sin(pi * (y + .5) / height)
        points += vertex.pack((x + .5 - width / 2) * spacing, (y - 35.5) * spacing, relief,
                              log(.44 * spacing), log(.44 * spacing), log(.12 * spacing), 0, 0, 0, 1,
                              *((v / 255 - .5) / .2820948 for v in (r, g, b)), log(alpha / (1 - alpha)))
fields = "x y z scale_0 scale_1 scale_2 rot_1 rot_2 rot_3 rot_0 f_dc_0 f_dc_1 f_dc_2 opacity"
header = ["ply", "format binary_little_endian 1.0", f"element vertex {width * height}"]
header += ["property float " + field for field in fields.split()] + ["end_header", ""]
(root / "Assets/StreamingAssets/Lumon/Splats/guardian-painted.ply").write_bytes("\n".join(header).encode() + points)
