using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LingyueEchoes
{
    public class LingyueGame:MonoBehaviour
    {
        public string stage="MENU";
        [NonSerialized] public Combat sim; [NonSerialized] public FixedClock clock;
        [NonSerialized] internal double? scriptedAxis;
        static string lastInputDevice;
        static int? pendingSaveSlot;
        Transform world;Camera view;PixelActor hero,guardian;PixelEffects effects;PrologueContent content;
        TutorialDirector tutorial;DialogueDirector speech=new DialogueDirector();
        float stageTime,cameraX=-960,nextPadCheck,checkpointToast,bladeDrawTime,tailFormTime;string modal="";
        bool debug,boxes,focused=true,motion,muted,hadTouch,showKeyHints,gameSettings;
        int settingsChoice,settingsDirection;
        static readonly Rect SettingsButton=new Rect(1125,35,120,52);
        static readonly (Rect bounds,string label)[] MenuOptions={
            (new Rect(105,395,340,62),"开始游戏"),
            (new Rect(105,477,340,58),"操作说明"),
            (new Rect(105,553,340,58),"声音与界面")
        };
        const string SavePicker="saves";
        static readonly Rect SaveBackButton=new Rect(520,476,240,49);
        static Rect SaveRow(int index)=>new Rect(375,192+index*88,530,72);
        int menuChoice,menuDirection,saveChoice;
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
        static int SaveValue(string key,int slot)=>PlayerPrefs.GetInt(PrologueLayout.SaveKey(key,slot),0);
        static bool HasMoonGateSave(int slot)=>SaveValue(PrologueLayout.CheckpointKey,slot)==1;
        static bool StorySeen(int slot)=>SaveValue(PrologueLayout.FirstVictoryStoryKey,slot)==1;
        bool FirstVictoryStorySeen=>StorySeen(SaveSlot);
        internal static string SaveStatus(int slot)=>StorySeen(slot)?"序章已完成 · 从月门灯继续":HasMoonGateSave(slot)?"月门灯 · 继续游戏":"空档 · 从序幕开始";
        void WriteSave(string key,int value)=>PlayerPrefs.SetInt(PrologueLayout.SaveKey(key,SaveSlot),value);
        string storyPhase="in";bool tutorialDialogue,phase2Spoken,deathAxisHeld;
        public float DeathMenuOpacity=>stage=="DEATH"?Ease(Mathf.Clamp01(stageTime/DeathMenuFadeDuration)):0;
        public bool DeathMenuReady=>stage=="DEATH"&&stageTime>=DeathMenuFadeDuration;
        Texture2D keyart,hudPortrait,hudRing,firstTail;Font font;GUIStyle textStyle,buttonStyle;
        readonly GUIContent measureContent=new GUIContent();
        Color[] hudRingPixels;float[] hudRingCoverage,hudRingPosition;int hudEcho=-1;
        AudioSource[] voices;AudioSource uiVoice,music;readonly Dictionary<string,AudioClip> soundClips=new Dictionary<string,AudioClip>();
        Combat soundModel;string musicKey="";
        readonly MobileControls mobileControls=new MobileControls();bool previewTouchControls;
        public bool PreviewTouchControls {get=>previewTouchControls;set{previewTouchControls=value;inputDevice.Select(value||Application.isMobilePlatform?"touch":"keyboard");ClearInput();}}
        bool TouchControlsVisible=>previewTouchControls||device=="touch";
        public bool BattleOn=>stage=="TUTORIAL"||stage=="GATE_READY"||stage=="GUARDIAN_FIGHT";
        bool TailStory=>stage=="TAIL_DIALOGUE"||stage=="FIRST_TAIL"||stage=="TAIL_EPILOGUE";
        bool WakeDialogue=>stage=="TUTORIAL"&&speech.Id=="routeWake";
        internal bool BrokenStepsCleared=>tutorial.StepsCleared;
        void Awake(){Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Input.simulateMouseWithTouches=true;
            content=JsonUtility.FromJson<PrologueContent>(Resources.Load<TextAsset>("Data/prologue-content").text);PixelWorld.Load();
            view=Camera.main;if(!view){var camera=new GameObject("Main Camera");camera.tag="MainCamera";view=camera.AddComponent<Camera>();}
            view.orthographic=true;view.orthographicSize=3.6f;view.transform.position=new Vector3(0,0,-10);view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.043f,.075f,.106f);
            if(!FindAnyObjectByType<AudioListener>())view.gameObject.AddComponent<AudioListener>();
            world=transform.Find("World");if(!world)world=PixelWorld.Create(transform);
            PixelWorld.PlaceBrokenSteps(world.Find("Props"));
            checkpointLamp=PixelWorld.PlaceCheckpoint(world.Find("Props"));
            leftSeal=PixelWorld.PlaceArenaSeal(world.Find("Props"),true);
            rightSeal=PixelWorld.PlaceArenaSeal(world.Find("Props"),false);
            hero=new PixelActor(world.Find("Hero").gameObject,true);guardian=new PixelActor(world.Find("Guardian").gameObject,false);effects=new PixelEffects(world);
            keyart=Resources.Load<Texture2D>("ui/main_menu_keyart");
            firstTail=Resources.Load<Texture2D>("prologue_route/first-fox-tail");
            CreatePlayerHud();
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Noto Sans CJK SC","Droid Sans Fallback","Arial Unicode MS"},24);
            motion=PlayerPrefs.GetString("lingyue.menuMotion","on")!="off";muted=PlayerPrefs.GetString("lingyue.sfxMuted","off")=="on";previewTouchControls=PlayerPrefs.GetInt("lingyue.touchControls",0)==1;
            showKeyHints=PlayerPrefs.GetInt("lingyue.keyHints",1)==1;
            inputDevice.Select(lastInputDevice??(Application.isMobilePlatform||previewTouchControls?"touch":Input.GetJoystickNames().Any(n=>!string.IsNullOrWhiteSpace(n))?"pad":"keyboard"));
            voices=new AudioSource[12];for(int i=0;i<12;i++)voices[i]=gameObject.AddComponent<AudioSource>();uiVoice=gameObject.AddComponent<AudioSource>();music=gameObject.AddComponent<AudioSource>();music.loop=true;music.volume=.22f;
            foreach(string name in new[]{"step","jump","land","slash1","slash2","slash3","guard","dodge","counter","hit_player","hit_boss","parry","perfect_dodge","boss_swing","break","phase","death","victory","ui_confirm","ui_back"})soundClips[name]=Resources.Load<AudioClip>("audio/sfx/"+name);
            speech.onClose=id=>{
                if(stage=="GATE_DIALOGUE"){BeginTrialIntro();return;}
                if(stage=="TUTORIAL"){tutorialDialogue=false;tutorial.FinishDialogue(sim,id);}
                if(id=="tail"&&stage=="TAIL_DIALOGUE"){SetStage("FIRST_TAIL");UiSound("victory");}
                if(id=="tailAfter"&&stage=="TAIL_EPILOGUE")CompleteFirstVictory();
            };
            Home();
            if(SceneManager.GetActiveScene().name=="Prologue"){
                SaveSlot=pendingSaveSlot??1;pendingSaveSlot=null;BeginTrial();
            }
        }
        public void StartTrial(){
            if(stage!="MENU"||modal!="")return;
            saveChoice=SaveSlot-1;modal=SavePicker;UiSound("ui_confirm");
        }
        internal void ActivateSaveChoice(int choice){
            if(!SavePickerOpen||choice<0||choice>PrologueLayout.SaveSlots)return;
            if(choice==PrologueLayout.SaveSlots){CloseMenuModal();return;}
            SaveSlot=choice+1;modal="";UiSound("ui_confirm");
            if(SceneManager.GetActiveScene().name!="Prologue"){
                pendingSaveSlot=SaveSlot;SceneManager.LoadScene("Prologue");
            }else BeginTrial();
        }
        public void BeginTrial(){
            bool resume=HasMoonGateSave(SaveSlot);
            PrepareRoute(resume);
            storyIndex=0;storyTime=0;storyPhase="in";
            SetStage(resume?"FADE_IN":"STORY");
            if(resume)SetMusic("prologue_stage_1");
        }
        void PrepareRoute(bool fromCheckpoint){
            sim=new Combat{continueAfterDeath=true,bossYields=true,ai=false,enemyActive=false};
            clock=new FixedClock(sim);sim.boundsMin=-1560;sim.boundsMax=PrologueLayout.ArenaMax;
            checkpointReached=fromCheckpoint;checkpointToast=0;tutorial=new TutorialDirector();
            sim.player.x=fromCheckpoint?PrologueLayout.CheckpointX:-1400;
            sim.boss.x=fromCheckpoint?PrologueLayout.GuardianWaitX:-1235;
            if(!fromCheckpoint){tutorial.Begin(sim,PrologueLayout.BrokenSteps());sim.beforeStep=tutorial.Pump;}
            else sim.terrain.Add(PrologueLayout.BrokenSteps());
            speech.Reset();tutorialDialogue=false;effects.Clear();cameraX=-960;ClearInput();
            world.gameObject.SetActive(true);SetArenaSeals(false);UpdateCheckpointLamp();gameSettings=false;
        }
        public void Home(){ClearInput();speech.Reset();effects?.Clear();modal="";gameSettings=false;menuChoice=menuDirection=0;menuPointer=Input.mousePosition;SetStage("MENU");world.gameObject.SetActive(false);SetMusic("menu_stage_select");}
        void SetStage(string value){stage=value;stageTime=0;}
        void BeginTrialIntro(){
            ClearInput();sim.beforeStep=null;sim.nonlethal=false;sim.dummyBox=null;
            sim.boundsMin=PrologueLayout.ArenaMin;sim.boundsMax=PrologueLayout.ArenaMax;
            sim.ResetBoss(PrologueLayout.GuardianFightX,520);
            var p=sim.player;p.x=PrologueLayout.PlayerFightX;p.y=p.vy=0;p.face=1;
            p.state="idle";p.move=null;p.age=p.combo=p.invuln=0;
            sim.ai=sim.enemyActive=false;clock=new FixedClock(sim);effects.Clear();
            phase2Spoken=false;cameraX=(float)PrologueLayout.ArenaCenter;
            SetStage("GUARDIAN_INTRO");SetArenaSeals(true);
        }
        void FinishGuardianFight(){
            StopSounds();SetArenaSeals(false);SetMusic("prologue_stage_1");
            sim.boundsMin=-1560;
            // Older save flags describe progression, not whether this closing scene has played.
            if(FirstVictoryStorySeen){
                SetStage("GATE_READY");Say("rematchWin");return;
            }
            ClearInput();effects.Clear();speech.Reset();tailFormTime=0;
            var p=sim.player;var b=sim.boss;
            p.state="idle";p.move=null;p.age=0;p.y=p.vy=0;b.y=b.vy=0;
            p.face=b.x>=p.x?1:-1;b.face=-p.face;
            SetStage("TAIL_DIALOGUE");Say("tail");
        }
        void CompleteFirstVictory(){
            WriteSave(PrologueLayout.TailCountKey,Math.Max(1,SaveValue(PrologueLayout.TailCountKey,SaveSlot)));
            WriteSave(PrologueLayout.ProgressKey,1);
            WriteSave(PrologueLayout.FirstVictoryStoryKey,1);PlayerPrefs.Save();
            SetStage("GATE_READY");
        }
        void SaveCheckpoint(){
            checkpointReached=true;checkpointToast=2.4f;UpdateCheckpointLamp();
            WriteSave(PrologueLayout.CheckpointKey,1);PlayerPrefs.Save();UiSound("ui_confirm");
        }
        void UpdateCheckpointLamp(){checkpointLamp.color=checkpointReached?new Color(1,.84f,.53f):new Color(.51f,.59f,.68f);}
        public void Retry(){if(!DeathMenuReady||sim==null)return;PrepareRoute(checkpointReached);SetStage("FADE_IN");SetMusic("prologue_stage_1");UiSound("ui_confirm");}
        void EnterDeath(){
            // Only player commands stop. The fixed clock, pending enemy moves, gravity, effects and audio continue.
            mobileControls.Reset();sim.ClearCommands();clock.paused=false;clock.ClearAccumulation();
            speech.Reset();tutorialDialogue=false;deathChoice=0;deathAxisHeld=false;deathFrame=sim.frame-Math.Max(0,sim.player.age-1);deathZoomTime=0;
            deathAnimationDuration=PixelWorld.Definition("pixel8","dead")?.duration??1.07f;
            deathCameraPosition=view.transform.position;deathCameraSize=view.orthographicSize;SetStage("DYING");}
        void UpdateDeath(float dt){sim.axis=0;clock.Advance(dt);SyncSounds();
            if(stage!="DYING")return;
            deathZoomTime=Math.Min(DeathZoomDuration,deathZoomTime+dt);
            if(deathZoomTime>=DeathZoomDuration&&(sim.frame-deathFrame)/60f>=deathAnimationDuration&&sim.player.vy==0)SetStage("DEATH");
        }
        void Update(){float dt=Math.Min(Time.unscaledDeltaTime,.25f);UpdateViewport();if(!focused&&!Application.isBatchMode)return;ReadInput();
            if(music)music.mute=muted;
            if(gameSettings)return;
            stageTime+=dt;
            switch(stage){case "MENU":break;
                case "STORY":UpdateStory(dt);break;
                case "FADE_IN":if(stageTime>=.8f)SetStage("CAMERA_REVEAL");break;
                case "CAMERA_REVEAL":if(stageTime>=3.5f)SetStage("WAKE_UP");break;
                case "WAKE_UP":if(stageTime>=.95f){SetStage(checkpointReached?"GATE_READY":"TUTORIAL");SetMusic("prologue_stage_1");}break;
                case "GATE_DIALOGUE":if(speech.Cue=="drawBlade")bladeDrawTime+=dt;break;
                case "TAIL_DIALOGUE":if(speech.Cue=="formTail"||tailFormTime>0)tailFormTime+=dt;break;
                case "TAIL_EPILOGUE":break;
                case "FIRST_TAIL":
                    if(stageTime>=2.75f){SetStage("TAIL_EPILOGUE");Say("tailAfter");}
                    break;
                case "GUARDIAN_INTRO":
                    if(stageTime>=TrialSlashTime&&stageTime-dt<TrialSlashTime)UiSound("boss_swing");
                    if(stageTime>=TrialIntroDuration){sim.ai=sim.enemyActive=true;SetStage("GUARDIAN_FIGHT");SetMusic("moon_gate_boss");}
                    break;
                case "DYING":case "DEATH":UpdateDeath(dt);break;
                default:
                    if(BattleOn){sim.axis=WakeDialogue?0:ReadAxis();clock.Advance(dt);SyncSounds();
                        if(sim.player.hp<=0&&sim.player.state=="dead"){EnterDeath();}
                        else if(stage=="TUTORIAL"){var id=tutorial.Speech(sim,!speech.Active);if(id!=null)Say(id);
                            if(!checkpointReached&&tutorial.GateBound&&sim.player.x>=PrologueLayout.CheckpointX)SaveCheckpoint();}
                        else if(stage=="GUARDIAN_FIGHT"&&sim.BossDefeated)FinishGuardianFight();
                        else if(stage=="GUARDIAN_FIGHT"&&sim.phase==2&&!phase2Spoken){phase2Spoken=true;Say("bossPhase2");}}
                    break;}
            if(speech.Active)speech.Update(dt);if(stage!="MENU")DrawWorld(dt);
            checkpointToast=Math.Max(0,checkpointToast-dt);
        }
        void UpdateStory(float dt){if(stageTime<.4f)return;storyTime+=dt;float span=storyPhase=="hold"?(float)DialogueDirector.ReadingHold(content.story[storyIndex].Length):.4f;if(storyTime>=span){storyTime=0;if(storyPhase=="in")storyPhase="hold";else if(storyPhase=="hold")storyPhase="out";else AdvanceStory();}}
        void AdvanceStory(){if(stage!="STORY"||stageTime<.4f)return;storyIndex++;storyPhase="in";storyTime=0;if(storyIndex>=content.story.Length){SetStage("FADE_IN");SetMusic("prologue_stage_1");}}
        void DrawWorld(float dt){var p=sim.player;var b=sim.boss;float zoom=1,focusY=0;
            if(stage=="DYING"||stage=="DEATH"){float t=Ease(Mathf.Clamp01(deathZoomTime/DeathZoomDuration));
                var target=new Vector3((float)p.x/100,((float)p.y-55)/100,-10);view.orthographicSize=Mathf.Lerp(deathCameraSize,3.6f/2.2f,t);view.transform.position=Vector3.Lerp(deathCameraPosition,target,t);}
            else{if(stage=="FADE_IN"||stage=="CAMERA_REVEAL"){float t=stage=="FADE_IN"?0:Mathf.Clamp01((stageTime-.7f)/2.8f),ease=Ease(t);zoom=Mathf.Lerp(2.2f,1,ease);cameraX=Mathf.Lerp((float)p.x,RouteCameraX(),ease);focusY=Mathf.Lerp((float)p.y+40,0,ease);}
                else if(stage=="GUARDIAN_INTRO"){float t=stageTime/TrialIntroDuration;zoom=1+.17f*Mathf.Sin(t*Mathf.PI);focusY=12*Mathf.Sin(t*Mathf.PI);}
                else if(TailStory){float focus=(float)((p.x+b.x)/2);cameraX=Mathf.Lerp(cameraX,focus,1-Mathf.Exp(-4*dt));zoom=1.14f;focusY=16;}
                else{float focus=stage=="TUTORIAL"||stage=="GATE_READY"||stage=="GATE_DIALOGUE"||stage=="WAKE_UP"?RouteCameraX():(float)PrologueLayout.ArenaCenter;cameraX=Mathf.Lerp(cameraX,focus,1-Mathf.Exp(-8*dt));}
                view.orthographicSize=3.6f/zoom;view.transform.position=new Vector3(cameraX/100,focusY/100,-10);}
            string pose=null;int cell=7;float angle=0,lift=0;
            if(stage=="FADE_IN"||stage=="CAMERA_REVEAL"){pose="dead";cell=7;angle=-8;}
            if(stage=="WAKE_UP"){float t=Ease(Mathf.Clamp01(stageTime/.95f));pose=stageTime<.95f*.45f?"dead":"hurt";cell=pose=="dead"?7:3;angle=(pose=="dead"?-8:-46)*(1-t);lift=pose=="dead"?0:-6*(1-t);}
            if(stage=="DYING"||stage=="DEATH"){pose="dead";cell=Math.Min(7,(sim.frame-deathFrame)*8/60);}
            if(stage=="GUARDIAN_INTRO"&&stageTime>=.65f&&stageTime<1.65f){pose="parry";cell=Mathf.Clamp((int)((stageTime-.65f)*8),0,7);}
            hero.Draw(p,sim.frame,false,pose,cell,angle,lift);
            if(stage=="GUARDIAN_INTRO"){
                int slashCell=stageTime<TrialSlashTime?Mathf.Clamp((int)((stageTime-.22f)*6),0,3):Mathf.Clamp(4+(int)((stageTime-TrialSlashTime)*8),4,7);
                guardian.Draw(b,sim.frame,pose:stageTime>=.22f&&stageTime<1.62f?"slash":"idle",poseCell:stageTime>=1.62f?0:slashCell);
            }else if(stage=="GATE_DIALOGUE"&&speech.Cue=="drawBlade")guardian.Draw(b,sim.frame,pose:"slash",poseCell:Mathf.Min(3,(int)(bladeDrawTime*5)));
            else if(TailStory)guardian.Draw(b,sim.frame,pose:"broken",poseCell:7);
            else guardian.Draw(b,sim.frame);
            effects.Draw(sim);
        }
        float RouteCameraX()=>Mathf.Clamp((float)sim.player.x+100,-960,(float)PrologueLayout.ArenaCenter);
        static float Ease(float t)=>t<.5f?4*t*t*t:1-Mathf.Pow(-2*t+2,3)/2;
        void Say(string id){var script=Array.Find(content.scripts,s=>s.id==id);if(script!=null){if(stage=="TUTORIAL")tutorial.BeginDialogue(id);speech.Play(script);}}
        string Interaction(){
            if((tutorialDialogue||stage=="GATE_DIALOGUE")&&speech.Active)return "继续";
            if(stage=="GATE_READY"&&Math.Abs(sim.player.x-sim.boss.x)<210)return FirstVictoryStorySeen?"再次挑战":"交谈";
            return stage=="TUTORIAL"&&tutorial.CanInteract(sim)?"交谈":null;
        }
        public void Interact(){
            if(clock==null||clock.paused)return;
            string interaction=Interaction();
            if(interaction==null)return;
            if(interaction=="继续"){speech.Confirm();return;}
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
            rightSeal.color=active?Color.white:new Color(.71f,.73f,.85f);
        }
        void SetMusic(string name){if(musicKey==name)return;musicKey=name;var clip=Resources.Load<AudioClip>("contentpack_20261104/music/"+name);if(!clip)return;music.Stop();music.clip=clip;music.Play();}
        void StopSounds(){if(voices!=null)foreach(var voice in voices)voice.Stop();}
        void UiSound(string name){if(!muted&&soundClips.TryGetValue(name,out var clip)&&clip){uiVoice.Stop();uiVoice.clip=clip;uiVoice.volume=.45f;uiVoice.Play();}}
        void SyncSounds(){if(soundModel!=sim){StopSounds();soundModel=sim;soundCursor=0;}foreach(var cue in sim.sounds){if(cue.id<=soundCursor)continue;soundCursor=cue.id;if(muted||clock.paused||cue.name=="victory")continue;if(!soundClips.TryGetValue(cue.name,out var clip)||!clip)continue;var voice=voices.FirstOrDefault(v=>!v.isPlaying)??voices[voiceIndex++%voices.Length];voice.Stop();voice.clip=clip;voice.volume=cue.name=="step"?.22f:cue.name=="guard"?.3f:.65f;voice.Play();}}
        void UpdateViewport(){float aspect=(float)Screen.width/Screen.height,desired=16f/9;if(aspect>desired){float w=desired/aspect;view.rect=new Rect((1-w)/2,0,w,1);}else{float h=aspect/desired;view.rect=new Rect(0,(1-h)/2,1,h);}}
        Vector2 UiPoint(Vector2 screen){float scale=Math.Min(Screen.width/1280f,Screen.height/720f);return new Vector2((screen.x-(Screen.width-1280*scale)/2)/scale,(Screen.height-screen.y-(Screen.height-720*scale)/2)/scale);}
        void DetectInputDevice(){bool keyboard=Input.anyKeyDown&&keyboardKeys.Any(k=>Input.GetKeyDown(k)),padButton=false;
            for(int i=0;i<20;i++)padButton|=Input.GetKeyDown(KeyCode.JoystickButton0+i);
            bool touch=Input.touchCount>0;
            bool mouse=!touch&&!hadTouch&&(Input.GetMouseButtonDown(0)||Input.GetMouseButtonDown(1)||Input.GetMouseButtonDown(2));
            inputDevice.Observe(keyboard||mouse&&!previewTouchControls,padButton,touch||mouse&&previewTouchControls,Input.GetAxisRaw("PadX"),Input.GetAxisRaw("PadDpad"));
            hadTouch=touch;
            if(Time.unscaledTime>=nextPadCheck){nextPadCheck=Time.unscaledTime+1;
                if(device=="pad"&&!Input.GetJoystickNames().Any(n=>!string.IsNullOrWhiteSpace(n)))inputDevice.Select(Application.isMobilePlatform||previewTouchControls?"touch":"keyboard");}
            lastInputDevice=device;
        }
        void ReadInput(){DetectInputDevice();bool padA=Input.GetKeyDown(KeyCode.JoystickButton0),padB=Input.GetKeyDown(KeyCode.JoystickButton1);bool confirm=Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space)||padA;
            if(stage=="DYING")return;if(stage=="DEATH"){ReadDeathInput(confirm,padB);return;}
            if(TailStory){
                if(confirm||Input.GetKeyDown(KeyCode.E)||Input.GetKeyDown(KeyCode.JoystickButton5))speech.Confirm();
                return;
            }
            bool escape=Input.GetKeyDown(KeyCode.Escape),menu=Input.GetKeyDown(KeyCode.JoystickButton7);
            if(gameSettings){ReadGameSettings(confirm,padB||escape||menu);return;}
            if((BattleOn||stage=="GATE_DIALOGUE")&&(escape||menu)){SetGameSettings(true);return;}
            if(stage=="MENU"){ReadMenuInput(confirm,padB||escape);return;}
            if(escape){Home();return;}
            if(stage=="STORY"){if(padB){Home();return;}if(confirm)AdvanceStory();return;}
            if(!BattleOn&&stage!="GATE_DIALOGUE")return;
            if(Input.GetKeyDown(KeyCode.E)||Input.GetKeyDown(KeyCode.JoystickButton5))Interact();
            if(stage=="GATE_DIALOGUE"){ReadTouchInput();return;}
            if(!BattleOn)return;
            if(Input.GetKeyDown(KeyCode.P))Act("pause");if(Input.GetKeyDown(KeyCode.O))Act("step");if(Input.GetKeyDown(KeyCode.T))Act("slow");
            if(Input.GetKeyDown(KeyCode.F1))debug=!debug;if(Input.GetKeyDown(KeyCode.F2))boxes=!boxes;
            if(Input.GetKeyDown(KeyCode.J)||Input.GetKeyDown(KeyCode.JoystickButton2))Act("attack");if(Input.GetKeyDown(KeyCode.K)||Input.GetKeyDown(KeyCode.JoystickButton3))Act("parry");
            if(Input.GetKeyDown(KeyCode.LeftShift)||Input.GetKeyDown(KeyCode.RightShift)||padB)Act("dodge");if(Input.GetKeyDown(KeyCode.Space)||padA)Act("jump");
            if(debug)for(int i=0;i<6;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i))sim.DebugBoss(i);
            ReadTouchInput();
        }
        void ReadMenuInput(bool confirm,bool back){
            float vertical=Input.GetAxisRaw("Vertical");
            int direction=vertical>.5f?-1:vertical<-.5f?1:0;
            bool moveSelection=direction!=0&&direction!=menuDirection;
            menuDirection=direction;
            Vector2 pointer=Input.mousePosition;
            bool moved=pointer!=menuPointer;
            menuPointer=pointer;
            if(modal!=""){
                if(back){CloseMenuModal();return;}
                if(!SavePickerOpen)return;
                int count=PrologueLayout.SaveSlots+1;
                if(moveSelection)saveChoice=(saveChoice+direction+count)%count;
                else if(moved&&!TouchControlsVisible){
                    Vector2 point=UiPoint(pointer);
                    if(SaveBackButton.Contains(point))saveChoice=PrologueLayout.SaveSlots;
                    else for(int i=0;i<PrologueLayout.SaveSlots;i++)if(SaveRow(i).Contains(point)){saveChoice=i;break;}
                }
                if(confirm)ActivateSaveChoice(saveChoice);
                return;
            }
            if(back)return;
            if(moveSelection)menuChoice=(menuChoice+direction+MenuOptions.Length)%MenuOptions.Length;
            else if(moved&&!TouchControlsVisible){
                Vector2 point=UiPoint(pointer);
                for(int i=0;i<MenuOptions.Length;i++)if(MenuOptions[i].bounds.Contains(point)){menuChoice=i;break;}
            }
            if(confirm)ActivateMenuOption(menuChoice);
        }
        void CloseMenuModal(){modal="";UiSound("ui_back");}
        void ActivateMenuOption(int option){
            menuChoice=option;
            switch(option){
                case 0:StartTrial();break;
                case 1:UiSound("ui_confirm");modal="controls";break;
                case 2:UiSound("ui_confirm");modal="settings";break;
            }
        }
        void SetGameSettings(bool open){
            ClearInput();gameSettings=open;
            if(open){settingsChoice=0;settingsDirection=0;}
            UiSound(open?"ui_confirm":"ui_back");
        }
        void ReadGameSettings(bool confirm,bool back){
            if(back){SetGameSettings(false);return;}
            float vertical=Input.GetAxisRaw("Vertical");
            int direction=vertical>.5f?-1:vertical<-.5f?1:0;
            if(direction!=0&&direction!=settingsDirection)settingsChoice=(settingsChoice+direction+4)%4;
            settingsDirection=direction;
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
        void ReadDeathInput(bool confirm,bool back){if(!DeathMenuReady)return;
            float vertical=Input.GetAxisRaw("Vertical");bool axis=Math.Abs(vertical)>.5f;
            if(Input.GetKeyDown(KeyCode.UpArrow)||Input.GetKeyDown(KeyCode.W)||axis&&!deathAxisHeld&&vertical>0)deathChoice=0;
            if(Input.GetKeyDown(KeyCode.DownArrow)||Input.GetKeyDown(KeyCode.S)||axis&&!deathAxisHeld&&vertical<0)deathChoice=1;
            deathAxisHeld=axis;
            if(back||Input.GetKeyDown(KeyCode.Escape)){Home();return;}
            if(confirm){if(deathChoice==0)Retry();else Home();}
        }
        void ReadTouchInput(){for(int i=0;i<Input.touchCount;i++){
                var touch=Input.GetTouch(i);
                if(touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled){mobileControls.End(touch.fingerId);continue;}
                Vector2 point=UiPoint(touch.position);
                if(touch.phase==TouchPhase.Began)MobileAction(mobileControls.Begin(touch.fingerId,point));
                else mobileControls.Move(touch.fingerId,point);
            }
            // Menu GUI still uses Unity's touch-to-mouse simulation; action controls consume real touches only once.
            const int mousePointer=int.MinValue;
            if(Input.touchCount>0){mobileControls.End(mousePointer);return;}
            if(!TouchControlsVisible)return;
            if(Input.GetMouseButtonDown(0))MobileAction(mobileControls.Begin(mousePointer,UiPoint(Input.mousePosition)));
            else if(Input.GetMouseButton(0))mobileControls.Move(mousePointer,UiPoint(Input.mousePosition));
            if(Input.GetMouseButtonUp(0)||!Input.GetMouseButton(0))mobileControls.End(mousePointer);
        }
        void MobileAction(string action){if(action==null||clock.paused&&action!="pause")return;if(action=="interact")Interact();else Act(action);}
        double ReadAxis(){if(scriptedAxis.HasValue)return scriptedAxis.Value;float pad=Input.GetAxisRaw("PadX"),dpad=Input.GetAxisRaw("PadDpad");if(Math.Abs(pad)>.2f)return pad;if(Math.Abs(dpad)>.2f)return dpad;
            bool left=Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow),right=Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow);int keys=(right?1:0)-(left?1:0);return keys!=0?keys:mobileControls.Axis;}
        public void Act(string action){if(!BattleOn||WakeDialogue)return;if(action=="pause"){clock.paused=!clock.paused;ClearInput();}else if(action=="step"){clock.paused=true;clock.Single();}else if(action=="slow")clock.speed=clock.speed==1?.25:1;else if(action=="charge"){sim.player.energy=3;sim.player.echo=0;}else if(action=="stun"){sim.boss.state="broken";sim.boss.age=0;sim.boss.move=null;sim.boss.posture=100;}else if(action=="echoWindow"){sim.player.energy=Math.Max(1,sim.player.energy);sim.executeWindow=30;}else sim.Press(action);}
        void ClearInput(){mobileControls.Reset();sim?.ClearCommands();clock?.ClearAccumulation();StopSounds();}
        void OnApplicationFocus(bool hasFocus){focused=hasFocus;ClearInput();}
        void OnApplicationPause(bool pause){if(pause)ClearInput();}
        void OnDestroy(){StopSounds();if(font)Destroy(font);if(hudPortrait)Destroy(hudPortrait);if(hudRing)Destroy(hudRing);}

        void OnGUI(){if(!font)return;var oldMatrix=GUI.matrix;float scale=Math.Min(Screen.width/1280f,Screen.height/720f);GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            if(textStyle==null){textStyle=new GUIStyle(GUI.skin.label){font=font,wordWrap=true};buttonStyle=new GUIStyle(GUI.skin.button){font=font,fontSize=21,alignment=TextAnchor.MiddleCenter,wordWrap=true};}
            if(stage=="MENU")MenuGUI();
            else{
                bool settingsOpen=gameSettings;
                GUI.enabled=!settingsOpen;GameGUI();GUI.enabled=true;
                if(settingsOpen)GameSettingsGUI();
            }
            GUI.matrix=oldMatrix;GUI.color=Color.white;
        }
        static Color Ivory=>new Color(.98f,.93f,.85f);static Color Gold=>new Color(.85f,.74f,.53f);static Color Ink=>new Color(.075f,.11f,.18f,.95f);
        void Fill(Rect rect,Color color){color.a*=guiOpacity;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;}
        void Text(Rect rect,string value,int size=20,Color? color=null,TextAnchor align=TextAnchor.UpperLeft){Color tint=color??Ivory;tint.a*=guiOpacity;textStyle.fontSize=size;textStyle.normal.textColor=tint;textStyle.alignment=align;GUI.Label(rect,value,textStyle);}
        float TextHeight(string text,float width,int size){measureContent.text=text;textStyle.fontSize=size;return Mathf.Ceil(textStyle.CalcHeight(measureContent,width))+4;}
        bool Button(Rect rect,string caption,bool primary=false,bool interactive=true){Fill(rect,primary?Ivory:Ink);Fill(new Rect(rect.x,rect.y,rect.width,1),new Color(Gold.r,Gold.g,Gold.b,.55f));Text(rect,caption,21,primary?Ink:Ivory,TextAnchor.MiddleCenter);return interactive&&GUI.Button(rect,GUIContent.none,GUIStyle.none);}
        void MenuGUI(){if(keyart)GUI.DrawTexture(new Rect(0,0,1280,720),keyart,ScaleMode.ScaleAndCrop);else Fill(new Rect(0,0,1280,720),Ink);
            for(int i=0;i<12;i++)Fill(new Rect(i*49,0,50,720),new Color(.04f,.08f,.15f,(142-i*10)/255f));
            Text(new Rect(105,97,450,35),"月下执铃 · 循响而行",18,Gold);Text(new Rect(100,140,520,95),"铃月回响",66);Text(new Rect(108,242,470,32),"L I N G Y U E   E C H O E S",16,Gold);
            Fill(new Rect(108,296,340,1),Gold);Text(new Rect(108,320,430,40),"铃声所至，月光为刃。",21,new Color(.75f,.78f,.86f));
            GUI.enabled=modal=="";
            for(int i=0;i<MenuOptions.Length;i++){
                var option=MenuOptions[i];
                if(Button(option.bounds,option.label,menuChoice==i))ActivateMenuOption(i);
            }
            GUI.enabled=true;
            Text(new Rect(105,690,550,24),ControlPrompts.MenuHint(device),14);
            Fill(new Rect(1000,640,265,65),Ink);Text(new Rect(1015,650,245,60),"12 帧弹刀 · 8 帧完美\n固定 60Hz 战斗",16,Gold);
            if(motion)for(int i=0;i<14;i++){float t=Time.unscaledTime;Fill(new Rect(60+(i*173)%1160+Mathf.Sin(t*.5f+i*1.7f)*18,((i*97-t*(8+i%4*4))%720+720)%720,8,3),new Color(.95f,.73f,.8f,.5f));}
            if(SavePickerOpen){SavePickerGUI();return;}
            if(modal!=""){Fill(new Rect(0,0,1280,720),new Color(.025f,.04f,.08f,.87f));Fill(new Rect(240,70,800,580),Ink);Text(new Rect(285,100,700,45),modal=="controls"?"循响入门":"声音与界面",30,Gold);
                if(modal=="controls")Text(new Rect(285,160,705,400),ControlPrompts.Summary(device)+"\n\n弹刀前 8 帧完美、共 12 帧；危险招式用闪避或跳跃。\n闪避 / 跳跃 / 格挡消耗 45 / 30 / 20 气力。\n成功闪避 / 格挡回复 34% 回响；完美成功回复 100% 并回满气力。\n小处决消耗 1 回响，大处决消耗全部回响。\n死亡后从菜单选择重试。",20);
                else{Text(new Rect(285,170,700,55),"声音、动态和序章进度会保存到本机。",21);if(Button(new Rect(350,250,580,60),"樱花动态    "+(motion?"开启":"关闭"))){motion=!motion;PlayerPrefs.SetString("lingyue.menuMotion",motion?"on":"off");}
                    if(Button(new Rect(350,335,580,60),"声音    "+(muted?"关闭":"开启")))ToggleSound();
                    if(Button(new Rect(350,420,580,60),"触屏按键预览    "+(previewTouchControls?"开启":"关闭"))){PreviewTouchControls=!previewTouchControls;PlayerPrefs.SetInt("lingyue.touchControls",previewTouchControls?1:0);PlayerPrefs.Save();}
                    Text(new Rect(285,500,700,50),"手机自动显示触屏按键 · 映射 Xbox 布局\n显示目标 60 FPS · 固定战斗 60Hz",17,Gold);}
                if(Button(new Rect(530,565,220,55),"返回",true))CloseMenuModal();}
        }
        void SavePickerGUI(){
            Fill(new Rect(0,0,1280,720),new Color(.025f,.04f,.08f,.78f));
            RoundedFill(new Rect(333,95,614,499),Ink,10);
            Text(new Rect(375,118,530,50),"选择存档",32,Gold,TextAnchor.MiddleLeft);
            Fill(new Rect(375,175,530,1),new Color(Gold.r,Gold.g,Gold.b,.55f));
            for(int i=0;i<PrologueLayout.SaveSlots;i++){
                var row=SaveRow(i);bool selected=saveChoice==i;
                Fill(row,selected?new Color(.22f,.28f,.37f):new Color(.12f,.18f,.27f));
                Fill(new Rect(row.x,row.y,selected?6:3,row.height),selected?Gold:new Color(.23f,.30f,.40f));
                Text(new Rect(row.x+21,row.y,145,row.height),$"存档 {i+1:00}",23,Ivory,TextAnchor.MiddleLeft);
                Text(new Rect(row.x+174,row.y,row.width-197,row.height),SaveStatus(i+1),18,Gold,TextAnchor.MiddleRight);
                if(GUI.Button(row,GUIContent.none,GUIStyle.none)){ActivateSaveChoice(i);return;}
            }
            if(Button(SaveBackButton,"返回",saveChoice==PrologueLayout.SaveSlots))ActivateSaveChoice(PrologueLayout.SaveSlots);
            Text(new Rect(375,542,530,30),ControlPrompts.MenuHint(device),16,Gold,TextAnchor.MiddleCenter);
        }
        void GameSettingsGUI(){
            RoundedFill(new Rect(914,96,330,326),Ink,9);
            Text(new Rect(934,106,290,37),"游戏设置",22,Gold,TextAnchor.MiddleLeft);
            Fill(new Rect(938,150,282,1),new Color(Gold.r,Gold.g,Gold.b,.6f));
            for(int i=0;i<4;i++){
                string label=i==0?"按键提示  ·  "+(showKeyHints?"开启":"关闭")
                    :i==1?"声音  ·  "+(muted?"关闭":"开启"):i==2?"继续游戏":"返回主菜单";
                if(Button(new Rect(938,166+i*55,282,45),label,settingsChoice==i))ActivateGameSetting(i);
            }
        }
        void GameGUI(){if(stage=="DYING")return;if(stage=="DEATH"){DeathGUI();return;}if(stage=="STORY"){Fill(new Rect(0,0,1280,720),Color.black);if(stageTime>=.4f&&storyIndex<content.story.Length){float a=storyPhase=="in"?storyTime/.4f:storyPhase=="out"?1-storyTime/.4f:1;Text(new Rect(90,300,1100,150),content.story[storyIndex],30,new Color(.96f,.94f,.88f,a),TextAnchor.MiddleCenter);Text(new Rect(900,650,300,35),ControlPrompts.StoryHint(device),16,Gold);}
                if(Event.current.type==EventType.MouseUp)AdvanceStory();return;}
            if(stage=="FADE_IN")Fill(new Rect(0,0,1280,720),new Color(0,0,0,1-Mathf.Clamp01(stageTime/.8f)));
            if(stage=="GUARDIAN_INTRO"){TrialIntroGUI();return;}
            if(TailStory){TailStoryGUI();return;}
            if(BattleOn||stage=="GATE_DIALOGUE"){
                if(Button(SettingsButton,"设置",gameSettings))SetGameSettings(true);
                float lessonBottom=TutorialLessonGUI();
                if(BattleOn){PlayerHud();
                    if(stage=="GUARDIAN_FIGHT"){Text(new Rect(450,25,520,30),"守门人 · 残月的守灯者",23,Gold,TextAnchor.MiddleCenter);Bar(new Rect(490,67,455,14),(float)(sim.boss.hp/sim.boss.maxHp),new Color(.71f,.31f,.37f));Bar(new Rect(490,92,455,9),(float)(sim.boss.posture/100),Gold);}
                    if(showKeyHints&&!gameSettings&&!TouchControlsVisible&&!speech.Active)RightControlHints();
                    string status=clock.paused?"暂停":sim.player.state=="finisher"?"铃月终结":sim.player.state=="execute"?"回响小处决":sim.CanFinish?ControlPrompts.Format("架势崩解 · {attack} 消耗全部回响处决",device):sim.boss.state=="stagger"?"守灯人踉跄":sim.frame-sim.successFrame<24?sim.successKind=="perfectDodge"?"极限闪避 · 气力回满":"完美弹刀 · 气力回满":"";
                    if(status!="")Text(new Rect(330,Mathf.Max(lessonBottom,TouchControlsVisible?195:155),620,44),status,25,Gold,TextAnchor.MiddleCenter);}
                string interaction=Interaction();
                if(!TouchControlsVisible&&interaction!=null){
                    if(showKeyHints)KeyPrompt(540,"interact",interaction);
                    else HintText(new Rect(1092,540,160,30),interaction,16,Ivory);
                    if(GUI.Button(new Rect(1030,540,220,30),GUIContent.none,GUIStyle.none))Interact();
                }
                if(speech.Active)DialogueBubble(lessonBottom);
                if(stage=="TUTORIAL"||stage=="GATE_READY")CheckpointGUI();
            }
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
            Fill(new Rect(0,0,1280,38),Ink);Fill(new Rect(0,682,1280,38),Ink);
            if(stage=="FIRST_TAIL"){
                float handoff=Ease(Mathf.Clamp01(stageTime/1.4f));
                Vector2 from=StoryPoint(sim.boss,72),to=StoryPoint(sim.player,80);
                to.x-=sim.player.face*25;
                Vector2 point=Vector2.Lerp(from,to,handoff);
                point.y-=Mathf.Sin(handoff*Mathf.PI)*48;
                float appear=Ease(Mathf.Clamp01(stageTime/.3f)),size=Mathf.Lerp(54,82,handoff);
                TailLight(point,size,appear,stageTime);
                if(stageTime>=1.25f){
                    Text(new Rect(320,122,640,52),"第一尾 · 归还",30,Gold,TextAnchor.MiddleCenter);
                    Text(new Rect(320,173,640,34),"九尾残响：1 / 9",19,Ivory,TextAnchor.MiddleCenter);
                }
                return;
            }
            if(stage=="TAIL_EPILOGUE"){
                Fill(new Rect(0,38,1280,644),new Color(.034f,.051f,.087f,.92f));
                if(firstTail)GUI.DrawTexture(new Rect(590,175,100,100),firstTail);
                Text(new Rect(340,278,600,45),"序章结束 · 月门余音",27,Gold,TextAnchor.MiddleCenter);
                Text(new Rect(275,332,730,118),speech.VisibleText,24,Ivory,TextAnchor.MiddleCenter);
                if(Button(new Rect(565,475,150,44),"继续"))speech.Confirm();
                return;
            }
            if(tailFormTime>0){var point=StoryPoint(sim.boss,72);TailLight(point,62,Ease(Mathf.Clamp01(tailFormTime/.8f)),tailFormTime);}
            RoundedFill(new Rect(252,538,776,123),Ink,8);
            Text(new Rect(276,550,550,30),speech.Speaker=="hero"?"铃月":"守灯人",18,Gold);
            Text(new Rect(276,583,590,60),speech.VisibleText,22,Ivory);
            if(Button(new Rect(896,599,106,42),"继续"))speech.Confirm();
        }
        void TailLight(Vector2 point,float size,float alpha,float time){
            for(int i=0;i<3;i++){
                float phase=Mathf.Repeat(time*.75f+i/3f,1);
                float r=3.5f*(1-phase);
                Vector2 dot=point+new Vector2(Mathf.Sin(i*2.1f+phase*3)*size*.43f,size*(.28f-phase*.9f));
                RoundedFill(new Rect(dot.x-r,dot.y-r,r*2,r*2),new Color(1,.67f,.46f,alpha*(1-phase)*.85f),r);
            }
            if(firstTail){GUI.color=new Color(1,1,1,alpha);GUI.DrawTexture(new Rect(point.x-size/2,point.y-size/2,size,size),firstTail);GUI.color=Color.white;}
        }
        Vector2 StoryPoint(Fighter actor,float height){
            var p=view.WorldToViewportPoint(new Vector3((float)actor.x/100,((float)actor.y+height-95)/100,0));
            return new Vector2(p.x*1280,(1-p.y)*720);
        }
        void RightControlHints(){
            float y=SettingsButton.yMax+12,keyRight=SettingsButton.center.x+31;
            KeyPrompt(y,"move","移动",keyRight);
            KeyPrompt(y+31,"jump","跳跃",keyRight);
            KeyPrompt(y+62,"attack","攻击",keyRight);
            KeyPrompt(y+93,"parry","格挡",keyRight);
            KeyPrompt(y+124,"dodge","闪避",keyRight);
            KeyPrompt(y+155,"interact","交谈",keyRight);
        }
        void KeyPrompt(float y,string action,string caption,float keyRight=1082){
            if(Event.current.type!=EventType.Repaint)return;
            string key=ControlPrompts.Key(action,device);
            float width=action=="move"?116:key.Length>=4?50:key.Length>=2?36:27;
            Rect cap=new Rect(keyRight-width,y+2,width,26);
            RoundedFill(cap,new Color(.85f,.74f,.53f,.85f),5);
            RoundedFill(new Rect(cap.x+1,cap.y+1,cap.width-2,cap.height-2),new Color(.055f,.08f,.125f,.91f),4);
            Text(cap,key,14,Ivory,TextAnchor.MiddleCenter);
            HintText(new Rect(keyRight+10,y,Mathf.Min(160,1270-keyRight),30),caption,16,Ivory);
        }
        void HintText(Rect rect,string caption,int size,Color color){
            if(Event.current.type!=EventType.Repaint)return;
            Color edge=new Color(.025f,.035f,.055f,1);
            Rect outline=rect;
            outline.x=rect.x-1.2f;Text(outline,caption,size,edge,TextAnchor.MiddleLeft);
            outline.x=rect.x+1.2f;Text(outline,caption,size,edge,TextAnchor.MiddleLeft);
            outline.x=rect.x;outline.y=rect.y-1.2f;Text(outline,caption,size,edge,TextAnchor.MiddleLeft);
            outline.y=rect.y+1.5f;Text(outline,caption,size,edge,TextAnchor.MiddleLeft);
            Text(rect,caption,size,color,TextAnchor.MiddleLeft);
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
                Text(new Rect(400,128,480,66),"月门试炼",38,new Color(Ivory.r,Ivory.g,Ivory.b,alpha),TextAnchor.MiddleCenter);
            }
        }
        void DeathGUI(){guiOpacity=DeathMenuOpacity;Fill(new Rect(0,0,1280,720),new Color(.018f,.025f,.04f,.76f));RoundedFill(new Rect(340,150,600,420),Ink,18);
            Text(new Rect(380,195,520,65),"铃声沉寂",40,Gold,TextAnchor.MiddleCenter);
            Text(new Rect(380,278,520,48),"气息渐冷，远处的灯仍亮着。",21,Ivory,TextAnchor.MiddleCenter);
            bool ready=DeathMenuReady;
            if(Button(new Rect(430,355,420,58),checkpointReached?"在月门灯下苏醒":"再次苏醒",deathChoice==0,ready))Retry();
            if(Button(new Rect(430,435,420,58),"返回主界面",deathChoice==1,ready))Home();
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
                Vector2 p=MobileControls.ButtonCenters[i];bool held=mobileControls.Pressed(MobileControls.Commands[i]);
                float radius=i<4?40:i==4?34:26;
                Color tint=i==0?new Color(.4f,.9f,.4f):i==1?new Color(1,.38f,.33f):i==2?new Color(.32f,.66f,1):i==3?new Color(1,.83f,.23f):Ivory;
                if(i==4&&!canTalk)tint=new Color(.6f,.65f,.7f,.45f);
                RoundedFill(new Rect(p.x-radius,p.y-radius,radius*2,radius*2),new Color(tint.r,tint.g,tint.b,held?.95f:i==4&&canTalk?.85f:.43f),radius);
                float inset=held?4:2;
                RoundedFill(new Rect(p.x-radius+inset,p.y-radius+inset,(radius-inset)*2,(radius-inset)*2),held?new Color(.1f,.17f,.21f,.92f):new Color(.035f,.055f,.08f,.72f),radius-inset);
                string letter=i==0?"A":i==1?"B":i==2?"X":i==3?"Y":i==4?"RB":clock.paused?"▶":"Ⅱ";
                string caption=i==0?"跳跃":i==1?"闪避":i==2?"攻击":i==3?"格挡":i==4?(interaction??"交谈"):"";
                Text(new Rect(p.x-radius,p.y-(i<5?29:23),radius*2,i<5?35:46),letter,i<4?28:i==4?21:23,tint,TextAnchor.MiddleCenter);
                if(i<5)Text(new Rect(p.x-radius,p.y+7,radius*2,24),caption,13,tint,TextAnchor.MiddleCenter);
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
        void PlayerHud(){if(Event.current.type!=EventType.Repaint)return;var p=sim.player;
            HudBar(new Rect(136,61,302,20),(float)(p.hp/p.maxHp),new Color(.12f,.78f,.32f));
            HudBar(new Rect(136,87,266,15),(float)(p.stamina/Rules.staminaMax),new Color(1,.81f,.12f));
            RoundedFill(new Rect(38,35,106,106),new Color(.025f,.035f,.05f,.98f),53);
            RoundedFill(new Rect(42,39,98,98),new Color(.14f,.18f,.23f),49);
            if(hudPortrait)GUI.DrawTexture(new Rect(43,40,96,96),hudPortrait);
            int echo=Mathf.RoundToInt(Mathf.Clamp01((float)(p.echo/Rules.echoMax))*100);
            if(hudRing&&echo!=hudEcho){hudEcho=echo;for(int i=0;i<hudRingPixels.Length;i++){
                    Color color=hudRingPosition[i]<=echo/100f&&echo>0?EchoOrange:new Color(.72f,.28f,.06f,.8f);
                    color.a*=hudRingCoverage[i];hudRingPixels[i]=color;}
                hudRing.SetPixels(hudRingPixels);hudRing.Apply(false,false);}
            if(hudRing)GUI.DrawTexture(new Rect(27,24,128,128),hudRing);
            for(int i=0;i<3;i++){float angle=(75-i*25)*Mathf.Deg2Rad;float x=91+60*Mathf.Cos(angle),y=88+60*Mathf.Sin(angle);
                RoundedFill(new Rect(x-8,y-8,16,16),new Color(.025f,.035f,.05f,.98f),8);
                RoundedFill(new Rect(x-6,y-6,12,12),EchoOrange,6);
                if(i>=p.energy)RoundedFill(new Rect(x-4,y-4,8,8),new Color(.12f,.09f,.08f),4);}
        }
        void HudBar(Rect rect,float ratio,Color color){RoundedFill(rect,new Color(.025f,.035f,.05f,.96f),rect.height/2);
            Rect inner=new Rect(rect.x+3,rect.y+3,rect.width-6,rect.height-6);RoundedFill(inner,new Color(color.r*.18f,color.g*.18f,color.b*.18f),inner.height/2);
            float width=inner.width*Mathf.Clamp01(ratio);if(width>0)RoundedFill(new Rect(inner.x,inner.y,width,inner.height),color,Math.Min(inner.height,width)/2);}
        void Bar(Rect rect,float ratio,Color color){Fill(rect,Ink);Fill(new Rect(rect.x+2,rect.y+2,(rect.width-4)*Mathf.Clamp01(ratio),rect.height-4),color);}
        void DrawBox(Box box,Color color){Vector3 a=view.WorldToViewportPoint(new Vector3((float)box.x/100,((float)box.y-95)/100,0)),b=view.WorldToViewportPoint(new Vector3((float)(box.x+box.w)/100,((float)(box.y+box.h)-95)/100,0));Rect r=new Rect(a.x*1280,(1-b.y)*720,(b.x-a.x)*1280,(b.y-a.y)*720);Fill(new Rect(r.x,r.y,r.width,1),color);Fill(new Rect(r.x,r.yMax,r.width,1),color);Fill(new Rect(r.x,r.y,1,r.height),color);Fill(new Rect(r.xMax,r.y,1,r.height),color);}
    }
}
