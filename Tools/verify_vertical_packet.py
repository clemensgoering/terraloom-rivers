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
    assert packet['schemaVersion'] == 1 and packet['revision'] in ('vertical-contact-v2', 'combined-waterfall-v2-r1')

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
    for mesh in packet['meshes']:
        vertices = vectors(mesh['vertices'], 3)
        normals = vectors(mesh['normals'], 3)
        uv = vectors(mesh['uv'], 2)
        indices = [row[0] for row in struct.iter_unpack('<i', read(mesh['indices']))]
        assert len(vertices) == len(normals) == mesh['vertexCount']
        assert len(uv) == mesh['uvCount'] and len(indices) == mesh['indexCount']
        assert len(indices) % 3 == 0 and all(0 <= i < len(vertices) for i in indices)
        decoded[mesh['name']] = (mesh, vertices)
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
    if packet['revision'] == 'combined-waterfall-v2-r1':
        assert packet['horizontalRun'] == 0 and len(packet['bedProbes']) == 15
        assert packet['meshes'][0]['uvCount'] == packet['meshes'][0]['vertexCount']
        for probe in packet['bedProbes']:
            assert abs(probe['actualBedY']-probe['intendedBedY']) < .004
            assert abs(probe['renderedWaterY']-probe['physicalWater']['y']-.01) < .00002
            assert probe['physicalWetDepth'] > 0
            if probe['lateral'] == 0:
                target = {'impact': 1.1, 'pool': 1.1, 'sill': .5, 'transition-end': .75, 'outflow': .75}[probe['name']]
                assert abs(probe['physicalWetDepth']-target) < .004
    return dict(case=packet['caseName'], files=19, holes=holes.count(0),
                colliderCentreError=packet['maximumColliderCentreError'],
                manifestSha256=hashlib.sha256((root/'manifest.json').read_bytes()).hexdigest())


if __name__ == '__main__':
    if len(sys.argv) < 2:
        raise SystemExit('Usage: verify_vertical_packet.py <packet-directory> [...]')
    for directory in sys.argv[1:]:
        print(json.dumps(verify(directory), sort_keys=True))
