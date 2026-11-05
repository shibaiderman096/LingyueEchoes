using System;
using UnityEditor;
using UnityEngine;

namespace LingyueEchoes.EditorTools
{
    public static class MobileControlsChecks
    {
        static void Require(bool valid,string message){if(!valid)throw new Exception("Mobile controls: "+message);}
        [MenuItem("Lingyue/Check tutorial device prompts")]
        public static void RunTutorialPrompts(){var tracker=new InputDeviceTracker();
            tracker.Observe(false,true,false,0,0);Require(tracker.Device=="pad","controller button selects Xbox prompts");
            tracker.Observe(false,false,false,.7f,0);tracker.Observe(true,false,false,.7f,0);
            Require(tracker.Device=="keyboard","keyboard overrides a held controller stick");
            tracker.Observe(false,false,false,.72f,0);Require(tracker.Device=="keyboard","small stick drift does not steal prompts");
            tracker.Observe(false,false,false,-.8f,0);Require(tracker.Device=="pad","deliberate stick movement selects controller");
            tracker.Observe(false,false,true,0,0);Require(tracker.Device=="touch","touch selects on-screen prompts");
            tracker.Observe(true,false,false,0,0);Require(tracker.Device=="keyboard","keyboard returns after touch");
            Require(ControlPrompts.Key("attack","keyboard")=="J"&&ControlPrompts.Key("interact","keyboard")=="E","keyboard bindings");
            Require(ControlPrompts.Key("attack","pad")=="X"&&ControlPrompts.Key("interact","pad")=="RB","Xbox bindings");
            Require(ControlPrompts.Key("attack","touch")=="屏幕 X"&&ControlPrompts.Key("move","touch")=="左侧摇杆","touch bindings");
            var content=JsonUtility.FromJson<PrologueContent>(Resources.Load<TextAsset>("Data/prologue-content").text);
            foreach(var script in content.scripts)foreach(var line in script.lines)foreach(string device in new[]{"keyboard","pad","touch"}){
                string rendered=ControlPrompts.Format(line.text,device);Require(!rendered.Contains("{")&&!rendered.Contains("}"),"all dialogue tokens resolve");
                if(line.text.Contains("{interact}"))Require(rendered.Contains(ControlPrompts.Key("interact",device)),"dialogue uses the current interaction binding");
            }
            var dialogue=new DialogueDirector();int closed=0;dialogue.onClose=id=>closed++;dialogue.resolveText=text=>ControlPrompts.Format(text,tracker.Device);
            dialogue.Play(new DialogueScript{id="prompt",lines=new[]{new DialogueLine{speaker="guardian",text="按{interact}继续，{attack}攻击。"}}},false);
            dialogue.Update(.06);Require(!dialogue.VisibleText.Contains("{"),"typing never reveals raw tokens");dialogue.Confirm();
            Require(dialogue.VisibleText=="按E继续，J攻击。","keyboard dialogue renders");
            tracker.Observe(false,true,false,0,0);Require(dialogue.VisibleText=="按RB继续，X攻击。"&&dialogue.Active&&closed==0,"open dialogue updates without restarting or advancing");
            tracker.Observe(false,false,true,0,0);Require(dialogue.VisibleText=="按屏幕 RB继续，屏幕 X攻击。","open dialogue switches to touch");
            for(int i=0;i<600;i++)dialogue.Update(1d/60);Require(dialogue.Active&&closed==0,"device changes preserve manual dialogue waiting");
            dialogue.Confirm();Require(!dialogue.Active&&closed==1,"normal dialogue confirmation still closes once");
            Debug.Log("TUTORIAL_DEVICE_PROMPTS_PASSED: keyboard/Xbox/touch bindings, last-used device and stick drift, all dialogue tokens, live dialogue switching without reset");
        }
        [MenuItem("Lingyue/Check mobile controls")]
        public static void Run(){var controls=new MobileControls();Vector2 origin=MobileControls.StickCenter;
            controls.Begin(7,origin);controls.Move(7,origin+Vector2.right*200);
            Require(controls.Axis==1,"stick clamps at full right");
            Require(controls.Begin(12,MobileControls.ButtonCenters[2])=="attack","X maps to attack");
            Require(controls.Axis==1&&controls.Pressed("attack"),"movement and attack use different fingers");
            controls.End(12);Require(controls.Axis==1&&!controls.Pressed("attack"),"action release preserves movement");
            controls.Begin(19,origin);controls.Move(19,origin-Vector2.right*52);controls.End(19);
            Require(controls.Axis==1,"another finger cannot steal or release the stick");
            controls.Move(7,origin+Vector2.right*4);Require(controls.Axis==0,"small movement stays in dead zone");
            controls.Move(7,origin-Vector2.right*52);Require(controls.Axis==-1,"left movement");
            controls.End(7);Require(controls.Axis==0&&!controls.StickHeld,"release returns to center");
            string[] expected={"jump","dodge","attack","parry","interact","pause"};
            for(int i=0;i<expected.Length;i++){
                Require(controls.Begin(i,MobileControls.ButtonCenters[i])==expected[i],"Xbox mapping "+i);
                Require(controls.Begin(i,MobileControls.ButtonCenters[(i+1)%expected.Length])==null,"held pointer cannot trigger a second action");
                controls.Move(i,origin);Require(!controls.StickHeld,"button drag cannot become movement");
                controls.End(i);Require(!controls.Pressed(expected[i]),"button release "+i);
            }
            controls.Begin(1,origin);controls.Move(1,origin+Vector2.right*52);controls.Begin(2,MobileControls.ButtonCenters[1]);controls.Reset();
            Require(controls.Axis==0&&!controls.StickHeld&&!controls.Pressed("dodge"),"focus loss/pause clears all pointers");
            Require(controls.Begin(42,new Vector2(640,360))==null&&!controls.StickHeld,"world taps do not activate controls");
            Debug.Log("MOBILE_CONTROLS_CHECKS_PASSED: Xbox mapping, independent fingers, stick clamp/dead zone, release/cancel/reset, button ownership");
        }
        [MenuItem("Lingyue/Preview mobile controls")]
        public static void Preview(){if(!EditorApplication.isPlaying){Debug.LogWarning("Enter Play mode before previewing mobile controls.");return;}
            var game=UnityEngine.Object.FindAnyObjectByType<LingyueGame>();if(!game)return;
            game.PreviewTouchControls=true;game.BeginTrial(false);game.stage="TUTORIAL";
        }
    }
}
