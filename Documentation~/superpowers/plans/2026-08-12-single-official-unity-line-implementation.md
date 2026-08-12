# Single Official Unity Line Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 Joi.H AppUI 落实为只维护 Unity 6.0 / `6000.0` 的单一官方发行线，同时提供可复现的外部 Consumer 验证、候选包快照、发布报告和社区移植教程；不增加第二条 Unity 兼容线，也不改变 AppUI Runtime/Core 公共协议。

**Architecture:** 官方源码、`package.json`、SemVer Tag 与完整发布门禁始终只有一份。仓库内 `Validation~/Unity6000.0Consumer/` 只是无缓存、无绝对路径的模板；发布工具从精确 Git Commit 导出候选包快照，再把模板复制到仓库外并物化 `file:` 或 Git URL 安装引用。Unity 批处理入口只使用 AppUI 公开程序集和消费项目自有 Adapter，生成真实页面、Popup、焦点列表、Binding、Scope 与 Lease 验证资产。完整报告与日志始终生成在候选 Commit 之外。

**Tech Stack:** Unity `6000.0.25f1`、C#、UGUI `2.0.0`、Unity Test Framework `1.4.5`、UPM、PowerShell 5.1、Git、GitHub CLI。

## Global Constraints

- 权威设计规格为 `Documentation~/superpowers/specs/2026-08-12-single-official-unity-line-design.md`。
- UPM Package ID 保持 `com.joih.appui`，根命名空间保持 `Joi.H.AppUI`。
- 官方唯一目标环境保持 Unity 6.0 / `6000.0`；理由是当前主要开发、真实项目和验证环境，不引用 Unity 上游 LTS 支持截止日期，也不自动跟随最新 LTS。
- 只维护一份 `package.json`、一条 `main`、一套 Consumer 和普通 SemVer Tag；不得增加 Unity 2022/2021 Profile、分支、包、Tag 或 CI 矩阵。
- 兼容状态严格使用 `Officially Supported`、`Community Verified`、`Community Port`、`Unsupported`、`Known Incompatible` 五种；`Official Target` 是独立概念。
- `Community Verified` 只索引外部证据，不产生官方包、Tag、CI、Bug 支持或 Release Gate。
- 不提前创建 `Runtime/Compatibility/`、`Editor/Compatibility/` 或空兼容类；只有真实、可复现的版本差异出现后再按独立设计实施。
- 不修改 `Runtime/`、`Editor/`、现有 asmdef 或公共 API；本计划只增加文档、验证模板和仓库外发布工具，并在发布阶段修改版本号与 Changelog。
- AppUI Core/Runtime 继续不提供 UniTask、Task、Awaitable、Coroutine、Resources 或任何默认异步/资源实现；Consumer 使用项目自有 Adapter。
- 候选快照必须来自精确 Git Commit，不能复制脏工作树；验证报告不得在测试后写回候选 Commit。
- `Validation~/Unity6000.0Consumer/` 不直接作为 Unity 工程打开；必须先由工具复制到仓库外工作目录并生成 `Packages/manifest.json`。
- 不提交 `Library/`、`Temp/`、`Logs/`、`Obj/`、`UserSettings/`、Player Build、Package Cache、绝对路径、凭据或秘密。
- 所有 Unity 管理的 Consumer `Assets/` 文件和目录必须提交稳定 `.meta`；`Tools~`、`Documentation~`、`Validation~` 自身是 UPM 隐藏目录，不为其中非 Unity 模板文件额外制造无意义 Meta。
- 每个 Unity 外部进程最多等待 120 秒；超时立即停止该验证并把门禁记为 `Blocked`，不得沿用旧通过结果。
- 当前已知证据仍是 `0.2.0-pre.1` 的 EditMode `125/125`、PlayMode `11/11`、Mono Passed；IL2CPP、不可变 Tag、Post-tag Git URL 冒烟和正式 Consumer 模板尚未完成。
- 首个按本方案尝试发布的未复用版本固定为 `0.2.0-pre.2` / `v0.2.0-pre.2`；任何门禁失败都不得创建 Tag。
- 推送候选 Commit、创建/推送不可变 Tag、创建 GitHub Release 都是外部状态变更，执行到对应检查点时必须再次取得用户明确授权。
- 专利与开源授权不在本计划范围；不得添加或修改 `LICENSE`，不得替用户推断授权条款。
- 所有实现采用测试先行：先增加会失败的契约测试或审计，再实现最小行为并确认测试通过。
- 每个任务完成后创建独立本地提交；未经用户明确授权不推送。

---

## Current Baseline

- 当前分支：`codex/merge-neutral-operation-main`。
- 当前包版本：`0.2.0-pre.1`。
- 当前官方最低 Editor：`package.json` 中的 `"unity": "6000.0"`。
- 当前官方依赖只有 `com.unity.ugui: 2.0.0`。
- 当前外部实验工程 `D:/UGit/JoiH-AppUI-Lab/UnityTestProject` 只能用来提取经验，不能直接入库：它指向旧 worktree、导入的 Sample 目录仍为 `0.1.0-pre.1`，并包含本机构建和缓存。
- 当前仓库没有 `Tools~/`、`Validation~/`、官方 Tag 或 GitHub Actions Unity 矩阵。
- 当前 `Samples~/Basic Integration` 已提供显式 Callback Operation、Unity 主线程上下文和 In-memory Asset Provider，可用于对照 Consumer Adapter，但 Consumer 必须拥有自己的验证实现。
- 当前 Binding CI 入口为 `Joi.H.AppUI.Editor.Binding.UIBindingValidationCommandLine.ValidateAll`；Consumer 闭环还需要显式 Generate、重新编译、Bind、Validate 三阶段。

## Target File Structure

```text
Documentation~/
├── supported-unity-versions.md
├── community-unity-porting.md
├── validation.md
└── superpowers/plans/2026-08-12-single-official-unity-line-implementation.md

Tools~/Release/
├── AppUI.ReleaseTools.psm1
├── New-AppUICandidateSnapshot.ps1
├── New-AppUIConsumerWorkspace.ps1
├── Invoke-AppUIPreTagValidation.ps1
├── Invoke-AppUIGitInstallSmoke.ps1
├── New-AppUIReleaseReport.ps1
├── New-AppUIReleaseArtifacts.ps1
├── Test-AppUIReleaseReadiness.ps1
└── Tests/Invoke-AppUIReleaseToolsTests.ps1

Validation~/Unity6000.0Consumer/
├── .gitignore
├── README.md
├── Assets/
│   ├── AppUIConsumer/
│   │   ├── Runtime/
│   │   │   ├── Adapters/ConsumerOperationFactory.cs
│   │   │   ├── Adapters/ConsumerExecutionContext.cs
│   │   │   ├── Adapters/ConsumerAssetProvider.cs
│   │   │   ├── ConsumerRuntimeInstaller.cs
│   │   │   ├── Controllers/ConsumerBasicPageController.cs
│   │   │   ├── Controllers/ConsumerPopupController.cs
│   │   │   ├── Controllers/ConsumerBindingPageController.cs
│   │   │   ├── Controllers/ConsumerFocusListController.cs
│   │   │   └── Joi.H.AppUI.Validation.Consumer.asmdef
│   │   ├── Editor/
│   │   │   ├── AppUIConsumerBatchCommand.cs
│   │   │   ├── AppUIConsumerFixtureCommand.cs
│   │   │   ├── AppUIConsumerFixturePaths.cs
│   │   │   ├── AppUIConsumerBindingCommand.cs
│   │   │   ├── AppUIConsumerBuildCommand.cs
│   │   │   ├── AppUIConsumerSmokeCommand.cs
│   │   │   └── Joi.H.AppUI.Validation.Consumer.Editor.asmdef
│   │   └── Tests/
│   │       ├── EditMode/AppUIConsumerAdapterTests.cs
│   │       ├── EditMode/AppUIConsumerEditModeTests.cs
│   │       ├── EditMode/Joi.H.AppUI.Validation.Consumer.EditModeTests.asmdef
│   │       ├── PlayMode/AppUIConsumerPlayModeTests.cs
│   │       └── PlayMode/Joi.H.AppUI.Validation.Consumer.PlayModeTests.asmdef
│   └── matching stable .meta files for every listed Assets directory and asset
├── Packages/manifest.template.json
└── ProjectSettings/
    ├── ProjectVersion.txt
    ├── ProjectSettings.asset
    ├── EditorSettings.asset
    ├── EditorBuildSettings.asset
    ├── GraphicsSettings.asset
    ├── QualitySettings.asset
    ├── InputManager.asset
    ├── TagManager.asset
    └── TimeManager.asset
```

`Assets/AppUIConsumerGenerated/`、`Packages/manifest.json`、`Packages/packages-lock.json`、测试 XML、Build 输出和发布报告只存在于仓库外物化工作区，不进入模板。

## Tool Contracts

`AppUI.ReleaseTools.psm1` 公开以下 PowerShell 函数：

```powershell
Resolve-AppUIGitIdentity -RepositoryPath <path> -SourceRef <ref>
Test-AppUISemVerTag -Tag <v-semver>
Invoke-AppUIGitRemoteText -RepositoryPath <path> -Arguments <string[]> -TimeoutSeconds 30
Test-AppUIReleaseReadiness -RepositoryPath <path> -CandidateCommit <40-sha> -PlannedTag <v-semver>
Write-AppUIJson -Path <json> -Value <object>
Export-AppUICandidateSnapshot -RepositoryPath <path> -SourceRef <ref> -DestinationPath <new-dir>
Test-AppUICandidateSnapshot -PackageRoot <path> -IdentityPath <json> -ManifestPath <json>
New-AppUIConsumerWorkspace -TemplatePath <path> -DestinationPath <new-dir> -PackageReference <file-or-git-url>
Test-AppUIPackagePolicy -RepositoryPath <path> -SourceRef <ref>
Invoke-AppUIProcess -FilePath <exe> -Arguments <string[]> -TimeoutSeconds 120
Invoke-AppUIUnityProcess -UnityPath <exe> -ProjectPath <path> -Arguments <string[]> -TimeoutSeconds 120
Test-AppUIBuildEnvironment -UnityPath <exe> -ExpectedUnityVersion 6000.0.25f1
Read-AppUINUnit3Result -Path <xml>
New-AppUIReleaseReport -IdentityPath <json> -EvidenceRoot <path> -OutputPath <json> -PlannedTag <v-semver>
Protect-AppUILog -InputPath <path> -OutputPath <path> -RepositoryPath <path> -ConsumerPath <path> -UserProfilePath <path>
Test-AppUIArtifactSecrets -Path <path>
Test-AppUIArtifactLocalPaths -Path <path>
New-AppUISanitizedLogArchive -InputDirectory <path> -OutputArchive <zip>
New-AppUIReleaseArtifacts -SourceDirectory <path> -OutputDirectory <new-dir> -Version <semver>
```

候选目录固定结构：

```text
<run-root>/candidate/
├── package/                         # git archive 导出的只读包快照
└── evidence/
    ├── candidate-identity.json
    ├── package-manifest.json
    └── package-manifest.canonical.txt
```

规范化 Manifest 每行固定为 UTF-8（无 BOM）和 LF：

```text
<pathUtf8ByteLength>:<normalized/path>\t<gitMode>\t<lowercaseFileSha256>\n
```

先按 `normalized/path` 使用 `StringComparer.Ordinal` 排序，再对整份 canonical 文件的原始 UTF-8 字节计算 `packageManifestSha256`。`candidate-identity.json` 固定包含 `repository`、`sourceCommit`、`sourceTree`、`packageVersion`、`packageManifestSha256`、`generatedAtUtc`。

## Execution Checkpoints

```text
Tasks 1-7：本地文档、模板、工具、测试与候选提交
        ↓
Task 8：本地 Pre-tag 完整门禁
        ↓ Passed
用户授权 Push Candidate Commit
        ↓
Task 9：远端 Commit SHA Git URL 冒烟
        ↓ Passed
用户授权 Immutable Tag + GitHub Release
        ↓
Task 10：Tag URL 冒烟、Release Artifact、主分支证据索引
```

任何箭头前的必需门禁失败都停止后续发布动作；失败不会降级成警告。

当前实施状态：`pre.2` 因 Tag smoke 命令未导出停止；`pre.3` 修复后通过 Pre-tag、Commit 与 Tag smoke，但正式 Artifact 路径审计拒绝了验证 Run Root 绝对路径。两个不可变 Tag 均保留且没有 GitHub Release。恢复流程已转入 `0.2.0-pre.4`：用显式 `ValidationRootPath` 脱敏已知验证根，同时保持未知机器路径严格拒绝；再从新的 Commit、Tree 与 Run Root 重跑 Tasks 8-10。用户已授权连续执行剩余完整计划，不再逐项等待 Push、Tag 与 Release 授权，但任一门禁失败仍立即停止对应版本。

远端 `ls-remote` 使用 30 秒有界进程；连接或远端失败必须报告 `Blocked/RemoteUnavailable`，超时报告 `Blocked/Timeout`。未拿到成功的远端响应时，不得推断为 `NotPushed` 或 Tag 未占用。

---

### Task 1: Publish the single-line support and community-port policy

**Files:**

- Create: `Documentation~/supported-unity-versions.md`
- Create: `Documentation~/community-unity-porting.md`
- Modify: `README.md`
- Modify: `Documentation~/index.md`
- Modify: `Documentation~/getting-started.md`
- Modify: `Documentation~/architecture.md`
- Modify: `Documentation~/faq.md`
- Modify: `Documentation~/validation.md`
- Modify: `CONTRIBUTING.md`
- Modify: `CHANGELOG.md`

**Interfaces:**

- Consumes: 已确认设计中的 Official Target、五级状态、Community Verified 证据模型和当前验证边界。
- Produces: 面向未知项目背景 Unity 开发者的唯一兼容政策入口和可独立执行的社区移植教程。
- Does not change: `Runtime/`、`Editor/`、asmdef、`package.json` 或公共 API。

- [x] **Step 1: Run a failing documentation contract audit**

在仓库根执行：

```powershell
$required = @(
  'Documentation~/supported-unity-versions.md',
  'Documentation~/community-unity-porting.md'
)
$missing = $required | Where-Object { -not (Test-Path -LiteralPath $_) }
if ($missing.Count -gt 0) { throw "Missing policy docs: $($missing -join ', ')" }
```

Expected: 失败并列出两个尚不存在的文件，证明测试命中真实缺口。

- [x] **Step 2: Write `supported-unity-versions.md` with mutually exclusive states**

固定结构：

1. Official Target：Unity 6.0 / `6000.0`，并使用“不与 Unity 当前最新 LTS 自动同步”的稳定表述；
2. Officially Supported Releases：初始写“暂无完成全部门禁的不可变官方 Tag”；
3. Community Verified Evidence Index：初始为空，并给出外链记录格式；
4. Community Port：Unity 2022.3、Unity 2021.3；
5. Unsupported：未列入其他状态的版本以及未经验证的后续 Unity 6 技术线；
6. Known Incompatible：初始为空，只有可复现证据才能加入；
7. 五种状态定义、转换条件与证据要求。

不得把 `Official Target` 写成第六种兼容状态，不得把 `Unsupported` 写成“确认不能运行”。

- [x] **Step 3: Write an independently executable community porting guide**

`community-unity-porting.md` 按以下真实流程写作：

```text
选择最近的稳定官方 Tag
→ Fork
→ 建 community/unity-2022.3 分支
→ 只在 Fork 修改 package.json 的 unity 与依赖
→ 用目标 Unity 建干净 Consumer
→ 解决 asmdef / UGUI / TMP / Editor API 差异
→ 仅在真实差异出现时集中 Compatibility
→ 保护序列化字段、enum 数值和 Meta GUID
→ EditMode / PlayMode / Binding / Mono / IL2CPP
→ 发布 community-unity2022.3-vX.Y.Z Tag
→ 提交 Community Verified 外部证据链接
```

教程必须让用户从目标 Unity 新工程的 `Packages/packages-lock.json` 选择兼容的 UGUI/TMP 版本，不替用户指定未经验证的旧版实现；示例清单明确属于社区 Fork，不得复制回官方 `main`。

- [x] **Step 4: Align README and the public documentation path**

README 增加：

- 唯一官方目标环境；
- 当前 `0.2.0-pre.1` 仍是候选，不宣称 Officially Supported Release；
- 五级状态入口；
- `vX.Y.Z` 普通 Tag 安装格式；
- 真实项目不要跟随 `main`；
- Unity 2022.3/2021.3 Community Port 入口；
- Community Verified 不等于官方维护；
- IL2CPP、不可变 Tag 和 Post-tag 冒烟仍缺失。

`getting-started.md` 顶部只增加其他 Unity 版本教程链接，正文继续只讲 Unity 6；`architecture.md` 补充环境差异属于边界适配；`index.md` 注册两篇新文档。

- [x] **Step 5: Expand FAQ and contribution rules**

FAQ 必须逐项回答设计规格第 14.7 节的十个问题。`CONTRIBUTING.md` 增加兼容 PR 审查顺序、Community Verified 必需证据、禁止官方旧版分支/Tag/清单和版本宏散布规则。

- [x] **Step 6: Record the policy change without claiming a release**

在 `CHANGELOG.md` 的 `Unreleased` 中记录单一官方 Unity 线、社区移植文档和计划中的 Consumer 门禁；不要把 `0.2.0-pre.2` 写成已发布，也不要创建 Tag。

- [x] **Step 7: Run link, vocabulary and temporal-language audits**

```powershell
$markdown = Get-ChildItem README.md,CONTRIBUTING.md,CHANGELOG.md,Documentation~ -Recurse -File -Filter *.md
$text = ($markdown | Get-Content -Encoding UTF8) -join "`n"
if ($text -match '仍处于 Unity 官方支持周期|当前最新 LTS') { throw 'Temporal LTS claim found.' }
if (-not ($text -match 'Known Incompatible')) { throw 'Known Incompatible is missing.' }
if (-not ($text -match 'Community Verified')) { throw 'Community Verified is missing.' }
if (-not ($text -match 'Community Port')) { throw 'Community Port is missing.' }
```

再逐个解析相对 Markdown 链接并确认目标存在，检查代码围栏成对。Expected: 全部通过；搜索 `unity2022`、`unity6` Tag 命名示例不得出现官方双 Tag 方案。

- [x] **Step 8: Verify the docs-only boundary and commit Task 1**

```powershell
git diff --name-only
git diff --check
git add README.md CONTRIBUTING.md CHANGELOG.md Documentation~
git commit -m "Document single Unity support policy"
```

Expected: diff 中不包含 `Runtime/`、`Editor/`、asmdef 或 `package.json`。

---

### Task 2: Build deterministic candidate snapshot identity

**Files:**

- Create: `Tools~/Release/AppUI.ReleaseTools.psm1`
- Create: `Tools~/Release/New-AppUICandidateSnapshot.ps1`
- Create: `Tools~/Release/Tests/Invoke-AppUIReleaseToolsTests.ps1`

**Interfaces:**

- Consumes: 一个 Git 仓库路径和任意可解析的 Commit Ref。
- Produces: 来自精确 Commit 的只读候选包目录、规范化内容 Manifest 与候选身份 JSON。
- Invariant: 脏工作树和未跟踪文件永远不能进入候选快照。

- [x] **Step 1: Write failing snapshot tests before the module exists**

测试脚本使用临时 Git 仓库建立以下用例：

1. Commit `package.json` 与 `Runtime/A.cs`；
2. Commit 后修改 `A.cs` 并增加未跟踪 `secret.txt`；
3. 从旧 Commit 导出两次不同目录；
4. 断言两个 `packageManifestSha256` 相等；
5. 断言候选仍是 Commit 内容且没有 `secret.txt`；
6. 创建第二个内容不同的 Commit，断言 Manifest Hash 改变；
7. 断言现有目标目录会被拒绝而不是递归覆盖。

运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools~/Release/Tests/Invoke-AppUIReleaseToolsTests.ps1 -TestGroup Snapshot
```

Expected: 因 `AppUI.ReleaseTools.psm1` 或函数不存在而失败。

- [x] **Step 2: Implement Git identity resolution**

`Resolve-AppUIGitIdentity` 必须：

- 通过 `git rev-parse <ref>^{commit}` 得到 40 位 `sourceCommit`；
- 通过 `git rev-parse <commit>^{tree}` 得到 `sourceTree`；
- 从该 Commit 的 `package.json` 读取版本，而不是读取工作树；
- 从 `origin` URL 规范化得到 `TechJoiH/JoiH-AppUI`；
- 对非 Commit、缺 `package.json`、版本为空或 Git 命令失败明确抛错。

- [x] **Step 3: Implement archive export from the tracked tree**

`Export-AppUICandidateSnapshot` 使用 `git archive --format=zip <sourceCommit>` 导出到新目录，再由 .NET `ZipArchive` 展开到 `candidate/package/`。Windows `bsdtar` 会按本机代码页损坏 UTF-8 Git 路径，因此不得用于候选解压。禁止 `Copy-Item $RepositoryPath`。目标目录必须不存在；工具只创建自己的新目录，不删除未知路径。

- [x] **Step 4: Implement the normalized content manifest**

使用 `git ls-tree -rz --full-tree <sourceCommit>` 的 NUL 分隔输出获取 Git mode 和原始相对路径，避免空格、Tab 或非 ASCII 文件名被错误切分；对快照文件逐一算 SHA-256。路径转 `/`，按 `StringComparer.Ordinal` 排序，按本计划定义的 length-prefixed 行格式写 `package-manifest.canonical.txt`，再计算总 Hash。

`package-manifest.json` 保存每项的 `path`、`gitMode`、`sha256`；`candidate-identity.json` 保存候选身份。所有 JSON UTF-8 无 BOM、稳定属性顺序、路径不包含本机根目录。

- [x] **Step 5: Add a thin command wrapper**

`New-AppUICandidateSnapshot.ps1` 只负责参数校验、导入模块、调用 `Export-AppUICandidateSnapshot` 和把 identity 输出到 Console；核心逻辑不得复制到脚本。

示例：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools~/Release/New-AppUICandidateSnapshot.ps1 `
  -RepositoryPath (Get-Location).Path `
  -SourceRef HEAD `
  -DestinationPath 'D:\UGit\JoiH-AppUI-Lab\release-work\snapshot-contract'
```

- [x] **Step 6: Run snapshot tests and a real-repository smoke**

Expected: 临时仓库用例全部通过；真实仓库快照的 `package/package.json` 版本等于 Commit 中的版本，未包含 `.git`、`.worktrees` 或未跟踪计划文件。

- [x] **Step 7: Commit Task 2**

```powershell
git add Tools~/Release/AppUI.ReleaseTools.psm1 Tools~/Release/New-AppUICandidateSnapshot.ps1 Tools~/Release/Tests/Invoke-AppUIReleaseToolsTests.ps1
git commit -m "Add deterministic AppUI candidate snapshots"
```

---

### Task 3: Materialize isolated consumers and enforce static package gates

**Files:**

- Modify: `Tools~/Release/AppUI.ReleaseTools.psm1`
- Create: `Tools~/Release/New-AppUIConsumerWorkspace.ps1`
- Modify: `Tools~/Release/Tests/Invoke-AppUIReleaseToolsTests.ps1`

**Interfaces:**

- Consumes: Consumer 模板、候选 `file:` 路径或 Git URL、新的仓库外目标目录。
- Produces: 可由 Unity 直接打开的外部 Consumer 工作区。
- Produces: `Test-AppUIPackagePolicy` 的机器可读结果；任一 Error 返回非零退出码。

- [x] **Step 1: Add failing materialization and static-policy tests**

新增用例：

- 模板只有 `Packages/manifest.template.json`，物化后才出现 `manifest.json`；
- `__APPUI_PACKAGE_REFERENCE__` 被 JSON 安全替换；
- `file:` 输入转为绝对正斜杠路径；Git URL 保持原值；
- 模板文件自身无变化；
- 目标目录已存在时拒绝；
- 模板出现 `Library/`、`UserSettings/`、Windows 绝对路径或 `file:../../package` 时拒绝；
- 生产边界出现 `UniTask`、`Sirenix`、`ResourcesUIAssetProvider`、`Annals` 或 `GameFramework` 时失败；
- `package.json.unity != 6000.0` 或依赖不只 `com.unity.ugui: 2.0.0` 时失败；
- Unity 导入目录中缺 `.meta` 或存在孤儿 `.meta` 时失败；
- `UNITY_2021`、`UNITY_2022`、`UNITY_6000` 出现在 Core/Runtime/Binding 生成路径时失败。

运行 `-TestGroup Consumer,Policy`。Expected: 新函数不存在导致失败。

- [x] **Step 2: Implement workspace materialization**

`New-AppUIConsumerWorkspace` 先完整验证模板，再复制到新目录，最后用 `System.Text.Json` 不可用时的 PowerShell JSON 读写方式生成 `Packages/manifest.json`。不要做字符串级 JSON 拼接。模板 Token 必须恰好出现一次。

物化后的 Manifest 固定包括：

```json
{
  "dependencies": {
    "com.joih.appui": "__APPUI_PACKAGE_REFERENCE__",
    "com.unity.test-framework": "1.4.5",
    "com.unity.ugui": "2.0.0"
  },
  "testables": ["com.joih.appui"]
}
```

- [x] **Step 3: Implement static package and repository policy checks**

`Test-AppUIPackagePolicy` 返回包含 `name`、`status`、`details` 的检查集合，并覆盖：

- Commit 中 `package.json` 的 name/version/unity/dependencies；
- Runtime/Core/Editor 的第三方和宿主 Token；
- 无官方 Unity 版本分支/Tag 命名配置文件；
- Compatibility 目录只允许在真实文件存在时另行审核，当前不得为空壳；
- Unity 导入目录 Meta 完整性与 GUID 重复；
- 根/Documentation Markdown 相对链接；
- 模板禁止目录、绝对路径和秘密 Token；
- `git diff --check` 等价的空白错误。

生产 Token 扫描限定在生产源与 asmdef，避免 FAQ 对 UniTask/Odin 的解释造成假阳性。

- [x] **Step 4: Add the command wrapper and rerun all PowerShell tests**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools~/Release/New-AppUIConsumerWorkspace.ps1 `
  -TemplatePath Validation~/Unity6000.0Consumer `
  -DestinationPath 'D:\UGit\JoiH-AppUI-Lab\release-work\consumer-contract' `
  -PackageReference 'file:D:/UGit/JoiH-AppUI-Lab/release-work/snapshot-contract/candidate/package'
```

在 Task 4 模板尚未存在时，模块单元测试用临时模板验证功能；真实模板 smoke 延后到 Task 4。

- [x] **Step 5: Commit Task 3**

```powershell
git add Tools~/Release
git commit -m "Add isolated AppUI consumer materialization"
```

---

### Task 4: Create the minimal Unity 6000.0 Consumer template

**Files:**

- Create: `Validation~/Unity6000.0Consumer/.gitignore`
- Create: `Validation~/Unity6000.0Consumer/README.md`
- Create: `Validation~/Unity6000.0Consumer/Packages/manifest.template.json`
- Create: `Validation~/Unity6000.0Consumer/ProjectSettings/ProjectVersion.txt`
- Create: `Validation~/Unity6000.0Consumer/ProjectSettings/ProjectSettings.asset`
- Create: `Validation~/Unity6000.0Consumer/ProjectSettings/EditorSettings.asset`
- Create: `Validation~/Unity6000.0Consumer/ProjectSettings/EditorBuildSettings.asset`
- Create: `Validation~/Unity6000.0Consumer/ProjectSettings/GraphicsSettings.asset`
- Create: `Validation~/Unity6000.0Consumer/ProjectSettings/QualitySettings.asset`
- Create: `Validation~/Unity6000.0Consumer/ProjectSettings/InputManager.asset`
- Create: `Validation~/Unity6000.0Consumer/ProjectSettings/TagManager.asset`
- Create: `Validation~/Unity6000.0Consumer/ProjectSettings/TimeManager.asset`
- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Runtime/Joi.H.AppUI.Validation.Consumer.asmdef`
- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Runtime/Adapters/ConsumerOperationFactory.cs`
- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Runtime/Adapters/ConsumerExecutionContext.cs`
- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Runtime/Adapters/ConsumerAssetProvider.cs`
- Create: matching `.meta` files for every `Assets/` directory and asset above.
- Modify: `Tools~/Release/Tests/Invoke-AppUIReleaseToolsTests.ps1`

**Interfaces:**

- Consumes: `IUIOperationFactory`、`IUIOperationSource<T>`、`IAppUIExecutionContext`、`IUIAssetProvider`、`UIAssetLease` 公开协议。
- Produces: 不依赖 package Tests、internal 类型或第三方异步包的消费项目自有 Adapter。
- Does not produce: 场景、Prefab、Definition 或生成 Binding；这些由 Task 5 在仓库外工作区生成。

- [x] **Step 1: Extend the policy test with the exact template contract**

测试先断言目标文件、Unity `6000.0.25f1`、UGUI `2.0.0`、Test Framework `1.4.5`、`manifest.template.json` Token、禁止目录和 Meta 完整性。Expected: 模板不存在而失败。

- [x] **Step 2: Create the stripped project settings from the proven consumer**

从 `D:/UGit/JoiH-AppUI-Lab/UnityTestProject` 只提取列出的 ProjectSettings，并做以下清理：

- 保持 `ProjectVersion.txt` 为 `6000.0.25f1 (4859ab7b5a49)`；
- Version Control 采用 Visible Meta Files，Asset Serialization 采用 Force Text；
- Windows x64 为构建目标；
- 场景列表由生成命令写入仓库外工作区，不提交本机 Scene GUID；
- 删除服务 ID、组织 ID、云项目 ID 和本机状态；
- 不复制 `Packages/packages-lock.json`。

- [x] **Step 3: Implement the project-owned manual operation factory**

`ConsumerOperationFactory` 提供确定性 `IUIOperationSource<T>`：

- `Create<T>(AppUIOperationDescriptor)` 返回可由验证代码 `Succeed`、`Fail`、`Cancel` 的 Source；
- 订阅终态后立即回放，终态只允许设置一次；
- 不使用 Task、UniTask、Awaitable 或 Coroutine；
- 支持保留一个 Pending Operation 用于晚到结果/取消测试。

Runtime asmdef 固定引用 `Joi.H.AppUI.Core`、`Joi.H.AppUI.Runtime`、`UnityEngine.UI`，不得引用 package Tests 或 Sample asmdef；Adapter 类型全部位于 `Joi.H.AppUI.Validation.Consumer` 命名空间。

- [x] **Step 4: Implement execution context and lease-tracking asset provider**

`ConsumerExecutionContext` 捕获 Unity 主线程 ID，`Post(Action)` 在主线程立即执行，非主线程入队并由验证驱动显式 Drain。`ConsumerAssetProvider` 以字典注册 `UnityEngine.Object`，每次成功 Load 返回独立 `UIAssetLease`，记录 `LoadCount`、`ReleaseCount`、Cancel/Failure，并允许测试显式完成 Pending Load。

- [x] **Step 5: Write the template README**

README 明确：

- 这是复制用 Consumer 模板，不是 AppUI 开发工程；
- 必须通过 `New-AppUIConsumerWorkspace.ps1`；
- 模板不含有效 `manifest.json`、缓存和本机路径；
- 生成资产只存在仓库外；
- 官方验证固定 Unity `6000.0.25f1`；
- 用户自行移植其他 Unity 版本请读 Community Guide。

- [x] **Step 6: Materialize a real workspace and confirm domain reload**

从当前已提交 Commit 创建快照，再物化模板到新的外部目录。用 Unity 打开：

```powershell
& 'C:\Unity\Unity 6000.0.25f1\Editor\Unity.exe' -batchmode -nographics -quit `
  -projectPath 'D:\UGit\JoiH-AppUI-Lab\release-work\task4-consumer' `
  -logFile 'D:\UGit\JoiH-AppUI-Lab\release-work\task4-domain-reload.log'
```

Expected: Package Manager 解析、Core/Runtime/Consumer Adapter 编译成功；没有从旧 `UnityTestProject` 或 worktree 穿透加载。

- [x] **Step 7: Run policy tests and commit Task 4**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools~/Release/Tests/Invoke-AppUIReleaseToolsTests.ps1 -TestGroup Consumer,Policy
git add Validation~ Tools~/Release/Tests/Invoke-AppUIReleaseToolsTests.ps1
git commit -m "Add Unity 6000 consumer template"
```

---

### Task 5: Add generated fixtures, Binding closure and consumer integration tests

**Files:**

- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Runtime/Controllers/ConsumerBasicPageController.cs`
- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Runtime/Controllers/ConsumerPopupController.cs`
- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Runtime/Controllers/ConsumerBindingPageController.cs`
- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Editor/Joi.H.AppUI.Validation.Consumer.Editor.asmdef`
- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Editor/AppUIConsumerFixtureCommand.cs`
- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Editor/AppUIConsumerBindingCommand.cs`
- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Editor/AppUIConsumerBuildCommand.cs`
- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Editor/AppUIConsumerSmokeCommand.cs`
- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Tests/EditMode/Joi.H.AppUI.Validation.Consumer.EditModeTests.asmdef`
- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Tests/EditMode/AppUIConsumerEditModeTests.cs`
- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Tests/PlayMode/Joi.H.AppUI.Validation.Consumer.PlayModeTests.asmdef`
- Create: `Validation~/Unity6000.0Consumer/Assets/AppUIConsumer/Tests/PlayMode/AppUIConsumerPlayModeTests.cs`
- Create: matching `.meta` files.

**Interfaces:**

- Consumes: AppUI 公开 Runtime/Editor asmdef、Task 4 Consumer Adapter、Unity Test Framework。
- Produces: 仓库外真实 EventSystem、Canvas、Layer、Prefab、Definition、Registry、Profile、Binding 和测试场景。
- Produces batch entry points:
  - `Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerFixtureCommand.ImportBasicIntegration`
  - `Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerFixtureCommand.CreateFixturesAndGenerateBindings`
  - `Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerBindingCommand.BindAndValidate`
  - `Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerBuildCommand.BuildMono`
  - `Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerBuildCommand.BuildIl2Cpp`
  - `Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerSmokeCommand.Run`

- [x] **Step 1: Write failing EditMode and PlayMode tests first**

EditMode 测试覆盖：

- 候选包由 `PackageInfo` 解析为 `com.joih.appui` 且版本等于环境变量 `APPUI_EXPECTED_PACKAGE_VERSION`；
- Consumer Operation 终态一次性和回放语义；
- Asset Lease 成功/失败/取消/Shutdown 只释放一次；
- 生成 Binding Page 的 `B_TitleText`、`B_ConfirmButton` 已写入序列化字段且 Validate 无 Error；
- Focus List 的默认节点和上下移动只使用公开 Focus API。

PlayMode 测试覆盖：

- Basic Page 完成 Initialize/Open/Refresh/Close/Release；
- Popup 位于 Popup/Modal Layer，Cancel 与 Background Click 遵守 Definition；
- Popup 阻挡时世界输入通道不可通过，关闭后恢复；
- SceneScope 结束会关闭并释放页面；
- Pending Load 在 Scope 结束后晚到成功，不重新显示页面且 Lease 释放一次；
- EventSystem、GraphicRaycaster 和 Focus List 产生真实选中对象变化。

首次物化并运行 Expected: 因 Controller、Fixture 或生成资产不存在而编译/测试失败。

- [x] **Step 2: Implement controllers and deterministic fixture generation**

Controller 只记录公开生命周期回调和刷新数据。`ConsumerBindingPageController` 必须是 `partial`，手写文件中不声明生成字段。

Fixture 命令使用 `AssetDatabase`、`PrefabUtility`、`EditorSceneManager` 和公开 AppUI 类型在外部工作区的 `Assets/AppUIConsumerGenerated/` 创建：

- `BasicPage.prefab` + `UIPageDefinition`；
- `Popup.prefab` + Modal/Popup Definition；
- 带真实 Button/ScrollRect/EventSystem 的 `FocusList.prefab`；
- 带 `B_TitleText`、`B_ConfirmButton` 的 `BindingPage.prefab`；
- Registry、LayerSettings、RuntimeProfile；
- `UIBindingSettings`，其 Page Registry 指向上述 Registry，Build Preprocess 保持关闭，由显式 Binding 命令验证；
- `AppUIConsumerValidation.unity` 与 EditorBuildSettings 场景条目。

生成必须幂等：每次先只删除 `Assets/AppUIConsumerGenerated/` 这个精确受控目录，再重建；不得删除模板源目录。

- [x] **Step 3: Implement the three-stage Binding pipeline**

第一轮 `CreateFixturesAndGenerateBindings` 对 Prefab Scope 调用 `UIBindingGenerator.Generate` 并写阶段 JSON，然后退出，触发下轮 Unity 编译 `.Bindings.cs`。第二轮 `BindAndValidate` 调用 `UIBindingPrefabBinder.Bind`、`UIBindingValidator.ValidateScope` 和 `UIBindingValidateAllRunner.ValidateAll`，把现有 Binding JSON 报告写到 `APPUI_VALIDATION_OUTPUT`。

任何 Generate/Bind/Validate Error 都抛异常并让 Unity 返回非零；不得在 Validate 阶段自动修复。

- [x] **Step 4: Implement Sample import and minimum smoke entry point**

`ImportBasicIntegration` 使用 `UnityEditor.PackageManager.UI.Sample.FindByPackage("com.joih.appui", expectedVersion)` 找到 `Basic Integration` 并导入。下一次 Unity invocation 负责验证导入 Sample 编译。

`AppUIConsumerSmokeCommand.Run` 读取已生成场景，通过公开 Runtime Host 完成 Initialize/Open/Close，验证 `PackageInfo.version`，写 `git-install-smoke.json` 后以明确退出码结束。

- [x] **Step 5: Implement Mono and IL2CPP build entry points**

统一私有 `Build(ScriptingImplementation backend, string label)`：

- Windows x64 Development Build；
- 场景只使用生成的 Validation Scene；
- 输出位于外部工作区 `Builds/WindowsMono` 或 `Builds/WindowsIL2CPP`；
- 将 `result`、`totalSize`、`totalTime`、`unityVersion`、`backend`、`outputRelativePath` 写入 `APPUI_VALIDATION_OUTPUT` 下 JSON；
- 失败抛异常，绝不只写 Console Warning。

- [x] **Step 6: Execute the consumer pipeline manually in isolated invocations**

顺序固定：

```text
ImportBasicIntegration
→ Domain Reload
→ CreateFixturesAndGenerateBindings
→ Domain Reload / compile generated partial
→ BindAndValidate
→ EditMode
→ PlayMode
→ BuildMono
→ BuildIl2Cpp
```

每个 Unity 进程使用同一外部 Consumer 路径和独立 Log，最长 120 秒。Expected: 除当前机器已知可能阻塞的 IL2CPP 环境外，其余门禁通过；IL2CPP 若阻塞必须保留为 Blocked，不伪造 Pass。

- [x] **Step 7: Commit tests, fixtures and commands**

```powershell
git add Validation~/Unity6000.0Consumer
git commit -m "Add AppUI consumer integration gates"
```

---

### Task 6: Orchestrate release gates and generate external evidence

**Files:**

- Modify: `Tools~/Release/AppUI.ReleaseTools.psm1`
- Create: `Tools~/Release/Invoke-AppUIPreTagValidation.ps1`
- Create: `Tools~/Release/Invoke-AppUIGitInstallSmoke.ps1`
- Create: `Tools~/Release/New-AppUIReleaseReport.ps1`
- Create: `Tools~/Release/New-AppUIReleaseArtifacts.ps1`
- Create: `Tools~/Release/Test-AppUIReleaseReadiness.ps1`
- Modify: `Tools~/Release/Tests/Invoke-AppUIReleaseToolsTests.ps1`

**Interfaces:**

- Consumes: 精确候选 Commit、Consumer 模板、Unity 可执行文件和新的外部 Run Root。
- Produces: `pretag-report.json`、NUnit XML、Binding JSON、Mono/IL2CPP JSON、Git smoke JSON、规范化 Hash 清单和已脱敏日志。
- Invariant: 报告和 Artifact 不进入候选包 Tree，不包含用户名、凭据或本机绝对路径。

- [x] **Step 1: Add failing parser, timeout, report and redaction tests**

PowerShell 测试增加：

- NUnit3 XML passed/failed/skipped 解析；
- 缺 XML、失败 Test Case、格式错误时明确失败；
- Unity 子进程超过 120 秒时返回 `Blocked/Timeout`；
- Report 的 sourceCommit/tree/version/hash 不一致时拒绝；
- `plannedTag` 必须严格等于 `v` + package version；
- Log 中 repo、consumer、User Profile 路径被替换为 `<REPOSITORY>`、`<CONSUMER>`、`<USER_PROFILE>`；
- `ghp_`、`github_pat_`、Authorization Header、私钥标记导致 Artifact 审计失败；
- Pre-tag report 允许 `resolvedTag` 为空，但正式 report 必须解析为同一 Commit。

Expected: 新功能不存在而失败。

- [x] **Step 2: Implement the bounded Unity process runner**

`Invoke-AppUIUnityProcess` 使用 `Start-Process -PassThru`、显式参数数组和独立 Log；120 秒未退出则停止该精确 Process，返回 `Blocked`，并停止当前发布脚本。不得继续运行后续门禁或复用之前候选的结果。

- [x] **Step 3: Implement the Pre-tag orchestrator**

`Invoke-AppUIPreTagValidation.ps1` 参数：

```powershell
-RepositoryPath <repo>
-SourceCommit <40-sha>
-PlannedTag v0.2.0-pre.2
-UnityPath 'C:\Unity\Unity 6000.0.25f1\Editor\Unity.exe'
-RunRoot <new-external-directory>
```

执行顺序严格为：clean tree → version/tag → static policy → snapshot → materialize file consumer → Sample import → fixture/generate → bind/validate → EditMode → PlayMode → Mono → IL2CPP → evidence identity audit → external pretag report。任一项失败立即非零退出。

Clean tree 检查在 `SourceCommit == HEAD` 时要求 `git status --porcelain` 为空；若验证历史 Commit，则工作树状态只影响操作者告警，不影响 Commit 快照身份。

- [x] **Step 4: Implement Git URL smoke orchestration**

`Invoke-AppUIGitInstallSmoke.ps1` 接受 `-PackageReference`，只允许：

- `https://github.com/TechJoiH/JoiH-AppUI.git#<40-sha>`；
- `https://github.com/TechJoiH/JoiH-AppUI.git#v<semver>`。

脚本创建全新 Consumer，运行 Sample import、Domain Reload、fixture/generate、Binding 第二阶段和 `AppUIConsumerSmokeCommand.Run`。Commit URL 输出 `commit-git-install-smoke.json`，Tag URL 输出 `tag-git-install-smoke.json`；Tag 证据必须先通过远端 Tag 解析绑定到其真实 Commit/Tree。不得复用 Pre-tag 的 `Library` 或 packages-lock。

- [x] **Step 5: Implement external report generation and identity checks**

`New-AppUIReleaseReport.ps1` 从候选 Identity、NUnit XML、Binding JSON、Build JSON、Git smoke JSON组合报告。报告固定包含设计规格第 13.1 节字段，并附每个 Gate 的 `status`、`evidenceFile`、`durationMs`。

Pre-tag 报告中：

- `resolvedTag = null`；
- `commitGitInstallSmoke` 单独记录；
- `tagGitInstallSmoke = NotRun`；
- 只有 EditMode、PlayMode、Binding、Mono、IL2CPP 全 Passed 才允许进入 Commit Git smoke。

正式报告中 Tag 必须经 `git ls-remote origin refs/tags/v0.2.0-pre.2` 解析，并验证其 Commit/Tree 与候选一致。

- [x] **Step 6: Sanitize release artifacts and audit for secrets**

原始本地 Log 保留在操作者外部 Run Root；上传版本先逐文件脱敏，再压缩为 `appui-v0.2.0-pre.2-logs.zip`。`New-AppUIReleaseArtifacts.ps1` 从显式组装的外部证据目录生成恰好十个最终文件，并对九个文本制品和 ZIP 内条目执行秘密与本地绝对路径审计。报告只记录 Artifact 相对文件名，不记录本机绝对路径。任一审计失败时不创建 GitHub Release。

- [x] **Step 7: Run all PowerShell tests and a no-build dry run**

Dry run 使用 fixture XML/JSON 验证报告拼装，不调用 Unity；随后用当前 HEAD 运行到 `-StopAfter StaticPolicy` 和 `-StopAfter Snapshot`。Expected: 单元测试、候选 Identity 和静态门禁通过。

- [x] **Step 8: Commit Task 6**

```powershell
git add Tools~/Release
git commit -m "Add reproducible AppUI release gates"
```

---

### Task 7: Align public validation instructions and prepare `0.2.0-pre.2`

**Files:**

- Modify: `README.md`
- Modify: `Documentation~/index.md`
- Modify: `Documentation~/getting-started.md`
- Modify: `Documentation~/architecture.md`
- Modify: `Documentation~/editor-tools-validation.md`
- Modify: `Documentation~/validation.md`
- Modify: `Documentation~/supported-unity-versions.md`
- Modify: `Documentation~/community-unity-porting.md`
- Modify: `CONTRIBUTING.md`
- Modify: `CHANGELOG.md`
- Modify: `package.json`

**Interfaces:**

- Consumes: Tasks 2-6 实际存在的命令、路径、报告字段和 Consumer 行为。
- Produces: 用户可复制执行的安装/移植/验证命令和版本为 `0.2.0-pre.2` 的发布候选 Commit。
- Does not claim: Tag 已存在或 `0.2.0-pre.2` 已 Officially Supported。

- [x] **Step 1: Run a failing docs-to-tool contract audit**

脚本提取文档中的 `Tools~/Release/*.ps1`、`Validation~/Unity6000.0Consumer`、C# `-executeMethod` 名称并断言文件/类型存在；再断言 `package.json.version == 0.2.0-pre.2`。Expected: 版本仍为 `0.2.0-pre.1`，测试失败。

- [x] **Step 2: Rewrite validation docs around the external consumer**

`Documentation~/validation.md` 明确区分：

- 包内单元测试；
- Commit 快照 `file:` Consumer；
- pushed Commit SHA Git URL smoke；
- immutable Tag URL smoke；
- 外部报告与 Artifact；
- 环境 Blocked 与测试 Failed；
- 120 秒进程边界；
- Tag 不可移动规则。

将旧的手工 `UnityTestProject` 路径从公开流程移除。

- [x] **Step 3: Document exact install channels**

README/Getting Started 只推荐正式 Tag：

```text
https://github.com/TechJoiH/JoiH-AppUI.git#v0.2.0-pre.2
```

但在 Tag 创建前明确标注“计划中的下一候选版本，只有 Release 页面出现后才可按 Tag 安装”。保留本地开发 `file:` 作为贡献者说明，不让真实项目跟随 `main`。

- [x] **Step 4: Update package version and Changelog**

将 `package.json.version` 改为 `0.2.0-pre.2`，`unity` 保持 `6000.0`，依赖保持只有 UGUI `2.0.0`。Changelog 新增 `0.2.0-pre.2 - Unreleased`，记录政策文档、外部 Consumer、快照/报告/Tag 门禁；不写“已发布”。

- [x] **Step 5: Run all static, docs and PowerShell tests**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools~/Release/Tests/Invoke-AppUIReleaseToolsTests.ps1 -TestGroup All
git diff --check
```

Expected: 无断链、无工具名漂移、无 `main` 生产安装推荐、无 Unity 上游支持周期表述、无 Runtime/Editor 改动。

- [x] **Step 6: Commit the exact release candidate**

```powershell
git add README.md CONTRIBUTING.md CHANGELOG.md package.json Documentation~ Validation~ Tools~
git commit -m "Prepare Joi.H AppUI 0.2.0-pre.2 validation"
git status --short
```

Expected: 工作树干净。记录 `git rev-parse HEAD` 作为唯一候选 Commit；从此任何改动都使旧验证失效并需要新 Commit。

---

### Task 8: Complete all local Pre-tag Unity 6 gates

**Files:**

- Create outside repo: `D:/UGit/JoiH-AppUI-Lab/release-work/v0.2.0-pre.2-local/`
- Modify in repo: only when a failing gate first reproduces a real defect; fixes require regression test and a new candidate Commit.

**Interfaces:**

- Consumes: Task 7 的干净 `0.2.0-pre.2` candidate Commit。
- Produces: 与该 Commit/Tree/Manifest Hash 绑定的外部 `pretag-report.json` 和脱敏 Artifact。
- Gate: 必须全部 Passed 才能请求 Push Candidate 权限。

- [x] **Step 1: Preflight Unity and C++ build environment**

确认 Unity 路径恰为 `6000.0.25f1`。使用 `vswhere.exe` 查找包含 `Microsoft.VisualStudio.Component.VC.Tools.x86.x64` 的 VS 2022 Build Tools，并验证 `vcvars64.bat` 可解析；不要把 VS 2026 检测结果伪装成 VS 2022。

若缺 C++ Build Tools：记录 `IL2CPP = Blocked/MissingToolchain`，停止 Task 8，向用户提供 Visual Studio Installer 中“Desktop development with C++”、MSVC v143 和 Windows SDK 的安装步骤。工具安装是用户环境变更，取得授权后再执行/指导。

- [ ] **Step 2: Run the complete Pre-tag orchestrator**

```powershell
$candidate = git rev-parse HEAD
powershell -NoProfile -ExecutionPolicy Bypass -File Tools~/Release/Invoke-AppUIPreTagValidation.ps1 `
  -RepositoryPath (Get-Location).Path `
  -SourceCommit $candidate `
  -PlannedTag 'v0.2.0-pre.2' `
  -UnityPath 'C:\Unity\Unity 6000.0.25f1\Editor\Unity.exe' `
  -RunRoot 'D:\UGit\JoiH-AppUI-Lab\release-work\v0.2.0-pre.2-local'
```

Expected: Static、Package resolve、Domain Reload、Sample、Binding、EditMode、PlayMode、Mono、IL2CPP 全 Passed。

- [ ] **Step 3: Verify evidence identity and totals**

人工交叉检查：

- `sourceCommit` 等于 `$candidate`；
- `sourceTree` 等于 `git rev-parse "$candidate^{tree}"`；
- package version `0.2.0-pre.2`；
- Manifest Hash 与 Consumer 安装快照一致；
- NUnit failed 为 0；
- Binding Error 为 0；
- Mono/IL2CPP JSON 都 Passed；
- 报告没有用户目录、repo 绝对路径或秘密。

测试数量不硬编码为旧的 125/11；报告从当前 XML 读取并记录真实总数。

- [ ] **Step 4: Handle a real failure with candidate invalidation**

若任一 Gate 失败：先保留失败证据，在对应测试中增加最小回归用例，再修复并本地提交。新的 HEAD 是新候选；删除/放弃旧 Run Root 的发布资格，使用新的空 Run Root 从头运行全部 Gate。不得只重跑失败项后复用旧报告。

- [ ] **Step 5: Stop for explicit authorization to push the candidate**

向用户汇报 candidate SHA/tree、版本、各 Gate、Artifact 路径和剩余风险。只有用户明确回复允许推送后，才能执行 Task 9 的 `git push`。

---

### Task 9: Prove the pushed Commit installation before creating a Tag

**Files:**

- Create outside repo: `D:/UGit/JoiH-AppUI-Lab/release-work/v0.2.0-pre.2-commit-smoke/`
- No repository file changes unless the smoke exposes a defect.

**Interfaces:**

- Consumes: 已完成 Task 8 且经用户授权推送的 candidate Commit。
- Produces: 从 GitHub Commit SHA 安装的独立 smoke 报告。
- Gate: Commit SHA smoke Passed 后才能请求不可变 Tag 授权。

- [ ] **Step 1: Push only the tested candidate branch after authorization**

```powershell
$candidate = git rev-parse HEAD
git fetch origin main
git merge-base --is-ancestor origin/main $candidate
if ($LASTEXITCODE -ne 0) { throw 'Candidate is not a fast-forward of origin/main.' }
git push origin "${candidate}:refs/heads/main"
```

若远端 `main` 已前进，停止并重新同步/审查；不得 force push。推送后用 `git ls-remote origin refs/heads/main` 确认远端指向同一 candidate。

- [ ] **Step 2: Run a clean Git URL Commit smoke**

```powershell
$candidate = git rev-parse HEAD
$url = "https://github.com/TechJoiH/JoiH-AppUI.git#$candidate"
powershell -NoProfile -ExecutionPolicy Bypass -File Tools~/Release/Invoke-AppUIGitInstallSmoke.ps1 `
  -PackageReference $url `
  -ExpectedPackageVersion '0.2.0-pre.2' `
  -UnityPath 'C:\Unity\Unity 6000.0.25f1\Editor\Unity.exe' `
  -RunRoot 'D:\UGit\JoiH-AppUI-Lab\release-work\v0.2.0-pre.2-commit-smoke'
```

Expected: Git fetch、Package resolve、Domain Reload、Sample import/compile、Binding 两阶段和 Open/Close smoke 全 Passed；工程不访问本地 package 路径。

- [ ] **Step 3: Bind the Commit smoke result into the external report**

用 `New-AppUIReleaseReport.ps1 -Mode PreTag` 合并本地完整门禁与 Commit smoke。确认 `commitGitInstallSmoke = Passed`、`tagGitInstallSmoke = NotRun`，candidate identity 未变化。

- [ ] **Step 4: Stop for immutable Tag and GitHub Release authorization**

向用户明确：下一步会创建无法移动的 `v0.2.0-pre.2`、推送 Tag、执行 Tag URL smoke，并在通过后创建 GitHub Pre-release。没有明确授权不得执行 Task 10。

---

### Task 10: Publish the immutable Tag, verify it, and index official evidence

**Files:**

- Create outside repo: `D:/UGit/JoiH-AppUI-Lab/release-work/v0.2.0-pre.2-tag-smoke/`
- Create outside repo: `D:/UGit/JoiH-AppUI-Lab/release-work/v0.2.0-pre.2-release/`
- Create outside repo: release report, sanitized logs ZIP, XML, build summaries, hash list, release notes.
- Modify after successful GitHub Release: `Documentation~/supported-unity-versions.md`
- Modify after successful GitHub Release: `Documentation~/validation.md`
- Modify after successful GitHub Release: `README.md`
- Modify after successful GitHub Release: `CHANGELOG.md`

**Interfaces:**

- Consumes: 用户授权、Task 9 Passed candidate、未使用的 `v0.2.0-pre.2`。
- Produces: 不可变 Git Tag、GitHub Pre-release、Tag URL smoke、外部正式报告和主分支人类可读证据索引。
- Invariant: Post-tag 失败时永不移动/删除并重建同名 Tag；修复只能发布 `v0.2.0-pre.3`。

- [ ] **Step 1: Prove the Tag name is unused and create it on the tested Commit**

```powershell
$candidate = git rev-parse HEAD
if (git tag --list 'v0.2.0-pre.2') { throw 'Local tag already exists.' }
if (git ls-remote --tags origin refs/tags/v0.2.0-pre.2) { throw 'Remote tag already exists.' }
git tag -a v0.2.0-pre.2 $candidate -m 'Joi.H AppUI 0.2.0-pre.2'
git push origin refs/tags/v0.2.0-pre.2
```

解析 annotated tag 到 Commit，确认等于 `$candidate`。

- [ ] **Step 2: Run a completely new Tag URL smoke**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools~/Release/Invoke-AppUIGitInstallSmoke.ps1 `
  -PackageReference 'https://github.com/TechJoiH/JoiH-AppUI.git#v0.2.0-pre.2' `
  -ExpectedPackageVersion '0.2.0-pre.2' `
  -UnityPath 'C:\Unity\Unity 6000.0.25f1\Editor\Unity.exe' `
  -RunRoot 'D:\UGit\JoiH-AppUI-Lab\release-work\v0.2.0-pre.2-tag-smoke'
```

Expected: 全部 smoke Passed，解析版本和 candidate Commit/Tree 完全一致。

- [ ] **Step 3: Handle Post-tag failure without moving the Tag**

若失败：立即停止 Release 创建，保留 Tag 和失败报告，文档不得写 Officially Supported。修复进入 `0.2.0-pre.3`，重新执行 Tasks 7-10；绝不 `git tag -f`、删除远端 Tag 或重用版本号。

- [ ] **Step 4: Generate the final sanitized release evidence**

正式报告设置：

- `plannedTag = resolvedTag = v0.2.0-pre.2`；
- Tag Commit/Tree == candidate Commit/Tree；
- file snapshot Manifest Hash == 被完整门禁验证的 Hash；
- Commit Git smoke 和 Tag Git smoke 都 Passed；
- 所有 Artifact 使用相对名并通过秘密/路径扫描。

Release Artifact 至少包括：

```text
appui-v0.2.0-pre.2-release-report.json
appui-v0.2.0-pre.2-package-manifest.json
appui-v0.2.0-pre.2-editmode.xml
appui-v0.2.0-pre.2-playmode.xml
appui-v0.2.0-pre.2-binding-validation.json
appui-v0.2.0-pre.2-mono-build.json
appui-v0.2.0-pre.2-il2cpp-build.json
appui-v0.2.0-pre.2-commit-smoke.json
appui-v0.2.0-pre.2-tag-smoke.json
appui-v0.2.0-pre.2-logs.zip
```

- [ ] **Step 5: Create the GitHub Pre-release only after all evidence passes**

```powershell
$releaseRoot = 'D:\UGit\JoiH-AppUI-Lab\release-work\v0.2.0-pre.2-release'
$artifactPaths = Get-ChildItem -LiteralPath "$releaseRoot\artifacts" -File | ForEach-Object { $_.FullName }
if ($artifactPaths.Count -ne 10) { throw "Expected 10 release artifacts, found $($artifactPaths.Count)." }
gh release create v0.2.0-pre.2 @artifactPaths `
  --repo TechJoiH/JoiH-AppUI `
  --verify-tag `
  --prerelease `
  --title 'Joi.H AppUI 0.2.0-pre.2' `
  --notes-file "$releaseRoot\release-notes.md"
```

创建后读取 Release URL 并核对 Artifact 数量与 Hash；上述外部路径只存在于操作者命令和报告生成工作区，不写进候选 Commit 或发布报告。

- [ ] **Step 6: Update the main-branch human-readable evidence index**

只有 Release URL 可访问后：

- `supported-unity-versions.md` 将 `v0.2.0-pre.2` 加入 Officially Supported Releases，精确写 Unity `6000.0.25f1`、Commit、Tag、Release 和 API 仍为 pre-release；
- `validation.md` 写真实 EditMode/PlayMode 数量、Binding/Mono/IL2CPP、Commit/Tag smoke 和 Release 链接，不提交本机路径或原始日志；
- README 把当前状态从 candidate 改为该已验证 Pre-release；
- Changelog 将 `0.2.0-pre.2 - Unreleased` 改为实际发布日期。

这是 Tag 之后的 docs-only main Commit，不改变已发布 Tag 的 Tree，也不把报告写回候选 Commit。

- [ ] **Step 7: Audit and commit the post-release index**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools~/Release/Tests/Invoke-AppUIReleaseToolsTests.ps1 -TestGroup Policy,Docs
git diff --check
git add README.md CHANGELOG.md Documentation~/supported-unity-versions.md Documentation~/validation.md
git commit -m "Document v0.2.0-pre.2 release evidence"
```

推送该 docs-only Commit 仍需使用用户已明确授权的发布范围；若授权只覆盖 Tag/Release，则再次询问后再 `git push origin main`。

- [ ] **Step 8: Final immutable-release verification**

确认：

- `git rev-list -n 1 v0.2.0-pre.2` 等于 candidate；
- GitHub Release Tag 等于 `v0.2.0-pre.2`；
- Tag 安装仍解析 `0.2.0-pre.2`；
- main 的后续 docs Commit 没有改变 Tag；
- README/Supported Versions 使用五级状态且没有把其他 Unity 版本提升为官方支持；
- 仓库仍只有一份 `package.json` 和一个官方 Consumer；
- 没有新增 Compatibility 空壳或版本宏扩散。

---

## Completion Criteria

- [x] 官方唯一目标环境固定为 Unity 6.0 / `6000.0`，措辞不依赖 Unity 上游支持周期。
- [ ] 官方仓库仍只有一份 `package.json`、一条源码线、一个 Consumer 和普通 SemVer Tag。
- [x] 文档严格区分 Official Target 与五种互斥兼容状态。
- [x] Community Verified 只保存外部证据索引，不成为官方发行产物或 Gate。
- [x] Unity 2022.3/2021.3 是 Community Port；Unsupported 与 Known Incompatible 完全分开。
- [x] Community Porting Guide 可由不了解 AppUI 内部实现的开发者独立执行。
- [x] 没有提前创建 Compatibility 空壳，版本宏没有进入 Core/Runtime/Binding 生成代码。
- [x] `Validation~/Unity6000.0Consumer/` 是无缓存、无绝对路径的仓库内模板。
- [ ] 实际验证发生在仓库外工作区，并安装精确 Commit 导出的候选快照或 Git URL。
- [x] 候选 Identity 包含 Commit、Tree、Version 和规范化 Manifest Hash。
- [x] Consumer 使用项目自有 Operation/Execution/Asset Adapter，没有默认异步或资源后端。
- [ ] Basic Page、Popup/Input、Focus List、Binding、Scope、Lease 都有外部集成证据。
- [ ] EditMode、PlayMode、Binding、Mono、IL2CPP 全部与同一候选 Commit 绑定并 Passed。
- [ ] Commit SHA 和不可变 Tag Git URL 都在全新 Consumer 中完成安装 smoke。
- [ ] 正式报告和脱敏 Artifact 位于候选 Commit 外，并与 Tag Commit/Tree/Manifest 一致。
- [x] 任一环境 Blocked 或测试 Failed 时未创建/移动官方 Tag。
- [ ] `v0.2.0-pre.2` 只在全部门禁通过和用户明确授权后发布。
- [ ] AppUI Core、Provider 注入、公共生命周期和宿主边界没有改变。

## Out of Scope Follow-ups

- Unity 2022/2021 官方 CI、官方 Tag、官方 Consumer 或官方 Bug 支持。
- 没有真实差异证据时的 Compatibility 层。
- 将 TMP 拆成可选包或替换 UGUI 依赖。
- Addressables、YooAsset、GameFramework 或其他宿主 Adapter 的官方实现。
- LICENSE、专利授权和最终开源许可选择。
- 自动迁移 Official Target 到 Unity 6.3 或未来 LTS。
