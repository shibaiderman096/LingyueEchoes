using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LingyueEchoes
{
    public partial class LingyueGame
    {
        const string WrathRoam="WRATH_ROUTE",WrathDialogue="WRATH_DIALOGUE",WrathArrive="WRATH_ARRIVE",WrathTransfer="WRATH_TRANSFER";
        const string ShrineEntrance="七罪石碑 · 前往焚心之城",ShrineReturn="返回残月神社";
        const double ShrineStoneX=2150;
        WrathChapter wrath;
        PrologueContent wrathText;
        SpriteRenderer shrineStone;
        int wrathCheckpoint,wrathNext,wrathActive=-1,wrathArea;
        float wrathAreaTime,wrathHintTime,wrathWeaponAsh;
        bool wrathShowOpening;
        string transferScene;
        bool ChapterDialogue=>stage==WrathDialogue;
        bool ChapterBoss=>wrath&&wrathActive==WrathRoute.Fights.Length-1;
        static bool ChapterUnlocked(int slot)=>StorySeen(slot)||SaveValue(WrathRoute.Unlocked,slot)==1||
            (SaveValue(PrologueLayout.ProgressKey,slot)==1&&SaveValue(PrologueLayout.TailCountKey,slot)>=1);
        static bool ChapterCompleted(int slot)=>SaveValue(WrathRoute.Completed,slot)==1;
        static bool ContinueWrath(int slot)=>ChapterUnlocked(slot)&&SaveValue(WrathRoute.Current,slot)==1;
        bool WrathCompleted=>ChapterCompleted(SaveSlot);
        static int SavedTailCount(int slot)=>Math.Max(ChapterCompleted(slot)?2:StorySeen(slot)?1:0,SaveValue(PrologueLayout.TailCountKey,slot));
        string EncounterName=>wrath&&wrathActive>=0?WrathRoute.Fights[wrathActive].Name:"守门人 · 残月的守灯者";
        float ChapterCameraX()=>Mathf.Clamp((float)sim.player.x+118,520,3820);
        void PlaceShrineEntrance()
        {
            shrineStone=PixelWorld.Sprite(world.Find("Props"),"SevenSinsStele","prologue_route/moon-boundary",(float)ShrineStoneX,-95,78,179,8,new Vector2(.5f,0));
            shrineStone.color=new Color(.83f,.67f,.62f);
        }
        void UpdateShrineEntrance(){if(shrineStone)shrineStone.enabled=ChapterUnlocked(SaveSlot);}
        void LoadCampaignScene(string name)
        {
            pendingSaveSlot=SaveSlot;
            SceneManager.LoadScene(name);
        }
        void TransferTo(string name)
        {
            if(stage==WrathTransfer)return;
            ClearInput();
            transferScene=name;SetStage(WrathTransfer);
        }
        void UpdateTransfer()
        {
            if(stageTime<.56f||transferScene==null)return;
            string target=transferScene;transferScene=null;
            bool entering=target==WrathRoute.Scene;
            WriteSave(WrathRoute.Current,entering?1:0);
            if(entering)WriteSave(WrathRoute.Unlocked,1);
            else WriteSave(PrologueLayout.CheckpointKey,1);
            PlayerPrefs.Save();
            LoadCampaignScene(target);
        }
        void ChapterSay(string id,bool automatic=true)
        {
            var script=Array.Find(wrathText.scripts,s=>s.id==id);
            if(script==null)throw new InvalidOperationException("Missing chapter dialogue "+id);
            speech.Play(script,automatic);
        }
        bool HandleChapterSpeech(string id)
        {
            if(stage==WrathDialogue){
                if(id=="stele"){TransferTo(WrathRoute.Scene);return true;}
                if(id=="wrathEnter"){SetStage(WrathRoam);return true;}
                if(id=="wrathBossPre"||id=="wrathRetry"){StartWrathEncounter(WrathRoute.Fights.Length-1);return true;}
            }
            if(wrath&&stage=="TAIL_DIALOGUE"&&id=="wrathBossAfter"){BeginTailHandoff("fenxin");return true;}
            if(wrath&&stage=="TAIL_EPILOGUE"&&id=="wrathEpilogue"){CompleteWrath();return true;}
            return false;
        }
        string ChapterInteraction()
        {
            if(!wrath){
                if(stage=="GATE_READY"&&ChapterUnlocked(SaveSlot)&&Math.Abs(sim.player.x-ShrineStoneX)<113)return ShrineEntrance;
                return null;
            }
            if(stage!=WrathRoam)return null;
            double x=sim.player.x;
            if(WrathCompleted&&(Math.Abs(x-WrathRoute.Portal)<106||x<180))return ShrineReturn;
            if(wrathNext>=WrathRoute.Fights.Length-1&&x>=WrathRoute.Fights[WrathRoute.Fights.Length-1].Trigger-90&&x<4040)
                return WrathCompleted?"再次挑战赤绫":"进入将军府 · 与赤绫交谈";
            if(Math.Abs(x-WrathRoute.FirstLamp)<84||Math.Abs(x-WrathRoute.SecondLamp)<84)return "点亮存档灯";
            return null;
        }
        bool HandleChapterInteraction(string action)
        {
            if(action==ShrineEntrance){
                ClearInput();SetStage(WrathDialogue);ChapterSay("stele");return true;
            }
            if(action==ShrineReturn){TransferTo("Prologue");return true;}
            if(action=="再次挑战赤绫"||action=="进入将军府 · 与赤绫交谈"){
                ClearInput();
                wrathWeaponAsh=0;
                var general=WrathRoute.Fights[WrathRoute.Fights.Length-1];
                sim.ResetBoss(general.EnemyX,general.Hp,general.Pattern);sim.boss.face=-1;
                sim.player.face=1;sim.player.state="idle";sim.player.move=null;sim.player.age=0;
                SetStage(WrathDialogue);ChapterSay(WrathCompleted?"wrathRetry":"wrathBossPre");return true;
            }
            if(wrath&&action=="点亮存档灯"){
                SaveWrathLamp(Math.Abs(sim.player.x-WrathRoute.SecondLamp)<84?2:1);return true;
            }
            return false;
        }
        void PrepareWrath(bool onEntry)
        {
            int saved=Mathf.Clamp(SaveValue(WrathRoute.Checkpoint,SaveSlot),0,2);
            wrathCheckpoint=WrathCompleted?2:saved;
            wrathNext=WrathRoute.EncounterAfter(wrathCheckpoint);
            wrathActive=-1;
            PrepareCampaignCombat();
            sim.terrain.AddRange(wrath.Terrain());
            sim.boundsMin=0;sim.boundsMax=WrathCompleted?WrathRoute.End:4020;
            sim.player.x=WrathRoute.Spawn(wrathCheckpoint);
            sim.boss.x=WrathRoute.Fights[wrathNext].EnemyX;
            cameraX=ChapterCameraX();
            wrathArea=WrathRoute.Area(sim.player.x);wrathAreaTime=0;wrathHintTime=0;wrathWeaponAsh=0;checkpointToast=0;
            wrath.ResetLook(wrathCheckpoint,WrathCompleted);
            wrathShowOpening=onEntry&&wrathCheckpoint==0;
            SetStage(WrathArrive);SetMusic("pilgrim_stage_2");
        }
        void UpdateWrathArrival()
        {
            if(stageTime<(wrathShowOpening?2.65f:1.1f))return;
            if(wrathShowOpening){SetStage(WrathDialogue);ChapterSay("wrathEnter");}
            else SetStage(WrathRoam);
        }
        void SaveWrathLamp(int lamp)
        {
            if(lamp<=wrathCheckpoint)return;
            wrathCheckpoint=lamp;
            wrath.LightLamp(lamp,true);
            sim.player.hp=sim.player.maxHp;sim.player.stamina=sim.PlayerMaxStamina;
            checkpointToast=2.5f;
            WriteSave(WrathRoute.Checkpoint,lamp);
            WriteSave(WrathRoute.Current,1);
            PlayerPrefs.Save();
            UiSound("ui_confirm");
        }
        void UpdateWrathRoute(float dt)
        {
            double x=sim.player.x;
            if(x>=WrathRoute.FirstLamp-45&&wrathNext>=1)SaveWrathLamp(1);
            if(x>=WrathRoute.SecondLamp-45&&wrathNext>=4)SaveWrathLamp(2);
            int area=WrathRoute.Area(x);
            if(wrathArea!=area){wrathArea=area;wrathAreaTime=2.3f;}
            else wrathAreaTime=Mathf.Max(0,wrathAreaTime-dt);
            if(wrathNext>=WrathRoute.Fights.Length)return;
            var next=WrathRoute.Fights[wrathNext];
            if(wrathNext<4&&x>=next.Trigger)StartWrathEncounter(wrathNext);
        }
        void StartWrathEncounter(int index)
        {
            wrathActive=index;
            var e=WrathRoute.Fights[index];
            ClearInput();speech.Reset();
            sim.boundsMin=e.Min;sim.boundsMax=e.Max;
            sim.ResetBoss(e.EnemyX,e.Hp,e.Pattern);
            sim.boss.face=-1;
            sim.player.x=Math.Min(sim.player.x,e.Trigger);
            if(ChapterBoss)sim.player.x=e.Min+142;
            sim.player.y=sim.player.vy=0;sim.player.face=1;sim.player.move=null;
            sim.player.state="idle";sim.player.age=sim.player.combo=sim.player.invuln=0;
            bossHealthTrail.Reset(1);postureTrail.Reset(0);phase2Spoken=false;wrathWeaponAsh=0;
            wrath.Seal(true,e.Min,e.Max);effects.Clear();
            clock=new FixedClock(sim);
            sim.enemyActive=sim.ai=!ChapterBoss;
            wrathHintTime=ChapterBoss?0:4.4f;
            if(ChapterBoss){cameraX=(float)((e.Min+e.Max)*.5);SetStage("GUARDIAN_INTRO");}
            else SetStage("GUARDIAN_FIGHT");
        }
        void FinishWrathEncounter()
        {
            bool boss=ChapterBoss;
            StopSounds();wrath.Seal(false);
            sim.boundsMin=0;sim.boundsMax=WrathCompleted?WrathRoute.End:4020;
            sim.visuals.Clear();effects.Clear();
            wrathNext=wrathActive+1;
            if(!boss){
                wrathActive=-1;
                var next=WrathRoute.Fights[wrathNext];
                sim.ResetBoss(next.EnemyX,next.Hp,next.Pattern);
                SetStage(WrathRoam);return;
            }
            SetMusic("pilgrim_stage_2");
            if(WrathCompleted){wrathActive=-1;SetStage(WrathRoam);ChapterSay("wrathRematch");return;}
            var p=sim.player;var b=sim.boss;
            p.state="idle";p.move=null;p.age=0;p.y=p.vy=0;b.y=b.vy=0;
            p.face=b.x>=p.x?1:-1;b.face=-p.face;
            SetStage("TAIL_DIALOGUE");ChapterSay("wrathBossAfter");
        }
        void CompleteWrath()
        {
            WriteSave(PrologueLayout.TailCountKey,Math.Max(2,sim.TailUpgrades));
            WriteSave(WrathRoute.Unlocked,1);
            WriteSave(WrathRoute.Current,1);
            WriteSave(WrathRoute.Checkpoint,2);
            WriteSave(WrathRoute.Defeated,1);
            WriteSave(WrathRoute.Completed,1);
            PlayerPrefs.Save();
            wrathCheckpoint=2;wrathActive=-1;
            wrath.returnSeal.enabled=true;
            sim.boundsMax=WrathRoute.End;
            wrathAreaTime=0;
            SetStage(WrathRoam);
        }
        void DrawWrathAtmosphere(float dt)
        {
            bool general=ChapterBoss||ChapterDialogue&&speech.Id!="wrathEnter";
            float heat=ChapterBoss&&sim.phase==2
                ?stage=="GUARDIAN_FIGHT"?1:stage=="GUARDIAN_DEFEAT"?1-Ramp(stageTime,0,.85f):0
                :0;
            if(speech.Cue=="weaponAsh"||wrathWeaponAsh>0)
                wrathWeaponAsh=Mathf.Min(1,wrathWeaponAsh+dt*1.7f);
            wrath.Animate(sim,cameraX,general,heat,wrathWeaponAsh);
        }
        bool ChapterStageGUI()
        {
            if(stage==WrathTransfer){
                Fill(TailCanvas,WithAlpha(Color.black,Ramp(stageTime,0,.54f)));
                TailScreenMargins(WithAlpha(Color.black,Ramp(stageTime,0,.54f)));return true;
            }
            if(stage==WrathDialogue){StoryDialogueGUI(wrath?"赤绫":"守灯人");return true;}
            if(stage!=WrathArrive)return false;
            float alpha=1-Ramp(stageTime,.06f,.83f);
            Fill(TailCanvas,WithAlpha(Color.black,alpha));TailScreenMargins(MoonNight);
            if(wrathShowOpening){
                float title=Ramp(stageTime,.28f,.95f)*(1-Ramp(stageTime,2.02f,2.65f));
                Text(new Rect(290,190,700,64),WrathRoute.Title,44,WithAlpha(MoonIvory,title),TextAnchor.MiddleCenter);
                Text(new Rect(405,258,470,36),"暴怒 · 焚心",25,WithAlpha(MoonGold,title),TextAnchor.MiddleCenter);
            }
            return true;
        }
        void ChapterInteractionGUI(string action)
        {
            var rect=new Rect(396,522,488,44);
            string key=showKeyHints?ControlPrompts.Key("interact",device):null;
            if(Button(rect,action,true,key:key))Interact();
        }
        void ChapterFieldGUI()
        {
            if(!wrath){
                if(WrathCompleted&&stage=="GATE_READY"&&Math.Abs(sim.player.x-ShrineStoneX)<310)
                    Text(new Rect(420,115,440,31),TailProgress(sim.TailUpgrades),17,MoonGold,TextAnchor.MiddleCenter);
                return;
            }
            if(wrathAreaTime>0)
                Text(new Rect(450,102,380,34),WrathRoute.Areas[wrathArea],21,WithAlpha(MoonGold,Mathf.Min(1,wrathAreaTime*2)),TextAnchor.MiddleCenter);
            if(checkpointToast>0)
                Text(new Rect(460,140,360,34),"存档灯 · 已记录",19,MoonGold,TextAnchor.MiddleCenter);
            if(wrathActive>=0&&wrathHintTime>0){
                RoundedFill(new Rect(394,198,492,39),Ink,7);
                Text(new Rect(404,198,472,39),WrathRoute.Fights[wrathActive].Hint,17,Ivory,TextAnchor.MiddleCenter);
            }
            if(WrathCompleted&&stage==WrathRoam&&sim.player.x>3360){
                Text(new Rect(375,135,530,38),"第一章完成 · 焚心余烬",22,MoonGold,TextAnchor.MiddleCenter);
                Text(new Rect(403,174,474,28),"沿将军府向东 · 返回残月神社",16,Ivory,TextAnchor.MiddleCenter);
            }
        }
    }
}
