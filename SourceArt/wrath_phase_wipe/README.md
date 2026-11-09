# 二阶段全屏灼烧转场

进入二阶段时，镜头随第一段人物动画平滑拉近至2.5倍Boss特写 → 第一段第15/16帧以6Hz交替循环1秒 → 播放本16帧全屏转场（1.6秒） → 第二段第1/2帧以6Hz交替循环1秒 → 接着起身，镜头随第二段动画平滑退回正常战斗视角。

Boss受击触发二阶段后，人物转换过程按0.5倍速度推进，起身结束恢复普通战斗速度；两处循环与火焰转场合计3.6秒，使用独立表现时钟，期间冻结战斗和输入。普通战斗速度及用户慢速设置不变。暂停、设置、背包或失焦时，表现时钟与特写镜头一并暂停。

火焰由底部向两侧及上方蔓延，焦黑裂纹层覆盖全屏，再从中间烧开缺口露出人物。运行资源在 `Assets/Resources/WrathGeneral/phase2/wipe`，每帧512×288、Point、binary alpha，透明RGB清零。按实际Screen尺寸铺满，包含宽高比边缘。峰值第9–11帧的绘画纹理约97.9%不透明；运行时另绘制完全不透明焦黑底层填补微孔，前后渐入/出。

原图由主智能体用内置image_gen绘制，记录在 `generation.json`；没有子智能体绘图。`package.py` 按4×4裁格、内缩2像素移除生成图分隔线、最近邻缩放和清理alpha，不生成火焰图形或插值姿势。`wipe.webp` 是纯FX透明预览；`before-hold-screen.png`、`peak-screen.png`、`after-hold-screen.png`、`after-rise-screen.png` 是Unity实际GameView画面。

## 验证

- 独立检查16张512×288帧，16种不同像素内容；alpha仅0/255，透明RGB=0。覆盖率按帧记录在manifest.json。
- 原始图的sheet_qc spill报告 `cross_cell_visible` 1项（低alpha边缘），以及boundary_band/safe_frame/faint_specks告警。binary alpha及逐格内缩裁切移除了软边串格；本素材是全屏出血特效，故不要求角色图集那样的透明边距。保留原始检查结果，不声称原图spill零告警。
- Unity `WrathPhaseWipeChecks.PlayBatch` 在独立副本原生GameView执行，退出0：90/120tick两种配置、大delta精确停在切点、两组指定帧各循环1秒、1.6秒烧屏、期间无战斗tick/生命变化、输入无效、设置/背包/失焦/应用暂停/显式暂停、恢复不补tick、重试复位通过。
- 镜头特写大小为1.44，中心对准Boss；起身后恢复3.6及竞技场中心。普通战斗20次1/60秒更新推进20tick，人物转换推进10tick，用户慢速设置保留。四张截图在真实室内坐标获取，主智能体检查人物完整及镜头恢复。
- `WrathPhaseTwoChecks.Run`：原96帧导入及两段人物动画采样通过。
- `MigrationChecks.Run`：7236配置快照、30/60/120/144Hz时钟和40个原生动画等回归通过。
- 实际截图1638×922，四角均RGB(15,1,1)，主智能体检查焦黑火层覆盖整屏，没有漏出场景。
- 初次batchmode截图未生成；改用独立副本的隐藏原生GameView完成真实OnGUI截图。日志仍有既存UnityEditor.Search.SearchDatabase启动索引异常，不属于游戏代码，未阻止最终检查。

战斗规则和数值、存档格式、用户手工摆放的场景未修改。FixedClock的可选逐tick截断回调用于切点冻结；人物转换的减速在章节表现层施加。
