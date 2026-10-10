"""Independent, Unity-free consumer check of final V2 evidence (no recipe synthesis)."""
import hashlib
import json
import math
from pathlib import Path
import struct
import sys


def verify(directory):
    root = Path(directory).resolve()
    packet = json.loads((root / 'manifest.json').read_text(encoding='utf-8'))
    assert packet['schemaVersion'] == 1 and packet['revision'] in ('vertical-contact-v2', 'combined-waterfall-v2-r1', 'natural-host-waterfall-v2-r1')

    def read(payload):
        path = (root / payload['file']).resolve()
        assert path.parent == root, 'Payload escapes packet directory'
        data = path.read_bytes()
        assert len(data) == payload['bytes'], 'Truncated ' + path.name
        assert hashlib.sha256(data).hexdigest() == payload['sha256'], 'Hash mismatch ' + path.name
        return data

    def vectors(payload, size):
        data = read(payload)
        assert len(data) % (size * 4) == 0
        result = list(struct.iter_unpack('<' + 'f' * size, data))
        assert all(math.isfinite(v) for row in result for v in row)
        return result

    n, h = packet['heightResolution'], packet['holeResolution']
    assert (n, h) == (513, 512)
    heights, holes, diagonals = (read(packet[k]) for k in ('heights', 'holes', 'diagonals'))
    assert len(heights) == n*n*2 and len(holes) == len(diagonals) == h*h
    assert holes.count(0) == 2400
    for hole, diagonal in zip(holes, diagonals):
        assert hole in (0, 1) and diagonal in ((0, 1) if hole else (255,))
    decoded = {}
    mesh_indices = {}
    for mesh in packet['meshes']:
        vertices = vectors(mesh['vertices'], 3)
        normals = vectors(mesh['normals'], 3)
        uv = vectors(mesh['uv'], 2)
        indices = [row[0] for row in struct.iter_unpack('<i', read(mesh['indices']))]
        assert len(vertices) == len(normals) == mesh['vertexCount']
        assert len(uv) == mesh['uvCount'] and len(indices) == mesh['indexCount']
        assert len(indices) % 3 == 0 and all(0 <= i < len(vertices) for i in indices)
        decoded[mesh['name']] = (mesh, vertices)
        mesh_indices[mesh['name']] = indices
    names = ['Upper reach to exact lip', 'Parametric fall, zero-run safe', 'Impact pool and receiving reach']
    for before, after in zip(names, names[1:]):
        a, av = decoded[before]; b, bv = decoded[after]
        assert len(a['endContact']) == len(b['startContact']) == 17
        for i, j in zip(a['endContact'], b['startContact']):
            assert math.dist(av[i], bv[j]) < 1e-6, 'Water boundary mismatch'
    assert len(packet['cameras']) == len(packet['controllerProbes']) == 3, 'Player measurements required'
    for camera in packet['cameras']:
        assert abs(camera['eye']['y'] - camera['ground']['y'] - 1.7) < 1e-5
        assert (camera['fov'], camera['width'], camera['height']) == (55, 1600, 1000)
    for probe in packet['controllerProbes']:
        assert probe['grounded'] and probe['start']['y'] - probe['end']['y'] > 2.5
        assert probe['steps'] == 140 and probe['fixedDeltaTime'] > 0
    decoded_bed_errors = []
    if packet['revision'] in ('combined-waterfall-v2-r1', 'natural-host-waterfall-v2-r1'):
        # Independent off-grid reconstruction, distinct from source collider cell-centre
        # agreement. Unity TerrainCollider and an imported triangle mesh can differ
        # slightly here; the import budget is 1mm, not the 4mm bed-design budget.
        height_codes = [v[0] for v in struct.iter_unpack('<H', heights)]
        origin, size = packet['terrainOrigin'], packet['terrainSize']

        def triangle_y(x, z, a, b, c):
            denominator = (b[2]-c[2])*(a[0]-c[0])+(c[0]-b[0])*(a[2]-c[2])
            if abs(denominator) < 1e-12:
                return None
            u = ((b[2]-c[2])*(x-c[0])+(c[0]-b[0])*(z-c[2]))/denominator
            v = ((c[2]-a[2])*(x-c[0])+(a[0]-c[0])*(z-c[2]))/denominator
            w = 1-u-v
            return u*a[1]+v*b[1]+w*c[1] if min(u, v, w) >= -1e-7 else None

        def decoded_bed(point):
            x, z = point['x'], point['z']
            gx = (x-origin['x'])/size['x']*h
            gz = (z-origin['z'])/size['z']*h
            ix, iz = math.floor(gx), math.floor(gz)
            assert 0 <= ix < h and 0 <= iz < h, 'Bed probe outside terrain'
            candidates = []
            if holes[iz*h+ix]:
                def vertex(dx, dz):
                    return (ix+dx, origin['y']+height_codes[(iz+dz)*n+ix+dx]/65535*size['y'], iz+dz)
                sw, nw, ne, se = vertex(0, 0), vertex(0, 1), vertex(1, 1), vertex(1, 0)
                triangles = ((sw, nw, ne), (sw, ne, se)) if diagonals[iz*h+ix] == 0 else ((sw, nw, se), (se, nw, ne))
                candidates.extend(y for tri in triangles if (y := triangle_y(gx, gz, *tri)) is not None)
            # Hole contacts use the exported solid, with no procedural recipe fallback.
            for name, (_, vertices) in decoded.items():
                if name in names:
                    continue
                indices = mesh_indices[name]
                for offset in range(0, len(indices), 3):
                    y = triangle_y(x, z, *(vertices[i] for i in indices[offset:offset+3]))
                    if y is not None:
                        candidates.append(y)
            assert candidates, 'No decoded solid beneath bed probe'
            return max(candidates)

        assert packet['horizontalRun'] == 0 and len(packet['bedProbes']) == 15
        assert packet['meshes'][0]['uvCount'] == packet['meshes'][0]['vertexCount']
        for probe in packet['bedProbes']:
            error = abs(decoded_bed(probe['physicalWater'])-probe['actualBedY'])
            assert error < .001, 'Decoded/source off-grid bed differs by >=1mm'
            decoded_bed_errors.append(error)
            assert abs(probe['actualBedY']-probe['intendedBedY']) < .004
            assert abs(probe['renderedWaterY']-probe['physicalWater']['y']-.01) < .00002
            assert probe['physicalWetDepth'] > 0
            if probe['lateral'] == 0:
                target = {'impact': 1.1, 'pool': 1.1, 'sill': .5, 'transition-end': .75, 'outflow': .75}[probe['name']]
                assert abs(probe['physicalWetDepth']-target) < .004
    return dict(case=packet['caseName'], files=19, holes=holes.count(0),
                maximumDecodedBedError=max(decoded_bed_errors, default=0),
                colliderCentreError=packet['maximumColliderCentreError'],
                manifestSha256=hashlib.sha256((root/'manifest.json').read_bytes()).hexdigest())


if __name__ == '__main__':
    if len(sys.argv) < 2:
        raise SystemExit('Usage: verify_vertical_packet.py <packet-directory> [...]')
    for directory in sys.argv[1:]:
        print(json.dumps(verify(directory), sort_keys=True))
