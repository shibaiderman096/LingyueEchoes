using System;
using System.IO;
using System.Linq;
using LingyueEchoes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WrathGeneralArtChecks
{
    const string PlayKey="Lingyue.WrathGeneralArtCheck";
    static int playFrame;
    static WrathGeneralArtChecks(){EditorApplication.playModeStateChanged+=OnPlayState;}
    static void Need(bool value,string message){if(!value)throw new Exception("Wrath general art: "+message);}

    [MenuItem("Lingyue/Check general animations")]
    public static void Run(){
        var art=new WrathGeneralArt();
        var fighter=new Fighter(2,3830,870){face=1};
        foreach(string action in new[]{"idle","walk","stun"}){
            fighter.state=action=="walk"?"run":action=="stun"?"broken":"idle";
            int fps=action=="idle"?8:action=="walk"?16:12;
            Sprite first=null;
            for(int i=0;i<16;i++){
                string path="Assets/Resources/"+WrathGeneralArt.ResourceRoot+action+"/"+action+"-"+(i+1).ToString("00")+".png";
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                Need(sprite&&importer,"all 48 resources import as sprites: "+path);
                Need(sprite.rect.width==WrathGeneralArt.CanvasPixels&&sprite.rect.height==WrathGeneralArt.CanvasPixels,"shared canvas: "+path);
                Need(Math.Abs(sprite.pixelsPerUnit-WrathGeneralArt.PixelsPerUnit)<.01&&
                    Vector2.Distance(sprite.pivot,new Vector2(WrathGeneralArt.RootX,WrathGeneralArt.FootY)*WrathGeneralArt.CanvasPixels)<.01,"shared root and scale: "+path);
                Need(importer.filterMode==FilterMode.Point&&!importer.mipmapEnabled&&importer.alphaIsTransparency&&
                    importer.textureCompression==TextureImporterCompression.Uncompressed,"pixel/alpha import: "+path);
                double time=action=="stun"?(i<12?(i+.01)/18:12d/18+(i-12+.01)/8):(i+.01)/fps;
                Need(art.Pose(fighter,null,7,time)==sprite,"state selects every authored frame: "+path);
                if(i==0)first=sprite;
            }
            if(action=="stun"){
                Need(art.Pose(fighter,null,7,12d/18+4d/8).name=="stun-13","stun entry plays once, then loops the kneeling hold");
                Need(art.Pose(fighter,null,7,12d/18+16d/8).name=="stun-13","a prolonged break never stands back up");
            }else Need(art.Pose(fighter,null,7,16d/fps)==first,"loop returns to first frame: "+action);
        }
        fighter.state="stagger";Need(art.Pose(fighter,null,7,0).name=="stun-01","ordinary stagger retains general identity");
        fighter.state="attack";fighter.move=WrathRoute.General.opening[0];
        Need(art.Pose(fighter,null,7,1).name=="idle-01","unauthored attacks retain the general's own ready pose");
        fighter.move=null;
        Need(art.Pose(fighter,"broken",0,0).name=="stun-01","scripted defeat uses general art");
        Debug.Log("WRATH_GENERAL_ART_CHECKS_PASSED: 48 frames, state selection, loop seams, shared scale/pivot and pixel import");
    }

    // Run through the actual chapter's PixelActor after entering Play mode and advancing frames.
    public static void PlayBatch(){
        foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/WrathGeneral"}))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid),ImportAssetOptions.ForceUpdate);
        WrathChapterChecks.Run();
        SessionState.SetBool(PlayKey,true);
        EditorSceneManager.OpenScene(WrathSceneSetup.ScenePath);
        EditorApplication.EnterPlaymode();
    }
    static void OnPlayState(PlayModeStateChange state){
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(PlayKey,false))return;
        playFrame=Time.frameCount;EditorApplication.update+=CheckPlaying;
    }
    static void CheckPlaying(){
        if(Time.frameCount<playFrame+8)return;
        EditorApplication.update-=CheckPlaying;SessionState.SetBool(PlayKey,false);
        GameObject cameraObject=null;Texture2D review=null;
        try{
            Need(Application.isPlaying,"Play mode is advancing");Run();
            var chapter=UnityEngine.Object.FindAnyObjectByType<WrathChapter>();
            var game=chapter.GetComponent<LingyueGame>();game.enabled=false;
            var art=chapter.UseActorArt(true,WrathRoute.Fights.Length-1);
            Need(art is WrathGeneralArt&&chapter.UseActorArt(false,0)==null&&chapter.UseActorArt(false,3)==null,
                "the sole boss uses general art and deleted approach encounters have none");
            chapter.SetInside(false);Need(!chapter.IsInside,"arrival and retry show the entrance");
            chapter.SetInside(true);Need(chapter.IsInside,"door interaction switches to the interior");
            var actorObject=chapter.world.Find("Guardian").gameObject;
            actorObject.layer=31;actorObject.SetActive(true);
            var renderer=actorObject.GetComponent<SpriteRenderer>();
            var actor=new PixelActor(actorObject,false){ChapterArt=art};
            var fighter=new Fighter(2,3830,870){face=1};
            cameraObject=new GameObject("General animation review camera");
            var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.35f;
            camera.transform.position=new Vector3(38.3f,-.25f,-10);camera.cullingMask=1<<31;
            camera.clearFlags=CameraClearFlags.SolidColor;
            review=new Texture2D(1152,768,TextureFormat.RGB24,false);
            int panel=0;
            foreach(string action in new[]{"idle","walk","stun"}){
                fighter.state=action=="walk"?"run":action=="stun"?"broken":"idle";
                int fps=action=="idle"?8:action=="walk"?16:12;
                for(int i=0;i<16;i++){
                    double time=action=="stun"?(i<12?i/18d:12d/18+(i-12)/8d):i/(double)fps;
                    int frame=1000+panel*200+(int)Math.Ceiling(time*60);
                    fighter.age=(int)Math.Ceiling(time*60);
                    actor.Draw(fighter,frame,idleDelta:i==0?0:1d/fps);
                    Need(renderer.sprite.name==action+"-"+(i+1).ToString("00"),"PixelActor advances "+action+" pose "+i);
                    Need(Math.Abs(actorObject.transform.localPosition.y+.95f)<.0001,"shared floor root");
                }
                foreach(int face in new[]{1,-1}){
                    fighter.face=face;
                    actor.Draw(fighter,5000+panel,pose:action,poseSeconds:action=="stun"?1:0);
                    Need(actorObject.transform.localScale.x==face,"mirrored facing");
                    camera.backgroundColor=face==1?new Color(.12f,.10f,.15f):new Color(.90f,.87f,.83f);
                    var target=RenderTexture.GetTemporary(384,384,24);var previous=RenderTexture.active;
                    try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                        review.ReadPixels(new Rect(0,0,384,384),panel%3*384,(1-panel/3)*384);
                    }finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
                    panel++;
                }
            }
            string output=Path.Combine(Application.dataPath,"../.utmp/wrath-general/play-review.png");
            Directory.CreateDirectory(Path.GetDirectoryName(output));review.Apply();File.WriteAllBytes(output,review.EncodeToPNG());
            Debug.Log("WRATH_GENERAL_ART_PLAY_PASSED: chapter actor, all 48 sampled poses, both facings, fixed floor, light/dark alpha render; "+output);
            EditorApplication.Exit(0);
        }catch(Exception exception){Debug.LogException(exception);EditorApplication.Exit(1);}
        finally{if(review)UnityEngine.Object.DestroyImmediate(review);if(cameraObject)UnityEngine.Object.DestroyImmediate(cameraObject);}
    }
}
