import bpy, sys
clip = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.render.fps = 30
bpy.ops.import_anim.bvh(filepath=f"C:/LudusForge/Tools/mocap/{clip}.bvh", global_scale=0.0254/0.45, frame_start=1,
                        use_fps_scale=True, update_scene_fps=False, update_scene_duration=True, rotate_mode="NATIVE",
                        axis_forward="-Z", axis_up="Y")
a = bpy.context.object
def joints(tag):
    bpy.context.view_layer.update()
    P = lambda n: a.matrix_world @ a.pose.bones[n].head
    def d(p, c):
        v = (P(c) - P(p)).normalized(); return "(%5.2f %5.2f %5.2f)" % (v.x, v.y, v.z)
    print(tag, "arm", d("LeftArm", "LeftForeArm"), "fore", d("LeftForeArm", "LeftHand"),
          "thigh", d("LeftUpLeg", "LeftLeg"), "shin", d("LeftLeg", "LeftFoot"), "spine", d("LowerBack", "Neck"),
          "hipsZ %.2f" % P("Hips").z)
sc = bpy.context.scene
act = a.animation_data.action
a.animation_data.action = None
for pb in a.pose.bones:
    pb.matrix_basis.identity()
joints("ZERO ")
a.animation_data.action = act
for f in (1, 2, 40):
    sc.frame_set(f); joints("F%-4d" % f)
