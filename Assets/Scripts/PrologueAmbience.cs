using UnityEngine;

namespace LingyueEchoes
{
    // Decorative layers only: no colliders, combat timers, or gameplay state.
    public sealed class PrologueAmbience:MonoBehaviour
    {
        public Transform rearLayer,frontLayer;
        public SpriteRenderer[] lanterns;
        Vector3 rearOrigin,frontOrigin;
        Camera sceneCamera;
        float elapsed;
        void Start(){rearOrigin=rearLayer.localPosition;frontOrigin=frontLayer.localPosition;sceneCamera=Camera.main;}
        void LateUpdate(){
            if(!sceneCamera)return;
            float x=sceneCamera.transform.position.x;
            rearLayer.localPosition=rearOrigin+Vector3.right*(x*.025f);
            frontLayer.localPosition=frontOrigin-Vector3.right*(x*.035f);
            elapsed+=Mathf.Min(Time.unscaledDeltaTime,.1f);
            for(int i=0;i<lanterns.Length;i++){
                float light=.96f+.04f*Mathf.Sin(elapsed*1.7f+i*1.3f);
                lanterns[i].color=new Color(1,light,.90f+.10f*light);
            }
        }
        public static void Create(Transform scenery){
            var old=scenery.Find("Layered shrine decorations");if(old)return;
            var root=new GameObject("Layered shrine decorations");root.transform.SetParent(scenery,false);
            var ambience=root.AddComponent<PrologueAmbience>();
            ambience.rearLayer=Layer(root.transform,"Rear · low balustrades and blossoms");
            var middle=Layer(root.transform,"Roadside · lanterns and offerings");
            ambience.frontLayer=Layer(root.transform,"Front · moss and hanging blossom branches");
            foreach(float x in new[]{-1320f,-660f,1510f,1880f}){
                Prop(ambience.rearLayer,"Balustrade", "balustrade",x,-99,250,111,-7).color=new Color(.91f,.92f,.84f);
            }
            foreach(float x in new[]{-1775f,-1100f,-520f,190f,1080f,1650f,2460f}){
                var shrub=Prop(ambience.rearLayer,"Flowering shrub","blossom-shrub",x,-103,172,104,-6);
                shrub.flipX=x>0;shrub.color=new Color(.91f,.94f,.86f);
            }
            float[] lights={-1760,-1030,-120,1260,1810,2510};
            ambience.lanterns=new SpriteRenderer[lights.Length];
            for(int i=0;i<lights.Length;i++)ambience.lanterns[i]=Prop(middle,"Lantern "+(i+1),"sunset-lantern",lights[i],-99,73,111,-2);
            Prop(middle,"Incense by the waking tree","incense-altar",-1520,-99,73,101,-2);
            Prop(middle,"Offering beside the moon gate","incense-altar",1960,-99,73,101,-2);
            foreach(float x in new[]{-1825f,-970f,-430f,1430f,1900f,2520f})
                Prop(middle,"Mossy flower stones","flower-stones",x,-105,110,43,-1);
            foreach(float x in new[]{-1650f,-920f,-180f,1190f,1730f,2440f}){
                var stones=Prop(ambience.frontLayer,"Foreground moss","flower-stones",x,-116,75,29,15);
                stones.color=new Color(.79f,.83f,.70f);stones.flipX=x>0;
            }
            Prop(ambience.frontLayer,"Left canopy fringe","hanging-blossoms",-1830,205,182,119,14);
            var branch=Prop(ambience.frontLayer,"Right canopy fringe","hanging-blossoms",2540,260,152,100,14);branch.flipX=true;
        }
        static Transform Layer(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static SpriteRenderer Prop(Transform parent,string name,string sprite,float x,float y,float w,float h,int order)=>
            PixelWorld.Sprite(parent,name,"prologue_route/warm/decor/"+sprite,x,y,w,h,order,new Vector2(.5f,0));
    }
}
