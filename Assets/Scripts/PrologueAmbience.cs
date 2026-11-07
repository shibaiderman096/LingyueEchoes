using UnityEngine;

namespace LingyueEchoes
{
    // Decorative layers only: no colliders, combat timers, or gameplay state.
    public sealed class PrologueAmbience:MonoBehaviour
    {
        // Embed the sparse leaf tips slightly so the body of each rock meets the paving.
        const float StoneBase=-97;
        public Transform rearLayer,frontLayer;
        public SpriteRenderer[] lanterns;
        public ParticleSystem[] petals;
        float elapsed;
        void OnEnable(){SceneParticleSettings.Changed+=SetParticles;SetParticles(SceneParticleSettings.Enabled);}
        void OnDisable(){SceneParticleSettings.Changed-=SetParticles;SetParticles(false);}
        void SetParticles(bool enabled){
            if(petals==null)return;
            foreach(var system in petals){
                if(!system)continue;
                if(enabled){if(!system.isPlaying)system.Play();}
                else system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        void LateUpdate(){
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
                Prop(middle,"Mossy flower stones","flower-stones",x,StoneBase,110,43,-1);
            foreach(float x in new[]{-1650f,-920f,-180f,1190f,1730f,2440f}){
                var stones=Prop(ambience.frontLayer,"Foreground moss","flower-stones",x,StoneBase,75,29,15);
                stones.color=new Color(.79f,.83f,.70f);stones.flipX=x>0;
            }
            Prop(ambience.frontLayer,"Left canopy fringe","hanging-blossoms",-1830,205,182,119,14);
            var branch=Prop(ambience.frontLayer,"Right canopy fringe","hanging-blossoms",2540,260,152,100,14);branch.flipX=true;
            ambience.InstallPetals();
        }
        public void GroundStones(){
            foreach(var stone in GetComponentsInChildren<SpriteRenderer>(true)){
                if(stone.name!="Foreground moss"&&stone.name!="Mossy flower stones")continue;
                var position=stone.transform.position;
                position.y+=StoneBase/100-stone.bounds.min.y;
                stone.transform.position=position;
            }
        }
        public void InstallPetals(){
            petals=new ParticleSystem[2];
            for(int i=0;i<petals.Length;i++){
                string name=i==0?"Cherry petals · distant":"Cherry petals · near";
                var child=transform.Find(name);
                if(!child){var go=new GameObject(name);go.transform.SetParent(transform,false);child=go.transform;}
                child.localPosition=new Vector3((PrologueLayout.BackdropLeft+PrologueLayout.BackdropRight)/200f,2,0);
                var system=child.GetComponent<ParticleSystem>();if(!system)system=child.gameObject.AddComponent<ParticleSystem>();
                system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=system.main;main.duration=12;main.loop=true;main.prewarm=true;main.playOnAwake=false;
                main.useUnscaledTime=true;main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.startLifetime=new ParticleSystem.MinMaxCurve(7,11);main.startSpeed=0;
                main.startSize=new ParticleSystem.MinMaxCurve(i==0?.045f:.07f,i==0?.075f:.12f);
                main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.maxParticles=i==0?104:48;
                main.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.68f,.73f,.65f),new Color(1,.88f,.84f,.9f));
                var emission=system.emission;emission.rateOverTime=i==0?9:4;
                var shape=system.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;
                shape.scale=new Vector3((PrologueLayout.BackdropRight-PrologueLayout.BackdropLeft)/100f,4,.01f);
                var velocity=system.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
                velocity.x=new ParticleSystem.MinMaxCurve(.05f,.13f);velocity.y=new ParticleSystem.MinMaxCurve(-.43f,-.24f);
                velocity.z=new ParticleSystem.MinMaxCurve(0,0);
                var rotation=system.rotationOverLifetime;rotation.enabled=true;rotation.separateAxes=true;
                rotation.x=new ParticleSystem.MinMaxCurve(-.6f,.6f);rotation.y=new ParticleSystem.MinMaxCurve(-1.1f,1.1f);
                rotation.z=new ParticleSystem.MinMaxCurve(-1.2f,1.2f);
                var noise=system.noise;noise.enabled=true;noise.separateAxes=true;
                noise.strengthX=.16f;noise.strengthY=.055f;noise.strengthZ=0;
                noise.frequency=.45f;noise.scrollSpeed=.18f;noise.quality=ParticleSystemNoiseQuality.Low;noise.octaveCount=1;
                var fade=system.colorOverLifetime;fade.enabled=true;
                var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(1,.65f),new GradientAlphaKey(0,1)});
                fade.color=gradient;
                var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Resources.Load<Material>("prologue_route/warm/cherry-petal");
                renderer.sortingOrder=i==0?-2:16;renderer.renderMode=ParticleSystemRenderMode.Billboard;
                petals[i]=system;
            }
        }
        static Transform Layer(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static SpriteRenderer Prop(Transform parent,string name,string sprite,float x,float y,float w,float h,int order)=>
            PixelWorld.Sprite(parent,name,"prologue_route/warm/decor/"+sprite,x,y,w,h,order,new Vector2(.5f,0));
    }
}
