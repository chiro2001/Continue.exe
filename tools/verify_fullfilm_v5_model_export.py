#!/usr/bin/env python3
"""Compare static FBX objects and connections, ignoring export header timestamps."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import zlib


def digest(blob):
    return hashlib.sha256(blob).hexdigest()


def load(path):
    blob = Path(path).read_bytes()
    if blob[:23] != b'Kaydara FBX Binary  \x00\x1a\x00':
        raise ValueError('Binary FBX required')
    version = struct.unpack_from('<I', blob, 23)[0]
    width = 8 if version >= 7500 else 4
    header, null_size = ('<QQQB', 25) if width == 8 else ('<IIIB', 13)
    cursor = 27

    def prop():
        nonlocal cursor
        kind = chr(blob[cursor]); cursor += 1
        scalar = {'Y': 'h', 'C': '?', 'I': 'i', 'F': 'f', 'D': 'd', 'L': 'q'}
        if kind in scalar:
            fmt = '<' + scalar[kind]
            value = struct.unpack_from(fmt, blob, cursor)[0]
            cursor += struct.calcsize(fmt)
            return value
        if kind in ('S', 'R'):
            size = struct.unpack_from('<I', blob, cursor)[0]; cursor += 4
            value = blob[cursor:cursor + size]; cursor += size
            return value.decode('utf-8') if kind == 'S' else {'raw': value.hex()}
        if kind in 'fdilbc':
            count, encoding, size = struct.unpack_from('<III', blob, cursor); cursor += 12
            value = blob[cursor:cursor + size]; cursor += size
            if encoding == 1:
                value = zlib.decompress(value)
            elif encoding != 0:
                raise ValueError('Unsupported array encoding')
            return {'array': kind, 'count': count, 'sha256': digest(value)}
        raise ValueError('Unsupported FBX property: ' + kind)

    def node():
        nonlocal cursor
        end, count, property_bytes, name_size = struct.unpack_from(header, blob, cursor)
        cursor += null_size
        if end == 0:
            return None
        name = blob[cursor:cursor + name_size].decode('utf-8'); cursor += name_size
        properties = [prop() for _ in range(count)]
        children = []
        while cursor < end:
            child = node()
            if child is None:
                break
            children.append(child)
        if cursor != end:
            raise ValueError('FBX node boundary mismatch: ' + name)
        return dict(name=name, properties=properties, children=children)

    nodes = []
    while cursor < len(blob) - null_size:
        value = node()
        if value is None:
            break
        nodes.append(value)
    objects = next(value['children'] for value in nodes if value['name'] == 'Objects')
    mapping = {0: 'ROOT'}
    for value in objects:
        mapping[value['properties'][0]] = value['name'] + ':' + value['properties'][1]
    normalized = []
    for value in objects:
        normalized.append(dict(kind=value['name'], properties=value['properties'][1:], children=value['children']))
    normalized.sort(key=lambda value: (value['kind'], str(value['properties'])))
    connections = next(value['children'] for value in nodes if value['name'] == 'Connections')
    links = []
    for value in connections:
        row = value['properties'][:]
        for index in (1, 2):
            if index < len(row):
                row[index] = mapping.get(row[index], row[index])
        links.append(row)
    links.sort(key=lambda row: json.dumps(row, ensure_ascii=True))
    payload = dict(objects=normalized, connections=links)
    canonical = json.dumps(payload, ensure_ascii=True, sort_keys=True, separators=(',', ':')).encode()
    return dict(path=str(Path(path).resolve()), file_sha256=digest(blob), canonical_sha256=digest(canonical),
                object_count=len(objects), connection_count=len(links), version=version), payload


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('first'); parser.add_argument('second'); parser.add_argument('--report', required=True)
    options = parser.parse_args()
    first, a = load(options.first); second, b = load(options.second)
    equal = a == b
    result = dict(success=equal, first=first, second=second, static_objects_and_connections_identical=equal,
                  scope='All static FBX Objects data, including vertices, normals, UVs, materials, model transforms and normalized connections; export headers are excluded')
    Path(options.report).write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(result, ensure_ascii=False, indent=2))
    if not equal:
        for suffix, payload in (('first', a), ('second', b)):
            Path(options.report + '.' + suffix + '.json').write_text(json.dumps(payload, ensure_ascii=True, indent=2))
        raise SystemExit(1)


if __name__ == '__main__':
    main()
