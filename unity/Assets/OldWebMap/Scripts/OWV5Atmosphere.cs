using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    // Colour script, sky, three star layers, stable grid floor, focus halo and screen light.
    // Meshes, materials, lights and all arrays are authored by OWEpisodeV5Atmosphere. Everything below is a pure
    // function of absolute performance time; the halo and light are posed from the montage's own focus formula
    // (region-local, parented under the shared world frame), so no LateUpdate ordering is required.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV5Atmosphere : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public OWV4Journey journey;
        public OWV4Montage montage;
        public Transform frameRoot;
        public bool autoSample=true;
        public Material skyMaterial;
        public Material farStarsMaterial;
        public Material midStarsMaterial;
        public Material nearStarsMaterial;
        public Material gridMaterial;
        public Material haloMaterial;
        public Transform haloTransform;
        public Light keyLight;
        public Light fillLight;
        public Light screenLight;
        public float duration=133.12f;
        public float transition=2.56f;
        public float intro=1.28f;
        public float outro=1.28f;
        public Vector3 referenceEye=new Vector3(0,1.35f,-2.8f);
        public float groundY=-.02f;
        // Colour script: keys share one time axis (sky + directional lights).
        public float[] scriptTimes;
        public Color[] skyTop;
        public Color[] skyHorizon;
        public Color[] keyColors;
        public float[] keyIntensities;
        public Color[] fillColors;
        public float[] fillIntensities;
        public Color waitingTop;
        public Color waitingHorizon;
        public Color v4KeyColor;
        public float v4KeyIntensity=.85f;
        public Color v4FillColor;
        public float v4FillIntensity=.28f;
        public float groundDarken=.55f;
        public float starTintMix=.3f;
        public float farWaiting=.35f;
        public float farShowing=1f;
        public float midShowing=.95f;
        public float nearShowing=1f;
        public float gridTintGain=1.6f;
        public float[] gridTimes;
        public float[] gridValues;
        public float gridWaiting=.25f;
        // Journey table (OWV4Journey.VirtualPosition = sum distance * smoothstep((t-start)/duration)).
        public float[] journeyStart;
        public float[] journeyDuration;
        public float[] journeyDistance;
        // Focus images: montage event indices (role==1) and their baked glow colours.
        public int[] focusEvents;
        public Color[] focusColors;
        public float haloScale=1.35f;
        public float haloBack=.05f;
        public float haloPeak=.85f;
        public float haloMinBottom=.75f;
        public float lightPeak=1.6f;
        public float lightRange=7f;
        public float lightTowardEye=1.2f;
        public float poolRadius=3.4f;
        public float poolGain=.3f;
        public int sampledState;
        public float sampledTime;
        public int sampledFocus=-1;
        public float sampledSpeed;
        public float sampledGrid;
        public float sampledHalo;
        public float sampledLight;

        private void Start(){if(director!=null)Sample(director.Elapsed());}
        private void LateUpdate(){if(autoSample && director!=null)Sample(director.Elapsed());}
        public void Seek(float time){Sample(time);}
        public void Waiting(){Apply(0,0);}
        public void Finished(){Apply(duration,2);}
        // 0 waiting, 1 performing, 2 finished. Mid-performance times count as performing even when the director
        // is not running (offline renders); a real waiting/finished director only reports 0 or the end time.
        public void Sample(float time)
        {
            bool playing=director!=null && director.IsPlaying();
            int state=time<duration-.0001f && (playing || time>.0001f)?1:(time>=duration-.01f?2:0);
            Apply(time,state);
        }
        private void Apply(float time,int state)
        {
            sampledState=state;sampledTime=time;
            if(scriptTimes==null || skyTop==null || skyMaterial==null)return;
            bool showing=state==1;
            float lived=0;
            if(showing)lived=Ease(time/Mathf.Max(.01f,intro))*(1-Ease((time-(duration-outro))/Mathf.Max(.01f,outro)));
            float clock=showing?time:Time.time;
            int k=0;
            for(int i=1;i<scriptTimes.Length;i++)if(time>=scriptTimes[i])k=i;
            int j=k>0?k-1:0;
            float p=k>0?Ease((time-scriptTimes[k])/transition):1;
            Color top=Color.Lerp(waitingTop,Color.Lerp(skyTop[j],skyTop[k],p),lived);
            Color hor=Color.Lerp(waitingHorizon,Color.Lerp(skyHorizon[j],skyHorizon[k],p),lived);
            skyMaterial.SetColor("_TopColor",top);
            skyMaterial.SetColor("_HorizonColor",hor);
            skyMaterial.SetColor("_GroundColor",new Color(hor.r*groundDarken,hor.g*groundDarken,hor.b*groundDarken,1));
            // Directional lights: exact V4 values outside the performance.
            Color kc=Color.Lerp(v4KeyColor,Color.Lerp(keyColors[j],keyColors[k],p),lived);
            float ki=Mathf.Lerp(v4KeyIntensity,Mathf.Lerp(keyIntensities[j],keyIntensities[k],p),lived);
            Color fc=Color.Lerp(v4FillColor,Color.Lerp(fillColors[j],fillColors[k],p),lived);
            float fi=Mathf.Lerp(v4FillIntensity,Mathf.Lerp(fillIntensities[j],fillIntensities[k],p),lived);
            if(keyLight!=null){keyLight.color=kc;keyLight.intensity=ki;}
            if(fillLight!=null){fillLight.color=fc;fillLight.intensity=fi;}
            // Stars: layer brightness, horizon-tinted white, journey velocity (derivative of the smoothstep table).
            float peak=Mathf.Max(hor.r,Mathf.Max(hor.g,hor.b));float inv=peak>.001f?1f/peak:1f;
            Color tint=new Color(Mathf.Lerp(1f,hor.r*inv,starTintMix),Mathf.Lerp(1f,hor.g*inv,starTintMix),Mathf.Lerp(1f,hor.b*inv,starTintMix),1);
            float vz=0;
            if(showing && journeyStart!=null)for(int i=0;i<journeyStart.Length;i++){float x=(time-journeyStart[i])/journeyDuration[i];if(x>0 && x<1)vz+=journeyDistance[i]/journeyDuration[i]*6f*x*(1-x);}
            sampledSpeed=vz;
            Vector4 velocity=new Vector4(0,0,vz,vz);
            if(farStarsMaterial!=null){farStarsMaterial.SetFloat("_Timeline",clock);farStarsMaterial.SetFloat("_Intensity",Mathf.Lerp(farWaiting,farShowing,lived));farStarsMaterial.SetColor("_Tint",tint);}
            if(midStarsMaterial!=null){midStarsMaterial.SetFloat("_Timeline",clock);midStarsMaterial.SetFloat("_Intensity",midShowing*lived);midStarsMaterial.SetColor("_Tint",tint);midStarsMaterial.SetVector("_Velocity",velocity);}
            if(nearStarsMaterial!=null){nearStarsMaterial.SetFloat("_Timeline",clock);nearStarsMaterial.SetFloat("_Intensity",nearShowing*lived);nearStarsMaterial.SetColor("_Tint",tint);nearStarsMaterial.SetVector("_Velocity",velocity);}
            // Grid floor opacity: smoothstep between consecutive keys, waiting/finished level outside the performance.
            float opacity=gridWaiting;
            if(gridTimes!=null && gridTimes.Length>1)
            {
                int gi=0;
                for(int i=1;i<gridTimes.Length;i++)if(time>=gridTimes[i])gi=i;
                float g=gridValues[gridTimes.Length-1];
                if(gi<gridTimes.Length-1)g=Mathf.Lerp(gridValues[gi],gridValues[gi+1],Ease((time-gridTimes[gi])/(gridTimes[gi+1]-gridTimes[gi])));
                opacity=Mathf.Lerp(gridWaiting,g,lived);
            }
            sampledGrid=opacity;
            if(gridMaterial!=null)
            {
                gridMaterial.SetFloat("_Timeline",clock);gridMaterial.SetFloat("_Opacity",opacity);
                gridMaterial.SetColor("_Tint",new Color(Mathf.Clamp01(hor.r*gridTintGain+.04f),Mathf.Clamp01(hor.g*gridTintGain+.04f),Mathf.Clamp01(hor.b*gridTintGain+.04f),1));
            }
            int active=-1;
            if(showing && montage!=null && focusEvents!=null && focusColors!=null)
            {
                float[] starts=montage.starts;float[] ends=montage.ends;
                for(int i=0;i<focusEvents.Length;i++){int e=focusEvents[i];if(time>=starts[e] && time<ends[e]){active=i;break;}}
            }
            sampledFocus=active;
            if(active<0)HideFocus();else PlaceFocus(active,time);
        }
        private void HideFocus()
        {
            sampledHalo=0;sampledLight=0;
            if(haloTransform!=null && haloTransform.gameObject.activeSelf)haloTransform.gameObject.SetActive(false);
            if(screenLight!=null && screenLight.gameObject.activeSelf)screenLight.gameObject.SetActive(false);
            if(haloMaterial!=null)haloMaterial.SetFloat("_Intensity",0);
            if(gridMaterial!=null)gridMaterial.SetColor("_PoolColor",new Color(0,0,0,0));
        }
        // Same enter / hold / exit pose as OWV4Montage for role==1 events (region-local, i.e. frame-local).
        private void PlaceFocus(int slot,float time)
        {
            int e=focusEvents[slot];Color col=focusColors[slot];
            float[] starts=montage.starts;float[] ends=montage.ends;float[] enters=montage.enters;float[] exits=montage.exits;float[] widths=montage.widths;float[] heights=montage.heights;
            Vector3[] fromAt=montage.from;Vector3[] toAt=montage.to;Vector3[] awayAt=montage.away;
            float age=time-starts[e],length=ends[e]-starts[e];
            float enter=Mathf.Max(.05f,enters[e]),exit=Mathf.Max(.05f,exits[e]);float leaveAt=length-exit;
            Vector3 pos=Vector3.zero;float scale=1,yaw=0,env=1;
            if(age<enter)
            {
                float q=Ease(age/enter);pos=Vector3.Lerp(fromAt[e],toAt[e],q);scale=.84f+.16f*q;yaw=(1-q)*(fromAt[e].x>toAt[e].x?10f:-10f);env=q;
            }
            else if(age<leaveAt)
            {
                float hold=age-enter;pos=toAt[e]+new Vector3(Mathf.Sin(hold*.5f)*.025f,Mathf.Sin(hold*.63f)*.018f,0);scale=1;yaw=Mathf.Sin(hold*.27f)*1.2f;env=1;
            }
            else
            {
                float q=Ease((age-leaveAt)/exit);pos=Vector3.Lerp(toAt[e],awayAt[e],q);scale=1-.16f*q;yaw=q*(awayAt[e].x>toAt[e].x?-10f:10f);env=1-q;
            }
            Quaternion rot=Quaternion.Euler(0,yaw,0);
            if(haloTransform!=null)
            {
                float w=widths[e]*scale*haloScale;
                // Stay above the low keyline band: bottom edge never below haloMinBottom (region-local y).
                float h=Mathf.Max(.05f,Mathf.Min(heights[e]*scale*haloScale,2f*(pos.y-haloMinBottom)));
                haloTransform.localPosition=pos+rot*new Vector3(0,0,haloBack);haloTransform.localRotation=rot;haloTransform.localScale=new Vector3(w,h,1);
                if(!haloTransform.gameObject.activeSelf)haloTransform.gameObject.SetActive(true);
            }
            sampledHalo=env*haloPeak;
            if(haloMaterial!=null){haloMaterial.SetColor("_Color",col);haloMaterial.SetFloat("_Intensity",sampledHalo);haloMaterial.SetFloat("_Timeline",time);}
            Vector3 virtualPosition=journey!=null?journey.VirtualPosition(time):Vector3.zero;
            Vector3 eye=referenceEye+virtualPosition;
            Vector3 lightLocal=pos+(eye-pos).normalized*lightTowardEye;
            sampledLight=env*lightPeak;
            if(screenLight!=null)
            {
                screenLight.transform.localPosition=lightLocal;screenLight.color=col;screenLight.intensity=sampledLight;screenLight.range=lightRange;
                bool on=env>.003f;if(screenLight.gameObject.activeSelf!=on)screenLight.gameObject.SetActive(on);
            }
            if(gridMaterial!=null)
            {
                Vector3 local=lightLocal-virtualPosition;Vector3 world=frameRoot!=null?frameRoot.TransformPoint(local):local;
                gridMaterial.SetVector("_PoolPos",new Vector4(world.x,groundY,world.z,0));
                gridMaterial.SetColor("_PoolColor",new Color(col.r*poolGain*env,col.g*poolGain*env,col.b*poolGain*env,0));
                gridMaterial.SetFloat("_PoolRadius",poolRadius);
            }
        }
        private float Ease(float value){return Mathf.SmoothStep(0,1,Mathf.Clamp01(value));}
    }
}
