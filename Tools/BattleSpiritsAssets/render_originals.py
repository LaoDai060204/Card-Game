#!/usr/bin/env python3
"""Render original low-poly Spirit figures and a menu arena using Blender.

This script builds its own geometry and materials. It does not extract models,
remove backgrounds from official card art, or claim official Bandai 3D models.
Run: blender --background --python render_originals.py -- --project /path/to/Card-Game
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from math import radians, sin, cos, pi


def xyz(value):
    """The gameplay figures use Unity's Y-up convention; Blender uses Z-up."""
    return Vector((value[0], value[2], value[1]))


def material(name, rgb, metallic=0.0, roughness=0.45, emission=0.0):
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*rgb, 1)
    result.use_nodes = True
    surface = result.node_tree.nodes.get('Principled BSDF')
    surface.inputs['Base Color'].default_value = (*rgb, 1)
    surface.inputs['Metallic'].default_value = metallic
    surface.inputs['Roughness'].default_value = roughness
    surface.inputs['Emission Color'].default_value = (*rgb, 1)
    surface.inputs['Emission Strength'].default_value = emission
    return result


def empty(name, location=(0, 0, 0), parent=None):
    result = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(result)
    result.parent = parent
    result.location = xyz(location)
    return result


def shape(name, kind, location, scale, surface, parent=None, bevel=False):
    if kind == 'cube':
        bpy.ops.mesh.primitive_cube_add(size=1)
    elif kind == 'ico':
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.5)
    elif kind == 'cylinder':
        bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.5, depth=1)
    elif kind == 'cone':
        bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=0.5, radius2=0, depth=1)
    else:
        raise ValueError(kind)
    result = bpy.context.object
    result.name = name
    result.parent = parent
    result.location = xyz(location)
    result.scale = xyz(scale)
    result.data.materials.append(surface)
    if bevel:
        modifier = result.modifiers.new('Edge glints', 'BEVEL')
        modifier.width = 0.075
        modifier.segments = 2
        modifier = result.modifiers.new('Weighted corner normals', 'WEIGHTED_NORMAL')
        modifier.keep_sharp = True
    return result


def rod(name, start, end, radius, surface, parent=None):
    direction = xyz(end) - xyz(start)
    result = shape(name, 'cylinder', (0, 0, 0), (1, 1, 1), surface, parent)
    result.location = (xyz(start) + xyz(end)) * 0.5
    result.rotation_mode = 'QUATERNION'
    result.rotation_quaternion = direction.to_track_quat('Z', 'Y')
    result.scale = (radius * 2, radius * 2, direction.length)
    return result


def cone_between(name, start, end, radius, surface, parent=None):
    direction = xyz(end) - xyz(start)
    result = shape(name, 'cone', (0, 0, 0), (1, 1, 1), surface, parent)
    result.location = (xyz(start) + xyz(end)) * 0.5
    result.rotation_mode = 'QUATERNION'
    result.rotation_quaternion = direction.to_track_quat('Z', 'Y')
    result.scale = (radius * 2, radius * 2, direction.length)
    return result


def wing(parent, side, machine, skin, armor, membrane, energy):
    result = empty(('Left' if side < 0 else 'Right') + ' wing joint', (side * 0.38, 1.65, -0.18), parent)
    points = [(0, 0, 0), (side * 0.95, 0.92, -0.15), (side * 2.65, 1.55, -0.5),
              (side * 2.18, -0.26, -0.85), (side * 1.45, -0.08, -0.8), (side * 0.48, -0.58, -0.35)]
    mesh = bpy.data.meshes.new('Original segmented wing membrane')
    mesh.from_pydata([xyz(point) for point in points], [], [(0, 1, 5), (1, 2, 3), (1, 3, 4), (1, 4, 5)])
    mesh.materials.append(armor if machine else membrane)
    web = bpy.data.objects.new('Armor wing' if machine else 'Crimson wing membrane', mesh)
    bpy.context.collection.objects.link(web)
    web.parent = result
    modifier = web.modifiers.new('Wing thickness', 'SOLIDIFY')
    modifier.thickness = 0.045
    for start, end in [(points[0], points[1]), (points[1], points[2]), (points[1], points[3]),
                       (points[1], points[4]), (points[1], points[5]), (points[0], points[5])]:
        rod('Wing gold skeleton', start, end, 0.035 if machine else 0.045, armor, result)
    if machine:
        for blade in range(4):
            start = (side * (0.95 + blade * 0.38), 0.55 + blade * 0.15, -0.25)
            end = (side * (1.3 + blade * 0.42), -0.34 + blade * 0.04, -0.9)
            cone_between('Valkyrius feather blade', start, end, 0.12, skin, result)
            rod('Wing energy conduit', start, end, 0.018, energy, result)
    else:
        cone_between('Wingtip horn', points[2], (side * 2.95, 1.88, -0.58), 0.095, armor, result)
    return result


def figure(card_id, machine, palette):
    skin, armor, membrane, energy, dark = palette
    root = empty(('White armored guardian' if machine else 'Red dragon emperor') + ' original fan figure')
    shape('Chest', 'cube' if machine else 'ico', (0, 1.35, 0), (0.95, 1.3, 1.2), skin, root, machine)
    shape('Belly plate', 'cube', (0, 1.26, 0.45), (0.75, 0.8, 0.22), armor, root, True)
    shape('Chest core', 'ico', (0, 1.5, 0.6), (0.24, 0.35, 0.16), energy, root)
    rod('Neck', (0, 1.7, 0.18), (0, 2.12, 0.72), 0.24, skin, root)
    head = empty('Head joint', (0, 2.23, 0.8), root)
    shape('Angular helm' if machine else 'Dragon skull', 'cube' if machine else 'ico', (0, 0, 0), (0.65, 0.5, 0.7), skin, head, machine)
    shape('Upper muzzle', 'cube', (0, -0.05, 0.4), (0.43, 0.21, 0.6), armor, head, True)
    shape('Lower jaw', 'cube', (0, -0.21, 0.33), (0.37, 0.11, 0.55), skin, head, True)
    shape('Mouth shadow', 'cube', (0, -0.14, 0.49), (0.36, 0.065, 0.4), dark, head)
    for side in (-1, 1):
        shape('Eye lens', 'ico', (side * 0.29, 0.075, 0.22), (0.095, 0.1, 0.18), energy, head)
        cone_between('Crown horn', (side * 0.19, 0.18, -0.18), (side * 0.45, 0.88, -0.49), 0.12, armor, head)
        shape('Jaw jewel', 'ico', (side * 0.3, -0.14, -0.02), (0.16, 0.17, 0.25), armor, head)
        shape('Hind thigh', 'cube' if machine else 'ico', (side * 0.5, 0.73, -0.3), (0.5, 0.8, 0.64), skin, root, machine)
        rod('Armored shin', (side * 0.54, 0.55, -0.25), (side * 0.68, 0.18, 0.2), 0.15, armor, root)
        shape('Planted foot', 'cube', (side * 0.68, 0.13, 0.4), (0.4, 0.23, 0.72), skin, root, True)
        rod('Forearm', (side * 0.48, 1.58, 0.28), (side * 0.85, 1.05, 0.8), 0.13, skin, root)
        shape('Gauntlet', 'cube' if machine else 'ico', (side * 0.87, 1.01, 0.83), (0.27, 0.28, 0.3), armor, root, machine)
        for index in range(3):
            x = side * 0.87 + (index - 1) * 0.09
            cone_between('Hand talon', (x, 0.96, 0.9), (x, 0.84, 1.2), 0.042, armor, root)
            cone_between('Foot talon', (side * 0.68 + (index - 1) * 0.13, 0.13, 0.66),
                         (side * 0.68 + (index - 1) * 0.14, 0.06, 0.94), 0.052, armor, root)
    wings = [wing(root, side, machine, skin, armor, membrane, energy) for side in (-1, 1)]
    tail = empty('Tail joint', (0, 1.02, -0.65), root)
    rod('Tail base', (0, 0, 0), (0.1, -0.32, -0.9), 0.23, skin, tail)
    rod('Tail blade', (0.1, -0.32, -0.9), (0.65, -0.12, -1.75), 0.11, armor, tail)
    cone_between('Tail fin', (0.65, -0.12, -1.75), (0.92, 0.14, -2.1), 0.18, armor, tail)
    for index in range(5):
        cone_between('Dorsal plate', (0, 1.8 - index * 0.17, -0.1 - index * 0.22),
                     (0, 2.15 - index * 0.19, -0.25 - index * 0.22), 0.12, armor, root)
    if machine:
        cone_between('Valkyrie crown', (0, 2.42, 0.72), (0, 3.1, 0.6), 0.13, energy, root)
        for side in (-1, 1):
            rod('Shoulder energy arch', (side * 0.6, 1.7, -0.2), (side * 1.05, 2.2, -0.65), 0.06, energy, root)
    # The .blend source carries editable motion examples. Runtime uses its
    # frame-rate-independent C# curves rather than importing a copied clip.
    for joint, direction in zip(wings, (-1, 1)):
        joint.rotation_mode = 'XYZ'
        for frame, degrees in [(1, 0), (12, direction * 20), (24, 0), (36, -direction * 15), (48, 0)]:
            joint.rotation_euler[1] = radians(degrees)
            joint.keyframe_insert(data_path='rotation_euler', frame=frame, group='Original idle wing beat')
        joint.animation_data.action.name = card_id + ' original idle wing beat'
    for frame, height in [(1, 1.2), (12, 0.6), (24, 0), (48, 0)]:
        root.location.z = height
        root.keyframe_insert(data_path='location', frame=frame, group='Original summon descent')
    root.animation_data.action.name = card_id + ' original summon descent'
    root.location = (0, 0, 0)
    return root


def descendants(root):
    return [root, *root.children_recursive]


def visibility(root, hidden):
    for item in descendants(root):
        item.hide_render = hidden


def field(palette):
    stone, edge, gold, core, soul = palette
    result = empty('Original Eternal six-color arena')
    shape('Arena base', 'cube', (0, -0.4, 0), (35, 0.8, 27), stone, result, True)
    for side in (-1, 1):
        shape('Cyan end rail', 'cube', (0, 0.05, side * 13), (34, 0.1, 0.08), edge, result)
        shape('Cyan side rail', 'cube', (side * 17, 0.05, 0), (0.08, 0.1, 26), edge, result)
        for z, length, depth in [(3.8, 23, 4.8), (7.4, 18, 1.8)]:
            shape('Spirit or Nexus border', 'cube', (0, 0.015, side * z), (length, 0.05, depth), edge, result)
            shape('Open field inset', 'cube', (0, 0.05, side * z), (length - 0.1, 0.05, depth - 0.1), stone, result)
        for x, z in [(-13, 9), (-5.5, 10.5), (5.5, 10.5), (13, 9)]:
            shape('Core pool', 'cylinder', (x, 0.13, side * z), (3.4, 0.12, 3.4), gold if x == -13 else stone, result)
            for count in range(3 if x != 13 else 0):
                item = shape('Blue core crystal', 'ico', (x + (count - 1) * 0.52, 0.52, side * z), (0.38, 0.7, 0.38), core, result)
                item.rotation_euler[2] = radians(45)
    colors = [(0.6, 0.025, 0.04), (0.26, 0.03, 0.5), (0.05, 0.4, 0.15),
              (0.75, 0.84, 0.9), (0.9, 0.62, 0.06), (0.04, 0.2, 0.8)]
    for index, color in enumerate(colors):
        theta = index * pi / 3
        shape('Six-color symbol', 'ico', (sin(theta) * 1.2, 0.26, cos(theta) * 0.65),
              (0.4, 0.28, 0.4), material('Original color emblem ' + str(index), color, emission=0.3), result)
    return result


def area(name, position, target, power, color, size):
    source = bpy.data.lights.new(name, 'AREA')
    source.energy = power
    source.color = color
    source.shape = 'DISK'
    source.size = size
    obj = bpy.data.objects.new(name, source)
    bpy.context.collection.objects.link(obj)
    obj.location = position
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--samples', type=int, default=48)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    root = args.project.resolve()
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = args.samples
    # Some portable Blender builds omit OpenImageDenoise. The source render is
    # deliberately independent of that optional native library.
    scene.cycles.use_denoising = False
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = 4
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.image_settings.color_depth = '8'
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'AgX'
    scene.world.color = (0.015, 0.02, 0.035)
    scene.render.fps = 24
    scene.frame_start, scene.frame_end = 1, 48
    red = material('Original crimson scales', (0.48, 0.018, 0.027), 0.28, 0.4)
    gold = material('Original warm gold armor', (0.76, 0.41, 0.075), 0.72, 0.28)
    wine = material('Original wine wing membrane', (0.12, 0.008, 0.025), 0.12, 0.5)
    fire = material('Original amber dragon core', (1, 0.25, 0.015), 0.2, 0.25, 3.0)
    dark = material('Dark insets', (0.008, 0.014, 0.025), 0.15, 0.65)
    white = material('Original ivory steel armor', (0.62, 0.75, 0.82), 0.65, 0.25)
    silver = material('Original wing silver', (0.22, 0.38, 0.49), 0.8, 0.25)
    ice = material('Original ice energy', (0.035, 0.5, 0.8), 0.2, 0.2, 3.0)
    cyan = material('Arena cyan rails', (0.01, 0.27, 0.32), 0.55, 0.3, 1.1)
    red_figure = figure('BS01-X01', False, (red, gold, wine, fire, dark))
    white_figure = figure('BS01-097', True, (white, gold, silver, ice, dark))
    arena = field((dark, cyan, gold, ice, fire))
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    scene.camera = camera
    camera.data.type = 'ORTHO'
    camera.data.lens = 50
    area('Warm key', (5, 8, 9), (0, 0, 1), 1300, (1, 0.79, 0.6), 5)
    area('Cool front fill', (-5, 5, 4), (0, 0, 1), 950, (0.4, 0.72, 1), 5)
    area('Blue rear rim', (0, -6, 7), (0, 0, 1.3), 1900, (0.15, 0.55, 1), 4)
    area('Top softbox', (0, 0, 10), (0, 0, 0), 550, (1, 1, 1), 5)
    output = []
    scene.frame_set(24)
    visibility(arena, True)
    scene.render.film_transparent = True
    for card_id, current, other in [('BS01-X01', red_figure, white_figure), ('BS01-097', white_figure, red_figure)]:
        visibility(current, False)
        visibility(other, True)
        current.location = (0, 0, 0)
        current.scale = (1, 1, 1)
        camera.location = (6.5, 10, 5.6)
        camera.rotation_euler = (Vector((0, 0, 1.65)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
        camera.data.ortho_scale = 6.5
        scene.render.resolution_x = scene.render.resolution_y = 1024
        path = root / 'Picture/Closeup' / (card_id + '.png')
        path.parent.mkdir(parents=True, exist_ok=True)
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        output.append({'path': str(path.relative_to(root)), 'cardId': card_id, 'kind': 'transparent original 3D render', 'width': 1024, 'height': 1024})
    visibility(arena, False)
    visibility(red_figure, False)
    visibility(white_figure, True)
    red_figure.location = xyz((3.7, 0.14, -0.4))
    red_figure.scale = (2.2, 2.2, 2.2)
    scene.render.film_transparent = False
    camera.location = (11, 16, 12)
    camera.rotation_euler = (Vector((1.5, 0, 1.6)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.ortho_scale = 20
    scene.render.resolution_x, scene.render.resolution_y = 1920, 1080
    path = root / 'Picture/Art/bs-menu.png'
    path.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    output.append({'path': str(path.relative_to(root)), 'kind': 'original 3D arena/menu render', 'width': 1920, 'height': 1080})
    source = root / 'Tools/BattleSpiritsAssets/Source/OriginalSpirits.blend'
    source.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    for entry in output:
        entry['sha256'] = hashlib.sha256((root / entry['path']).read_bytes()).hexdigest()
    manifest = {'version': 1, 'creator': 'LaoDai060204 project original procedural geometry',
                'source': 'Tools/BattleSpiritsAssets/render_originals.py', 'license': 'CC0-1.0 for original meshes/rendering code and generated renders',
                'notice': 'Original fan interpretations assigned to game card IDs. These are not official Bandai models, screenshots, or edits of official card artwork. Battle Spirits names and card art retain their respective owners.',
                'sourceBlend': str(source.relative_to(root)), 'sourceBlendSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                'blenderVersion': bpy.app.version_string, 'renderer': 'Cycles CPU', 'samples': args.samples,
                'unityPlayOrRenderingTested': False, 'files': output}
    destination = root / 'Tools/BattleSpiritsAssets/visual-manifest.json'
    destination.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'rendered': len(output), 'manifest': str(destination)}, indent=2))


if __name__ == '__main__':
    main()
