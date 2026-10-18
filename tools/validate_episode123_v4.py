#!/usr/bin/env python3
"""Audit review-critical source, score, note and chronology invariants for V4."""
from collections import Counter, defaultdict
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = Path.home() / 'tmp/vrc-old-web-map/episode123-v4/reports'


def read(name):
    return json.loads((ROOT / 'data' / name).read_text())


def main():
    score = read('episode123-v4-score.json')
    audit = read('episode123-v4-material-audit.json')
    chronology = read('episode123-v4-chronology-audit.json')
    operations = read('episode123-v4-operations.json')
    images = {r['id']: r for r in score['images']}
    assert len(images) == len(score['images']), 'Duplicate material IDs'
    signatures = set()
    anonymous_copies = 0
    anonymous_regions = 0
    for image in images.values():
        path = Path(image['source_path'])
        assert path.is_file(), path
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        assert digest == image['sha256'], f'Stale material: {path}'
        assert digest not in signatures, f'Duplicate content: {path}'
        signatures.add(digest)
        if image.get('display_source_path'):
            display = Path(image['display_source_path'])
            assert display.is_file() and hashlib.sha256(display.read_bytes()).hexdigest() == image['display_sha256']
            assert image['display_path'].startswith('Assets/OldWebMap/Episode123/Revision4/Images/')
            anonymous_copies += 1
            anonymous_regions += len(image['anonymous_regions'])
    notes = {}
    with (ROOT / 'audio/keil-keygen-2008/tracks.csv').open() as stream:
        for row in csv.DictReader(stream):
            key = (round(float(row['time_s']), 5), int(row['channel']) - 1)
            notes[key] = row
    slots = defaultdict(list)
    focus = []
    note_hits = 0
    chronology_counts = Counter()
    for event in score['events']:
        image = images[event['image']]
        assert 0 <= event['slot'] < score['slots']
        assert 0 <= event['start'] < event['end'] <= score['duration']
        assert event['placement_mode'] == image['placement_mode']
        assert event['enter'] + event['exit'] < event['end'] - event['start']
        expected_region = 0 if event['start'] < 79.36 else 1 if event['start'] < 86.4 else 2 if event['start'] < 102.4 else 3
        assert event['region'] == expected_region
        assert event['to'][2] >= [0, 24, 52, 80][expected_region]
        if image['placement_mode'] == 'archive-recall':
            assert event['start'] >= 122.88 - 1e-4, image['id']
        if image['kind'] == 'current-project-self-capture':
            assert event['start'] >= 128 and image['display_year'] == '现在'
        if image['placement_mode'] == 'dated-recall':
            assert event['start'] >= 105 and image['display_year']
        if event['role'] == 'focus':
            assert event['slot'] == 0 and event['hold'] >= 2.4 - 1e-4
            assert abs(event['enter'] + event['hold'] + event['exit'] - (event['end'] - event['start'])) < 1e-4
            for group in operations['groups']:
                assert event['end'] <= group['start'] or event['start'] >= group['end'], 'Focus obscures a continuous operation'
            focus.append(event)
        else:
            assert event['slot'] != 0
        trigger = event['trigger']
        if trigger['type'] == 'actual-note':
            key = (round(event['start'], 5), event['channel'])
            assert key in notes, f'Claimed note is absent from original track: {key}'
            row = notes[key]
            for field in ['row', 'pattern', 'instrument']:
                assert int(row[field]) == trigger[field]
            assert abs(event['start'] - trigger['time']) < 1e-5
            note_hits += 1
        else:
            assert trigger['type'] in ['phrase-boundary', 'score-row']
        slots[event['slot']].append(event)
        chronology_counts[image['placement_mode']] += 1
    for slot, events in slots.items():
        events.sort(key=lambda r: r['start'])
        for left, right in zip(events, events[1:]):
            assert left['end'] <= right['start'] + 1e-5, f'Overlapping events in slot {slot}'
    used = {r['image'] for r in score['events']}
    assert used == set(images), 'Unused claimed content'
    assert all(r['image'] in used for r in score['ambient'])
    first = {key: min(e['start'] for e in score['events'] if e['image'] == key) for key in images}
    assert all(r['reveal'] >= first[r['image']] for r in score['ambient']), 'Retained window precedes its first material'
    forbidden = ['Agent', '审核', '本项目原创', '风格复刻', '示意', '原图拆层', '留出阅读时间', '会遮住画面吗']
    for row in score['comments'] + score['keylines']:
        assert not any(word in row['text'] for word in forbidden), row
    assert min(r['time'] for r in score['comments']) >= 53
    old_ids = {r['id'] for r in read('episode123-v3-score.json')['images']}
    assert old_ids == {r['id'] for r in chronology['items']}, 'Missing baseline chronology audit'
    assert len(images) == audit['images'] == audit['unique_images_used']
    assert len(score['events']) == audit['events'] and len(focus) == audit['focus_anchors']
    assert note_hits == audit['actual_note_events']
    result = dict(success=True, images=len(images), unique_used_assets=len(used),
                  events=len(score['events']), focus_anchors=len(focus),
                  focus_hold_range=[min(r['hold'] for r in focus), max(r['hold'] for r in focus)],
                  actual_note_events=note_hits, phrase_boundary_events=len(score['events'])-note_hits,
                  source_sha256_checked=len(images), chronology_items=len(chronology['items']),
                  anonymous_display_copies=anonymous_copies, anonymous_regions=anonymous_regions,
                  chronology_event_modes=dict(chronology_counts),
                  ambient=len(score['ambient']), comments=len(score['comments']),
                  first_comment=min(r['time'] for r in score['comments']), keylines=len(score['keylines']),
                  kinds=dict(Counter(r['kind'] for r in images.values())),
                  limitations='Data/source audit only. Does not replace live Udon, visual, HMD or multiplayer verification.')
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'static-data-validation.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
