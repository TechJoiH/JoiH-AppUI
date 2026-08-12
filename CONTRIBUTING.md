# 贡献指南

感谢关注 Joi.H AppUI。当前项目仍处于 `0.x` 预发布阶段，公开 API 和序列化格式可能变化。

## 提交前

- 先通过 Issue 说明问题、使用场景和期望行为；
- 不提交任何真实项目的业务代码、资产、凭据或未获授权内容；
- 保持 Core/Runtime 不依赖具体异步库、资源框架和消费项目程序集；
- 新行为应包含能复现问题并防止回归的测试；
- 文档以中文为主，公开 API、类型名和路径保留英文。

## 本地验证

至少完成：

1. 在没有第三方异步包的 Unity 6 消费项目完成 Domain Reload；
2. 全量 EditMode 与 PlayMode 测试；
3. `git diff --check`；
4. Runtime/asmdef/package 依赖边界检查；
5. 涉及 Player 行为时完成对应目标平台构建。

发布级验证不直接打开仓库中的模板，而是从精确 Commit 导出候选包，再将 `Validation~/Unity6000.0Consumer/` 物化到仓库外。完整入口和证据边界见 [验证与发布门禁](Documentation~/validation.md)。贡献者可先运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools~/Release/Tests/Invoke-AppUIReleaseToolsTests.ps1 -TestGroup All
```

本机缺少 C++ toolchain 时，IL2CPP 必须记录为 `Blocked`，不能用 Mono、旧日志或另一个 Commit 的结果替代。

## Pull Request

PR 应说明职责边界、行为变化、测试结果、兼容性影响和人工验证步骤。不要在同一 PR 顺带重构无关模块。

## Unity 版本兼容贡献

官方唯一目标环境是 Unity 6.0 / `6000.0`。非官方版本适配先按[社区 Unity 移植指南](Documentation~/community-unity-porting.md)在 Fork 和干净 Consumer 中复现、验证。

兼容 PR 按以下顺序审查：

1. 是否给出精确 Unity 版本、AppUI Commit 和最小复现；
2. 是否只需修改社区 Fork 的 `package.json`；
3. 是否能改用已有共同公共 API；
4. 是否确实需要集中式 Compatibility 门面；
5. 是否保持公开 API、序列化字段、enum 数值和 Meta GUID；
6. 是否保持 Unity 6 全量门禁；
7. 文档是否明确标记为非官方适配。

以下内容不会合入官方仓库：

- 官方 Unity 2022/2021 清单、分支、Tag、Consumer 或 CI 矩阵；
- 为旧 Unity 修改 AppUI Core 协议；
- 在普通 Runtime/Editor 文件散布版本宏；
- 旧版专用第三方依赖；
- 降低 Unity 6 的 Binding、测试或 Player Build 门禁。

申请加入 Community Verified 索引时，必须提供精确 Unity 版本、上游和 Fork Commit、清单、EditMode/PlayMode XML、Binding 报告、Mono/IL2CPP 摘要、Tag 安装冒烟和已知限制的长期链接。收录只代表社区证据可复查，不产生官方维护责任。

项目的对外分发许可证尚未确定。在 LICENSE 发布和贡献授权流程明确前，请先通过 Issue 讨论，不要提交需要合并的外部代码。
