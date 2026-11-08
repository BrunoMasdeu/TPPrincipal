"""Run with Blender --background --python Tools/Animation/create_wallrun.py.
Author on the original CC skeleton; export baked, in-place cycles for Unity.
Blender axes: +X character left, -Y forward, +Z up; source units are cm.
"""
import bpy, math
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Animations/WallRun'
SOURCE = ROOT / 'ArtSource/WallRun'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(ROOT / 'Assets/Prefabs/Soldier/Mark_1_Olive_IdlePose.fbx'))
rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
rig.animation_data_clear()
scene = bpy.context.scene
scene.render.fps = 30
scene.frame_start, scene.frame_end = 1, 25

def update():
    bpy.context.view_layer.update()

def aim(name, child, target):
    bone = rig.pose.bones[name]
    origin = bone.head.copy()
    current = rig.pose.bones[child].head - origin
    q = current.rotation_difference(Vector(target) - origin)
    bone.matrix = Matrix.Translation(origin) @ q.to_matrix().to_4x4() @ bone.matrix.to_quaternion().to_matrix().to_4x4()
    update()

def leg(side, ankle):
    thigh, calf, foot = [f'CC_Base_{side}_{n}' for n in ('Thigh','Calf','Foot')]
    hip = rig.pose.bones[thigh].head.copy()
    a = (rig.pose.bones[calf].head-hip).length
    b = (rig.pose.bones[foot].head-rig.pose.bones[calf].head).length
    delta = Vector(ankle)-hip
    d = min(delta.length, a+b-0.5)
    axis = delta.normalized()
    pole = Vector((0,-1,0))
    pole = (pole-axis*pole.dot(axis)).normalized()
    along = (a*a-b*b+d*d)/(2*d)
    knee = hip+axis*along+pole*math.sqrt(max(0,a*a-along*along))
    aim(thigh,calf,knee)
    aim(calf,foot,ankle)

for wall, sign in [('Left',1),('Right',-1)]:
    action = bpy.data.actions.new('WallRun'+wall)
    action.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = action
    for frame in range(1,26):
        scene.frame_set(frame)
        for p in rig.pose.bones:
            p.matrix_basis = Matrix.Identity(4)
            p.rotation_mode = 'QUATERNION'
        update()
        phase = (frame-1)/24 * math.tau
        hip = rig.pose.bones['CC_Base_Hip']
        # Small hip bank leaves the weapon platform steady; legs reach the wall.
        hip.rotation_quaternion = Quaternion((0,1,0), math.radians(-sign*10))
        hip.location.z = 1.2*math.cos(phase*2)
        update()
        for side, offset in [('L',0),('R',math.pi)]:
            p = phase+offset
            # Stance moves backward along wall; recovery lifts away from it.
            recovery = max(0,math.sin(p))
            ankle = (sign*(46-12*recovery), 24*math.cos(p), 39+11*recovery)
            leg(side,ankle)
            foot = rig.pose.bones[f'CC_Base_{side}_Foot']
            # Rotate the rest foot so the sole faces the wall, with toe forward.
            rest = foot.bone.matrix_local.to_quaternion()
            bank = Quaternion((0,1,0), math.radians(-sign*82))
            foot.matrix = Matrix.Translation(foot.head) @ (bank @ rest).to_matrix().to_4x4()
            update()
        # Relaxed counter-swing for unarmed use. Armed poses override this in Unity.
        for side, s, offset in [('L',1,0),('R',-1,math.pi)]:
            p = phase+offset
            shoulder = rig.pose.bones[f'CC_Base_{side}_Upperarm'].head.copy()
            elbow = shoulder+Vector((s*10, -12-5*math.cos(p), -19))
            hand = elbow+Vector((-s*3,-19,-2))
            aim(f'CC_Base_{side}_Upperarm',f'CC_Base_{side}_Forearm',elbow)
            aim(f'CC_Base_{side}_Forearm',f'CC_Base_{side}_Hand',hand)
        for bone in rig.pose.bones:
            bone.keyframe_insert('location', frame=frame, group=bone.name)
            bone.keyframe_insert('rotation_quaternion', frame=frame, group=bone.name)
            bone.keyframe_insert('scale', frame=frame, group=bone.name)
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=str(OUT / ('WallRun'+wall+'.fbx')),
        use_selection=True, object_types={'ARMATURE'}, add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0, axis_forward='-Z', axis_up='Y')

scene.frame_set(7)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / 'PlayerWallRun.blend'))
print('WALLRUN_EXPORT_COMPLETE')
