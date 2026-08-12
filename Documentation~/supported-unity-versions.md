# Unity 版本支持政策

本文说明 Joi.H AppUI 对不同 Unity 版本承担什么责任。这里的“可以尝试运行”和“官方承诺维护”不是一回事。

## Official Target

Joi.H AppUI 当前唯一官方目标环境为 Unity 6.0 / `6000.0`。这是框架当前主要开发、真实项目使用和发布验证环境。

AppUI 的官方目标由项目自身的验证策略决定，不与 Unity 当前最新 LTS 自动同步。Unity 发布新版本不会自动扩大 AppUI 的官方支持范围；官方目标迁移必须经过独立设计、Consumer 验证和发布决策。

`package.json` 中的以下字段表示官方包要求的最低 Editor 技术版本：

```json
{
  "unity": "6000.0"
}
```

它不表示所有 `6000.x` 或更高 Unity 版本都已完成 AppUI 验证。

## Officially Supported Releases

目前没有完成全部发布门禁的不可变官方 Tag。

当前计划发布的 `0.2.0-pre.2` 是 Unity 6 Official Target 下的预发布候选。框架开发阶段已有前序候选的外部 Consumer、Binding、EditMode、PlayMode 和 Mono 构建证据，但发布证据不能跨 Commit 复用；当前最新候选在完整重跑前因缺少 Windows C++ toolchain 停止于环境预检，也尚未完成远端 Commit、不可变 Tag 和 Tag URL 安装冒烟，因此不能标记为 `Officially Supported`。

官方 Release 记录必须包含：

- AppUI Tag 与精确 Commit；
- 精确 Unity Editor 版本；
- UGUI 版本；
- EditMode、PlayMode 与 Binding 结果；
- Mono 与 IL2CPP Player Build 结果；
- Commit SHA 与 Tag Git URL 安装结果；
- 可下载的验证报告和证据。

## Compatibility Status

每个“精确 Unity 版本 + 精确 AppUI Commit/Tag”组合只能属于以下五种状态之一：

| 状态 | 含义 | 官方责任 |
|---|---|---|
| `Officially Supported` | AppUI 官方完成规定测试并承诺维护 | 官方验证、记录并处理范围内缺陷 |
| `Community Verified` | 社区提供完整、可复查的外部验证证据 | 官方只索引证据，不维护该版本 |
| `Community Port` | 允许用户自行移植并提供教程，但尚无完整验证证据 | 官方不保证可运行，不提供旧版发行物 |
| `Unsupported` | 不在当前支持范围，也没有兼容保证 | 不代表确认不能运行 |
| `Known Incompatible` | 已有可复现证据表明该组合无法正常工作 | 记录问题、复现条件和已知限制 |

`Official Target` 描述官方投入验证的环境，不是第六种兼容状态。只有某个不可变 Release 完成全部门禁后，它才获得 `Officially Supported` 状态。

## Community Verified Evidence Index

当前没有已收录的 Community Verified 记录。

未来每条记录使用以下格式：

```text
Unity: 2022.3.62f3
AppUI source: community fork URL + 40-character commit
Package manifest: URL
EditMode / PlayMode: evidence URL
Binding: evidence URL
Mono / IL2CPP: evidence URL
Known limitations: URL or explicit None
Verified at: YYYY-MM-DD
```

Community Verified 是文档状态，不是官方发行产物。收录记录不会产生官方旧版 CI、Package、Tag、Release 或 Bug 支持，也不会成为官方 Unity 6 Release Gate。

证据链接失效、Fork/Commit 无法解析或测试范围不完整时，记录会退回 `Community Port`，不会自动改成 `Known Incompatible`。

## Community Port

| Unity 版本 | 当前状态 | 说明 |
|---|---|---|
| Unity 2022.3 LTS | `Community Port` | 可按社区教程 Fork 和验证，官方未验证 |
| Unity 2021.3 LTS | `Community Port` | 可按社区教程 Fork 和验证，官方未验证 |

操作步骤见[社区 Unity 移植指南](community-unity-porting.md)。

## Unsupported

- Unity 6.1、6.2、6.3 及其他未列入 Officially Supported 或 Community Verified 的后续技术线；
- Unity 2020.3 及更早版本；
- 未提供精确 Unity 版本、AppUI Commit 和依赖清单的模糊组合。

`Unsupported` 只说明官方不承担兼容责任。它与“已确认不能运行”不同，用户仍可自行实验或建立 Community Port。

## Known Incompatible

当前没有已确认的 Known Incompatible 组合。

只有同时提供精确 Unity 版本、AppUI Commit、最小复现工程、错误日志以及无法通过合理边界适配解决的证据，才会加入本节。猜测、宿主项目特有问题或单次编译失败不能作为 Known Incompatible 结论。

## 状态转换

```text
Community Port
    ├─ 完整社区证据 → Community Verified
    ├─ 官方纳入目标并完成全门禁 → Officially Supported
    ├─ 官方明确不再接收该范围 → Unsupported
    └─ 可复现不兼容证据 → Known Incompatible
```

Officially Supported Release 后续发现严重不兼容时，官方会保留原 Tag，发布新的修复版本并在本页记录影响，不移动已经发布的 Tag。
