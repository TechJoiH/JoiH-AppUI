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

## Passed、Failed 与 Blocked

- `Passed`：当前候选、当前环境和当前步骤存在完整成功证据；
- `Failed`：测试断言、Binding、编译或 Player Build 已运行并确认失败；
- `Blocked`：门禁因环境缺失或超时未完成，例如缺少 Windows C++ toolchain；
- `NotRun`：尚未进入该阶段，例如 Tag 创建前的 Tag URL Smoke。

每个 Unity 子进程默认最多运行 120 秒。超时后工具终止该精确进程，当前流水线停止；不得继续后续步骤，也不得用旧候选的结果补齐本次报告。`Blocked` 不是 `Passed`，也不是 `Known Incompatible`。

## 报告与 Artifact

`pretag-report.json` 和正式 Release Report 都生成在候选 Commit 之外，至少绑定：

- Repository、Source Commit、Source Tree；
- Planned Tag 与正式阶段解析到的 Remote Tag；
- Package Version 与 `packageManifestSha256`；
- Unity、UGUI 与操作系统；
- EditMode、PlayMode、Binding、Mono、IL2CPP；
- Commit SHA Smoke 与 Tag Smoke；
- 每个 Gate 的证据文件和耗时。

正式报告必须通过 `git ls-remote origin refs/tags/<tag>` 解析远端 Tag，并确认 Commit/Tree 与候选一致。日志上传前先替换 Repository、Consumer 和 User Profile 路径，再扫描 `ghp_`、`github_pat_`、Authorization Header 和私钥标记；秘密审计失败时不创建 Release。

## 当前 `0.2.0-pre.2` 候选证据

截至当前本地实现阶段：

- Unity：`6000.0.25f1`；
- Package Manager、Basic Integration、Domain Reload：通过；
- Binding：0 Error、0 Warning、8 Info；
- EditMode：134/134 通过；
- PlayMode：17/17 通过；
- Windows x64 Mono Development Build：通过，0 Error、0 Warning；
- Windows x64 IL2CPP：`Blocked/MissingToolchain`，本机未发现 Unity 该版本可用的 Windows C++ toolchain；
- 远端 Commit SHA Smoke：`NotRun`；
- 不可变 Tag 与 Tag URL Smoke：`NotRun`。

因此 `0.2.0-pre.2` 当前仍是 Official Target 下的未发布候选。IL2CPP、远端 Commit、不可变 Tag 与 Tag URL 证据全部完成前，不得登记为 Officially Supported Release。

## 人工验收

自动门禁不替代最终项目中的字体、布局、动画手感、点击面积、不同分辨率、手柄设备和真实资源系统验收。接入项目仍应使用最终 Prefab 做鼠标、键盘、手柄、Cancel、焦点恢复和 Input Zone 的可视检查。
