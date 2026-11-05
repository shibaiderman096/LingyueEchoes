using System;
using UnityEditor;
using UnityEngine;

public static class PrologueFlowChecks
{
    static LingyueEchoes.LingyueGame game;
    static int phase,deathFrame,menuFrame,progress,tails;static double deadline,deathX;
    static bool sawZoom,sawFade,sawReveal,sawWake;
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static LingyueEchoes.LingyueGame Prepare()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play mode first.");
        var current=UnityEngine.Object.FindAnyObjectByType<LingyueEchoes.LingyueGame>();
        Require(current!=null,"No game found.");
        // Load a saved arena entrance without changing the player's existing save.
        string key=LingyueEchoes.PrologueLayout.SaveKey(LingyueEchoes.PrologueLayout.CheckpointKey,current.SaveSlot);
        bool hadCheckpoint=PlayerPrefs.HasKey(key);int checkpoint=PlayerPrefs.GetInt(key,0);
        PlayerPrefs.SetInt(key,1);
        try{current.BeginTrial();}
        finally{if(hadCheckpoint)PlayerPrefs.SetInt(key,checkpoint);else PlayerPrefs.DeleteKey(key);}
        current.stage="GUARDIAN_FIGHT";
        current.sim.player.x=LingyueEchoes.PrologueLayout.PlayerFightX;
        current.sim.boss.x=current.sim.player.x+260;
        current.sim.ai=current.sim.enemyActive=true;current.sim.DebugBoss(2);
        Camera.main.orthographicSize=3.6f;
        Camera.main.transform.position=new Vector3((float)LingyueEchoes.PrologueLayout.ArenaCenter/100,0,-10);
        return current;
    }
    [MenuItem("Lingyue/Preview death menu")]
    public static void PreviewDeath(){var current=Prepare();current.sim.player.hp=0;}
    [MenuItem("Lingyue/Check death menu flow")]
    public static void Run()
    {
        EditorApplication.update-=Pump;game=Prepare();phase=0;deadline=EditorApplication.timeSinceStartup+25;
        sawZoom=sawFade=sawReveal=sawWake=false;deathX=game.sim.player.x;
        progress=Progress(LingyueEchoes.PrologueLayout.ProgressKey);tails=Progress(LingyueEchoes.PrologueLayout.TailCountKey);
        game.sim.player.y=70;game.sim.player.vy=3;game.sim.player.hp=0;EditorApplication.update+=Pump;
    }
    static void Pump()
    {
        if(!EditorApplication.isPlaying||!game){EditorApplication.update-=Pump;return;}
        try{
            Require(EditorApplication.timeSinceStartup<deadline,"Death flow timed out.");
            if(phase==0){if(game.stage!="DYING")return;
                Require(!game.BattleOn&&!game.clock.paused&&game.sim.boss.move!=null,"Dying must stop player controls while the enemy move keeps running.");
                Require(game.sim.player.state=="dead"&&game.DeathMenuOpacity==0,"Death pose precedes the menu.");
                deathFrame=game.sim.frame;game.Retry();Require(game.stage=="DYING","Cannot respawn during the death animation.");phase=1;return;}
            if(phase==1){if(game.sim.frame-deathFrame<20)return;
                sawZoom=Camera.main.orthographicSize<3.58;
                Require(game.stage=="DYING"&&game.DeathMenuOpacity==0&&sawZoom,"The camera zooms during the death animation while the menu waits.");
                Require(game.sim.boss.age>=20,"Pending enemy move advances during the death animation.");phase=2;return;}
            if(phase==2){if(game.stage!="DEATH")return;
                Require(sawZoom&&game.sim.frame-deathFrame>=63&&Camera.main.orthographicSize<1.7,"Both the full death pose and camera push-in finish before the menu.");
                Require(game.sim.player.y==0&&game.sim.player.x==deathX&&game.sim.player.hp==0,"Corpse lands without moving horizontally or reviving.");
                Require(!game.DeathMenuReady&&game.DeathMenuOpacity<.1,"Menu starts transparent and inactive.");
                menuFrame=game.sim.frame;game.Retry();Require(game.stage=="DEATH","Cannot confirm during menu fade.");phase=3;return;}
            if(phase==3){float opacity=game.DeathMenuOpacity;if(opacity>.02&&opacity<.98)sawFade=true;if(!game.DeathMenuReady)return;
                Require(sawFade&&game.sim.frame>menuFrame+30&&!game.clock.paused,"Menu fades in while the world continues advancing.");
                Require(game.sim.boss.move==null&&game.sim.boss.state=="idle","Enemy finishes the move and settles instead of attacking the corpse.");
                var old=game.sim;game.Retry();
                Require(game.stage=="FADE_IN"&&game.sim!=old&&game.sim.player.hp==game.sim.player.maxHp,"Respawn creates a living player and starts the prologue fade.");
                Require(game.sim.player.x==LingyueEchoes.PrologueLayout.CheckpointX&&game.sim.boss.x==LingyueEchoes.PrologueLayout.GuardianWaitX&&!game.sim.ai&&!game.sim.enemyActive,"Respawn returns to the checkpoint with the guardian waiting.");phase=4;return;}
            if(phase==4){if(game.stage=="CAMERA_REVEAL")sawReveal=true;if(game.stage=="WAKE_UP")sawWake=true;if(game.stage!="GATE_READY")return;
                Require(sawReveal&&sawWake&&Math.Abs(Camera.main.orthographicSize-3.6)<.01,"Respawn plays the same camera reveal and wake-up before returning control.");
                game.sim.player.hp=0;phase=5;return;}
            if(!game.DeathMenuReady)return;game.Home();Require(game.stage=="MENU","Death menu can return home.");
            Require(Progress(LingyueEchoes.PrologueLayout.ProgressKey)==progress&&Progress(LingyueEchoes.PrologueLayout.TailCountKey)==tails,"Death/retry must not grant progression.");
            EditorApplication.update-=Pump;Debug.Log("DEATH_MENU_FLOW_PASSED: simultaneous death animation and camera push-in, ongoing enemy move, delayed menu fade, checkpoint respawn with prologue cinematic, home, unchanged progress");
        }catch(Exception error){EditorApplication.update-=Pump;Debug.LogException(error);if(game)game.Home();}
    }
    static int Progress(string key)=>PlayerPrefs.GetInt(LingyueEchoes.PrologueLayout.SaveKey(key,game.SaveSlot),0);
}
