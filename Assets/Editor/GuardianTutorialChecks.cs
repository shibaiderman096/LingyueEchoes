using System;
using System.Linq;
using LingyueEchoes;
using UnityEngine;
using UnityEditor;

public static class GuardianTutorialChecks
{
    [Serializable] class MoveSlot {public Move move;}
    [Serializable] class BossMoves {public MoveSlot[] moves;}
    static void Need(bool value,string message){if(!value)throw new Exception("Guardian tutorial: "+message);}
    static Move JsonSlash()=>JsonUtility.FromJson<Move>("{\"name\":\"横斩\",\"pose\":\"slash\",\"wind\":30,\"active\":5,\"recovery\":28,\"reach\":203,\"height\":142,\"damage\":18,\"posture\":24,\"parry\":true,\"pulses\":null}");
    static Combat Spar(Move move,out TutorialDirector guide){
        var c=new Combat{ai=false,enemyActive=false,boundsMin=PrologueLayout.RouteStart,boundsMax=PrologueLayout.ArenaMax};
        guide=new TutorialDirector();guide.Begin(c);
        guide.BeginDialogue("routeWake");guide.FinishDialogue(c,"routeWake");c.boss.x=PrologueLayout.DummyGuideX;guide.Pump(c);
        guide.BeginDialogue("routeDummy");guide.FinishDialogue(c,"routeDummy");c.boss.x=PrologueLayout.SparGuideX;c.player.x=c.boss.x-100;c.player.face=1;guide.Pump(c);
        c.ResetBoss(PrologueLayout.SparGuideX,650,new OpponentPattern(new[]{move},30,0,180));
        guide.BeginDialogue("routeSpar");guide.FinishDialogue(c,"routeSpar");c.beforeStep=guide.Pump;return c;
    }
    [MenuItem("Lingyue/Check guardian tutorial hit feedback")]
    public static void Run(){
        var move=JsonSlash();Debug.Log("GUARDIAN_TUTORIAL_JSON_PULSES: "+(move.pulses==null?"null":move.pulses.Length.ToString()));
        foreach(bool parry in new[]{false,true}){
            var c=Spar(move,out var guide);bool pressed=false,hit=false,impact=false,spark=false;double hp=c.player.hp;
            var root=new GameObject("Guardian tutorial feedback check");
            try{var effects=new PixelEffects(root.transform);
                for(int tick=0;tick<240;tick++){
                    if(parry&&!pressed&&c.boss.move!=null&&c.boss.age==c.boss.move.wind-2){c.Press("parry");pressed=true;}
                    c.Step();hit|=c.hits.Any(h=>h.owner==2);impact|=c.visuals.Any(v=>v.kind=="impact");effects.Draw(c);
                    spark|=root.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.sprite&&r.sprite.name.StartsWith("parry_success-",StringComparison.Ordinal));
                }
                Need(hit,"JSON-loaded teaching slash never opens its hit window");
                Need(c.player.hp==hp,"teaching keeps its nonlethal protection");
                if(parry){Need(c.deflectSerial==1&&!guide.Practicing,"one successful parry must finish the lesson");Need(spark,"successful teaching parry must display an imported spark sprite");}
                else Need(impact,"unblocked teaching hit must emit impact feedback");
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        // Verify every configured guardian attack, not only the teaching slash.
        var asset=Resources.Load<TextAsset>("Data/Bosses/guardian");
        if(asset){var data=JsonUtility.FromJson<BossMoves>(asset.text);Need(data.moves.Length==6,"guardian still exposes six configured moves");
            foreach(var slot in data.moves){var c=new Combat{ai=false,god=true,boundsMin=-2000,boundsMax=2000};c.player.x=-200;c.ResetBoss(0,650,new OpponentPattern(new[]{slot.move},30,0,180));c.DebugBoss(0);bool hit=false;
                int duration=slot.move.wind+slot.move.active+slot.move.recovery+20;for(int i=0;i<duration;i++){c.Step();hit|=c.hits.Any(h=>h.owner==2);}
                Need(hit,"configured guardian move has no hit frames: "+slot.move.name);
            }
        }
        Debug.Log("GUARDIAN_TUTORIAL_CHECKS_PASSED: JSON-loaded attacks, impact feedback, nonlethal protection, parry spark and lesson completion");
    }
    public static void RunBatch(){try{Run();MigrationChecks.Run();WrathChapterChecks.Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
