using System;
using System.Linq;
using UnityEngine;

namespace LingyueEchoes
{
    public static class WrathRoute
    {
        public const string Scene="Chapter01_Wrath";
        public const string Unlocked="lingyue.wrath.unlocked",Current="lingyue.chapter",
            Checkpoint="lingyue.wrath.checkpoint",Defeated="lingyue.wrath.firstDefeat",Completed="lingyue.wrath.complete";
        public static readonly string[] SaveKeys={Unlocked,Current,Checkpoint,Defeated,Completed};
        public const double Start=85,FirstLamp=865,SecondLamp=3065,End=4290,Portal=4215;
        public const string Title="第一章 · 焚心之城", NextTitle="第二章 · 贪婪 · 囚金";
        public static readonly string[] Areas={"焦土城郊","断桥遗迹","焚毁街巷","将军府"};
        public static int Area(double x)=>x<920?0:x<1850?1:x<3120?2:3;
        public static int EncounterAfter(int checkpoint)=>checkpoint==2?4:checkpoint==1?1:0;
        public static double Spawn(int checkpoint)=>checkpoint==2?SecondLamp:checkpoint==1?FirstLamp:Start;
        public static Box[] Bridge()=>new[]{
            new Box(1008,0,112,38),new Box(1164,0,128,76),new Box(1334,0,89,42)
        };
        public sealed class Encounter
        {
            public readonly string Name,Hint;
            public readonly double Trigger,Min,Max,EnemyX,Hp;
            public readonly OpponentPattern Pattern;
            public Encounter(string name,string hint,double trigger,double min,double max,double enemyX,double hp,OpponentPattern pattern)
            {Name=name;Hint=hint;Trigger=trigger;Min=min;Max=max;EnemyX=enemyX;Hp=hp;Pattern=pattern;}
        }
        static Move Move(string name,string pose,int wind,int active,int rest,double reach,double height,double hp,double posture,bool parry=true,double rush=0,int[] pulses=null,int fire=0)
            =>new Move{name=name,pose=pose,wind=wind,active=active,recovery=rest,reach=reach,height=height,damage=hp,posture=posture,parry=parry,speed=rush,pulses=pulses,fireDelay=fire};
        static readonly OpponentPattern Soldier=new OpponentPattern(new[]{
            Move("焦刃横斩","slash",36,5,33,147,103,14,22)
        },32,2.1,138,isBoss:false);
        static readonly OpponentPattern Hunter=new OpponentPattern(new[]{
            Move("赤焰突进","thrust",35,10,34,143,86,19,26,true,10),
            Move("掠火回刃","slash",23,5,32,130,98,13,18)
        },25,4.7,231,isBoss:false);
        static readonly OpponentPattern Elite=new OpponentPattern(new[]{
            Move("禁卫双连斩","double",28,27,36,159,110,16,24,true,pulses:new[]{0,22}),
            Move("焚石重踏","slam",47,7,47,208,48,24,35,false)
        },25,3.0,163,isBoss:false);
        static OpponentPattern general;
        public static OpponentPattern General=>general??(general=BossBehaviorCatalog.Pattern("wrath-general"));
        static Encounter[] fights;
        public static Encounter[] Fights=>fights??(fights=CreateFights());
        static Encounter[] CreateFights()=>new[]{
            new Encounter("焦土残兵","刀抬起后再弹反。它的第一刀并不快。",310,215,795,686,64,Soldier),
            new Encounter("赤焰游猎","留意突进前的停顿，闪开刀锋后再反击。",1515,1440,1835,1745,86,Hunter),
            new Encounter("焦土残兵","趁横斩收刀时贴近。",2030,1910,2370,2290,67,Soldier),
            new Encounter("焚城禁卫","双连斩之间还有一刀；重踏可跳过或闪开。",2540,2400,2970,2840,158,Elite),
            new Encounter("焚城将军 · 赤绫","横扫之后留意停顿，突进落空后正是反击的机会。",3475,3210,4130,3830,870,General)
        };
    }

    public interface IWrathPoseSource
    {
        Sprite Pose(Fighter fighter,string scripted,int cell,double? seconds);
    }

    public sealed class WrathFoeArt:IWrathPoseSource
    {
        public const string Resource="WrathFoes";
        public const string MotionResource="WrathFoesMotion";
        // Per-pose ownership rectangles, in top-down atlas coordinates, and the planted foot line.
        static readonly RectInt[][] Boxes={
            new[]{new RectInt(77,81,240,302),new RectInt(477,64,240,319),new RectInt(852,109,380,274)},
            new[]{new RectInt(54,500,357,250),new RectInt(419,453,295,297),new RectInt(805,525,441,223)},
            new[]{new RectInt(53,841,274,338),new RectInt(401,815,319,364),new RectInt(777,895,462,284)}
        };
        static readonly int[][] Roots={new[]{228,589,989},new[]{195,604,968},new[]{227,607,968}};
        static readonly int[] Feet={378,743,1172};
        static readonly float[] PixelsPerUnit={253,219,258};
        static readonly RectInt[][] MotionBoxes={
            new[]{new RectInt(90,68,279,324),new RectInt(480,68,273,324),new RectInt(861,137,290,255)},
            new[]{new RectInt(58,473,377,292),new RectInt(476,461,343,303),new RectInt(857,523,363,241)},
            new[]{new RectInt(51,826,341,354),new RectInt(422,834,334,346),new RectInt(806,886,357,293)}
        };
        static readonly int[][] MotionRoots={new[]{231,624,1004},new[]{218,617,1008},new[]{237,612,1012}};
        static readonly int[] MotionFeet={385,755,1172};
        static readonly float[] MotionScale={269,252,267};
        readonly Sprite[] poses=new Sprite[6];
        readonly int stride;
        public WrathFoeArt(Texture2D atlas,Texture2D motion,int kind){
            stride=kind==1?6:9;
            for(int i=0;i<3;i++){
                poses[i]=Slice(atlas,Boxes[kind][i],Roots[kind][i],Feet[kind],PixelsPerUnit[kind]);
                poses[3+i]=Slice(motion,MotionBoxes[kind][i],MotionRoots[kind][i],MotionFeet[kind],MotionScale[kind]);
            }
            for(int i=0;i<poses.Length;i++)poses[i].name=$"Wrath foe {kind+1} pose {i+1}";
        }
        static Sprite Slice(Texture2D atlas,RectInt r,int root,int foot,float scale)
            =>Sprite.Create(atlas,new Rect(r.x,atlas.height-r.yMax,r.width,r.height),
                new Vector2((root-r.x)/(float)r.width,(r.yMax-foot)/(float)r.height),scale,0,SpriteMeshType.FullRect);
        public Sprite Pose(Fighter fighter,string scripted,int cell,double? seconds){
            var move=fighter.move;int tick=Math.Max(0,fighter.age-1),pose=0;
            if(scripted=="broken")pose=seconds>=.20?5:0;
            else if(fighter.state=="broken"||fighter.hp<=0)pose=5;
            else if(fighter.state=="run")pose=3+tick/stride%2;
            else if(move!=null){
                if(tick<move.wind)pose=1;
                else if(move.pulses?.Length>1){
                    int contact=tick-move.wind,second=move.pulses[1];
                    pose=contact<4||contact>=second&&contact<second+4?2:contact<second?1:0;
                }else if(tick<move.wind+move.active)pose=2;
            }
            return poses[pose];
        }
        public void Dispose(){foreach(var pose in poses)UnityEngine.Object.Destroy(pose);}
    }

    // Scene references are serialized so the level can be edited without rebuilding runtime geometry.
    public sealed class WrathChapter:MonoBehaviour
    {
        public Transform world,embers,backdrop;
        public GameObject generalArmor;
        public SpriteRenderer[] bridgeStones;
        public SpriteRenderer[] scenery;
        public SpriteRenderer firstLamp,secondLamp,leftSeal,rightSeal,returnSeal;
        public SpriteRenderer fireMark,fireShadow,fireWarning,weaponGlow,armorGlow;
        SpriteRenderer[] ash,weapon;
        WrathFoeArt[] foeArt;
        void OnEnable(){SceneParticleSettings.Changed+=SetAmbientParticles;SetAmbientParticles(SceneParticleSettings.Enabled);}
        void OnDisable(){SceneParticleSettings.Changed-=SetAmbientParticles;}
        void SetAmbientParticles(bool enabled){if(embers)embers.gameObject.SetActive(enabled);}
        void Awake()
        {
            if(embers)ash=embers.GetComponentsInChildren<SpriteRenderer>();
            var foes=Resources.Load<Texture2D>(WrathFoeArt.Resource);
            var motion=Resources.Load<Texture2D>(WrathFoeArt.MotionResource);
            if(foes&&foes.width==1254&&foes.height==1254&&motion&&motion.width==1254&&motion.height==1254)
                foeArt=new[]{new WrathFoeArt(foes,motion,0),new WrathFoeArt(foes,motion,1),new WrathFoeArt(foes,motion,2)};
            if(generalArmor){
                weapon=generalArmor.GetComponentsInChildren<SpriteRenderer>(true)
                    .Where(r=>r.name.StartsWith("Polearm",StringComparison.Ordinal)).ToArray();
            }
        }
        void OnDestroy(){if(foeArt!=null)foreach(var foe in foeArt)foe.Dispose();}
        public IWrathPoseSource UseActorArt(bool general,int encounter){
            if(general)return null;
            return foeArt!=null&&encounter>=0&&encounter<4?foeArt[encounter==1?1:encounter==3?2:0]:null;
        }
        public System.Collections.Generic.IEnumerable<Box> Terrain()
        {
            foreach(var stone in bridgeStones){
                var bounds=stone.bounds;
                yield return new Box(bounds.min.x*100,Math.Max(0,bounds.min.y*100+95),bounds.size.x*100,bounds.size.y*100);
            }
        }
        public void ResetLook(int checkpoint,bool completed)
        {
            LightLamp(1,checkpoint>=1);LightLamp(2,checkpoint>=2);
            Seal(false);returnSeal.enabled=completed;ClearFireMark();weaponGlow.enabled=armorGlow.enabled=false;
        }
        public void LightLamp(int number,bool lit)
        {
            (number==1?firstLamp:secondLamp).color=lit?new Color(1,.87f,.64f):new Color(.65f,.57f,.61f);
        }
        public void Seal(bool active,double left=0,double right=0)
        {
            leftSeal.enabled=rightSeal.enabled=active;
            if(!active)return;
            leftSeal.transform.localPosition=new Vector3((float)(left-40)/100,-.22f,0);
            rightSeal.transform.localPosition=new Vector3((float)(right+40)/100,-.22f,0);
        }
        static readonly float[] SceneryCenters={5.3f,14f,25f,36.8f};
        static readonly float[] SceneryCrossings={10.1f,19.5f,32.2f};
        public void FrameScenery(float cameraX,float cameraY){
            if(scenery==null||scenery.Length!=4)return;
            int from=0;
            while(from<3&&cameraX>=SceneryCrossings[from]+1.2f)from++;
            float fade=from==3?0:Mathf.SmoothStep(0,1,Mathf.InverseLerp(SceneryCrossings[from]-1.2f,SceneryCrossings[from]+1.2f,cameraX));
            for(int i=0;i<scenery.Length;i++){
                var plate=scenery[i];
                plate.enabled=i==from||i==from+1&&fade>0;
                if(!plate.enabled)continue;
                plate.transform.localPosition=new Vector3(cameraX+Mathf.Clamp((SceneryCenters[i]-cameraX)*.16f,-1.45f,1.45f),.12f+cameraY*.84f,0);
                plate.color=new Color(.88f,.84f,.89f,i==from?1:fade);
            }
        }
        public void Animate(Combat sim,Camera camera,bool general,float heat,float weaponAsh)
        {
            float now=Time.unscaledTime;
            var cam=camera.transform.position;
            float cameraX=cam.x*100;
            FrameScenery(cam.x,cam.y);
            if(backdrop)backdrop.localPosition=new Vector3((cameraX-520)*.0012f,0,0);
            generalArmor.SetActive(general);
            if(ash!=null&&embers.gameObject.activeSelf)for(int i=0;i<ash.Length;i++){
                float life=Mathf.Repeat(now*(.12f+(i%5)*.02f)+i*.618f,1);
                float x=cameraX/100+Mathf.Repeat(i*1.77f+life*1.6f,13)-6.5f;
                var mote=ash[i];
                mote.transform.localPosition=new Vector3(x,-1.15f+life*5.4f,0);
                mote.color=new Color(1,.49f+i%4*.10f,.24f,Mathf.Sin(life*Mathf.PI)*(.42f+.30f*heat));
            }
            float pulse=.62f+.27f*Mathf.Sin(now*11);
            weaponGlow.enabled=armorGlow.enabled=heat>0;
            if(heat>0){
                weaponGlow.color=new Color(1,.29f,.07f,pulse*heat);
                armorGlow.color=new Color(1,.51f,.15f,pulse*.85f*heat);
            }
            if(weapon!=null)foreach(var piece in weapon){
                var tint=piece.color;tint.a=1-weaponAsh;piece.color=tint;
            }
            ClearFireMark();
            if(!sim.enemyActive)return;
            for(int i=sim.visuals.Count-1;i>=0;i--){
                var cue=sim.visuals[i];
                if(cue.kind!="fire_warning"&&cue.kind!="fire_burst")continue;
                int age=sim.frame-cue.frame;bool burst=cue.kind=="fire_burst";
                int duration=burst?18:sim.boss.move?.fireDelay??0;
                if(age>=duration||(!burst&&sim.boss.attackId!=cue.attackId))break;
                float life=age/(float)duration;
                if(!burst&&fireWarning&&fireShadow){
                    float x=(float)cue.x/100;
                    fireShadow.transform.localPosition=new Vector3(x,-.89f,0);
                    fireWarning.transform.localPosition=new Vector3(x,-.88f,0);
                    fireWarning.color=new Color(1,.76f+life*.15f,.39f+life*.30f,.77f+life*.23f);
                    fireWarning.enabled=fireShadow.enabled=true;
                    break;
                }
                fireMark.enabled=true;
                fireMark.transform.localPosition=new Vector3((float)cue.x/100,burst?-.61f:-.91f,0);
                var sprite=fireMark.sprite;
                fireMark.transform.localScale=new Vector3(1.36f*sprite.pixelsPerUnit/sprite.rect.width,
                    (burst?.73f*(1-life):.10f)*sprite.pixelsPerUnit/sprite.rect.height,1);
                fireMark.color=new Color(1,burst?.44f:.29f,.11f,burst?1-life:.45f+life*.45f);
                break;
            }
        }
        void ClearFireMark(){
            fireMark.enabled=false;
            if(fireWarning)fireWarning.enabled=false;
            if(fireShadow)fireShadow.enabled=false;
        }
    }
}
