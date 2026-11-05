#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;

namespace LingyueEchoes
{
    // Opt-in validation bot: command-line flag or the explicit editor check menu only.
    public class MigrationSmoke:MonoBehaviour
    {
        float elapsed,beatTime,parryFarewellTime;
        int phase,firstVictoryFrame,stepsBlockedFrame=-1;
        bool editorRun,captured,restored,sawTail,sawEpilogue,sawOpeningPose,stepsJumpIssued,lostFirstFight,sawOpeningStory,sawDummyStop;
        const int TestSlot=2;
        static readonly string[] progressKeys={PrologueLayout.ProgressKey,PrologueLayout.TailCountKey,PrologueLayout.CheckpointKey,PrologueLayout.FirstVictoryStoryKey};
        static readonly string[] saveKeys=new[]{1,TestSlot}.SelectMany(slot=>progressKeys.Select(key=>PrologueLayout.SaveKey(key,slot))).ToArray();
        static string TestKey(string key)=>PrologueLayout.SaveKey(key,TestSlot);
        readonly int?[] saved=new int?[saveKeys.Length];
        double originalHp;
        Fighter originalPlayer;
#if UNITY_EDITOR
        public static void RunInEditor(){if(!Application.isPlaying)throw new InvalidOperationException("Enter Play mode first.");if(FindAnyObjectByType<MigrationSmoke>())return;var go=new GameObject("MigrationSmoke");DontDestroyOnLoad(go);go.AddComponent<MigrationSmoke>().editorRun=true;FindAnyObjectByType<LingyueGame>().Home();}
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-lingyue-smoke")<0)return;var go=new GameObject("MigrationSmoke");DontDestroyOnLoad(go);go.AddComponent<MigrationSmoke>();}
        void Start(){
            for(int i=0;i<saveKeys.Length;i++){string key=saveKeys[i];saved[i]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetInt(key):null;}
            captured=true;foreach(string key in saveKeys)PlayerPrefs.DeleteKey(key);
        }
        void Restore(){if(!captured||restored)return;restored=true;for(int i=0;i<saveKeys.Length;i++){if(saved[i].HasValue)PlayerPrefs.SetInt(saveKeys[i],saved[i].Value);else PlayerPrefs.DeleteKey(saveKeys[i]);}PlayerPrefs.Save();}
        void Finish(int code){Restore();if(editorRun){var game=FindAnyObjectByType<LingyueGame>();if(game){game.scriptedAxis=null;game.Home();}Destroy(gameObject);}else Application.Quit(code);}
        void OnDestroy(){if(editorRun)Restore();}
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        void Next(){phase++;beatTime=0;}
        void Update(){
            var game=FindAnyObjectByType<LingyueGame>();if(!game)return;
            elapsed+=Time.unscaledDeltaTime;beatTime+=Time.unscaledDeltaTime;
            try{
                Require(elapsed<180,$"Timed out in phase {phase} ({game.stage}).");
                if(phase==0&&game.stage=="MENU"){
                    foreach(string key in progressKeys)PlayerPrefs.SetInt(key,1);
                    game.StartTrial();
                    Require(game.SavePickerOpen&&LingyueGame.SaveStatus(1).StartsWith("序章已完成")&&LingyueGame.SaveStatus(TestSlot).StartsWith("空档"),"Start opens a picker with legacy progress in slot one and an independent empty slot.");
                    game.ActivateSaveChoice(PrologueLayout.SaveSlots);
                    Require(game.stage=="MENU"&&!game.SavePickerOpen,"Returning from the picker does not launch the game.");
                    game.StartTrial();Next();game.ActivateSaveChoice(TestSlot-1);return;
                }
                if(phase==1&&game.stage=="STORY")sawOpeningStory=true;
                if(phase==1&&game.stage=="TUTORIAL"){
                    Require(sawOpeningStory&&game.SaveSlot==TestSlot,"Selecting an empty slot plays the opening even with a completed first slot.");
                    var world=game.transform.Find("World");
                    Require(game.sim.terrain.Count==1&&world.Find("SceneryModules").childCount==6,"Expected one broken-step obstacle on the modular route.");
                    var steps=world.Find("Props/BrokenSteps").GetComponent<SpriteRenderer>();
                    float pad=PixelWorld.RubblePadding/100;
                    Require(steps.sprite&&steps.sprite.name.StartsWith("broken-rubble")&&Math.Abs(steps.bounds.min.x+pad-PrologueLayout.StepsLeft/100)<.01&&
                        Math.Abs(steps.bounds.max.y-pad-(-.95+PrologueLayout.StepsHeight/100))<.01,"The visible rubble aligns with the ground and collision.");
                    Require(!Seal(game,true).enabled&&Seal(game,false).enabled,"Only the outer arena boundary is sealed during exploration.");
                    originalPlayer=game.sim.player;originalHp=originalPlayer.hp;game.sim.god=true;Next();
                }
                if(phase<2)return;
                var c=game.sim;var p=c.player;var b=c.boss;
                switch(phase){
                    case 2: // Complete each lesson by interacting while the guardian is stationary.
                        game.Interact();if(b.x>-1235)Next();break;
                    case 3:
                        if(!game.BrokenStepsCleared){
                            game.scriptedAxis=1;
                            double wall=PrologueLayout.StepsLeft-Combat.BodyWidth(p)/2;
                            // The crossing is recorded on the first fixed step after landing.
                            if(!stepsJumpIssued&&p.x>=wall&&p.y==0&&p.vy==0){
                                if(stepsBlockedFrame<0)stepsBlockedFrame=c.frame;
                                Require(p.x==wall,"Walking must stop at the broken steps.");
                                if(c.frame-stepsBlockedFrame>=18){game.Act("jump");stepsJumpIssued=true;}
                            }
                            return;
                        }
                        Require(stepsJumpIssued&&p.x-Combat.BodyWidth(p)/2>=PrologueLayout.StepsLeft+PrologueLayout.StepsWidth,
                            "Tutorial completes only after crossing and landing beyond the steps.");
                        game.scriptedAxis=p.x<770?1:0;
                        if(b.x==PrologueLayout.DummyGuideX&&b.state=="idle"){
                            var dummy=game.transform.Find("World/Props/PracticeDummy");
                            Require(dummy&&Math.Abs(dummy.position.x-b.x/100)>1,"Guide stands apart from the visible dummy.");
                            sawDummyStop=true;
                            if(p.x>=770)game.Interact();
                        }
                        if(b.x>PrologueLayout.PracticeDummyX&&sawDummyStop)Next();
                        break;
                    case 4:
                        game.scriptedAxis=p.x<910?1:0;
                        if(p.x>=910&&b.x==1030&&!c.enemyActive)game.Interact();
                        if(c.enemyActive)Next();
                        break;
                    case 5:
                        if(b.move!=null&&b.age>=b.move.wind-4&&b.age<b.move.wind)game.Act("parry");
                        if(c.deflectSerial==1&&b.x==1030)parryFarewellTime+=Time.unscaledDeltaTime;
                        if(b.x>1030){
                            Require(c.deflectSerial==1&&!c.enemyActive&&parryFarewellTime>1f,"After one parry, the guide stays for the farewell before moving.");
                            Next();
                        }
                        break;
                    case 6:
                        game.scriptedAxis=p.x<PrologueLayout.GuardianWaitX-120?1:0;
                        Require(game.stage=="TUTORIAL"&&!c.enemyActive,"Approaching the gate must not start combat.");
                        if(p.x>=PrologueLayout.GuardianWaitX-120&&beatTime>3.5f){
                            Require(PlayerPrefs.GetInt(TestKey(PrologueLayout.CheckpointKey),0)==1,"Passing the gate lamp saves to the selected slot.");
                            game.Interact();if(game.stage=="GATE_DIALOGUE")Next();
                        }
                        break;
                    case 7:case 11:
                        Require(game.stage=="GATE_DIALOGUE"&&!c.enemyActive&&!c.ai,"Battle must wait for the dialogue to close.");
                        if(beatTime>.5f)game.Interact();
                        if(game.stage=="GUARDIAN_INTRO"){
                            Require(p==originalPlayer&&p.x==PrologueLayout.PlayerFightX&&c.boss.x==PrologueLayout.GuardianFightX&&p.y==0&&p.vy==0&&p.move==null&&p.hp==originalHp,"Trial intro resets the fighters' positions and preserves the hero and HP.");
                            Require(c.boundsMin==PrologueLayout.ArenaMin&&c.boundsMax==PrologueLayout.ArenaMax&&c.phase==1&&c.moveIndex==0&&c.boss.hp==c.boss.maxHp&&!c.BossDefeated,"Each round resets boss state and uses full arena bounds.");
                            sawOpeningPose=false;Next();
                        }
                        break;
                    case 8:case 12:
                        if(game.stage=="GUARDIAN_INTRO"){
                            Require(!c.enemyActive&&!c.ai&&c.hits.Count==0,"Opening animation is purely visual.");
                            Require(Seal(game,true).enabled&&Seal(game,false).enabled,"Both arena edges are marked during the trial.");
                            if(Camera.main.orthographicSize<3.4f)sawOpeningPose=true;
                            return;
                        }
                        Require(game.stage=="GUARDIAN_FIGHT"&&c.enemyActive&&c.ai&&sawOpeningPose,"Camera and intro precede active combat.");
                        if(!lostFirstFight){
                            c.god=c.ai=false;p.hp=1;p.x=b.x-90;game.scriptedAxis=0;c.DebugBoss(0);
                            phase=14;beatTime=0;break;
                        }
                        Next();break;
                    case 9:case 13:
                        if(game.stage=="GUARDIAN_FIGHT"){
                            c.ai=false;
                            double distance=b.x-p.x;game.scriptedAxis=Math.Abs(distance)>125?Math.Sign(distance):0;
                            if(Math.Abs(distance)<=125&&(p.state=="idle"||p.state=="run")){game.Act("charge");game.Act("stun");game.Act("attack");}
                            return;
                        }
                        game.scriptedAxis=0;
                        Require(c.BossDefeated&&!c.enemyActive&&b.hp>0&&b.state!="dead"&&game.transform.Find("World/Guardian").gameObject.activeInHierarchy,"The defeated guardian survives and remains visible.");
                        Require(!Seal(game,true).enabled,"The arena exit opens after a victory.");
                        if(phase==9){
                            Require(game.stage=="TAIL_DIALOGUE","First victory automatically starts the tail story.");
                            firstVictoryFrame=c.frame;Next();
                        }
                        else{
                            Require(game.stage=="GATE_READY"&&PlayerPrefs.GetInt(TestKey(PrologueLayout.TailCountKey),0)==1&&
                                progressKeys.All(key=>PlayerPrefs.GetInt(key,0)==1),"Rematch does not grant another tail or change the first slot.");
                            Debug.Log("MIGRATION_RUNTIME_SMOKE_PASSED: save picker opens and cancels, independent empty and occupied slots, selected-slot reload, checkpoint retry, first victory, epilogue, and rematch.");
                            Finish(0);
                        }
                        break;
                    case 10:
                        if(game.stage=="FIRST_TAIL")sawTail=true;
                        if(game.stage=="TAIL_EPILOGUE")sawEpilogue=true;
                        if(game.stage!="GATE_READY"){
                            Require(!game.BattleOn&&c.frame==firstVictoryFrame&&!PlayerPrefs.HasKey(TestKey(PrologueLayout.FirstVictoryStoryKey)),"Story blocks combat and is marked seen only after completion.");
                            return;
                        }
                        Require(sawTail&&sawEpilogue&&PlayerPrefs.GetInt(TestKey(PrologueLayout.TailCountKey),0)==1&&PlayerPrefs.GetInt(TestKey(PrologueLayout.ProgressKey),0)==1&&
                            PlayerPrefs.GetInt(TestKey(PrologueLayout.FirstVictoryStoryKey),0)==1&&LingyueGame.SaveStatus(TestSlot).StartsWith("序章已完成"),"First tail, epilogue and saved progression in the selected slot.");
                        game.Interact();if(game.stage=="GATE_DIALOGUE")Next();
                        break;
                    case 14:
                        if(!game.DeathMenuReady)return;
                        Require(p.hp==0&&!c.BossDefeated&&!PlayerPrefs.HasKey(TestKey(PrologueLayout.FirstVictoryStoryKey))&&PlayerPrefs.GetInt(TestKey(PrologueLayout.TailCountKey),0)==0,
                            "A real first defeat opens the death menu without granting victory progress.");
                        // Simulate a pre-update save with progression flags but no record of this closing scene.
                        PlayerPrefs.SetInt(TestKey(PrologueLayout.ProgressKey),1);PlayerPrefs.SetInt(TestKey(PrologueLayout.TailCountKey),1);
                        lostFirstFight=true;game.Retry();
                        Require(game.stage=="FADE_IN"&&game.sim.player.x==PrologueLayout.CheckpointX&&game.sim.player.hp==game.sim.player.maxHp,
                            "Retry returns a living player to the moon gate checkpoint.");
                        Next();break;
                    case 15:
                        if(game.stage!="GATE_READY")return;
                        game.Home();
                        game.StartTrial();
                        Require(game.SavePickerOpen&&LingyueGame.SaveStatus(TestSlot).StartsWith("月门灯"),"Picker shows the saved location, even if older progress flags exist.");
                        Next();game.ActivateSaveChoice(TestSlot-1);break;
                    case 16:
                        Require(game.stage!="STORY"&&game.stage!="TUTORIAL","Starting from a saved checkpoint skips the opening.");
                        if(game.stage!="GATE_READY")return;
                        Require(game.SaveSlot==TestSlot&&!PlayerPrefs.HasKey(TestKey(PrologueLayout.FirstVictoryStoryKey)),"Retry and reload keep the same slot without marking the story as seen.");
                        c.god=true;originalPlayer=p;originalHp=p.hp;
                        game.scriptedAxis=p.x<PrologueLayout.GuardianWaitX-120?1:0;
                        if(p.x>=PrologueLayout.GuardianWaitX-120){
                            game.Interact();if(game.stage=="GATE_DIALOGUE"){phase=7;beatTime=0;}
                        }
                        break;
                }
            }catch(Exception error){Debug.LogError("MIGRATION_RUNTIME_SMOKE_FAILED: "+error);Finish(1);}
        }
        static SpriteRenderer Seal(LingyueGame game,bool left)=>game.transform.Find(left?"World/Props/ArenaSealLeft":"World/Props/ArenaSealRight").GetComponent<SpriteRenderer>();
    }
}
#endif
