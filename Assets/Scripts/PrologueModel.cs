using System;
using System.Collections.Generic;

namespace LingyueEchoes
{
    public static class PrologueLayout
    {
        public const double ArenaMin=1200,ArenaMax=2280,ArenaCenter=(ArenaMin+ArenaMax)/2,GateEntry=1340;
        public const double CheckpointX=1100,GuardianWaitX=ArenaCenter,PlayerFightX=1530,GuardianFightX=1940;
        public const string CheckpointKey="lingyue.moonGateCheckpoint";
    }
    [Serializable] public class DialogueLine {public string speaker,text;}
    [Serializable] public class DialogueScript {public string id;public DialogueLine[] lines;}
    [Serializable] public class Zone {public string id,name;public double min,max;}
    [Serializable] public class PrologueContent {public DialogueScript[] scripts;public Zone[] zones;public string[] story;}
    public class DialogueDirector
    {
        DialogueScript script;int index;double typed,hold;string phase="idle";bool automatic;
        public System.Action<string> onClose;
        public Func<string,string> resolveText;
        public bool Active=>phase=="typing"||phase=="waiting";
        public string Speaker=>script!=null&&index<script.lines.Length?script.lines[index].speaker:"narration";
        string LineText=>script==null?"":resolveText?.Invoke(script.lines[index].text)??script.lines[index].text;
        public string VisibleText {get{string text=LineText;return phase=="waiting"?text:text.Substring(0,Math.Min(text.Length,(int)Math.Floor(typed)));}}
        public void Play(DialogueScript s,bool autoAdvance=true){script=s;automatic=autoAdvance;index=0;typed=hold=0;phase=s.lines.Length>0?"typing":"done";if(s.lines.Length==0)Finish();}
        public void Reset(){script=null;index=0;typed=hold=0;phase="idle";}
        public void Update(double dt){if(phase=="waiting"){hold+=Math.Max(0,dt);if(automatic&&hold>=Math.Max(2.2,LineText.Length*.085))Confirm();return;}if(phase!="typing")return;typed=Math.Min(LineText.Length+.12,typed+34*dt);if(typed>=LineText.Length)phase="waiting";}
        public void Confirm(){if(phase=="typing"){typed=LineText.Length;phase="waiting";return;}if(phase!="waiting")return;index++;hold=typed=0;if(index>=script.lines.Length)Finish();else phase="typing";}
        void Finish(){string id=script?.id??"";script=null;phase="done";index=0;typed=0;onClose?.Invoke(id);}
    }
    public class TutorialDirector
    {
        readonly HashSet<string> spoken=new HashSet<string>();int wait=45,waypoint,lastDodge,lastDummy,practiceDeflect;bool moving,practicing,farewell;
        string speaking;
        readonly double[] route={-1235,790,1030,PrologueLayout.GuardianWaitX};
        public bool GateBound=>waypoint==3;
        public bool Practicing=>practicing;
        public bool CanInteract(Combat sim)=>speaking==null&&!farewell&&!moving&&!practicing&&Math.Abs(sim.player.x-sim.boss.x)<210;
        public void Begin(Combat sim){spoken.Clear();wait=45;waypoint=0;moving=practicing=farewell=false;speaking=null;lastDodge=sim.dodgeEvades;lastDummy=sim.dummyHits;sim.dummyBox=new Box(762,0,56,120);sim.terrain=new List<Box>();}
        public string Interact(Combat sim){if(!CanInteract(sim))return null;return new[]{"routeWake","routeDummy","routeSpar","bossPre"}[waypoint];}
        public void BeginDialogue(string id){speaking=id;}
        public void FinishDialogue(Combat sim,string id){
            if(speaking!=id)return;
            speaking=null;
            if(id=="routeWake"||id=="routeDummy")Depart(waypoint+1);
            else if(id=="routeSpar"){practicing=true;practiceDeflect=sim.deflectSerial;}
            else if(id=="routeParry"&&farewell){farewell=false;Depart(3);}
        }
        void Depart(int next){waypoint=next;moving=true;}
        public void Pump(Combat sim){var p=sim.player;var b=sim.boss;
            if(practicing&&sim.deflectSerial>practiceDeflect){practicing=false;farewell=true;b.move=null;b.state="idle";b.age=0;}
            // Players can skip the optional lesson before starting it.
            if(!practicing&&!farewell&&speaking==null&&p.x>=1240&&waypoint<3)Depart(3);
            bool spar=practicing&&p.x>=880&&p.x<1200&&Math.Abs(p.x-b.x)<210;
            sim.ai=false;sim.enemyActive=spar;sim.nonlethal=spar;if(!spar&&b.move!=null){b.move=null;b.state=b.y>0?"air":"idle";b.age=0;}
            double target=route[waypoint];if(b.state=="idle"||b.state=="run"||b.state=="air"){double delta=target-b.x;
                if(Math.Abs(delta)>8){b.face=delta>0?1:-1;b.x+=b.face*Math.Min(8,Math.Abs(delta));string state=b.y>0?"air":"run";if(b.state!=state){b.state=state;b.age=0;}}
                else{b.x=target;moving=false;string state=b.y>0?"air":"idle";if(b.state!=state){b.state=state;b.age=0;}b.face=p.x>b.x?1:-1;}}
            if(!spar){wait=45;return;}if(b.state!="idle"&&b.state!="run"){wait=45;return;}if(--wait<=0){wait=150;sim.DebugBoss(0);}}
        public string Speech(Combat sim,bool canSpeak){double x=sim.player.x;string id=null;
            if(sim.dummyHits>lastDummy){lastDummy=sim.dummyHits;id="routeHit";}if(sim.dodgeEvades>lastDodge){lastDodge=sim.dodgeEvades;id="routeDodge";}
            if(!canSpeak||speaking!=null||moving)return null;
            if(farewell)return "routeParry";
            if(id!=null&&!spoken.Contains(id)&&Math.Abs(sim.boss.x-x)<420){spoken.Add(id);return id;}return null;}
        public static string Title(double x)=>x<=-800?"苏醒之庭":x<=520?"旧日参道":x<=1240?"练武之庭":"月门试炼场";
    }
}
