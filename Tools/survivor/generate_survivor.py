# Generates the EMBER survivor with MPFB (MakeHuman for Blender) and renders previews.
#
#   blender --background --python generate_survivor.py -- <out_dir>
#
# Spec, from the character sheet: male, early 30s, lean and hardened, weathered olive-tan
# skin, brown eyes, scruffy stubble; knit beanie, long open coat, work trousers, winter
# boots, short gloves. Rig: mixamo_unity, so Unity's Humanoid mapping picks it up as-is.
import bpy, sys, os, math
from mathutils import Vector
from bl_ext.user_default.mpfb.services import HumanService

out_dir = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "C:/LudusForge/Tools/survivor/out"
os.makedirs(out_dir, exist_ok=True)

# A clean, empty scene.
bpy.ops.wm.read_factory_settings(use_empty=True)

# MakeHuman age: 0.5 = 25 years, 1.0 = 90 years, linear in between -> 32 years ~ 0.554.
human_info = HumanService._create_default_human_info_dict()
human_info["phenotype"] = {
    "gender": 1.0, "age": 0.554, "muscle": 0.62, "weight": 0.42,
    "proportions": 0.65, "height": 0.56, "cupsize": 0.5, "firmness": 0.5,
    # A mix rather than one preset reads as olive-tan rather than pale or dark.
    "race": {"asian": 0.12, "caucasian": 0.63, "african": 0.25},
}
human_info["rig"] = "mixamo_unity"
human_info["eyes"] = "high-poly.mhclo"
human_info["eyebrows"] = "eyebrow010.mhclo"
human_info["eyelashes"] = "eyelashes01.mhclo"
human_info["teeth"] = "teeth_base.mhclo"
human_info["tongue"] = "tongue01.mhclo"
human_info["hair"] = "short02.mhclo"
human_info["clothes"] = [
    "male_casualsuit04.mhclo",                      # shirt + trousers under the coat
    "mindfront_cardigan_long_open_front.mhclo",      # the long open coat (re-coloured later)
    "mindfront_knitted_hat_01.mhclo",                # knit beanie
    "punkduck_winter_boots.mhclo",
    "toigo_gloves_short.mhclo",
    "wdg_scruffy_beard.mhclo",
]
human_info["skin_mhmat"] = "middleage_caucasian_male.mhmat"   # weathered, not young
human_info["skin_material_type"] = "GAMEENGINE"   # plain textured PBR: survives FBX export
human_info["eyes_material_type"] = "MAKESKIN"
human_info["clothes_material_type"] = "GAMEENGINE"
human_info["alternative_materials"] = {}

settings = HumanService.get_default_deserialization_settings()
settings["subdiv_levels"] = 0          # game mesh: no subdivision
settings["material_instances"] = "NEVER"
basemesh = HumanService.deserialize_from_dict(human_info, settings)

# ---- report
objs = [o for o in bpy.data.objects]
print("OBJECTS", [(o.name, o.type, len(o.data.vertices) if o.type == "MESH" else (len(o.data.bones) if o.type == "ARMATURE" else 0)) for o in objs])
rig = next((o for o in objs if o.type == "ARMATURE"), None)
height = max((o.matrix_world @ Vector(c)).z for o in objs if o.type == "MESH" for c in o.bound_box)
print("HEIGHT", round(height, 3), "RIG", rig.name if rig else None)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, "survivor_raw.blend"))

# ---- preview lighting and camera: warm lantern key, cool moon rim, dark world
scene = bpy.context.scene
world = bpy.data.worlds.new("Night")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.012, 0.016, 0.026, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
scene.world = world

def light(name, kind, loc, energy, color, size=1.0):
    data = bpy.data.lights.new(name, kind)
    data.energy = energy
    data.color = color
    if kind == "AREA":
        data.size = size
    ob = bpy.data.objects.new(name, data)
    ob.location = loc
    scene.collection.objects.link(ob)
    direction = Vector((0, 0, 1.2)) - ob.location
    ob.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    return ob

light("Key", "AREA", (-1.2, -1.8, 1.7), 220, (1.0, 0.72, 0.45), 0.8)
light("Fill", "AREA", (1.6, -1.6, 1.2), 40, (0.55, 0.62, 0.8), 1.5)
light("Rim", "AREA", (0.6, 2.0, 2.2), 260, (0.55, 0.7, 1.0), 1.0)

cam_data = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam

def aim(loc, target, lens):
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    cam_data.lens = lens

try:
    scene.render.engine = "BLENDER_EEVEE"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.view_settings.view_transform = "AgX"
scene.render.film_transparent = False

def render(name, res):
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.filepath = os.path.join(out_dir, name)
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath)

aim((0.0, -4.6, 1.0), (0.0, 0.0, 0.92), 50)
render("preview_body.png", (700, 1100))
aim((0.18, -0.95, height - 0.12), (0.0, 0.0, height - 0.14), 85)
render("preview_face.png", (900, 900))
