using System;
using UnityEngine;

namespace LingyueEchoes
{
    public static class WrathRoute
    {
        public const string Scene="Chapter01_Wrath";
        public const string Unlocked="lingyue.wrath.unlocked",Current="lingyue.chapter",
            Checkpoint="lingyue.wrath.checkpoint",Defeated="lingyue.wrath.firstDefeat",Completed="lingyue.wrath.complete";
        public static readonly string[] SaveKeys={Unlocked,Current,Checkpoint,Defeated,Completed};
        public const double BoundsMin=3210,BoundsMax=4290,Entrance=3340,DoorX=Entrance;
        public const double Start=Entrance,FirstLamp=Entrance,SecondLamp=Entrance,End=BoundsMax,Portal=Entrance;
        public const string Title="第一章 · 焚心之城", NextTitle="第二章 · 贪婪 · 囚金";
        public static readonly string[] Areas={"将军府内殿"};
        public static int Area(double x)=>0;
        // All legacy route checkpoints now resume at the general's entrance.
        public static int EncounterAfter(int checkpoint)=>0;
        public static double Spawn(int checkpoint)=>Entrance;
        public static Box[] Bridge()=>Array.Empty<Box>();
        public sealed class Encounter
        {
            public readonly string Name,Hint;
            public readonly double Trigger,Min,Max,EnemyX,Hp;
            public readonly OpponentPattern Pattern;
            public Encounter(string name,string hint,double trigger,double min,double max,double enemyX,double hp,OpponentPattern pattern)
            {Name=name;Hint=hint;Trigger=trigger;Min=min;Max=max;EnemyX=enemyX;Hp=hp;Pattern=pattern;}
        }
        static OpponentPattern general;
        public static OpponentPattern General=>general??(general=BossBehaviorCatalog.Pattern("wrath-general"));
        static Encounter[] fights;
        public static Encounter[] Fights=>fights??(fights=CreateFights());
        static Encounter[] CreateFights()=>new[]{
            new Encounter("焚城将军 · 赤绫","横扫之后留意停顿，突进落空后正是反击的机会。",3475,3210,4130,3830,870,General)
        };
    }

    public interface IWrathPoseSource
    {
        Sprite Pose(Fighter fighter,string scripted,int cell,double? seconds);
    }

    // Scene references are serialized so the level can be edited without rebuilding runtime geometry.
    public sealed class WrathChapter:MonoBehaviour
    {
        public Transform world,embers,backdrop;
        public SpriteRenderer[] bridgeStones;
        public SpriteRenderer[] scenery;
        public Transform exteriorArrivalAnchor;
        public Vector3 ExteriorViewCenter {get;private set;}=new Vector3(29.5f,.65f,-10);
        public float ExteriorViewSize {get;private set;}=2.4f;
        bool exteriorViewCaptured;
        public void CaptureExteriorView(Camera camera){
            if(exteriorViewCaptured||!camera)return;
            ExteriorViewCenter=camera.transform.position;
            ExteriorViewSize=Mathf.Max(.01f,camera.orthographicSize);
            exteriorViewCaptured=true;
        }
        public SpriteRenderer exteriorScenery;
        public SpriteRenderer exteriorCanopy;
        public SpriteRenderer[] exteriorPillars;
        public SpriteRenderer fireMark,fireShadow,fireWarning,entranceDoor;
        public Sprite[] entranceDoorFrames;
        [Range(0,1)] public float exteriorDoorSeamNormalized=.503125f;
        public MeshRenderer exteriorDoorDarkness;
        public SpriteRenderer exteriorCheckpointLamp;
        public float ExteriorDoorSeamX {
            get {
                if(!entranceDoor||!entranceDoor.sprite)return (float)WrathRoute.DoorX/100;
                var sprite=entranceDoorFrames!=null&&entranceDoorFrames.Length>0&&entranceDoorFrames[0]?entranceDoorFrames[0]:entranceDoor.sprite;
                float x=(sprite.rect.width*exteriorDoorSeamNormalized-sprite.pivot.x)/sprite.pixelsPerUnit;
                if(entranceDoor.flipX)x=-x;
                return entranceDoor.transform.TransformPoint(new Vector3(x,0,0)).x;
            }
        }
        public float DoorOpening {get;private set;}
        public void SetDoorOpening(float progress){
            DoorOpening=Mathf.Clamp01(progress);
            if(entranceDoor&&entranceDoorFrames!=null&&entranceDoorFrames.Length>0)
                entranceDoor.sprite=entranceDoorFrames[Mathf.Min(entranceDoorFrames.Length-1,
                    Mathf.FloorToInt(DoorOpening*entranceDoorFrames.Length))];
        }
        public bool IsInside {get;private set;}
        public void SetInside(bool inside){
            IsInside=inside;
            if(exteriorScenery)exteriorScenery.enabled=!inside;
            if(exteriorCanopy)exteriorCanopy.enabled=!inside;
            if(exteriorPillars!=null)foreach(var pillar in exteriorPillars)if(pillar)pillar.enabled=!inside;
            if(scenery!=null)foreach(var plate in scenery)if(plate)plate.enabled=inside;
            if(entranceDoor)entranceDoor.enabled=!inside;
            if(exteriorDoorDarkness)exteriorDoorDarkness.enabled=!inside;
            if(exteriorCheckpointLamp)exteriorCheckpointLamp.enabled=!inside;
            if(!inside)SetDoorOpening(0);
            SetAmbientParticles(SceneParticleSettings.Enabled);
        }
        SpriteRenderer[] ash;
        WrathGeneralArt generalArt;
        void OnEnable(){SceneParticleSettings.Changed+=SetAmbientParticles;SetAmbientParticles(SceneParticleSettings.Enabled);}
        void OnDisable(){SceneParticleSettings.Changed-=SetAmbientParticles;}
        void SetAmbientParticles(bool enabled){if(embers)embers.gameObject.SetActive(IsInside&&enabled);}
        void Awake()
        {
            if(embers)ash=embers.GetComponentsInChildren<SpriteRenderer>(true);
            generalArt=new WrathGeneralArt();
        }
        public IWrathPoseSource UseActorArt(bool general,int encounter)=>general?generalArt:null;
        public System.Collections.Generic.IEnumerable<Box> Terrain()
        {
            foreach(var stone in bridgeStones){
                var bounds=stone.bounds;
                yield return new Box(bounds.min.x*100,Math.Max(0,bounds.min.y*100+95),bounds.size.x*100,bounds.size.y*100);
            }
        }
        public void ResetLook()
        {
            ClearFireMark();
        }
        public void Animate(Combat sim,Camera camera,bool general,float heat,float weaponAsh)
        {
            float now=Time.unscaledTime;
            var cam=camera.transform.position;
            float cameraX=cam.x*100;
            if(ash!=null&&embers.gameObject.activeSelf)for(int i=0;i<ash.Length;i++){
                float life=Mathf.Repeat(now*(.12f+(i%5)*.02f)+i*.618f,1);
                float x=cameraX/100+Mathf.Repeat(i*1.77f+life*1.6f,13)-6.5f;
                var mote=ash[i];
                mote.transform.localPosition=new Vector3(x,-1.15f+life*5.4f,0);
                mote.color=new Color(1,.49f+i%4*.10f,.24f,Mathf.Sin(life*Mathf.PI)*(.42f+.30f*heat));
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
