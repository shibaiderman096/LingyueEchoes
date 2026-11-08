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
        WrathSceneSetup.RebuildIndoor();WrathChapterChecks.Run();BossBehaviorChecks.Run();WrathGeneralArtChecks.Run();
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
            Need(game.sim.player.x==WrathRoute.Entrance&&!game.sim.enemyActive&&!chapter.IsInside&&chapter.entranceDoor.enabled,"moon gate lands directly at the closed boss entrance");
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
        var rt=RenderTexture.GetTemporary(1280,720,24);var previous=RenderTexture.active;
        var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
            Directory.CreateDirectory(".utmp/wrath-indoor");File.WriteAllBytes(".utmp/wrath-indoor/"+name+".png",texture.EncodeToPNG());
        }finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(texture);}
    }
    static void Check(){
        if(Time.frameCount<frame+8)return;EditorApplication.update-=Check;SessionState.SetBool(Key,false);
        int code=1;
        try{
            var chapter=UnityEngine.Object.FindAnyObjectByType<WrathChapter>();var game=chapter.GetComponent<LingyueGame>();game.enabled=false;
            foreach(int checkpoint in new[]{0,1,2}){
                PlayerPrefs.SetInt(PrologueLayout.SaveKey(WrathRoute.Checkpoint,1),checkpoint);
                Call(game,"PrepareWrath",false);
                Need(game.sim.player.x==WrathRoute.Entrance&&!game.sim.enemyActive&&!chapter.IsInside&&chapter.entranceDoor.enabled,"legacy save and retry arrive before the closed door: "+checkpoint);
                Need(game.sim.boundsMax==WrathRoute.DoorX+80&&game.sim.terrain.Count==0,"entrance bounds block walking around the door");
            }
            PlayerPrefs.SetInt(PrologueLayout.SaveKey(WrathRoute.Completed,1),0);
            Call(game,"SetStage","WRATH_ROUTE");Call(game,"DrawWorld",0f);Capture(Camera.main,"entrance");
            game.Interact();Need(chapter.IsInside&&!chapter.entranceDoor.enabled&&game.stage=="WRATH_DIALOGUE","interact opens the door and starts boss dialogue");
            Call(game,"HandleChapterSpeech","wrathBossPre");Need(game.stage=="GUARDIAN_INTRO"&&!game.sim.enemyActive,"boss intro follows dialogue without premature combat");
            TimeAt(game,2.2f);Call(game,"Update");
            Need(game.stage=="GUARDIAN_FIGHT"&&game.sim.enemyActive&&game.sim.ai&&game.sim.boss.maxHp==870,"intro enables the sole general fight");
            Call(game,"DrawWorld",0f);Capture(Camera.main,"battle");
            game.sim.player.hp=0;Call(game,"SetStage","DEATH");TimeAt(game,2f);game.Retry();
            Need(game.stage=="WRATH_ARRIVE"&&game.sim.player.hp==game.sim.player.maxHp&&game.sim.player.x==WrathRoute.Entrance&&!chapter.IsInside&&!game.sim.enemyActive,"death retry restores health at entrance without replaying approach");
            Call(game,"CompleteWrath");Need(game.stage=="WRATH_ROUTE"&&!chapter.IsInside&&game.sim.player.x==WrathRoute.Entrance&&game.sim.boundsMax==WrathRoute.DoorX+80,"victory returns to entrance");
            Need(PlayerPrefs.GetInt(PrologueLayout.SaveKey(WrathRoute.Completed,1))==1,"victory progress persists");
            game.Interact();Need(chapter.IsInside&&game.stage=="WRATH_DIALOGUE","completed chapter can challenge the boss again");
            game.sim.player.x=WrathRoute.Portal-80;Call(game,"SetStage","WRATH_ROUTE");
            Need((string)Call(game,"ChapterInteraction")=="返回残月神社","return portal remains accessible");
            Debug.Log("WRATH_INDOOR_PLAY_PASSED: legacy saves, entrance blockade, door dialogue, boss intro, 870 HP combat, retry, victory persistence, rematch and return portal");code=0;
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
