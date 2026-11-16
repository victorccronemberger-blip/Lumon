"""Build the Guardian's proportion meshes from registered front/profile sections."""
from math import cos, pi, sin
from pathlib import Path

import bmesh
import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector

ASSET = Path(__file__).resolve().parents[1]
MASTER = ASSET / "CH_Guardian_master.blend"
METRES_PER_PIXEL = 1.8 / 653
GROUND = 666
AXIS = 180


def section_mesh(collection, name, sections, exponent=1.0):
    """Closed loft using (reference row, front left/right, profile back/front)."""
    ring_size = 32
    verts = []
    for row, left, right, back, front in sections:
        center_x = (left + right) / 2 - AXIS
        center_y = AXIS - (back + front) / 2
        radius_x = (right - left) / 2
        radius_y = (front - back) / 2
        for i in range(ring_size):
            angle = 2 * pi * i / ring_size
            x = cos(angle)
            y = sin(angle)
            x = center_x + radius_x * abs(x) ** exponent * (1 if x >= 0 else -1)
            y = center_y + radius_y * abs(y) ** exponent * (1 if y >= 0 else -1)
            verts.append((x * METRES_PER_PIXEL, y * METRES_PER_PIXEL,
                          (GROUND - row) * METRES_PER_PIXEL))

    faces = [tuple(reversed(range(ring_size)))]
    for offset in range(0, len(verts) - ring_size, ring_size):
        for i in range(ring_size):
            a = offset + i
            b = offset + (i + 1) % ring_size
            faces.append((a, b, b + ring_size, a + ring_size))
    faces.append(tuple(range(len(verts) - ring_size, len(verts))))

    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.validate()
    topology = bmesh.new()
    topology.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(topology, faces=list(topology.faces))
    topology.to_mesh(mesh)
    topology.free()
    obj = bpy.data.objects.new(name, mesh)
    obj["phase"] = "02_blockout"
    collection.objects.link(obj)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    return obj


def mirror_sections(sections):
    return [(row, 2 * AXIS - right - 4, 2 * AXIS - left - 4, back, front)
            for row, left, right, back, front in sections]


def build(collection):
    # Front silhouette and profile depths sampled from the scaled reference rasters.
    section_mesh(collection, "Blockout_Hair", [
        (13, 178, 187, 175, 187), (21, 157, 200, 160, 208),
        (30, 144, 210, 148, 223), (42, 133, 221, 136, 223),
        (59, 129, 224, 126, 216), (76, 136, 219, 117, 199),
        (93, 137, 213, 113, 191), (106, 142, 208, 116, 181),
        (119, 150, 199, 124, 154), (125, 160, 192, 132, 148),
    ])
    section_mesh(collection, "Blockout_Head", [
        (35, 153, 204, 159, 219), (47, 146, 208, 152, 219),
        (62, 144, 208, 151, 225), (75, 144, 207, 151, 229),
        (80, 147, 204, 154, 232), (89, 148, 201, 157, 222),
        (100, 152, 199, 160, 218), (111, 161, 192, 164, 207),
        (115, 169, 184, 172, 200),
    ])
    section_mesh(collection, "Blockout_Collar", [
        (105, 164, 195, 164, 197), (117, 137, 218, 143, 201),
        (127, 132, 218, 147, 212), (145, 150, 209, 152, 219),
    ], exponent=0.78)
    section_mesh(collection, "Blockout_Coat", [
        (124, 154, 208, 151, 209), (136, 125, 233, 148, 221),
        (148, 105, 251, 145, 230), (165, 110, 245, 145, 235),
        (185, 117, 239, 146, 238), (205, 120, 235, 149, 237),
        (223, 124, 233, 150, 237), (244, 127, 230, 148, 234),
        (260, 130, 226, 146, 230), (267, 131, 224, 148, 226),
    ], exponent=0.82)
    section_mesh(collection, "Blockout_Hips", [
        (257, 133, 226, 150, 224), (274, 122, 233, 149, 233),
        (296, 111, 241, 147, 237), (319, 101, 245, 145, 235),
        (337, 105, 249, 148, 231), (350, 109, 246, 153, 226),
        (355, 143, 213, 155, 222),
    ], exponent=0.83)

    # Depth and sagittal tracking of both arms are inferred where the profile overlaps the torso.
    arm = [
        (149, 104, 133, 150, 217), (164, 95, 143, 141, 222),
        (180, 88, 141, 140, 220), (194, 81, 134, 143, 218),
        (220, 65, 115, 150, 221), (238, 55, 103, 152, 215),
        (260, 42, 91, 152, 210), (272, 40, 78, 157, 201),
        (281, 41, 70, 161, 198),
    ]
    hand = [
        (273, 40, 71, 160, 201), (285, 35, 70, 159, 204),
        (300, 30, 63, 156, 210), (311, 22, 49, 157, 212),
        (323, 17, 42, 159, 212), (335, 13, 38, 161, 209),
        (346, 13, 31, 164, 204), (358, 17, 24, 168, 195),
        (366, 20, 24, 173, 185),
    ]
    leg = [
        (314, 105, 178, 151, 231), (343, 104, 176, 151, 231),
        (365, 103, 175, 153, 228), (395, 104, 172, 151, 222),
        (430, 103, 168, 152, 217), (451, 99, 167, 149, 214),
        (473, 101, 164, 141, 205), (485, 103, 162, 137, 201),
        (509, 102, 162, 134, 199), (531, 104, 158, 137, 194),
        (548, 106, 155, 140, 190), (561, 112, 149, 144, 188),
    ]
    boot = [
        (541, 107, 154, 140, 193), (550, 106, 154, 139, 194),
        (573, 111, 150, 145, 191), (590, 113, 146, 148, 189),
        (610, 113, 144, 145, 196), (627, 112, 147, 144, 206),
        (642, 104, 149, 141, 230), (653, 98, 149, 138, 245),
        (662, 98, 148, 139, 245), (666, 103, 145, 144, 238),
    ]
    for piece, sections, power in [
        ("Sleeve", arm, 0.92), ("Hand", hand, 0.85),
        ("Trouser", leg, 0.90), ("Boot", boot, 0.66),
    ]:
        section_mesh(collection, f"Blockout_{piece}_R", sections, power)
        section_mesh(collection, f"Blockout_{piece}_L", mirror_sections(sections), power)


def main():
    bpy.ops.wm.open_mainfile(filepath=str(MASTER))
    scene = bpy.context.scene
    collection = bpy.data.collections["LOW"]
    for obj in list(collection.objects):
        if obj.get("phase") == "02_blockout":
            mesh = obj.data
            bpy.data.objects.remove(obj, do_unlink=True)
            if mesh.users == 0:
                bpy.data.meshes.remove(mesh)
    build(collection)

    camera = scene.camera
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    camera.location = (0, -9.7, 6.3)
    camera.rotation_quaternion = Vector((0, cos(pi / 6), -sin(pi / 6))).to_track_quat("-Z", "Y")
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 8.2 * 16 / 9
    bpy.context.view_layer.update()

    ground = world_to_camera_view(scene, camera, Vector((0, 0, 0)))
    top = world_to_camera_view(scene, camera, Vector((0, 0, 1.8)))
    projected_pixels = (top.y - ground.y) * 720
    assert abs(projected_pixels - 136.9) < 0.2, projected_pixels
    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(MASTER))
    print(f"RESULT meshes={len(collection.objects)} | gameplay_height_px={projected_pixels:.2f}")


if __name__ == "__main__":
    main()
