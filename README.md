# 铃月回响 / LingyueEchoes

Unity 6000.6.8f1 的 2D 横版像素动作项目。用 Unity 打开本目录，打开 `Assets/Scenes/MainMenu.unity`，点击 Play；选择开始试炼进入序章。

保留连续序章路线、非阻塞 NPC 气泡、可选练习与切磋、月门试炼、第一尾奖励和进度保存。战斗为独立 C# 的 60Hz 整数帧模拟，包含六招双阶段 Boss、三段连斩、弹刀、闪避、小处决和大处决。

键盘：A/D 或方向键移动，空格跳跃，J 攻击，K 弹刀，Shift 闪避，P 暂停，R 原地重试，Esc 主界面。手柄：左摇杆/十字键移动，A 跳跃，X 攻击，Y 弹刀，B 闪避，Start 暂停。移动端保留多点触屏控制。

美术/声音位于 `Assets/Resources`；角色与特效的 32 段动画为 Unity 原生 AnimationClip，可在 Animation 窗口编辑。`Assets/Prefabs` 含角色预制体，`SourceArt` 保存原始美术源文件与生成记录。

`Lingyue/Run migration checks` 运行原工程逐帧对照、跨渲染帧率和动画引用检查。`Lingyue/Build Windows` 构建 Windows 开发版。`Lingyue/Prepare project` 会重建初始场景、预制体和动画，只在需要重新初始化时使用。

迁移检查与 Windows 构建不代表 Android 真机验收或帧率达标。浏览器本地进度不能自动跨引擎继承；Unity 使用本机 PlayerPrefs 保存序章和设置。
