using System;
using System.Collections.Generic;
using System.Linq;

namespace LingyueEchoes
{
    // Engine independent. All coordinates are feet positions, Y up; all ages are 60 Hz ticks.
    [Serializable] public class Move
    {
        public string name; public int wind, active, recovery; public double reach, height, damage, posture, speed;
        public bool parry, low; public int[] pulses;
        public Move Copy() => (Move)MemberwiseClone();
    }
    public static class Rules
    {
        public const int hz=60, parry=20, parryPerfect=12, hitstop=3, buffer=8, dodgeFrames=28,
            invulnStart=2, dodgeInvulnFrames=12, perfectDodgeFrames=8, invulnEnd=invulnStart+dodgeInvulnFrames, perfectDodgeEnd=invulnStart+perfectDodgeFrames,
            parryFrames=30, hurtFrames=28, successSlowFrames=24, executeWindow=30, executeCost=1, executeHitstop=8, staggerFrames=36, tailStatBonus=10;
        public const double playerMaxHp=120, speed=5, jump=13, gravity=.6, dodgeSpeed=9, successSlowRate=.25, finisherReach=195, finisherBase=45,
            finisherPerEnergy=75, staminaMax=100, staminaDodge=45, staminaGuard=20, staminaJump=30, staminaRegen=.4,
            echoMax=100, echoPerDamage=.9, echoParry=34, echoDodge=34, echoPerfect=100;
        public static readonly Move[] Player={
            M("斩一",8,6,18,100,90,18,12,true), M("斩二",7,6,19,115,95,22,15,true), M("斩三",12,7,29,140,110,35,26,true)};
        public static readonly Move[] Boss={
            M("横斩",30,5,28,150,105,18,24,true), M("双连斩",24,29,30,160,105,14,20,true,pulses:new[]{0,24}),
            M("突刺",36,10,32,150,85,24,30,true,10), M("震地",48,8,40,260,35,28,38,false,low:true),
            M("跃击",42,6,36,180,170,26,32,true,7), M("赤月回旋",54,34,45,210,140,22,30,false,pulses:new[]{0,28})};
        public static readonly Move Execute=M("回响处决",6,4,20,130,110,45,40,false);
        public static Move Finisher => new Move {name="铃月终结",wind=30,active=5,recovery=40,reach=finisherReach,height=170};
        static Move M(string n,int w,int a,int r,double reach,double height,double damage,double posture,bool parry,double speed=0,bool low=false,int[] pulses=null)
            => new Move{name=n,wind=w,active=a,recovery=r,reach=Math.Floor(reach*1.35+.5),height=Math.Floor(height*1.35+.5),damage=damage,posture=posture,parry=parry,speed=speed,low=low,pulses=pulses};
    }
    [Serializable] public class Fighter
    {
        public int id,face,age,attackId,combo,invuln,energy; public double x,y,vy,hp,maxHp,posture,stamina=Rules.staminaMax,echo;
        public string state="idle"; public Move move;
        public Fighter(int id,double x,double hp){this.id=id;this.x=x;this.hp=this.maxHp=hp;face=id==1?1:-1;}
    }
    [Serializable] public class Box
    {
        public double x,y,w,h;
        public bool noFoothold;
        public Box(){} public Box(double x,double y,double w,double h,bool noFoothold=false){this.x=x;this.y=y;this.w=w;this.h=h;this.noFoothold=noFoothold;}
        public static bool Overlaps(Box a,Box b)=>a.x<b.x+b.w&&a.x+a.w>b.x&&a.y<b.y+b.h&&a.y+a.h>b.y;
    }
    public class Hitbox:Box {public int owner,attackId,pulse;public Move move;}
    public class VisualCue {
        public int id,frame,face,combo,owner,attackId;public double x,y,originX,originY;public string kind;public bool power,danger;
        public int RotationDegrees=>kind=="slash"?face*(combo==1?-40:combo==2?45:0):0;
        public bool TryGetAnchor(Fighter actor,out double anchorX,out double anchorY){
            anchorX=x;anchorY=y;
            if(kind!="slash"&&kind!="charge")return true;
            if(actor.move==null||actor.attackId!=attackId||actor.face!=face)return false;
            anchorX+=actor.x-originX;anchorY+=actor.y-originY;return true;
        }
    }
    public class SoundCue {public int id,frame;public string name;}
    public class DummyContact {public int id,attackId,combo,frame;}
    public class Combat
    {
        public Fighter player=new Fighter(1,-250,Rules.playerMaxHp),boss=new Fighter(2,250,650);
        public int TailUpgrades {get;private set;}
        public double PlayerMaxStamina=>Rules.staminaMax+TailUpgrades*Rules.tailStatBonus;
        public void ApplyTailUpgrades(int count){
            count=Math.Max(0,Math.Min(9,count));
            int added=(count-TailUpgrades)*Rules.tailStatBonus;
            TailUpgrades=count;
            player.maxHp=Rules.playerMaxHp+count*Rules.tailStatBonus;
            if(player.hp>0)player.hp=Math.Max(1,Math.Min(player.maxHp,player.hp+added));
            player.stamina=Math.Max(0,Math.Min(PlayerMaxStamina,player.stamina+added));
        }
        public int frame,tick,hitstop,phase=1,moveIndex,tempoSerial,successFrame=-100,executeWindow,deflectSerial,dummyHits,dodgeEvades;
        public bool ai=true,god,enemyActive=true,nonlethal,continueAfterDeath,bossYields;
        public bool BossDefeated {get;private set;}
        public double axis,boundsMin=-560,boundsMax=560;
        public string successKind="";
        public List<Hitbox> hits=new List<Hitbox>(); public List<string> events=new List<string>();
        public List<VisualCue> visuals=new List<VisualCue>();public List<SoundCue> sounds=new List<SoundCue>();
        public List<Box> terrain=new List<Box>();public Box dummyBox;
        public List<DummyContact> dummyContacts=new List<DummyContact>();
        public System.Action<Combat> beforeStep;
        public int aiOpenWait=75,aiWaitPhase1=38,aiWaitPhase2=18;
        readonly HashSet<string> dummySeen=new HashSet<string>(),seen=new HashSet<string>();
        class Command {public string action;public int until;}
        readonly List<Command> queue=new List<Command>();
        bool dodgeRewarded;int serial,visualSerial,soundSerial,aiWait=75,lastChoice=-1;
        public static double BodyWidth(Fighter a)=>a.id==1?54:78;
        public static double BodyHeight(Fighter a)=>a.id==1?117:146;
        public void TuneBoss(double hp,int open=90,int wait=46,int wait2=24){boss.maxHp=boss.hp=hp;aiOpenWait=open;aiWaitPhase1=wait;aiWaitPhase2=wait2;aiWait=open;}
        public void ResetBoss(double x,double hp){
            boss=new Fighter(2,x,hp);TuneBoss(hp);
            BossDefeated=false;phase=1;moveIndex=0;lastChoice=-1;
            hitstop=executeWindow=0;successFrame=-100;successKind="";hits.Clear();visuals.Clear();
        }
        public void Press(string action){queue.Add(new Command{action=action,until=frame+Rules.buffer});if(queue.Count>16)queue.RemoveAt(0);}
        public void ClearCommands(){queue.Clear();axis=0;}
        public Box Hurtbox(Fighter a)=>new Box(a.x-BodyWidth(a)/2,a.y,BodyWidth(a),BodyHeight(a));
        public bool CanFinish=>enemyActive&&boss.hp>0&&boss.state=="broken"&&player.energy>=1&&Math.Abs(player.x-boss.x)<=Rules.finisherReach&&Math.Abs(player.y-boss.y)<45;
        public bool CanExecute=>enemyActive&&boss.hp>0&&boss.state!="phase"&&boss.invuln<=Rules.Execute.wind+1&&
            executeWindow>0&&player.energy>=Rules.executeCost&&Math.Abs(boss.x-player.x)<Rules.Execute.reach+BodyWidth(boss)*.5&&
            boss.y<player.y+Rules.Execute.height&&player.y<boss.y+BodyHeight(boss);
        void Log(string text){events.Insert(0,frame+": "+text);if(events.Count>7)events.RemoveAt(7);}
        void Visual(string kind,double x,double y,Fighter a){visuals.Add(new VisualCue{id=++visualSerial,frame=frame,kind=kind,x=x,y=y,originX=a.x,originY=a.y,attackId=a.attackId,face=a.face,combo=a.id==1&&a.state=="attack"?a.combo:0,power=a.state=="finisher"||a.state=="execute",owner=a.id,danger=a.move!=null&&!a.move.parry});if(visuals.Count>32)visuals.RemoveAt(0);}
        void Sound(string name){sounds.Add(new SoundCue{id=++soundSerial,frame=frame,name=name});if(sounds.Count>64)sounds.RemoveAt(0);}
        void Success(string kind){tempoSerial++;successKind=kind;successFrame=frame;hitstop=Rules.hitstop;}
        void Enter(Fighter a,string state,Move move=null){bool changed=a.state!=state;a.state=state;a.age=0;a.move=move;
            if(a.id==1&&state=="dodge"){dodgeRewarded=false;Sound("dodge");}if(a.id==1&&state=="parry")Sound("guard");
            if(changed&&state=="broken")Sound("break");if(changed&&state=="phase")Sound("phase");if(changed&&state=="dead")Sound(a.id==1?"death":"victory");
            if(move!=null){a.attackId=++serial;if(seen.Count>256)seen.RemoveWhere(k=>int.Parse(k.Split(':')[0])<serial-16);}}
        void StartAttack(Fighter a,Move m,string state="attack")=>Enter(a,state,m);
        bool Consume(string action){int i=queue.FindIndex(q=>q.action==action);if(i<0)return false;queue.RemoveAt(i);return true;}
        bool Spend(Fighter a,double cost){if(a.stamina<cost)return false;a.stamina-=cost;return true;}
        void GainEcho(Fighter a,double amount){if(amount<=0)return;a.echo+=amount;while(a.echo>=100&&a.energy<3){a.echo-=100;a.energy++;}if(a.energy>=3)a.echo=Math.Min(a.echo,99);}
        static bool Free(string s)=>s=="idle"||s=="run"||s=="air";
        public void Step()
        {
            tick++;if(hitstop>0){hitstop--;return;}frame++;hits.Clear();if(executeWindow>0)executeWindow--;queue.RemoveAll(q=>q.until<frame);
            if(player.hp<=0&&!continueAfterDeath||(enemyActive&&boss.hp<=0))return;
            var p=player;var b=boss;double oldPX=p.x,oldBX=b.x;beforeStep?.Invoke(this);
            if(continueAfterDeath&&p.hp<=0&&p.state!="dead")Enter(p,"dead");
            if(p.hp>0&&enemyActive&&!nonlethal&&phase==1&&b.hp<=b.maxHp*.5&&b.state!="broken"&&b.state!="stagger"&&p.state!="finisher"&&p.state!="execute"){phase=2;Enter(b,"phase");Log("第二阶段：节奏加速");}
            if(p.hp>0)ControlPlayer();ControlBoss();
            foreach(var a in new[]{p,b}){
                if(a.invuln>0)a.invuln--;
                if(a.id==2&&a.move?.name=="跃击"){if(a.age==a.move.wind-24)a.vy=7.2;if(a.age>=a.move.wind-24&&a.age<a.move.wind)a.x+=a.face*5;}
                double oldY=a.y;bool airborne=a.vy!=0;a.y+=a.vy;a.vy-=Rules.gravity;
                if(a.id==1&&a.state=="run"&&a.age%20==0)Sound("step");if(a.state=="dodge")a.x+=a.face*Rules.dodgeSpeed;
                double radius=BodyWidth(a)/2,floor=0,oldX=a.id==1?oldPX:oldBX;
                foreach(var slab in terrain){double top=slab.y+slab.h;
                    if(a.y<top&&oldY<top){if(oldX+radius<=slab.x&&a.x+radius>slab.x)a.x=slab.x-radius;if(oldX-radius>=slab.x+slab.w&&a.x-radius<slab.x+slab.w)a.x=slab.x+slab.w+radius;}
                    if(a.x+radius>slab.x&&a.x-radius<slab.x+slab.w&&a.y<=top&&a.vy<=0){
                        if(slab.noFoothold&&oldY>slab.y){
                            // Follow the loose face to either side without resetting the fall or granting another jump.
                            double edge=oldX<=slab.x+slab.w*.5?slab.x-radius:slab.x+slab.w+radius;
                            a.x+=Math.Sign(edge-a.x)*Math.Min(12,Math.Abs(edge-a.x));
                            double slope=slab.h*Math.Abs(edge-a.x)/(slab.w*.5+radius);
                            a.y=Math.Max(a.y,slab.y+slope);
                        }else if(!slab.noFoothold&&oldY>=top)floor=Math.Max(floor,top);
                    }}
                if(a.y<=floor){a.y=floor;a.vy=0;if(airborne&&a.id==1)Sound("land");}
                var move=a.move;
                if(move!=null&&a.age>=move.wind&&a.age<move.wind+move.active)a.x+=a.face*move.speed;
                a.x=Math.Max(boundsMin,Math.Min(boundsMax,a.x));
            }
            if(p.state=="execute"&&p.move!=null&&p.age<=p.move.wind&&b.x!=p.x)p.face=b.x>p.x?1:-1;
            EmitAttack(p);EmitAttack(b);
            if(dummyBox!=null)foreach(var h in hits){if(h.owner!=1||p.state=="execute"||!Box.Overlaps(h,dummyBox))continue;string key=h.attackId+":"+h.pulse;if(!dummySeen.Add(key))continue;
                dummyHits++;dummyContacts.Add(new DummyContact{id=dummyHits,attackId=h.attackId,combo=p.combo,frame=frame});if(dummyContacts.Count>32)dummyContacts.RemoveAt(0);
                Visual("impact",dummyBox.x+dummyBox.w*.5,dummyBox.y+dummyBox.h*.5,p);Sound("hit_boss");}
            foreach(var h in hits){var target=h.owner==1?b:p;if(target.id==2&&!enemyActive)continue;if(Box.Overlaps(h,Hurtbox(target)))Resolve(h,target);}
            foreach(var a in new[]{p,b}){a.age++;if(a.hp<=0){if(!continueAfterDeath||a.state!="dead")Enter(a,"dead");continue;}
                if(a.move!=null&&a.age>=a.move.wind+a.move.active+a.move.recovery)Enter(a,a.y>0?"air":"idle");
                else if((a.state=="parry"&&a.age>=Rules.parryFrames)||(a.state=="dodge"&&a.age>=Rules.dodgeFrames)||(a.state=="hurt"&&a.age>=Rules.hurtFrames)||(a.state=="stagger"&&a.age>=Rules.staggerFrames)||(a.state=="broken"&&a.age>=150&&p.state!="finisher")||(a.state=="phase"&&a.age>=90)){
                    if(a.state=="broken")a.posture=0;Enter(a,a.y>0?"air":"idle");}
                if(a.id==2&&(a.state=="idle"||a.state=="run")&&a.age%30==0)a.posture=Math.Max(0,a.posture-3);
                if(a.id==1&&(a.state=="idle"||a.state=="run")&&a.stamina<PlayerMaxStamina)a.stamina=Math.Min(PlayerMaxStamina,a.stamina+Rules.staminaRegen);
            }
        }
        void EmitAttack(Fighter a){
            var m=a.move;if(m==null)return;
            int t=a.age-m.wind;
            if(a.id==2&&t==-12){var pt=WeaponPoint(m.name);Visual("charge",a.x+a.face*pt.Item1,a.y+pt.Item2,a);}
            if(t<0||t>=m.active)return;
            int pulse=m.pulses==null?0:Array.FindIndex(m.pulses,s=>t>=s&&t<s+4);
            if(pulse<0)return;
            bool execute=a.id==1&&a.state=="execute";
            // The target was in reach at startup; resolve the lock at its current position on contact.
            var area=execute?Hurtbox(boss):new Box(a.face>0?a.x:a.x-m.reach,a.y,m.reach,m.height);
            hits.Add(new Hitbox{x=area.x,y=area.y,w=area.w,h=area.h,owner=a.id,attackId=a.attackId,pulse=pulse,move=m});
            if(t==(m.pulses==null?0:m.pulses[pulse])){
                Visual("slash",a.x+a.face*m.reach*.45,a.y+m.height*.5,a);
                Sound(a.id==1?(a.state=="finisher"||execute?"counter":new[]{"slash1","slash2","slash3"}[a.combo]):"boss_swing");
            }
        }
        static Tuple<double,double> WeaponPoint(string n){switch(n){case "横斩":return Tuple.Create(35d,62d);case "双连斩":return Tuple.Create(30d,100d);case "突刺":return Tuple.Create(40d,65d);case "震地":return Tuple.Create(-15d,146d);case "跃击":return Tuple.Create(-10d,145d);case "赤月回旋":return Tuple.Create(-35d,75d);default:return Tuple.Create(35d,75d);}}
        void ControlPlayer(){var p=player;bool free=Free(p.state),recovering=(p.move!=null&&(p.state=="attack"||p.state=="finisher"||p.state=="execute")&&p.age>=p.move.wind+p.move.active)||(p.state=="parry"&&p.age>=Rules.parry);
            bool canAct=free||recovering||p.state=="parry"||p.state=="dodge";
            if(CanFinish&&canAct&&Consume("attack")){double charge=p.energy+p.echo/100;p.energy=0;p.echo=0;executeWindow=0;p.face=boss.x>=p.x?1:-1;var m=Rules.Finisher;m.damage=Math.Floor(45+75*charge+.5);StartAttack(p,m,"finisher");Log("铃月终结 · 消耗全部回响");}
            else if(CanExecute&&canAct&&Consume("attack")){executeWindow=0;StartAttack(p,Rules.Execute,"execute");Log("回响处决");}
            else if(free||recovering){if(Consume("dodge")){if(Spend(p,45)){if(axis!=0)p.face=Math.Sign(axis);Enter(p,"dodge");}else Log("气力不足 · 无法闪避");}
                else if(free){if(Consume("parry")){if(Spend(p,20))Enter(p,"parry");else Log("气力不足 · 无法格挡");}
                    else if(Consume("attack")){p.combo=0;if(axis!=0)p.face=Math.Sign(axis);StartAttack(p,Rules.Player[0]);}
                    else if(p.vy==0&&Consume("jump")){if(Spend(p,30)){p.vy=13;Enter(p,"air");Sound("jump");}else Log("气力不足 · 无法跳跃");}}
                else if(p.state=="attack"&&p.combo<2&&Consume("attack")){p.combo++;if(axis!=0)p.face=Math.Sign(axis);StartAttack(p,Rules.Player[p.combo]);}}
            if(Free(p.state)){p.x+=axis*5;if(axis!=0)p.face=Math.Sign(axis);string next=p.y>0||p.vy>0?"air":axis!=0?"run":"idle";if(p.state!=next)Enter(p,next);}}
        void ControlBoss(){var b=boss;var p=player;if(!enemyActive||!ai||(b.state!="idle"&&b.state!="run"))return;
            if(p.hp<=0){if(b.state=="run")Enter(b,b.y>0?"air":"idle");return;}b.face=p.x>=b.x?1:-1;
            if(--aiWait>0){bool moving=Math.Abs(p.x-b.x)>215;if(moving)b.x+=b.face*(phase==2?3:2);string next=moving?"run":"idle";if(b.state!=next)Enter(b,next);return;}
            double distance=Math.Abs(p.x-b.x);int choice=moveIndex++%6;if(distance>330&&choice<2)choice=2;if(choice==lastChoice)choice=(choice+1)%6;lastChoice=choice;var m=Rules.Boss[choice].Copy();
            if(phase==2){m.wind=(int)Math.Ceiling(m.wind*.75);m.recovery=(int)Math.Ceiling(m.recovery*.75);}StartAttack(b,m);aiWait=phase==2?aiWaitPhase2:aiWaitPhase1;Log(m.name+(m.parry?" · 可弹反":" · 危险招式"));}
        public void DebugBoss(int index){if(!enemyActive||boss.hp<=0)return;boss.face=player.x>=boss.x?1:-1;StartAttack(boss,Rules.Boss[index].Copy());}
        void Resolve(Hitbox h,Fighter t){string key=h.attackId+":"+h.pulse+":"+t.id;if(!seen.Add(key))return;if(t.hp<=0||t.state=="phase"||t.invuln>0)return;
            var attacker=h.owner==1?player:boss;if(attacker.attackId!=h.attackId||attacker.move==null)return;
            if(t.state=="dodge"&&t.age>=Rules.invulnStart&&t.age<Rules.invulnEnd){if(t.id==1){dodgeEvades++;if(!dodgeRewarded){dodgeRewarded=true;bool perfect=t.age<Rules.perfectDodgeEnd;GainEcho(t,perfect?Rules.echoPerfect:Rules.echoDodge);
                        if(perfect){t.stamina=PlayerMaxStamina;Visual("perfectDodge",t.x,t.y+42,t);Sound("perfect_dodge");Success("perfectDodge");executeWindow=30;Log("极限闪避 · 回响充满 · 气力回满");}else Log("闪避成功 · 回复回响");}}return;}
            if(t.id==1&&t.state=="parry"&&t.age<Rules.parry&&h.move.parry&&t.face*(attacker.x-t.x)>=0){bool perfect=t.age<Rules.parryPerfect;deflectSerial++;Visual("parry",t.x+t.face*18,t.y+48,t);Sound("parry");attacker.posture+=32;GainEcho(t,perfect?Rules.echoPerfect:Rules.echoParry);
                if(perfect){t.stamina=PlayerMaxStamina;executeWindow=30;Success("perfectParry");Log("完美弹刀 · 气力回满");}else Log("弹刀成功");if(attacker.posture>=100){Enter(attacker,"broken");Log("Boss 架势崩解");}Enter(t,t.y>0?"air":"idle");return;}
            bool finisher=attacker.state=="finisher",small=attacker.state=="execute";double dealt=h.move.damage*(t.state=="broken"&&!finisher?1.5:1);
            if(small)attacker.energy-=Rules.executeCost;
            if(!(t.id==1&&(god||nonlethal)))t.hp=Math.Max(nonlethal?1:0,t.hp-dealt);
            if(attacker.id==1&&!finisher)GainEcho(attacker,dealt*.9);Visual("impact",t.x,t.y+Hurtbox(t).h*.55,attacker);Sound(t.id==1?"hit_player":"hit_boss");t.invuln=10;
            if(t.id==2)t.posture=finisher?0:t.posture+h.move.posture;
            if(t.hp==0&&t.id==2&&bossYields){
                t.hp=1;BossDefeated=true;enemyActive=ai=false;Enter(t,"broken");
            }else if(t.hp==0)Enter(t,"dead");else if(t.id==2&&small){t.posture=Math.Min(100,t.posture);Enter(t,"stagger");}else if(t.id==2&&t.posture>=100){t.posture=100;Enter(t,"broken");}else if(t.id==2&&finisher)Enter(t,"stagger");else if(t.id==1)Enter(t,"hurt");
            hitstop=small||finisher?8:3;Log((t.id==1?"玩家":"Boss")+" 受击 "+h.move.damage);
        }
    }
    public class FixedClock
    {
        public double accumulator,speed=1;public bool paused;public int cinematicTicks;double fraction;int tempoSeen;public Combat combat;
        public FixedClock(Combat combat){this.combat=combat;tempoSeen=combat.tempoSerial;}
        public double Rate=>speed*(cinematicTicks>0?.25:1);
        public void ClearAccumulation(){accumulator=0;fraction=0;}
        void ObserveTempo(){if(tempoSeen!=combat.tempoSerial){tempoSeen=combat.tempoSerial;cinematicTicks=24;fraction=0;}}
        public void Advance(double dt){if(paused)return;accumulator+=Math.Min(Math.Max(dt,0),.25);while(accumulator+1e-9>=1d/60){accumulator-=1d/60;ObserveTempo();if(combat.hitstop>0){combat.Step();continue;}double rate=Rate;if(cinematicTicks>0)cinematicTicks--;fraction+=rate;while(fraction+1e-9>=1){fraction--;combat.Step();ObserveTempo();}}}
        public void Single(){combat.Step();ObserveTempo();}
    }
}
