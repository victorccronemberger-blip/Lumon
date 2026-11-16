"""Sculpt the Guardian's face and swept hair in the editable HIGH collection."""
from math import cos, exp, pi, sin
from pathlib import Path
import sys

import bmesh
import bpy
import numpy as np

ASSET = Path(__file__).resolve().parents[1]
SKILL = Path.home() / ".codex" / "skills" / "scenario-blender-sculpting" / "scripts"
sys.path.insert(0, str(SKILL))
import bx_sculpt as sculpt

UNIT = 1.8 / 653
MASTER = ASSET / "CH_Guardian_master.blend"
OUT = ASSET / "review" / "03-portrait"


def p(x, depth, row):
    return (x * UNIT, -depth * UNIT, (666 - row) * UNIT)


def ellipsoid(x, depth, row, width, thickness, height, rot=None):
    return sculpt.sd_ellipsoid(p(x, depth, row), (width * UNIT, thickness * UNIT, height * UNIT), rot)


def stroke(points, radii):
    curve = sculpt.bezier(*(p(*point) for point in points), n=32)
    r0, r1 = radii
    sizes = [(r0 + (r1 - r0) * i / (len(curve) - 1)) * UNIT for i, _ in enumerate(curve)]
    return sculpt.sd_tube(curve, sizes, blend=0.8 * UNIT)


def new_clay():
    return sculpt.Clay(p(-66, 61, 161), p(66, -76, 4), voxel=0.0029)


CONTOURS = np.array([
        (23, 3, -15, -3), (27, 19, -27, 13), (34, 29, -36, 28),
        (43, 32.5, -38, 32), (52, 34, -37, 34), (61, 34.5, -36, 34),
        (69, 35, -34, 34), (76, 34, -31, 33.5), (84, 31.5, -27, 34.5),
        (91, 28, -22, 36.5), (98, 24, -18, 34), (104, 20, -16, 34),
        (110, 15, -16, 29), (116, 14, -17, 16), (124, 15, -18, 9),
        (135, 17, -19, 8), (142, 13, -16, 6), (148, 6, -12, 0),
], dtype=float)


def profile(row):
    # Cubic Hermite through the measured stations prevents a ring at each sampled row.
    i = int(np.clip(np.searchsorted(CONTOURS[:, 0], row) - 1, 0, len(CONTOURS) - 2))
    start, end = CONTOURS[i:i + 2]
    before = CONTOURS[max(i - 1, 0)]
    after = CONTOURS[min(i + 2, len(CONTOURS) - 1)]
    delta = end[0] - start[0]
    t = (row - start[0]) / delta
    start_slope = (end[1:] - before[1:]) / (end[0] - before[0])
    end_slope = (after[1:] - start[1:]) / (after[0] - start[0])
    return ((2 * t**3 - 3 * t**2 + 1) * start[1:] + (t**3 - 2 * t**2 + t) * delta * start_slope
            + (-2 * t**3 + 3 * t**2) * end[1:] + (t**3 - t**2) * delta * end_slope)


def face_depth(row, x, facing, back, front):
    depth = (front + back) / 2 + (front - back) / 2 * facing
    abs_x = abs(x)

    def mass(cx, cy, wx, wy):
        return exp(-((abs_x - cx) / wx) ** 2 - ((row - cy) / wy) ** 2)

    nose = 5.2 * mass(0, 67, 6.5, 15) + 10.4 * mass(0, 77, 5.4, 5.6)
    nose += 3.8 * mass(5.7, 80, 3.5, 3.1) - 1.7 * mass(4.5, 81.8, 1.75, 0.95)
    cheeks = 3.15 * mass(21.4, 74.8, 9.5, 9.0) + 1.35 * mass(16, 55.4, 12.5, 3.8)
    eyesocket = -3.5 * mass(16.1, 62.8, 9.2, 5.0)
    mouth = 1.08 * mass(0, 88.8, 11.7, 2.0) + 1.18 * mass(0, 92.6, 10.8, 2.15)
    mouth -= 1.35 * mass(0, 90.7 - abs_x * 0.11, 14, 0.82)
    return depth + (nose + cheeks + eyesocket + mouth) * max(facing, 0) ** 2


def eye_surfaces(collection):
    # Almond-shaped exposed surfaces, gently domed and embedded into the socket.
    from math import sqrt

    def vertex(x, row, height):
        width, back, front = profile(row)
        forward = sqrt(max(0.0, 1 - (x / width) ** 2))
        return p(x, face_depth(row, x, forward, back, front) + height, row)

    points_per_ring = 48
    rings = [0.22, 0.44, 0.65, 0.83, 1.0]
    for sign, side in [(1, "L"), (-1, "R")]:
        vertices = [vertex(16.5 * sign, 63.0, 1.25)]
        for radius in rings:
            for i in range(points_per_ring):
                angle = 2 * pi * i / points_per_ring
                offset = radius * cos(angle)
                x = sign * (16.5 + 8.25 * offset)
                opening = (3.15 if sin(angle) > 0 else 2.15) * radius * sin(angle)
                row = 63.0 - 1.55 * offset - opening
                vertices.append(vertex(x, row, 1.25 * (1 - radius**2) - 0.23))
        faces = [(0, 1 + i, 1 + (i + 1) % points_per_ring) for i in range(points_per_ring)]
        for ring in range(len(rings) - 1):
            left = 1 + ring * points_per_ring
            right = left + points_per_ring
            for i in range(points_per_ring):
                j = (i + 1) % points_per_ring
                faces.append((left + i, right + i, right + j, left + j))
        faces.append(tuple(range(len(vertices) - points_per_ring, len(vertices))))
        name = f"CH_Guardian_EyeSurface_{side}"
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(vertices, [], faces)
        topology = bmesh.new()
        topology.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(topology, faces=list(topology.faces))
        topology.to_mesh(mesh)
        topology.free()
        for face in mesh.polygons:
            face.use_smooth = True
        obj = bpy.data.objects.new(name, mesh)
        collection.objects.link(obj)
        yield obj


def face(collection):
    rows = np.linspace(CONTOURS[0, 0], CONTOURS[-1, 0], 126)
    n = 112
    vertices = []
    for row in rows:
        width, back, front = profile(row)
        for i in range(n):
            angle = 2 * pi * i / n
            x = width * cos(angle)
            depth = face_depth(row, x, sin(angle), back, front)
            vertices.append(p(x, depth, row))

    faces = [tuple(reversed(range(n)))]
    for offset in range(0, len(vertices) - n, n):
        for i in range(n):
            a = offset + i
            b = offset + (i + 1) % n
            faces.append((a, b, b + n, a + n))
    faces.append(tuple(range(len(vertices) - n, len(vertices))))
    mesh = bpy.data.meshes.new("CH_Guardian_Head_Sculpt")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate()
    topology = bmesh.new()
    topology.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(topology, faces=list(topology.faces))
    topology.to_mesh(mesh)
    topology.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    obj = bpy.data.objects.new(mesh.name, mesh)
    collection.objects.link(obj)
    return obj

def hair(collection):
    clay = new_clay()
    # Cap behind the forehead. The lopsided sweep feeds back into a low tied knot.
    base = ellipsoid(0, -19, 51, 37, 35, 34)
    base = base.clip(sculpt.sd_halfspace(p(0, 11, 46), (0, -1, -0.46)), 2 * UNIT)
    clay.add(base)
    clay.add(ellipsoid(0, -42, 93, 18, 23, 25, (-15, 0, 0)), blend=0.010)
    clay.add(ellipsoid(-1, -50, 105, 12, 16, 16, (20, 0, 0)), blend=0.007)
    clay.add(stroke([(12, -4, 22), (-6, 27, 12), (-24, 34, 38), (-41, 7, 57)], (8.6, 5.5)), blend=0.006)
    clay.add(stroke([(9, -16, 22), (-17, 3, 8), (-30, 27, 36), (-41, 8, 54)], (6.4, 5.8)), blend=0.006)
    clay.add(stroke([(11, 20, 24), (36, 23, 24), (27, 13, 47), (42, -9, 54)], (7, 5)), blend=0.006)
    clay.add(stroke([(16, -8, 21), (32, 5, 24), (44, 1, 50), (32, -28, 79)], (9, 7)), blend=0.008)
    clay.add(stroke([(-35, 2, 49), (-51, -4, 68), (-24, -3, 83), (-38, 13, 101)], (5.3, 2)), blend=0.006)
    clay.add(stroke([(35, -6, 46), (58, -9, 69), (24, -5, 85), (42, 12, 99)], (6, 2)), blend=0.006)
    clay.add(stroke([(-21, -19, 70), (-28, -58, 81), (-12, -60, 110), (1, -32, 111)], (11, 6)), blend=0.006)
    clay.add(stroke([(23, -15, 68), (29, -45, 74), (8, -70, 92), (-2, -49, 115)], (10, 6)), blend=0.006)
    return clay.to_object("CH_Guardian_Hair_Sculpt", collection, symmetric=False)


def drop_phase(collection):
    for obj in list(collection.objects):
        if obj.get("phase") == "03_portrait":
            mesh = obj.data
            bpy.data.objects.remove(obj, do_unlink=True)
            if mesh.users == 0:
                bpy.data.meshes.remove(mesh)


def main():
    bpy.ops.wm.open_mainfile(filepath=str(MASTER))
    out_collection = bpy.data.collections["HIGH"]
    drop_phase(out_collection)
    head = face(out_collection)
    coif = hair(out_collection)
    parts = [head, coif, *eye_surfaces(out_collection)]
    for obj in parts:
        obj["phase"] = "03_portrait"

    # Preserve the low-poly review stage, and show the primary sculpt without the three proxy masses.
    stage_copy = []
    for obj in bpy.data.collections["LOW"].objects:
        if obj.name not in {"Blockout_Head", "Blockout_Hair"}:
            copy = obj.copy()
            copy.name = "Forms_" + obj.name.removeprefix("Blockout_")
            out_collection.objects.link(copy)
            copy["phase"] = "03_portrait"
            stage_copy.append(copy)
    bpy.context.scene.view_layers[0].layer_collection.children["LOW"].exclude = True
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(MASTER))

    OUT.mkdir(parents=True, exist_ok=True)
    for name, direction in [
        ("front", (0, -1, 0.01)),
        ("profile", (-1, 0, 0.015)),
        ("three-quarter", (-0.61, -1, 0.12)),
    ]:
        sculpt.final_render(parts, str(OUT / f"clay-{name}.png"),
                            direction=direction, lens=90, res=650, cavity=False)
    print("RESULT", sculpt.stage_report(head), sculpt.stage_report(coif))


if __name__ == "__main__":
    main()
