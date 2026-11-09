using System;
using System.Collections.Generic;
using System.Linq;

namespace LingyueEchoes
{
    // Engine independent. All coordinates are feet positions, Y up; all ages are 60 Hz ticks.
    [Serializable] public class Move
    {
        public string name; public int wind, active, recovery, chainAt; public double reach, height, damage, posture, speed;
        public bool parry, low; public int[] pulses;
        public string pose; public int fireDelay;
        public Move Copy() => (Move)MemberwiseClone();
    }
    public sealed class OpponentPattern
    {
        public readonly Move[] opening,rage;
        public readonly int wait,rageWait,phaseFrames;
        public readonly double pace,engage;
        public readonly bool isBoss;
        public readonly BossDefinition behavior;
        public OpponentPattern(Move[] opening,int wait,double pace,double engage,Move[] rage=null,int rageWait=0,int phaseFrames=54,bool isBoss=true,BossDefinition behavior=null){
            this.opening=opening??Array.Empty<Move>();this.wait=wait;this.pace=pace;this.engage=engage;this.rage=rage;this.rageWait=rageWait;this.phaseFrames=phaseFrames;
            this.isBoss=isBoss;this.behavior=behavior?.Copy();
        }
        public static OpponentPattern FromBoss(BossDefinition definition){definition.Validate();var d=definition.Copy();var moves=d.moves.Select(m=>m.move).ToArray();return new OpponentPattern(moves,d.initialWait,d.phases[0].approachSpeed,d.phases[0].preferredRange,d.phases.Length>1?moves:null,0,d.phases[0].transitionFrames,true,d);}
        public Move[] Moves(int phase)=>phase==2&&rage!=null?rage:opening;
    }
    public static class Rules
    {
        public const int hz=60, parry=20, parryPerfect=12, hitstop=3, buffer=8, comboBufferLead=5, dodgeFrames=28,
            invulnStart=2, dodgeInvulnFrames=12, perfectDodgeFrames=8, invulnEnd=invulnStart+dodgeInvulnFrames, perfectDodgeEnd=invulnStart+perfectDodgeFrames,
            parryFrames=30, hurtFrames=28, successSlowFrames=24, executeWindow=30, executeCost=1, executeHitstop=8, staggerFrames=36, tailStatBonus=10,
            healCost=1, healRelease=18, healFrames=42, healAmount=15;
        public const double playerMaxHp=120, speed=5, jump=13, gravity=.6, dodgeSpeed=9, successSlowRate=.25, finisherReach=195, finisherBase=35,
            finisherPerEnergy=25, staminaMax=100, staminaDodge=45, staminaGuard=20, staminaJump=30, staminaRegen=.4,
            echoMax=100, echoPerDamage=.9, echoParry=34, echoDodge=34, echoPerfect=100;
        public const double plungeMinHeight=72, plungeFallSpeed=18, plungeDrift=3.5;
        public const double guardianHp=950, guardianWalkScale=1.3;
        public static readonly Move[] Player={
            M("横斩",8,4,18,100,90,18,12,true,chainAt:14), M("踏步突刺",10,6,20,115,70,22,15,true,5,chainAt:18),
            M("回旋斩",12,8,28,140,110,35,26,true,chainAt:29), M("蓄力下斩",27,5,30,115,125,42,30,true)};
        public static readonly Move[] Boss={
            M("横斩",30,5,28,150,105,18,24,true), M("双连斩",24,29,30,160,105,14,20,true,pulses:new[]{0,24}),
            M("突刺",36,10,32,150,85,24,30,true,10), M("震地",48,8,40,260,35,28,38,false,low:true),
            M("跃击",42,6,36,180,170,26,32,true,7), M("赤月回旋",54,34,45,210,140,22,30,false,pulses:new[]{0,28})};
        public static readonly Move Execute=M("回响处决",6,4,20,130,110,45,40,false);
        public static readonly Move PlungeImpact=new Move{name="下落斩",active=3,recovery=17,chainAt=11,reach=84,height=82,damage=28,posture=20};
        public static Move Finisher => new Move {name="铃月终结",wind=30,active=5,recovery=40,reach=finisherReach,height=170};
        static Move M(string n,int w,int a,int r,double reach,double height,double damage,double posture,bool parry,double speed=0,bool low=false,int[] pulses=null,int chainAt=0)
            => new Move{name=n,wind=w,active=a,recovery=r,chainAt=chainAt,reach=Math.Floor(reach*1.35+.5),height=Math.Floor(height*1.35+.5),damage=damage,posture=posture,parry=parry,speed=speed,low=low,pulses=pulses};
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
    public class Hitbox:Box {public int owner,attackId,pulse;public Move move;public bool released;public double sourceX;public BossAttackPose pose;
        public bool Touches(Box target){if(!Box.Overlaps(this,target))return false;return pose==null||pose.Touches(target);}
    }
    public class VisualCue {
        public int id,frame,face,combo,owner,attackId,pulse;public double x,y,originX,originY;public string kind;public bool power,danger;
        public bool IsPlayerCombo=>kind=="slash"&&owner==1&&!power;
        public bool TryGetAnchor(Fighter actor,out double anchorX,out double anchorY){
            anchorX=x;anchorY=y;
            if(kind!="slash"&&kind!="charge")return true;
            if(actor.move==null||actor.attackId!=attackId||actor.face!=face)return false;
            anchorX+=actor.x-originX;anchorY+=actor.y-originY;return true;
        }
    }
    public class SoundCue {public int id,frame;public string name;}
    public class DummyContact {public int id,attackId,combo,frame;}
    public partial class Combat
    {
        static readonly string[] PlayerSlashSounds={"slash1","slash2","slash3","slash4"};
        public Fighter player=new Fighter(1,-250,Rules.playerMaxHp),boss=new Fighter(2,250,Rules.guardianHp);
        public int TailUpgrades {get;private set;}
        public bool HealUnlocked=>TailUpgrades>0;
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
        bool dodgeRewarded,comboBuffered;int serial,visualSerial,soundSerial,aiWait=75,lastChoice=-1;
        OpponentPattern opponent;
        double bossWalkScale=1;
        int fireAttack;double fireX;
        static readonly Move DelayedFire=new Move{name="余焰",damage=16};
        public static double BodyWidth(Fighter a)=>a.id==1?54:78;
        public static double BodyHeight(Fighter a)=>a.id==1?117:146;
        public void TuneBoss(double hp,int open=90,int wait=46,int wait2=24){boss.maxHp=boss.hp=hp;aiOpenWait=open;aiWaitPhase1=wait;aiWaitPhase2=wait2;aiWait=open;}
        public Combat(){ResetBossBehavior(null,1);}
        public void ResetBoss(double x,double hp,OpponentPattern pattern=null,double walkScale=1,int seed=1){
            boss=new Fighter(2,x,hp);TuneBoss(hp);
            bossWalkScale=walkScale;
            opponent=pattern;fireAttack=0;if(pattern!=null)aiWait=30;
            BossDefeated=false;phase=1;moveIndex=0;lastChoice=-1;
            hitstop=executeWindow=0;successFrame=-100;successKind="";hits.Clear();visuals.Clear();
            ResetBossBehavior(pattern,seed);
        }
        public void Press(string action){
            var m=player.move;
            int bufferFrom=player.state=="plungeLand"&&m!=null?m.active:m?.wind-Rules.comboBufferLead??0;
            if(action=="attack"&&CanLinkCombo(player)&&player.age>=bufferFrom&&player.age<m.chainAt){
                comboBuffered=true;return;
            }
            queue.Add(new Command{action=action,until=frame+Rules.buffer});if(queue.Count>16)queue.RemoveAt(0);
        }
        public void ClearCommands(){queue.Clear();comboBuffered=false;axis=0;}
        public Box Hurtbox(Fighter a)=>new Box(a.x-BodyWidth(a)/2,a.y,BodyWidth(a),BodyHeight(a));
        public bool CanFinish=>enemyActive&&boss.hp>0&&boss.state=="broken"&&player.energy>=1&&Math.Abs(player.x-boss.x)<=Rules.finisherReach&&Math.Abs(player.y-boss.y)<45;
        public bool BossPostureReady=>enemyActive&&boss.hp>0&&boss.posture>=BossPostureLimit&&boss.state!="broken";
        public bool CanExecute=>enemyActive&&boss.hp>0&&boss.state!="broken"&&boss.state!="phase"&&boss.invuln<=Rules.Execute.wind+1&&
            (executeWindow>0||BossPostureReady)&&player.energy>=Rules.executeCost&&Math.Abs(boss.x-player.x)<Rules.Execute.reach+BodyWidth(boss)*.5&&
            boss.y<player.y+Rules.Execute.height&&player.y<boss.y+BodyHeight(boss);
        void Log(string text){events.Insert(0,frame+": "+text);if(events.Count>7)events.RemoveAt(7);}
        VisualCue Visual(string kind,double x,double y,Fighter a){var cue=new VisualCue{id=++visualSerial,frame=frame,kind=kind,x=x,y=y,originX=a.x,originY=a.y,attackId=a.attackId,face=a.face,combo=a.id==1&&a.state=="attack"?a.combo:0,power=a.state=="finisher"||a.state=="execute",owner=a.id,danger=a.move!=null&&!a.move.parry};visuals.Add(cue);if(visuals.Count>32)visuals.RemoveAt(0);return cue;}
        void Sound(string name){sounds.Add(new SoundCue{id=++soundSerial,frame=frame,name=name});if(sounds.Count>64)sounds.RemoveAt(0);}
        void Success(string kind){tempoSerial++;successKind=kind;successFrame=frame;hitstop=Rules.hitstop;}
        void Enter(Fighter a,string state,Move move=null){if(a.id==2)OnBossStateChanged(state);bool changed=a.state!=state;a.state=state;a.age=0;a.move=move;
            if(a.id==1)comboBuffered=false;
            if(a.id==1&&state=="dodge"){dodgeRewarded=false;Sound("dodge");}if(a.id==1&&state=="parry")Sound("guard");
            if(changed&&state=="broken")Sound("break");if(changed&&state=="phase")Sound("phase");if(changed&&state=="dead")Sound(a.id==1?"death":"victory");
            if(move!=null){a.attackId=++serial;if(seen.Count>256)seen.RemoveWhere(k=>int.Parse(k.Split(':')[0])<serial-16);}}
        void StartAttack(Fighter a,Move m,string state="attack")=>Enter(a,state,m);
        bool Consume(string action){int i=queue.FindIndex(q=>q.action==action);if(i<0)return false;queue.RemoveAt(i);return true;}
        bool ConsumeAttack(bool canChain){
            if(canChain&&comboBuffered){comboBuffered=false;return true;}
            return Consume("attack");
        }
        bool Spend(Fighter a,double cost){if(a.stamina<cost)return false;a.stamina-=cost;return true;}
        void GainEcho(Fighter a,double amount){if(amount<=0)return;a.echo+=amount;while(a.echo>=100&&a.energy<3){a.echo-=100;a.energy++;}if(a.energy>=3)a.echo=Math.Min(a.echo,99);}
        static bool Free(string s)=>s=="idle"||s=="run"||s=="air";
        static bool CanLinkCombo(Fighter p)=>(p.state=="attack"||p.state=="plungeLand")&&p.move!=null&&p.move.chainAt>0;
        public bool CanPlunge {
            get {
                var p=player;
                if(!Free(p.state)||p.y<Rules.plungeMinHeight||p.vy==0&&p.state!="air")return false;
                double radius=BodyWidth(p)/2;
                return !terrain.Any(s=>!s.noFoothold&&p.x+radius>s.x&&p.x-radius<s.x+s.w&&
                    p.y>=s.y+s.h&&p.y-(s.y+s.h)<Rules.plungeMinHeight);
            }
        }
        public void Step()
        {
            tick++;if(hitstop>0){hitstop--;return;}frame++;hits.Clear();if(executeWindow>0)executeWindow--;queue.RemoveAll(q=>q.until<frame);
            if(player.hp<=0&&!continueAfterDeath||(enemyActive&&boss.hp<=0)){if(boss.hp<=0)InterruptBoss(BossInterrupt.Death);return;}
            var p=player;var b=boss;double oldPX=p.x,oldBX=b.x;beforeStep?.Invoke(this);
            if(continueAfterDeath&&p.hp<=0&&p.state!="dead")Enter(p,"dead");
            BeginBossStep();
            bool phaseThreshold=!UsesBossBehavior&&(opponent==null?b.hp<=b.maxHp*.5:opponent.rage!=null&&b.hp<b.maxHp*.5);
            if(p.hp>0&&enemyActive&&!nonlethal&&phase==1&&phaseThreshold&&b.state!="broken"&&b.state!="stagger"&&p.state!="finisher"&&p.state!="execute"){phase=2;Enter(b,"phase");Log("第二阶段：节奏加速");}
            if(p.hp>0)ControlPlayer();ControlBoss();
            foreach(var a in new[]{p,b}){
                if(a.invuln>0)a.invuln--;
                if(a.id==2&&(a.move?.name=="跃击"||a.move?.pose=="leap")){if(a.age==a.move.wind-24)a.vy=7.2;if(a.age>=a.move.wind-24&&a.age<a.move.wind)a.x+=a.face*5;}
                bool plunging=a.id==1&&a.state=="plunge";
                if(plunging){a.vy=-Rules.plungeFallSpeed;a.x+=a.face*Rules.plungeDrift;}
                double oldY=a.y;bool airborne=a.vy!=0;a.y+=a.vy;a.vy-=Rules.gravity;
                if(a.id==1&&a.state=="run"&&a.age%20==0)Sound("step");if(a.state=="dodge")a.x+=a.face*Rules.dodgeSpeed;
                var move=a.move;
                bool stepping=move!=null&&a.age>=move.wind&&a.age<move.wind+move.active;
                if(stepping&&(a.id==1||UsesBossBehavior))a.x+=a.face*move.speed; // Committed lunges use the normal terrain collision.
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
                if(a.y<=floor){a.y=floor;a.vy=0;
                    if(plunging)StartAttack(a,Rules.PlungeImpact,"plungeLand");
                    else if(airborne&&a.id==1)Sound("land");}
                if(stepping&&a.id==2&&!UsesBossBehavior)a.x+=a.face*move.speed;
                a.x=Math.Max(boundsMin,Math.Min(boundsMax,a.x));
            }
            if(p.state=="execute"&&p.move!=null&&p.age<=p.move.wind&&b.x!=p.x)p.face=b.x>p.x?1:-1;
            EmitAttack(p);EmitAttack(b);AdvanceBossProjectiles();
            if(dummyBox!=null)foreach(var h in hits){if(h.owner!=1||p.state=="execute"||!h.Touches(dummyBox))continue;string key=h.attackId+":"+h.pulse;if(!dummySeen.Add(key))continue;
                dummyHits++;dummyContacts.Add(new DummyContact{id=dummyHits,attackId=h.attackId,combo=p.combo,frame=frame});if(dummyContacts.Count>32)dummyContacts.RemoveAt(0);
                Visual("impact",dummyBox.x+dummyBox.w*.5,dummyBox.y+dummyBox.h*.5,p);Sound("hit_boss");}
            foreach(var h in hits){var target=h.owner==1?b:p;if(target.id==2&&!enemyActive)continue;if(h.Touches(Hurtbox(target)))Resolve(h,target);}
            if(p.state=="heal"&&p.age==Rules.healRelease&&p.hp>0&&p.hp<p.maxHp&&p.energy>=Rules.healCost){
                p.energy-=Rules.healCost;p.hp=Math.Min(p.maxHp,p.hp+Rules.healAmount);
                Visual("heal_fx",p.x,p.y,p);Sound("heal");Log("回响疗愈 · 恢复生命");
            }
            foreach(var a in new[]{p,b}){if(a.id==2&&AdvanceBossAction())continue;a.age++;if(a.hp<=0){if(!continueAfterDeath||a.state!="dead")Enter(a,"dead");continue;}
                if(a.move!=null&&a.age>=a.move.wind+a.move.active+a.move.recovery)Enter(a,a.y>0?"air":"idle");
                else if((a.state=="parry"&&a.age>=Rules.parryFrames)||(a.state=="dodge"&&a.age>=Rules.dodgeFrames)||(a.state=="heal"&&a.age>=Rules.healFrames)||(a.state=="hurt"&&a.age>=Rules.hurtFrames)||(a.state=="stagger"&&a.age>=Rules.staggerFrames)||(a.state=="broken"&&a.age>=(a.id==2?BossStaggerFrames:150)&&p.state!="finisher")||(a.state=="phase"&&a.age>=(a.id==2?BossTransitionFrames:90))){
                    if(a.state=="broken")a.posture=0;Enter(a,a.y>0?"air":"idle");}
                if(a.id==2&&(a.state=="idle"||a.state=="run")&&a.age%30==0&&!BossPostureReady)a.posture=Math.Max(0,a.posture-3);
                if(a.id==1&&(a.state=="idle"||a.state=="run")&&a.stamina<PlayerMaxStamina)a.stamina=Math.Min(PlayerMaxStamina,a.stamina+Rules.staminaRegen);
            }
        }
        void EmitAttack(Fighter a){
            var m=a.move;if(m==null)return;
            if(a.id==2&&TryEmitBossProjectile())return;
            int t=a.age-m.wind;
            if(a.id==2&&m.fireDelay>0)EmitDelayedFire(a,m,t);
            if(a.id==2&&t==-12){var pt=WeaponPoint(m.name);Visual("charge",a.x+a.face*pt.Item1,a.y+pt.Item2,a);}
            bool normal=a.id==1&&a.state=="attack";
            if(normal&&a.combo==3&&t==-12)Visual("charge",a.x-a.face*15,a.y+132,a);
            if(t<0||t>=m.active)return;
            // Unity JSON may turn null arrays into empty arrays; both mean one hit window.
            bool hasPulses=m.pulses!=null&&m.pulses.Length>0;
            int pulse=hasPulses?Array.FindIndex(m.pulses,s=>t>=s&&t<s+4):0;
            if(pulse<0)return;
            bool execute=a.id==1&&a.state=="execute",plunge=a.id==1&&a.state=="plungeLand";
            bool spin=normal&&a.combo==2;
            // The target was in reach at startup; resolve the lock at its current position on contact.
            var area=execute?Hurtbox(boss):plunge?new Box(a.x+a.face*20-m.reach,a.y,m.reach*2,m.height):
                spin?new Box(a.x-m.reach,a.y,m.reach*2,m.height):new Box(a.face>0?a.x:a.x-m.reach,a.y,m.reach,m.height);
            var pose=a.id==2?BossAttackGeometry.Pose(m,a.face,a.x,a.y,pulse,t-(hasPulses?m.pulses[pulse]:0)):null;
            if(a.id==2){if(pose==null)return;area=pose.area;}
            hits.Add(new Hitbox{x=area.x,y=area.y,w=area.w,h=area.h,pose=pose,owner=a.id,attackId=a.attackId,pulse=pulse,move=m});
            if(t==(hasPulses?m.pulses[pulse]:0)){
                if(plunge){Visual("plunge_impact",a.x+a.face*20,a.y+10,a);Sound("plunge");return;}
                double x=normal?(a.combo==0?63:a.combo==1?88:a.combo==2?0:72):m.reach*.45;
                double y=normal?(a.combo==0?64:a.combo==1?63:a.combo==2?52:79):m.height*.5;
                Visual("slash",a.x+a.face*x,a.y+y,a).pulse=pulse;
                Sound(normal?PlayerSlashSounds[a.combo]:a.id==1?"counter":"boss_swing");
            }
        }
        void EmitDelayedFire(Fighter a,Move m,int t){
            if(t==0){
                fireAttack=a.attackId;
                fireX=Math.Max(boundsMin+48,Math.Min(boundsMax-48,a.x+a.face*m.reach*.73));
                Visual("fire_warning",fireX,0,a);
            }
            if(fireAttack!=a.attackId||t<m.fireDelay||t>=m.fireDelay+5)return;
            if(t==m.fireDelay){Visual("fire_burst",fireX,0,a);Sound("fire_burst");}
            hits.Add(new Hitbox{x=fireX-68,y=0,w=136,h=52,owner=2,attackId=a.attackId,pulse=-1,move=DelayedFire});
        }
        static Tuple<double,double> WeaponPoint(string n){switch(n){case "横斩":return Tuple.Create(35d,62d);case "双连斩":return Tuple.Create(30d,100d);case "突刺":return Tuple.Create(40d,65d);case "震地":return Tuple.Create(-15d,146d);case "跃击":return Tuple.Create(-10d,145d);case "赤月回旋":return Tuple.Create(-35d,75d);default:return Tuple.Create(35d,75d);}}
        void ControlPlayer(){var p=player;bool free=Free(p.state),recovering=(p.move!=null&&(p.state=="attack"||p.state=="finisher"||p.state=="execute")&&p.age>=p.move.wind+p.move.active)||(p.state=="parry"&&p.age>=Rules.parry);
            bool canChain=CanLinkCombo(p)&&p.age>=p.move.chainAt;
            recovering|=p.state=="plungeLand"&&canChain;
            bool canAct=free||recovering||p.state=="parry"||p.state=="dodge";
            bool canPlunge=CanPlunge;
            bool normalOnly=canPlunge||p.state=="plungeLand";
            if(!normalOnly&&CanFinish&&canAct&&ConsumeAttack(canChain)){double charge=p.energy+p.echo/100;p.energy=0;p.echo=0;executeWindow=0;p.face=boss.x>=p.x?1:-1;var m=Rules.Finisher;m.damage=Math.Floor(Rules.finisherBase+Rules.finisherPerEnergy*charge+.5);StartAttack(p,m,"finisher");Log("铃月终结 · 消耗全部回响");}
            else if(!normalOnly&&CanExecute&&canAct&&ConsumeAttack(canChain)){executeWindow=0;StartAttack(p,Rules.Execute,"execute");Log("回响处决");}
            else if(free||recovering){if(Consume("dodge")){if(Spend(p,45)){if(axis!=0)p.face=Math.Sign(axis);Enter(p,"dodge");}else Log("气力不足 · 无法闪避");}
                else if(free){if(Consume("parry")){if(Spend(p,20))Enter(p,"parry");else Log("气力不足 · 无法格挡");}
                    else if(Consume("attack")){p.combo=0;if(axis!=0)p.face=Math.Sign(axis);
                        if(canPlunge)Enter(p,"plunge");else StartAttack(p,Rules.Player[0]);}
                    else if(p.vy==0&&Consume("jump")){if(Spend(p,30)){p.vy=13;Enter(p,"air");Sound("jump");}else Log("气力不足 · 无法跳跃");}
                    else if(p.y==0&&p.vy==0&&Consume("heal")&&HealUnlocked&&p.hp<p.maxHp&&p.energy>=Rules.healCost){
                        if(axis!=0)p.face=Math.Sign(axis);Enter(p,"heal");}}
                else if(canChain&&ConsumeAttack(true)){p.combo=p.state=="plungeLand"?2:p.combo+1;if(axis!=0)p.face=Math.Sign(axis);StartAttack(p,Rules.Player[p.combo]);}}
            if(Free(p.state)){p.x+=axis*5;if(axis!=0)p.face=Math.Sign(axis);string next=p.y>0||p.vy>0?"air":axis!=0?"run":"idle";if(p.state!=next)Enter(p,next);}}
        void ControlBoss(){var b=boss;var p=player;if(!enemyActive||!ai||(b.state!="idle"&&b.state!="run"))return;
            if(UsesBossBehavior){ControlBossBehavior();return;}
            if(p.hp<=0){if(b.state=="run")Enter(b,b.y>0?"air":"idle");return;}b.face=p.x>=b.x?1:-1;
            if(opponent!=null){
                var moves=opponent.Moves(phase);
                if(moves.Length==0){if(b.state=="run")Enter(b,b.y>0?"air":"idle");return;}
                double gap=Math.Abs(p.x-b.x);
                if(aiWait>0)aiWait--;
                if(aiWait>0||gap>opponent.engage){
                    bool approach=gap>opponent.engage*.82;
                    if(approach)b.x+=b.face*opponent.pace*(phase==2?1.15:1);
                    string state=approach?"run":"idle";if(b.state!=state)Enter(b,state);return;
                }
                var move=moves[moveIndex++%moves.Length];
                StartAttack(b,move);
                aiWait=phase==2?opponent.rageWait:opponent.wait;
                Log(move.name+(move.parry?" · 可弹反":" · 危险招式"));return;
            }
            if(--aiWait>0){bool moving=Math.Abs(p.x-b.x)>215;if(moving)b.x+=b.face*(phase==2?3:2)*bossWalkScale;string next=moving?"run":"idle";if(b.state!=next)Enter(b,next);return;}
            double distance=Math.Abs(p.x-b.x);int choice=moveIndex++%6;if(distance>330&&choice<2)choice=2;if(choice==lastChoice)choice=(choice+1)%6;lastChoice=choice;var m=Rules.Boss[choice].Copy();
            if(phase==2){m.wind=(int)Math.Ceiling(m.wind*.75);m.recovery=(int)Math.Ceiling(m.recovery*.75);}StartAttack(b,m);aiWait=phase==2?aiWaitPhase2:aiWaitPhase1;Log(m.name+(m.parry?" · 可弹反":" · 危险招式"));}
        public void DebugBoss(int index){if(!enemyActive||boss.hp<=0)return;if(UsesBossBehavior){DebugBossBehavior(index);return;}var moves=opponent==null?Rules.Boss:opponent.Moves(phase);if(moves.Length==0)return;boss.face=player.x>=boss.x?1:-1;StartAttack(boss,moves[(index%moves.Length+moves.Length)%moves.Length].Copy());}
        void Resolve(Hitbox h,Fighter t){string key=h.attackId+":"+h.pulse+":"+t.id;if(!seen.Add(key))return;if(t.hp<=0||t.state=="phase"||t.invuln>0)return;
            var attacker=h.owner==1?player:boss;if(attacker.hp<=0||!h.released&&(attacker.attackId!=h.attackId||attacker.move==null))return;
            if(t.state=="dodge"&&t.age>=Rules.invulnStart&&t.age<Rules.invulnEnd){if(t.id==1){dodgeEvades++;if(!dodgeRewarded){dodgeRewarded=true;bool perfect=t.age<Rules.perfectDodgeEnd;GainEcho(t,perfect?Rules.echoPerfect:Rules.echoDodge);
                        if(perfect){t.stamina=PlayerMaxStamina;Visual("perfectDodge",t.x,t.y+42,t);Sound("perfect_dodge");Success("perfectDodge");executeWindow=30;Log("极限闪避 · 回响充满 · 气力回满");}else Log("闪避成功 · 回复回响");}}return;}
            if(t.id==1&&t.state=="parry"&&t.age<Rules.parry&&h.move.parry&&t.face*((h.released?h.sourceX:attacker.x)-t.x)>=0){bool perfect=t.age<Rules.parryPerfect;deflectSerial++;Visual("parry",t.x+t.face*18,t.y+48,t);Sound("parry");if(attacker.state!="phase")attacker.posture=Math.Min(BossPostureLimit,attacker.posture+32);GainEcho(t,perfect?Rules.echoPerfect:Rules.echoParry);
                if(perfect){t.stamina=PlayerMaxStamina;executeWindow=30;Success("perfectParry");Log("完美弹刀 · 气力回满");}else Log("弹刀成功");if(BossPostureReady)Log("Boss 架势已满 · 小处决可击溃");Enter(t,t.y>0?"air":"idle");return;}
            bool finisher=attacker.state=="finisher",small=attacker.state=="execute";
            bool postureReady=t.id==2&&t.posture>=BossPostureLimit&&t.state!="broken";
            bool heavySlash=attacker.id==1&&attacker.state=="attack"&&attacker.combo==3;
            double dealt=h.move.damage*(t.state=="broken"&&!finisher?1.5:1);
            if(small)attacker.energy-=Rules.executeCost;
            if(!(t.id==1&&(god||nonlethal)))t.hp=Math.Max(nonlethal?1:0,t.hp-dealt);
            if(attacker.id==1&&!finisher)GainEcho(attacker,dealt*.9);Visual("impact",t.x,t.y+Hurtbox(t).h*.55,attacker);Sound(t.id==1?"hit_player":"hit_boss");t.invuln=10;
            if(t.id==2)t.posture=finisher?0:Math.Min(BossPostureLimit,t.posture+h.move.posture);
            if(t.hp==0&&t.id==2&&bossYields){
                t.hp=1;BossDefeated=true;enemyActive=ai=false;Enter(t,"broken");
            }else if(t.hp==0)Enter(t,"dead");else if(t.id==2&&small&&t.state!="broken"){Enter(t,postureReady?"broken":"stagger");}else if(t.id==2&&(finisher||t.state!="broken"&&heavySlash&&!BossHyperArmor))Enter(t,"stagger");else if(t.id==1)Enter(t,"hurt");
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
        public void Advance(double dt,Func<bool> stopAfterTick=null){
            if(paused)return;
            accumulator+=Math.Min(Math.Max(dt,0),.25);
            while(accumulator+1e-9>=1d/60){
                accumulator-=1d/60;ObserveTempo();
                if(combat.hitstop>0){combat.Step();if(stopAfterTick?.Invoke()==true){ClearAccumulation();return;}continue;}
                double rate=Rate;if(cinematicTicks>0)cinematicTicks--;fraction+=rate;
                while(fraction+1e-9>=1){fraction--;combat.Step();ObserveTempo();if(stopAfterTick?.Invoke()==true){ClearAccumulation();return;}}
            }
        }
        public void Single(){combat.Step();ObserveTempo();}
    }
}
