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
        RefreshPrologue();
        AssetDatabase.SaveAssets();if(!string.IsNullOrEmpty(returnPath))EditorSceneManager.OpenScene(returnPath);
        Debug.Log("SCENERY_REBUILD_PASSED: prologue sunrise backdrop, independent landmarks and reusable paving.");
    }
    public static void RefreshPrologue()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity");
        var game=UnityEngine.Object.FindAnyObjectByType<LingyueGame>();
        var world=game.transform.Find("World");
        var props=world.Find("Props");
        Remove(world,"SceneryModules");
        foreach(string old in new[]{"MoonLantern1200","MoonLantern2280","BrokenSteps","MoonGateCheckpoint","MoonGateExit"})Remove(props,old);
        PixelWorld.CreatePrologueBackdrop(world);
        PixelWorld.PlacePracticeDummy(props);
        PixelWorld.PlaceBrokenSteps(props);
        PixelWorld.PlaceCheckpoint(props);
        PixelWorld.PlacePrologueGate(props);
        MoveToX(world.Find("Hero"),(float)PrologueLayout.WakeX/100);
        MoveToX(world.Find("Guardian"),(float)PrologueLayout.WakeGuideX/100);
        Camera.main.backgroundColor=PixelWorld.PrologueSky;
        MoveToX(Camera.main.transform,PrologueLayout.ClampViewX((float)PrologueLayout.WakeX+100,PrologueLayout.FullViewHalfWidth)/100);
        EditorSceneManager.SaveScene(scene);
    }
    static void MoveToX(Transform target,float x){var p=target.localPosition;p.x=x;target.localPosition=p;}
    static void Remove(Transform parent,string name){var child=parent.Find(name);if(child)UnityEngine.Object.DestroyImmediate(child.gameObject);}
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
