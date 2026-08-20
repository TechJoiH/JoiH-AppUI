# Unity 6000.0 Consumer Template

这是 Joi.H AppUI 官方唯一的外部 Consumer 模板，固定使用 Unity `6000.0.25f1`。它验证一个与包源码隔离的 Unity 项目能否通过 UPM 安装候选快照，并仅依赖 AppUI 公开程序集完成编译、测试和 Player Build。

不要直接在仓库中打开本目录。正确流程是：

1. 从精确 AppUI Git Commit 生成候选包快照；
2. 运行 `Tools~/Release/New-AppUIConsumerWorkspace.ps1`；
3. 把本模板复制到仓库外的新目录；
4. 由工具生成 `Packages/manifest.json` 并写入候选 `file:` 或 Git URL；
5. 用 Unity `6000.0.25f1` 打开物化后的目录。

模板刻意不提交有效 `manifest.json`、`packages-lock.json`、Library、缓存、Build、生成 Fixture 或本机绝对路径。`Assets/AppUIConsumerGenerated/` 只由验证命令在仓库外工作区创建。

`Assets/AppUIConsumer/Runtime/Adapters` 是消费项目自己的 Operation、Execution Context 和 Asset Provider 实现，不属于 AppUI Core 默认后端，也不使用 UniTask、Task、Awaitable、Coroutine 或 Resources。基础 Consumer 仅使用 UGUI `Text` 和自己的 `ConsumerNoticeView`，不含 TMP 类型或程序集依赖。

需要移植到其他 Unity 版本时，请建立自己的 Fork 和 Consumer，并阅读仓库的 `Documentation~/community-unity-porting.md`。这个模板不代表 Unity 2022/2021 或后续 Unity 6 技术线获得官方支持。
