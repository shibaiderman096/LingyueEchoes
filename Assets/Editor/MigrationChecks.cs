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
            if(test.kind=="route"){c.god=false;c.player.x=PrologueLayout.WakeX;c.boss.x=PrologueLayout.WakeGuideX;c.boundsMin=PrologueLayout.RouteStart;c.boundsMax=PrologueLayout.ArenaMax;var tutorial=new TutorialDirector();tutorial.Begin(c);c.beforeStep=tutorial.Pump;}
            for(int t=0;t<test.steps;t++){if(test.kind=="route"){c.axis=1;if(t%90==0)c.Press("jump");if(t%37==0)c.Press("attack");}
                else if(test.kind!="contact"){c.axis=t%240<120?1:-1;if(t%37==0)c.Press("attack");if(t%113==0)c.Press("parry");if(t%157==0)c.Press("dodge");if(t%191==0)c.Press("jump");if(t%173==0&&test.kind=="mixed")c.DebugBoss(t/173%6);}
                c.Step();if(update)test.expected[t]=Capture(c);else Compare(c,test.expected[t],test.name+" tick "+t);}}
        foreach(var test in data.clocks){var c=new Combat();c.god=true;c.player.x=0;c.boss.x=150;var clock=new FixedClock(c);c.Press("parry");for(int i=0;i<test.hz*10;i++)clock.Advance(1d/test.hz);if(update)test.expected=Capture(c);else Compare(c,test.expected,"clock "+test.hz);}
        CheckDefenseAndTutorial();CheckExecutionAndEffects();CheckPlayerCombo();CheckPlungeCombo();CheckTailUpgrades();CheckTailHealing();CheckDeathAftermath();CheckPrologueContent();CheckBrokenSteps();
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
                    int index=Array.IndexOf(clip.folder=="guardian8"?new[]{"slash","double","thrust","slam","leap","spin"}:new[]{"attack1","attack2","attack3","attack4"},clip.name);
                    var move=index>=0?(clip.folder=="guardian8"?Rules.Boss[index]:Rules.Player[index]):
                        clip.folder=="pixel8"&&clip.name=="execute"?Rules.Execute:
                        clip.folder=="pixel8"&&clip.name=="finisher"?Rules.Finisher:null;
                    if(move==null)continue;
                    Assert(PixelActor.DrawnAttackCell(clip,move,move.wind)<clip.contactPose&&PixelActor.DrawnAttackCell(clip,move,move.wind+1)==clip.contactPose,"Drawn contact: "+clip.path);
                    if(clip.secondContactPose>0)Assert(PixelActor.DrawnAttackCell(clip,move,move.wind+move.pulses[1]+1)==clip.secondContactPose,"Drawn second contact: "+clip.path);
                    if(clip.authoredTiming){
                        Assert(clip.times.Length==clip.frames.Length&&Mathf.RoundToInt(clip.duration*Rules.hz)==move.wind+move.active+move.recovery,"Authored timing matches move length: "+clip.name);
                        var holds=clip.times.Skip(1).Zip(clip.times,(a,b)=>Mathf.RoundToInt((a-b)*Rules.hz)).ToArray();
                        Assert(holds.All(n=>n>0)&&holds.Distinct().Count()>1,"Ordered poses have varied holds: "+clip.name);
                        if(clip.folder=="pixel8"&&clip.name=="attack3")Assert(PixelActor.DrawnAttackCell(clip,move,move.chainAt)>=12,"Spin returns to its starting direction before the chain can cut it off");
                    }
                }}}finally{UnityEngine.Object.DestroyImmediate(go);}
        if(update){File.WriteAllText("Assets/Editor/Tests/combat-golden.json",JsonUtility.ToJson(data));AssetDatabase.Refresh();Debug.Log("COMBAT_EXPECTATIONS_UPDATED: new defense rewards and interactive route; independent rule checks passed.");}
        else Debug.Log("MIGRATION_CHECKS_PASSED: "+compared+" configured snapshots; 30/60/120/144 Hz clocks; defense boundaries/rewards/cap/dedup; tail healing; V2 content, reading hold, broken steps and optional route; death aftermath; "+manifest.clips.Length+" native animations");}
    static void CheckTailHealing(){
        Combat Setup(int tails,int energy,double missing){var c=new Combat{ai=false,enemyActive=false};c.ApplyTailUpgrades(tails);
            c.player.hp=c.player.maxHp-missing;c.player.energy=energy;c.player.echo=46;c.Press("heal");return c;}
        foreach(var c in new[]{Setup(0,1,40),Setup(1,0,40),Setup(1,1,0)}){
            double hp=c.player.hp;int energy=c.player.energy;c.Step();
            Assert(c.player.state!="heal"&&c.player.hp==hp&&c.player.energy==energy,"Healing cannot start while locked, empty, or full");
        }
        var heal=Setup(1,2,40);var p=heal.player;
        for(int i=0;i<Rules.healRelease;i++)heal.Step();
        Assert(p.state=="heal"&&p.hp==90&&p.energy==2&&heal.visuals.Count==0,"Healing windup does not spend or restore");
        heal.Step();Assert(p.hp==105&&p.energy==1&&p.echo==46&&heal.visuals.Single().kind=="heal_fx"&&heal.sounds.Single().name=="heal","Release spends one full point, restores 15 and emits a single cue");
        for(int i=0;i<Rules.healFrames;i++)heal.Step();
        Assert(p.state=="idle"&&p.hp==105&&p.energy==1&&heal.visuals.Count==1,"Healing recovers to idle without repeat spending");
        var capped=Setup(1,1,4);for(int i=0;i<=Rules.healRelease;i++)capped.Step();
        Assert(capped.player.hp==capped.player.maxHp&&capped.player.energy==0,"Healing is capped at the current maximum");
        var interrupted=Setup(1,1,40);interrupted.player.x=0;
        for(int i=0;i<Rules.healRelease;i++)interrupted.Step();
        interrupted.enemyActive=true;interrupted.boss.x=90;interrupted.boss.face=-1;
        interrupted.boss.state="attack";interrupted.boss.move=Rules.Boss[0].Copy();interrupted.boss.age=interrupted.boss.move.wind;
        interrupted.Step();Assert(interrupted.player.state=="hurt"&&interrupted.player.hp==72&&interrupted.player.energy==1&&
            !interrupted.visuals.Any(c=>c.kind=="heal_fx"),"Hit on the release frame interrupts without spending");
    }
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
        var route=new Combat{ai=false,enemyActive=false,boundsMin=PrologueLayout.RouteStart,boundsMax=PrologueLayout.ArenaMax};route.player.x=PrologueLayout.WakeX;route.boss.x=PrologueLayout.WakeGuideX;var hero=route.player;var tutorial=new TutorialDirector();tutorial.Begin(route);route.beforeStep=tutorial.Pump;
        for(int i=0;i<600;i++)route.Step();Assert(route.boss.x==PrologueLayout.WakeGuideX&&!route.enemyActive&&tutorial.CanInteract(route),"Guide waits without attacking before interaction");
        Assert(tutorial.Interact(route)=="routeWake","Initial dialogue prompt");tutorial.BeginDialogue("routeWake");
        route.player.x=PrologueLayout.GateEntry+40;for(int i=0;i<60;i++)route.Step();
        Assert(route.boss.x==PrologueLayout.WakeGuideX&&!tutorial.GateBound,"Running past the lesson cannot interrupt active dialogue");
        route.player.x=PrologueLayout.WakeX;tutorial.FinishDialogue(route,"routeWake");
        for(int i=0;i<400;i++)route.Step();Assert(route.boss.x==PrologueLayout.DummyGuideX&&
            route.boss.x+Combat.BodyWidth(route.boss)/2<route.dummyBox.x,"Guide stops clear of the practice dummy");
        route.axis=1;for(int i=0;i<600;i++)route.Step();Assert(route.player==hero&&route.player.hp==120&&route.terrain.Count==0&&route.player.x>=PrologueLayout.GateEntry&&route.boss.x>=PrologueLayout.ArenaMin,"Continuous optional route without a jump obstacle");
        Assert(PrologueLayout.ArenaMax-PrologueLayout.ArenaMin==1080&&PrologueLayout.ArenaMax<PrologueLayout.MoonGateX&&PrologueLayout.MoonGateX<PrologueLayout.RouteEnd,"The route ends at the chapter portal past the unchanged trial arena");
    }
    static void CheckTailUpgrades(){
        var c=new Combat{ai=false,enemyActive=false};
        c.player.hp=37;c.player.stamina=50;c.ApplyTailUpgrades(1);
        Assert(c.player.maxHp==130&&c.PlayerMaxStamina==110&&c.player.hp==47&&c.player.stamina==60,"First tail adds ten to both caps");
        c.ApplyTailUpgrades(1);Assert(c.player.maxHp==130&&c.player.hp==47&&c.player.stamina==60,"Reapplying saved tails never stacks bonuses");
        c.ApplyTailUpgrades(2);Assert(c.player.maxHp==140&&c.PlayerMaxStamina==120,"Each new tail adds its own ten-point increase");
        for(int i=0;i<240;i++)c.Step();Assert(c.player.stamina==120,"Passive regeneration fills the upgraded cap");
        foreach(string defense in new[]{"parry","dodge"}){
            var perfect=Contact(defense,defense=="parry"?0:2);perfect.ApplyTailUpgrades(1);perfect.player.stamina=40;perfect.Step();
            Assert(perfect.player.stamina==110,"Perfect defense refills the upgraded stamina cap");
        }
        var loaded=new Combat();loaded.ApplyTailUpgrades(1);
        Assert(loaded.player.hp==130&&loaded.player.stamina==110,"Reload derives full upgraded stats from tail count");
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
        Assert(new VisualCue{kind="slash",owner=1,combo=1,face=-1}.IsPlayerCombo&&!new VisualCue{kind="slash",owner=1,power=true}.IsPlayerCombo,"Normal combo uses authored arcs; executions retain the shared effect");
    }
    static void CheckPlungeCombo(){
        var c=new Combat{ai=false};var p=c.player;var b=c.boss;p.x=0;b.x=68;
        c.Press("jump");
        for(int i=0;i<10&&!c.CanPlunge;i++)c.Step();
        Assert(c.CanPlunge,"A normal jump can reach plunge height");
        p.energy=1;c.executeWindow=30;
        c.Press("attack");c.Step();
        Assert(p.state=="plunge"&&p.vy<0&&p.energy==1&&c.hits.Count==0&&b.hp==b.maxHp,
            "High attack dives even in an execution window, without hitting in midair");
        c.axis=-1;
        for(int i=0;i<15&&p.state=="plunge";i++)c.Step();
        Assert(p.state=="plungeLand"&&p.face==1&&p.y==0&&b.hp==b.maxHp-Rules.PlungeImpact.damage&&
            c.visuals.Count(v=>v.kind=="plunge_impact")==1,"A plunge keeps its heading and hits once on landing");
        while(p.age<4)c.Step();
        c.axis=0;c.Press("attack");
        for(int i=0;i<24&&p.state=="plungeLand";i++)c.Step();
        Assert(p.state=="attack"&&p.combo==2&&p.move==Rules.Player[2]&&b.hp==b.maxHp-Rules.PlungeImpact.damage,
            "Buffered follow-up starts 3A without a duplicate impact");
        while(p.age<24)c.Step();
        c.Press("attack");
        for(int i=0;i<24&&p.combo==2;i++)c.Step();
        Assert(p.state=="attack"&&p.combo==3&&p.move==Rules.Player[3],"3A chains into 4A");
        while(p.age<Rules.Player[3].wind)c.Step();
        c.DebugBoss(0);b.age=b.move.wind;double hp=p.hp;
        c.Step();
        Assert(b.state=="stagger"&&b.move==null&&p.hp==hp,"4A staggers the boss and cancels a same-frame swing");

        var platform=new Combat{ai=false,enemyActive=false};
        platform.terrain.Add(new Box(-500,0,500,80));
        platform.player.y=150;platform.player.vy=-1;platform.player.state="air";
        platform.Press("attack");platform.Step();
        Assert(platform.player.state=="attack"&&platform.player.combo==0,"Low clearance above a platform keeps the ordinary attack");
        platform.player.y=162;platform.player.state="air";platform.player.move=null;
        platform.Press("attack");
        for(int i=0;i<15&&platform.player.state!="plungeLand";i++)platform.Step();
        Assert(platform.player.state=="plungeLand"&&platform.player.y==80,"Plunge lands on a real platform");
        var rubble=new Combat{ai=false,enemyActive=false};
        rubble.terrain.Add(new Box(-300,0,100,36,noFoothold:true));
        rubble.player.y=126;rubble.player.vy=-3;rubble.player.state="air";
        rubble.Press("attack");
        for(int i=0;i<32&&rubble.player.state!="plungeLand";i++)rubble.Step();
        Assert(rubble.player.state=="plungeLand"&&rubble.player.y==0,"Loose rubble does not trigger the landing attack above the floor");
    }
    static void CheckPlayerCombo(){
        var c=new Combat{ai=false,enemyActive=false};var p=c.player;p.x=0;
        c.Press("attack");c.Step();
        for(int stage=0;stage<4;stage++){
            Assert(p.state=="attack"&&p.combo==stage&&p.move==Rules.Player[stage],"Attack chain enters stage "+(stage+1));
            var move=p.move;
            if(move.chainAt>0){
                while(p.age<move.wind-Rules.comboBufferLead)c.Step();
                c.Press("attack");
            }
            while(p.age<move.wind)c.Step();
            Assert(c.hits.Count==0,"No hit before contact for stage "+(stage+1));
            if(stage==3)Assert(c.visuals.Any(v=>v.kind=="charge"&&v.owner==1&&v.attackId==p.attackId),"Fourth attack telegraphs before contact");
            c.Step();var hit=c.hits.Single();var fx=c.visuals.Last();
            Assert(fx.IsPlayerCombo&&fx.combo==stage&&fx.originX==p.x&&fx.originY==p.y,"Combo effect matches hit frame and pose");
            if(stage==2)Assert(hit.x<p.x&&hit.x+hit.w>p.x,"Spinning cut covers both sides");
            else Assert(hit.x==p.x,"Directional attacks hit forward only");
            int until=move.chainAt>0?move.chainAt:move.wind+move.active;
            while(p.age<until){c.Step();Assert(p.move==move,"Follow-up never skips contact or the required follow-through");}
            if(move.chainAt>0){
                Assert(until<move.wind+move.active+move.recovery,"Combo can skip the final recovery poses");
                c.Step();
            }
        }
        Assert(p.x==30,"Only the thrust moves the player, by one short step");
        c.Press("attack");c.Step();Assert(p.combo==3,"Fourth attack does not wrap while recovering");
        c.ClearCommands();while(p.state=="attack")c.Step();
        c.Press("attack");c.Step();Assert(p.combo==0,"A new chain starts from the horizontal cut");
        var cancel=new Combat{ai=false,enemyActive=false};
        cancel.Press("attack");cancel.Step();
        while(cancel.player.age<Rules.Player[0].wind)cancel.Step();
        cancel.Press("attack");
        while(cancel.player.age<Rules.Player[0].wind+Rules.Player[0].active)cancel.Step();
        cancel.Press("dodge");cancel.Step();
        Assert(cancel.player.state=="dodge","Dodge can cancel a buffered combo during recovery");
        for(int i=0;i<Rules.dodgeFrames+4;i++)cancel.Step();
        Assert(cancel.player.state=="idle","Interrupted combo does not replay buffered attacks");
        var wall=new Combat{ai=false,enemyActive=false};wall.player.x=0;wall.player.face=-1;wall.player.combo=1;
        wall.player.state="attack";wall.player.move=Rules.Player[1];wall.player.age=wall.player.move.wind;
        wall.terrain.Add(new Box(-70,0,25,45));for(int i=0;i<6;i++)wall.Step();
        Assert(wall.player.x==-18&&wall.visuals.Single().face==-1,"Left-facing thrust stops at terrain and emits a mirrored cue");
        var rear=new Combat{ai=false};rear.player.x=0;rear.boss.x=-130;rear.player.combo=2;
        rear.player.state="attack";rear.player.move=Rules.Player[2];rear.player.age=rear.player.move.wind;
        for(int i=0;i<12;i++)rear.Step();Assert(rear.boss.hp==rear.boss.maxHp-Rules.Player[2].damage,"Spinning cut hits a rear target exactly once");
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
        var sim=new Combat{ai=false,enemyActive=false,boundsMin=PrologueLayout.RouteStart,boundsMax=PrologueLayout.ArenaMax};
        var steps=PrologueLayout.BrokenSteps();var guide=new TutorialDirector();
        sim.player.x=steps.x-100;sim.boss.x=PrologueLayout.WakeGuideX;
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
