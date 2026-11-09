using System;
using System.Linq;
using LingyueEchoes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WrathSceneSetup
{
    public const string ScenePath="Assets/Scenes/Chapter01_Wrath.unity";
    const string PalettePath="Assets/Scenes/WrathPlaceholderSprites.asset";
    const string ArtPath="Assets/Art/Wrath/";
    const float ExteriorWidth=14.4f,ExteriorCenterX=31.08f,ExteriorFloorFromTop=.68f;
    const float InteriorWidth=20f,InteriorCenterX=37.1f,FloorY=-.95f;
    const float EntranceDoorX=34.36f,EntranceDoorY=1.22f,EntranceDoorSize=3.90f;
    static Color C(int r,int g,int b,int a=255)=>new Color32((byte)r,(byte)g,(byte)b,(byte)a);
    [MenuItem("Lingyue/Create Wrath scene (if missing)")]
    public static void EnsureScene(){
        if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))RebuildIndoor();
        var scenes=EditorBuildSettings.scenes;
        if(!scenes.Any(s=>s.path==ScenePath))
            EditorBuildSettings.scenes=scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
        else EditorBuildSettings.scenes=scenes.Select(s=>s.path==ScenePath?new EditorBuildSettingsScene(ScenePath,true):s).ToArray();
    }
    static Sprite LoadArt(string name){
        string path=ArtPath+name+".png";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=4096;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        settings.spritePixelsPerUnit=100;settings.spriteMeshType=SpriteMeshType.FullRect;
        settings.spriteAlignment=(int)SpriteAlignment.Center;importer.SetTextureSettings(settings);importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path)??throw new InvalidOperationException("Missing indoor art "+path);
    }
    public static void ApplyArt()=>RebuildIndoor();
    [MenuItem("Lingyue/Rebuild indoor general arena")]
    public static void RebuildIndoor(){
        var interior=LoadArt("Interior");
        var exterior=LoadArt("Exterior");
        var doors=Enumerable.Range(1,16).Select(i=>LoadArt("DoorOpening/door-"+i.ToString("00"))).ToArray();
        var shapes=AssetDatabase.LoadAllAssetsAtPath(PalettePath).OfType<Sprite>().ToDictionary(x=>x.name);
        var block=shapes["Block"];var flame=shapes["Flame"];
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Main Camera");camera.tag="MainCamera";
        var cam=camera.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=3.6f/1.5f;
        cam.transform.position=new Vector3(32.5f,0,-10);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=C(25,19,28);
        camera.AddComponent<AudioListener>();
        var root=new GameObject("LingyueEchoes");root.AddComponent<LingyueGame>();
        var chapter=root.AddComponent<WrathChapter>();
        var world=chapter.world=Group(root.transform,"World");
        var room=chapter.backdrop=Group(world,"General hall · enclosed interior");
        // Floor line in the source is authored at 68% from the top; no scrolling plate drift.
        float height=InteriorWidth*interior.rect.height/interior.rect.width;
        chapter.scenery=new[]{Piece(room,"General hall interior",interior,InteriorCenterX,FloorY+.18f*height,InteriorWidth,height,-20,Color.white)};
        float exteriorHeight=ExteriorWidth*exterior.rect.height/exterior.rect.width;
        chapter.exteriorScenery=Piece(room,"General hall exterior",exterior,ExteriorCenterX,FloorY+(ExteriorFloorFromTop-.5f)*exteriorHeight,ExteriorWidth,exteriorHeight,-20,Color.white);
        chapter.bridgeStones=Array.Empty<SpriteRenderer>();
        var props=Group(world,"Props");
        chapter.entranceDoorFrames=doors;
        chapter.entranceDoor=Piece(props,"General hall · oblique entrance",doors[0],EntranceDoorX,EntranceDoorY,EntranceDoorSize,EntranceDoorSize,6,Color.white);
        chapter.entranceDoor.flipX=true;
        chapter.embers=Group(world,"Interior · drifting cinders");
        for(int i=0;i<18;i++)Piece(chapter.embers,"Cinder "+i,block,32.3f+i*.6f,0,.018f,.018f,18,C(253,130,60,70));
        chapter.fireMark=Piece(world,"Delayed fire burst",flame,0,-.91f,1.36f,.10f,17,C(255,81,34));
        chapter.fireShadow=Piece(world,"Fire warning · dark base",block,0,-.89f,1.36f,.13f,17,C(63,18,31));
        chapter.fireWarning=Piece(world,"Fire warning · bright line",block,0,-.88f,1.36f,.045f,18,C(255,203,126));
        chapter.fireMark.enabled=chapter.fireShadow.enabled=chapter.fireWarning.enabled=false;
        Actor(world,"Hero","pixel8/frames/idle/idle-01",(float)WrathRoute.Entrance/100,1.5f,12);
        Actor(world,"Guardian","guardian8/frames/idle/idle-01",38.3f,1.22f,10);
        chapter.SetInside(false);
        EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();EnsureScene();
        Debug.Log("WRATH_INDOOR_SCENE_CREATED: single room, entrance and sole general");
    }
    static Transform Group(Transform parent,string name)
    {
        var t=new GameObject(name).transform;t.SetParent(parent,false);return t;
    }
    static Transform Actor(Transform parent,string name,string path,float x,float size,int order)
    {
        var actor=Group(parent,name);
        var renderer=actor.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite=Resources.Load<Sprite>(path);renderer.sortingOrder=order;
        actor.localPosition=new Vector3(x,-.95f,0);actor.localScale=new Vector3(size,size,1);
        return actor;
    }
    static SpriteRenderer Piece(Transform parent,string name,Sprite sprite,float x,float y,float w,float h,int order,Color color)
    {
        var t=Group(parent,name);
        t.localPosition=new Vector3(x,y,0);
        t.localScale=new Vector3(w*sprite.pixelsPerUnit/sprite.rect.width,h*sprite.pixelsPerUnit/sprite.rect.height,1);
        var renderer=t.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite=sprite;renderer.color=color;renderer.sortingOrder=order;return renderer;
    }
}
