using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LingyueEchoes;

public static class ScenerySetup
{
    [MenuItem("Lingyue/Run prologue play check")]
    public static void RunPlayCheck()=>MigrationSmoke.RunInEditor();
    [MenuItem("Lingyue/Rebuild reusable scenery")]
    public static void Rebuild()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before rebuilding scenery.");
        var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(current.isDirty)throw new InvalidOperationException("Save the current scene before rebuilding scenery.");
        string returnPath=current.path;
        Directory.CreateDirectory("Assets/Resources/Scenery");AssetDatabase.Refresh();
        for(int column=1;column<=3;column++)Save(PixelWorld.CreateSceneryModule(column));
        SaveProp("PracticeDummy","prologue_route/practice_dummy",84,120);
        SaveProp("MoonLantern","prologue_route/moon_lantern",59,118);
        foreach(string path in new[]{"Assets/Scenes/MainMenu.unity","Assets/Scenes/Prologue.unity"})
        {
            var scene=EditorSceneManager.OpenScene(path);var game=UnityEngine.Object.FindAnyObjectByType<LingyueGame>();
            if(!game)throw new InvalidOperationException("Missing game root in "+path);
            var old=game.transform.Find("World");Transform hero=null,guardian=null;
            if(old){hero=old.Find("Hero");guardian=old.Find("Guardian");if(hero)hero.SetParent(game.transform,false);if(guardian)guardian.SetParent(game.transform,false);UnityEngine.Object.DestroyImmediate(old.gameObject);}
            var world=PixelWorld.Create(game.transform);
            foreach(var actor in new[]{hero,guardian})if(actor){UnityEngine.Object.DestroyImmediate(world.Find(actor.name).gameObject);actor.SetParent(world,false);}
            world.gameObject.SetActive(scene.name!="MainMenu");EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();if(!string.IsNullOrEmpty(returnPath))EditorSceneManager.OpenScene(returnPath);
        Debug.Log("SCENERY_REBUILD_PASSED: 3 reusable background modules at 75% scale, 2 prop prefabs, broken-step obstacle, arena 1200..2280.");
    }
    static void SaveProp(string name,string resource,float width,float height)
    {
        var root=new GameObject(name);PixelWorld.Sprite(root.transform,name,resource,0,-95,width,height,5,new Vector2(.5f,0));Save(root);
    }
    static void Save(GameObject root)
    {
        try{PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Scenery/"+root.name+".prefab");}
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
