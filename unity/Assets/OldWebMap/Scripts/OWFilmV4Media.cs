using UdonSharp;
using UnityEngine;
using TMPro;

namespace OldWebMap
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWFilmV4Media:UdonSharpBehaviour
    {
        public OWFilmV4Presentation run;
        public Transform relatedRoot;
        public Transform[] relatedPlanes;
        public Material[] relatedMaterials;
        public TMP_Text[] relatedLabels;
        public Transform[] relatedLabelTransforms;
        public Texture2D[] sources;
        public float[] aspects;
        public string[] labels;
        public float[] groupStarts;
        public float[] groupEnds;
        public int[] poolOffsets;
        public int[] poolCounts;
        public int[] poolIndices;
        public GameObject historyRoot;
        public Renderer historyMain;
        public Renderer historyAside;
        public Material historyMainMaterial;
        public Material historyAsideMaterial;
        public Texture2D[] historyFrames;
        public int[] historyMainIndices;
        public int[] historyCompanionA;
        public int[] historyCompanionB;
        public float[] historyStarts;
        public float[] historyEnds;
        public string[] historyLabels;
        public TMP_Text historyDate;
        public Texture2D[] filmAtlases;
        public float[] filmStarts;
        public float[] filmEnds;
        public float[] filmFps;
        public int[] filmCounts;
        public bool[] filmOnAside;
        public Material filmMaterial;
        public Renderer filmPlane;
        public Transform[] emotePlanes;
        public Material[] emoteMaterials;
        public Texture2D[] emotes;
        public float[] emoteStarts;
        public float[] emoteEnds;
        public float[] emoteAspects;
        public TMP_Text provenanceTitle;
        public TMP_Text provenanceSubtitle;
        public Transform provenanceHeading;
        public Material provenanceHeadingMaterial;
        public Transform playPulse;
        public Material playPulseMaterial;
        public int sampledGroup=-1;
        public int sampledHistory=-1;
        public int sampledFilm=-1;
        public int activeRelated;
        public int activeEmotes;
        public int[] sampledRelated;

        private float E(float x){return Mathf.SmoothStep(0,1,Mathf.Clamp01(x));}
        private void On(GameObject go,bool visible){if(go!=null&&go.activeSelf!=visible)go.SetActive(visible);}
        public void Sample(float time,bool show,int state)
        {
            if(run.nativeSecondary!=null)On(run.nativeSecondary.gameObject,show&&run.sampledNative==1);
            if(run.archiveDetail!=null)On(run.archiveDetail.gameObject,show&&time>=168.96f&&time<186.88f);
            SampleRelated(time,show);
            SampleHistory(time,show);
            SampleEmotes(time,show);
            if(run.details.focusHalos!=null&&run.details.focusHalos.Length>1)On(run.details.focusHalos[1].gameObject,false);
            bool provenance=show&&time>=120.32f&&time<133.12f;
            On(provenanceHeading==null?null:provenanceHeading.gameObject,provenance);
            if(provenance)
            {
                int page=Mathf.Min(2,Mathf.FloorToInt((time-120.32f)/3.84f));
                provenanceTitle.text=page==0?"盗模":page==1?"授权":"模型，由谁决定？";
                provenanceSubtitle.text=page==0?"未经许可复制、上传":page==1?"作者的许可，仍然有效":"社区提案 · 尚非已实施规则";
                float reveal=E((time-120.32f-page*3.84f)/.32f);
                provenanceTitle.color=new Color(1,1,1,reveal);provenanceSubtitle.color=new Color(.84f,.93f,1,reveal);
                provenanceHeadingMaterial.SetFloat("_Opacity",reveal*.94f);
            }
            bool emergence=show&&time>=217.6f&&time<222.72f;
            On(playPulse==null?null:playPulse.gameObject,emergence);
            if(emergence&&run.workspaceScreen!=null)
            {
                float p=E((time-217.6f)/5.12f);
                playPulse.position=Vector3.Lerp(run.workspaceScreen.position,new Vector3(0,1.35f,-1.2f),p);
                playPulse.rotation=Quaternion.Euler(0,0,Mathf.Sin(p*Mathf.PI)*24);
                playPulse.localScale=new Vector3(2.1f,1.2f,1)*Mathf.Lerp(.02f,15,p);
                playPulseMaterial.SetFloat("_Timeline",time);playPulseMaterial.SetFloat("_Intensity",Mathf.Sin(p*Mathf.PI)*.38f);
            }
        }
        private void SampleRelated(float time,bool show)
        {
            sampledGroup=-1;activeRelated=0;
            if(show&&groupStarts!=null)for(int i=0;i<groupStarts.Length;i++)if(time>=groupStarts[i]&&time<groupEnds[i])sampledGroup=i;
            On(relatedRoot==null?null:relatedRoot.gameObject,sampledGroup>=0);
            if(sampledGroup<0||relatedPlanes==null||sources==null)return;
            Transform primary=run.sampledNative>=0?run.nativeMain.transform:run.archiveMain;
            if(time<77.44f&&run.sampledNative<0)
            {
                var m=run.basis.legacy.montage;
                for(int i=0;i<m.windows.Length;i++)if(m.windows[i].gameObject.activeInHierarchy)
                    for(int j=m.slotFirst[i];j<m.slotFirst[i]+m.slotCount[i];j++)if(m.roles[j]==1&&time>=m.starts[j]&&time<m.ends[j])primary=m.imagePlanes[i];
                if(run.details.flashPlane!=null&&run.details.flashPlane.gameObject.activeInHierarchy)primary=run.details.flashPlane.transform;
                if(run.details.gamePlane!=null&&run.details.gamePlane.gameObject.activeInHierarchy)primary=run.details.gamePlane.transform;
            }
            relatedRoot.SetPositionAndRotation(primary.position,primary.rotation);
            bool dense=time>=133.12f;float age=time-groupStarts[sampledGroup];
            int slots=dense?6:time<10.24f?2:4;
            if(run.nativeSecondary!=null&&run.sampledNative>=0)On(run.nativeSecondary.gameObject,run.sampledNative==1);
            if(dense&&run.archiveDetail!=null)On(run.archiveDetail.gameObject,false);
            for(int slot=0;slot<relatedPlanes.Length;slot++)
            {
                bool visible=slot<slots;On(relatedPlanes[slot].gameObject,visible);if(relatedLabelTransforms!=null&&slot<relatedLabelTransforms.Length)On(relatedLabelTransforms[slot].gameObject,visible);
                if(!visible){sampledRelated[slot]=-1;continue;}
                float rate=slot==0?1.28f:slot==1?.64f:slot==2?(dense?.32f:.64f):slot==3?1.28f:.64f;
                int tick=Mathf.FloorToInt((age+slot*.16f)/rate);
                int offset=poolOffsets[sampledGroup],count=poolCounts[sampledGroup];
                int index=poolIndices[offset+(tick+slot*3)%count];sampledRelated[slot]=index;
                Material mat=relatedMaterials[slot];mat.mainTexture=sources[index];mat.SetTexture("_PreviousTex",sources[index]);
                float phase=Mathf.Repeat(age+slot*.16f,rate);float opacity=E(phase/.06f)*(1-E((phase-rate+.06f)/.06f));
                float crop=slot==1||slot==3?.72f:1;
                mat.SetVector("_Rect",new Vector4(0,(1-crop)*E(phase/rate),1,crop));mat.SetVector("_PreviousRect",new Vector4(0,(1-crop)*E(phase/rate),1,crop));mat.SetFloat("_Blend",1);
                mat.SetFloat("_Opacity",opacity);mat.SetFloat("_Timeline",time);mat.SetFloat("_Activity",.65f);
                int layout=(Mathf.FloorToInt(age/2.56f)+sampledGroup)%3;
                Vector3 p;float width;
                if(slot==0){p=new Vector3(3.25f,.72f,-.10f);width=dense?2.6f:2.25f;}
                else if(slot==1){p=new Vector3(1.55f,2.06f,.20f);width=dense?2.75f:2.35f;}
                else if(slot==2){p=new Vector3(-2.15f,1.92f,.12f);width=dense?2.65f:2.1f;}
                else if(slot==3){p=new Vector3(3.25f,-1.12f,-.12f);width=dense?2.55f:2.1f;}
                else if(slot==4){p=new Vector3(-3.35f,-.95f,.28f);width=2.9f;}
                else {p=new Vector3(-.55f,-2.10f,-.08f);width=3.1f;}
                if(layout==1){p.x=-p.x;if(slot==0||slot==3)p.y+=.12f;}
                if(layout==2&&(slot==1||slot==2))p.x+=slot==1?.5f:-.25f;
                float onset=slot%4==0?run.sampledAccents.x:slot%4==1?run.sampledAccents.y:slot%4==2?run.sampledAccents.z:run.sampledAccents.w;
                p.y+=onset*.035f;p.z+=(1-E(phase/.12f))*.1f;
                relatedPlanes[slot].localPosition=p;relatedPlanes[slot].localRotation=Quaternion.Euler(0,Mathf.Sin(time*.4f+slot)*2,(slot%2==0?1:-1)*1.5f);
                float height=Mathf.Min(dense?1.65f:1.25f,width/aspects[index]*crop);
                float visibleAspect=width/height;
                // Match the displayed rectangle to the source crop, not a stretched thumbnail.
                float scaleY=height;float rectH=Mathf.Min(crop,aspects[index]/visibleAspect);
                mat.SetVector("_Rect",new Vector4(0,(1-rectH)*(1-.4f*E(phase/rate)),1,rectH));mat.SetVector("_PreviousRect",new Vector4(0,(1-rectH)*(1-.4f*E(phase/rate)),1,rectH));
                relatedPlanes[slot].localScale=new Vector3(width,scaleY,1);
                if(relatedLabels!=null&&slot<relatedLabels.Length){relatedLabels[slot].text=labels[index];relatedLabels[slot].color=new Color(.88f,.95f,1,opacity*.8f);relatedLabelTransforms[slot].SetPositionAndRotation(relatedPlanes[slot].position-relatedPlanes[slot].up*(height*.5f+.10f),relatedPlanes[slot].rotation);}
                activeRelated++;
            }
        }
        private void SampleHistory(float time,bool show)
        {
            sampledHistory=-1;sampledFilm=-1;
            if(show&&historyStarts!=null)for(int i=0;i<historyStarts.Length;i++)if(time>=historyStarts[i]&&time<historyEnds[i])sampledHistory=i;
            bool visible=sampledHistory>=0;On(historyRoot,visible);
            if(visible)
            {
                int index=sampledHistory;int source=historyMainIndices[index];historyMainMaterial.mainTexture=historyFrames[source];historyMainMaterial.SetTexture("_PreviousTex",historyFrames[source]);historyMainMaterial.SetFloat("_Blend",1);
                int next=Mathf.FloorToInt((time-historyStarts[index])/.64f)%2==0?historyCompanionA[index]:historyCompanionB[index];historyAsideMaterial.mainTexture=historyFrames[next];historyAsideMaterial.SetTexture("_PreviousTex",historyFrames[next]);historyAsideMaterial.SetFloat("_Blend",1);
                float age=time-historyStarts[index];float opacity=E(age/.16f)*(1-E((time-historyEnds[index]+.16f)/.16f));historyMainMaterial.SetFloat("_Opacity",opacity);historyAsideMaterial.SetFloat("_Opacity",opacity);
                historyMain.transform.localPosition=new Vector3(-1.6f,2.5f,1.8f);historyMain.transform.localScale=new Vector3(3.4f,1.9125f,1);
                historyAside.transform.localPosition=new Vector3(2.4f,2.7f,2.0f);historyAside.transform.localScale=new Vector3(2.2f,1.2375f,1);
                if(historyDate!=null){historyDate.text=historyLabels[index];historyDate.color=new Color(.9f,.96f,1,opacity);}
                var journey=run.basis.legacy.journey;int region=journey.Region(time);historyRoot.transform.localPosition=journey.RegionOffset(region);
            }
            if(show&&filmStarts!=null)for(int i=0;i<filmStarts.Length;i++)if(time>=filmStarts[i]&&time<filmEnds[i])sampledFilm=i;
            bool film=sampledFilm>=0;On(filmPlane==null?null:filmPlane.gameObject,film);
            if(film)
            {
                int i=sampledFilm;filmMaterial.mainTexture=filmAtlases[i];filmMaterial.SetFloat("_Frame",Mathf.Min(filmCounts[i]-1,Mathf.Floor((time-filmStarts[i])*filmFps[i])));filmMaterial.SetFloat("_Opacity",E((time-filmStarts[i])/.16f)*(1-E((time-filmEnds[i]+.16f)/.16f)));
                var target=filmOnAside[i]?historyAside:historyMain;
                filmPlane.transform.SetPositionAndRotation(target.transform.position-target.transform.forward*.015f,target.transform.rotation);filmPlane.transform.localScale=target.transform.localScale;
                if(historyMain!=null)historyMain.enabled=filmOnAside[i];if(historyAside!=null)historyAside.enabled=!filmOnAside[i];
            }
            else {if(historyMain!=null)historyMain.enabled=true;if(historyAside!=null)historyAside.enabled=true;}
        }
        private void SampleEmotes(float time,bool show)
        {
            activeEmotes=0;if(emotePlanes==null||emotes==null||emotes.Length==0)return;
            bool dense=show&&time>=133.12f&&time<168.96f,early=show&&time>=20.48f&&time<77.44f;
            bool reunion=show&&time>=235.52f&&time<249.6f;
            for(int i=0;i<emotePlanes.Length;i++)
            {
                int candidate=-1;int count=0;int desired=(Mathf.FloorToInt(time/(dense?.64f:2.56f))+i*3)%emotes.Length;
                for(int j=0;j<emotes.Length;j++){int at=(desired+j)%emotes.Length;if(time>=emoteStarts[at]&&time<emoteEnds[at]){candidate=at;count++;if(count>i%3)break;}}
                bool visible=(dense||early&&i<4||reunion&&i<6)&&candidate>=0;
                On(emotePlanes[i].gameObject,visible);if(!visible)continue;
                emoteMaterials[i].mainTexture=emotes[candidate];
                float side=i%2==0?-1:1;float scale=dense?.58f:.44f;
                emotePlanes[i].localPosition=new Vector3(side*(3.7f+(i%3)*.62f),1.15f+(i%5)*.55f,5.0f+(i/4)*1.1f);
                emotePlanes[i].localRotation=Quaternion.Euler(0,-side*(dense?14:5),Mathf.Sin(time*.55f+i)*(dense?12:3));
                emotePlanes[i].localScale=new Vector3(scale,scale/emoteAspects[candidate],1);
                if(time<133.12f){var journey=run.basis.legacy.journey;emotePlanes[i].position=journey.worldFrame.TransformPoint(emotePlanes[i].localPosition+journey.RegionOffset(journey.Region(time)));}
                activeEmotes++;
            }
        }
    }
}
