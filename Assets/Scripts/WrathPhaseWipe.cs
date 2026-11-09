using System;
using UnityEngine;

namespace LingyueEchoes
{
    // Presentation-only interval inserted at the boundary between the two authored clips.
    public sealed class WrathPhaseWipe
    {
        public const int FrameCount=16;
        public const float Duration=1.6f;
        public const string ResourceRoot="WrathGeneral/phase2/wipe/wipe-";
        public bool Active {get;private set;}
        public bool Consumed {get;private set;}
        public float Elapsed {get;private set;}
        public int FrameIndex=>Mathf.Min(FrameCount-1,Mathf.FloorToInt(Elapsed/Duration*FrameCount));
        public void Reset(){Active=Consumed=false;Elapsed=0;}
        public bool Observe(Combat combat,bool eligible){
            if(!eligible||combat.phase<2){Reset();return false;}
            if(Consumed||combat.boss.state!="phase")return false;
            int frame=WrathGeneralArt.ReactionFrame(Math.Max(0,combat.boss.age-1)/60d,combat.BossTransitionFrames,WrathGeneralArt.PhaseTransitionFrameCount);
            if(frame<WrathGeneralArt.PhaseEngulfFrameCount)return false;
            Active=Consumed=true;Elapsed=0;return true;
        }
        public bool Advance(float unscaledDelta,bool suspended){
            if(!Active||suspended)return false;
            Elapsed=Mathf.Min(Duration,Elapsed+Mathf.Max(0,unscaledDelta));
            if(Elapsed<Duration)return false;
            Active=false;return true;
        }
    }
}
