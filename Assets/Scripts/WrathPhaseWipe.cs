using System;
using UnityEngine;

namespace LingyueEchoes
{
    // Presentation-only interval inserted at the boundary between the two authored clips.
    public sealed class WrathPhaseWipe
    {
        public const int FrameCount=16;
        public const float Duration=1.6f,HoldDuration=1f,TotalDuration=HoldDuration*2+Duration;
        public const string ResourceRoot="WrathGeneral/phase2/wipe/wipe-";
        public bool Active {get;private set;}
        public bool Consumed {get;private set;}
        public float Timeline {get;private set;}
        float carried;
        public float Elapsed=>Mathf.Clamp(Timeline-HoldDuration,0,Duration);
        public bool BeforeHold=>Active&&Timeline<HoldDuration;
        public bool WipeActive=>Active&&Timeline>=HoldDuration&&Timeline<HoldDuration+Duration;
        public bool AfterHold=>Active&&Timeline>=HoldDuration+Duration;
        public int HoldSpriteIndex=>((int)(Mathf.Max(0,AfterHold?Timeline-HoldDuration-Duration:Timeline)*6)%2)+(AfterHold?0:14);
        public int FrameIndex=>Mathf.Min(FrameCount-1,Mathf.FloorToInt(Elapsed/Duration*FrameCount+.00001f));
        public void Reset(){Active=Consumed=false;Timeline=carried=0;}
        public bool Observe(Combat combat,bool eligible){
            if(!eligible||combat.phase<2){Reset();return false;}
            if(Consumed||combat.boss.state!="phase")return false;
            int frame=WrathGeneralArt.ReactionFrame(Math.Max(0,combat.boss.age-1)/60d,combat.BossTransitionFrames,WrathGeneralArt.PhaseTransitionFrameCount);
            if(frame<WrathGeneralArt.PhaseEngulfFrameCount)return false;
            Active=Consumed=true;Timeline=carried=0;return true;
        }
        public bool Advance(float unscaledDelta,bool suspended){
            if(!Active||suspended)return false;
            // Keep overshoot, but always render each major stage at least once.
            float end=BeforeHold?HoldDuration:WipeActive?HoldDuration+Duration:TotalDuration;
            float target=Timeline+carried+Mathf.Max(0,unscaledDelta);
            Timeline=target>=end-.00001f?end:target;carried=Mathf.Max(0,target-end);
            if(Timeline<TotalDuration)return false;
            Active=false;return true;
        }
    }
}
