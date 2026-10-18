using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWFilmV5Provenance:UdonSharpBehaviour
    {
        public OWFilmV5Presentation run;
        public GameObject contentRoot;
        public Transform[] cards;
        public Renderer[] cardRenderers;
        public Material[] cardMaterials;
        public Material[] haloMaterials;
        public Texture2D[] plates;
        public Texture2D[] details;
        public Texture2D[] heroPlates;
        public float[] heroAspects;
        public float[] aspects;
        public float[] detailAspects;
        public int[] mainSequence;
        public int[] companionA;
        public int[] companionB;
        public int sampledCue=-1;
        public int sampledMain=-1;
        public int visibleCards;
        public int sampledSticker=-1;

        private float E(float x){return Mathf.SmoothStep(0,1,Mathf.Clamp01(x));}
        private void On(GameObject go,bool value){if(go!=null&&go.activeSelf!=value)go.SetActive(value);}
        public void Sample(float time,bool show,int state)
        {
            sampledCue=-1;sampledMain=-1;visibleCards=0;sampledSticker=-1;
            if(run==null||run.media==null||run.details==null||cards==null||plates==null||mainSequence==null)return;
            bool prelude=show&&time>=120.32f&&time<133.12f;
            bool overload=show&&time>=133.12f&&time<186.88f;
            On(contentRoot,prelude||overload);
            // V4's old policy heading and unrelated wiki plates are superseded.
            if(run.media.provenanceHeading!=null)On(run.media.provenanceHeading.gameObject,false);
            On(run.details.provenanceRoot,false);
            if(!prelude&&!overload)return;
            for(int i=0;i<cards.Length;i++)On(cards[i].gameObject,false);
            if(prelude)
            {
                float alpha=E((time-120.32f)/.64f)*(1-E((time-132.8f)/.32f));
                Card(0,6,true,new Vector3(-.9f,2.35f,5.1f),3.6f,2.6f,alpha,time,.2f);
                float permission=E((time-125.44f)/.48f)*(1-E((time-130.08f)/.48f));
                Card(1,11,false,new Vector3(2.4f,2.05f,5.05f),1.7f,2.15f,permission,time,.08f);
                return;
            }
            int cue=Mathf.Clamp(Mathf.FloorToInt((time-133.12f)/3.84f),0,mainSequence.Length-1);
            sampledCue=cue;int main=mainSequence[cue];sampledMain=main;
            float age=time-133.12f-cue*3.84f;
            float alphaMain=E(age/.24f)*(1-E((age-3.52f)/.32f));
            float collapse=E((time-180.48f)/6.4f);
            if(main>=0)
            {
                if(run.archiveMain!=null)On(run.archiveMain.gameObject,false);
                if(run.archiveDetail!=null)On(run.archiveDetail.gameObject,false);
                if(run.archivePointer!=null)On(run.archivePointer.gameObject,false);
                bool wide=aspects[main]>1.2f;
                // A main source holds for 3.84 s with a short entrance and exit.
                // Only its small detail layer follows individual channel accents.
                Vector3 p=new Vector3(wide?-.9f:-.65f,2.25f,5.0f+.15f*(1-E(age/.48f)));
                Card(0,main,main==10||main==9&&cue==10,p,wide?4.35f:3.8f,3.0f,alphaMain,time,.48f);
                cards[0].localRotation=Quaternion.Euler(0,Mathf.Sin(age*.7f)*1.2f,-collapse*3);
                if(run.details.focusHalos!=null)for(int i=0;i<run.details.focusHalos.Length;i++)if(run.details.focusHalos[i]!=null)On(run.details.focusHalos[i].gameObject,false);
                // Leave two distinct, closely related companions near the main.
                if(run.media.relatedRoot!=null)run.media.relatedRoot.position=new Vector3(-.65f,2.15f,5.2f);
                if(run.media.relatedPlanes!=null)for(int i=0;i<run.media.relatedPlanes.Length;i++)
                {
                    if(i==0||i==3){On(run.media.relatedPlanes[i].gameObject,false);if(run.media.relatedLabelTransforms!=null)On(run.media.relatedLabelTransforms[i].gameObject,false);}
                }
            }
            else
            {
                // The original chronology/failure image resumes between these
                // windows; explicitly restore gates changed in an earlier cue.
                if(run.archiveMain!=null)On(run.archiveMain.gameObject,true);
                if(run.archivePointer!=null)On(run.archivePointer.gameObject,true);
            }
            int c=companionA[(cue+1)%companionA.Length];
            if(c==main||c>=12||c==companionA[cue]||c==companionB[cue])c=cue%2==0?2:3;
            int a=Mathf.FloorToInt(age/1.28f)%2==0?companionA[cue]:c;
            int b=Mathf.FloorToInt(age/.64f)%2==0?companionB[cue]:c;
            if(b==a)b=a==companionA[cue]?companionB[cue]:companionA[cue];
            float phase=Mathf.Repeat(age,.64f)/.64f;
            float onset=cue%4==0?run.sampledAccents.x:cue%4==1?run.sampledAccents.y:cue%4==2?run.sampledAccents.z:run.sampledAccents.w;
            float supportAlpha=E(age/.16f)*(1-E((age-3.68f)/.16f));
            Card(1,a,true,new Vector3(main>=0?2.45f:3.35f,2.6f+onset*.025f,4.93f),2.65f,1.68f,supportAlpha*(.78f+.22f*E(Mathf.Repeat(age,1.28f)/.12f)),time,.75f);
            Card(2,b,cue%2==0,new Vector3(main>=0?1.9f:-2.8f,main>=0?.7f:3.5f,5.25f),2.55f,1.1f,supportAlpha*(.70f+.18f*E(Mathf.Repeat(age,.64f)/.08f)),time,.85f);
            cards[2].localRotation=Quaternion.Euler(0,0,(cue%2==0?1:-1)*(1.4f+phase*.8f));
            // The complete postmark and meme each appear only a few times. Their
            // transparency is preserved and they never cover the held sentence.
            bool stamp=(cue==4||cue==9||cue==10)&&age>=.96f&&age<2.56f;
            bool meme=(cue==9||cue==10)&&age>=2.56f&&age<3.68f;
            if(stamp||meme)
            {
                int id=stamp?13:12;sampledSticker=id;
                float begin=stamp?.96f:2.56f,finish=stamp?2.56f:3.68f;
                float alpha=E((age-begin)/.12f)*(1-E((age-finish+.16f)/.16f));
                Card(3,id,false,new Vector3(stamp?2.7f:-2.4f,stamp?3.7f:1.1f,4.75f),stamp?2.0f:2.4f,stamp?1.12f:.85f,alpha,time,.15f);
                cards[3].localRotation=Quaternion.Euler(0,0,stamp?-8:2);
            }
        }
        private void Card(int slot,int source,bool close,Vector3 position,float maxWidth,float maxHeight,float alpha,float time,float activity)
        {
            if(source<0||source>=plates.Length||slot>=cards.Length)return;
            On(cards[slot].gameObject,alpha>.001f);
            var mat=cardMaterials[slot];Texture2D tex=close?details[source]:slot==0&&heroPlates!=null?heroPlates[source]:plates[source];
            float aspect=close?detailAspects[source]:slot==0&&heroAspects!=null?heroAspects[source]:aspects[source];
            float width=Mathf.Min(maxWidth,maxHeight*aspect),height=width/aspect;
            cards[slot].localPosition=position;cards[slot].localRotation=Quaternion.identity;
            cards[slot].localScale=new Vector3(width,height,1);
            mat.mainTexture=tex;mat.SetTexture("_PreviousTex",tex);mat.SetVector("_Rect",new Vector4(0,0,1,1));mat.SetVector("_PreviousRect",new Vector4(0,0,1,1));
            mat.SetFloat("_Blend",1);mat.SetFloat("_Opacity",alpha);mat.SetFloat("_Timeline",time);mat.SetFloat("_Activity",activity);
            mat.SetColor("_Tint",Color.Lerp(Color.white,new Color(.67f,.7f,.73f,1),E((time-179.2f)/7.68f)*.45f));
            if(haloMaterials!=null&&slot<haloMaterials.Length&&haloMaterials[slot]!=null)
            {
                haloMaterials[slot].SetFloat("_Timeline",time);haloMaterials[slot].SetFloat("_Intensity",alpha*(slot==0?.42f:.24f));
            }
            if(alpha>.001f)visibleCards++;
        }
    }
}
