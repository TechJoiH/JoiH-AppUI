# Joi.H AppUI 公开接入 Skill 接管说明

更新时间：2026-08-21

## 接管目标

本分支是 `integrating-joih-appui` 公开用户接入 Skill 的开发快照，供另一台电脑直接继续工作。它不是正式发布分支；不得由此创建 Tag、GitHub Release 或直接合并 `main`，除非之后重新完成最终验证并获得明确授权。

快照分支：`codex/appui-skill-handoff-2026-08-21`

快照基线：远端 `main` 的 `e561ef64a873e513cbb8693fec2d310c3493e602`，叠加本地 12 个已验证提交、公开 Skill 入口文件及本接管说明。

## 已完成

1. Task 1：公开接入 Skill 的行为场景、基线问题和测试骨架。
2. Task 2：完整设计、实施计划及 0.4 文档对齐。
3. Task 3：通过标准 Skill 初始化器创建 `Skills~/integrating-joih-appui`，补齐 `SKILL.md` 与 `agents/openai.yaml`。
4. Task 4：实现只读项目检查器和独立验证证明生成器；覆盖安装状态、版本引用、宿主边界、Binding/Runtime 证据、扫描预算、路径与秘密信息安全。
5. Task 5：完成安装、宿主边界、运行时根节点三篇核心接入文档，并加入 Skill 路由。
6. 维护者 Skill 仍与公开用户 Skill 分离，并以换机归档形式保存于 `Handoff~/2026-08-21/maintaining-joih-appui-08f03ef.zip`。

## 最近验证证据

- 公开接入 focused suite：`Passed=48 Failed=0`（Task 5 提交后）。
- 项目检查器 hardened suite：`Passed=44 Failed=0`。
- Release 工具回归套件：`Passed=32 Failed=0`。
- PowerShell 5.1 AST：3 个生产脚本均为 0 error。
- `quick_validate.py`：`Skill is valid!`。
- 私有路径、Secret、可变安装 URL、`.meta`、链接与 diff 检查均通过。

验证结果只证明当前已完成范围；尚未完成 Task 8/9 的最终新代理行为验证与真实干净 Unity Consumer 验证。

## 剩余工作（按顺序）

1. 独立复核 Task 5 的三篇核心文档与路由，不直接扩展实现。
2. Task 6：编写 `page-production.md` 与 `binding-focus-input.md`，覆盖页面生产、Binding、Focus 和 Input 的完整接入闭环。
3. Task 7：编写 `optional-textmeshpro.md`、`migration.md`、`troubleshooting.md`，完成最终 `SKILL.md` 路由图。
4. Task 8：让全新代理仅依赖公开 Skill 执行行为场景并达到 GREEN，修复文档或脚本中的实际缺口。
5. Task 9：在一次性 Unity 6.0 Consumer 项目中验证 Git URL 安装、初始化、Binding、生命周期测试和 Player Build；Unity Test Framework 1.4.5 命令不得使用会提前退出测试运行的 `-quit`。
6. Task 10：更新仓库 `README.md`、`Documentation~/index.md` 与公开安装/Skill 使用说明，并执行 package/release gate（只验证，不发布）。
7. Task 11：最终分支级代码审查、完整回归和只读远端状态核验。是否合并、打 Tag 或发布必须另行决定。

## 权威入口

- 实施计划：`Documentation~/superpowers/plans/2026-08-20-integrating-joih-appui-skill-implementation.md`
- 设计说明：`Documentation~/superpowers/specs/2026-08-20-joih-appui-codex-skills-design.md`
- 公开 Skill：`Skills~/integrating-joih-appui/SKILL.md`
- 自动项目检查：`Skills~/integrating-joih-appui/scripts/inspect-appui-project.ps1`
- 验证证明生成：`Skills~/integrating-joih-appui/scripts/new-appui-validation-attestation.ps1`
- 行为测试：`Skills~/integrating-joih-appui/tests/Invoke-IntegratingAppUISkillTests.ps1`

## 另一台电脑接管

```powershell
git clone https://github.com/TechJoiH/JoiH-AppUI.git
Set-Location JoiH-AppUI
git fetch origin codex/appui-skill-handoff-2026-08-21
git switch --track origin/codex/appui-skill-handoff-2026-08-21
New-Item -ItemType Directory -Force "$env:USERPROFILE\.codex\skills"
Expand-Archive -LiteralPath ".\Handoff~\2026-08-21\maintaining-joih-appui-08f03ef.zip" -DestinationPath "$env:USERPROFILE\.codex\skills" -Force
```

确认 `$env:USERPROFILE\.codex\skills\maintaining-joih-appui\SKILL.md` 存在并重启 Codex。然后先阅读本文件和实施计划，从“独立复核 Task 5”开始。不要复用已有的 `v0.4.0-pre.1` Tag；它与当前候选提交冲突，而且本快照不包含任何发布授权。

## 边界

- 本快照不修改 `main`。
- 本快照不创建 PR、Tag 或 GitHub Release。
- 维护者 Skill 仅作为 `Handoff~` 下的换机归档，不混入公开用户 Skill 的运行目录或 UPM 内容。
- 继续工作时保持 AppUI Core 的接口边界，不恢复 UniTask、Odin 或强制 TMP 依赖。
