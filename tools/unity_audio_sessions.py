#!/usr/bin/env python3
"""Inspect or restore only a specified Unity process's Windows audio sessions."""
from pathlib import Path
import argparse
import json
import subprocess
import sys

from mute_unity_workstation import CS
from unity_remote import DEFAULT_HOST, run_pwsh, pwsh_literal

INTEROP = CS[:CS.index('public static class OWMQuietOutput')] + r'''
[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface OWMSessionDevices {
 [PreserveSig] int GetCount(out uint count);
 [PreserveSig] int Item(uint index,out OWMDevice device);
}
[ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface OWMSessionManager {
 [PreserveSig] int GetAudioSessionControl(ref Guid id,uint flags,out IntPtr control);
 [PreserveSig] int GetSimpleAudioVolume(ref Guid id,uint flags,out IntPtr volume);
 [PreserveSig] int GetSessionEnumerator(out OWMSessions sessions);
}
[ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface OWMSessions {
 [PreserveSig] int GetCount(out int count);
 [PreserveSig] int GetSession(int index,out OWMSession control);
}
[ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface OWMSession {
 [PreserveSig] int GetState(out int state);
 [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)]out string name);
 [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)]string name,ref Guid context);
 [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)]out string path);
 [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)]string path,ref Guid context);
 [PreserveSig] int GetGroupingParam(out Guid group);
 [PreserveSig] int SetGroupingParam(ref Guid group,ref Guid context);
 [PreserveSig] int RegisterAudioSessionNotification(IntPtr notify);
 [PreserveSig] int UnregisterAudioSessionNotification(IntPtr notify);
 [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);
 [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);
 [PreserveSig] int GetProcessId(out uint pid);
}
[ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface OWMSimpleVolume {
 [PreserveSig] int SetMasterVolume(float value,ref Guid context);
 [PreserveSig] int GetMasterVolume(out float value);
 [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)]bool value,ref Guid context);
 [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)]out bool value);
}
[ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface OWMOutputMeter { [PreserveSig] int GetPeakValue(out float peak); }
public static class OWMUnitySessions {
 static string Q(string s) {return "\""+(s??"").Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n")+"\"";}
 static string F(float value) {return value.ToString("R",System.Globalization.CultureInfo.InvariantCulture);}
 static string B(bool value) {return value?"true":"false";}
 static string Friendly(string id) {
  try {string key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\"+id.Substring(id.LastIndexOf('{'))+@"\Properties";
   using(var p=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key))return p==null?id:Convert.ToString(p.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},14"));
  }catch{return id;}
 }
 public static string Run(int processId,bool restore) {
  var e=(OWMEnumerator)new OWMEndpointEnumerator();var rows=new List<string>();var devices=new List<string>();var observed=new List<string>();var defaults=new List<string>();
  for(int role=0;role<3;role++){OWMDevice d;if(e.GetDefaultAudioEndpoint(0,role,out d)>=0){string id;d.GetId(out id);defaults.Add(id);Marshal.ReleaseComObject(d);}}
  IntPtr ptr;Marshal.ThrowExceptionForHR(e.EnumAudioEndpoints(0,1,out ptr));
  var collection=(OWMSessionDevices)Marshal.GetObjectForIUnknown(ptr);Marshal.Release(ptr);uint count;Marshal.ThrowExceptionForHR(collection.GetCount(out count));
  for(uint index=0;index<count;index++) {
   OWMDevice d;collection.Item(index,out d);string deviceId;d.GetId(out deviceId);
   try {
    Guid managerId=new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");object mo;Marshal.ThrowExceptionForHR(d.Activate(ref managerId,23,IntPtr.Zero,out mo));
    var manager=(OWMSessionManager)mo;OWMSessions sessions;Marshal.ThrowExceptionForHR(manager.GetSessionEnumerator(out sessions));int sessionCount;Marshal.ThrowExceptionForHR(sessions.GetCount(out sessionCount));
    devices.Add("{\"name\":"+Q(Friendly(deviceId))+",\"sessionCount\":"+sessionCount+",\"isDefault\":"+B(defaults.Contains(deviceId))+"}");
    for(int j=0;j<sessionCount;j++) {
     OWMSession session;Marshal.ThrowExceptionForHR(sessions.GetSession(j,out session));uint pid;int pidResult=session.GetProcessId(out pid);
     observed.Add("{\"pid\":"+pid+",\"getPidResult\":"+pidResult+",\"device\":"+Q(Friendly(deviceId))+"}");
     if(pid!=(uint)processId){Marshal.ReleaseComObject(session);continue;}
     int state;session.GetState(out state);var simple=(OWMSimpleVolume)session;float before,after;bool mutedBefore,mutedAfter;
     simple.GetMasterVolume(out before);simple.GetMute(out mutedBefore);Guid context=Guid.Empty;
     if(restore){Marshal.ThrowExceptionForHR(simple.SetMute(false,ref context));if(before==0)Marshal.ThrowExceptionForHR(simple.SetMasterVolume(1,ref context));}
     simple.GetMasterVolume(out after);simple.GetMute(out mutedAfter);
     float sessionPeak=-1;try{((OWMOutputMeter)session).GetPeakValue(out sessionPeak);}catch{}
     Guid volumeId=new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");object vo;d.Activate(ref volumeId,23,IntPtr.Zero,out vo);var endpoint=(OWMVolume)vo;
     bool deviceBefore,deviceAfter;float deviceVolume;endpoint.GetMute(out deviceBefore);
     if(restore&&state==1&&deviceBefore)Marshal.ThrowExceptionForHR(endpoint.SetMute(false,Guid.Empty));
     endpoint.GetMute(out deviceAfter);endpoint.GetMasterVolumeLevelScalar(out deviceVolume);Marshal.ReleaseComObject(vo);
     float devicePeak=-1;try{Guid meterId=new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");object meter;d.Activate(ref meterId,23,IntPtr.Zero,out meter);((OWMOutputMeter)meter).GetPeakValue(out devicePeak);Marshal.ReleaseComObject(meter);}catch{}
     rows.Add("{\"pid\":"+pid+",\"state\":"+state+",\"device\":"+Q(Friendly(deviceId))+",\"deviceId\":"+Q(deviceId)+",\"isDefaultDevice\":"+B(defaults.Contains(deviceId))+",\"volumeBefore\":"+F(before)+",\"volume\":"+F(after)+",\"mutedBefore\":"+B(mutedBefore)+",\"muted\":"+B(mutedAfter)+",\"sessionPeak\":"+F(sessionPeak)+",\"deviceMutedBefore\":"+B(deviceBefore)+",\"deviceMuted\":"+B(deviceAfter)+",\"deviceVolume\":"+F(deviceVolume)+",\"devicePeak\":"+F(devicePeak)+"}");
     Marshal.ReleaseComObject(session);
    }
    Marshal.ReleaseComObject(sessions);Marshal.ReleaseComObject(mo);
   }catch(Exception ex){rows.Add("{\"deviceId\":"+Q(deviceId)+",\"error\":"+Q(ex.Message)+"}");}
   finally{Marshal.ReleaseComObject(d);}
  }
  Marshal.ReleaseComObject(collection);Marshal.ReleaseComObject(e);
  return "{\"pid\":"+processId+",\"callerPid\":"+System.Diagnostics.Process.GetCurrentProcess().Id+",\"callerSession\":"+System.Diagnostics.Process.GetCurrentProcess().SessionId+",\"deviceCount\":"+count+",\"devices\":["+String.Join(",",devices)+"],\"observedSessions\":["+String.Join(",",observed)+"],\"restoreRequested\":"+B(restore)+",\"sessions\":["+String.Join(",",rows)+"]}";
 }
}
'''


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--pid',type=int,required=True)
    p.add_argument('--restore',action='store_true')
    p.add_argument('--prepare-only',action='store_true',help='Upload the script for execution from the interactive Unity process')
    a=p.parse_args()
    base=Path.home()/'tmp/vrc-old-web-map/fullfilm-v1/acceptance';base.mkdir(parents=True,exist_ok=True)
    ps=base/'unity-sessions.ps1'
    ps.write_text("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\nAdd-Type -TypeDefinition @'\n"+INTEROP+"\n'@\n[OWMUnitySessions]::Run("+str(a.pid)+','+('$true' if a.restore else '$false')+')',encoding='utf-8-sig')
    dest='C:/Users/Chiro/tmp/vrc-old-web-map/fullfilm-v1/acceptance/unity-sessions.ps1'
    subprocess.run(['scp','-q',str(ps),DEFAULT_HOST+':'+dest],check=True)
    if a.prepare_only:
        print(json.dumps({'prepared':True,'script':dest,'pid':a.pid,'restore':a.restore}));return 0
    r=run_pwsh('& '+pwsh_literal(dest),capture_output=True)
    if r.returncode:
        sys.stderr.buffer.write(r.stderr);return r.returncode
    report=json.loads(r.stdout.decode('utf-8-sig'))
    report_path=base/('windows-unity-audio-restored.json' if a.restore else 'windows-unity-audio-inspect.json')
    report_path.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(report,ensure_ascii=False,indent=2))
    return 0


if __name__=='__main__':raise SystemExit(main())
