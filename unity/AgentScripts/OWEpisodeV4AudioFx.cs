using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UdonSharp;
using UdonSharpEditor;
using OldWebMap;
using Object = UnityEngine.Object;

// Run only through Pipeline. All serialized content is authored and persisted here.
public static class OWEpisodeV4AudioFx
{
    const string ScenePath = "Assets/Scenes/OWM_Episode123_v4.unity";
    const string Root = "Assets/OldWebMap/Episode123/Revision4";
    const string DataPath = "AgentScripts/data/episode123-v4-waveforms.json";
    const string AudioDataPath = "AgentScripts/data/episode123-audio.json";
    static readonly string[] Colors = {"#53d2e2", "#edbd67", "#bf90ef", "#86cf83"};
    static string ReportDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp/vrc-old-web-map/episode123-v4/wave-fx");
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        if (slash < 0) throw new InvalidOperationException("Missing asset root: " + path);
        Folder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
    static GameObject Child(string name, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go;
    }
    static Color Hex(string value) { Color color; ColorUtility.TryParseHtmlString(value, out color); return color; }
    static Material MaterialAsset(string name, string shaderName)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find(shaderName);
        if (shader == null || !shader.isSupported) throw new InvalidOperationException("Required shader unavailable: " + shaderName);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        else mat.shader = shader;
        mat.enableInstancing = true; EditorUtility.SetDirty(mat); return mat;
    }
    static Mesh MeshAsset(string name, int columns, bool screen)
    {
        string path = Root + "/Meshes/" + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh(); mesh.name = name; AssetDatabase.CreateAsset(mesh, path); }
        else mesh.Clear();
        if (screen)
        {
            mesh.vertices = new[] {new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0)};
            mesh.uv = new[] {Vector2.zero,Vector2.right,Vector2.up,Vector2.one};
            mesh.triangles = new[] {0,2,1,2,3,1};
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 20);
        }
        else
        {
            var vertices = new Vector3[columns * 2]; var uv = new Vector2[vertices.Length];
            var indices = new int[(columns - 1) * 6];
            for (int i = 0; i < columns; i++)
            {
                float u = i / (float)(columns - 1);
                vertices[i*2] = new Vector3(u-.5f,-1,0); vertices[i*2+1] = new Vector3(u-.5f,1,0);
                uv[i*2] = new Vector2(u,0); uv[i*2+1] = new Vector2(u,1);
                if (i == columns - 1) continue;
                int at = i*6, a=i*2;
                indices[at]=a; indices[at+1]=a+1; indices[at+2]=a+2;
                indices[at+3]=a+2; indices[at+4]=a+1; indices[at+5]=a+3;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = indices;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(18,8,10));
        }
        EditorUtility.SetDirty(mesh); return mesh;
    }
    static MeshRenderer Surface(string name, Transform parent, Mesh mesh, Material material)
    {
        var go = Child(name, parent); go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.allowOcclusionWhenDynamic = false; return renderer;
    }
    static Texture2D ImportAtlas(JObject data)
    {
        string path = Root + "/Data/" + (string)data["atlas"];
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("Copy the generated waveform PNG before Build", path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false; importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false; importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = (int)data["atlas_height"]; importer.isReadable = false;
        // Existing platform overrides can silently discard the alpha voice or
        // reduce rows. Force the active PC import to uncompressed four-channel RGBA.
        var settings = importer.GetPlatformTextureSettings("Standalone");
        settings.name = "Standalone"; settings.overridden = true; settings.format = TextureImporterFormat.RGBA32;
        settings.maxTextureSize = (int)data["atlas_height"]; settings.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(settings); importer.SaveAndReimport();
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (atlas == null || atlas.width != (int)data["columns"] || atlas.height != (int)data["atlas_height"])
            throw new InvalidOperationException("Waveform atlas dimensions changed during import");
        return atlas;
    }
    static Gradient FadeGradient(Color color)
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] {new GradientColorKey(color,0), new GradientColorKey(color,.7f),new GradientColorKey(Color.white,1)},
            new[] {new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.08f),new GradientAlphaKey(.35f,.55f),new GradientAlphaKey(0,1)});
        return gradient;
    }
    // ParticleSystem rateOverTime curves are evaluated over normalized system
    // duration. Keep the score in seconds while baking, then normalize every key.
    // Explicit linear tangents prevent cubic overshoot at sharp note attacks.
    static AnimationCurve NormalizedEmissionCurve(IEnumerable<Keyframe> seconds, float duration)
    {
        var source = seconds.OrderBy(k=>k.time).ToArray();
        var keys = new Keyframe[source.Length];
        for(int i=0;i<source.Length;i++)
        {
            float time=Mathf.Clamp01(source[i].time/duration);
            float incoming=i==0?0:(source[i].value-source[i-1].value)*duration/Mathf.Max(.00001f,source[i].time-source[i-1].time);
            float outgoing=i==source.Length-1?0:(source[i+1].value-source[i].value)*duration/Mathf.Max(.00001f,source[i+1].time-source[i].time);
            keys[i]=new Keyframe(time,Mathf.Max(0,source[i].value),incoming,outgoing);
        }
        var curve=new AnimationCurve(keys);curve.preWrapMode=WrapMode.ClampForever;curve.postWrapMode=WrapMode.ClampForever;return curve;
    }
    static AnimationCurve VoiceRateCurve(float[] energy,float fps,float duration,float[] notes)
    {
        var times=new SortedSet<float>();
        Action<float> add=time=>{if(time>=0 && time<=duration)times.Add(Mathf.Round(time*10000f)/10000f);};
        add(0);add(duration);
        // RMS baseline is sampled at the same 30 Hz as the actual wave atlas.
        for(int frame=0;frame<energy.Length;frame++)add(frame/fps);
        // Bake every real note at its precise score time. The 1 ms leading key
        // preserves the onset edge; the following knots integrate a short release.
        // No Burst array: Unity 2022 actually truncates SetBursts to eight entries.
        foreach(float note in notes)
        {
            add(note-.001f);add(note);add(note+.025f);add(note+.06f);
            add(note+.12f);add(note+.20f);add(note+.32f);
        }
        var keys=new List<Keyframe>();int latest=-1;
        foreach(float time in times)
        {
            while(latest+1<notes.Length && notes[latest+1]<=time+.00002f)latest++;
            float notePulse=0;
            for(int index=latest;index>=0 && time-notes[index]<.32f;index--)
                notePulse+=Mathf.Exp(-Mathf.Max(0,time-notes[index])*18f);
            float sample=Mathf.Clamp(time*fps,0,energy.Length-1);int frame=Mathf.FloorToInt(sample);
            float rms=Mathf.Lerp(energy[frame],energy[Mathf.Min(frame+1,energy.Length-1)],sample-frame);
            float open=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-81.92f)/2.56f))*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((time-89.6f)/2.56f)));
            float fade=1-Mathf.Clamp01((time-duration+1.8f)/1.8f);
            // ~3 particles of onset integral in normal sections, ~7 at the climax,
            // plus a true-RMS floor for sustained notes. Silence stays quiet.
            float rate=(rms*(12+open*34)+notePulse*(56+open*66)*Mathf.Clamp01(rms/.18f))*fade;
            keys.Add(new Keyframe(time,time>=duration?0:rate));
        }
        return NormalizedEmissionCurve(keys,duration);
    }
    static ParticleSystem VoiceParticles(int channel, Transform parent, Material material, float[] energy, float fps, float duration, float[] notes)
    {
        var go = Child("CH0"+(channel+1)+" actual-note signal particles", parent);
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.useAutoRandomSeed = false; ps.randomSeed = (uint)(18123 + channel*239);
        var main = ps.main;
        main.duration = duration; main.loop = false; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; main.simulationSpeed = 1;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.65f,1.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.12f,.75f);
        main.startSize = new ParticleSystem.MinMaxCurve(.045f,.1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);
        main.startColor = Color.white; main.gravityModifier = -.012f;
        main.maxParticles = 220; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .26f;
        var emission = ps.emission; emission.enabled = true;emission.burstCount=0;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(1,VoiceRateCurve(energy,fps,duration,notes));
        var size = ps.sizeOverLifetime; size.enabled=true;
        size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.4f),new Keyframe(.2f,1),new Keyframe(1,0)));
        var color = ps.colorOverLifetime; color.enabled=true; color.color = new ParticleSystem.MinMaxGradient(FadeGradient(Hex(Colors[channel])));
        var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=material;
        renderer.renderMode=ParticleSystemRenderMode.Billboard; renderer.alignment=ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        renderer.lightProbeUsage=LightProbeUsage.Off; renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        renderer.sortingFudge=1; return ps;
    }
    static ParticleSystem AmbientParticles(Transform parent, Material material, float duration)
    {
        var go=Child("Persistent data dust - authored particle system",parent);go.transform.position=new Vector3(0,2.2f,4);
        var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.useAutoRandomSeed=false;ps.randomSeed=41023;
        var main=ps.main;main.duration=duration;main.loop=false;main.playOnAwake=false;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startLifetime=new ParticleSystem.MinMaxCurve(2.5f,5.2f);
        main.startSize=new ParticleSystem.MinMaxCurve(.025f,.065f);main.startSpeed=new ParticleSystem.MinMaxCurve(.015f,.09f);
        main.startColor=new Color(.46f,.72f,.83f,.42f);main.maxParticles=180;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(13,5.5f,9);
        var emission=ps.emission;emission.enabled=true;emission.burstCount=0;
        emission.rateOverTime=new ParticleSystem.MinMaxCurve(1,NormalizedEmissionCurve(new[]{
            new Keyframe(0,0),new Keyframe(1.28f,12),new Keyframe(30.72f,16),new Keyframe(81.92f,18),new Keyframe(84.48f,28),new Keyframe(92.16f,12),new Keyframe(102.4f,9),new Keyframe(128,4),new Keyframe(duration,0)},duration));
        var color=ps.colorOverLifetime;color.enabled=true;color.color=new ParticleSystem.MinMaxGradient(FadeGradient(new Color(.5f,.73f,.85f)));
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return ps;
    }
    static void RetireOldMeters(OWEpisodeDirector director)
    {
        var music=director.music;
        if(music.meterBars!=null)foreach(var meter in music.meterBars)if(meter!=null)meter.gameObject.SetActive(false);
        if(music.pulseGlyphs!=null)foreach(var glyph in music.pulseGlyphs)if(glyph!=null)glyph.gameObject.SetActive(false);
        var montage=director.montage;
        if(montage==null)return;
        if(montage.spectrumBars!=null)foreach(var bar in montage.spectrumBars)if(bar!=null)bar.gameObject.SetActive(false);
        if(montage.energyObjects!=null)foreach(var cube in montage.energyObjects)if(cube!=null)cube.gameObject.SetActive(false);
        if(montage.channelLights!=null)foreach(var light in montage.channelLights)if(light!=null)light.enabled=false;
    }
    public static object Build()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path!=ScenePath)
            throw new InvalidOperationException("Build AudioFx only in the stopped V4 scene after compilation");
        var director=Object.FindObjectOfType<OWEpisodeDirector>();
        if(director==null || director.music==null || director.music.sources==null || director.music.sources.Length!=4)
            throw new InvalidOperationException("V3 scene must contain director and four real stem sources");
        Folder(Root+"/Meshes");Folder(Root+"/Materials");Folder(Root+"/Data");
        var data=JObject.Parse(File.ReadAllText(DataPath));var audio=JObject.Parse(File.ReadAllText(AudioDataPath));
        var atlas=ImportAtlas(data);var mesh=MeshAsset("Triggered waveform ribbon",(int)data["columns"],false);var screen=MeshAsset("Per eye screen surface",4,true);
        var episode=GameObject.Find("OWEpisode123");
        var journey=Object.FindObjectOfType<OWV4Journey>();if(journey==null||journey.worldFrame==null)throw new InvalidOperationException("V4 Journey required");
        var owner=journey.worldFrame;
        var previous=owner.Find("Revision4AudioFx");
        var previousOsc=previous!=null?previous.GetComponentInChildren<OWV4Oscilloscope>(true):null;
        var previousFx=previous!=null?previous.GetComponentInChildren<OWV4CameraFx>(true):null;
        Vector3 fused=previousOsc!=null?previousOsc.fusedCenter:new Vector3(0,2.55f,5.8f);
        Vector3 orbit=previousOsc!=null?previousOsc.orbitCenter:new Vector3(0,1.65f,-1.4f);
        float radius=previousOsc!=null?previousOsc.orbitRadius:4.2f,gain=previousOsc!=null?previousOsc.masterGain:1;
        float fxGain=previousFx!=null?previousFx.masterIntensity:1;
        if(previous!=null)Object.DestroyImmediate(previous.gameObject);
        var root=Child("Revision4AudioFx",owner).transform;
        var osc=Child("Real four-channel oscilloscope",root).AddUdonSharpComponent<OWV4Oscilloscope>();
        osc.director=director;osc.journey=journey;osc.music=director.music;osc.duration=(float)data["duration"];osc.frameRate=(float)data["fps"];
        osc.fusedCenter=new Vector3(0,4.25f,8.2f);osc.orbitCenter=new Vector3(0,1.65f,-2.8f);osc.orbitRadius=radius;osc.masterGain=gain;
        osc.frameCount=(int)data["frames"];osc.atlasHeight=(int)data["atlas_height"];
        osc.waves=new Transform[4];osc.waveRenderers=new Renderer[4];osc.waveMaterials=new Material[4];osc.particles=new ParticleSystem[5];osc.particleAnchors=new Transform[4];
        osc.energy0=data["energy"][0].ToObject<float[]>();osc.energy1=data["energy"][1].ToObject<float[]>();osc.energy2=data["energy"][2].ToObject<float[]>();osc.energy3=data["energy"][3].ToObject<float[]>();
        float[][] energy={osc.energy0,osc.energy1,osc.energy2,osc.energy3};
        var particleMat=MaterialAsset("Phosphor sparks","OldWebMap/V3/Signal Particles");particleMat.SetFloat("_Shape",0);
        var dustMat=MaterialAsset("Data square motes","OldWebMap/V3/Signal Particles");dustMat.SetFloat("_Shape",1);
        for(int ch=0;ch<4;ch++)
        {
            var mat=MaterialAsset("Actual waveform CH0"+(ch+1),"OldWebMap/V3/Triggered Oscilloscope");
            mat.SetTexture("_WaveAtlas",atlas);mat.SetFloat("_Channel",ch);mat.SetColor("_Color",Hex(Colors[ch]));mat.SetFloat("_Width",.04f);
            var renderer=Surface("CH0"+(ch+1)+" phase-triggered PCM ribbon",osc.transform,mesh,mat);
            osc.waves[ch]=renderer.transform;osc.waveRenderers[ch]=renderer;osc.waveMaterials[ch]=mat;
            var anchor=Child("CH0"+(ch+1)+" particle anchor",osc.transform).transform;osc.particleAnchors[ch]=anchor;
            osc.particles[ch]=VoiceParticles(ch,anchor,particleMat,energy[ch],osc.frameRate,osc.duration,audio["notes"][ch].ToObject<float[]>());
            var source=director.music.sources[ch];
            if(source==null || source.clip==null)throw new InvalidOperationException("Real audio clip missing for channel "+ch);
            source.spatialize=true;source.dopplerLevel=0;source.spread=18;source.minDistance=30;source.maxDistance=150;
        }
        osc.regionalWaves=new Transform[16];osc.regionalRenderers=new Renderer[16];
        for(int region=0;region<4;region++)for(int ch=0;ch<4;ch++)
        {
            Renderer renderer=region==0?osc.waveRenderers[ch]:Surface("CH0"+(ch+1)+" destination "+region+" PCM ribbon",osc.transform,mesh,osc.waveMaterials[ch]);
            osc.regionalWaves[region*4+ch]=renderer.transform;osc.regionalRenderers[region*4+ch]=renderer;
        }
        osc.particles[4]=AmbientParticles(root,dustMat,osc.duration);
        var regionalParticles=new System.Collections.Generic.List<ParticleSystem>(osc.particles);
        for(int region=1;region<4;region++)
        {
            var dust=Object.Instantiate(osc.particles[4],root);dust.name="Destination data dust "+region;dust.transform.localPosition=osc.particles[4].transform.localPosition+journey.RegionOffset(region);regionalParticles.Add(dust);
        }
        osc.particles=regionalParticles.ToArray();
        director.music.externalVoicePositionDriver=true;
        RetireOldMeters(director);
        var fxMaterial=MaterialAsset("Local camera signal","OldWebMap/V3/Local Camera Signal");fxMaterial.enableInstancing=false;
        var fxRenderer=Surface("Primary view screen effect - never locks head",root,screen,fxMaterial);
        var fx=fxRenderer.gameObject.AddUdonSharpComponent<OWV4CameraFx>();fx.director=director;fx.overlayRenderer=fxRenderer;fx.effectMaterial=fxMaterial;
        fx.transform.SetParent(episode.transform,false);
        fx.fallbackCamera=Camera.main!=null?Camera.main.transform:null;fx.masterIntensity=fxGain;
        ConfigureStableSound();fx.Sample(0);
        foreach(var behaviour in root.GetComponentsInChildren<UdonSharpBehaviour>(true))
        {UdonSharpEditorUtility.CopyProxyToUdon(behaviour);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour));}
        UdonSharpEditorUtility.CopyProxyToUdon(director.music);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(director.music));
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        // Main V4 builder owns the final scene save after integrating all modules.
        return JsonConvert.SerializeObject(new {scene=ScenePath,root=root.name,waves=4,realStemClips=director.music.sources.Select(s=>AssetDatabase.GetAssetPath(s.clip)).ToArray(),particles=osc.particles.Length,atlas=AssetDatabase.GetAssetPath(atlas),atlasBytes=atlas.width*atlas.height*4,automaticSampling="LateUpdate reads director.Elapsed()",sceneSaved=false});
    }
    // Minimal migration entry for an already integrated scene. This keeps all
    // authored audio clips, volumes, timbres and effect assets intact.
    public static object ConfigureStableSound()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path!=ScenePath)
            throw new InvalidOperationException("Configure stable sound only in the stopped V4 scene after compilation");
        var osc=Object.FindObjectsOfType<OWV4Oscilloscope>(true).FirstOrDefault();
        if(osc==null || osc.music==null || osc.music.sources==null || osc.music.sources.Length!=4)
            throw new InvalidOperationException("Four integrated V4 stem sources required");
        var episode=GameObject.Find("OWEpisode123");if(episode==null)throw new InvalidOperationException("V4 owner missing");
        var physical=episode.transform.Find("V4 physical sound sources");
        if(physical==null)physical=Child("V4 physical sound sources",episode.transform).transform;
        if(osc.journey!=null && osc.journey.worldFrame!=null && physical.IsChildOf(osc.journey.worldFrame))
            throw new InvalidOperationException("Physical audio root must remain outside the travelling world");
        foreach(var source in osc.music.sources)
        {
            if(source==null || source.clip==null)throw new InvalidOperationException("A real music stem is missing");
            source.transform.SetParent(physical,true);source.dopplerLevel=0;EditorUtility.SetDirty(source);
        }
        osc.music.externalVoicePositionDriver=true;
        var foley=Object.FindObjectsOfType<OWV4Foley>(true).FirstOrDefault();
        if(foley!=null)
        {
            foley.contentWorldPosition=new Vector3(0,2.1f,5.6f);
            if(foley.sources!=null)foreach(var source in foley.sources)if(source!=null)
            {source.transform.SetParent(physical,true);EditorUtility.SetDirty(source);}
            UdonSharpEditorUtility.CopyProxyToUdon(foley);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(foley));
        }
        osc.Seek(0);
        foreach(var behaviour in new UdonSharpBehaviour[]{osc,osc.music})
        {UdonSharpEditorUtility.CopyProxyToUdon(behaviour);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour));}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return JsonConvert.SerializeObject(new{positionPolicy="physical world, paused motion clock during virtual travel",musicClipsChanged=false,musicGainChanged=false,
            contentFoleyWorldPosition=foley==null?null:new[]{foley.contentWorldPosition.x,foley.contentWorldPosition.y,foley.contentWorldPosition.z},
            keyboardFoley="actual keyboard world position at each onset; the playing one-shot remains at that physical point",sceneSaved=false});
    }
    public static object Sample(float time)
    {
        var osc=Object.FindObjectOfType<OWV4Oscilloscope>();var fx=Object.FindObjectOfType<OWV4CameraFx>();
        if(osc==null || fx==null)throw new InvalidOperationException("Build AudioFx first");
        osc.Seek(time);fx.Sample(time);return Describe(osc,fx);
    }
    static object Describe(OWV4Oscilloscope osc,OWV4CameraFx fx)
    {
        return new {time=osc.sampledTime,motionTime=osc.sampledMotionTime,open=osc.sampledOpen,frame=osc.sampledFrame,voiceAnchorError=osc.maxVoiceAnchorError,particleReseeks=osc.particleReseeks,
            sourceAnchorErrors=Enumerable.Range(0,4).Select(i=>Vector3.Distance(osc.waves[i].position,osc.music.sources[i].transform.position)).ToArray(),
            voicePositions=osc.waves.Select(t=>new[]{t.position.x,t.position.y,t.position.z}).ToArray(),
            particleCounts=osc.particles.Select(p=>p.particleCount).ToArray(),cameraIntensity=fx.sampledIntensity};
    }
    static string ParticleState(OWV4Oscilloscope osc)
    {
        var rows=new List<object>();
        foreach(var ps in osc.particles)
        {
            var buffer=new ParticleSystem.Particle[ps.main.maxParticles];int count=ps.GetParticles(buffer);
            rows.Add(buffer.Take(count).Select(p=>new {seed=p.randomSeed,life=p.remainingLifetime,position=new[]{p.position.x,p.position.y,p.position.z},velocity=new[]{p.velocity.x,p.velocity.y,p.velocity.z}}).ToArray());
        }
        return JsonConvert.SerializeObject(rows);
    }
    static object ParticleDensityReport(OWV4Oscilloscope osc,bool assertDensity)
    {
        var rateCurves=osc.particles.Select(p=>p.emission.rateOverTime).ToArray();
        for(int i=0;i<osc.particles.Length;i++)
        {
            var curve=rateCurves[i].curve;
            if(osc.particles[i].emission.burstCount!=0 || curve==null || curve.length<2 || Mathf.Abs(curve.keys[0].time)>.00001f || Mathf.Abs(curve.keys[curve.length-1].time-1)>.00001f)
                throw new InvalidOperationException("Particle emission must use normalized 0..1 curves and zero Burst entries: "+i);
        }
        var rows=new List<object>();
        foreach(float time in new[]{6.4f,43.52f,86.4f,112f})
        {
            osc.Seek(time);var counts=osc.particles.Select(p=>p.particleCount).ToArray();
            if(assertDensity && time==6.4f && (counts[0]<3 || counts[2]<3 || counts[3]<3 || counts[4]<15))throw new InvalidOperationException("Intro active voices/dust remain invisible: "+string.Join(",",counts));
            if(assertDensity && time==43.52f && (counts.Take(4).Sum()<35 || counts[4]<20))throw new InvalidOperationException("Normal-section particle density below the audible-score floor: "+string.Join(",",counts));
            if(assertDensity && time==86.4f && (counts.Take(4).Min()<8 || counts.Take(4).Sum()<100 || counts[4]<35))throw new InvalidOperationException("Climax particle density below the audible-score floor: "+string.Join(",",counts));
            rows.Add(new{time,counts,normalizedRates=rateCurves.Select(r=>r.curve.Evaluate(time/osc.duration)*r.curveMultiplier).ToArray(),simulationTimes=osc.particles.Select(p=>p.time).ToArray()});
        }
        float[][] notes={osc.music.notes0,osc.music.notes1,osc.music.notes2,osc.music.notes3};
        float[][] energies={osc.energy0,osc.energy1,osc.energy2,osc.energy3};
        var noteChecks=Enumerable.Range(0,4).Select(ch=>new{channel=ch,notes=notes[ch].Where(n=>n>30.72f && n<osc.duration-2 && energies[ch][Mathf.Clamp(Mathf.FloorToInt(n*osc.frameRate),0,energies[ch].Length-1)]>.25f)
            .Where((n,i)=>i%7==0).Take(6).Select(n=>new{time=n,pre=rateCurves[ch].curve.Evaluate((n-.004f)/osc.duration),peak=rateCurves[ch].curve.Evaluate((n+.003f)/osc.duration),release=rateCurves[ch].curve.Evaluate((n+.12f)/osc.duration)}).ToArray()}).ToArray();
        return new{rows,bursts=osc.particles.Select(p=>p.emission.burstCount).ToArray(),curveKeys=rateCurves.Select(r=>r.curve.length).ToArray(),
            keyRanges=rateCurves.Select(r=>new[]{r.curve.keys[0].time,r.curve.keys[r.curve.length-1].time}).ToArray(),noteChecks,
            noteEmission="Every score onset baked into normalized rate; normal onset integral ~3 particles, climax ~7; AC-RMS sustain floor",densityFloorsChecked=assertDensity};
    }
    public static object ParticleDensity()
    {
        var osc=Object.FindObjectOfType<OWV4Oscilloscope>();if(osc==null)throw new InvalidOperationException("Build AudioFx first");
        var report=ParticleDensityReport(osc,true);Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(Path.Combine(ReportDirectory,"particle-density.json"),JsonConvert.SerializeObject(report,Formatting.Indented));
        osc.Seek(0);return JsonConvert.SerializeObject(report);
    }
    public static object Validate()
    {
        var osc=Object.FindObjectOfType<OWV4Oscilloscope>();var fx=Object.FindObjectOfType<OWV4CameraFx>();
        if(osc==null || fx==null)throw new InvalidOperationException("Build AudioFx first");
        var rows=new List<object>();
        foreach(float time in new[]{0f,6.4f,30.72f,43.52f,81.92f,84.48f,89.6f,92.16f,102.4f,112f,122f,133.12f})
        {
            osc.Seek(time);fx.Sample(time);
            for(int ch=0;ch<4;ch++)if(Vector3.Distance(osc.waves[ch].position,osc.music.sources[ch].transform.position)>.0001f)throw new InvalidOperationException("Sound/wave mismatch");
            if((time<81.92f || time>=92.16f) && osc.sampledOpen>.001f)throw new InvalidOperationException("Unexpected surround outside climax");
            rows.Add(Describe(osc,fx));
        }
        // Identical absolute seeks must produce the same authored particle set.
        osc.Seek(86.4f);var first=osc.particles.Select(p=>p.particleCount).ToArray();string firstState=ParticleState(osc);
        osc.Seek(10.24f);osc.Seek(86.4f);var second=osc.particles.Select(p=>p.particleCount).ToArray();
        if(!first.SequenceEqual(second) || firstState!=ParticleState(osc))throw new InvalidOperationException("Particle positions/seeds/lifetimes are not repeatable after seek");
        foreach(var ps in osc.particles)
        {
            if(ps.useAutoRandomSeed || ps.main.simulationSpace!=ParticleSystemSimulationSpace.Local)throw new InvalidOperationException("Invalid deterministic particle configuration");
        }
        var shaderMessages=new[]{"OldWebMap/V3/Triggered Oscilloscope","OldWebMap/V3/Signal Particles","OldWebMap/V3/Local Camera Signal"}
            .Select(name=>new {name,messages=ShaderUtil.GetShaderMessages(Shader.Find(name)).Select(m=>new{m.message,m.line,severity=m.severity.ToString()}).ToArray()}).ToArray();
        if(shaderMessages.Any(s=>s.messages.Any(m=>m.severity=="Error")))throw new InvalidOperationException("V3 shader compiler errors; inspect shader messages");
        var density=ParticleDensityReport(osc,true);
        var result=new {rows,particleReplayCountsEqual=true,particleReplayStateEqual=true,particleCounts=first,density,shaderMessages,externalVoiceDriver=osc.music.externalVoicePositionDriver,
            persistentWaveMeshes=osc.waveRenderers.All(r=>AssetDatabase.Contains(r.GetComponent<MeshFilter>().sharedMesh)),persistentMaterials=osc.waveMaterials.All(AssetDatabase.Contains),
            particleSystems=osc.particles.Length,stereoVisualCheckStillRequired=true,vrHeadTrackingVisualCheckStillRequired=true};
        Directory.CreateDirectory(ReportDirectory);File.WriteAllText(Path.Combine(ReportDirectory,"audio-fx-validation.json"),JsonConvert.SerializeObject(result,Formatting.Indented));
        osc.Seek(0);fx.Sample(0);return JsonConvert.SerializeObject(result);
    }
}
