import bpy, sys
f = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=f)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
for n in ["Hips", "LowerBack", "Spine1", "Neck", "Head", "LeftArm", "LeftForeArm", "RightArm", "LeftUpLeg", "LeftLeg", "RightUpLeg"]:
    b = arm.data.bones.get(n)
    if b is None: print("REST", n, "missing"); continue
    d = (arm.matrix_world.to_3x3() @ (b.tail_local - b.head_local)).normalized()
    print("REST %-12s dir=(%5.2f %5.2f %5.2f) head_z=%.3f" % (n, d.x, d.y, d.z, (arm.matrix_world @ b.head_local).z))
act = arm.animation_data.action if arm.animation_data else None
print("ACTION", act.name if act else None, act.frame_range if act else None)
