# 社区 Unity 版本移植指南

本教程面向需要在非官方目标 Unity 版本中使用 Joi.H AppUI 的开发者。你不需要了解 AppUI 内部实现，但需要能够维护自己的 Fork、Unity 包清单和验证工程。

社区移植的目标是：在不改变 AppUI Core 协议、序列化数据和 Unity 6 官方行为的前提下，让一个明确的 Unity 版本能够安装、编译、测试和构建。

## 开始前

先确认以下边界：

- 官方唯一目标是 Unity 6.0 / `6000.0`；
- 社区移植不属于官方支持；
- 你的 Fork 负责清单、兼容代码、验证、Tag 和后续维护；
- 官方仓库不会为旧 Unity 维护第二份 `package.json`、分支或 Release；
- 移植结果应明确标记为非官方版本。

查看[Unity 版本支持政策](supported-unity-versions.md)，确认目标版本当前不是 `Known Incompatible`。

## 1. 选择移植基线

优先选择最近一个已通过完整门禁的官方稳定 Tag，不要从变化中的 `main` 开始。记录：

```text
Upstream repository: https://github.com/TechJoiH/JoiH-AppUI
Upstream tag: vX.Y.Z
Upstream commit: 40-character SHA
Target Unity: 2022.3.62f3
```

如果当前尚无 Officially Supported Tag，只能把工作标为实验性 Port，并固定一个精确 Commit；不得把它描述成官方发布基线。

## 2. Fork 与分支

Fork 仓库后，从基线创建明确分支：

```bash
git switch -c community/unity-2022.3 vX.Y.Z
```

不要在官方 `main` 上直接降低最低 Unity 版本，也不要要求官方创建 `unity2022` Release 分支。

## 3. 修改社区 Fork 的包清单

只在你的 Fork 中修改 `package.json`：

1. 把 `unity` 改为目标 Editor 的 `major.minor`；
2. 在目标 Unity 创建一个全新 UGUI 项目；
3. 从该工程实际解析出的 `Packages/packages-lock.json` 选择 UGUI/TMP 版本；
4. 保持 `com.joih.appui` Package ID，除非你的分发渠道要求使用独立 ID；
5. 不增加 UniTask、Odin、Addressables 或宿主框架作为 AppUI Core 依赖。

旧 Unity 的 UGUI/TMP 版本必须来自目标 Editor 的真实解析结果。不要照抄未经验证的示例版本。

## 4. 建立干净 Consumer Project

使用目标 Unity 创建全新工程，只安装：

- 你的 AppUI Fork 和精确 Commit；
- 目标 Unity 对应的 UGUI；
- Unity Test Framework；
- Consumer 自己的 Operation、Asset Provider 与 Execution Context。

不要在真实业务项目里直接排查第一轮兼容问题。干净 Consumer 能区分 AppUI 差异与宿主项目包冲突。

确认以下基础步骤：

1. Package Manager 解析成功；
2. Domain Reload 后无编译错误；
3. Runtime、Editor 和 Basic Integration Sample asmdef 可编译；
4. 没有通过本机绝对 `file:` 路径穿透到另一个工作树。

## 5. 按边界定位编译差异

建议按以下顺序处理：

### package.json 和包版本

先解决最低 Unity、UGUI、TMP 和 Test Framework 的清单问题。清单能解决的问题不要改 Runtime。

### asmdef

检查目标版本中真实存在的程序集名。保持 Core、Runtime、Editor 和 Consumer Adapter 的依赖方向，不让 Core 引用 Editor、Sample 或第三方异步程序集。

### C# 与 Runtime API

优先使用目标版本共同支持的公共 API。不要为了旧版本改变：

- `IUIOperation<T>`、`IUIAssetProvider`、`IAppUIExecutionContext`；
- Open、Refresh、Close、Release 生命周期语义；
- Definition 序列化字段；
- enum 数值；
- Binding 生成格式。

### Editor API

Prefab、AssetDatabase、Package Manager Sample、Build Pipeline 等 Editor API 更容易出现版本差异。先写能复现差异的最小 Editor 测试，再决定是否需要兼容门面。

## 6. 何时创建 Compatibility 门面

只有真实、可复现、无法通过清单或共同公共 API 解决的版本差异，才创建：

```text
Editor/Compatibility/
Runtime/Compatibility/
```

并遵守：

- 版本宏只存在于 Compatibility 文件；
- Core、公开生命周期、Definition 和 Binding 生成代码中不散布版本宏；
- 门面保持同一语义，不能用行为不同的 fallback 假装成功；
- 没有 Runtime 差异就不要创建 `Runtime/Compatibility/`；
- 每个门面都有目标版本回归测试，同时重跑 Unity 6 官方测试。

以下写法禁止进入普通 Runtime 文件：

```csharp
#if UNITY_2022
// scattered compatibility behavior
#endif
```

## 7. 保护序列化与 GUID

移植不得改变：

- 已存在的 `.meta` GUID；
- Definition 和 Settings 序列化字段名；
- enum 已发布数值；
- Prefab、Scene、ScriptableObject 的引用关系；
- 生成 Binding partial class 的命名与字段所有权。

确实需要序列化迁移时，应独立设计迁移工具和回滚方式，不能把它混入 Unity 版本兼容补丁。

## 8. 完整验证

至少在干净 Consumer 中完成：

1. Package Manager 安装和 Domain Reload；
2. Basic Integration Sample 导入与编译；
3. EditMode 全量测试；
4. PlayMode 页面 Open/Refresh/Close、Popup、Input、Focus、Scope 与 Lease；
5. Binding Generate；
6. 等待编译和 Domain Reload；
7. Binding Bind 与只读 Validate；
8. 一个 Mono Development Player Build；
9. 一个 IL2CPP Development Player Build；
10. 安装社区 Tag 后重新完成最小 Open/Close 冒烟。

环境缺失必须写 `Blocked`，不能把另一个 Commit 或旧日志当作当前通过证据。

## 9. 发布社区 Tag

社区 Tag 必须与官方 Tag 明显区分，例如：

```text
community-unity2022.3-v0.2.0-pre.2.1
```

Release 说明必须包含：

```text
This is an unofficial community port.
It is not maintained or supported by the Joi.H AppUI project.
Target Unity: 2022.3.62f3
Upstream AppUI commit: <40-character SHA>
Community fork commit: <40-character SHA>
```

不要使用容易被误解为官方产物的 `v0.2.0-unity2022`，也不要让用户以为官方会处理该 Fork 的兼容 Bug。

## 10. 申请 Community Verified 索引

通过 Issue 或贡献流程提交：

- 精确 Unity 完整版本；
- 上游 AppUI Tag/Commit；
- 社区 Fork URL 与 40 位 Commit；
- 社区 `package.json`；
- EditMode 与 PlayMode XML；
- Binding 报告；
- Mono 与 IL2CPP 构建摘要；
- 社区 Tag 安装冒烟；
- 已知限制；
- 可长期访问的证据链接。

官方只会把完整证据加入[Community Verified Evidence Index](supported-unity-versions.md#community-verified-evidence-index)。索引不会把移植版本变成官方发行线。

## 11. 向上游提交兼容 PR

可以提交：

- 用更低且稳定的共同公共 API 替换不必要的新 API；
- 把已复现的 Editor 差异集中到最小 Compatibility 门面；
- 不改变 Unity 6 行为的可移植性修复；
- 教程修正和外部验证证据链接。

不要提交：

- 官方旧版 `package.json`、分支、Tag、Consumer 或 CI 矩阵；
- 为旧 Unity 修改 Core 协议；
- 散布版本宏；
- 改动序列化字段、enum 数值或 Meta GUID；
- 旧版专用第三方依赖；
- 降低 Unity 6 发布门禁。
