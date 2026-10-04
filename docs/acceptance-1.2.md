# OrbitWheel 1.2 本地验收

日期：2026-10-04。分支：`codex/action-reliability-1.2`，实施基线为 `30949d1`。产品版本为 `1.2`，程序集和文件数字版本为 `1.2.0.0`。本文记录发布前的本机验收结果。

本次范围为 [#1](https://github.com/V0idream/OrbitWheel/issues/1)、[#2](https://github.com/V0idream/OrbitWheel/issues/2)、[#3](https://github.com/V0idream/OrbitWheel/issues/3)。本机五组自动检查与构建、打包核对均通过。

发布复核时，四组常规检查通过；同一轮真实托盘检查曾失败，诊断中任务栏 UI Automation 返回零个按钮、没有可见隐藏图标面板。随后仅重跑 `-TrayOnly` 通过，专用图标收到真实双击回调。此处保留失败与重跑结果，避免将有桌面的复测结果计作稳定的无人值守 CI。

| 核心验收 | 本机结果与证据范围 |
| --- | --- |
| 保存、重启后扇区绑定不变 | 真实 `SettingsForm/DataGridView` 的所有列均为 `NotSortable`；向列标题发送真实控件鼠标消息，两页 12 个绑定顺序不变。编辑指定扇区后调用生产提交及配置保存方法，再由独立进程加载并打开设置，名称、类型、目标均保持原扇区。测试抑制 UI 自动保存和启动项写入。 |
| 移出圆环后松开不执行 | 使用真实 `Cursor.Position` 调用生产 `ExecuteHoldSelection`。覆盖移出、没有收到离开或移动事件、外缘一像素之外、中心及内外边界、六个扇区、移出后重新进入、翻页后释放、重复释放与数字键编号。动作请求被测试回调截获，不执行命令或系统动作。此项属于控件事件层检查。 |
| 同名不同路径程序分别识别 | 启动两个不同目录的专用同名 EXE。仅 A 运行时，B 的运行检测、主窗口查找、窗口枚举与托盘窗口等待均不命中 A；生产启动入口启动 B，返回 B 的真实 PID。再次执行 B 时复用已有窗口，隐藏 B 后恢复 B。 |
| 托盘唤醒正常 | 自建程序创建真实 Windows 通知图标并隐藏窗口，从关闭的隐藏图标面板开始调用生产托盘唤醒逻辑；收到真实双击回调，恢复路径匹配的窗口，鼠标位置还原。两个同名程序身份冲突时跳过不确定的托盘点击。 |

## 复现命令

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-action-reliability.ps1 -IncludeTray
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release.ps1 -Version 1.2
```

成功运行的隔离目录为 `dist/tests/run-e90f9711fdef43848056a3c56633bd74`，原始日志保存在该目录的 `acceptance.log`。自建测试进程已关闭。测试没有启动生产 `OrbitContext`，没有使用用户配置、全局快捷键或系统动作。

## 候选包

- `dist/release/OrbitWheel-1.2.zip`
- `dist/release/SHA256SUMS.txt`
- 首轮本地候选 ZIP SHA256：`f2d3e3a0065708bb4a947f44d677505e452b4e95771d3c6e1e9d498e822e292f`；正式附件以 Release 中的 `SHA256SUMS.txt` 为准。
- 包内仅有 `OrbitWheel.exe`、`README.md`、`RELEASE_NOTES.md`、`LICENSE`；打包脚本已核对版本与包内 EXE 哈希。

## 验收范围

真人全局快捷键的“按住—移入—移出—松开”、常用应用（如微信、QQ）的托盘兼容性，以及真实 Store/UWP 应用仍需人工验收。专用进程和托盘夹具通过不代表所有第三方软件已验收。

CI 与 Release workflow 已加入无真实任务栏操作的四组回归检查。本文不将本机结果计作远端 CI；远端运行和正式发布状态以 GitHub 上的 PR、Actions 和 Release 为准。
