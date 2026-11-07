using System;
using System.Collections.Generic;

namespace LingyueEchoes
{
    public static class PrologueLayout
    {
        public const double RouteStart=-1390,WakeX=-1190,WakeGuideX=WakeX+165;
        public const double ArenaMax=1128.75,ArenaMin=ArenaMax-1080;
        public const double ArenaCenter=(ArenaMin+ArenaMax)/2,GateEntry=-40,CheckpointX=20;
        public const double MoonGateX=ArenaCenter+1145,RouteEnd=MoonGateX+60;
        public const double GuardianWaitX=ArenaCenter,PlayerFightX=ArenaMin+330,GuardianFightX=ArenaMin+740;
        public const double PracticeDummyX=-345,DummyGuideX=-485,SparGuideX=-170;
        public const double StepsLeft=-850,StepsWidth=112,StepsHeight=36,StepsGuideX=-640;
        public const double WakeEnd=-935,RoadEnd=-465;
        public const float BackdropLeft=-1940,BackdropRight=2650;
        public const float CameraPadding=8,FullViewHalfWidth=640;
        public static float ClampViewX(float center,float halfWidth){
            float left=BackdropLeft+halfWidth+CameraPadding,right=BackdropRight-halfWidth-CameraPadding;
            return left<=right?Math.Max(left,Math.Min(center,right)):(BackdropLeft+BackdropRight)/2;
        }
        public static Box BrokenSteps()=>new Box(StepsLeft,0,StepsWidth,StepsHeight,noFoothold:true);
        public const string CheckpointKey="lingyue.moonGateCheckpoint";
        public const string FirstVictoryStoryKey="lingyue.firstVictoryStoryV2";
        public const string ProgressKey="lingyue.prologue",TailCountKey="lingyue.tails";
        public const int SaveSlots=3;
        // The first slot keeps the original keys, including saves made before the picker existed.
        public static string SaveKey(string key,int slot)=>slot==1?key:key+".slot"+slot;
    }
    public struct StatusTrail
    {
        const float Hold=.22f, DrainPerSecond=1.1f;
        float value,hold;
        public float Width {get;private set;}
        public void Reset(float ratio){value=Width=ratio;hold=0;}
        public void Update(float ratio,float dt){
            if(ratio<value&&Width<=value)hold=Hold;
            value=ratio;
            if(Width<=ratio){Width=ratio;hold=0;return;}
            float held=Math.Min(hold,dt);hold-=held;
            Width=Math.Max(ratio,Width-DrainPerSecond*(dt-held));
        }
    }
    [Serializable] public class DialogueLine {public string speaker,text,cue;}
    [Serializable] public class DialogueScript {public string id;public DialogueLine[] lines;}
    [Serializable] public class Zone {public string id,name;public double min,max;}
    [Serializable] public class TutorialLesson {public string id,title,text,controls;}
    [Serializable] public class PrologueContent {public DialogueScript[] scripts;public TutorialLesson[] lessons;public Zone[] zones;public string[] story;}
    public class DialogueDirector
    {
        DialogueScript script;int index;double typed,hold;string phase="idle";bool automatic;
        public System.Action<string> onClose;
        public bool Active=>phase=="typing"||phase=="waiting";
        DialogueLine Current=>script!=null&&index<script.lines.Length?script.lines[index]:null;
        public string Id=>script?.id;
        public string Cue=>Current?.cue;
        public string Speaker=>Current?.speaker??"narration";
        public string LineText=>Current?.text??"";
        public string VisibleText {get{string text=LineText;return phase=="waiting"?text:text.Substring(0,Math.Min(text.Length,(int)Math.Floor(typed)));}}
        public static double ReadingHold(int characters)=>Math.Max(2.4,.65+characters*.11);
        public void Play(DialogueScript s,bool autoAdvance=true){script=s;automatic=autoAdvance;index=0;typed=hold=0;phase=s.lines.Length>0?"typing":"done";if(s.lines.Length==0)Finish();}
        public void Reset(){script=null;index=0;typed=hold=0;phase="idle";}
        public void Update(double dt){if(phase=="waiting"){hold+=Math.Max(0,dt);if(automatic&&hold>=ReadingHold(LineText.Length))Confirm();return;}if(phase!="typing")return;typed=Math.Min(LineText.Length,typed+34*Math.Max(0,dt));if(typed>=LineText.Length)phase="waiting";}
        public void Confirm(){if(phase=="typing"){typed=LineText.Length;phase="waiting";return;}if(phase!="waiting")return;index++;hold=typed=0;if(index>=script.lines.Length)Finish();else phase="typing";}
        void Finish(){string id=script?.id??"";script=null;phase="done";index=0;typed=0;onClose?.Invoke(id);}
    }
    public class TutorialDirector
    {
        const int Wake=0,Steps=1,Dummy=2,Spar=3,Gate=4;
        readonly HashSet<string> spoken=new HashSet<string>();int wait=45,waypoint,lastDodge,lastDummy,practiceDeflect,stepsClearFrame;
        bool moving,practicing,farewell,approachedSteps,aboveSteps,hitPending,dodgePending;
        string speaking;
        Box steps;
        readonly double[] route={PrologueLayout.WakeGuideX,PrologueLayout.StepsGuideX,PrologueLayout.DummyGuideX,PrologueLayout.SparGuideX,PrologueLayout.GuardianWaitX};
        public bool GateBound=>waypoint==Gate;
        public bool Practicing=>practicing;
        public bool StepsCleared=>stepsClearFrame>0;
        public bool CanInteract(Combat sim)=>waypoint!=Steps&&speaking==null&&!farewell&&!moving&&!practicing&&Math.Abs(sim.player.x-sim.boss.x)<210&&(waypoint!=Wake||sim.player.vy==0);
        public void Begin(Combat sim,Box steps=null){
            spoken.Clear();wait=45;waypoint=Wake;stepsClearFrame=0;
            moving=practicing=farewell=approachedSteps=aboveSteps=hitPending=dodgePending=false;
            speaking=null;this.steps=steps;lastDodge=sim.dodgeEvades;lastDummy=sim.dummyHits;
            sim.dummyBox=new Box(PrologueLayout.PracticeDummyX-28,0,56,120);sim.terrain=new List<Box>();if(steps!=null)sim.terrain.Add(steps);
        }
        public string Interact(Combat sim){if(!CanInteract(sim))return null;return new[]{"routeWake",null,"routeDummy","routeSpar","bossPre"}[waypoint];}
        public void BeginDialogue(string id){speaking=id;}
        public void FinishDialogue(Combat sim,string id){
            if(speaking!=id)return;
            speaking=null;
            if(id=="routeWake")Depart(steps==null?Dummy:Steps);
            else if(id=="routeStepsClear")Depart(Dummy);
            else if(id=="routeDummy")Depart(Spar);
            else if(id=="routeSpar"){practicing=true;practiceDeflect=sim.deflectSerial;}
            else if(id=="routeParry"&&farewell){farewell=false;Depart(Gate);}
        }
        void Depart(int next){waypoint=next;moving=true;}
        public void Pump(Combat sim){var p=sim.player;var b=sim.boss;
            TrackSteps(sim);
            if(practicing&&sim.deflectSerial>practiceDeflect){practicing=false;farewell=true;dodgePending=false;b.move=null;b.state="idle";b.age=0;}
            // Players can skip the optional lesson before starting it.
            if(!practicing&&!farewell&&speaking==null&&p.x>=PrologueLayout.GateEntry&&waypoint<Gate)Depart(Gate);
            bool spar=practicing&&p.x>=PrologueLayout.SparGuideX-150&&p.x<PrologueLayout.ArenaMin&&Math.Abs(p.x-b.x)<210;
            sim.ai=false;sim.enemyActive=spar;sim.nonlethal=spar;if(!spar&&b.move!=null){b.move=null;b.state=b.y>0?"air":"idle";b.age=0;}
            double target=route[waypoint];if(b.state=="idle"||b.state=="run"||b.state=="air"){double delta=target-b.x;
                if(Math.Abs(delta)>8){
                    b.face=delta>0?1:-1;double advance=Math.Min(8,Math.Abs(delta)),radius=Combat.BodyWidth(b)/2;
                    if(steps!=null&&b.vy==0&&b.y<steps.h&&
                        (b.face>0&&b.x+radius<=steps.x&&b.x+radius+advance>steps.x||
                         b.face<0&&b.x-radius>=steps.x+steps.w&&b.x-radius-advance<steps.x+steps.w))
                        b.vy=Rules.jump;
                    b.x+=b.face*advance;string state=b.y>0||b.vy>0?"air":"run";if(b.state!=state){b.state=state;b.age=0;}}
                else{b.x=target;moving=false;string state=b.y>0?"air":"idle";if(b.state!=state){b.state=state;b.age=0;}b.face=p.x>b.x?1:-1;}}
            if(!spar){wait=45;return;}if(b.state!="idle"&&b.state!="run"){wait=45;return;}if(--wait<=0){wait=150;sim.DebugBoss(0);}}
        void TrackSteps(Combat sim){
            if(steps==null||StepsCleared)return;
            var p=sim.player;double radius=Combat.BodyWidth(p)/2,right=steps.x+steps.w;
            if(p.x+radius<=steps.x&&p.x>=steps.x-320){approachedSteps=true;aboveSteps=false;}
            if(approachedSteps&&p.x>=steps.x&&p.x<=right&&p.y>=steps.y+steps.h)aboveSteps=true;
            if(aboveSteps&&p.x-radius>=right&&p.y==0&&p.vy==0)stepsClearFrame=sim.frame;
        }
        public string Lesson(Combat sim){
            double x=sim.player.x;
            if(practicing)return "spar";
            if(steps!=null){
                double approach=Math.Max(PrologueLayout.WakeEnd,steps.x-268);
                if(!StepsCleared&&x>=approach&&x<steps.x+steps.w)return "steps";
                if(StepsCleared&&sim.frame-stepsClearFrame<150)return "stepsClear";
                if(waypoint==Steps&&x<approach)return "move";
            }
            if(waypoint==Wake&&CanInteract(sim))return "meet";
            if(x>=PrologueLayout.RoadEnd&&x<=PrologueLayout.PracticeDummyX+50&&sim.dummyHits==0)return "dummy";
            if(waypoint==Spar&&CanInteract(sim))return "sparOffer";
            return null;
        }
        public string Speech(Combat sim,bool canSpeak){double x=sim.player.x;
            if(sim.dummyHits>lastDummy){lastDummy=sim.dummyHits;hitPending=true;}
            if(sim.dodgeEvades>lastDodge){lastDodge=sim.dodgeEvades;dodgePending=practicing;}
            if(!canSpeak||speaking!=null||moving)return null;
            if(farewell)return "routeParry";
            if(waypoint==Steps){
                if(StepsCleared&&spoken.Add("routeStepsClear"))return "routeStepsClear";
                if(x>=steps.x-270&&x<steps.x&&spoken.Add("routeSteps"))return "routeSteps";
            }
            if(Math.Abs(sim.boss.x-x)<420){
                if(hitPending&&Math.Abs(x-PrologueLayout.PracticeDummyX)<320&&spoken.Add("routeHit")){hitPending=false;return "routeHit";}
                if(dodgePending&&practicing&&spoken.Add("routeDodge")){dodgePending=false;return "routeDodge";}
            }
            return null;}
        public static string Title(double x)=>x<PrologueLayout.WakeEnd?"苏醒之庭":x<PrologueLayout.RoadEnd?"旧日参道":x<PrologueLayout.GateEntry?"练武之庭":"月门试炼场";
    }
}
