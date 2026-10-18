using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.SDKBase;

namespace OldWebMap
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV3Details:UdonSharpBehaviour
    {
        public OWV3Presentation run;
        public Material portalMaterial;
        public Material pixelCylinderMaterial;
        public Transform textRibbon;
        public Material textRibbonMaterial;
        public Transform menuGeometry;
        public CanvasGroup menuCanvasGroup;
        public Button[] menuButtons;
        public Transform rearWall;
        public Vector3 openingMenuPosition=new Vector3(0,1.15f,-.70f);
        public Vector3 replayMenuPosition=new Vector3(0,1.05f,.5f);
        public Renderer cursorSwarm;
        public Material cursorSwarmMaterial;
        public Transform[] focusHalos;
        public Material[] haloMaterials;
        public Transform[] nativePointerPoints;
        public Vector3[] nativePointerPositions;
        public Transform[] comments;
        public TMP_Text[] commentTexts;
        public string[] commentWords;
        public GameObject generatedArtRoot;
        public Renderer comicPlane;
        public Renderer gamePlane;
        public Renderer gameDetailPlane;
        public Renderer homepagePlane;
        public Renderer flashPlane;
        public Renderer conceptPlane;
        public Renderer stillLifePlane;
        public Texture2D[] comicStates;
        public Texture2D[] gameStates;
        public Texture2D[] flashStates;
        public Material comicMaterial;
        public Material gameMaterial;
        public Material gameDetailMaterial;
        public Material homepageMaterial;
        public Material flashMaterial;
        public Material conceptMaterial;
        public Material[] mangaNativeMaterials;
        public Texture2D[] mangaNativeStates;
        public Transform[] stickerPlanes;
        public GameObject provenanceRoot;
        public Renderer provenanceMain;
        public Renderer provenanceDetail;
        public Material provenanceMainMaterial;
        public Material provenanceDetailMaterial;
        public Texture2D[] provenanceTextures;
        public Texture2D[] provenanceDetails;
        public float[] provenanceAspects;
        public float[] provenanceDetailAspects;
        public string[] provenanceLabels;
        public TMP_Text provenanceLabel;
        public Collider[] worldFloorColliders;
        public Collider[] originalFloorColliders;
        public GameObject penRoot;
        public Renderer[] penRenderers;
        public Collider[] penColliders;
        public VRC_Pickup[] penPickups;
        public Canvas[] penCanvases;
        public Material wordBackground;
        public float sampledRibbonLift;
        public float sampledWordAlpha;
        public int sampledGeneratedState=-1;
        public int activeCursors;
        public int visibleComments;
        public int namedPlayers;
        public bool floorReleased;
        private int _lastState=-1;
        private GameObject _penInkRoot;
        private float _nextInkCheck;

        private float E(float x){return Mathf.SmoothStep(0,1,Mathf.Clamp01(x));}
        private void On(GameObject go,bool value){if(go!=null&&go.activeSelf!=value)go.SetActive(value);}
        public void Sample(float time,bool show,int state)
        {
            if(run==null||run.basis==null)return;
            float end=run.director==null?256f:run.director.duration;
            SamplePanel(time,show,state,end);
            SampleRibbon(time,show,state,end);
            SampleWorld(time,show,state);
            SampleCursors(time,show);
            SampleArt(time,show);
            SampleProvenance(time,show);
            SampleFocus(time,show);
            SampleComments(time,show);
            SampleNames();
            float alpha=show?E((time-(end-7.68f))/1.28f)*(1-E((time-(end-2.56f))/2.56f)):0;
            sampledWordAlpha=alpha;On(run.finalWordRoot,alpha>.001f);
            if(run.finalWord!=null){run.finalWord.color=new Color(1,.96f,.85f,alpha);run.finalWordRoot.transform.localPosition=new Vector3(0,1.16f,5.2f);run.finalWordRoot.transform.localScale=Vector3.one*(.94f+.06f*E((time-(end-7.68f))/2.56f));}
            if(wordBackground!=null)wordBackground.SetFloat("_Opacity",alpha*.72f);
            bool usable=state==2;
            floorReleased=usable;
            // Change these gates once per state transition. QvPen owns its
            // marker/collider state during use; do not overwrite it every frame.
            if(_lastState!=state)
            {
                if(worldFloorColliders!=null)for(int i=0;i<worldFloorColliders.Length;i++)if(worldFloorColliders[i]!=null)worldFloorColliders[i].enabled=usable;
                if(originalFloorColliders!=null)for(int i=0;i<originalFloorColliders.Length;i++)if(originalFloorColliders[i]!=null)originalFloorColliders[i].enabled=!usable;
                if(penRenderers!=null)for(int i=0;i<penRenderers.Length;i++)if(penRenderers[i]!=null)penRenderers[i].enabled=usable;
                if(penColliders!=null)for(int i=0;i<penColliders.Length;i++)if(penColliders[i]!=null)penColliders[i].enabled=usable;
                if(penCanvases!=null)for(int i=0;i<penCanvases.Length;i++)if(penCanvases[i]!=null)penCanvases[i].enabled=usable;
                if(penPickups!=null)for(int i=0;i<penPickups.Length;i++)if(penPickups[i]!=null)penPickups[i].pickupable=usable;
            }
            // QvPen reparents its generated strokes beneath a global pool at
            // initialization. Keep its managers and late-join synchronizers alive;
            // gate only the ink renderers/colliders, including newly received ink.
            if(!run.offlinePreview&&(_lastState!=state||Time.time>=_nextInkCheck))
            {
                _nextInkCheck=Time.time+.20f;
                if(_penInkRoot==null)_penInkRoot=GameObject.Find("QvPen_Objects");
                if(_penInkRoot!=null)
                {
                    var inkRenderers=_penInkRoot.GetComponentsInChildren<Renderer>(true);
                    for(int i=0;i<inkRenderers.Length;i++)inkRenderers[i].enabled=usable;
                    var inkColliders=_penInkRoot.GetComponentsInChildren<Collider>(true);
                    for(int i=0;i<inkColliders.Length;i++)inkColliders[i].enabled=usable;
                }
            }
            if(_lastState==2&&state!=2)
            {
                if(penPickups!=null)for(int i=0;i<penPickups.Length;i++)if(penPickups[i]!=null&&penPickups[i].IsHeld)penPickups[i].Drop();
                if(!run.offlinePreview&&Utilities.IsValid(Networking.LocalPlayer))
                {var p=Networking.LocalPlayer.GetPosition();if(p.y<-.10f){p.y=-.025f;Networking.LocalPlayer.TeleportTo(p,Networking.LocalPlayer.GetRotation());}}
            }
            _lastState=state;
        }
        private void SamplePanel(float time,bool show,int state,float end)
        {
            var legacy=run.basis.legacy;float landing=E((time-(end-2.56f))/2.56f);bool visible=!show||landing>.001f;
            On(legacy.menuRoot,visible);On(menuGeometry==null?null:menuGeometry.gameObject,visible);
            Vector3 target=state==0?openingMenuPosition:replayMenuPosition;
            Quaternion face=Quaternion.identity;
            Vector3 position=target+(show?new Vector3(0,(1-landing)*4.2f,0):Vector3.zero);
            if(legacy.menuRoot!=null)legacy.menuRoot.transform.SetPositionAndRotation(position,face);
            if(menuGeometry!=null)menuGeometry.SetPositionAndRotation(position+face*Vector3.forward*.22f,face);
            if(menuCanvasGroup!=null)menuCanvasGroup.alpha=show?landing:1;
            if(menuButtons!=null)for(int i=0;i<menuButtons.Length;i++)if(menuButtons[i]!=null)menuButtons[i].interactable=!show;
            if(visible&&state!=0){if(legacy.menuTitle!=null)legacy.menuTitle.text="继续";if(legacy.menuBody!=null)legacy.menuBody.text="";if(legacy.playLabel!=null)legacy.playLabel.text="重播";}
            if(portalMaterial!=null){portalMaterial.SetFloat("_Timeline",time);portalMaterial.SetFloat("_Idle",run.offlinePreview?0:1);}
        }
        private void SampleRibbon(float time,bool show,int state,float end)
        {
            var signal=run.basis.signal;
            if(signal.greetz!=null&&signal.greetz.ringRenderer!=null)signal.greetz.ringRenderer.enabled=false;
            if(signal.continuationGreetzRenderer!=null)signal.continuationGreetzRenderer.enabled=false;
            if(textRibbon==null||textRibbonMaterial==null)return;
            float up=show?E(time/2.56f)*(1-E((time-(end-3.84f))/3.84f)):0;sampledRibbonLift=up;
            Vector3 dock=menuGeometry==null?new Vector3(0,.55f,-.49f):menuGeometry.TransformPoint(new Vector3(0,-.795f,-.012f));
            textRibbon.position=Vector3.Lerp(dock,new Vector3(0,4.7f,-2.8f),up);
            textRibbon.rotation=Quaternion.Slerp(menuGeometry==null?Quaternion.identity:menuGeometry.rotation,Quaternion.Euler(Mathf.Sin(time*.21f)*4,Mathf.Sin(time*.11f)*5,Mathf.Sin(time*.18f)*3),up);
            textRibbonMaterial.SetFloat("_Morph",up);textRibbonMaterial.SetFloat("_Timeline",show?time:Time.time);
            float energetic=E((time-76.8f)/2.56f)*(1-E((time-97.28f)/2.56f))+E((time-235.52f)/2.56f)*(1-E((time-250.88f)/2.56f));
            textRibbonMaterial.SetFloat("_HueSpeed",.125f*Mathf.Clamp01(energetic));
            float quiet=E((time-184.32f)/2.56f)*(1-E((time-197.12f)/2.56f));
            textRibbonMaterial.SetFloat("_Opacity",1-quiet);textRibbonMaterial.SetFloat("_Amp",Mathf.Lerp(.02f,.16f+energetic*.18f,up));
            float scroll=signal.greetz==null?0:signal.greetz.sampledScroll;
            textRibbonMaterial.SetFloat("_Scroll",show?scroll+time*55f:Time.time*110f);
            textRibbonMaterial.SetFloat("_ArcLength",47.12389f);
            if(pixelCylinderMaterial!=null){pixelCylinderMaterial.SetFloat("_Turn",time*1.2271846f);pixelCylinderMaterial.SetFloat("_Pulse",Mathf.Max(run.sampledAccents.x,run.sampledAccents.z));}
            if(run.cylinder!=null)run.cylinder.localPosition=Vector3.Lerp(new Vector3(0,4.75f,6.5f),new Vector3(-3.5f,4.15f,7.3f),E((time-2.56f)/5.12f));
        }
        private void SampleWorld(float time,bool show,int state)
        {
            var cast=run.basis.legacy.cast;
            if(cast!=null)
            {
                Reveal(cast.hub2017,time,79.36f,102.40f,show);Reveal(cast.hub2019,time,86.40f,102.40f,show);Reveal(cast.home2019,time,86.40f,102.40f,show);
                On(cast.conditionsRoot==null?null:cast.conditionsRoot.gameObject,show&&time>=102.4f&&time<133.12f);
                On(cast.maintenanceRoot==null?null:cast.maintenanceRoot.gameObject,show&&time>=102.4f&&time<133.12f);
            }
            if(rearWall!=null){bool back=state==0||show&&time<35.84f;On(rearWall.gameObject,back);rearWall.localPosition=new Vector3(0,1.7f,-7-E((time-30.72f)/5.12f)*15);}
            var sig=run.basis.signal;if(sig.gridRenderer!=null)sig.gridRenderer.enabled=false;
            if(sig.atmosphere!=null&&sig.atmosphere.gridMaterial!=null)sig.atmosphere.gridMaterial.SetFloat("_Opacity",0);
            if(run.hubDetails!=null){bool v=show&&time>=79.36f&&time<102.4f;On(run.hubDetails.gameObject,v);if(v)run.hubDetails.localScale=Vector3.one*E((time-79.36f)/.64f);}
        }
        private void Reveal(Transform target,float time,float start,float end,bool show)
        {if(target==null)return;On(target.gameObject,show&&time>=start&&time<end);target.localScale=Vector3.one*E((time-start)/.64f);}
        private void SampleNames()
        {
            namedPlayers=0;var cast=run.basis.legacy.cast;if(cast==null||cast.nameplates==null)return;var names=cast.nameplates;
            if(names.playerIds==null||names.playerLabels==null)return;
            for(int i=0;i<names.playerIds.Length&&i<names.playerLabels.Length;i++)
            {
                if(names.playerIds[i]<0||names.playerLabels[i]==null)continue;
                if(names.playerLabels[i].text!=names.anonymousLabel){namedPlayers++;continue;}
                VRCPlayerApi player=VRCPlayerApi.GetPlayerById(names.playerIds[i]);
                if(Utilities.IsValid(player)){if(names.playerLabels[i].text!=player.displayName)names.playerLabels[i].text=player.displayName;namedPlayers++;}
            }
        }
        private void SampleFocus(float time,bool show)
        {
            var atmosphere=run.basis.signal.atmosphere;
            if(atmosphere!=null&&atmosphere.haloTransform!=null)On(atmosphere.haloTransform.gameObject,false);
            Transform primary=null,secondary=null;
            if(show&&run.nativeRoot!=null&&run.nativeRoot.activeInHierarchy){primary=run.nativeMain.transform;secondary=run.nativeSecondary.transform;}
            else if(show&&run.archiveFocus!=null&&run.archiveFocus.gameObject.activeInHierarchy){primary=run.archiveMain;secondary=run.archiveDetail;}
            else if(show&&time<133.12f)
            {
                var m=run.basis.legacy.montage;
                for(int i=0;i<m.windows.Length;i++)if(m.windows[i].gameObject.activeInHierarchy)
                    for(int j=m.slotFirst[i];j<m.slotFirst[i]+m.slotCount[i];j++)
                        if(m.roles[j]==1&&time>=m.starts[j]&&time<m.ends[j])primary=m.imagePlanes[i];
            }
            if(show&&provenanceRoot!=null&&provenanceRoot.activeInHierarchy){primary=provenanceMain.transform;secondary=provenanceDetail.transform;}
            if(show&&gamePlane!=null&&gamePlane.gameObject.activeInHierarchy){primary=gamePlane.transform;secondary=gameDetailPlane==null?null:gameDetailPlane.transform;}
            if(focusHalos==null)return;
            for(int i=0;i<focusHalos.Length;i++)
            {
                var target=i==0?primary:secondary;var halo=focusHalos[i];if(halo==null)continue;
                bool visible=target!=null&&target.gameObject.activeInHierarchy;On(halo.gameObject,visible);
                if(!visible)continue;
                halo.SetPositionAndRotation(target.position+target.forward*.035f,target.rotation);
                Vector3 size=target.lossyScale*1.18f,parent=halo.parent==null?Vector3.one:halo.parent.lossyScale;
                halo.localScale=new Vector3(Mathf.Abs(size.x/parent.x),Mathf.Abs(size.y/parent.y),1);
                if(haloMaterials!=null&&i<haloMaterials.Length){haloMaterials[i].SetFloat("_Timeline",time);haloMaterials[i].SetFloat("_Intensity",i==0?.26f:.14f);}
            }
            if(atmosphere!=null&&atmosphere.screenLight!=null)
            {
                On(atmosphere.screenLight.gameObject,primary!=null);
                if(primary!=null){Vector3 eye=new Vector3(0,1.35f,-2.8f);atmosphere.screenLight.transform.position=primary.position+(eye-primary.position).normalized*.8f;atmosphere.screenLight.intensity=.7f;}
            }
        }
        private void SampleCursors(float time,bool show)
        {
            float density=show&&time>=133.12f&&time<186.88f?E((time-133.12f)/15.36f):0;
            activeCursors=Mathf.RoundToInt(density*128);if(cursorSwarm!=null)cursorSwarm.enabled=density>.001f;
            if(cursorSwarmMaterial!=null){cursorSwarmMaterial.SetFloat("_Timeline",time);cursorSwarmMaterial.SetFloat("_Density",density);cursorSwarmMaterial.SetFloat("_Opacity",1-E((time-183.04f)/3.84f));cursorSwarmMaterial.SetVector("_Pulse",run.sampledAccents);}
            if(!show||run.sampledNative<0||nativePointerPositions==null||nativePointerPositions.Length<24||run.nativePointer==null)return;
            int group=run.sampledNative;float age=time-run.nativeStarts[group];int state=Mathf.Clamp(Mathf.FloorToInt(age/2.56f),0,3);
            Vector3 target=nativePointerPositions[group*4+state],from=state==0?target+new Vector3(.9f,-.3f,0):nativePointerPositions[group*4+state-1];
            float phase=Mathf.Repeat(age,2.56f);Vector3 pos=Vector3.Lerp(from,target,E(phase/.55f));
            if(group==0||group==4)pos.x+=.22f*E((phase-.85f)/.7f)*(1-E((phase-2.1f)/.3f));
            run.nativePointer.localPosition=pos;run.nativePointer.localRotation=Quaternion.identity;
            run.nativePointer.localScale=Vector3.one*(.32f*(1-.12f*Mathf.Exp(-Mathf.Abs(phase-.65f)*24)));
        }
        private void SampleArt(float time,bool show)
        {
            On(generatedArtRoot,show);
            sampledGeneratedState=-1;
            if(show&&run.sampledNative==1&&mangaNativeStates!=null&&mangaNativeStates.Length>=8)
            {
                int state=Mathf.Clamp(Mathf.FloorToInt((time-30.72f)/2.56f),0,3);sampledGeneratedState=state;
                run.nativeMainMaterial.mainTexture=mangaNativeStates[state*2];run.nativeMainMaterial.SetTexture("_PreviousTex",mangaNativeStates[state*2]);run.nativeMainMaterial.SetFloat("_Blend",1);
                run.nativeSecondaryMaterial.mainTexture=mangaNativeStates[state*2+1];run.nativeSecondaryMaterial.SetTexture("_PreviousTex",mangaNativeStates[state*2+1]);run.nativeSecondaryMaterial.SetVector("_Rect",new Vector4(0,0,1,1));
                var tex=mangaNativeStates[state*2+1];float aspect=(float)tex.width/tex.height;
                run.nativeSecondary.transform.localScale=new Vector3(1.7f,1.7f/aspect,1);
            }
            bool flash=show&&time>=22.4f&&time<30.72f;On(flashPlane==null?null:flashPlane.gameObject,flash);
            if(flash&&flashStates!=null&&flashStates.Length>0)flashMaterial.mainTexture=flashStates[Mathf.FloorToInt((time-22.4f)/.32f)%flashStates.Length];
            bool game=show&&time>=71.68f&&time<77.44f;On(gamePlane==null?null:gamePlane.gameObject,game);
            On(gameDetailPlane==null?null:gameDetailPlane.gameObject,game);
            if(game&&gameStates!=null&&gameStates.Length>0){int state=Mathf.FloorToInt((time-71.68f)/2.56f)%gameStates.Length;gameMaterial.mainTexture=gameStates[state];gamePlane.transform.localRotation=Quaternion.Euler(0,Mathf.Sin(time*.3f)*4,0);if(gameDetailMaterial!=null){gameDetailMaterial.mainTexture=gameStates[(state+1)%gameStates.Length];gameDetailMaterial.SetVector("_Rect",new Vector4(0,.015f+.03f*Mathf.Sin(time),1,.33f));}}
            bool home=show&&time>=51.2f&&time<61.44f;On(homepagePlane==null?null:homepagePlane.gameObject,home);
            if(home&&homepagePlane!=null)homepagePlane.transform.localPosition=new Vector3(2.75f,2.42f+Mathf.Sin(time*.8f)*.035f,5.65f);
            bool comic=false;On(comicPlane==null?null:comicPlane.gameObject,comic);
            if(comic&&comicStates!=null&&comicStates.Length>0)comicMaterial.mainTexture=comicStates[Mathf.Min(2,Mathf.FloorToInt((time-34.56f)/2.56f))];
            bool concept=show&&time>=207.36f&&time<212.48f;On(conceptPlane==null?null:conceptPlane.gameObject,concept);
            if(concept&&conceptPlane!=null){float p=E((time-207.36f)/1.28f);conceptPlane.transform.localPosition=new Vector3(2.65f,2.0f,5.1f);conceptPlane.transform.localScale=new Vector3(2.3f,1.29375f,1)*p;}
            On(stillLifePlane==null?null:stillLifePlane.gameObject,show&&time>=197.12f&&time<202.24f);
            if(game)
            {
                var montage=run.basis.legacy.montage;
                for(int i=0;i<montage.windows.Length;i++)if(montage.windows[i].gameObject.activeInHierarchy)
                    for(int j=montage.slotFirst[i];j<montage.slotFirst[i]+montage.slotCount[i];j++)if(montage.roles[j]==1&&time>=montage.starts[j]&&time<montage.ends[j])On(montage.windows[i].gameObject,false);
            }
            if(stickerPlanes!=null)for(int i=0;i<stickerPlanes.Length;i++)
            {
                bool late=time>=235.52f&&time<249.6f;bool dense=time>=133.12f&&time<168.96f;
                bool visible=show&&(dense||late&&i>=6||time>=35.84f&&time<71.68f&&i<4);
                On(stickerPlanes[i].gameObject,visible);if(!visible)continue;
                float sign=i%2==0?-1:1;float age=time-(late?235.52f:dense?133.12f:35.84f)-i*.16f;
                float reveal=E(age/.64f);stickerPlanes[i].localPosition=new Vector3(sign*(4.0f+(i%3)*.55f),1.35f+(i%4)*.5f+Mathf.Sin(time*.6f+i)*.06f,5.8f+(i/4)*.8f);
                stickerPlanes[i].localRotation=Quaternion.Euler(0,-sign*(late?25:8),Mathf.Sin(time*.4f+i)*(late?9:3));
                stickerPlanes[i].localScale=Vector3.one*(.52f*reveal*(1+(late?.12f:.035f)*Mathf.Sin(time*2+i)));
            }
        }
        private void SampleProvenance(float time,bool show)
        {
            bool visible=show&&time>=120.32f&&time<130.56f&&provenanceTextures!=null&&provenanceTextures.Length>0;On(provenanceRoot,visible);
            if(!visible)return;int i=Mathf.Min(provenanceTextures.Length-1,Mathf.FloorToInt((time-120.32f)/3.84f));int j=i*2+Mathf.FloorToInt((time-120.32f)/1.28f)%2;
            provenanceMainMaterial.mainTexture=provenanceTextures[i];provenanceDetailMaterial.mainTexture=provenanceDetails[j];
            float a=provenanceAspects[i],b=provenanceDetailAspects[j];float w=Mathf.Min(4.3f,a*2.8f);
            provenanceMain.transform.localScale=new Vector3(w,w/a,1);provenanceDetail.transform.localScale=new Vector3(2.4f,Mathf.Min(2,2.4f/b),1);
            if(provenanceLabel!=null&&provenanceLabels!=null&&i<provenanceLabels.Length)provenanceLabel.text=provenanceLabels[i];
            On(run.nativeRoot,false);
        }
        private void SampleComments(float time,bool show)
        {
            visibleComments=0;if(comments==null||commentTexts==null||commentWords==null||commentWords.Length==0)return;
            bool active=show&&time>=30.72f&&time<181.76f||show&&time>=230.4f&&time<250.88f;
            float high=E((time-133.12f)/10.24f);float climax=E((time-76.8f)/2.56f)*(1-E((time-99.84f)/2.56f))+E((time-235.52f)/2.56f);
            for(int i=0;i<comments.Length&&i<commentTexts.Length;i++)
            {
                float period=8.96f+(i%3)*1.28f,p=Mathf.Repeat(time+i*1.73f,period)/period;
                bool visible=active&&p<.84f&&(i<4||high>.3f||climax>.3f);On(comments[i].gameObject,visible);if(!visible)continue;
                int group=time<40.96f?0:time<51.2f?1:time<61.44f?2:time<77.44f?3:time<102.4f?4:time<133.12f?5:time<168.96f?6:time<186.88f?7:8;
                int word=group*4+(Mathf.FloorToInt((time+i*1.73f)/period)*3+i)%4;commentTexts[i].text=commentWords[word%commentWords.Length];
                float x=Mathf.Lerp(8,-8,p/.84f),z=6.8f+(i%3)*.7f;float y=3.80f+(i%4)*.28f;
                comments[i].localPosition=new Vector3(x,y+Mathf.Sin(p*6.283f+i)*climax*.24f,z+Mathf.Sin(p*3.14f)*climax*.6f);
                comments[i].localRotation=Quaternion.Euler(0,Mathf.Sin(p*6.283f)*climax*14,Mathf.Sin(time*.5f+i)*climax*5);
                float alpha=E(p/.08f)*(1-E((p-.74f)/.1f))*(.55f+climax*.30f);
                Color c=i%3==0?new Color(.86f,.94f,1,alpha):i%3==1?new Color(1,.82f,.40f,alpha):new Color(.70f,1,.87f,alpha);commentTexts[i].color=c;visibleComments++;
            }
        }
    }
}
