# 免费场景素材推荐清单（铃月回响 LingyueEchoes）

> 调研日期：2026-11-06。目标：为序章「月夜神社路线」寻找**可商用免费**的场景/背景素材。
> 所有候选页面均已实际抓取核实许可证原文；预览图已逐张目检（存放于临时目录 lye-assets\）。

---

## 一、项目美术约束（来自项目实际代码与规范）

| 约束项 | 数值 / 规则 | 出处 |
|---|---|---|
| 渲染 | 2D 正交，orthographicSize=3.6，1280×720，底色 #0B131B（暗青夜空） | `Assets/Scripts/LingyueGame.cs:93` |
| 采样 | **Point（最近邻）**，PixelsPerUnit=100 | `moonlit_route.png.meta` |
| 模块宽度 | SceneryWidth=900 世界单位，源图 724px 宽 → 缩放比 1.243 | `Assets/Scripts/PixelWorld.cs` |
| 三层结构 | canopy (724×300) / landmarks (724×248) / foundation (724×176) | `PixelWorld.CreateSceneryModule` |
| 地面线 | world_ground_y = -95；源图 ground_y = 548 | `SourceArt/prologue_route/manifest.json` |
| 调色板 | 午夜青 / 薰衣草紫 / 尘粉 / 暖金（月夜神社全景图实测） | `Assets/Resources/prologue_route/moonlit_route.png` |
| 角色规范 | 原创萌系女性化像素、透明背景、统一脚底锚点、最近邻采样 | `AGENTS.md` |
| 沿用先例 | 项目唯一外部素材为 OGA CC0 音乐（`SourceArt/contentpack_20261104/MUSIC_LICENSE.txt`），有先例可循 | — |

**结论：优先找「分层视差 (parallax layers) + 夜晚/月光配色 + 像素或可转像素」的素材包。**

---

## 二、强烈推荐（许可证干净 + 画风匹配）

### ★★★★★ 1. Mountain Dusk Parallax Background（ansimuz）
- 页面：https://ansimuz.itch.io/mountain-dusk-parallax-background ｜ OGA 备份：https://opengameart.org/content/mountain-at-dusk-background
- 许可证：**CC0 1.0**（两处页面均确认；OGA 页为单一 CC0 无歧义）✅ 可商用、可修改、无需署名
- 内容：4 组黄昏背景，**完整分层**且无缝循环；预览确认为「满月升上紫粉色群山 + 松林前景」——与铃月「月夜/薰衣草紫」调色板几乎原生匹配
- 文件：`Super Mountain Dusk Files.zip`（2.5MB，分层 PNG+PSD）
- 集成建议：替换/补充 canopy 层的山与云；月亮可与现有 moonlight 元素二选一；Point 采样导入后按 100 PPU 缩放

### ★★★★★ 2. Forest Background（ansimuz）
- 页面：https://opengameart.org/content/forest-background（**CC0**）
- 内容：分层可无缝循环的像素森林，附 PSD，39590 下载
- 集成建议：暖橙棕色，做深夜化重着色后可充当 GroveBoundary / 02_OldRoad 段的 landmarks 层

### ★★★★ 3. Bamboo silhouette（BunnyX，2025 新作）
- 页面：https://opengameart.org/content/bamboo-silhouette —— **CC0**，明确「无需署名」
- 内容：1025×889 纯黑竹丛剪影，矢量级干净形状（Fireworks 原稿含图层）
- 集成建议：整体染成深青紫后是现成的 **canopy 层**，直接对应「竹院 BambooCourtyard」模块；白色背景需抠透明（Fireworks 原稿自带分层，抠图容易）

### ★★★★ 4. Sunset Temple Tiles（Pyromantic）
- 页面：https://opengameart.org/content/sunset-temple-tiles —— 双许可 **CC-BY 3.0 或 CC0**（自选其一即可）
- 内容：64×64 横版地编 + 一张视差背景图（1914×1080 实测，像素颗粒干净）
- 集成建议：整体色相偏移到夜青/紫后可作为 stone/temple 基座层；64px 网格与项目 foundation 层尺寸好对齐

---

## 三、备选（主题契合但需注意）

| 素材 | 页面 | 许可证 | 说明 |
|---|---|---|---|
| Asian Mountain Forest BG | https://opengameart.org/content/asian-mountain-forest-bg | **CC-BY 4.0** | 文件名就是 `sprite_northWindShrineBG.png`——「北风神社」主题最贴切；但为**水墨风非像素**，861×640，横卷。需署名 + 作者请求给其游戏《Spirit of the Wind》喊话 |
| Asian Mountain Trees | https://opengameart.org/content/asian-mountain-trees | **CC-BY 4.0** | 日本山杉三棵（863×640），与上一条配套作中景树 |
| Parallax (Forest/Desert/Sky/**Moon**) | https://bongseng.itch.io/parallax-forest-desert-sky-moon | 自定义可商用禁转售；⚠️ **页面标注 AI Assisted** | Moon 组 6 层视差较少见；若团队有「不用 AI 素材」要求则排除 |
| Glacial Mountains Parallax | https://vnitti.itch.io/glacial-mountains-parallax-background | ⚠️ **许可不明**（多人询问无答复） | 质量高（4.9 分）但商用前必须先问作者 |
| itch 免费背景总览 | https://itch.io/game-assets/free/tag-backgrounds | 逐个看 | ~1676 个免费背景包，按 Popular 排序继续挖掘 |

---

## 四、不推荐 / 已排除

- ansimuz Parallax Forest（itch 镜像）：itch 标 CC-BY 而 OGA 页标 CC0，**许可有歧义**——统一改用 OGA 版（条目 2）
- vnitti Glacial Mountains：许可证缺失，未解决前不建议商用
- 其余 OGA 搜索命中（Gothicvania Town、Warped City、Cyberpunk Street 等）：题材为西式/科幻，与和风月夜不符

---

## 五、集成注意事项

1. **导入设置**：Texture Type=Sprite(2D and UI)、Filter Mode=**Point (no filter)**、Compression=None、Pixels Per Unit=100、alphaIsTransparency=true。
2. **对齐现有管线**：新模块走 `PixelWorld.CreateSceneryModule` 的三段式（canopy/landmarks/foundation，各 724px 宽源图）→ 建议把下载素材重采样/裁切成 724px 宽的三段切片，或直接扩 `SceneryNames` 数组并修改 `edges={0,300,548,724}` 分割行。
3. **重着色**：CC0 许可允许任意修改。月光夜化统一做法：色相 → 220°~250°（青紫），饱和度降 20%，暗部压至 #1A2233 级别；或叠加项目底色 #0B131B 的 multiply 层。
4. **地面线**：任何新素材的地面像素行都应对齐源图 y=548（world y=-95），否则跑图时脚底悬空。
5. **许可证归档**：沿用项目惯例，把每个素材的来源 URL + 许可证文本写进 `SourceArt/<素材名>/LICENSE.txt`（参照 `SourceArt/contentpack_20261104/MUSIC_LICENSE.txt` 格式）。CC-BY 4.0 的两个 Crisisworks 条目还需在游戏内 credits 署名 Crisisworks 并链接原页面。
6. **避坑**：pwsh/curl 在本机 TLS 握手对 opengameart.org 全挂（SEC_E_NO_CREDENTIALS），下载 zip 时请用 `python -c "import urllib.request; ..."` 或浏览器手动下载。

---

## 六、下一步建议

1. 下载 `Super Mountain Dusk Files.zip`，确认分层结构后直接试装一个模块；
2. 用 Pillow 批量把 Sunset Temple / Bamboo Silhouette 重着色为月夜配色，生成 724px 三段切片；
3. 若确认采用，运行编辑器菜单 **Lingyue → Rebuild reusable scenery** 重建模块并目检 `Prologue.unity`。
