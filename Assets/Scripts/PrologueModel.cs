using System;
using System.Collections.Generic;

namespace LingyueEchoes
{
    [Serializable] public class DialogueLine {public string speaker,text;}
    [Serializable] public class DialogueScript {public string id;public DialogueLine[] lines;}
    [Serializable] public class Zone {public string id,name;public double min,max;}
    [Serializable] public class PrologueContent {public DialogueScript[] scripts;public Zone[] zones;public string[] story;}
    public class DialogueDirector
    {
        DialogueScript script;int index;double typed,hold;string phase="idle";
        public System.Action<string> onClose;
        public bool Active=>phase=="typing"||phase=="waiting";
        public string Speaker=>script!=null&&index<script.lines.Length?script.lines[index].speaker:"narration";
        public string VisibleText=>script==null?"":script.lines[index].text.Substring(0,Math.Min(script.lines[index].text.Length,(int)Math.Floor(typed)));
        public void Play(DialogueScript s){script=s;index=0;typed=hold=0;phase=s.lines.Length>0?"typing":"done";if(s.lines.Length==0)Finish();}
        public void Reset(){script=null;index=0;typed=hold=0;phase="idle";}
        public void Update(double dt){if(phase=="waiting"){hold+=Math.Max(0,dt);if(hold>=Math.Max(2.2,script.lines[index].text.Length*.085))Confirm();return;}if(phase!="typing")return;typed=Math.Min(script.lines[index].text.Length+.12,typed+34*dt);if(typed>=script.lines[index].text.Length)phase="waiting";}
        public void Confirm(){if(phase=="typing"){typed=script.lines[index].text.Length;phase="waiting";return;}if(phase!="waiting")return;index++;hold=typed=0;if(index>=script.lines.Length)Finish();else phase="typing";}
        void Finish(){string id=script?.id??"";script=null;phase="done";index=0;typed=0;onClose?.Invoke(id);}
    }
    public class TutorialDirector
    {
        readonly HashSet<string> spoken=new HashSet<string>();int wait=45,waypoint,rest=90,lastDodge,lastDeflect,lastDummy;
        readonly double[] route={-1235,1030,1640,1510};readonly int[] holds={90,120,180,120};
        public void Begin(Combat sim){spoken.Clear();wait=45;waypoint=0;rest=90;lastDodge=sim.dodgeEvades;lastDeflect=sim.deflectSerial;lastDummy=sim.dummyHits;sim.dummyBox=new Box(762,0,56,120);sim.terrain=new List<Box>{new Box(240,0,160,46)};}
        public void Pump(Combat sim){var p=sim.player;var b=sim.boss;bool practicing=waypoint==1&&rest>0&&p.x>=880&&p.x<1150&&Math.Abs(p.x-b.x)<190;
            sim.ai=false;sim.enemyActive=practicing;sim.nonlethal=practicing;if(!practicing&&b.move!=null){b.move=null;b.state=b.y>0?"air":"idle";b.age=0;}
            if(rest>0){rest--;if(rest==0)waypoint=waypoint==3?2:waypoint+1;}
            double target=route[waypoint];if(b.state=="idle"||b.state=="run"||b.state=="air"){double delta=target-b.x;
                if(Math.Abs(delta)>8){b.face=delta>0?1:-1;if(b.vy==0&&b.y==0&&((b.x<240&&b.x>155&&b.face>0)||(b.x>400&&b.x<485&&b.face<0)))b.vy=13;b.x+=b.face*Math.Min(8,Math.Abs(delta));string state=b.y>0?"air":"run";if(b.state!=state){b.state=state;b.age=0;}}
                else{if(rest==0)rest=holds[waypoint];string state=b.y>0?"air":"idle";if(b.state!=state){b.state=state;b.age=0;}if(practicing)b.face=p.x>b.x?1:-1;}}
            if(!practicing){wait=45;return;}if(b.state!="idle"&&b.state!="run"){wait=45;return;}if(--wait<=0){wait=150;sim.DebugBoss(0);}}
        public string Speech(Combat sim,bool canSpeak){double x=sim.player.x;string id=x<120?"routeWake":x<520?"routeJump":x<880?"routeDummy":x<1150?"routeSpar":"routeGate";
            if(sim.dummyHits>lastDummy){lastDummy=sim.dummyHits;id="routeHit";}if(sim.dodgeEvades>lastDodge){lastDodge=sim.dodgeEvades;id="routeDodge";}if(sim.deflectSerial>lastDeflect){lastDeflect=sim.deflectSerial;id="routeParry";}
            if(canSpeak&&!spoken.Contains(id)&&Math.Abs(sim.boss.x-x)<420){spoken.Add(id);return id;}return null;}
        public static string Title(double x)=>x<=-800?"苏醒之庭":x<=520?"旧日参道":x<=1240?"练武之庭":"月门试炼场";
        public static string Hint(double x)=>x<120?"沿灯火向右前进":x<520?"跳过前方崩石":x<880?"木桩可自由练习连斩":x<1150?"可与守灯人切磋，也可继续前进":"穿过月门，迎接试炼";
    }
}
