"""Export only review meshes for a temporary load in the Unity evaluation scene."""
from pathlib import Path

import bmesh
import bpy

ASSET = Path(__file__).resolve().parents[1]


def main():
    bpy.ops.wm.open_mainfile(filepath=str(ASSET / "CH_Guardian_master.blend"))
    bpy.ops.object.select_all(action="DESELECT")
    meshes = list(bpy.data.collections["LOW"].objects)
    for mesh in meshes:
        assert mesh.type == "MESH" and mesh.get("phase") == "02_blockout", mesh.name
        topology = bmesh.new()
        topology.from_mesh(mesh.data)
        assert all(edge.is_manifold for edge in topology.edges), mesh.name
        topology.free()
        mesh.select_set(True)

    output = ASSET / "review" / "02-blockout" / "CH_Guardian_review.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(output),
        use_selection=True,
        object_types={"MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=True,
        bake_anim=False,
        use_mesh_modifiers=False,
    )
    print(f"RESULT meshes={len(meshes)} output={output}")


if __name__ == "__main__":
    main()
