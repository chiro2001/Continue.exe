#!/usr/bin/env python3
"""Copy the completed review to the user's Windows tmp and verify every file."""
import hashlib
import json
from pathlib import Path
import subprocess
from html.parser import HTMLParser
from urllib.parse import unquote,urlsplit
from unity_remote import run_pwsh,ssh_control_args,pwsh_literal

TMP=Path.home()/'tmp/vrc-old-web-map/fullfilm-v4'
OUT=TMP/'review'
WINDOWS='C:/Users/Chiro/tmp/vrc-old-web-map/fullfilm-v4'

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

class Page(HTMLParser):
    def __init__(self):super().__init__();self.links=[];self.video=[]
    def handle_starttag(self,tag,attrs):
        values=dict(attrs)
        for k in ['href','src']:
            if k in values:self.links.append(values[k])
        if tag=='video':self.video.append(values)

def main():
    package=json.loads((OUT/'package.json').read_text());page=Page();page.feed((OUT/'index.html').read_text())
    if len(page.video)!=1 or 'muted' not in page.video[0] or 'autoplay' in page.video[0]:raise ValueError('Review must be muted and not autoplay')
    links=set()
    for value in page.links:
        parsed=urlsplit(value)
        if parsed.scheme or not parsed.path:continue
        p=OUT/unquote(parsed.path)
        if not p.is_file():raise ValueError('Broken local review link: '+value)
        links.add(str(p.relative_to(OUT)))
    for row in package['artifacts']:
        p=OUT/row['file']
        if not p.is_file() or p.stat().st_size!=row['bytes'] or sha(p)!=row['sha256']:raise ValueError('Local artifact changed: '+row['file'])
    subprocess.run(['scp',*ssh_control_args(),'-q','-r',str(OUT),'gpu5080:'+WINDOWS+'/'],check=True)
    script='''$reviewRoot='''+pwsh_literal(WINDOWS+'/review')+'''
$packageFile=Join-Path $reviewRoot 'package.json'
$reviewPackage=Get-Content -LiteralPath $packageFile -Raw | ConvertFrom-Json
$verified=0
foreach($item in $reviewPackage.artifacts){
  $target=Join-Path $reviewRoot $item.file
  if(-not(Test-Path -LiteralPath $target -PathType Leaf)){throw ('Missing review file: '+$item.file)}
  if((Get-Item -LiteralPath $target).Length -ne $item.bytes){throw ('Review file size: '+$item.file)}
  if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $item.sha256){throw ('Review file hash: '+$item.file)}
  $verified++
}
[pscustomobject]@{success=$true;artifactsVerified=$verified;packageSha256=(Get-FileHash -LiteralPath $packageFile -Algorithm SHA256).Hash.ToLowerInvariant();review=(Join-Path $reviewRoot 'index.html');played=$false} | ConvertTo-Json -Compress
'''
    r=run_pwsh(script,capture_output=True)
    if r.returncode:raise RuntimeError(r.stderr.decode('utf-8','replace'))
    result=json.loads(r.stdout.decode('utf-8-sig'));result['localLinksVerified']=len(links)
    if result['packageSha256']!=sha(OUT/'package.json'):raise ValueError('Windows package manifest differs')
    (TMP/'reports/windows-delivery.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    delivery=json.loads((OUT/'package.json').read_text());delivery.update(windows_artifacts_verified=result['artifactsVerified'],local_links_verified=len(links),windows_package_sha256=result['packageSha256'])
    state=json.loads((TMP/'reports/output-mute-state.json').read_text());delivery['windows_original_output_state_restored']=state.get('active') is False
    (Path(__file__).resolve().parents[1]/'data/fullfilm-v4-delivery.json').write_text(json.dumps(delivery,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(result,ensure_ascii=False,indent=2))

if __name__=='__main__':main()
