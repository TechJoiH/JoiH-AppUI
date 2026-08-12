# FAQ

## 安装后为什么还不能直接 Open？

“直接安装使用”表示包本身不需要额外第三方依赖即可编译；运行页面仍必须由项目选择并注入 Operation、Asset Provider 和 Execution Context。可先导入 Basic Integration Sample 作为参考实现。

## AppUI 为什么不自带异步实现？

因为框架不应替项目决定 Task、Awaitable、协程、回调或第三方库。Core 统一的是取消、完成、失败、过期和订阅语义，而不是语法。

## 我还能使用 UniTask 吗？

可以，但它只是项目自己的可选适配器。安装、版本和转换扩展都由项目维护，AppUI 包不会引用或自动安装它。

## 为什么没有 Resources Provider？

Resources 是具体资源策略。AppUI 只使用 `IUIAssetProvider`；项目可适配 Addressables、AssetBundle、远端缓存、Resources 或测试内存表。Basic Integration 使用显式对象引用，不调用 Resources。

## Operation Succeeded，为什么页面仍然打开失败？

Succeeded 表示框架正常产出了 `UIOpenResult`。继续检查 `UIOpenResult.Success` 和 `Error`。DefinitionNotFound、LayerNotFound、打开策略拒绝等是领域失败，不是异常。

## Cancelled 与 Expired 有什么区别？

Cancelled 表示取消请求被确认；Expired 表示结果已被新版本、新意图、场景或 Runtime 代次替代，不再允许提交。

## 为什么 Binding 要分 Generate 与 Bind 两步？

生成字段必须先被 Unity 编译，Binder 才能通过类型信息写入引用。两步之间必须等待 Domain Reload。

## Prefab 已存在，为什么 Open 报 DefinitionNotFound？

Prefab 不会自动注册。请确认 Definition 的 PageId、Registry 条目和 Runtime Profile 引用一致。

## 焦点移动为什么改变了选中视觉或触发业务？

Focus、业务 Selection 和 Hover 应分离。默认移动只改变 Focus；业务确认通常发生在 Click/Submit。检查是否把三种状态绑定到了同一个全选框或在 Select 回调里执行了业务命令。

## UI 上的点击为什么仍触发世界操作？

确认 EventSystem 与 GraphicRaycaster 存在，页面有可 Raycast 面，输入系统在执行世界命令前查询 `AppUIInputHitResolver`，并检查 `AppUIInputZone` 是否错误放行了该通道。

## 谁释放资源？

Provider 在成功结果中返回 `UIAssetLease`。AppUI 在页面/Notice 释放或晚到结果被丢弃时 Dispose Lease；项目实现 Lease 回调并保持可重复调用安全。

## Unity 2022.3 能使用吗？

可以尝试移植，但当前状态是 `Community Port`，不是官方支持。请 Fork 一个固定的 AppUI Tag/Commit，修改你自己 Fork 的包清单，在干净 Unity 2022.3 Consumer 中完成编译、Binding、测试、Mono 和 IL2CPP。步骤见[社区 Unity 移植指南](community-unity-porting.md)。

## 为什么官方只维护 Unity 6.0？

Unity 6.0 / `6000.0` 是 AppUI 当前主要开发、真实项目使用和完整验证环境。一条官方线能让个人开源项目把精力放在 API、Focus、Binding、工具链和接入稳定性，而不是维护多套 Package、Tag、Consumer 与构建矩阵。这个选择不依赖 Unity 上游支持周期。

## Unity 6.1、6.2、6.3 会自动受支持吗？

不会。`package.json` 的最低版本约束和“官方完成验证”是两回事。新 Unity 版本只有经过明确的目标迁移决策、外部 Consumer 和全部发布门禁后，才会进入 Officially Supported Releases。

## Official Target 和 Officially Supported 有什么区别？

Official Target 是官方投入开发和验证的目标环境。Officially Supported 是某个精确 AppUI Tag 在精确 Unity 版本中完成全部门禁后的发布状态。当前目标是 Unity 6.0，但计划中的 `0.2.0-pre.2` 仍缺 IL2CPP、远端 Commit 与 Tag 安装证据，因此尚不是 Officially Supported Release。

## Community Port 和 Community Verified 有什么区别？

Community Port 表示允许自行移植并有教程，但没有完整证据。Community Verified 表示社区已经提交精确版本、Fork/Commit、测试、Binding 和 Player Build 证据。两者都不等于官方维护；Community Verified 只会进入外部证据索引。

## Unsupported 是否表示一定不能运行？

不是。Unsupported 只表示不在官方支持范围且没有兼容保证。只有存在精确、可复现的不兼容证据时，状态才是 Known Incompatible。

## Known Incompatible 如何判定？

必须给出精确 Unity 版本、AppUI Commit、最小复现工程、错误日志以及无法正常工作的明确证据。宿主项目的单次报错、猜测或缺少本机工具链都不足以判定 Known Incompatible。

## 可以提交 Unity 2022 兼容 PR 吗？

可以提交不改变 Unity 6 行为的共同 API 修正、已复现的最小 Compatibility 门面、教程修正和外部证据链接。不能要求官方降低 `package.json`、建立旧版分支/Tag/Consumer/CI、修改 Core 协议或散布版本宏。

## 为什么不能直接修改官方 package.json 支持旧 Unity？

官方清单定义官方安装与验证边界。降低它会让所有用户误以为旧版本已完成维护承诺。社区 Fork 可以修改自己的清单并承担对应测试和发布责任。

## 为什么真实项目应该安装 Tag 而不是 main？

`main` 会继续接收改动，不能保证某天重新解析时得到相同源码。不可变 SemVer Tag 可以把包版本、Commit、测试报告和迁移说明固定在一起。官方 Tag 发布后不会移动；如果发现问题，会发布新版本。
