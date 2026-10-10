"""Identify exported mesh faces at a captured pixel (terrain occlusion excluded)."""
import argparse
import json
import math
from pathlib import Path
import struct
from verify_vertical_packet import verify

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('directory')
parser.add_argument('--camera', default='lip')
parser.add_argument('--pixel', type=int, nargs=2, action='append', required=True)
args = parser.parse_args()
root = Path(args.directory)
verify(root)
packet = json.loads((root/'manifest.json').read_text())
camera = next(c for c in packet['cameras'] if c['name'] == args.camera)


def vec(d): return tuple(d[k] for k in ('x', 'y', 'z'))
def sub(a, b): return tuple(x-y for x, y in zip(a, b))
def add(a, b): return tuple(x+y for x, y in zip(a, b))
def mul(a, s): return tuple(x*s for x in a)
def dot(a, b): return sum(x*y for x, y in zip(a, b))
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


q, qw, eye = vec(camera['rotation']), camera['rotation']['w'], vec(camera['eye'])
meshes = []
for mesh in packet['meshes']:
    vertices = list(struct.iter_unpack('<fff', (root/mesh['vertices']['file']).read_bytes()))
    faces = list(struct.iter_unpack('<iii', (root/mesh['indices']['file']).read_bytes()))
    meshes.append((mesh, vertices, faces))
for px, py in args.pixel:
    tangent = math.tan(math.radians(camera['fov'])*.5)
    local = ((2*(px+.5)/camera['width']-1)*tangent*camera['width']/camera['height'],
             (1-2*(py+.5)/camera['height'])*tangent, 1)
    ray = add(local, add(mul(cross(q, local), 2*qw), mul(cross(q, cross(q, local)), 2)))
    hits = []
    for mesh, vertices, faces in meshes:
        for face, ids in enumerate(faces):
            a, b, c = [vertices[i] for i in ids]
            e1, e2 = sub(b, a), sub(c, a)
            h = cross(ray, e2); determinant = dot(e1, h)
            if abs(determinant) < 1e-10: continue
            s = sub(eye, a); u = dot(s, h)/determinant
            if u < 0 or u > 1: continue
            z = cross(s, e1); v = dot(ray, z)/determinant
            if v < 0 or u+v > 1: continue
            distance = dot(e2, z)/determinant
            if distance <= 0: continue
            hits.append(dict(distance=distance, mesh=mesh['name'], triangle=face, indices=ids,
                             point=add(eye, mul(ray, distance)), vertices=[a, b, c],
                             normal=cross(e1, e2), backFacing=determinant < 0))
    hits.sort(key=lambda hit: hit['distance'])
    upper_y = packet['lip']['y']+packet['renderOffset']['y']
    crossing = add(eye, mul(ray, (upper_y-eye[1])/ray[1])) if abs(ray[1]) > 1e-12 else None
    print(json.dumps(dict(pixel=[px, py], nearest=hits[:2], upperPlaneCrossing=crossing)))
