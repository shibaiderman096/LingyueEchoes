# 铃月回响 / LingyueEchoes

Unity 6000.6.8f1 的 2D 横版像素动作项目。用 Unity 打开本目录，打开 `Assets/Scenes/MainMenu.unity`，点击 Play；选择「开始游戏」后，从三个存档位中选择一个。空档从序幕开始，已有存档从月门灯继续。旧版的存档保留在第一档。可在已有存档旁选择「删除」，确认后清空该档。

序章沿残月神社的参道展开。跳过断阶并落地后完成跳跃教学；木桩练习和切磋可跳过。操作说明随当前输入设备显示在教学卡上，角色台词单独显示。首次赢得月门试炼后播放第一尾剧情，守灯人之后仍可再挑战。战斗为独立 C# 的 60Hz 整数帧模拟，包含六招双阶段 Boss、三段连斩、弹刀、闪避、小处决和大处决。

键盘：A/D 或方向键移动，空格跳跃，J 攻击，K 格挡，Shift 闪避，E 交谈，Esc 打开设置。手柄：左摇杆/十字键移动，A 跳跃，X 攻击，Y 格挡，B 闪避，RB 交谈，Menu 打开设置。移动端支持多点触屏控制。死亡后从死亡菜单重试，已存档时从月门灯苏醒。

美术/声音位于 `Assets/Resources`；角色与特效的 32 段动画为 Unity 原生 AnimationClip，可在 Animation 窗口编辑。`Assets/Prefabs` 含角色预制体，`SourceArt` 保存原始美术源文件与生成记录。

`Lingyue/Run migration checks` 运行战斗逐帧对照、跨渲染帧率、动画引用、序章文案和断阶检测。`Lingyue/Check tutorial device prompts` 验证设备按键映射，`Lingyue/Run prologue play check` 在 Play 模式运行完整序章。`Lingyue/Build Windows` 构建 Windows 开发版。`Lingyue/Prepare project` 会重建初始场景、预制体和动画，只在需要重新初始化时使用。

迁移检查与 Windows 构建不代表 Android 真机验收或帧率达标。浏览器本地进度不能自动跨引擎继承；Unity 使用本机 PlayerPrefs 保存序章和设置。
