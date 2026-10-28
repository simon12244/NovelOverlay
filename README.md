# NovelOverlay · 无边框透明悬浮小说阅读器

[![release](https://img.shields.io/github/v/release/simon12244/NovelOverlay?color=success)](https://github.com/simon12244/NovelOverlay/releases/latest)
[![build](https://github.com/simon12244/NovelOverlay/actions/workflows/build.yml/badge.svg)](https://github.com/simon12244/NovelOverlay/actions/workflows/build.yml)
[![license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![platform](https://img.shields.io/badge/platform-Windows%207%20%2F%208%20%2F%2010%20%2F%2011-lightgrey.svg)](#)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.0%2B-512bd4.svg)](https://dotnet.microsoft.com/download/dotnet-framework)

一个只为「边看小说边干别的事」而生的极简阅读器。**没有窗口、没有边框、没有底色**——屏幕上只有字，字的颜色会自己跟着背后的画面调，平时完全隐身，鼠标划过去才浮现，移开又消失。

- 单文件 exe，约 65 KB，无安装、无依赖、无后台服务
- 兼容 Windows 7 SP1 / 8 / 10 / 11（需 .NET Framework 4.0+）
- 支持 `txt` / `epub` / `mobi` / `azw` / `azw3`（未压缩或 PalmDOC 压缩）/ `html`

---

## 一、开始使用

**方式 A · 直接下载（推荐，不用装开发环境）**

到 [**Releases**](https://github.com/simon12244/NovelOverlay/releases/latest) 下载 `NovelOverlay.exe`（65 KB，免安装），双击就能用。
需要 .NET Framework 4.0+；Win7 如果没装过，装一下 [4.8](https://dotnet.microsoft.com/download/dotnet-framework) 即可。

**方式 B · 自己编译**

双击 **`build.bat`** —— 它会用系统自带的 .NET 编译器生成 `NovelOverlay.exe` 并自动启动。
（提示找不到编译器，同样是装 [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework)）

**第一次打开之后：**

1. 窗口默认贴在**屏幕右侧**，尺寸 330×240，此时它是**逐像素透明**的 —— 只有字，没有底板。把鼠标移到那块区域，文字就会浮现。
2. **右键** → `打开文件…`，选一本小说。也可以把书拖到 `NovelOverlay.exe` 图标上，或命令行 `NovelOverlay.exe 书名.txt`。
3. 之后每次启动自动接着上次的书、上次的章节和位置。

> 找不到窗口了？鼠标在屏幕右侧上下扫一下；或右键 → `窗口尺寸` → `重置到屏幕右侧`。

---

## 二、操作方式

| 操作 | 效果 |
| --- | --- |
| 鼠标移入窗口范围 | 淡入显示，文字从当前位置逐行/逐字浮现 |
| 鼠标移开 | 约 0.4 秒后淡出隐藏，**停在你离开的那一屏** |
| 左键拖动 | 任意位置拖动整个窗口 |
| 拖动四边 / 四角（隐形热区） | 无边框拉伸，鼠标靠近边缘变双向箭头；热区会随窗口变小自动收窄 |
| 滚轮 | 手动滚动（会暂停自动滑动 1.8 秒） |
| 双击左键 / 空格 | 暂停 / 继续自动滑动 |
| 右键 | 打开全部功能菜单（**平时唯一的入口**） |
| `Esc` | 立即隐藏 |
| `Ctrl+O` | 打开文件 |
| `Ctrl+加号 / 减号` | 字号增减 |
| `PgUp / PgDn` | 上下翻页 |
| `← / →` | 上一章 / 下一章 |

**窗口能做多小？** 没有下限：拖动四边可以一路缩到 **90×52**，那是「一行只显示两三个字」的竖条。菜单里还有现成的 `极小 90×52` / `单行 220×48` 两个预设。字号、内边距都会随窗口自动收，缩到极小时不会因为留白把字挤出去。

---

## 三、透明与配色（这次的重点）

- 窗口用的是 **分层窗口（per-pixel alpha）**：画面每个像素单独带透明度，所以背景是**真的透明**，不是「看起来像黑色」的半透明底。只有笔画像素不透明，其余像素完全穿透视觉。
- **文字颜色自动融入背景**：程序会取窗口四周一圈的屏幕颜色（避开窗口自身，不会被自己画出来的字干扰），算出平均色和亮度：
  - 背景偏暗 → 用亮字，并掺一点背景的**补色**，看起来像被背景"染色"过，而不是硬贴上去的白字；
  - 背景偏亮 → 用暗字，同样掺一点补色；
  - 中间亮度 → 自动上**文字投影**（浅字配暗影、暗字配亮影），任何壁纸上都读得清。
  - 每 1 秒重新取一次，切换壁纸、切窗口、拖到别处都会重新适配。也可以在 `文字颜色 ▸ 自动融入背景` 里关掉，改用手动颜色。
- 不想全透明也可以：`底色 ▸` 里挑半透明底板（深色/墨蓝/米黄/白）或纯黑不透明，并可用 `底色浓度` 无极调节——想要「一点点底」就调到 15%。
- **边缘渐隐**在透明模式下改成**逐行淡出**（不再画渐变矩形）；窗口只有一行高、或刚好到章首/章尾时不会淡，保证字始终完整。

---

## 四、右键菜单一览

```
《书名》
2 / 12 · 第二章 旧地图
────────────────────
打开文件…            Ctrl+O
最近打开 ▸
章节 ▸  上一章 / 下一章 / 章节列表…
────────────────────
字号 ▸  12 14 16 18 20 22 24 26 28 32 36 42 48 56 64
快速缩放 ▸  ×0.90 ×1.05 ×1.20 ×1.40 / 还原 19
字体…                       选择系统里任意字体
行距 ▸  紧凑 / 较紧 / 标准 / 宽松 / 很宽 / 超宽
文字颜色 ▸  ✓自动融入背景（跟随屏幕） / 白 米白 暖黄 浅绿 天蓝 粉 灰 墨黑 / 自定义…
底色 ▸  ✓完全透明（无底色） / 深色半透明 墨蓝半透明 米黄半透明 白色半透明 纯黑不透明
        / 底色浓度 0~100% / 自定义颜色…
整体不透明度 ▸  35% 50% 65% 80% 90% 100%
✓ 文字投影（更易读）
────────────────────
浮现方式 ▸  瞬间显示 / 逐行浮现 / 逐字浮现
浮现速度 ▸  很慢 慢 标准 快 很快 极快
滑动速度 ▸  8 / 14 / 22 / 34 / 50 / 76 px 每秒
✓ 自动滑动（悬停时）
────────────────────
✓ 窗口置顶
✓ 悬停才显示（否则隐藏）
✓ 边缘渐隐
✓ 首行缩进
✓ 锁定位置/大小
  鼠标穿透（点击到下层窗口）
窗口尺寸 ▸  极小 90×52 / 单行 220×48 / 窄条 260×全高 / 标准 330×240 / 宽幅 560×420
            / 更宽/更窄/更高/更矮 / 重置到屏幕右侧
回到本章开头
隐藏（Esc）
────────────────────
退出
```

> `鼠标穿透` 打开后，点击会落到下面的窗口（适合把书放在图纸/网页上读），但此时**不能拖动、不能右键**，只能靠菜单先关掉它。

---

## 五、几个细节

- **「浮动」效果**：每次浮现，从你当前停留的那一行开始，一行行（或一字字）带位移淡入；这就是「悬停后字体滑动，一个个显示内容」。
- **「停在那」**：窗口隐藏时动画和自动滑动全部停止，位置、进度原样保留。
- **折行**：逐字贪心排版，中文按字断、英文尽量按单词断（单词比窗口还宽时才按字断），所以窗口很窄也不会出现半个字跑出画面。
- **不抢焦点**：`ShowWithoutActivation` + 工具窗口样式，悬停浮现时不会打断你正在打的字。
- **滚轮归属**：用消息过滤器拦 `WM_MOUSEWHEEL`，鼠标压在悬浮窗上时滚的是小说，不会把背后的 Word 一起滚了。
- **章节识别**：txt 自动按 `第X章 / 第X节 / Chapter N / 楔子 / 序章 / 番外…` 切章；epub 按 spine 顺序 + `<h1~h3>` 标题。
- **编码识别**：txt 自动判断 UTF-8（含 BOM）/ UTF-16 / GBK / GB18030。
- **绿色**：所有配置都在 exe 同目录的 `settings.ini`，删掉即恢复出厂；不写注册表。

### 配置文件（exe 同目录 settings.ini）

```ini
FontName=Microsoft YaHei
FontSize=17
TextColor=240,242,245      # 手动文字颜色（自动融入背景打开时不生效）
BgColor=16,18,22           # 底色颜色
BgAlpha=0                  # 底色浓度，0 = 完全透明
AutoColor=1                # 文字自动跟随屏幕背景
TextShadow=1              # 文字投影
Opacity=0.95               # 整体不透明度
LineSpacing=1.45
PadX=14
PadY=10
EdgeFade=1
IndentFirst=1
TopMost=1
HoverHide=1
AutoScroll=1
ScrollSpeed=22
RevealMode=1               # 0 瞬间 1 逐行 2 逐字
RevealSpeed=1
Locked=0
MouseThrough=0
HoverPad=26
WinW=330
WinH=240
CfgVer=2                   # 配置版本，老配置首次启动自动迁移成透明小窗
```

---

## 六、已知限制

1. **azw3 里 HUFF/CDIC 压缩的 mobi** 暂不支持（会提示「建议转换为 epub 或 txt」）。
2. **不支持带 DRM 的电子书**。
3. 极长的单章（几十万字不分章）首次排版会有零点几秒停顿，之后滚动流畅。
4. 全屏独占的游戏/视频播放器之上可能盖不住（系统限制），窗口化或无边框全屏正常。
5. Win7 需 SP1 + .NET Framework 4.0 以上。

---

## 七、源码结构

```
build.bat              一键编译（自动找系统编译器，编译并启动）
src/Program.cs         入口、全局异常记录（error.log）
src/Settings.cs        settings.ini 读写 + 版本迁移
src/Formats.cs         txt/epub/mobi 解析 + 自带 ZIP 读取器 + PalmDOC 解压
src/Overlay.cs         分层窗口(UpdateLayeredWindow)、屏幕取样、对比色计算、鼠标穿透
src/ReaderView.cs      排版、浮现/滑动动画、透明度逐行淡出（纯逻辑，无控件）
src/MainForm.cs        分层透明窗口、渲染循环、悬停显隐、拖动拉伸、右键菜单、章节选择器
test/示例小说.txt · 示例.epub · 示例.mobi   三份测试样书
tools/make_samples.py  生成上面三份样书
tools/genbat.py        生成 GBK 编码的 build.bat
tools/check.py         括号配对自检（开发用）
```

---

## 八、参与贡献

欢迎 issue 和 PR。提交前请确认：

- 代码需保持 **C# 5 / .NET Framework 4.0 语法**（不能用 `$"{x}"` 字符串插值、`?.`、`nameof`、`out var`、Tuple 等），否则 Win7 自带的 `csc.exe` 编不过；
- 不要引入第三方 NuGet 包，保持「单文件、零依赖」；
- `settings.ini` 属于本地个人配置，已在 `.gitignore` 里，请勿提交；
- 推 PR 前跑一下括号自检：`python tools/check.py`。

编译方式任选其一：

| 方式 | 命令 |
| --- | --- |
| 一键脚本 | 双击 `build.bat` |
| 手动 csc | `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:NovelOverlay.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll src\*.cs` |
| CI | push 到 `main` 后由 GitHub Actions 编译，产物在 Actions 页面的 Artifacts 里 |

---

## 九、开源协议

[MIT License](LICENSE) © 2026 simon12244

你可以自由使用、修改、分发、商用，只需保留版权声明与协议原文。
