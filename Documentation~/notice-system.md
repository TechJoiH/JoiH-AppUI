# Notice System

Notice 用于 Toast、Tooltip、FloatingText、DamageNumber 等短生命周期视觉。它不进入页面
Stack，不参与页面 OpenPolicy，也不会因为“没有配置”而自动创建文本或降级 Prefab。

## 显式配置

每种视觉都由 `AppUINoticeVisualSettings` 声明：

- `Enabled`：是否启用；默认 `false`；
- `PrefabAssetId`：交给宿主 `IUIAssetProvider` 的稳定 ID；
- `DefaultDuration`、并发数和优先级等视觉策略。

`Enabled=false` 时调用返回无效 Handle，并按运行时代次记录可观测警告，不加载资源。
`Enabled=true` 时必须存在 AssetId、Notice Layer、可加载 Prefab 和
`NoticeViewBase`；任一缺失都会明确失败并释放 Lease，不会临时改用另一种文本实现。

## 自定义 View

项目通过继承 `NoticeViewBase` 决定如何渲染内容：

```csharp
using Joi.H.AppUI;
using UnityEngine.UI;

public sealed class ProjectToastView : NoticeViewBase
{
    public Text Label;

    protected override void ApplyContent(in UINoticeContent content)
    {
        Label.text = content.Text;
    }

    protected override void ClearContent()
    {
        Label.text = string.Empty;
    }
}
```

Prefab 需要预先配置 `CanvasGroup` 和真实文本引用。AppUI 只负责实例、显示、回收和
Lease；字体、富文本、动画、布局与本地化由 View/项目负责。

## 生命周期与所有权

Provider 成功加载后返回 `UIAssetLease`。Notice 初始化失败、显示完成、SceneScope 清理或
Runtime Shutdown 时，AppUI 会按照实例策略回收对象并且最多归还一次 Lease。项目自有池若
保留活对象，也必须保留 Lease，直到 eviction/shutdown 时一起释放。

## TMP

基础包不包含 TMP Notice。选择 TMP 时使用可选
`TextMeshProNoticeView`，并把带 `CanvasGroup`、`TMP_Text` 和已写入引用的 Prefab 交给
Provider。完整步骤见 [TextMeshPro 可选集成](textmeshpro-integration.md)。
