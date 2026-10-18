#!/usr/bin/env python3
"""Offline mirror of OWEpisodeV5Cast.Apply/Validate for data/episode123-v5-cast.json.

Rebuilds the V5 OWV4HubCast tables from the cached V4 table plus the authored edits, replays
OWV4HubCast.SampleActors (positions, gestures) every 0.16 s and checks the same rules as the
Unity builder: viewer clearance, near-field moments, speed, keyline band, gestures, gates and
actor separation. Runs without Unity. Report: ~/tmp/vrc-old-web-map/episode123-v5/reports/cast-check.json
"""
import json, math, os, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / 'data/episode123-v5-cast.json'
OUT = Path(os.path.expanduser('~/tmp/vrc-old-web-map/episode123-v5/reports'))
EYE = (0.0, 1.35, -2.8)


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


def gaze_rule(time, i, p, g):
    if 87.04 <= time < 102.4: gaze = (0, 1.1, 5.0)
    elif 81.92 <= time < 85.76: gaze = (-5, 1.2, 8)
    elif 85.76 <= time < 87.04: gaze = (5, 1.4, 6.2)
    else: gaze = (-4.9 if i % 2 == 0 else 4.9, .95, 4.2)
    if g in (3, 7): gaze = (2.7 if i % 2 == 0 else -2.7, 1.0, p[2])
    if 89.60 <= time < 102.4: gaze = (1.5 if i % 2 == 0 else -1.5, 1.0, 6.6 if i < 2 else 4.1)
    if time >= 102.4:
        gaze = (4.8, 1.0, 6.0) if i == 3 else (p[0], 1.15, 6.0 if time < 117.76 else 10.2)
    if time >= 122.88:
        gaze = (4.25, 1.04, 5.9) if i == 3 else (4.72, .85, 4.65)
    return gaze


def yaw_of(p, gaze):
    return math.degrees(math.atan2(gaze[0] - p[0], gaze[2] - p[2]))


def v4_table(doc):
    """V4 base rows: rows[k][slot] = dict(p=(x,y,z), g, gaze, yaw)."""
    v4 = doc['v4']; kt = v4['keyTimes']; lift = v4['hallLift']
    rows = []
    for k, t in enumerate(kt):
        row = []
        for s in range(4):
            sl = v4['slots'][s]
            y = lift['y'] if s in lift['slots'] and lift['from'] <= t <= lift['to'] else 0.0
            p = (sl['x'][k], y, sl['z'][k]); g = sl['g'][k]
            gz = gaze_rule(t, s, p, g)
            row.append(dict(p=p, g=g, gaze=gz, yaw=yaw_of(p, gz)))
        rows.append(row)
    return kt, rows


def derive_v5(doc):
    """V5 tables = V4 rows + habit gestures (V4 keys) + inserted hold keys + path edits."""
    kt4, base = v4_table(doc)
    base = [[dict(r) for r in row] for row in base]
    idx4 = {round(t, 4): i for i, t in enumerate(kt4)}
    for hb in doc['habits']:
        for ed in hb['edits']:
            j = idx4.get(round(ed['t'], 4))
            if j is None: raise SystemExit('habit edit time is not a V4 key: %s' % ed['t'])
            r = base[j][hb['slot']]
            if r['g'] != ed['was']: raise SystemExit('habit edit %s slot %d expected V4 gesture %d, found %d' % (ed['t'], hb['slot'], ed['was'], r['g']))
            r['g'] = ed['g']
    kt = sorted(set(kt4) | set(doc['newKeys']))
    edited = set((round(ed['t'], 4), pa['slot']) for pa in doc['paths'] for ed in pa['edits'])
    rows, notes = [], []
    for t in kt:
        j = idx4.get(round(t, 4))
        if j is not None:
            rows.append([dict(r) for r in base[j]]); continue
        k = max(i for i in range(len(kt4)) if kt4[i] < t)  # inserted key: hold of the previous key, per slot
        rows.append([dict(r) for r in base[k]])
        for s in range(4):
            if (round(t, 4), s) in edited: continue
            a_, b_ = base[k][s], base[k + 1][s]
            notes.append(dict(t=t, slot=s, holds=a_['p'] == b_['p'] and a_['gaze'] == b_['gaze'] and abs(a_['yaw'] - b_['yaw']) < 1e-6, gestureShift=a_['g'] != b_['g']))
    index = {round(t, 4): i for i, t in enumerate(kt)}
    for pa in doc['paths']:
        slot = pa['slot']
        for ed in pa['edits']:
            j = index.get(round(ed['t'], 4))
            if j is None: raise SystemExit('edit time is not a key: %s' % ed['t'])
            r = rows[j][slot]; moved = False
            if 'x' in ed or 'z' in ed:
                p = r['p']; r['p'] = (ed.get('x', p[0]), ed.get('y', p[1]), ed.get('z', p[2])); moved = True
            if ed.get('g') is not None: r['g'] = ed['g']
            gz = ed.get('gaze')
            if gz == 'viewer': r['gaze'] = EYE
            elif isinstance(gz, list): r['gaze'] = tuple(gz)
            elif gz == 'rule' or (gz is None and moved): r['gaze'] = gaze_rule(kt[j], slot, r['p'], r['g'])
            else: gz = None
            if gz is not None or moved: r['yaw'] = yaw_of(r['p'], r['gaze'])
    pos, yaw, ges, gaze = [], [], [], []
    for r in rows:
        for s in range(4):
            pos.append(r[s]['p']); yaw.append(r[s]['yaw']); ges.append(r[s]['g']); gaze.append(r[s]['gaze'])
    return kt, pos, yaw, ges, gaze, notes


def region_offset(r): return (0, 24, 52, 80)[r]


def virtual_z(t):
    return 24 * smooth((t - 79.36) / 2.56) + 28 * smooth((t - 86.40) / 1.92) + 28 * smooth((t - 102.40) / 2.56)


def sample(kt, pos, ges, time, n=4):
    key = 0
    for k in range(len(kt) - 1):
        if time >= kt[k]: key = k
    nxt = min(key + 1, len(kt) - 1)
    span = max(.01, kt[nxt] - kt[key])
    part = min(1, max(0, (time - kt[key]) / span))
    move = smooth(part / .72)
    out = []
    for i in range(n):
        a = key * n + i; b = nxt * n + i
        d = [pos[b][j] - pos[a][j] for j in range(3)]
        walking = (d[0] ** 2 + d[1] ** 2 + d[2] ** 2) > .06 and part < .72
        p = [pos[a][j] + d[j] * move for j in range(3)]
        if walking: p[1] += math.sin(part * span * 9) * .009
        out.append(dict(p=p, g=6 if walking else ges[a if part < .72 else b], walking=walking, key=key))
    return out


def band_hit(p, t, lim, band):
    m = lim['bandMargin']
    vz = virtual_z(t)
    for r in range(4):
        if abs(band['regionZ'][r] - vz) > .25: continue  # band is travelling with the journey; only its rest position is occupied space
        z0 = band['zMin'] + band['regionZ'][r] - vz; z1 = band['zMax'] + band['regionZ'][r] - vz
        if abs(p[0]) < band['halfWidth'] + m and z0 - m < p[2] < z1 + m and p[1] < band['yMax']: return r
    return -1


def keepout_hit(p, t, doc):
    r = doc['limits']['bodyRadius']
    for ko in doc.get('keepOut', []):
        if not ko['from'] <= t < ko['to']: continue
        if 'rect' in ko:
            x0, z0, x1, z1 = ko['rect']
            dx = max(x0 - p[0], 0, p[0] - x1); dz = max(z0 - p[2], 0, p[2] - z1)
            if math.hypot(dx, dz) < r - 1e-6: return ko['name']
        else:
            cx, cz, cr = ko['circle']
            if math.hypot(p[0] - cx, p[2] - cz) < cr + r: return ko['name']
    return None


def run(doc, tables, base=None):
    """Replay one table; `base` (V4 table) marks authored samples = positions that differ from V4."""
    kt, pos, yaw, ges, gaze = tables[:5]
    lim = doc['limits']; band = doc['band']
    step = lim['sampleStep']; n = int(round(133.12 / step)) + 1
    times = [min(i * step, 133.12) for i in range(n)]
    prev = None
    res = dict(minViewer=9, minViewerAt=None, maxSpeed=0, maxAuthoredSpeed=0, band=[], keepOut=[], badGesture=[], sep=9, sepAt=None, authoredSep=9, authoredSepAt=None, cuts=[], fast=[], authoredFast=[])
    blocked = True
    rows = []
    for ti, t in enumerate(times):
        s = sample(kt, pos, ges, t)
        b = sample(base[0], base[1], base[3], t) if base else None
        rows.append(s)
        auth = [False] * 4
        for i, a in enumerate(s):
            p = a['p']
            d = math.hypot(p[0], p[2] + 2.8)
            if d < res['minViewer']: res['minViewer'] = d; res['minViewerAt'] = (round(t, 2), i)
            if not 0 <= a['g'] <= 11: res['badGesture'].append((t, i, a['g']))
            r = band_hit(p, t, lim, band)
            if r >= 0: res['band'].append((round(t, 2), i, r, [round(c, 2) for c in p]))
            if b is not None: auth[i] = math.hypot(p[0] - b[i]['p'][0], p[2] - b[i]['p'][2]) > .01
            k = keepout_hit(p, t, doc)
            if k and auth[i]: res['keepOut'].append((round(t, 2), i, k))  # V4 motion is inherited as is
            if prev:
                q = prev[i]['p']; sp = math.hypot(p[0] - q[0], p[2] - q[2]) / step
                res['maxSpeed'] = max(res['maxSpeed'], sp)
                if auth[i] or prev_auth[i]: res['maxAuthoredSpeed'] = max(res['maxAuthoredSpeed'], sp)
                if sp > lim['speedMax']:
                    res['fast'].append((round(t, 2), i, round(sp, 2)))
                    if auth[i] or prev_auth[i]: res['authoredFast'].append((round(t, 2), i, round(sp, 2)))
                if sp * step > lim['cutStep']: res['cuts'].append((round(t, 2), i))
        for i in range(4):
            for j in range(i + 1, 4):
                dd = math.hypot(s[i]['p'][0] - s[j]['p'][0], s[i]['p'][2] - s[j]['p'][2])
                if dd < res['sep']: res['sep'] = dd; res['sepAt'] = (round(t, 2), i, j)
                if (auth[i] or auth[j]) and dd < res['authoredSep']: res['authoredSep'] = dd; res['authoredSepAt'] = (round(t, 2), i, j)
        if 107.52 <= t < 117.76: blocked &= s[2]['p'][2] < 5.6
        if 112.64 <= t < 120.32: blocked &= s[2]['p'][2] < 9.6
        if 117.76 <= t < 122.88: blocked &= s[3]['p'][2] < 5.6
        prev = s; prev_auth = auth
    res['gatesBlocked'] = blocked
    res['fastSegments'] = sorted(set((int(f[0]), f[1]) for f in res['fast']))
    moments = []
    for m in doc['moments']:
        w0, w1 = m['window']; sl = m['slot']
        dist = [(t, math.hypot(rows[i][sl]['p'][0], rows[i][sl]['p'][2] + 2.8)) for i, t in enumerate(times)]
        inside = [d for t, d in dist if w0 <= t <= w1]
        spans = []; start = None; last_t = None
        for t, d in dist:
            if d <= lim['nearMax'] + .05 and start is None: start = t
            if d > lim['nearMax'] + .05 and start is not None: spans.append((round(start, 2), round(last_t, 2))); start = None
            last_t = t
        if start is not None: spans.append((round(start, 2), round(times[-1], 2)))
        span = next((sp for sp in spans if sp[1] >= w0 and sp[0] <= w1), None)
        overlap = 0 if span is None else max(0, min(span[1], w1) - max(span[0], w0))
        gestures = sorted(set(rows[i][sl]['g'] for i, t in enumerate(times) if w0 <= t <= w1))
        moments.append(dict(id=m['id'], slot=sl, window=m['window'], minDistance=min(inside) if inside else None, nearSpan=span, overlap=round(overlap, 2),
                            windowCover=round(overlap / (w1 - w0), 3), gestures=gestures))
    res['moments'] = moments
    return res


def main():
    doc = json.loads(DATA.read_text())
    tables = derive_v5(doc)
    kt4, base = v4_table(doc)
    v4 = ([*kt4], [r[s]['p'] for r in base for s in range(4)], [r[s]['yaw'] for r in base for s in range(4)], [r[s]['g'] for r in base for s in range(4)], [r[s]['gaze'] for r in base for s in range(4)])
    r5 = run(doc, tables, v4); r4 = run(doc, v4)
    lim = doc['limits']
    fails = []
    if r5['minViewer'] < lim['viewerClearance']: fails.append('actor within %.2f m of the viewer column' % r5['minViewer'])
    for m in r5['moments']:
        if m['minDistance'] is None or not (lim['nearMin'] <= m['minDistance'] <= lim['nearMax']): fails.append('moment %s min distance %s' % (m['id'], m['minDistance']))
        if m['windowCover'] < .95: fails.append('moment %s covers %.0f%% of its window' % (m['id'], m['windowCover'] * 100))
        need = next(x for x in doc['moments'] if x['id'] == m['id']).get('need', [])
        if any(g not in m['gestures'] for g in need): fails.append('moment %s lacks gestures %s (has %s)' % (m['id'], need, m['gestures']))
    if r5['band']: fails.append('band hits: %s' % r5['band'][:5])
    if r5['badGesture']: fails.append('bad gesture ids')
    if not r5['gatesBlocked']: fails.append('gate blocking broken')
    if r5['cuts']: fails.append('cuts %s' % r5['cuts'][:5])
    if r5['authoredFast']: fails.append('authored motion above %.1f m/s: %s' % (lim['speedMax'], r5['authoredFast'][:5]))
    if r5['keepOut']: fails.append('keep-out hits: %s' % r5['keepOut'][:5])
    if r5['authoredSep'] < lim['actorSeparationMin']: fails.append('authored motion within %.2f m of another actor at %s' % (r5['authoredSep'], r5['authoredSepAt']))
    bad_new = [x for x in tables[5] if not x['holds']]
    if bad_new: fails.append('inserted key breaks a moving slot: %s' % bad_new[:3])
    res = dict(success=not fails, fails=fails, keys=len(tables[0]), v4Keys=len(kt4), moments=r5['moments'],
               v5=dict(minViewer=r5['minViewer'], maxSpeed=r5['maxSpeed'], maxAuthoredSpeed=r5['maxAuthoredSpeed'], inheritedFastSegments=r5['fastSegments'], sep=r5['sep'], sepAt=r5['sepAt'], authoredSep=r5['authoredSep'], authoredSepAt=r5['authoredSepAt']),
               v4=dict(minViewer=r4['minViewer'], maxSpeed=r4['maxSpeed'], fastSegments=r4['fastSegments'], sep=r4['sep'], sepAt=r4['sepAt']),
               gestureShift=[x for x in tables[5] if x['gestureShift']])
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'cast-check.json').write_text(json.dumps(res, ensure_ascii=False, indent=1))
    print('time  slot  min-dist  gestures  near-span  window-cover')
    for m in r5['moments']:
        print('%-3s  slot%d  %.2f m   %s   %s   %.0f%%' % (m['id'], m['slot'], m['minDistance'], m['gestures'], m['nearSpan'], m['windowCover'] * 100))
    print('V5 min viewer %.2f m; authored motion peak %.2f m/s (limit %.1f); inherited V4 sprints up to %.2f m/s in %d segments (V4 had %d); min actor separation %.2f (V4 %.2f), authored %.2f' % (
        r5['minViewer'], r5['maxAuthoredSpeed'], lim['speedMax'], r5['maxSpeed'], len(r5['fastSegments']), len(r4['fastSegments']), r5['sep'], r4['sep'], r5['authoredSep']))
    print('success' if res['success'] else 'FAIL: ' + '; '.join(fails))
    return 0 if res['success'] else 1


if __name__ == '__main__':
    sys.exit(main())
