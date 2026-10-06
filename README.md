# DemoOCR · 截图、识字与桌面贴图 `V0.0.1`

让屏幕上的文字，随手可用。

DemoOCR 是一个 Windows 桌面截图与离线 OCR 工具。通过快捷键框选屏幕，即可识别文字、复制截图，或将图片贴在桌面上。程序常驻系统托盘，使用 RapidOCR 与内嵌的 PP-OCRv6 模型在本机完成推理。

项目主页：[GitHub · peijiehuang/DemoOCR](https://github.com/peijiehuang/DemoOCR) · [下载程序](https://github.com/peijiehuang/DemoOCR/releases)

![DemoOCR 软件海报：截图、识字与贴图](docs/images/demoocr-poster.png)

> 海报为功能示意，实际界面以运行程序为准。图中 F1、F2、F3 为默认快捷键；发生冲突时请以托盘菜单显示的生效快捷键为准。

## 核心功能

- **截图识字**：框选后自动识别，结果窗口同时显示原图、文字检测框和可编辑全文；点击文字框可复制对应文字。
- **截图复制**：直接将选区图片复制到剪贴板，并自动保存 PNG 截图。
- **桌面贴图**：将剪贴板图片或最近截图置顶，支持拖动、滚轮缩放、透明度调整，以及从贴图再次识别文字。
- **历史回看**：在贴图窗口切换最近截图，默认展示最近 10 张，可在设置中调整至 1–100 张。
- **本机推理**：OCR 模型与字典内嵌发布程序，无需在运行时下载模型或上传截图到识别服务；首次启动会释放模型并在后台预热。
- **托盘与快捷键**：自定义全局快捷键、冲突时尝试备用组合，支持可选开机启动和单实例运行。

## 运行环境

- Windows x64，建议 Windows 10/11。
- 开发构建需要 **.NET 10 SDK**。
- 使用本文发布脚本生成的程序为自包含单文件，无需另装 .NET 运行时。

在 [Releases](https://github.com/peijiehuang/DemoOCR/releases) 下载 `DemoOCR-v0.0.1-win-x64.exe`，直接运行；也可按下面的命令从源码构建。

## 快速使用

1. 启动 `DemoOCR.exe`，程序常驻系统托盘；初次启动时等待 OCR 模型预热。
2. 按 **F1**，拖动鼠标框选屏幕区域。默认松开鼠标即开始识别；关闭自动识别后，可按 Enter、双击选区或点击确认按钮。
3. 在结果窗口编辑或复制全文，点击检测框复制单段文字，也可复制、保存原图或切换窗口置顶。
4. 按 **F2** 框选区域，将截图复制到剪贴板；按 **F3** 将剪贴板图片贴到桌面。剪贴板无图时，会尝试读取最近截图。
5. 右键托盘图标打开设置，修改快捷键、自动识别、开机启动与历史数量；退出程序也通过托盘菜单完成。

### 常用操作

| 场景 | 操作 | 效果 |
| --- | --- | --- |
| 全局 | F1 / F2 / F3 | 默认分别为截屏识别、截屏复制、贴图置顶 |
| 框选中 | Esc 或鼠标右键 | 取消截图 |
| 框选完成 | Enter 或双击选区 | 确认识别或复制 |
| 框选完成 | F3 | 将当前选区贴到桌面 |
| 识别结果 | 点击文字框 | 复制对应文字并联动全文选区 |
| 识别结果 | 图像区域滚轮 / Ctrl + 滚轮 | 缩放截图预览 |
| 贴图窗口 | 鼠标拖动 / 滚轮 | 移动 / 缩放贴图 |
| 贴图窗口 | ← 或 A / → 或 D | 查看更早 / 更新的历史截图 |
| 贴图窗口 | Ctrl + C / Ctrl + S | 复制 / 保存图片 |
| 贴图窗口 | 右键菜单 | OCR、透明度、置顶与打开截图目录 |
| 贴图窗口 | Esc 或双击 | 关闭当前贴图 |

全局快捷键可在设置中修改。框选界面内的 F3 是固定操作键。

## 数据与模型

| 内容 | 默认位置 |
| --- | --- |
| 设置 | `%LocalAppData%\DemoOCR\settings.json` |
| 模型缓存 | `%LocalAppData%\DemoOCR\models\` |
| 启动与错误日志 | `%LocalAppData%\DemoOCR\` 下的 `startup.log`、`app.log`、`error.log`，按需生成 |
| 自动保存截图 | 用户图片目录下的 `DemoOCR\`，通常为 `%USERPROFILE%\Pictures\DemoOCR\` |

历史数量控制最近截图列表，**不会删除已经保存的 PNG 文件**。移动发布程序后，已有设置与截图仍保存在上述用户目录；启用开机启动时，程序启动会同步当前可执行文件路径。

模型源文件位于 `src/DemoOCR.Core/Models/`，包括检测、方向分类、识别模型和字典。它们嵌入 Core 程序集，再随主程序打包发布。

## 项目结构

桌面应用单向依赖基础服务，构建产物使用各项目默认的 `bin/`、`obj/` 目录。

```text
DemoOCR/
├── DemoOCR.slnx                 # 解决方案
├── README.md                    # 使用、构建与发布说明
├── .github/workflows/           # 构建检查与标签自动发布
├── src/
│   ├── DemoOCR.Core/            # 配置、截图、历史、平台与 OCR 服务
│   ├── DemoOCR.Wpf/             # WPF 应用、界面与 Assets/ 应用图标
│   ├── scripts/                # 发布和清理脚本
│   ├── Directory.Build.props   # 公共编译配置
│   └── nuget.config            # NuGet 源
└── docs/
    ├── architecture.md         # 架构与验证说明
    └── images/                 # README 海报
```

Core 使用 WPF 的图像、坐标和按键类型，因此同样面向 Windows。它不依赖桌面应用项目。详见 [架构与验证说明](docs/architecture.md)。

`src/DemoOCR.Wpf/Assets/App.ico` 是 EXE、窗口和系统托盘共用的应用图标，包含 16、24、32、48、64、128、256 像素尺寸；PNG 原稿为 `src/DemoOCR.Wpf/Assets/App.png`。图标通过资源嵌入应用，发布时无需携带外部图标文件。

## 构建与调试

在根目录打开 `DemoOCR.slnx`，将 `DemoOCR.Wpf` 设为启动项目，或执行：

```powershell
dotnet build DemoOCR.slnx -c Release
dotnet run --project src/DemoOCR.Wpf/DemoOCR.Wpf.csproj
```

首次构建需要联网还原 NuGet 包。应用依赖 CommunityToolkit.Mvvm、RapidOcrNet 和 SkiaSharp.NativeAssets.Win32，版本维护在相应项目文件中。

## 发布与清理

```powershell
# 在 PowerShell 中发布 Windows x64 自包含单文件
./src/scripts/publish.ps1

# 清理各项目默认的 bin、obj 目录
./src/scripts/clean.ps1
```

发布结果位于 `src/DemoOCR.Wpf/bin/Release/net10.0-windows/win-x64/publish/DemoOCR.exe`。模型和原生依赖随程序打包；原生库会由 .NET 单文件运行机制按需解压。

也可以双击 `src/scripts/clean.cmd`；命令行自动执行时使用 `src/scripts/clean.cmd --no-pause`。清理脚本会删除各项目的 `bin`、`obj`，其中包括默认发布目录；可用 `./src/scripts/clean.ps1 -WhatIf` 预览清理范围。

## 版本与自动发布

版本统一维护在 `src/Directory.Build.props`：产品版本 `0.0.1`，文件和程序集版本 `0.0.1.0`，程序显示 `V0.0.1`。设置窗口和托盘菜单提供 GitHub 项目入口。

推送 `main` 或提交 Pull Request 时，GitHub Actions 自动检查 Release 构建。推送与产品版本一致的 `v*` 标签后，工作流构建 Windows x64 自包含单文件，并创建 GitHub Release，附带 EXE 和 SHA-256 校验文件。

```powershell
git tag v0.0.1
git push origin v0.0.1
```

## 当前边界

- 面向桌面截图与图片文字识别，未实现 PDF 批量识别、翻译或云端同步。
- OCR 结果受字体、缩放、画质和版面影响，请核对关键内容。
- 架构与交互验收方式见 [开发说明](docs/architecture.md)。

## 技术栈

- .NET 10 / WPF / CommunityToolkit.Mvvm
- [RapidOcrNet](https://github.com/BobLd/RapidOcrNet) / [RapidOCR](https://github.com/RapidAI/RapidOCR) / PP-OCRv6
- [SkiaSharp](https://github.com/mono/SkiaSharp) / ONNX Runtime / Win32 GDI
