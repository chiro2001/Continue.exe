#!/usr/bin/env python3
"""Mute Windows default render endpoints while preserving all project audio."""
from pathlib import Path
import argparse
import json
import subprocess
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from unity_remote import run_pwsh, pwsh_literal, DEFAULT_HOST

CS = r'''
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class OWMEndpointEnumerator {}
[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface OWMEnumerator {
 [PreserveSig] int EnumAudioEndpoints(int flow,int state,out IntPtr devices);
 [PreserveSig] int GetDefaultAudioEndpoint(int flow,int role,out OWMDevice device);
}
[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface OWMDevice {
 [PreserveSig] int Activate(ref Guid iid,int clsctx,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)] out object instance);
 [PreserveSig] int OpenPropertyStore(int access,out IntPtr properties);
 [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
}
[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface OWMVolume {
 [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
 [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
 [PreserveSig] int GetChannelCount(out uint count);
 [PreserveSig] int SetMasterVolumeLevel(float level,Guid context);
 [PreserveSig] int SetMasterVolumeLevelScalar(float level,Guid context);
 [PreserveSig] int GetMasterVolumeLevel(out float level);
 [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
 [PreserveSig] int SetChannelVolumeLevel(uint channel,float level,Guid context);
 [PreserveSig] int SetChannelVolumeLevelScalar(uint channel,float level,Guid context);
 [PreserveSig] int GetChannelVolumeLevel(uint channel,out float level);
 [PreserveSig] int GetChannelVolumeLevelScalar(uint channel,out float level);
 [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute,Guid context);
 [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}
public static class OWMQuietOutput {
 public static string Run(bool apply) {
  var e=(OWMEnumerator)new OWMEndpointEnumerator();
  var rows=new List<string>(); bool all=true;
  for(int role=0;role<3;role++) {
   OWMDevice d; int hr=e.GetDefaultAudioEndpoint(0,role,out d);
   if(hr<0) {all=false;rows.Add("{\"role\":"+role+",\"error\":"+hr+"}");continue;}
   Guid iid=new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");object v;
   Marshal.ThrowExceptionForHR(d.Activate(ref iid,23,IntPtr.Zero,out v));var volume=(OWMVolume)v;
   bool before,after;Marshal.ThrowExceptionForHR(volume.GetMute(out before));
   if(apply)Marshal.ThrowExceptionForHR(volume.SetMute(true,Guid.Empty));
   Marshal.ThrowExceptionForHR(volume.GetMute(out after));all &= after;
   rows.Add("{\"role\":"+role+",\"was_muted\":"+before.ToString().ToLowerInvariant()+",\"muted\":"+after.ToString().ToLowerInvariant()+"}");
   Marshal.ReleaseComObject(v);Marshal.ReleaseComObject(d);
  }
  Marshal.ReleaseComObject(e);
  return "{\"all_default_outputs_muted\":"+all.ToString().ToLowerInvariant()+",\"endpoints\":["+String.Join(",",rows)+"]}";
 }
}
'''

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check-only',action='store_true')
    parser.add_argument('--output',type=Path,default=Path.home()/'tmp/vrc-old-web-map/fullfilm-v1/reports/windows-output-muted.json')
    args=parser.parse_args()
    script="Add-Type -TypeDefinition @'\n"+CS+"\n'@; [OWMQuietOutput]::Run("+('$false' if args.check_only else '$true')+")"
    args.output.parent.mkdir(parents=True,exist_ok=True)
    ps1=args.output.parent.parent/'mute-output.ps1'
    ps1.write_text(script,encoding='utf-8-sig')
    remote_dir='C:/Users/Chiro/tmp/vrc-old-web-map/fullfilm-v1'
    setup=run_pwsh('New-Item -ItemType Directory -Force -Path '+pwsh_literal(remote_dir)+' | Out-Null',capture_output=True)
    if setup.returncode:
        sys.stderr.buffer.write(setup.stderr);return setup.returncode
    transfer=subprocess.run(['scp','-q',str(ps1),DEFAULT_HOST+':'+remote_dir+'/mute-output.ps1'])
    if transfer.returncode:return transfer.returncode
    result=run_pwsh('& '+pwsh_literal(remote_dir+'/mute-output.ps1'),capture_output=True)
    if result.returncode:
        sys.stderr.buffer.write(result.stderr);return result.returncode
    report=json.loads(result.stdout.decode('utf-8-sig'))
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(report,ensure_ascii=False))
    return 0 if report['all_default_outputs_muted'] else 6

if __name__=='__main__':
    raise SystemExit(main())
