# 设置窗口的 Windows 11 系统效果

> 历史原型记录：以下描述的是移入 `legacy/WinFormsSettings.cs` 的 WinForms 设置编辑器，仅用于兼容回归，不再编译进主程序或发布包。当前 WinUI 3 设置及双进程部署以 [WinUI 部署说明](winui-deployment.md) 为准。

设置窗口保留 WinForms 和 .NET Framework 构建方式，通过 Windows 自带的 `dwmapi.dll` 接入窗口效果，不依赖 Windows App SDK、WinUI 3 或新的运行时。

- 设置主界面使用系统绘制的 TabControl 管理常规、页面与动作、外观效果、快捷键、高级、关于六页。每页用 GroupBox 分组、TableLayoutPanel 排版，使用标准 Button、Label、CheckBox、TextBox、ComboBox、ListBox 和 DataGridView。移除旧侧栏、卡片外壳、玻璃面板、渐变和自定义圆角。应用选择对话框保留普通列表，不再自绘应用图标和列表项。
- 页面与动作使用双列 TableLayoutPanel：左侧页面列表和增删按钮，右侧页面名称和六个扇区编辑表格。其他页按原生分组纵向排列；说明文字自动换行。标签页使用原生滚动条访问小屏幕或高 DPI 下超出视口的内容。
- 切换标签前通过 TabControl.Selecting 提交正在编辑的单元格，提交失败时阻止切换，保留现有自动保存、页面增删和固定扇区绑定逻辑。原生键盘箭头切换标签也经过同一提交路径。
- Windows 11 使用系统圆角和浅色标题栏，与 .NET Framework WinForms 的原生浅色控件保持一致。这里不是 WinUI 3 的 Fluent 控件，也不模拟 Windows 设置应用。
- Windows 11 22H2 起请求 `DWMSBT_MAINWINDOW` 系统背景材质，目前对应 Mica。标签页和分组保持不透明，窗口周围的底层区域显示系统材质；不是整个设置页透明或背景模糊。
- 系统透明效果关闭或高对比度开启时取消背景材质。设置控件采用 SystemColors；选择表格行使用系统 Highlight / HighlightText。
- 不支持这些 DWM 属性、DWM 调用失败时保留现有纯色窗口。窗口保留标准标题栏、缩放和最大化行为。
- 窗口句柄重建，以及系统设置、主题或 DWM 合成变化后重新应用效果。Mica 的最终呈现由系统决定，非活动窗口等情况下可能呈现纯色。
- 扩展系统材质仅覆盖窗口内边距，不覆盖 GDI 控件区域，避免原生黑色文字因玻璃区域的 alpha 处理而消失；内边距随 DPI 缩放变化时同步更新。

## 验证

运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-settings-usability.ps1`。测试覆盖六个原生标签页、分组和嵌套表格布局、说明文字换行、小屏幕滚动、六个扇区编辑可达性、原生键盘切换标签时提交编辑、自动保存和独立进程重读。同时读取真实 HWND 的 DWM 属性，验证透明效果关闭、高对比度策略、主题消息和窗口句柄重建。测试不会更改用户的系统透明效果或高对比度设置。

在交互桌面上，编译上述测试后运行 `dist/tests/SettingsUsabilityTests.exe --native-window`，会短暂显示置顶的隔离配置窗口，在本机实际显示 DPI 下保存六页的系统屏幕截图 `native-tab-0.png` 至 `native-tab-5.png`。该模式执行窗口效果及应用选择器检查，不启动生产托盘或注册快捷键。

本机 Windows 11 的 DWM 属性检查、实际窗口截图和原有回归通过。100%、150%、200% 是控件缩放回归；本机实际显示 DPI 是 175%。旧 Windows 的回退、真正切换高对比度/透明设置、多显示器和远程桌面的视觉表现仍需人工验收，不将模拟策略参数计作这些系统环境的验收。

本次实现不修改版本号或发布远程版本。包仍只有原有 EXE、README、发行说明和许可证，不引入新的部署依赖；本地 ZIP 大小见打包脚本输出。

## 接口依据

- [DWM 窗口属性与 Windows 版本支持](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute)
- [系统背景材质及 Mica 对应关系](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type)
- [扩展系统窗口框架到客户区](https://learn.microsoft.com/en-us/windows/win32/dwm/blur-ovw)
