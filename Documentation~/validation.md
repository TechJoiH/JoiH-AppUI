# 验证与发布门禁

## 验证目标

AppUI 当前唯一 Official Target 是 Unity 6.0 / `6000.0`，固定验证 Editor 为 `6000.0.25f1`。这个目标来自框架当前主要开发、真实项目和验证环境，不与 Unity 最新 LTS 自动同步。

“仓库代码能编译”不足以证明包可接入。官方验证对象是一份从精确 Git Commit 导出的 UPM 候选包，以及一个位于仓库外、没有旧 `Library` 和本地缓存的 Consumer Project。

```text
exact Commit
    ↓
deterministic package snapshot
    ↓
external Unity6000.0Consumer
    ↓
Sample → Fixture → Binding → Tests → Player Builds
    ↓
external report + sanitized artifacts
```

## 四层证据

### 1. 包内契约测试

包内 EditMode/PlayMode 测试验证 Operation、Definition、Registry、Focus、Input、Binding、Lease 和页面生命周期等框架行为。这一层能发现框架回归，但不能单独证明 UPM 安装成功。

### 2. Commit 快照 `file:` Consumer

`New-AppUICandidateSnapshot.ps1` 从 40 位 Commit 导出候选包并生成：

- `candidate-identity.json`：Repository、Commit、Tree、Package Version；
- `package-manifest.json`：每个跟踪文件的规范化路径、Git mode 和 SHA-256；
- `packageManifestSha256`：规范化内容清单的总哈希。

`New-AppUIConsumerWorkspace.ps1` 将 `Validation~/Unity6000.0Consumer/` 复制到仓库外，生成只指向该候选快照的 `Packages/manifest.json`。之后才运行 Sample、Domain Reload、Fixture、Binding、测试与构建。

完整流程结束后会重新计算候选快照的逐文件哈希与总 Manifest Hash，并拒绝任何被修改、缺失或额外出现的文件。这保证 Unity Consumer 运行不能悄悄改变被验证的 UPM 候选内容。

### 3. 已推送 Commit SHA Git URL Smoke

本地全门禁通过并获得推送授权后，用全新的 Consumer 安装：

```text
https://github.com/TechJoiH/JoiH-AppUI.git#<40-character-commit-sha>
```

这一层证明 GitHub 上的候选 Commit 可以被 UPM 获取、解析和运行。它不得复用本地 Pre-tag Consumer 的 `Library`、Package Cache 或 `packages-lock.json`。

### 4. 不可变 Tag Git URL Smoke

只有 Commit SHA Smoke 通过并再次获得明确授权，才创建普通 SemVer Tag。Tag 创建后使用另一个全新 Consumer 安装：

```text
https://github.com/TechJoiH/JoiH-AppUI.git#v0.2.0-pre.2
```

Tag Smoke 失败时不移动、不删除并重建同名 Tag。修复必须发布新版本。

## 外部 Consumer 流程

完整顺序固定为：

```text
Static Policy
→ Candidate Snapshot
→ Materialize Consumer
→ Import Basic Integration
→ Domain Reload
→ Create Fixtures + Generate Bindings
→ Domain Reload / compile generated partials
→ Bind + Validate
→ EditMode
→ PlayMode
→ Windows Mono Development Build
→ Windows IL2CPP Development Build
→ External Report
```

Static Policy 会从精确 Commit 快照检查：根 `package.json` 是唯一包清单、版本符合严格 SemVer、唯一官方 Consumer 为 `Validation~/Unity6000.0Consumer/`、Unity 为 `6000.0`、依赖只有 UGUI `2.0.0`，并继续执行禁用依赖、版本宏、Compatibility、Unity Meta、文档链接与 Git 空白检查。

Consumer 使用真实 EventSystem、Canvas、GraphicRaycaster、Layer Root、Definition、Registry、Profile、Button、ScrollRect 和 `B_` Binding 节点。测试覆盖 Basic 页面完整生命周期、Popup Cancel/Background/Input Block、SceneScope 释放、Pending Load 晚到成功、真实焦点选择变化和 Lease 单次释放。

## 可执行命令

从精确 Commit 创建候选快照：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools~/Release/New-AppUICandidateSnapshot.ps1 `
  -RepositoryPath (Get-Location).Path `
  -SourceRef <40-character-commit> `
  -DestinationPath D:\AppUIValidation\snapshot
```

物化一个外部 Consumer：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools~/Release/New-AppUIConsumerWorkspace.ps1 `
  -TemplatePath Validation~/Unity6000.0Consumer `
  -DestinationPath D:\AppUIValidation\consumer `
  -PackageReference D:\AppUIValidation\snapshot\candidate\package
```

执行完整 Pre-tag 门禁：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools~/Release/Invoke-AppUIPreTagValidation.ps1 `
  -RepositoryPath (Get-Location).Path `
  -SourceCommit <40-character-commit> `
  -PlannedTag v0.2.0-pre.2 `
  -UnityPath 'C:\Unity\Unity 6000.0.25f1\Editor\Unity.exe' `
  -RunRoot D:\AppUIValidation\v0.2.0-pre.2-local
```

远端 Commit 或 Tag Smoke 使用 `Tools~/Release/Invoke-AppUIGitInstallSmoke.ps1`。该脚本只接受 TechJoiH 官方仓库的 40 位 Commit 或 SemVer Tag URL。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools~/Release/Invoke-AppUIGitInstallSmoke.ps1 `
  -PackageReference 'https://github.com/TechJoiH/JoiH-AppUI.git#<commit-or-tag>' `
  -ExpectedPackageVersion '0.2.0-pre.2' `
  -UnityPath 'C:\Unity\Unity 6000.0.25f1\Editor\Unity.exe' `
  -RunRoot D:\AppUIValidation\git-smoke
```

## Passed、Failed 与 Blocked

- `Passed`：当前候选、当前环境和当前步骤存在完整成功证据；
- `Failed`：测试断言、Binding、编译或 Player Build 已运行并确认失败；
- `Blocked`：门禁因环境缺失或超时未完成，例如缺少 Windows C++ toolchain；
- `NotRun`：尚未进入该阶段，例如 Tag 创建前的 Tag URL Smoke。

每个 Unity 子进程默认最多运行 120 秒。超时后工具终止该精确进程，当前流水线停止；不得继续后续步骤，也不得用旧候选的结果补齐本次报告。`Blocked` 不是 `Passed`，也不是 `Known Incompatible`。

完整运行前会先验证 Unity 精确版本，并通过 `vswhere.exe` 只接受 Visual Studio 2022（17.x）C++ toolchain。`vcvars64.bat` 必须能暴露 `cl.exe`、`link.exe`、`rc.exe` 与 `WindowsSdkDir`；否则写出 `build-environment.json` 并以 `Blocked/MissingToolchain` 或 `Blocked/ToolchainProbeFailed` 停止。

## 报告与 Artifact

`pretag-report.json` 和正式 Release Report 都生成在候选 Commit 之外，至少绑定：

- Repository、Source Commit、Source Tree；
- Planned Tag 与正式阶段解析到的 Remote Tag；
- Package Version 与 `packageManifestSha256`；
- Unity、UGUI 与操作系统；
- EditMode、PlayMode、Binding、Mono、IL2CPP；
- Commit SHA Smoke 与 Tag Smoke；
- 每个 Gate 的证据文件和耗时。

Commit 与 Tag Smoke 可以来自各自独立 Run Root；`New-AppUIReleaseReport.ps1` 通过 `-CommitSmokePath` 与 `-TagSmokePath` 显式组合这些证据，并逐项核对 Commit、Tree、Version、Manifest Hash 与安装 URL，不要求人工复制或修改 Smoke JSON。

正式报告必须通过 `git ls-remote origin refs/tags/<tag>` 解析远端 Tag，并确认 Commit/Tree 与候选一致。日志上传前先替换 Repository、Consumer 和 User Profile 路径，再扫描 `ghp_`、`github_pat_`、Authorization Header 和私钥标记；秘密审计失败时不创建 Release。

`Invoke-AppUIPreTagValidation.ps1` 将原始日志保留在外部 Run Root 的 `logs/`，在 `evidence/` 生成脱敏且通过秘密审计的 `appui-v0.2.0-pre.2-logs.zip`。候选仓库不接收这些运行产物。

Tag Smoke 完成后，`Tools~/Release/New-AppUIReleaseArtifacts.ps1` 将正式报告、Package Manifest、EditMode/PlayMode XML、Binding、Mono/IL2CPP、Commit/Tag Smoke 和日志 ZIP 复制到新的 `artifacts/` 目录。九个文本文件都会执行同样的路径脱敏与秘密审计；目录必须恰好包含十个文件才能进入 GitHub Release。

创建 Tag 前可运行 `Tools~/Release/Test-AppUIReleaseReadiness.ps1`。它只读解析远端 `main` 和 Tag：只有远端 `main` 正好等于候选且 Tag 尚不存在时返回 `ReadyForTag`；未推送为 `NotPushed`，仅本地存在同名 Tag 为 `LocalTagExists`，远端同名 Tag 已指向候选为 `TagExists`，指向其他提交为 `TagConflict`。它不会执行 push、Tag 或 Release 操作。

## 当前 `0.2.0-pre.2` 验证状态

框架开发阶段曾在独立 Consumer 对前序实现候选完成以下验证：

- Unity：`6000.0.25f1`；
- Package Manager、Basic Integration、Domain Reload：通过；
- Binding：0 Error、0 Warning、8 Info；
- EditMode：134/134 通过；
- PlayMode：17/17 通过；
- Windows x64 Mono Development Build：通过，0 Error、0 Warning；
- Windows x64 IL2CPP：因缺少可用的 Windows C++ toolchain 未通过。

这些结果属于 `Historical Development Evidence`，用于证明 Consumer、Fixture 与门禁实现可工作，但它们绑定前序 Commit，**不能作为当前精确发布候选的 Release 证据复用**。任何候选 Tree 变化后，都必须用新的 Run Root 从 Static Policy 重新执行完整流程。

当前最新干净候选的 `Current Candidate Evidence` 状态是：

- Static Policy：`Passed`；
- Candidate Snapshot 与 Commit/Tree/Version/Manifest Hash：`Passed`；
- Unity `6000.0.25f1` 版本检查：`Passed`；
- Visual Studio 2022 C++ 工具链预检：`Blocked/MissingToolchain`；
- 当前候选的 Package resolve、Sample、Binding、EditMode、PlayMode、Mono、IL2CPP：`NotRun`，因为流水线已在环境预检处停止；
- 远端 Commit SHA Smoke：`NotRun`；
- 不可变 Tag 与 Tag URL Smoke：`NotRun`。

因此 `0.2.0-pre.2` 当前仍是 Official Target 下的未发布候选。当前候选的 Consumer 全门禁、IL2CPP、远端 Commit、不可变 Tag 与 Tag URL 证据全部完成前，不得登记为 Officially Supported Release。

## 人工验收

自动门禁不替代最终项目中的字体、布局、动画手感、点击面积、不同分辨率、手柄设备和真实资源系统验收。接入项目仍应使用最终 Prefab 做鼠标、键盘、手柄、Cancel、焦点恢复和 Input Zone 的可视检查。
