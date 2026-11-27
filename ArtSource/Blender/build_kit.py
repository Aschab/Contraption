# Builds the Contraption modular machine kit in Blender and exports FBX pieces to Unity.
# Run inside Blender: exec(open(<this file>).read()). Convention: model faces -Y (Blender front view),
# Z up, origin at the piece's attach point. Unity honours the FBX axis header, so a plain export lands as
# Unity (-x, z, -y) (front facing +Z) whatever axis_forward says. export() rotates 180° about Z first,
# giving Unity (x, z, y): front faces -Z (towards the camera) with identity rotations.
# Material names: "<Slot>" or "<Slot>.<KIT>"; Unity remaps by the part before the first '.'.
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

PROJECT = "/Volumes/Arturonchis/superserious/Contraption"
EXPORT = os.path.join(PROJECT, "Assets/_Project/Art/Models/Kit")
SCENE = "ContraptionKit"

PALETTES = {
    "":   {"Body": (0.16, 0.15, 0.15), "Trim": (0.78, 0.52, 0.18), "Panel": (0.90, 0.84, 0.70), "Metal": (0.72, 0.55, 0.28),
           "Accent": (0.35, 0.20, 0.12), "Glow": (0.25, 1.0, 0.85), "Dark": (0.08, 0.08, 0.09), "Glass": (0.7, 0.9, 0.95), "Liquid": (0.3, 0.9, 0.8),
           "Wood": (0.42, 0.28, 0.2), "Stone": (0.33, 0.29, 0.34), "Sky": (0.75, 0.85, 1.0)},
    "CO": {"Body": (0.16, 0.15, 0.15), "Trim": (0.78, 0.52, 0.18), "Panel": (0.90, 0.84, 0.70), "Metal": (0.60, 0.60, 0.62),
           "Accent": (0.35, 0.20, 0.12), "Glow": (0.25, 1.0, 0.85), "Dark": (0.08, 0.08, 0.09)},
    "TH": {"Body": (0.20, 0.38, 0.35), "Trim": (0.66, 0.27, 0.18), "Panel": (0.90, 0.84, 0.70), "Metal": (0.76, 0.56, 0.26),
           "Accent": (0.66, 0.27, 0.18), "Glow": (0.25, 1.0, 0.85), "Dark": (0.10, 0.12, 0.12)},
}

def scene():
    sc = bpy.data.scenes.get(SCENE) or bpy.data.scenes.new(SCENE)
    bpy.context.window.scene = sc
    return sc

def mat(slot, kit=""):
    name = slot if not kit else f"{slot}.{kit}"
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    col = PALETTES[kit].get(slot, PALETTES[""][slot])
    bsdf.inputs["Base Color"].default_value = (*col, 1)
    bsdf.inputs["Roughness"].default_value = 0.55
    if slot in ("Metal",): bsdf.inputs["Metallic"].default_value = 0.6
    if slot in ("Glow", "Liquid"):
        bsdf.inputs["Emission Color"].default_value = (*col, 1)
        bsdf.inputs["Emission Strength"].default_value = 2.0
    if slot == "Glass":
        bsdf.inputs["Alpha"].default_value = 0.25
    return m

class MB:
    """Accumulates primitives into one bmesh with per-part materials."""
    def __init__(self, kit=""):
        self.bm = bmesh.new(); self.mats = []; self.kit = kit
    def _mi(self, slot, kit=None):
        m = mat(slot, self.kit if kit is None else kit)
        if m not in self.mats: self.mats.append(m)
        return self.mats.index(m)
    def _finish(self, verts, slot, bevel, kit=None):
        faces = {f for v in verts for f in v.link_faces}
        mi = self._mi(slot, kit)
        for f in faces: f.material_index = mi
        if bevel > 0:
            edges = list({e for v in verts for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=1, affect='EDGES', clamp_overlap=True, profile=0.5)
        return self
    def box(self, size, center, slot, bevel=0.02, kit=None, rot=None):
        M = Matrix.Translation(center) @ (rot.to_4x4() if rot else Matrix()) @ Matrix.Diagonal((*size, 1))
        r = bmesh.ops.create_cube(self.bm, size=1.0, matrix=M)
        return self._finish(r["verts"], slot, bevel, kit)
    def cyl(self, r1, depth, center, slot, segs=8, r2=None, axis="Y", bevel=0.0, kit=None, rot_z=0.0):
        R = {"Y": Matrix.Rotation(math.radians(90), 4, 'X'), "X": Matrix.Rotation(math.radians(90), 4, 'Y'), "Z": Matrix()}[axis]
        M = Matrix.Translation(center) @ R @ Matrix.Rotation(rot_z, 4, 'Z')
        r = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=segs, radius1=r1, radius2=r1 if r2 is None else r2, depth=depth, matrix=M)
        return self._finish(r["verts"], slot, bevel, kit)
    def sphere(self, r, center, slot, kit=None, subdiv=1):
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=r, matrix=Matrix.Translation(center))
        return self._finish(res["verts"], slot, 0, kit)
    def build(self, name, parent=None, location=(0, 0, 0)):
        me = bpy.data.meshes.new(name)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(me); self.bm.free()
        for m in self.mats: me.materials.append(m)
        for p in me.polygons: p.use_smooth = False
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        ob.location = location
        if parent: ob.parent = parent
        return ob

def empty(name, parent, loc):
    e = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(e)
    e.parent = parent; e.location = loc; e.empty_display_size = 0.05
    return e

def rivets(mb, xs, z, y, slot="Metal", r=0.025):
    for x in xs: mb.cyl(r, 0.03, (x, y, z), slot, segs=6)


# ---------------------------------------------------------------- grid conventions
# Parts sit on a CELL grid; a WxH part has its origin at the footprint centre, plate front at y=-0.03.
# Chassis body front is y=0 and extends D deep (+Y).
CELL, D = 1.1, 0.9
PI = math.pi

def sz(w, h): return "" if (w, h) == (1, 1) else f"_{w}x{h}"

def plate(mb, w, h, slot="Panel", rivet=True):
    # Chamfered panel with a raised inner field and corner rivets (concept art t1–t4).
    W, H = w * CELL - 0.14, h * CELL - 0.14
    mb.box((W, 0.03, H), (0, -0.015, 0), slot, 0.035)
    mb.box((W - 0.1, 0.012, H - 0.1), (0, -0.034, 0), slot, 0.012)
    if rivet:
        for x in (-W / 2 + 0.05, W / 2 - 0.05):
            for z in (-H / 2 + 0.05, H / 2 - 0.05): mb.cyl(0.018, 0.02, (x, -0.035, z), "Metal", segs=6)

def frame(root, w=1, h=1):
    W, H = w * CELL - 0.1, h * CELL - 0.1
    f = MB()
    f.box((W, 0.02, 0.06), (0, -0.01, H / 2), "Trim"); f.box((W, 0.02, 0.06), (0, -0.01, -H / 2), "Trim")
    f.box((0.06, 0.02, H), (W / 2, -0.01, 0), "Trim"); f.box((0.06, 0.02, H), (-W / 2, -0.01, 0), "Trim")
    return f.build(root.name + "_Frame", root)

# ---------------------------------------------------------------- controls
def ctl_button(w=1, h=1):
    mb = MB(); plate(mb, w, h); m = MB()
    if (w, h) == (1, 1):
        mb.cyl(0.27, 0.08, (0, -0.07, 0.05), "Metal", segs=8, bevel=0.015, rot_z=PI / 8)
        m.cyl(0.19, 0.12, (0, 0, 0), "Glow", segs=8, r2=0.16, bevel=0.015, rot_z=PI / 8); at = (0, -0.16, 0.05)
    elif h == 1:   # wide plunger bar
        mb.box((w * CELL - 0.45, 0.08, 0.5), (0, -0.07, 0.08), "Metal", 0.03)
        m.box((w * CELL - 0.65, 0.12, 0.32), (0, 0, 0), "Glow", 0.05); at = (0, -0.17, 0.08)
    else:          # big mushroom
        mb.cyl(0.72, 0.1, (0, -0.08, 0.1), "Metal", segs=12, bevel=0.02)
        mb.cyl(0.6, 0.06, (0, -0.14, 0.1), "Dark", segs=12)
        m.cyl(0.52, 0.2, (0, 0, 0), "Glow", segs=12, r2=0.4, bevel=0.03)
        m.cyl(0.25, 0.1, (0, 0.12, 0), "Metal", segs=10); at = (0, -0.26, 0.1)
    root = mb.build("Ctl_Button" + sz(w, h)); frame(root, w, h)
    m.build("Moving", root, at)
    return root

def ctl_lever(h=1):
    mb = MB(); plate(mb, 1, h); m = MB()
    if h == 1:
        mb.box((0.18, 0.04, 0.7), (0, -0.05, 0.05), "Dark", 0.015)
        mb.box((0.26, 0.05, 0.12), (0, -0.06, 0.05), "Metal", 0.015)
        m.box((0.09, 0.09, 0.42), (0, 0, 0.21), "Metal", 0.015)
        m.cyl(0.09, 0.24, (0, 0, 0.46), "Accent", segs=6, axis="Z", bevel=0.015)
        m.cyl(0.07, 0.16, (0, 0, 0), "Metal", segs=8, axis="X"); at = (0, -0.1, 0.05)
    else:          # big knife switch hinged near the bottom; Unity swings it out of the panel
        L = h * CELL; pivot = -L / 2 + 0.4; arm = L - 0.85
        mb.box((0.2, 0.04, L - 0.45), (0, -0.05, 0.08), "Dark", 0.015)
        mb.box((0.4, 0.1, 0.26), (0, -0.07, pivot), "Metal", 0.03)
        m.box((0.11, 0.11, arm), (0, 0, arm / 2), "Metal", 0.02)
        m.cyl(0.1, 0.36, (0, 0, arm + 0.05), "Accent", segs=6, axis="X", bevel=0.02)
        m.cyl(0.09, 0.3, (0, 0, 0), "Metal", segs=8, axis="X"); at = (0, -0.14, pivot)
    root = mb.build("Ctl_Lever" + sz(1, h)); frame(root, 1, h)
    m.build("Moving", root, at)
    return root

def ctl_dial(s=1):
    k = s; mb = MB(); plate(mb, s, s)
    mb.cyl(0.35 * k, 0.04, (0, -0.05, 0.05 * k), "Dark", segs=12)
    root = mb.build("Ctl_Dial" + sz(s, s)); frame(root, s, s)
    m = MB()
    m.cyl(0.3 * k, 0.1, (0, 0, 0), "Metal", segs=8 if s == 1 else 12, r2=0.24 * k, bevel=0.015, rot_z=PI / 8)
    m.box((0.05 * k, 0.05, 0.165 * k), (0, -0.07, 0.135 * k), "Dark", 0.005)
    m.cyl(0.04 * k, 0.1, (0, -0.08, 0.255 * k), "Glow", segs=6)
    if s > 1:
        for i in range(6):   # grip studs on the big wheel
            a = i * PI / 3
            m.cyl(0.07, 0.08, (math.sin(a) * 0.5, -0.06, math.cos(a) * 0.5), "Accent", segs=6)
    m.build("Moving", root, (0, -0.12, 0.05 * k))
    return root

def ctl_knob(s=1):
    k = s; mb = MB(); plate(mb, s, s)
    mb.cyl(0.33 * k, 0.04, (0, -0.05, 0.05 * k), "Dark", segs=12)
    root = mb.build("Ctl_Knob" + sz(s, s)); frame(root, s, s)
    m = MB()
    m.cyl(0.25 * k, 0.14, (0, 0, 0), "Metal", segs=6, r2=0.2 * k, bevel=0.015)
    m.cyl(0.14 * k, 0.2 * k, (0, -0.07 - 0.1 * k, 0), "Metal", segs=6, r2=0.0)
    m.box((0.05 * k, 0.03, 0.14 * k), (0, -0.09, 0.26 * k), "Dark", 0.0)
    m.build("Moving", root, (0, -0.1, 0.05 * k))
    return root

def ctl_slider(w, h):
    # Handle "Moving" travels between the Min and Max empties; Unity spreads the positions along it.
    horiz = w > h; n = max(w, h); L = n * CELL - 0.55; c = 0.08 if horiz else 0.05
    mb = MB(); plate(mb, w, h)
    if horiz:
        mb.box((L + 0.16, 0.04, 0.14), (0, -0.05, c), "Dark", 0.015)
        for dz in (-0.13, 0.13): mb.box((L + 0.2, 0.05, 0.04), (0, -0.06, c + dz), "Metal", 0.01)
    else:
        mb.box((0.14, 0.04, L + 0.16), (c - 0.05, -0.05, 0.08), "Dark", 0.015)
        for dx in (-0.13, 0.13): mb.box((0.04, 0.05, L + 0.2), (dx, -0.06, 0.08), "Metal", 0.01)
    root = mb.build(f"Ctl_Slider_{w}x{h}"); frame(root, w, h)
    m = MB()
    if horiz:
        m.box((0.2, 0.14, 0.34), (0, 0, 0), "Accent", 0.03); m.box((0.04, 0.02, 0.26), (0, -0.08, 0), "Glow", 0)
        a, b = (-L / 2, -0.13, c), (L / 2, -0.13, c)
    else:
        m.box((0.34, 0.14, 0.2), (0, 0, 0), "Accent", 0.03); m.box((0.26, 0.02, 0.04), (0, -0.08, 0), "Glow", 0)
        a, b = (0, -0.13, 0.08 - L / 2), (0, -0.13, 0.08 + L / 2)
    m.build("Moving", root, a)
    empty("Min", root, a); empty("Max", root, b)
    return root

def ctl_joystick(s=1):
    k = s; mb = MB(); plate(mb, s, s)
    mb.cyl(0.3 * k, 0.05, (0, -0.05, 0.05 * k), "Metal", segs=8, bevel=0.015, rot_z=PI / 8)
    mb.cyl(0.2 * k, 0.1, (0, -0.1, 0.05 * k), "Dark", segs=8, r2=0.12 * k, rot_z=PI / 8)
    root = mb.build("Ctl_Joystick" + sz(s, s)); frame(root, s, s)
    m = MB()
    m.cyl(0.04 * k, 0.3 * k, (0, -0.15 * k, 0), "Metal", segs=8)
    m.sphere(0.09 * k, (0, -0.32 * k, 0), "Accent")
    m.build("Moving", root, (0, -0.12, 0.05 * k))
    return root

# ---------------------------------------------------------------- displays
def gem(mb, b, k):
    # Octagonal faceted gem in a metal bezel: Bulb gets a flat table and a sloped crown.
    rot = Matrix.Rotation(PI / 8, 3, 'Y')
    mb.cyl(0.27 * k, 0.07, (0, -0.06, 0), "Metal", segs=8, bevel=0.015, rot_z=PI / 8)
    mb.cyl(0.21 * k, 0.03, (0, -0.1, 0), "Dark", segs=8, rot_z=PI / 8)
    b.cyl(0.19 * k, 0.05, (0, 0, 0), "Glow", segs=8, rot_z=PI / 8)
    b.cyl(0.19 * k, 0.06 * k, (0, -0.025 - 0.03 * k, 0), "Glow", segs=8, r2=0.11 * k, rot_z=PI / 8)

def disp_light(w=1, h=1):
    mb = MB(); plate(mb, w, h); b = MB()
    if (w, h) == (1, 1):
        gem(mb, b, 1.0); at = (0, -0.1, 0)
    elif w == h:   # big caged beacon
        gem(mb, b, 2.4); at = (0, -0.1, 0)
    else:          # neon tube
        horiz = w > h; L = max(w, h) * CELL - 0.45; ax = "X" if horiz else "Z"
        for sgn in (-1, 1):
            p = (sgn * L / 2, -0.12, 0) if horiz else (0, -0.12, sgn * L / 2)
            mb.cyl(0.15, 0.14, p, "Metal", segs=8, axis=ax, bevel=0.02)
            mb.box((0.1, 0.1, 0.1), (p[0], -0.06, p[2]), "Metal", 0.02)
        b.cyl(0.11, L - 0.1, (0, 0, 0), "Glow", segs=8, axis=ax); at = (0, -0.12, 0)
    root = mb.build("Disp_Light" + sz(w, h))
    b.build("Bulb", root, at)
    return root

def disp_bar(w=1, h=1):
    horiz = w > h; n = max(w, h)
    S = 0.95 if n == 1 else n * CELL - 0.3; across = 0.38 if n == 1 else 0.5
    mb = MB(); plate(mb, w, h)
    hs = (S, 0.05, across) if horiz else (across, 0.05, S)
    mb.box(hs, (0, -0.055, 0), "Metal", 0.02)
    mb.box((hs[0] - 0.08, 0.03, hs[2] - 0.08), (0, -0.09, 0), "Dark", 0)
    root = mb.build("Disp_Bar" + sz(w, h))
    step = (S - 0.15) / 5; seg = step - 0.04; t = across - 0.16
    for i in range(5):
        p = -(S - 0.15) / 2 + step * (i + 0.5)
        s = MB(); s.box((seg, 0.03, t) if horiz else (t, 0.03, seg), (0, 0, 0), "Glow", 0.01)
        s.build(f"Seg{i}", root, (p, -0.11, 0) if horiz else (0, -0.11, p))
    return root

def disp_gauge(w=1, h=1):
    mb = MB(); plate(mb, w, h)
    if w == h:
        k = w
        mb.cyl(0.42 * k, 0.06, (0, -0.06, 0), "Metal", segs=12 if k == 1 else 16, bevel=0.015)
        mb.cyl(0.36 * k, 0.03, (0, -0.1, 0), "Panel", segs=12 if k == 1 else 16)
        for i in range(7):
            a = math.radians(-70 + i * 140 / 6)
            mb.box((0.02 * k, 0.01, 0.06 * k), (math.sin(a) * 0.29 * k, -0.12, math.cos(a) * 0.29 * k - 0.08 * k), "Dark", 0, rot=Matrix.Rotation(-a, 3, 'Y'))
        root = mb.build("Disp_Gauge" + sz(w, h))
        n = MB(); n.box((0.03 * k, 0.02, 0.36 * k), (0, 0, 0.18 * k), "Accent", 0); n.cyl(0.04 * k, 0.03, (0, 0, 0), "Dark", segs=6)
        n.build("Needle", root, (0, -0.13, -0.1 * k))
    else:          # linear meter: needle slides between Min and Max
        L = w * CELL - 0.6
        mb.box((w * CELL - 0.3, 0.06, 0.7), (0, -0.06, 0), "Metal", 0.03)
        mb.box((w * CELL - 0.42, 0.03, 0.56), (0, -0.1, 0), "Panel", 0)
        for i in range(7):
            mb.box((0.02, 0.01, 0.14 if i % 3 == 0 else 0.08), (-L / 2 + i * L / 6, -0.12, -0.16), "Dark", 0)
        root = mb.build("Disp_Gauge" + sz(w, h))
        n = MB(); n.box((0.04, 0.02, 0.42), (0, 0, 0.02), "Accent", 0); n.cyl(0.05, 0.03, (0, 0, -0.2), "Dark", segs=3, rot_z=PI)
        n.build("Needle", root, (-L / 2, -0.13, 0))
        empty("Min", root, (-L / 2, -0.13, 0)); empty("Max", root, (L / 2, -0.13, 0))
    return root

def disp_glyph(w=1, h=1):
    k = 2 if (w, h) == (2, 2) else 1
    mb = MB(); plate(mb, w, h)
    if (w, h) == (2, 1):
        mb.box((2 * CELL - 0.3, 0.06, 0.8), (0, -0.06, 0), "Metal", 0.03)
        for sgn in (-1, 1):
            for i in range(4): mb.box((0.3, 0.02, 0.05), (sgn * 0.75, -0.1, -0.22 + i * 0.15), "Dark", 0)
    else:
        mb.box((0.8 * k, 0.06, 0.8 * k), (0, -0.06, 0), "Metal", 0.03)
    mb.box((0.66 * k, 0.03, 0.66 * k), (0, -0.1, 0), "Dark", 0)
    root = mb.build("Disp_Glyph" + sz(w, h))
    for i in range(9):
        c = MB(); c.box((0.13 * k, 0.02, 0.13 * k), (0, 0, 0), "Glow", 0.01)
        c.build(f"Cell{i}", root, ((i % 3 - 1) * 0.18 * k, -0.12, (1 - i // 3) * 0.18 * k))
    return root

# ---------------------------------------------------------------- test triggers (outside the grid)
def test_plunger():
    # Stands on the chassis top; "Moving" is pushed down when the machine runs a test.
    mb = MB()
    mb.cyl(0.38, 0.12, (0, 0.45, 0.06), "Metal", segs=12, axis="Z", bevel=0.02)
    mb.cyl(0.24, 0.4, (0, 0.45, 0.32), "Body", segs=10, axis="Z", r2=0.2, bevel=0.02)
    mb.cyl(0.33, 0.06, (0, 0.45, 0.53), "Glow", segs=12, axis="Z")
    root = mb.build("Test_Plunger")
    m = MB()
    m.cyl(0.12, 0.2, (0, 0, 0.1), "Metal", segs=8, axis="Z")
    m.cyl(0.36, 0.18, (0, 0, 0.26), "Trim", segs=14, axis="Z", r2=0.3, bevel=0.03)
    m.cyl(0.16, 0.06, (0, 0, 0.37), "Accent", segs=10, axis="Z")
    m.build("Moving", root, (0, 0.45, 0.56))
    return root

def test_lever():
    # Bolted to the chassis' left side (origin on the wall, extends -X); Unity swings "Moving" down-left.
    mb = MB()
    mb.box((0.1, 0.5, 0.7), (-0.05, 0.25, 0), "Metal", 0.02)
    mb.box((0.26, 0.24, 0.3), (-0.2, 0.0, 0), "Trim", 0.03)
    for z in (-0.25, 0.25): mb.cyl(0.04, 0.03, (-0.05, -0.02, z), "Dark", segs=6)
    root = mb.build("Test_Lever")
    m = MB()
    m.cyl(0.1, 0.3, (0, 0, 0), "Metal", segs=8, axis="Y")
    m.box((0.1, 0.1, 1.0), (0, 0, 0.5), "Metal", 0.02)
    m.sphere(0.17, (0, 0, 1.08), "Trim", subdiv=2)
    m.cyl(0.12, 0.04, (0, 0, 0.85), "Glow", segs=10, axis="Z")
    m.build("Moving", root, (-0.36, -0.12, 0))
    return root

def disp_rune(s=1):
    # CRT showing one line-drawn symbol; Unity draws the strokes centred on the "Screen" empty.
    k = s; mb = MB(); plate(mb, s, s)
    mb.box((0.84 * k, 0.1, 0.84 * k), (0, -0.07, 0), "Trim", 0.08 if s == 1 else 0.12)
    mb.box((0.7 * k, 0.04, 0.7 * k), (0, -0.13, 0), "Dark", 0.04)
    for x in (-1, 1): mb.cyl(0.035 * k, 0.04, (x * 0.3 * k, -0.13, -0.38 * k), "Glow", segs=6)
    root = mb.build("Disp_Rune" + sz(s, s))
    empty("Screen", root, (0, -0.16, 0))
    return root

def disp_script(w):
    # Wide screen with a line of alien writing (one word per value).
    mb = MB(); plate(mb, w, 1)
    W = w * CELL - 0.3
    mb.box((W, 0.1, 0.78), (0, -0.07, 0), "Metal", 0.04)
    mb.box((W - 0.14, 0.04, 0.6), (0, -0.13, 0), "Dark", 0.02)
    for i in range(3): mb.box((0.06, 0.03, 0.03), (-W / 2 + 0.2 + i * 0.1, -0.13, 0.35), "Trim", 0)
    root = mb.build(f"Disp_Script_{w}x1")
    empty("Screen", root, (0, -0.16, 0))
    return root

def lamp():
    # "Machine fixed" indicator on the chassis top: a small box with an octagonal lens.
    mb = MB()
    mb.box((0.62, 0.55, 0.6), (0, 0.4, 0.3), "Panel", 0.05)
    mb.cyl(0.21, 0.06, (0, 0.1, 0.32), "Metal", segs=8, bevel=0.015, rot_z=PI / 8)
    root = mb.build("Lamp")
    b = MB(); b.cyl(0.15, 0.06, (0, 0, 0), "Dark", segs=8, rot_z=PI / 8); b.build("Bulb", root, (0, 0.06, 0.32))
    return root

# ---------------------------------------------------------------- fillers (decorative panels)
def corner_rivets(mb, w=1, h=1, slot="Metal"):
    hx, hz = w * CELL / 2 - 0.15, h * CELL / 2 - 0.15
    for x in (-hx, hx):
        for z in (-hz, hz): mb.cyl(0.03, 0.03, (x, -0.04, z), slot, segs=6)

def fill_vent():
    mb = MB(); plate(mb, 1, 1)
    for i in range(5): mb.box((0.66, 0.02, 0.06), (0, -0.035, -0.26 + i * 0.13), "Dark", 0)
    return mb.build("Fill_Vent")

def fill_grille():
    mb = MB(); plate(mb, 1, 1, "Metal")
    mb.box((0.7, 0.02, 0.7), (0, -0.035, 0), "Dark", 0)
    for i in (-1, 0, 1):
        mb.box((0.03, 0.04, 0.7), (i * 0.2, -0.05, 0), "Metal", 0)
        mb.box((0.7, 0.04, 0.03), (0, -0.05, i * 0.2), "Metal", 0)
    return mb.build("Fill_Grille")

def fill_plate():
    mb = MB(); plate(mb, 1, 1)
    mb.box((0.62, 0.02, 0.07), (0, -0.045, 0.24), "Dark", 0.01)
    mb.box((0.42, 0.05, 0.4), (0, -0.05, -0.08), "Panel", 0.03)
    return mb.build("Fill_Plate")

def fill_pipes():
    # Pipes run the full cell height so stacked pipe panels join up.
    mb = MB(); mb.box((CELL - 0.14, 0.03, CELL - 0.14), (0, -0.015, 0), "Dark", 0.02)
    for x in (-0.2, 0.2):
        mb.cyl(0.08, CELL, (x, -0.1, 0), "Metal", segs=8, axis="Z")
        mb.cyl(0.11, 0.06, (x, -0.1, 0.15 if x < 0 else -0.2), "Trim", segs=8, axis="Z", bevel=0.01)
    return mb.build("Fill_Pipes")

def fill_porthole():
    mb = MB(); plate(mb, 1, 1)
    mb.cyl(0.36, 0.06, (0, -0.05, 0), "Metal", segs=12, bevel=0.015)
    mb.cyl(0.29, 0.02, (0, -0.07, 0), "Liquid", segs=12)
    for i in range(6):   # wavy line seen through the glass
        x = -0.22 + i * 0.09; z = 0.04 if i % 2 else -0.04
        mb.box((0.11, 0.01, 0.025), (x + 0.045, -0.085, 0.0), "Dark", 0, rot=Matrix.Rotation(-0.7 if i % 2 else 0.7, 3, 'Y'))
    mb.cyl(0.29, 0.02, (0, -0.1, 0), "Glass", segs=12)
    for i in range(6):
        a = i * PI / 3; mb.cyl(0.025, 0.03, (math.sin(a) * 0.33, -0.09, math.cos(a) * 0.33), "Trim", segs=6)
    return mb.build("Fill_Porthole")

def fill_fan():
    mb = MB(); plate(mb, 2, 2, "Metal")
    mb.cyl(0.85, 0.06, (0, -0.05, 0), "Body", segs=16, bevel=0.02)
    mb.cyl(0.76, 0.03, (0, -0.07, 0), "Dark", segs=16)
    corner_rivets(mb, 2, 2, "Trim")
    root = mb.build("Fill_Fan_2x2")
    f = MB(); f.cyl(0.16, 0.12, (0, 0, 0), "Trim", segs=8)
    for i in range(6):
        a = i * PI / 3
        f.box((0.18, 0.03, 0.62), (math.sin(a) * 0.4, 0.02, math.cos(a) * 0.4), "Metal", 0.01,
              rot=Matrix.Rotation(-a, 3, 'Y') @ Matrix.Rotation(0.4, 3, 'Z'))
    f.build("Spin", root, (0, -0.1, 0))
    for a in (0, PI / 2):   # guard bars
        mb2 = MB(); mb2.box((1.6, 0.03, 0.04), (0, 0, 0), "Metal", 0, rot=Matrix.Rotation(a, 3, 'Y')); mb2.build("Guard", root, (0, -0.2, 0))
    return root

def fill_hatch():
    mb = MB(); plate(mb, 2, 2, "Panel")
    mb.box((1.7, 0.06, 1.7), (0, -0.06, 0), "Body", 0.06)
    mb.box((1.5, 0.03, 1.5), (0, -0.1, 0), "Trim", 0.03)
    for z in (-0.5, 0.5): mb.box((0.14, 0.1, 0.26), (-0.82, -0.1, z), "Metal", 0.02)
    mb.cyl(0.32, 0.06, (0.2, -0.14, 0), "Metal", segs=12, bevel=0.01)
    mb.cyl(0.25, 0.08, (0.2, -0.14, 0), "Dark", segs=12)
    for a in (0, PI / 3, 2 * PI / 3):
        mb.box((0.5, 0.05, 0.05), (0.2, -0.18, 0), "Metal", 0, rot=Matrix.Rotation(a, 3, 'Y'))
    corner_rivets(mb, 2, 2)
    return mb.build("Fill_Hatch_2x2")

# ---------------------------------------------------------------- containers
# Inputs stand on the chassis top (origin = stand base); outputs stand on the ground. "Liquid" is the inner
# volume; Unity clips it at the fill height. Outlet/Inlet empties mark pipe connections.
def stand(mb, h=0.25, r=0.3):
    mb.cyl(r, 0.06, (0, 0, 0.03), "Metal", segs=8, axis="Z", bevel=0.01)
    for i in range(3):
        a = i * 2 * PI / 3 + PI / 2
        mb.box((0.05, 0.05, h), (math.cos(a) * r * 0.8, math.sin(a) * r * 0.8, h / 2), "Metal", 0.01)
    mb.cyl(0.06, h, (0, 0, h / 2), "Metal", segs=8, axis="Z")

def liquid(parent, build):
    l = MB(); build(l); return l.build("Liquid", parent)

def in_tank():
    mb = MB(); stand(mb)
    for z in (0.28, 1.15): mb.cyl(0.3, 0.07, (0, 0, z), "Metal", segs=10, axis="Z", bevel=0.015)
    for i in range(4):
        a = i * PI / 2 + PI / 4; mb.box((0.04, 0.04, 0.87), (math.cos(a) * 0.28, math.sin(a) * 0.28, 0.715), "Metal", 0.005)
    mb.cyl(0.25, 0.86, (0, 0, 0.715), "Glass", segs=12, axis="Z")
    root = mb.build("In_Tank")
    liquid(root, lambda l: l.cyl(0.23, 0.82, (0, 0, 0.715), "Liquid", segs=12, axis="Z"))
    empty("Outlet", root, (0, 0, 0)); return root

def in_flask():
    mb = MB(); stand(mb)
    mb.sphere(0.34, (0, 0, 0.6), "Glass", subdiv=2)
    mb.cyl(0.1, 0.36, (0, 0, 1.05), "Glass", segs=10, axis="Z")
    mb.cyl(0.13, 0.06, (0, 0, 1.24), "Metal", segs=10, axis="Z", bevel=0.01)
    mb.cyl(0.36, 0.05, (0, 0, 0.6), "Metal", segs=12, axis="Z")
    root = mb.build("In_Flask")
    liquid(root, lambda l: l.sphere(0.31, (0, 0, 0.6), "Liquid", subdiv=2))
    empty("Outlet", root, (0, 0, 0)); return root

def in_hopper():
    mb = MB()
    for i in range(4):
        a = i * PI / 2 + PI / 4; mb.box((0.05, 0.05, 1.0), (math.cos(a) * 0.38, math.sin(a) * 0.38, 0.5), "Metal", 0.01)
    mb.cyl(0.08, 0.3, (0, 0, 0.15), "Metal", segs=8, axis="Z")
    mb.cyl(0.11, 0.78, (0, 0, 0.66), "Glass", segs=12, r2=0.42, axis="Z")
    mb.cyl(0.45, 0.06, (0, 0, 1.06), "Metal", segs=12, axis="Z", bevel=0.01)
    mb.cyl(0.14, 0.06, (0, 0, 0.29), "Metal", segs=8, axis="Z", bevel=0.01)
    root = mb.build("In_Hopper")
    liquid(root, lambda l: l.cyl(0.09, 0.72, (0, 0, 0.66), "Liquid", segs=12, r2=0.39, axis="Z"))
    empty("Outlet", root, (0, 0, 0)); return root

def in_sphere():
    mb = MB(); stand(mb, 0.2, 0.34)
    mb.sphere(0.4, (0, 0, 0.62), "Glass", subdiv=2)
    mb.cyl(0.42, 0.07, (0, 0, 0.62), "Trim", segs=14, axis="Z", bevel=0.01)
    mb.cyl(0.08, 0.14, (0, 0, 1.06), "Metal", segs=8, axis="Z")
    root = mb.build("In_Sphere")
    liquid(root, lambda l: l.sphere(0.37, (0, 0, 0.62), "Liquid", subdiv=2))
    empty("Outlet", root, (0, 0, 0)); return root

def out_tank():
    mb = MB(); mb.box((0.9, 0.9, 0.16), (0, 0, 0.08), "Metal", 0.03)
    for z in (0.2, 1.5): mb.cyl(0.42, 0.08, (0, 0, z), "Metal", segs=12, axis="Z", bevel=0.015)
    mb.cyl(0.38, 1.26, (0, 0, 0.85), "Glass", segs=14, axis="Z")
    mb.cyl(0.1, 0.16, (0, 0, 1.6), "Metal", segs=8, axis="Z")
    for i in range(4):
        a = i * PI / 2 + PI / 4; mb.box((0.05, 0.05, 1.3), (math.cos(a) * 0.4, math.sin(a) * 0.4, 0.85), "Trim", 0.01)
    root = mb.build("Out_Tank")
    liquid(root, lambda l: l.cyl(0.35, 1.2, (0, 0, 0.84), "Liquid", segs=14, axis="Z"))
    empty("Inlet", root, (0, 0, 1.68)); return root

def out_vat():
    mb = MB(); mb.box((1.2, 0.9, 0.14), (0, 0, 0.07), "Metal", 0.03)
    mb.box((1.04, 0.74, 0.76), (0, 0, 0.52), "Glass", 0.02)
    for x in (-0.54, 0.54):
        for y in (-0.39, 0.39): mb.box((0.06, 0.06, 0.82), (x, y, 0.53), "Trim", 0.01)
    mb.box((1.14, 0.84, 0.06), (0, 0, 0.93), "Metal", 0.01)
    mb.box((1.0, 0.7, 0.06), (0, 0, 0.93), "Dark", 0)
    root = mb.build("Out_Vat")
    liquid(root, lambda l: l.box((0.98, 0.68, 0.7), (0, 0, 0.5), "Liquid", 0))
    empty("Inlet", root, (0, 0, 1.0)); return root

def out_flask():
    mb = MB(); mb.cyl(0.55, 0.1, (0, 0, 0.05), "Metal", segs=12, axis="Z", bevel=0.02)
    mb.cyl(0.5, 1.0, (0, 0, 0.6), "Glass", segs=14, r2=0.15, axis="Z")
    mb.cyl(0.15, 0.36, (0, 0, 1.28), "Glass", segs=10, axis="Z")
    mb.cyl(0.18, 0.07, (0, 0, 1.46), "Metal", segs=10, axis="Z", bevel=0.01)
    root = mb.build("Out_Flask")
    liquid(root, lambda l: l.cyl(0.46, 0.92, (0, 0, 0.58), "Liquid", segs=14, r2=0.17, axis="Z"))
    empty("Inlet", root, (0, 0, 1.5)); return root

def vial():
    mb = MB()
    mb.cyl(0.13, 0.36, (0, 0, 0), "Glass", segs=10, axis="Z")
    for z in (-0.2, 0.2): mb.cyl(0.155, 0.06, (0, 0, z), "Metal", segs=10, axis="Z", bevel=0.01)
    root = mb.build("Vial")
    liquid(root, lambda l: l.cyl(0.11, 0.34, (0, 0, 0), "Liquid", segs=10, axis="Z"))
    empty("Inlet", root, (0, 0, 0.23)); empty("Outlet", root, (0, 0, -0.23)); return root

def pipe():
    mb = MB(); mb.cyl(0.06, 1.0, (0.5, 0, 0), "Metal", segs=8, axis="X")
    return mb.build("Pipe")

def flange():
    mb = MB(); mb.cyl(0.09, 0.06, (0, 0, 0), "Metal", segs=8, axis="X", bevel=0.01)
    return mb.build("Flange")

# ---------------------------------------------------------------- chassis tiles (dual grid)
# A tile sits on a grid corner and covers a quarter of each of the 4 cells around it.
# mask bits: 1 = bottom-left cell, 2 = bottom-right, 4 = top-left, 8 = top-right. Diagonal masks (6, 9)
# never occur (the layout generator rejects them). Silhouette edges always run through the tile centre.
Q = CELL / 2
QUAD = {1: (-1, -1), 2: (1, -1), 4: (-1, 1), 8: (1, 1)}

def tile(kit, mask):
    mb = MB(kit)
    filled = lambda sx, sz: any(mask & b and QUAD[b] == (sx, sz) for b in QUAD)
    for b, (sx, sz) in QUAD.items():
        if mask & b: mb.box((Q, D, Q), (sx * Q / 2, D / 2, sz * Q / 2), "Body", 0)
    for b, (sx, sz) in QUAD.items():
        if not mask & b: continue
        # Neighbour quadrant empty above/below → horizontal edge; left/right → vertical edge.
        if not filled(sx, -sz): edge(mb, kit, "top" if sz < 0 else "bottom", sx * Q / 2, 0)
        if not filled(-sx, sz): edge(mb, kit, "side", 0, sz * Q / 2)
    n = bin(mask).count("1")
    if n in (1, 3): corner(mb, kit)
    if n == 4: mb.box((0.09, 0.05, 0.09), (0, -0.02, 0), "Metal", 0.015, rot=Matrix.Rotation(PI / 4, 3, 'Y'))   # diamond stud
    return mb.build(f"{kit}_Tile_{mask}")

def edge(mb, kit, kind, cx, cz):
    if kit == "CO":
        if kind == "side":
            mb.box((0.08, D + 0.04, Q), (0, D / 2, cz), "Trim", 0)
            mb.cyl(0.02, 0.03, (0, -0.035, cz), "Metal", segs=6)
        else:
            t = 0.16 if kind == "top" else 0.2
            mb.box((Q, D + 0.06, t), (cx, D / 2, 0), "Trim", 0)
            mb.cyl(0.025, 0.03, (cx, -0.04, 0), "Metal", segs=6)
    else:
        if kind == "side": mb.cyl(0.08, Q, (0, 0.05, cz), "Trim", segs=8, axis="Z")
        else:
            r = 0.1 if kind == "top" else 0.12
            mb.cyl(r, Q, (cx, 0.05, 0), "Trim", segs=8, axis="X")
            mb.box((Q, D - 0.1, 2 * r - 0.04), (cx, D / 2 + 0.05, 0), "Trim", 0)

def corner(mb, kit):
    if kit == "CO":
        mb.box((0.24, D + 0.08, 0.24), (0, D / 2, 0), "Trim", 0.03)
        mb.cyl(0.04, 0.03, (0, -0.06, 0), "Metal", segs=6)
    else:
        mb.box((0.26, D + 0.04, 0.26), (0, D / 2, 0), "Trim", 0.08)
        mb.cyl(0.045, 0.04, (0, -0.04, 0), "Metal", segs=8)

TILE_MASKS = [m for m in range(1, 16) if m not in (6, 9)]

# ---------------------------------------------------------------- top & base attachments (kit specific)
# Top pieces: origin on the chassis top at a cell centre. Base pieces: origin at the chassis bottom.
def top_crest(kit):
    mb = MB(kit)
    if kit == "CO":
        mb.box((0.7, 0.5, 0.25), (0, 0.45, 0.12), "Trim", 0.05); mb.box((0.4, 0.02, 0.08), (0, 0.19, 0.12), "Glow", 0)
    else:
        mb.box((0.8, 0.5, 0.3), (0, 0.45, 0.15), "Trim", 0.1); mb.box((0.5, 0.02, 0.1), (0, 0.19, 0.15), "Glow", 0)
    return mb.build(f"{kit}_Top_Crest")

def top_chimney(kit):
    mb = MB(kit)
    mb.box((0.42, 0.42, 0.12), (0, 0.45, 0.06), "Trim", 0.03 if kit == "CO" else 0.06)
    mb.cyl(0.15, 0.8, (0, 0.45, 0.5), "Metal", segs=8 if kit == "CO" else 12, axis="Z")
    mb.cyl(0.19, 0.08, (0, 0.45, 0.55), "Trim", segs=8, axis="Z", bevel=0.01)
    if kit == "CO": mb.cyl(0.22, 0.12, (0, 0.45, 0.94), "Dark", segs=8, r2=0.12, axis="Z")
    else: mb.sphere(0.17, (0, 0.45, 0.95), "Glow")
    return mb.build(f"{kit}_Top_Chimney")

def top_dome(kit):
    mb = MB(kit)
    mb.cyl(0.4, 0.1, (0, 0.45, 0.05), "Trim", segs=12, axis="Z", bevel=0.02)
    if kit == "CO":
        mb.sphere(0.32, (0, 0.45, 0.1), "Metal", subdiv=2); mb.cyl(0.06, 0.3, (0, 0.45, 0.5), "Dark", segs=6, axis="Z")
    else:
        mb.sphere(0.34, (0, 0.45, 0.1), "Glass", subdiv=2); mb.sphere(0.16, (0, 0.45, 0.2), "Glow", subdiv=1)
    return mb.build(f"{kit}_Top_Dome")

def top_crystal(kit):
    mb = MB(kit)
    mb.cyl(0.26, 0.1, (0, 0.45, 0.05), "Metal", segs=8, axis="Z", bevel=0.02)
    mb.cyl(0.18, 0.12, (0, 0.45, 0.16), "Trim", segs=8, axis="Z", bevel=0.02)
    mb.cyl(0.13, 0.4, (0, 0.45, 0.42), "Panel", segs=6, axis="Z", r2=0.1)
    mb.cyl(0.1, 0.22, (0, 0.45, 0.73), "Panel", segs=6, axis="Z", r2=0.0)
    for a in (0, 2 * PI / 3, 4 * PI / 3):
        mb.cyl(0.05, 0.22, (math.cos(a) * 0.13, 0.45 + math.sin(a) * 0.13, 0.3), "Panel", segs=6, axis="Z", r2=0.0)
    return mb.build(f"{kit}_Top_Crystal")

def top_antenna(kit):
    mb = MB(kit)
    mb.box((0.3, 0.3, 0.14), (0, 0.45, 0.07), "Trim", 0.03)
    mb.cyl(0.03, 1.0, (0, 0.45, 0.6), "Metal", segs=6, axis="Z")
    for z in (0.3, 0.42, 0.54): mb.cyl(0.1 if kit == "CO" else 0.12, 0.03, (0, 0.45, z), "Accent" if kit == "CO" else "Metal", segs=8, axis="Z")
    mb.sphere(0.08, (0, 0.45, 1.12), "Glow")
    return mb.build(f"{kit}_Top_Antenna")

def base_foot(kit):
    mb = MB(kit)
    if kit == "CO":
        mb.box((0.36, 0.5, 0.2), (0, 0.45, -0.1), "Metal", 0.03)
        mb.cyl(0.18, 0.4, (0, 0.45, -0.4), "Metal", segs=6, r2=0.12, axis="Z", bevel=0.02)
    else:
        mb.cyl(0.2, 0.45, (0, 0.45, -0.25), "Panel", segs=6, r2=0.12, axis="Z", bevel=0.02)
        mb.cyl(0.21, 0.1, (0, 0.45, -0.5), "Metal", segs=6, axis="Z", bevel=0.02)
    return mb.build(f"{kit}_Base_Foot")

def base_plinth(kit):
    mb = MB(kit)
    if kit == "CO":
        mb.box((CELL, D + 0.1, 0.36), (0, D / 2, -0.18), "Accent", 0)
        mb.box((CELL, 0.03, 0.06), (0, -0.065, -0.1), "Trim", 0)
        rivets(mb, (-0.3, 0.3), -0.25, -0.06)
    else:
        mb.box((CELL, D, 0.36), (0, D / 2, -0.18), "Trim", 0)
        mb.cyl(0.1, CELL, (0, 0.0, -0.08), "Panel", segs=8, axis="X")
    return mb.build(f"{kit}_Base_Plinth")

def base_skid(kit):
    mb = MB(kit)
    for y in (0.12, D - 0.12): mb.box((CELL, 0.16, 0.12), (0, y, -0.4), "Metal", 0)
    mb.box((0.22, D - 0.2, 0.36), (0, D / 2, -0.2), "Trim" if kit == "CO" else "Panel", 0.03)
    return mb.build(f"{kit}_Base_Skid")

# ---------------------------------------------------------------- workshop: books and table (real-world metres)
PAGE_W, PAGE_H, PAGE_TOP = 0.38, 0.52, 0.048

def book(name, cover):
    # Open book lying flat; page tops face +Z, page top edge toward +Y (away from the reader).
    mb = MB()
    for sx in (-1, 1):
        mb.box((0.41, 0.56, 0.012), (sx * 0.215, 0, 0.006), cover, 0.004)
        mb.box((PAGE_W, PAGE_H, 0.036), (sx * (PAGE_W / 2 + 0.012), 0, 0.03), "Panel", 0.01)
        for sy in (-1, 1):   # brass corners
            cx, cy = sx * 0.405, sy * 0.265
            mb.box((0.07, 0.022, 0.016), (cx - sx * 0.025, cy, 0.01), "Trim", 0.004)
            mb.box((0.022, 0.07, 0.016), (cx, cy - sy * 0.025, 0.01), "Trim", 0.004)
    mb.box((0.03, 0.56, 0.02), (0, 0, 0.012), cover, 0.006)
    mb.box((0.03, 0.06, 0.012), (0.43, 0, 0.012), "Metal", 0.004)          # clasp
    mb.box((0.02, 0.12, 0.003), (0, -0.31, 0.003), "Dark", 0)                # bookmark ribbon
    root = mb.build(name)
    empty("PageL", root, (-(PAGE_W / 2 + 0.012), 0, PAGE_TOP))
    empty("PageR", root, (PAGE_W / 2 + 0.012, 0, PAGE_TOP))
    return root

def book_sheet():
    # A single page that turns around the spine (origin), lying over the right page.
    mb = MB(); mb.box((PAGE_W, PAGE_H, 0.004), (PAGE_W / 2 + 0.012, 0, 0.002), "Panel", 0.001)
    return mb.build("Book_Sheet")

def sample_tube():
    # Test tube with a cork and a paper tag (front, facing -Y). Origin at the tube bottom; "TagFace" is the tag's front centre.
    mb = MB()
    mb.cyl(0.035, 0.26, (0, 0, 0.16), "Glass", segs=10, axis="Z")
    mb.sphere(0.035, (0, 0, 0.03), "Glass", subdiv=2)
    mb.cyl(0.032, 0.05, (0, 0, 0.31), "Wood", segs=8, axis="Z", r2=0.028)
    mb.box((0.004, 0.004, 0.05), (0, -0.04, 0.27), "Dark", 0)                 # string
    mb.box((0.075, 0.006, 0.055), (0, -0.045, 0.22), "Panel", 0.004)          # tag
    mb.cyl(0.006, 0.008, (0, -0.049, 0.243), "Dark", segs=6)                  # tag hole
    root = mb.build("Sample_Tube")
    liquid(root, lambda l: (l.cyl(0.03, 0.2, (0, 0, 0.13), "Liquid", segs=10, axis="Z"), l.sphere(0.03, (0, 0, 0.035), "Liquid", subdiv=2)))
    empty("TagFace", root, (0, -0.049, 0.215))
    return root

def sample_rack():
    # Wooden rack for four tubes along X (0.15 apart); tubes stand at Slot0..3, 0.02 above the table.
    mb = MB()
    mb.box((0.66, 0.14, 0.03), (0, 0, 0.015), "Wood", 0.008)
    mb.box((0.66, 0.12, 0.025), (0, 0, 0.13), "Wood", 0.006)
    for x in (-0.32, 0.32): mb.box((0.03, 0.12, 0.15), (x, 0, 0.075), "Wood", 0.006)
    for i in range(4): mb.cyl(0.042, 0.03, (-0.225 + i * 0.15, 0, 0.13), "Dark", segs=10, axis="Z")
    root = mb.build("Sample_Rack")
    for i in range(4): empty(f"Slot{i}", root, (-0.225 + i * 0.15, 0, 0.03))
    return root

def table():
    # Heavy workbench, origin on the floor; top surface at z = 1.0, 5 x 2.4 m.
    mb = MB()
    for i in range(8):
        mb.box((5.0, 0.29, 0.1), (0, -1.05 + i * 0.3, 0.95), "Accent", 0.02)
    for sx in (-1, 1):
        for sy in (-1, 1):
            mb.box((0.16, 0.16, 0.9), (sx * 2.3, sy * 1.0, 0.45), "Accent", 0.02)
            mb.box((0.26, 0.26, 0.12), (sx * 2.42, sy * 1.12, 0.95), "Trim", 0.02)
            mb.cyl(0.03, 0.03, (sx * 2.42, sy * 1.12 - 0.13 * sy, 0.95), "Metal", segs=6)
    for sy in (-1, 1): mb.box((4.6, 0.08, 0.14), (0, sy * 1.0, 0.78), "Accent", 0.01)
    mb.box((1.2, 0.06, 0.22), (0, -1.08, 0.76), "Accent", 0.02)              # drawer front
    mb.box((0.36, 0.05, 0.05), (0, -1.12, 0.76), "Metal", 0.01)
    mb.box((5.04, 0.06, 0.08), (0, -1.21, 0.94), "Trim", 0.01)               # brass edge
    return mb.build("Table")

def workshop_room():
    # Greybox workshop around the table (table at the origin, camera side = -Y). Lamp* empties become warm lights in Unity.
    import random
    rnd = random.Random(7)
    mb = MB()
    W, L, H = 30.0, 22.0, 10.0
    for ix in range(int(W / 1.5)):                       # stone floor tiles
        for iy in range(int(L / 1.5)):
            x, y = -W / 2 + 0.75 + ix * 1.5, -L / 2 + 0.75 + iy * 1.5
            mb.box((1.44, 1.44, 0.1), (x, y, -0.05 + rnd.uniform(-0.015, 0.015)), "Stone", 0.03)
    for side in (-1, 1):                                  # side walls with timber posts
        mb.box((0.4, L, H), (side * W / 2, 0, H / 2), "Stone", 0)
        for y in range(-10, 11, 4): mb.box((0.5, 0.4, H), (side * (W / 2 - 0.25), y, H / 2), "Wood", 0.04)
        mb.box((0.5, L, 0.4), (side * (W / 2 - 0.25), 0, 7.0), "Wood", 0.04)
    mb.box((W, 0.4, H), (0, L / 2, H / 2), "Stone", 0)
    mb.box((W, 0.4, H), (0, -L / 2, H / 2), "Stone", 0)
    for x in range(-12, 13, 4): mb.box((0.4, 0.5, H), (x, L / 2 - 0.25, H / 2), "Wood", 0.04)
    for y in range(-9, 10, 4):                            # roof trusses
        mb.box((W, 0.4, 0.5), (0, y, H - 0.25), "Wood", 0.05)
        mb.box((0.3, 0.3, 2.5), (0, y, H + 1.0), "Wood", 0.03)
    for y in (-6, 4):                                     # tall windows (left wall) and a skylight
        mb.box((0.3, 2.6, 4.2), (-W / 2 + 0.3, y, 5.0), "Wood", 0.05)
        mb.box((0.32, 2.2, 3.8), (-W / 2 + 0.32, y, 5.0), "Sky", 0)
        mb.box((0.34, 0.12, 3.8), (-W / 2 + 0.34, y, 5.0), "Wood", 0)
        mb.box((0.34, 2.2, 0.12), (-W / 2 + 0.34, y, 5.6), "Wood", 0)
    mb.box((3.0, 2.0, 0.1), (2, 4, H - 0.05), "Sky", 0)
    mb.box((14, 0.35, 0.45), (0, 4, 6.6), "Trim", 0.04)    # crane beam with chains
    mb.box((14, 1.4, 0.12), (0, L / 2 - 1.2, 1.0), "Wood", 0.03)   # back workbench
    for x in range(-6, 7, 3):
        mb.box((0.14, 1.2, 1.0), (x, L / 2 - 1.2, 0.5), "Wood", 0.02)
        mb.box((1.2, 0.08, 0.5), (x + 1.5, L / 2 - 1.9, 0.6), "Accent", 0.02)
        mb.box((0.3, 0.05, 0.05), (x + 1.5, L / 2 - 1.95, 0.6), "Trim", 0.01)
    for z in (1.2, 2.4, 3.6):                             # shelves with bottles (right wall)
        mb.box((0.8, 12, 0.08), (W / 2 - 0.8, 2, z), "Wood", 0.02)
        for i in range(14):
            y = -3.5 + i * 0.8 + rnd.uniform(-0.1, 0.1); h = rnd.uniform(0.3, 0.55)
            mb.cyl(0.13, h, (W / 2 - 0.8, y, z + 0.04 + h / 2), "Glass", segs=8, axis="Z")
            mb.cyl(0.11, h * 0.7, (W / 2 - 0.8, y, z + 0.04 + h * 0.35), rnd.choice(["Glow", "Liquid", "Trim", "Accent"]), segs=8, axis="Z")
            mb.cyl(0.05, 0.1, (W / 2 - 0.8, y, z + 0.09 + h), "Wood", segs=6, axis="Z")
    root = mb.build("Workshop_Room")
    for i, (x, y, z) in enumerate([(-6, 8, 3.2), (6, 8, 3.2), (-8, -2, 4.5), (0, 1.5, 4.5), (12, 2, 4.2)]):
        lamp = MB(); lamp.cyl(0.25, 0.2, (0, 0, 0), "Metal", segs=8, axis="Z", r2=0.12); lamp.sphere(0.12, (0, 0, -0.12), "Glow")
        lamp.box((0.03, 0.03, H - z), (0, 0, (H - z) / 2), "Metal", 0)
        lamp.build(f"Lamp{i}", root, (x, y, z))
    return root

PROPS_SRC = os.path.join(PROJECT, "Share/objects")
PROPS_TEX = os.path.join(EXPORT, "Textures")   # separate files: Unity ignores textures embedded by Blender
# Tripo props from Share/objects: (file, size kind "h" height / "w" widest side, metres, (x, y, z), rot Z°, tilt X°).
# Positions in room space (table at the origin, camera side -Y); z = base height. Hanging props get chains to the crane beam.
# Export with export(obj, EXPORT, embed=False) so the FBX points at the JPEGs in Kit/Textures.
PROPS = [
    ("drill", "w", 4.2, (-2.2, 4.0, 3.9), 0, 0), ("orb", "h", 1.7, (2.0, 4.0, 3.7), 0, 0), ("jugs", "h", 1.3, (5.0, 4.0, 3.8), 0, 0),
    ("boiler", "h", 2.4, (6.0, 8.6, 0), -15, 0), ("stuff", "h", 1.3, (3.0, 6.2, 0), 10, 0),
    ("machine4", "h", 2.0, (-7.5, 7.8, 0), 20, 0), ("machine3", "w", 1.6, (9.0, 6.0, 0), -30, 0),
    ("machine1", "w", 1.8, (-1.6, 9.85, 1.06), 0, 0), ("machine2", "w", 1.5, (2.3, 9.85, 1.06), 0, 0),
    ("machine5", "w", 1.6, (-4.9, 9.7, 1.06), 0, 0), ("box3", "w", 1.0, (4.9, 9.8, 1.06), 8, 0), ("gear3", "h", 0.8, (6.3, 9.8, 1.06), 0, 0),
    ("tools", "w", 2.2, (0.4, 10.6, 2.1), 0, 0), ("gear5", "h", 2.2, (-10.0, 10.0, 0), 0, -8), ("gear4", "h", 1.7, (10.0, 10.0, 0), 0, -8),
    ("gear1", "h", 1.0, (-5.5, 6.0, 0), 25, 0), ("gear2", "h", 0.9, (8.0, 3.0, 0), -20, 0),
    ("ladder", "h", 3.4, (13.4, 1.0, 0), -90, 0), ("box7", "h", 1.9, (-13.95, -1.0, 0), 90, 0),
    ("box1", "h", 1.1, (-11.5, 8.0, 0), 12, 0), ("box8", "w", 1.0, (-11.5, 8.0, 1.08), 30, 0), ("box2", "w", 1.1, (-9.6, 9.3, 0), -8, 0),
    ("chest", "w", 1.8, (11.5, 8.8, 0), -20, 0), ("box5", "w", 1.3, (-12.0, -7.0, 0), 5, 0), ("box6", "w", 1.3, (-12.0, -7.0, 0.62), -10, 0),
    ("box4", "w", 0.8, (7.8, 9.6, 0), 25, 0),
]
HANG = {"drill": (-1.2, 1.2), "orb": (0.15,), "jugs": (0.0,)}   # chain x offsets
BEAM_Z = 6.4

def _import_prop(name, tex_size=1024):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(PROPS_SRC, name + ".glb"))
    new = [o for o in bpy.data.objects if o not in before]
    ob = next(o for o in new if o.type == 'MESH')
    for o in new:
        if o is not ob: bpy.data.objects.remove(o, do_unlink=True)
    ob.data.transform(ob.matrix_world); ob.parent = None; ob.matrix_world = Matrix()
    vs = [v.co for v in ob.data.vertices]
    lo = Vector([min(v[i] for v in vs) for i in range(3)]); hi = Vector([max(v[i] for v in vs) for i in range(3)])
    ob.data.transform(Matrix.Translation(-Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))))
    for i, m in enumerate(ob.data.materials):
        m.name = f"Prop_{name}" + (f"_{i}" if i else "")
        for n in m.node_tree.nodes:
            if n.type != 'TEX_IMAGE' or not n.image: continue
            src = n.image
            if max(src.size) > tex_size: src.scale(tex_size, tex_size)
            # Re-saved as a small JPEG next to the FBX (props sit in the blurred background).
            w, h = src.size
            px = [0.0] * (w * h * 4); src.pixels.foreach_get(px)
            tmp = bpy.data.images.new("tmp", w, h); tmp.pixels.foreach_set(px)
            os.makedirs(PROPS_TEX, exist_ok=True)
            path = os.path.join(PROPS_TEX, f"Prop_{name}" + (f"_{i}" if i else "") + ".jpg")
            tmp.filepath_raw = path; tmp.file_format = 'JPEG'; tmp.save()
            bpy.data.images.remove(tmp)
            img = bpy.data.images.load(path)
            n.image = img
    return ob, hi - lo

def workshop_props():
    root = bpy.data.objects.new("Workshop_Props", None)
    bpy.context.scene.collection.objects.link(root)
    chains = MB()
    for name, kind, size, pos, rz, tx in PROPS:
        ob, dims = _import_prop(name)
        for c in ob.users_collection: c.objects.unlink(ob)
        bpy.context.scene.collection.objects.link(ob)
        k = size / (dims.z if kind == "h" else max(dims.x, dims.y))
        R = Matrix.Rotation(math.radians(rz), 4, 'Z') @ Matrix.Rotation(math.radians(tx), 4, 'X')
        ob.data.transform(R @ Matrix.Scale(k, 4))
        ob.name = "Prop_" + name; ob.parent = root; ob.location = pos
        top = pos[2] + dims.z * k
        for cx in HANG.get(name, ()):
            x = pos[0] + cx
            chains.box((0.05, 0.05, BEAM_Z - top + 0.15), (x, pos[1], (BEAM_Z + top - 0.15) / 2), "Metal", 0)
    chains.build("Chains", root)
    return root

# ---------------------------------------------------------------- catalog
SHARED = [
    ctl_button, lambda: ctl_button(2, 1), lambda: ctl_button(2, 2),
    ctl_lever, lambda: ctl_lever(2), lambda: ctl_lever(3),
    ctl_dial, lambda: ctl_dial(2), ctl_knob, lambda: ctl_knob(2),
    *[(lambda w=w, h=h: ctl_slider(w, h)) for (w, h) in ((2, 1), (3, 1), (4, 1), (1, 2), (1, 3), (1, 4))],
    ctl_joystick, lambda: ctl_joystick(2),
    *[(lambda w=w, h=h: disp_light(w, h)) for (w, h) in ((1, 1), (1, 2), (2, 1), (2, 2))],
    *[(lambda w=w, h=h: disp_bar(w, h)) for (w, h) in ((1, 1), (1, 2), (1, 3), (1, 4), (2, 1), (3, 1), (4, 1))],
    *[(lambda w=w, h=h: disp_gauge(w, h)) for (w, h) in ((1, 1), (2, 2), (2, 1), (3, 1))],
    *[(lambda w=w, h=h: disp_glyph(w, h)) for (w, h) in ((1, 1), (2, 1), (2, 2))],
    disp_rune, lambda: disp_rune(2), lambda: disp_script(2), lambda: disp_script(3),
    test_plunger, test_lever, lamp,
    lambda: book("Book_Manual", "Accent"), lambda: book("Book_Log", "Dark"), book_sheet, table, sample_tube, sample_rack, workshop_room, workshop_props,
    fill_vent, fill_grille, fill_plate, fill_pipes, fill_porthole, fill_fan, fill_hatch,
    in_tank, in_flask, in_hopper, in_sphere, out_tank, out_vat, out_flask, vial, pipe, flange,
]
KIT = [*[(lambda k, m=m: tile(k, m)) for m in TILE_MASKS],
       top_crest, top_chimney, top_dome, top_antenna, top_crystal, base_foot, base_plinth, base_skid]

def clear_scene(sc):
    for ob in list(sc.collection.all_objects):
        if ob.get("tripo") or ob.name in ("TH_Crucible", "TH_TwinFans", "TH_CoilTube", "CO_Gauges", "CO_Tank", "CO_CrystalBeacon"):
            continue  # keep Tripo props
        bpy.data.objects.remove(ob, do_unlink=True)

def build_all():
    sc = scene(); clear_scene(sc)
    pieces = [f() for f in SHARED]
    for kit in ("CO", "TH"):
        pieces += [f(kit) for f in KIT]
    return pieces

def _turn(obj):
    # 180° about Z baked into mesh data and child offsets; applying it twice restores the original.
    R = Matrix.Rotation(math.pi, 4, 'Z')
    for o in [obj] + list(obj.children_recursive):
        if o.type == 'MESH': o.data.transform(R)
        if o is not obj: o.location = R @ o.location

def export(obj, folder, embed=True):
    os.makedirs(folder, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in [obj] + list(obj.children_recursive): o.select_set(True)
    loc = obj.location.copy(); obj.location = (0, 0, 0)
    _turn(obj)
    try:
        bpy.ops.export_scene.fbx(filepath=os.path.join(folder, obj.name + ".fbx"), use_selection=True,
            axis_forward='-Z', axis_up='Y', bake_space_transform=True, apply_scale_options='FBX_SCALE_UNITS',
            object_types={'MESH', 'EMPTY'}, mesh_smooth_type='FACE', add_leaf_bones=False,
            path_mode='COPY' if embed else 'RELATIVE', embed_textures=embed)
    finally:
        _turn(obj)
        obj.location = loc

def layout_preview(pieces, per_row=12):
    for i, p in enumerate(pieces):
        p.location = ((i % per_row) * 2.6, 0, -(i // per_row) * 2.8)
