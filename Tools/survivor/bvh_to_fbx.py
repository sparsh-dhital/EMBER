# Converts CMU BVH motion capture into Unity-ready FBX clips, plus per-frame metrics used to
# pick loop ranges.
#   blender --background --python bvh_to_fbx.py -- <out_dir> <clip> [<clip> ...]
#
# The cgspeed CMU files carry a calibration T-pose in frame 1, but the skeleton's zero pose
# has the legs splayed ~21 degrees. Unity takes a model's rest pose as its Humanoid T-pose,
# so exporting as-is would retarget every clip with the legs crossed. Each clip is therefore
# re-rested on its frame-1 T-pose: a copy of the armature takes that pose as rest, follows the
# original through world-space Copy Transforms constraints, and the result is baked.
import bpy, sys, os, json, math
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
OUT, CLIPS = args[0], args[1:]
SRC = "C:/LudusForge/Tools/mocap"
SCALE = 0.0254 / 0.45          # CMU units are 0.45 inch
anim_dir = os.path.join(OUT, "unity", "anims")
met_dir = os.path.join(OUT, "anim_metrics")
os.makedirs(anim_dir, exist_ok=True)
os.makedirs(met_dir, exist_ok=True)

for clip in CLIPS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = 30
    bpy.ops.import_anim.bvh(filepath=f"{SRC}/{clip}.bvh", global_scale=SCALE, frame_start=1,
                            use_fps_scale=True, update_scene_fps=False, update_scene_duration=True,
                            rotate_mode="NATIVE", axis_forward="-Z", axis_up="Y")
    src = bpy.context.object
    # The take's own length: the scene keeps its 250-frame default for shorter clips.
    end = int(math.floor(src.animation_data.action.frame_range[1]))
    scene.frame_end = end

    # The re-rested copy.
    scene.frame_set(1)
    dst = src.copy()
    dst.data = src.data.copy()
    dst.animation_data_clear()
    dst.name = "CMU_" + clip
    scene.collection.objects.link(dst)
    for pb in dst.pose.bones:
        pb.matrix_basis = src.pose.bones[pb.name].matrix_basis.copy()
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = dst
    dst.select_set(True)
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.armature_apply(selected=False)
    R = lambda n: dst.matrix_world @ dst.data.bones[n].head_local
    arm = (R("LeftForeArm") - R("LeftArm")).normalized()
    thigh = (R("LeftLeg") - R("LeftUpLeg")).normalized()
    print("RESTCHECK", clip, "arm(%.2f %.2f %.2f)" % tuple(arm), "thigh(%.2f %.2f %.2f)" % tuple(thigh))
    for pb in dst.pose.bones:
        c = pb.constraints.new("COPY_TRANSFORMS")
        c.target, c.subtarget = src, pb.name
    bpy.ops.nla.bake(frame_start=2, frame_end=end, only_selected=False, visual_keying=True,
                     clear_constraints=True, use_current_action=False, bake_types={"POSE"})
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.data.objects.remove(src, do_unlink=True)

    # Metrics, every frame: hips position, facing, foot and toe heights (Blender Z-up, metres).
    def wpos(name):
        return dst.matrix_world @ dst.pose.bones[name].head
    frames = []
    for f in range(2, end + 1):
        scene.frame_set(f)
        hips = wpos("Hips")
        side = wpos("LeftUpLeg") - wpos("RightUpLeg")
        fwd = Vector((0, 0, 1)).cross(side)          # left-hand side x up -> forward
        heading = math.degrees(math.atan2(fwd.x, -fwd.y))
        frames.append([round(hips.x, 4), round(hips.y, 4), round(hips.z, 4), round(heading, 2),
                       round(wpos("LeftFoot").z, 4), round(wpos("RightFoot").z, 4),
                       round(wpos("LeftToeBase").z, 4), round(wpos("RightToeBase").z, 4),
                       *[round(c, 4) for c in wpos("RightHand")], *[round(c, 4) for c in wpos("LeftHand")]])
    with open(os.path.join(met_dir, clip + ".json"), "w") as fh:
        json.dump({"clip": clip, "fps": 30, "first_frame": 2, "frames": frames}, fh)

    scene.frame_start, scene.frame_end = 2, end
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(anim_dir, f"CMU_{clip}.fbx"), object_types={"ARMATURE"},
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        armature_nodetype="NULL", bake_anim=True, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
        bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
        axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL")
    print("CLIP", clip, "frames", end - 1, "hips@start", frames[0][:3])
