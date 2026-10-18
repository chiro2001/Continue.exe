#!/usr/bin/env python3
"""Run dependent full-film Pipeline entries in order and preserve exact envelopes."""
import argparse
import json
from pathlib import Path
import subprocess
import sys

ROOT=Path(__file__).resolve().parent.parent
LOG=Path.home()/'tmp/vrc-old-web-map/fullfilm-v1/reports'

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('entries',nargs='+')
    p.add_argument('--args-json')
    args=p.parse_args();LOG.mkdir(parents=True,exist_ok=True)
    for entry in args.entries:
        cls=entry.split('.')[0]
        revision='v4' if cls.startswith('OWFilmV4') else 'v3' if cls.startswith('OWV3') else 'v2' if cls.startswith('OWV2') else 'v1'
        entry_log=Path.home()/f'tmp/vrc-old-web-map/fullfilm-{revision}/reports'
        entry_log.mkdir(parents=True,exist_ok=True)
        slow=entry.endswith('.Live') or '.SdkBuild' in entry
        outer_timeout=660 if ".SdkBuild" in entry else 330 if slow else 120
        inner_timeout=640000 if ".SdkBuild" in entry else 310000 if slow else 115000
        cmd=[sys.executable,str(ROOT/'tools/unity_remote.py'),'--check-result','--','--timeout',str(outer_timeout),'run_script','--file',f'AgentScripts/{cls}.cs','--entry',entry,'--timeout_ms',str(inner_timeout)]
        if args.args_json:cmd+=['--args',args.args_json]
        print('Running',entry,flush=True)
        r=subprocess.run(cmd,capture_output=True,timeout=outer_timeout+60)
        (entry_log/('cli-'+entry+'.json')).write_bytes(r.stdout)
        if r.returncode:
            print(r.stdout.decode('utf-8','replace')[-7000:]);print(r.stderr.decode('utf-8','replace')[-2500:]);return r.returncode
        envelope=json.loads(r.stdout);result=envelope.get('data',{}).get('result',{})
        if result.get('success') is False:print(json.dumps(result,ensure_ascii=False));return 6
        out=result.get('result')
        if isinstance(out,str):
            try:out=json.loads(out)
            except ValueError:pass
        if isinstance(out,dict):
            if out.get('success') is False:print(json.dumps(out,ensure_ascii=False));return 6
            out={k:v for k,v in out.items() if k not in ['rows','checks','positions','scenes','newMaterials','journeys']}
        print(json.dumps(out,ensure_ascii=False)[:3000],flush=True)
    return 0

if __name__=='__main__':raise SystemExit(main())
