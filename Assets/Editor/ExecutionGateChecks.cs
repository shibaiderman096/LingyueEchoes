using System;
using LingyueEchoes;
using UnityEditor;
using UnityEngine;

public static class ExecutionGateChecks
{
    static int assertions;
    static void Need(bool value,string message){assertions++;if(!value)throw new Exception("Execution gate: "+message);}
    static Combat Fight(){var c=new Combat{ai=false,god=true,boundsMin=-2000,boundsMax=2000};c.player.x=500;c.ResetBoss(0,600);return c;}
    static void Tick(Combat c,int frames){for(int i=0;i<frames;i++)c.Step();}
    static void Until(Combat c,Func<bool> condition,int limit,string message){for(int i=0;i<limit&&!condition();i++)c.Step();Need(condition(),message);}
    [MenuItem("Lingyue/Check execution gate")]
    public static void Run(){assertions=0;CheckExecutionGate();CheckNaturalExecutionChain();Debug.Log("EXECUTION_GATE_CHECKS_PASSED: "+assertions+" assertions");}
    static void PlayerHit(Combat c,Move move,string state,int id){
        c.hitstop=0;c.boss.invuln=0;c.player.x=-60;c.player.face=1;c.player.state=state;
        c.player.move=move;c.player.age=move.wind;c.player.attackId=id;c.Step();
    }
    static void CheckExecutionGate(){
        Need(Rules.finisherBase==35&&Rules.finisherPerEnergy==25,"large execution uses reduced damage constants");
        var c=Fight();c.boss.posture=99;
        PlayerHit(c,Rules.Player[0],"attack",201);
        Need(c.BossPostureReady&&c.boss.state!="broken"&&c.boss.posture==100,"ordinary threshold hit saturates posture without stun");
        c.player.state="idle";c.player.move=null;c.player.energy=1;Tick(c,180);
        Need(c.BossPostureReady&&c.CanExecute&&!c.CanFinish,"full posture supplies a persistent small execution opportunity without a timed counter window");
        double hp=c.boss.hp;PlayerHit(c,Rules.Execute,"execute",202);
        Need(c.boss.state=="broken"&&c.boss.move==null&&c.boss.hp==hp-Rules.Execute.damage,"small execution converts full posture to stun with original damage");
        Need(!c.CanExecute,"small execution cannot restart a broken boss");
        c.hitstop=0;Tick(c,20);int age=c.boss.age;PlayerHit(c,Rules.Execute,"execute",203);
        Need(c.boss.state=="broken"&&c.boss.age==age+1,"even an already committed small execution cannot restart stun");
        c=Fight();c.player.energy=2;c.boss.posture=0;PlayerHit(c,Rules.Execute,"execute",204);
        Need(c.boss.state=="stagger"&&c.boss.posture==Rules.Execute.posture,"counter small execution below full posture preserves short stagger");
        c=Fight();c.player.energy=2;c.boss.posture=100;c.boss.hp=1;PlayerHit(c,Rules.Execute,"execute",205);
        Need(c.boss.state=="dead","lethal execution takes priority over posture stun");
        c=Fight();c.boss.posture=100;c.ResetBoss(0,600,null);
        Need(!c.BossPostureReady&&c.boss.posture==0,"reset removes the pending full posture opportunity");
        c=Fight();c.player.energy=2;c.player.echo=50;c.boss.state="broken";c.boss.posture=100;c.boss.age=0;c.boss.move=null;c.player.x=-60;c.Press("attack");c.Step();
        Need(c.player.state=="finisher"&&c.player.move.damage==98&&c.player.energy==0&&c.player.echo==0,"large execution snapshots 35+25 times all charge and consumes it once");
        c=Fight();c.DebugBoss(0);c.boss.posture=100;c.player.energy=2;PlayerHit(c,Rules.Execute,"execute",206);
        Need(c.boss.state=="broken"&&c.boss.move==null,"full posture small execution explicitly interrupts the committed action");

    }
    static void CheckNaturalExecutionChain(){
        var c=Fight();c.player.x=-60;c.player.face=1;c.player.energy=3;c.boss.posture=99;
        c.DebugBoss(0);int attack=c.boss.attackId;Tick(c,25);c.Press("parry");
        Until(c,()=>c.BossPostureReady,20,"a natural parry fills posture");
        Need(c.boss.posture==100&&c.boss.move!=null&&c.boss.attackId==attack&&c.boss.state=="attack","parry saturation preserves the current committed Boss action");
        Need(c.CanExecute&&!c.CanFinish,"parry saturation offers the small execution first");
        double hp=c.boss.hp;c.Press("attack");
        Until(c,()=>c.player.state=="execute",20,"attack input starts the small execution");
        Until(c,()=>c.boss.state=="broken",30,"small execution contact stuns the Boss");
        Need(c.boss.hp==hp-Rules.Execute.damage&&c.boss.move==null,"small execution deals original damage and interrupts the Boss");
        Until(c,()=>c.player.state=="idle",60,"player recovers naturally from the small execution");
        Need(c.CanFinish&&c.boss.state=="broken","the large execution remains available after small execution recovery");
        double charge=c.player.energy+c.player.echo/100;double damage=Math.Floor(Rules.finisherBase+Rules.finisherPerEnergy*charge+.5);hp=c.boss.hp;
        c.Press("attack");c.Step();
        Need(c.player.state=="finisher"&&c.player.move.damage==damage&&c.player.energy==0&&c.player.echo==0,"next attack starts the large execution and consumes the remaining charge once");
        Until(c,()=>c.boss.hp<hp,50,"large execution reaches its authored contact frame");
        Need(c.boss.hp==hp-damage&&c.boss.state=="stagger","large execution applies its reduced damage and ends the vulnerability");
    }
}
