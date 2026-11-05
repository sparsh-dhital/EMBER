# Preview renders of the exported survivor, close to how Unity will show it:
# stubbled skin, the planned material tints, arms relaxed out of the T-pose.
#   blender --background out/Survivor.blend --python render_preview.py -- <out_dir> [tag]
import bpy, sys, os
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
OUT = args[0]
TAG = args[1] if len(args) > 1 else "v2"
scene = bpy.context.scene

# Same tints Unity will apply as _BaseColor (multiplied over each texture).
TINTS = {
    "Survivor_Body":   (1.0, 0.86, 0.74),     # sun-weathered, olive-tan
    "Survivor_Coat":   (0.36, 0.25, 0.15),    # faded umber
    "Survivor_Beanie": (0.30, 0.08, 0.07),    # dark rust-maroon
    "Survivor_Shirt":  (0.42, 0.40, 0.38),    # charcoal work shirt and trousers
    "Survivor_Gloves": (0.50, 0.42, 0.35),
    "Survivor_Eyebrows": (0.40, 0.32, 0.26),
}
stubbled = os.path.join(OUT, "unity", "Survivor.fbm", "middleage_lightskinned_male_diffuse.png")
for img in bpy.data.images:
    if img.filepath.replace("\\", "/").endswith("middleage_lightskinned_male_diffuse.png"):
        img.filepath = stubbled
        img.reload()

for ob in bpy.data.objects:
    if ob.type != "MESH" or ob.name not in TINTS:
        continue
    for slot in ob.material_slots:
        mat = slot.material
        if not mat or not mat.use_nodes:
            continue
        nt = mat.node_tree
        bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if not bsdf or not bsdf.inputs["Base Color"].is_linked:
            continue
        link = bsdf.inputs["Base Color"].links[0]
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        nt.links.new(link.from_socket, mix.inputs[6])
        mix.inputs[7].default_value = (*TINTS[ob.name], 1.0)
        nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])

# Relax the arms out of the T-pose for the shot (rest pose stays T for Unity).
rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="POSE")
for side, x in (("Left", 1.0), ("Right", -1.0)):
    for bone, target in (("Arm", Vector((0.36 * x, -0.08, -0.93))), ("ForeArm", Vector((0.22 * x, -0.25, -0.94)))):
        pb = rig.pose.bones["mixamorig:" + side + bone]
        bpy.context.view_layer.update()
        d = (pb.tail - pb.head).normalized()
        rot = d.rotation_difference(target.normalized())
        pb.matrix = Matrix.Translation(pb.head) @ rot.to_matrix().to_4x4() @ Matrix.Translation(-pb.head) @ pb.matrix
bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode="OBJECT")

world = bpy.data.worlds.new("Night")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.012, 0.016, 0.026, 1)
scene.world = world

def light(name, loc, energy, color, size):
    d = bpy.data.lights.new(name, "AREA")
    d.energy, d.color, d.size = energy, color, size
    ob = bpy.data.objects.new(name, d)
    ob.location = loc
    scene.collection.objects.link(ob)
    ob.rotation_euler = (Vector((0, 0, 1.3)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()

light("Key", (-1.1, -1.7, 1.6), 230, (1.0, 0.68, 0.40), 0.7)      # lantern-warm key
light("Fill", (1.6, -1.6, 1.2), 30, (0.55, 0.62, 0.8), 1.5)
light("Rim", (0.7, 2.0, 2.2), 280, (0.55, 0.7, 1.0), 1.0)          # cold moon rim
cam_data = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
scene.render.engine = "BLENDER_EEVEE"
scene.view_settings.view_transform = "AgX"

head = rig.matrix_world @ rig.data.bones["mixamorig:Head"].head_local
eye_z = head.z + 0.09

def shot(name, loc, target, lens, res):
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    cam_data.lens = lens
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.filepath = os.path.join(OUT, name)
    bpy.ops.render.render(write_still=True)
    print("RENDERED", name)

shot("survivor_%s_face.png" % TAG, (0.16, -0.78, eye_z - 0.02), (0.0, 0.0, eye_z - 0.05), 85, (900, 900))
shot("survivor_%s_body.png" % TAG, (-1.9, -3.6, 1.15), (0.0, 0.0, 0.93), 45, (800, 1100))
shot("survivor_%s_back.png" % TAG, (1.6, 3.8, 1.15), (0.0, 0.0, 0.93), 45, (800, 1100))
