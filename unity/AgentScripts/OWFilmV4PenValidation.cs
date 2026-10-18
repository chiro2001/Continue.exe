using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;
using UdonSharpEditor;
using VRC.SDKBase;
using VRC.SDK3.ClientSim;
using OldWebMap;
using Object=UnityEngine.Object;
using Vm=VRC.Udon.UdonBehaviour;

public static class OWFilmV4PenValidation
{
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v4");
    static string Report(string name,object value){Directory.CreateDirectory(Tmp+"/reports");string json=JsonConvert.SerializeObject(value,Formatting.Indented);File.WriteAllText(Tmp+"/reports/"+name+".json",json);return json;}
    public static object Describe()
    {
        var types=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name.Contains("ClientSim")).SelectMany(a=>a.GetTypes()).Where(t=>t.Name.Contains("Pickup")||t.Name=="ClientSimPlayer"||t.Name.Contains("Interact"));
        return Report("qvpen-clientsim-api",new{types=types.Select(t=>new{type=t.FullName,methods=t.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.DeclaredOnly).Where(m=>!m.IsSpecialName).Select(m=>m.ToString()).ToArray()}).ToArray()});
    }
    public static object SourceApi()
    {
        var package=UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VRC.SDK3.ClientSim.ClientSimPickupHelper).Assembly);
        string path=Directory.GetFiles(package.resolvedPath,"ClientSimPickupHelper.cs",SearchOption.AllDirectories).Single();
        return new{path,source=File.ReadAllText(path)};
    }
    static void Need(bool b,string reason){if(!b)throw new Exception(reason);}
    static Vm V(UdonSharp.UdonSharpBehaviour b)=>UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
    static float[] Vec(Vector3 v)=>new[]{v.x,v.y,v.z};
    static void Call(ClientSimPlayerHand hand,string method,params object[] args)=>typeof(ClientSimPlayerHand).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hand,args);
    public static async Task<object> EndSnapshot()
    {
        var run=Object.FindObjectOfType<OWFilmV4Presentation>(true);var d=V(run.director);d.SetProgramVariable("_offset",run.director.duration);d.SetProgramVariable("_playing",false);d.SetProgramVariable("_revision",Convert.ToInt32(d.GetProgramVariable("_revision"))+1);d.SendCustomEvent("_onDeserialization");await Task.Delay(700);
        var details=run.details;var vm=V(details);var actual=(VRC_Pickup[])vm.GetProgramVariable("penPickups");return Report("qvpen-end-diagnostic",new{state=V(run).GetProgramVariable("sampledState"),detailState=vm.GetProgramVariable("_lastState"),floorReleased=vm.GetProgramVariable("floorReleased"),pickups=actual.Where(p=>p!=null).Select(p=>new{p.name,p.pickupable,p.IsHeld}).ToArray(),vmPickups=actual.Length,allChildPickups=details.penRoot.GetComponentsInChildren<VRC_Pickup>(true).Length,floors=details.worldFloorColliders.Select(c=>new{c.name,c.enabled,c.gameObject.activeInHierarchy}).ToArray(),oldFloors=details.originalFloorColliders.Select(c=>new{c.name,c.enabled,c.gameObject.activeInHierarchy}).ToArray(),renderers=details.penRenderers.Where(r=>r!=null).Select(r=>new{r.name,r.enabled,r.gameObject.activeInHierarchy}).ToArray()});
    }
    public static async Task<object> Draw()
    {
        Need(EditorApplication.isPlaying,"Actual ClientSim Play Mode required");var run=Object.FindObjectOfType<OWFilmV4Presentation>(true);var director=V(run.director);var savedPosition=Networking.LocalPlayer.GetPosition();var savedRotation=Networking.LocalPlayer.GetRotation();
        ClientSimPlayerHand hand=null;object handType=null;string phase="release ending";
        try
        {
            director.SetProgramVariable("_offset",run.director.duration);director.SetProgramVariable("_playing",false);director.SetProgramVariable("_revision",Convert.ToInt32(director.GetProgramVariable("_revision"))+1);director.SendCustomEvent("_onDeserialization");await Task.Delay(700);
            Need(Convert.ToInt32(V(run).GetProgramVariable("sampledState"))==2,"Ending gate did not release");
            phase="select pen";var pickup=run.details.penPickups.First(p=>p!=null&&p.name=="Pen");Need(pickup.pickupable,"Pen is not pickable after the end");var helper=pickup.GetComponent<ClientSimPickupHelper>();Need(helper!=null,"ClientSim did not initialize the original pickup");
            phase="select ClientSim right hand";hand=Resources.FindObjectsOfTypeAll<ClientSimPlayerHand>().First(h=>(VRC_Pickup.PickupHand)typeof(ClientSimPlayerHand).GetField("_pickupHandType",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(h)==VRC_Pickup.PickupHand.Right);
            phase="grab via ClientSim input";handType=typeof(ClientSimPlayerHand).GetField("handType",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hand);hand.SetHoverPickupable(helper);Call(hand,"GrabInput",true,handType);await Task.Delay(700);
            Need(pickup.IsHeld&&pickup.currentPlayer==Networking.LocalPlayer&&hand.IsHolding(),"Actual ClientSim grab did not take the pen");
            phase="draw original QvPen stroke";var drawingStart=Networking.LocalPlayer.GetPosition();Call(hand,"UseInput",true,handType);
            for(int i=0;i<24;i++){float motion=i/23f;Networking.LocalPlayer.TeleportTo(drawingStart+new Vector3(.18f*Mathf.Sin(motion*Mathf.PI*2),0,.16f*motion),savedRotation);await Task.Delay(70);}
            Call(hand,"UseInput",false,handType);await Task.Delay(900);
            var pool=GameObject.Find("QvPen_Objects");Need(pool!=null,"Original QvPen ink pool absent");
            var lines=pool.GetComponentsInChildren<LineRenderer>(true).Where(l=>l.gameObject.activeInHierarchy&&l.positionCount>=3).ToArray();Need(lines.Length>0,"No original QvPen stroke was created");
            var geometry=lines.Select(l=>new{l.name,l.positionCount,l.enabled,points=Enumerable.Range(0,Math.Min(l.positionCount,8)).Select(i=>Vec(l.GetPosition(i))).ToArray()}).ToArray();
            phase="clear original pen";var penVm=pickup.GetComponents<Vm>().First(v=>v.programSource!=null&&v.programSource.name=="QvPen_Pen");
            var manager=run.details.penRoot.GetComponentsInChildren<Vm>(true).First(v=>v.programSource!=null&&v.programSource.name=="QvPen_PenManager"&&ReferenceEquals(v.GetProgramVariable("pen"),penVm));
            manager.SendCustomEvent("Clear");await Task.Delay(600);Need(lines.All(l=>l==null||!l.gameObject.activeInHierarchy),"Original clear did not remove the drawn stroke");
            // Draw a second stroke so replay must hide live generated ink too.
            Call(hand,"UseInput",true,handType);for(int i=0;i<12;i++){Networking.LocalPlayer.TeleportTo(drawingStart+new Vector3(i*.015f,0,.2f),savedRotation);await Task.Delay(70);}Call(hand,"UseInput",false,handType);await Task.Delay(600);
            var live=pool.GetComponentsInChildren<LineRenderer>(true).Where(l=>l.gameObject.activeInHierarchy&&l.positionCount>=3).ToArray();Need(live.Length>0,"Second stroke absent");
            director.SendCustomEvent("StopAtStart");await Task.Delay(450);Need(!pickup.IsHeld&&!pickup.pickupable&&live.Where(l=>l!=null).All(l=>!l.enabled),"Replay did not drop/hide pen and generated ink");
            return Report("qvpen-drawing-validation",new{success=true,actualClientSimGrab=true,actualUseInput=true,strokeCount=geometry.Length,geometry,originalClear=true,replayDropsPen=true,replayHidesGeneratedInk=true,poolManagersRemainActive=pool.activeInHierarchy,scope="Actual single-client original QvPen Udon and ClientSim hand input; scripted movement generates test strokes, no HMD gesture/remote multiplayer claim"});
        }
        catch(Exception e){Report("qvpen-drawing-failure",new{success=false,phase,error=e.ToString()});throw;}
        finally{if(hand!=null){if(handType!=null)Call(hand,"UseInput",false,handType);var current=Networking.LocalPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Right);if(current!=null)current.Drop();}Networking.LocalPlayer.TeleportTo(savedPosition,savedRotation);director.SendCustomEvent("StopAtStart");}
    }
}
