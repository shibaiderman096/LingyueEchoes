using System;

namespace LingyueEchoes
{
    public sealed class InputDeviceTracker
    {
        public string Device {get;private set;}="keyboard";
        float previousStick,previousDpad;
        public void Select(string device){Device=device;}
        public void Observe(bool keyboard,bool padButton,bool touch,float stick,float dpad)
        {
            bool moved=Moved(stick,previousStick)||Moved(dpad,previousDpad);
            previousStick=stick;previousDpad=dpad;
            if(padButton||moved)Device="pad";
            if(keyboard)Device="keyboard";
            if(touch)Device="touch";
        }
        // A held stick or small drift must not override a later keyboard press.
        static bool Moved(float value,float previous)=>Math.Abs(value)>.3f&&(Math.Abs(previous)<=.3f||Math.Abs(value-previous)>.35f);
    }
    public static class ControlPrompts
    {
        public static string Key(string action,string device)
        {
            bool keyboard=device=="keyboard",touch=device=="touch";
            switch(action){
                case "move":return keyboard?"A / D 或方向键":touch?"左侧摇杆":"左摇杆 / 十字键";
                case "jump":return keyboard?"空格":touch?"屏幕 A":"A";
                case "attack":return keyboard?"J":touch?"屏幕 X":"X";
                case "parry":return keyboard?"K":touch?"屏幕 Y":"Y";
                case "dodge":return keyboard?"Shift":touch?"屏幕 B":"B";
                case "interact":return keyboard?"E":touch?"屏幕 RB":"RB";
                case "pause":return keyboard?"P":touch?"右上暂停键":"Menu";
                case "settings":return keyboard?"Esc":touch?"右上设置":"Menu";
                default:return action;
            }
        }
        public static string Format(string text,string device)
        {
            foreach(string action in Actions)text=text.Replace("{"+action+"}",Key(action,device));
            return text;
        }
        static readonly string[] Actions={"move","jump","attack","parry","dodge","interact","pause","settings"};
        public static string Summary(string device)=>Format("{move} 移动\n{jump} 跳跃 · {attack} 攻击\n{parry} 格挡 · {dodge} 闪避\n{interact} 交谈 · {settings} 游戏设置",device);
        public static string MenuHint(string device)=>device=="keyboard"?"↑ / ↓ 选择 · ENTER 确认 · ESC 返回":device=="pad"?"左摇杆 ↑ / ↓ 选择 · A 确认 · B 返回":"轻触按钮选择";
        public static string StoryHint(string device)=>device=="keyboard"?"空格 / ENTER 继续":device=="pad"?"A 继续":"轻触屏幕继续";
        public static string DeathMenuHint(string device)=>device=="keyboard"?"↑ / ↓ 选择 · ENTER 确认":device=="pad"?"左摇杆 ↑ / ↓ 选择 · A 确认 · B 返回主界面":"轻触选项继续";
    }
}
