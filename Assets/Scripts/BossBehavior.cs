using System;
using System.Collections.Generic;

namespace LingyueEchoes
{
    [Flags] public enum BossMoveTag { Melee=1,Ranged=2,Gap=4,Grab=8,Aoe=16,Punish=32,Guardbreak=64,Delayed=128 }
    [Serializable] public sealed class BossMoveDefinition
    {
        public string id; public Move move;
        public double minRange,maxRange,maxHeight=220,maxAngle=180,baseWeight=10;
        public int minPhase=1,cooldown=90,budgetCost=1,trackingFrames,armorFrom=-1,armorTo=-1;
        public string[] followUps; public int tags; public string kind="melee";
        public double projectileSpeed=7,projectileRadius=14; public int projectileLife=120;
        public BossMoveDefinition Copy(){var c=(BossMoveDefinition)MemberwiseClone();c.move=CopyMove(move);c.followUps=followUps==null?null:(string[])followUps.Clone();return c;}
        internal static Move CopyMove(Move m){if(m==null)return null;var c=m.Copy();c.pulses=m.pulses==null?null:(int[])m.pulses.Clone();return c;}
    }
    [Serializable] public sealed class BossPhaseDefinition
    {
        public double threshold=1; public int minBudget=1,maxBudget=2,maxActions=2,breatherMin=60,breatherMax=90,repositionMin=18,repositionMax=36,transitionFrames=90;
        public double approachSpeed=2,preferredRange=170;
        public BossPhaseDefinition Copy()=>(BossPhaseDefinition)MemberwiseClone();
    }
    [Serializable] public sealed class BossDefinition
    {
        public string id; public int seed=1,reactionFrames=12,initialWait=45;
        public double postureLimit=100,postureGrowth=10; public int staggerFrames=150;
        public BossMoveDefinition[] moves; public BossPhaseDefinition[] phases;
        static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
        static void Require(bool ok,string message){if(!ok)throw new ArgumentException("Boss definition: "+message);}
        public void Validate(){
            Require(!string.IsNullOrWhiteSpace(id),"missing id");
            Require(reactionFrames>=0&&reactionFrames<=600&&initialWait>=0&&staggerFrames>0,"invalid frame settings");
            Require(Finite(postureLimit)&&postureLimit>0&&Finite(postureGrowth)&&postureGrowth>=0,"invalid posture");
            Require(moves!=null&&moves.Length>0&&phases!=null&&phases.Length>0,"missing moves or phases");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var d in moves){
                Require(d!=null&&!string.IsNullOrWhiteSpace(d.id)&&ids.Add(d.id),"missing or duplicate move id");
                var m=d.move;Require(m!=null,"missing move");
                Require(m.wind>=18&&m.active>0&&m.recovery>0&&(long)m.wind+m.active+m.recovery<=int.MaxValue,"invalid action timing: at least 18 telegraph frames and positive recovery required");
                Require(d.cooldown>=0&&d.budgetCost>0&&d.minPhase>=1&&d.minPhase<=phases.Length&&d.trackingFrames>=0&&d.trackingFrames<=m.wind,"invalid move limits");
                Require(d.kind=="melee"||d.kind=="projectile","unknown action kind");
                Require(d.kind!="projectile"||d.projectileSpeed>0,"projectile speed must be positive");
                Require(Finite(d.minRange)&&Finite(d.maxRange)&&d.minRange>=0&&d.maxRange>=d.minRange&&Finite(d.maxHeight)&&d.maxHeight>=0&&Finite(d.maxAngle)&&d.maxAngle>=0&&d.maxAngle<=180&&Finite(d.baseWeight)&&d.baseWeight>=0,"invalid selection range or weight");
                Require(Finite(m.reach)&&m.reach>=0&&Finite(m.height)&&m.height>=0&&Finite(m.damage)&&m.damage>=0&&Finite(m.posture)&&m.posture>=0&&Finite(m.speed),"invalid move values");
                Require(m.fireDelay>=0&&m.chainAt>=0,"invalid delayed timing");
                Require(m.fireDelay==0||(long)m.fireDelay+5<=(long)m.active+m.recovery,"delayed hit must finish within its owning action");
                Require((d.armorFrom==-1&&d.armorTo==-1)||(d.armorFrom>=0&&d.armorTo>d.armorFrom&&d.armorTo<=(long)m.wind+m.active+m.recovery),"invalid armor window");
                Require(Finite(d.projectileSpeed)&&d.projectileSpeed>=0&&Finite(d.projectileRadius)&&d.projectileRadius>0&&d.projectileLife>0,"invalid projectile");
                if(m.pulses!=null){int previous=-1;foreach(int pulse in m.pulses){Require(pulse>=0&&pulse<m.active&&pulse>previous,"invalid pulse timing");previous=pulse;}}
            }
            foreach(var d in moves)if(d.followUps!=null)foreach(var next in d.followUps)Require(next!=null&&ids.Contains(next),"unknown follow-up");
            double last=double.PositiveInfinity;
            foreach(var p in phases){Require(p!=null,"missing phase");Require(Finite(p.threshold)&&p.threshold>0&&p.threshold<=1&&p.threshold<last,"thresholds must descend");last=p.threshold;
                Require(p.minBudget>0&&p.maxBudget>=p.minBudget&&p.maxActions>0&&p.breatherMin>=0&&p.breatherMax>=p.breatherMin&&p.repositionMin>=0&&p.repositionMax>=p.repositionMin&&p.transitionFrames>=0,"invalid phase timing or budget");
                Require(Finite(p.approachSpeed)&&p.approachSpeed>=0&&Finite(p.preferredRange)&&p.preferredRange>=0,"invalid phase movement");}
            Require(phases[0].threshold==1,"first phase threshold must be one");
            // Every edge consumes a positive budget; maxActions adds a second finite bound even for cycles.
        }
        public BossDefinition Copy(){var c=(BossDefinition)MemberwiseClone();if(moves!=null){c.moves=new BossMoveDefinition[moves.Length];for(int i=0;i<moves.Length;i++)c.moves[i]=moves[i]?.Copy();}if(phases!=null){c.phases=new BossPhaseDefinition[phases.Length];for(int i=0;i<phases.Length;i++)c.phases[i]=phases[i]?.Copy();}return c;}
    }
    public struct BossSnapshot
    {
        public int frame;public double playerX,playerY,bossX,bossY;public int bossFace;public string playerState;
        public double Distance=>Math.Abs(playerX-bossX);
        public bool Healing=>playerState=="heal";
        public bool Rolling=>playerState=="dodge"||playerState=="roll";
        public bool Charging=>playerState=="charge"||playerState=="charging";
    }
    public sealed class BossPlayerProfile
    {
        public double Rolls {get;private set;}public double Heals {get;private set;}public double Charges {get;private set;}
        string previous;int lastFrame;bool initialized;
        internal void Observe(BossSnapshot s){int elapsed=initialized?Math.Max(0,s.frame-lastFrame):0;double decay=Math.Pow(.5,elapsed/300.0);Rolls*=decay;Heals*=decay;Charges*=decay;
            if(s.playerState!=previous){if(s.Rolling)Rolls=Math.Min(3,Rolls+1);if(s.Healing)Heals=Math.Min(3,Heals+1);if(s.Charging)Charges=Math.Min(3,Charges+1);}previous=s.playerState;lastFrame=s.frame;initialized=true;}
        public double CounterBias(BossMoveDefinition d){double bias=1;var t=(BossMoveTag)d.tags;if((t&(BossMoveTag.Delayed|BossMoveTag.Grab))!=0)bias+=Rolls*.3;if((t&(BossMoveTag.Punish|BossMoveTag.Gap|BossMoveTag.Ranged))!=0)bias+=Heals*.4;if((t&(BossMoveTag.Punish|BossMoveTag.Guardbreak))!=0)bias+=Charges*.3;return Math.Min(3,bias);}
        internal void Reset(){Rolls=Heals=Charges=0;previous=null;lastFrame=0;initialized=false;}
    }
    public sealed class BossPerception
    {
        readonly int delay;readonly Queue<BossSnapshot> history=new Queue<BossSnapshot>();BossSnapshot stable;bool initialized;int lastObserved=int.MinValue;
        public BossPlayerProfile Profile {get;}=new BossPlayerProfile();
        public BossPerception(int reactionFrames){delay=Math.Max(0,Math.Min(600,reactionFrames));}
        public BossSnapshot Observe(int frame,Fighter player,Fighter boss){
            if(initialized&&frame<lastObserved)Reset();
            var raw=new BossSnapshot{frame=frame,playerX=player.x,playerY=player.y,bossX=boss.x,bossY=boss.y,bossFace=boss.face,playerState=player.state};
            if(player.state=="attack"&&player.move!=null&&(player.move.name=="蓄力下斩"||player.move.pose=="charge"))raw.playerState="charge";
            if(!initialized){stable=raw;initialized=true;}
            if(frame!=lastObserved){history.Enqueue(raw);lastObserved=frame;}
            while(history.Count>0&&history.Peek().frame<=(long)frame-delay)stable=history.Dequeue();
            var result=stable;result.bossX=boss.x;result.bossY=boss.y;result.bossFace=boss.face;
            Profile.Observe(result);return result;
        }
        public void Reset(){history.Clear();stable=default(BossSnapshot);initialized=false;lastObserved=int.MinValue;Profile.Reset();}
    }
    public sealed class BossBrain
    {
        readonly BossDefinition definition;readonly Dictionary<string,int> committed=new Dictionary<string,int>(StringComparer.Ordinal);string last,previous;uint state;
        public BossBrain(BossDefinition definition,int seed){if(definition==null)throw new ArgumentNullException(nameof(definition));definition.Validate();this.definition=definition.Copy();Reset(seed);}
        uint Next(){uint x=state;x^=x<<13;x^=x>>17;x^=x<<5;return state=x;}
        public int Range(int minInclusive,int maxInclusive){if(maxInclusive<minInclusive)throw new ArgumentOutOfRangeException(nameof(maxInclusive));ulong count=(ulong)((long)maxInclusive-minInclusive+1);return (int)(minInclusive+(long)(Next()%count));}
        public void Reset(int seed){state=unchecked((uint)seed);if(state==0)state=0x9e3779b9;committed.Clear();last=previous=null;}
        public void Commit(BossMoveDefinition move,int frame){if(move==null)throw new ArgumentNullException(nameof(move));committed[move.id]=frame;previous=last;last=move.id;}
        public BossMoveDefinition Pick(int phase,BossSnapshot snapshot,int frame,BossPlayerProfile profile,string[] allowed=null,int budget=int.MaxValue){
            var candidates=new List<BossMoveDefinition>();var weights=new List<double>();double total=0,maxBase=0;foreach(var entry in definition.moves)maxBase=Math.Max(maxBase,entry.baseWeight);
            foreach(var d in definition.moves){if(allowed!=null&&Array.IndexOf(allowed,d.id)<0)continue;if(d.minPhase>phase||d.budgetCost>budget||snapshot.Distance<d.minRange||snapshot.Distance>d.maxRange||Math.Abs(snapshot.playerY-snapshot.bossY)>d.maxHeight)continue;
                double dx=snapshot.playerX-snapshot.bossX,dy=snapshot.playerY-snapshot.bossY;double angle=Math.Atan2(Math.Abs(dy),dx*(snapshot.bossFace<0?-1:1))*180/Math.PI;if(angle>d.maxAngle)continue;
                int start;if(committed.TryGetValue(d.id,out start)&&(long)frame-start<d.cooldown)continue;
                double context=profile?.CounterBias(d)??1;var tags=(BossMoveTag)d.tags;
                if(snapshot.Healing&&(tags&(BossMoveTag.Punish|BossMoveTag.Gap|BossMoveTag.Ranged))!=0)context*=1.6;
                if(snapshot.Rolling&&(tags&(BossMoveTag.Delayed|BossMoveTag.Grab))!=0)context*=1.5;
                if(snapshot.Charging&&(tags&(BossMoveTag.Punish|BossMoveTag.Guardbreak))!=0)context*=1.5;
                double weight=(maxBase>0?d.baseWeight/maxBase:0)*Math.Min(3,context)*(d.id==last ? .2 : d.id==previous ? .5 : 1);if(weight<=0)continue;
                candidates.Add(d);weights.Add(weight);total+=weight;
            }
            if(candidates.Count==0)return null;
            // Normalize before summation so arbitrarily large finite authored weights cannot overflow.
            double largest=0;foreach(double w in weights)largest=Math.Max(largest,w);total=0;for(int i=0;i<weights.Count;i++){weights[i]/=largest;total+=weights[i];}
            double choice=(Next()/(uint.MaxValue+1.0))*total;
            for(int i=0;i<candidates.Count;i++){choice-=weights[i];if(choice<0)return candidates[i].Copy();}return candidates[candidates.Count-1].Copy();
        }
    }
    public enum BossActionStage { Idle,Telegraph,Active,Recovery }
    public enum BossInterrupt { Death,PostureBreak,HeavyHit,Execution,PhaseTransition,Reset,Disabled }
    public sealed class BossActionRunner
    {
        int wind,active,recovery,tracking,armorFrom,armorTo;
        public BossMoveDefinition Current {get;private set;}public Move Move=>Current?.move;public int Age {get;private set;}public int Face {get;private set;}
        public double TargetX {get;private set;}public double TargetY {get;private set;}public bool Running {get;private set;}public BossInterrupt LastInterrupt {get;private set;}
        public BossActionStage Stage=>!Running?BossActionStage.Idle:Age<wind?BossActionStage.Telegraph:Age<(long)wind+active?BossActionStage.Active:BossActionStage.Recovery;
        public bool HyperArmor=>Running&&armorFrom>=0&&Age>=armorFrom&&Age<armorTo;
        public void Start(BossMoveDefinition move,BossSnapshot snapshot,int face){if(Running)throw new InvalidOperationException("Interrupt or finish the current action before starting another.");if(move?.move==null)throw new ArgumentNullException(nameof(move));Current=move.Copy();wind=Move.wind;active=Move.active;recovery=Move.recovery;tracking=Current.trackingFrames;armorFrom=Current.armorFrom;armorTo=Current.armorTo;Age=0;Face=face<0?-1:1;TargetX=snapshot.playerX;TargetY=snapshot.playerY;Running=true;}
        public void Track(BossSnapshot snapshot){if(!Running||Age>=tracking||Age>=wind)return;TargetX=snapshot.playerX;TargetY=snapshot.playerY;if(TargetX!=snapshot.bossX)Face=TargetX>snapshot.bossX?1:-1;}
        public bool Advance(){if(!Running)return false;Age++;if(Age<(long)wind+active+recovery)return false;Running=false;return true;}
        public void Interrupt(BossInterrupt reason){Running=false;LastInterrupt=reason;Current=null;Age=0;}
    }
    public sealed class BossPhaseManager
    {
        readonly BossDefinition definition;public int Pending {get;private set;}=1;
        public BossPhaseManager(BossDefinition definition){if(definition==null)throw new ArgumentNullException(nameof(definition));definition.Validate();this.definition=definition.Copy();}
        public int Observe(double hpRatio,int currentPhase){Pending=Math.Max(Pending,currentPhase);for(int i=1;i<definition.phases.Length;i++)if(hpRatio<=definition.phases[i].threshold)Pending=Math.Max(Pending,i+1);return Pending;}
        public bool CanTransition(BossActionRunner runner,string bossState,string playerState)=>!(runner?.Running??false)&&bossState!="broken"&&bossState!="stagger"&&bossState!="finisher"&&bossState!="execute"&&playerState!="finisher"&&playerState!="execute";
        public void Reset(){Pending=1;}
    }
}


