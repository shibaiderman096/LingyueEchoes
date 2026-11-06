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
        },32,2.1,138);
        static readonly OpponentPattern Hunter=new OpponentPattern(new[]{
            Move("赤焰突进","thrust",35,10,34,143,86,19,26,true,10),
            Move("掠火回刃","slash",23,5,32,130,98,13,18)
        },25,4.7,231);
        static readonly OpponentPattern Elite=new OpponentPattern(new[]{
            Move("禁卫双连斩","double",28,27,36,159,110,16,24,true,pulses:new[]{0,22}),
            Move("焚石重踏","slam",47,7,47,208,48,24,35,false)
        },25,3.0,163);
        public static readonly OpponentPattern General=new OpponentPattern(new[]{
            Move("断垣横扫","slash",29,6,29,179,111,19,23),
            Move("破军突刺","thrust",35,9,30,160,96,22,28,true,8.5),
            Move("残城双斩","double",25,26,36,164,110,15,21,true,pulses:new[]{0,21}),
            Move("裂阶重踏","slam",43,7,43,226,45,24,34,false),
            Move("燎城跃击","leap",40,7,38,172,165,24,31),
            Move("焚关重斩","spin",49,7,43,197,142,28,37,true)
        },19,3.4,191,new[]{
            Move("余焰横扫","slash",26,6,35,179,111,19,23,fire:30),
            Move("追火突刺","thrust",28,9,31,166,96,22,28,true,9),
            Move("连烽斩","double",23,26,34,164,110,15,21,true,pulses:new[]{0,21}),
            Move("地火重踏","slam",36,7,47,226,45,24,34,false,fire:33),
            Move("浴火跃击","leap",34,7,36,172,165,24,31),
            Move("焚城重斩","spin",43,7,49,197,142,28,37,true,fire:32)
        },10);
        public static readonly Encounter[] Fights={
            new Encounter("焦土残兵","刀抬起后再弹反。它的第一刀并不快。",310,215,795,686,64,Soldier),
            new Encounter("赤焰游猎","留意突进前的停顿，闪开刀锋后再反击。",1515,1440,1835,1745,86,Hunter),
            new Encounter("焦土残兵","趁横斩收刀时贴近。",2030,1910,2370,2290,67,Soldier),
            new Encounter("焚城禁卫","双连斩之间还有一刀；重踏可跳过或闪开。",2540,2400,2970,2840,158,Elite),
            new Encounter("焚城将军 · 赤绫","地面亮起火纹时，余焰还未爆发。",3475,3210,4130,3830,595,General)
        };
    }

    public sealed class WrathGeneralArt
    {
        public const string Resource="WrathGeneral";
        const float PixelsPerUnit=310;
        // Atlas coordinates are top-down. Each frame is anchored between the boots.
        static readonly Vector2Int[] Anchors={new(232,448),new(736,448),new(1214,448),new(218,952),new(752,952),new(1245,952)};
        static readonly Vector2Int[] Blades={new(325,86),new(905,140),new(1482,318),new(469,784),new(948,777),new(1490,932)};
        static readonly Vector2Int[] Armor={new(227,230),new(737,234),new(1226,266),new(214,783),new(742,747),new(1248,818)};
        readonly Sprite[] poses;
        public readonly Sprite LooseBlade;
        int current;
        public WrathGeneralArt(Texture2D atlas){
            poses=new[]{Slice(atlas,32,14,366,454,0),Slice(atlas,512,53,950,454,1),Slice(atlas,997,142,1536,454,2),
                Slice(atlas,18,650,502,961,3),Slice(atlas,545,565,983,961,4),Slice(atlas,1028,657,1340,961,5)};
            LooseBlade=Slice(atlas,1340,892,1529,960,5,new Vector2Int(1340,957));
        }
        static Sprite Slice(Texture2D t,int left,int top,int right,int bottom,int pose,Vector2Int? origin=null){
            Vector2Int foot=origin??Anchors[pose];float w=right-left,h=bottom-top;
            return Sprite.Create(t,new Rect(left,t.height-bottom,w,h),new Vector2((foot.x-left)/w,(bottom-foot.y)/h),PixelsPerUnit,0,SpriteMeshType.FullRect);
        }
        public Sprite Pose(Fighter fighter,string scripted,int cell,double? seconds){
            var move=fighter.move;int tick=Math.Max(0,fighter.age-1);
            if(scripted=="broken")current=seconds>=.26?5:4;
            else if(scripted=="slash")current=cell<4?1:cell<6?2:4;
            else if(fighter.hp<=0||fighter.state=="broken")current=5;
            else if(move!=null){
                int strike=move.pose=="thrust"?3:2;
                current=tick<move.wind?1:tick<move.wind+move.active?strike:4;
                if(move.pulses!=null&&move.pulses.Length>1){
                    int follow=move.wind+move.pulses[1];
                    if(tick>=follow)current=tick<follow+5?strike:4;
                    else if(tick>=move.wind+5)current=1;
                }
            }else current=fighter.state=="phase"?1:fighter.state=="run"||fighter.state=="stagger"?4:0;
            return poses[current];
        }
        public bool Kneeling=>current==5;
        public Vector2 BladePosition=>Position(Blades[current]);
        public Vector2 ArmorPosition=>Position(Armor[current]);
        public Vector2 LooseBladePosition=>Position(new Vector2Int(1340,957));
        Vector2 Position(Vector2Int p)=>new Vector2(p.x-Anchors[current].x,Anchors[current].y-p.y)/PixelsPerUnit;
        public void Dispose(){foreach(var pose in poses)UnityEngine.Object.Destroy(pose);UnityEngine.Object.Destroy(LooseBlade);}
    }

    // Scene references are serialized so the level can be edited without rebuilding runtime geometry.
    public sealed class WrathChapter:MonoBehaviour
    {
        public Transform world,embers,backdrop;
        public GameObject generalArmor;
        public SpriteRenderer[] bridgeStones;
        public SpriteRenderer firstLamp,secondLamp,leftSeal,rightSeal,returnSeal;
        public SpriteRenderer fireMark,weaponGlow,armorGlow;
        SpriteRenderer[] ash,weapon;
        SpriteRenderer looseBlade;
        WrathGeneralArt generalArt;
        void Awake()
        {
            if(embers)ash=embers.GetComponentsInChildren<SpriteRenderer>();
            if(generalArmor){
                weapon=generalArmor.GetComponentsInChildren<SpriteRenderer>(true)
                    .Where(r=>r.name.StartsWith("Polearm",StringComparison.Ordinal)).ToArray();
                var atlas=Resources.Load<Texture2D>(WrathGeneralArt.Resource);
                if(atlas&&atlas.width==1536&&atlas.height==1024){
                    generalArt=new WrathGeneralArt(atlas);
                    var blade=new GameObject("赤绫 · 将军刀灰烬");blade.transform.SetParent(generalArmor.transform,false);
                    looseBlade=blade.AddComponent<SpriteRenderer>();looseBlade.sprite=generalArt.LooseBlade;looseBlade.sortingOrder=11;looseBlade.enabled=false;
                    foreach(var r in generalArmor.GetComponentsInChildren<SpriteRenderer>(true))
                        if(r!=armorGlow&&r!=weaponGlow)r.enabled=false;
                }
            }
        }
        void OnDestroy()=>generalArt?.Dispose();
        public WrathGeneralArt UseGeneralArt(bool general)=>general?generalArt:null;
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
            Seal(false);returnSeal.enabled=completed;fireMark.enabled=false;weaponGlow.enabled=armorGlow.enabled=false;
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
        public void Animate(Combat sim,float cameraX,bool general,float heat,float weaponAsh)
        {
            float now=Time.unscaledTime;
            if(backdrop)backdrop.localPosition=new Vector3((cameraX-520)*.0012f,0,0);
            generalArmor.SetActive(general);
            if(ash!=null)for(int i=0;i<ash.Length;i++){
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
            if(general&&generalArt!=null){
                weaponGlow.transform.localPosition=generalArt.BladePosition;
                armorGlow.transform.localPosition=generalArt.ArmorPosition;
                looseBlade.enabled=generalArt.Kneeling&&weaponAsh<1;
                if(looseBlade.enabled){looseBlade.transform.localPosition=generalArt.LooseBladePosition;looseBlade.color=new Color(1,1,1,1-weaponAsh);}
            }else if(weapon!=null)foreach(var piece in weapon){
                var tint=piece.color;tint.a=1-weaponAsh;piece.color=tint;
            }
            fireMark.enabled=false;
            if(!sim.enemyActive)return;
            for(int i=sim.visuals.Count-1;i>=0;i--){
                var cue=sim.visuals[i];
                if(cue.kind!="fire_warning"&&cue.kind!="fire_burst")continue;
                int age=sim.frame-cue.frame;bool burst=cue.kind=="fire_burst";
                int duration=burst?18:sim.boss.move?.fireDelay??0;
                if(age>=duration||(!burst&&sim.boss.attackId!=cue.attackId))break;
                float life=age/(float)duration;
                fireMark.enabled=true;
                fireMark.transform.localPosition=new Vector3((float)cue.x/100,burst?-.61f:-.91f,0);
                var sprite=fireMark.sprite;
                fireMark.transform.localScale=new Vector3(1.36f*sprite.pixelsPerUnit/sprite.rect.width,
                    (burst?.73f*(1-life):.10f)*sprite.pixelsPerUnit/sprite.rect.height,1);
                fireMark.color=new Color(1,burst?.44f:.29f,.11f,burst?1-life:.45f+life*.45f);
                break;
            }
        }
    }
}
