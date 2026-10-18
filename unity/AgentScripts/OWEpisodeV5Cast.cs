using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UdonSharp;
using UdonSharpEditor;
using OldWebMap;
using Object=UnityEngine.Object;

// V5 cast module: three near-field moments and per-slot signature habits, written as new OWV4HubCast tables.
// Entries (run in this order, scene stopped, active scene exactly Assets/Scenes/OWM_Episode123_v5.unity):
//   Apply()    rebuilds keyTimes/positions/yaws/gestures/gazePositions from AgentScripts/data/episode123-v5-cast.json
//              (cached V4 table + authored edits); idempotent; recognises the V4 or the V5 state, refuses anything else.
//   Validate() samples 0..133.12 s every 0.16 s through the real OWV4HubCast.Sample and checks the rules; report
//              reports/cast-validate.json. Neither entry saves the scene. No runtime script is touched.
// Coordinates: actor world position == positions[] (ActorRegionOffset(t) in the actor and the worldFrame offset cancel).
public static class OWEpisodeV5Cast
{
    const string ScenePath="Assets/Scenes/OWM_Episode123_v5.unity";
    const string DataPath="AgentScripts/data/episode123-v5-cast.json";
    static readonly Vector3 Eye=new Vector3(0f,1.35f,-2.8f);
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v5");
    static T F<T>() where T:Object=>Object.FindObjectsOfType<T>(true).FirstOrDefault();
    static string Report(string name,object value){Directory.CreateDirectory(Tmp+"/reports");string s=JsonConvert.SerializeObject(value,Formatting.Indented);File.WriteAllText(Tmp+"/reports/"+name,s);return s;}
    static void Guard(){if(EditorApplication.isPlaying||EditorApplication.isCompiling||SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Stopped editor with active scene "+ScenePath+" required");}
    static JObject Doc(){if(!File.Exists(DataPath))throw new InvalidOperationException("Missing "+DataPath);return JObject.Parse(File.ReadAllText(DataPath));}
    static OWV4HubCast Cast()
    {
        var run=F<OWV4HubCast>();if(run==null||run.actors==null||run.actors.Length!=4)throw new InvalidOperationException("V5 scene needs the V4 OWV4HubCast with four actors");
        if(run.journey==null||run.journey.worldFrame==null)throw new InvalidOperationException("OWV4HubCast.journey/worldFrame missing");return run;
    }
    static void Sync(UdonSharpBehaviour b){UdonSharpEditorUtility.CopyProxyToUdon(b);var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(b);if(vm!=null)EditorUtility.SetDirty(vm);EditorUtility.SetDirty(b);}

    // ---- tables ------------------------------------------------------------------------------------------------
    sealed class Row{public Vector3 p;public int g;public Vector3 gaze;public float yaw;public Row Copy(){return (Row)MemberwiseClone();}}
    sealed class Table{public float[] time;public Vector3[] pos;public float[] yaw;public int[] gesture;public Vector3[] gaze;}
    sealed class Note{public float time;public int slot;public bool holds;public bool shift;}
    sealed class Stance{public Vector3 p;public int g;public bool walking;}

    // The V4 BuildCast gaze rule, verbatim.
    static Vector3 GazeRule(float time,int i,Vector3 p,int g)
    {
        Vector3 gaze=time>=87.04f && time<102.4f?new Vector3(0,1.1f,5.0f):time>=81.92f && time<85.76f?new Vector3(-5f,1.2f,8f):time>=85.76f && time<87.04f?new Vector3(5f,1.4f,6.2f):new Vector3(i%2==0?-4.9f:4.9f,.95f,4.2f);
        if(g==3 || g==7)gaze=new Vector3(i%2==0?2.7f:-2.7f,1.0f,p.z);
        if(time>=89.60f && time<102.4f)gaze=new Vector3(i%2==0?1.5f:-1.5f,1.0f,i<2?6.6f:4.1f);
        if(time>=102.4f)gaze=i==3?new Vector3(4.8f,1.0f,6.0f):new Vector3(p.x,1.15f,time<117.76f?6.0f:10.2f);
        if(time>=122.88f)gaze=i==3?new Vector3(4.25f,1.04f,5.9f):new Vector3(4.72f,.85f,4.65f);
        return gaze;
    }
    static float YawOf(Vector3 p,Vector3 gaze){Vector3 d=gaze-p;return Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;}
    static int KeyIndex(float[] times,float value){for(int i=0;i<times.Length;i++)if(Mathf.Abs(times[i]-value)<5e-4f)return i;return -1;}

    static Table V4Table(JObject doc)
    {
        var v4=doc["v4"];float[] kt=v4["keyTimes"].Select(x=>(float)x).ToArray();var lift=v4["hallLift"];int[] liftSlots=lift["slots"].Select(x=>(int)x).ToArray();float liftFrom=(float)lift["from"],liftTo=(float)lift["to"],ly=(float)lift["y"];
        int n=kt.Length*4;var t=new Table{time=kt,pos=new Vector3[n],yaw=new float[n],gesture=new int[n],gaze=new Vector3[n]};
        for(int k=0;k<kt.Length;k++)for(int s=0;s<4;s++)
        {
            var sl=v4["slots"][s];float y=liftSlots.Contains(s) && kt[k]>=liftFrom && kt[k]<=liftTo?ly:0f;
            Vector3 p=new Vector3((float)sl["x"][k],y,(float)sl["z"][k]);int g=(int)sl["g"][k];Vector3 gz=GazeRule(kt[k],s,p,g);int at=k*4+s;
            t.pos[at]=p;t.gesture[at]=g;t.gaze[at]=gz;t.yaw[at]=YawOf(p,gz);
        }
        return t;
    }

    // V5 = V4 rows + habit gestures (V4 keys) + inserted hold keys + path edits (positions, gestures, gaze).
    static Table DeriveV5(JObject doc,List<Note> notes)
    {
        var b4=V4Table(doc);int k4=b4.time.Length;var rows=new Row[k4][];
        for(int k=0;k<k4;k++){rows[k]=new Row[4];for(int s=0;s<4;s++){int at=k*4+s;rows[k][s]=new Row{p=b4.pos[at],g=b4.gesture[at],gaze=b4.gaze[at],yaw=b4.yaw[at]};}}
        foreach(var hb in doc["habits"])
        {
            int slot=(int)hb["slot"];
            foreach(var ed in hb["edits"])
            {
                int j=KeyIndex(b4.time,(float)ed["t"]);if(j<0)throw new InvalidOperationException("Habit time is not a V4 key: "+ed["t"]);
                if(rows[j][slot].g!=(int)ed["was"])throw new InvalidOperationException("Habit edit "+ed["t"]+" slot "+slot+" expects V4 gesture "+ed["was"]+" but V4 has "+rows[j][slot].g);
                rows[j][slot].g=(int)ed["g"];
            }
        }
        float[] times=b4.time.Concat(doc["newKeys"].Select(x=>(float)x)).Distinct().OrderBy(x=>x).ToArray();
        var edited=new HashSet<string>();foreach(var pa in doc["paths"])foreach(var ed in pa["edits"])edited.Add((int)pa["slot"]+"@"+Mathf.RoundToInt((float)ed["t"]*1000f));
        var all=new Row[times.Length][];
        for(int n=0;n<times.Length;n++)
        {
            all[n]=new Row[4];int j=KeyIndex(b4.time,times[n]);
            if(j>=0){for(int s=0;s<4;s++)all[n][s]=rows[j][s].Copy();continue;}
            int k=0;for(int q=0;q<k4;q++)if(b4.time[q]<times[n])k=q;
            for(int s=0;s<4;s++)
            {
                all[n][s]=rows[k][s].Copy();if(edited.Contains(s+"@"+Mathf.RoundToInt(times[n]*1000f)))continue;
                int a=k*4+s,b=(k+1)*4+s;
                notes.Add(new Note{time=times[n],slot=s,holds=Vector3.Distance(b4.pos[a],b4.pos[b])<1e-4f && Vector3.Distance(b4.gaze[a],b4.gaze[b])<1e-4f && Mathf.Abs(Mathf.DeltaAngle(b4.yaw[a],b4.yaw[b]))<1e-3f,shift=rows[k][s].g!=rows[k+1][s].g});
            }
        }
        foreach(var pa in doc["paths"])
        {
            int slot=(int)pa["slot"];
            foreach(var ed in pa["edits"])
            {
                int j=KeyIndex(times,(float)ed["t"]);if(j<0)throw new InvalidOperationException("Path edit time is not a key: "+ed["t"]);
                var r=all[j][slot];bool moved=false;
                if(ed["x"]!=null || ed["y"]!=null || ed["z"]!=null){r.p=new Vector3(ed["x"]!=null?(float)ed["x"]:r.p.x,ed["y"]!=null?(float)ed["y"]:r.p.y,ed["z"]!=null?(float)ed["z"]:r.p.z);moved=true;}
                if(ed["g"]!=null && ed["g"].Type!=JTokenType.Null)r.g=(int)ed["g"];
                var gz=ed["gaze"];string mode=gz==null || gz.Type==JTokenType.Null?"":gz.Type==JTokenType.String?(string)gz:"vec";bool aim=false;
                if(mode=="viewer"){r.gaze=Eye;aim=true;}
                else if(mode=="vec"){r.gaze=new Vector3((float)gz[0],(float)gz[1],(float)gz[2]);aim=true;}
                else if(mode=="rule" || moved){r.gaze=GazeRule(times[j],slot,r.p,r.g);aim=true;}
                if(aim)r.yaw=YawOf(r.p,r.gaze);
            }
        }
        int total=times.Length*4;var t=new Table{time=times,pos=new Vector3[total],yaw=new float[total],gesture=new int[total],gaze=new Vector3[total]};
        for(int n=0;n<times.Length;n++)for(int s=0;s<4;s++){int at=n*4+s;t.pos[at]=all[n][s].p;t.yaw[at]=all[n][s].yaw;t.gesture[at]=all[n][s].g;t.gaze[at]=all[n][s].gaze;}
        return t;
    }

    static Table ReadScene(OWV4HubCast run){return new Table{time=run.keyTimes,pos=run.positions,yaw=run.yaws,gesture=run.gestures,gaze=run.gazePositions};}
    static string Diff(Table a,Table b)
    {
        if(a.time==null || b.time==null || a.time.Length!=b.time.Length)return "key count "+(a.time==null?0:a.time.Length)+" vs "+(b.time==null?0:b.time.Length);
        int n=a.time.Length;
        if(a.pos==null || a.yaw==null || a.gesture==null || a.gaze==null || a.pos.Length!=n*4 || a.yaw.Length!=n*4 || a.gesture.Length!=n*4 || a.gaze.Length!=n*4)return "per-slot array length";
        for(int k=0;k<n;k++)
        {
            if(Mathf.Abs(a.time[k]-b.time[k])>1e-4f)return "key time "+k+" ("+a.time[k]+" vs "+b.time[k]+")";
            for(int s=0;s<4;s++)
            {
                int i=k*4+s;
                if(Vector3.Distance(a.pos[i],b.pos[i])>1e-3f)return "position key "+k+" slot "+s;
                if(Mathf.Abs(Mathf.DeltaAngle(a.yaw[i],b.yaw[i]))>.05f)return "yaw key "+k+" slot "+s;
                if(a.gesture[i]!=b.gesture[i])return "gesture key "+k+" slot "+s;
                if(Vector3.Distance(a.gaze[i],b.gaze[i])>1e-3f)return "gaze key "+k+" slot "+s;
            }
        }
        return null;
    }

    // Replays OWV4HubCast.SampleActors (position, gesture, walking) for a table.
    static Stance[] Model(Table t,float time)
    {
        int key=0;for(int k=0;k<t.time.Length-1;k++)if(time>=t.time[k])key=k;
        int next=Mathf.Min(key+1,t.time.Length-1);float span=Mathf.Max(.01f,t.time[next]-t.time[key]);float part=Mathf.Clamp01((time-t.time[key])/span);float move=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(part/.72f));
        var r=new Stance[4];
        for(int i=0;i<4;i++)
        {
            int a=key*4+i,b=next*4+i;Vector3 delta=t.pos[b]-t.pos[a];bool walking=delta.sqrMagnitude>.06f && part<.72f;
            Vector3 p=Vector3.Lerp(t.pos[a],t.pos[b],move);if(walking)p.y+=Mathf.Sin(part*span*9f)*.009f;
            r[i]=new Stance{p=p,g=walking?6:t.gesture[part<.72f?a:b],walking=walking};
        }
        return r;
    }

    // ---- Apply -------------------------------------------------------------------------------------------------
    public static object Apply(){return Write(false);}
    // Overwrites the tables even when they match neither V4 nor V5 (someone edited them). Only for a deliberate reset.
    public static object ApplyForce(){return Write(true);}
    static object Write(bool force)
    {
        Guard();var doc=Doc();var run=Cast();var notes=new List<Note>();var v5=DeriveV5(doc,notes);var v4=V4Table(doc);
        var broken=notes.Where(n=>!n.holds).ToArray();if(broken.Length>0)throw new InvalidOperationException("Inserted key moves another slot: "+JsonConvert.SerializeObject(broken));
        var scene=ReadScene(run);string toV5=Diff(scene,v5),toV4=Diff(scene,v4);string state=toV5==null?"v5":toV4==null?"v4":null;
        if(state==null && !force)throw new InvalidOperationException("OWV4HubCast tables match neither the V4 table nor the V5 table; refusing to overwrite (ApplyForce() overrides). First V5 mismatch: "+toV5+"; first V4 mismatch: "+toV4);
        if(state==null)state="forced over unknown tables ("+toV4+")";
        float time=run.sampledTime;
        run.keyTimes=v5.time.ToArray();run.positions=v5.pos.ToArray();run.yaws=v5.yaw.ToArray();run.gestures=v5.gesture.ToArray();run.gazePositions=v5.gaze.ToArray();
        Sync(run);run.Sample(time);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        var result=new{scene=ScenePath,stateBefore=state,changed=state!="v5",keys=v5.time.Length,v4Keys=v4.time.Length,insertedKeys=doc["newKeys"].Select(x=>(float)x).ToArray(),gestureShift=notes.Where(n=>n.shift).Select(n=>new{n.time,n.slot}).ToArray(),moments=doc["moments"].Select(m=>(string)m["id"]+" slot"+(int)m["slot"]+" "+m["window"][0]+"-"+m["window"][1]).ToArray(),note="scene marked dirty, not saved"};
        return Report("cast-apply.json",result);
    }

    // ---- Validate ----------------------------------------------------------------------------------------------
    static void Wake(OWActorRig actor)
    {
        actor.gameObject.SetActive(true);if(actor.visualRoot!=null)actor.visualRoot.gameObject.SetActive(true);
        if(actor.animator==null)return;for(var t=actor.animator.transform;t!=null && t.IsChildOf(actor.transform);t=t.parent)t.gameObject.SetActive(true);actor.animator.enabled=true;
    }
    static bool KeepOut(JToken ko,Vector3 p,float radius)
    {
        if(ko["rect"]!=null){float x0=(float)ko["rect"][0],z0=(float)ko["rect"][1],x1=(float)ko["rect"][2],z1=(float)ko["rect"][3];float dx=Mathf.Max(x0-p.x,0f,p.x-x1),dz=Mathf.Max(z0-p.z,0f,p.z-z1);return Mathf.Sqrt(dx*dx+dz*dz)<radius-1e-6f;}
        float cx=(float)ko["circle"][0],cz=(float)ko["circle"][1],cr=(float)ko["circle"][2];return Mathf.Sqrt((p.x-cx)*(p.x-cx)+(p.z-cz)*(p.z-cz))<cr+radius;
    }
    static float VirtualZ(float t){return 24f*Mathf.SmoothStep(0,1,Mathf.Clamp01((t-79.36f)/2.56f))+28f*Mathf.SmoothStep(0,1,Mathf.Clamp01((t-86.40f)/1.92f))+28f*Mathf.SmoothStep(0,1,Mathf.Clamp01((t-102.40f)/2.56f));}

    public static object Validate()
    {
        Guard();var doc=Doc();var run=Cast();var journey=run.journey;var notes=new List<Note>();var v5=DeriveV5(doc,notes);var v4=V4Table(doc);
        var lim=doc["limits"];float step=(float)lim["sampleStep"],speedMax=(float)lim["speedMax"],clearance=(float)lim["viewerClearance"],nearMin=(float)lim["nearMin"],nearMax=(float)lim["nearMax"],radius=(float)lim["bodyRadius"],bandMargin=(float)lim["bandMargin"],cutStep=(float)lim["cutStep"],sepMin=(float)lim["actorSeparationMin"];
        var band=doc["band"];float bandHalf=(float)band["halfWidth"],bandZ0=(float)band["zMin"],bandZ1=(float)band["zMax"],bandY=(float)band["yMax"];float[] regionZ=band["regionZ"].Select(x=>(float)x).ToArray();
        var fails=new List<string>();
        string diff=Diff(ReadScene(run),v5);if(diff!=null)fails.Add("Scene tables differ from "+DataPath+" ("+diff+"); run OWEpisodeV5Cast.Apply() first");
        foreach(var bad in notes.Where(x=>!x.holds))fails.Add("inserted key "+bad.time+" moves slot "+bad.slot);
        if(run.gestures!=null && run.gestures.Any(g=>g<0 || g>11))fails.Add("gestures array holds ids outside 0..11");
        var pres=F<OWV4Presentation>();bool wasShown=pres!=null && pres.performanceRoot!=null && pres.performanceRoot.activeSelf;float presTime=pres==null?0f:pres.sampledTime;
        float original=run.sampledTime,journeyTime=journey.sampledTime;Vector3 framePos=journey.worldFrame.localPosition;
        int count=(int)Math.Round(133.12/step)+1;var times=new float[count];for(int i=0;i<count;i++)times[i]=Mathf.Min(i*step,133.12f);
        var world=new Vector3[count][];int gestureChecked=0,gestureWrong=0,frameWrong=0,badGesture=0;float frameMax=0,minViewer=99,maxSpeed=0,maxAuthored=0,minSep=99,minAuthoredSep=99;float minViewerAt=0;int minViewerSlot=0;
        var inherited=new HashSet<string>();var authoredFast=new List<string>();var cuts=new List<string>();var bandHits=new List<string>();var keepHits=new List<string>();var gates=true;int gateSamples=0;
        object hands=null;float[] soles=null;object plates=null;
        try
        {
            if(pres!=null && !wasShown)pres.Preview(1.0f);
            bool[] authoredPrev=new bool[4];Vector3[] prevWorld=null;
            for(int s=0;s<count;s++)
            {
                float t=times[s];journey.Sample(t);run.Sample(t);var m5=Model(v5,t);var m4=Model(v4,t);world[s]=new Vector3[4];bool[] authored=new bool[4];
                for(int i=0;i<4;i++)
                {
                    var actor=run.actors[i];Vector3 w=actor.transform.position;world[s][i]=w;
                    float fe=Vector3.Distance(w,m5[i].p);frameMax=Mathf.Max(frameMax,fe);if(fe>.01f)frameWrong++;
                    float dx=m5[i].p.x-m4[i].p.x,dz=m5[i].p.z-m4[i].p.z;authored[i]=Mathf.Sqrt(dx*dx+dz*dz)>.01f;
                    var driver=run.appearanceChanged && run.alternateActors!=null && i<run.alternateActors.Length && run.alternateActors[i]!=null?run.alternateActors[i]:actor;
                    if(driver.animator!=null && driver.animator.gameObject.activeInHierarchy && driver.animator.enabled){int ag=driver.animator.GetInteger("V4Pose");gestureChecked++;if(ag!=m5[i].g)gestureWrong++;if(ag<0 || ag>11)badGesture++;}
                    if(m5[i].g<0 || m5[i].g>11)badGesture++;
                    float d=Mathf.Sqrt(w.x*w.x+(w.z+2.8f)*(w.z+2.8f));if(d<minViewer){minViewer=d;minViewerAt=t;minViewerSlot=i;}
                    float vz=VirtualZ(t);
                    for(int r=0;r<regionZ.Length;r++)
                    {
                        if(Mathf.Abs(regionZ[r]-vz)>.25f)continue;float z0=bandZ0+regionZ[r]-vz-bandMargin,z1=bandZ1+regionZ[r]-vz+bandMargin;
                        if(Mathf.Abs(w.x)<bandHalf+bandMargin && w.z>z0 && w.z<z1 && w.y<bandY)bandHits.Add(t.ToString("0.00")+" slot"+i+" region"+r);
                    }
                    if(authored[i])foreach(var ko in doc["keepOut"])if(t>=(float)ko["from"] && t<(float)ko["to"] && KeepOut(ko,w,radius))keepHits.Add(t.ToString("0.00")+" slot"+i+" "+(string)ko["name"]);
                    if(prevWorld!=null)
                    {
                        float sp=Mathf.Sqrt((w.x-prevWorld[i].x)*(w.x-prevWorld[i].x)+(w.z-prevWorld[i].z)*(w.z-prevWorld[i].z))/step;maxSpeed=Mathf.Max(maxSpeed,sp);
                        bool auth=authored[i] || authoredPrev[i];if(auth)maxAuthored=Mathf.Max(maxAuthored,sp);
                        if(sp>speedMax){if(auth)authoredFast.Add(t.ToString("0.00")+" slot"+i+" "+sp.ToString("0.00"));else inherited.Add(((int)t)+"/"+i);}
                        if(sp*step>cutStep)cuts.Add(t.ToString("0.00")+" slot"+i);
                    }
                }
                for(int i=0;i<4;i++)for(int j=i+1;j<4;j++)
                {
                    float dd=Mathf.Sqrt((world[s][i].x-world[s][j].x)*(world[s][i].x-world[s][j].x)+(world[s][i].z-world[s][j].z)*(world[s][i].z-world[s][j].z));minSep=Mathf.Min(minSep,dd);if(authored[i] || authored[j])minAuthoredSep=Mathf.Min(minAuthoredSep,dd);
                }
                if(t>=102.4f && t<=122.88f && run.sampledGateLocked!=null && run.sampledGateLocked.Length>=3)
                {
                    gateSamples++;Vector3 a2=world[s][2],a3=world[s][3];
                    if(t<122.88f){if(run.sampledGateLocked[0] && !(a2.z<5.6f))gates=false;if(run.sampledGateLocked[1] && !(a2.z<9.6f))gates=false;if(run.sampledGateLocked[2] && !(a3.z<5.6f))gates=false;}
                }
                prevWorld=world[s];authoredPrev=authored;
            }
            if(pres!=null && run.actors.All(a=>a.animator!=null && a.animator.gameObject.activeInHierarchy))
            {
                hands=Hands(run,doc);soles=SoleStats(run,journey);plates=PlateStats(run,journey,new[]{82.5f,85.12f,88.64f,90.5f,92.5f,94.0f,96.0f,100.0f});
            }
            else fails.Add("Actor animators are inactive and OWV4Presentation could not show the performance; hands/soles/nameplates unverified");
        }
        finally
        {
            journey.Sample(journeyTime);run.Sample(original);journey.worldFrame.localPosition=framePos;
            if(pres!=null && !wasShown){pres.Waiting();journey.Sample(journeyTime);journey.worldFrame.localPosition=framePos;}
            else if(pres!=null && wasShown)pres.Preview(presTime);
        }
        // moments table
        var table=new List<string>();var momentRows=new List<object>();
        foreach(var m in doc["moments"])
        {
            int slot=(int)m["slot"];float w0=(float)m["window"][0],w1=(float)m["window"][1];float min=99,minT=w0;var seen=new HashSet<int>();
            int[] need=m["need"]==null?new int[0]:m["need"].Select(x=>(int)x).ToArray();
            for(int s=0;s<count;s++)
            {
                float d=Mathf.Sqrt(world[s][slot].x*world[s][slot].x+(world[s][slot].z+2.8f)*(world[s][slot].z+2.8f));
                if(times[s]>=w0 && times[s]<=w1){if(d<min){min=d;minT=times[s];}seen.Add(Model(v5,times[s])[slot].g);}
            }
            float near0=-1,near1=-1,cover=0;float bestOverlap=0;float start=-1,last=-1;
            for(int s=0;s<=count;s++)
            {
                bool inside=s<count && Mathf.Sqrt(world[s][slot].x*world[s][slot].x+(world[s][slot].z+2.8f)*(world[s][slot].z+2.8f))<=nearMax+.05f;
                if(inside && start<0)start=times[s];
                if(!inside && start>=0){float ov=Mathf.Max(0f,Mathf.Min(last,w1)-Mathf.Max(start,w0));if(ov>bestOverlap){bestOverlap=ov;near0=start;near1=last;}start=-1;}
                if(s<count)last=times[s];
            }
            cover=bestOverlap/(w1-w0);string id=(string)m["id"];
            if(min<nearMin || min>nearMax)fails.Add("moment "+id+" closest approach "+min.ToString("0.00")+" m is outside "+nearMin+".."+nearMax);
            if(cover<.95f)fails.Add("moment "+id+" is within "+nearMax+" m for only "+(cover*100).ToString("0")+"% of its window");
            foreach(int g in need)if(!seen.Contains(g))fails.Add("moment "+id+" never performs gesture "+g+" inside its window");
            Func<float,string> at=time=>{int index=Mathf.Clamp(Mathf.RoundToInt(time/step),0,count-1);return time.ToString("0.0")+"s="+Mathf.Sqrt(world[index][slot].x*world[index][slot].x+(world[index][slot].z+2.8f)*(world[index][slot].z+2.8f)).ToString("0.00")+"m";};
            string line=id+"  slot"+slot+" "+(string)m["name"]+"  window "+w0.ToString("0.0")+"-"+w1.ToString("0.0")+" s  distance "+at(w0)+" "+at((w0+w1)*.5f)+" "+at(w1)+"  closest "+min.ToString("0.00")+" m @"+minT.ToString("0.00")+"  within "+nearMax+" m "+near0.ToString("0.00")+"-"+near1.ToString("0.00")+" s ("+(cover*100).ToString("0")+"% of window)  gestures "+string.Join(",",seen.OrderBy(x=>x).Select(x=>x.ToString()).ToArray());
            table.Add(line);Debug.Log("[OWEpisodeV5Cast] "+line);
            momentRows.Add(new{id,slot,window=new[]{w0,w1},closest=min,closestAt=minT,nearFrom=near0,nearTo=near1,cover,gestures=seen.OrderBy(x=>x).ToArray()});
        }
        if(minViewer<clearance)fails.Add("actor "+minViewerSlot+" within "+minViewer.ToString("0.00")+" m of the viewer column at "+minViewerAt.ToString("0.00")+" s");
        if(frameWrong>0)fails.Add(frameWrong+" actor samples differ from the table model by >1 cm (max "+frameMax.ToString("0.000")+" m); hierarchy offset or table mismatch");
        if(gestureWrong>0)fails.Add(gestureWrong+" animator V4Pose values differ from the sampled table gesture");
        if(badGesture>0)fails.Add(badGesture+" gesture ids outside 0..11");
        if(authoredFast.Count>0)fails.Add("authored motion above "+speedMax+" m/s: "+string.Join("; ",authoredFast.Take(5).ToArray()));
        if(cuts.Count>0)fails.Add("position cuts: "+string.Join("; ",cuts.Take(5).ToArray()));
        if(bandHits.Count>0)fails.Add("actor inside the keyline band volume: "+string.Join("; ",bandHits.Take(5).ToArray()));
        if(keepHits.Count>0)fails.Add("authored path crosses scenery: "+string.Join("; ",keepHits.Take(5).ToArray()));
        if(minAuthoredSep<sepMin)fails.Add("authored motion within "+minAuthoredSep.ToString("0.00")+" m of another actor");
        if(!gates)fails.Add("an actor passed a locked gate (slot 2 z>=5.6/9.6 or slot 3 z>=5.6)");
        if(hands!=null){var h=JObject.FromObject(hands);if((int)h["crossed"]>0)fails.Add((int)h["crossed"]+" hand-crossing samples");}
        if(soles!=null){if(soles[0]>=.01f)fails.Add("idle sole error "+soles[0].ToString("0.0000")+" m >= 1 cm");if(soles[1]>=.015f)fails.Add("idle height error "+soles[1].ToString("0.0000")+" m");}
        if(plates!=null){var p=JObject.FromObject(plates);if((float)p["maxError"]>=.001f)fails.Add("nameplate does not follow the head (max error "+(float)p["maxError"]+" m)");}
        var result=new{success=fails.Count==0,fails=fails.ToArray(),moments=table.ToArray(),momentRows=momentRows.ToArray(),sampleStep=step,samples=count,minViewerDistance=minViewer,minViewerAt,minViewerSlot,maxAuthoredSpeed=maxAuthored,maxSpeedAll=maxSpeed,speedLimit=speedMax,inheritedFastSegmentsFromV4=inherited.Count,inheritedNote="V4 hub and gate sprints (up to ~4.8 m/s) are unchanged; every authored sample stays below the limit",cuts=cuts.Count,bandHits=bandHits.Count,keepOutHits=keepHits.Count,minActorSeparation=minSep,minAuthoredSeparation=minAuthoredSep,gatesBlocked=gates,gateSamples,gestureSamplesChecked=gestureChecked,tableModelMaxError=frameMax,hands,idleSoleError=soles==null?-1f:soles[0],idleHeightError=soles==null?-1f:soles[1],nameplates=plates,gestureShift=notes.Where(n=>n.shift).Select(n=>n.time+"/slot"+n.slot).ToArray(),keys=v5.time.Length};
        string json=Report("cast-validate.json",result);if(fails.Count>0)throw new InvalidOperationException(json);
        return new{result.success,result.moments,maxAuthoredSpeed=maxAuthored,minViewerDistance=minViewer,hands=result.hands,idleSoleError=result.idleSoleError,nameplates=result.nameplates,report=Tmp+"/reports/cast-validate.json"};
    }

    // Same dozen clips x five phases as OWEpisodeV4HubCast.ValidateHands, on every body.
    static object Hands(OWV4HubCast run,JObject doc)
    {
        string[] names=doc["gestureNames"].Select(x=>(string)x).ToArray();int samples=0,crossed=0;float minSeparation=99;
        foreach(var actor in run.actors.Concat(run.alternateActors).Where(a=>a!=null && a.animator!=null && a.leftHand!=null && a.rightHand!=null))
        {
            Wake(actor);
            foreach(string action in names)foreach(float phase in new[]{.1f,.35f,.5f,.7f,.9f})
            {
                actor.animator.SetFloat("V4PoseTime",phase);actor.animator.Play(action,0,phase);actor.animator.Update(.02f);actor.animator.Update(0);
                Vector3 left=actor.transform.InverseTransformPoint(actor.leftHand.position),right=actor.transform.InverseTransformPoint(actor.rightHand.position);
                samples++;minSeparation=Mathf.Min(minSeparation,right.x-left.x);if(left.x>=right.x-.015f)crossed++;
            }
        }
        return new{samples,crossed,minSeparation};
    }

    // V4Idle sole/height envelope (the V4 Validate metric), base bodies at 60 s and replacement bodies at 100 s.
    static float[] SoleStats(OWV4HubCast run,OWV4Journey journey)
    {
        float maxSole=0,maxHeight=0;
        foreach(bool alternate in new[]{false,true})
        {
            float t=alternate?100f:60f;journey.Sample(t);run.Sample(t);var rigs=alternate?run.alternateActors:run.actors;
            for(int slot=0;slot<rigs.Length;slot++)
            {
                var actor=rigs[slot];if(actor==null || actor.animator==null)continue;Wake(actor);
                actor.animator.SetFloat("V4PoseTime",0f);actor.animator.Play("V4Idle",0,0f);actor.animator.Update(0f);
                int s=slot;bool alt=alternate;var renderers=run.bodyRenderers.Where((r,index)=>r!=null && run.bodySlots[index]==s && run.bodyAlternates[index]==alt && run.bodyBaseEnabled[index] && r.gameObject.activeInHierarchy).ToArray();
                if(renderers.Length==0)continue;var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                maxSole=Mathf.Max(maxSole,Mathf.Abs(bounds.min.y-actor.transform.position.y));maxHeight=Mathf.Max(maxHeight,Mathf.Abs(bounds.size.y-1.25f));
            }
        }
        return new[]{maxSole,maxHeight};
    }

    // Plates follow the visible body's head at the nameplate headGap after the same two-pass pose as OWEpisodeV5Build.Pose.
    static object PlateStats(OWV4HubCast run,OWV4Journey journey,float[] times)
    {
        var names=run.nameplates;if(names==null || names.actorPlates==null)return new{checkedPlates=0,maxError=0f};int checkedPlates=0;float max=0;
        string[] clips={"V4Idle","V4Type","V4Carry","V4Present","V4Point","V4Inspect","V4Walk","V4Wave","V4Calibrate","V4Talk","V4Adjust","V4Shelve"};
        foreach(float t in times)
        {
            journey.Sample(t);run.Sample(t);
            foreach(var a in run.actors.Concat(run.alternateActors).Where(a=>a!=null && a.animator!=null && a.animator.gameObject.activeInHierarchy)){int g=Mathf.Clamp(a.animator.GetInteger("V4Pose"),0,11);a.animator.Play(clips[g],0,a.animator.GetFloat("V4PoseTime"));a.animator.Update(0);}
            run.Sample(t);
            for(int i=0;i<names.actorPlates.Length && i<run.actors.Length;i++)
            {
                var plate=names.actorPlates[i];if(plate==null || !plate.gameObject.activeSelf)continue;
                var body=run.appearanceChanged && run.alternateActors[i]!=null?run.alternateActors[i]:run.actors[i];if(body.head==null)continue;
                max=Mathf.Max(max,Vector3.Distance(plate.position,body.head.position+Vector3.up*names.headGap));checkedPlates++;
            }
        }
        return new{checkedPlates,maxError=max};
    }
}
