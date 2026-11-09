using System;
using UnityEngine;

namespace LingyueEchoes
{
    // The first-chapter general owns these frames; no guardian/prologue art is reused.
    public sealed class WrathGeneralArt:IWrathPoseSource
    {
        public const string ResourceRoot="WrathGeneral/frames/";
        public const string Phase2ResourceRoot="WrathGeneral/phase2/frames/";
        public const int FrameCount=16;
        public const int PhaseEngulfFrameCount=9,PhaseRiseFrameCount=7;
        public const int StunLoopStart=15,StunEntryFps=24,StunHoldFps=8;
        // Shared import calibration, measured from the bottom-left of each square canvas.
        public const int CanvasPixels=384;
        public const float RootX=230f/384,FootY=37f/384,PixelsPerUnit=180f;
        readonly Sprite[] idle=Load("idle"),walk=Load("walk"),stun=Load("stun"),hurt=Load("hurt");
        Sprite[] phase2Idle,phase2Walk,phase2Stun,phase2Hurt,phaseEngulf,phaseRise;
        public int CurrentPhase {get;set;}=1;
        public int PhaseTransitionFrames {get;set;}=BossBehaviorCatalog.WrathGeneral.phases[1].transitionFrames;
        // The owning combat view supplies its active definition; standalone previews use the catalog.
        public int BossStaggerFrames {get;set;}=BossBehaviorCatalog.WrathGeneral.staggerFrames;
        public bool CanRecoverFromBreak {get;set;}=true;

        static Sprite[] Load(string action,string root=ResourceRoot,int count=FrameCount){
            var frames=new Sprite[count];
            for(int i=0;i<frames.Length;i++){
                string path=root+action+"/"+action+"-"+(i+1).ToString("00");
                frames[i]=Resources.Load<Sprite>(path);
                if(!frames[i])throw new InvalidOperationException("Missing Wrath General animation frame: Resources/"+path+".png");
            }
            return frames;
        }

        public Sprite Pose(Fighter fighter,string scripted,int cell,double? seconds){
            double elapsed=Math.Max(0,seconds??Math.Max(0,fighter.age-1)/60d);
            string state=scripted??fighter.state;
            if(state=="phase"||state=="phase-engulf"||state=="phase-rise"){
                if(phaseEngulf==null)phaseEngulf=Load("phase-engulf",Phase2ResourceRoot,PhaseEngulfFrameCount);
                if(phaseRise==null)phaseRise=Load("phase-rise",Phase2ResourceRoot,PhaseRiseFrameCount);
                // Combat keeps its original sixteen-frame clock across the two clips.
                if(state=="phase"){
                    int globalFrame=ReactionFrame(elapsed,PhaseTransitionFrames);
                    return globalFrame<PhaseEngulfFrameCount?phaseEngulf[globalFrame]:phaseRise[globalFrame-PhaseEngulfFrameCount];
                }
                var clip=state=="phase-engulf"?phaseEngulf:phaseRise;
                // An explicit clip preview uses the configured transition duration for that clip.
                int clipFrame=scripted!=null&&!seconds.HasValue?Mathf.Clamp(cell*(clip.Length-1)/7,0,clip.Length-1):
                    ReactionFrame(elapsed,PhaseTransitionFrames,clip.Length);
                return clip[clipFrame];
            }
            Sprite[] idle=this.idle,walk=this.walk,stun=this.stun,hurt=this.hurt;
            if(CurrentPhase>=2){
                if(phase2Idle==null)phase2Idle=Load("idle",Phase2ResourceRoot);
                if(phase2Walk==null)phase2Walk=Load("walk",Phase2ResourceRoot);
                if(phase2Stun==null)phase2Stun=Load("stun",Phase2ResourceRoot);
                if(phase2Hurt==null)phase2Hurt=Load("hurt",Phase2ResourceRoot);
                idle=phase2Idle;walk=phase2Walk;stun=phase2Stun;hurt=phase2Hurt;
            }
            bool kneeling=state=="broken"||state=="stun"||state=="dead"||fighter.hp<=0;
            if(kneeling){
                // Scripted defeat and death stay down. Only a timed combat break rises again.
                bool recover=scripted==null&&state=="broken"&&fighter.hp>0&&CanRecoverFromBreak;
                if(scripted!=null&&!seconds.HasValue)return stun[Mathf.Clamp(cell*StunLoopStart/7,0,StunLoopStart)];
                return stun[BreakFrame(elapsed,BossStaggerFrames,recover)];
            }
            if(state=="hurt"||state=="stagger"){
                int duration=state=="hurt"?Rules.hurtFrames:Rules.staggerFrames;
                if(scripted!=null&&!seconds.HasValue)return hurt[Mathf.Clamp(cell*15/7,0,15)];
                // Each reaction contains the flinch and return, once, within its existing combat state.
                return hurt[ReactionFrame(elapsed,duration)];
            }
            // Unauthored combat moves keep this same character's ready pose.
            if(scripted==null&&fighter.move!=null)return idle[0];
            var frames=state=="run"||state=="walk"?walk:idle;
            int index=scripted!=null&&!seconds.HasValue?Mathf.Clamp(cell*15/7,0,15):
                (int)Math.Floor(elapsed*(frames==walk?16:8)+1e-7);
            return frames[index%FrameCount];
        }

        public static int ReactionFrame(double seconds,int durationFrames,int count=FrameCount){
            // age reaches duration before the next render, so age=duration-1 is the last visible tick.
            double lastTick=Math.Max(1,durationFrames-2);
            return Math.Min(count-1,(int)Math.Floor(Math.Max(0,seconds)*60*(count-1)/lastTick+1e-7));
        }

        public static int BreakFrame(double seconds,int durationFrames,bool recover){
            double elapsed=Math.Max(0,seconds);
            double entrySeconds=(double)StunLoopStart/StunEntryFps;
            if(!recover)return Math.Min(StunLoopStart,(int)Math.Floor(elapsed*StunEntryFps+1e-7));
            double endSeconds=Math.Max(0,durationFrames-2)/60d;
            // Very short configured breaks still retain entry, hold, and recovery in that order.
            double motionSeconds=Math.Min(entrySeconds,endSeconds/3);
            if(motionSeconds<=0)return 0;
            if(elapsed<motionSeconds)return Math.Min(StunLoopStart,(int)Math.Floor(elapsed/motionSeconds*StunLoopStart+1e-7));
            double riseStart=endSeconds-motionSeconds;
            if(elapsed<riseStart)return StunLoopStart;
            return Math.Max(0,StunLoopStart-(int)Math.Floor((elapsed-riseStart)/motionSeconds*StunLoopStart+1e-7));
        }
    }
}
