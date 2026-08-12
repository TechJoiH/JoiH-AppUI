# Editor Tools & Validation

## 工具入口

- `Assets > App UI > Create Page Definition`
- `Assets > App UI > Create Group Definition`
- `Tools > Joi.H AppUI > Binding Validation`
- `Tools > Joi.H AppUI > Validate Input Policies`
- `Tools > Joi.H AppUI > Validate Focus P0`
- `Tools > Joi.H AppUI > Open Focus Runtime Trace`

## 推荐日常流程

1. 创建/修改 Controller 与 Prefab；
2. Generate Bindings；
3. 等待编译；
4. Bind References；
5. 运行 Binding、Input、Focus 验证；
6. 在最终场景实际测试鼠标、键盘、手柄和 Cancel；
7. 运行 EditMode/PlayMode；
8. 发布前完成 Player Build。

验证器是只读门禁，不应在 Build 时偷偷生成或修复资产。自动化证明契约，不替代字体、布局、动画、点击面积和视觉层级的人工验收。

## CI 建议

- 检查 Runtime 与 package.json 不含项目命名空间和未声明第三方依赖；
- EditMode 覆盖 Definition、Operation、Focus、Input、Binding 与 Lease；
- PlayMode 覆盖打开/刷新/关闭、取消、晚到加载与真实 EventSystem Raycast；
- 从干净消费项目安装包并完成 Domain Reload；
- 至少在一个目标平台做 IL2CPP Development Build。

官方仓库通过仓库外 `Validation~/Unity6000.0Consumer/` 执行上述流程。发布工具入口为：

- `Tools~/Release/New-AppUICandidateSnapshot.ps1`；
- `Tools~/Release/New-AppUIConsumerWorkspace.ps1`；
- `Tools~/Release/Invoke-AppUIPreTagValidation.ps1`；
- `Tools~/Release/Invoke-AppUIGitInstallSmoke.ps1`；
- `Tools~/Release/New-AppUIReleaseReport.ps1`。
- `Tools~/Release/New-AppUIReleaseArtifacts.ps1`：从正式报告、测试、Binding、Build、Commit/Tag Smoke 与日志归档生成恰好十个脱敏上传文件。
- `Tools~/Release/Test-AppUIReleaseReadiness.ps1`：用有界只读查询检查远端 `main`、候选 Commit/Tree 与 Tag 是否占用；远端不可达时返回 `Blocked`，不创建或移动任何远端引用。

不要直接打开仓库内 Consumer 模板，也不要把它生成的 `Library`、Fixture、Build 或报告提交回包仓库。完整的候选身份、执行顺序、超时和 Artifact 规则见[验证与发布门禁](validation.md)。

当前证据和版本数字见[验证与发布门禁](validation.md)。
