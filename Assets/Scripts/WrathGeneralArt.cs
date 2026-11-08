using System;
using UnityEngine;

namespace LingyueEchoes
{
    // The first-chapter general owns these frames; no guardian/prologue art is reused.
    public sealed class WrathGeneralArt:IWrathPoseSource
    {
        public const string ResourceRoot="WrathGeneral/frames/";
        public const int FrameCount=16;
        public const int StunLoopStart=12,StunEntryFps=18,StunHoldFps=8;
        // Shared import calibration, measured from the bottom-left of each square canvas.
        public const int CanvasPixels=384;
        public const float RootX=230f/384,FootY=37f/384,PixelsPerUnit=180f;
        readonly Sprite[] idle=Load("idle"),walk=Load("walk"),stun=Load("stun");

        static Sprite[] Load(string action){
            var frames=new Sprite[FrameCount];
            for(int i=0;i<frames.Length;i++){
                string path=ResourceRoot+action+"/"+action+"-"+(i+1).ToString("00");
                frames[i]=Resources.Load<Sprite>(path);
                if(!frames[i])throw new InvalidOperationException("Missing Wrath General animation frame: Resources/"+path+".png");
            }
            return frames;
        }

        public Sprite Pose(Fighter fighter,string scripted,int cell,double? seconds){
            // PixelActor supplies its presentation clock (fixed 60Hz, plus dialogue idle time).
            double elapsed=Math.Max(0,seconds??Math.Max(0,fighter.age-1)/60d);
            string state=scripted??fighter.state;
            bool stunned=state=="broken"||state=="stagger"||state=="hurt"||state=="stun"||fighter.hp<=0;
            var frames=stunned?stun:state=="run"||state=="walk"?walk:idle;
            int fps=frames==walk?16:8;
            double entryDuration=(double)StunLoopStart/StunEntryFps;
            int index=scripted!=null&&!seconds.HasValue?Mathf.Clamp(cell*15/7,0,15):
                stunned?elapsed<entryDuration?(int)Math.Floor(elapsed*StunEntryFps+1e-7):
                    StunLoopStart+(int)Math.Floor((elapsed-entryDuration)*StunHoldFps+1e-7):
                    (int)Math.Floor(elapsed*fps+1e-7);
            // Enter the kneel once, then hold the kneeling breathing poses (13-16).
            // Unauthored combat moves keep this same character's ready pose.
            if(scripted==null&&!stunned&&fighter.move!=null)return idle[0];
            if(fighter.hp<=0)return frames[Math.Min(FrameCount-1,index)];
            if(stunned&&index>=FrameCount)index=StunLoopStart+(index-StunLoopStart)%(FrameCount-StunLoopStart);
            return frames[index%FrameCount];
        }
    }
}
