# 二阶段 Boss 素材与验证

## 内容

- 转阶段重新绘制为两段各16帧，共32帧：`phase-engulf.webp` 为站立失力 → 单膝跪地 → 地面怒火吞噬全身、武器灰化；`phase-rise.webp` 为黑化逐渐显现 → 抬头 → 起身。对应运行资源分别在 `phase-engulf/` 和 `phase-rise/`，每段从01到16编号。
- `phase.webp` 是32帧连续播放预览。游戏在原转阶段总时长中依次播放两段，两段之间不重复、不跳帧；90/120 tick下逐tick检查顺序与覆盖。显式 pose 名称 `phase-engulf`、`phase-rise` 可独立预览，各自使用 `PhaseTransitionFrames` 时长。
- `idle.webp`、`walk.webp`、`hurt.webp`：二阶段空手待机、移动、受击，各 16 帧。
- `stun.webp`：16 帧跪下动作，预览包含末帧停留和倒放起身。游戏按实际眩晕时长分配进入、保持和恢复。
- 游戏帧位于 `Assets/Resources/WrathGeneral/phase2/frames`，384×384，PPU 180，统一 pivot (230/384, 37/384)。

## 来源与处理

两套16帧原图均由主智能体使用内置 image_gen 重新绘制，补画吞噬和显形/起身的中间动作；没有复制旧16帧或插值凑数。子智能体只参与代码接入和检查。当前无可用视频生成通道，未使用视频生成。`reference.png` 为用户参考，`master.png` 为二阶段身份参考。生成费用由工具管理，未提供金额。

最终转阶段提示记录在 `phase32-prompts.json`，原图为 `phase-engulf.png`、`phase-rise.png`，各1261×1247，按4×4独立裁格。`engulf-end-reference.png` 是首段末帧裁图，用作第二段姿态参考。`phase.png`、`phase-kneeling-prompt.json`、`prompts.json`、`generation.json` 保留旧版来源记录，旧phase.png不参与打包。最终行走使用 `walk-stride-prompt.json`。后续一次换腿定向修正仍未解决问题，未替换当前素材。

`package.py` 仅裁帧、最近邻缩放、脚底注册与透明清理；没有程序绘制角色、骨骼变形或插值伪造动作。原图格线使用 half-up 取整；每动作一个固定缩放比例。两段转换以站立参考高度校准缩放，前脚金色鞋底注册到同一位置(x260,y346)，减少切段漂移。alpha>=128 归为不透明，小于 3 像素的独立碎点移除，透明 RGB 清零。

## 已通过

- 96 张帧图：尺寸正确，每动作 16 个不同像素哈希；alpha 仅 0/255，透明区域 RGB=0，无画布边缘接触。转换独立核对为16+16，共32个新绘制的不同帧。
- 六张整理后的动作图集均通过 sheet_qc spill：无跨格、无裁切、无空格、无边界带及碎点告警。安全边距按 1% 检查；打包另断言每帧至少 8 像素边距。未改动的四段沿用之前的检查结果，两段新转换重新检查。
- Unity `WrathPhaseTwoChecks.PlayBatch` 在独立项目副本原生运行，退出码 0：96 帧 Sprite 导入与采样、90/120 tick 配置下的实际半血转阶段、32帧完整顺序与末帧完成、两独立片段各16帧采样、二阶段待机、重试恢复一阶段、武器灰化状态复位、眩晕停留/倒放起身、两个朝向共 192 个 PixelActor 样本通过。
- `native-play-review.png` 为该次运行导出的深浅背景、两个朝向末帧截图，由主智能体检查。

## 未解决的画面质量项

- **移动步态未通过**：最后一版 walk 的自动 NEAR/FAR 检查仍报告 `leg_alternation: fail`（15 帧判 FAR、1 帧不明确）。肉眼也无法确认完整、清楚的双腿交替；保留为已接入的临时动作，不作为最终步态验收通过。
- 待机自动分析有 near-duplicate、torso_drift、row_baseline、seam 告警。哈希均不同；长发/裙摆影响自动躯干定位，但帧间细节与基线仍有轻微差异。
- 行走自动分析另有 phase_coverage、torso_drift (4.88 px)、row_baseline (2 px)、seam 告警。未放宽阈值消除告警。完整数值见 `qc-summary.json`。
- 原始生成图有半透明碎点等告警；整理后的运行图透明检查通过。没有白膜。
- 验证日志另有 UnityEditor.Search.SearchDatabase 启动索引异常；堆栈不属于游戏代码，未阻止上述检查与退出。没有将其描述为零异常运行。

未新增二阶段攻击动画；既有未绘制动作继续使用该阶段准备姿势。未修改战斗数值、保存格式、用户场景摆放或未提交的旧素材导入设置。
