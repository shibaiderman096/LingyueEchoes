using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor.Build.Reporting;
using LingyueEchoes;

public class PixelImport:AssetPostprocessor
{
    void OnPreprocessTexture(){if(!assetPath.StartsWith("Assets/Resources/")||!assetPath.EndsWith(".png"))return;
        var importer=(TextureImporter)assetImporter;importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;
        importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=8192;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Custom;
        bool hero=assetPath.Contains("/pixel8/frames/")&&!new[]{"slash/","impact/","parry_success/","perfect_dodge/"}.Any(s=>assetPath.Contains(s));
        settings.spritePivot=assetPath.Contains("/guardian8/frames/")?new Vector2(.5f,24f/224):hero?new Vector2(.5f,.125f):new Vector2(.5f,.5f);importer.SetTextureSettings(settings);
    }
}
public static class ProjectSetup
{
    [MenuItem("Lingyue/Prepare project")]
    public static void Prepare(){EditorSettings.defaultBehaviorMode=EditorBehaviorMode.Mode2D;PlayerSettings.companyName="Lingyue";PlayerSettings.productName="铃月回响";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"com.lingyue.echoes");PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.lingyue.echoes");
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
        PlayerSettings.runInBackground=false;PlayerSettings.colorSpace=ColorSpace.Gamma;SetInput();
        var manifest=JsonUtility.FromJson<ClipManifest>(File.ReadAllText("Assets/Resources/Data/animations.json"));
        Directory.CreateDirectory("Assets/Resources/Animations/pixel8");Directory.CreateDirectory("Assets/Resources/Animations/guardian8");Directory.CreateDirectory("Assets/Scenes");Directory.CreateDirectory("Assets/Prefabs");AssetDatabase.Refresh();
        foreach(var d in manifest.clips){var clip=new AnimationClip{name=d.name,frameRate=d.sample,legacy=false};
            var keys=d.times.Select((time,i)=>new ObjectReferenceKeyframe{time=time,value=Resources.Load<Sprite>(d.frames[i])}).ToList();
            if(keys.Any(k=>!k.value))throw new Exception("Missing animation frame: "+d.name);
            keys.Add(new ObjectReferenceKeyframe{time=Math.Max(d.duration-.00001f,d.times.Last()),value=keys.Last().value});
            AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys.ToArray());
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=d.loop;AnimationUtility.SetAnimationClipSettings(clip,settings);clip.wrapMode=d.loop?WrapMode.Loop:WrapMode.ClampForever;
            string output="Assets/Resources/"+d.path+".anim";if(File.Exists(output))AssetDatabase.DeleteAsset(output);AssetDatabase.CreateAsset(clip,output);}
        AssetDatabase.SaveAssets();
        foreach(string folder in new[]{"pixel8","guardian8"}){string path="Assets/Resources/Animations/"+folder+"/Controller.controller";if(File.Exists(path))AssetDatabase.DeleteAsset(path);var controller=UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);var machine=controller.layers[0].stateMachine;foreach(var d in manifest.clips.Where(c=>c.folder==folder)){var state=machine.AddState(d.name);state.motion=Resources.Load<AnimationClip>(d.path);if(d.name=="idle")machine.defaultState=state;}}
        AssetDatabase.SaveAssets();CreateScene("MainMenu");CreateScene("Prologue");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity",true),new EditorBuildSettingsScene("Assets/Scenes/Prologue.unity",true)};
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");AssetDatabase.SaveAssets();
        MigrationChecks.Run();Debug.Log("LINGYUE_MIGRATION_READY: 2 scenes, native sprite clips, golden parity passed");
    }
    static void SetInput(){var playerSettings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);var active=playerSettings.FindProperty("activeInputHandler");if(active!=null){active.intValue=0;playerSettings.ApplyModifiedPropertiesWithoutUndo();}
        var manager=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);var axes=manager.FindProperty("m_Axes");
        foreach(var entry in new[]{Tuple.Create("PadX",0),Tuple.Create("PadDpad",5)}){int index=-1;for(int i=0;i<axes.arraySize;i++)if(axes.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue==entry.Item1)index=i;
            if(index<0){index=axes.arraySize;axes.InsertArrayElementAtIndex(index);}var axis=axes.GetArrayElementAtIndex(index);axis.FindPropertyRelative("m_Name").stringValue=entry.Item1;
            foreach(string key in new[]{"negativeButton","positiveButton","altNegativeButton","altPositiveButton"})axis.FindPropertyRelative(key).stringValue="";
            axis.FindPropertyRelative("gravity").floatValue=0;axis.FindPropertyRelative("dead").floatValue=.2f;axis.FindPropertyRelative("sensitivity").floatValue=1;axis.FindPropertyRelative("snap").boolValue=false;axis.FindPropertyRelative("invert").boolValue=false;axis.FindPropertyRelative("type").intValue=2;axis.FindPropertyRelative("axis").intValue=entry.Item2;axis.FindPropertyRelative("joyNum").intValue=0;}
        manager.ApplyModifiedPropertiesWithoutUndo();}
    static void CreateScene(string name){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var camera=new GameObject("Main Camera");camera.tag="MainCamera";var cam=camera.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=3.6f;cam.transform.position=new Vector3(-9.6f,0,-10);cam.backgroundColor=new Color(.043f,.075f,.106f);camera.AddComponent<AudioListener>();
        var root=new GameObject("LingyueEchoes");root.AddComponent<LingyueGame>();var world=PixelWorld.Create(root.transform);
        foreach(string actor in new[]{"Hero","Guardian"}){var go=world.Find(actor).gameObject;PrefabUtility.SaveAsPrefabAsset(go,"Assets/Prefabs/"+actor+".prefab");}
        if(name=="MainMenu")world.gameObject.SetActive(false);EditorSceneManager.SaveScene(scene,"Assets/Scenes/"+name+".unity");}
    [MenuItem("Lingyue/Build Windows")]
    public static void BuildWindows(){MigrationChecks.Run();Directory.CreateDirectory("Builds/Windows");var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName="Builds/Windows/LingyueEchoes.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed: "+result.summary.result);Debug.Log("LINGYUE_WINDOWS_BUILD_PASSED: "+result.summary.totalSize);}
}
