# Aether Drake sample model

An original low-poly creature created for this project's summon demonstration.
It is a stand-in model, not a recreation of any particular Battle Spirits card.

Runtime asset: `Assets/BattleSpirits/Resources/Models/SummonedDragon.fbx`.
Editable source: `Tools/Art/SummonedDragon.blend` (Blender 4.3.2).

The model has one skinned mesh, 13 bones, 943 vertices and approximately 1,578
triangles. Six plain materials provide crimson scales, dark wing membranes,
bronze armor, gold horns/claws, cyan eyes and ivory teeth. No textures or
external art downloads are required.

## Animation import

The FBX contains three baked actions at 24 fps:

| Action | Frames | Use |
| --- | --- | --- |
| Idle | 1–49 | Two-second breathing, wing and tail loop |
| Summon | 1–48 | Rise into view, spread wings and settle |
| Attack | 1–36 | Forward lunge, jaw opening and claw motion |

FBX take names can include the rig name. Match the suffix `Idle`, `Summon` or
`Attack`. This project imports a Generic rig and uses `Animator` with a
`PlayableGraph` to blend the three clips. The Editor setup configures the Idle
loop; the runtime returns to Idle after Summon/Attack. The presentation assigns
URP-compatible materials when it creates the model.

The standing mesh is about 2.175 units high, 3.964 units wide with extended
wings, and 3.32 units long. Its origin is at ground level. Unity's resource name
is `Models/SummonedDragon`.

## Regenerate and audit

From the Unity project directory:

```sh
blender --background --python Tools/Art/generate_summoned_dragon.py -- --project-root .
blender --background --python Tools/Art/validate_dragon_fbx.py
```

The generator optionally accepts `--preview /absolute/path/preview.png`. It
exports the FBX, saves the editable source, and writes a build report to the
sibling `battle-spirits-work` folder. Preview cameras, lights and the floor are
not exported to the runtime FBX.

The round-trip audit checks Blender's ability to reimport the exported FBX,
bone and mesh counts, vertex weights and animated bounds. It is not a Unity
import, material or Play test; those require the target Unity Editor.
