"""
Low-poly mouse for a Unity mobile game.
Builds mesh + armature + animations (Idle, Run, Carry) procedurally and exports FBX.

Run:  python3 build_mouse.py            (bpy module)
  or: blender -b -P build_mouse.py

Conventions (Blender): mouse faces -Y, Z up, 1 unit = 1 m, origin on the ground.
FBX export (Forward -Z, Up Y) -> in Unity the mouse faces +Z.
"""
import math
import os
import sys

import bpy  # must be imported before bmesh/mathutils
import bmesh
from mathutils import Euler, Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
if os.path.basename(HERE) == "Source~":
    # script lives inside Assets/Mouse/Source~ -> write next to it
    UNITY_DIR = ROOT
else:
    UNITY_DIR = os.path.join(ROOT, "Unity", "Assets", "Mouse")
MODEL_DIR = os.path.join(UNITY_DIR, "Models")
TEX_DIR = os.path.join(UNITY_DIR, "Textures")
FPS = 30

# --------------------------------------------------------------------------
# Palette texture (one tiny texture, one material for the whole mouse)
# --------------------------------------------------------------------------
PALETTE = [
    ("fur", (0x9C, 0x98, 0xA6)),
    ("belly", (0xDD, 0xD8, 0xE0)),
    ("pink", (0xF2, 0xA6, 0xB2)),
    ("nose", (0xE0, 0x6F, 0x84)),
    ("eye", (0x1C, 0x1B, 0x24)),
    ("white", (0xFF, 0xFF, 0xFF)),
    ("cheese", (0xF6, 0xC9, 0x45)),
    ("cheese_dark", (0xD8, 0x9E, 0x2A)),
]
PAL = {name: i for i, (name, _) in enumerate(PALETTE)}
CELL = 8  # px per cell -> 64x8 texture


def write_palette(path):
    from PIL import Image

    img = Image.new("RGB", (CELL * len(PALETTE), CELL))
    for i, (_, rgb) in enumerate(PALETTE):
        for x in range(i * CELL, (i + 1) * CELL):
            for y in range(CELL):
                img.putpixel((x, y), rgb)
    img.save(path)


def uv_of(color):
    i = PAL[color]
    return Vector(((i + 0.5) / len(PALETTE), 0.5))


# --------------------------------------------------------------------------
# Scene reset
# --------------------------------------------------------------------------
def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scn = bpy.context.scene
    scn.render.fps = FPS
    scn.unit_settings.system = "METRIC"
    scn.unit_settings.scale_length = 1.0
    return scn


# --------------------------------------------------------------------------
# Skeleton definition (armature space, metres)
# --------------------------------------------------------------------------
TAIL_PTS = [
    Vector((0, 0.150, 0.085)),
    Vector((0, 0.235, 0.060)),
    Vector((0, 0.320, 0.045)),
    Vector((0, 0.400, 0.045)),
    Vector((0, 0.470, 0.065)),
]

BONES = [
    # name, head, tail, parent, deform
    ("Root", (0, 0, 0), (0, -0.06, 0), None, False),
    ("Body", (0, 0.07, 0.11), (0, 0.0, 0.11), "Root", True),
    ("Spine", (0, 0.0, 0.11), (0, -0.09, 0.12), "Body", True),
    ("Head", (0, -0.09, 0.12), (0, -0.21, 0.135), "Spine", True),
    ("Ear_L", (0.050, -0.115, 0.185), (0.062, -0.112, 0.245), "Head", True),
    ("Ear_R", (-0.050, -0.115, 0.185), (-0.062, -0.112, 0.245), "Head", True),
    ("Leg_FL", (0.048, -0.070, 0.085), (0.048, -0.072, 0.008), "Spine", True),
    ("Leg_FR", (-0.048, -0.070, 0.085), (-0.048, -0.072, 0.008), "Spine", True),
    ("Leg_BL", (0.058, 0.085, 0.090), (0.058, 0.080, 0.008), "Body", True),
    ("Leg_BR", (-0.058, 0.085, 0.090), (-0.058, 0.080, 0.008), "Body", True),
    ("Tail_1", tuple(TAIL_PTS[0]), tuple(TAIL_PTS[1]), "Body", True),
    ("Tail_2", tuple(TAIL_PTS[1]), tuple(TAIL_PTS[2]), "Tail_1", True),
    ("Tail_3", tuple(TAIL_PTS[2]), tuple(TAIL_PTS[3]), "Tail_2", True),
    ("Tail_4", tuple(TAIL_PTS[3]), tuple(TAIL_PTS[4]), "Tail_3", True),
    # Attach point for carried food (between the front paws in the Carry pose)
    ("Food_Socket", (0, -0.170, 0.078), (0, -0.170, 0.108), "Spine", False),
]
DEFORM_BONES = [b[0] for b in BONES if b[4]]


# --------------------------------------------------------------------------
# Mesh building helpers (single bmesh, per-part colour + weights)
# --------------------------------------------------------------------------
class MeshBuilder:
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()
        self.dl = self.bm.verts.layers.deform.verify()
        self.gidx = {name: i for i, name in enumerate(DEFORM_BONES)}

    def _finish(self, verts, color_fn, weight_fn):
        verts = list(verts)
        faces = set()
        for v in verts:
            faces.update(v.link_faces)
        self.bm.normal_update()
        for f in faces:
            f.smooth = False
            c = color_fn(f) if callable(color_fn) else color_fn
            uv = uv_of(c)
            for loop in f.loops:
                loop[self.uv].uv = uv
        for v in verts:
            w = weight_fn(v.co) if callable(weight_fn) else {weight_fn: 1.0}
            total = sum(w.values())
            for bone, val in w.items():
                if val > 1e-4:
                    v[self.dl][self.gidx[bone]] = val / total
        return verts

    def sphere(self, center, radii, seg, rings, color, weights, shaper=None, rot=None):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=rings, radius=1.0)
        verts = res["verts"]
        R = rot.to_matrix() if rot else Matrix.Identity(3)
        for v in verts:
            p = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2]))
            if shaper:
                p = shaper(p)
            v.co = R @ p + Vector(center)
        return self._finish(verts, color, weights)

    def cone(self, center, r1, r2, depth, seg, color, weights, rot=None, scale=(1, 1, 1), caps=True):
        res = bmesh.ops.create_cone(
            self.bm, cap_ends=caps, cap_tris=False, segments=seg,
            radius1=r1, radius2=r2, depth=depth,
        )
        verts = res["verts"]
        R = rot.to_matrix() if rot else Matrix.Identity(3)
        for v in verts:
            p = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2]))
            v.co = R @ p + Vector(center)
        return self._finish(verts, color, weights)

    def tube(self, pts, radii, sides, color, weights):
        """Tube along a polyline, closed with a cap at the start and a point at the end."""
        rings = []
        up = Vector((0, 0, 1))
        for i, p in enumerate(pts):
            if i == 0:
                d = (pts[1] - pts[0]).normalized()
            elif i == len(pts) - 1:
                d = (pts[-1] - pts[-2]).normalized()
            else:
                d = (pts[i + 1] - pts[i - 1]).normalized()
            side = d.cross(up).normalized()
            nrm = side.cross(d).normalized()
            ring = []
            for k in range(sides):
                a = 2 * math.pi * k / sides
                off = (side * math.cos(a) + nrm * math.sin(a)) * radii[i]
                ring.append(self.bm.verts.new(p + off))
            rings.append(ring)
        tip = self.bm.verts.new(pts[-1] + (pts[-1] - pts[-2]).normalized() * radii[-1] * 2)
        for a, b in zip(rings[:-1], rings[1:]):
            for k in range(sides):
                self.bm.faces.new((a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k]))
        last = rings[-1]
        for k in range(sides):
            self.bm.faces.new((last[k], last[(k + 1) % sides], tip))
        self.bm.faces.new(list(reversed(rings[0])))
        verts = [v for r in rings for v in r] + [tip]
        return self._finish(verts, color, weights)

    def to_object(self, name):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        obj = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(obj)
        for bname in DEFORM_BONES:
            obj.vertex_groups.new(name=bname)
        return obj


def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def build_mouse_mesh():
    mb = MeshBuilder()

    # ---- Body: egg shape, wider at the rear, light belly ------------------
    def body_shape(p):
        # rear (+Y) slightly bigger, front narrower
        k = 1.0 + 0.12 * (p.y / 0.14)
        return Vector((p.x * k, p.y, p.z * k))

    def body_weights(co):
        t = smoothstep(-0.05, 0.05, co.y)  # 0 at front, 1 at rear
        return {"Spine": 1 - t, "Body": t}

    def body_color(f):
        return "belly" if f.normal.z < -0.45 else "fur"

    mb.sphere((0, 0.015, 0.105), (0.082, 0.135, 0.078), 10, 7,
              body_color, body_weights, shaper=body_shape)

    # ---- Head: rounded with a tapered snout -------------------------------
    def head_shape(p):
        # p is local around head centre; taper toward -Y (snout)
        if p.y < 0:
            t = min(1.0, -p.y / 0.085)
            s = 1.0 - 0.62 * t
            return Vector((p.x * s, p.y * 1.15, p.z * s - 0.012 * t))
        return p

    def head_color(f):
        c = f.calc_center_median()
        return "belly" if (f.normal.z < -0.5 and c.y < -0.14) else "fur"

    mb.sphere((0, -0.145, 0.142), (0.068, 0.085, 0.064), 10, 7, head_color, "Head",
              shaper=head_shape)

    # nose, eyes (+ highlight)
    mb.sphere((0, -0.247, 0.130), (0.016, 0.016, 0.015), 5, 3, "nose", "Head")
    for sx in (1, -1):
        mb.sphere((sx * 0.037, -0.190, 0.166), (0.014, 0.012, 0.015), 5, 3, "eye", "Head")
        mb.sphere((sx * 0.041, -0.199, 0.173), (0.0045, 0.0045, 0.0045), 4, 2, "white", "Head")

    # ---- Ears: flat discs, pink on the front face -------------------------
    for sx, bone in ((1, "Ear_L"), (-1, "Ear_R")):
        rot = Euler((math.radians(90 - 15), 0, math.radians(sx * -25)), "XYZ")
        fwd = rot.to_matrix() @ Vector((0, 0, 1))

        def ear_color(f, fwd=fwd):
            return "pink" if f.normal.dot(fwd) > 0.7 else "fur"

        mb.cone((sx * 0.058, -0.112, 0.222), 0.046, 0.046, 0.010, 8, ear_color, bone,
                rot=rot, scale=(1.0, 1.12, 1.0))

    # ---- Legs --------------------------------------------------------------
    for sx, bone in ((1, "Leg_FL"), (-1, "Leg_FR")):
        x = sx * 0.048
        mb.cone((x, -0.071, 0.050), 0.013, 0.018, 0.075, 5, "fur", bone, caps=False)
        mb.sphere((x, -0.080, 0.008), (0.016, 0.022, 0.008), 5, 3, "pink", bone)
    for sx, bone in ((1, "Leg_BL"), (-1, "Leg_BR")):
        x = sx * 0.058
        # haunch
        mb.sphere((x, 0.075, 0.085), (0.030, 0.050, 0.045), 6, 4, "fur", bone)
        mb.cone((x, 0.082, 0.040), 0.013, 0.018, 0.065, 5, "fur", bone, caps=False)
        mb.sphere((x, 0.064, 0.008), (0.017, 0.034, 0.008), 5, 3, "pink", bone)

    # ---- Tail: smooth curve through TAIL_PTS, weights blended per segment --
    pts, radii = [], []
    n_sub = 2
    seg_len = len(TAIL_PTS) - 1
    for i in range(seg_len):
        for s in range(n_sub):
            t = s / n_sub
            pts.append(TAIL_PTS[i].lerp(TAIL_PTS[i + 1], t))
    pts.append(TAIL_PTS[-1].copy())
    for k in range(len(pts)):
        u = k / (len(pts) - 1)
        radii.append(0.013 * (1 - u) + 0.004 * u)

    tail_bones = ["Tail_1", "Tail_2", "Tail_3", "Tail_4"]
    seg_lengths = [(TAIL_PTS[i + 1] - TAIL_PTS[i]).length for i in range(seg_len)]

    def tail_weights(co):
        # project to closest segment, blend toward neighbours near joints
        best, best_d, best_t = 0, 1e9, 0
        for i in range(seg_len):
            a, b = TAIL_PTS[i], TAIL_PTS[i + 1]
            ab = b - a
            t = max(0.0, min(1.0, (co - a).dot(ab) / ab.length_squared))
            d = (a + ab * t - co).length
            if d < best_d:
                best, best_d, best_t = i, d, t
        w = {tail_bones[best]: 1.0}
        if best == 0 and best_t < 0.25:
            # root of tail blends into body
            w = {"Tail_1": 0.5 + 2 * best_t, "Body": 0.5 - 2 * best_t}
        elif best_t < 0.2 and best > 0:
            k = 0.5 + 2.5 * best_t
            w = {tail_bones[best]: k, tail_bones[best - 1]: 1 - k}
        elif best_t > 0.8 and best < seg_len - 1:
            k = 0.5 + 2.5 * (1 - best_t)
            w = {tail_bones[best]: k, tail_bones[best + 1]: 1 - k}
        return w

    mb.tube(pts, radii, 5, "pink", tail_weights)

    obj = mb.to_object("Mouse_Mesh")
    return obj


def build_cheese_mesh():
    """Small low-poly cheese wedge prop, origin at its centre."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.verify()
    w, d, h = 0.058, 0.050, 0.032
    tri = [Vector((-w / 2, -d / 2, 0)), Vector((w / 2, -d / 2, 0)), Vector((0, d / 2, 0))]
    c = (tri[0] + tri[1] + tri[2]) / 3
    tri = [p - c for p in tri]
    bot = [bm.verts.new(p + Vector((0, 0, -h / 2))) for p in tri]
    top = [bm.verts.new(p + Vector((0, 0, h / 2))) for p in tri]
    faces = [bm.faces.new(list(reversed(bot))), bm.faces.new(top)]
    for i in range(3):
        j = (i + 1) % 3
        faces.append(bm.faces.new((bot[i], bot[j], top[j], top[i])))
    # holes: small dark discs on the long side face
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = False
        col = "cheese"
        for loop in f.loops:
            loop[uv].uv = uv_of(col)
    hole_faces = []
    for (hx, hz, r) in ((-0.010, 0.003, 0.006), (0.012, -0.005, 0.0045)):
        res = bmesh.ops.create_circle(bm, cap_ends=True, segments=6, radius=r)
        n = Vector((0, -1, 0))
        for v in res["verts"]:
            v.co = Vector((v.co.x + hx, -d / 2 - c.y - 0.0008, v.co.y + hz))
        for v in res["verts"]:
            for f in v.link_faces:
                hole_faces.append(f)
    bm.normal_update()
    for f in set(hole_faces):
        if f.normal.y > 0:
            f.normal_flip()
        f.smooth = False
        for loop in f.loops:
            loop[uv].uv = uv_of("cheese_dark")
    me = bpy.data.meshes.new("Cheese")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("Cheese", me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


# --------------------------------------------------------------------------
# Armature
# --------------------------------------------------------------------------
def build_armature():
    arm_data = bpy.data.armatures.new("MouseRig")
    arm = bpy.data.objects.new("MouseRig", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    ebs = {}
    for name, head, tail, parent, deform in BONES:
        eb = arm_data.edit_bones.new(name)
        eb.head = Vector(head)
        eb.tail = Vector(tail)
        eb.roll = 0.0
        eb.use_deform = deform
        if parent:
            eb.parent = ebs[parent]
            eb.use_connect = False
        ebs[name] = eb
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return arm


def skin(mesh_obj, arm):
    mesh_obj.parent = arm
    mod = mesh_obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm


def make_material(objs, tex_path):
    mat = bpy.data.materials.new("M_Mouse")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Roughness"].default_value = 0.85
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(tex_path)
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(mat)
    return mat


# --------------------------------------------------------------------------
# Animation helpers: rotations/offsets are given in ARMATURE space and
# converted to each bone's local rest space, so bone roll does not matter.
# --------------------------------------------------------------------------
def arm_to_local_quat(bone, euler_deg, order="XYZ"):
    r_arm = Euler([math.radians(a) for a in euler_deg], order).to_matrix()
    rest = bone.matrix_local.to_3x3()
    return (rest.inverted() @ r_arm @ rest).to_quaternion()


def arm_to_local_vec(bone, vec):
    rest = bone.matrix_local.to_3x3()
    return rest.inverted() @ Vector(vec)


def two_pi(x):
    return 2 * math.pi * x


def bump(t, c, w):
    """Smooth 0..1..0 bump centred at c with half-width w."""
    d = abs(t - c)
    if d >= w:
        return 0.0
    return 0.5 * (1 + math.cos(math.pi * d / w))


def pose_idle(t):
    s = math.sin
    p = {}
    breathe = s(two_pi(2 * t))
    p["Body"] = ((0.8 * breathe, 0, 0), (0, 0, 0.0025 * breathe))
    p["Spine"] = ((-1.5 * breathe, 0, 0), (0, 0, 0))
    sniff = 3.5 * s(two_pi(9 * t)) * (bump(t, 0.68, 0.14))
    look = 14 * s(two_pi(t)) * (0.6 + 0.4 * s(two_pi(t) + 1.0))
    p["Head"] = ((-4 + sniff + 1.0 * breathe, 0, look), (0, 0, 0))
    p["Ear_L"] = ((-14 * bump(t, 0.30, 0.05), 0, 0), (0, 0, 0))
    p["Ear_R"] = ((-14 * bump(t, 0.82, 0.05), 0, 0), (0, 0, 0))
    for i in range(4):
        sway = (6 + 3 * i) * s(two_pi(t) - 0.7 * i)
        lift = 2.5 * s(two_pi(2 * t) - 0.9 * i)
        p[f"Tail_{i + 1}"] = ((lift, 0, sway), (0, 0, 0))
    return p


def pose_run(t):
    s = math.sin
    p = {}
    ph = two_pi(t)
    # bound gait: hind pair together, fore pair half a cycle later, small L/R offset
    p["Leg_BL"] = ((-38 * s(ph), 0, 0), (0, 0, 0))
    p["Leg_BR"] = ((-38 * s(ph - 0.35), 0, 0), (0, 0, 0))
    p["Leg_FL"] = ((-42 * s(ph + math.pi), 0, 0), (0, 0, 0))
    p["Leg_FR"] = ((-42 * s(ph + math.pi - 0.35), 0, 0), (0, 0, 0))
    pitch = 7 * s(ph + math.pi / 2)
    p["Body"] = ((pitch, 0, 0), (0, 0, 0.004 + 0.012 * s(two_pi(2 * t) - 0.6)))
    p["Spine"] = ((-0.8 * pitch, 0, 0), (0, 0, 0))
    p["Head"] = ((-6 - 0.4 * pitch, 0, 0), (0, 0, 0))
    p["Ear_L"] = ((-28 + 4 * s(ph), 0, 0), (0, 0, 0))
    p["Ear_R"] = ((-28 + 4 * s(ph - 0.3), 0, 0), (0, 0, 0))
    for i in range(4):
        lift = 9 if i == 0 else 3
        p[f"Tail_{i + 1}"] = ((lift + 6 * s(ph - 0.8 * (i + 1)), 0,
                               (5 + 4 * i) * s(ph - 0.9 * i)), (0, 0, 0))
    return p


CARRY_POSE = {
    "Leg_FL": ((-78, 0, -22), (0, 0, 0)),
    "Leg_FR": ((-78, 0, 22), (0, 0, 0)),
    "Head": ((-20, 0, 0), (0, 0, 0)),
}


def pose_carry(t):
    return CARRY_POSE


CLIPS = [
    # name, frames (last frame == first frame for seamless loop), pose fn
    ("Idle", 60, pose_idle),
    ("Run", 16, pose_run),
    ("Carry", 10, pose_carry),
]


# Every clip keys the same channel set so Idle/Run/Carry blend cleanly in Unity;
# static bones (Root, Food_Socket) and unused translation channels are not keyed.
ANIMATED_BONES = {b[0] for b in BONES if b[0] not in ("Root", "Food_Socket")}
TRANSLATED_BONES = {"Body"}


def bake_clip(arm, name, frames, fn):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = action
    bones = arm.data.bones
    for f in range(frames + 1):
        t = f / frames
        pose = fn(t)
        for pb in arm.pose.bones:
            rot, loc = pose.get(pb.name, ((0, 0, 0), (0, 0, 0)))
            # yaw (Z) applied after pitch (X) so a leg first swings, then turns in
            pb.rotation_quaternion = arm_to_local_quat(bones[pb.name], rot, "XYZ")
            pb.location = arm_to_local_vec(bones[pb.name], loc)
            pb.scale = (1, 1, 1)
            if pb.name in ANIMATED_BONES:
                pb.keyframe_insert("rotation_quaternion", frame=f, group=pb.name)
            if pb.name in TRANSLATED_BONES:
                pb.keyframe_insert("location", frame=f, group=pb.name)
    action.frame_range = (0, frames)
    try:
        action.use_frame_range = True
    except AttributeError:
        pass
    return action


def reset_pose(arm):
    for pb in arm.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)


# --------------------------------------------------------------------------
# Export
# --------------------------------------------------------------------------
def select_only(objs):
    for o in bpy.context.scene.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def export_fbx(path, objs, animated):
    select_only(objs)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=False,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=False,
        armature_nodetype="NULL",
        bake_anim=animated,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=animated,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=1.0,  # drop redundant keys
        path_mode="STRIP",
        embed_textures=False,
    )


def main():
    os.makedirs(MODEL_DIR, exist_ok=True)
    os.makedirs(TEX_DIR, exist_ok=True)
    reset_scene()

    tex_path = os.path.join(TEX_DIR, "T_Mouse_Palette.png")
    write_palette(tex_path)

    mesh = build_mouse_mesh()
    arm = build_armature()
    skin(mesh, arm)
    cheese = build_cheese_mesh()
    make_material([mesh, cheese], tex_path)

    for name, frames, fn in CLIPS:
        bake_clip(arm, name, frames, fn)
    arm.animation_data.action = bpy.data.actions["Idle"]
    bpy.context.scene.frame_start = 0
    bpy.context.scene.frame_end = 60

    tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    print(f"[mouse] verts={len(mesh.data.vertices)} tris={tris} bones={len(arm.data.bones)}")

    export_fbx(os.path.join(MODEL_DIR, "Mouse.fbx"), [arm, mesh], animated=True)

    cheese.location = (0, 0, 0)
    export_fbx(os.path.join(MODEL_DIR, "Cheese.fbx"), [cheese], animated=False)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Mouse.blend"))
    print("[mouse] done")


if __name__ == "__main__":
    main()
