using System;
using System.IO;
using System.Reflection;
using LingyueEchoes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WrathPhaseWipeChecks
{
    const string Key="Lingyue.WrathPhaseWipeChecks";
    static int started,captureAt=-1,captureStage;
    static string screenshot;
    static EditorWindow gameView;
    static double captureStarted;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static WrathPhaseWipeChecks(){EditorApplication.playModeStateChanged+=OnPlay;}
    static void Need(bool condition,string message){if(!condition)throw new Exception("Wrath wipe: "+message);}
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
    static void Tick(LingyueGame game,float dt)=>typeof(LingyueGame).GetMethod("UpdateFrame",Flags).Invoke(game,new object[]{dt});
    static string BossSprite(LingyueGame game){
        var actor=(PixelActor)typeof(LingyueGame).GetField("guardian",Flags).GetValue(game);
        return ((SpriteRenderer)typeof(PixelActor).GetField("renderer",Flags).GetValue(actor)).sprite.name;
    }
    static Combat Prepare(LingyueGame game,int duration){
        var definition=BossBehaviorCatalog.WrathGeneral.Copy();definition.phases[1].transitionFrames=duration;
        var combat=new Combat{ai=false};combat.ResetBoss(3830,870,OpponentPattern.FromBoss(definition));combat.boss.hp=400;
        combat.boundsMin=WrathRoute.BoundsMin;combat.boundsMax=WrathRoute.BoundsMax;combat.player.x=3500;combat.player.y=0;
        game.GetComponent<WrathChapter>().SetInside(true);
        ((Transform)typeof(LingyueGame).GetField("world",Flags).GetValue(game)).gameObject.SetActive(true);
        Set(game,"wrathPhaseCamera",false);Set(game,"cameraX",(float)((WrathRoute.Fights[WrathRoute.Fights.Length-1].Min+WrathRoute.Fights[WrathRoute.Fights.Length-1].Max)*.5));
        Camera.main.orthographicSize=3.6f;Camera.main.transform.position=new Vector3((float)combat.boss.x/100,0,-10);
        game.sim=combat;game.clock=new FixedClock(combat);game.stage="GUARDIAN_FIGHT";game.PhaseWipe.Reset();
        Set(game,"wrathActive",WrathRoute.Fights.Length-1);Set(game,"phase2Spoken",true);Set(game,"focused",true);
        Set(game,"backpackOpen",false);Set(game,"gameSettings",false);Set(game,"applicationSuspended",false);
        return combat;
    }
    public static void Run(){
        for(int i=1;i<=WrathPhaseWipe.FrameCount;i++){
            string path="Assets/Resources/"+WrathPhaseWipe.ResourceRoot+i.ToString("00")+".png";
            var texture=Resources.Load<Texture2D>(WrathPhaseWipe.ResourceRoot+i.ToString("00"));
            Need(texture&&texture.width==512&&texture.height==288,"texture dimensions "+i);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            Need(importer&&importer.filterMode==FilterMode.Point&&importer.alphaIsTransparency,"point transparent import "+i);
        }
        foreach(int duration in new[]{90,120}){
            var c=new Combat{ai=false};var d=BossBehaviorCatalog.WrathGeneral.Copy();d.phases[1].transitionFrames=duration;c.ResetBoss(3830,870,OpponentPattern.FromBoss(d));c.boss.hp=400;
            var wipe=new WrathPhaseWipe();var clock=new FixedClock(c);int guard=0;
            while(!wipe.Active&&guard++<30)clock.Advance(.25,()=>wipe.Observe(c,true));
            Need(wipe.Active&&clock.accumulator==0,"large dt stops at boundary "+duration);
            Need(WrathGeneralArt.ReactionFrame(Math.Max(0,c.boss.age-1)/60d,duration,32)==16,"first rise frame boundary");
            int frame=c.frame,age=c.boss.age;double hp=c.player.hp;
            wipe.Advance(4,true);Need(wipe.BeforeHold&&wipe.Timeline==0,"pause freezes interval");
            Need(wipe.HoldSpriteIndex==14,"front hold starts frame 15");wipe.Advance(.2f,false);Need(wipe.HoldSpriteIndex==15,"front hold alternates frame 16");
            wipe.Advance(.8f,false);Need(wipe.WipeActive&&wipe.Elapsed==0,"one second front hold");
            wipe.Advance(.8f,false);Need(wipe.FrameIndex==8,"peak frame");wipe.Advance(.8f,false);
            Need(wipe.AfterHold&&wipe.HoldSpriteIndex==0,"1.6 second wipe then rise frame one");wipe.Advance(.2f,false);Need(wipe.HoldSpriteIndex==1,"rise hold alternates frame two");wipe.Advance(.8f,false);
            Need(!wipe.Active&&wipe.Consumed&&c.frame==frame&&c.boss.age==age&&c.player.hp==hp,"presentation has no simulation writes");
            Need(!wipe.Observe(c,true),"only once");c.ResetBoss(3830,870,WrathRoute.General);wipe.Observe(c,true);Need(!wipe.Consumed,"retry resets");
        }
        Debug.Log("WRATH_PHASE_WIPE_CHECKS_PASSED: imports, 90/120 ticks, exact large-dt boundary, pause and reset");
    }
    public static void RunBatch(){try{Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
    static void ActivateGameView(){
        var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        Need(type!=null,"GameView editor type available");
        gameView=EditorWindow.GetWindow(type);gameView.Show();gameView.Focus();gameView.Repaint();
        EditorApplication.QueuePlayerLoopUpdate();
    }
    public static void PlayBatch(){SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(WrathSceneSetup.ScenePath);ActivateGameView();EditorApplication.EnterPlaymode();}
    static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){started=Time.frameCount;ActivateGameView();EditorApplication.update+=CheckPlay;}}
    static void CheckPlay(){
        if(gameView)gameView.Repaint();EditorApplication.QueuePlayerLoopUpdate();
        if(Time.frameCount<started+8)return;
        try{
            var game=UnityEngine.Object.FindAnyObjectByType<LingyueGame>();Need(game,"actual game");if(captureAt<0)game.enabled=false;
            if(captureAt>=0){
                Need(EditorApplication.timeSinceStartup-captureStarted<15,"peak screenshot timed out after 15 seconds; actual GameView rendering is required");
                if(Time.frameCount<captureAt)return;
                if(screenshot==null){
                    string name=captureStage==0?"before-hold-screen.png":captureStage==1?"peak-screen.png":captureStage==2?"after-hold-screen.png":"after-rise-screen.png";
                    screenshot=Path.GetFullPath(Path.Combine(Application.dataPath,"../.utmp/wrath-phase-wipe/"+name));Directory.CreateDirectory(Path.GetDirectoryName(screenshot));
                    if(File.Exists(screenshot))File.Delete(screenshot);ScreenCapture.CaptureScreenshot(screenshot);captureAt=Time.frameCount+1;return;
                }
                if(!File.Exists(screenshot)||new FileInfo(screenshot).Length==0)return;
                if(captureStage<3){
                    game.enabled=false;game.clock.paused=false;Set(game,"focused",true);
                    if(captureStage==0){Tick(game,1);Tick(game,.8f);}
                    else if(captureStage==1){Tick(game,.8f);Tick(game,.2f);}
                    else{
                        int guard=0;while(game.sim.boss.state=="phase"&&guard++<400)Tick(game,1f/60);
                        Need(game.sim.boss.state!="phase","rise completes before normal arena screenshot");
                        Need(Mathf.Abs(Camera.main.orthographicSize-3.6f)<.01f,"after-rise camera normal size");
                        float center=(float)((WrathRoute.Fights[WrathRoute.Fights.Length-1].Min+WrathRoute.Fights[WrathRoute.Fights.Length-1].Max)*.5)/100;
                        Need(Mathf.Abs(Camera.main.transform.position.x-center)<.01f&&Mathf.Abs(Camera.main.transform.position.y)<.01f,"after-rise camera arena center");
                    }
                    captureStage++;screenshot=null;game.clock.paused=true;game.enabled=true;captureStarted=EditorApplication.timeSinceStartup;captureAt=Time.frameCount+3;return;
                }
                EditorApplication.update-=CheckPlay;SessionState.SetBool(Key,false);
                Debug.Log("WRATH_PHASE_WIPE_PLAY_PASSED: real UpdateFrame freeze, pause/settings/backpack/focus, resume and retry; peak screenshot "+screenshot);
                EditorApplication.Exit(0);return;
            }
            Run();WrathPhaseTwoChecks.Run();MigrationChecks.Run();
            foreach(int duration in new[]{90,120}){
                var c=Prepare(game,duration);int guard=0;
                while(!game.PhaseWipe.Active&&guard++<200)Tick(game,1f/60);
                Need(game.PhaseWipe.BeforeHold,"real Update reaches front hold");
                Need(BossSprite(game)=="phase-engulf-15","front hold starts penultimate pose");
                Need(Camera.main.orthographicSize<1.5f&&Mathf.Abs(Camera.main.transform.position.x-(float)c.boss.x/100)<.01f,"close shot centered on boss");
                int frame=c.frame,age=c.boss.age;double playerHp=c.player.hp,bossHp=c.boss.hp;
                game.Act("attack");game.Act("step");game.Act("stun");
                foreach(string field in new[]{"gameSettings","backpackOpen","applicationSuspended"}){Set(game,field,true);Tick(game,2);Need(game.PhaseWipe.Timeline==0,"suspend "+field);Set(game,field,false);}
                Set(game,"focused",false);Tick(game,2);Set(game,"focused",true);Need(game.PhaseWipe.Timeline==0,"focus suspension");
                game.Act("pause");Tick(game,2);Need(game.PhaseWipe.Timeline==0,"explicit pause");game.Act("pause");
                Tick(game,.2f);Need(BossSprite(game)=="phase-engulf-16","front hold alternates");
                Tick(game,.2f);Need(BossSprite(game)=="phase-engulf-15","front hold loops");
                Tick(game,.59f);Need(game.PhaseWipe.BeforeHold&&BossSprite(game)=="phase-engulf-16","front hold ends on last pose");
                Tick(game,.01f);Need(game.PhaseWipe.WipeActive,"one second before flames");
                Tick(game,.8f);Need(game.PhaseWipe.FrameIndex==8,"midpoint");
                Tick(game,.8f);Need(game.PhaseWipe.AfterHold&&BossSprite(game)=="phase-rise-01","post hold starts rise pose one");
                Tick(game,.2f);Need(BossSprite(game)=="phase-rise-02","post hold alternates");
                Tick(game,.2f);Need(BossSprite(game)=="phase-rise-01","post hold loops");
                Tick(game,.59f);Need(game.PhaseWipe.AfterHold&&BossSprite(game)=="phase-rise-02","post hold ends pose two");
                Need(c.frame==frame&&c.boss.age==age&&c.player.hp==playerHp&&c.boss.hp==bossHp,"no battle ticks or damage during all holds and flames");
                Tick(game,.01f);Need(!game.PhaseWipe.Active&&c.frame==frame&&game.clock.accumulator==0,"completion does not catch up");
                Need(BossSprite(game)=="phase-rise-02","continues from second pose without reversal");
                int riseTicks=0;while(c.boss.state=="phase"&&riseTicks++<300)Tick(game,1f/60);
                Need(Camera.main.orthographicSize>3.59f,"camera returns to normal arena size");
                c.ResetBoss(3830,870,WrathRoute.General);Tick(game,0);Need(!game.PhaseWipe.Consumed,"real retry resets");
                // Ordinary simulation remains sixty ticks per second; phase alone runs at eighty percent.
                c=Prepare(game,duration);c.boss.hp=c.boss.maxHp;for(int i=0;i<20;i++)Tick(game,1f/60);Need(c.frame==20,"ordinary clock unchanged");
                c.boss.hp=400;Tick(game,1f/60);Need(c.boss.state=="phase","enter phase");int phaseStart=c.frame;
                for(int i=0;i<20;i++)Tick(game,1f/60);Need(c.frame-phaseStart==16,"phase runs at 0.8 rate");
                game.clock.speed=.25;Tick(game,1f/60);Need(game.clock.speed==.25,"user slow setting preserved");
            }
            Prepare(game,90);while(!game.PhaseWipe.Active)Tick(game,.25f);game.clock.paused=true;game.enabled=true;ActivateGameView();captureStarted=EditorApplication.timeSinceStartup;captureAt=Time.frameCount+3;
        }catch(Exception e){EditorApplication.update-=CheckPlay;SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
