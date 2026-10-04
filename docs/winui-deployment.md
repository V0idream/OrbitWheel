# OrbitWheel 2.0 WinUI 原型与部署

## 两个进程

运行根目录的 `OrbitWheel.exe`。它仍是 .NET Framework WinForms 程序，负责托盘、热键、鼠标手势和原有径向菜单。设置入口启动 `Settings/OrbitWheel.Settings.exe`，该程序是 C# / .NET 10 / Windows App SDK 1.8 的真正 WinUI 3 窗口，使用 NavigationView、ToggleSwitch、ComboBox、TextBox、Expander、ContentDialog、原生文件选择器和 Mica。设置不再加载 WinForms 标签页。

主程序退出时不会强制终止正在编辑的设置窗口。独立打开设置可以编辑配置；启动项、热键和手势由主程序应用，主程序未运行时不会声称已注册成功。

## 共享配置

继续使用 `%APPDATA%/OrbitWheel/config.json`，保留原字段、动作类型和六个扇区顺序。两种运行时编译相同的 `shared/Models.cs`、`ConfigStore.cs` 和动作名称映射；.NET Framework 使用原序列化器，WinUI 使用 System.Text.Json。

读写通过按配置路径命名的跨进程互斥锁协调，保存先写临时文件并落盘，再替换目标文件。UI 保存还核对加载时的文件哈希：另一处已保存时拒绝覆盖，保留窗口中的编辑并提示用户重新加载。坏 JSON 或暂时读取失败不会被替换成默认配置。读不到有效配置时，运行中的主程序保留最后有效状态。

WinUI 输入变化后延迟 500 毫秒保存，切换页面和关闭窗口前提交。主程序每 300 毫秒检查更新，WinUI 每 500 毫秒检查外部修改。主程序检测到新配置会关闭已有轮盘，避免继续执行旧扇区缓存，再应用配置变化。配置中的命令不会因为同步而执行。

## 两种候选部署

目前同时构建候选包，最终发行方式尚未锁定。

| 包 | 随包携带 | 外部要求 |
| --- | --- | --- |
| `OrbitWheel-2.0-self-contained.zip` | .NET 与 Windows App SDK 运行时 | Windows 10 2004 起、.NET Framework、Visual C++ x64 运行库；Mica 由系统支持情况决定 |
| `OrbitWheel-2.0-framework-dependent.zip` | 设置应用及托管依赖 | 另需 .NET 10 x64 Runtime 和 Windows App Runtime 1.8 的匹配版本 |

不要只复制根目录 EXE 或 Settings 中一个 EXE。保留整个目录；自包含表示 .NET/Windows App SDK 随包携带，不代表绕过操作系统或 Visual C++ 前置依赖。共享运行时版的 ZIP 小一些，但首次安装运行时的下载量和系统占用并未消失。

实测大小由 `scripts/package-release.ps1` 输出到 `PACKAGE-SIZES.json`，分别记录 ZIP 字节数、解压后文件总字节数和文件数量。本机未裁剪的 x64 原型实测如下（MiB = 1,048,576 字节）：

| 候选包 | ZIP | 解压后 |
| --- | ---: | ---: |
| 自包含 | 约 84.4 MiB | 约 215.2 MiB |
| 共享运行时 | 约 10.6 MiB | 约 38.0 MiB |

作为参照，1.3 发布 ZIP 为 866,726 字节（约 0.83 MiB）。WinUI 的体积代价明显。这里不包含共享运行时首次安装的下载量；精确字节数以同次构建生成的 `PACKAGE-SIZES.json` 为准。原型未启用 trimming，也没有删除 Windows App SDK 的随包依赖。

本机两种候选均已实际启动，双进程配置回归通过。共享运行时验证环境已有 .NET 10 x64 和 Windows App Runtime 1.8（8000.994.2142.0）；这不是未安装依赖的干净机器验收。旧编辑器 66 项兼容布局检查、轮盘动作与真实同名进程回归通过，workflow 通过 actionlint 1.7.12 静态检查。

## 构建与验证

使用 Windows PowerShell 5.1、.NET SDK 10.0.400。NuGet 锁文件固定 Windows App SDK `1.8.260804001`、Windows SDK BuildTools `10.0.26100.4654` 和传递依赖。如 dotnet 不在 PATH，可设置 `ORBITWHEEL_DOTNET` 为 SDK 自带的 dotnet.exe 绝对路径。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/package-release.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-action-reliability.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-shared-config.ps1 -WinUI -SettingsPath '<候选包目录>\Settings\OrbitWheel.Settings.exe'
```

双进程测试启动真实 WinUI 窗口及生产 WinForms ApplicationContext，使用隔离配置，禁用测试主程序的托盘显示及硬件钩子。测试触发真实 Fluent 控件事件，验证页面增删、模式/名称/第六扇区保存、另一进程回写、拒绝旧版本保存、坏文件保护、浅深色和窄窗口，并生成 WinUI RenderTargetBitmap 截图。它不是人工鼠标操作或完整系统桌面截图。

`legacy/WinFormsSettings.cs` 只参与旧编辑器兼容回归，不参与主程序构建或发布。CI 同时运行原轮盘/进程身份回归、共享配置及自包含 WinUI 原型测试。共享运行时版的本机运行证据另行记录；构建成功不等于未安装运行时的机器可运行。

人工验收仍需检查应用/文件夹选择器、快捷键实际触发与冲突提示、真实鼠标手势、从托盘反复打开/关闭设置、系统主题/高对比度、多显示器 DPI，以及干净机器上的依赖安装。CI 调整和本地包不等同于已发布 GitHub Release。

## 官方部署依据

- [Windows App SDK 共享运行时部署](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps)
- [.NET 部署模型](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
- [Windows App SDK 自包含部署](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)
