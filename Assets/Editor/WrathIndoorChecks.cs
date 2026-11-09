using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LingyueEchoes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WrathIndoorChecks
{
    const string Key="Lingyue.WrathIndoorCheck";
    static int frame;
    static WrathIndoorChecks(){EditorApplication.playModeStateChanged+=BeginMoonGate;EditorApplication.playModeStateChanged+=state=>{
        if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){
            frame=Time.frameCount;EditorApplication.update+=Check;
        }
    };}
    static void Need(bool ok,string message){if(!ok)throw new Exception("Indoor chapter: "+message);}
    static object Call(LingyueGame game,string method,params object[] args)=>typeof(LingyueGame)
        .GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,args);
    static void TimeAt(LingyueGame game,float seconds)=>typeof(LingyueGame)
        .GetField("stageTime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,seconds);
    public static void PlayBatch(){
        WrathChapterChecks.Run();BossBehaviorChecks.Run();WrathGeneralArtChecks.Run();
        foreach(string key in WrathRoute.SaveKeys.Concat(new[]{PrologueLayout.TailCountKey})){
            string save=PrologueLayout.SaveKey(key,1);
            SessionState.SetString(Key+save,PlayerPrefs.HasKey(save)?PlayerPrefs.GetInt(save).ToString():"missing");
        }
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(WrathSceneSetup.ScenePath);EditorApplication.EnterPlaymode();
    }
    public static void MoonGateBatch(){
        foreach(string key in WrathRoute.SaveKeys.Concat(new[]{PrologueLayout.TailCountKey,PrologueLayout.ProgressKey,PrologueLayout.CheckpointKey,PrologueLayout.FirstVictoryStoryKey})){
            string save=PrologueLayout.SaveKey(key,1);
            SessionState.SetString(Key+save,PlayerPrefs.HasKey(save)?PlayerPrefs.GetInt(save).ToString():"missing");
        }
        EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity");
        SessionState.SetBool(Key+"MoonGate",true);
        EditorApplication.EnterPlaymode();
    }
    static void BeginMoonGate(PlayModeStateChange state){
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key+"MoonGate",false))return;
        SessionState.SetBool(Key+"MoonGate",false);
        try{
            var game=UnityEngine.Object.FindAnyObjectByType<LingyueGame>();game.enabled=false;
            foreach(string key in new[]{WrathRoute.Unlocked,PrologueLayout.ProgressKey,PrologueLayout.FirstVictoryStoryKey,PrologueLayout.TailCountKey})
                PlayerPrefs.SetInt(PrologueLayout.SaveKey(key,1),1);
            PlayerPrefs.SetInt(PrologueLayout.SaveKey(WrathRoute.Completed,1),0);
            Call(game,"PrepareRoute",true);game.sim.player.x=PrologueLayout.MoonGateX;Call(game,"SetStage","GATE_READY");
            game.Interact();Need(game.stage=="WRATH_DIALOGUE","moon gate interaction opens transfer dialogue");
            Call(game,"HandleChapterSpeech","stele");TimeAt(game,.6f);Call(game,"Update");
            frame=Time.frameCount;EditorApplication.update+=CheckMoonArrival;
        }catch(Exception error){Debug.LogException(error);FinishMoon(1);}
    }
    static void CheckMoonArrival(){
        if(Time.frameCount<frame+8)return;EditorApplication.update-=CheckMoonArrival;
        try{
            var chapter=UnityEngine.Object.FindAnyObjectByType<WrathChapter>();var game=chapter.GetComponent<LingyueGame>();
            Need(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==WrathRoute.Scene&&game.SaveSlot==1,"moon gate transfers the same save slot to chapter one");
            Need(Math.Abs(game.sim.player.x-chapter.exteriorArrivalAnchor.position.x*100)<.01&&!game.sim.enemyActive&&!chapter.IsInside&&chapter.entranceDoor.enabled,"moon gate lands at the authored exterior arrival anchor");
            CheckScenery(chapter);Need(game.sim.player.x<chapter.entranceDoor.transform.position.x*100,"moon gate arrival stands outside the physical door");
            Call(game,"DrawWorld",0f);CheckExteriorCamera(chapter);
            Need(game.sim.TailUpgrades==1,"chapter transfer preserves tail progression");
            Debug.Log("WRATH_MOON_GATE_PLAY_PASSED: actual prologue gate interaction and scene transfer, same save and tail, direct boss entrance, no approach or automatic combat");FinishMoon(0);
        }catch(Exception error){Debug.LogException(error);FinishMoon(1);}
    }
    static void FinishMoon(int code){
        foreach(string key in WrathRoute.SaveKeys.Concat(new[]{PrologueLayout.TailCountKey,PrologueLayout.ProgressKey,PrologueLayout.CheckpointKey,PrologueLayout.FirstVictoryStoryKey})){
            string save=PrologueLayout.SaveKey(key,1),value=SessionState.GetString(Key+save,"missing");
            if(value=="missing")PlayerPrefs.DeleteKey(save);else PlayerPrefs.SetInt(save,int.Parse(value));SessionState.EraseString(Key+save);
        }
        PlayerPrefs.Save();EditorApplication.Exit(code);
    }
    static void Capture(Camera camera,string name){
        int width=Screen.width,height=Screen.height;
        var rt=RenderTexture.GetTemporary(width,height,24);var previous=RenderTexture.active;
        var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
            Directory.CreateDirectory(".utmp/wrath-indoor");File.WriteAllBytes(".utmp/wrath-indoor/"+name+".png",texture.EncodeToPNG());
        }finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(texture);}
    }
    static void CheckScenery(WrathChapter chapter){
        Need(chapter.exteriorScenery&&chapter.exteriorScenery.sprite&&chapter.exteriorScenery.enabled==!chapter.IsInside,"authored exterior visibility follows the entrance state");
        Need(!chapter.exteriorCanopy||(chapter.exteriorCanopy.sprite&&chapter.exteriorCanopy.enabled==!chapter.IsInside),"optional authored canopy visibility follows the entrance state");
        Need(chapter.exteriorPillars!=null&&chapter.exteriorPillars.Length==2&&
            chapter.exteriorPillars.All(r=>r&&r.sprite&&r.enabled==!chapter.IsInside)&&
            chapter.exteriorPillars[0].sprite==chapter.exteriorPillars[1].sprite,
            "both shared-sprite pillars follow the entrance state");
        Need(chapter.scenery.All(r=>r&&r.enabled==chapter.IsInside),"interior visibility follows the room state");
        Need(chapter.entranceDoor.flipX,"door faces the arriving hero");
    }
    static void CheckExteriorCamera(WrathChapter chapter,bool checkRoadBounds=true){
        if(chapter.IsInside)return;
        var camera=Camera.main;
        float halfWidth=camera.orthographicSize*camera.aspect;
        Need(camera.transform.position.x+halfWidth<=chapter.exteriorPillars[1].bounds.max.x+.001f,
            "the exterior viewport ends at or before the second pillar");
        if(!checkRoadBounds)return;
        float expectedCenter=Mathf.Min(chapter.ExteriorViewCenter.x,chapter.exteriorPillars[1].bounds.max.x-halfWidth);
        Need(Math.Abs(camera.orthographicSize-chapter.ExteriorViewSize)<.001f&&
            Math.Abs(camera.transform.position.x-expectedCenter)<.001f,
            "exterior preserves the authored camera size and center within the second pillar limit");
        Need(Math.Abs(chapter.GetComponent<LingyueGame>().sim.boundsMin-(camera.transform.position.x-halfWidth)*100)<.1&&
            Math.Abs(chapter.GetComponent<LingyueGame>().sim.boundsMax-(camera.transform.position.x+halfWidth)*100)<.1,
            "movement bounds cover both edges of the visible exterior road");
    }
    static void WalkExterior(LingyueGame game,WrathChapter chapter){
        Call(game,"SetStage","WRATH_ROUTE");Call(game,"DrawWorld",0f);CheckExteriorCamera(chapter);
        double left=game.sim.boundsMin+30,middle=(game.sim.boundsMin+game.sim.boundsMax)*.5,
            nearDoor=chapter.entranceDoor.transform.position.x*100-60;
        foreach(double target in new[]{left,middle,nearDoor}){
            int steps=0;
            while(Math.Abs(game.sim.player.x-target)>5&&steps++<1000){
                game.sim.axis=Math.Sign(target-game.sim.player.x);game.sim.Step();
            }
            game.sim.axis=0;
            Need(Math.Abs(game.sim.player.x-target)<=5,"the hero actually walks across left, middle and door-side exterior road");
            Need(!chapter.IsInside&&!game.sim.enemyActive,"walking the exterior does not start combat or enter the room");
        }
    }
    static void ApproachDoor(LingyueGame game,WrathChapter chapter){
        game.sim.player.x=chapter.entranceDoor.transform.position.x*100-60;
        Call(game,"SetStage","WRATH_ROUTE");game.Interact();
    }
    static void EnterDoor(LingyueGame game,WrathChapter chapter,bool review){
        Need(game.stage=="WRATH_ENTER"&&!chapter.IsInside&&chapter.entranceDoor.enabled,"interact starts the entrance cinematic with the door visible");
        int frame=game.sim.frame;double lastX=game.sim.player.x;
        game.Act("attack");game.Interact();
        Call(game,"SetBackpack",true);
        Need(game.stage=="WRATH_ENTER"&&game.sim.player.move==null&&!(bool)typeof(LingyueGame)
            .GetField("backpackOpen",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game),"entry blocks combat, backpack and repeated interaction");
        Sprite walking=null;
        foreach(float time in new[]{.10f,.42f,.80f,1.20f,1.65f,2.14f,2.16f,2.60f,3.01f}){
            TimeAt(game,time);Call(game,"UpdateWrathEntrance");Call(game,"DrawWorld",0f);
            CheckExteriorCamera(chapter,false);
            Need(game.sim.frame==frame&&!game.sim.ai&&!game.sim.enemyActive,"combat stays frozen throughout entry");
            Need(game.sim.player.x>=lastX,"entry advances continuously across the threshold");lastX=game.sim.player.x;
            Need(chapter.IsInside==(time>=2.15f),"room changes only under the fade to black");
            CheckScenery(chapter);
            Need(chapter.world.Find("Guardian").gameObject.activeSelf==(time>=2.15f),"the boss is revealed with the room, before dialogue, under the fade");
            if(time<3)Need(game.stage=="WRATH_ENTER","dialogue waits for the entrance cinematic");
            if(time<.85f)Need(chapter.DoorOpening>0&&chapter.DoorOpening<1,"door opening advances through intermediate frames");
            if(Math.Abs(time-.8f)<.01)walking=chapter.world.Find("Hero").GetComponent<SpriteRenderer>().sprite;
            if(Math.Abs(time-1.2f)<.01)Need(chapter.world.Find("Hero").GetComponent<SpriteRenderer>().sprite!=walking,"walking animation advances despite frozen combat clock");
            if(review&&time<3)Capture(Camera.main,"entry-"+time.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));
        }
        Need(chapter.IsInside&&!chapter.entranceDoor.enabled&&game.stage=="WRATH_DIALOGUE"&&game.sim.player.x==3500&&game.sim.player.y==0,
            "entry finishes inside, then starts the boss conversation");
    }
    static void Check(){
        if(Time.frameCount<frame+8)return;EditorApplication.update-=Check;SessionState.SetBool(Key,false);
        int code=1;
        try{
            var chapter=UnityEngine.Object.FindAnyObjectByType<WrathChapter>();var game=chapter.GetComponent<LingyueGame>();game.enabled=false;
            foreach(int checkpoint in new[]{0,1,2}){
                PlayerPrefs.SetInt(PrologueLayout.SaveKey(WrathRoute.Checkpoint,1),checkpoint);
                Call(game,"PrepareWrath",false);
                Need(Math.Abs(game.sim.player.x-chapter.exteriorArrivalAnchor.position.x*100)<.01&&!game.sim.enemyActive&&!chapter.IsInside&&chapter.entranceDoor.enabled,"legacy save and retry arrive before the closed door: "+checkpoint);
                CheckScenery(chapter);Need(game.sim.player.x<chapter.entranceDoor.transform.position.x*100,"arrival stands before the physical door threshold");
                Call(game,"DrawWorld",0f);CheckExteriorCamera(chapter);
                Need(game.sim.terrain.Count==0,"the exterior road has no deleted approach terrain");
            }
            PlayerPrefs.SetInt(PrologueLayout.SaveKey(WrathRoute.Completed,1),0);
            Call(game,"SetStage","WRATH_ROUTE");Call(game,"DrawWorld",0f);Capture(Camera.main,"entrance");
            CheckExteriorCamera(chapter);
            WalkExterior(game,chapter);ApproachDoor(game,chapter);EnterDoor(game,chapter,true);
            Call(game,"HandleChapterSpeech","wrathBossPre");Need(game.stage=="GUARDIAN_INTRO"&&!game.sim.enemyActive,"boss intro follows dialogue without premature combat");
            Need(game.sim.player.x==3500,"boss intro preserves the position reached by walking inside");
            TimeAt(game,2.2f);Call(game,"Update");
            Need(game.stage=="GUARDIAN_FIGHT"&&game.sim.enemyActive&&game.sim.ai&&game.sim.boss.maxHp==870,"intro enables the sole general fight");
            Call(game,"DrawWorld",0f);Capture(Camera.main,"battle");
            game.sim.player.hp=0;Call(game,"SetStage","DEATH");TimeAt(game,2f);game.Retry();
            Need(game.stage=="WRATH_ARRIVE"&&game.sim.player.hp==game.sim.player.maxHp&&Math.Abs(game.sim.player.x-chapter.exteriorArrivalAnchor.position.x*100)<.01&&!chapter.IsInside&&!game.sim.enemyActive,"death retry restores health at entrance without replaying approach");
            Call(game,"DrawWorld",0f);CheckExteriorCamera(chapter);
            Need(Math.Abs(Camera.main.orthographicSize-chapter.ExteriorViewSize)<.001,"retry resets the exterior camera zoom immediately");
            Call(game,"CompleteWrath");Need(game.stage=="WRATH_ROUTE"&&!chapter.IsInside&&Math.Abs(game.sim.player.x-chapter.exteriorArrivalAnchor.position.x*100)<.01,"victory returns to entrance");
            Need(PlayerPrefs.GetInt(PrologueLayout.SaveKey(WrathRoute.Completed,1))==1,"victory progress persists");
            Need(chapter.DoorOpening==0&&chapter.entranceDoor.sprite==chapter.entranceDoorFrames[0],"returning to the entrance resets the closed door");
            Call(game,"DrawWorld",0f);CheckExteriorCamera(chapter);
            ApproachDoor(game,chapter);EnterDoor(game,chapter,false);
            Call(game,"CompleteWrath");
            game.sim.player.x=chapter.exteriorArrivalAnchor.position.x*100-30;Call(game,"SetStage","WRATH_ROUTE");
            Need((string)Call(game,"ChapterInteraction")=="返回残月神社","return portal remains accessible");
            Debug.Log("WRATH_INDOOR_PLAY_PASSED: legacy saves, full-width exterior walking, door dialogue, boss intro, 870 HP combat, retry, victory persistence, rematch and return portal");code=0;
        }catch(Exception error){Debug.LogException(error);}
        finally{
            foreach(string key in WrathRoute.SaveKeys.Concat(new[]{PrologueLayout.TailCountKey})){
                string save=PrologueLayout.SaveKey(key,1),value=SessionState.GetString(Key+save,"missing");
                if(value=="missing")PlayerPrefs.DeleteKey(save);else PlayerPrefs.SetInt(save,int.Parse(value));
                SessionState.EraseString(Key+save);
            }
            PlayerPrefs.Save();EditorApplication.Exit(code);
        }
    }
}
