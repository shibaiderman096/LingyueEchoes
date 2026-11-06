using UnityEngine;

namespace LingyueEchoes
{
    public partial class LingyueGame
    {
        const string TailArchiveTitle="九尾残响";
        const float TailGather=.45f,TailReveal=1.35f,TailFlight=2.15f,TailArrival=2.95f,TailEnd=3.5f;
        const float MusicVolume=.22f;
        static readonly Color32 MoonIvory=new Color32(244,238,227,255);
        static readonly Color32 MoonGold=new Color32(217,188,134,255);
        static readonly Color32 MoonFire=new Color32(203,231,232,255);
        static readonly Color32 MoonNight=new Color32(14,21,34,255);
        static readonly Rect TailCanvas=new Rect(0,0,1280,720);

        readonly struct TailReward
        {
            public readonly int Order;
            public readonly string Id,Name,Quote,SpritePath,Epilogue;
            public readonly Color32 Theme,Fire;
            public string DisplayName=>$"{(Order==1?"初尾":Order==2?"贰尾":$"第 {Order} 尾")} · {Name}";
            public TailReward(int order,string id,string name,string quote=null,string spritePath=null,string epilogue=null,Color32? theme=null,Color32? fire=null)
            {
                Order=order;Id=id;Name=name;Quote=quote;SpritePath=spritePath;Epilogue=epilogue;Theme=theme??MoonGold;Fire=fire??MoonFire;
            }
            public bool IsObtained(int count)=>count>=Order;
        }
        static readonly TailReward[] TailRewards={
            new TailReward(1,"shoudeng","守灯","「长夜未尽，仍有人为你守着一盏灯。」","prologue_route/first-fox-tail","tailAfter"),
            new TailReward(2,"fenxin","焚心","「怒火能焚尽仇敌，也能焚尽归途。」","prologue_route/first-fox-tail","wrathEpilogue",
                new Color32(246,168,97,255),new Color32(255,201,158,255)),new TailReward(3,"qiujin","囚金"),
            new TailReward(4,"shiyue","噬月"),new TailReward(5,"yongmian","永眠"),
            new TailReward(6,"jingying","镜影"),new TailReward(7,"qimeng","绮梦"),
            new TailReward(8,"lingxiao","凌霄"),new TailReward(9,"guixin","归心")
        };
        static string TailProgress(int count)=>$"九尾归一 · {count} / {TailRewards.Length}";

        AudioSource tailAudio;
        AudioClip[] tailClips;
        Texture2D tailGlow,tailVignette;
        Texture2D[] tailIcons;
        TailReward currentTail;
        int tailBeat;
        float tailCameraStart,tailZoomStart,tailCameraYStart;
        double tailFocusX;

        static float Ramp(float time,float from,float to)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,time));
        static Color WithAlpha(Color tint,float alpha){tint.a=alpha;return tint;}
        static void DrawTintedTexture(Rect rect,Texture texture,Color tint,ScaleMode mode=ScaleMode.StretchToFill)
        {
            var previous=GUI.color;
            GUI.color=tint;
            GUI.DrawTexture(rect,texture,mode);
            GUI.color=previous;
        }
        void BeginTailHandoff(string id)
        {
            currentTail=System.Array.Find(TailRewards,reward=>reward.Id==id&&reward.Epilogue!=null);
            if(currentTail.Order==0)throw new System.ArgumentException("Unknown tail reward: "+id);
            ClearInput();
            tailCameraStart=cameraX;
            tailZoomStart=3.6f/view.orthographicSize;
            tailCameraYStart=view.transform.position.y*100;
            tailFocusX=(sim.player.x+sim.boss.x)*.5;
            tailBeat=0;
            if(tailClips==null)
                tailClips=new[]{
                    Resources.Load<AudioClip>("audio/tail_ritual/moonlight"),
                    Resources.Load<AudioClip>("audio/tail_ritual/gather"),
                    Resources.Load<AudioClip>("audio/tail_ritual/reveal"),
                    Resources.Load<AudioClip>("audio/tail_ritual/return")
                };
            if(!tailGlow)tailGlow=TailGradient(false);
            if(!tailVignette)tailVignette=TailGradient(true);
            tailAudio.Stop();
            tailAudio.mute=muted;
            SetStage("FIRST_TAIL");
            PlayTailClip(0,.75f);
        }
        void PlayTailClip(int index,float volume)
        {
            if(tailClips[index])tailAudio.PlayOneShot(tailClips[index],volume);
        }
        void UpdateTailRitual()
        {
            // This stage uses unscaled presentation time and never advances the combat clock.
            if(tailBeat==0&&stageTime>=TailGather){tailBeat=1;PlayTailClip(1,.54f);}
            if(tailBeat==1&&stageTime>=TailReveal){tailBeat=2;PlayTailClip(2,.46f);}
            if(tailBeat==2&&stageTime>=TailArrival){
                tailBeat=3;
                sim.ApplyTailUpgrades(Mathf.Max(currentTail.Order,sim.TailUpgrades));
                PlayTailClip(3,.58f);
            }
            float duck=Ramp(stageTime,0,TailGather)*(1-Ramp(stageTime,TailArrival,TailEnd));
            music.volume=MusicVolume*(1-.82f*duck);
            if(stageTime>=TailEnd){
                SetStage("TAIL_EPILOGUE");
                if(wrath)ChapterSay(currentTail.Epilogue);else Say(currentTail.Epilogue);
            }
        }
        void TailCamera(out float zoom,out float y)
        {
            float enter=Ramp(stageTime,0,TailGather),exit=Ramp(stageTime,TailArrival,TailEnd);
            float tremor=(1-Ramp(stageTime,TailArrival,TailArrival+.24f))*Ramp(stageTime,TailArrival,TailArrival+.045f);
            float shake=Mathf.Sin((stageTime-TailArrival)*49)*tremor*2;
            cameraX=Mathf.Lerp(Mathf.Lerp(tailCameraStart,(float)tailFocusX,enter),RouteCameraX(),exit)+shake;
            zoom=Mathf.Lerp(Mathf.Lerp(tailZoomStart,1.2f,enter),1,exit);
            y=Mathf.Lerp(Mathf.Lerp(tailCameraYStart,21,enter),0,exit)+shake*.42f;
        }
        static Texture2D TailGradient(bool vignette)
        {
            const int size=96;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[size*size];
            for(int i=0;i<pixels.Length;i++){
                float x=(i%size)*2f/(size-1)-1,y=(i/size)*2f/(size-1)-1;
                float radius=Mathf.Sqrt(x*x+y*y);
                float alpha=vignette?Ramp(radius,.31f,1.21f):Mathf.Pow(Mathf.Clamp01(1-radius),2.3f);
                pixels[i]=new Color32(255,255,255,(byte)Mathf.RoundToInt(255*alpha));
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);
            return texture;
        }
        void MoonGlow(Vector2 center,float width,float height,Color tint,float alpha)
        {
            if(alpha<=.001f)return;
            DrawTintedTexture(new Rect(center.x-width/2,center.y-height/2,width,height),tailGlow,WithAlpha(tint,alpha));
        }
        static Vector2 TailArc(Vector2 from,Vector2 to,float travel)
        {
            return Vector2.Lerp(from,to,travel)+Vector2.down*(47*4*travel*(1-travel));
        }
        void TailScreenMargins(Color tint)
        {
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            if(scale<=0)return;
            float x=(Screen.width/scale-1280)*.5f,y=(Screen.height/scale-720)*.5f;
            // The camera only owns the 16:9 viewport; the presentation also covers its unused edges.
            if(x>.1f){
                Fill(new Rect(-x-1,0,x+1.5f,720),tint);
                Fill(new Rect(1279.5f,0,x+1.5f,720),tint);
            }
            if(y>.1f){
                Fill(new Rect(0,-y-1,1280,y+1.5f),tint);
                Fill(new Rect(0,719.5f,1280,y+1.5f),tint);
            }
        }
        void TailRitualGUI()
        {
            if(Event.current.type!=EventType.Repaint)return;
            float t=stageTime;
            float exit=Ramp(t,TailArrival,TailEnd);
            float settle=Ramp(t,0,TailGather),envelope=settle*(1-exit);
            var reward=currentTail;
            var icon=tailIcons[reward.Order-1];
            TailScreenMargins(WithAlpha(MoonNight,settle));
            Fill(TailCanvas,WithAlpha(MoonNight,.19f*envelope));
            DrawTintedTexture(TailCanvas,tailVignette,WithAlpha(MoonNight,.89f*envelope));

            var source=StoryPoint(sim.boss,75);
            var target=StoryPoint(sim.player.x-sim.player.face*28,(float)sim.player.y+62);
            var from=StoryPoint(tailFocusX,104)+new Vector2(0,Mathf.Sin(t*3.7f)*4);
            float gathered=Ramp(t,TailGather,TailReveal);
            float travel=Ramp(t,TailFlight,TailArrival);
            var tail=TailArc(from,target,travel);

            for(int i=0;i<10;i++){
                float phase=Mathf.Repeat(t*1.03f+i*.173f,1);
                var point=source+new Vector2(Mathf.Sin(i*2.39f+phase*3)*27,-phase*(38+i*3));
                float alpha=Mathf.Sin(phase*Mathf.PI)*envelope*(1-Ramp(t,TailGather,TailFlight));
                MoonGlow(point,10+i%3*5,15+i%3*8,reward.Fire,.83f*alpha);
            }
            for(int i=0;i<19;i++){
                float angle=i*2.39996f+t*.63f;
                float orbit=(1-gathered)*(47+i%5*17)+(20+i%4*6);
                var point=from+new Vector2(Mathf.Cos(angle)*orbit,Mathf.Sin(angle)*orbit*.64f);
                float spark=.44f+.56f*Mathf.Pow(.5f+.5f*Mathf.Sin(t*7+i*2.7f),2);
                float alpha=Ramp(t,.32f+i%4*.06f,.66f+i%4*.06f)*(1-travel)*envelope*spark;
                MoonGlow(point,9+i%4*3,9+i%4*3,i%3==0?reward.Fire:reward.Theme,.92f*alpha);
            }

            if(t>=TailFlight&&t<TailArrival+.18f){
                for(int i=8;i>=1;i--){
                    float trailTime=t-i*.026f;
                    if(trailTime<TailFlight||trailTime>=TailArrival)continue;
                    var trace=TailArc(from,target,Ramp(trailTime,TailFlight,TailArrival));
                    float alpha=(1-i/9f)*(1-Ramp(t,TailArrival,TailArrival+.18f));
                    MoonGlow(trace,21+i*2,13+i*2,reward.Theme,alpha*.68f);
                }
            }
            float tailAlpha=gathered*(1-Ramp(t,TailArrival-.055f,TailArrival+.18f));
            float breath=1+.04f*Mathf.Sin(t*7);
            float size=Mathf.Lerp(68,126,gathered)*breath*Mathf.Lerp(1,.53f,travel);
            MoonGlow(tail,size*2.2f,size*1.68f,reward.Theme,tailAlpha*.70f);
            if(icon&&tailAlpha>0)
                DrawTintedTexture(new Rect(tail.x-size/2,tail.y-size/2,size,size),icon,WithAlpha(MoonIvory,tailAlpha),ScaleMode.ScaleToFit);

            float pulse=Ramp(t,TailArrival-.035f,TailArrival+.045f)*(1-Ramp(t,TailArrival+.045f,TailArrival+.36f));
            MoonGlow(target,280,245,reward.Fire,pulse*.80f);
            MoonGlow(target,97,120,MoonIvory,pulse*.95f);
            if(icon&&pulse>0)
                DrawTintedTexture(new Rect(target.x-36,target.y-36,72,72),icon,WithAlpha(MoonIvory,pulse*.82f),ScaleMode.ScaleToFit);
            Fill(TailCanvas,WithAlpha(MoonIvory,pulse*.10f));
            for(int i=0;i<12;i++){
                float age=Ramp(t,TailArrival,TailEnd);
                float angle=i*2.39996f;
                var point=target+new Vector2(Mathf.Cos(angle)*(25+age*95),Mathf.Sin(angle)*(18+age*61)-age*19);
                MoonGlow(point,12,12,i%2==0?reward.Fire:reward.Theme,settle*Ramp(t,TailArrival,TailArrival+.07f)*(1-age)*.95f);
            }

            float textIn=Ramp(t,TailReveal,TailReveal+.30f)*(1-exit);
            MoonGlow(new Vector2(640,190),1160,575,MoonNight,textIn*.94f);
            Fill(new Rect(412,78,188,1),WithAlpha(reward.Theme,textIn*.59f));
            Fill(new Rect(680,78,188,1),WithAlpha(reward.Theme,textIn*.59f));
            MoonGlow(new Vector2(640,78),22,22,reward.Theme,textIn);
            TailTitleLine(TailArchiveTitle,91,52,40,reward.Theme,TailReveal);
            TailTitleLine(reward.DisplayName,141,77,62,MoonIvory,1.51f);
            TailTitleLine(reward.Quote,223,39,30,reward.Fire,1.71f);
            TailTitleLine(TailProgress(reward.Order),265,32,24,reward.Theme,1.92f);

            float hud=(1-settle)+Ramp(t,3.30f,TailEnd);
            if(hud>0){guiOpacity=hud;PlayerHud();guiOpacity=1;}
        }
        void TailTitleLine(string line,float y,float height,int size,Color tint,float start)
        {
            float enter=Ramp(stageTime,start,start+.25f);
            float opacity=enter*(1-Ramp(stageTime,TailArrival,TailEnd));
            if(opacity<=.001f)return;
            var rect=new Rect(190,y+9*(1-enter),900,height);
            int fontSize=Mathf.RoundToInt(size*Mathf.Lerp(.94f,1,enter));
            var shadow=rect;shadow.y+=2;
            Text(shadow,line,fontSize,WithAlpha(MoonNight,opacity*.9f),TextAnchor.MiddleCenter,false);
            Text(rect,line,fontSize,WithAlpha(tint,opacity),TextAnchor.MiddleCenter,false);
        }
    }
}
