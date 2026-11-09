using System;
using System.Collections.Generic;
using System.Linq;

namespace LingyueEchoes
{
    public sealed class BossProjectile
    {
        public int id,attackId,age,life;public double x,y,vx,vy,radius;public Move move;
    }

    // Adapter between the pure behavior layers and the existing fixed-step combat simulation.
    public partial class Combat
    {
        BossDefinition defaultBoss,bossDefinition;
        BossPerception perception;BossBrain brain;BossPhaseManager phases;
        readonly BossActionRunner actionRunner=new BossActionRunner();
        readonly List<BossProjectile> projectiles=new List<BossProjectile>();
        BossSnapshot sensed;BossMoveDefinition completed;
        int bossRest,bossBudget,bossActions,repositionLeft,repositionDirection,breaks,projectileSerial;
        double repositionSpeed;
        public bool UsesBossBehavior=>bossDefinition!=null;
        public BossActionRunner BossAction=>actionRunner;
        public IReadOnlyList<BossProjectile> BossProjectiles=>projectiles;
        public double BossPostureLimit=>bossDefinition==null?100:bossDefinition.postureLimit+breaks*bossDefinition.postureGrowth;
        public bool BossHyperArmor=>UsesBossBehavior&&actionRunner.HyperArmor;
        public int BossPendingPhase=>phases?.Pending??phase;
        public string BossBehaviorState=>!UsesBossBehavior?"Unmanaged":boss.state=="broken"?(boss.age<18?"Stagger":"Vulnerable"):
            boss.state=="phase"?"PhaseTransition":actionRunner.Running?actionRunner.Stage.ToString():bossRest>0?"Breather":repositionLeft>0?"Reposition":"Decide";
        public int BossStaggerFrames=>bossDefinition?.staggerFrames??150;
        public int BossTransitionFrames=>UsesBossBehavior?PhaseSettings.transitionFrames:opponent?.phaseFrames??90;
        BossPhaseDefinition PhaseSettings=>bossDefinition.phases[Math.Max(0,Math.Min(bossDefinition.phases.Length-1,phase-1))];

        public void ConfigureDefaultBoss(BossDefinition definition)
        {
            definition.Validate();defaultBoss=definition.Copy();
            if(opponent==null)ResetBossBehavior(null,1);
        }
        // Existing Move-based bosses also enter the same pipeline. Ordinary enemies explicitly opt out.
        static BossDefinition AdaptBoss(OpponentPattern pattern)
        {
            var opening=pattern?.opening??Rules.Boss;
            var rage=pattern?.rage;
            var definitions=new List<BossMoveDefinition>();
            foreach(var move in opening.Concat(rage??Array.Empty<Move>())){
                if(definitions.Any(d=>ReferenceEquals(d.move,move)))continue;
                definitions.Add(new BossMoveDefinition{id="move-"+definitions.Count,move=move,
                    minPhase=Array.IndexOf(opening,move)>=0?1:2,maxRange=move.reach+Math.Abs(move.speed)*move.active+54,
                    maxHeight=Math.Max(146,move.height),cooldown=move.wind+move.active+move.recovery+30,
                    tags=(int)(Math.Abs(move.speed)>0?BossMoveTag.Gap:BossMoveTag.Melee),followUps=Array.Empty<string>()});
            }
            if(definitions.Count==0)return null;
            var first=new BossPhaseDefinition{threshold=1,approachSpeed=pattern?.pace??2,preferredRange=pattern?.engage??180,
                transitionFrames=pattern?.phaseFrames??90};
            var second=first.Copy();second.threshold=.5;second.approachSpeed*=1.5;second.breatherMin=42;second.breatherMax=66;
            var definition=new BossDefinition{id="move-based-boss",initialWait=pattern?.wait??90,moves=definitions.ToArray(),
                phases=pattern==null||rage!=null?new[]{first,second}:new[]{first}};
            definition.Validate();return definition;
        }
        void ResetBossBehavior(OpponentPattern pattern,int seed)
        {
            actionRunner.Interrupt(BossInterrupt.Reset);projectiles.Clear();completed=null;
            bossBudget=bossActions=repositionLeft=breaks=projectileSerial=0;
            bossDefinition=pattern!=null&&!pattern.isBoss?null:(pattern?.behavior??(pattern==null?defaultBoss:null)??AdaptBoss(pattern))?.Copy();
            if(bossDefinition==null){perception=null;brain=null;phases=null;bossRest=0;return;}
            bossDefinition.Validate();perception=new BossPerception(bossDefinition.reactionFrames);
            brain=new BossBrain(bossDefinition,unchecked(seed^bossDefinition.seed));phases=new BossPhaseManager(bossDefinition);
            sensed=perception.Observe(frame,player,boss);bossRest=bossDefinition.initialWait;
        }
        public void InterruptBoss(BossInterrupt reason)
        {
            if(!UsesBossBehavior)return;
            CancelBossAction(reason);
            string state=reason==BossInterrupt.Death?"dead":reason==BossInterrupt.PostureBreak?"broken":
                reason==BossInterrupt.HeavyHit||reason==BossInterrupt.Execution?"stagger":reason==BossInterrupt.PhaseTransition?"phase":"idle";
            if(boss.state!=state)Enter(boss,state);
        }
        void CancelBossAction(BossInterrupt reason)
        {
            if(!UsesBossBehavior)return;
            actionRunner.Interrupt(reason);boss.move=null;completed=null;bossBudget=bossActions=repositionLeft=0;
            bossRest=PhaseSettings.breatherMin;
            if(reason==BossInterrupt.Reset||reason==BossInterrupt.Disabled||reason==BossInterrupt.Death)projectiles.Clear();
        }
        public void BreakBossPosture(){boss.posture=BossPostureLimit;Enter(boss,"broken");}
        void OnBossStateChanged(string next)
        {
            if(!UsesBossBehavior)return;
            if(boss.state=="broken"&&next!="broken"){breaks++;boss.posture=0;}
            bool interruption=next=="dead"||next=="broken"||next=="stagger"||next=="phase";
            if(!actionRunner.Running&&!interruption)return;
            BossInterrupt reason=next=="dead"?BossInterrupt.Death:next=="broken"?BossInterrupt.PostureBreak:
                next=="phase"?BossInterrupt.PhaseTransition:next=="stagger"?
                    (player.state=="execute"||player.state=="finisher"?BossInterrupt.Execution:BossInterrupt.HeavyHit):BossInterrupt.Disabled;
            CancelBossAction(reason);
        }
        void BeginBossStep()
        {
            if(!UsesBossBehavior)return;
            if(!enemyActive){if(actionRunner.Running||projectiles.Count>0)InterruptBoss(BossInterrupt.Disabled);return;}
            sensed=perception.Observe(frame,player,boss);
            if(actionRunner.Running){
                if(boss.move!=actionRunner.Move||boss.state!="attack")CancelBossAction(boss.state=="broken"?BossInterrupt.PostureBreak:BossInterrupt.Disabled);
                else{actionRunner.Track(sensed);boss.face=actionRunner.Face;boss.age=actionRunner.Age;}
            }
            if(!nonlethal&&player.hp>0)phases.Observe(boss.hp/Math.Max(1,boss.maxHp),phase);
            if(phases.Pending>phase&&boss.move==null&&boss.state!="phase"&&phases.CanTransition(actionRunner,boss.state,player.state)){
                phase=phases.Pending;completed=null;bossBudget=bossActions=repositionLeft=0;bossRest=PhaseSettings.breatherMin;
                Enter(boss,"phase");Log("阶段 "+phase+" · 转阶段");
            }
        }
        void StartBossAction(BossMoveDefinition definition)
        {
            if(actionRunner.Running)throw new InvalidOperationException("Cannot replace a committed boss action");
            int face=sensed.playerX>=boss.x?1:-1;
            StartAttack(boss,BossMoveDefinition.CopyMove(definition.move));
            actionRunner.Start(definition,sensed,face);boss.move=actionRunner.Move;boss.face=actionRunner.Face;
            brain.Commit(definition,frame);bossBudget-=definition.budgetCost;bossActions++;moveIndex++;
            completed=null;repositionLeft=0;
            Visual("charge",boss.x+boss.face*35,boss.y+75,boss);Sound("boss_swing");
            Log(definition.move.name+(definition.move.parry?" · 可弹反":" · 危险招式"));
        }
        bool AdvanceBossAction()
        {
            if(!UsesBossBehavior||!actionRunner.Running)return false;
            bool ended=actionRunner.Advance();boss.age=actionRunner.Age;
            if(ended){completed=actionRunner.Current.Copy();Enter(boss,boss.y>0?"air":"idle");}
            return true;
        }
        void StartBreather()
        {
            completed=null;bossBudget=bossActions=repositionLeft=0;
            bossRest=brain.Range(PhaseSettings.breatherMin,PhaseSettings.breatherMax);
            if(boss.state=="run")Enter(boss,"idle");
        }
        void ControlBossBehavior()
        {
            if(player.hp<=0||actionRunner.Running||boss.move!=null)return;
            if(bossRest>0){bossRest--;if(boss.state=="run")Enter(boss,"idle");return;}
            if(completed!=null){
                var next=bossActions<PhaseSettings.maxActions&&bossBudget>0&&phases.Pending<=phase?
                    brain.Pick(phase,sensed,frame,perception.Profile,completed.followUps??Array.Empty<string>(),bossBudget):null;
                if(next!=null){StartBossAction(next);return;}
                StartBreather();return;
            }
            if(repositionLeft>0){
                repositionLeft--;boss.face=sensed.playerX>=boss.x?1:-1;
                boss.x+=repositionDirection*repositionSpeed;
                if(boss.state!="run")Enter(boss,"run");return;
            }
            if(bossBudget<=0){bossBudget=brain.Range(PhaseSettings.minBudget,PhaseSettings.maxBudget);bossActions=0;}
            var chosen=brain.Pick(phase,sensed,frame,perception.Profile,null,bossBudget);
            if(chosen!=null){StartBossAction(chosen);return;}
            // Commit one short positioning segment; never oscillate direction every rendered frame.
            int toward=sensed.playerX>=boss.x?1:-1;
            repositionDirection=(sensed.Distance>PhaseSettings.preferredRange?toward:-toward);
            if((boss.x<=boundsMin+20&&repositionDirection<0)||(boss.x>=boundsMax-20&&repositionDirection>0))repositionDirection=-repositionDirection;
            repositionSpeed=PhaseSettings.approachSpeed*brain.Range(85,115)/100;
            repositionLeft=brain.Range(PhaseSettings.repositionMin,PhaseSettings.repositionMax);
        }
        void DebugBossBehavior(int index)
        {
            if(actionRunner.Running||boss.move!=null||!Free(boss.state))return;
            var eligible=bossDefinition.moves.Where(m=>m.minPhase<=phase).ToArray();if(eligible.Length==0)return;
            var move=eligible[(index%eligible.Length+eligible.Length)%eligible.Length];
            // Explicit debug commands target the inspected position, outside gameplay perception.
            sensed=new BossSnapshot{frame=frame,playerX=player.x,playerY=player.y,bossX=boss.x,bossY=boss.y,bossFace=boss.face,playerState=player.state};bossBudget=move.budgetCost;bossActions=0;bossRest=0;
            StartBossAction(move);
        }
        bool TryEmitBossProjectile()
        {
            if(!UsesBossBehavior||!actionRunner.Running||actionRunner.Current.kind!="projectile")return false;
            if(boss.age==boss.move.wind){
                var d=actionRunner.Current;double x=boss.x+boss.face*38,y=boss.y+76;
                double dx=actionRunner.TargetX-x,dy=actionRunner.TargetY+58-y,length=Math.Max(1,Math.Sqrt(dx*dx+dy*dy));
                projectiles.Add(new BossProjectile{id=++projectileSerial,attackId=boss.attackId,x=x,y=y,
                    vx=dx/length*d.projectileSpeed,vy=dy/length*d.projectileSpeed,radius=d.projectileRadius,life=d.projectileLife,move=BossMoveDefinition.CopyMove(boss.move)});
                Sound("boss_swing");
            }
            return true;
        }
        void AdvanceBossProjectiles()
        {
            if(!enemyActive||boss.hp<=0){projectiles.Clear();return;}
            for(int i=projectiles.Count-1;i>=0;i--){
                var p=projectiles[i];double source=p.x;p.x+=p.vx;p.y+=p.vy;p.age++;
                var hit=new Hitbox{x=p.x-p.radius,y=p.y-p.radius,w=p.radius*2,h=p.radius*2,owner=2,attackId=p.attackId,pulse=-2,
                    move=p.move,released=true,sourceX=source};
                if(p.age>p.life||p.x<boundsMin-100||p.x>boundsMax+100||p.y<0||terrain.Any(s=>Box.Overlaps(hit,s))){projectiles.RemoveAt(i);continue;}
                hits.Add(hit);
                if(Box.Overlaps(hit,Hurtbox(player)))projectiles.RemoveAt(i);
            }
        }
    }
}
