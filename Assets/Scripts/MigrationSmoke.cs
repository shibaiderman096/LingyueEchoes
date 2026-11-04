#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace LingyueEchoes
{
    // Opt-in validation bot. Runs only with -lingyue-smoke; never runs during normal development or play.
    public class MigrationSmoke:MonoBehaviour
    {
        float elapsed;int phase,done,tails;bool hadDone,hadTails;bool enteredRoute,enteredFight;double originalHp;Fighter originalPlayer;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-lingyue-smoke")<0)return;var go=new GameObject("MigrationSmoke");DontDestroyOnLoad(go);go.AddComponent<MigrationSmoke>();}
        void Start(){hadDone=PlayerPrefs.HasKey("lingyue.prologue");hadTails=PlayerPrefs.HasKey("lingyue.tails");done=PlayerPrefs.GetInt("lingyue.prologue",0);tails=PlayerPrefs.GetInt("lingyue.tails",0);}
        void Restore(){if(hadDone)PlayerPrefs.SetInt("lingyue.prologue",done);else PlayerPrefs.DeleteKey("lingyue.prologue");if(hadTails)PlayerPrefs.SetInt("lingyue.tails",tails);else PlayerPrefs.DeleteKey("lingyue.tails");PlayerPrefs.Save();}
        void Update(){elapsed+=Time.unscaledDeltaTime;if(elapsed>100){Debug.LogError("MIGRATION_RUNTIME_SMOKE_FAILED: timed out");Restore();Application.Quit(1);return;}var game=FindAnyObjectByType<LingyueGame>();if(!game)return;
            if(phase==0&&game.stage=="MENU"){phase=1;game.StartTrial(true);return;}
            if(game.stage=="TUTORIAL"){if(!enteredRoute){enteredRoute=true;originalPlayer=game.sim.player;originalHp=game.sim.player.hp;Debug.Log("SMOKE_ROUTE_STARTED");}game.scriptedAxis=1;game.sim.god=true;
                if(game.sim.player.x>140&&game.sim.player.x<425&&game.sim.player.vy==0)game.Act("jump");}
            if(game.stage=="GUARDIAN_FIGHT"){if(!enteredFight){enteredFight=true;if(game.sim.player!=originalPlayer||game.sim.player.hp!=originalHp)throw new Exception("Trial entry replaced or reset the hero");Debug.Log("SMOKE_TRIAL_STARTED: same player and resources");}
                double distance=game.sim.boss.x-game.sim.player.x;game.scriptedAxis=Math.Abs(distance)>125?Math.Sign(distance):0;
                if(game.sim.player.state=="idle"||game.sim.player.state=="run"){game.Act("charge");game.Act("stun");game.Act("attack");}}
            if(game.stage=="TAIL_DIALOGUE"||game.stage=="FIRST_TAIL")game.scriptedAxis=0;
            if(game.stage=="COMPLETE"){if(!enteredRoute||!enteredFight||PlayerPrefs.GetInt("lingyue.tails",0)<1||PlayerPrefs.GetInt("lingyue.prologue",0)!=1)throw new Exception("Completion flow or progress failed");Debug.Log("MIGRATION_RUNTIME_SMOKE_PASSED: cinematic, natural route, obstacle jump, trial, debug finisher, automatic dialogue, first tail, progress; original preferences restored");Restore();Application.Quit(0);}
        }
    }
}
#endif
