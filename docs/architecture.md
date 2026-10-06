# DemoOCR 开发说明

## 项目职责

`DemoOCR.Wpf` 负责应用生命周期、托盘、窗口和 MVVM 交互，单向依赖 `DemoOCR.Core`。Core 负责配置、截图、历史、平台调用和 OCR，使用 WPF 的图像、坐标与按键类型，因此同样面向 Windows。

| 目录 | 职责 |
| --- | --- |
| `Core/Configuration` | JSON 设置与快捷键配置 |
| `Core/Capture` | 虚拟屏幕抓取、光标位置、裁剪和图像转换 |
| `Core/Persistence` | PNG 保存与最近截图列表 |
| `Core/Platform` | Win32、全局热键与开机启动 |
| `Core/Ocr`、`Core/Models` | OCR 推理、结果与内嵌模型 |
| `Wpf/Infrastructure` | 托盘菜单和通知 |
| `Wpf/Views`、`Wpf/ViewModels` | 窗口、显示状态与命令 |

## 版本、名称与项目地址

`src/Directory.Build.props` 统一维护产品名称、版本及 GitHub 地址。`AppInfo` 从程序集元数据读取这些值，供设置窗口、项目链接和关于信息使用。当前产品为 `DemoOCR V0.0.1`。

应用图标位于 `src/DemoOCR.Wpf/Assets/`，同一 ICO 用于 EXE、窗口和托盘；图标资源嵌入程序，发布时无需外部文件。

## 构建与发布

从根目录执行：

```powershell
dotnet build DemoOCR.slnx -c Release
./src/scripts/publish.ps1
```

构建采用项目内默认 `bin/`、`obj/`。发布程序位于 `src/DemoOCR.Wpf/bin/Release/net10.0-windows/win-x64/publish/DemoOCR.exe`，为 Windows x64 自包含单文件，内嵌模型和原生依赖。

GitHub Actions 对 `main` 和 Pull Request 执行构建检查；标签版本与产品版本一致时，自动发布 EXE 和 SHA-256 校验文件。`src/scripts/clean.ps1` 只清理项目内 `bin`、`obj`，可用 `-WhatIf` 预览；其中的发布程序也会被清理。

## 交互验收

1. 启动发布程序，确认托盘菜单、程序名称、版本和项目链接。
2. 用 F1 框选文字，检查识别结果、点击检测框复制与全文编辑。
3. 用 F2 截图并粘贴，检查 PNG 自动保存。
4. 用 F3 贴图，检查移动、缩放、历史切换、透明度与再次识别。
5. 修改快捷键并保存，检查托盘更新与重启后的设置保留。
6. 从托盘退出，确认图标移除和快捷键释放。

当前没有独立自动化测试项目，构建检查不代替上述桌面交互验收。
