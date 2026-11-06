using System;
using System.Collections.Generic;
using UnityEngine;

namespace LingyueEchoes
{
    [Serializable] public class ClipDefinition {public string name,folder,path;public float duration,sample;public bool loop;public float[] times;public string[] frames;public int contactPose,secondContactPose;}
    [Serializable] public class ClipManifest {public ClipDefinition[] clips;}
    public static class PixelWorld
    {
        static readonly Dictionary<string,ClipDefinition> definitions=new Dictionary<string,ClipDefinition>();
        static readonly Dictionary<string,AnimationClip> clips=new Dictionary<string,AnimationClip>();
        static readonly Dictionary<string,RuntimeAnimatorController> controllers=new Dictionary<string,RuntimeAnimatorController>();
        public static void Load(){if(definitions.Count>0)return;var data=Resources.Load<TextAsset>("Data/animations");foreach(var c in JsonUtility.FromJson<ClipManifest>(data.text).clips)definitions[c.folder+"/"+c.name]=c;}
        public static ClipDefinition Definition(string folder,string clip){Load();definitions.TryGetValue(folder+"/"+clip,out var d);return d;}
        public static void Sample(GameObject target,string folder,string name,double seconds,int cell=-1){var d=Definition(folder,name);if(d==null)return;string key=folder+"/"+name;
            if(!clips.TryGetValue(key,out var clip)){clip=Resources.Load<AnimationClip>(d.path);clips[key]=clip;}if(!clip)return;
            double time=cell>=0?d.times[Mathf.Clamp(cell,0,d.times.Length-1)]+1e-6:d.loop?seconds%d.duration:Math.Min(seconds,d.duration-1e-6);
            if(!controllers.TryGetValue(folder,out var controller)){controller=Resources.Load<RuntimeAnimatorController>("Animations/"+folder+"/Controller");controllers[folder]=controller;}
            var animator=target.GetComponent<Animator>();if(!animator)animator=target.AddComponent<Animator>();if(animator.runtimeAnimatorController!=controller){animator.runtimeAnimatorController=controller;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.speed=0;animator.Rebind();}
            animator.Play(name,0,(float)(Math.Max(0,time)/clip.length));animator.Update(0);}
        public static SpriteRenderer Sprite(Transform parent,string name,string resource,float x,float y,float width,float height,int order,Vector2 anchor){
            var go=new GameObject(name);go.transform.SetParent(parent,false);var sp=go.AddComponent<SpriteRenderer>();sp.sprite=Resources.Load<Sprite>(resource);sp.sortingOrder=order;
            if(!sp.sprite)throw new InvalidOperationException("Missing sprite "+resource);
            go.transform.localPosition=new Vector3((x+width*(.5f-anchor.x))/100,(y+height*(.5f-anchor.y))/100,0);
            go.transform.localScale=new Vector3(width/sp.sprite.rect.width,height/sp.sprite.rect.height,1);return sp;}
        public static readonly string[] SceneryNames={"MoonlitGrove","BambooCourtyard","MoonGate"};
        public const float SceneryWidth=900;
        // Each reusable module has a local left edge at zero and a shared walking surface at -95.
        // Canopy and landmarks are 75% of their former size; the ground extends below the camera.
        public static GameObject CreateSceneryModule(int column){var module=new GameObject(SceneryNames[column-1]);float scale=SceneryWidth/724,top=-95+548*scale;
            string[] rows={"canopy","landmarks","foundation"};int[] edges={0,300,548,724};
            for(int row=0;row<3;row++){float y=top-edges[row]*scale,h=row==2?330:(edges[row+1]-edges[row])*scale;
                Sprite(module.transform,rows[row],"prologue_route/parts/"+rows[row]+"-"+column,0,y,SceneryWidth,h,row,new Vector2(0,1));}
            return module;}
        static GameObject ModuleInstance(Transform parent,string resource){var prefab=Resources.Load<GameObject>("Scenery/"+resource);if(!prefab)return null;
#if UNITY_EDITOR
            if(!Application.isPlaying)return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab,parent);
#endif
            return UnityEngine.Object.Instantiate(prefab,parent,false);}
        static void PlaceScenery(Transform parent,int column,float x,string name,bool mirrored=false){var module=ModuleInstance(parent,SceneryNames[column-1])??CreateSceneryModule(column);
            module.transform.SetParent(parent,false);module.name=name;module.transform.localPosition=new Vector3((x+(mirrored?SceneryWidth:0))/100,0,0);module.transform.localScale=new Vector3(mirrored?-1:1,1,1);}
        static void PlaceProp(Transform parent,string name,string resource,float x,float width,float height){var prop=ModuleInstance(parent,name);
            if(prop){prop.transform.localPosition=new Vector3(x/100,0,0);return;}var root=new GameObject(name);root.transform.SetParent(parent,false);root.transform.localPosition=new Vector3(x/100,0,0);
            Sprite(root.transform,name,resource,0,-95,width,height,5,new Vector2(.5f,0));}
        public static SpriteRenderer PlaceCheckpoint(Transform props){
            var marker=new GameObject("MoonGateCheckpoint").transform;
            marker.SetParent(props,false);marker.localPosition=new Vector3((float)PrologueLayout.CheckpointX/100,0,0);
            PlaceProp(marker,"MoonLantern","prologue_route/moon_lantern",0,68,135);
            return marker.GetComponentInChildren<SpriteRenderer>();
        }
        public const float RubblePadding=3;
        public static void PlaceBrokenSteps(Transform props){
            if(props.Find("BrokenSteps"))return;
            Sprite(props,"BrokenSteps","prologue_route/broken-rubble",(float)PrologueLayout.StepsLeft-RubblePadding,-95-RubblePadding,
                (float)PrologueLayout.StepsWidth+2*RubblePadding,(float)PrologueLayout.StepsHeight+2*RubblePadding,6,Vector2.zero);
        }
        public static SpriteRenderer PlaceArenaSeal(Transform props,bool left){
            int boundary=(int)(left?PrologueLayout.ArenaMin:PrologueLayout.ArenaMax);
            var old=props.Find("MoonLantern"+boundary);
            if(old)old.gameObject.SetActive(false);
            // Fighters' centers stop at the boundary; leave their feet in front of the stone.
            return Sprite(props,left?"ArenaSealLeft":"ArenaSealRight","prologue_route/moon-boundary",
                boundary+(left?-54:54),-98,61,176,8,new Vector2(.5f,0));
        }
        public static Transform Create(Transform parent){var world=new GameObject("World").transform;world.SetParent(parent,false);
            var scenery=new GameObject("SceneryModules").transform;scenery.SetParent(world,false);
            PlaceScenery(scenery,1,-2540,"GroveBoundary",true);PlaceScenery(scenery,1,-1640,"01_WakingGrove");
            PlaceScenery(scenery,2,-740,"02_OldRoad");PlaceScenery(scenery,3,160,"03_TrainingCourtyard");
            PlaceScenery(scenery,3,1060,"04_MoonGateArena",true);PlaceScenery(scenery,2,1960,"05_ArenaExtension",true);
            var props=new GameObject("Props").transform;props.SetParent(world,false);
            PlaceBrokenSteps(props);
            PlaceProp(props,"PracticeDummy","prologue_route/practice_dummy",(float)PrologueLayout.PracticeDummyX,84,120);
            var boss=new GameObject("Guardian");boss.transform.SetParent(world,false);boss.AddComponent<SpriteRenderer>().sortingOrder=10;boss.transform.localPosition=new Vector3(-12.35f,-.95f,0);boss.GetComponent<SpriteRenderer>().sprite=Resources.Load<Sprite>("guardian8/frames/idle/idle-01");
            var hero=new GameObject("Hero");hero.transform.SetParent(world,false);hero.AddComponent<SpriteRenderer>().sortingOrder=12;hero.transform.localPosition=new Vector3(-14,-.95f,0);hero.transform.localScale=Vector3.one*1.5f;hero.GetComponent<SpriteRenderer>().sprite=Resources.Load<Sprite>("pixel8/frames/idle/idle-01");
            return world;
        }
    }
    public class PixelActor
    {
        readonly GameObject go;readonly SpriteRenderer renderer;readonly bool hero;string previous="";int started,previousFrame=-1,attackId=-1;Fighter actor;
        double idleSeconds;
        public PixelActor(GameObject go,bool hero){this.go=go;this.hero=hero;renderer=go.GetComponent<SpriteRenderer>();}
        public void Draw(Fighter a,int frame,bool victory=false,string pose=null,int poseCell=7,float angle=0,float lift=0,float alpha=1,double? poseSeconds=null,double? idleDelta=null){
            string clip=hero?(a.state=="attack"?"attack"+(a.combo+1):a.state=="air"?"jump":a.state=="broken"||a.state=="stagger"?"hurt":a.state=="phase"?"idle":a.state):GuardianClip(a);
            if(victory&&a.hp>0)clip="victory";if(pose!=null)clip=pose;string folder=hero?"pixel8":"guardian8";var definition=PixelWorld.Definition(folder,clip);
            if(definition==null){clip="idle";definition=PixelWorld.Definition(folder,clip);}
            if(clip!=previous||frame<previousFrame||actor!=a||(!hero&&attackId!=a.attackId)){
                started=frame;idleSeconds=0;previous=clip;actor=a;attackId=a.attackId;
            }else if(clip=="idle")idleSeconds+=idleDelta??(frame-previousFrame)/60d;
            previousFrame=frame;
            int cell=-1;
            if(pose!=null){
                if(!poseSeconds.HasValue)cell=poseCell;
                if(cell>=0&&definition.contactPose>0){
                    int contact=definition.contactPose;
                    cell=poseCell<=4?poseCell*contact/4:contact+(poseCell-4)*(definition.frames.Length-1-contact)/3;
                }
            }
            else if(a.move!=null&&definition.contactPose>0)cell=DrawnAttackCell(definition,a.move,a.age);
            else if(hero&&a.move!=null&&(clip.StartsWith("attack")||clip=="execute"||clip=="finisher"))cell=AttackCell(clip,a.move,a.age);
            else if(!hero&&a.move!=null){var m=a.move;int age=Math.Max(0,a.age-1),contact=clip=="double"||clip=="spin"?3:4;
                if(age<m.wind){int slots=Math.Max(1,(int)Math.Ceiling(m.wind*8d/60));cell=1+(int)Math.Floor(Math.Floor(age*8d/60)*(contact-2)/Math.Max(1,slots-1));}
                else if(m.pulses!=null&&age>=m.wind+m.pulses[1])cell=Math.Min(7,5+(int)Math.Floor((age-m.wind-m.pulses[1])*8d/60));
                else cell=Math.Min(m.pulses!=null?4:7,contact+(int)Math.Floor((age-m.wind)*8d/60));}
            else if(clip=="jump")cell=hero?(a.y==0?7:a.vy>10?0:a.vy>6?1:a.vy>2?2:a.vy>-1?3:a.vy>-5?4:a.y<25?6:5):(a.y==0?7:a.vy>6?1:a.vy>1?2:a.vy>-2?3:a.vy>-6?4:5);
            else if(!hero&&clip=="stagger")cell=new[]{1,2,4,6,7}[Math.Min(4,(int)Math.Floor(a.age*8d/60))];
            else if(!hero&&clip=="broken")cell=Math.Min(7,(int)Math.Floor(a.age*8d/60));
            int sampleAge=clip=="heal"?Math.Max(0,a.age-1):a.age;
            double seconds=poseSeconds??(clip=="idle"?idleSeconds:(clip=="run"||clip=="dead"||clip=="victory"?frame-started:sampleAge)/60d+1e-7);
            PixelWorld.Sample(go,folder,clip,seconds,cell);
            go.transform.localPosition=new Vector3((float)Math.Floor(a.x+.5)/100,((float)Math.Floor(a.y+.5)-95+lift)/100,0);
            float scale=hero?1.5f:1;go.transform.localScale=new Vector3(a.face*scale,scale,1);go.transform.localRotation=Quaternion.Euler(0,0,angle*a.face);
            renderer.color=hero&&pose==null&&a.invuln>0&&(frame/3)%2==0?new Color(1,.86f,.86f,.65f):new Color(1,1,1,alpha);go.SetActive(alpha>0);
        }
        static string GuardianClip(Fighter a){if(a.move!=null){switch(a.move.name){case "双连斩":return "double";case "突刺":return "thrust";case "震地":return "slam";case "跃击":return "leap";case "赤月回旋":return "spin";default:return "slash";}}return a.state=="air"?"jump":a.state=="hurt"?"stagger":a.state;}
        // Art is retimed around resolved contacts; this never advances the combat clock.
        public static int DrawnAttackCell(ClipDefinition clip,Move move,int age){
            int tick=Math.Max(0,age-1),contact=clip.contactPose;
            if(tick<move.wind)return tick*contact/Math.Max(1,move.wind);
            int startTick=move.wind,startPose=contact;
            if(clip.secondContactPose>contact&&move.pulses!=null&&move.pulses.Length>1){
                int pulse=move.pulses[1];
                if(tick<move.wind+pulse)return contact+(tick-move.wind)*(clip.secondContactPose-contact)/Math.Max(1,pulse);
                startTick+=pulse;startPose=clip.secondContactPose;
            }
            int endTick=move.wind+move.active+move.recovery-1,last=clip.frames.Length-1;
            return Math.Min(last,startPose+(tick-startTick)*(last-startPose)/Math.Max(1,endTick-startTick));
        }
        public static int AttackCell(string clip,Move move,int age){int contact=clip=="finisher"?4:3,resolved=Math.Max(0,age-1);if(resolved<move.wind){int slots=Math.Max(1,(int)Math.Ceiling(move.wind*8d/60)),slot=(int)Math.Floor(resolved*8d/60);return 1+(int)Math.Floor(slot*(contact-2d)/Math.Max(1,slots-1));}
            int after=(int)Math.Floor((resolved-move.wind)*8d/60),count=Math.Max(1,(int)Math.Ceiling((move.active+move.recovery)*8d/60));return Math.Min(7,contact+(int)Math.Ceiling(after*(7d-contact)/Math.Max(1,count-1)));}
    }
    public class PixelEffects
    {
        class Effect {public GameObject go;public SpriteRenderer renderer;public VisualCue cue;}
        readonly Effect[] pool=new Effect[12];int cursor,next;Combat model;
        public PixelEffects(Transform parent){for(int i=0;i<pool.Length;i++){var go=new GameObject("Effect-"+i);go.transform.SetParent(parent,false);pool[i]=new Effect{go=go,renderer=go.AddComponent<SpriteRenderer>()};pool[i].renderer.sortingOrder=20;go.SetActive(false);}}
        public void Clear(){foreach(var e in pool){e.cue=null;e.go.SetActive(false);}cursor=next=0;model=null;}
        public void Draw(Combat sim){if(model!=sim){Clear();model=sim;}foreach(var cue in sim.visuals){if(cue.id<=cursor)continue;cursor=cue.id;pool[next++%pool.Length].cue=cue;}
            foreach(var e in pool){var cue=e.cue;if(cue==null)continue;string clip=cue.IsPlayerCombo?"cut_fx"+(cue.combo+1):cue.kind=="charge"?"impact":cue.kind=="parry"?"parry_success":cue.kind=="perfectDodge"?"perfect_dodge":cue.kind;
                var d=PixelWorld.Definition("pixel8",clip);if(d==null){e.cue=null;e.go.SetActive(false);continue;}
                int source=(int)Math.Round(d.duration*60),life=cue.kind=="charge"?Math.Min(12,source):source,age=sim.frame-cue.frame;
                if(age>=life||!cue.TryGetAnchor(cue.owner==1?sim.player:sim.boss,out double x,out double y)){e.cue=null;e.go.SetActive(false);continue;}
                e.go.SetActive(age>=0);float size=cue.kind=="charge"?(cue.owner==1?.48f:.22f):cue.IsPlayerCombo?1.5f:cue.kind=="slash"?cue.power?2.2f:1.65f:cue.kind=="impact"?1.1f:cue.kind=="heal_fx"?1.5f:1.8f;
                e.go.transform.localPosition=new Vector3((float)Math.Floor(x+.5)/100,((float)Math.Floor(y+.5)-95)/100,0);e.go.transform.localScale=new Vector3(cue.face*size,size,1);
                e.renderer.color=cue.owner==2&&(cue.kind=="charge"||cue.kind=="slash")?(cue.danger?new Color(1,.39f,.53f):new Color(1,.87f,.57f)):cue.kind=="charge"?new Color(1,.78f,.49f):Color.white;
                PixelWorld.Sample(e.go,"pixel8",clip,age/60d*(cue.kind=="charge"?(double)source/life:1));}
        }
    }
}
