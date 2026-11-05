# Builds the EMBER survivor end to end with MPFB (MakeHuman for Blender):
# generate -> dress -> clean up -> T-pose -> export FBX for Unity -> preview renders.
#
#   blender --background --python build_survivor.py -- <out_dir>
#
# Spec (character sheet): male, early 30s, lean and hardened, weathered skin, brown eyes,
# close-cropped beard; knit beanie, long open coat over a work shirt and trousers, tall
# leather boots, short gloves. Colours (umber coat, maroon beanie, charcoal trousers) are
# applied in Unity as material tints over these textures.
import bpy, bmesh, sys, os, math, json
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from bl_ext.user_default.mpfb.services import HumanService, ExportService

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "C:/LudusForge/Tools/survivor/out"
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

# ------------------------------------------------------------------ generate
human_info = HumanService._create_default_human_info_dict()
human_info["phenotype"] = {
    # MakeHuman age: 0.5 = 25 y, 1.0 = 90 y, linear -> 32 y ~ 0.554
    "gender": 1.0, "age": 0.554, "muscle": 0.62, "weight": 0.42,
    "proportions": 0.65, "height": 0.56, "cupsize": 0.5, "firmness": 0.5,
    "race": {"asian": 0.12, "caucasian": 0.63, "african": 0.25},
}
human_info["rig"] = "mixamo_unity"
human_info["eyes"] = "high-poly.mhclo"
human_info["eyebrows"] = "eyebrow001.mhclo"
human_info["eyelashes"] = "eyelashes01.mhclo"
human_info["hair"] = "short02.mhclo"
CLOTHES = {
    "Shirt":  "male_casualsuit01",                    # work shirt + trousers
    "Coat":   "mindfront_cardigan_long_open_front",   # long open coat
    "Beanie": "mindfront_knitted_hat_01",
    "Boots":  "mindfront_shoes_biker_boots_male",
    "Gloves": "toigo_gloves_short",
    "Beard":  "wdg_scruffy_beard",
}
human_info["clothes"] = [v + ".mhclo" for v in CLOTHES.values()]
human_info["skin_mhmat"] = "middleage_caucasian_male.mhmat"
human_info["skin_material_type"] = "GAMEENGINE"
human_info["eyes_material_type"] = "MAKESKIN"
human_info["clothes_material_type"] = "GAMEENGINE"
human_info["alternative_materials"] = {}
settings = HumanService.get_default_deserialization_settings()
settings["subdiv_levels"] = 0
settings["material_instances"] = "NEVER"
basemesh = HumanService.deserialize_from_dict(human_info, settings)
rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")

def meshes():
    return [o for o in bpy.data.objects if o.type == "MESH"]

def find(suffix):
    return next(o for o in meshes() if o.name.endswith(suffix))

def ctx(o):
    return bpy.context.temp_override(object=o, active_object=o, selected_objects=[o], selected_editable_objects=[o])

# ------------------------------------------------------------------ bake to a plain game mesh
# Helper geometry and the clothes' delete-groups (body hidden under clothing) baked in;
# then every morph collapsed into the mesh, since Unity gets one fixed body.
ExportService.bake_modifiers_remove_helpers(basemesh, bake_masks=True, bake_subdiv=False,
                                            remove_helpers=True, also_proxy=True)
for o in meshes():
    for m in list(o.modifiers):
        if m.type != "ARMATURE":
            o.modifiers.remove(m)
    if o.data.shape_keys:
        with ctx(o):
            bpy.ops.object.shape_key_remove(all=True, apply_mix=True)

# Mobile budget: the boots and gloves came in far denser than anything else on the body.
def decimate(o, ratio):
    m = o.modifiers.new("Decimate", "DECIMATE")
    m.ratio = ratio
    with ctx(o):
        bpy.ops.object.modifier_move_to_index(modifier=m.name, index=0)
        bpy.ops.object.modifier_apply(modifier=m.name)

decimate(find("mindfront_shoes_biker_boots_male"), 0.35)
decimate(find("toigo_gloves_short"), 0.5)

# ------------------------------------------------------------------ geometry helpers
def world_bvh(o):
    m = o.matrix_world
    verts = [m @ v.co for v in o.data.vertices]
    return BVHTree.FromPolygons(verts, [p.vertices for p in o.data.polygons]), verts

def push_inside(inner, outers, gap, reach, max_push):
    """Moves inner-layer vertices back under the outer layers wherever they poke out or
    sit closer than `gap` beneath them. Only outer surface within `reach` counts, so an
    open coat front leaves the shirt behind it alone."""
    trees = [world_bvh(o)[0] for o in outers]
    mw = inner.matrix_world
    inv = mw.inverted()
    nmat = mw.to_3x3().inverted().transposed()
    moved = 0
    for v in inner.data.vertices:
        co = mw @ v.co
        n = (nmat @ v.normal).normalized()
        push = 0.0
        for t in trees:
            hit = t.find_nearest(co, reach)
            if hit[0] is None:
                continue
            loc, onorm = hit[0], hit[1]
            signed = (co - loc).dot(onorm)          # > 0: inner vertex is outside that surface
            if signed > -gap:
                push = max(push, signed + gap)
        if push > 0.0:
            co -= n * min(push, max_push)
            v.co = inv @ co
            moved += 1
    return moved

def delete_faces(obj, test):
    """Deletes the faces of obj for which test(world_centre, world_normal) is true."""
    mw = obj.matrix_world
    nmat = mw.to_3x3().inverted().transposed()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    doomed = [f for f in bm.faces
              if test(mw @ f.calc_center_median(), (nmat @ f.normal).normalized())]
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bm.to_mesh(obj.data)
    bm.free()
    return len(doomed)

def covered_by(outers, reach, poke=0.0, tilts=4):
    """True for a face that every one of five rays - along its normal and tilted 25 degrees
    four ways - meets an outer layer within reach: buried for good, so it can go. Faces at
    the edge of an outer layer (the open coat front) keep at least one clear ray and stay.
    With poke > 0, a face with an outer layer just *beneath* it is poking through that layer
    and goes too: pushing it back would only crease it."""
    trees = [world_bvh(o)[0] for o in outers]
    def test(c, n):
        if poke > 0.0 and any(t.ray_cast(c - n * 0.001, -n, poke)[0] is not None for t in trees):
            return True
        side = n.orthogonal().normalized()
        up = n.cross(side).normalized()
        k = math.tan(math.radians(25))
        dirs = (n, n + side * k, n - side * k, n + up * k, n - up * k)[:1 + tilts]
        for d in dirs:
            d = d.normalized()
            if not any(t.ray_cast(c + d * 0.001, d, reach)[0] is not None for t in trees):
                return False
        return True
    return test

shirt, coat, boots = find(CLOTHES["Shirt"]), find(CLOTHES["Coat"]), find(CLOTHES["Boots"])
gloves, hat, hair = find(CLOTHES["Gloves"]), find(CLOTHES["Beanie"]), find("short02")
beard = find(CLOTHES["Beard"])

# ------------------------------------------------------------------ stubble, not a 3D beard
# The beard asset is a full beard made of cards; a few days' growth reads far better painted
# into the skin. Record which skin the beard covers (as UV triangles), then drop the mesh.
beard_tree, _ = world_bvh(beard)
mw = basemesh.matrix_world
uv = basemesh.data.uv_layers.active.data
stubble = []
for p in basemesh.data.polygons:
    c = mw @ p.center
    hit = beard_tree.find_nearest(c, 0.014)
    if hit[0] is not None:
        stubble.append([[uv[li].uv[0], uv[li].uv[1]] for li in p.loop_indices])
with open(os.path.join(OUT, "stubble_uv.json"), "w") as fh:
    json.dump(stubble, fh)
print("STUBBLE faces", len(stubble))
bpy.data.objects.remove(beard, do_unlink=True)

# ------------------------------------------------------------------ layering
# Inner layers the outer ones bury for good are deleted; whatever is left that still pokes
# out is pushed back underneath.
# Under the coat the shirt only needs to survive where the open front shows it, so the test
# is looser here: the normal ray and two side tilts, reaching 8 cm.
print("HIDDEN shirt", delete_faces(shirt, covered_by([coat, boots], 0.08, poke=0.03, tilts=2)))
# Under a long coat the shirt is only ever seen through the open front and at the collar.
# Everything else (back, sides, sleeves) is dropped outright: kept, it pokes through the
# coat as soon as the arms move, because the two garments are weighted differently.
hip_z = (rig.matrix_world @ rig.data.bones["mixamorig:Hips"].head_local).z
neck_z = (rig.matrix_world @ rig.data.bones["mixamorig:Neck"].head_local).z
coat_tree = world_bvh(coat)[0]
FORWARD = Vector((0.0, -1.0, 0.0))            # MakeHuman faces -Y
def shirt_unseen(c, n):
    if c.z < hip_z + 0.05:
        return False                          # trousers: the boots rule handles the shins
    if abs(c.x) > 0.22:
        return True                           # sleeves, inside the coat sleeves
    if c.z > neck_z - 0.05 and abs(c.x) < 0.11:
        return False                          # collar
    if n.y < 0.15 and coat_tree.ray_cast(c + FORWARD * 0.002, FORWARD, 0.5)[0] is None:
        return False                          # seen through the open front
    return True
print("UNSEEN shirt", delete_faces(shirt, shirt_unseen))
# Trousers are tucked into the boots: everything below the shafts is hidden or bunching out.
boot_top = max((boots.matrix_world @ v.co).z for v in boots.data.vertices)
print("TUCKED trousers", delete_faces(shirt, lambda c, n: c.z < boot_top - 0.012))
print("HIDDEN gloves", delete_faces(gloves, covered_by([coat], 0.04)))
rim = min((hat.matrix_world @ v.co).z for v in hat.data.vertices)
print("HAIR above beanie rim", delete_faces(hair, lambda c, n: c.z > rim + 0.004))
print("PUSH shirt", push_inside(shirt, [coat, boots], gap=0.006, reach=0.035, max_push=0.025))
print("PUSH gloves", push_inside(gloves, [coat], gap=0.004, reach=0.03, max_push=0.02))
print("PUSH hair", push_inside(hair, [hat], gap=0.004, reach=0.05, max_push=0.05))

# ------------------------------------------------------------------ T-pose
# MakeHuman builds in an A-pose. Unity's Humanoid avatar wants a T-pose so retargeted
# motion lands correctly, so swing each arm chain out level and make that the rest pose.
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="POSE")
for side, x in (("Left", 1.0), ("Right", -1.0)):
    for bone in ("Arm", "ForeArm", "Hand"):
        pb = rig.pose.bones["mixamorig:" + side + bone]
        bpy.context.view_layer.update()
        head = rig.matrix_world @ pb.head
        tail = rig.matrix_world @ pb.tail
        rot = (tail - head).normalized().rotation_difference(Vector((x, 0.0, 0.0)))
        m = Matrix.Translation(pb.head) @ rot.to_matrix().to_4x4() @ Matrix.Translation(-pb.head)
        pb.matrix = m @ pb.matrix
bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode="OBJECT")

for o in meshes():
    arm = next((m for m in o.modifiers if m.type == "ARMATURE"), None)
    if arm:
        with ctx(o):
            bpy.ops.object.modifier_apply(modifier=arm.name)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="POSE")
bpy.ops.pose.select_all(action="SELECT")
bpy.ops.pose.armature_apply(selected=False)
bpy.ops.object.mode_set(mode="OBJECT")
for o in meshes():
    mod = o.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    mod.use_vertex_groups = True

# ------------------------------------------------------------------ names Unity will see
rig.name = "Survivor"
names = {basemesh: "Body", find("high-poly"): "Eyes", find("eyebrow001"): "Eyebrows",
         find("eyelashes01"): "Eyelashes", hair: "Hair", shirt: "Shirt", coat: "Coat",
         hat: "Beanie", boots: "Boots", gloves: "Gloves"}
for o, n in names.items():
    o.name = "Survivor_" + n
    for i, slot in enumerate(o.material_slots):
        if slot.material:
            slot.material.name = "M_Survivor_" + n + ("" if i == 0 else str(i))

per = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes()}
print("TRIS", per)
tris = sum(per.values())
head_bone = rig.data.bones["mixamorig:Head"]
eye_z = (rig.matrix_world @ head_bone.head_local).z + 0.09
print("STATS meshes", len(meshes()), "triangles", tris, "bones", len(rig.data.bones), "eye height", round(eye_z, 3))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "Survivor.blend"))

# ------------------------------------------------------------------ FBX for Unity
fbx_dir = os.path.join(OUT, "unity")
os.makedirs(fbx_dir, exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=os.path.join(fbx_dir, "Survivor.fbx"),
    object_types={"ARMATURE", "MESH"}, use_mesh_modifiers=True, mesh_smooth_type="FACE",
    add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
    armature_nodetype="NULL", bake_anim=False,
    axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL",
    path_mode="COPY", embed_textures=False)
print("EXPORTED", os.path.join(fbx_dir, "Survivor.fbx"))
