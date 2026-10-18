#!/usr/bin/env python3
"""Temporarily mute workstation endpoints and restore their recorded mute flags."""
import argparse
import json
import subprocess
from pathlib import Path
from mute_unity_workstation import CS
from unity_remote import run_pwsh,DEFAULT_HOST

STATE=Path.home()/'tmp/vrc-old-web-map/fullfilm-v3/reports/output-mute-state.json'

def run(apply,flags):
    cs=CS.replace('Run(bool apply)','Run(bool apply, bool[] targets)').replace('SetMute(true,Guid.Empty)','SetMute(targets[role],Guid.Empty)')
    values=','.join('$true' if x else '$false' for x in flags)
    script="Add-Type -TypeDefinition @'\n"+cs+"\n'@\n[OWMQuietOutput]::Run("+('$true' if apply else '$false')+",[bool[]]@("+values+"))"
    local=STATE.parent.parent/'quiet-output-command.ps1';local.write_text(script,encoding='utf-8-sig')
    remote='C:/Users/Chiro/tmp/vrc-old-web-map/fullfilm-v3/quiet-output-command.ps1'
    subprocess.run(['scp','-q',str(local),DEFAULT_HOST+':'+remote],check=True)
    result=run_pwsh("& '"+remote+"'",capture_output=True)
    if result.returncode:raise RuntimeError(result.stderr.decode('utf-8','replace'))
    return json.loads(result.stdout.decode('utf-8-sig'))

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('action',choices=['mute','restore','check']);a=p.parse_args();STATE.parent.mkdir(parents=True,exist_ok=True)
    if a.action=='check':print(json.dumps(run(False,[False]*3)));return
    if a.action=='mute':
        if STATE.exists() and json.loads(STATE.read_text()).get('active'):raise RuntimeError('A mute baseline already exists; restore it before replacing')
        baseline=run(False,[False]*3);record={'active':True,'baseline':baseline,'applied':run(True,[True]*3)};STATE.write_text(json.dumps(record,indent=2)+'\n');print(json.dumps(record['applied']));return
    record=json.loads(STATE.read_text());flags=[False]*3
    for row in record['baseline']['endpoints']:
        if 'muted' in row:flags[row['role']]=row['muted']
    record['restored']=run(True,flags);record['active']=False;STATE.write_text(json.dumps(record,indent=2)+'\n');print(json.dumps(record['restored']))

if __name__=='__main__':main()
