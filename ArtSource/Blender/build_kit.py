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
           "Accent": (0.35, 0.20, 0.12), "Glow": (0.25, 1.0, 0.85), "Dark": (0.08, 0.08, 0.09), "Glass": (0.7, 0.9, 0.95), "Liquid": (0.3, 0.9, 0.8)},
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
    def sphere(self, r, center, slot, kit=None):
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=1, radius=r, matrix=Matrix.Translation(center))
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

def rivets(mb, xs, z, y, slot="Metal", r=0.025):
    for x in xs: mb.cyl(r, 0.03, (x, y, z), slot, segs=6)

# ---------------------------------------------------------------- shared mechanisms
def control_plate(mb):
    mb.box((0.96, 0.03, 0.96), (0, -0.015, 0), "Panel", 0.02)

def frame(root):
    f = MB(); 
    f.box((1.0, 0.02, 0.06), (0, -0.01, 0.5), "Trim"); f.box((1.0, 0.02, 0.06), (0, -0.01, -0.5), "Trim")
    f.box((0.06, 0.02, 1.0), (0.5, -0.01, 0), "Trim"); f.box((0.06, 0.02, 1.0), (-0.5, -0.01, 0), "Trim")
    return f.build(root.name + "_Frame", root)

def ctl_button():
    mb = MB(); control_plate(mb)
    mb.cyl(0.27, 0.08, (0, -0.07, 0.05), "Metal", segs=8, bevel=0.015, rot_z=math.pi / 8)
    root = mb.build("Ctl_Button"); frame(root)
    m = MB(); m.cyl(0.19, 0.12, (0, 0, 0), "Glow", segs=8, r2=0.16, bevel=0.015, rot_z=math.pi / 8)
    m.build("Moving", root, (0, -0.16, 0.05))
    return root

def ctl_lever():
    mb = MB(); control_plate(mb)
    mb.box((0.18, 0.04, 0.7), (0, -0.05, 0.05), "Dark", 0.015)
    mb.box((0.26, 0.05, 0.12), (0, -0.06, 0.05), "Metal", 0.015)
    root = mb.build("Ctl_Lever"); frame(root)
    m = MB(); m.box((0.09, 0.09, 0.42), (0, 0, 0.21), "Metal", 0.015)
    m.cyl(0.09, 0.24, (0, 0, 0.46), "Accent", segs=6, axis="Z", bevel=0.015)
    m.cyl(0.07, 0.16, (0, 0, 0), "Metal", segs=8, axis="X")
    m.build("Moving", root, (0, -0.1, 0.05))
    return root

def ctl_dial(name, radius, slot):
    mb = MB(); control_plate(mb)
    mb.cyl(radius + 0.05, 0.04, (0, -0.05, 0.05), "Dark", segs=12)
    root = mb.build(name); frame(root)
    m = MB()
    m.cyl(radius, 0.1, (0, 0, 0), slot, segs=8, r2=radius * 0.8, bevel=0.015, rot_z=math.pi / 8)
    m.box((0.05, 0.05, radius * 0.55), (0, -0.07, radius * 0.45), "Dark", 0.005)
    m.cyl(0.04, 0.1, (0, -0.08, radius * 0.85), "Glow", segs=6)
    m.build("Moving", root, (0, -0.12, 0.05))
    return root

def ctl_knob():
    mb = MB(); control_plate(mb)
    mb.cyl(0.33, 0.04, (0, -0.05, 0.05), "Dark", segs=12)
    root = mb.build("Ctl_Knob"); frame(root)
    m = MB()
    m.cyl(0.25, 0.14, (0, 0, 0), "Metal", segs=6, r2=0.2, bevel=0.015)
    m.cyl(0.14, 0.2, (0, -0.15, 0), "Metal", segs=6, r2=0.0)   # pointed cap like the concept knobs
    m.box((0.05, 0.03, 0.14), (0, -0.09, 0.26), "Dark", 0.0)
    m.build("Moving", root, (0, -0.1, 0.05))
    return root

def ctl_joystick():
    # Stick points at the camera (-Y); Moving pivots at the boot so tilting moves the ball up/down/left/right.
    mb = MB(); control_plate(mb)
    mb.cyl(0.3, 0.05, (0, -0.05, 0.05), "Metal", segs=8, bevel=0.015, rot_z=math.pi / 8)
    mb.cyl(0.2, 0.1, (0, -0.1, 0.05), "Dark", segs=8, r2=0.12, rot_z=math.pi / 8)
    root = mb.build("Ctl_Joystick"); frame(root)
    m = MB()
    m.cyl(0.04, 0.3, (0, -0.15, 0), "Metal", segs=8)
    m.sphere(0.09, (0, -0.32, 0), "Accent")
    m.build("Moving", root, (0, -0.12, 0.05))
    return root

def disp_light():
    mb = MB(); mb.cyl(0.27, 0.06, (0, -0.03, 0), "Metal", segs=8, bevel=0.015, rot_z=math.pi / 8)
    mb.cyl(0.2, 0.04, (0, -0.07, 0), "Dark", segs=8, rot_z=math.pi / 8)
    root = mb.build("Disp_Light")
    b = MB(); b.sphere(0.17, (0, 0, 0), "Glow"); b.build("Bulb", root, (0, -0.12, 0))
    return root

def disp_bar():
    mb = MB(); mb.box((0.38, 0.05, 0.95), (0, -0.025, 0), "Metal", 0.02)
    mb.box((0.3, 0.03, 0.85), (0, -0.06, 0), "Dark", 0.0)
    root = mb.build("Disp_Bar")
    for i in range(5):
        s = MB(); s.box((0.22, 0.03, 0.12), (0, 0, 0), "Glow", 0.01); s.build(f"Seg{i}", root, (0, -0.08, -0.32 + i * 0.16))
    return root

def disp_gauge():
    mb = MB(); mb.cyl(0.42, 0.06, (0, -0.03, 0), "Metal", segs=12, bevel=0.015)
    mb.cyl(0.36, 0.03, (0, -0.07, 0), "Panel", segs=12)
    for i in range(7):
        a = math.radians(-70 + i * 140 / 6)
        mb.box((0.02, 0.01, 0.06), (math.sin(a) * 0.29, -0.09, math.cos(a) * 0.29 - 0.08), "Dark", 0, rot=Matrix.Rotation(-a, 3, 'Y'))
    root = mb.build("Disp_Gauge")
    n = MB(); n.box((0.03, 0.02, 0.36), (0, 0, 0.18), "Accent", 0); n.cyl(0.04, 0.03, (0, 0, 0), "Dark", segs=6)
    n.build("Needle", root, (0, -0.1, -0.1))
    return root

def disp_glyph():
    mb = MB(); mb.box((0.8, 0.06, 0.8), (0, -0.03, 0), "Metal", 0.03)
    mb.box((0.66, 0.03, 0.66), (0, -0.07, 0), "Dark", 0)
    root = mb.build("Disp_Glyph")
    for i in range(9):
        c = MB(); c.box((0.13, 0.02, 0.13), (0, 0, 0), "Glow", 0.01)
        c.build(f"Cell{i}", root, ((i % 3 - 1) * 0.18, -0.09, (1 - i // 3) * 0.18))
    return root

def tank():
    # Height 1.2, origin at bottom centre. "Liquid" is 1m tall from its origin; Unity scales it to fill.
    mb = MB()
    for z in (0.0, 1.2):
        mb.cyl(0.3, 0.08, (0, 0, z), "Metal", segs=8, axis="Z", bevel=0.015)
    for i in range(4):
        a = i * math.pi / 2 + math.pi / 4
        mb.box((0.04, 0.04, 1.2), (math.cos(a) * 0.27, math.sin(a) * 0.27, 0.6), "Metal", 0.005)
    mb.cyl(0.25, 1.12, (0, 0, 0.6), "Glass", segs=8, axis="Z")
    root = mb.build("Tank")
    l = MB(); l.cyl(0.22, 1.0, (0, 0, 0.5), "Liquid", segs=8, axis="Z"); l.build("Liquid", root, (0, 0, 0.04))
    return root

def pipe():
    mb = MB(); mb.cyl(0.06, 1.0, (0.5, 0, 0), "Metal", segs=8, axis="X")
    return mb.build("Pipe")

def flange():
    mb = MB(); mb.cyl(0.09, 0.06, (0, 0, 0), "Metal", segs=8, axis="X", bevel=0.01)
    return mb.build("Flange")

# ---------------------------------------------------------------- kit-specific chassis
# Segment: one 1.1m column, 2.6m tall, 0.9m deep; origin bottom-front-centre.
def segment(kit):
    mb = MB(kit)
    if kit == "CO":
        mb.box((1.1, 0.9, 2.5), (0, 0.45, 1.3), "Body", 0.03)
        for z in (0.03, 2.6):
            mb.box((1.12, 0.96, 0.16), (0, 0.45, z), "Trim", 0.03)
            rivets(mb, (-0.4, 0.4), z, -0.04)
        mb.box((0.5, 0.02, 0.06), (0, -0.03, 0.03), "Glow", 0)
    else:
        mb.box((1.1, 0.9, 2.5), (0, 0.45, 1.3), "Body", 0.12)
        mb.box((1.12, 0.96, 0.18), (0, 0.45, 2.6), "Trim", 0.05)
        mb.box((1.0, 0.94, 0.14), (0, 0.45, 0.02), "Trim", 0.05)
        mb.box((0.4, 0.02, 0.08), (0, -0.0, 0.18), "Glow", 0)
    # cream insets for display row and control row
    mb.box((0.94, 0.04, 1.0), (0, -0.0, 1.95), "Panel", 0.03)
    mb.box((0.94, 0.04, 1.1), (0, -0.0, 0.72), "Panel", 0.03)
    rivets(mb, (-0.47, 0.47), 1.95, -0.03)
    return mb.build(f"{kit}_Segment")

def end_cap(kit, side):
    s = -1 if side == "L" else 1
    mb = MB(kit)
    if kit == "CO":
        mb.box((0.3, 0.9, 2.3), (s * 0.15, 0.45, 1.3), "Body", 0.1)
        mb.box((0.34, 0.96, 0.16), (s * 0.15, 0.45, 2.6), "Trim", 0.03)
        mb.box((0.34, 0.96, 0.16), (s * 0.15, 0.45, 0.03), "Trim", 0.03)
        mb.box((0.04, 0.02, 0.5), (s * 0.15, -0.03, 1.3), "Glow", 0)
    else:
        mb.box((0.32, 0.8, 2.0), (s * 0.16, 0.45, 1.3), "Body", 0.14)
        mb.box((0.24, 0.6, 2.3), (s * 0.12, 0.45, 1.3), "Body", 0.1)
        for z in (1.0, 1.6):
            mb.box((0.04, 0.02, 0.3), (s * 0.2, 0.03, z), "Glow", 0, kit="TH")
    return mb.build(f"{kit}_End{side}")

def foot(kit):
    mb = MB(kit)
    if kit == "CO":
        mb.box((0.36, 0.5, 0.2), (0, 0.45, -0.1), "Metal", 0.03)
        mb.cyl(0.18, 0.4, (0, 0.45, -0.4), "Metal", segs=6, r2=0.12, axis="Z", bevel=0.02)
    else:
        mb.cyl(0.2, 0.45, (0, 0.45, -0.25), "Panel", segs=6, r2=0.12, axis="Z", bevel=0.02)
        mb.cyl(0.21, 0.1, (0, 0.45, -0.5), "Metal", segs=6, axis="Z", bevel=0.02)
    return mb.build(f"{kit}_Foot")

def bay(kit):
    # Side module holding one tank; 0.9 wide, origin bottom-front-centre. Tank sits at (0, 0.1, 0.55).
    mb = MB(kit)
    if kit == "CO":
        mb.box((0.9, 0.6, 2.1), (0, 0.5, 1.05), "Body", 0.05)
        mb.box((0.94, 0.7, 0.14), (0, 0.45, 2.1), "Trim", 0.03)
        mb.box((0.8, 0.5, 0.12), (0, 0.15, 0.5), "Trim", 0.03)
        mb.box((0.08, 0.1, 1.6), (-0.4, 0.1, 1.3), "Trim", 0.02)
        mb.box((0.08, 0.1, 1.6), (0.4, 0.1, 1.3), "Trim", 0.02)
    else:
        mb.box((0.9, 0.6, 2.0), (0, 0.5, 1.0), "Body", 0.12)
        mb.box((0.84, 0.5, 0.14), (0, 0.15, 0.5), "Trim", 0.04)
        mb.box((0.84, 0.5, 0.18), (0, 0.4, 2.05), "Trim", 0.06)
        mb.box((0.3, 0.02, 0.06), (0, -0.11, 0.5), "Glow", 0)
    return mb.build(f"{kit}_Bay")

def top_crest(kit):
    mb = MB(kit)
    if kit == "CO":
        mb.box((0.7, 0.5, 0.25), (0, 0.45, 0.12), "Trim", 0.05)
        mb.box((0.4, 0.02, 0.08), (0, 0.19, 0.12), "Glow", 0)
    else:
        mb.box((0.8, 0.5, 0.3), (0, 0.45, 0.15), "Trim", 0.1)
        mb.box((0.5, 0.02, 0.1), (0, 0.19, 0.15), "Glow", 0)
    return mb.build(f"{kit}_Top")

SHARED = [ctl_button, ctl_lever, lambda: ctl_dial("Ctl_Dial", 0.3, "Metal"), ctl_knob, ctl_joystick,
          disp_light, disp_bar, disp_gauge, disp_glyph, tank, pipe, flange]
KIT = [segment, lambda k: end_cap(k, "L"), lambda k: end_cap(k, "R"), foot, bay, top_crest]

def clear_scene(sc):
    for ob in list(sc.collection.all_objects):
        if ob.name.startswith(("TH_", "CO_")) and not any(ob.name.endswith(s) for s in ("_Segment", "_EndL", "_EndR", "_Foot", "_Bay", "_Top")):
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

def export(obj, folder):
    os.makedirs(folder, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in [obj] + list(obj.children_recursive): o.select_set(True)
    loc = obj.location.copy(); obj.location = (0, 0, 0)
    _turn(obj)
    try:
        bpy.ops.export_scene.fbx(filepath=os.path.join(folder, obj.name + ".fbx"), use_selection=True,
            axis_forward='-Z', axis_up='Y', bake_space_transform=True, apply_scale_options='FBX_SCALE_UNITS',
            object_types={'MESH', 'EMPTY'}, mesh_smooth_type='FACE', add_leaf_bones=False,
            path_mode='COPY', embed_textures=True)
    finally:
        _turn(obj)
        obj.location = loc

def layout_preview(pieces):
    x = 0
    for p in pieces:
        p.location = (x, 0, 0); x += 1.4
