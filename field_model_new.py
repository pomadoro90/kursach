"""
Комплексная упрощённая 3D-модель нефтяного месторождения для Blender.

Состав сцены:
- добывающая скважина с насосной арматурой / станком-качалкой;
- нагнетательная скважина;
- ДНС, УПСВ, УПН, БКНС, КНС;
- система трубопроводов: нефтегазожидкостные, водоводы, товарная нефть;
- резервуары, сепараторы, насосные блоки, эстакады, стрелки потоков и подписи.

Запуск:
    blender --background --python field_model.py
или внутри Blender: Scripting -> Open -> Run Script.

Результат сохраняется рядом со скриптом:
    field_development_complex.blend
"""

from __future__ import annotations

import math
import os
import importlib.util
import sys
from typing import Iterable, Tuple

import bpy
from mathutils import Matrix, Vector

Vec3 = Tuple[float, float, float]

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.getcwd()
OUT_BLEND = os.path.join(HERE, "field_development_complex_new.blend")

# ---------------------------------------------------------------------------
# Сцена, материалы, базовые примитивы
# ---------------------------------------------------------------------------


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for block in (
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.lights,
        bpy.data.cameras,
        bpy.data.curves,
        bpy.data.fonts,
    ):
        for item in list(block):
            if getattr(item, "users", 0) == 0:
                block.remove(item)


def mat(name: str, rgba: Tuple[float, float, float, float], metallic=0.0, roughness=0.55, alpha=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    if alpha is not None or rgba[3] < 1.0:
        m.blend_method = "BLEND"
        m.show_transparent_back = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = metallic
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = roughness
        if "Alpha" in bsdf.inputs:
            bsdf.inputs["Alpha"].default_value = alpha if alpha is not None else rgba[3]
    return m


MATS = {}


# ---------------------------------------------------------------------------
# Спецификация трубопроводов — унифицированные Ду
# ---------------------------------------------------------------------------

PIPE_SPEC = {
    "dn50":   {"radius": 0.055, "mat": "pipe_oil",    "vertices": 14},
    "dn80":   {"radius": 0.075, "mat": "pipe_oil",    "vertices": 18},
    "dn100":  {"radius": 0.095, "mat": "pipe_product","vertices": 24},
    "dn125":  {"radius": 0.105, "mat": "pipe_product","vertices": 24},
    "dn150":  {"radius": 0.120, "mat": "pipe_product","vertices": 26},
    "dn200":  {"radius": 0.150, "mat": "pipe_product","vertices": 28},
    "dn250":  {"radius": 0.180, "mat": "pipe_product","vertices": 32},
}

FLANGE_SCALE = 1.38
SUPPORT_MIN_Z = 0.42
EQUIPMENT_ZONES = []


def make_materials():
    MATS.update(
        ground=mat("Ground / tundra", (0.18, 0.27, 0.16, 1), roughness=0.9),
        gravel=mat("Gravel pads", (0.33, 0.32, 0.29, 1), roughness=0.85),
        concrete=mat("Pumpjack concrete foundations", (0.47, 0.46, 0.42, 1), roughness=0.88),
        road=mat("Service roads", (0.10, 0.10, 0.10, 1), roughness=0.8),
        steel=mat("Dark steel", (0.08, 0.09, 0.11, 1), metallic=0.8, roughness=0.35),
        orange=mat("Safety orange", (0.95, 0.43, 0.08, 1), metallic=0.25, roughness=0.55),
        blue=mat("Water blue", (0.05, 0.34, 0.86, 1), metallic=0.1, roughness=0.3),
        pipe_oil=mat("Oil / emulsion pipeline", (0.13, 0.13, 0.14, 1), metallic=0.7, roughness=0.33),
        pipe_water=mat("Injection water pipeline", (0.02, 0.28, 0.85, 1), metallic=0.45, roughness=0.38),
        pipe_product=mat("Sales oil pipeline", (0.95, 0.74, 0.12, 1), metallic=0.55, roughness=0.35),
        pipe_gas=mat("Gas line", (0.50, 0.76, 0.95, 0.42), metallic=0.15, roughness=0.12, alpha=0.42),
        building=mat("White process modules", (0.82, 0.84, 0.80, 1), metallic=0.15, roughness=0.5),
        tank=mat("Storage tank metal", (0.68, 0.70, 0.66, 1), metallic=0.55, roughness=0.28),
        red=mat("Valve red", (0.8, 0.05, 0.04, 1), metallic=0.25, roughness=0.5),
        green=mat("Equipment green", (0.12, 0.42, 0.18, 1), metallic=0.2, roughness=0.6),
        text=mat("Black label text", (0.02, 0.02, 0.02, 1), roughness=0.5),
        glass=mat("Transparent vessels", (0.55, 0.82, 1.0, 0.32), metallic=0.1, roughness=0.06, alpha=0.32),
    )


def assign(obj, material):
    obj.data.materials.append(material)
    return obj


def add_box(name: str, loc: Vec3, scale: Vec3, material, rot=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign(obj, material)
    if bevel > 0:
        mod = obj.modifiers.new(name="small_edge_bevel", type="BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.affect = "EDGES"
    return obj


def add_cylinder(name: str, loc: Vec3, radius: float, depth: float, material, vertices=32, axis="Z", rot=(0, 0, 0)):
    rotation = rot
    if axis == "X":
        rotation = (0, math.radians(90), 0)
    elif axis == "Y":
        rotation = (math.radians(90), 0, 0)
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rotation)
    obj = bpy.context.active_object
    obj.name = name
    assign(obj, material)
    try:
        bpy.ops.object.shade_smooth()
    except Exception:
        pass
    return obj



def add_cone(name: str, loc: Vec3, r1: float, r2: float, depth: float, material, vertices=32):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r2, depth=depth, location=loc)
    obj = bpy.context.active_object
    obj.name = name
    assign(obj, material)
    try:
        bpy.ops.object.shade_smooth()
    except Exception:
        pass
    return obj


def new_mesh_obj(name: str, verts, faces, material):
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    assign(obj, material)
    try:
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.shade_smooth()
        obj.select_set(False)
    except Exception:
        pass
    return obj


def add_annular_sector(name: str, loc: Vec3, outer_r: float, inner_r: float,
                       thickness: float, start_deg: float, end_deg: float,
                       material, segments=18):
    """Экструдированный сектор кольца в плоскости YZ, толщина вдоль X.

    Используется для реалистичных сегментных противовесов станка-качалки:
    это не случайные висящие диски, а толстые болтовые грузы на кривошипах.
    """
    x, y, z = loc
    a0 = math.radians(start_deg)
    a1 = math.radians(end_deg)
    if a1 < a0:
        a0, a1 = a1, a0
    pts_outer = []
    pts_inner = []
    for i in range(segments + 1):
        a = a0 + (a1 - a0) * i / segments
        pts_outer.append((outer_r * math.cos(a), outer_r * math.sin(a)))
        pts_inner.append((inner_r * math.cos(a), inner_r * math.sin(a)))
    loop = pts_outer + list(reversed(pts_inner))
    verts = [(x - thickness / 2, y + yy, z + zz) for yy, zz in loop] + [(x + thickness / 2, y + yy, z + zz) for yy, zz in loop]
    n = len(loop)
    faces = [tuple(range(n)), tuple(range(2 * n - 1, n - 1, -1))]
    for i in range(n):
        faces.append((i, (i + 1) % n, (i + 1) % n + n, i + n))
    return new_mesh_obj(name, verts, faces, material)


def _unit(v: Vector) -> Vector:
    if v.length <= 1e-8:
        return Vector((0, 0, 0))
    return v.normalized()


def add_quarter_torus_elbow(name: str, corner: Vec3, prev_pt: Vec3, next_pt: Vec3,
                            pipe_radius: float, material, bend_radius=None, seg=16, ring=12):
    """90° трубный отвод как четверть тора, без шаровых муфт.

    prev_pt -> corner -> next_pt должны образовывать примерно прямой угол.
    Отвод касается прямых труб в точках на расстоянии bend_radius от угла.
    """
    c = Vector(corner)
    u = _unit(Vector(prev_pt) - c)   # от угла назад к входящей трубе
    v = _unit(Vector(next_pt) - c)   # от угла по выходящей трубе
    if u.length == 0 or v.length == 0 or abs(u.dot(v)) > 0.12:
        return None
    bend_radius = bend_radius or pipe_radius * 3.8
    plane_n = u.cross(v)
    if plane_n.length <= 1e-8:
        return None
    plane_n.normalize()
    center = c + u * bend_radius + v * bend_radius
    verts = []
    faces = []
    for i in range(seg + 1):
        t = (math.pi / 2) * i / seg
        # starts at corner+u*R and ends at corner+v*R
        arc = center - v * (bend_radius * math.cos(t)) - u * (bend_radius * math.sin(t))
        tangent = _unit(v * math.sin(t) - u * math.cos(t))
        binormal = _unit(tangent.cross(plane_n))
        for j in range(ring):
            a = 2 * math.pi * j / ring
            pos = arc + plane_n * (pipe_radius * math.cos(a)) + binormal * (pipe_radius * math.sin(a))
            verts.append(tuple(pos))
    for i in range(seg):
        for j in range(ring):
            a = i * ring + j
            b = i * ring + (j + 1) % ring
            c2 = (i + 1) * ring + (j + 1) % ring
            d = (i + 1) * ring + j
            faces.append((a, b, c2, d))
    return new_mesh_obj(name, verts, faces, material)


def add_flange(name: str, loc: Vec3, direction: Vec3, pipe_radius: float, material):
    """Плоская фланцевая шайба на конце трубы.

    Фланцевый диаметр автоматически вычисляется как pipe_radius * FLANGE_SCALE.
    """
    flange_r = pipe_radius * FLANGE_SCALE
    d = _unit(Vector(direction))
    if d.length == 0:
        return None
    half_thick = flange_r * 0.12  # толщина пропорциональна фланцу
    return cylinder_between(
        name,
        tuple(Vector(loc) - d * half_thick),
        tuple(Vector(loc) + d * half_thick),
        flange_r,
        material,
        vertices=32,
    )


def add_pipe_support_at(name: str, p: Vec3, pipe_radius: float, direction: Vec3 = (0, 1, 0)):
    """H-поддержка под трубой с ориентацией перпендикулярно направлению трубы."""
    x, y, z = p
    if z <= SUPPORT_MIN_Z:
        return
    for (x0, x1, y0, y1) in EQUIPMENT_ZONES:
        if x0 <= x <= x1 and y0 <= y <= y1:
            return
    top_z = max(0.20, z - pipe_radius - 0.05)
    add_h_support(name, x, y, top_z, direction)


def cylinder_between(name: str, p1: Vec3, p2: Vec3, radius: float, material, vertices=24):
    v1, v2 = Vector(p1), Vector(p2)
    mid = (v1 + v2) / 2
    direction = v2 - v1
    length = direction.length
    if length <= 1e-6:
        return None
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=length, location=mid)
    obj = bpy.context.active_object
    obj.name = name
    quat = direction.to_track_quat("Z", "Y")
    obj.rotation_euler = quat.to_euler()
    assign(obj, material)
    try:
        bpy.ops.object.shade_smooth()
    except Exception:
        pass
    return obj


def pipe_path(name: str, points: Iterable[Vec3], radius: float, material, elevated=True, bend_factor=4.0):
    """Связная трасса трубы — единый меш без видимых швов."""
    pts = [Vector(p) for p in points]
    if len(pts) < 2:
        return
    bend_radius = radius * bend_factor
    all_verts = []
    all_faces = []
    vert_offset = 0
    ring = 20

    def is_elbow(i: int) -> bool:
        if i <= 0 or i >= len(pts) - 1:
            return False
        a = _unit(pts[i - 1] - pts[i])
        b = _unit(pts[i + 1] - pts[i])
        return a.length > 0 and b.length > 0 and abs(a.dot(b)) < 0.12

    # Прямые участки: у углов оставляем место под радиусный отвод.
    for i in range(len(pts) - 1):
        start = Vector(pts[i])
        end = Vector(pts[i + 1])
        direction = _unit(end - start)
        if direction.length == 0:
            continue
        if is_elbow(i):
            start = pts[i] + direction * bend_radius
        if is_elbow(i + 1):
            end = pts[i + 1] - direction * bend_radius
        seg_len = (end - start).length
        if seg_len <= radius * 2.0:
            continue

        right = Vector((0, 1, 0)) if abs(direction.y) < 0.9 else Vector((1, 0, 0))
        right = (right - right.dot(direction) * direction).normalized()
        up = direction.cross(right).normalized()
        n_rings = max(2, int(seg_len / (radius * 0.8)) + 1)
        for ring_i in range(n_rings + 1):
            t = ring_i / n_rings
            center = start + (end - start) * t
            for j in range(ring):
                a = 2 * math.pi * j / ring
                pos = center + right * (radius * math.cos(a)) + up * (radius * math.sin(a))
                all_verts.append(tuple(pos))
        for ring_i in range(n_rings):
            for j in range(ring):
                a = ring_i * ring + j
                b = ring_i * ring + (j + 1) % ring
                c2 = (ring_i + 1) * ring + (j + 1) % ring
                d = (ring_i + 1) * ring + j
                all_faces.append((a + vert_offset, b + vert_offset, c2 + vert_offset, d + vert_offset))
        vert_offset += (n_rings + 1) * ring

        if elevated:
            span = end - start
            steps = max(1, int(span.length // 3.8))
            for k in range(1, steps + 1):
                p = start + span * (k / (steps + 1))
                add_pipe_support_at(f"{name}_support_{i+1:02d}_{k:02d}", tuple(p), radius, tuple(direction))

    # Фитинги: 90° = четверть тора, прямые промежуточные точки = фланцевая муфта.
    for i in range(1, len(pts) - 1):
        if is_elbow(i):
            c = pts[i]
            u = _unit(pts[i - 1] - c)
            v = _unit(pts[i + 1] - c)
            if u.length == 0 or v.length == 0 or abs(u.dot(v)) > 0.12:
                continue
            plane_n = u.cross(v)
            if plane_n.length <= 1e-8:
                continue
            plane_n.normalize()
            center = c + u * bend_radius + v * bend_radius
            seg = 16
            for si in range(seg + 1):
                t = (math.pi / 2) * si / seg
                arc = center - v * (bend_radius * math.cos(t)) - u * (bend_radius * math.sin(t))
                tangent = _unit(v * math.sin(t) - u * math.cos(t))
                binormal = _unit(tangent.cross(plane_n))
                for j in range(ring):
                    a = 2 * math.pi * j / ring
                    pos = arc + plane_n * (radius * math.cos(a)) + binormal * (radius * math.sin(a))
                    all_verts.append(tuple(pos))
            for si in range(seg):
                for j in range(ring):
                    a = si * ring + j
                    b = si * ring + (j + 1) % ring
                    c2 = (si + 1) * ring + (j + 1) % ring
                    d = (si + 1) * ring + j
                    all_faces.append((a + vert_offset, b + vert_offset, c2 + vert_offset, d + vert_offset))
            vert_offset += (seg + 1) * ring
        else:
            d = _unit(pts[i + 1] - pts[i - 1])
            # Муфты/стыковые шайбы делаем стальными, а не цветом трубы: так они
            # читаются как фланцы, а не как шарообразные жёлтые фитинги.
            add_flange(f"{name}_coupling_{i:02d}", tuple(pts[i]), tuple(d), radius, MATS["steel"])

    # Видимые фланцы на начальном и конечном подключении к оборудованию.
    add_flange(f"{name}_start_flange", tuple(pts[0]), tuple(pts[1] - pts[0]), radius, material)
    add_flange(f"{name}_end_flange", tuple(pts[-1]), tuple(pts[-1] - pts[-2]), radius, material)

    if all_verts:
        new_mesh_obj(name, all_verts, all_faces, material)
        try:
            bpy.ops.object.shade_smooth()
        except Exception:
            pass


# ---------------------------------------------------------------------------
# Универсальные трубные API (на базе PIPE_SPEC)
# ---------------------------------------------------------------------------


def pipe_path_by_spec(name: str, points: Iterable[Vec3], spec_key: str, elevated=True):
    """Связная трасса трубы по спецификации Ду (PIPE_SPEC)."""
    spec = PIPE_SPEC.get(spec_key)
    if spec is None:
        raise KeyError(f"Unknown spec_key: {spec_key} (available: {list(PIPE_SPEC.keys())})")
    pipe_path(name, points, spec["radius"], MATS[spec["mat"]], elevated=elevated)


def add_pipe_spool(name: str, p1: Vec3, p2: Vec3, spec_key: str,
                   with_flanges=True, with_support=True):
    """Цилиндр-спул с фланцами и (опционально) поддержкой.

    Заменяет пару cylinder_between + add_flange в inline-узлах.
    """
    spec = PIPE_SPEC.get(spec_key)
    if spec is None:
        raise KeyError(f"Unknown spec_key: {spec_key}")
    radius = spec["radius"]
    material = MATS[spec["mat"]]
    cylinders = cylinder_between(
        name + "_spool", p1, p2, radius, material,
        vertices=spec.get("vertices", 24),
    )
    if cylinders is None:
        return
    if with_flanges:
        direction = Vector(p2) - Vector(p1)
        if direction.length > 0:
            add_flange(name + "_flange_p1", p1, tuple(direction), radius, material)
            add_flange(name + "_flange_p2", p2, tuple(direction), radius, material)
    if with_support:
        span = Vector(p2) - Vector(p1)
        if span.length > 3.8:
            mid = (Vector(p1) + Vector(p2)) / 2
            add_pipe_support_at(name + "_support", tuple(mid), radius)


# ---------------------------------------------------------------------------
# Подписи, дороги, площадки
# ---------------------------------------------------------------------------


def add_label(text: str, loc: Vec3, size=0.42, rot_z=0.0):
    # Подписи сделаны крупнее и подняты выше поверхностей: раньше текстовые
    # объекты лежали слишком близко к плитам/земле и местами пропадали в
    # текстурах из-за z-fighting.
    x, y, z = loc
    raised_loc = (x, y, z + 0.62)
    bpy.ops.object.text_add(location=raised_loc, rotation=(math.radians(66), 0, rot_z))
    obj = bpy.context.active_object
    obj.name = "Label_" + text
    obj.data.body = text
    obj.data.align_x = "CENTER"
    obj.data.align_y = "CENTER"
    obj.data.size = size * 1.45
    obj.data.extrude = 0.018
    assign(obj, MATS["text"])
    return obj


def add_pad(name: str, loc: Vec3, sx: float, sy: float):
    add_box(name, (loc[0], loc[1], 0.025), (sx, sy, 0.05), MATS["gravel"])
    x, y = loc[0], loc[1]
    half_w, half_h = sx / 2, sy / 2
    EQUIPMENT_ZONES.append((x - half_w, x + half_w, y - half_h, y + half_h))


def add_road(name: str, p1: Vec3, p2: Vec3, width=1.0):
    x1, y1, _ = p1
    x2, y2, _ = p2
    dx, dy = x2 - x1, y2 - y1
    length = math.hypot(dx, dy)
    angle = math.atan2(dy, dx)
    add_box(name, ((x1 + x2) / 2, (y1 + y2) / 2, 0.035), (length, width, 0.035), MATS["road"], rot=(0, 0, angle))


def build_road_network():
    """Build all road segments as a single merged mesh to avoid z-fighting at junctions."""
    import bmesh

    segments = [
        # (x1, y1, x2, y2, width) -- all at z=0.035
        (-26, -6, 24, -6, 1.0),
        (-10.5, -4.5, -10.5, -6, 0.75),
        (-10.5, -6, -6, -6, 0.75),
        (1, -6, 1, -3.5, 0.85),
        (11.5, -6, 11.5, -4, 0.85),
        (10.5, -6, 10.5, 9.5, 0.85),
        (10.5, 9.5, 4.5, 9.5, 0.75),
        (4.5, 9.5, -2, 9.5, 0.75),
        (-2, 9.5, -2, 15, 0.75),
        (-2, 15, 0, 15, 0.75),
    ]

    z_top = 0.035
    bm = bmesh.new()

    for x1, y1, x2, y2, width in segments:
        dx = x2 - x1
        dy = y2 - y1
        length = math.sqrt(dx * dx + dy * dy)
        if length < 0.001:
            continue

        nx = -dy / length * width / 2
        ny = dx / length * width / 2

        v0 = bm.verts.new((x1 + nx, y1 + ny, z_top))
        v1 = bm.verts.new((x1 - nx, y1 - ny, z_top))
        v2 = bm.verts.new((x2 - nx, y2 - ny, z_top))
        v3 = bm.verts.new((x2 + nx, y2 + ny, z_top))
        bm.faces.new([v0, v1, v2, v3])

    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.05)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

    mesh = bpy.data.meshes.new("Roads_mesh")
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("Roads", mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(MATS["road"])
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.shade_flat()
    obj.select_set(False)

    return obj


# ---------------------------------------------------------------------------
# Нефтепромысловые элементы
# ---------------------------------------------------------------------------


def add_h_support(name: str, x: float, y: float, top_z: float, direction: Vec3 = (0, 1, 0)):
    dx, dy = abs(direction[0]), abs(direction[1])
    post_h = max(0.45, top_z)
    if dx > dy:
        for off in (-0.42, 0.42):
            add_box(name + f"_post_{off}", (x, y + off, post_h / 2), (0.08, 0.08, post_h), MATS["steel"])
        add_box(name + "_beam", (x, y, post_h), (0.10, 1.05, 0.08), MATS["steel"])
    else:
        for off in (-0.42, 0.42):
            add_box(name + f"_post_{off}", (x + off, y, post_h / 2), (0.08, 0.08, post_h), MATS["steel"])
        add_box(name + "_beam", (x, y, post_h), (1.05, 0.10, 0.08), MATS["steel"])


def add_valve_tree(name: str, loc: Vec3, scale=1.0, injection=False):
    x, y, z = loc
    material = MATS["blue"] if injection else MATS["steel"]
    add_cylinder(name + "_casing", (x, y, z + 0.45 * scale), 0.14 * scale, 0.9 * scale, material, vertices=24)
    add_cylinder(name + "_flange", (x, y, z + 0.95 * scale), 0.25 * scale, 0.12 * scale, MATS["orange"], vertices=24)
    add_cylinder(name + "_cross", (x, y, z + 1.16 * scale), 0.07 * scale, 0.9 * scale, material, axis="X", vertices=16)
    add_cylinder(name + "_top", (x, y, z + 1.42 * scale), 0.07 * scale, 0.45 * scale, material, vertices=16)
    for sx in (-1, 1):
        add_cylinder(name + f"_handwheel_{sx}", (x + sx * 0.52 * scale, y, z + 1.16 * scale), 0.16 * scale, 0.04 * scale, MATS["red"], axis="X", vertices=24)


def add_pumpjack(name: str, loc: Vec3, scale=0.8):
    """Станок-качалка — единая цельная машина, а не набор разрозненных деталей.

    Упрощён до ~10 сплошных компонентов: фундамент, стойки, балка, голова,
    редуктор, противовесы, подвеска, устье. Без мелкого крепежа и отдельных
    пластин — чтобы на рендере качалка читалась как собранная установка.
    """
    x, y, z = loc
    s = scale

    # 1. Фундамент — сплошная монолитная плита, слегка утопленная.
    add_box(name + "_pad", (x, y, z + 0.015 * s), (5.0 * s, 9.5 * s, 0.20 * s), MATS["concrete"], bevel=0.03 * s)

    # 2. Основание/рамная часть — сплошной тёмный параллелепипед на плите.
    add_box(name + "_skid", (x, y - 0.15 * s, z + 0.28 * s), (3.4 * s, 7.8 * s, 0.28 * s), MATS["steel"], bevel=0.02 * s)

    # 3. A-frame — четыре толстые стойки как единая пирамида из 4 цилиндров.
    leg_top_z = z + 5.15 * s
    leg_bot_z = z + 0.50 * s
    half_x, half_y = 1.45 * s, 1.25 * s
    for bx, by, suffix in [(-half_x, -half_y, "FL"), (half_x, -half_y, "FR"),
                           (-half_x, half_y, "RL"), (half_x, half_y, "RR")]:
        cylinder_between(name + "_leg_" + suffix, (x + bx, y + by, leg_bot_z), (x, y, leg_top_z), 0.14 * s, MATS["steel"], vertices=14)
    # Диагонали крестовые — толстые стержни, читаются как жёсткость рамы.
    for y_off in (-0.60 * s, 0.60 * s):
        cylinder_between(name + "_cross_y" + str(int(abs(y_off))), (x - 0.85 * s, y + y_off, z + 2.55 * s), (x + 0.85 * s, y + y_off, z + 2.55 * s), 0.08 * s, MATS["steel"], vertices=10)
    for x_off in (-0.75 * s, 0.75 * s):
        cylinder_between(name + "_cross_x" + str(int(abs(x_off))), (x + x_off, y - 0.65 * s, z + 2.55 * s), (x + x_off, y + 0.65 * s, z + 2.55 * s), 0.08 * s, MATS["steel"], vertices=10)

    # 4. Седло и ось — толстая коробка + ось через неё.
    add_box(name + "_saddle", (x, y, leg_top_z + 0.08 * s), (1.35 * s, 1.50 * s, 0.42 * s), MATS["steel"], bevel=0.02 * s)
    add_cylinder(name + "_pivot_pin", (x, y, leg_top_z + 0.22 * s), 0.16 * s, 1.85 * s, MATS["steel"], axis="X", vertices=18)

    # 5. Балка — одна сплошная коробка вместо 3 частей H-профиля.
    beam_len = 9.5 * s
    beam_y0 = y - 0.85 * s
    beam_z = leg_top_z + 0.38 * s
    add_box(name + "_beam", (x, beam_y0, beam_z), (0.75 * s, beam_len, 0.52 * s), MATS["steel"])

    # 6. Лошадиная голова — утолщённая двусторонняя кривая деталь.
    head_y_beam = beam_y0 - beam_len / 2
    head_y_tip = head_y_beam - 1.5 * s
    head_top = beam_z + 0.75 * s
    head_bot = beam_z - 1.05 * s
    chord_half = (head_top - head_bot) / 2
    chord_mid = (head_top + head_bot) / 2
    sagitta = 1.15 * s
    uc = (sagitta * sagitta - chord_half * chord_half) / (2 * sagitta)
    r_seg = sagitta - uc
    theta_bot = math.atan2(-chord_half, -uc)
    theta_top = -theta_bot
    arc = []
    for k in range(12):
        t = theta_bot + (theta_top - theta_bot) * k / 11
        u = uc + r_seg * math.cos(t)
        v = r_seg * math.sin(t)
        arc.append((head_y_tip - u, chord_mid + v))
    sil = [(head_y_beam, head_top), (head_y_beam, beam_z - 0.30 * s)] + arc
    # Двусторонняя экструзия: левая сторона и правая сторона одной толщины.
    hh_thick = 0.65 * s
    verts = [(-hh_thick / 2, yy, zz) for yy, zz in sil] + [(hh_thick / 2, yy, zz) for yy, zz in sil]
    verts = [(vx + x, vy, vz) for vx, vy, vz in verts]
    n = len(sil)
    faces = [tuple(range(n)), tuple(range(2 * n - 1, n - 1, -1))]
    for i in range(n):
        faces.append((i, (i + 1) % n, (i + 1) % n + n, i + n))
    new_mesh_obj(name + "_horse_head", verts, faces, MATS["steel"])

    # 7. Редуктор + мотор — сплошные блоки на раме, единый цвет.
    crank_y = y + 2.95 * s
    crank_z = z + 1.35 * s
    add_box(name + "_gearbox", (x, crank_y, z + 0.88 * s), (1.55 * s, 1.25 * s, 0.88 * s), MATS["steel"], bevel=0.025 * s)
    add_box(name + "_motor", (x + 1.25 * s, crank_y + 1.20 * s, z + 0.65 * s), (0.80 * s, 1.05 * s, 0.58 * s), MATS["steel"], bevel=0.02 * s)
    # Монтажная рама под редуктор — сплошная плита.
    add_box(name + "_gearbox_base", (x, crank_y, z + 0.38 * s), (1.90 * s, 1.50 * s, 0.12 * s), MATS["steel"])
    add_box(name + "_motor_base", (x + 1.25 * s, crank_y + 1.20 * s, z + 0.30 * s), (1.10 * s, 1.25 * s, 0.10 * s), MATS["steel"])

    # 8. Кривошип и противовес — один цельный вал + 2 сектора.
    pin_y = crank_y - 0.50 * s
    pin_z = crank_z + 0.40 * s
    add_cylinder(name + "_crankshaft", (x, crank_y, crank_z), 0.13 * s, 2.2 * s, MATS["steel"], axis="X", vertices=18)
    for side, sx in [("L", -0.90 * s), ("R", 0.90 * s)]:
        # Кривошипная щека — толстый цилиндр на валу.
        add_cylinder(name + "_crank_hub_" + side, (x + sx, crank_y, crank_z), 0.20 * s, 0.28 * s, MATS["steel"], axis="X", vertices=18)
        # Жирный кривошипный рычаг.
        cylinder_between(name + "_crank_arm_" + side, (x + sx, crank_y, crank_z), (x + sx, pin_y, pin_z), 0.095 * s, MATS["steel"], vertices=12)
        # Крупный противовес-сектор на оси кривошипа.
        add_annular_sector(name + "_counterweight_" + side, (x + sx, crank_y, crank_z), 1.45 * s, 0.75 * s, 0.42 * s, 70, 230, MATS["orange"], segments=28)
        # Шатун.
        cylinder_between(name + "_pitman_" + side, (x + sx, pin_y, pin_z), (x + sx * 0.40, beam_y0 + beam_len / 2 - 0.25 * s, beam_z - 0.32 * s), 0.05 * s, MATS["steel"], vertices=10)

    # 9. Подвеска и устье.
    cable_y = head_y_tip
    cable_top_z = head_bot + 0.25 * s
    cable_bot_z = z + 0.85 * s
    cylinder_between(name + "_bridle", (x, cable_y, cable_bot_z), (x, cable_y, cable_top_z), 0.04 * s, MATS["steel"], vertices=10)
    add_box(name + "_carrier", (x, cable_y, cable_bot_z), (0.75 * s, 0.12 * s, 0.08 * s), MATS["steel"])
    add_valve_tree(name + "_wellhead", (x, cable_y, z), scale=1.0 * s)



def add_injection_well(name: str, loc: Vec3):
    x, y, z = loc
    add_pad(name + "_pad", loc, 4.0, 4.0)
    add_valve_tree(name + "_tree", (x, y, z), scale=1.05, injection=True)
    add_cylinder(name + "_filter_skid", (x + 1.15, y + 0.75, z + 0.45), 0.26, 1.4, MATS["blue"], axis="X", vertices=24)
    add_box(name + "_control_box", (x - 1.1, y + 0.6, z + 0.65), (0.8, 0.55, 1.0), MATS["building"])
    add_label("Нагнетательная\nскважина", (x, y - 2.8, z + 0.08), size=0.34)


def add_horizontal_vessel(name: str, loc: Vec3, length: float, radius: float, material):
    """Горизонтальный аппарат на седловых опорах от земли до нижней образующей."""
    x, y, z = loc
    # страховка: центр не ниже радиуса + небольшой зазор под седла
    z = max(z, radius + 0.35)
    add_cylinder(name + "_body", (x, y, z), radius, length + 0.16, material, axis="X", vertices=40)
    saddle_h = max(0.12, z - radius)
    for sx in (-0.34, 0.34):
        sx_abs = x + sx * length
        add_box(name + f"_saddle_{sx}", (sx_abs, y, saddle_h / 2), (0.36, radius * 1.75, saddle_h), MATS["steel"])
        add_box(name + f"_saddle_cap_{sx}", (sx_abs, y, z - radius + 0.035), (0.52, radius * 1.95, 0.07), MATS["orange"])
    # патрубки аппарата: вход/выход + верхний газоотвод, чтобы трубы имели куда подключаться
    nozzle_r = 0.13  # соответствует DN250-ish
    add_flange(name + "_left_nozzle", (x - length / 2 - 0.08, y, z), (-1, 0, 0), nozzle_r, MATS["steel"])
    add_flange(name + "_right_nozzle", (x + length / 2 + 0.08, y, z), (1, 0, 0), nozzle_r, MATS["steel"])
    add_cylinder(name + "_gas_nozzle", (x, y, z + radius + 0.18), 0.08, 0.36, MATS["steel"], vertices=16)


def add_tank(name: str, loc: Vec3, radius=1.0, height=2.0, label=""):
    x, y, z = loc
    base_z = 0.0 if z < 0.02 else z
    # фундаментное кольцо остается отдельным объектом с материалом гравия.
    add_cylinder(name + "_foundation", (x, y, base_z + 0.08), radius + 0.42, 0.16, MATS["gravel"], vertices=56)
    add_cylinder(name + "_body", (x, y, base_z + 0.16 + (height + 0.28) / 2), radius, height + 0.28, MATS["tank"], vertices=56)
    add_cone(name + "_roof", (x, y, base_z + 0.28 + height + 0.08), radius * 0.98, radius * 0.18, 0.16, MATS["tank"], vertices=56)
    add_cylinder(name + "_roof_center_cap", (x, y, base_z + 0.28 + height + 0.22), radius * 0.18, 0.12, MATS["steel"], vertices=24)
    # боковые патрубки/фланцы: nozzle spool торчит из стенки, flange на конце spool
    nozzle_z = base_z + 0.28 + min(1.0, height * 0.45)
    spool_len = 0.18
    spool_r = 0.075   # DN80
    # inlet (left)
    spool_in_start = (x - radius, y, nozzle_z)
    spool_in_end   = (x - radius - spool_len, y, nozzle_z)
    cylinder_between(name + "_inlet_spool", spool_in_start, spool_in_end, spool_r, MATS["steel"], vertices=14)
    add_flange(name + "_inlet_nozzle", spool_in_end, (-1, 0, 0), spool_r, MATS["steel"])
    # outlet (right)
    spool_out_start = (x + radius, y, nozzle_z)
    spool_out_end   = (x + radius + spool_len, y, nozzle_z)
    cylinder_between(name + "_outlet_spool", spool_out_start, spool_out_end, spool_r, MATS["steel"], vertices=14)
    add_flange(name + "_outlet_nozzle", spool_out_end, (1, 0, 0), spool_r, MATS["steel"])
    if label:
        add_label(label, (x, y - radius - 0.7, base_z + 0.08), size=0.32)



def add_pump_block(name: str, loc: Vec3, count=3, water=False):
    """Насосный блок на общей раме с входным/напорным коллекторами."""
    x, y, z = loc
    material = MATS["blue"] if water else MATS["orange"]
    span = max(1.0, (count - 1) * 0.9 + 0.9)
    add_box(name + "_common_skid", (x, y, z + 0.12), (2.4, span + 0.45, 0.18), MATS["steel"])
    suction_x = x - 1.28
    discharge_x = x + 1.12
    add_cylinder(name + "_suction_header", (suction_x, y, z + 0.55), 0.09, span + 0.55, MATS["steel"], axis="Y", vertices=18)
    add_cylinder(name + "_discharge_header", (discharge_x, y, z + 0.62), 0.09, span + 0.55, material, axis="Y", vertices=18)
    for i in range(count):
        yy = y + (i - (count - 1) / 2) * 0.9
        add_box(f"{name}_pump_base_{i+1}", (x, yy, z + 0.24), (1.55, 0.48, 0.14), MATS["steel"])
        add_box(f"{name}_pump_{i+1}", (x, yy, z + 0.50), (1.25, 0.36, 0.38), material)
        add_cylinder(f"{name}_motor_{i+1}", (x - 0.78, yy, z + 0.51), 0.20, 0.55, MATS["green"], axis="X", vertices=20)
        cylinder_between(f"{name}_suction_spool_{i+1}", (suction_x, yy, z + 0.55), (x - 0.62, yy, z + 0.55), 0.055, MATS["steel"], vertices=14)
        cylinder_between(f"{name}_discharge_spool_{i+1}", (x + 0.62, yy, z + 0.62), (discharge_x, yy, z + 0.62), 0.055, material, vertices=14)
        add_flange(f"{name}_pump_suction_flange_{i+1}", (x - 0.62, yy, z + 0.55), (1, 0, 0), 0.055, MATS["steel"])
        add_flange(f"{name}_pump_discharge_flange_{i+1}", (x + 0.62, yy, z + 0.62), (1, 0, 0), 0.055, material)



def add_facility_dns(loc: Vec3):
    x, y, z = loc
    add_pad("DNS_pad", loc, 8.5, 6.5)
    add_box("DNS_control_module", (x - 2.6, y + 1.6, z + 0.55), (2.1, 1.5, 1.1), MATS["building"])
    add_horizontal_vessel("DNS_group_meter", (x - 0.3, y - 0.9, z + 1.05), 3.0, 0.48, MATS["glass"])
    add_pump_block("DNS_booster", (x + 2.35, y - 0.4, z), count=3)
    add_cylinder("DNS_flare_stack", (x + 3.6, y + 2.0, z + 2.0), 0.08, 4.0, MATS["steel"], vertices=12)
    add_cylinder("DNS_flare_burner_tip", (x + 3.6, y + 2.0, z + 4.17), 0.13, 0.26, MATS["orange"], vertices=16)
    add_cylinder("DNS_flare_cap", (x + 3.6, y + 2.0, z + 4.34), 0.16, 0.05, MATS["steel"], vertices=16)
    add_label("ДНС\nдожимная насосная", (x, y - 3.8, z + 0.08), size=0.34)


def add_facility_upsv(loc: Vec3):
    x, y, z = loc
    add_pad("UPSV_pad", loc, 9.0, 7.0)
    add_horizontal_vessel("UPSV_separator_1", (x - 1.7, y + 1.1, z + 1.1), 3.2, 0.55, MATS["glass"])
    add_horizontal_vessel("UPSV_separator_2", (x - 1.7, y - 0.7, z + 1.1), 3.2, 0.55, MATS["glass"])
    add_tank("UPSV_water_tank", (x + 2.2, y + 0.9, z), radius=0.9, height=1.8, label="вода")
    add_box("UPSV_reagent_block", (x + 2.1, y - 1.55, z + 0.55), (2.0, 1.0, 1.1), MATS["building"])
    add_label("УПСВ\nпредварительный\nсброс воды", (x, y - 4.0, z + 0.08), size=0.32)


def add_export_pump_block_connected(name: str, loc: Vec3):
    """Насосный блок УПН без визуально тупиковых патрубков.

    В отличие от универсального add_pump_block(), здесь входной и напорный
    коллекторы заданы как крупные видимые трубы, а каждая насосная ветка
    явно врезана в оба коллектора. Это убирает эффект "висящих" коротких
    жёлтых патрубков на крупном рендере УПН.
    """
    x, y, z = loc
    yy_list = [y - 0.45, y + 0.45]
    suction_x = x - 1.28
    discharge_x = x + 1.12
    add_box(name + "_common_skid", (x, y, z + 0.12), (2.55, 1.55, 0.18), MATS["steel"])
    cylinder_between(name + "_visible_suction_header", (suction_x, y - 0.60, z + 0.55), (suction_x, y + 0.60, z + 0.55), 0.13, MATS["steel"], vertices=24)
    cylinder_between(name + "_visible_discharge_header", (discharge_x, y - 0.60, z + 0.62), (discharge_x, y + 0.60, z + 0.62), 0.135, MATS["pipe_product"], vertices=26)
    add_flange(name + "_suction_header_lower_cap", (suction_x, y - 0.60, z + 0.55), (0, -1, 0), 0.135, MATS["steel"])
    add_flange(name + "_suction_header_upper_cap", (suction_x, y + 0.60, z + 0.55), (0, 1, 0), 0.135, MATS["steel"])
    add_flange(name + "_discharge_header_lower_cap", (discharge_x, y - 0.60, z + 0.62), (0, -1, 0), 0.135, MATS["steel"])
    add_flange(name + "_discharge_header_upper_cap", (discharge_x, y + 0.60, z + 0.62), (0, 1, 0), 0.135, MATS["steel"])
    for i, yy in enumerate(yy_list, start=1):
        add_box(f"{name}_pump_base_{i}", (x, yy, z + 0.24), (1.58, 0.48, 0.14), MATS["steel"])
        add_box(f"{name}_pump_{i}", (x, yy, z + 0.50), (1.18, 0.36, 0.38), MATS["orange"])
        add_cylinder(f"{name}_motor_{i}", (x - 0.78, yy, z + 0.51), 0.20, 0.55, MATS["green"], axis="X", vertices=20)
        cylinder_between(f"{name}_suction_tie_{i}", (x - 0.52, yy, z + 0.62), (suction_x, yy, z + 0.62), 0.12, MATS["pipe_product"], vertices=20)
        cylinder_between(f"{name}_discharge_tie_{i}", (x + 0.58, yy, z + 0.62), (discharge_x, yy, z + 0.62), 0.105, MATS["pipe_product"], vertices=20)
        add_flange(f"{name}_suction_tie_flange_{i}", (suction_x, yy, z + 0.55), (0, 1, 0), 0.12, MATS["steel"])
        add_flange(f"{name}_discharge_tie_flange_{i}", (discharge_x, yy, z + 0.62), (0, 1, 0), 0.105, MATS["steel"])


def add_facility_upn(loc: Vec3):
    x, y, z = loc
    add_pad("UPN_pad", loc, 13.0, 9.0)
    add_horizontal_vessel("UPN_treater", (x - 2.5, y + 1.0, z + 1.2), 3.7, 0.62, MATS["glass"])
    add_box("UPN_heater", (x - 2.5, y - 1.4, z + 0.75), (2.7, 1.15, 1.5), MATS["orange"])
    add_tank("UPN_sales_tank_A", (x + 0.5, y + 1.0, z), radius=0.95, height=2.1, label="товарная\nнефть")
    add_tank("UPN_sales_tank_B", (x + 4.0, y + 1.0, z), radius=0.95, height=2.1)
    add_export_pump_block_connected("UPN_export_pumps", (x + 2.3, y - 3.0, z))
    add_label("УПН\nподготовка нефти", (x, y - 4.5, z + 0.08), size=0.34)


def add_facility_bkns(loc: Vec3):
    x, y, z = loc
    add_pad("BKNS_pad", loc, 7.5, 5.0)
    add_box("BKNS_module", (x - 1.9, y, z + 0.65), (2.3, 1.6, 1.3), MATS["building"])
    add_pump_block("BKNS_high_pressure", (x + 1.1, y, z), count=4, water=True)
    add_cylinder("BKNS_blue_header", (x + 2.9, y, z + 0.65), 0.11, 3.6, MATS["blue"], axis="Y", vertices=16)
    add_label("БКНС\nблочная кустовая\nнасосная", (x, y - 3.0, z + 0.08), size=0.31)


def add_facility_kns(loc: Vec3):
    x, y, z = loc
    add_pad("KNS_pad", loc, 6.5, 4.5)
    add_tank("KNS_intake_tank", (x - 1.7, y + 0.4, z), radius=0.7, height=1.45, label="стоки")
    add_pump_block("KNS_transfer", (x + 1.2, y - 0.15, z), count=2, water=True)
    # Внутренняя обвязка КНС: бак -> всасывающий коллектор.
    # Напорная линия уходит к БКНС одной общей трассой в build_field(), чтобы
    # справа от КНС не было двух состыкованных рядом труб и лишних фланцев.
    pipe_path(
        "KNS_intake_tank_to_suction_header",
        [
            (x - 0.92, y + 0.40, z + 0.93),
            (x - 0.08, y + 0.40, z + 0.93),
            (x - 0.08, y + 0.30, z + 0.55),
        ],
        0.075,
        MATS["pipe_water"],
        elevated=True,
    )
    add_box("KNS_operator_box", (x + 1.2, y + 1.45, z + 0.45), (1.4, 0.8, 0.9), MATS["building"])
    add_label("КНС\nкустовая насосная", (x, y - 2.7, z + 0.08), size=0.31)


def add_facility_flare(loc: Vec3):
    """Размещает факельную установку + сепаратор из build_all.py."""
    existing = set(bpy.data.objects.keys())
    build_all_path = os.path.join(HERE, "build_all.py")
    module_name = "_field_model_build_all_flare"

    spec = importlib.util.spec_from_file_location(module_name, build_all_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load flare scene script: {build_all_path}")

    module = importlib.util.module_from_spec(spec)
    module._FLARE_IMPORT_MODE = True
    sys.modules[module_name] = module
    try:
        spec.loader.exec_module(module)
    finally:
        sys.modules.pop(module_name, None)

    new_objs = [obj for obj in bpy.data.objects if obj.name not in existing]
    cleanup_names = {"Ground", "Pad", "geo_ground", "Camera"}
    for obj in list(new_objs):
        if obj.name in cleanup_names or obj.type in {"CAMERA", "LIGHT"}:
            bpy.data.objects.remove(obj, do_unlink=True)
            new_objs.remove(obj)

    # build_all.py anchors guy wires at 20 m from the stack center (FX=0, FY=-1).
    # Put the southeastern lowest anchor (315 deg) exactly at the requested point.
    anchor_angle = math.radians(315.0)
    anchor_original = Vector((math.cos(anchor_angle) * 20.0, -1.0 + math.sin(anchor_angle) * 20.0, 0.0))
    # Shift right by distance between adjacent anchors (2 * GR * sin(45°)).
    ANCHOR_SPACING = 2 * 20.0 * math.sin(math.pi / 4)  # ≈ 28.284
    offset = Vector((17.0, 8.0, 0.0)) - anchor_original
    offset.x += ANCHOR_SPACING
    translation = Matrix.Translation(offset)
    world_matrices = {obj: obj.matrix_world.copy() for obj in new_objs}
    new_obj_names = {obj.name for obj in new_objs}
    # Only translate root objects (no parent in new_objs) — children follow via parent transform
    for obj in new_objs:
        if obj.parent is None or obj.parent.name not in new_obj_names:
            obj.matrix_world = translation @ world_matrices[obj]

    stack_loc = (offset.x, -1.0 + offset.y, 0.0)
    add_label("Факельная\nустановка", (stack_loc[0], stack_loc[1] - 2.0, 0.08), size=0.32)


def add_arrow(name: str, loc: Vec3, direction="X", material=None):
    """Плоская стрелка направления потока вместо торчащего конуса.

    Предыдущие 3D-конусы воспринимались как случайные цветные капли/маркеры.
    Здесь стрелка лежит тонкой табличкой над трубой и не маскируется под фитинг.
    """
    material = material or MATS["orange"]
    x, y, z = loc
    length = 0.86
    width = 0.36
    tail = 0.46
    thick = 0.028
    local = [
        (-length / 2, -width * 0.30, 0),
        (-length / 2 + tail, -width * 0.30, 0),
        (-length / 2 + tail, -width / 2, 0),
        (length / 2, 0, 0),
        (-length / 2 + tail, width / 2, 0),
        (-length / 2 + tail, width * 0.30, 0),
        (-length / 2, width * 0.30, 0),
    ]
    yaw = {"X": 0.0, "-X": math.pi, "Y": math.pi / 2, "-Y": -math.pi / 2}.get(direction, 0.0)
    verts_top = []
    verts_bot = []
    for lx, ly, _ in local:
        rx = lx * math.cos(yaw) - ly * math.sin(yaw)
        ry = lx * math.sin(yaw) + ly * math.cos(yaw)
        verts_top.append((x + rx, y + ry, z + thick / 2))
        verts_bot.append((x + rx, y + ry, z - thick / 2))
    n = len(local)
    verts = verts_top + verts_bot
    faces = [tuple(range(n)), tuple(range(2 * n - 1, n - 1, -1))]
    for i in range(n):
        faces.append((i, (i + 1) % n, (i + 1) % n + n, i + n))
    obj = new_mesh_obj(name, verts, faces, material)
    add_box(name + "_stand", (x, y, z - 0.11), (0.08, 0.08, 0.22), MATS["steel"])
    return obj



# ---------------------------------------------------------------------------
# Анимации: нефтекачки и движущиеся стрелки потоков
# ---------------------------------------------------------------------------


def _make_empty(name: str, loc: Vec3):
    empty = bpy.data.objects.new(name, None)
    empty.empty_display_type = "PLAIN_AXES"
    empty.empty_display_size = 0.55
    empty.location = loc
    bpy.context.collection.objects.link(empty)
    return empty


def _parent_keep_world(obj, parent):
    mw = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = mw


def _safe_obj(name: str):
    return bpy.data.objects.get(name)


def _rotate_yz_about(point_yz, pivot_yz, angle):
    py, pz = pivot_yz
    y0, z0 = point_yz
    dy, dz = y0 - py, z0 - pz
    ca, sa = math.cos(angle), math.sin(angle)
    return (py + dy * ca - dz * sa, pz + dy * sa + dz * ca)


def _circle_intersections(c1, r1, c2, r2):
    y1, z1 = c1
    y2, z2 = c2
    dy, dz = y2 - y1, z2 - z1
    d = math.hypot(dy, dz)
    if d < 1e-6 or d > r1 + r2 or d < abs(r1 - r2):
        return []
    a = (r1 * r1 - r2 * r2 + d * d) / (2 * d)
    h2 = max(0.0, r1 * r1 - a * a)
    h = math.sqrt(h2)
    ym = y1 + a * dy / d
    zm = z1 + a * dz / d
    oy = -dz / d * h
    oz = dy / d * h
    return [(ym + oy, zm + oz), (ym - oy, zm - oz)]


def _set_cylinder_between_keyframed(obj, p1: Vec3, p2: Vec3, base_len: float, frame: int):
    if obj is None:
        return
    v1, v2 = Vector(p1), Vector(p2)
    mid = (v1 + v2) / 2
    direction = v2 - v1
    length = max(direction.length, 1e-4)
    obj.location = tuple(mid)
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    obj.scale.z = length / max(base_len, 1e-4)
    obj.keyframe_insert(data_path="location", frame=frame)
    obj.keyframe_insert(data_path="rotation_euler", frame=frame)
    obj.keyframe_insert(data_path="scale", frame=frame)


def add_pumpjack_animation(name: str, loc: Vec3, scale=0.8, start_frame=1, end_frame=120):
    """Запекает кинематику реального четырёхзвенника станка-качалки.

    Фиксированы: шарнир балансира, ось кривошипа/противовесов, длина шатунов,
    точка входа подвеса в землю. Меняется только угол балансира, вращение
    кривошипа и длина вертикального подвеса.
    """
    x, y, z = loc
    s = scale
    pivot = (x, y, z + 5.28 * s)
    crank = (x, y + 3.10 * s, z + 1.45 * s)
    beam_len = 10.0 * s  # NEW: longer beam for realistic proportions
    beam_y0 = y - 0.80 * s
    beam_z = z + 5.55 * s
    beam_rear_rest = (beam_y0 + beam_len / 2 - 0.3 * s, beam_z - 0.35 * s)
    pin_rest = (y + 3.10 * s - 0.55 * s, z + 1.45 * s + 0.45 * s)
    cable_y = beam_y0 - beam_len / 2 - 0.75 * s
    cable_top_rest = (cable_y, beam_z - 1.20 * s)
    cable_ground = (x, cable_y, z + 0.9 * s)
    crank_radius = math.hypot(pin_rest[0] - crank[1], pin_rest[1] - crank[2])
    crank_rest_angle = math.atan2(pin_rest[1] - crank[2], pin_rest[0] - crank[1])
    beam_radius = math.hypot(beam_rear_rest[0] - pivot[1], beam_rear_rest[1] - pivot[2])
    beam_rest_angle = math.atan2(beam_rear_rest[1] - pivot[2], beam_rear_rest[0] - pivot[1])
    pitman_len = math.hypot(beam_rear_rest[0] - pin_rest[0], beam_rear_rest[1] - pin_rest[1])

    beam_empty = _make_empty(name + "_ANIM_beam_pivot", pivot)
    for suffix in ("_beam_top_flange", "_beam_bottom_flange", "_beam_web", "_horse_head_curved"):
        obj = _safe_obj(name + suffix)
        if obj:
            _parent_keep_world(obj, beam_empty)

    crank_empties = []
    for side in ("L", "R"):
        sx = (-0.95 if side == "L" else 0.95) * s
        empty = _make_empty(name + f"_ANIM_crank_{side}", (x + sx, crank[1], crank[2]))
        crank_empties.append(empty)
        for mid in ("_crank_axis_hub_", "_crank_arm_", "_crank_pin_hub_", "_counterweight_pin_lug_", "_counterweight_segment_"):
            obj = _safe_obj(name + mid + side)
            if obj:
                _parent_keep_world(obj, empty)
        for bolt_i in (1, 2, 3):
            obj = _safe_obj(f"{name}_counterweight_bolt_{side}_{bolt_i}")
            if obj:
                _parent_keep_world(obj, empty)

    pitmans = []
    for side in ("L", "R"):
        obj = _safe_obj(name + "_pitman_" + side)
        sx = (-0.95 if side == "L" else 0.95) * s
        top_x = x + sx * 0.45
        bot_x = x + sx
        base_len = obj.dimensions.z if obj else pitman_len
        pitmans.append((obj, top_x, bot_x, base_len))
    cable = _safe_obj(name + "_bridle_cable")
    cable_base_len = cable.dimensions.z if cable else max(0.2, cable_top_rest[1] - cable_ground[2])

    scene = bpy.context.scene
    frames = list(range(start_frame, end_frame + 1, 10))
    if frames[-1] != end_frame:
        frames.append(end_frame)
    for frame in frames:
        t = (frame - start_frame) / max(1, end_frame - start_frame)
        phase = 2 * math.pi * t
        crank_angle = crank_rest_angle + phase
        pin_yz = (crank[1] + crank_radius * math.cos(crank_angle), crank[2] + crank_radius * math.sin(crank_angle))

        # ВАЖНО: текущая декоративная геометрия качалки не является точным
        # замкнутым четырёхзвенником на полный оборот: строгий circle-solver
        # давал диапазон почти -24°..+5° и визуально "ломал" станок. Поэтому
        # для обзорной сцены используем устойчивую кинематику реальной логики:
        # оси фиксированы, кривошипы вращаются, балансир качается вокруг
        # седла в малом рабочем диапазоне, а шатуны остаются цельными связями
        # между пальцем кривошипа и задним концом балансира.
        beam_delta = math.radians(7.0) * math.sin(phase + math.radians(18))
        beam_attach = _rotate_yz_about(beam_rear_rest, (pivot[1], pivot[2]), beam_delta)
        beam_empty.rotation_euler = (beam_delta, 0, 0)
        beam_empty.keyframe_insert(data_path="rotation_euler", frame=frame)

        crank_delta = crank_angle - crank_rest_angle
        for empty in crank_empties:
            empty.rotation_euler = (crank_delta, 0, 0)
            empty.keyframe_insert(data_path="rotation_euler", frame=frame)

        for obj, top_x, bot_x, base_len in pitmans:
            _set_cylinder_between_keyframed(obj, (bot_x, pin_yz[0], pin_yz[1]), (top_x, beam_attach[0], beam_attach[1]), base_len, frame)

        cable_top_yz = _rotate_yz_about(cable_top_rest, (pivot[1], pivot[2]), beam_delta)
        _set_cylinder_between_keyframed(cable, cable_ground, (x, cable_top_yz[0], cable_top_yz[1]), cable_base_len, frame)

    # Линейная интерполяция и циклическое повторение.
    for obj in [beam_empty, *crank_empties, cable] + [p[0] for p in pitmans]:
        if obj is None or obj.animation_data is None or obj.animation_data.action is None:
            continue
        for fc in obj.animation_data.action.fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation = "BEZIER"
            try:
                fc.modifiers.new(type="CYCLES")
            except Exception:
                pass


def animate_flow_arrow(name: str, path, start_frame=1, end_frame=120):
    obj = _safe_obj(name)
    stand = _safe_obj(name + "_stand")
    if obj is None or len(path) < 2:
        return
    total = 0.0
    lengths = []
    pts = [Vector(p) for p in path]
    for a, b in zip(pts, pts[1:]):
        l = (b - a).length
        lengths.append(l)
        total += l
    if total <= 1e-6:
        return
    frames = list(range(start_frame, end_frame + 1, 8))
    if frames[-1] != end_frame:
        frames.append(end_frame)
    for frame in frames:
        t = ((frame - start_frame) / max(1, end_frame - start_frame)) % 1.0
        d = t * total
        acc = 0.0
        seg_i = 0
        for i, l in enumerate(lengths):
            if acc + l >= d:
                seg_i = i
                break
            acc += l
        a, b = pts[seg_i], pts[seg_i + 1]
        u = 0.0 if lengths[seg_i] <= 1e-6 else (d - acc) / lengths[seg_i]
        pos = a.lerp(b, u)
        direction = (b - a).normalized()
        yaw = math.atan2(direction.y, direction.x)
        obj.location = tuple(pos)
        obj.rotation_euler = (0, 0, yaw)
        obj.keyframe_insert(data_path="location", frame=frame)
        obj.keyframe_insert(data_path="rotation_euler", frame=frame)
        if stand:
            stand.location = (pos.x, pos.y, pos.z - 0.11)
            stand.keyframe_insert(data_path="location", frame=frame)
    for animated in (obj, stand):
        if animated is None or animated.animation_data is None or animated.animation_data.action is None:
            continue
        for fc in animated.animation_data.action.fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"
            try:
                fc.modifiers.new(type="CYCLES")
            except Exception:
                pass


def add_scene_animations():
    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = 120
    scene.render.fps = 24
    pump_scale = 1.0  # NEW: enlarged for visibility and correct proportions
    for i, p in enumerate([(-22, -9, 0), (-17, -11, 0), (-20, -4, 0)], start=1):
        add_pumpjack_animation(f"Producing_well_{i}", p, scale=pump_scale, start_frame=1, end_frame=120)
    animate_flow_arrow("Arrow_gathering", [(-13, -6, 1.15), (-10.5, -6, 1.15), (-10.5, -1.9, 1.15), (-7.88, -1.9, 1.15)])
    animate_flow_arrow("Arrow_dns_upsv", [(-4.70, -1.9, 1.35), (-1.0, -1.9, 1.35), (-1.0, 1.1, 1.35), (1.62, 1.1, 1.35)])
    animate_flow_arrow("Arrow_upsv_upn", [(4.98, 1.1, 1.40), (5.60, 1.1, 1.40), (5.60, -2.55, 1.30), (11.90, -2.55, 1.30), (11.90, 1.0, 1.30), (12.57, 1.0, 1.40)])
    animate_flow_arrow("Arrow_export", [(20.42, -1.85, 1.32), (29.0, -1.85, 1.32)])
    animate_flow_arrow("Arrow_water_to_bkns", [(11.35, 13.85, 1.15), (-5.18, 13.85, 1.15), (-5.18, 12.0, 1.15)])
    animate_flow_arrow("Arrow_water_to_inj", [(-2.78, 12.0, 1.25), (-2.78, 15.0, 1.25), (-0.20, 15.0, 1.25)])

# ---------------------------------------------------------------------------
# Компоновка месторождения
# ---------------------------------------------------------------------------


def build_field():
    clear_scene()
    make_materials()

    # Земля и дороги
    add_box("Field_ground", (10, 5, -0.03), (100, 60, 0.04), MATS["ground"])
    build_road_network()

    # Куст добывающих скважин
    producing = [(-22, -9, 0), (-17, -11, 0), (-20, -4, 0)]
    pump_scale = 1.0  # NEW: enlarged for visibility and correct proportions
    for i, p in enumerate(producing, start=1):
        add_pumpjack(f"Producing_well_{i}", p, scale=pump_scale)
        add_label(f"Добывающая\nскважина {i}\nс насосной\nарматурой", (p[0], p[1] - 5.0, 0.08), size=0.28)

    # Нагнетательный куст и насосные станции воды
    inj = (0, 15, 0)
    add_injection_well("Injection_well", inj)
    add_facility_kns((8, 12, 0))
    add_facility_bkns((-5, 12, 0))

    # Технологическая цепочка подготовки нефти
    add_facility_dns((-6, -1, 0))
    add_facility_upsv((5, 0, 0))
    add_facility_upn((17, 0, 0))
    add_facility_flare((15, 8, 0))

    # Трубопроводы добычи: куст -> ДНС
    manifold = (-13, -6, 0.72)
    add_cylinder("Wellpad_manifold", manifold, 0.18, 3.5, MATS["pipe_oil"], axis="Y", vertices=24)
    for i, p in enumerate(producing, start=1):
        # Труба начинается не "рядом с качалкой", а от бокового патрубка
        # устьевой арматуры под horsehead. Это убирает висящие обрезанные концы.
        wellhead_y = p[1] - 5.55 * pump_scale
        tree_scale = 0.72 * pump_scale
        tree_side = (p[0] + 0.52 * tree_scale, wellhead_y, 1.16 * tree_scale)
        wellhead_port = (p[0] + 0.90 * tree_scale, wellhead_y, 1.16 * tree_scale)
        wellhead_drop = (p[0] + 0.90 * tree_scale, p[1] - 3.65 * pump_scale, 0.68)
        add_pipe_spool(f"Flowline_well_{i}_wellhead_spool", tree_side, wellhead_port, "dn80",
                       with_flanges=True, with_support=False)
        add_cylinder(f"Flowline_well_{i}_wellhead_gate_valve", ((tree_side[0] + wellhead_port[0]) / 2, wellhead_y, 1.16 * tree_scale + 0.12), 0.085, 0.12, MATS["red"], vertices=18)
        pipe_path(
            f"Flowline_well_{i}_to_manifold",
            [wellhead_port, wellhead_drop, (p[0], manifold[1], 0.75), manifold],
            0.07,
            MATS["pipe_oil"],
            elevated=True,
        )
    pipe_path(
        "Gathering_pipeline_manifold_to_DNS",
        [manifold, (-10.5, -6, 0.9), (-10.5, -1.9, 1.05), (-7.88, -1.9, 1.05)],
        0.13,
        MATS["pipe_oil"],
        elevated=True,
    )

    # Нефтяная технологическая линия: ДНС -> УПСВ -> УПН -> экспорт
    # --- Вход эмульсии в УПСВ: стыкуется с левым nozzle separator_1 (x≈1.62),
    #     без пересечения корпуса сосуда. Левый фланец аппарата в (1.62, 1.1, 1.1). ---
    pipe_path("DNS_to_UPSV_emulsion", [(-4.70, -1.9, 1.05), (-1.0, -1.9, 1.10), (-1.0, 1.1, 1.10), (1.62, 1.1, 1.10)], 0.15, MATS["pipe_oil"])
    add_flange("UPSV_sep1_inlet_flange", (1.62, 1.1, 1.10), (-1, 0, 0), 0.15, MATS["pipe_oil"])

    # --- Выход нефти из УПСВ: от правого торца separator_1 (x=4.90) вниз под
    #     площадкой (z=0.40), затем на восток ниже всего оборудования, подъём к
    #     УПН. Не пересекает ни reagent_block, ни water_tank. ---
    pipe_path("UPSV_to_UPN_treater_inlet", [
        (4.98, 1.1, 1.20),    # правый nozzle separator_1 (фланец аппарата)
        (5.60, 1.1, 1.20),    # выход по +X перпендикулярно торцу
        (5.60, -2.55, 1.20),  # поворот на юг (торообразный отвод)
        (11.90, -2.55, 1.20), # горизонтально к УПН (укорочено — без захода на x=12.57)
        (11.90, 1.00, 1.20),  # поворот на север (торообразный отвод)
        (12.57, 1.00, 1.20),  # вход в левый nozzle treater по +X
    ], 0.15, MATS["pipe_oil"])
    add_flange("UPSV_sep1_outlet_flange", (4.98, 1.1, 1.20), (1, 0, 0), 0.15, MATS["steel"])

    # Перелив водяного бака удалён — соединяется напрямую через Produced_water_UPSV_to_KNS

    # --- Produced Water: от outlet-фланца бака на север
    #     (на высоте outlet), на запад к КНС. Без коллизий с оборудованием. ---
    pipe_path("Produced_water_UPSV_to_KNS", [
        (8.10, 0.90, 1.09),   # правый outlet бака
        (8.80, 0.90, 1.09),   # выход по +X от бака
        (8.80, 3.50, 1.09),   # на север за пределами УПСВ
        (5.52, 3.50, 1.09),   # на запад
        (5.52, 12.4, 1.09),   # на север к КНС
    ], 0.11, MATS["pipe_water"])
    add_flange("UPSV_water_tank_outlet_flange", (8.10, 0.9, 1.09), (1, 0, 0), 0.11, MATS["pipe_water"])

    # УПН / товарная нефть: прямые спулы и прямоугольные трассы с 90° отводами.

    # Treater outlet -> резервуар A: короткий прямой спул
    cylinder_between("UPN_treater_to_tank_A", (16.43, 1.00, 1.20), (16.37, 1.00, 1.20), 0.12, MATS["pipe_product"], vertices=24)
    add_flange("UPN_treater_outlet_flange", (16.43, 1.00, 1.20), (1, 0, 0), 0.12, MATS["steel"])
    add_flange("UPN_tank_A_inlet_flange", (16.37, 1.00, 1.20), (-1, 0, 0), 0.12, MATS["steel"])

    # Резервуар A -> резервуар B (уравнительная перемычка, прямой спул)
    cylinder_between("UPN_tanks_equalizing_spool", (18.63, 1.00, 1.225), (19.87, 1.00, 1.225), 0.105, MATS["pipe_product"], vertices=24)

    # Резервуар B -> насос suction: огибает танки с юга, стыкуется с верхним концом suction_header
    pipe_path("UPN_tank_B_to_pump_suction", [
        (22.13, 1.00, 1.225),   # правый nozzle tank_B
        (22.13, 1.00, 0.55),    # спуск к низкому уровню (90° отвод)
        (22.13, -0.50, 0.55),   # на юг, обходя танки (90° отвод)
        (18.02, -0.50, 0.55),   # на запад, южнее танков (90° отвод)
        (18.02, -2.40, 0.55),   # на юг к верхнему концу suction_header (90° отвод)
    ], 0.13, MATS["pipe_product"], bend_factor=2.0)
    add_flange("UPN_tank_B_outlet_flange", (22.13, 1.00, 1.225), (1, 0, 0), 0.13, MATS["steel"])

    # Насос discharge -> экспорт: от верхнего конца discharge_header вверх и на восток
    pipe_path("UPN_pump_to_export", [
        (20.42, -2.40, 0.62),  # верхний конец discharge_header
        (20.42, -2.40, 1.20),  # подъём до уровня коллектора (90° отвод)
        (29.00, -2.40, 1.20),  # горизонтально к экспорту (90° отвод)
    ], 0.16, MATS["pipe_product"], bend_factor=2.0)
    add_flange("UPN_export_boundary_flange", (29.00, -2.40, 1.20), (1, 0, 0), 0.16, MATS["steel"])
    for idx, p in enumerate([(22.70, -2.40, 1.20), (25.15, -2.40, 1.20), (27.40, -2.40, 1.20)], start=1):
        add_pipe_support_at(f"UPN_export_support_{idx}", p, 0.16, (1, 0, 0))
    add_label("товарная нефть\nна внешний\nнефтепровод", (28, -6.2, 0.08), size=0.32)

    # Вода: КНС -> БКНС -> нагнетательная скважина
    # (Produced_water_UPSV_to_KNS уже объявлена выше в блоке УПСВ)
    # КНС -> БКНС: единая чистая трасса прямо от напорного коллектора насосов.
    # За счёт этого справа от КНС нет отдельного внутреннего стояка, внешней
    # трубы поверх него и дублирующих фланцев в одной точке.
    pipe_path("KNS_to_BKNS_water", [(10.32, 12.30, 0.62), (11.35, 12.30, 0.62), (11.35, 12.30, 0.95), (11.35, 13.85, 0.95), (-5.18, 13.85, 0.95), (-5.18, 12.0, 0.95)], 0.12, MATS["pipe_water"])
    add_flange("BKNS_suction_external_tie_in_flange", (-5.18, 12.0, 0.95), (0, -1, 0), 0.12, MATS["steel"])
    pipe_path("BKNS_to_injection_well", [(-2.78, 12.0, 1.05), (-2.78, 15.0, 1.05), (-0.20, 15.0, 1.05)], 0.13, MATS["pipe_water"])
    add_flange("Injection_well_water_tie_in_flange", (-0.20, 15.0, 1.05), (1, 0, 0), 0.13, MATS["steel"])

    # Газовая линия и факел
    # Все газовые трубы на единой высоте z=2.30 с 90° торообразными отводами
    # (add_quarter_torus_elbow) на каждом повороте — через единый pipe_path.

    # DNS group_meter gas_nozzle -> газовый коллектор (продлён до встречи с трубой)
    cylinder_between("DNS_group_meter_gas_riser", (-0.3, -0.9, 1.89), (-0.3, -0.9, 2.30), 0.05, MATS["pipe_gas"], vertices=16)
    cylinder_between("DNS_group_meter_gas_tie", (-0.3, -0.9, 2.30), (-0.3, -0.7, 2.30), 0.05, MATS["pipe_gas"], vertices=16)

    # ДНС→УПСВ: от gas_nozzle DNS до T-junction над UPSV, спуск к sep2
    pipe_path("Gas_line_DNS_UPSV", [
        (-6.3, -1.9, 2.03),   # DNS gas_nozzle
        (-6.3, -1.9, 2.30),   # подъём до уровня коллектора (90° отвод)
        (-6.3, -0.7, 2.30),   # поворот на юг (90° отвод)
        (3.3, -0.7, 2.30),    # горизонтально к УПСВ (90° отвод)
        (3.3, -0.7, 1.83),    # спуск к sep2 gas_nozzle (90° отвод)
    ], 0.07, MATS["pipe_gas"])
    add_flange("DNS_gas_riser_flange", (-6.3, -1.9, 2.03), (0, 0, 1), 0.07, MATS["pipe_gas"])

    # УПСВ→УПН: от T-junction над UPSV до treater gas_nozzle
    pipe_path("Gas_line_UPSV_UPN", [
        (3.3, -0.7, 2.30),    # T-junction над sep2
        (3.3, 3.35, 2.30),    # поворот на север (90° отвод)
        (14.5, 3.35, 2.30),   # горизонтально к УПН (90° отвод)
        (14.5, 1.0, 2.30),    # поворот на юг (90° отвод)
        (14.5, 1.0, 2.00),    # спуск к treater gas_nozzle (90° отвод)
    ], 0.07, MATS["pipe_gas"])
    add_flange("UPN_treater_gas_riser_flange", (14.5, 1.0, 2.00), (0, 0, -1), 0.07, MATS["pipe_gas"])

    # Ответвление газопровода к сепаратору факельной установки
    pipe_path("Gas_line_UPN_to_flare_sep", [
        (14.5, 3.35, 2.30),    # T-junction на газопроводе
        (14.5, 3.35, 5.50),    # подъём над оборудованием
        (19.27, 3.35, 5.50),   # горизонтально на восток
        (19.27, 17.14, 5.50),  # на север к сепаратору
        (19.27, 17.14, 4.55),  # спуск к Sep_Inlet_BlindCap сверху
    ], 0.07, MATS["pipe_gas"])
    add_flange("Flare_sep_inlet_flange", (19.27, 17.14, 4.55), (0, 1, 0), 0.07, MATS["pipe_gas"])

    # Газоотвод от вентиля сепаратора к стволу факела
    pipe_path("Gas_line_sep_vent_to_stack", [
        (23.24, 17.14, 4.55),  # от Vent BlindCap
        (23.24, 17.14, 5.50),  # подъём над периллами
        (30.49, 17.14, 5.50),  # горизонтально к стволу (западная стенка ствола)
        (30.49, 22.14, 5.50),  # на север вдоль ствола
        (30.49, 22.14, 6.10),  # подъём к высоте входа
    ], 0.07, MATS["pipe_gas"])
    add_flange("Flare_sep_vent_flange", (23.24, 17.14, 4.55), (0, 0, 1), 0.07, MATS["pipe_gas"])

    # Паровая/служебная линия от ствола факела к колену KNS_to_BKNS
    pipe_path("Steam_line_stack_to_KNS", [
        (30.49, 22.14, 0.60),  # от ствола факела на уровне земли
        (30.49, 12.30, 0.60),  # на юг к KNS
        (11.35, 12.30, 0.60),  # на запад к колену KNS_to_BKNS
        (11.35, 12.30, 0.95),  # подъём к колену
    ], 0.06, MATS["pipe_gas"])
    add_flange("Stack_steam_outlet_flange", (30.49, 22.14, 0.60), (0, -1, 0), 0.06, MATS["pipe_gas"])
    add_flange("KNS_steam_inlet_flange", (11.35, 12.30, 0.95), (0, 0, 1), 0.06, MATS["pipe_gas"])

    # Стрелки потоков
    add_arrow("Arrow_gathering", (-9.0, -3.8, 1.15), direction="Y", material=MATS["orange"])
    add_arrow("Arrow_dns_upsv", (0.5, -1.0, 1.35), direction="X", material=MATS["orange"])
    add_arrow("Arrow_upsv_upn", (11.0, 0.2, 1.4), direction="X", material=MATS["orange"])
    add_arrow("Arrow_gas_to_flare", (17, 10, 3.2), direction="Y", material=MATS["pipe_gas"])
    add_arrow("Arrow_vent_to_stack", (27, 17, 5.0), direction="X", material=MATS["pipe_gas"])
    add_arrow("Arrow_steam_to_kns", (20, 12.3, 0.8), direction="-X", material=MATS["pipe_gas"])
    add_arrow("Arrow_export", (25.5, -2.2, 1.2), direction="X", material=MATS["pipe_product"])
    add_arrow("Arrow_water_to_bkns", (2.0, 12.0, 1.15), direction="-X", material=MATS["blue"])
    add_arrow("Arrow_water_to_inj", (-4.0, 15.0, 1.25), direction="X", material=MATS["blue"])

    # Легенда
    add_box("Legend_panel", (-25, 13, 0.75), (7.0, 0.12, 1.5), MATS["building"])
    add_label("Схема месторождения:\nчёрный — нефть/эмульсия\nсиний — вода на закачку\nжёлтый — товарная нефть\nголубой прозрачный — газ", (-25, 12.7, 1.05), size=0.28)

    # Baked-анимации: качалки и движущиеся стрелки потоков.
    add_scene_animations()

    # Камера и свет
    bpy.ops.object.light_add(type="SUN", location=(0, -8, 20), rotation=(math.radians(45), 0, math.radians(25)))
    bpy.context.active_object.name = "Sun_main"
    bpy.context.active_object.data.energy = 3.0
    bpy.ops.object.light_add(type="AREA", location=(0, -12, 10))
    bpy.context.active_object.name = "Area_fill"
    bpy.context.active_object.data.energy = 450
    bpy.context.active_object.data.size = 8

    # Ортографическая камера: вся схема помещается в кадр без перспективного
    # обрезания левого куста добывающих скважин.
    bpy.ops.object.camera_add(location=(1, -38, 26), rotation=(math.radians(58), 0, math.radians(4)))
    cam = bpy.context.active_object
    bpy.context.scene.camera = cam
    cam.name = "Camera_overview"
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 60

    # Рендер-настройки
    scene = bpy.context.scene
    engine_items = {e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items}
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engine_items else "BLENDER_EEVEE"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1000
    scene.view_settings.view_transform = "Filmic"
    scene.view_settings.look = "Medium High Contrast"

    # Организация: origin/cursor
    bpy.context.scene.cursor.location = (0, 0, 0)


def main():
    build_field()
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND, compress=True)
    print("Saved:", OUT_BLEND)
    print("Objects:", len(bpy.data.objects))


if __name__ == "__main__":
    main()
