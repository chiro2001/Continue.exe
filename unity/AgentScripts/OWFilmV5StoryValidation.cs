using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using OldWebMap;
using Object=UnityEngine.Object;

public static class OWFilmV5StoryValidation
{
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v5");
    static void Need(bool value,string message){if(!value)throw new Exception(message);}
    static void Active(Renderer r,string message){Need(r!=null&&r.enabled&&r.gameObject.activeInHierarchy,message);}
    static string FileName(Texture texture)=>texture==null?"":Path.GetFileName(AssetDatabase.GetAssetPath(texture));
    public static object Run()
    {
        Need(!EditorApplication.isPlaying&&!EditorApplication.isCompiling,"Stopped compiled editor required");
        var run=Object.FindObjectsOfType<OWFilmV5Presentation>(true).Single(r=>r.gameObject.scene==SceneManager.GetActiveScene());
        var d=run.details;var m=run.media;var p=run.provenance;var rows=new List<object>();
        try
        {
            run.Waiting();Need(d.openingRoom.gameObject.activeInHierarchy&&d.openingWalls.Length==6,"Opening is not enclosed");
            Need(d.openingWalls.All(t=>t.GetComponent<MeshFilter>().sharedMesh.bounds.size.sqrMagnitude>2.9f),"An opening face is still a flat screen");
            var wallMaterials=d.openingWalls.Select(t=>t.GetComponent<Renderer>().sharedMaterial).Distinct().ToArray();Need(wallMaterials.Length==1&&wallMaterials[0].shader.name=="OldWebMap/FilmV5/Waiting Room Walls"&&wallMaterials[0].renderQueue<2500,"Wall paint or opaque depth mismatch");
            Need(!d.creditsRoot.activeInHierarchy&&!p.contentRoot.activeInHierarchy,"Later content leaks before start");
            run.Preview(.12f);Need(d.sampledRibbonState==1&&d.sampledRibbonHead<d.ribbonPixels[1]*.05f&&d.textRibbonMaterial.mainTexture==d.ribbonFlowAtlas,"First sentence is already a completed ring");
            foreach(float t in new[]{5.12f,15.36f,25.6f,35.84f,46.08f,56.32f,66.56f,73f,81f,95f,108f,116f,125f,137f,148f,159f,170f,183f,202f,213f,226f,244f})
            {
                run.Preview(t);int cue=d.sampledRibbonState;
                Need(t>=d.ribbonStarts[cue]&&t<d.ribbonEnds[cue],"Ribbon content is out of phase at "+t);
                Need(d.textRibbonMaterial.mainTexture==d.ribbonFlowAtlas&&d.ribbonFlowPixels.Length>0,"Cumulative ribbon flow was replaced at "+t);
                if(t>=30.72f&&t<40.96f&&m.sampledRelated!=null)Need(!m.sampledRelated.Contains(58),"2014 shutdown image leaks into the earlier translation pool");
                rows.Add(new{time=t,ribbon=cue,head=d.sampledRibbonHead,fall=d.sampledRibbonFall,chronology=m.sampledChronology,theft=p.sampledMain,cards=p.visibleCards});
            }
            foreach(int cue in Enumerable.Range(0,m.chronologyStarts.Length))
            {
                float t=(m.chronologyStarts[cue]+m.chronologyEnds[cue])*.5f;run.Preview(t);
                Need(m.sampledChronology==cue,"Dated cue not selected");Active(m.chronologyImage,"Dated source invisible");
                Need(m.chronologyMaterial.mainTexture==m.sources[m.chronologyIndices[cue]],"Dated source binding mismatch");
                Need(!m.historyRoot.activeInHierarchy&&!m.filmPlane.gameObject.activeInHierarchy,"Unrelated VRC footage overlays the dated source");
            }
            run.Preview(126);Need(p.sampledMain<0&&p.visibleCards<=2&&p.sampledSticker<0&&p.cardMaterials[1].mainTexture==p.plates[11]&&p.cards[1].localPosition.x>2,"Permission prelude is not a supporting side image");
            run.Preview(133.6f);var held=p.cardMaterials[0].mainTexture;var heldPos=p.cards[0].position;Active(p.cardRenderers[0],"Private source is not held at the climax");
            run.Preview(135.6f);Need(p.cardMaterials[0].mainTexture==held&&Vector3.Distance(heldPos,p.cards[0].position)<.25f,"Central source does not hold long enough to see");
            run.Preview(149.76f);Need(p.sampledSticker==13&&FileName(p.cardMaterials[3].mainTexture)=="provenance-14.png","Complete postmark is not used");
            Need(p.plates[13].width==1400&&p.plates[13].height==1000,"Postmark was reduced to its old word rectangle");
            foreach(float t in new[]{138f,146f,158f,170f,182f})
            {
                run.Preview(t);Need(p.visibleCards>=2,"Theft attention area lacks related companion material at "+t);
                foreach(int slot in Enumerable.Range(0,p.cards.Length))if(p.cards[slot].gameObject.activeInHierarchy)
                {Need(p.cardMaterials[slot].mainTexture!=null,"White source card");var backing=p.cards[slot].Find("Attached backing");Need(backing!=null&&Vector3.Distance(backing.position,p.cards[slot].position+p.cards[slot].forward*.035f)<.001f,"Detached emphasis backing");}
            }
            run.Preview(205);if(d.excludedArt!=null)Need(d.excludedArt.All(g=>g==null||!g.activeInHierarchy),"Curated image is still visible");
            run.Preview(246);Need(run.finalWord.text=="创作仍将继续"&&run.finalWordRoot.activeInHierarchy&&run.finalWordRoot.transform.position.y>.8f,"End phrase or height mismatch");
            run.Finished();Need(d.creditsRoot.activeInHierarchy,"Separate credits missing after the end");
            var boards=d.creditsRoot.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Readable credits").ToArray();Need(boards.Length>=2&&boards.All(r=>r.enabled&&r.sharedMaterial.mainTexture!=null),"Credits pages are not actual rendered boards");
            var shelves=Object.FindObjectsOfType<MeshRenderer>(true).Where(r=>r.gameObject.scene==SceneManager.GetActiveScene()&&r.name=="Living_Shelf").ToArray();Need(shelves.Length>=4,"Shelf instances missing");
            Need(shelves.All(r=>FileName(r.sharedMaterials[0].mainTexture)=="archive-shelf-wood017-albedo.jpg"),"A shelf still wears a book cover");
            var books=Object.FindObjectsOfType<MeshRenderer>(true).Where(r=>r.gameObject.scene==SceneManager.GetActiveScene()&&r.name=="Books").ToArray();Need(books.Length>=4&&books.All(r=>r.sharedMaterials.Length==3&&r.sharedMaterials[1]!=r.sharedMaterials[2]&&FileName(r.sharedMaterials[1].mainTexture)=="archive-books-book4.png"&&FileName(r.sharedMaterials[2].mainTexture)=="archive-books-book5.png"),"Distinct embedded book covers are still collapsed");
            var canopy=run.finalWorld.Find("Refined canopy");Need(canopy!=null&&canopy.gameObject.activeInHierarchy&&canopy.GetComponentsInChildren<MeshRenderer>(true).Count(r=>r.enabled)>=70,"Refined canopy absent");
            var oldCanopy=run.finalWorld.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="BranchCanopy");Need(oldCanopy.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled),"Old and new canopy overlap");
            var result=new{success=true,ribbonCues=d.ribbonStates.Length,datedCues=m.chronologyStarts.Length,redactedSources=p.plates.Length,creditsPages=boards.Length,shelves=shelves.Length,books=books.Length,canopyMeshes=canopy.GetComponentsInChildren<MeshRenderer>(true).Length,rows,scope="Authored Unity scene, actual imported resources, renderer gates and sampled transforms; screenshots and live Udon are separate checks"};
            Directory.CreateDirectory(Tmp+"/reports");string json=JsonConvert.SerializeObject(result,Formatting.Indented);File.WriteAllText(Tmp+"/reports/v5-story-validation.json",json);return json;
        }
        catch(Exception e){Directory.CreateDirectory(Tmp+"/reports");File.WriteAllText(Tmp+"/reports/v5-story-failure.json",JsonConvert.SerializeObject(new{success=false,error=e.ToString(),rows},Formatting.Indented));throw;}
        finally{run.Waiting();run.offlinePreview=false;}
    }
}
