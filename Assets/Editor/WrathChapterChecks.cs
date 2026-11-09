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
                chapter.embers&&
                chapter.entranceDoor&&chapter.exteriorScenery&&chapter.exteriorArrivalAnchor&&chapter.fireMark,"chapter has native scene references");
            Need(!chapter.world.Find("Props/Entrance checkpoint")&&!chapter.world.Find("Props/Arena seal left")&&
                !chapter.world.Find("Props/Arena seal right")&&!chapter.world.Find("Props/Shrine return"),
                "removed checkpoint and arena steles are absent");
            Need(chapter.entranceDoorFrames!=null&&chapter.entranceDoorFrames.Length==16&&
                chapter.entranceDoorFrames.All(sprite=>sprite&&sprite.rect.width==320&&sprite.rect.height==320),
                "the oblique doorway has sixteen registered opening frames");
            Need(chapter.entranceDoor.flipX,"the doorway is mirrored toward the arriving hero");
            Need(Quaternion.Angle(chapter.entranceDoor.transform.localRotation,Quaternion.identity)<.01f,
                "door perspective is authored in the artwork, without tilting its upright posts");
            Need(chapter.scenery!=null&&chapter.scenery.Length==1&&chapter.scenery[0]&&chapter.scenery[0].sprite,
                "one interior backdrop supplies the hall");
            Need(chapter.exteriorScenery.sprite&&chapter.exteriorPillars!=null&&chapter.exteriorPillars.Length==2&&
                chapter.exteriorPillars.All(r=>r&&r.sprite)&&chapter.exteriorPillars[0].sprite==chapter.exteriorPillars[1].sprite,
                "the entrance has two pillars sharing one sprite");
            var farPillar=chapter.exteriorPillars[0];var nearPillar=chapter.exteriorPillars[1];
            Need(!chapter.exteriorCanopy||chapter.exteriorCanopy.sprite,"an optional entrance canopy has its authored sprite");
            var hero=chapter.world.Find("Hero").GetComponent<SpriteRenderer>();
            Need(chapter.exteriorScenery.sortingOrder<farPillar.sortingOrder&&farPillar.sortingOrder<hero.sortingOrder&&
                nearPillar.sortingOrder>hero.sortingOrder,"far and near pillars straddle the hero sorting layer");
            chapter.SetInside(false);
            Need(!chapter.exteriorCanopy||chapter.exteriorCanopy.enabled,"outside enables an optional entrance canopy");
            Need(chapter.exteriorScenery.enabled&&chapter.exteriorPillars.All(r=>r.enabled)&&chapter.scenery.All(r=>!r.enabled)&&chapter.entranceDoor.enabled,"outside enables the exterior, pillars and door");
            chapter.SetInside(true);
            Need(!chapter.exteriorCanopy||!chapter.exteriorCanopy.enabled,"inside hides an optional entrance canopy");
            Need(!chapter.exteriorScenery.enabled&&chapter.exteriorPillars.All(r=>!r.enabled)&&chapter.scenery.All(r=>r.enabled)&&!chapter.entranceDoor.enabled,"inside enables only the interior");
            var camera=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<Camera>(true)).Single(c=>c.CompareTag("MainCamera"));
            // Gameplay letterboxes to 16:9 before calculating the exterior bounds.
            float viewLeft=camera.transform.position.x-camera.orthographicSize*(16f/9);
            Need(chapter.exteriorArrivalAnchor.position.x>viewLeft&&chapter.exteriorArrivalAnchor.position.x<camera.transform.position.x,
                "the authored arrival anchor stands in the visible left half of the exterior road");
            Need(chapter.exteriorArrivalAnchor.position.x<chapter.entranceDoor.transform.position.x,
                "arrival begins outside before the physical door threshold");
            Need(farPillar.sortingOrder<chapter.entranceDoor.sortingOrder,"the far pillar base is partially covered by the door");
            Need(WrathRoute.Bridge().Length==0&&!chapter.Terrain().Any(),"the deleted approach has no remaining bridge terrain");
            Need(chapter.UseActorArt(false,0)==null&&chapter.UseActorArt(false,4)==null,"no approach enemy art is available");
            Need(chapter.world.GetComponentsInChildren<SpriteRenderer>(true).All(r=>r.sprite),"all chapter sprites resolve");
        }finally{EditorSceneManager.ClosePreviewScene(scene);}
        var text=JsonUtility.FromJson<PrologueContent>(Resources.Load<TextAsset>("Data/wrath-content").text);
        var ids=new[]{"stele","wrathEnter","wrathBossPre","wrathPhase2","wrathBossAfter","wrathEpilogue","wrathRetry","wrathRematch"};
        Need(ids.All(id=>text.scripts.Count(s=>s.id==id&&s.lines.Length>0&&s.lines.All(l=>!string.IsNullOrWhiteSpace(l.text)))==1),"all progression dialogue IDs are unique");
        Need(Enumerable.Range(0,3).All(checkpoint=>WrathRoute.EncounterAfter(checkpoint)==0&&WrathRoute.Spawn(checkpoint)==WrathRoute.Entrance),
            "every legacy checkpoint resumes at the general entrance");
        Need(WrathRoute.Fights.Length==1&&WrathRoute.Fights[0].Pattern==WrathRoute.General,"the chapter contains only the general boss");
        var general=WrathRoute.Fights[0];
        Need(general.Trigger==3475&&general.Min==3210&&general.Max==4130&&general.EnemyX==3830&&general.Hp==870,
            "the general's combat position, arena and health remain unchanged");
        foreach(var e in WrathRoute.Fights){
            Need(e.Min<e.Trigger&&e.Trigger<e.EnemyX&&e.EnemyX<e.Max,"encounter start and enemy stand inside the arena");
            foreach(var move in e.Pattern.opening.Concat(e.Pattern.rage??Array.Empty<Move>())){
                var clip=PixelWorld.Definition("guardian8",move.pose);
                Need(clip!=null&&PixelActor.DrawnAttackCell(clip,move,move.wind+1)==clip.contactPose,"custom move has an aligned contact animation");
                Need(move.fireDelay==0||move.fireDelay>=move.active&&move.fireDelay+5<=move.active+move.recovery,"a delayed flame resolves inside its owning move");
            }
        }
        var regular=new Combat{ai=false};regular.ResetBoss(90,64,new OpponentPattern(new[]{Rules.Boss[0]},30,2,180,isBoss:false));
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
