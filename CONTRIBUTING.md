# 贡献与自动构建

默认分支为 `OrbitWheel`。修改通过 Pull Request 合入，功能修复、界面迁移和工作流配置分别提交，便于审阅和回退。

## 没有写权限：Fork 后贡献

1. 在 GitHub 将 `V0idream/OrbitWheel` Fork 到自己的账号。
2. 本地已经克隆原仓库时，可以保留当前文件和分支，只调整远程地址。将下面的 `YOUR_USERNAME` 替换为自己的 GitHub 用户名：

```powershell
git remote rename origin upstream
git remote add origin https://github.com/YOUR_USERNAME/OrbitWheel.git
git fetch upstream
```

3. 新工作从上游默认分支建立修改分支，例如：

```powershell
git switch -c fix/action-reliability upstream/OrbitWheel
```

4. 完成修改和验证后，提交相关文件，推送修改分支到自己的 Fork。
5. 创建 PR，目标为 `V0idream/OrbitWheel:OrbitWheel`。PR 描述说明问题、行为变化、关联 issue 和验证结果；修复完成时使用 `Fixes #编号`。
6. 等待 CI 和维护者审阅，合并后同步 Fork，再开始下一项工作。

Fork PR 的 Actions 运行可能需要维护者首次批准，由 GitHub 的仓库设置决定。无需向贡献者提供私人 Token 或仓库 Secrets。

## 有写权限：仓库内分支贡献

长期共同维护时，仓库所有者可以邀请贡献者成为 Collaborator。接受邀请后，可直接向原仓库的修改分支推送，继续通过 PR 合入 `OrbitWheel`。CI 和发布 workflow 使用同一套配置。

建议所有者为 `OrbitWheel` 设置分支保护或规则集：要求 PR、要求 `Windows build` 检查通过，并按双方的协作约定设置审阅要求。权限、Actions 开关和分支保护需要仓库所有者设置，提交 YAML 文件不会自动更改这些设置。

## 本地构建与打包

环境要求与原项目一致：Windows、Windows PowerShell 5.1、.NET Framework 4.x。无需启动软件、注册全局热键或执行系统动作即可完成构建检查。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release.ps1
```

脚本调用原 `build.ps1`，检查 EXE 产品名和版本、README/发布说明版本、四个分发文件、ZIP 内容和打包后的 EXE 哈希，生成：

```text
dist/release/OrbitWheel-<version>.zip
dist/release/SHA256SUMS.txt
dist/release/RELEASE_NOTES.md
```

ZIP 根目录仅包含 `OrbitWheel.exe`、`README.md`、`RELEASE_NOTES.md` 和 `LICENSE`。本地审计文件等其他 `dist` 内容不会进入分发包。

发布时可以明确指定产品版本，例如当前基线：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release.ps1 -Version 1.1.2
```

版本支持两段或三段数字及可选预发布后缀。产品版本 `1.2` 对应 EXE 数字版本 `1.2.0.0`，`1.1.2` 对应 `1.1.2.0`。启用两段产品版本时，README、关于页面、发布说明和 Tag 应同步调整；保留已发布的历史版本。

## CI workflow

`.github/workflows/ci.yml` 在以下情况执行：

- PR 目标为 `OrbitWheel`；
- 向 `OrbitWheel` 推送；
- 手动运行 Actions 中的 CI。

`Windows build` 在 GitHub 托管的 Windows 2022 runner 上构建、检查并打包，上传 ZIP 和 SHA256 文件供维护者下载验收。权限为只读，不创建 Release。

这些检查覆盖构建和打包。功能修复 PR 应另外加入对应回归检查；真实快捷键、鼠标手势、主题、DPI 和窗口操作等交互验收，应单独报告实际验证结果。

## Release workflow

`.github/workflows/release.yml` 仅在原仓库 `V0idream/OrbitWheel` 推送 `v*` Tag 时运行。流程为：

1. 确认 Tag 指向的提交已经合入 `OrbitWheel`；
2. 确认 Tag 的产品版本与 EXE 数字版本相符；
3. 在 Windows runner 构建、验证并生成 ZIP、SHA256 和发布说明；
4. 在独立 job 下载构建产物并再次核对 ZIP 的 SHA256；
5. 用 `GITHUB_TOKEN` 创建 Draft Release，附 ZIP 和 `SHA256SUMS.txt`，正文使用 `RELEASE_NOTES.md`；带后缀的版本标记为预发布。

只有创建 Draft Release 的 job 具有 `contents: write`。Fork 中推送 Tag 不会创建原项目 Release。

所有者或有写权限的维护者应在合并和验收完成后，将唯一的新版本 Tag 指向已验收的提交。示例中的 `VERIFIED_COMMIT` 应替换为实际提交 SHA：

```powershell
git tag -a v1.2 VERIFIED_COMMIT -m 'OrbitWheel 1.2'
git push origin v1.2
```

运行 workflow 前应先把源码的数字版本改为 `1.2.0.0` 并更新版本文案。只有原仓库有写权限时，上述 Tag 推送才应指向原仓库；Fork 贡献者由原仓库维护者执行此发布步骤。

Draft Release 完成后，维护者核对版本、附件和实际 Windows 验收结果，再在 GitHub 发布。已存在的 Release 会使创建步骤失败，避免重新运行时覆盖既有发布内容。

## 推荐的贡献批次

1. 首个 PR 引入构建、打包 workflow 和贡献说明。
2. `1.2` 的修复按 issue #1、#2、#3 分别提交并验证。
3. `1.3` 修复 issue #4 的窗口布局。
4. `2.0` 的 issue #5 先做主界面原型，再集成和验收；届时按新工具链更新 workflow。

## GitHub 官方说明

- [Fork 与原仓库的关系](https://docs.github.com/en/pull-requests/reference/forks)
- [个人仓库的所有者与 Collaborator 权限](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/repository-access-and-collaboration/permission-levels-for-a-personal-account-repository)
- [批准 Fork PR 的 workflow 运行](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/approve-runs-from-forks)
