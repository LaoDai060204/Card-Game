#!/usr/bin/env python3
"""Review the production paper mesh with its unedited RGBA character texture.

This Blender render verifies exported geometry/UV/alpha and is not a Unity
screenshot or URP shader/input test. No source card or cutout pixels are edited.

Export: BATTLE_SPIRITS_STANDEE_EXPORT=/tmp/bs-paper dotnet run --project \
  Tests/BattleSpirits/Presentation/BattleSpirits.Presentation.Tests.csproj \
  -c Release -- /path/to/Card-Game --export-standees-only
Render: blender --background --python render_character_standee_preview.py -- \
  --standees /tmp/bs-paper/production-standees.json --output /tmp/bs-paper/renders
"""
import argparse
import json
from pathlib import Path
import sys

import bpy
from mathutils import Quaternion, Vector


def point(value):
    # Unity is left-handed, Blender right-handed. Reflect handedness while
    # preserving character left/right; reverse triangle winding below.
    return (value[0], value[2], value[1])


def solid_material(name, color, roughness=.42, vertex_color=False):
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    shader = result.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Roughness"].default_value = roughness
    if vertex_color:
        attribute = result.node_tree.nodes.new("ShaderNodeVertexColor")
        attribute.layer_name = "ProductionVertexColor"
        tint = result.node_tree.nodes.new("ShaderNodeMixRGB")
        tint.blend_type = "MULTIPLY"
        tint.inputs[0].default_value = 1
        tint.inputs[2].default_value = (*color, 1)
        result.node_tree.links.new(attribute.outputs["Color"], tint.inputs[1])
        result.node_tree.links.new(tint.outputs[0], shader.inputs["Base Color"])
    return result


def artwork_material(path):
    result = bpy.data.materials.new("UneditedCharacterRGBA")
    result.use_nodes = True
    nodes, links = result.node_tree.nodes, result.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    mix = nodes.new("ShaderNodeMixShader")
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    emission = nodes.new("ShaderNodeEmission")
    image = nodes.new("ShaderNodeTexImage")
    image.image = bpy.data.images.load(path, check_existing=True)
    image.interpolation = "Linear"
    cutoff = nodes.new("ShaderNodeMath")
    cutoff.operation = "GREATER_THAN"
    cutoff.inputs[1].default_value = 32 / 255
    links.new(image.outputs["Color"], emission.inputs["Color"])
    links.new(image.outputs["Alpha"], cutoff.inputs[0])
    links.new(cutoff.outputs[0], mix.inputs[0])
    links.new(transparent.outputs[0], mix.inputs[1])
    links.new(emission.outputs[0], mix.inputs[2])
    links.new(mix.outputs[0], output.inputs["Surface"])
    return result


def mesh_object(name, row, materials, is_paper=False):
    data = bpy.data.meshes.new(name)
    triangles = row["triangles"]
    data.from_pydata([point(v) for v in row["vertices"]], [],
                     [(triangles[i], triangles[i + 2], triangles[i + 1])
                      for i in range(0, len(triangles), 3)])
    data.update()
    colors = data.color_attributes.new(name="ProductionVertexColor", type="FLOAT_COLOR", domain="POINT")
    for index, color in enumerate(row["colors"]):
        # Production project uses Gamma color space for the material vertex tint.
        colors.data[index].color_srgb = color
    layer = data.uv_layers.new(name="ProductionUV")
    for polygon in data.polygons:
        face = row["colors"][polygon.vertices[0]][3] > .5
        polygon.material_index = 0 if is_paper and face else 1 if is_paper else 0
        for loop in polygon.loop_indices:
            layer.data[loop].uv = row["uv"][data.loops[loop].vertex_index]
    if row.get("normals"):
        data.normals_split_custom_set_from_vertices([point(n) for n in row["normals"]])
    for material in materials:
        data.materials.append(material)
    result = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(result)
    return result


def create_standee(row, location=(0, 0, 0)):
    parent = bpy.data.objects.new("Standee_" + row["id"], None)
    bpy.context.collection.objects.link(parent)
    parent.location = location
    base = mesh_object("ProductionOvalBase_" + row["id"], row["plinth"],
                       [solid_material("ProductionBaseTint_" + row["id"],
                                       tuple(max(.06, c * .42) for c in row["accent"]), vertex_color=True)])
    paper = mesh_object("ProductionCharacterPaper_" + row["id"], row["paper"],
                        [artwork_material(row["cutoutPath"]), solid_material("PaperStockEdge", (1, 1, 1), .65, vertex_color=True)], True)
    paper.parent = base.parent = parent
    paper.location = point(row["paperOffset"])
    x, y, z, w = row["paperRotation"]
    paper.rotation_mode = "QUATERNION"
    paper.rotation_quaternion = Quaternion((w, -x, -z, -y))
    return parent


def light(name, location, energy, color, target=(0, 0, 1.4)):
    data = bpy.data.lights.new(name, "AREA")
    data.energy, data.color, data.size = energy, color, 5
    result = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(result)
    result.location = location
    result.rotation_euler = (Vector(target) - result.location).to_track_quat("-Z", "Y").to_euler()


def render(row, output, resolution, samples, side=False):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = False
    scene.render.resolution_x = scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    # Emission retains the printed artwork colors; matte stock/base are lit.
    scene.view_settings.view_transform = "Standard"
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.025, .035, .055, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .55
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.005))
    bpy.context.object.data.materials.append(solid_material("ReviewGround", (.027, .038, .055), .65))
    # Use exactly the production oval base, with the production instance tint.
    create_standee(row)
    light("Key", (3, -4, 7), 900, (1, .87, .72))
    light("Fill", (-4, -2, 5), 650, (.48, .73, 1))
    light("Rim", (1, 3, 5), 1000, (.35, .8, 1))
    data = bpy.data.cameras.new("GeometryReviewCamera")
    camera = bpy.data.objects.new("GeometryReviewCamera", data)
    bpy.context.collection.objects.link(camera)
    camera.location = (5.5, -6.5, 4.2) if side else (2.4, -8, 4.5)
    camera.rotation_euler = (Vector((0, 0, 1.55)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    data.type, data.ortho_scale = "ORTHO", 4.7
    scene.camera = camera
    output.mkdir(parents=True, exist_ok=True)
    path = output / (row["id"] + ("-side" if side else "") + ".png")
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    return path


def render_gallery(rows, output, resolution, samples):
    # A single 3D scene containing the actual meshes, avoiding image compositing
    # or changes to the source cutouts. This is a geometry inspection gallery.
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = False
    scene.render.resolution_x = max(1600, resolution * 2)
    scene.render.resolution_y = int(scene.render.resolution_x * .80)
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.025, .035, .055, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .65
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.005))
    bpy.context.object.data.materials.append(solid_material("ReviewGalleryFloor", (.027, .038, .055), .8))
    columns = 5
    row_count = (len(rows) + columns - 1) // columns
    for index, row in enumerate(rows):
        col, number = index % columns, index // columns
        position = ((col - (columns - 1) / 2) * 3.6, ((row_count - 1) / 2 - number) * 5.4, 0)
        create_standee(row, position)
        data = bpy.data.curves.new("CardId_" + row["id"], "FONT")
        data.body = row["id"]
        data.align_x, data.size = "CENTER", .23
        text = bpy.data.objects.new("CardId_" + row["id"], data)
        bpy.context.collection.objects.link(text)
        text.location = (position[0], position[1] - 1.5, .015)
        # Text lies facing up on the review floor toward the camera.
        text.rotation_euler = (0, 0, 0)
        data.materials.append(solid_material("Label", (.75, .85, .95)))
    light("GalleryKey", (0, -4, 18), 5500, (1, .9, .8), (0, 0, 0))
    light("GalleryFill", (-12, 0, 12), 4000, (.5, .75, 1), (0, 0, 0))
    data = bpy.data.cameras.new("GalleryReviewCamera")
    camera = bpy.data.objects.new("GalleryReviewCamera", data)
    bpy.context.collection.objects.link(camera)
    camera.location = (0, -30, 25)
    camera.rotation_euler = (Vector((0, 0, 1.3)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    data.type, data.ortho_scale = "ORTHO", 21
    scene.camera = camera
    output.mkdir(parents=True, exist_ok=True)
    path = output / "all-available-standees.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    return path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--standees", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--id", default="")
    parser.add_argument("--resolution", type=int, default=800)
    parser.add_argument("--samples", type=int, default=48)
    parser.add_argument("--side", action="store_true")
    parser.add_argument("--gallery", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    payload = json.loads(args.standees.read_text())
    rows = [row for row in payload["standees"] if not args.id or row["id"] == args.id]
    if not rows:
        raise SystemExit("No actual cutout mesh matches the requested ID.")
    rendered = [str(render_gallery(rows, args.output, args.resolution, args.samples))] if args.gallery else [
        str(render(row, args.output, args.resolution, args.samples, args.side)) for row in rows]
    (args.output / "review-manifest.json").write_text(json.dumps({
        "source": str(args.standees), "renderer": "Blender Cycles CPU", "unityRenderingVerified": False,
        "notes": "Production mesh/UV with the unedited source cutout; parent field transform zeroed for inspection.",
        "renders": rendered}, indent=2))


if __name__ == "__main__":
    main()
