using System;
using System.IO;
using System.Linq;
using LingyueEchoes;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CombatPresentationChecks
{
    const string PlayKey="Lingyue.CombatPresentationCheck";
    static int assertions,playFrame;
    static CombatPresentationChecks(){EditorApplication.playModeStateChanged+=OnPlayState;}
    static void Need(bool value,string message){assertions++;if(!value)throw new Exception("Combat presentation: "+message);}
    [MenuItem("Lingyue/Check combat presentation")]
    public static void Run(){Check(null);}
    static void Check(Texture2D sheet){
        assertions=0;
        var root=new GameObject("Combat presentation checks");
        var cameraObject=new GameObject("Combat check camera");
        var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.6f;
        camera.transform.position=new Vector3(1000,0,-10);camera.backgroundColor=new Color(.2f,.22f,.25f);camera.clearFlags=CameraClearFlags.SolidColor;
        root.transform.position=new Vector3(1000,0,0);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Quad);floor.name="Check ground";floor.transform.SetParent(root.transform,false);
        floor.transform.localPosition=new Vector3(0,-1.03f,1);floor.transform.localScale=new Vector3(8,.16f,1);
        var material=new Material(Shader.Find("Sprites/Default"));material.color=new Color(.6f,.4f,.25f);floor.GetComponent<MeshRenderer>().sharedMaterial=material;
        try{
            var heroObject=new GameObject("Check hero");heroObject.transform.SetParent(root.transform,false);heroObject.AddComponent<SpriteRenderer>().sortingOrder=12;
            var hero=new PixelActor(heroObject,true);var p=new Fighter(1,0,0){state="dead"};
            for(int face=-1;face<=1;face+=2){p.face=face;hero.Draw(p,90,pose:"dead",poseSeconds:2);
                Need(Math.Abs(heroObject.transform.localPosition.y+.995f)<.00001,"collapsed body meets the ground in facing "+face);
                Need(heroObject.GetComponent<SpriteRenderer>().sprite==Resources.Load<Sprite>("pixel8/frames16/dead/dead-16"),"death holds its final authored pose");
                if(sheet)Capture(camera,sheet,face==1?1:0);
                hero.Draw(p,91,pose:"dead",poseSeconds:0);
                Need(Math.Abs(heroObject.transform.localPosition.y+.95f)<.00001,"reverse arrival restores the standing anchor");}
            heroObject.SetActive(false);
            string[] clips={"cut_fx1","cut_fx1","cut_fx2","cut_fx4","cut_fx4","cut_fx3"};
            for(int move=0;move<6;move++)for(int face=-1;face<=1;face+=2){
                var c=new Combat{ai=false,god=true,boundsMin=-2000,boundsMax=2000,phase=2};c.player.x=face*500;c.boss.x=0;c.DebugBoss(move);
                for(int i=0;i<120&&!c.visuals.Any(v=>v.kind=="slash"&&v.owner==2);i++)c.Step();
                var cue=c.visuals.LastOrDefault(v=>v.kind=="slash"&&v.owner==2);Need(cue!=null,"move emits contact effect "+move);
                var bossObject=new GameObject("Check guardian");bossObject.transform.SetParent(root.transform,false);bossObject.AddComponent<SpriteRenderer>().sortingOrder=10;
                var boss=new PixelActor(bossObject,false);boss.Draw(c.boss,c.frame);
                var effectRoot=new GameObject("Check effects");effectRoot.transform.SetParent(root.transform,false);var effects=new PixelEffects(effectRoot.transform);effects.Draw(c);
                var expected=PixelWorld.Definition("pixel8",clips[move]);
                var renderer=effectRoot.GetComponentsInChildren<SpriteRenderer>().SingleOrDefault(r=>r.gameObject.activeSelf&&r.sprite==Resources.Load<Sprite>(expected.frames[0]));
                Need(renderer,"move resolves its matching contact art "+move+" facing "+face);
                Need(Math.Sign(renderer.transform.localScale.x)==face,"contact art mirrors with the committed facing");
                Need(renderer.sharedMaterial.shader.name=="LingyueEchoes/BossAttackTint"&&
                    (cue.danger?renderer.color.g<.2f:renderer.color.g>.8f),"red means unblockable; yellow means parryable");
                if(sheet)Capture(camera,sheet,2+move*2+(face==1?1:0));
                var at=renderer.transform.localPosition;var sprite=renderer.sprite;c.hitstop=3;for(int i=0;i<3;i++)c.Step();effects.Draw(c);
                Need(renderer.sprite==sprite&&renderer.transform.localPosition==at,"hit stop freezes effect pose and anchor");
                c.boss.face=-face;effects.Draw(c);Need(!renderer.gameObject.activeSelf,"turning hides the previous swing");
                UnityEngine.Object.DestroyImmediate(effectRoot);UnityEngine.Object.DestroyImmediate(bossObject);
            }
            Debug.Log("COMBAT_PRESENTATION_CHECKS_PASSED: "+assertions+" assertions; death grounding, reverse arrival, six Boss contacts, both facings, hit stop and stale effects");
        }finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(material);}
    }
    static void Capture(Camera camera,Texture2D sheet,int panel){
        var target=RenderTexture.GetTemporary(512,256,24);var old=RenderTexture.active;
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;sheet.ReadPixels(new Rect(0,0,512,256),panel%2*512,(6-panel/2)*256);}
        finally{camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);}
    }
    public static void RunBatch(){try{ExecutionGateChecks.Run();Run();BossAttackAlignmentChecks.Run();MigrationChecks.Run();WrathChapterChecks.Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
    public static void PlayBatch(){SessionState.SetBool(PlayKey,true);EditorApplication.EnterPlaymode();}
    static void OnPlayState(PlayModeStateChange state){
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(PlayKey,false))return;
        playFrame=Time.frameCount;EditorApplication.update+=CheckPlaying;
    }
    static void CheckPlaying(){
        if(Time.frameCount<playFrame+4)return;
        EditorApplication.update-=CheckPlaying;SessionState.SetBool(PlayKey,false);
        Texture2D sheet=null;
        try{
            Need(Application.isPlaying,"checks execute in Play mode after advancing frames");
            sheet=new Texture2D(1024,1792,TextureFormat.RGB24,false);Check(sheet);
            string output=Path.Combine(Application.dataPath,"../.utmp/combat-fix/play-contacts.png");Directory.CreateDirectory(Path.GetDirectoryName(output));sheet.Apply();File.WriteAllBytes(output,sheet.EncodeToPNG());
            ExecutionGateChecks.Run();BossAttackAlignmentChecks.Run();Debug.Log("COMBAT_PRESENTATION_PLAY_PASSED: actual Play frames advanced and all contact poses rendered");EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}finally{if(sheet)UnityEngine.Object.DestroyImmediate(sheet);}
    }
}
