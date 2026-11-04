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
        static bool replayRequested;
        Transform world;Camera view;PixelActor hero,guardian;PixelEffects effects;PrologueContent content;
        TutorialDirector tutorial;DialogueDirector speech=new DialogueDirector();
        float stageTime,cameraX=-960,hintLeft;string hintKey="",device="keyboard",modal="";bool debug,boxes,focused=true,motion,muted,tailGranted;
        int storyIndex,soundCursor,voiceIndex;float storyTime;string storyPhase="in";
        Texture2D keyart;Font font;GUIStyle textStyle,buttonStyle;
        AudioSource[] voices;AudioSource uiVoice,music;readonly Dictionary<string,AudioClip> soundClips=new Dictionary<string,AudioClip>();
        Combat soundModel;string musicKey="";
        readonly Dictionary<int,string> touchCommands=new Dictionary<int,string>();bool mouseLeft,mouseRight;
        public bool BattleOn=>stage=="TUTORIAL"||stage=="GUARDIAN_FIGHT"||stage=="TAIL_DIALOGUE"||stage=="FIRST_TAIL";
        void Awake(){Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Input.simulateMouseWithTouches=true;
            content=JsonUtility.FromJson<PrologueContent>(Resources.Load<TextAsset>("Data/prologue-content").text);PixelWorld.Load();
            view=Camera.main;if(!view){var camera=new GameObject("Main Camera");camera.tag="MainCamera";view=camera.AddComponent<Camera>();}
            view.orthographic=true;view.orthographicSize=3.6f;view.transform.position=new Vector3(0,0,-10);view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.043f,.075f,.106f);
            if(!FindAnyObjectByType<AudioListener>())view.gameObject.AddComponent<AudioListener>();
            world=transform.Find("World");if(!world)world=PixelWorld.Create(transform);
            hero=new PixelActor(world.Find("Hero").gameObject,true);guardian=new PixelActor(world.Find("Guardian").gameObject,false);effects=new PixelEffects(world);
            keyart=Resources.Load<Texture2D>("ui/main_menu_keyart");
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Noto Sans CJK SC","Droid Sans Fallback","Arial Unicode MS"},24);
            motion=PlayerPrefs.GetString("lingyue.menuMotion","on")!="off";muted=PlayerPrefs.GetString("lingyue.sfxMuted","off")=="on";
            voices=new AudioSource[12];for(int i=0;i<12;i++)voices[i]=gameObject.AddComponent<AudioSource>();uiVoice=gameObject.AddComponent<AudioSource>();music=gameObject.AddComponent<AudioSource>();music.loop=true;music.volume=.22f;
            foreach(string name in new[]{"step","jump","land","slash1","slash2","slash3","guard","dodge","counter","hit_player","hit_boss","parry","perfect_dodge","boss_swing","break","phase","death","victory","ui_confirm","ui_back"})soundClips[name]=Resources.Load<AudioClip>("audio/sfx/"+name);
            speech.onClose=id=>{if(id=="tail"&&stage=="TAIL_DIALOGUE")SetStage("FIRST_TAIL");};
            Home();if(SceneManager.GetActiveScene().name=="Prologue")BeginTrial(replayRequested);
        }
        public void StartTrial(bool replay){if(stage!="MENU")return;UiSound("ui_confirm");replayRequested=replay;if(SceneManager.GetActiveScene().name!="Prologue")SceneManager.LoadScene("Prologue");else BeginTrial(replay);}
        public void BeginTrial(bool replay){sim=new Combat();clock=new FixedClock(sim);sim.player.x=-1400;sim.boss.x=-1235;sim.boundsMin=-1560;sim.boundsMax=1800;
            tutorial=new TutorialDirector();tutorial.Begin(sim);sim.beforeStep=tutorial.Pump;sim.ai=false;sim.enemyActive=false;
            speech.Reset();effects.Clear();tailGranted=false;cameraX=-960;ClearInput();world.gameObject.SetActive(true);hintKey="";
            storyIndex=0;storyTime=0;storyPhase="in";SetStage(!replay&&PlayerPrefs.GetInt("lingyue.prologue",0)==1?"TUTORIAL":"STORY");
        }
        public void Home(){StopSounds();speech.Reset();effects?.Clear();touchCommands.Clear();modal="";SetStage("MENU");world.gameObject.SetActive(false);if(clock!=null)clock.ClearAccumulation();SetMusic("menu_stage_select");}
        void SetStage(string value){stage=value;stageTime=0;}
        public void BeginGuardianFight(){sim.beforeStep=null;sim.nonlethal=false;sim.dummyBox=null;sim.boundsMin=1300;sim.boundsMax=1800;sim.TuneBoss(520);
            sim.boss.posture=0;sim.boss.state="idle";sim.boss.age=0;sim.boss.move=null;sim.ai=true;sim.enemyActive=true;SetStage("GUARDIAN_FIGHT");Say("bossPre");SetLamps(true);SetMusic("moon_gate_boss");}
        public void Retry(){if(sim==null)return;var old=sim;bool trial=stage=="GUARDIAN_FIGHT";sim=new Combat();sim.player.x=old.player.x;sim.player.y=old.player.y;sim.player.face=old.player.face;sim.boss.x=old.boss.x;sim.boss.y=old.boss.y;sim.boss.face=old.boss.face;sim.boundsMin=old.boundsMin;sim.boundsMax=old.boundsMax;sim.terrain=old.terrain;sim.god=old.god;
            clock=new FixedClock(sim);speech.Reset();ClearInput();effects.Clear();if(trial)BeginGuardianFight();else{tutorial.Begin(sim);sim.beforeStep=tutorial.Pump;sim.ai=false;sim.enemyActive=false;}}
        void Update(){float dt=Math.Min(Time.unscaledDeltaTime,.25f);UpdateViewport();if(!focused&&!Application.isBatchMode)return;ReadInput();stageTime+=dt;
            switch(stage){case "MENU":break;
                case "STORY":UpdateStory(dt);break;
                case "FADE_IN":if(stageTime>=.8f)SetStage("CAMERA_REVEAL");break;
                case "CAMERA_REVEAL":if(stageTime>=3.5f)SetStage("WAKE_UP");break;
                case "WAKE_UP":if(stageTime>=.95f){SetStage("TUTORIAL");SetMusic("prologue_stage_1");}break;
                default:
                    if(BattleOn){sim.axis=ReadAxis();clock.Advance(dt);SyncSounds();
                        if(stage=="TUTORIAL"){var id=tutorial.Speech(sim,!speech.Active);if(id!=null)Say(id);if(sim.player.x>=1340&&sim.player.x<=1800&&sim.boss.x>=1300)BeginGuardianFight();}
                        else if(stage=="GUARDIAN_FIGHT"&&sim.boss.hp<=0){sim.ai=false;sim.enemyActive=false;sim.boss.move=null;sim.boss.state="dead";SetStage("TAIL_DIALOGUE");Say("tail");}
                        else if(stage=="FIRST_TAIL"&&stageTime>=2.7f&&!tailGranted){tailGranted=true;PlayerPrefs.SetInt("lingyue.tails",Math.Max(1,PlayerPrefs.GetInt("lingyue.tails",0)));PlayerPrefs.SetInt("lingyue.prologue",1);PlayerPrefs.Save();SetStage("COMPLETE");}}
                    break;}
            if(speech.Active)speech.Update(dt);if(stage!="MENU"){DrawWorld(dt);string key=(stage=="TUTORIAL"?TutorialDirector.Title(sim.player.x):"月门试炼")+device;if(key!=hintKey){hintKey=key;hintLeft=7;}else hintLeft=Math.Max(0,hintLeft-dt);}
            if(music)music.mute=muted;
        }
        void UpdateStory(float dt){if(stageTime<.4f)return;storyTime+=dt;float span=storyPhase=="hold"?2.5f:.4f;if(storyTime>=span){storyTime=0;if(storyPhase=="in")storyPhase="hold";else if(storyPhase=="hold")storyPhase="out";else AdvanceStory();}}
        void AdvanceStory(){if(stage!="STORY"||stageTime<.4f)return;storyIndex++;storyPhase="in";storyTime=0;if(storyIndex>=content.story.Length){SetStage("FADE_IN");SetMusic("prologue_stage_1");}}
        void DrawWorld(float dt){var p=sim.player;var b=sim.boss;float zoom=1,focusY=0;
            if(stage=="FADE_IN"||stage=="CAMERA_REVEAL"){float t=stage=="FADE_IN"?0:Mathf.Clamp01((stageTime-.7f)/2.8f),ease=Ease(t);zoom=Mathf.Lerp(2.2f,1,ease);cameraX=Mathf.Lerp((float)p.x,Mathf.Clamp((float)p.x+100,-960,1260),ease);focusY=Mathf.Lerp((float)p.y+40,0,ease);}
            else{float focus=stage=="TUTORIAL"||stage=="WAKE_UP"?Mathf.Clamp((float)p.x+100,-960,1260):1550;cameraX=Mathf.Lerp(cameraX,focus,1-Mathf.Exp(-8*dt));}
            view.orthographicSize=3.6f/zoom;view.transform.position=new Vector3(cameraX/100,focusY/100,-10);
            string pose=null;int cell=7;float angle=0,lift=0;
            if(stage=="FADE_IN"||stage=="CAMERA_REVEAL"){pose="dead";cell=7;angle=-8;}
            if(stage=="WAKE_UP"){float t=Ease(Mathf.Clamp01(stageTime/.95f));pose=stageTime<.95f*.45f?"dead":"hurt";cell=pose=="dead"?7:3;angle=(pose=="dead"?-8:-46)*(1-t);lift=pose=="dead"?0:-6*(1-t);}
            hero.Draw(p,sim.frame,(stage=="FIRST_TAIL"||stage=="COMPLETE")&&p.state=="idle",pose,cell,angle,lift);
            float alpha=stage=="FIRST_TAIL"?Mathf.Max(0,1-stageTime/1.1f):stage=="COMPLETE"?0:1;guardian.Draw(b,sim.frame,alpha:alpha);effects.Draw(sim);
        }
        static float Ease(float t)=>t<.5f?4*t*t*t:1-Mathf.Pow(-2*t+2,3)/2;
        void Say(string id){var script=Array.Find(content.scripts,s=>s.id==id);if(script!=null)speech.Play(script);}
        void SetLamps(bool lit){foreach(Transform child in world)if(child.name.StartsWith("MoonLantern"))child.GetComponent<SpriteRenderer>().color=lit?new Color(1,.94f,.83f):new Color(.51f,.59f,.68f);}
        void SetMusic(string name){if(musicKey==name)return;musicKey=name;var clip=Resources.Load<AudioClip>("contentpack_20261104/music/"+name);if(!clip)return;music.Stop();music.clip=clip;music.Play();}
        void StopSounds(){if(voices!=null)foreach(var voice in voices)voice.Stop();}
        void UiSound(string name){if(!muted&&soundClips.TryGetValue(name,out var clip)&&clip){uiVoice.Stop();uiVoice.clip=clip;uiVoice.volume=.45f;uiVoice.Play();}}
        void SyncSounds(){if(soundModel!=sim){StopSounds();soundModel=sim;soundCursor=0;}foreach(var cue in sim.sounds){if(cue.id<=soundCursor)continue;soundCursor=cue.id;if(muted||clock.paused)continue;if(!soundClips.TryGetValue(cue.name,out var clip)||!clip)continue;var voice=voices.FirstOrDefault(v=>!v.isPlaying)??voices[voiceIndex++%voices.Length];voice.Stop();voice.clip=clip;voice.volume=cue.name=="step"?.22f:cue.name=="guard"?.3f:.65f;voice.Play();}}
        void UpdateViewport(){float aspect=(float)Screen.width/Screen.height,desired=16f/9;if(aspect>desired){float w=desired/aspect;view.rect=new Rect((1-w)/2,0,w,1);}else{float h=aspect/desired;view.rect=new Rect(0,(1-h)/2,1,h);}}
        Vector2 UiPoint(Vector2 screen){float scale=Math.Min(Screen.width/1280f,Screen.height/720f);return new Vector2((screen.x-(Screen.width-1280*scale)/2)/scale,(Screen.height-screen.y-(Screen.height-720*scale)/2)/scale);}
        static Rect TouchRect(int i)=>new Rect(new[]{44,144,724,824,924,1024}[i],600,86,76);
        static readonly string[] touchNames={"left","right","jump","attack","parry","dodge"};
        void ReadInput(){bool padA=Input.GetKeyDown(KeyCode.JoystickButton0),padB=Input.GetKeyDown(KeyCode.JoystickButton1);bool confirm=Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space)||padA;
            if(Input.GetKeyDown(KeyCode.Escape)){if(modal!="")modal="";else if(stage!="MENU")Home();return;}
            if(stage=="MENU"){if(padB)modal="";if(confirm&&modal=="")StartTrial(false);return;}
            if(stage=="STORY"){if(padB){Home();return;}if(confirm)AdvanceStory();return;}
            if(stage=="COMPLETE"){if(confirm||padB)Home();return;}
            if(!BattleOn)return;
            if(Input.GetKeyDown(KeyCode.P)||Input.GetKeyDown(KeyCode.JoystickButton7))Act("pause");if(Input.GetKeyDown(KeyCode.R))Retry();if(Input.GetKeyDown(KeyCode.O))Act("step");if(Input.GetKeyDown(KeyCode.T))Act("slow");
            if(Input.GetKeyDown(KeyCode.F1))debug=!debug;if(Input.GetKeyDown(KeyCode.F2))boxes=!boxes;
            if(Input.GetKeyDown(KeyCode.J)||Input.GetKeyDown(KeyCode.JoystickButton2))Act("attack");if(Input.GetKeyDown(KeyCode.K)||Input.GetKeyDown(KeyCode.JoystickButton3))Act("parry");
            if(Input.GetKeyDown(KeyCode.LeftShift)||Input.GetKeyDown(KeyCode.RightShift)||padB)Act("dodge");if(Input.GetKeyDown(KeyCode.Space)||padA)Act("jump");
            if(debug)for(int i=0;i<6;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i))sim.DebugBoss(i);
            for(int i=0;i<Input.touchCount;i++){var touch=Input.GetTouch(i);if(touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled){touchCommands.Remove(touch.fingerId);continue;}device="touch";if(touch.phase==TouchPhase.Began){Vector2 point=UiPoint(touch.position);for(int k=0;k<6;k++)if(TouchRect(k).Contains(point)){touchCommands[touch.fingerId]=touchNames[k];if(k>1)Act(touchNames[k]);break;}}}
            if(Input.anyKeyDown&&!padA&&!padB)device="keyboard";if(Math.Abs(Input.GetAxisRaw("PadX"))>.2f||Input.GetKeyDown(KeyCode.JoystickButton2)||padA||padB)device="pad";
            if(Input.GetMouseButtonUp(0)){mouseLeft=mouseRight=false;}
        }
        double ReadAxis(){if(scriptedAxis.HasValue)return scriptedAxis.Value;float pad=Input.GetAxisRaw("PadX"),dpad=Input.GetAxisRaw("PadDpad");if(Math.Abs(pad)>.2f)return pad;if(Math.Abs(dpad)>.2f)return dpad;
            bool left=Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)||touchCommands.ContainsValue("left")||mouseLeft,right=Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)||touchCommands.ContainsValue("right")||mouseRight;return (right?1:0)-(left?1:0);}
        public void Act(string action){if(!BattleOn)return;if(action=="pause"){clock.paused=!clock.paused;StopSounds();}else if(action=="step"){clock.paused=true;clock.Single();}else if(action=="slow")clock.speed=clock.speed==1?.25:1;else if(action=="charge"){sim.player.energy=3;sim.player.echo=0;}else if(action=="stun"){sim.boss.state="broken";sim.boss.age=0;sim.boss.move=null;sim.boss.posture=100;}else if(action=="echoWindow"){sim.player.energy=Math.Max(1,sim.player.energy);sim.executeWindow=30;}else sim.Press(action);}
        void ClearInput(){touchCommands.Clear();mouseLeft=mouseRight=false;sim?.ClearCommands();clock?.ClearAccumulation();StopSounds();}
        void OnApplicationFocus(bool hasFocus){focused=hasFocus;ClearInput();}
        void OnApplicationPause(bool pause){if(pause)ClearInput();}
        void OnDestroy(){StopSounds();if(font)Destroy(font);}

        void OnGUI(){if(!font)return;var oldMatrix=GUI.matrix;float scale=Math.Min(Screen.width/1280f,Screen.height/720f);GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            if(textStyle==null){textStyle=new GUIStyle(GUI.skin.label){font=font,wordWrap=true};buttonStyle=new GUIStyle(GUI.skin.button){font=font,fontSize=21,alignment=TextAnchor.MiddleCenter,wordWrap=true};}
            if(stage=="MENU")MenuGUI();else GameGUI();GUI.matrix=oldMatrix;GUI.color=Color.white;
        }
        static Color Ivory=>new Color(.98f,.93f,.85f);static Color Gold=>new Color(.85f,.74f,.53f);static Color Ink=>new Color(.075f,.11f,.18f,.95f);
        void Fill(Rect rect,Color color){GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;}
        void Text(Rect rect,string value,int size=20,Color? color=null,TextAnchor align=TextAnchor.UpperLeft){textStyle.fontSize=size;textStyle.normal.textColor=color??Ivory;textStyle.alignment=align;GUI.Label(rect,value,textStyle);}
        bool Button(Rect rect,string caption,bool primary=false){Fill(rect,primary?Ivory:Ink);Fill(new Rect(rect.x,rect.y,rect.width,1),new Color(Gold.r,Gold.g,Gold.b,.55f));Text(rect,caption,21,primary?Ink:Ivory,TextAnchor.MiddleCenter);return GUI.Button(rect,GUIContent.none,GUIStyle.none);}
        void MenuGUI(){if(keyart)GUI.DrawTexture(new Rect(0,0,1280,720),keyart,ScaleMode.ScaleAndCrop);else Fill(new Rect(0,0,1280,720),Ink);
            for(int i=0;i<12;i++)Fill(new Rect(i*49,0,50,720),new Color(.04f,.08f,.15f,(142-i*10)/255f));
            Text(new Rect(105,97,450,35),"月下执铃 · 循响而行",18,Gold);Text(new Rect(100,140,520,95),"铃月回响",66);Text(new Rect(108,242,470,32),"L I N G Y U E   E C H O E S",16,Gold);
            Fill(new Rect(108,296,340,1),Gold);Text(new Rect(108,320,430,40),"铃声所至，月光为刃。",21,new Color(.75f,.78f,.86f));
            GUI.enabled=modal=="";
            if(Button(new Rect(105,395,340,62),"开始试炼",true))StartTrial(false);
            if(Button(new Rect(105,477,340,58),"操作说明")){UiSound("ui_confirm");modal="controls";}
            if(Button(new Rect(105,553,340,58),"声音与界面")){UiSound("ui_confirm");modal="settings";}
            if(Button(new Rect(105,628,340,42),"重温序章"))StartTrial(true);
            GUI.enabled=true;
            Text(new Rect(105,690,550,24),"ENTER / 手柄 A 开始    ·    ESC / 手柄 B 返回",14);
            Fill(new Rect(1000,640,265,65),Ink);Text(new Rect(1015,650,245,60),"12 帧弹刀 · 8 帧完美\n固定 60Hz 战斗",16,Gold);
            if(motion)for(int i=0;i<14;i++){float t=Time.unscaledTime;Fill(new Rect(60+(i*173)%1160+Mathf.Sin(t*.5f+i*1.7f)*18,((i*97-t*(8+i%4*4))%720+720)%720,8,3),new Color(.95f,.73f,.8f,.5f));}
            if(modal!=""){Fill(new Rect(0,0,1280,720),new Color(.025f,.04f,.08f,.87f));Fill(new Rect(240,70,800,580),Ink);Text(new Rect(285,100,700,45),modal=="controls"?"循响入门":"声音与界面",30,Gold);
                if(modal=="controls")Text(new Rect(285,170,705,390),"A / D 或方向键 移动，空格 跳跃\nJ 三段连斩，K 弹刀，Shift 闪避\n手柄：左摇杆 / 十字键移动，A 跳跃，X 连斩，Y 弹刀，B 闪避，Start 暂停\n弹刀前 8 帧完美、共 12 帧；危险招式用闪避或跳跃。\n闪避 / 跳跃 / 格挡消耗 45 / 30 / 20 气力。\n完美弹刀与极限闪避回满气力、充能回响并触发慢放。\n完美弹刀或极闪后普攻：消耗 1 回响发动小处决。\nBoss 架势眩晕时近身普攻：消耗全部回响发动大处决。\nF1 调试，F2 判定框，R 原地重试，P 暂停，Esc 主界面。",21);
                else{Text(new Rect(285,170,700,55),"声音、动态和序章进度会保存到本机。",21);if(Button(new Rect(350,250,580,60),"樱花动态    "+(motion?"开启":"关闭"))){motion=!motion;PlayerPrefs.SetString("lingyue.menuMotion",motion?"on":"off");}
                    if(Button(new Rect(350,335,580,60),"声音    "+(muted?"关闭":"开启"))){muted=!muted;PlayerPrefs.SetString("lingyue.sfxMuted",muted?"on":"off");if(muted)StopSounds();PlayerPrefs.Save();}
                    Text(new Rect(285,445,700,90),"显示目标 60 FPS · 固定战斗 60Hz\n3 帧停顿 · 完美弹刀 / 极闪后 0.25x 慢放",21,Gold);}
                if(Button(new Rect(530,565,220,55),"返回",true)){UiSound("ui_back");modal="";}}
        }
        void GameGUI(){if(stage=="STORY"){Fill(new Rect(0,0,1280,720),Color.black);if(stageTime>=.4f&&storyIndex<content.story.Length){float a=storyPhase=="in"?storyTime/.4f:storyPhase=="out"?1-storyTime/.4f:1;Text(new Rect(90,300,1100,150),content.story[storyIndex],30,new Color(.96f,.94f,.88f,a),TextAnchor.MiddleCenter);Text(new Rect(900,650,300,35),"空格 / A / 点击 继续",16,Gold);}
                if(Event.current.type==EventType.MouseUp)AdvanceStory();return;}
            if(stage=="FADE_IN")Fill(new Rect(0,0,1280,720),new Color(0,0,0,1-Mathf.Clamp01(stageTime/.8f)));
            if(BattleOn||stage=="COMPLETE"){
                Text(new Rect(35,20,720,30),"铃月回响  /  LINGYUE ECHOES",17,Gold);if(Button(new Rect(1125,35,120,52),"主界面"))Home();
                if(BattleOn){Bar(new Rect(35,62,345,18),(float)(sim.player.hp/sim.player.maxHp),new Color(.83f,.31f,.45f));Text(new Rect(40,82,345,27),"铃月   "+Math.Ceiling(sim.player.hp)+" / "+sim.player.maxHp,15);
                    Bar(new Rect(35,112,310,12),(float)(sim.player.stamina/100),new Color(.45f,.8f,.65f));Text(new Rect(355,103,115,25),"气力 "+Math.Round(sim.player.stamina),15);
                    Bar(new Rect(35,136,310,10),(float)(sim.player.echo/100),new Color(.5f,.73f,.96f));Text(new Rect(355,128,180,27),"回响  "+new string('◆',sim.player.energy)+new string('◇',3-sim.player.energy),15,Gold);
                    if(stage=="GUARDIAN_FIGHT"){Text(new Rect(450,25,520,30),"守门人 · 残月的守灯者",23,Gold,TextAnchor.MiddleCenter);Bar(new Rect(490,67,455,14),(float)(sim.boss.hp/sim.boss.maxHp),new Color(.71f,.31f,.37f));Bar(new Rect(490,92,455,9),(float)(sim.boss.posture/100),Gold);}
                    if(hintLeft>0){Fill(new Rect(983,200,276,322),Ink);Text(new Rect(1000,220,240,38),stage=="TUTORIAL"?TutorialDirector.Title(sim.player.x):"月门试炼",20,Gold);Text(new Rect(1000,266,240,60),stage=="TUTORIAL"?TutorialDirector.Hint(sim.player.x):"观察前摇，留意气力与回响",17,new Color(.65f,.83f,.8f));Text(new Rect(1000,337,240,175),device=="pad"?"左摇杆 / 十字键 移动\nA 跳跃 · X 连斩\nY 弹刀 · B 闪避\n完美弹刀 / 极闪后 X 小处决\n架势眩晕后 X 大处决":"A / D 移动 · 空格 跳跃\nJ 连斩 · K 弹刀\nShift 闪避\n完美弹刀 / 极闪后 J 小处决\n架势眩晕后 J 大处决",17);}
                    string status=sim.player.hp<=0?"试炼失败 · R 原地再试":clock.paused?"暂停":sim.player.state=="finisher"?"铃月终结":sim.player.state=="execute"?"回响小处决":sim.CanFinish?"架势崩解 · 普攻消耗全部回响处决":sim.boss.state=="stagger"?"守灯人踉跄":sim.frame-sim.successFrame<24?sim.successKind=="perfectDodge"?"极限闪避 · 气力回满":"完美弹刀 · 气力回满":"";
                    Text(new Rect(330,155,620,60),status,25,Gold,TextAnchor.MiddleCenter);if(sim.player.hp<=0&&Button(new Rect(1055,122,175,50),"重试 / R"))Retry();}
                if(speech.Active){var actor=speech.Speaker=="hero"?sim.player:sim.boss;Vector3 pos=view.WorldToViewportPoint(new Vector3((float)actor.x/100,((float)actor.y-95+(float)Combat.BodyHeight(actor)+20)/100,0));float x=Mathf.Clamp(pos.x*1280,200,1080),y=Mathf.Clamp((1-pos.y)*720-90,140,470);Fill(new Rect(x-180,y,360,86),Ink);Text(new Rect(x-168,y+10,336,68),speech.VisibleText,18);}
            }
            if(BattleOn&&(Application.isMobilePlatform||device=="touch")){string[] names={"←","→","跳跃","攻击","弹刀","闪避"};for(int i=0;i<6;i++){var rect=TouchRect(i);Fill(rect,Ink);Text(rect,names[i],20,Gold,TextAnchor.MiddleCenter);if(Input.touchCount==0&&Event.current.type==EventType.MouseDown&&rect.Contains(Event.current.mousePosition)){device="touch";if(i==0)mouseLeft=true;else if(i==1)mouseRight=true;else Act(touchNames[i]);}}}
            if(debug&&BattleOn){Fill(new Rect(15,385,670,168),Ink);Text(new Rect(30,395,640,72),$"60 Hz  Tick {sim.tick}  Frame {sim.frame}  {clock.Rate}x\n玩家 {sim.player.state} [{sim.player.age}]  Boss {sim.boss.state} [{sim.boss.age}]\n停顿 {sim.hitstop}  慢放 {clock.cinematicTicks}  处决窗 {sim.executeWindow}",16);string[] labels={"AI","无敌","暂停","单帧","慢速","回响","崩解","小处决"};for(int i=0;i<8;i++)if(Button(new Rect(24+i*81,493,76,39),labels[i])){if(i==0)sim.ai=!sim.ai;else if(i==1)sim.god=!sim.god;else Act(new[]{"","","pause","step","slow","charge","stun","echoWindow"}[i]);}}
            if(boxes&&BattleOn){DrawBox(sim.Hurtbox(sim.player),Color.green);DrawBox(sim.Hurtbox(sim.boss),Color.green);foreach(var hit in sim.hits)DrawBox(hit,new Color(1,.3f,.3f,.6f));}
            if(stage=="FIRST_TAIL"){float t=Mathf.Clamp01((stageTime-1.1f)/1.6f);Vector3 p=view.WorldToViewportPoint(new Vector3((float)sim.player.x/100,((float)sim.player.y-55)/100,0));float x=p.x*1280,y=Mathf.Lerp(80,(1-p.y)*720,Ease(t));Fill(new Rect(x-7,y-7,14,14),new Color(.7f,.86f,1,1-t));if(t>.86f)Fill(new Rect(0,0,1280,720),new Color(1,1,1,Mathf.Sin((t-.86f)/.14f*Mathf.PI)*.8f));}
            if(stage=="COMPLETE"){Fill(new Rect(280,160,720,400),Ink);Text(new Rect(310,215,660,65),"第一尾 · 归还",36,Gold,TextAnchor.MiddleCenter);Text(new Rect(330,315,620,180),"已寻回狐尾   "+PlayerPrefs.GetInt("lingyue.tails",1)+" / 9\n\n序章完成 · 残月初醒\n七罪之门尚未开启",23,Ivory,TextAnchor.MiddleCenter);if(Button(new Rect(510,488,260,48),"返回主界面",true))Home();}
        }
        void Bar(Rect rect,float ratio,Color color){Fill(rect,Ink);Fill(new Rect(rect.x+2,rect.y+2,(rect.width-4)*Mathf.Clamp01(ratio),rect.height-4),color);}
        void DrawBox(Box box,Color color){Vector3 a=view.WorldToViewportPoint(new Vector3((float)box.x/100,((float)box.y-95)/100,0)),b=view.WorldToViewportPoint(new Vector3((float)(box.x+box.w)/100,((float)(box.y+box.h)-95)/100,0));Rect r=new Rect(a.x*1280,(1-b.y)*720,(b.x-a.x)*1280,(b.y-a.y)*720);Fill(new Rect(r.x,r.y,r.width,1),color);Fill(new Rect(r.x,r.yMax,r.width,1),color);Fill(new Rect(r.x,r.y,1,r.height),color);Fill(new Rect(r.xMax,r.y,1,r.height),color);}
    }
}
