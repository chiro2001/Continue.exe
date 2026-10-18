#!/usr/bin/env python3
"""Run the owned V4 integration and review checks sequentially via Pipeline."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parent.parent
REPORTS = Path.home() / 'tmp/vrc-old-web-map/episode123-v4/reports'


def command(arguments, timeout=120):
    process = subprocess.run([sys.executable, str(ROOT / 'tools/unity_remote.py'), '--check-result', '--', '--timeout', str(timeout), *arguments], capture_output=True, text=True)
    if process.returncode:
        raise RuntimeError(process.stdout + process.stderr)
    envelope = json.loads(process.stdout)
    result = envelope['data']['result']
    if isinstance(result, dict) and (result.get('success') is False or result.get('failed') is True):
        raise RuntimeError(json.dumps(result, ensure_ascii=False))
    return result


def script(name, method, timeout=120):
    entry = name + '.' + method
    print('Running ' + entry, flush=True)
    result = command(['run_script', '--file', 'AgentScripts/' + name + '.cs', '--entry', entry, '--timeout_ms', str((timeout-5)*1000)], timeout)
    REPORTS.mkdir(parents=True, exist_ok=True)
    (REPORTS / ('cli-' + entry + '.json')).write_text(json.dumps(result, ensure_ascii=False, indent=2))
    payload = result.get('result')
    if isinstance(payload, str):
        try:
            payload = json.loads(payload)
        except ValueError:
            pass
    if isinstance(payload, dict) and payload.get('success') is False:
        raise RuntimeError(entry + ': ' + json.dumps(payload, ensure_ascii=False))
    print('Passed ' + entry + ': ' + json.dumps(payload, ensure_ascii=False)[:600], flush=True)
    return payload


def play():
    try:
        command(['editor_play'])
        # Entering Play Mode may restart the local Pipeline server once.
        for attempt in range(20):
            time.sleep(1)
            try:
                status = command(['editor_status'])
                if status.get('playMode') == 'playing' and not status.get('compiling') and not status.get('domainReloadInProgress'):
                    break
            except RuntimeError:
                if attempt == 19:
                    raise
        else:
            raise RuntimeError('Editor did not finish entering Play Mode')
        # The first complete run exercises startup from the persisted preview.
        script('OWEpisodeV4Validation', 'LiveValidation', 200)
        script('OWEpisodeV4Validation', 'InteractionChecks')
        script('OWEpisodeV4StableSoundValidation', 'Runtime')
    finally:
        status = command(['editor_status'])
        if status.get('playMode') == 'playing':
            command(['editor_stop'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('phase', choices=['editor', 'play', 'save', 'build', 'movie'])
    parser.add_argument('--programs-ready', action='store_true', help='Use the just-verified compilation without compiling the same sources again')
    parser.add_argument('--resume-from', help='Resume the editor checks at this method after a diagnosed failure')
    options = parser.parse_args()
    phase = options.phase
    if phase == 'editor':
        resumed = options.resume_from is None
        for name, method in [
            ('OWEpisodeV4Build', 'RepairPrograms'), ('OWEpisodeV4Build', 'ActivateForBuild'),
            ('OWEpisodeV4Build', 'ImportAnonymizedImages'), ('OWEpisodeV4Montage', 'ApplyPresentationLayout'),
            ('OWEpisodeV4AudioFx', 'ConfigureStableSound'), ('OWEpisodeV4Build', 'FinalizeAndSave'),
            ('OWEpisodeV4Build', 'BeginReview'), ('OWEpisodeV4Montage', 'Validate'),
            ('OWEpisodeV4Montage', 'ValidatePresentationLayout'), ('OWEpisodeV4AudioFx', 'Validate'),
            ('OWEpisodeV4HubCast', 'ValidateNameplates'), ('OWEpisodeV4Build', 'KeylineVisibilityChecks'),
            ('OWEpisodeV4Build', 'TextAndSpaceChecks'), ('OWEpisodeV4Build', 'CaptureAll'),
            ('OWEpisodeV4Build', 'FinalizeAndSave'), ('OWEpisodeV4Build', 'ReopenCheck')]:
            if not resumed:
                if method != options.resume_from:
                    continue
                resumed = True
            if options.programs_ready and method == 'RepairPrograms':
                continue
            script(name, method, 180 if method == 'ValidatePresentationLayout' else 120)
    elif phase == 'play':
        play()
    elif phase == 'save':
        script('OWEpisodeV4Build', 'FinalizeAndSave')
        script('OWEpisodeV4Build', 'ReopenCheck')
        script('OWEpisodeV4StableSoundValidation', 'SavedScene')
    elif phase == 'build':
        script('OWEpisodeV4Build', 'SdkBuildOnly', 300)
        script('OWEpisodeV4Build', 'InspectBuiltBundle')
    elif phase == 'movie':
        script('OWEpisodeV4Build', 'StartMovie')
        while True:
            time.sleep(15)
            status = script('OWEpisodeV4Build', 'MovieStatus')
            if status.get('error'):
                raise RuntimeError(status['error'])
            if not status['running']:
                if status['frame'] != status['total']:
                    raise RuntimeError('Incomplete movie export')
                break


if __name__ == '__main__':
    main()
