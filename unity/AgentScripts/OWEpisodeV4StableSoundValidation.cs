using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UdonSharpEditor;
using VRC.SDKBase;
using OldWebMap;
using Object=UnityEngine.Object;
using UdonVm=VRC.Udon.UdonBehaviour;

// Independent Pipeline entry. Never writes scene/assets or substitutes proxy
// execution for the optional live backing-Udon check below.
public static class OWEpisodeV4StableSoundValidation
{
    const string ScenePath="Assets/Scenes/OWM_Episode123_v4.unity";
    const string RuntimeSource="Assets/OldWebMap/Scripts/OWV4Oscilloscope.cs";
    const string FoleySource="Assets/OldWebMap/Scripts/OWV4Foley.cs";
    const int SampleRate=240;
    const float AnchorTolerance=.0001f;
    const float TrajectoryTolerance=.00015f;
    static readonly float[][] Intervals={new[]{79.36f,81.92f},new[]{86.40f,88.32f},new[]{102.40f,104.96f}};
    static readonly float[] KeyTimes={0,30.72f,79.36f,80.64f,81.92f,84.48f,86.40f,87.36f,88.32f,89.60f,92.16f,102.40f,103.68f,104.96f,133.12f};
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v4/reports");
    static T F<T>() where T:Object=>Object.FindObjectsOfType<T>(true).FirstOrDefault();
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static float[] Vec(Vector3 p)=>new[]{p.x,p.y,p.z};
    static string Hash(string path)
    {
        using(var sha=System.Security.Cryptography.SHA256.Create())using(var stream=File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    }
    static object Write(string name,object result)
    {
        Directory.CreateDirectory(Tmp);string json=JsonConvert.SerializeObject(result,Formatting.Indented);
        File.WriteAllText(Path.Combine(Tmp,name),json);return json;
    }
    static float Ease(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
    // Independent elapsed-minus-consumed-time formula, not the behaviour helper.
    static float ExpectedMotion(float t)
    {
        return t-Mathf.Clamp(t-79.36f,0,81.92f-79.36f)
            -Mathf.Clamp(t-86.40f,0,88.32f-86.40f)-Mathf.Clamp(t-102.40f,0,104.96f-102.40f);
    }
    static float ExpectedOpen(float t)
    {
        float motion=ExpectedMotion(t);
        return Ease((motion-79.36f)/2.56f)*(1-Ease((motion-85.12f)/2.56f));
    }
    static Vector3 ExpectedPosition(OWV4Oscilloscope osc,float t,int ch)
    {
        float motion=ExpectedMotion(t),phase=ch*1.57079633f,angle=phase+(motion-81.92f)*.46f;
        Vector3 front=osc.fusedCenter+new Vector3(-1.8f+ch*1.2f+Mathf.Sin(motion*.31f+phase)*.07f,
            (ch-1.5f)*.13f+Mathf.Sin(motion*.53f+phase)*.11f,(ch-1.5f)*.13f);
        Vector3 surround=osc.orbitCenter+new Vector3(Mathf.Sin(angle)*osc.orbitRadius,.38f+Mathf.Sin(angle*1.7f+phase)*.4f,Mathf.Cos(angle)*osc.orbitRadius);
        return Vector3.Lerp(front,surround,ExpectedOpen(t));
    }
    sealed class Metrics
    {
        public int samples;public float sourceWaveError,particleWaveError,trajectoryError,motionError,openError;
        public void Check(OWV4Oscilloscope osc,float t,Transform[] waves,Transform[] anchors,float motion,float open,int frame)
        {
            Require(waves!=null&&waves.Length==4&&anchors!=null&&anchors.Length==4,"Four active ribbons and particle anchors required");
            for(int ch=0;ch<4;ch++)
            {
                Vector3 actual=osc.music.sources[ch].transform.position;
                sourceWaveError=Mathf.Max(sourceWaveError,Vector3.Distance(actual,waves[ch].position));
                particleWaveError=Mathf.Max(particleWaveError,Vector3.Distance(anchors[ch].position,waves[ch].position));
                trajectoryError=Mathf.Max(trajectoryError,Vector3.Distance(actual,ExpectedPosition(osc,t,ch)));
                if(osc.regionalWaves!=null&&osc.regionalRenderers!=null)
                    for(int at=ch;at<osc.regionalWaves.Length;at+=4)
                        if(osc.regionalWaves[at]!=waves[ch]&&osc.regionalRenderers[at].enabled)
                            Require(Vector3.Distance(osc.regionalWaves[at].position,actual)>=.499f,"A regional waveform ghost overlaps the physical active ribbon at "+t);
            }
            motionError=Mathf.Max(motionError,Mathf.Abs(motion-ExpectedMotion(t)));openError=Mathf.Max(openError,Mathf.Abs(open-ExpectedOpen(t)));
            int expectedFrame=Mathf.Clamp(Mathf.FloorToInt(t*osc.frameRate),0,osc.frameCount-1);
            Require(frame==expectedFrame,"Waveform PCM sampling used the paused spatial clock at "+t);
            Require(sourceWaveError<AnchorTolerance&&particleWaveError<AnchorTolerance,"Physical source/ribbon/particle world positions diverged at "+t);
            Require(trajectoryError<TrajectoryTolerance&&motionError<.00003f&&openError<.00003f,"Sound position disagrees with the independent physical trajectory at "+t);
            samples++;
        }
    }
    static void CheckSetup(OWV4Oscilloscope osc)
    {
        Require(osc!=null&&osc.music!=null&&osc.journey!=null&&osc.journey.worldFrame!=null,"Integrated V4 sound/journey references required");
        Require(osc.music.externalVoicePositionDriver,"Legacy music position driver must be disabled");
        Require(osc.music.sources!=null&&osc.music.sources.Length==4,"Four real music sources required");
        foreach(var source in osc.music.sources)
        {
            Require(source!=null&&source.clip!=null,"Real music clip missing");
            Require(!source.transform.IsChildOf(osc.journey.worldFrame),"A music source is parented under the travelling frame");
            Require(source.dopplerLevel==0,"A moving position driver must not change the original pitch");
        }
    }
    static object Snapshot(OWV4Oscilloscope osc,float time)
    {
        osc.journey.Sample(time);osc.Sample(time);
        return new{time,motionTime=osc.sampledMotionTime,open=osc.sampledOpen,frame=osc.sampledFrame,
            positions=osc.music.sources.Select(s=>Vec(s.transform.position)).ToArray()};
    }

    // Run after FinalizeAndSave + ReopenCheck. The clean scene is reopened before
    // and after sampling. Material and particle references are temporarily muted
    // so validation cannot alter persistent material values or simulate particles.
    public static object SavedScene()
    {
        var scene=SceneManager.GetActiveScene();
        Require(!EditorApplication.isPlaying&&!EditorApplication.isCompiling&&scene.path==ScenePath&&!scene.isDirty,"Saved, stopped, clean V4 scene required; finish the owned integration/save first");
        string sceneHash=Hash(ScenePath);EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var osc=F<OWV4Oscilloscope>();CheckSetup(osc);var foley=F<OWV4Foley>();Require(foley!=null,"V4 Foley required");
        var materials=osc.waveMaterials;var particles=osc.particles;var metrics=new Metrics();
        var rows=new List<object>();var boundaries=new List<object>();var snapshots=new List<object>();
        var originals=osc.music.sources.Select(s=>new{clip=AssetDatabase.GetAssetPath(s.clip),volume=s.volume,pitch=s.pitch}).ToArray();
        var fused=Vec(osc.fusedCenter);var orbit=Vec(osc.orbitCenter);float radius=osc.orbitRadius;
        float baseVolume=osc.music.baseVolume,endFade=osc.music.endFadeSeconds,waveGain=osc.masterGain;
        var contentPoint=Vec(foley.contentWorldPosition);
        Require(foley.sources!=null&&foley.sources.All(s=>s!=null&&!s.transform.IsChildOf(osc.journey.worldFrame)),"Foley sources must remain outside worldFrame");
        bool passed=false;
        try
        {
            osc.waveMaterials=new Material[4];osc.particles=new ParticleSystem[0];
            foreach(var interval in Intervals)
            {
                float start=interval[0],end=interval[1];int steps=Mathf.CeilToInt((end-start)*SampleRate);
                osc.journey.Sample(start);osc.Sample(start);var fixedPositions=osc.music.sources.Select(s=>s.transform.position).ToArray();
                float drift=0,foleyDrift=0;int firstFrame=osc.sampledFrame;
                for(int i=0;i<=steps;i++)
                {
                    float time=Mathf.Lerp(start,end,i/(float)steps);osc.journey.Sample(time);osc.Sample(time);
                    metrics.Check(osc,time,osc.waves,osc.particleAnchors,osc.sampledMotionTime,osc.sampledOpen,osc.sampledFrame);
                    for(int ch=0;ch<4;ch++)drift=Mathf.Max(drift,Vector3.Distance(fixedPositions[ch],osc.music.sources[ch].transform.position));
                    foleyDrift=Mathf.Max(foleyDrift,Vector3.Distance(foley.EmitterWorldPosition(0),new Vector3(contentPoint[0],contentPoint[1],contentPoint[2])));
                    if(foley.emitters!=null&&foley.emitters.Length>1&&foley.emitters[1]!=null)
                        Require(Vector3.Distance(foley.EmitterWorldPosition(1),foley.emitters[1].position)<.000001f,"Keyboard event did not use its actual physical contact point");
                }
                Require(drift==0,"A music source moved during virtual travel "+start+".."+end+": "+drift);
                Require(foleyDrift==0,"Content Foley inherited virtual travel");
                Require(osc.sampledFrame>firstFrame,"The actual PCM waveform clock froze during travel");
                rows.Add(new{start,end,samples=steps+1,maxSourceDrift=drift,contentFoleyDrift=foleyDrift,firstPcmFrame=firstFrame,lastPcmFrame=osc.sampledFrame});
                foreach(float boundary in interval)
                {
                    const float epsilon=.0001f;osc.journey.Sample(boundary);osc.Sample(boundary);
                    var at=osc.music.sources.Select(s=>s.transform.position).ToArray();float entry=0,exit=0;
                    foreach(float side in new[]{-epsilon,epsilon})
                    {
                        osc.journey.Sample(boundary+side);osc.Sample(boundary+side);
                        for(int ch=0;ch<4;ch++)
                        {
                            float distance=Vector3.Distance(at[ch],osc.music.sources[ch].transform.position);
                            if(side<0)entry=Mathf.Max(entry,distance);else exit=Mathf.Max(exit,distance);
                        }
                    }
                    Require(entry<.001f&&exit<.001f,"Music position jumped at a journey boundary "+boundary);
                    boundaries.Add(new{time=boundary,epsilon,beforeDistance=entry,afterDistance=exit});
                }
            }
            foreach(float time in KeyTimes)
            {
                snapshots.Add(Snapshot(osc,time));
                metrics.Check(osc,time,osc.waves,osc.particleAnchors,osc.sampledMotionTime,osc.sampledOpen,osc.sampledFrame);
            }
            foreach(float time in new[]{81.92f,92.16f})Require(ExpectedOpen(time)<.00001f,"Surround boundary must stay closed at "+time);
            foreach(float time in new[]{84.48f,89.60f})Require(ExpectedOpen(time)>.99999f,"Surround must remain fully open at "+time);
            // Reversed seeks prove there is no hidden last-position freeze state.
            foreach(float time in KeyTimes.Reverse())
            {
                osc.journey.Sample(time);osc.Seek(time);
                metrics.Check(osc,time,osc.waves,osc.particleAnchors,osc.sampledMotionTime,osc.sampledOpen,osc.sampledFrame);
            }
            for(int ch=0;ch<4;ch++)Require(AssetDatabase.GetAssetPath(osc.music.sources[ch].clip)==originals[ch].clip&&osc.music.sources[ch].volume==originals[ch].volume&&osc.music.sources[ch].pitch==originals[ch].pitch,"Validation changed music clip/gain/pitch");
            passed=true;
        }
        finally
        {
            osc.waveMaterials=materials;osc.particles=particles;
            // Only the validator's own in-memory preview is discarded; dirty
            // user scenes were rejected before opening or touching anything.
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        }
        Require(Hash(ScenePath)==sceneHash,"Saved scene bytes changed while checking sound positions");
        return Write("stable-sound-saved-scene.json",new{success=passed,scene=ScenePath,sceneSha256=sceneHash,
            runtimeSourceSha256=Hash(RuntimeSource),foleySourceSha256=Hash(FoleySource),
            sampleRate=SampleRate,journeyIntervals=Intervals,metrics,intervals=rows,boundaries,trajectorySamples=snapshots,
            fusedCenter=fused,orbitCenter=orbit,orbitRadius=radius,contentFoleyWorldPosition=contentPoint,
            musicBaseVolume=baseVolume,musicEndFadeSeconds=endFade,waveMasterGain=waveGain,
            keyboardFoley="actual contact emitter world position at onset; source remains outside travelling frame",
            clips=originals,sceneSavedUnchanged=true,scope="reopened saved-scene C# behaviour sampling; run Runtime separately to check actual backing Udon execution"});
    }

    static UdonVm Vm(UdonSharp.UdonSharpBehaviour b)
    {var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(b);Require(vm!=null,"Backing Udon VM missing");return vm;}
    static float N(UdonVm vm,string field)=>Convert.ToSingle(vm.GetProgramVariable(field));
    static int I(UdonVm vm,string field)=>Convert.ToInt32(vm.GetProgramVariable(field));
    static double Clock()=>Utilities.IsValid(Networking.LocalPlayer)?Networking.GetServerTimeInSeconds():Time.realtimeSinceStartup;

    // Run in ClientSim Play Mode on the integrated V4 scene. Only the director
    // anchor is sought; source/wave/particle positions are read after real Udon
    // Presentation.LateUpdate execution, never set by this validator.
    public static async Task<object> Runtime()
    {
        Require(EditorApplication.isPlaying&&SceneManager.GetActiveScene().path==ScenePath,"V4 ClientSim Play Mode required");
        var osc=F<OWV4Oscilloscope>();CheckSetup(osc);var d=Vm(osc.director);var o=Vm(osc);var presentation=F<OWV4Presentation>();
        Require(presentation!=null,"V4 Presentation required");var p=Vm(presentation);
        Require(!Convert.ToBoolean(p.GetProgramVariable("offlinePreview")),"Live Udon sampling is disabled by offlinePreview");
        Require(Convert.ToBoolean(Vm(osc.music).GetProgramVariable("externalVoicePositionDriver")),"Actual music VM still owns source position");
        var metrics=new Metrics();var rows=new List<object>();bool success=false;
        try
        {
            d.SendCustomEvent("StopAtStart");await Task.Delay(250);
            foreach(var interval in Intervals)
            {
                float start=interval[0],end=interval[1],target=start-.2f;
                d.SetProgramVariable("_offset",target);d.SetProgramVariable("_anchor",Clock());d.SetProgramVariable("_playing",true);d.SetProgramVariable("_revision",I(d,"_revision")+1);
                var watch=Stopwatch.StartNew();float previous=-1,drift=0;int inside=0,before=0,after=0;bool bracketStarted=false;
                var fixedPositions=Enumerable.Range(0,4).Select(ch=>ExpectedPosition(osc,start,ch)).ToArray();
                while(watch.Elapsed.TotalSeconds<end-start+3)
                {
                    await Task.Delay(5);Require(EditorApplication.isPlaying,"Play Mode stopped during stable sound validation");
                    float time=N(o,"sampledTime");if(time<target-.01f||time==previous)continue;
                    if(!bracketStarted&&(time>start+.12f))continue;bracketStarted=true;previous=time;
                    var waves=o.GetProgramVariable("waves") as Transform[];var anchors=o.GetProgramVariable("particleAnchors") as Transform[];
                    metrics.Check(osc,time,waves,anchors,N(o,"sampledMotionTime"),N(o,"sampledOpen"),I(o,"sampledFrame"));
                    if(time<start)before++;else if(time<=end)
                    {
                        inside++;for(int ch=0;ch<4;ch++)drift=Mathf.Max(drift,Vector3.Distance(fixedPositions[ch],osc.music.sources[ch].transform.position));
                    }
                    else after++;
                    if(time>end+.1f)break;
                }
                Require(inside>=16&&before>0&&after>0,"Insufficient actual Udon samples on both sides of journey "+start+"; before/inside/after="+before+"/"+inside+"/"+after);
                Require(drift<TrajectoryTolerance,"Actual Udon music position drifted during journey "+start+": "+drift);
                rows.Add(new{start,end,observedBefore=before,observedInside=inside,observedAfter=after,maxSourceDrift=drift});
            }
            success=true;
            return Write("stable-sound-runtime.json",new{success,scene=ScenePath,runtimeSourceSha256=Hash(RuntimeSource),foleySourceSha256=Hash(FoleySource),intervals=rows,metrics,
                scope="actual backing Udon execution and real source/ribbon/particle transforms in ClientSim; director seeks bracket each virtual journey"});
        }
        catch(Exception e)
        {
            Write("stable-sound-runtime-failure.json",new{success=false,error=e.ToString(),intervals=rows,metrics});throw;
        }
        finally
        {
            if(EditorApplication.isPlaying){d.SendCustomEvent("StopAtStart");await Task.Delay(250);}
        }
    }
}
