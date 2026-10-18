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

// Run only through Pipeline, in this order: Import (after the WAVs are copied to Revision5/Foley/Audio), Build, Validate.
// V5 era Foley: one AudioSource per clip under "V4 physical sound sources/V5 foley", emitters, and the OWV4Foley event contract
// (OWV4Foley runtime is reused unchanged; the 4 FC14 music voices are never touched).
public static class OWEpisodeV5Foley
{
    const string ScenePath="Assets/Scenes/OWM_Episode123_v5.unity";
    const string Root="Assets/OldWebMap/Episode123/Revision5/Foley";
    const string AudioRoot=Root+"/Audio";
    const string DataPath="AgentScripts/data/episode123-v5-foley.json";
    const string SourceRootName="V5 foley";
    const string EmitterRootName="V5 foley emitters";
    const string SourcePrefix="V5 foley · ";
    const string EmitterPrefix="V5 emitter · ";
    static readonly float[] JourneyStarts={79.36f,86.40f,102.40f};
    static readonly float[] JourneyEnds={81.92f,88.32f,104.96f};
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v5");
    static T F<T>() where T:Object=>Object.FindObjectsOfType<T>(true).FirstOrDefault();
    static Transform Child(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
    static string Report(string name,object value){string dir=Path.Combine(Tmp,"reports");Directory.CreateDirectory(dir);string s=JsonConvert.SerializeObject(value,Formatting.Indented);File.WriteAllText(Path.Combine(dir,name),s);return s;}
    static JObject Load(){return JObject.Parse(File.ReadAllText(DataPath));}
    static Vector3 V3(JToken t){return new Vector3((float)t[0],(float)t[1],(float)t[2]);}
    static float[] Arr(Vector3 v){return new[]{v.x,v.y,v.z};}
    static void Guard()
    {
        if(EditorApplication.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new InvalidOperationException("Stopped, compiled editor required");
        if(SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Active scene must be exactly "+ScenePath+" (was "+SceneManager.GetActiveScene().path+")");
    }
    static Type SpatialType(){return AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("VRC.SDK3.Components.VRCSpatialAudioSource")??a.GetType("VRC.SDKBase.VRC_SpatialAudioSource")).FirstOrDefault(t=>t!=null);}
    static void CopyValue(SerializedProperty a,SerializedProperty b)
    {
        switch(a.propertyType)
        {
            case SerializedPropertyType.Float:b.floatValue=a.floatValue;break;
            case SerializedPropertyType.Boolean:b.boolValue=a.boolValue;break;
            case SerializedPropertyType.Integer:b.intValue=a.intValue;break;
        }
    }
    // Fallback = the component VRChat auto-added to the V4 foley sources (read from the saved V2/V4 scenes).
    static void SetDefault(SerializedProperty p,string field)
    {
        switch(p.propertyType)
        {
            case SerializedPropertyType.Float:p.floatValue=field=="Gain"?10f:field=="Far"?80f:0f;break;
            case SerializedPropertyType.Boolean:p.boolValue=field=="EnableSpatialization";break;
        }
    }
    static void CopySpatial(GameObject from,GameObject to)
    {
        var type=SpatialType();if(type==null)throw new InvalidOperationException("VRCSpatialAudioSource type not found");
        var target=to.GetComponent(type);if(target==null)target=to.AddComponent(type);
        var comp=from!=null?from.GetComponent(type):null;var source=comp!=null?new SerializedObject(comp):null;var so=new SerializedObject(target);
        foreach(string field in new[]{"Gain","Near","Far","VolumetricRadius","EnableSpatialization","UseAudioSourceVolumeCurve"})
        {
            var p=so.FindProperty(field);if(p==null)continue;var q=source!=null?source.FindProperty(field):null;
            if(q!=null)CopyValue(q,p);else SetDefault(p,field);
        }
        so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target);
    }
    static void ApplyTemplate(AudioSource s,AudioSource t)
    {
        bool has=t!=null;
        s.spatialBlend=has?t.spatialBlend:1f;s.spatialize=has?t.spatialize:true;s.spatializePostEffects=has&&t.spatializePostEffects;
        s.dopplerLevel=0f;s.spread=has?t.spread:0f;s.priority=has?t.priority:128;s.minDistance=has?t.minDistance:3f;s.maxDistance=has?t.maxDistance:18f;
        s.rolloffMode=has?t.rolloffMode:AudioRolloffMode.Linear;s.panStereo=0f;s.reverbZoneMix=has?t.reverbZoneMix:1f;
        s.bypassEffects=false;s.bypassListenerEffects=false;s.bypassReverbZones=false;
        if(has&&s.rolloffMode==AudioRolloffMode.Custom)s.SetCustomCurve(AudioSourceCurveType.CustomRolloff,t.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
    }

    public static object Import()
    {
        Guard();var data=Load();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);var rows=new List<object>();
        foreach(JObject c in (JArray)data["clips"])
        {
            string file=(string)c["file"],path=AudioRoot+"/"+file;var importer=AssetImporter.GetAtPath(path) as AudioImporter;
            if(importer==null)throw new InvalidOperationException("Foley WAV missing in the project (copy art/export/episode123-v5/foley/*.wav to "+AudioRoot+" first): "+path);
            float seconds=(float)c["duration"];bool longClip=seconds>=1.0f;
            importer.forceToMono=true;importer.loadInBackground=false;importer.ambisonic=false;
            var s=importer.defaultSampleSettings;s.loadType=AudioClipLoadType.DecompressOnLoad;s.preloadAudioData=true;s.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            s.compressionFormat=longClip?AudioCompressionFormat.Vorbis:AudioCompressionFormat.PCM;if(longClip)s.quality=.75f;
            importer.defaultSampleSettings=s;importer.SaveAndReimport();
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if(clip==null||clip.channels!=1||clip.frequency!=48000||Mathf.Abs(clip.length-seconds)>.01f)throw new InvalidOperationException("Foley clip import mismatch: "+path+" channels="+(clip==null?-1:clip.channels)+" frequency="+(clip==null?-1:clip.frequency)+" length="+(clip==null?-1f:clip.length)+" expected "+seconds);
            rows.Add(new{file,seconds=clip.length,format=s.compressionFormat.ToString(),quality=longClip?s.quality:0f});
        }
        return Report("foley-import.json",new{success=true,clips=rows.Count,rows});
    }

    static Vector3 FrameLocal(Transform frame,string id,JObject def,OWV4HubCast hub,out bool derived)
    {
        Vector3 fallback=V3(def["frame_local"]);derived=false;if(hub==null)return fallback;
        Vector3 world=Vector3.zero;bool found=false;
        if(id=="campfire"&&hub.fireLight!=null){world=hub.fireLight.transform.position;found=true;}
        else if(id.StartsWith("gate-")&&hub.barrierLeftLeaves!=null)
        {
            int g=id[5]-'a';
            if(g>=0&&g<hub.barrierLeftLeaves.Length&&hub.barrierLeftLeaves[g]!=null&&hub.barrierLeftLeaves[g].parent!=null){world=hub.barrierLeftLeaves[g].parent.TransformPoint(new Vector3(0,1.1f,0));found=true;}
        }
        else if(id=="archive-desk"&&hub.maintenanceRoot!=null){world=hub.maintenanceRoot.TransformPoint(new Vector3(0,.8f,0));found=true;}
        if(!found)return fallback;
        Vector3 local=frame.InverseTransformPoint(world);
        // The scene may be posed at a late time (conditions root retreating): accept the real object only when it is where the score puts it.
        if(Vector3.Distance(local,fallback)>1.0f)return fallback;
        derived=true;return local;
    }

    public static object Build()
    {
        Guard();var data=Load();var d=F<OWEpisodeDirector>();var journey=F<OWV4Journey>();var foley=F<OWV4Foley>();var hub=F<OWV4HubCast>();
        if(d==null||journey==null||foley==null||journey.worldFrame==null)throw new InvalidOperationException("V4 director, journey and OWV4Foley required");
        var owner=GameObject.Find("OWEpisode123");if(owner==null)throw new InvalidOperationException("OWEpisode123 missing");
        var physical=owner.transform.Find("V4 physical sound sources");if(physical==null)throw new InvalidOperationException("V4 physical sound sources root missing");
        var frame=journey.worldFrame;if(physical.IsChildOf(frame))throw new InvalidOperationException("Physical audio root must stay outside worldFrame");
        var clips=(JArray)data["clips"];var defs=(JArray)data["emitters"];var events=(JArray)data["events"];
        double previous=-1;
        foreach(JObject e in events)
        {
            double t=(double)e["time"];int clip=(int)e["clip"],emitter=(int)e["emitter"];
            if(Math.Abs(t/.08-Math.Round(t/.08))>=1e-4||t<previous||clip<0||clip>=clips.Count||emitter<0||emitter>=defs.Count)throw new InvalidOperationException("Invalid Foley event in data: "+e.ToString(Formatting.None));
            previous=t;
        }
        var template=owner.GetComponentsInChildren<AudioSource>(true).FirstOrDefault(a=>a.name=="Original mouse_click");
        var root=physical.Find(SourceRootName);if(root==null)root=Child(SourceRootName,physical);
        root.localPosition=Vector3.zero;root.localRotation=Quaternion.identity;root.localScale=Vector3.one;
        var wanted=new HashSet<string>(clips.Select(c=>SourcePrefix+(string)c["id"]));var stale=new List<GameObject>();
        foreach(Transform child in root)if(child.name.StartsWith(SourcePrefix)&&!wanted.Contains(child.name))stale.Add(child.gameObject);
        foreach(var go in stale)Object.DestroyImmediate(go);
        var sources=new AudioSource[clips.Count];
        for(int i=0;i<clips.Count;i++)
        {
            var c=(JObject)clips[i];string id=(string)c["id"];var t=root.Find(SourcePrefix+id);if(t==null)t=Child(SourcePrefix+id,root);
            t.localPosition=Vector3.zero;var source=t.gameObject.GetComponent<AudioSource>();if(source==null)source=t.gameObject.AddComponent<AudioSource>();
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot+"/"+(string)c["file"]);if(clip==null)throw new InvalidOperationException("Foley clip not imported: "+(string)c["file"]);
            source.clip=clip;source.playOnAwake=false;source.loop=false;source.mute=false;source.volume=(float)c["volume"];
            ApplyTemplate(source,template);CopySpatial(template!=null?template.gameObject:null,t.gameObject);
            sources[i]=source;EditorUtility.SetDirty(source);EditorUtility.SetDirty(t.gameObject);
        }
        // Emitters: 0/1 are the existing V4 content/keyboard points, 2 is outside the frame, the rest ride inside worldFrame
        // (OWV4Foley samples emitters[i].position at the onset; no event may fire during a journey).
        var emitterRoot=frame.Find(EmitterRootName);if(emitterRoot==null)emitterRoot=Child(EmitterRootName,frame);
        emitterRoot.localPosition=Vector3.zero;emitterRoot.localRotation=Quaternion.identity;emitterRoot.localScale=Vector3.one;
        var old=foley.emitters;var emitters=new Transform[defs.Count];var emitterRows=new List<object>();
        for(int i=0;i<defs.Count;i++)
        {
            var def=(JObject)defs[i];string id=(string)def["id"],kind=(string)def["kind"];Transform t=null;bool derived=false;
            if(kind=="existing")
            {
                t=old!=null&&i<old.Length?old[i]:null;
                if(t==null){t=root.Find(EmitterPrefix+id);if(t==null)t=Child(EmitterPrefix+id,root);t.position=V3(def["physical"]);}
            }
            else if(kind=="physical"){t=root.Find(EmitterPrefix+id);if(t==null)t=Child(EmitterPrefix+id,root);t.position=V3(def["physical"]);}
            else{t=emitterRoot.Find(id);if(t==null)t=Child(id,emitterRoot);t.localPosition=FrameLocal(frame,id,def,hub,out derived);}
            emitters[i]=t;EditorUtility.SetDirty(t.gameObject);
            emitterRows.Add(new{id,kind,path=HierarchyPath(t,owner.transform),insideWorldFrame=t.IsChildOf(frame),positionFromSceneObject=derived,frameLocal=Arr(frame.InverseTransformPoint(t.position))});
        }
        foley.director=d;foley.journey=journey;foley.sources=sources;foley.emitters=emitters;
        foley.eventTimes=events.Select(e=>(float)e["time"]).ToArray();foley.eventClips=events.Select(e=>(int)e["clip"]).ToArray();foley.eventEmitters=events.Select(e=>(int)e["emitter"]).ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(foley);var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(foley);if(vm!=null)EditorUtility.SetDirty(vm);EditorUtility.SetDirty(foley);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("foley-build.json",new{success=true,sceneSaved=false,sources=sources.Length,events=foley.eventTimes.Length,emitters=emitters.Length,spatialTemplate=template!=null?"V4 Original mouse_click":"fallback Gain 10 / Near 0 / Far 80",
            v4SourcesLeftIdle=owner.GetComponentsInChildren<AudioSource>(true).Count(a=>a.name.StartsWith("Original ")),emitterRows});
    }
    static string HierarchyPath(Transform t,Transform stop){var parts=new List<string>();while(t!=null&&t!=stop){parts.Insert(0,t.name);t=t.parent;}return string.Join("/",parts);}
    static float ClipLength(AudioSource s){return s!=null&&s.clip!=null?s.clip.length:0f;}
    static Vector3 PhysicalAt(Transform emitter,float time,Transform frame,OWV4Journey journey)
    {
        if(emitter.IsChildOf(frame))
        {
            Vector3 local=frame.InverseTransformPoint(emitter.position)-journey.VirtualPosition(time);
            return frame.parent!=null?frame.parent.TransformPoint(local):local;
        }
        return emitter.position;
    }
    static object Fail(List<string> errors,object extra)
    {
        string json=JsonConvert.SerializeObject(new{success=false,errors,extra},Formatting.Indented);
        string dir=Path.Combine(Tmp,"reports");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"foley-validate.json"),json);
        throw new InvalidOperationException(json);
    }

    public static object Validate()
    {
        Guard();var data=Load();var errors=new List<string>();
        var d=F<OWEpisodeDirector>();var journey=F<OWV4Journey>();var foleyProxy=F<OWV4Foley>();var hub=F<OWV4HubCast>();
        if(d==null||journey==null||foleyProxy==null||journey.worldFrame==null)return Fail(new List<string>{"V4 director, journey and OWV4Foley required"},null);
        // Other validators leave the cast posed at arbitrary times; compare emitters in the saved t=0 pose.
        journey.Sample(0);if(hub!=null)hub.Sample(0);
        var owner=GameObject.Find("OWEpisode123");var physical=owner!=null?owner.transform.Find("V4 physical sound sources"):null;var frame=journey.worldFrame;
        var clips=(JArray)data["clips"];var events=(JArray)data["events"];var defs=(JArray)data["emitters"];
        string[] names=clips.Select(c=>(string)c["id"]).ToArray();string[] emitterIds=defs.Select(e=>(string)e["id"]).ToArray();
        // Read what is persisted in the Udon program; fall back to the proxy only if the VM does not expose the field.
        var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(foleyProxy);
        var sources=(vm!=null?vm.GetProgramVariable("sources") as AudioSource[]:null)??foleyProxy.sources;
        var times=(vm!=null?vm.GetProgramVariable("eventTimes") as float[]:null)??foleyProxy.eventTimes;
        var eventClips=(vm!=null?vm.GetProgramVariable("eventClips") as int[]:null)??foleyProxy.eventClips;
        var eventEmitters=(vm!=null?vm.GetProgramVariable("eventEmitters") as int[]:null)??foleyProxy.eventEmitters;
        var emitters=(vm!=null?vm.GetProgramVariable("emitters") as Transform[]:null)??foleyProxy.emitters;
        void Need(bool ok,string message){if(!ok&&errors.Count<80)errors.Add(message);}
        Need(physical!=null&&!physical.IsChildOf(frame),"V4 physical sound sources root missing or inside worldFrame");
        Need(sources!=null&&sources.Length==names.Length,"source count "+(sources==null?-1:sources.Length)+" != "+names.Length);
        Need(emitters!=null&&emitters.Length==defs.Count,"emitter count "+(emitters==null?-1:emitters.Length)+" != "+defs.Count);
        Need(times!=null&&eventClips!=null&&eventEmitters!=null&&times.Length==events.Count&&eventClips.Length==events.Count&&eventEmitters.Length==events.Count,"event arrays do not match the data file");
        if(errors.Count>0)return Fail(errors,null);
        var spatial=SpatialType();var perClip=new int[names.Length];
        for(int i=0;i<names.Length;i++)
        {
            var c=(JObject)clips[i];var s=sources[i];Need(s!=null,"source "+names[i]+" missing");if(s==null)continue;var clip=s.clip;string path=AudioRoot+"/"+(string)c["file"];
            Need(clip!=null&&EditorUtility.IsPersistent(clip)&&AssetDatabase.GetAssetPath(clip)==path,names[i]+": clip not the persisted asset "+path);
            if(clip!=null)Need(clip.channels==1&&clip.frequency==48000&&Mathf.Abs(clip.length-(float)c["duration"])<.01f,names[i]+": clip format/length differs from the data");
            var importer=AssetImporter.GetAtPath(path) as AudioImporter;
            Need(importer!=null&&importer.forceToMono&&!importer.loadInBackground&&importer.defaultSampleSettings.preloadAudioData,names[i]+": importer not mono/preload/foreground");
            Need(!s.playOnAwake&&!s.loop&&!s.mute&&Mathf.Abs(s.volume-(float)c["volume"])<1e-4f,names[i]+": playOnAwake/loop/mute/volume wrong (volume "+s.volume+" vs "+(float)c["volume"]+")");
            Need(s.spatialize&&Mathf.Approximately(s.spatialBlend,1f)&&s.dopplerLevel==0f,names[i]+": not a spatialised, Doppler-free one-shot source");
            Need(physical!=null&&s.transform.IsChildOf(physical)&&!s.transform.IsChildOf(frame),names[i]+": source must live under the physical root, outside worldFrame");
            Need(spatial==null||s.GetComponent(spatial)!=null,names[i]+": VRCSpatialAudioSource missing");
        }
        // Music is untouched: four FC14 voices, none of them one of ours.
        var music=d.music!=null?d.music.sources:null;Need(music!=null&&music.Length==4,"four FC14 voices required");
        if(music!=null&&music.Length==4)for(int i=0;i<4;i++)
        {
            Need(music[i]!=null&&music[i].clip!=null&&music[i].clip.name=="FC14_Mono_"+(i+1).ToString("00"),"FC14 voice "+(i+1)+" clip changed");
            Need(music[i]==null||!sources.Contains(music[i]),"FC14 voice "+(i+1)+" is listed as a foley source");
        }
        Need(Vector3.Distance(foleyProxy.contentWorldPosition,new Vector3(0,2.1f,5.6f))<.001f,"contentWorldPosition changed");
        // Events
        int mech=0,laptop=0,air=0,fire=0;double previous=-1;var firstUse=new Dictionary<int,float>();var lastFire=0.0;
        for(int k=0;k<times.Length;k++)
        {
            double t=times[k];int clipIndex=eventClips[k],emitterIndex=eventEmitters[k];var row=(JObject)events[k];
            Need(Math.Abs(t-(double)row["time"])<1e-4&&clipIndex==(int)row["clip"]&&emitterIndex==(int)row["emitter"],"event "+k+" differs from the data file");
            Need(Math.Abs(t/.08-Math.Round(t/.08))<1e-4,"event "+k+" ("+t+") is off the 80 ms grid");
            Need(t>=previous&&t>=0&&t<133.12,"event "+k+" is unsorted or outside the show");previous=t;
            bool okIndex=clipIndex>=0&&clipIndex<sources.Length&&sources[clipIndex]!=null&&emitterIndex>=0&&emitterIndex<emitters.Length&&emitters[emitterIndex]!=null;
            Need(okIndex,"event "+k+" has an invalid clip/emitter index");if(!okIndex)continue;
            perClip[clipIndex]++;if(!firstUse.ContainsKey(emitterIndex))firstUse[emitterIndex]=(float)t;
            string name=names[clipIndex];bool isAir=name.StartsWith("journey_air");
            if(!isAir)for(int j=0;j<3;j++)Need(!(t>=JourneyStarts[j]-1e-4&&t<=JourneyEnds[j]+1e-4),"event "+k+" "+name+" at "+t+" is inside journey "+(j+1));
            if(name.StartsWith("keyboard_mech")){mech++;Need(t<51.20-1e-4,"mechanical keyboard at "+t+" after 51.20");}
            if(name.StartsWith("keyboard_laptop")){laptop++;Need(t>=51.20-1e-4,"laptop keyboard at "+t+" before 51.20");}
            if(isAir){air++;Need(emitterIds[emitterIndex]=="journey-air","journey_air must use the journey-air emitter");}
            if(name.StartsWith("campfire_crackle"))
            {
                fire++;Need(emitterIds[emitterIndex]=="campfire","campfire crackle must use the campfire emitter");
                Need(t>=88.80-1e-4&&t+ClipLength(sources[clipIndex])<=101.92+.005,"campfire crackle "+t+" runs outside 88.80-101.92");lastFire=Math.Max(lastFire,t+ClipLength(sources[clipIndex]));
            }
            if(name=="crt_power_on")Need(Math.Abs(t-0.08)<1e-4,"crt_power_on must sit at 0.08");
            if(name=="modem_handshake")Need(Math.Abs(t-1.28)<1e-4,"modem_handshake must sit at 1.28");
        }
        for(int j=0;j<3;j++)
        {
            int count=0;for(int k=0;k<times.Length;k++)if(eventClips[k]>=0&&eventClips[k]<names.Length&&names[eventClips[k]].StartsWith("journey_air")&&Math.Abs(times[k]-(JourneyStarts[j]-.24f))<1e-4)count++;
            Need(count==1,"journey "+(j+1)+" needs exactly one journey_air starting 0.24 s early (found "+count+")");
        }
        Need(air==3&&fire==2,"expected 3 journey_air and 2 campfire events, found "+air+"/"+fire);
        // Emitters: where each one really is at the onset, and whether it matches the scene object it represents.
        var emitterPhysical=new Dictionary<string,float[]>();var emitterFrameLocal=new Dictionary<string,float[]>();
        for(int i=0;i<emitters.Length;i++)
        {
            float t=firstUse.ContainsKey(i)?firstUse[i]:0f;Vector3 p=i==0?foleyProxy.contentWorldPosition:PhysicalAt(emitters[i],t,frame,journey);// OWV4Foley reads contentWorldPosition for emitter 0
            emitterPhysical[emitterIds[i]]=Arr(p);emitterFrameLocal[emitterIds[i]]=Arr(frame.InverseTransformPoint(emitters[i].position));
            string kind=(string)defs[i]["kind"];if(kind=="frame")Need(emitters[i].IsChildOf(frame),emitterIds[i]+" must ride inside worldFrame");else if(kind=="physical")Need(!emitters[i].IsChildOf(frame),emitterIds[i]+" must stay outside worldFrame");
            if(kind=="frame"&&defs[i]["region"]!=null)
            {
                Vector3 expected=V3(defs[i]["frame_local"]);Need(Vector3.Distance(frame.InverseTransformPoint(emitters[i].position),expected)<1.2f,emitterIds[i]+" is more than 1.2 m from the planned position");
                float regionZ=(float)data["region_offsets_z"][((int)defs[i]["region"]).ToString()];
                Need(Mathf.Abs(p.z-(expected.z-regionZ))<1.2f,emitterIds[i]+" physical depth does not match its region at first use");
            }
        }
        if(hub!=null)
        {
            int campfire=Array.IndexOf(emitterIds,"campfire");if(campfire>=0&&hub.fireLight!=null&&emitters[campfire]!=null)Need(Vector3.Distance(emitters[campfire].position,hub.fireLight.transform.position)<1.2f,"campfire emitter is not at the fire (scene must be posed at its saved state)");
            for(int g=0;g<3;g++){int gi=Array.IndexOf(emitterIds,"gate-"+(char)('a'+g));if(gi>=0&&hub.barrierLeftLeaves!=null&&g<hub.barrierLeftLeaves.Length&&hub.barrierLeftLeaves[g]!=null&&hub.barrierLeftLeaves[g].parent!=null)Need(Vector3.Distance(emitters[gi].position,hub.barrierLeftLeaves[g].parent.position)<2.0f,"gate emitter "+g+" is not at its gate");}
            int archive=Array.IndexOf(emitterIds,"archive-desk");if(archive>=0&&hub.maintenanceRoot!=null)Need(Vector3.Distance(emitters[archive].position,hub.maintenanceRoot.position)<2.0f,"archive emitter is not at the maintenance desk");
        }
        if(errors.Count>0)return Fail(errors,new{emitterPhysical,perClip});
        var counts=new Dictionary<string,int>();for(int i=0;i<names.Length;i++)counts[names[i]]=perClip[i];
        return Report("foley-validate.json",new{success=true,scene=ScenePath,persistedFromUdon=vm!=null,sources=sources.Length,events=times.Length,emitters=emitters.Length,mechanicalKeys=mech,laptopKeys=laptop,journeyAir=air,campfire=fire,campfireEnd=lastFire,
            gridChecked=times.Length,musicVoicesUntouched=true,emitterPhysical,emitterFrameLocal,counts});
    }
}
