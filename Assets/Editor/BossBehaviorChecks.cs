using System;
using System.Linq;
using LingyueEchoes;
using UnityEngine;
using UnityEditor;

public static class BossBehaviorChecks
{
    static int assertions;
    static void Need(bool value,string text){assertions++;if(!value)throw new Exception("Boss behavior: "+text);}
    static BossDefinition Definition(){return new BossDefinition{id="test",reactionFrames=12,initialWait=0,moves=new[]{
        new BossMoveDefinition{id="swing",move=new Move{name="Test swing",pose="slash",wind=30,active=6,recovery=24,reach=100,height=140,damage=18,posture=24,parry=true},
            maxRange=900,cooldown=100,followUps=Array.Empty<string>()}},phases=new[]{
        new BossPhaseDefinition{threshold=1,minBudget=1,maxBudget=1,maxActions=1,breatherMin=30,breatherMax=30},
        new BossPhaseDefinition{threshold=.65,minBudget=1,maxBudget=1,maxActions=1,breatherMin=24,breatherMax=24},
        new BossPhaseDefinition{threshold=.30,minBudget=1,maxBudget=1,maxActions=1,breatherMin=18,breatherMax=18}}};}
    static Combat Fight(BossDefinition definition,double distance=500){var c=new Combat{ai=false,god=true,boundsMin=-2000,boundsMax=2000};c.player.x=distance;c.ResetBoss(0,600,OpponentPattern.FromBoss(definition),seed:77);return c;}
    static void Tick(Combat c,int n){for(int i=0;i<n;i++)c.Step();}
    [MenuItem("Lingyue/Check shared Boss behavior")]
    public static void Run(){assertions=0;CheckConfiguration();CheckLayers();CheckCommitment();CheckInterrupts();CheckExecutionGate();CheckPacing();CheckProjectiles();CheckPresentation();CheckClocks();
        Debug.Log("BOSS_BEHAVIOR_CHECKS_PASSED: "+assertions+" assertions; commitment, phase boundaries, delayed perception, deterministic selection, cooldowns, budgets, interrupts, armor, projectiles, clocks and configuration");}
    static void CheckConfiguration(){
        var guardian=BossBehaviorCatalog.Guardian;var wrath=BossBehaviorCatalog.WrathGeneral;guardian.Validate();wrath.Validate();
        Need(guardian.moves.Length==6&&wrath.moves.Length==4,"both existing bosses have independent definitions");
        Need(guardian.phases.Length==2&&wrath.phases.Length==3,"arbitrary phase counts are supported");
        for(int i=0;i<Rules.Boss.Length;i++){var a=Rules.Boss[i];var b=guardian.moves[i].move;
            Need(a.name==b.name&&a.wind==b.wind&&a.active==b.active&&a.recovery==b.recovery&&a.reach==b.reach&&a.height==b.height&&a.damage==b.damage&&a.posture==b.posture&&a.speed==b.speed&&a.parry==b.parry,"guardian move values preserved "+i);
            Need((a.pulses??Array.Empty<int>()).SequenceEqual(b.pulses??Array.Empty<int>()),"guardian pulse windows preserved "+i);}
        var copy=BossBehaviorCatalog.Guardian;copy.moves[0].move.wind=999;Need(BossBehaviorCatalog.Guardian.moves[0].move.wind==30,"resource loads isolate mutable configuration");
        Need(new Combat().UsesBossBehavior,"default and future combat bosses automatically use the shared pipeline");
        var single=Definition();single.phases=new[]{single.phases[0]};var c=Fight(single);Need(c.UsesBossBehavior,"single-phase future bosses use the shared pipeline");
        c.ResetBoss(0,64,new OpponentPattern(new[]{Rules.Boss[0]},30,2,180,isBoss:false));Need(!c.UsesBossBehavior,"regular enemies explicitly retain their existing controller");
        c.ResetBoss(0,600,new OpponentPattern(new[]{Rules.Boss[0]},30,2,180));Need(c.UsesBossBehavior,"future Move-based boss patterns automatically enter the shared pipeline");
        var invalid=Definition();invalid.moves[0].followUps=new[]{"missing"};bool rejected=false;try{invalid.Validate();}catch(ArgumentException){rejected=true;}Need(rejected,"unknown follow-ups fail authoring validation");
        invalid=Definition();invalid.moves[0].move.wind=1;rejected=false;try{invalid.Validate();}catch(ArgumentException){rejected=true;}Need(rejected,"unreadable windups fail authoring validation");
    }
    static void CheckLayers(){
        var d=Definition();var s=new BossSnapshot{playerX=100,bossFace=1,playerState="idle"};var runner=new BossActionRunner();runner.Start(d.moves[0],s,1);
        d.moves[0].move.wind=999;Need(runner.Move.wind==30,"runner snapshots the committed move");
        bool rejected=false;try{runner.Start(d.moves[0],s,-1);}catch(InvalidOperationException){rejected=true;}Need(rejected,"running actions cannot be replaced");
        for(int i=0;i<30;i++)runner.Advance();Need(runner.Stage==BossActionStage.Active,"active begins on authored frame");
        for(int i=0;i<6;i++)runner.Advance();Need(runner.Stage==BossActionStage.Recovery,"recovery starts after the full hit window");
        for(int i=0;i<24;i++)runner.Advance();Need(!runner.Running&&runner.Age==60,"full recovery is mandatory");
        var p=new Fighter(1,100,120);var b=new Fighter(2,0,600);var sense=new BossPerception(12);sense.Observe(0,p,b);p.state="heal";
        for(int i=1;i<13;i++)Need(!sense.Observe(i,p,b).Healing,"state observation has reaction latency "+i);
        Need(sense.Observe(13,p,b).Healing,"observed state becomes available after reaction delay");
        for(int i=14;i<300;i++){p.state=i%2==0?"dodge":"heal";sense.Observe(i,p,b);}Need(sense.Profile.CounterBias(new BossMoveDefinition{tags=255})<=3,"history cannot multiply bias without a cap");
        var a=Definition();a.moves=new[]{a.moves[0],a.moves[0].Copy()};a.moves[1].id="other";foreach(var move in a.moves)move.cooldown=0;
        var first=new BossBrain(a,123);var second=new BossBrain(a,123);string last=null;int repeats=0;
        for(int i=0;i<1000;i++){var x=first.Pick(1,s,i,null);var y=second.Pick(1,s,i,null);Need(x.id==y.id,"seeded decisions reproduce "+i);if(x.id==last)repeats++;last=x.id;first.Commit(x,i);second.Commit(y,i);}
        Need(repeats<450,"recent-use penalty reduces immediate repetition");
        a=Definition();first=new BossBrain(a,5);var picked=first.Pick(1,s,0,null);first.Commit(picked,0);Need(first.Pick(1,s,99,null)==null,"cooldown filters before weighting");Need(first.Pick(1,s,100,null)!=null,"cooldown expires on exact frame");
        s.playerX=1000;Need(first.Pick(1,s,100,null)==null,"out-of-range moves are never forced");s.playerX=100;Need(first.Pick(1,s,100,null,Array.Empty<string>())==null,"empty follow-up list cannot select unrelated moves");
    }
    static void CheckCommitment(){
        var d=Definition();d.moves[0].trackingFrames=12;var c=Fight(d);c.DebugBoss(0);Tick(c,13);int face=c.boss.face;int id=c.boss.attackId;c.player.x=-500;
        c.DebugBoss(0);Need(c.boss.attackId==id,"debug requests also respect action commitment");
        for(int i=13;i<60;i++){c.Step();Need(c.boss.face==face,"direction stays locked after the tracking window "+i);if(i<59)Need(c.boss.move!=null,"action cannot stop early "+i);}
        Need(c.boss.move==null&&c.boss.age==0,"full action ends at its declared boundary");
        c=Fight(Definition());c.DebugBoss(0);Tick(c,10);c.boss.hp=100;
        for(int i=10;i<60;i++){c.Step();Need(c.phase==1&&(i==59||c.boss.state=="attack"),"phase request waits through active and recovery "+i);}
        // The last step above completes the action and changes its state to idle.
    }
    static void CheckInterrupts(){
        var c=Fight(Definition());c.DebugBoss(0);Tick(c,10);c.BreakBossPosture();
        Need(!c.BossAction.Running&&c.boss.move==null&&c.BossAction.LastInterrupt==BossInterrupt.PostureBreak,"posture break explicitly cancels the action");
        Tick(c,20);Need(c.BossBehaviorState=="Vulnerable","posture break exposes a readable execution window");
        Tick(c,130);Need(c.BossPostureLimit==110&&c.boss.posture==0,"recovery resets posture and increases the next threshold");
        c=Fight(Definition());c.DebugBoss(0);Tick(c,60);c.BreakBossPosture();Tick(c,150);c.ai=true;int completedAttack=c.boss.attackId;Tick(c,29);
        Need(c.boss.attackId==completedAttack&&c.BossBehaviorState=="Breather","posture break between actions also clears pending chains and preserves recovery breathing");
        c=Fight(Definition());c.BreakBossPosture();Tick(c,50);c.player.x=-60;c.player.face=1;c.player.combo=0;c.player.state="attack";c.player.move=Rules.Player[0];c.player.age=Rules.Player[0].wind;c.player.attackId=101;c.Step();
        Need(c.boss.state=="broken"&&c.boss.age==51,"ordinary hits do not restart the vulnerable window");
        var armor=Definition();armor.moves[0].armorFrom=30;armor.moves[0].armorTo=36;c=Fight(armor);c.DebugBoss(0);Tick(c,30);
        c.player.x=-60;c.player.face=1;c.player.combo=3;c.player.state="attack";c.player.move=Rules.Player[3];c.player.age=Rules.Player[3].wind;c.player.attackId=111;c.Step();
        Need(c.BossAction.Running&&c.boss.state=="attack","authored armor rejects ordinary heavy-hit interruption");
        c.boss.posture=c.BossPostureLimit-1;c.boss.invuln=0;c.hitstop=0;c.player.age=Rules.Player[3].wind;c.player.attackId=112;c.Step();Need(c.BossPostureReady&&c.boss.state=="attack"&&c.BossAction.Running,"full posture preserves the armored committed action until a small execution");
        c=Fight(Definition());c.DebugBoss(0);Tick(c,10);c.enemyActive=false;c.Step();Need(!c.BossAction.Running&&c.boss.move==null,"disabling a fight cancels all owned action state");c.enemyActive=true;c.ai=true;Tick(c,120);Need(c.boss.state!="attack"||c.boss.move!=null,"reactivation cannot leave an empty attack state");
        c=Fight(Definition());c.continueAfterDeath=true;c.DebugBoss(0);Tick(c,10);c.player.hp=0;Tick(c,50);Need(c.boss.move==null,"a committed action finishes after player death");
        c=Fight(Definition());c.DebugBoss(0);c.boss.hp=0;c.Step();Need(!c.BossAction.Running&&c.boss.state=="dead","death has immediate interruption priority");
    }
    static void PlayerHit(Combat c,Move move,string state,int id){
        c.hitstop=0;c.boss.invuln=0;c.player.x=-60;c.player.face=1;c.player.state=state;
        c.player.move=move;c.player.age=move.wind;c.player.attackId=id;c.Step();
    }
    static void CheckExecutionGate(){
        Need(Rules.finisherBase==35&&Rules.finisherPerEnergy==25,"large execution uses reduced damage constants");
        var c=Fight(Definition());c.boss.posture=c.BossPostureLimit-1;
        PlayerHit(c,Rules.Player[0],"attack",201);
        Need(c.BossPostureReady&&c.boss.state!="broken"&&c.boss.posture==c.BossPostureLimit,"ordinary threshold hit saturates posture without stun");
        c.player.state="idle";c.player.move=null;c.player.energy=1;Tick(c,180);
        Need(c.BossPostureReady&&c.CanExecute&&!c.CanFinish,"full posture supplies a persistent small execution opportunity without a timed counter window");
        double hp=c.boss.hp;PlayerHit(c,Rules.Execute,"execute",202);
        Need(c.boss.state=="broken"&&!c.BossAction.Running&&c.boss.hp==hp-Rules.Execute.damage,"small execution converts full posture to stun with original damage");
        Need(!c.CanExecute,"small execution cannot restart a broken boss");
        c.hitstop=0;Tick(c,20);int age=c.boss.age;PlayerHit(c,Rules.Execute,"execute",203);
        Need(c.boss.state=="broken"&&c.boss.age==age+1,"even an already committed small execution cannot restart stun");
        c=Fight(Definition());c.player.energy=2;c.boss.posture=0;PlayerHit(c,Rules.Execute,"execute",204);
        Need(c.boss.state=="stagger"&&c.boss.posture==Rules.Execute.posture,"counter small execution below full posture preserves short stagger");
        c=Fight(Definition());c.player.energy=2;c.boss.posture=c.BossPostureLimit;c.boss.hp=1;PlayerHit(c,Rules.Execute,"execute",205);
        Need(c.boss.state=="dead","lethal execution takes priority over posture stun");
        c=Fight(Definition());c.boss.posture=c.BossPostureLimit;c.ResetBoss(0,600,OpponentPattern.FromBoss(Definition()));
        Need(!c.BossPostureReady&&c.boss.posture==0&&c.BossPostureLimit==100,"reset removes the pending full posture opportunity");
        c=Fight(Definition());c.player.energy=2;c.player.echo=50;c.BreakBossPosture();c.player.x=-60;c.Press("attack");c.Step();
        Need(c.player.state=="finisher"&&c.player.move.damage==98&&c.player.energy==0&&c.player.echo==0,"large execution snapshots 35+25 times all charge and consumes it once");
        c=Fight(Definition());c.DebugBoss(0);c.boss.posture=c.BossPostureLimit;c.player.energy=2;PlayerHit(c,Rules.Execute,"execute",206);
        Need(c.boss.state=="broken"&&!c.BossAction.Running&&c.BossAction.LastInterrupt==BossInterrupt.PostureBreak,"full posture small execution explicitly interrupts the committed action");

    }
    static void CheckPacing(){
        var d=Definition();d.moves[0].cooldown=0;d.moves[0].followUps=new[]{"swing"};foreach(var p in d.phases){p.minBudget=p.maxBudget=2;p.maxActions=2;p.breatherMin=p.breatherMax=30;}
        var c=Fight(d);c.ai=true;int previous=0,consecutive=0,longest=0,restFrames=0;
        for(int i=0;i<1800;i++){c.Step();if(c.BossBehaviorState=="Breather"){restFrames++;consecutive=0;}if(c.boss.attackId!=previous){previous=c.boss.attackId;consecutive++;longest=Math.Max(longest,consecutive);}}
        Need(c.moveIndex>4&&longest<=2&&restFrames>=120,"cyclic follow-ups remain bounded by budget, action count and breathing windows");
        c=Fight(Definition());c.DebugBoss(0);Tick(c,10);c.boss.hp=100;Tick(c,50);Need(c.phase==1&&!c.BossAction.Running,"threshold cannot cancel the last recovery frame");c.Step();Need(c.phase==3&&c.boss.state=="phase","pending threshold transitions at the next safe boundary");
        int phase=c.phase;Tick(c,90);Need(c.phase==phase&&c.boss.state!="phase","phase transition completes once");
    }
    static void CheckProjectiles(){
        var d=Definition();d.moves[0].kind="projectile";d.moves[0].move.active=1;d.moves[0].move.recovery=30;d.moves[0].projectileSpeed=7;d.moves[0].projectileLife=120;
        var c=Fight(d);c.god=false;c.DebugBoss(0);Tick(c,61);Need(!c.BossAction.Running&&c.BossProjectiles.Count==1,"released projectile survives its action recovery");Tick(c,60);Need(c.player.hp==102,"released projectile independently contacts and deals damage once");
        c=Fight(d);c.god=false;c.terrain.Add(new Box(200,0,20,140));c.DebugBoss(0);Tick(c,120);Need(c.player.hp==120&&c.BossProjectiles.Count==0,"terrain blocks projectile damage");
        c=Fight(d);c.DebugBoss(0);Tick(c,20);c.BreakBossPosture();Tick(c,100);Need(c.BossProjectiles.Count==0&&c.hits.All(h=>h.owner!=2),"interrupted telegraph never releases a projectile");
        c=Fight(d);c.DebugBoss(0);Tick(c,31);Need(c.BossProjectiles.Count==1,"projectile appears on its authored release frame");c.ResetBoss(0,600,OpponentPattern.FromBoss(d));Need(c.BossProjectiles.Count==0,"battle reset clears released projectiles");
    }
    static void CheckPresentation(){
        var d=Definition();d.moves[0].kind="projectile";var c=Fight(d);c.DebugBoss(0);Tick(c,31);
        var root=new GameObject("Boss presentation check");
        try{var effects=new PixelEffects(root.transform);effects.Draw(c);var projectile=root.transform.Find("Boss projectile");
            Need(projectile&&projectile.gameObject.activeSelf&&projectile.GetComponent<SpriteRenderer>().sprite,"released projectile resolves an imported Unity sprite");
            var p=c.BossProjectiles[0];Need(Math.Abs(projectile.localPosition.x-p.x/100)<.0001&&Math.Abs(projectile.localPosition.y-(p.y-95)/100)<.0001,"projectile presentation matches simulation position");
            var position=projectile.localPosition;int age=p.age;c.hitstop=3;Tick(c,3);effects.Draw(c);Need(p.age==age&&projectile.localPosition==position,"projectile presentation freezes with simulation hit stop");
            c.ResetBoss(0,600,OpponentPattern.FromBoss(d));effects.Draw(c);Need(!projectile.gameObject.activeSelf,"reset hides pooled projectile presentation");
        }finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    static void CheckClocks(){
        string expected=null;
        foreach(int hz in new[]{30,60,120,144}){var c=Fight(Definition());c.ai=true;var clock=new FixedClock(c);for(int i=0;i<hz*10;i++)clock.Advance(1d/hz);
            string state=c.frame+":"+c.moveIndex+":"+c.boss.age+":"+c.BossBehaviorState+":"+c.boss.x;
            if(expected==null)expected=state;Need(state==expected,"same seed and fixed ticks produce identical AI at "+hz+" Hz");
            int frame=c.frame,age=c.boss.age;clock.paused=true;clock.Advance(.2);Need(c.frame==frame&&c.boss.age==age,"pause freezes action and AI clocks");
            clock.paused=false;c.hitstop=3;Tick(c,3);Need(c.frame==frame&&c.boss.age==age,"hit stop freezes action and AI clocks");
            clock.speed=.25;for(int i=0;i<hz;i++)clock.Advance(1d/hz);Need(c.frame==frame+15,"slow motion changes tick rate without action timing drift");
        }
    }
    public static void RunBatch(){try{Run();MigrationChecks.Run();WrathChapterChecks.Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
    public static void RebaselineBatch(){try{Run();MigrationChecks.UpdateExpectations();MigrationChecks.Run();WrathChapterChecks.Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
