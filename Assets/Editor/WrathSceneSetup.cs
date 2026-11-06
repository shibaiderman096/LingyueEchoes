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
    static readonly string[] BackgroundNames={"Outskirts","Bridge","Street","Manor"};
    static Sprite block,moon,ridge,ruin,roof,flame;
    static Color C(int r,int g,int b,int a=255)=>new Color32((byte)r,(byte)g,(byte)b,(byte)a);

    [MenuItem("Lingyue/Create Wrath scene (if missing)")]
    public static void EnsureScene()
    {
        if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))CreateScene();
        var scenes=EditorBuildSettings.scenes;
        if(!scenes.Any(s=>s.path==ScenePath))
            EditorBuildSettings.scenes=scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
        else if(scenes.Any(s=>s.path==ScenePath&&!s.enabled))
            EditorBuildSettings.scenes=scenes.Select(s=>s.path==ScenePath?new EditorBuildSettingsScene(ScenePath,true):s).ToArray();
    }
    static void CreateScene()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        LoadPalette();
        var camera=new GameObject("Main Camera");camera.tag="MainCamera";
        var cam=camera.AddComponent<Camera>();
        cam.orthographic=true;cam.orthographicSize=3.6f;cam.transform.position=new Vector3(5.2f,0,-10);
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=C(38,24,37);
        camera.AddComponent<AudioListener>();
        var root=new GameObject("LingyueEchoes");
        root.AddComponent<LingyueGame>();
        var chapter=root.AddComponent<WrathChapter>();
        var world=chapter.world=Group(root.transform,"World");
        var sky=chapter.backdrop=Group(world,"Background · red moon and silhouettes");
        Piece(sky,"Cinder Sky",block,23,1,64,12,-15,C(59,33,48));
        Piece(sky,"Horizon haze",block,23,.25f,64,2.3f,-14,C(88,43,50));
        Piece(sky,"Upper smoke",ridge,22,2.58f,61,2.3f,-13,C(74,41,55));
        Piece(sky,"Eclipsed Moon",moon,7.3f,2.72f,1.65f,1.65f,-12,C(232,108,90));
        Piece(sky,"Eclipsed Moon over the manor",moon,37.4f,2.45f,1.45f,1.45f,-12,C(232,108,90));
        var rand=new System.Random(1906);
        float R(float a,float b)=>a+(b-a)*(float)rand.NextDouble();
        for(int i=0;i<15;i++){
            float x=-4+i*4;
            Piece(sky,"Distant ridgeline "+i,ridge,x,.06f,R(5.8f,8.4f),R(3.4f,5.2f),-11,C(76,49,65));
            Piece(sky,"Burnt skyline "+i,ruin,x+R(-1.2f,1.2f),-.13f,R(3,5.5f),R(2.7f,4.4f),-10,C(55,37,51));
        }
        var middle=Group(world,"Middle · gates, bridge arches and burnt streets");
        for(int i=0;i<13;i++){
            float x=-2+i*4.1f,h=R(1.8f,3.7f),w=R(2.3f,3.9f);
            if(x>=9&&x<15)continue;
            Piece(middle,"Roofless house "+i,ruin,x,-.90f+h/2,w,h,-7,C(44,29,42));
            if(i%3!=1)Piece(middle,"Broken roof "+i,roof,x+.2f,-.81f+h,w*1.12f,.65f,-6,C(64,37,45));
            if(i%2==0){
                Piece(middle,"Window "+i,block,x-.25f,.06f,.29f,.55f,-6,C(136,58,47));
                Piece(middle,"Window light "+i,block,x-.28f,.03f,.12f,.25f,-5,C(207,102,55));
            }
        }
        Piece(middle,"Ruin arch west",ruin,10.15f,-.02f,1.4f,2.45f,-7,C(50,35,48));
        Piece(middle,"Ruin arch east",ruin,14.13f,-.08f,1.3f,2.25f,-7,C(53,36,46));
        for(int i=0;i<3;i++){
            float x=34.1f+i*3.0f;
            Piece(middle,"Manor foundation "+i,block,x,.34f,2.8f,2.5f,-7,C(58,35,46));
            Piece(middle,"Manor roof "+i,roof,x,1.75f,3.5f,1.04f,-6,C(93,42,49));
            Piece(middle,"Manor pillar "+i,block,x-1.15f,.15f,.27f,2.05f,-5,C(114,55,56));
            Piece(middle,"Manor doorway "+i,block,x,-.01f,.76f,1.62f,-5,C(36,25,39));
        }
        var route=Group(world,"Route · safe walkway and stonework");
        Piece(route,"Continuous floor",block,21.5f,-2.43f,59,2.96f,0,C(48,38,48));
        Piece(route,"Walkable rim",block,21.5f,-1.005f,59,.12f,2,C(148,104,94));
        for(int i=0;i<61;i++){
            float x=-7+i*.96f;
            Piece(route,"Stone course "+i,block,x,-1.23f,R(.76f,.91f),R(.25f,.34f),1,i%3==1?C(98,76,79):C(81,64,71));
            if(i%2==0)Piece(route,"Lower masonry "+i,block,x+.47f,-1.76f,R(1.35f,1.7f),R(.29f,.45f),1,C(68,52,62));
        }
        var bridge=Group(world,"Bridge · simulation footholds");
        var terrain=WrathRoute.Bridge();
        chapter.bridgeStones=new SpriteRenderer[terrain.Length];
        for(int i=0;i<terrain.Length;i++){
            var slab=terrain[i];float x=(float)(slab.x+slab.w/2)/100,w=(float)slab.w/100,h=(float)slab.h/100;
            chapter.bridgeStones[i]=Piece(bridge,"Bridge stone "+(i+1),block,x,-.95f+h/2,w,h,4,C(107,82,85));
            Piece(bridge,"Bridge lip "+(i+1),block,x,-.95f+h-.035f,w,.07f,5,C(171,124,108));
            Piece(bridge,"Bridge split "+(i+1),block,x-.14f,-.95f+h/2,.065f,h*.65f,5,C(66,47,56));
        }
        var props=Group(world,"Props");
        for(int i=0;i<13;i++){
            float x=-.9f+i*3.63f;
            Piece(props,"Heat halo "+i,moon,x,-.53f,.84f,1.03f,3,C(195,58,38,44));
            Piece(props,"Low fire "+i,flame,x,-.61f,R(.34f,.47f),R(.59f,.81f),4,C(210,84,44));
            Piece(props,"Hot core "+i,flame,x+.04f,-.73f,.19f,.38f,5,C(255,159,74));
        }
        chapter.firstLamp=Lantern(props,"Checkpoint A · outskirts",WrathRoute.FirstLamp);
        chapter.secondLamp=Lantern(props,"Checkpoint C · street",WrathRoute.SecondLamp);
        chapter.leftSeal=PixelWorld.Sprite(props,"Arena seal left","prologue_route/moon-boundary",0,-.95f*100,55,160,7,new Vector2(.5f,0));
        chapter.rightSeal=PixelWorld.Sprite(props,"Arena seal right","prologue_route/moon-boundary",0,-.95f*100,55,160,7,new Vector2(.5f,0));
        chapter.leftSeal.color=chapter.rightSeal.color=C(253,119,76);
        chapter.leftSeal.enabled=chapter.rightSeal.enabled=false;
        chapter.returnSeal=PixelWorld.Sprite(props,"Shrine return","prologue_route/moon-boundary",(float)WrathRoute.Portal,-95,81,178,7,new Vector2(.5f,0));
        chapter.returnSeal.color=C(237,197,141);chapter.returnSeal.enabled=false;
        chapter.embers=Group(world,"Floating ash · pooled sprites");
        for(int i=0;i<44;i++)
            Piece(chapter.embers,"Ember "+i,block,R(-2,12),R(-1,3),i%6==0?.052f:.028f,i%4==0?.042f:.021f,18,C(253,130,60,80));
        chapter.fireMark=Piece(world,"Delayed fire telegraph",flame,0,-.91f,1.36f,.10f,17,C(255,81,34));
        chapter.fireMark.enabled=false;

        Actor(world,"Hero","pixel8/frames/idle/idle-01",.85f,1.5f,12);
        var guardian=Actor(world,"Guardian","guardian8/frames/idle/idle-01",38.3f,1.22f,10);
        var placeholder=Group(guardian,"General armor · placeholder");
        Piece(placeholder,"Red cuirass",block,0,.65f,.29f,.37f,9,C(107,36,43,170));
        Piece(placeholder,"Crimson cape",flame,-.35f,.5f,.49f,.93f,9,C(129,34,40,197));
        Piece(placeholder,"Polearm shaft",block,.41f,.58f,.05f,1.37f,11,C(113,74,67)).transform.localRotation=Quaternion.Euler(0,0,-17);
        Piece(placeholder,"Polearm blade",flame,.59f,1.2f,.22f,.38f,11,C(190,153,139));
        chapter.weaponGlow=Piece(placeholder,"Enraged blade",flame,.61f,1.25f,.35f,.64f,13,C(255,107,42));
        chapter.armorGlow=Piece(placeholder,"Enraged armor crack",block,-.02f,.7f,.039f,.24f,13,C(255,107,42));
        chapter.armorGlow.transform.localRotation=Quaternion.Euler(0,0,-30);
        chapter.weaponGlow.enabled=chapter.armorGlow.enabled=false;
        chapter.generalArmor=placeholder.gameObject;

        if(AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath+"Outskirts.png"))InstallArt(chapter);
        EditorSceneManager.SaveScene(scene,ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("WRATH_SCENE_CREATED: "+ScenePath);
    }
    public static void ApplyArt(){
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var chapter=scene.GetRootGameObjects().Select(go=>go.GetComponent<WrathChapter>()).First(c=>c);
        LoadPalette();
        InstallArt(chapter);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("WRATH_ART_SCENE_SAVED");
    }
    static void InstallArt(WrathChapter chapter){
        var old=chapter.world.Find("Chapter art · backgrounds");
        if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
        var painted=Group(chapter.world,"Chapter art · backgrounds");
        chapter.scenery=new SpriteRenderer[BackgroundNames.Length];
        for(int i=0;i<BackgroundNames.Length;i++){
            string name=BackgroundNames[i];
            var plate=LoadArt(name);
            var renderer=Piece(painted,WrathRoute.Areas[i],plate,5.2f,.12f,16.2f,9.1f,-20+i,C(224,214,227));
            renderer.enabled=i==0;
            chapter.scenery[i]=renderer;
        }
        chapter.backdrop.gameObject.SetActive(false);
        chapter.world.Find("Middle · gates, bridge arches and burnt streets").gameObject.SetActive(false);
        var road=LoadArt("Causeway");
        old=chapter.world.Find("Chapter art · causeway");
        if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
        var walkway=Group(chapter.world,"Chapter art · causeway");
        const float segment=9.60f,height=2.88f;
        for(int i=0;i<7;i++){
            var tile=Piece(walkway,$"Causeway {i+1}",road,-6.8f+(i+.5f)*segment,-.95f-height*.5f,segment+.01f,height,2,Color.white);
            tile.flipX=i%2==1;
        }
        chapter.world.Find("Route · safe walkway and stonework").gameObject.SetActive(false);
        var bridge=chapter.world.Find("Bridge · simulation footholds");
        for(int i=0;i<chapter.bridgeStones.Length;i++){
            var slab=chapter.bridgeStones[i];
            var expected=WrathRoute.Bridge()[i];
            slab.sprite=LoadArt("Foothold"+(i+1));
            slab.transform.localScale=new Vector3((float)expected.w/100*slab.sprite.pixelsPerUnit/slab.sprite.rect.width,
                (float)expected.h/100*slab.sprite.pixelsPerUnit/slab.sprite.rect.height,1);
            slab.color=Color.white;
            bridge.Find("Bridge lip "+(i+1)).gameObject.SetActive(false);
            bridge.Find("Bridge split "+(i+1)).gameObject.SetActive(false);
        }
        foreach(var prop in chapter.world.Find("Props").GetComponentsInChildren<SpriteRenderer>(true))
            if(prop.name.StartsWith("Heat halo ")||prop.name.StartsWith("Low fire ")||prop.name.StartsWith("Hot core "))prop.gameObject.SetActive(false);
        old=chapter.world.Find("Delayed fire · contrast mark");
        if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
        var warning=Group(chapter.world,"Delayed fire · contrast mark");
        chapter.fireShadow=Piece(warning,"Fire warning · dark base",block,0,-.89f,1.36f,.13f,17,C(63,18,31));
        chapter.fireWarning=Piece(warning,"Fire warning · bright line",block,0,-.88f,1.36f,.045f,18,C(255,203,126));
        chapter.fireShadow.enabled=chapter.fireWarning.enabled=false;
    }
    static Sprite LoadArt(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath+name+".png")??throw new InvalidOperationException("Missing chapter sprite: "+name);
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
    static SpriteRenderer Lantern(Transform parent,string name,double x)
        =>PixelWorld.Sprite(parent,name,"prologue_route/moon_lantern",(float)x,-95,64,128,7,new Vector2(.5f,0));
    static SpriteRenderer Piece(Transform parent,string name,Sprite sprite,float x,float y,float w,float h,int order,Color color)
    {
        var t=Group(parent,name);
        t.localPosition=new Vector3(x,y,0);
        t.localScale=new Vector3(w*sprite.pixelsPerUnit/sprite.rect.width,h*sprite.pixelsPerUnit/sprite.rect.height,1);
        var renderer=t.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite=sprite;renderer.color=color;renderer.sortingOrder=order;return renderer;
    }
    static void LoadPalette()
    {
        if(!AssetDatabase.LoadMainAssetAtPath(PalettePath)){
            var palette=new Texture2D(1,1,TextureFormat.RGBA32,false){name="Wrath placeholder palette"};
            palette.SetPixel(0,0,Color.white);palette.Apply();
            AssetDatabase.CreateAsset(palette,PalettePath);
            AddShape("Block",8,8,(x,y)=>1);
            AddShape("Moon",48,48,(x,y)=>Math.Sqrt(x*x+y*y)<.94&&Math.Sqrt((x-.28)*(x-.28)+(y-.20)*(y-.20))>.63?1:0);
            AddShape("Ridge",72,36,(x,y)=>y<-.37+.60*Math.Abs(Math.Sin(x*3.1+.7))+.22*Math.Abs(Math.Sin(x*8.3))?1:0);
            AddShape("Ruin",56,56,(x,y)=>Math.Abs(x)<.83&&y<.51+.26*Math.Sin(Math.Floor((x+1)*8)*2.31)&&
                !(y>.12&&x>.12&&x<.34)&&!(y<-.42&&Math.Abs(x+.24)<.12)?1:0);
            AddShape("Roof",64,24,(x,y)=>y<.81-1.62*Math.Abs(x)&&y>-.79+.18*Math.Pow(Math.Abs(x),8)?1:0);
            AddShape("Flame",36,48,(x,y)=>{
                double mid=.20*Math.Sin((y+1)*3.5),width=(1-y)*.45;
                return y>-.98&&Math.Abs(x-mid)<width&&!(y>.08&&x<mid-.1&&Math.Sin(y*13)>.30)?1:0;
            });
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PalettePath);
        }
        var sprites=AssetDatabase.LoadAllAssetsAtPath(PalettePath).OfType<Sprite>().ToDictionary(s=>s.name);
        block=sprites["Block"];moon=sprites["Moon"];ridge=sprites["Ridge"];ruin=sprites["Ruin"];roof=sprites["Roof"];flame=sprites["Flame"];
    }
    static void AddShape(string name,int w,int h,Func<double,double,int> sample)
    {
        var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){name=name+" pixels",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color32[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)pixels[y*w+x]=new Color32(255,255,255,(byte)(255*sample(2.0*x/w-1,2.0*y/h-1)));
        texture.SetPixels32(pixels);texture.Apply();AssetDatabase.AddObjectToAsset(texture,PalettePath);
        var sprite=Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
        sprite.name=name;AssetDatabase.AddObjectToAsset(sprite,PalettePath);
    }
}
