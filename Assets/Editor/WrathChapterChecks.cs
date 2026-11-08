using System;
using System.Linq;
using LingyueEchoes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WrathChapterChecks
{
    static void Need(bool value,string text){if(!value)throw new Exception("Wrath: "+text);}
    [MenuItem("Lingyue/Check Wrath chapter")]
    public static void Run()
    {
        Need(EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==WrathSceneSetup.ScenePath),"scene is enabled for build");
        var scene=EditorSceneManager.OpenPreviewScene(WrathSceneSetup.ScenePath);
        try{
            var root=scene.GetRootGameObjects().Single(go=>go.GetComponent<WrathChapter>());
            var chapter=root.GetComponent<WrathChapter>();
            Need(root.GetComponent<LingyueGame>()&&chapter.world&&chapter.world.Find("Hero")&&chapter.world.Find("Guardian")&&
                chapter.firstLamp&&chapter.secondLamp&&chapter.leftSeal&&chapter.rightSeal&&chapter.returnSeal&&chapter.embers&&
                chapter.generalArmor&&chapter.weaponGlow&&chapter.armorGlow&&chapter.fireMark,"chapter has native scene references");
            var expected=WrathRoute.Bridge();var actual=chapter.Terrain().ToArray();
            Need(actual.Length==expected.Length&&actual.Zip(expected,(a,b)=>Math.Abs(a.x-b.x)<.1&&Math.Abs(a.h-b.h)<.1&&Math.Abs(a.w-b.w)<.1).All(x=>x),"three physical footholds match their artwork");
            Need(chapter.world.GetComponentsInChildren<SpriteRenderer>(true).All(r=>r.sprite),"all chapter sprites resolve");
        }finally{EditorSceneManager.ClosePreviewScene(scene);}
        var text=JsonUtility.FromJson<PrologueContent>(Resources.Load<TextAsset>("Data/wrath-content").text);
        var ids=new[]{"stele","wrathEnter","wrathBossPre","wrathPhase2","wrathBossAfter","wrathEpilogue","wrathRetry","wrathRematch"};
        Need(ids.All(id=>text.scripts.Count(s=>s.id==id&&s.lines.Length>0&&s.lines.All(l=>!string.IsNullOrWhiteSpace(l.text)))==1),"all progression dialogue IDs are unique");
        Need(WrathRoute.EncounterAfter(0)==0&&WrathRoute.EncounterAfter(1)==1&&WrathRoute.EncounterAfter(2)==4,"each lamp restarts at the correct encounter");
        Need(WrathRoute.Fights.Select(f=>f.Trigger).SequenceEqual(WrathRoute.Fights.Select(f=>f.Trigger).OrderBy(x=>x)),"encounters are ordered along the route");
        foreach(var e in WrathRoute.Fights){
            Need(e.Min<e.Trigger&&e.Trigger<e.EnemyX&&e.EnemyX<e.Max,"encounter start and enemy stand inside the arena");
            foreach(var move in e.Pattern.opening.Concat(e.Pattern.rage??Array.Empty<Move>())){
                var clip=PixelWorld.Definition("guardian8",move.pose);
                Need(clip!=null&&PixelActor.DrawnAttackCell(clip,move,move.wind+1)==clip.contactPose,"custom move has an aligned contact animation");
                Need(move.fireDelay==0||move.fireDelay>=move.active&&move.fireDelay+5<=move.active+move.recovery,"a delayed flame resolves inside its owning move");
            }
        }
        var regular=new Combat{ai=false};regular.ResetBoss(90,64,WrathRoute.Fights[0].Pattern);
        regular.boss.hp=20;regular.Step();Need(regular.phase==1,"a regular enemy never enters the boss phase");
        var c=new Combat{ai=false};c.ResetBoss(0,600,WrathRoute.General);
        c.player.x=400;c.boss.hp=391;c.Step();
        Need(c.phase==1,"above 65 percent health stays in the first phase");
        c.boss.hp=390;c.Step();Need(c.phase==2&&c.boss.state=="phase","65 percent health enters the distinct wrath phase");
        for(int i=0;i<WrathRoute.General.phaseFrames;i++)c.Step();
        c.boss.hp=180;c.Step();Need(c.phase==3&&c.boss.state=="phase","30 percent health enters the third phase");
        Need(WrathRoute.General.behavior.moves.Length==4&&WrathRoute.General.behavior.phases.Length==3,"general uses the shared configurable behavior");
        Debug.Log("WRATH_CHAPTER_CHECKS_PASSED");
    }
    public static void RunBatch()
    {
        try{MigrationChecks.Run();Run();if(Application.isBatchMode)EditorApplication.Exit(0);}
        catch(Exception exception){Debug.LogException(exception);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }
}
