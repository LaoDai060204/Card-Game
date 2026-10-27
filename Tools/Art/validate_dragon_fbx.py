"""FBX round-trip audit. This is not a Unity import/Play test.

  blender --background --python Tools/Art/validate_dragon_fbx.py
"""

import json
from pathlib import Path

import bpy


project = Path(__file__).resolve().parents[2]
path = project / "Assets/BattleSpirits/Resources/Models/SummonedDragon.fbx"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True)
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
rigs = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
assert len(meshes) == len(rigs) == 1
mesh, rig = meshes[0], rigs[0]
actions = list(bpy.data.actions)
assert len(actions) == 3
for name in ("Idle", "Summon", "Attack"):
    assert len([action for action in actions if action.name.endswith(name)]) == 1
assert len(rig.data.bones) == 13
assert all(abs(sum(group.weight for group in vertex.groups) - 1.0) < 0.0001 for vertex in mesh.data.vertices)


def bounds():
    deps = bpy.context.evaluated_depsgraph_get()
    evaluated = mesh.evaluated_get(deps)
    transformed = [evaluated.matrix_world @ point.co for point in evaluated.data.vertices]
    return {
        "min": [min(point[index] for point in transformed) for index in range(3)],
        "max": [max(point[index] for point in transformed) for index in range(3)],
    }


audits = []
for action in actions:
    rig.animation_data.action = action
    start, end = action.frame_range
    snapshots = []
    for frame in sorted(set(int(start + (end - start) * fraction) for fraction in (0, 0.25, 0.5, 0.75, 1))):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        snapshots.append({"frame": frame, "bounds": bounds()})
    audits.append({"name": action.name, "frame_range": [start, end], "samples": snapshots})
    assert action.fcurves, action.name

idle = next(item for item in audits if item["name"].endswith("Idle"))
summon = next(item for item in audits if item["name"].endswith("Summon"))
attack = next(item for item in audits if item["name"].endswith("Attack"))
idle_height = idle["samples"][0]["bounds"]["max"][2] - idle["samples"][0]["bounds"]["min"][2]
summon_start_height = summon["samples"][0]["bounds"]["max"][2] - summon["samples"][0]["bounds"]["min"][2]
assert 1.8 < idle_height < 2.6
assert summon_start_height < idle_height * 0.1
assert abs(attack["samples"][2]["bounds"]["min"][1] - attack["samples"][0]["bounds"]["min"][1]) > 0.2
assert max(sample["bounds"]["max"][0] for sample in idle["samples"]) - min(sample["bounds"]["max"][0] for sample in idle["samples"]) > 0.01
report = {
    "fbx": str(path),
    "round_trip": "Blender FBX export and import",
    "passed": True,
    "vertices": len(mesh.data.vertices),
    "bones": len(rig.data.bones),
    "weights": "Every vertex has total weight 1.0",
    "actions": audits,
    "unity_import_tested": False,
}
output = project.parent / "battle-spirits-work/model-roundtrip-report.json"
output.write_text(json.dumps(report, indent=2) + "\n")
print("FBX_ROUND_TRIP_REPORT " + json.dumps(report))
