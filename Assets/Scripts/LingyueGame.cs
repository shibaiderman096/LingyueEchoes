using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LingyueEchoes
{
    public partial class LingyueGame:MonoBehaviour
    {
        public string stage="MENU";
        [NonSerialized] public Combat sim; [NonSerialized] public FixedClock clock;
        [NonSerialized] internal double? scriptedAxis;
        static string lastInputDevice;
        static int? pendingSaveSlot;
        Transform world;Camera view;PixelActor hero,guardian;PixelEffects effects;PrologueContent content;
        TutorialDirector tutorial;DialogueDirector speech=new DialogueDirector();
        float stageTime,cameraX=-960,nextPadCheck,checkpointToast,bladeDrawTime;string modal="";
        static int TailStarPoints=>TailRewards.Length;
        static readonly Rect BackpackPortrait=new Rect(35,32,110,112);
        bool backpackOpen,backpackGuideSeen;
        int backpackSelection,backpackDirection;
        float backpackTime;
        float healGuideTime,healFeedbackTime;string healFeedback;
        bool debug,boxes,focused=true,motion,muted,hadTouch,showKeyHints,gameSettings;
        int settingsChoice,settingsDirection;
        static readonly Rect SettingsButton=new Rect(1125,35,120,52);
        const float RightHintKeyEdge=1180,HintRightMargin=18,HintKeyGap=10;
        static readonly (Rect bounds,string label)[] MenuOptions={
            (new Rect(105,395,340,62),"开始游戏"),
            (new Rect(105,477,340,58),"操作说明"),
            (new Rect(105,553,340,58),"声音与界面")
        };
        const string SavePicker="saves";
        const KeyCode PadConfirm=KeyCode.JoystickButton0,PadBack=KeyCode.JoystickButton1,PadSaveDelete=KeyCode.JoystickButton2;
        static readonly Rect SaveBackButton=new Rect(520,476,240,49);
        static Rect SaveRow(int index)=>new Rect(375,192+index*88,530,72);
        static readonly Rect[] MenuPanelButtons={new Rect(350,250,580,60),new Rect(350,335,580,60),new Rect(350,420,580,60),new Rect(530,565,220,55)};
        static readonly Rect[] DeleteSaveButtons={new Rect(385,388,240,52),new Rect(655,388,240,52)};
        static readonly Rect[] DeathButtons={new Rect(430,355,420,58),new Rect(430,435,420,58)};
        static Rect GameSettingButton(int index)=>new Rect(938,166+index*55,282,45);
        static readonly string[] SaveDataKeys=new[]{PrologueLayout.CheckpointKey,PrologueLayout.ProgressKey,PrologueLayout.TailCountKey,PrologueLayout.FirstVictoryStoryKey}.Concat(WrathRoute.SaveKeys).ToArray();
        int menuChoice,menuDirection,menuPanelChoice,saveChoice,deleteSaveSlot,deleteSaveDirection,deleteSaveChoice;
        Vector2 menuPointer;
        readonly InputDeviceTracker inputDevice=new InputDeviceTracker();string device=>inputDevice.Device;
        static readonly KeyCode[] keyboardKeys=Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>().Where(k=>k>=KeyCode.Backspace&&k<KeyCode.Mouse0).Distinct().ToArray();
        int storyIndex,soundCursor,voiceIndex,deathChoice,deathFrame;float storyTime,deathAnimationDuration,deathZoomTime,deathCameraSize,guiOpacity=1;
        Vector3 deathCameraPosition;const float DeathZoomDuration=1.2f,DeathMenuFadeDuration=1.2f;
        const float TrialIntroDuration=2.1f,TrialSlashTime=.94f;
        SpriteRenderer checkpointLamp,leftSeal,rightSeal;
        bool checkpointReached;
        public int SaveSlot {get;private set;}=1;
        internal bool SavePickerOpen=>stage=="MENU"&&modal==SavePicker;
        bool CanDeleteSelectedSave=>SavePickerOpen&&deleteSaveSlot==0&&saveChoice>=0&&saveChoice<PrologueLayout.SaveSlots&&HasSaveData(saveChoice+1);
        static int SaveValue(string key,int slot)=>PlayerPrefs.GetInt(PrologueLayout.SaveKey(key,slot),0);
        static bool HasMoonGateSave(int slot)=>SaveValue(PrologueLayout.CheckpointKey,slot)==1;
        static bool StorySeen(int slot)=>SaveValue(PrologueLayout.FirstVictoryStoryKey,slot)==1;
        static bool HasSaveData(int slot){
            foreach(string key in SaveDataKeys)if(PlayerPrefs.HasKey(PrologueLayout.SaveKey(key,slot)))return true;
            return false;
        }
        bool FirstVictoryStorySeen=>StorySeen(SaveSlot);
        internal static string SaveStatus(int slot)=>ContinueWrath(slot)
            ?ChapterCompleted(slot)?"焚心之城 · 已完成":SaveValue(WrathRoute.Checkpoint,slot)==2?"焚心之城 · 将军府灯":SaveValue(WrathRoute.Checkpoint,slot)==1?"焚心之城 · 城郊灯":"焚心之城 · 城郊"
            :ChapterCompleted(slot)?"残月神社 · 焚心已完成":StorySeen(slot)?"序章已完成 · 从月门灯继续":HasMoonGateSave(slot)?"月门灯 · 继续游戏":HasSaveData(slot)?"旧存档 · 从序幕开始":"空档 · 从序幕开始";
        void WriteSave(string key,int value)=>PlayerPrefs.SetInt(PrologueLayout.SaveKey(key,SaveSlot),value);
        string storyPhase="in";bool tutorialDialogue,phase2Spoken;int deathDirection;
        public float DeathMenuOpacity=>stage=="DEATH"?Ease(Mathf.Clamp01(stageTime/DeathMenuFadeDuration)):0;
        public bool DeathMenuReady=>stage=="DEATH"&&stageTime>=DeathMenuFadeDuration;
        Texture2D keyart,hudPortrait,hudRing,backpackArt;Font font;GUIStyle textStyle,buttonStyle;
        readonly GUIContent measureContent=new GUIContent();
        Color[] hudRingPixels;float[] hudRingCoverage,hudRingPosition;int hudEcho=-1,hudEchoTrail=-1,lastEchoPoints;
        StatusTrail healthTrail,staminaTrail,echoTrail,echoPointsTrail,bossHealthTrail,postureTrail;
        HudBarGrowth healthBar,staminaBar;
        AudioSource[] voices;AudioSource uiVoice,music;readonly Dictionary<string,AudioClip> soundClips=new Dictionary<string,AudioClip>();
        Combat soundModel;string musicKey="";
        readonly MobileControls mobileControls=new MobileControls();bool previewTouchControls;
        public bool PreviewTouchControls {get=>previewTouchControls;set{previewTouchControls=value;inputDevice.Select(value||Application.isMobilePlatform?"touch":"keyboard");ClearInput();}}
        bool TouchControlsVisible=>previewTouchControls||device=="touch";
        public bool BattleOn=>stage=="TUTORIAL"||stage=="GATE_READY"||stage=="GUARDIAN_FIGHT"||stage==WrathRoam;
        internal bool BackpackUnlocked=>sim!=null&&sim.TailUpgrades>0;
        internal bool BackpackOpen=>backpackOpen;
        bool BackpackAvailable=>BackpackUnlocked&&BattleOn&&!WakeDialogue&&!gameSettings;
        bool HealAvailable=>BattleOn&&sim.HealUnlocked&&!speech.Active&&!gameSettings&&!backpackOpen;
        bool TailStory=>stage=="TAIL_DIALOGUE"||stage=="FIRST_TAIL"||stage=="TAIL_EPILOGUE";
        int guardianDefeatFrame;
        float guardianDefeatDuration;
        double GuardianDefeatElapsed=>(sim.frame-guardianDefeatFrame)/60d;
        internal bool HasActiveDialogue=>speech.Active;
        bool WakeDialogue=>stage=="TUTORIAL"&&speech.Id=="routeWake";
        internal bool BrokenStepsCleared=>tutorial.StepsCleared;
        void Awake(){Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Input.simulateMouseWithTouches=true;
            content=JsonUtility.FromJson<PrologueContent>(Resources.Load<TextAsset>("Data/prologue-content").text);PixelWorld.Load();
            view=Camera.main;if(!view){var camera=new GameObject("Main Camera");camera.tag="MainCamera";view=camera.AddComponent<Camera>();}
            view.orthographic=true;view.orthographicSize=3.6f;view.transform.position=new Vector3(0,0,-10);view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.043f,.075f,.106f);
            if(!FindAnyObjectByType<AudioListener>())view.gameObject.AddComponent<AudioListener>();
            wrath=GetComponent<WrathChapter>();
            wrathText=JsonUtility.FromJson<PrologueContent>(Resources.Load<TextAsset>("Data/wrath-content").text);
            world=wrath?wrath.world:transform.Find("World");if(!world)world=PixelWorld.Create(transform);
            if(!wrath){
                view.backgroundColor=PixelWorld.PrologueSky;
                PixelWorld.PlaceBrokenSteps(world.Find("Props"));
                checkpointLamp=PixelWorld.PlaceCheckpoint(world.Find("Props"));
                leftSeal=PixelWorld.PlaceArenaSeal(world.Find("Props"),true);
                rightSeal=PixelWorld.PlaceArenaSeal(world.Find("Props"),false);
                PlaceShrineEntrance();
            }else view.backgroundColor=new Color(.12f,.07f,.10f);
            hero=new PixelActor(world.Find("Hero").gameObject,true);guardian=new PixelActor(world.Find("Guardian").gameObject,false);effects=new PixelEffects(world);
            keyart=Resources.Load<Texture2D>("ui/main_menu_keyart");
            tailIcons=TailRewards.Select(reward=>reward.SpritePath==null?null:Resources.Load<Texture2D>(reward.SpritePath)).ToArray();
            backpackArt=Resources.Load<Texture2D>("ui/backpack_moon_gate");
            CreatePlayerHud();
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Noto Sans CJK SC","Droid Sans Fallback","Arial Unicode MS"},24);
            motion=PlayerPrefs.GetString("lingyue.menuMotion","on")!="off";muted=PlayerPrefs.GetString("lingyue.sfxMuted","off")=="on";previewTouchControls=PlayerPrefs.GetInt("lingyue.touchControls",0)==1;
            showKeyHints=PlayerPrefs.GetInt("lingyue.keyHints",1)==1;
            inputDevice.Select(lastInputDevice??(Application.isMobilePlatform||previewTouchControls?"touch":Input.GetJoystickNames().Any(n=>!string.IsNullOrWhiteSpace(n))?"pad":"keyboard"));
            CreateAudioSources();
            foreach(string name in new[]{"step","jump","land","slash1","slash2","slash3","guard","dodge","counter","hit_player","hit_boss","parry","perfect_dodge","boss_swing","break","phase","death","victory","heal","ui_confirm","ui_back"})soundClips[name]=Resources.Load<AudioClip>("audio/sfx/"+name);
            soundClips["slash4"]=soundClips["slash3"];
            soundClips["plunge"]=soundClips["slash3"];
            soundClips["fire_burst"]=soundClips["boss_swing"];
            speech.onClose=id=>{
                if(HandleChapterSpeech(id))return;
                if(stage=="GATE_DIALOGUE"){BeginTrialIntro();return;}
                if(stage=="TUTORIAL"){tutorialDialogue=false;tutorial.FinishDialogue(sim,id);}
                if(id=="tail"&&stage=="TAIL_DIALOGUE")BeginTailHandoff("shoudeng");
                if(id=="tailAfter"&&stage=="TAIL_EPILOGUE")CompleteFirstVictory();
            };
            Home();
            if(wrath||SceneManager.GetActiveScene().name=="Prologue"){
                SaveSlot=pendingSaveSlot??1;pendingSaveSlot=null;
                if(wrath)PrepareWrath(true);else BeginTrial();
            }
        }
        void CreateAudioSources(){
            var audioRoot=new GameObject("2D Audio");
            audioRoot.transform.SetParent(transform,false);
            // Non-spatial audio can stay behind the camera, away from on-screen editor gizmos.
            audioRoot.transform.localPosition=Vector3.back*100;
            AudioSource AddVoice(){var source=audioRoot.AddComponent<AudioSource>();source.spatialBlend=0;return source;}
            voices=new AudioSource[12];for(int i=0;i<voices.Length;i++)voices[i]=AddVoice();
            uiVoice=AddVoice();music=AddVoice();music.loop=true;music.volume=MusicVolume;
            tailAudio=AddVoice();
        }
        public void StartTrial(){
            if(stage!="MENU"||modal!="")return;
            saveChoice=SaveSlot-1;modal=SavePicker;UiSound("ui_confirm");
        }
        internal void ActivateSaveChoice(int choice){
            if(!SavePickerOpen||deleteSaveSlot!=0||choice<0||choice>PrologueLayout.SaveSlots)return;
            if(choice==PrologueLayout.SaveSlots){CloseMenuModal();return;}
            SaveSlot=choice+1;modal="";backpackGuideSeen=false;UiSound("ui_confirm");
            ContinueCampaign();
        }
        void ContinueCampaign(){
            string target=ContinueWrath(SaveSlot)?WrathRoute.Scene:"Prologue";
            if(SceneManager.GetActiveScene().name!=target)LoadCampaignScene(target);
            else if(wrath)PrepareWrath(true);else BeginTrial();
        }
        void RequestSaveDeletion(){
            if(!CanDeleteSelectedSave)return;
            deleteSaveSlot=saveChoice+1;deleteSaveChoice=0;
            deleteSaveDirection=SaveHorizontalDirection();UiSound("ui_confirm");
        }
        void ResolveSaveDeletion(bool remove){
            if(!SavePickerOpen||deleteSaveSlot==0)return;
            if(remove){
                foreach(string key in SaveDataKeys)PlayerPrefs.DeleteKey(PrologueLayout.SaveKey(key,deleteSaveSlot));
                PlayerPrefs.Save();
            }
            deleteSaveSlot=0;UiSound(remove?"ui_confirm":"ui_back");
        }
        public void BeginTrial(){
            bool resume=HasMoonGateSave(SaveSlot)||ChapterUnlocked(SaveSlot);
            PrepareRoute(resume);
            storyIndex=0;storyTime=0;storyPhase="in";
            SetStage(resume?"FADE_IN":"STORY");
            if(resume)SetMusic("prologue_stage_1");
        }
        void PrepareCampaignCombat(){
            sim=new Combat{continueAfterDeath=true,bossYields=true,ai=false,enemyActive=false};
            sim.ApplyTailUpgrades(SavedTailCount(SaveSlot));
            backpackOpen=false;
            healGuideTime=healFeedbackTime=0;
            healthTrail.Reset(1);staminaTrail.Reset(1);echoTrail.Reset(0);echoPointsTrail.Reset(0);lastEchoPoints=0;
            healthBar.Reset(HealthHudWidth);
            staminaBar.Reset(StaminaHudWidth);
            clock=new FixedClock(sim);
            speech.Reset();tutorialDialogue=false;effects.Clear();ClearInput();
            world.gameObject.SetActive(true);gameSettings=false;
        }
        void PrepareRoute(bool fromCheckpoint){
            PrepareCampaignCombat();
            sim.boundsMin=PrologueLayout.RouteStart;sim.boundsMax=PrologueLayout.ArenaMax;
            checkpointReached=fromCheckpoint;checkpointToast=0;tutorial=new TutorialDirector();
            sim.player.x=fromCheckpoint?PrologueLayout.CheckpointX:PrologueLayout.WakeX;
            sim.boss.x=fromCheckpoint?PrologueLayout.GuardianWaitX:PrologueLayout.WakeGuideX;
            if(!fromCheckpoint){tutorial.Begin(sim,PrologueLayout.BrokenSteps());sim.beforeStep=tutorial.Pump;}
            else sim.terrain.Add(PrologueLayout.BrokenSteps());
            cameraX=RouteCameraX();SetArenaSeals(false);UpdateCheckpointLamp();UpdateShrineEntrance();
        }
        public void Home(){ClearInput();speech.Reset();effects?.Clear();modal="";deleteSaveSlot=0;gameSettings=backpackOpen=false;menuChoice=menuDirection=0;menuPointer=Input.mousePosition;SetStage("MENU");world.gameObject.SetActive(false);SetMusic("menu_stage_select");}
        void SetStage(string value){
            if(stage=="FIRST_TAIL"&&value!="FIRST_TAIL"){
                music.volume=MusicVolume;
                if(value!="TAIL_EPILOGUE")tailAudio.Stop();
            }else if(stage=="TAIL_EPILOGUE"&&value!="GATE_READY")tailAudio.Stop();
            stage=value;stageTime=0;
        }
        void BeginTrialIntro(){
            ClearInput();sim.beforeStep=null;sim.nonlethal=false;sim.dummyBox=null;
            sim.boundsMin=PrologueLayout.ArenaMin;sim.boundsMax=PrologueLayout.ArenaMax;
            sim.ResetBoss(PrologueLayout.GuardianFightX,520);
            bossHealthTrail.Reset(1);postureTrail.Reset(0);
            var p=sim.player;p.x=PrologueLayout.PlayerFightX;p.y=p.vy=0;p.face=1;
            p.state="idle";p.move=null;p.age=p.combo=p.invuln=0;
            sim.ai=sim.enemyActive=false;clock=new FixedClock(sim);effects.Clear();
            phase2Spoken=false;cameraX=(float)PrologueLayout.ArenaCenter;
            SetStage("GUARDIAN_INTRO");SetArenaSeals(true);
        }
        void BeginGuardianDefeat(){
            mobileControls.Reset();sim.ClearCommands();clock.paused=false;clock.ClearAccumulation();speech.Reset();
            var p=sim.player;
            p.state=p.y>0||p.vy!=0?"air":"idle";p.move=null;p.age=0;
            if(!wrath||ChapterBoss)p.hp=p.maxHp;
            guardianDefeatFrame=sim.frame;
            guardianDefeatDuration=PixelWorld.Definition("guardian8","broken").duration;
            SetStage("GUARDIAN_DEFEAT");
        }
        void UpdateGuardianDefeat(float dt){
            sim.axis=0;clock.Advance(dt);SyncSounds();
            if(GuardianDefeatElapsed>=guardianDefeatDuration&&sim.player.vy==0&&sim.boss.vy==0)FinishGuardianFight();
        }
        void FinishGuardianFight(){
            if(wrath){FinishWrathEncounter();return;}
            StopSounds();SetArenaSeals(false);SetMusic("prologue_stage_1");
            sim.boundsMin=PrologueLayout.RouteStart;
            sim.visuals.Clear();effects.Clear();
            // Older save flags describe progression, not whether this closing scene has played.
            if(FirstVictoryStorySeen){
                SetStage("GATE_READY");Say("rematchWin");return;
            }
            var p=sim.player;var b=sim.boss;
            p.state="idle";p.move=null;p.age=0;p.y=p.vy=0;b.y=b.vy=0;
            p.face=b.x>=p.x?1:-1;b.face=-p.face;
            SetStage("TAIL_DIALOGUE");Say("tail");
        }
        void CompleteFirstVictory(){
            WriteSave(PrologueLayout.TailCountKey,sim.TailUpgrades);
            WriteSave(PrologueLayout.ProgressKey,1);
            WriteSave(PrologueLayout.FirstVictoryStoryKey,1);WriteSave(WrathRoute.Unlocked,1);PlayerPrefs.Save();
            SetStage("GATE_READY");UpdateShrineEntrance();
            if(sim.HealUnlocked)healGuideTime=9;
        }
        void SaveCheckpoint(){
            checkpointReached=true;checkpointToast=2.4f;UpdateCheckpointLamp();
            WriteSave(PrologueLayout.CheckpointKey,1);PlayerPrefs.Save();UiSound("ui_confirm");
        }
        void UpdateCheckpointLamp(){checkpointLamp.color=checkpointReached?new Color(1,.84f,.53f):new Color(.51f,.59f,.68f);}
        public void Retry(){if(!DeathMenuReady||sim==null)return;
            if(wrath)PrepareWrath(false);
            else {PrepareRoute(checkpointReached);SetStage("FADE_IN");SetMusic("prologue_stage_1");}
            UiSound("ui_confirm");}
        void EnterDeath(){
            // Only player commands stop. The fixed clock, pending enemy moves, gravity, effects and audio continue.
            mobileControls.Reset();sim.ClearCommands();clock.paused=false;clock.ClearAccumulation();
            speech.Reset();tutorialDialogue=false;deathChoice=deathDirection=0;deathFrame=sim.frame-Math.Max(0,sim.player.age-1);deathZoomTime=0;
            deathAnimationDuration=PixelWorld.Definition("pixel8","dead")?.duration??1.07f;
            deathCameraPosition=view.transform.position;deathCameraSize=view.orthographicSize;SetStage("DYING");}
        void UpdateDeath(float dt){sim.axis=0;clock.Advance(dt);SyncSounds();
            if(stage!="DYING")return;
            deathZoomTime=Math.Min(DeathZoomDuration,deathZoomTime+dt);
            if(deathZoomTime>=DeathZoomDuration&&(sim.frame-deathFrame)/60f>=deathAnimationDuration&&sim.player.vy==0)SetStage("DEATH");
        }
        void Update(){float dt=Math.Min(Time.unscaledDeltaTime,.25f);UpdateViewport();if(!focused&&!Application.isBatchMode)return;ReadInput();
            if(music)music.mute=muted;
            if(tailAudio)tailAudio.mute=muted;
            if(backpackOpen){backpackTime+=dt;DrawActors(dt);return;}
            if(gameSettings){DrawActors(dt);return;}
            stageTime+=dt;
            switch(stage){case "MENU":break;
                case "STORY":UpdateStory(dt);break;
                case WrathTransfer:UpdateTransfer();break;
                case WrathArrive:UpdateWrathArrival();break;
                case WrathDialogue:break;
                case "FADE_IN":if(stageTime>=.8f)SetStage("CAMERA_REVEAL");break;
                case "CAMERA_REVEAL":if(stageTime>=3.5f)SetStage("WAKE_UP");break;
                case "WAKE_UP":if(stageTime>=.95f){SetStage(checkpointReached?"GATE_READY":"TUTORIAL");SetMusic("prologue_stage_1");}break;
                case "GATE_DIALOGUE":if(speech.Cue=="drawBlade")bladeDrawTime+=dt;break;
                case "TAIL_DIALOGUE":break;
                case "TAIL_EPILOGUE":break;
                case "FIRST_TAIL":UpdateTailRitual();break;
                case "GUARDIAN_INTRO":
                    if(stageTime>=TrialSlashTime&&stageTime-dt<TrialSlashTime)UiSound("boss_swing");
                    if(stageTime>=TrialIntroDuration){sim.ai=sim.enemyActive=true;SetStage("GUARDIAN_FIGHT");SetMusic("moon_gate_boss");}
                    break;
                case "GUARDIAN_DEFEAT":UpdateGuardianDefeat(dt);break;
                case "DYING":case "DEATH":UpdateDeath(dt);break;
                default:
                    if(BattleOn){sim.axis=WakeDialogue?0:ReadAxis();clock.Advance(dt);SyncSounds();
                        if(sim.player.hp<=0&&sim.player.state=="dead"){EnterDeath();}
                        else if(stage=="TUTORIAL"){var id=tutorial.Speech(sim,!speech.Active);if(id!=null)Say(id);
                            if(!checkpointReached&&tutorial.GateBound&&sim.player.x>=PrologueLayout.CheckpointX)SaveCheckpoint();}
                        else if(stage=="GUARDIAN_FIGHT"&&sim.BossDefeated)BeginGuardianDefeat();
                        else if(stage==WrathRoam)UpdateWrathRoute(dt);
                        else if(stage=="GUARDIAN_FIGHT"&&sim.phase==2&&!phase2Spoken){phase2Spoken=true;if(wrath)ChapterSay("wrathPhase2");else Say("bossPhase2");}}
                    break;}
            if(speech.Active)speech.Update(dt);
            if(stage!="MENU"){UpdateStatusTrails(dt);DrawWorld(dt);}
            checkpointToast=Math.Max(0,checkpointToast-dt);
            if(wrath)wrathHintTime=Mathf.Max(0,wrathHintTime-dt);
            if(HealAvailable){healGuideTime=Mathf.Max(0,healGuideTime-dt);healFeedbackTime=Mathf.Max(0,healFeedbackTime-dt);}
        }
        void UpdateStory(float dt){if(stageTime<.4f)return;storyTime+=dt;float span=storyPhase=="hold"?(float)DialogueDirector.ReadingHold(content.story[storyIndex].Length):.4f;if(storyTime>=span){storyTime=0;if(storyPhase=="in")storyPhase="hold";else if(storyPhase=="hold")storyPhase="out";else AdvanceStory();}}
        void AdvanceStory(){if(stage!="STORY"||stageTime<.4f)return;storyIndex++;storyPhase="in";storyTime=0;if(storyIndex>=content.story.Length){SetStage("FADE_IN");SetMusic("prologue_stage_1");}}
        void DrawWorld(float dt){var p=sim.player;var b=sim.boss;float zoom=1,focusY=0;
            if(stage=="DYING"||stage=="DEATH"){float t=Ease(Mathf.Clamp01(deathZoomTime/DeathZoomDuration));
                var target=new Vector3((float)p.x/100,((float)p.y-55)/100,-10);view.orthographicSize=Mathf.Lerp(deathCameraSize,3.6f/2.2f,t);view.transform.position=Vector3.Lerp(deathCameraPosition,target,t);}
            else{if(stage=="FADE_IN"||stage=="CAMERA_REVEAL"){float t=stage=="FADE_IN"?0:Mathf.Clamp01((stageTime-.7f)/2.8f),ease=Ease(t);zoom=Mathf.Lerp(2.2f,1,ease);cameraX=Mathf.Lerp((float)p.x,RouteCameraX(),ease);focusY=Mathf.Lerp((float)p.y+40,0,ease);}
                else if(stage=="GUARDIAN_INTRO"){float t=stageTime/TrialIntroDuration;zoom=1+.17f*Mathf.Sin(t*Mathf.PI);focusY=12*Mathf.Sin(t*Mathf.PI);}
                else if(stage=="FIRST_TAIL")TailCamera(out zoom,out focusY);
                else if(stage=="TAIL_DIALOGUE"){float focus=(float)((p.x+b.x)/2);cameraX=Mathf.Lerp(cameraX,focus,1-Mathf.Exp(-4*dt));zoom=1.14f;focusY=16;}
                else if(wrath){float focus=wrathActive>=0&&stage=="GUARDIAN_FIGHT"
                        ?(float)((WrathRoute.Fights[wrathActive].Min+WrathRoute.Fights[wrathActive].Max)*.5):ChapterCameraX();
                    cameraX=Mathf.Lerp(cameraX,focus,1-Mathf.Exp(-8*dt));}
                else{float focus=stage=="TUTORIAL"||stage=="GATE_READY"||stage=="GATE_DIALOGUE"||stage=="WAKE_UP"||stage=="TAIL_EPILOGUE"||ChapterDialogue||stage==WrathTransfer?RouteCameraX():(float)PrologueLayout.ArenaCenter;cameraX=Mathf.Lerp(cameraX,focus,1-Mathf.Exp(-8*dt));}
                view.orthographicSize=3.6f/zoom;view.transform.position=new Vector3(cameraX/100,focusY/100,-10);}
            if(!wrath){
                var cameraPosition=view.transform.position;
                cameraPosition.x=PrologueLayout.ClampViewX(cameraPosition.x*100,view.orthographicSize*view.aspect*100)/100;
                view.transform.position=cameraPosition;
            }
            // Noninteractive scenes still need idle loops, even when the battle clock is stopped.
            bool ticked=BattleOn||stage=="GUARDIAN_DEFEAT"||stage=="DYING"||stage=="DEATH";
            DrawActors(ticked?(double?)null:dt);
            effects.Draw(sim);
            if(wrath)DrawWrathAtmosphere(dt);
        }
        void DrawActors(double? idleDelta){
            var p=sim.player;var b=sim.boss;
            if(wrath){
                guardian.ChapterArt=wrath.UseActorArt(ChapterBoss||ChapterDialogue&&speech.Id!="wrathEnter",wrathActive);
                guardian.Tint=guardian.ChapterArt!=null?(sim.phase==2?new Color(1,.82f,.72f):Color.white):
                    wrathActive==0||wrathActive==2?new Color(.80f,.71f,.58f):
                    wrathActive==1?new Color(.84f,.52f,.35f):wrathActive==3?new Color(.65f,.52f,.61f):
                    sim.phase==2?new Color(1,.43f,.30f):new Color(.91f,.61f,.54f);
                guardian.Size=wrathActive==1?.9f:wrathActive==3?1.13f:ChapterBoss?1.22f:1;
            }
            string pose=null;double? poseSeconds=null;
            if(stage=="FADE_IN"||stage=="CAMERA_REVEAL"){pose="dead";poseSeconds=PixelWorld.Definition("pixel8",pose).duration;}
            else if(stage=="WAKE_UP"&&stageTime<.86f){
                float t=Ease(Mathf.Clamp01(stageTime/.86f));pose="dead";
                poseSeconds=PixelWorld.Definition("pixel8",pose).duration*(1-t);
            }
            else if(stage=="DYING"||stage=="DEATH"){pose="dead";poseSeconds=(sim.frame-deathFrame)/(double)Rules.hz;}
            else if(stage=="GUARDIAN_INTRO"&&stageTime>=.65f&&stageTime<1.65f){
                pose="parry";poseSeconds=(stageTime-.65f)*PixelWorld.Definition("pixel8",pose).duration;
            }
            hero.Draw(p,sim.frame,pose:pose,poseSeconds:poseSeconds,idleDelta:idleDelta);
            if(stage=="GUARDIAN_INTRO"&&stageTime>=.22f&&stageTime<1.62f){
                int slashCell=stageTime<TrialSlashTime?Mathf.Clamp((int)((stageTime-.22f)*6),0,3):Mathf.Clamp(4+(int)((stageTime-TrialSlashTime)*8),4,7);
                guardian.Draw(b,sim.frame,pose:"slash",poseCell:slashCell);
            }else if(stage=="GATE_DIALOGUE"&&speech.Cue=="drawBlade")guardian.Draw(b,sim.frame,pose:"slash",poseCell:Mathf.Min(3,(int)(bladeDrawTime*5)));
            else if(stage=="GUARDIAN_DEFEAT")guardian.Draw(b,sim.frame,pose:"broken",poseSeconds:GuardianDefeatElapsed);
            else if(TailStory)guardian.Draw(b,sim.frame,pose:"broken",poseSeconds:guardianDefeatDuration);
            else guardian.Draw(b,sim.frame,alpha:wrath&&wrathActive<0&&(!ChapterDialogue||speech.Id=="wrathEnter")?0:1,idleDelta:idleDelta);
        }
        float RouteCameraX()=>wrath?ChapterCameraX():PrologueLayout.ClampViewX((float)sim.player.x+100,PrologueLayout.FullViewHalfWidth);
        static float Ease(float t)=>t<.5f?4*t*t*t:1-Mathf.Pow(-2*t+2,3)/2;
        void Say(string id){var script=Array.Find(content.scripts,s=>s.id==id);if(script!=null){if(stage=="TUTORIAL")tutorial.BeginDialogue(id);speech.Play(script);}}
        string Interaction(){
            if((tutorialDialogue||stage=="GATE_DIALOGUE"||ChapterDialogue)&&speech.Active)return "继续";
            string chapterAction=ChapterInteraction();
            if(chapterAction!=null)return chapterAction;
            if(stage=="GATE_READY"&&Math.Abs(sim.player.x-sim.boss.x)<210)return FirstVictoryStorySeen?"再次挑战":"交谈";
            return stage=="TUTORIAL"&&tutorial.CanInteract(sim)?"交谈":null;
        }
        public void Interact(){
            if(clock==null||clock.paused||backpackOpen)return;
            string interaction=Interaction();
            if(interaction==null)return;
            if(interaction=="继续"){speech.Confirm();return;}
            if(HandleChapterInteraction(interaction))return;
            string id=stage=="GATE_READY"?"bossPre":tutorial.Interact(sim);
            var script=Array.Find(content.scripts,s=>s.id==id);if(script==null)return;
            if(id=="bossPre"){
                ClearInput();
                sim.player.state="idle";sim.player.move=null;sim.player.age=0;
                sim.player.y=sim.player.vy=0;sim.player.face=sim.boss.x>=sim.player.x?1:-1;
                sim.boss.face=-sim.player.face;bladeDrawTime=0;SetStage("GATE_DIALOGUE");SetArenaSeals(true);
                if(interaction=="再次挑战")script=Array.Find(content.scripts,s=>s.id=="bossRetry");
            }else{tutorialDialogue=true;tutorial.BeginDialogue(id);if(id=="routeWake")ClearInput();}
            speech.Play(script,false);
        }
        void SetArenaSeals(bool active){
            leftSeal.enabled=active;
            rightSeal.enabled=active;
        }
        void SetMusic(string name){if(musicKey==name)return;musicKey=name;var clip=Resources.Load<AudioClip>("contentpack_20261104/music/"+name);if(!clip)return;music.Stop();music.clip=clip;music.Play();}
        void StopSounds(){if(voices!=null)foreach(var voice in voices)voice.Stop();}
        void UiSound(string name){if(!muted&&soundClips.TryGetValue(name,out var clip)&&clip){uiVoice.Stop();uiVoice.clip=clip;uiVoice.volume=.45f;uiVoice.Play();}}
        void SyncSounds(){if(soundModel!=sim){StopSounds();soundModel=sim;soundCursor=0;}foreach(var cue in sim.sounds){if(cue.id<=soundCursor)continue;soundCursor=cue.id;if(muted||clock.paused||cue.name=="victory")continue;if(!soundClips.TryGetValue(cue.name,out var clip)||!clip)continue;var voice=voices.FirstOrDefault(v=>!v.isPlaying)??voices[voiceIndex++%voices.Length];voice.Stop();voice.clip=clip;voice.pitch=cue.name=="plunge"?.70f:cue.name=="slash4"?.76f:1;voice.volume=cue.name=="step"?.22f:cue.name=="guard"?.3f:.65f;voice.Play();}}
        void UpdateViewport(){float aspect=(float)Screen.width/Screen.height,desired=16f/9;if(aspect>desired){float w=desired/aspect;view.rect=new Rect((1-w)/2,0,w,1);}else{float h=aspect/desired;view.rect=new Rect(0,(1-h)/2,1,h);}}
        Vector2 UiPoint(Vector2 screen){float scale=Math.Min(Screen.width/1280f,Screen.height/720f);return new Vector2((screen.x-(Screen.width-1280*scale)/2)/scale,(Screen.height-screen.y-(Screen.height-720*scale)/2)/scale);}
        void DetectInputDevice(){bool keyboard=Input.anyKeyDown&&keyboardKeys.Any(k=>Input.GetKeyDown(k)),padButton=false;
            for(int i=0;i<20;i++)padButton|=Input.GetKeyDown(KeyCode.JoystickButton0+i);
            bool touch=Input.touchCount>0;
            bool mouse=!touch&&!hadTouch&&(Input.GetMouseButtonDown(0)||Input.GetMouseButtonDown(1)||Input.GetMouseButtonDown(2));
            inputDevice.Observe(keyboard||mouse&&!previewTouchControls,padButton,touch||mouse&&previewTouchControls,Input.GetAxisRaw("PadX"),Input.GetAxisRaw("PadDpad"),Input.GetAxisRaw("PadY"));
            hadTouch=touch;
            if(Time.unscaledTime>=nextPadCheck){nextPadCheck=Time.unscaledTime+1;
                if(device=="pad"&&!Input.GetJoystickNames().Any(n=>!string.IsNullOrWhiteSpace(n)))inputDevice.Select(Application.isMobilePlatform||previewTouchControls?"touch":"keyboard");}
            lastInputDevice=device;
        }
        void ReadInput(){DetectInputDevice();bool padA=Input.GetKeyDown(PadConfirm),padB=Input.GetKeyDown(PadBack);bool confirm=Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space)||padA;
            Vector2 pointer=Input.mousePosition;
            Vector2? hover=pointer!=menuPointer&&!TouchControlsVisible?UiPoint(pointer):null;
            menuPointer=pointer;
            if(stage=="DYING"||stage=="GUARDIAN_DEFEAT"||stage==WrathTransfer)return;if(stage=="DEATH"){ReadDeathInput(confirm,padB,hover);return;}
            if(TailStory||ChapterDialogue){
                if(stage!="FIRST_TAIL"&&(confirm||Input.GetKeyDown(KeyCode.E)||Input.GetKeyDown(KeyCode.JoystickButton5)))speech.Confirm();
                return;
            }
            bool escape=Input.GetKeyDown(KeyCode.Escape),menu=Input.GetKeyDown(KeyCode.JoystickButton7),viewKey=Input.GetKeyDown(KeyCode.JoystickButton6);
            bool backpackKey=Input.GetKeyDown(KeyCode.B)||menu;
            if(backpackOpen){
                if(backpackKey||padB||escape||viewKey)SetBackpack(false);
                else ReadBackpackNavigation(hover);
                return;
            }
            if(gameSettings){ReadGameSettings(confirm,padB||escape||menu||viewKey,hover);return;}
            if(BackpackAvailable&&backpackKey){SetBackpack(true);return;}
            if((BattleOn||stage=="GATE_DIALOGUE")&&(escape||viewKey||menu&&!BackpackUnlocked)){SetGameSettings(true);return;}
            if(stage=="MENU"){ReadMenuInput(confirm,padB||escape,hover);return;}
            if(escape){Home();return;}
            if(stage=="STORY"){if(padB){Home();return;}if(confirm)AdvanceStory();return;}
            if(!BattleOn&&stage!="GATE_DIALOGUE")return;
            bool ePressed=Input.GetKeyDown(KeyCode.E);
            if(Input.GetKeyDown(KeyCode.JoystickButton5)||ePressed&&Interaction()!=null)Interact();
            else if(ePressed)RequestHeal();
            if(Input.GetKeyDown(KeyCode.JoystickButton4))RequestHeal();
            if(stage=="GATE_DIALOGUE"){ReadTouchInput();return;}
            if(!BattleOn)return;
            if(Input.GetKeyDown(KeyCode.P))Act("pause");if(Input.GetKeyDown(KeyCode.O))Act("step");if(Input.GetKeyDown(KeyCode.T))Act("slow");
            if(Input.GetKeyDown(KeyCode.F1))debug=!debug;if(Input.GetKeyDown(KeyCode.F2))boxes=!boxes;
            if(Input.GetKeyDown(KeyCode.J)||Input.GetKeyDown(KeyCode.JoystickButton2))Act("attack");if(Input.GetKeyDown(KeyCode.K)||Input.GetKeyDown(KeyCode.JoystickButton3))Act("parry");
            if(Input.GetKeyDown(KeyCode.LeftShift)||Input.GetKeyDown(KeyCode.RightShift)||padB)Act("dodge");if(Input.GetKeyDown(KeyCode.Space)||padA)Act("jump");
            if(debug)for(int i=0;i<6;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i))sim.DebugBoss(i);
            ReadTouchInput();
        }
        static bool MoveMenuFocus(ref int previous,out int direction){
            float vertical=Input.GetAxisRaw("Vertical");
            direction=vertical>.5f?-1:vertical<-.5f?1:0;
            bool moved=direction!=0&&direction!=previous;previous=direction;return moved;
        }
        static int HoveredOption(Rect[] options,Vector2 point,int fallback){
            for(int i=0;i<options.Length;i++)if(options[i].Contains(point))return i;
            return fallback;
        }
        void ReadMenuInput(bool confirm,bool back,Vector2? hover){
            bool moveSelection=MoveMenuFocus(ref menuDirection,out int direction);
            if(modal!=""){
                if(deleteSaveSlot!=0){
                    if(back){ResolveSaveDeletion(false);return;}
                    int horizontal=SaveHorizontalDirection();
                    if(horizontal!=0&&horizontal!=deleteSaveDirection)deleteSaveChoice=horizontal>0?1:0;
                    else if(moveSelection)deleteSaveChoice=1-deleteSaveChoice;
                    else if(hover.HasValue)deleteSaveChoice=HoveredOption(DeleteSaveButtons,hover.Value,deleteSaveChoice);
                    deleteSaveDirection=horizontal;
                    if(confirm)ResolveSaveDeletion(deleteSaveChoice==1);
                    return;
                }
                if(back){CloseMenuModal();return;}
                if(!SavePickerOpen){
                    if(modal=="settings"){
                        if(moveSelection)menuPanelChoice=(menuPanelChoice+direction+MenuPanelButtons.Length)%MenuPanelButtons.Length;
                        else if(hover.HasValue)menuPanelChoice=HoveredOption(MenuPanelButtons,hover.Value,menuPanelChoice);
                    }
                    if(confirm)ActivateMenuPanelOption(menuPanelChoice);
                    return;
                }
                int count=PrologueLayout.SaveSlots+1;
                if(moveSelection)saveChoice=(saveChoice+direction+count)%count;
                else if(hover.HasValue){
                    Vector2 point=hover.Value;
                    if(SaveBackButton.Contains(point))saveChoice=PrologueLayout.SaveSlots;
                    else for(int i=0;i<PrologueLayout.SaveSlots;i++)if(SaveRow(i).Contains(point)){saveChoice=i;break;}
                }
                bool delete=Input.GetKeyDown(KeyCode.Delete)||Input.GetKeyDown(PadSaveDelete);
                if(delete)RequestSaveDeletion();
                else if(confirm)ActivateSaveChoice(saveChoice);
                return;
            }
            if(back)return;
            if(moveSelection)menuChoice=(menuChoice+direction+MenuOptions.Length)%MenuOptions.Length;
            else if(hover.HasValue){
                for(int i=0;i<MenuOptions.Length;i++)if(MenuOptions[i].bounds.Contains(hover.Value)){menuChoice=i;break;}
            }
            if(confirm)ActivateMenuOption(menuChoice);
        }
        static int SaveHorizontalDirection(){float axis=Input.GetAxisRaw("Horizontal");return axis>.5f?1:axis<-.5f?-1:0;}
        void CloseMenuModal(){modal="";UiSound("ui_back");}
        void ActivateMenuOption(int option){
            menuChoice=option;
            switch(option){
                case 0:StartTrial();break;
                case 1:UiSound("ui_confirm");modal="controls";menuPanelChoice=MenuPanelButtons.Length-1;break;
                case 2:UiSound("ui_confirm");modal="settings";menuPanelChoice=0;break;
            }
        }
        void ActivateMenuPanelOption(int option){
            menuPanelChoice=option;
            if(option==MenuPanelButtons.Length-1){CloseMenuModal();return;}
            switch(option){
                case 0:motion=!motion;PlayerPrefs.SetString("lingyue.menuMotion",motion?"on":"off");PlayerPrefs.Save();break;
                case 1:ToggleSound();break;
                case 2:PreviewTouchControls=!previewTouchControls;PlayerPrefs.SetInt("lingyue.touchControls",previewTouchControls?1:0);PlayerPrefs.Save();break;
            }
        }
        void SetGameSettings(bool open){
            ClearInput();gameSettings=open;
            if(open){settingsChoice=0;settingsDirection=0;}
            UiSound(open?"ui_confirm":"ui_back");
        }
        internal void SetBackpack(bool open){
            if((open&&!BackpackAvailable)||open==backpackOpen)return;
            ClearInput();backpackOpen=open;
            if(open){backpackGuideSeen=true;backpackSelection=backpackDirection=0;backpackTime=0;}
            UiSound(open?"ui_confirm":"ui_back");
        }
        void ReadBackpackNavigation(Vector2? hover){
            float horizontal=Input.GetAxisRaw("Horizontal"),vertical=Input.GetAxisRaw("Vertical"),dpad=Input.GetAxisRaw("PadDpad");
            float axis=Math.Abs(horizontal)>.5f?horizontal:Math.Abs(dpad)>.5f?dpad:-vertical;
            int direction=axis>.5f?1:axis<-.5f?-1:0;
            if(direction!=0&&direction!=backpackDirection)backpackSelection=(backpackSelection+direction+TailStarPoints)%TailStarPoints;
            else if(hover.HasValue)for(int i=0;i<TailStarPoints;i++)if(TailStarNode(i).Contains(hover.Value)){backpackSelection=i;break;}
            backpackDirection=direction;
        }
        void ReadGameSettings(bool confirm,bool back,Vector2? hover){
            if(back){SetGameSettings(false);return;}
            if(MoveMenuFocus(ref settingsDirection,out int direction))settingsChoice=(settingsChoice+direction+4)%4;
            else if(hover.HasValue)for(int i=0;i<4;i++)if(GameSettingButton(i).Contains(hover.Value)){settingsChoice=i;break;}
            if(confirm)ActivateGameSetting(settingsChoice);
        }
        void ActivateGameSetting(int option){
            settingsChoice=option;
            switch(option){
                case 0:showKeyHints=!showKeyHints;PlayerPrefs.SetInt("lingyue.keyHints",showKeyHints?1:0);PlayerPrefs.Save();break;
                case 1:ToggleSound();break;
                case 2:SetGameSettings(false);break;
                case 3:Home();break;
            }
        }
        void ToggleSound(){
            muted=!muted;PlayerPrefs.SetString("lingyue.sfxMuted",muted?"on":"off");
            if(muted)StopSounds();
            PlayerPrefs.Save();
        }
        void ReadDeathInput(bool confirm,bool back,Vector2? hover){if(!DeathMenuReady)return;
            if(MoveMenuFocus(ref deathDirection,out int direction))deathChoice=direction>0?1:0;
            else if(hover.HasValue)deathChoice=HoveredOption(DeathButtons,hover.Value,deathChoice);
            if(back||Input.GetKeyDown(KeyCode.Escape)){Home();return;}
            if(confirm){if(deathChoice==0)Retry();else Home();}
        }
        void ReadTouchInput(){for(int i=0;i<Input.touchCount;i++){
                var touch=Input.GetTouch(i);
                if(touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled){mobileControls.End(touch.fingerId);continue;}
                Vector2 point=UiPoint(touch.position);
                if(touch.phase==TouchPhase.Began)MobileAction(mobileControls.Begin(touch.fingerId,point,HealAvailable));
                else mobileControls.Move(touch.fingerId,point);
            }
            // Menu GUI still uses Unity's touch-to-mouse simulation; action controls consume real touches only once.
            const int mousePointer=int.MinValue;
            if(Input.touchCount>0){mobileControls.End(mousePointer);return;}
            if(!TouchControlsVisible)return;
            if(Input.GetMouseButtonDown(0))MobileAction(mobileControls.Begin(mousePointer,UiPoint(Input.mousePosition),HealAvailable));
            else if(Input.GetMouseButton(0))mobileControls.Move(mousePointer,UiPoint(Input.mousePosition));
            if(Input.GetMouseButtonUp(0)||!Input.GetMouseButton(0))mobileControls.End(mousePointer);
        }
        void MobileAction(string action){if(action==null||clock.paused&&action!="pause")return;if(action=="interact")Interact();else if(action=="heal")RequestHeal();else Act(action);}
        void RequestHeal(){
            if(!HealAvailable||clock.paused||sim.player.hp<=0||sim.player.state=="heal")return;
            var p=sim.player;
            healFeedback=p.hp>=p.maxHp?"生命已满":p.energy<Rules.healCost?"回响不足":null;
            healFeedbackTime=healFeedback==null?0:1.4f;
            if(healFeedback==null)Act("heal");
        }
        double ReadAxis(){if(scriptedAxis.HasValue)return scriptedAxis.Value;float pad=Input.GetAxisRaw("PadX"),dpad=Input.GetAxisRaw("PadDpad");if(Math.Abs(pad)>.2f)return pad;if(Math.Abs(dpad)>.2f)return dpad;
            bool left=Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow),right=Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow);int keys=(right?1:0)-(left?1:0);return keys!=0?keys:mobileControls.Axis;}
        public void Act(string action){if(!BattleOn||WakeDialogue||backpackOpen)return;if(action=="pause"){clock.paused=!clock.paused;ClearInput();}else if(action=="step"){clock.paused=true;clock.Single();}else if(action=="slow")clock.speed=clock.speed==1?.25:1;else if(action=="charge"){sim.player.energy=3;sim.player.echo=0;}else if(action=="stun"){sim.boss.state="broken";sim.boss.age=0;sim.boss.move=null;sim.boss.posture=100;}else if(action=="echoWindow"){sim.player.energy=Math.Max(1,sim.player.energy);sim.executeWindow=30;}else sim.Press(action);}
        void ClearInput(){mobileControls.Reset();sim?.ClearCommands();clock?.ClearAccumulation();StopSounds();}
        void OnApplicationFocus(bool hasFocus){focused=hasFocus;ClearInput();}
        void OnApplicationPause(bool pause){if(pause)ClearInput();}
        void OnDestroy(){StopSounds();if(font)Destroy(font);if(hudPortrait)Destroy(hudPortrait);if(hudRing)Destroy(hudRing);if(tailGlow)Destroy(tailGlow);if(tailVignette)Destroy(tailVignette);}

        void OnGUI(){if(!font)return;var oldMatrix=GUI.matrix;float scale=Math.Min(Screen.width/1280f,Screen.height/720f);GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            if(textStyle==null){textStyle=new GUIStyle(GUI.skin.label){font=font,wordWrap=true};buttonStyle=new GUIStyle(GUI.skin.button){font=font,fontSize=21,alignment=TextAnchor.MiddleCenter,wordWrap=true};}
            if(stage=="MENU")MenuGUI();
            else{
                if(backpackOpen)BackpackGUI();
                else{
                    bool settingsOpen=gameSettings;
                    GUI.enabled=!settingsOpen;GameGUI();GUI.enabled=true;
                    if(settingsOpen)GameSettingsGUI();
                }
            }
            GUI.matrix=oldMatrix;GUI.color=Color.white;
        }
        static Color Ivory=>new Color(.98f,.93f,.85f);static Color Gold=>new Color(.85f,.74f,.53f);static Color Ink=>new Color(.075f,.11f,.18f,.95f);
        void Fill(Rect rect,Color color){color.a*=guiOpacity;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;}
        void Text(Rect rect,string value,int size=20,Color? color=null,TextAnchor align=TextAnchor.UpperLeft,bool wrap=true){Color tint=color??Ivory;tint.a*=guiOpacity;textStyle.fontSize=size;textStyle.normal.textColor=tint;textStyle.alignment=align;textStyle.wordWrap=wrap;GUI.Label(rect,value,textStyle);}
        float TextHeight(string text,float width,int size){measureContent.text=text;textStyle.fontSize=size;textStyle.wordWrap=true;return Mathf.Ceil(textStyle.CalcHeight(measureContent,width))+4;}
        bool Button(Rect rect,string caption,bool selected=false,bool interactive=true,string key=null,bool hover=true){
            bool hovered=hover&&GUI.enabled&&interactive&&device=="keyboard"&&rect.Contains(Event.current.mousePosition);
            bool highlight=selected||hovered;
            Fill(rect,highlight?Ivory:Ink);
            Fill(new Rect(rect.x,rect.y,rect.width,1),highlight?Gold:new Color(Gold.r,Gold.g,Gold.b,.55f));
            if(highlight)Fill(new Rect(rect.x,rect.y,4,rect.height),Gold);
            var label=rect;
            if(!string.IsNullOrEmpty(key)){
                float width=key.Length>1?40:23;
                var cap=new Rect(rect.xMax-width-8,rect.center.y-11,width,22);
                RoundedFill(cap,highlight?Ink:new Color(.20f,.26f,.34f),4);
                Text(cap,key,14,Ivory,TextAnchor.MiddleCenter);
                label.xMin+=6;label.xMax=cap.x-4;
            }
            Text(label,caption,21,highlight?Ink:Ivory,TextAnchor.MiddleCenter);
            return interactive&&GUI.Button(rect,GUIContent.none,GUIStyle.none);
        }
        string PadKey(string key)=>device=="pad"?key:null;
        bool MenuButton(Rect rect,string caption,bool selected,bool interactive=true)=>
            Button(rect,caption,selected,interactive,selected?PadKey("A"):null,false);
        void MenuGUI(){if(keyart)GUI.DrawTexture(new Rect(0,0,1280,720),keyart,ScaleMode.ScaleAndCrop);else Fill(new Rect(0,0,1280,720),Ink);
            for(int i=0;i<12;i++)Fill(new Rect(i*49,0,50,720),new Color(.04f,.08f,.15f,(142-i*10)/255f));
            Text(new Rect(105,97,450,35),"月下执铃 · 循响而行",18,Gold);Text(new Rect(100,140,520,95),"铃月回响",66);Text(new Rect(108,242,470,32),"L I N G Y U E   E C H O E S",16,Gold);
            Fill(new Rect(108,296,340,1),Gold);Text(new Rect(108,320,430,40),"铃声所至，月光为刃。",21,new Color(.75f,.78f,.86f));
            GUI.enabled=modal=="";
            for(int i=0;i<MenuOptions.Length;i++){
                var option=MenuOptions[i];
                if(MenuButton(option.bounds,option.label,menuChoice==i,modal==""))ActivateMenuOption(i);
            }
            GUI.enabled=true;
            Text(new Rect(105,690,550,24),ControlPrompts.MenuHint(device,false),14);
            Fill(new Rect(1000,640,265,65),Ink);Text(new Rect(1015,650,245,60),$"{Rules.parry} 帧弹刀 · {Rules.parryPerfect} 帧完美\n固定 60Hz 战斗",16,Gold);
            if(motion)for(int i=0;i<14;i++){float t=Time.unscaledTime;Fill(new Rect(60+(i*173)%1160+Mathf.Sin(t*.5f+i*1.7f)*18,((i*97-t*(8+i%4*4))%720+720)%720,8,3),new Color(.95f,.73f,.8f,.5f));}
            if(SavePickerOpen){SavePickerGUI();return;}
            if(modal!=""){Fill(new Rect(0,0,1280,720),new Color(.025f,.04f,.08f,.87f));Fill(new Rect(240,70,800,580),Ink);Text(new Rect(285,100,700,45),modal=="controls"?"循响入门":"声音与界面",30,Gold);
                if(modal=="controls")Text(new Rect(285,160,705,400),ControlPrompts.Summary(device)+$"\n\n弹刀前 {Rules.parryPerfect} 帧完美、共 {Rules.parry} 帧；闪避有效 {Rules.dodgeInvulnFrames} 帧，其中前 {Rules.perfectDodgeFrames} 帧完美。\n闪避 / 跳跃 / 格挡消耗 45 / 30 / 20 气力。\n成功闪避 / 格挡回复 34% 回响；完美成功回复 100% 并回满气力。\n小处决在有效范围内锁定目标，命中消耗 1 回响并使守灯人踉跄。大处决消耗全部回响。\n死亡后从菜单选择重试。",20);
                else{Text(new Rect(285,170,700,55),"声音、动态和序章进度会保存到本机。",21);
                    for(int i=0;i<MenuPanelButtons.Length-1;i++){
                        string label=i==0?"樱花动态    "+(motion?"开启":"关闭"):i==1?"声音    "+(muted?"关闭":"开启"):"触屏按键预览    "+(previewTouchControls?"开启":"关闭");
                        if(MenuButton(MenuPanelButtons[i],label,menuPanelChoice==i))ActivateMenuPanelOption(i);
                    }
                    Text(new Rect(285,500,700,50),"手机自动显示触屏按键 · 映射 Xbox 布局\n显示目标 60 FPS · 固定战斗 60Hz",17,Gold);}
                int backIndex=MenuPanelButtons.Length-1;
                if(MenuButton(MenuPanelButtons[backIndex],"返回",menuPanelChoice==backIndex))CloseMenuModal();
                if(device!="touch")Text(new Rect(355,620,570,26),ControlPrompts.MenuHint(device),14,Gold,TextAnchor.MiddleCenter);}
        }
        void SavePickerGUI(){
            Fill(new Rect(0,0,1280,720),new Color(.025f,.04f,.08f,.78f));
            if(deleteSaveSlot!=0){DeleteSaveGUI();return;}
            RoundedFill(new Rect(333,95,614,499),Ink,10);
            Text(new Rect(375,118,530,50),"选择存档",32,Gold,TextAnchor.MiddleLeft);
            Fill(new Rect(375,175,530,1),new Color(Gold.r,Gold.g,Gold.b,.55f));
            bool canDelete=CanDeleteSelectedSave;
            for(int i=0;i<PrologueLayout.SaveSlots;i++){
                var row=SaveRow(i);bool selected=saveChoice==i,showDelete=selected&&canDelete;
                Fill(row,selected?new Color(.22f,.28f,.37f):new Color(.12f,.18f,.27f));
                Fill(new Rect(row.x,row.y,selected?6:3,row.height),selected?Gold:new Color(.23f,.30f,.40f));
                if(selected)Fill(new Rect(row.x,row.y,row.width,1),Gold);
                Text(new Rect(row.x+21,row.y,145,row.height),$"存档 {i+1:00}",23,Ivory,TextAnchor.MiddleLeft);
                var play=new Rect(row.x,row.y,row.width-(showDelete?104:0),row.height);
                Text(new Rect(row.x+155,row.y,play.width-173,row.height),SaveStatus(i+1),18,Gold,TextAnchor.MiddleRight);
                if(GUI.Button(play,GUIContent.none,GUIStyle.none)){
                    // Touch has no hover: select the row before letting the next tap open it.
                    if(TouchControlsVisible&&!selected)saveChoice=i;
                    else ActivateSaveChoice(i);
                    return;
                }
                if(showDelete&&Button(new Rect(play.xMax,row.y,104,row.height),"删除",key:device=="pad"?"X":"×")){
                    RequestSaveDeletion();return;
                }
            }
            if(MenuButton(SaveBackButton,"返回",saveChoice==PrologueLayout.SaveSlots))ActivateSaveChoice(PrologueLayout.SaveSlots);
            string hint=ControlPrompts.SavePickerHint(device,canDelete);
            Text(new Rect(375,542,530,30),hint,16,Gold,TextAnchor.MiddleCenter);
        }
        void DeleteSaveGUI(){
            RoundedFill(new Rect(333,218,614,284),Ink,10);
            Text(new Rect(375,238,530,50),$"删除存档 {deleteSaveSlot:00}？",30,Gold,TextAnchor.MiddleLeft);
            Fill(new Rect(375,300,530,1),new Color(Gold.r,Gold.g,Gold.b,.55f));
            Text(new Rect(375,315,530,51),"该档的月门灯、狐尾和剧情进度将被清除，无法恢复。",20,Ivory,TextAnchor.MiddleLeft);
            if(MenuButton(DeleteSaveButtons[0],"取消",deleteSaveChoice==0)){ResolveSaveDeletion(false);return;}
            if(MenuButton(DeleteSaveButtons[1],"确认删除",deleteSaveChoice==1)){ResolveSaveDeletion(true);return;}
            string hint=device=="pad"?"左摇杆 ← / → 选择 · A 确认 · B 取消":device=="keyboard"?"← / → 选择 · ENTER 确认 · ESC 取消":"";
            Text(new Rect(375,455,530,28),hint,16,Gold,TextAnchor.MiddleCenter);
        }
        void GameSettingsGUI(){
            RoundedFill(new Rect(914,96,330,342),Ink,9);
            Text(new Rect(934,106,290,37),"游戏设置",22,Gold,TextAnchor.MiddleLeft);
            Fill(new Rect(938,150,282,1),new Color(Gold.r,Gold.g,Gold.b,.6f));
            for(int i=0;i<4;i++){
                string label=i==0?"按键提示  ·  "+(showKeyHints?"开启":"关闭")
                    :i==1?"声音  ·  "+(muted?"关闭":"开启"):i==2?"继续游戏":"返回主菜单";
                if(MenuButton(GameSettingButton(i),label,settingsChoice==i))ActivateGameSetting(i);
            }
            Text(new Rect(925,392,308,35),ControlPrompts.MenuHint(device),13,Gold,TextAnchor.MiddleCenter);
        }
        void BackpackEntryGUI(){
            if(!BackpackAvailable)return;
            if((TouchControlsVisible||Application.isMobilePlatform)&&GUI.Button(BackpackPortrait,GUIContent.none,GUIStyle.none)){
                SetBackpack(true);return;
            }
            if(TouchControlsVisible||Application.isMobilePlatform){
                RoundedFill(new Rect(82,33,71,23),Ink,7);
                Text(new Rect(82,33,71,23),TailArchiveTitle,13,Gold,TextAnchor.MiddleCenter);
            }
            if(backpackGuideSeen||speech.Active||stage!="GATE_READY")return;
            var card=new Rect(153,115,310,54);
            RoundedFill(card,Ink,7);
            Text(new Rect(card.x+12,card.y+2,290,25),TailArchiveTitle+"已解锁",17,Gold,TextAnchor.MiddleLeft);
            if(showKeyHints)Text(new Rect(card.x+12,card.y+26,290,24),ControlPrompts.Format("{backpack} 打开"+TailArchiveTitle,device),15,Ivory,TextAnchor.MiddleLeft);
        }
        void HealGuideGUI(){
            if(!HealAvailable)return;
            float y=stage=="GATE_READY"&&!backpackGuideSeen?177:115;
            if(healGuideTime>0){
                var card=new Rect(153,y,365,54);
                RoundedFill(card,Ink,7);
                Text(new Rect(card.x+12,y+2,341,25),"第一尾能力 · 回响疗愈",17,Gold);
                string description=showKeyHints?ControlPrompts.Format("{heal}  消耗一点回响，恢复少量生命",device):"消耗一点回响，恢复少量生命";
                Text(new Rect(card.x+12,y+26,341,24),description,15,Ivory);
                y+=60;
            }
            if(healFeedbackTime>0)HintText(new Rect(165,y,160,27),healFeedback,16,Ivory);
        }
        static Vector2 TailStarPoint(int index){
            float angle=index*Mathf.PI*2/TailStarPoints;
            return new Vector2(466+Mathf.Sin(angle)*190,392-Mathf.Cos(angle)*190);
        }
        static Rect TailStarNode(int index){var point=TailStarPoint(index);return new Rect(point.x-28,point.y-28,56,56);}
        void StarLine(Vector2 from,Vector2 to,float thickness,Color tint){
            if(Event.current.type!=EventType.Repaint)return;
            var oldMatrix=GUI.matrix;
            var delta=to-from;
            // Position and rotate in backpack coordinates before applying viewport scaling.
            GUI.matrix=oldMatrix*Matrix4x4.TRS(new Vector3(from.x,from.y,0),
                Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg),Vector3.one);
            Fill(new Rect(0,-thickness/2,delta.magnitude,thickness),tint);
            GUI.matrix=oldMatrix;
        }
        void BackpackGUI(){
            TailScreenMargins(MoonNight);
            if(backpackArt)GUI.DrawTexture(new Rect(0,0,1280,720),backpackArt,ScaleMode.ScaleAndCrop);
            else Fill(new Rect(0,0,1280,720),new Color(.045f,.066f,.12f));
            Text(new Rect(274,58,384,64),TailArchiveTitle,36,MoonIvory,TextAnchor.MiddleCenter);
            Text(new Rect(305,119,322,27),"九尾星图   /   每一颗星，记着一条尾巴",16,MoonGold,TextAnchor.MiddleCenter);
            if(Button(new Rect(1030,63,157,49),"关闭",key:PadKey("B"))){SetBackpack(false);return;}
            var dim=new Color(.49f,.61f,.76f,.72f);
            int tails=Mathf.Clamp(sim.TailUpgrades,0,TailStarPoints);
            for(int i=0;i<TailStarPoints;i++)
                StarLine(TailStarPoint(i),TailStarPoint((i+1)%TailStarPoints),1,new Color(.54f,.62f,.72f,.23f));
            for(int i=0;i<TailStarPoints;i++){
                int starLink=(i+2)%TailStarPoints;
                StarLine(TailStarPoint(i),TailStarPoint(starLink),1.5f,i<tails&&starLink<tails?MoonGold:WithAlpha(dim,.37f));
            }
            for(int i=0;i<TailStarPoints;i++){
                Vector2 point=TailStarPoint(i);
                var node=TailStarNode(i);
                var reward=TailRewards[i];
                bool obtained=reward.IsObtained(tails),selected=i==backpackSelection;
                if(selected){
                    float glow=.4f+.18f*Mathf.Sin(backpackTime*5);
                    RoundedFill(new Rect(point.x-34,point.y-34,68,68),WithAlpha(MoonGold,glow),34);
                }
                RoundedFill(node,obtained?reward.Theme:new Color(.28f,.37f,.51f),28);
                RoundedFill(new Rect(node.x+2,node.y+2,52,52),obtained?new Color(.34f,.22f,.14f):new Color(.085f,.12f,.21f),26);
                if(obtained&&tailIcons[i])DrawTintedTexture(new Rect(node.x+6,node.y+6,44,44),tailIcons[i],i==1?reward.Fire:Color.white,ScaleMode.ScaleToFit);
                else Text(node,reward.Order.ToString("00"),20,obtained?reward.Theme:new Color(.66f,.74f,.83f),TextAnchor.MiddleCenter);
                if(GUI.Button(node,GUIContent.none,GUIStyle.none))backpackSelection=i;
            }
            var details=new Rect(744,173,365,382);
            RoundedFill(details,new Color(.075f,.11f,.18f,.95f),12);
            Fill(new Rect(770,232,313,1),new Color(.85f,.74f,.53f,.55f));
            var selectedTail=TailRewards[backpackSelection];
            bool owned=selectedTail.IsObtained(tails);
            Text(new Rect(770,189,312,38),selectedTail.DisplayName,26,selectedTail.Theme,TextAnchor.MiddleLeft);
            Text(new Rect(770,247,312,41),owned?$"已归还 · 生命与气力上限各 +{Rules.tailStatBonus}":"尚未归还",18,Ivory,TextAnchor.MiddleLeft);
            Text(new Rect(770,295,312,30),TailProgress(tails),16,Gold,TextAnchor.MiddleLeft);
            BackpackStat(new Rect(770,343,313,55),"生命上限",sim.player.maxHp,new Color(.30f,.84f,.52f));
            BackpackStat(new Rect(770,419,313,55),"气力上限",sim.PlayerMaxStamina,new Color(1,.81f,.31f));
            if(owned&&backpackSelection==0)Text(new Rect(770,476,313,29),"回响疗愈 · 消耗一点回响恢复生命",15,Gold);
            string guide=device=="pad"?"左摇杆 / 十字键切换   ·   Start / B 关闭":device=="touch"?"轻触星位查看   ·   点击关闭":"方向键切换   ·   B / Esc 关闭";
            Text(new Rect(744,507,365,33),guide,15,Ivory,TextAnchor.MiddleCenter);
        }
        void BackpackStat(Rect rect,string label,double value,Color color){
            RoundedFill(rect,new Color(.115f,.17f,.25f),6);
            Fill(new Rect(rect.x+1,rect.y+8,3,rect.height-16),color);
            Text(new Rect(rect.x+15,rect.y,140,rect.height),label,18,Ivory,TextAnchor.MiddleLeft);
            Text(new Rect(rect.x+150,rect.y,rect.width-166,rect.height),value.ToString("0"),24,color,TextAnchor.MiddleRight);
        }
        void GameGUI(){if(stage=="DYING")return;if(stage=="DEATH"){DeathGUI();return;}if(stage=="STORY"){Fill(new Rect(0,0,1280,720),Color.black);if(stageTime>=.4f&&storyIndex<content.story.Length){float a=storyPhase=="in"?storyTime/.4f:storyPhase=="out"?1-storyTime/.4f:1;Text(new Rect(90,300,1100,150),content.story[storyIndex],30,new Color(.96f,.94f,.88f,a),TextAnchor.MiddleCenter);Text(new Rect(900,650,300,35),ControlPrompts.StoryHint(device),16,Gold);}
                if(Event.current.type==EventType.MouseUp)AdvanceStory();return;}
            if(ChapterStageGUI())return;
            if(stage=="FADE_IN")Fill(new Rect(0,0,1280,720),new Color(0,0,0,1-Mathf.Clamp01(stageTime/.8f)));
            if(stage=="GUARDIAN_INTRO"){TrialIntroGUI();BossHud();return;}
            if(stage=="GUARDIAN_DEFEAT"){PlayerHud();BossHud();return;}
            if(TailStory){TailStoryGUI();return;}
            if(BattleOn||stage=="GATE_DIALOGUE"){
                if(Button(SettingsButton,"设置",gameSettings,key:PadKey("View")))SetGameSettings(true);
                float lessonBottom=TutorialLessonGUI();
                if(BattleOn){PlayerHud();BackpackEntryGUI();HealGuideGUI();
                    if(stage=="GUARDIAN_FIGHT")BossHud();
                    if(showKeyHints&&!gameSettings&&!TouchControlsVisible&&!speech.Active)RightControlHints();
                    bool plungeCombo=sim.CanPlunge||sim.player.state=="plungeLand";
                    string status=clock.paused?"暂停":sim.player.state=="finisher"?"铃月终结":sim.player.state=="execute"?"回响小处决":!plungeCombo&&sim.CanFinish?ControlPrompts.Format("架势崩解 · {attack} 消耗全部回响处决",device):!plungeCombo&&sim.CanExecute?ControlPrompts.Format("{attack} 回响小处决",device):sim.boss.state=="stagger"?"守灯人踉跄":sim.frame-sim.successFrame<24?sim.successKind=="perfectDodge"?"极限闪避 · 气力回满":"完美弹刀 · 气力回满":"";
                    if(status!="")Text(new Rect(330,Mathf.Max(lessonBottom,TouchControlsVisible?195:155),620,44),status,25,Gold,TextAnchor.MiddleCenter);}
                string interaction=Interaction();
                if(interaction==ShrineEntrance||interaction==ShrineReturn||interaction=="进入将军府 · 与赤绫交谈")
                    ChapterInteractionGUI(interaction);
                else if(!TouchControlsVisible&&interaction!=null){
                    if(showKeyHints)KeyPrompt(540,"interact",interaction);
                    else HintText(new Rect(1092,540,160,30),interaction,16,Ivory);
                    if(GUI.Button(new Rect(1030,540,220,30),GUIContent.none,GUIStyle.none))Interact();
                }
                if(speech.Active)DialogueBubble(lessonBottom);
                if(stage=="TUTORIAL"||stage=="GATE_READY")CheckpointGUI();
            }
            if(BattleOn)ChapterFieldGUI();
            if((BattleOn||stage=="GATE_DIALOGUE")&&TouchControlsVisible)MobileControlsGUI();
            if(debug&&BattleOn){Fill(new Rect(15,385,670,168),Ink);Text(new Rect(30,395,640,72),$"60 Hz  Tick {sim.tick}  Frame {sim.frame}  {clock.Rate}x\n玩家 {sim.player.state} [{sim.player.age}]  Boss {sim.boss.state} [{sim.boss.age}]\n停顿 {sim.hitstop}  慢放 {clock.cinematicTicks}  处决窗 {sim.executeWindow}",16);string[] labels={"AI","无敌","暂停","单帧","慢速","回响","崩解","小处决"};for(int i=0;i<8;i++)if(Button(new Rect(24+i*81,493,76,39),labels[i])){if(i==0)sim.ai=!sim.ai;else if(i==1)sim.god=!sim.god;else Act(new[]{"","","pause","step","slow","charge","stun","echoWindow"}[i]);}}
            if(boxes&&BattleOn){DrawBox(sim.Hurtbox(sim.player),Color.green);DrawBox(sim.Hurtbox(sim.boss),Color.green);foreach(var hit in sim.hits)DrawBox(hit,new Color(1,.3f,.3f,.6f));}
        }
        float TutorialLessonGUI(){
            if(stage!="TUTORIAL"||tutorialDialogue||gameSettings)return 0;
            var id=tutorial.Lesson(sim);
            if(id==null)return 0;
            var lesson=Array.Find(content.lessons,l=>l.id==id);
            if(lesson==null)return 0;
            const float x=350,y=144,width=580,inside=544;
            float body=TextHeight(lesson.text,inside,18);
            bool keys=showKeyHints&&!string.IsNullOrEmpty(lesson.controls);
            float height=45+body+(keys?29:0);
            RoundedFill(new Rect(x,y,width,height),Ink,8);
            Text(new Rect(x+18,y+7,inside,27),lesson.title,19,Gold,TextAnchor.MiddleCenter);
            Text(new Rect(x+18,y+34,inside,body),lesson.text,18,Ivory,TextAnchor.MiddleCenter);
            if(keys)Text(new Rect(x+18,y+35+body,inside,26),ControlPrompts.Format(lesson.controls,device),16,Gold,TextAnchor.MiddleCenter);
            return y+height+8;
        }
        void DialogueBubble(float lessonBottom){
            bool manual=tutorialDialogue||stage=="GATE_DIALOGUE";
            var actor=speech.Speaker=="hero"?sim.player:sim.boss;
            var point=StoryPoint(actor,(float)Combat.BodyHeight(actor)+20);
            const float width=380,pad=16,inside=width-2*pad;
            float body=TextHeight(speech.LineText,inside,18),height=16+body+(manual?25:0);
            float x=Mathf.Clamp(point.x-width/2,14,TouchControlsVisible?892-width:1266-width);
            float y=Mathf.Clamp(point.y-height-14,Mathf.Max(136,lessonBottom),TouchControlsVisible?385:480);
            RoundedFill(new Rect(x,y,width,height),Ink,7);
            Text(new Rect(x+pad,y+7,inside,body),speech.VisibleText,18);
            if(manual)Text(new Rect(x+pad,y+7+body,inside,25),(showKeyHints?ControlPrompts.Key("interact",device)+" ":"")+"继续交谈",14,Gold);
        }
        void TailStoryGUI(){
            if(stage=="FIRST_TAIL"){TailRitualGUI();return;}
            if(stage=="TAIL_DIALOGUE"){Fill(new Rect(0,0,1280,38),Ink);Fill(new Rect(0,682,1280,38),Ink);}
            if(stage=="TAIL_EPILOGUE"){
                TailScreenMargins(MoonNight);
                Fill(new Rect(0,0,1280,720),WithAlpha(MoonNight,.94f*Ramp(stageTime,0,.32f)));
                PlayerHud();
                Text(new Rect(340,278,600,45),wrath?"第一章完成 · 焚心余烬":"序章结束 · 月门余音",27,Gold,TextAnchor.MiddleCenter);
                Text(new Rect(275,332,730,118),speech.VisibleText,24,Ivory,TextAnchor.MiddleCenter);
                if(MenuButton(new Rect(565,475,150,44),"继续",true))speech.Confirm();
                return;
            }
            StoryDialogueGUI(wrath?"赤绫":"守灯人");
        }
        void StoryDialogueGUI(string npc){
            RoundedFill(new Rect(252,538,776,123),Ink,8);
            Text(new Rect(276,550,550,30),speech.Speaker=="hero"?"铃月":speech.Speaker=="narration"?"":npc,18,Gold);
            Text(new Rect(276,583,590,60),speech.VisibleText,22,Ivory);
            if(MenuButton(new Rect(896,599,106,42),"继续",true))speech.Confirm();
        }
        Vector2 StoryPoint(Fighter actor,float height)=>StoryPoint(actor.x,(float)actor.y+height);
        Vector2 StoryPoint(double x,float height){
            var p=view.WorldToViewportPoint(new Vector3((float)x/100,(height-95)/100,0));
            return new Vector2(p.x*1280,(1-p.y)*720);
        }
        void RightControlHints(){
            float y=SettingsButton.yMax+12,keyRight=RightHintKeyEdge;
            KeyPrompt(y,"move","移动",keyRight);
            KeyPrompt(y+31,"jump","跳跃",keyRight);
            KeyPrompt(y+62,"attack",sim.CanPlunge?"下落斩":"攻击",keyRight);
            KeyPrompt(y+93,"parry","格挡",keyRight);
            KeyPrompt(y+124,"dodge","闪避",keyRight);
            if(sim.HealUnlocked&&device=="keyboard"){
                bool canTalk=Interaction()!=null;
                KeyPrompt(y+155,canTalk?"interact":"heal",canTalk?"交谈":"回响疗愈",keyRight);
            }else{
                KeyPrompt(y+155,"interact","交谈",keyRight);
                if(sim.HealUnlocked)KeyPrompt(y+186,"heal","回响疗愈",keyRight);
            }
        }
        void KeyPrompt(float y,string action,string caption,float keyRight=1082){
            if(Event.current.type!=EventType.Repaint)return;
            string key=ControlPrompts.Key(action,device);
            float width=action=="move"?116:key.Length>=4?50:key.Length>=2?36:27;
            Rect cap=new Rect(keyRight-width,y+2,width,26);
            RoundedFill(cap,new Color(.85f,.74f,.53f,.85f),5);
            RoundedFill(new Rect(cap.x+1,cap.y+1,cap.width-2,cap.height-2),new Color(.055f,.08f,.125f,.91f),4);
            Text(cap,key,14,Ivory,TextAnchor.MiddleCenter,wrap:false);
            float captionX=keyRight+HintKeyGap;
            HintText(new Rect(captionX,y,Mathf.Min(160,1280-HintRightMargin-captionX),30),caption,16,Ivory);
        }
        void HintText(Rect rect,string caption,int size,Color color){
            if(Event.current.type!=EventType.Repaint)return;
            Color edge=new Color(.025f,.035f,.055f,1);
            Rect outline=rect;
            outline.x=rect.x-1.2f;Text(outline,caption,size,edge,TextAnchor.MiddleLeft,wrap:false);
            outline.x=rect.x+1.2f;Text(outline,caption,size,edge,TextAnchor.MiddleLeft,wrap:false);
            outline.x=rect.x;outline.y=rect.y-1.2f;Text(outline,caption,size,edge,TextAnchor.MiddleLeft,wrap:false);
            outline.y=rect.y+1.5f;Text(outline,caption,size,edge,TextAnchor.MiddleLeft,wrap:false);
            Text(rect,caption,size,color,TextAnchor.MiddleLeft,wrap:false);
        }
        void CheckpointGUI(){
            if(Math.Abs(sim.player.x-PrologueLayout.CheckpointX)>330)return;
            Vector3 p=view.WorldToViewportPoint(new Vector3((float)PrologueLayout.CheckpointX/100,.47f,0));
            float x=p.x*1280,y=(1-p.y)*720-24;
            RoundedFill(new Rect(x-78,y,156,32),Ink,6);
            Text(new Rect(x-76,y,152,32),checkpointToast>0?"进度已保存":checkpointReached?"月门灯 · 已存档":"月门灯 · 存档点",16,checkpointReached?Gold:Ivory,TextAnchor.MiddleCenter);
        }
        void TrialIntroGUI(){
            float t=stageTime,bars=54*Mathf.Min(Ease(Mathf.Clamp01(t/.3f)),Ease(Mathf.Clamp01((TrialIntroDuration-t)/.28f)));
            Fill(new Rect(0,0,1280,bars),Color.black);Fill(new Rect(0,720-bars,1280,bars),Color.black);
            float slash=Mathf.Clamp01((t-TrialSlashTime)/.34f);
            if(slash>0&&slash<1){
                Vector3 blade=view.WorldToViewportPoint(new Vector3((float)sim.boss.x/100-.65f,-.14f,0));
                float x=blade.x*1280-200*slash,y=(1-blade.y)*720-28*slash;
                Matrix4x4 matrix=GUI.matrix;
                GUIUtility.RotateAroundPivot(-14,new Vector2(x,y));
                Fill(new Rect(x-140*slash,y-4,140*slash+85,9),new Color(.99f,.53f,.29f,.55f*(1-slash)));
                Fill(new Rect(x-130*slash,y-1,130*slash+80,3),new Color(1,.94f,.73f,1-slash));
                GUI.matrix=matrix;
            }
            float alpha=Mathf.Min(Mathf.Clamp01((t-1.02f)/.2f),Mathf.Clamp01((TrialIntroDuration-t)/.26f));
            if(alpha>0){
                Fill(new Rect(470,190,340,1),new Color(Gold.r,Gold.g,Gold.b,alpha*.6f));
                Text(new Rect(400,128,480,66),ChapterBoss?"将军府 · 赤绫":"月门试炼",38,new Color(Ivory.r,Ivory.g,Ivory.b,alpha),TextAnchor.MiddleCenter);
            }
        }
        void DeathGUI(){guiOpacity=DeathMenuOpacity;Fill(new Rect(0,0,1280,720),new Color(.018f,.025f,.04f,.76f));RoundedFill(new Rect(340,150,600,420),Ink,18);
            Text(new Rect(380,195,520,65),"铃声沉寂",40,Gold,TextAnchor.MiddleCenter);
            Text(new Rect(380,278,520,48),"气息渐冷，远处的灯仍亮着。",21,Ivory,TextAnchor.MiddleCenter);
            bool ready=DeathMenuReady;
            if(MenuButton(DeathButtons[0],checkpointReached?"在月门灯下苏醒":"再次苏醒",deathChoice==0,ready))Retry();
            if(MenuButton(DeathButtons[1],"返回主界面",deathChoice==1,ready))Home();
            Text(new Rect(365,522,550,30),ControlPrompts.DeathMenuHint(device),16,Gold,TextAnchor.MiddleCenter);guiOpacity=1;
        }
        void MobileControlsGUI(){Vector2 center=MobileControls.StickCenter;
            RoundedFill(new Rect(center.x-86,center.y-86,172,172),new Color(.78f,.85f,.91f,.3f),86);
            RoundedFill(new Rect(center.x-83,center.y-83,166,166),new Color(.04f,.07f,.11f,.56f),83);
            Text(new Rect(center.x-72,center.y-16,28,32),"‹",24,new Color(1,1,1,.35f),TextAnchor.MiddleCenter);
            Text(new Rect(center.x+44,center.y-16,28,32),"›",24,new Color(1,1,1,.35f),TextAnchor.MiddleCenter);
            Vector2 knob=center+mobileControls.Stick*MobileControls.StickTravel;
            RoundedFill(new Rect(knob.x-34,knob.y-34,68,68),mobileControls.StickHeld?new Color(.88f,.93f,.96f,.85f):new Color(.72f,.8f,.87f,.64f),34);
            Text(new Rect(knob.x-26,knob.y-17,52,34),"LS",18,Ink,TextAnchor.MiddleCenter);
            Text(new Rect(center.x-70,center.y+95,140,28),"移动",16,new Color(.9f,.93f,.96f,.7f),TextAnchor.MiddleCenter);
            string interaction=Interaction();bool canTalk=interaction!=null;
            for(int i=0;i<MobileControls.Commands.Length;i++){
                bool heal=i==6;
                if(heal&&!HealAvailable)continue;
                Vector2 p=MobileControls.ButtonCenters[i];bool held=mobileControls.Pressed(MobileControls.Commands[i]);
                float radius=i<4?40:i==5?26:34;
                Color tint=i==0?new Color(.4f,.9f,.4f):i==1?new Color(1,.38f,.33f):i==2?new Color(.32f,.66f,1):i==3?new Color(1,.83f,.23f):heal?new Color(.5f,.93f,.70f):Ivory;
                if(i==4&&!canTalk)tint=new Color(.6f,.65f,.7f,.45f);
                RoundedFill(new Rect(p.x-radius,p.y-radius,radius*2,radius*2),new Color(tint.r,tint.g,tint.b,held?.95f:i==4&&canTalk?.85f:.43f),radius);
                float inset=held?4:2;
                RoundedFill(new Rect(p.x-radius+inset,p.y-radius+inset,(radius-inset)*2,(radius-inset)*2),held?new Color(.1f,.17f,.21f,.92f):new Color(.035f,.055f,.08f,.72f),radius-inset);
                string letter=i==0?"A":i==1?"B":i==2?"X":i==3?"Y":i==4?"RB":heal?"LB":clock.paused?"▶":"Ⅱ";
                string caption=i==0?"跳跃":i==1?"闪避":i==2?"攻击":i==3?"格挡":i==4?(canTalk?"交互":"交谈"):heal?"疗愈":"";
                Text(new Rect(p.x-radius,p.y-(i==5?23:29),radius*2,i==5?46:35),letter,i<4?28:i==5?23:21,tint,TextAnchor.MiddleCenter);
                if(i!=5)Text(new Rect(p.x-radius,p.y+7,radius*2,24),caption,13,tint,TextAnchor.MiddleCenter);
            }
        }
        static Color EchoOrange=>new Color(1,.39f,.08f);
        // Reuse the original character art; read it once without changing its sprite import settings.
        void CreatePlayerHud(){const int size=128;hudRingPixels=new Color[size*size];hudRingCoverage=new float[size*size];hudRingPosition=new float[size*size];
            hudRing=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Player echo arc",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<size;y++)for(int x=0;x<size;x++){int i=y*size+x;float dx=x-63.5f,dy=63.5f-y;
                float angle=Mathf.Repeat(-90-Mathf.Atan2(dy,dx)*Mathf.Rad2Deg,360);
                hudRingPosition[i]=angle/180;hudRingCoverage[i]=angle<=180?Mathf.Clamp01(3.5f-Mathf.Abs(Mathf.Sqrt(dx*dx+dy*dy)-60)):0;}
            var source=Resources.Load<Texture2D>("pixel8/frames/idle/idle-01");if(!source)return;
            var previous=RenderTexture.active;var copy=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
            Texture2D readable=null;
            try{Graphics.Blit(source,copy);RenderTexture.active=copy;readable=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
                readable.ReadPixels(new Rect(0,0,source.width,source.height),0,0);readable.Apply();var original=readable.GetPixels();
                const int portraitSize=96;var pixels=new Color[portraitSize*portraitSize];
                for(int y=0;y<portraitSize;y++)for(int x=0;x<portraitSize;x++){
                    int sx=Mathf.Clamp((int)(source.width*(.385f+.25f*x/(portraitSize-1))),0,source.width-1);
                    int sy=Mathf.Clamp((int)(source.height*(.39f+.375f*y/(portraitSize-1))),0,source.height-1);
                    Color pixel=original[sy*source.width+sx];float dx=x-47.5f,dy=y-47.5f;
                    pixel.a*=Mathf.Clamp01(47.5f-Mathf.Sqrt(dx*dx+dy*dy));pixels[y*portraitSize+x]=pixel;}
                hudPortrait=new Texture2D(portraitSize,portraitSize,TextureFormat.RGBA32,false){name="Lingyue portrait",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                hudPortrait.SetPixels(pixels);hudPortrait.Apply(false,true);
            }finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(copy);if(readable)Destroy(readable);}}
        void RoundedFill(Rect rect,Color color,float radius){color.a*=guiOpacity;GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,Vector4.zero,radius);}
        static float GrowthBarWidth(float baseWidth,float limit,double gain){
            gain=Math.Max(0,gain);
            return Mathf.Lerp(baseWidth,limit,(float)(gain/(gain+30)));
        }
        float HealthHudWidth=>GrowthBarWidth(302,398,sim.player.maxHp-Rules.playerMaxHp);
        float StaminaHudWidth=>GrowthBarWidth(266,374,sim.PlayerMaxStamina-Rules.staminaMax);
        struct HudBarGrowth{
            const float ExtendTime=.72f,FadeTime=.36f;
            float target,elapsed;
            public float Width {get;private set;}
            public float FromWidth {get;private set;}
            public float Glow=>1-Ease(Mathf.Clamp01((elapsed-ExtendTime)/FadeTime));
            public void Reset(float width){Width=FromWidth=target=width;elapsed=ExtendTime+FadeTime;}
            public void Update(float width,float dt){
                if(width<target){Reset(width);return;}
                if(width>target){FromWidth=Width;target=width;elapsed=0;return;}
                elapsed=Mathf.Min(ExtendTime+FadeTime,elapsed+dt);
                Width=Mathf.Lerp(FromWidth,target,Ease(Mathf.Clamp01(elapsed/ExtendTime)));
            }
        }
        void UpdateStatusTrails(float dt){
            var p=sim.player;var b=sim.boss;
            if(stage!="FIRST_TAIL"||stageTime>=3.30f){
                healthBar.Update(HealthHudWidth,dt);
                staminaBar.Update(StaminaHudWidth,dt);
            }
            healthTrail.Update(Mathf.Clamp01((float)(p.hp/p.maxHp)),dt);
            staminaTrail.Update(Mathf.Clamp01((float)(p.stamina/sim.PlayerMaxStamina)),dt);
            float echo=Mathf.Clamp01((float)(p.echo/Rules.echoMax));
            if(p.energy>lastEchoPoints)echoTrail.Reset(echo);else echoTrail.Update(echo,dt);
            lastEchoPoints=p.energy;
            echoPointsTrail.Update(Mathf.Clamp01(p.energy/3f),dt);
            if(stage=="GUARDIAN_FIGHT"||stage=="GUARDIAN_DEFEAT"){
                bossHealthTrail.Update(sim.BossDefeated?0:Mathf.Clamp01((float)(b.hp/b.maxHp)),dt);
                postureTrail.Update(Mathf.Clamp01((float)(b.posture/100)),dt);
            }
        }
        void BossHud(){
            if(Event.current.type!=EventType.Repaint)return;
            var b=sim.boss;
            bool intro=stage=="GUARDIAN_INTRO",defeat=stage=="GUARDIAN_DEFEAT";
            float fill=intro?Ramp(stageTime,1.02f,TrialIntroDuration-.08f):1;
            float opacity=intro?Ramp(stageTime,.92f,1.24f):defeat?1-Ramp(stageTime,.22f,.78f):1;
            if(opacity<=0)return;
            float previous=guiOpacity;guiOpacity*=opacity;
            RoundedFill(new Rect(300,615,680,70),WithAlpha(MoonNight,.89f),9);
            Text(new Rect(318,618,644,28),EncounterName,21,MoonGold,TextAnchor.MiddleCenter);
            float health=defeat?0:Mathf.Clamp01((float)(b.hp/b.maxHp))*fill;
            var healthRect=new Rect(326,649,628,13);
            Bar(healthRect,health,intro?health:bossHealthTrail.Width,new Color(.74f,.32f,.37f));
            Bar(new Rect(326,669,628,6),defeat?0:Mathf.Clamp01((float)(b.posture/100)),defeat?0:postureTrail.Width,MoonGold);
            if(intro&&fill>0&&fill<1)
                Fill(new Rect(328+624*health-2,649,2,13),WithAlpha(MoonIvory,.8f*Mathf.Sin(fill*Mathf.PI)));
            guiOpacity=previous;
        }
        void PlayerHud(){if(Event.current.type!=EventType.Repaint)return;var p=sim.player;
            HudBar(new Rect(136,61,healthBar.Width,20),(float)(p.hp/p.maxHp),healthTrail.Width,new Color(.12f,.78f,.32f),healthBar);
            HudBar(new Rect(136,87,staminaBar.Width,15),(float)(p.stamina/sim.PlayerMaxStamina),staminaTrail.Width,new Color(1,.81f,.12f),staminaBar);
            RoundedFill(new Rect(38,35,106,106),new Color(.025f,.035f,.05f,.98f),53);
            RoundedFill(new Rect(42,39,98,98),new Color(.14f,.18f,.23f),49);
            if(hudPortrait)DrawTintedTexture(new Rect(43,40,96,96),hudPortrait,WithAlpha(Color.white,guiOpacity));
            int echo=Mathf.RoundToInt(Mathf.Clamp01((float)(p.echo/Rules.echoMax))*100),trail=Mathf.RoundToInt(echoTrail.Width*100);
            if(hudRing&&(echo!=hudEcho||trail!=hudEchoTrail)){hudEcho=echo;hudEchoTrail=trail;for(int i=0;i<hudRingPixels.Length;i++){
                    float arc=hudRingPosition[i]*100;
                    Color color=arc<=echo&&echo>0?EchoOrange:arc<=trail&&trail>echo?Color.white:new Color(.72f,.28f,.06f,.8f);
                    color.a*=hudRingCoverage[i];hudRingPixels[i]=color;}
                hudRing.SetPixels(hudRingPixels);hudRing.Apply(false,false);}
            if(hudRing)DrawTintedTexture(new Rect(27,24,128,128),hudRing,WithAlpha(Color.white,guiOpacity));
            for(int i=0;i<3;i++){float angle=(75-i*25)*Mathf.Deg2Rad;float x=91+60*Mathf.Cos(angle),y=88+60*Mathf.Sin(angle);
                RoundedFill(new Rect(x-8,y-8,16,16),new Color(.025f,.035f,.05f,.98f),8);
                RoundedFill(new Rect(x-6,y-6,12,12),EchoOrange,6);
                if(i>=p.energy){
                    RoundedFill(new Rect(x-4,y-4,8,8),new Color(.12f,.09f,.08f),4);
                    float spent=Mathf.Clamp01(echoPointsTrail.Width*3-i);
                    if(spent>0)RoundedFill(new Rect(x-6,y-6,12,12),new Color(1,1,1,spent),6);
                }}
        }
        void HudBar(Rect rect,float ratio,float trail,Color color,HudBarGrowth growth){RoundedFill(rect,new Color(.025f,.035f,.05f,.96f),rect.height/2);
            Rect inner=new Rect(rect.x+3,rect.y+3,rect.width-6,rect.height-6);RoundedFill(inner,new Color(color.r*.18f,color.g*.18f,color.b*.18f),inner.height/2);
            if(trail>ratio){float oldWidth=inner.width*Mathf.Clamp01(trail);RoundedFill(new Rect(inner.x,inner.y,oldWidth,inner.height),Color.white,Math.Min(inner.height,oldWidth)/2);}
            float width=inner.width*Mathf.Clamp01(ratio);if(width>0)RoundedFill(new Rect(inner.x,inner.y,width,inner.height),color,Math.Min(inner.height,width)/2);
            float glow=growth.Glow, start=growth.FromWidth-6;
            float added=inner.width-start;
            if(glow<=0||added<=0)return;
            float filled=width-start;
            if(filled>0)RoundedFill(new Rect(inner.x+start,inner.y,filled,inner.height),new Color(1,.9f,.6f,.76f*glow),Mathf.Min(filled,inner.height)/2);
            // The edge marks new capacity even if part of the bar is empty.
            var gold=new Color(1,.86f,.52f,.85f*glow);
            float edge=added-inner.height*.5f,tip=Mathf.Min(added,2.5f);
            if(edge>0)RoundedFill(new Rect(inner.x+start,inner.y-1,edge,2),gold,1);
            RoundedFill(new Rect(inner.xMax-tip,inner.center.y-2,tip,4),gold,tip/2);
        }
        void Bar(Rect rect,float ratio,float trail,Color color){Fill(rect,Ink);
            if(trail>ratio)Fill(new Rect(rect.x+2,rect.y+2,(rect.width-4)*trail,rect.height-4),Color.white);
            Fill(new Rect(rect.x+2,rect.y+2,(rect.width-4)*Mathf.Clamp01(ratio),rect.height-4),color);}
        void DrawBox(Box box,Color color){Vector3 a=view.WorldToViewportPoint(new Vector3((float)box.x/100,((float)box.y-95)/100,0)),b=view.WorldToViewportPoint(new Vector3((float)(box.x+box.w)/100,((float)(box.y+box.h)-95)/100,0));Rect r=new Rect(a.x*1280,(1-b.y)*720,(b.x-a.x)*1280,(b.y-a.y)*720);Fill(new Rect(r.x,r.y,r.width,1),color);Fill(new Rect(r.x,r.yMax,r.width,1),color);Fill(new Rect(r.x,r.y,1,r.height),color);Fill(new Rect(r.xMax,r.y,1,r.height),color);}
    }
}
