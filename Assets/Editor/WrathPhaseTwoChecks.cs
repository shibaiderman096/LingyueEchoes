using System;
using System.IO;
using LingyueEchoes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WrathPhaseTwoChecks
{
    const string Key="Lingyue.WrathPhaseTwoChecks";
    static int started;
    static WrathPhaseTwoChecks(){EditorApplication.playModeStateChanged+=OnPlay;}
    static void Need(bool ok,string message){if(!ok)throw new Exception("Wrath phase two: "+message);}
    static Sprite Frame(string action,int index)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/"+WrathGeneralArt.Phase2ResourceRoot+action+"/"+action+"-"+index.ToString("00")+".png");
    public static void Run(){
        var art=new WrathGeneralArt{CurrentPhase=2};var fighter=new Fighter(2,3830,870);
        foreach(string action in new[]{"idle","walk","hurt","stun","phase"}){
            fighter.state=action=="walk"?"run":action=="stun"?"broken":action;
            for(int i=0;i<16;i++){
                string path="Assets/Resources/"+WrathGeneralArt.Phase2ResourceRoot+action+"/"+action+"-"+(i+1).ToString("00")+".png";
                var sprite=Frame(action,i+1);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                Need(sprite&&importer,"80 unique sprite paths: "+path);
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                Need(importer.textureType==TextureImporterType.Sprite&&importer.spriteImportMode==SpriteImportMode.Single&&settings.spriteMeshType==SpriteMeshType.FullRect,"single full rectangle sprite: "+path);
                Need(sprite.rect.size==new Vector2(384,384)&&Math.Abs(sprite.pixelsPerUnit-180)<.01&&Vector2.Distance(sprite.pivot,new Vector2(230,37))<.01,"canvas, PPU, pivot: "+path);
                Need(importer.filterMode==FilterMode.Point&&!importer.mipmapEnabled&&importer.alphaIsTransparency&&importer.textureCompression==TextureImporterCompression.Uncompressed,"pixel import: "+path);
                double seconds=action=="phase"?(i+.001)*(art.PhaseTransitionFrames-2)/(15d*60):action=="hurt"?(i+.001)*(Rules.hurtFrames-2)/(15d*60):action=="stun"?(i+.001)/24:(i+.001)/(action=="walk"?16:8);
                Need(art.Pose(fighter,null,7,seconds)==sprite,"all authored frames: "+path);
            }
        }
        foreach(int duration in new[]{90,120}){
            art.PhaseTransitionFrames=duration;fighter.state="phase";
            Need(art.Pose(fighter,null,7,0)==Frame("phase",1),"transition first frame");
            Need(art.Pose(fighter,null,7,(duration-2)/60d)==Frame("phase",16),"configured transition last visible frame");
        }
        fighter.state="broken";
        double end=(art.BossStaggerFrames-2)/60d,motion=Math.Min(15d/24,end/3),rise=end-motion;
        Need(art.Pose(fighter,null,7,(motion+rise)/2)==Frame("stun",16),"holds kneeling frame");
        for(int i=0;i<16;i++)Need(art.Pose(fighter,null,7,rise+(i+.001)*motion/15)==Frame("stun",16-i),"reverse recovery "+i);
        fighter.state="idle";art.CurrentPhase=1;
        Need(art.Pose(fighter,null,7,0)==Resources.Load<Sprite>(WrathGeneralArt.ResourceRoot+"idle/idle-01"),"phase one restores after phase two");
        Debug.Log("WRATH_PHASE_TWO_CHECKS_PASSED: 80 sprite imports and samples, configured transition timing, reverse recovery and phase reset");
    }
    public static void RunBatch(){try{Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
    public static void PlayBatch(){SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(WrathSceneSetup.ScenePath);EditorApplication.EnterPlaymode();}
    static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){started=Time.frameCount;EditorApplication.update+=CheckPlay;}}
    static void CheckPlay(){
        if(Time.frameCount<started+8)return;
        EditorApplication.update-=CheckPlay;SessionState.SetBool(Key,false);
        GameObject actorObject=null,cameraObject=null;Texture2D review=null;
        try{
            Run();var chapter=UnityEngine.Object.FindAnyObjectByType<WrathChapter>();Need(chapter,"actual chapter loaded");chapter.GetComponent<LingyueGame>().enabled=false;
            actorObject=new GameObject("Phase two review actor",typeof(SpriteRenderer));actorObject.layer=31;
            var renderer=actorObject.GetComponent<SpriteRenderer>();var art=(WrathGeneralArt)chapter.UseActorArt(true,0);var actor=new PixelActor(actorObject,false){ChapterArt=art};
            foreach(int duration in new[]{90,120}){
                var definition=BossBehaviorCatalog.WrathGeneral.Copy();definition.phases[1].transitionFrames=duration;
                var combat=new Combat{ai=false};combat.ResetBoss(3830,870,OpponentPattern.FromBoss(definition));combat.boss.hp=400;
                combat.Step();Need(combat.phase==2&&combat.boss.state=="phase","real half HP enters transition");
                chapter.PrepareActorArt(combat,false);Need(art.PhaseTransitionFrames==duration,"active definition timing sync");
                actor.Draw(combat.boss,combat.frame);Need(renderer.sprite==Frame("phase",1),"real transition starts frame one");
                int guard=0;Sprite last=null;
                while(combat.boss.state=="phase"&&guard++<duration+2){actor.Draw(combat.boss,combat.frame);last=renderer.sprite;combat.Step();}
                Need(guard<duration+2&&last==Frame("phase",16)&&combat.boss.state=="idle","real transition reaches frame sixteen then idle");
                chapter.PrepareActorArt(combat,false);actor.Draw(combat.boss,combat.frame);Need(renderer.sprite==Frame("idle",1),"real combat idle switches to phase two");
                combat.ResetBoss(3830,870,WrathRoute.General);chapter.PrepareActorArt(combat,false);Need(art.CurrentPhase==1,"retry resets phase");
                chapter.PrepareActorArt(combat,true);Need(art.CurrentPhase==2,"weapon ash selects empty hand phase two");
                chapter.PrepareActorArt(combat,false);Need(art.CurrentPhase==1,"weapon ash selection is not sticky");
            }
            art.CurrentPhase=2;cameraObject=new GameObject("Phase two review camera",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.35f;camera.transform.position=new Vector3(38.3f,-.25f,-10);camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;
            review=new Texture2D(1920,768,TextureFormat.RGB24,false);int panel=0;
            foreach(string action in new[]{"idle","walk","hurt","stun","phase"})foreach(int face in new[]{1,-1}){
                var fighter=new Fighter(2,3830,870){face=face,state=action};
                for(int i=0;i<16;i++){
                    double time=action=="phase"?(i+.001)*(art.PhaseTransitionFrames-2)/(15d*60):action=="hurt"?(i+.001)*(Rules.hurtFrames-2)/(15d*60):action=="stun"?(i+.001)/24:(i+.001)/(action=="walk"?16:8);
                    actor.Draw(fighter,1000+panel*200+i,pose:action,poseSeconds:time);
                    Need(renderer.sprite==Frame(action,i+1),"PixelActor samples every frame in both directions");
                    Need(actorObject.transform.localScale.x==face&&Math.Abs(actorObject.transform.localPosition.y+.95f)<.0001,"mirrored fixed floor");
                }
                camera.backgroundColor=face==1?new Color(.12f,.1f,.15f):new Color(.9f,.87f,.83f);
                var target=RenderTexture.GetTemporary(384,384,24);var previous=RenderTexture.active;
                try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;review.ReadPixels(new Rect(0,0,384,384),panel%5*384,(1-panel/5)*384);}finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}panel++;
            }
            string output=Path.Combine(Application.dataPath,"../.utmp/wrath-phase2/play-review.png");Directory.CreateDirectory(Path.GetDirectoryName(output));review.Apply();File.WriteAllBytes(output,review.EncodeToPNG());
            Debug.Log("WRATH_PHASE_TWO_PLAY_PASSED: native chapter, real HP transition at 90/120 ticks, retry/ash reset, 160 PixelActor samples, mirrored floor review; "+output);EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}finally{if(review)UnityEngine.Object.DestroyImmediate(review);if(actorObject)UnityEngine.Object.DestroyImmediate(actorObject);if(cameraObject)UnityEngine.Object.DestroyImmediate(cameraObject);}
    }
}
