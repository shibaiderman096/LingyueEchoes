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
    // Road-end posts support the separate lintel and eaves; keep the near capital visible.
    const float ExteriorPillarHeight=3f,ExteriorNearPillarHeight=4.03f,ExteriorPillarWidth=.445f;
    const float InteriorWidth=20f,InteriorCenterX=37.1f,FloorY=-.95f;
    const float EntranceDoorX=34.31f,EntranceDoorY=.9875f,EntranceDoorWidth=2.48f,EntranceDoorHeight=4f;
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
    [MenuItem("Lingyue/Apply exterior arrival anchor")]
    public static void ApplyExteriorArrival(){
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before editing the exterior.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!=ScenePath)throw new InvalidOperationException("Open Chapter01_Wrath before applying the arrival anchor.");
        var chapter=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<WrathChapter>(true)).Single();
        var camera=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Camera>(true)).Single(c=>c.CompareTag("MainCamera"));
        Undo.RecordObject(chapter,"Set exterior arrival anchor");
        if(!chapter.exteriorArrivalAnchor){
            var anchor=new GameObject("Exterior arrival anchor");
            Undo.RegisterCreatedObjectUndo(anchor,"Create exterior arrival anchor");
            anchor.transform.SetParent(chapter.world,false);
            anchor.transform.position=new Vector3(camera.transform.position.x-camera.orthographicSize*camera.aspect+.35f,FloorY,0);
            chapter.exteriorArrivalAnchor=anchor.transform;
        }
        var far=chapter.exteriorPillars[0];
        Undo.RecordObject(far,"Place far pillar behind the door");
        far.sortingOrder=chapter.entranceDoor.sortingOrder-(chapter.exteriorDoorDarkness?2:1);
        var hero=chapter.world.Find("Hero");
        if(hero){Undo.RecordObject(hero,"Preview exterior arrival");hero.position=chapter.exteriorArrivalAnchor.position;}
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("WRATH_EXTERIOR_ARRIVAL_APPLIED: current layout preserved, left arrival anchor and far pillar occlusion saved");
    }
    const string DoorwayMeshPath=ArtPath+"ExteriorDoorwayDarkness.asset";
    const string DoorwayMaterialPath=ArtPath+"ExteriorDoorwayBlack.mat";
    [MenuItem("Lingyue/Apply exterior doorway and checkpoint")]
    public static void ApplyExteriorDoorwayAndCheckpoint(){
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode before editing the exterior.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!=ScenePath)throw new InvalidOperationException("Open Chapter01_Wrath before applying the doorway and checkpoint.");
        var chapter=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<WrathChapter>(true)).Single();
        var door=chapter.entranceDoor;
        if(!door||!door.sprite||!chapter.exteriorArrivalAnchor)throw new InvalidOperationException("The existing door and exterior arrival anchor are required.");
        var lampSprite=Resources.Load<Sprite>("prologue_route/moon_lantern");
        var shader=Shader.Find("Sprites/Default")??Shader.Find("Unlit/Color");
        if(!lampSprite||!shader)throw new InvalidOperationException("Missing moon lantern sprite or unlit shader.");
        Undo.RecordObject(chapter,"Set exterior doorway and checkpoint references");
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(DoorwayMeshPath);
        if(!mesh){mesh=new Mesh{name="Exterior doorway darkness"};AssetDatabase.CreateAsset(mesh,DoorwayMeshPath);}
        Undo.RecordObject(mesh,"Fit doorway darkness polygon");
        var sprite=chapter.entranceDoorFrames!=null&&chapter.entranceDoorFrames.Length>0&&chapter.entranceDoorFrames[0]?chapter.entranceDoorFrames[0]:door.sprite;
        // Source pixels are measured from top left. Two pixel overlap seals the transparent inner edge.
        var pixels=new[]{new Vector2(78,74),new Vector2(231,86),new Vector2(231,299),new Vector2(78,311)};
        var vertices=pixels.Select(pixel=>{
            float x=(pixel.x/320f*sprite.rect.width-sprite.pivot.x)/sprite.pixelsPerUnit;
            float y=((1-pixel.y/320f)*sprite.rect.height-sprite.pivot.y)/sprite.pixelsPerUnit;
            return new Vector3(door.flipX?-x:x,door.flipY?-y:y,0);
        }).ToArray();
        mesh.Clear();mesh.vertices=vertices;
        mesh.triangles=new[]{0,1,2,0,2,3,0,2,1,0,3,2};
        mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        var material=AssetDatabase.LoadAssetAtPath<Material>(DoorwayMaterialPath);
        if(!material){material=new Material(shader){name="Exterior doorway black"};AssetDatabase.CreateAsset(material,DoorwayMaterialPath);}
        Undo.RecordObject(material,"Set doorway black material");material.shader=shader;material.color=Color.black;material.renderQueue=3000;EditorUtility.SetDirty(material);
        var darkness=chapter.exteriorDoorDarkness;
        if(!darkness){
            var existing=door.transform.Find("General hall doorway darkness");
            var obj=existing?existing.gameObject:new GameObject("General hall doorway darkness");
            if(!existing)Undo.RegisterCreatedObjectUndo(obj,"Create doorway darkness");
            darkness=obj.GetComponent<MeshRenderer>();if(!darkness)darkness=Undo.AddComponent<MeshRenderer>(obj);
            chapter.exteriorDoorDarkness=darkness;
        }
        Undo.RecordObject(darkness.transform,"Fit doorway darkness transform");
        Undo.SetTransformParent(darkness.transform,door.transform,"Parent doorway darkness");
        darkness.transform.localPosition=Vector3.zero;darkness.transform.localRotation=Quaternion.identity;darkness.transform.localScale=Vector3.one;
        var filter=darkness.GetComponent<MeshFilter>();if(!filter)filter=Undo.AddComponent<MeshFilter>(darkness.gameObject);
        Undo.RecordObject(filter,"Set doorway mesh");filter.sharedMesh=mesh;
        Undo.RecordObject(darkness,"Set doorway renderer");darkness.sharedMaterial=material;
        darkness.sortingLayerID=door.sortingLayerID;darkness.sortingOrder=door.sortingOrder-1;
        darkness.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;darkness.receiveShadows=false;darkness.enabled=door.enabled;
        if(chapter.exteriorPillars!=null&&chapter.exteriorPillars.Length>0&&chapter.exteriorPillars[0]){
            var far=chapter.exteriorPillars[0];Undo.RecordObject(far,"Set far pillar occlusion");far.sortingLayerID=door.sortingLayerID;far.sortingOrder=door.sortingOrder-2;
        }
        var lamp=chapter.exteriorCheckpointLamp;
        if(!lamp){
            var existing=chapter.world.Find("Exterior moon lantern checkpoint");
            var obj=existing?existing.gameObject:new GameObject("Exterior moon lantern checkpoint");
            if(!existing)Undo.RegisterCreatedObjectUndo(obj,"Create exterior checkpoint lamp");
            lamp=obj.GetComponent<SpriteRenderer>();if(!lamp)lamp=Undo.AddComponent<SpriteRenderer>(obj);
            chapter.exteriorCheckpointLamp=lamp;
        }
        Undo.RecordObject(lamp,"Set exterior checkpoint lamp");Undo.RecordObject(lamp.transform,"Place exterior checkpoint lamp");
        Undo.SetTransformParent(lamp.transform,chapter.world,"Parent checkpoint lamp");
        lamp.sprite=lampSprite;lamp.color=Color.white;lamp.sortingLayerID=door.sortingLayerID;lamp.sortingOrder=12;
        lamp.transform.rotation=Quaternion.identity;
        lamp.transform.localScale=new Vector3(.55f/lampSprite.bounds.size.x,1.1f/lampSprite.bounds.size.y,1);
        lamp.transform.position=chapter.exteriorArrivalAnchor.position+new Vector3(.32f,.55f,0);
        lamp.enabled=door.enabled;
        EditorUtility.SetDirty(chapter);AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("WRATH_EXTERIOR_DOORWAY_CHECKPOINT_APPLIED: existing layout preserved; "+DoorwayMeshPath+"; "+DoorwayMaterialPath);
    }
    [MenuItem("Lingyue/Rebuild indoor general arena")]
    public static void RebuildIndoor(){
        var interior=LoadArt("Interior");
        var exterior=LoadArt("Exterior");
        var pillar=LoadArt("Pillar");
        var canopy=LoadArt("GateCanopy");
        var doors=Enumerable.Range(1,16).Select(i=>LoadArt("DoorOpening/door-"+i.ToString("00"))).ToArray();
        var shapes=AssetDatabase.LoadAllAssetsAtPath(PalettePath).OfType<Sprite>().ToDictionary(x=>x.name);
        var block=shapes["Block"];var flame=shapes["Flame"];
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Main Camera");camera.tag="MainCamera";
        var cam=camera.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=3.6f/1.5f;
        cam.transform.position=new Vector3(29.5f,.65f,-10);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=C(25,19,28);
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
        float pillarBottomGutter=8f*ExteriorPillarHeight/pillar.rect.height;
        chapter.exteriorPillars=new[]{
            Piece(room,"General hall exterior far pillar",pillar,33.7f,-.65f+ExteriorPillarHeight*.5f-pillarBottomGutter,ExteriorPillarWidth,ExteriorPillarHeight,7,Color.white),
            Piece(room,"General hall exterior near pillar",pillar,34.9f,FloorY+ExteriorNearPillarHeight*(.5f-8f/pillar.rect.height),ExteriorPillarWidth,ExteriorNearPillarHeight,14,Color.white)
        };
        chapter.exteriorCanopy=Piece(room,"General hall exterior crossbeam and eaves",canopy,34.1409880f,2.2650413f,2.4474263f,1.3717742f,15,Color.white);
        chapter.bridgeStones=Array.Empty<SpriteRenderer>();
        var props=Group(world,"Props");
        chapter.entranceDoorFrames=doors;
        chapter.entranceDoor=Piece(props,"General hall · oblique entrance",doors[0],EntranceDoorX,EntranceDoorY,EntranceDoorWidth,EntranceDoorHeight,6,Color.white);
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
        EditorSceneManager.SaveScene(scene,ScenePath);ApplyExteriorArrival();ApplyExteriorDoorwayAndCheckpoint();AssetDatabase.SaveAssets();EnsureScene();
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
