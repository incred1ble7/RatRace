"""
"Fridge Heist" kitchen map (mouse scale) for Unity.

Run:  python3 build_kitchen.py   (bpy module)   or   blender -b -P build_kitchen.py

Performance by design:
  * one palette texture + one material for the whole map;
  * all visible geometry is joined into ONE mesh  -> 1 draw call;
  * collision = separate invisible boxes named COL_* -> Unity turns them into BoxColliders;
  * gameplay markers are empties (no geometry).

Conventions (Blender): Z up, 1 unit = 1 m. Back wall (fridge) is at +Y.
FBX export (Forward -Z, Up Y): Blender (x, y, z) -> Unity (x, z, y). Camera in Unity looks toward +Z.

Markers (empties) read by Unity:
  SPAWN_<n>            player spawn, arrow points into the room (+Y of the empty)
  HOLE_<n>             mouse hole (delivery zone), paired with SPAWN_<n>
  FOOD_<tier>_<nn>     food spawn point, tier 1 floor / 2 fridge / 3 table (premium)
  CATZONE_<n>          cat paw area, empty scale X = radius (m)
  CAT                  where the cat sits
"""
import math
import os

import bpy
import bmesh
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
if os.path.basename(HERE) == "Source~":
    MAP_DIR = os.path.dirname(HERE)
else:
    MAP_DIR = os.path.join(HERE, "Map")
MODEL_DIR = os.path.join(MAP_DIR, "Models")
TEX_DIR = os.path.join(MAP_DIR, "Textures")

# --------------------------------------------------------------------------
# Palette
# --------------------------------------------------------------------------
PALETTE = [
    ("tile_a", (0xE9, 0xE2, 0xD3)),
    ("tile_b", (0xC9, 0xB8, 0x9C)),
    ("wall", (0xF3, 0xDE, 0xB8)),
    ("baseboard", (0x8B, 0x5E, 0x3C)),
    ("wood", (0xB9, 0x7A, 0x48)),
    ("wood_dark", (0x7A, 0x4C, 0x2B)),
    ("board", (0xD9, 0xB0, 0x78)),
    ("cabinet", (0x6F, 0x9F, 0x8E)),
    ("counter", (0xEE, 0xEE, 0xEA)),
    ("fridge", (0xF7, 0xF8, 0xFA)),
    ("fridge_in", (0xCF, 0xDD, 0xE6)),
    ("metal", (0x9A, 0xA1, 0xA8)),
    ("black", (0x24, 0x22, 0x26)),
    ("danger", (0xE0, 0x3C, 0x3C)),
    ("bowl", (0x3F, 0x7F, 0xC8)),
    ("book_r", (0xB8, 0x42, 0x3A)),
    ("book_g", (0x4E, 0x8B, 0x55)),
    ("hole", (0x14, 0x10, 0x10)),
    ("crate", (0xC7, 0x9A, 0x5B)),
    ("window", (0xA9, 0xD8, 0xF2)),
]
PAL = {n: i for i, (n, _) in enumerate(PALETTE)}
CELL = 4  # px per cell -> 80x4 texture


def write_palette(path):
    from PIL import Image

    img = Image.new("RGB", (CELL * len(PALETTE), CELL))
    for i, (_, rgb) in enumerate(PALETTE):
        for x in range(i * CELL, (i + 1) * CELL):
            for y in range(CELL):
                img.putpixel((x, y), rgb)
    img.save(path)


def uv_of(color):
    return Vector(((PAL[color] + 0.5) / len(PALETTE), 0.5))


# --------------------------------------------------------------------------
# Mesh builder: everything visible goes into one bmesh
# --------------------------------------------------------------------------
class Builder:
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def face(self, pts, color):
        vs = [self.bm.verts.new(p) for p in pts]
        f = self.bm.faces.new(vs)
        for l in f.loops:
            l[self.uv].uv = uv_of(color)
        return f

    def box(self, mn, mx, color, top=None, bottom=False, skip=()):
        """Axis-aligned box. top: colour of +Z face. bottom faces are skipped by default (never seen)."""
        x0, y0, z0 = mn
        x1, y1, z1 = mx
        c = color
        if "pz" not in skip:
            self.face([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], top or c)
        if bottom and "nz" not in skip:
            self.face([(x0, y1, z0), (x1, y1, z0), (x1, y0, z0), (x0, y0, z0)], c)
        if "ny" not in skip:
            self.face([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], c)
        if "py" not in skip:
            self.face([(x1, y1, z0), (x0, y1, z0), (x0, y1, z1), (x1, y1, z1)], c)
        if "nx" not in skip:
            self.face([(x0, y1, z0), (x0, y0, z0), (x0, y0, z1), (x0, y1, z1)], c)
        if "px" not in skip:
            self.face([(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)], c)

    def quad_box(self, corners_bottom, height, color, top=None):
        """Box from 4 bottom corners (any orientation, CCW from above)."""
        b = [Vector(p) for p in corners_bottom]
        t = [p + Vector((0, 0, height)) for p in b]
        self.face(t, top or color)
        for i in range(4):
            j = (i + 1) % 4
            self.face([b[i], b[j], t[j], t[i]], color)

    def cylinder(self, center, r, z0, z1, color, top=None, seg=12):
        cx, cy = center
        ring0 = [(cx + r * math.cos(2 * math.pi * i / seg), cy + r * math.sin(2 * math.pi * i / seg)) for i in range(seg)]
        self.face([(x, y, z1) for x, y in ring0], top or color)
        for i in range(seg):
            a, b = ring0[i], ring0[(i + 1) % seg]
            self.face([(a[0], a[1], z0), (b[0], b[1], z0), (b[0], b[1], z1), (a[0], a[1], z1)], color)

    def ring(self, center, r0, r1, z, color, seg=24):
        cx, cy = center
        for i in range(seg):
            a0 = 2 * math.pi * i / seg
            a1 = 2 * math.pi * (i + 1) / seg
            self.face([
                (cx + r0 * math.cos(a0), cy + r0 * math.sin(a0), z),
                (cx + r1 * math.cos(a0), cy + r1 * math.sin(a0), z),
                (cx + r1 * math.cos(a1), cy + r1 * math.sin(a1), z),
                (cx + r0 * math.cos(a1), cy + r0 * math.sin(a1), z),
            ], color)

    def arch(self, x, y, r, normal_x, color, seg=8):
        """Half-disc mouse-hole decal on a wall facing +X (normal_x=1) or -X (normal_x=-1)."""
        pts = []
        for i in range(seg + 1):
            a = math.pi * i / seg
            pts.append((x, y + r * math.cos(a) * normal_x, r * math.sin(a)))
        pts.append((x, y, 0.0))
        self.face(pts, color)

    def to_object(self, name):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=0.0001)
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for p in me.polygons:
            p.use_smooth = False
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob


# --------------------------------------------------------------------------
# Colliders (invisible boxes) and markers
# --------------------------------------------------------------------------
COLLIDERS = []
MARKERS = []


def col_box(name, mn, mx, rot_z=0.0, rot_y=0.0, center=None, size=None):
    """Collision box. Either min/max (axis-aligned) or center+size+rotation."""
    if center is None:
        center = [(a + b) / 2 for a, b in zip(mn, mx)]
        size = [b - a for a, b in zip(mn, mx)]
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    me = bpy.data.meshes.new("COL_" + name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("COL_" + name, me)
    ob.location = center
    ob.scale = size
    ob.rotation_euler = (0.0, rot_y, rot_z)
    bpy.context.scene.collection.objects.link(ob)
    COLLIDERS.append(ob)
    return ob


def marker(name, loc, rot_z=0.0, scale=1.0):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_type = "SINGLE_ARROW" if name.startswith("SPAWN") else "PLAIN_AXES"
    ob.location = loc
    ob.rotation_euler = (0.0, 0.0, rot_z)
    ob.scale = (scale, scale, scale)
    bpy.context.scene.collection.objects.link(ob)
    MARKERS.append(ob)
    return ob


# --------------------------------------------------------------------------
# Layout constants (metres, mouse ~0.5 m)
# --------------------------------------------------------------------------
HX, HY = 11.0, 9.0          # inner half-size of the room
WALL_T, WALL_H = 0.5, 8.0
BASE_H, BASE_T = 0.6, 0.1
TABLE = dict(x0=-4.0, x1=4.0, y0=-2.5, y1=2.5, top=3.5, thick=0.25)
HOLE_Y = (-5.0, 2.5)        # hole positions along the side walls
HOLE_R = 0.55


def build():
    b = Builder()

    # ---- floor (checker tiles, 2 m) --------------------------------------
    tile = 2.0
    nx, ny = int(2 * HX / tile), int(2 * HY / tile)
    for i in range(nx):
        for j in range(ny):
            x0, y0 = -HX + i * tile, -HY + j * tile
            c = "tile_a" if (i + j) % 2 == 0 else "tile_b"
            b.face([(x0, y0, 0), (x0 + tile, y0, 0), (x0 + tile, y0 + tile, 0), (x0, y0 + tile, 0)], c)
    col_box("Floor", (-HX - 1, -HY - 1, -0.5), (HX + 1, HY + 1, 0.0))

    # ---- walls: back + sides (front is open towards the camera) ---------
    b.box((-HX - WALL_T, HY, 0), (HX + WALL_T, HY + WALL_T, WALL_H), "wall", skip=("py",))
    b.box((-HX - WALL_T, -HY, 0), (-HX, HY, WALL_H), "wall", skip=("nx",))
    b.box((HX, -HY, 0), (HX + WALL_T, HY, WALL_H), "wall", skip=("px",))
    col_box("Wall_Back", (-HX - WALL_T, HY, 0), (HX + WALL_T, HY + WALL_T, WALL_H))
    col_box("Wall_Left", (-HX - WALL_T, -HY - 1, 0), (-HX, HY, WALL_H))
    col_box("Wall_Right", (HX, -HY - 1, 0), (HX + WALL_T, HY, WALL_H))
    col_box("Wall_Front", (-HX - 1, -HY - 1, 0), (HX + 1, -HY, WALL_H))   # invisible

    # baseboards (decor, inside the wall colliders' thickness budget)
    b.box((-HX, HY - BASE_T, 0), (HX, HY, BASE_H), "baseboard", skip=("py",), top="baseboard")
    b.box((-HX, -HY, 0), (-HX + BASE_T, HY - BASE_T, BASE_H), "baseboard", skip=("nx",))
    b.box((HX - BASE_T, -HY, 0), (HX, HY - BASE_T, BASE_H), "baseboard", skip=("px",))

    # window on the back wall (decor)
    b.box((-2.0, HY - 0.02, 5.6), (2.0, HY, 7.6), "window", skip=("py",))
    b.box((-2.2, HY - 0.06, 5.4), (2.2, HY - 0.02, 5.6), "wood")

    # ---- mouse holes: arch decals + markers + spawns ---------------------
    n = 1
    for side in (-1, 1):
        x_face = side * (HX - BASE_T) - side * 0.005
        for hy in HOLE_Y:
            b.arch(x_face, hy, HOLE_R, -side, "hole")
            marker(f"HOLE_{n}", (side * (HX - BASE_T - 0.05), hy, 0.0), rot_z=math.radians(-90 if side < 0 else 90))  # +Y into the room
            # spawn 1.4 m into the room, arrow (+Y of empty) points to the room centre
            marker(f"SPAWN_{n}", (side * (HX - 1.4), hy, 0.0), rot_z=math.radians(-90 if side < 0 else 90))
            n += 1

    # ---- fridge (back centre, open door) ---------------------------------
    fx0, fx1, fy0, fy1, fh = -2.6, 2.6, 5.0, HY, 10.0
    t = 0.3
    b.box((fx0, fy0, 0), (fx0 + t, fy1, fh), "fridge")                 # left side
    b.box((fx1 - t, fy0, 0), (fx1, fy1, fh), "fridge")                 # right side
    b.box((fx0, fy1 - t, 0), (fx1, fy1, fh), "fridge_in", skip=("py",))  # back
    b.box((fx0, fy0, fh - t), (fx1, fy1, fh), "fridge")                # top
    b.box((fx0 + t, fy0 + 0.2, 3.2), (fx1 - t, fy1 - t, 3.35), "fridge_in", top="metal")   # shelves
    b.box((fx0 + t, fy0 + 0.2, 6.4), (fx1 - t, fy1 - t, 6.55), "fridge_in", top="metal")
    b.face([(fx0 + t, fy0, 0.005), (fx1 - t, fy0, 0.005), (fx1 - t, fy1 - t, 0.005), (fx0 + t, fy1 - t, 0.005)], "fridge_in")
    # door hinged on the right, swung open ~150 deg towards the right counter (stays clear of the table)
    hinge = Vector((fx1, fy0, 0))
    ang = math.radians(150)
    w, d = fx1 - fx0, 0.35
    dir_w = Vector((-math.cos(ang), -math.sin(ang), 0))      # from hinge along the door width
    dir_d = Vector((math.sin(ang), -math.cos(ang), 0))       # door thickness direction
    p0 = hinge
    p1 = hinge + dir_w * w
    p2 = p1 + dir_d * d
    p3 = hinge + dir_d * d
    b.quad_box([p0, p3, p2, p1], fh - 0.4, "fridge", top="fridge")
    door_center = (p0 + p1 + p2 + p3) / 4
    col_box("Fridge_L", (fx0, fy0, 0), (fx0 + t, fy1, fh))
    col_box("Fridge_R", (fx1 - t, fy0, 0), (fx1, fy1, fh))
    col_box("Fridge_Back", (fx0, fy1 - t, 0), (fx1, fy1, fh))
    col_box("Fridge_Door", None, None, rot_z=ang, center=(door_center.x, door_center.y, (fh - 0.4) / 2),
            size=(w, d, fh - 0.4))

    # ---- counters (back-left with sink, back-right with stove) ----------
    for side in (-1, 1):
        cx0, cx1 = (-HX, -4.0) if side < 0 else (4.0, HX)
        cy0, cy1 = 6.0, HY
        b.box((cx0, cy0 + 0.35, 0), (cx1, cy1, 0.4), "black", skip=("nz",))           # toe kick
        b.box((cx0, cy0, 0.4), (cx1, cy1, 4.3), "cabinet")
        b.box((cx0 - 0.0, cy0 - 0.15, 4.3), (cx1, cy1, 4.5), "counter")               # worktop
        # door panels (decor)
        n_doors = 3
        dw = (cx1 - cx0) / n_doors
        for k in range(n_doors):
            x0 = cx0 + k * dw + 0.15
            b.box((x0, cy0 - 0.04, 0.7), (x0 + dw - 0.3, cy0, 4.0), "cabinet", top="cabinet")
            b.box((x0 + dw / 2 - 0.35, cy0 - 0.1, 3.4), (x0 + dw / 2 - 0.15, cy0 - 0.04, 3.7), "metal")
        # upper cabinets
        b.box((cx0, HY - 1.6, 6.2), (cx1, HY, 7.8), "cabinet")
        col_box(f"Counter_{'L' if side < 0 else 'R'}", (cx0, cy0 - 0.15, 0), (cx1, cy1, 4.5))
        if side < 0:   # sink
            b.box((-9.0, 6.6, 4.5), (-6.0, 8.4, 4.52), "metal")
            b.box((-8.6, 6.9, 4.52), (-6.4, 8.1, 4.53), "black")
            b.box((-7.6, 8.4, 4.5), (-7.4, 8.6, 5.6), "metal")
        else:          # stove
            b.box((5.5, 6.5, 4.5), (9.5, 8.5, 4.52), "black")
            for (sx, sy) in ((6.5, 7.0), (8.5, 7.0), (6.5, 8.0), (8.5, 8.0)):
                b.ring((sx, sy), 0.3, 0.42, 4.53, "metal", seg=12)

    # ---- dining table ------------------------------------------------------
    T = TABLE
    top_z0 = T["top"] - T["thick"]
    b.box((T["x0"], T["y0"], top_z0), (T["x1"], T["y1"], T["top"]), "wood", top="wood", bottom=True)
    col_box("Table_Top", (T["x0"], T["y0"], top_z0), (T["x1"], T["y1"], T["top"]))
    leg = 0.35
    for lx in (T["x0"] + 0.3, T["x1"] - 0.3 - leg):
        for ly in (T["y0"] + 0.3, T["y1"] - 0.3 - leg):
            b.box((lx, ly, 0), (lx + leg, ly + leg, top_z0), "wood_dark", skip=("pz",))
            col_box(f"Table_Leg_{lx:.0f}_{ly:.0f}", (lx, ly, 0), (lx + leg, ly + leg, top_z0))

    # cat paw danger zones on the table (red rings) + markers
    zones = [(-2.4, 0.0, 1.1), (0.0, 0.0, 1.1), (2.4, 0.0, 1.1)]
    for i, (zx, zy, zr) in enumerate(zones, 1):
        b.ring((zx, zy), zr - 0.12, zr, T["top"] + 0.006, "danger")
        marker(f"CATZONE_{i}", (zx, zy, T["top"]), scale=zr)
    marker("CAT", (0.0, 4.0, 0.0), rot_z=math.radians(180))

    # ---- ramps: boards from the floor up to the table ends --------------
    rw, rt = 1.4, 0.15
    for side in (-1, 1):
        x_low, x_high = side * 10.0, side * T["x1"]
        z_high = T["top"]
        y0, y1 = -rw / 2, rw / 2
        # top surface from (x_low, 0) to (x_high, z_high); thickness below
        length = math.hypot(x_high - x_low, z_high)
        nzx = -side * z_high / length          # board normal (x, z) scaled
        nzz = abs(x_high - x_low) / length
        off = Vector((nzx * rt, 0, -nzz * rt))   # = -normal * thickness
        a = Vector((x_low, 0, 0.0)); c = Vector((x_high, 0, z_high))
        tl = [Vector((a.x, y0, a.z)), Vector((c.x, y0, c.z)), Vector((c.x, y1, c.z)), Vector((a.x, y1, a.z))]
        if side > 0:
            tl = [tl[0], tl[3], tl[2], tl[1]]
        bl = [p + off for p in tl]
        b.face(tl, "board")
        for i in range(4):
            j = (i + 1) % 4
            b.face([bl[i], bl[j], tl[j], tl[i]], "board")
        ang_y = math.atan2(z_high, abs(x_high - x_low)) * side
        mid = (a + c) / 2 + off / 2
        col_box(f"Ramp_{'L' if side < 0 else 'R'}", None, None, rot_y=ang_y, center=(mid.x, 0.0, mid.z),
                size=(length, rw, rt))
        # book stacks under the board: a staircase that fills the space below it.
        # Each stack stops just under the board (never pokes through) and is solid,
        # so mice can neither walk through the books nor crawl under the ramp.
        run = abs(x_high - x_low)
        under = rt / math.cos(math.atan2(z_high, run))          # vertical thickness of the board
        def underside(u):                                        # u = distance from room centre
            return (abs(x_low) - u) / run * z_high - under
        spans = [(4.0, 5.3), (5.3, 6.3), (6.3, 7.3), (7.3, 8.3), (8.3, 9.3)]
        palette = ("book_r", "book_g", "board", "book_r", "book_g")
        for k, (u0, u1) in enumerate(spans):
            h = underside(u1) - 0.02
            if h <= 0.05:
                continue
            xa, xb = sorted((side * u0, side * u1))
            ya, yb = -rw / 2 + 0.05, rw / 2 - 0.05
            layers = max(1, math.ceil(h / 0.35))
            lh = h / layers
            for L in range(layers):
                col = palette[(k + L) % len(palette)]
                inset = 0.04 if L % 2 else 0.0                    # slightly uneven pile, looks like books
                b.box((xa + inset, ya + inset, L * lh), (xb - inset, yb - inset, (L + 1) * lh), col, top=col)
            col_box(f"Books_{'L' if side < 0 else 'R'}_{k}", (xa, ya, 0.0), (xb, yb, h))

    # ---- chairs (obstacles / cover) -------------------------------------
    for cx in (-2.2, 2.2):
        cy = -4.2
        s = 1.1
        b.box((cx - s, cy - s, 2.0), (cx + s, cy + s, 2.2), "wood", bottom=True)
        for lx in (cx - s + 0.1, cx + s - 0.35):
            for ly in (cy - s + 0.1, cy + s - 0.35):
                b.box((lx, ly, 0), (lx + 0.25, ly + 0.25, 2.0), "wood_dark", skip=("pz",))
                col_box(f"Chair_Leg_{cx:.0f}_{lx:.1f}_{ly:.1f}", (lx, ly, 0), (lx + 0.25, ly + 0.25, 2.0))
        b.box((cx - s, cy - s, 2.2), (cx + s, cy - s + 0.2, 4.4), "wood")

    # ---- obstacles: cat bowl, crate --------------------------------------
    b.cylinder((-7.0, -6.2), 0.9, 0.0, 0.45, "bowl", top="black")
    col_box("Bowl", (-7.9, -7.1, 0), (-6.1, -5.3, 0.45))
    b.box((6.2, -7.4, 0), (8.0, -5.6, 1.6), "crate", top="crate")
    b.box((6.2, -7.45, 0.7), (8.0, -7.4, 0.9), "wood_dark")
    col_box("Crate", (6.2, -7.4, 0), (8.0, -5.6, 1.6))

    # ---- food spawn markers ------------------------------------------------
    food = []
    # tier 2: inside / in front of the fridge
    food += [(2, -1.2, 7.0), (2, 0.0, 7.6), (2, 1.2, 7.0), (2, -0.6, 4.2), (2, 0.8, 4.0)]
    # tier 3: on the table (premium, cat zones)
    food += [(3, -2.4, 0.0), (3, 0.0, 0.8), (3, 2.4, 0.0), (3, -1.2, -1.4), (3, 1.2, 1.4)]
    # tier 1: on the floor
    food += [(1, -8.0, 4.8), (1, 8.0, 4.8), (1, -4.5, -6.5), (1, 4.5, -6.5), (1, 0.0, -6.8),
             (1, -8.5, -1.8), (1, 8.5, -1.8)]
    for k, (tier, x, y) in enumerate(food, 1):
        z = T["top"] if tier == 3 else 0.0
        marker(f"FOOD_{tier}_{k:02d}", (x, y, z))

    vis = b.to_object("Kitchen_Visual")
    return vis


def make_material(obj, tex_path):
    mat = bpy.data.materials.new("M_Kitchen")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(tex_path)
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.9
    obj.data.materials.append(mat)


def group(name, objs):
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    for o in objs:
        o.parent = root
    return root


def main():
    os.makedirs(MODEL_DIR, exist_ok=True)
    os.makedirs(TEX_DIR, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scn = bpy.context.scene
    scn.unit_settings.system = "METRIC"

    tex_path = os.path.join(TEX_DIR, "T_Kitchen_Palette.png")
    write_palette(tex_path)

    vis = build()
    make_material(vis, tex_path)
    root = bpy.data.objects.new("Kitchen", None)
    scn.collection.objects.link(root)
    vis.parent = root
    group("Colliders", COLLIDERS).parent = root
    group("Markers", MARKERS).parent = root

    tris = sum(len(p.vertices) - 2 for p in vis.data.polygons)
    print(f"[kitchen] visual tris={tris} verts={len(vis.data.vertices)} colliders={len(COLLIDERS)} markers={len(MARKERS)}")

    for o in scn.objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(MODEL_DIR, "Kitchen.fbx"),
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=False,
        mesh_smooth_type="FACE",
        bake_anim=False,
        path_mode="STRIP",
        embed_textures=False,
    )
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Kitchen.blend"))
    print("[kitchen] done")


if __name__ == "__main__":
    main()
