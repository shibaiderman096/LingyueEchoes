using System;
using System.IO;
using System.Linq;
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
    public static void Run()=>RunSuite(false);
    [MenuItem("Lingyue/Update combat expectations")]
    public static void UpdateExpectations()=>RunSuite(true);
    static void RunSuite(bool update){compared=0;var data=JsonUtility.FromJson<Golden>(File.ReadAllText("Assets/Editor/Tests/combat-golden.json"));
        foreach(var test in data.cases){var c=new Combat();c.god=true;c.player.x=0;c.boss.x=150;
            if(test.kind=="contact"){c.god=false;c.ai=false;c.boss.x=90;c.player.state=test.state;c.player.age=test.age;c.boss.state="attack";c.boss.move=Rules.Boss[0].Copy();c.boss.age=30;c.boss.attackId=55;}
            if(test.kind=="terrain"){c.enemyActive=false;c.terrain.Add(new Box(240,0,160,46));c.dummyBox=new Box(450,0,56,120);}
            if(test.kind=="practice")c.nonlethal=true;
            if(test.kind=="finisher"){c.boss.state="broken";c.boss.age=0;c.boss.posture=100;c.player.energy=2;c.player.echo=57;}
            if(test.kind=="execute"){c.player.energy=1;c.executeWindow=30;}
            if(test.kind=="route"){c.god=false;c.player.x=-1400;c.boss.x=-1235;c.boundsMin=-1560;c.boundsMax=1800;var tutorial=new TutorialDirector();tutorial.Begin(c);c.beforeStep=tutorial.Pump;}
            for(int t=0;t<test.steps;t++){if(test.kind=="route"){c.axis=1;if(t%90==0)c.Press("jump");if(t%37==0)c.Press("attack");}
                else if(test.kind!="contact"){c.axis=t%240<120?1:-1;if(t%37==0)c.Press("attack");if(t%113==0)c.Press("parry");if(t%157==0)c.Press("dodge");if(t%191==0)c.Press("jump");if(t%173==0&&test.kind=="mixed")c.DebugBoss(t/173%6);}
                c.Step();if(update)test.expected[t]=Capture(c);else Compare(c,test.expected[t],test.name+" tick "+t);}}
        foreach(var test in data.clocks){var c=new Combat();c.god=true;c.player.x=0;c.boss.x=150;var clock=new FixedClock(c);c.Press("parry");for(int i=0;i<test.hz*10;i++)clock.Advance(1d/test.hz);if(update)test.expected=Capture(c);else Compare(c,test.expected,"clock "+test.hz);}
        CheckDefenseAndTutorial();CheckExecutionAndEffects();CheckDeathAftermath();CheckPrologueContent();CheckBrokenSteps();
        int callbacks=0;var dialogue=new DialogueDirector();dialogue.onClose=id=>callbacks++;dialogue.Play(new DialogueScript{id="test",lines=new[]{new DialogueLine{speaker="guardian",text="铃月，沿着灯火前进。"}}});for(int i=0;i<600;i++)dialogue.Update(1d/60);Assert(!dialogue.Active&&callbacks==1,"Nonblocking dialogue auto advance");
        var sim=new Combat();sim.player.x=1393;sim.player.hp=72;sim.player.energy=2;var tut=new TutorialDirector();tut.Begin(sim);Assert(sim.player.x==1393&&sim.player.hp==72&&sim.player.energy==2,"Tutorial must not reset player");
        var manifest=JsonUtility.FromJson<ClipManifest>(Resources.Load<TextAsset>("Data/animations").text);var go=new GameObject("ClipCheck");var renderer=go.AddComponent<SpriteRenderer>();
        try{foreach(var clip in manifest.clips){Assert(Resources.Load<AnimationClip>(clip.path),"Native clip: "+clip.path);for(int cell=0;cell<clip.frames.Length;cell++){
                    PixelWorld.Sample(go,clip.folder,clip.name,0,cell);var sprite=Resources.Load<Sprite>(clip.frames[cell]);
                    Assert(sprite&&renderer.sprite==sprite,"Exact animation frame: "+clip.folder+"/"+clip.name+" cell "+cell);
                    if(clip.frames[cell].Contains("/frames16/")){
                        var foot=clip.folder=="pixel8"?new Vector2(96,16):new Vector2(128,24);
                        Assert((sprite.pivot-foot).sqrMagnitude<.001f,"Imported foot pivot: "+clip.frames[cell]+" actual "+sprite.pivot);
                    }}
                if(clip.contactPose>0){
                    int index=Array.IndexOf(clip.folder=="guardian8"?new[]{"slash","double","thrust","slam","leap","spin"}:new[]{"attack1","attack2","attack3"},clip.name);
                    if(index<0)continue;
                    var move=clip.folder=="guardian8"?Rules.Boss[index]:Rules.Player[index];
                    Assert(PixelActor.DrawnAttackCell(clip,move,move.wind)<clip.contactPose&&PixelActor.DrawnAttackCell(clip,move,move.wind+1)==clip.contactPose,"Drawn contact: "+clip.path);
                    if(clip.secondContactPose>0)Assert(PixelActor.DrawnAttackCell(clip,move,move.wind+move.pulses[1]+1)==clip.secondContactPose,"Drawn second contact: "+clip.path);
                }}}finally{UnityEngine.Object.DestroyImmediate(go);}
        if(update){File.WriteAllText("Assets/Editor/Tests/combat-golden.json",JsonUtility.ToJson(data));AssetDatabase.Refresh();Debug.Log("COMBAT_EXPECTATIONS_UPDATED: new defense rewards and interactive route; independent rule checks passed.");}
        else Debug.Log("MIGRATION_CHECKS_PASSED: "+compared+" configured snapshots; 30/60/120/144 Hz clocks; defense boundaries/rewards/cap/dedup; V2 content, reading hold, broken steps and optional route; death aftermath; "+manifest.clips.Length+" native animations");}
    static Snapshot Capture(Combat c){var snapshot=new Snapshot{player=c.player,boss=c.boss,visuals=c.visuals.Count,sounds=c.sounds.Count};foreach(var f in typeof(Snapshot).GetFields()){var source=typeof(Combat).GetField(f.Name);if(source!=null&&source.FieldType==f.FieldType)f.SetValue(snapshot,source.GetValue(c));}return JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(snapshot));}
    static Combat Contact(string state,int age,int energy=0,double echo=0){var c=new Combat{ai=false};c.player.x=0;c.player.state=state;c.player.age=age;c.player.energy=energy;c.player.echo=echo;c.boss.x=90;c.boss.state="attack";c.boss.move=Rules.Boss[0].Copy();c.boss.age=30;c.boss.attackId=55;return c;}
    static void CheckDefenseAndTutorial(){
        foreach(string state in new[]{"parry","dodge"})for(int age=0;age<22;age++){
            var c=Contact(state,age);c.Step();bool success=state=="parry"?age<20:age>=2&&age<14;bool perfect=success&&(state=="parry"?age<12:age<10);
            Assert(c.player.hp==(success?120:102),state+" defense window "+age);
            Assert(c.player.energy==(perfect?1:0)&&c.player.echo==(success&&!perfect?34:0),state+" defense reward "+age);}
        var dodge=Contact("dodge",10);dodge.Step();dodge.boss.attackId=56;dodge.Step();Assert(dodge.player.echo==34,"One echo reward per dodge action");
        var carry=Contact("parry",0,2,80);carry.Step();Assert(carry.player.energy==3&&carry.player.echo==80,"Perfect defense carries existing partial charge");
        var cap=Contact("parry",0,3,80);cap.Step();Assert(cap.player.energy==3&&cap.player.echo==99,"Echo capacity remains three points plus partial charge");
        var empty=new Combat{enemyActive=false,ai=false};empty.Press("dodge");for(int i=0;i<40;i++)empty.Step();empty.Press("parry");for(int i=0;i<40;i++)empty.Step();Assert(empty.player.energy==0&&empty.player.echo==0,"Empty defensive actions do not grant echo");
        var dialogue=new DialogueDirector();dialogue.Play(new DialogueScript{id="manual",lines=new[]{new DialogueLine{speaker="guardian",text="按 E 继续"}}},false);for(int i=0;i<600;i++)dialogue.Update(1d/60);Assert(dialogue.Active,"Teaching waits for a dialogue key");dialogue.Confirm();Assert(!dialogue.Active,"Dialogue key advances teaching");
        var route=new Combat{ai=false,enemyActive=false,boundsMin=-1560,boundsMax=PrologueLayout.ArenaMax};route.player.x=-1400;route.boss.x=-1235;var hero=route.player;var tutorial=new TutorialDirector();tutorial.Begin(route);route.beforeStep=tutorial.Pump;
        for(int i=0;i<600;i++)route.Step();Assert(route.boss.x==-1235&&!route.enemyActive&&tutorial.CanInteract(route),"Guide waits without attacking before interaction");
        Assert(tutorial.Interact(route)=="routeWake","Initial dialogue prompt");tutorial.BeginDialogue("routeWake");
        route.player.x=1280;for(int i=0;i<60;i++)route.Step();
        Assert(route.boss.x==-1235&&!tutorial.GateBound,"Running past the lesson cannot interrupt active dialogue");
        route.player.x=-1400;tutorial.FinishDialogue(route,"routeWake");
        for(int i=0;i<400;i++)route.Step();Assert(route.boss.x==PrologueLayout.DummyGuideX&&
            route.boss.x+Combat.BodyWidth(route.boss)/2<route.dummyBox.x,"Guide stops clear of the practice dummy");
        route.axis=1;for(int i=0;i<600;i++)route.Step();Assert(route.player==hero&&route.player.hp==120&&route.terrain.Count==0&&route.player.x>=PrologueLayout.GateEntry&&route.boss.x>=PrologueLayout.ArenaMin,"Continuous optional route without a jump obstacle");
        Assert(PrologueLayout.ArenaMax-PrologueLayout.ArenaMin==1080,"Expanded prologue arena width");
    }
    static void CheckExecutionAndEffects(){
        var c=new Combat{ai=false};var p=c.player;var b=c.boss;p.x=0;p.face=-1;b.x=80;b.posture=80;p.energy=1;c.executeWindow=30;
        c.DebugBoss(0);b.age=b.move.wind-Rules.Execute.wind;
        c.Press("attack");c.Step();
        Assert(p.state=="execute"&&p.face==1&&p.energy==1,"Execution faces the target and pays on contact");
        b.x=-70;b.y=180;b.vy=1;
        for(int i=0;i<Rules.Execute.wind;i++)c.Step();
        Assert(p.face==-1&&b.hp==b.maxHp-Rules.Execute.damage&&p.energy==0&&b.posture==100&&b.state=="stagger"&&b.move==null,
            "Execution tracks a crossing, airborne target and interrupts even when posture fills");
        Assert(p.hp==p.maxHp&&c.hitstop==Rules.executeHitstop,"The interrupted boss attack cannot land on the same frame");
        var trade=new Combat{ai=false};trade.player.x=0;trade.boss.x=80;trade.player.energy=1;trade.executeWindow=30;
        trade.DebugBoss(0);trade.boss.age=trade.boss.move.wind-Rules.Execute.wind;
        trade.Press("attack");for(int i=0;i<=Rules.Execute.wind;i++)trade.Step();
        Assert(trade.player.hp==trade.player.maxHp&&trade.boss.state=="stagger","Execution wins the same-frame clash");
        var far=new Combat{ai=false};far.player.energy=1;far.executeWindow=30;far.Press("attack");far.Step();
        Assert(far.player.state=="attack"&&far.player.energy==1,"An out-of-range attack does not spend an execution");
        var turn=new Combat{ai=false,enemyActive=false};turn.axis=-1;turn.player.y=90;turn.player.vy=3;turn.Press("attack");turn.Step();
        Assert(turn.player.face==-1,"Turning and attacking on the same airborne frame uses the new direction");
        var edge=new Combat{ai=false,god=true};edge.player.x=560;edge.boss.x=559;edge.boss.y=65;edge.boss.vy=-2;
        edge.DebugBoss(2);edge.player.x=500;edge.boss.age=edge.boss.move.wind;edge.Step();
        var slash=edge.visuals.First(v=>v.kind=="slash");var hit=edge.hits.First(h=>h.owner==2);
        Assert(edge.boss.x==560&&hit.x==560&&slash.originX==560&&slash.originY==edge.boss.y,"Airborne attack contact and effects share the clamped pose");
        edge.Step();Assert(slash.TryGetAnchor(edge.boss,out double x,out double y)&&edge.boss.y!=slash.originY&&Math.Abs(y-slash.y-edge.boss.y+slash.originY)<1e-7&&x==slash.x,"The slash follows the moving attack pose");
        edge.boss.face=-1;Assert(!slash.TryGetAnchor(edge.boss,out _,out _),"An old slash is hidden when the actor turns");
        Assert(new VisualCue{kind="slash",combo=1,face=-1}.RotationDegrees==40,"Angled combo arcs mirror with the actor");
    }
    static void CheckPrologueContent(){
        var asset=Resources.Load<TextAsset>("Data/prologue-content");
        Assert(asset,"Prologue JSON asset loads");
        var data=JsonUtility.FromJson<PrologueContent>(asset.text);
        string[] required={"routeWake","routeSteps","routeStepsClear","routeDummy","routeSpar","routeHit","routeDodge","routeParry",
            "bossPre","bossRetry","bossPhase2","tail","tailAfter","rematchWin"};
        Assert(data.scripts.Length==required.Length&&required.All(id=>data.scripts.Count(s=>s.id==id)==1),"Prologue IDs are unique and preserved");
        Assert(data.story.Length==4&&data.lessons.Length==7&&data.scripts.All(s=>s.lines.Length>0),"V2 opening, dialogue and lesson content");
        foreach(var line in data.scripts.SelectMany(s=>s.lines))
            Assert(!string.IsNullOrWhiteSpace(line.text)&&line.text.Length<=36&&line.text.IndexOfAny(new[]{'{','}','%','％'})<0,
                "Role text fits the bubble and contains no bindings or percentages: "+line.text);
        var full=data.scripts.Single(s=>s.id=="routeSpar").lines.Last().text;
        var dialogue=new DialogueDirector();int closed=0;dialogue.onClose=id=>closed++;
        dialogue.Play(new DialogueScript{id="long",lines=new[]{new DialogueLine{speaker="guardian",text=full}}});
        dialogue.Update(full.Length/34d+.01);
        dialogue.Update(DialogueDirector.ReadingHold(full.Length)-.1);
        Assert(dialogue.Active&&dialogue.VisibleText==full&&closed==0,"Long automatic dialogue remains fully visible for its reading hold");
        dialogue.Update(.11);Assert(closed==1&&!dialogue.Active,"Automatic dialogue advances after the reading hold");
    }
    static void CheckBrokenSteps(){
        var sim=new Combat{ai=false,enemyActive=false,boundsMin=-1560,boundsMax=PrologueLayout.ArenaMax};
        var steps=PrologueLayout.BrokenSteps();var guide=new TutorialDirector();
        sim.player.x=steps.x-100;sim.boss.x=-1235;
        guide.Begin(sim,steps);sim.beforeStep=guide.Pump;sim.axis=1;
        for(int i=0;i<50;i++)sim.Step();
        double wall=steps.x-Combat.BodyWidth(sim.player)/2;
        Assert(sim.player.x==wall&&!guide.StepsCleared&&guide.Lesson(sim)=="steps","Walking is blocked; a hint alone does not complete the jump");
        sim.axis=0;sim.Press("jump");for(int i=0;i<60;i++)sim.Step();
        Assert(sim.player.x==wall&&!guide.StepsCleared,"Jumping in place cannot complete the obstacle");
        sim.axis=1;sim.Press("jump");bool aboveRubble=false;
        for(int i=0;i<60;i++){
            if(sim.player.x>=steps.x+15)sim.axis=0;
            sim.Step();
            if(sim.player.x>=steps.x&&sim.player.y>steps.h)aboveRubble=true;
            Assert(sim.player.y!=steps.h||sim.player.vy!=0,"Loose rubble never supports standing");
        }
        Assert(aboveRubble&&sim.player.x==wall&&sim.player.y==0&&!guide.StepsCleared,
            "A short jump onto the rubble slips back to ground without clearing the route");
        sim.axis=1;sim.Press("jump");
        bool pastInAir=false;
        for(int i=0;i<85&&!guide.StepsCleared;i++){
            sim.Step();
            if(sim.player.x-Combat.BodyWidth(sim.player)/2>=steps.x+steps.w&&sim.player.vy!=0){
                pastInAir=true;Assert(!guide.StepsCleared,"Airborne beyond the obstacle is not yet a successful landing");
            }
        }
        Assert(pastInAir&&guide.StepsCleared&&sim.player.y==0&&guide.Lesson(sim)=="stepsClear","Approach, jump across and land to complete the obstacle");
        for(int i=0;i<780;i++)sim.Step();
        sim.player.x=PrologueLayout.GuardianWaitX-120;sim.axis=0;sim.Step();
        Assert(guide.GateBound&&!sim.enemyActive&&sim.dummyHits==0&&sim.deflectSerial==0&&guide.Interact(sim)=="bossPre",
            "Skipping the wood dummy and sparring still reaches the required boss dialogue");
    }
    static void CheckDeathAftermath(){var c=new Combat{continueAfterDeath=true};c.player.x=0;c.player.y=65;c.player.vy=3;c.player.hp=0;c.player.state="dead";c.boss.x=100;c.DebugBoss(2);
        c.axis=1;c.Press("attack");c.Press("jump");for(int i=0;i<180;i++)c.Step();
        Assert(c.frame==180&&c.player.hp==0&&c.player.x==0&&c.player.y==0&&c.player.move==null&&c.player.state=="dead","Death keeps gravity running without player control or revival");
        Assert(c.boss.move==null&&c.boss.state=="idle"&&c.moveIndex==0,"Enemy completes its pending move but does not start new attacks on a corpse");
        Assert(c.sounds.Exists(s=>s.name=="boss_swing")&&!c.sounds.Exists(s=>s.name=="hit_player")&&c.visuals.Exists(v=>v.kind=="slash"),"Enemy sounds and effects continue without hitting the dead player");
    }
    static void Compare(Combat c,Snapshot expected,string label){CompareActor(c.player,expected.player,label+" player");CompareActor(c.boss,expected.boss,label+" boss");
        foreach(var f in typeof(Snapshot).GetFields()){if(f.Name=="player"||f.Name=="boss")continue;object actual=f.Name=="visuals"?(object)c.visuals.Count:f.Name=="sounds"?c.sounds.Count:typeof(Combat).GetField(f.Name).GetValue(c);if(!actual.Equals(f.GetValue(expected)))throw new Exception(label+" "+f.Name+": "+actual+" != "+f.GetValue(expected));}compared++;}
    static void CompareActor(Fighter actual,Fighter expected,string label){foreach(var f in typeof(Fighter).GetFields()){if(f.Name=="move")continue;object a=f.GetValue(actual),b=f.GetValue(expected);if(a is double){if(Math.Abs((double)a-(double)b)>1e-7)throw new Exception(label+" "+f.Name+": "+a+" != "+b);}else if(!Equals(a,b))throw new Exception(label+" "+f.Name+": "+a+" != "+b);}}
    static void Assert(bool value,string label){if(!value)throw new Exception(label);}
}
