import bpy, math
from pathlib import Path
from mathutils import Vector
root = Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(root/'ArtSource/WallRun/PlayerWallRun.blend'))
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.render.resolution_x, scene.render.resolution_y = 800, 800
scene.render.resolution_percentage = 100
mat = bpy.data.materials.new('Preview clay')
mat.diffuse_color = (0.23,0.32,0.39,1)
for obj in list(scene.objects):
    if obj.type == 'MESH':
        obj.data.materials.clear()
        obj.data.materials.append(mat)
bpy.ops.mesh.primitive_cube_add(size=1, location=(-.58,0,1))
wall = bpy.context.object
wall.name = 'Preview wall'
wall.scale = (.10, 3, 2.6)
wm = bpy.data.materials.new('Wall grey')
wm.diffuse_color = (.15,.16,.18,1)
wall.data.materials.append(wm)
bpy.ops.mesh.primitive_plane_add(size=200, location=(0,0,-.1))
bpy.ops.object.light_add(type='AREA', location=(2,-3,4))
bpy.context.object.data.energy=450
bpy.context.object.data.shape='DISK'
bpy.context.object.data.size=4
bpy.ops.object.light_add(type='AREA', location=(1,3,2))
bpy.context.object.data.energy=300
bpy.context.object.data.size=3
bpy.ops.object.camera_add(location=(3,-4,2.3))
cam=bpy.context.object
cam.rotation_euler=(Vector((0,0,.95))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO'
cam.data.ortho_scale=2.25
scene.camera=cam
scene.world.color=(.25,.25,.25)
for frame in [1,7,13]:
    scene.frame_set(frame)
    scene.render.filepath=str(root/f'ArtSource/WallRun/preview-{frame:02}.png')
    bpy.ops.render.render(write_still=True)
