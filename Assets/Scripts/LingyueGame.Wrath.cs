using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LingyueEchoes
{
    public partial class LingyueGame
    {
        const string WrathRoam="WRATH_ROUTE",WrathDialogue="WRATH_DIALOGUE",WrathArrive="WRATH_ARRIVE",WrathTransfer="WRATH_TRANSFER",WrathEnter="WRATH_ENTER";
        const float WrathDoorOpenEnd=.85f,WrathWalkStart=.65f,WrathWalkEnd=1.85f,WrathFadeStart=1.7f,WrathInsideTime=2.15f,WrathFadeEnd=2.85f,WrathEnterDuration=3f;
        const float WrathInsideX=3500,WrathOutsideCameraX=3250,WrathOutsideZoom=1.5f;
        const string ShrineEntrance="月门 · 前往焚心之城",ShrineReturn="返回残月神社";
        WrathChapter wrath;
        PrologueContent wrathText;
        SpriteRenderer shrineGate;
        int wrathCheckpoint,wrathNext,wrathActive=-1,wrathArea;
        float wrathAreaTime,wrathHintTime,wrathWeaponAsh;
        bool wrathShowOpening,wrathEntered;
        float wrathEnterX,wrathEnterCameraX,wrathNextStep;
        string transferScene;
        bool ChapterDialogue=>stage==WrathDialogue;
        bool ChapterBoss=>wrath&&wrathActive==WrathRoute.Fights.Length-1;
        bool ChapterGeneralVisible=>ChapterBoss||ChapterDialogue&&speech.Id!="wrathEnter"||stage==WrathEnter&&wrathEntered;
        static bool ChapterUnlocked(int slot)=>StorySeen(slot)||SaveValue(WrathRoute.Unlocked,slot)==1||
            (SaveValue(PrologueLayout.ProgressKey,slot)==1&&SaveValue(PrologueLayout.TailCountKey,slot)>=1);
        static bool ChapterCompleted(int slot)=>SaveValue(WrathRoute.Completed,slot)==1;
        static bool ContinueWrath(int slot)=>ChapterUnlocked(slot)&&SaveValue(WrathRoute.Current,slot)==1;
        bool WrathCompleted=>ChapterCompleted(SaveSlot);
        static int SavedTailCount(int slot)=>Math.Max(ChapterCompleted(slot)?2:StorySeen(slot)?1:0,SaveValue(PrologueLayout.TailCountKey,slot));
        string EncounterName=>wrath&&wrathActive>=0?WrathRoute.Fights[wrathActive].Name:"守门人 · 残月的守灯者";
        float ChapterCameraX()=>wrath&&!wrath.IsInside?WrathOutsideCameraX:Mathf.Clamp((float)sim.player.x+118,(float)WrathRoute.BoundsMin+520,(float)WrathRoute.BoundsMax-470);
        void PlaceShrineEntrance()
        {
            shrineGate=PixelWorld.PlacePrologueGate(world.Find("Props"));
        }
        void UpdateShrineEntrance(){if(shrineGate)shrineGate.color=ChapterUnlocked(SaveSlot)?Color.white:new Color(.85f,.81f,.78f);}
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
                if(stage=="GATE_READY"&&ChapterUnlocked(SaveSlot)&&Math.Abs(sim.player.x-PrologueLayout.MoonGateX)<=PrologueLayout.MoonGateWidth/2)return ShrineEntrance;
                return null;
            }
            if(stage!=WrathRoam)return null;
            double x=sim.player.x;
            if(x<=WrathRoute.Portal-70)return ShrineReturn;
            if(Math.Abs(x-WrathRoute.DoorX)<=130)
                return WrathCompleted?"再次挑战赤绫":"进入将军府 · 与赤绫交谈";
            return null;
        }
        bool HandleChapterInteraction(string action)
        {
            if(action==ShrineEntrance){
                ClearInput();SetStage(WrathDialogue);ChapterSay("stele");return true;
            }
            if(action==ShrineReturn){TransferTo("Prologue");return true;}
            if(action=="再次挑战赤绫"||action=="进入将军府 · 与赤绫交谈"){
                if(stage!=WrathRoam)return true;
                BeginWrathEntrance();return true;
            }
            return false;
        }
        void BeginWrathEntrance()
        {
            ClearInput();speech.Reset();sim.enemyActive=sim.ai=false;
            wrathEntered=false;wrathEnterX=(float)sim.player.x;wrathEnterCameraX=cameraX;wrathNextStep=WrathWalkStart;
            wrathWeaponAsh=0;wrath.SetInside(false);wrath.SetDoorOpening(0);
            sim.player.face=1;sim.player.state="idle";sim.player.move=null;sim.player.age=0;sim.player.y=sim.player.vy=0;
            var general=WrathRoute.Fights[WrathRoute.Fights.Length-1];
            sim.ResetBoss(general.EnemyX,general.Hp,general.Pattern);sim.boss.face=-1;
            SetStage(WrathEnter);UiSound("guard");
        }
        void UpdateWrathEntrance()
        {
            sim.axis=0;sim.enemyActive=sim.ai=false;
            wrath.SetDoorOpening(Ramp(stageTime,0,WrathDoorOpenEnd));
            if(!wrathEntered){
                float walk=Ramp(stageTime,WrathWalkStart,WrathWalkEnd);
                sim.player.x=Mathf.Lerp(wrathEnterX,(float)WrathRoute.DoorX+142,walk);
                sim.player.y=30*walk;sim.player.vy=0;
                if(stageTime>=wrathNextStep&&stageTime<=WrathWalkEnd){UiSound("step");wrathNextStep+=.33f;}
                if(stageTime>=WrathInsideTime){
                    wrathEntered=true;wrath.SetInside(true);sim.boundsMax=WrathRoute.BoundsMax;
                    sim.player.x=WrathInsideX;sim.player.y=sim.player.vy=0;
                }
            }
            if(stageTime<WrathEnterDuration)return;
            sim.player.state="idle";sim.player.age=0;
            SetStage(WrathDialogue);ChapterSay(WrathCompleted?"wrathRetry":"wrathBossPre");
        }
        void WrathEntranceCamera(out float zoom,out float focusY)
        {
            float travel=Ease(Ramp(stageTime,.25f,WrathInsideTime));
            float settle=Ease(Ramp(stageTime,WrathInsideTime,WrathEnterDuration));
            float thresholdCamera=(float)WrathRoute.DoorX-75;
            cameraX=stageTime<WrathInsideTime?Mathf.Lerp(wrathEnterCameraX,thresholdCamera,travel):Mathf.Lerp(thresholdCamera,ChapterCameraX(),settle);
            zoom=stageTime<WrathInsideTime?Mathf.Lerp(WrathOutsideZoom,1.62f,travel):Mathf.Lerp(1.62f,1,settle);
            focusY=18*travel*(1-settle);
        }
        void PrepareWrath(bool onEntry)
        {
            fallenArrival=!onEntry;
            int saved=Mathf.Clamp(SaveValue(WrathRoute.Checkpoint,SaveSlot),0,2);
            wrathCheckpoint=saved;
            wrathNext=WrathRoute.EncounterAfter(wrathCheckpoint);
            wrathActive=-1;
            PrepareCampaignCombat();
            sim.terrain.AddRange(wrath.Terrain());
            sim.boundsMin=WrathRoute.BoundsMin;sim.boundsMax=WrathRoute.DoorX+80;
            sim.player.x=WrathRoute.Spawn(wrathCheckpoint);
            sim.boss.x=WrathRoute.Fights[wrathNext].EnemyX;
            wrath.SetInside(false);wrath.SetDoorOpening(0);wrathEntered=false;
            cameraX=ChapterCameraX();
            wrathArea=WrathRoute.Area(sim.player.x);wrathAreaTime=0;wrathHintTime=0;wrathWeaponAsh=0;checkpointToast=0;
            wrath.ResetLook(wrathCheckpoint,WrathCompleted);
            wrathShowOpening=onEntry&&wrathCheckpoint==0;
            SaveWrathLamp(2);
            SetStage(WrathArrive);SetMusic("pilgrim_stage_2");
        }
        void UpdateWrathArrival()
        {
            if(stageTime<Mathf.Max(wrathShowOpening?2.65f:1.1f,.42f+ArrivalRiseDuration))return;
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
            // The entrance is the sole checkpoint; combat begins only through its interaction.
            wrathAreaTime=Mathf.Max(0,wrathAreaTime-dt);
        }
        void StartWrathEncounter(int index)
        {
            wrathActive=index;
            var e=WrathRoute.Fights[index];
            ClearInput();speech.Reset();
            sim.boundsMin=e.Min;sim.boundsMax=e.Max;
            sim.ResetBoss(e.EnemyX,e.Hp,e.Pattern);
            sim.boss.face=-1;
            sim.player.x=ChapterBoss?Math.Max(e.Min+80,Math.Min(sim.player.x,e.Max-80)):Math.Min(sim.player.x,e.Trigger);
            sim.player.y=sim.player.vy=0;sim.player.face=1;sim.player.move=null;
            sim.player.state="idle";sim.player.age=sim.player.combo=sim.player.invuln=0;
            bossHealthTrail.Reset(1);postureTrail.Reset(0);phase2Spoken=false;wrathWeaponAsh=0;
            wrath.Seal(true,e.Min,e.Max);effects.Clear();
            clock=new FixedClock(sim);
            sim.enemyActive=sim.ai=false;
            wrathHintTime=0;
            SetStage("GUARDIAN_INTRO");
        }
        void FinishWrathEncounter()
        {
            StopSounds();wrath.Seal(false);
            sim.boundsMin=WrathRoute.BoundsMin;sim.boundsMax=WrathRoute.BoundsMax;
            sim.visuals.Clear();effects.Clear();
            wrathNext=wrathActive+1;
            SetMusic("pilgrim_stage_2");
            if(WrathCompleted){ReturnToWrathEntrance();ChapterSay("wrathRematch");return;}
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
            wrathCheckpoint=2;
            ReturnToWrathEntrance();
            wrath.returnSeal.enabled=true;
            wrathAreaTime=0;
            SetStage(WrathRoam);
        }
        void ReturnToWrathEntrance()
        {
            wrathActive=-1;wrathNext=0;
            wrath.SetInside(false);wrath.SetDoorOpening(0);wrathEntered=false;wrathNextStep=WrathWalkStart;
            sim.boundsMin=WrathRoute.BoundsMin;sim.boundsMax=WrathRoute.DoorX+80;
            sim.player.x=WrathRoute.Entrance;sim.player.y=sim.player.vy=0;
            sim.player.state="idle";sim.player.move=null;sim.player.age=0;sim.player.face=1;
            sim.enemyActive=sim.ai=false;
            cameraX=ChapterCameraX();
            SetStage(WrathRoam);
        }
        void DrawWrathAtmosphere(float dt)
        {
            bool general=ChapterGeneralVisible;
            float heat=ChapterBoss&&sim.phase>=2
                ?stage=="GUARDIAN_FIGHT"?1:stage=="GUARDIAN_DEFEAT"?1-Ramp(stageTime,0,.85f):0
                :0;
            if(speech.Cue=="weaponAsh"||wrathWeaponAsh>0)
                wrathWeaponAsh=Mathf.Min(1,wrathWeaponAsh+dt*1.7f);
            wrath.Animate(sim,view,general,heat,wrathWeaponAsh);
        }
        bool ChapterStageGUI()
        {
            if(stage==WrathTransfer){
                Fill(TailCanvas,WithAlpha(Color.black,Ramp(stageTime,0,.54f)));
                TailScreenMargins(WithAlpha(Color.black,Ramp(stageTime,0,.54f)));return true;
            }
            if(stage==WrathEnter){
                float blackout=Ramp(stageTime,WrathFadeStart,WrathInsideTime)*(1-Ramp(stageTime,WrathInsideTime,WrathFadeEnd));
                Fill(TailCanvas,WithAlpha(Color.black,blackout));TailScreenMargins(Color.black);
                float bars=Ramp(stageTime,0,.25f)*(1-Ramp(stageTime,WrathFadeEnd,WrathEnterDuration));
                Fill(new Rect(0,0,1280,58),WithAlpha(Color.black,bars));
                Fill(new Rect(0,662,1280,58),WithAlpha(Color.black,bars));return true;
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
            var rect=new Rect(396,action==ShrineEntrance?608:522,488,44);
            string key=showKeyHints?ControlPrompts.Key("interact",device):null;
            if(Button(rect,action,true,key:key))Interact();
        }
        void ChapterFieldGUI()
        {
            if(!wrath){
                if(WrathCompleted&&stage=="GATE_READY"&&Math.Abs(sim.player.x-PrologueLayout.MoonGateX)<=PrologueLayout.MoonGateWidth/2){
                    RoundedFill(new Rect(536,115,208,31),Ink,6);
                    Text(new Rect(536,115,208,31),TailProgress(sim.TailUpgrades),17,MoonGold,TextAnchor.MiddleCenter);
                }
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
            if(WrathCompleted&&stage==WrathRoam){
                Text(new Rect(375,135,530,38),"第一章完成 · 焚心余烬",22,MoonGold,TextAnchor.MiddleCenter);
                Text(new Rect(403,174,474,28),"门前可再次挑战 · 向左走返回残月神社",16,Ivory,TextAnchor.MiddleCenter);
            }
        }
    }
}
