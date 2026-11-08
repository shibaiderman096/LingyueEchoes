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
        foreach(string action in new[]{"idle","walk","hurt","stun"}){
            fighter.state=action=="walk"?"run":action=="stun"?"broken":action=="hurt"?"hurt":"idle";
            int fps=action=="idle"?8:action=="walk"?16:12;
            Sprite first=null;
            for(int i=0;i<16;i++){
                string path="Assets/Resources/"+WrathGeneralArt.ResourceRoot+action+"/"+action+"-"+(i+1).ToString("00")+".png";
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                Need(sprite&&importer,"all 64 resources import as sprites: "+path);
                Need(sprite.rect.width==WrathGeneralArt.CanvasPixels&&sprite.rect.height==WrathGeneralArt.CanvasPixels,"shared canvas: "+path);
                Need(Math.Abs(sprite.pixelsPerUnit-WrathGeneralArt.PixelsPerUnit)<.01&&
                    Vector2.Distance(sprite.pivot,new Vector2(WrathGeneralArt.RootX,WrathGeneralArt.FootY)*WrathGeneralArt.CanvasPixels)<.01,"shared root and scale: "+path);
                Need(importer.filterMode==FilterMode.Point&&!importer.mipmapEnabled&&importer.alphaIsTransparency&&
                    importer.textureCompression==TextureImporterCompression.Uncompressed,"pixel/alpha import: "+path);
                double time=action=="stun"?(i+.01)/WrathGeneralArt.StunEntryFps:action=="hurt"?(i+.01)*(Rules.hurtFrames-2)/(15d*60):(i+.01)/fps;
                Need(art.Pose(fighter,null,7,time)==sprite,"state selects every authored frame: "+path);
                if(i==0)first=sprite;
            }
            if(action=="idle"||action=="walk")Need(art.Pose(fighter,null,7,16d/fps)==first,"loop returns to first frame: "+action);
        }
        foreach(string state in new[]{"hurt","stagger"}){
            fighter.state=state;
            int duration=state=="hurt"?Rules.hurtFrames:Rules.staggerFrames;
            for(int i=0;i<16;i++)Need(art.Pose(fighter,null,7,(i+.01)*(duration-2)/(15d*60)).name=="hurt-"+(i+1).ToString("00"),state+" uses the complete hurt reaction");
            Need(art.Pose(fighter,null,7,duration/60d).name=="hurt-16",state+" finishes once without looping");
        }
        fighter.state="broken";
        foreach(int duration in new[]{150,90,210}){
            art.BossStaggerFrames=duration;
            double end=(duration-2)/60d,motion=Math.Min(15d/24,end/3),rise=end-motion;
            Need(art.Pose(fighter,null,7,0).name=="stun-01","break begins standing");
            for(int i=0;i<16;i++)Need(art.Pose(fighter,null,7,(i+.001)*motion/15).name=="stun-"+(i+1).ToString("00"),"break kneels in authored order: "+duration);
            foreach(double time in new[]{motion,(motion+rise)/2,rise-.001})Need(art.Pose(fighter,null,7,time).name=="stun-16","break holds one stable kneeling frame: "+duration);
            for(int i=0;i<16;i++)Need(art.Pose(fighter,null,7,rise+(i+.001)*motion/15).name=="stun-"+(16-i).ToString("00"),"break rises in reverse order: "+duration);
            Need(art.Pose(fighter,null,7,end).name=="stun-01","break finishes standing: "+duration);
        }
        fighter.hp=0;
        Need(art.Pose(fighter,null,7,10).name=="stun-16","death never rises");
        fighter.hp=870;art.CanRecoverFromBreak=false;
        Need(art.Pose(fighter,null,7,10).name=="stun-16","finisher blocks break recovery");
        art.CanRecoverFromBreak=true;
        Need(art.Pose(fighter,"broken",7,10).name=="stun-16","scripted defeat never rises");
        fighter.state="attack";fighter.move=WrathRoute.General.opening[0];
        Need(art.Pose(fighter,null,7,1).name=="idle-01","unauthored attacks retain the general's own ready pose");
        fighter.move=null;
        Need(art.Pose(fighter,"broken",0,0).name=="stun-01","scripted defeat uses general art");
        Debug.Log("WRATH_GENERAL_ART_CHECKS_PASSED: 64 frames, hurt reactions, timed break hold and recovery, loop seams, shared scale/pivot and pixel import");
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
            chapter.SetInside(false);Need(!chapter.IsInside&&chapter.exteriorScenery.enabled&&chapter.scenery.All(r=>!r.enabled)&&chapter.entranceDoor.flipX,"arrival and retry show the exterior with the mirrored door");
            chapter.SetInside(true);Need(chapter.IsInside&&!chapter.exteriorScenery.enabled&&chapter.scenery.All(r=>r.enabled),"door interaction switches to the interior");
            var actorObject=chapter.world.Find("Guardian").gameObject;
            actorObject.layer=31;actorObject.SetActive(true);
            var renderer=actorObject.GetComponent<SpriteRenderer>();
            var actor=new PixelActor(actorObject,false){ChapterArt=art};
            var combat=new Combat{ai=false};combat.ResetBoss(3830,870,WrathRoute.General);
            combat.player.x=3300;combat.BreakBossPosture();
            ((WrathGeneralArt)art).BossStaggerFrames=combat.BossStaggerFrames;
            int held=0,rose=0;Sprite previousPose=null;
            for(int tick=0;tick<combat.BossStaggerFrames;tick++){
                combat.Step();actor.Draw(combat.boss,combat.frame);
                if(combat.boss.state!="broken")break;
                double elapsed=Math.Max(0,combat.boss.age-1)/60d;
                Need(renderer.sprite.name=="stun-"+(WrathGeneralArt.BreakFrame(elapsed,combat.BossStaggerFrames,true)+1).ToString("00"),"actual combat age drives the complete break");
                if(renderer.sprite.name=="stun-16")held++;
                if(previousPose&&String.CompareOrdinal(renderer.sprite.name,previousPose.name)<0)rose++;
                previousPose=renderer.sprite;
            }
            Need(held>30&&rose>=12&&previousPose.name=="stun-01"&&combat.boss.state=="idle","combat holds down, then stands before the break state ends");
            foreach(string reaction in new[]{"hurt","stagger"}){
                combat.boss.state=reaction;combat.boss.age=1;combat.boss.move=null;actor.Draw(combat.boss,5000);
                Need(renderer.sprite.name=="hurt-01","actual PixelActor selects the first reaction pose");
                combat.boss.age=(reaction=="hurt"?Rules.hurtFrames:Rules.staggerFrames)-1;actor.Draw(combat.boss,5035);
                Need(renderer.sprite.name=="hurt-16","actual PixelActor reaches the last reaction pose");
            }
            var fighter=new Fighter(2,3830,870){face=1};
            cameraObject=new GameObject("General animation review camera");
            var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.35f;
            camera.transform.position=new Vector3(38.3f,-.25f,-10);camera.cullingMask=1<<31;
            camera.clearFlags=CameraClearFlags.SolidColor;
            review=new Texture2D(1536,768,TextureFormat.RGB24,false);
            int panel=0;
            foreach(string action in new[]{"idle","walk","hurt","stun"}){
                fighter.state=action=="walk"?"run":action=="stun"?"broken":action=="hurt"?"hurt":"idle";
                int fps=action=="idle"?8:action=="walk"?16:12;
                for(int i=0;i<16;i++){
                    double time=action=="stun"?(i+.01)/WrathGeneralArt.StunEntryFps:action=="hurt"?(i+.01)*(Rules.hurtFrames-2)/(15d*60):i/(double)fps;
                    int frame=1000+panel*200+(int)Math.Ceiling(time*60);
                    fighter.age=1+(int)Math.Ceiling(time*60);
                    actor.Draw(fighter,frame,pose:action,poseSeconds:time,idleDelta:i==0?0:1d/fps);
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
                        review.ReadPixels(new Rect(0,0,384,384),panel%4*384,(1-panel/4)*384);
                    }finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
                    panel++;
                }
            }
            string output=Path.Combine(Application.dataPath,"../.utmp/wrath-general/play-review.png");
            Directory.CreateDirectory(Path.GetDirectoryName(output));review.Apply();File.WriteAllBytes(output,review.EncodeToPNG());
            Debug.Log("WRATH_GENERAL_ART_PLAY_PASSED: chapter actor, all 64 sampled poses, both facings, fixed floor, light/dark alpha render; "+output);
            EditorApplication.Exit(0);
        }catch(Exception exception){Debug.LogException(exception);EditorApplication.Exit(1);}
        finally{if(review)UnityEngine.Object.DestroyImmediate(review);if(cameraObject)UnityEngine.Object.DestroyImmediate(cameraObject);}
    }
}
