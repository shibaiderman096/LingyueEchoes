using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using LingyueEchoes;

public static class MigrationChecks
{
    [Serializable] class Snapshot {public Fighter player,boss;public int frame,tick,hitstop,phase,moveIndex,tempoSerial,successFrame,executeWindow,deflectSerial,dummyHits,dodgeEvades,visuals,sounds;public string successKind;}
    [Serializable] class Case {public string name,kind,state;public int age,steps;public Snapshot[] expected;}
    [Serializable] class ClockCase {public int hz;public Snapshot expected;}
    [Serializable] class Golden {public Case[] cases;public ClockCase[] clocks;}
    static int compared;
    [MenuItem("Lingyue/Run migration checks")]
    public static void Run(){compared=0;var data=JsonUtility.FromJson<Golden>(File.ReadAllText("Assets/Editor/Tests/combat-golden.json"));
        foreach(var test in data.cases){var c=new Combat();c.god=true;c.player.x=0;c.boss.x=150;
            if(test.kind=="contact"){c.god=false;c.ai=false;c.boss.x=90;c.player.state=test.state;c.player.age=test.age;c.boss.state="attack";c.boss.move=Rules.Boss[0].Copy();c.boss.age=30;c.boss.attackId=55;}
            if(test.kind=="terrain"){c.enemyActive=false;c.terrain.Add(new Box(240,0,160,46));c.dummyBox=new Box(450,0,56,120);}
            if(test.kind=="practice")c.nonlethal=true;
            if(test.kind=="finisher"){c.boss.state="broken";c.boss.age=0;c.boss.posture=100;c.player.energy=2;c.player.echo=57;}
            if(test.kind=="execute"){c.player.energy=1;c.executeWindow=30;}
            if(test.kind=="route"){c.god=false;c.player.x=-1400;c.boss.x=-1235;c.boundsMin=-1560;c.boundsMax=1800;var tutorial=new TutorialDirector();tutorial.Begin(c);c.beforeStep=tutorial.Pump;}
            for(int t=0;t<test.steps;t++){if(test.kind=="route"){c.axis=1;if(t%90==0)c.Press("jump");if(t%37==0)c.Press("attack");}
                else if(test.kind!="contact"){c.axis=t%240<120?1:-1;if(t%37==0)c.Press("attack");if(t%113==0)c.Press("parry");if(t%157==0)c.Press("dodge");if(t%191==0)c.Press("jump");if(t%173==0&&test.kind=="mixed")c.DebugBoss(t/173%6);}
                c.Step();Compare(c,test.expected[t],test.name+" tick "+t);}}
        foreach(var test in data.clocks){var c=new Combat();c.god=true;c.player.x=0;c.boss.x=150;var clock=new FixedClock(c);c.Press("parry");for(int i=0;i<test.hz*10;i++)clock.Advance(1d/test.hz);Compare(c,test.expected,"clock "+test.hz);}
        int callbacks=0;var dialogue=new DialogueDirector();dialogue.onClose=id=>callbacks++;dialogue.Play(new DialogueScript{id="test",lines=new[]{new DialogueLine{speaker="guardian",text="铃月，沿着灯火前进。"}}});for(int i=0;i<600;i++)dialogue.Update(1d/60);Assert(!dialogue.Active&&callbacks==1,"Nonblocking dialogue auto advance");
        var sim=new Combat();sim.player.x=1393;sim.player.hp=72;sim.player.energy=2;var tut=new TutorialDirector();tut.Begin(sim);Assert(sim.player.x==1393&&sim.player.hp==72&&sim.player.energy==2,"Tutorial must not reset player");
        var manifest=JsonUtility.FromJson<ClipManifest>(Resources.Load<TextAsset>("Data/animations").text);var go=new GameObject("ClipCheck");var renderer=go.AddComponent<SpriteRenderer>();
        try{foreach(var clip in manifest.clips){Assert(Resources.Load<AnimationClip>(clip.path),"Native clip: "+clip.path);for(int cell=0;cell<8;cell++){PixelWorld.Sample(go,clip.folder,clip.name,0,cell);Assert(renderer.sprite==Resources.Load<Sprite>(clip.frames[cell]),"Exact animation frame: "+clip.folder+"/"+clip.name+" cell "+cell);}}}finally{UnityEngine.Object.DestroyImmediate(go);}
        Debug.Log("MIGRATION_CHECKS_PASSED: "+compared+" exact original snapshots; 30/60/120/144 Hz clocks; dialogue; tutorial; "+manifest.clips.Length+" native animations");}
    static void Compare(Combat c,Snapshot expected,string label){CompareActor(c.player,expected.player,label+" player");CompareActor(c.boss,expected.boss,label+" boss");
        foreach(var f in typeof(Snapshot).GetFields()){if(f.Name=="player"||f.Name=="boss")continue;object actual=f.Name=="visuals"?(object)c.visuals.Count:f.Name=="sounds"?c.sounds.Count:typeof(Combat).GetField(f.Name).GetValue(c);if(!actual.Equals(f.GetValue(expected)))throw new Exception(label+" "+f.Name+": "+actual+" != "+f.GetValue(expected));}compared++;}
    static void CompareActor(Fighter actual,Fighter expected,string label){foreach(var f in typeof(Fighter).GetFields()){if(f.Name=="move")continue;object a=f.GetValue(actual),b=f.GetValue(expected);if(a is double){if(Math.Abs((double)a-(double)b)>1e-7)throw new Exception(label+" "+f.Name+": "+a+" != "+b);}else if(!Equals(a,b))throw new Exception(label+" "+f.Name+": "+a+" != "+b);}}
    static void Assert(bool value,string label){if(!value)throw new Exception(label);}
}
