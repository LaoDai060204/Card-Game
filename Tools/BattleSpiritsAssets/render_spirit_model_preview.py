#!/usr/bin/env python3
"""Render the actual production skinned rest meshes exported by the C# harness.

This is a Blender geometry review, not a Unity screenshot or shader/input test.
It never reads, crops, paints or rewrites official card artwork.

Export: BATTLE_SPIRITS_MODEL_EXPORT=/tmp/cardgame-spirit-preview dotnet run \
  --project Tests/BattleSpirits/Presentation/BattleSpirits.Presentation.Tests.csproj \
  -c Release -- /path/to/Card-Game --export-models-only
Render: blender --background --python render_spirit_model_preview.py -- \
  --models /tmp/cardgame-spirit-preview/production-models.json \
  --output /tmp/cardgame-spirit-preview/renders
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import shutil
import subprocess
import sys

import bpy
from mathutils import Vector


def point(value):
    # Rotate Y-up into Z-up without a reflection, preserving winding/normals.
    return (value[0], -value[2], value[1])


def material(name, color, roughness=.45, metallic=.0):
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    surface = result.node_tree.nodes.get("Principled BSDF")
    surface.inputs["Base Color"].default_value = (*color, 1)
    surface.inputs["Roughness"].default_value = roughness
    surface.inputs["Metallic"].default_value = metallic
    return result


def area_light(name, location, color, power, size, target=(0, 0, 1.5)):
    data = bpy.data.lights.new(name, "AREA")
    data.color = color
    data.energy = power
    data.shape = "DISK"
    data.size = size
    result = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(result)
    result.location = location
    result.rotation_euler = (Vector(target) - result.location).to_track_quat("-Z", "Y").to_euler()
    return result


def setup_scene(resolution, samples):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    # Some headless Blender builds omit OpenImageDenoise. Keep this geometry
    # review portable rather than failing the entire batch after rendering.
    scene.cycles.use_denoising = False
    scene.cycles.use_adaptive_sampling = True
    scene.render.resolution_x = resolution
    scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.world.color = (.025, .035, .055)
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (.022, .034, .055, 1)
    background.inputs["Strength"].default_value = .4
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.10))
    bpy.context.object.name = "PreviewGround_NotProductionField"
    bpy.context.object.data.materials.append(material("ReviewFloor", (.018, .03, .045), .38, .12))
    bpy.ops.mesh.primitive_cylinder_add(vertices=96, radius=1.75, depth=.14, location=(0, 0, -.025))
    bpy.context.object.name = "PreviewPlinth_NotProductionField"
    bpy.context.object.data.materials.append(material("ReviewPlinth", (.035, .065, .09), .34, .28))
    bevel = bpy.context.object.modifiers.new("PreviewPlinthEdge", "BEVEL")
    bevel.width = .045
    bevel.segments = 3
    area_light("WarmKey", (3.5, 5.5, 7.0), (1, .86, .69), 1100, 5)
    area_light("CoolFill", (-4, 2, 4), (.45, .73, 1), 750, 4)
    area_light("CyanRim", (1, -4, 5), (.28, .75, 1), 1300, 3)
    data = bpy.data.cameras.new("ReviewCamera")
    camera = bpy.data.objects.new("ReviewCamera", data)
    bpy.context.collection.objects.link(camera)
    # Production profiles face Unity -Z, which maps to Blender +Y.
    camera.location = (4.8, 7.8, 4.9)
    camera.rotation_euler = (Vector((0, 0, 1.55)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    data.type = "ORTHO"
    data.ortho_scale = 4.7
    scene.camera = camera
    return scene


def create_production_model(row, gamma_colors):
    data = bpy.data.meshes.new("ProductionRestMesh_" + row["id"])
    vertices = [point(value) for value in row["vertices"]]
    triangles = row["triangles"]
    data.from_pydata(vertices, [], [triangles[i:i + 3] for i in range(0, len(triangles), 3)])
    data.update()
    color = data.color_attributes.new(name="ProductionVertexColor", type="FLOAT_COLOR", domain="POINT")
    for index, value in enumerate(row["colors"]):
        if gamma_colors:
            color.data[index].color_srgb = value
        else:
            color.data[index].color = value
    for polygon in data.polygons:
        polygon.use_smooth = True
    # Preserve the model builder's analytic round normals and flat accessory
    # normals; no subdivision or replacement geometry is applied here.
    normals = row.get("normals")
    if normals and len(normals) == len(vertices):
        data.normals_split_custom_set_from_vertices([point(value) for value in normals])
    surface = material("ProductionVertexColorReview", (1, 1, 1), .48, .18)
    attribute = surface.node_tree.nodes.new("ShaderNodeVertexColor")
    attribute.layer_name = "ProductionVertexColor"
    shader = surface.node_tree.nodes.get("Principled BSDF")
    surface.node_tree.links.new(attribute.outputs["Color"], shader.inputs["Base Color"])
    result = bpy.data.objects.new("ProductionModel_" + row["id"], data)
    bpy.context.collection.objects.link(result)
    data.materials.append(surface)
    return result


def contact_sheet(output, rows):
    # Composite generated review renders only, never the source card artwork.
    python = shutil.which("python3") or shutil.which("python")
    if python is None:
        return None
    helper = r'''
import json, math, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
out=Path(sys.argv[1]); rows=json.loads(sys.argv[2]); side=320; label=58; columns=5
sheet=Image.new('RGB',(columns*side,math.ceil(len(rows)/columns)*(side+label)+72),(9,15,24))
d=ImageDraw.Draw(sheet)
try: font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',16)
except OSError: font=ImageFont.load_default()
d.text((18,12),'BATTLE SPIRITS - 19 production 3D rest meshes',font=font,fill=(228,238,250))
d.text((18,38),'Blender review only - original fan interpretations, not Unity rendering',font=font,fill=(143,171,195))
for i,row in enumerate(rows):
    x=i%columns*side; y=i//columns*(side+label)+72
    with Image.open(out/(row['id']+'.png')) as im: sheet.paste(im.convert('RGB').resize((side,side),Image.Resampling.LANCZOS),(x,y))
    d.text((x+12,y+side+5),row['id'],font=font,fill=(225,238,247))
    text=row['profile']; d.text((x+12,y+side+27),text[:34],font=font,fill=(143,171,195))
target=out/'production-model-contact-sheet.jpg'; sheet.save(target,quality=94)
print(target)
'''
    result = subprocess.run([python, "-c", helper, str(output), json.dumps(rows)], capture_output=True, text=True)
    if result.returncode:
        print("Contact sheet unavailable: " + result.stderr, file=sys.stderr)
        return None
    return output / "production-model-contact-sheet.jpg"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--models", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--ids", nargs="*")
    parser.add_argument("--resolution", type=int, default=512)
    parser.add_argument("--samples", type=int, default=24)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    source = json.loads(args.models.read_text())
    rows = [row for row in source["models"] if not args.ids or row["id"] in args.ids]
    if not rows:
        raise ValueError("No requested production meshes were found.")
    args.output.mkdir(parents=True, exist_ok=True)
    records = []
    for row in rows:
        scene = setup_scene(max(128, args.resolution), max(1, args.samples))
        create_production_model(row, source.get("projectColorSpace") == "Gamma")
        path = args.output / (row["id"] + ".png")
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        records.append({"id": row["id"], "profile": row["profile"], "triangles": len(row["triangles"]) // 3,
                        "vertices": len(row["vertices"]), "file": path.name,
                        "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
        print("REVIEW_RENDER " + row["id"] + " " + str(path), flush=True)
    sheet = contact_sheet(args.output, records)
    metadata = {"sourceMeshExportSha256": hashlib.sha256(args.models.read_bytes()).hexdigest(),
                "renderer": "Blender " + bpy.app.version_string + "/Cycles CPU", "samples": args.samples,
                "unityRenderingVerified": False, "meshVerticesChangedByPreview": False,
                "previewLightingAndGroundAreNotProductionUnityField": True,
                "sourceVertexColorEncoding": source.get("projectColorSpace", "Linear"),
                "contactSheet": sheet.name if sheet is not None else None, "renders": records}
    (args.output / "preview-manifest.json").write_text(json.dumps(metadata, indent=2) + "\n")


if __name__ == "__main__":
    main()
