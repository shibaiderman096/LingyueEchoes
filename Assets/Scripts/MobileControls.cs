using System.Collections.Generic;
using UnityEngine;

namespace LingyueEchoes
{
    // Pointer ownership keeps the movement thumb independent of action fingers.
    public sealed class MobileControls
    {
        public static readonly Vector2 StickCenter=new Vector2(145,570);
        public const float StickRadius=86,StickTravel=52,DeadZone=.18f;
        public static readonly Vector2[] ButtonCenters={new Vector2(1120,638),new Vector2(1200,558),new Vector2(1040,558),new Vector2(1120,478),new Vector2(1120,372),new Vector2(1070,61)};
        public static readonly string[] Commands={"jump","dodge","attack","parry","interact","pause"};
        readonly Dictionary<int,string> held=new Dictionary<int,string>();
        int? stickPointer;
        public Vector2 Stick {get;private set;}
        public float Axis=>Mathf.Abs(Stick.x)<=DeadZone?0:Mathf.Sign(Stick.x)*(Mathf.Abs(Stick.x)-DeadZone)/(1-DeadZone);
        public bool StickHeld=>stickPointer.HasValue;
        public bool Pressed(string command)=>held.ContainsValue(command);
        public string Begin(int pointer,Vector2 position)
        {
            if(held.ContainsKey(pointer)||stickPointer==pointer)return null;
            if(!stickPointer.HasValue&&(position-StickCenter).sqrMagnitude<=StickRadius*StickRadius){stickPointer=pointer;Move(pointer,position);return null;}
            for(int i=0;i<ButtonCenters.Length;i++){
                float radius=i<4?43:i==4?38:28;
                if((position-ButtonCenters[i]).sqrMagnitude>radius*radius)continue;
                held[pointer]=Commands[i];return Commands[i];
            }
            return null;
        }
        public void Move(int pointer,Vector2 position){if(stickPointer==pointer)Stick=Vector2.ClampMagnitude((position-StickCenter)/StickTravel,1);}
        public void End(int pointer){held.Remove(pointer);if(stickPointer==pointer){stickPointer=null;Stick=Vector2.zero;}}
        public void Reset(){held.Clear();stickPointer=null;Stick=Vector2.zero;}
    }
}
