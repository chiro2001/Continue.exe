#!/usr/bin/env python3
"""Build the V5 tree-shaped shade canopy and recover Room01 shelf maps.

Run with Blender 4.2+ in background mode:
  blender --factory-startup --background --python tools/build_fullfilm_v5_models.py -- \
    --output art/models/fullfilm-v5 \
    --texture-output art/export/fullfilm-v5/model-textures \
    --shelf-source-fbx 'Assets/Room01/Scene/model/Room01.fbx' \
    --manifest data/fullfilm-v5-model-assets.json --render

The model is authored in metres, Blender Z-up, with +Y toward the viewer. The
exported canopy is an independent replacement child for the existing
BranchCanopy pivot; it does not include the neighboring planter or world mesh.
"""
import argparse
import hashlib
import json
import math
import os
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


def parse_args():
    p = argparse.ArgumentParser()
    p.add_argument('--output', required=True, help='Model output directory')
    p.add_argument('--texture-output', required=True, help='Recovered Room01 shelf textures')
    p.add_argument('--shelf-source-fbx', required=True, help='Existing imported Room01 FBX; read only')
    p.add_argument('--manifest', required=True, help='V5 JSON manifest output path')
    p.add_argument('--room01-recovery-manifest', help='Detailed source material mapping JSON path')
    p.add_argument('--render', action='store_true')
    return p.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])


OPT = parse_args()
OUT = Path(OPT.output).resolve()
TEXTURE_OUT = Path(OPT.texture_output).resolve()
MANIFEST_PATH = Path(OPT.manifest).resolve()
ROOM01_RECOVERY_PATH = (Path(OPT.room01_recovery_manifest).resolve() if OPT.room01_recovery_manifest
                        else MANIFEST_PATH.parent/'fullfilm-v5-room01-material-recovery.json')
OUT.mkdir(parents=True, exist_ok=True)
TEXTURE_OUT.mkdir(parents=True, exist_ok=True)
MANIFEST_PATH.parent.mkdir(parents=True, exist_ok=True)
MAT = {}
ASSET_STEM = 'fullfilm_v5_branch_canopy'


def sha256(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def material(name, rgb, roughness=.55, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1.0)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*rgb, 1.0)
    p.inputs['Roughness'].default_value = roughness
    p.inputs['Metallic'].default_value = metallic
    MAT[name] = m
    return m


def palette():
    material('M_FFV5_TrunkTimber', (.255, .145, .083), .62)
    material('M_FFV5_BranchTimber', (.39, .235, .126), .58)
    material('M_FFV5_EndGrain', (.52, .335, .19), .7)
    material('M_FFV5_CanopySage', (.025, .085, .035), .84)
    material('M_FFV5_CanopyLightSage', (.05, .14, .06), .84)
    material('M_FFV5_CanopyUnderside', (.085, .17, .08), .86)
    material('M_FFV5_CanopyUndersideLight', (.12, .22, .105), .86)
    material('M_FFV5_CanopyBinding', (.012, .045, .018), .86)
    material('M_FFV5_JoineryBronze', (.16, .095, .035), .52, .34)


def empty(name, parent):
    o = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(o)
    o.parent = parent
    o.empty_display_type = 'PLAIN_AXES'
    o.empty_display_size = .09
    return o


def set_uv(mesh_data, face_uvs, name='SurfaceUV'):
    layer = mesh_data.uv_layers.new(name=name)
    for poly, coords in zip(mesh_data.polygons, face_uvs):
        for loop_index, uv in zip(poly.loop_indices, coords):
            layer.data[loop_index].uv = uv


def mesh_object(name, verts, faces, parent, mat, face_uvs=None, material_slots=None, face_material_indices=None):
    d = bpy.data.meshes.new(name + '_Mesh')
    d.from_pydata(verts, [], faces)
    d.update()
    o = bpy.data.objects.new(name, d)
    bpy.context.collection.objects.link(o)
    o.parent = parent
    if material_slots:
        for item in material_slots:
            d.materials.append(item)
    elif mat:
        d.materials.append(mat)
    if face_material_indices:
        for poly, index in zip(d.polygons, face_material_indices):
            poly.material_index = index
    if face_uvs:
        set_uv(d, face_uvs)
    else:
        # Planar object-space UVs keep every static part paint-ready without
        # changing the authored silhouette or adding image dependencies.
        uv = d.uv_layers.new(name='SurfaceUV')
        for poly in d.polygons:
            normal = poly.normal
            axis = max(range(3), key=lambda i: abs(normal[i]))
            axes = [i for i in range(3) if i != axis]
            for li in poly.loop_indices:
                co = d.vertices[d.loops[li].vertex_index].co
                uv.data[li].uv = (co[axes[0]], co[axes[1]])
    for poly in d.polygons:
        poly.use_smooth = False
    o['static_source_mesh'] = True
    o['uv_policy'] = 'SurfaceUV authored in Blender local metres'
    return o


def tube_mesh(name, controls, radii, parent, mat, sides=12, steps_per_curve=4):
    """Create a tapered, capped timber member following cubic control points."""
    controls = [Vector(p) for p in controls]
    assert len(controls) == 4 and len(radii) == 2
    ring_centres = []
    ring_radii = []
    steps = max(4, steps_per_curve)
    for i in range(steps + 1):
        t = i / steps
        s = 1.0 - t
        c = controls[0] * (s*s*s) + controls[1] * (3*s*s*t) + controls[2] * (3*s*t*t) + controls[3] * (t*t*t)
        ring_centres.append(c)
        ring_radii.append(radii[0] * (1.0 - t) + radii[1] * t)
    verts = []
    for i, center in enumerate(ring_centres):
        tangent = (ring_centres[min(i + 1, steps)] - ring_centres[max(i - 1, 0)]).normalized()
        ref = Vector((0, 0, 1)) if abs(tangent.z) < .91 else Vector((0, 1, 0))
        u = tangent.cross(ref).normalized()
        v = tangent.cross(u).normalized()
        for j in range(sides):
            a = 2.0 * math.pi * j / sides
            verts.append(center + ring_radii[i] * (math.cos(a) * u + math.sin(a) * v))
    faces = []
    face_uvs = []
    for i in range(steps):
        for j in range(sides):
            a = i * sides + j
            b = i * sides + (j + 1) % sides
            c = (i + 1) * sides + (j + 1) % sides
            d = (i + 1) * sides + j
            faces.append((a, b, c, d))
            j_next = j + 1
            face_uvs.append(((j/sides, i/steps), (j_next/sides, i/steps),
                             (j_next/sides, (i+1)/steps), (j/sides, (i+1)/steps)))
    faces.append(tuple(reversed(tuple(range(sides)))))
    face_uvs.append(tuple((.5 + .5*math.cos(2*math.pi*j/sides), .5 + .5*math.sin(2*math.pi*j/sides)) for j in reversed(range(sides))))
    last = steps * sides
    faces.append(tuple(last + j for j in range(sides)))
    face_uvs.append(tuple((.5 + .5*math.cos(2*math.pi*j/sides), .5 + .5*math.sin(2*math.pi*j/sides)) for j in range(sides)))
    return mesh_object(name, verts, faces, parent, mat, face_uvs)


def torus(name, center, major_radius, minor_radius, parent, mat, axis=(0, 0, 1), major_segments=36, minor_segments=8):
    axis = Vector(axis).normalized()
    ref = Vector((0, 0, 1)) if abs(axis.z) < .91 else Vector((0, 1, 0))
    u = axis.cross(ref).normalized()
    v = axis.cross(u).normalized()
    center = Vector(center)
    verts = []
    faces = []
    face_uvs = []
    for i in range(major_segments):
        a = 2 * math.pi * i / major_segments
        radial = math.cos(a) * u + math.sin(a) * v
        for j in range(minor_segments):
            b = 2 * math.pi * j / minor_segments
            verts.append(center + (major_radius + minor_radius * math.cos(b)) * radial + minor_radius * math.sin(b) * axis)
    for i in range(major_segments):
        for j in range(minor_segments):
            a = i * minor_segments + j
            b = i * minor_segments + (j + 1) % minor_segments
            c = ((i + 1) % major_segments) * minor_segments + (j + 1) % minor_segments
            d = ((i + 1) % major_segments) * minor_segments + j
            faces.append((a, b, c, d))
            face_uvs.append(((i/major_segments, j/minor_segments),
                             (i/major_segments, (j+1)/minor_segments),
                             ((i+1)/major_segments, (j+1)/minor_segments),
                             ((i+1)/major_segments, j/minor_segments)))
    return mesh_object(name, verts, faces, parent, mat, face_uvs)


def line_curve(name, p0, p1, parent, mat, radius, bend=None, end_radius=None, sides=10):
    p0, p1 = Vector(p0), Vector(p1)
    delta = p1 - p0
    if bend is None:
        c1, c2 = p0 + delta * .34, p0 + delta * .68
    else:
        c1, c2 = Vector(bend[0]), Vector(bend[1])
    return tube_mesh(name, [p0, c1, c2, p1], [radius, end_radius if end_radius is not None else radius], parent, mat, sides=sides)


def polar(radius, angle, z):
    return Vector((radius * math.cos(angle), radius * math.sin(angle), z))


def canopy_z(q, lateral):
    # A shallow, continuous crown with a modest droop at each cloth panel corner.
    return 2.94 - .28*q - .035*math.sin(math.pi*q) - .075*(lateral*lateral)*q + .025*(math.cos(lateral*math.pi/2)**4)*q


def canopy_panel(name, sector, parent, material_index):
    radial_divisions = 10
    angular_divisions = 10
    inner, outer = .16, 1.80
    center_angle = math.radians(30 + sector * 60)
    half_angle = math.radians(30)
    count = (radial_divisions + 1) * (angular_divisions + 1)
    verts = []
    top_uv = []
    for i in range(radial_divisions + 1):
        q = i / radial_divisions
        for j in range(angular_divisions + 1):
            u = j / angular_divisions
            lateral = 2*u - 1
            angle = center_angle + (2*u - 1) * half_angle
            # A shallow six-lobed perimeter reads as a crown of cloth leaves,
            # while each panel still meets its neighbors at the same eave seam.
            outer_at_u = outer + .07 * (math.cos(lateral*math.pi/2)**2)
            r = inner + (outer_at_u-inner) * q
            z = canopy_z(q, lateral)
            verts.append((r*math.cos(angle), r*math.sin(angle), z))
            top_uv.append((u, q))
    for i in range(radial_divisions + 1):
        for j in range(angular_divisions + 1):
            x, y, z = verts[i*(angular_divisions+1)+j]
            verts.append((x, y, z - .018))
    faces = []
    face_uvs = []
    face_material_indices = []
    for i in range(radial_divisions):
        for j in range(angular_divisions):
            a = i*(angular_divisions+1)+j
            b = a+1
            c = (i+1)*(angular_divisions+1)+j
            d = c+1
            # Radial first, then angular, yields an upward-facing top surface.
            faces.append((a, c, d, b))
            face_uvs.append((top_uv[a], top_uv[c], top_uv[d], top_uv[b]))
            face_material_indices.append(material_index)
            off = count
            faces.append((off+a, off+b, off+d, off+c))
            face_uvs.append((top_uv[a], top_uv[b], top_uv[d], top_uv[c]))
            face_material_indices.append(material_index + 2)
    mesh = mesh_object(name, verts, faces, parent, None, face_uvs,
                       material_slots=[MAT['M_FFV5_CanopySage'], MAT['M_FFV5_CanopyLightSage'],
                                       MAT['M_FFV5_CanopyUnderside'], MAT['M_FFV5_CanopyUndersideLight']],
                       face_material_indices=face_material_indices)
    mesh['purpose'] = 'Two-sided shaped fabric sector with a contrasting underside lining'
    mesh['uv_policy'] = 'Cloth panel UV 0..1 per sector; repeat not required'
    return mesh


def build_model():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    palette()
    root = empty('BranchCanopy_Refined', None)
    root['integration_parent'] = 'Existing BranchCanopy pivot at Blender local (0.95, -0.35, 0.0)'
    root['purpose'] = 'Tree-shaped timber shade pavilion: tapered trunk, three Y-fork supports, six radial ribs, six sewn cloth crown panels'
    root['units'] = 'metres; X right, Z up, +Y front'

    trunk = empty('Timber trunk and buttress roots', root)
    primary = empty('Three primary Y supports', root)
    secondary = empty('Six tapered branch arms', root)
    crown = empty('Radial crown frame and joinery', root)
    cloth = empty('Six-segment cloth crown', root)

    tube_mesh('Trunk tapered main mast', [(-.035, .015, .29), (-.025, .010, .98), (-.075, .025, 1.72), (-.025, .015, 2.78)], [.155, .073], trunk, MAT['M_FFV5_TrunkTimber'], sides=16, steps_per_curve=16)
    # Four short buttress roots remain inside the existing 0.41 m planter.
    for i in range(4):
        a = math.radians(45 + i*90)
        p0 = polar(.055, a, .57) + Vector((-.06, .02, 0))
        p1 = polar(.32, a, .34)
        d = p1-p0
        tube_mesh('Buttress root %02d'%i, [p0, p0+d*.28+Vector((0,0,-.025)), p0+d*.70+Vector((0,0,-.02)), p1], [.060, .018], trunk, MAT['M_FFV5_BranchTimber'], sides=10, steps_per_curve=7)
    for z, radius, band in [(.41, .16, .021), (.54, .145, .012), (1.45, .115, .012)]:
        torus('Trunk turned collar %.2f'%z, (-.04, .015, z), radius, band, trunk, MAT['M_FFV5_EndGrain'], major_segments=32)

    primary_angles = [math.radians(60 + i*120) for i in range(3)]
    tip_angles = []
    for i, a in enumerate(primary_angles):
        start = Vector((-.065, .02, 1.42))
        joint = polar(.78, a, 2.09)
        delta = joint-start
        tube_mesh('Primary branch %02d'%i,
                  [start, start+Vector((math.cos(a)*.18, math.sin(a)*.18, .27)),
                   joint-delta*.22+Vector((0,0,.16)), joint], [.112, .073], primary,
                  MAT['M_FFV5_BranchTimber'], sides=12, steps_per_curve=10)
        direction = (joint-start).normalized()
        torus('Bronze primary joint ferrule %02d'%i, joint, .083, .018, primary,
              MAT['M_FFV5_JoineryBronze'], axis=direction, major_segments=24, minor_segments=8)
        # Two measured secondary arms form a clear fork at each primary branch.
        for side in (-1, 1):
            tip_angle = a + math.radians(side*30)
            tip_angles.append(tip_angle)
            tip = polar(1.57, tip_angle, 2.53)
            d = tip-joint
            tube_mesh('Forked crown branch %02d_%s'%(i, 'L' if side<0 else 'R'),
                      [joint, joint+d*.24+Vector((0,0,.07)), joint+d*.72+Vector((0,0,.04)), tip],
                      [.073, .038], secondary, MAT['M_FFV5_BranchTimber'], sides=10, steps_per_curve=10)
            cap_axis = d.normalized()
            toe_q = (1.57-.16)/(1.80-.16)
            shoe_center = tip-d.normalized()*.07
            shoe_center.z = canopy_z(toe_q, 0)-.15
            torus('Bronze branch-tip shoe %02d_%s'%(i, 'L' if side<0 else 'R'), shoe_center,
                  .044, .012, secondary, MAT['M_FFV5_JoineryBronze'], axis=cap_axis,
                  major_segments=20, minor_segments=8)

    hub = Vector((-.025, .015, 2.89))
    for i, a in enumerate(tip_angles):
        edge = polar(1.87, a, canopy_z(1.0, 0)-.025)
        mid = polar(1.04, a, canopy_z(.53, 0)-.095)
        start = Vector((hub.x*.25, hub.y*.25, hub.z-.105))
        line_curve('Radial roof rib %02d'%i, start, edge, crown, MAT['M_FFV5_EndGrain'], .036,
                   bend=[start.lerp(mid,.63)+Vector((0,0,.015)), mid.lerp(edge,.42),], end_radius=.016, sides=10)
        # Each fork arrives beneath the roof rib at a legible bronze saddle.
        shoe = polar(1.53, a, canopy_z((1.53-.16)/(1.80-.16), 0)-.075)
        torus('Rib saddle clamp %02d'%i, shoe, .041, .010, crown, MAT['M_FFV5_JoineryBronze'],
              axis=(math.cos(a), math.sin(a), -.12), major_segments=20, minor_segments=8)

    # Six compact ring battens connect the rafters, with a continuous eave rail.
    for ring_index, radius in enumerate((.66, 1.18, 1.78)):
        q = (radius-.16)/(1.80-.16)
        points = [polar(radius, math.radians(30+i*60), canopy_z(q, 0)-.048) for i in range(6)]
        for i in range(6):
            p0, p1 = points[i], points[(i+1)%6]
            line_curve(('Eave rail' if ring_index==2 else 'Crown cross batten %d'%ring_index)+(' %02d'%i),
                       p0, p1, crown, MAT['M_FFV5_BranchTimber'], .020 if ring_index==2 else .012,
                       end_radius=.020 if ring_index==2 else .012, sides=8)

    # The hub is intentionally visible from below and from the small-avatar view.
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=.17, location=hub)
    boss = bpy.context.object
    boss.name = 'Timber crown hub'
    boss.parent = crown
    boss.data.materials.append(MAT['M_FFV5_JoineryBronze'])
    boss['static_source_mesh'] = True
    for i in range(6):
        a = math.radians(i*60)
        p = Vector((hub.x + .12*math.cos(a), hub.y + .12*math.sin(a), hub.z+.112))
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=.018, location=p)
        bolt = bpy.context.object
        bolt.name = 'Crown hub rivet %02d'%i
        bolt.parent = crown
        bolt.data.materials.append(MAT['M_FFV5_EndGrain'])
        bolt['static_source_mesh'] = True

    for i in range(6):
        canopy_panel('Sewn crown canvas panel %02d'%i, i, cloth, i%2)
        center_angle = math.radians(30+i*60)
        # Dark stitched binding follows the six panel seams; it sits above cloth.
        p0 = polar(.17, center_angle-math.radians(30), canopy_z(0, -1)+.012)
        p1 = polar(1.80, center_angle-math.radians(30), canopy_z(1, -1)+.012)
        seam_mid1 = p0.lerp(p1,.34); seam_mid1.z += .018
        seam_mid2 = p0.lerp(p1,.70); seam_mid2.z += .008
        tube_mesh('Sewn radial canopy binding %02d'%i,
                  [p0, seam_mid1, seam_mid2, p1], [.009, .009], cloth,
                  MAT['M_FFV5_CanopyBinding'], sides=6, steps_per_curve=10)

    root['bounds_contract_m'] = 'approximately 3.60 m X x 3.74 m Y x 2.77 m tall; preserve the existing planter and parent pivot'
    return root


def object_row(o):
    row = {'name': o.name, 'parent': o.parent.name if o.parent else None, 'type': o.type,
           'pivot_local_blender_m': [round(v, 6) for v in o.location],
           'dimensions_m': [round(v, 6) for v in o.dimensions],
           'rotation_euler_deg': [round(math.degrees(v), 4) for v in o.rotation_euler]}
    if o.type == 'MESH':
        o.data.calc_loop_triangles()
        row.update({'vertices': len(o.data.vertices), 'triangles': len(o.data.loop_triangles),
                    'uv_layers': [u.name for u in o.data.uv_layers],
                    'materials': [m.name for m in o.data.materials]})
    return row


def render_preview(root, output_path, detail=False):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1100
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.film_transparent = False
    scene.render.image_settings.color_depth = '8'
    scene.view_settings.view_transform = 'AgX'
    world = bpy.data.worlds.new('V5 canopy review world')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (.09, .12, .13, 1)
    world.node_tree.nodes['Background'].inputs[1].default_value = .34
    scene.world = world
    target = (0, 0, 1.52 if not detail else 2.05)
    camera_location = (5.8, 8.4, 2.75) if not detail else (3.8, 5.5, 1.55)
    bpy.ops.object.camera_add(location=camera_location)
    camera = bpy.context.object
    camera.name = 'PREVIEW_camera'
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 4.85 if not detail else 3.25
    camera.rotation_euler = (Vector(target)-camera.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = camera
    preview_mats = []
    planter_mat = bpy.data.materials.new('PREVIEW_existing planter')
    planter_mat.diffuse_color = (.36, .15, .095, 1)
    planter_mat.use_nodes = True
    planter_mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.36, .15, .095, 1)
    planter_mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = .77
    preview_mats.append(planter_mat)
    bpy.ops.mesh.primitive_cylinder_add(vertices=40, radius=.405, depth=.32, location=(0, 0, .16))
    planter = bpy.context.object
    planter.name = 'PREVIEW_context_only_existing_planter'
    planter.data.materials.append(planter_mat)
    bevel = planter.modifiers.new('Preview planter edge', 'BEVEL')
    bevel.width = .018; bevel.segments = 2
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.008))
    ground = bpy.context.object
    ground.name = 'PREVIEW_ground'
    ground_mat = bpy.data.materials.new('PREVIEW_ground material')
    ground_mat.diffuse_color = (.12, .16, .17, 1)
    ground_mat.use_nodes = True
    ground_mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.12, .16, .17, 1)
    ground_mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = .9
    ground.data.materials.append(ground_mat)
    preview_mats.append(ground_mat)
    lights = []
    for name, loc, energy, size in [('PREVIEW_key',(3,4,7),780,4.2), ('PREVIEW_fill',(-4,1,4.8),400,3.6), ('PREVIEW_rim',(0,-4,5.5),600,3.2)]:
        bpy.ops.object.light_add(type='AREA', location=loc)
        light = bpy.context.object; light.name = name; light.data.energy = energy; light.data.shape = 'DISK'; light.data.size = size
        light.rotation_euler = (Vector(target)-light.location).to_track_quat('-Z','Y').to_euler()
        lights.append(light)
    scene.render.filepath = str(output_path)
    bpy.ops.render.render(write_still=True)
    # Preview-only context never enters the FBX, GLB, or saved Blender source.
    for obj in [camera, planter, ground] + lights:
        bpy.data.objects.remove(obj, do_unlink=True)
    for mat in preview_mats:
        if mat.users == 0: bpy.data.materials.remove(mat)
    bpy.data.worlds.remove(world)


def export_model(root):
    objects = [root] + list(root.children_recursive)
    for o in objects:
        if o.type != 'MESH':
            continue
        # Keep the deliberately opposed top/bottom windings on the two-sided
        # cloth panels. Recalculating an open pair of disconnected surfaces
        # flips both islands toward the same direction and hides the underside.
        if not o.name.startswith('Sewn crown canvas panel '):
            bm = bmesh.new(); bm.from_mesh(o.data)
            bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
            bm.to_mesh(o.data); bm.free(); o.data.update()
        o.data.calc_loop_triangles()
        if not o.data.uv_layers:
            raise RuntimeError('Missing UV layer on ' + o.name)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=str(OUT/(ASSET_STEM+'.fbx')), use_selection=True,
        object_types={'MESH','EMPTY'}, add_leaf_bones=False, bake_anim=False,
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True, global_scale=1.0,
        mesh_smooth_type='FACE', path_mode='AUTO')
    bpy.ops.export_scene.gltf(filepath=str(OUT/(ASSET_STEM+'.glb')), export_format='GLB',
        use_selection=True, export_yup=True, export_animations=False,
        export_cameras=False, export_lights=False)
    bpy.context.scene['asset_contract'] = 'Static canopy child model in local metres; preview objects excluded from exports and source blend.'


def validate_reimports(expected_meshes, expected_triangles):
    reports = []
    for extension in ('.fbx', '.glb'):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path = OUT/(ASSET_STEM+extension)
        if extension == '.fbx': bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
        else: bpy.ops.import_scene.gltf(filepath=str(path))
        meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
        tri = 0; uv_ok = True; mats = set()
        for o in meshes:
            o.data.calc_loop_triangles(); tri += len(o.data.loop_triangles)
            uv_ok = uv_ok and len(o.data.uv_layers) > 0
            mats.update(m.name for m in o.data.materials if m)
        report = {'format': extension[1:], 'mesh_count': len(meshes), 'triangles': tri,
                  'all_meshes_have_uv': uv_ok, 'material_names': sorted(mats),
                  'cameras': sum(o.type=='CAMERA' for o in bpy.context.scene.objects),
                  'lights': sum(o.type=='LIGHT' for o in bpy.context.scene.objects)}
        report['passed'] = len(meshes)==expected_meshes and tri==expected_triangles and uv_ok and report['cameras']==0 and report['lights']==0
        if not report['passed']:
            raise RuntimeError('Export reimport check failed: ' + json.dumps(report, ensure_ascii=False))
        reports.append(report)
    return reports


def extract_source_textures():
    """Recover the packed image files from the user-owned Room01 FBX, without editing it."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(Path(OPT.shelf_source_fbx).resolve()), use_anim=False)
    shelf = next((o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.lower()=='living_shelf'), None)
    if shelf is None:
        raise RuntimeError('Source FBX has no Living_Shelf mesh')
    slot = next((s for s in shelf.material_slots if s.name.lower()=='shelf' and s.material), None)
    if slot is None or not slot.material.use_nodes:
        raise RuntimeError('Living_Shelf has no node material in slot named shelf')
    expected = {
        'Wood017_2K-JPG_Color.jpg': 'archive-shelf-wood017-albedo.jpg',
        'Wood017_2K-JPG_NormalGL.jpg': 'archive-shelf-wood017-normal-gl.jpg',
        'Wood017_2K-JPG_Roughness.jpg': 'archive-shelf-wood017-roughness.jpg',
    }
    found = {}
    for node in slot.material.node_tree.nodes:
        if node.type=='TEX_IMAGE' and node.image and node.image.name in expected:
            found[node.image.name] = node.image
    missing = set(expected)-set(found)
    if missing:
        raise RuntimeError('Embedded shelf maps missing: ' + ', '.join(sorted(missing)))
    rows = []
    paths = {}
    for source_name, output_name in expected.items():
        image = found[source_name]
        output = TEXTURE_OUT/output_name
        if image.packed_file:
            data = bytes(image.packed_file.data)
        else:
            source_path = Path(bpy.path.abspath(image.filepath))
            if not source_path.is_file():
                raise RuntimeError('Shelf source image is not packed and file is missing: '+str(source_path))
            data = source_path.read_bytes()
        if source_name.endswith('.jpg') and not data.startswith(b'\xff\xd8'):
            raise RuntimeError('Expected original JPEG bytes for '+source_name)
        output.write_bytes(data)
        relative = 'art/export/fullfilm-v5/model-textures/'+output_name
        paths[source_name] = output
        rows.append({'source_image':source_name, 'output':relative, 'embedded_in_source_fbx':bool(image.packed_file),
                     'width':int(image.size[0]), 'height':int(image.size[1]), 'source_format':image.file_format,
                     'source_color_space':image.colorspace_settings.name, 'sha256':sha256(output)})
    # Unity Standard expects smoothness in the metallic map alpha channel. The
    # shelf is non-metallic, so export black RGB and (1 - roughness) in alpha.
    import numpy as np
    rough = found['Wood017_2K-JPG_Roughness.jpg']
    rough.colorspace_settings.name = 'Non-Color'
    width, height = int(rough.size[0]), int(rough.size[1])
    pixels = np.empty(width*height*4, dtype=np.float32)
    rough.pixels.foreach_get(pixels)
    rgba = np.zeros((height, width, 4), dtype=np.float32)
    rgba[:, :, 3] = 1.0 - pixels.reshape((height, width, 4))[:, :, 0]
    smoothness = bpy.data.images.new('Archive shelf metallic-smoothness source', width, height, alpha=True, float_buffer=False)
    smoothness.colorspace_settings.name = 'Non-Color'
    smoothness.pixels.foreach_set(rgba.reshape(-1))
    smoothness.filepath_raw = str(TEXTURE_OUT/'archive-shelf-wood017-metallic-smoothness.png')
    smoothness.file_format = 'PNG'
    smoothness.save()
    smooth_path = Path(smoothness.filepath_raw)
    rows.append({'source_image':'derived: 1 - Wood017_2K-JPG_Roughness.jpg; RGB=0 metallic, alpha=smoothness',
                 'output':'art/export/fullfilm-v5/model-textures/archive-shelf-wood017-metallic-smoothness.png',
                 'embedded_in_source_fbx':False, 'width':width, 'height':height,
                 'source_format':'PNG RGBA', 'source_color_space':'Non-Color', 'sha256':sha256(smooth_path)})
    mesh = shelf.data
    mesh.calc_loop_triangles()
    active_uv = mesh.uv_layers.active
    uv_range = [[min(loop.uv[i] for loop in active_uv.data), max(loop.uv[i] for loop in active_uv.data)] for i in (0,1)]
    source_info = {'source_fbx':'Assets/Room01/Scene/model/Room01.fbx', 'object':'Living_Shelf',
        'vertices':len(mesh.vertices), 'triangles':len(mesh.loop_triangles),
        'material_slot':0, 'material_name':slot.material.name,
        'uv_layers':[layer.name for layer in mesh.uv_layers], 'active_uv_layer':active_uv.name,
        'active_uv_range':[[round(v,6) for v in axis] for axis in uv_range],
        'texture_wrap':'Repeat; source UVs intentionally span negative and >1 values',
        'material_textures':rows}

    # The plural Books mesh has two embedded cover images. Their Unity import
    # names book4.png/book5.png correspond to packed source images 4.png/1.png.
    books = next((o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.lower()=='books'), None)
    if books is None:
        raise RuntimeError('Source FBX has no Books mesh')
    book_map = {'4.png':'archive-books-book4.png', '1.png':'archive-books-book5.png'}
    book_rows = []
    book_slots = []
    for slot_index, book_slot in enumerate(books.material_slots):
        image_nodes = [n.image for n in book_slot.material.node_tree.nodes
                       if book_slot.material and book_slot.material.use_nodes
                       and n.type=='TEX_IMAGE' and n.image]
        slot_images = []
        for image in image_nodes:
            if image.name not in book_map:
                continue
            output = TEXTURE_OUT/book_map[image.name]
            if image.packed_file:
                data = bytes(image.packed_file.data)
            else:
                source_path = Path(bpy.path.abspath(image.filepath))
                if not source_path.is_file():
                    raise RuntimeError('Book image is not packed and file is missing: '+str(source_path))
                data = source_path.read_bytes()
            if not data.startswith(b'\x89PNG\r\n\x1a\n'):
                raise RuntimeError('Expected original PNG bytes for '+image.name)
            output.write_bytes(data)
            unity_texture = 'book4.png' if image.name=='4.png' else 'book5.png'
            relative = 'art/export/fullfilm-v5/model-textures/'+output.name
            slot_images.append({'source_image':image.name, 'unity_original_texture':unity_texture,
                'output':relative, 'embedded_in_source_fbx':bool(image.packed_file),
                'width':int(image.size[0]), 'height':int(image.size[1]), 'source_format':image.file_format,
                'source_color_space':image.colorspace_settings.name, 'sha256':sha256(output)})
            book_rows.extend(slot_images[-1:])
        book_slots.append({'source_slot':slot_index, 'name':book_slot.name,
            'images':[x['source_image'] for x in slot_images]})
    if {x['source_image'] for x in book_rows} != set(book_map):
        raise RuntimeError('Books source FBX must contain packed images 4.png and 1.png')
    books.data.calc_loop_triangles()
    books_uv = books.data.uv_layers.active
    books_uv_range = [[min(loop.uv[i] for loop in books_uv.data), max(loop.uv[i] for loop in books_uv.data)] for i in (0,1)]
    books_info = {'source_fbx':'Assets/Room01/Scene/model/Room01.fbx', 'object':'Books',
        'vertices':len(books.data.vertices), 'triangles':len(books.data.loop_triangles),
        'source_slots':book_slots, 'source_uv_layers':[layer.name for layer in books.data.uv_layers],
        'active_uv_layer':books_uv.name,
        'active_uv_range':[[round(v,6) for v in axis] for axis in books_uv_range],
        'unity_recovery':{'slot_0':'paper material unchanged; source has no texture',
            'slot_1':'V5-owned BookCover04 material, original embedded 4.png / Unity book4.png',
            'slot_2':'V5-owned BookCover05 material, original embedded 1.png / Unity book5.png'},
        'material_textures':book_rows}

    # Recover only the two FBX-embedded wood materials that the latest V4
    # Renderer report identifies as colliding with the book4 material clone.
    wood_expected = {
        'Wood021_1K-JPG_Color.jpg': 'room01-wood021-color.jpg',
        'Wood021_1K-JPG_NormalGL.jpg': 'room01-wood021-normal-gl.jpg',
        'Wood021_1K-JPG_Roughness.jpg': 'room01-wood021-roughness.jpg',
        'Wood032_2K-JPG_NormalGL.jpg': 'room01-wood032-normal-gl.jpg',
        'Wood032_2K-JPG_Roughness.jpg': 'room01-wood032-roughness.jpg',
    }
    source_wood = {}
    for material_name in ('wood.001','wood.004'):
        mat = bpy.data.materials.get(material_name)
        if mat is None or not mat.use_nodes:
            raise RuntimeError('Room01 FBX is missing node material '+material_name)
        principled = next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
        if principled is None:
            raise RuntimeError('Room01 FBX material has no Principled node: '+material_name)
        images = {n.image.name:n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image}
        mapped_names = set(images).intersection(wood_expected)
        if material_name=='wood.001' and mapped_names != {
                'Wood021_1K-JPG_Color.jpg','Wood021_1K-JPG_NormalGL.jpg','Wood021_1K-JPG_Roughness.jpg'}:
            raise RuntimeError('wood.001 embedded image set changed: '+str(sorted(mapped_names)))
        if material_name=='wood.004' and mapped_names != {
                'Wood032_2K-JPG_NormalGL.jpg','Wood032_2K-JPG_Roughness.jpg'}:
            raise RuntimeError('wood.004 embedded image set changed: '+str(sorted(mapped_names)))
        texture_rows=[]
        for source_name in sorted(mapped_names):
            image=images[source_name]
            output=TEXTURE_OUT/wood_expected[source_name]
            data=bytes(image.packed_file.data) if image.packed_file else Path(bpy.path.abspath(image.filepath)).read_bytes()
            if not data.startswith(b'\xff\xd8'):
                raise RuntimeError('Expected original JPEG bytes for '+source_name)
            output.write_bytes(data)
            purpose=('BaseColor' if 'Color.jpg' in source_name else
                     'TangentNormal' if 'NormalGL.jpg' in source_name else 'Roughness')
            texture_rows.append({'source_image':source_name,
                'purpose':purpose,
                'output':'art/export/fullfilm-v5/model-textures/'+output.name,
                'embedded_in_source_fbx':bool(image.packed_file),'width':int(image.size[0]),'height':int(image.size[1]),
                'source_format':image.file_format,'source_color_space':image.colorspace_settings.name,'sha256':sha256(output)})
        rough_name='Wood021_1K-JPG_Roughness.jpg' if material_name=='wood.001' else 'Wood032_2K-JPG_Roughness.jpg'
        rough=images[rough_name]
        rough.colorspace_settings.name='Non-Color'
        width,height=int(rough.size[0]),int(rough.size[1])
        pixels=np.empty(width*height*4,dtype=np.float32)
        rough.pixels.foreach_get(pixels)
        rgba=np.zeros((height,width,4),dtype=np.float32)
        rgba[:,:,3]=1.0-pixels.reshape((height,width,4))[:,:,0]
        smooth_name=('room01-wood021-metallic-smoothness.png' if material_name=='wood.001'
                     else 'room01-wood032-specular-smoothness.png')
        smooth=bpy.data.images.new(material_name+' smoothness recovery',width,height,alpha=True,float_buffer=False)
        smooth.colorspace_settings.name='Non-Color';smooth.pixels.foreach_set(rgba.reshape(-1))
        smooth.filepath_raw=str(TEXTURE_OUT/smooth_name);smooth.file_format='PNG';smooth.save()
        smooth_path=Path(smooth.filepath_raw)
        texture_rows.append({'source_image':'derived: 1 - '+rough_name+'; RGB=0, alpha=smoothness',
            'purpose':'MetallicSmoothnessAlpha' if material_name=='wood.001' else 'SpecularSmoothnessAlpha',
            'output':'art/export/fullfilm-v5/model-textures/'+smooth_name,
            'embedded_in_source_fbx':False,'width':width,'height':height,'source_format':'PNG RGBA',
            'source_color_space':'Non-Color','sha256':sha256(smooth_path)})
        normal=next((n for n in mat.node_tree.nodes if n.type=='NORMAL_MAP'),None)
        source_wood[material_name]={
            'source_material':material_name,
            'base_color':list(principled.inputs['Base Color'].default_value),
            'base_color_is_texture_linked':bool(principled.inputs['Base Color'].is_linked),
            'metallic':float(principled.inputs['Metallic'].default_value),
            'roughness_socket_default':float(principled.inputs['Roughness'].default_value),
            'roughness_is_texture_linked':bool(principled.inputs['Roughness'].is_linked),
            'specular_ior_level':float(principled.inputs['Specular IOR Level'].default_value),
            'normal_map':None if normal is None else {'space':normal.space,'uv_map':normal.uv_map,
                'strength':float(normal.inputs['Strength'].default_value)},
            'images':texture_rows,
            'unity_material':('Room01_Wood021_V5: Standard metallic setup, tint white because Wood021 Color drives BaseColor; metallic 0, smoothness from source roughness alpha'
                if material_name=='wood.001' else 'Room01_Wood032_V5: Standard specular setup with exact FBX base color, black specular color, metallic 0, smoothness from source roughness alpha')}

    # Unity's imported slot order was measured from the existing HubCast build
    # report and the current full Renderer baseline. It differs from Blender's
    # FBX slot order for Desk_Lamp and Sofa, so preserve the target slot number
    # explicitly instead of guessing from the Blender import index.
    affected_bindings=[
        {'mesh':'Desk_Lamp','unity_slot':1,'source_material':'wood.004','instances_in_current_report':10},
        {'mesh':'Wooden_Table','unity_slot':0,'source_material':'wood.001','instances_in_current_report':8},
        {'mesh':'LowTable_wood','unity_slot':0,'source_material':'wood.004','instances_in_current_report':1},
        {'mesh':'Sofa','unity_slot':1,'source_material':'wood.001','instances_in_current_report':1},
    ]
    source_meshes=[]
    for mesh_name in ('Desk_Lamp','Wooden_Table','LowTable_wood','Sofa'):
        obj=next((o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.lower()==mesh_name.lower()),None)
        if obj is None: raise RuntimeError('Source Room01 mesh missing: '+mesh_name)
        obj.data.calc_loop_triangles()
        uv_layers=[]
        for layer in obj.data.uv_layers:
            uv_layers.append({'name':layer.name,'range':[[round(min(loop.uv[i] for loop in layer.data),6),
                round(max(loop.uv[i] for loop in layer.data),6)] for i in (0,1)]})
        source_meshes.append({'mesh':obj.name,'vertices':len(obj.data.vertices),
            'triangles':len(obj.data.loop_triangles),'uv_layers':uv_layers,
            'blender_material_slots':[s.name for s in obj.material_slots]})
    wood_info={'source_fbx':'Assets/Room01/Scene/model/Room01.fbx',
        'source_slots':[{'mesh':'Desk_Lamp','blender_slot_name':'wood.004','blender_slot_index':0,'unity_affected_slot':1},
                        {'mesh':'Wooden_Table','blender_slot_name':'wood.001','blender_slot_index':0,'unity_affected_slot':0},
                        {'mesh':'LowTable_wood','blender_slot_name':'wood.004','blender_slot_index':0,'unity_affected_slot':0},
                        {'mesh':'Sofa','blender_slot_name':'wood.001','blender_slot_index':0,'unity_affected_slot':1}],
        'materials':source_wood,
        'source_meshes':source_meshes,
        'affected_unity_bindings':affected_bindings,
        'collision_evidence':{'report':'~/tmp/vrc-old-web-map/fullfilm-v5/reports/room01-materials.json',
            'current_renderers_total':47,'book4_material_renderer_slots':'The 20 furniture slots above all resolve to Inherited-ab7a9631e24269a46b38253421ef9828 with mainTexture Assets/Room01/texture/book4.png.'},
        'preserve_policy':'In V5, replace only the four listed mesh/slot pairs confirmed by the 47-Renderer report. Leave all other slots and healthy external materials untouched.'}
    return {'shelf':source_info, 'books':books_info, 'wood_furniture':wood_info}


def main():
    root = build_model()
    root_name = root.name
    bpy.context.view_layer.update()
    objects = [root] + list(root.children_recursive)
    mesh_objects = [o for o in objects if o.type=='MESH']
    rows = [object_row(o) for o in objects]
    mesh_count = len(mesh_objects)
    triangles = sum(r.get('triangles',0) for r in rows)
    vertices = sum(r.get('vertices',0) for r in rows)
    materials = sorted({m.name for o in mesh_objects for m in o.data.materials if m})
    bounds_corners = [o.matrix_world @ Vector(c) for o in mesh_objects for c in o.bound_box]
    bounds = [[round(min(p[i] for p in bounds_corners),6), round(max(p[i] for p in bounds_corners),6)] for i in range(3)]
    export_model(root)
    preview_paths = []
    if OPT.render:
        primary = OUT/(ASSET_STEM+'_preview.png')
        render_preview(root, primary, detail=False)
        preview_paths.append(primary)
        detail = OUT/(ASSET_STEM+'_detail.png')
        render_preview(root, detail, detail=True)
        preview_paths.append(detail)
    else:
        # Preserve previously reviewed static previews when the user requests
        # a data-only rerun of the source-material extraction.
        for name in (ASSET_STEM+'_preview.png', ASSET_STEM+'_detail.png'):
            path = OUT/name
            if path.is_file(): preview_paths.append(path)
    # Save a static master after removing all temporary preview context.
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(ASSET_STEM+'.blend')))
    export_validation = validate_reimports(mesh_count, triangles)
    source_materials = extract_source_textures()
    source_shelf = source_materials['shelf']
    source_books = source_materials['books']
    source_wood = source_materials['wood_furniture']
    wood_texture_rows = [image for material_data in source_wood['materials'].values()
                         for image in material_data['images']]
    all_recovery_textures = source_shelf['material_textures']+source_books['material_textures']+wood_texture_rows
    ROOM01_RECOVERY_PATH.parent.mkdir(parents=True,exist_ok=True)
    room01_recovery = {
        'schema':1,
        'source_fbx':source_wood['source_fbx'],
        'diagnosis':{
            'report':'~/tmp/vrc-old-web-map/fullfilm-v5/reports/room01-materials.json',
            'renderers_in_report':47,'affected_furniture_renderers':20,
            'bad_material_name':'Inherited-ab7a9631e24269a46b38253421ef9828',
            'bad_texture':'Assets/Room01/texture/book4.png',
            'finding':'V4 isolated distinct embedded material sub-assets using the FBX path GUID only. The affected slots all point to the book4 clone.'},
        'affected_source_slots':source_wood['source_slots'],
        'source_materials':source_wood['materials'],
        'binding_policy':source_wood['preserve_policy'],
        'unity_entrypoint':'OWFilmV5ModelBuild.RestoreRoom01WoodMaterials',
        'textures':wood_texture_rows,
        'all_recovery_texture_files':[x['output'] for x in all_recovery_textures]
    }
    ROOM01_RECOVERY_PATH.write_text(json.dumps(room01_recovery,ensure_ascii=False,indent=2),encoding='utf-8')
    objects_path = OUT/(ASSET_STEM+'_objects.json')
    objects_path.write_text(json.dumps({'id':ASSET_STEM,'root':root_name,'mesh_count':mesh_count,
        'triangles':triangles,'vertices':vertices,'bounds_blender_m':bounds,'objects':rows},
        ensure_ascii=False,indent=2),encoding='utf-8')
    validation_path = OUT/'export-reimport-validation.json'
    validation_path.write_text(json.dumps({'blender':bpy.app.version_string,
        'method':'Fresh empty Blender scene import for each exported format',
        'formats':export_validation,'passed':all(x['passed'] for x in export_validation)},
        ensure_ascii=False,indent=2),encoding='utf-8')
    files = {}
    for ext in ('.blend','.fbx','.glb'):
        p = OUT/(ASSET_STEM+ext)
        files[p.name] = {'path':'art/models/fullfilm-v5/'+p.name, 'sha256':sha256(p), 'bytes':p.stat().st_size}
    for p in preview_paths:
        files[p.name] = {'path':'art/models/fullfilm-v5/'+p.name, 'sha256':sha256(p), 'bytes':p.stat().st_size}
    for p in (objects_path, validation_path):
        files[p.name] = {'path':'art/models/fullfilm-v5/'+p.name, 'sha256':sha256(p), 'bytes':p.stat().st_size}
    for texture in all_recovery_textures:
        p = TEXTURE_OUT/Path(texture['output']).name
        files[p.name] = {'path':texture['output'], 'sha256':sha256(p), 'bytes':p.stat().st_size}
    files[ROOM01_RECOVERY_PATH.name] = {'path':'data/'+ROOM01_RECOVERY_PATH.name,
        'sha256':sha256(ROOM01_RECOVERY_PATH),'bytes':ROOM01_RECOVERY_PATH.stat().st_size}
    manifest = {
        'schema':1,
        'units':'metres',
        'blender':bpy.app.version_string,
        'axes':{'blender':'X right, Z up, +Y front','fbx_unity':'Unity axis conversion owned by ModelImporter; do not add a second manual rotation/scale'},
        'assets':[{'id':ASSET_STEM,'root':root_name,'integration_target':'Existing final-world BranchCanopy pivot',
            'replacement_scope':'Only child geometry under the target BranchCanopy pivot; preserve outer world model, planter sibling, seating, floor, collisions, QvPen and screen anchors.',
            'pivot_blender_m':[0,0,0], 'target_parent_pivot_blender_m':[.95,-.35,0],
            'bounds_blender_m':bounds, 'mesh_count':mesh_count,'triangles':triangles,'vertices':vertices,
            'material_count':len(materials),'materials':materials,'objects':rows,
            'material_zones':[
              {'material':'M_FFV5_TrunkTimber','zone':'one tapered structural trunk'},
              {'material':'M_FFV5_BranchTimber','zone':'three primary branches and six fork arms'},
              {'material':'M_FFV5_EndGrain','zone':'radial rafters and trunk end bands'},
              {'material':'M_FFV5_CanopySage / M_FFV5_CanopyLightSage','zone':'alternating upper faces of six cloth crown panels'},
              {'material':'M_FFV5_CanopyUnderside / M_FFV5_CanopyUndersideLight','zone':'contrasting underside lining, visible from low avatar eye height'},
              {'material':'M_FFV5_CanopyBinding','zone':'six stitched panel seams'},
              {'material':'M_FFV5_JoineryBronze','zone':'visible fork ferrules, rib saddles and crown hub'}],
            'readability_contract':'The trunk rises from the existing planter; three load-bearing Y forks split into six tapered radial arms. Six ribs and cross battens support a six-panel sewn crown. Taper, fork collars, eave rails and distinct fabric/wood/metal zones identify a timber shade structure.',
            'files':[files[ASSET_STEM+ext]['path'] for ext in ('.blend','.fbx','.glb')],
            'export_reimport_validation':export_validation}],
        'source_recovery':{'id':'room01_embedded_material_recovery','scope':'Only V5-owned materials and extracted packed FBX images are created; no Room01 asset is modified.',
            'source_mesh':source_shelf,'texture_policy':'Extract the original packed images from the user-owned Room01 FBX. Unity shelf slot 0 must use albedo with Repeat wrap; normal map is OpenGL convention; the roughness map is preserved and converted to Unity Standard metallic-smoothness alpha.',
            'books_mesh':source_books,
            'wood_furniture':source_wood,
            'unity_binding':'OWFilmV5ModelBuild.IntegrateRecovery creates V5-owned source-derived materials; it binds only shelf/Books slots previously contaminated and the four explicitly listed furniture mesh slots. Other external slots are preserved.',
            'files':[x['output'] for x in all_recovery_textures],
            'detailed_manifest':'data/'+ROOM01_RECOVERY_PATH.name},
        'files':files,
        'provenance':'The tree model is authored from scratch for the existing scene pivot. Shelf maps are extracted byte-for-byte from packed Room01 FBX images; the smoothness-alpha PNG is deterministically derived from the packed roughness map.'}
    MANIFEST_PATH.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print('FFV5_MODEL_BUILD_COMPLETE '+json.dumps({'mesh_count':mesh_count,'triangles':triangles,'vertices':vertices,
        'material_count':len(materials),'bounds_m':bounds,'model_sha256':files[ASSET_STEM+'.fbx']['sha256'],
        'recovery_textures':len(all_recovery_textures),'room01RecoveryManifest':str(ROOM01_RECOVERY_PATH),
        'manifest':str(MANIFEST_PATH)},ensure_ascii=False))


main()
