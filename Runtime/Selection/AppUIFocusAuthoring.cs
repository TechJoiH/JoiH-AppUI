using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    public enum AppUIFocusAuthoringBoundaryTarget
    {
        FocusGroup = 0,
        FocusGroupFirst = 1,
        FocusGroupLastFocused = 2,
        FocusAnchor = 3,
        FocusRegionDefault = 4,
        FocusRegionLastFocused = 5,
        ExitToParentRegion = 6,
    }

    [Serializable]
    public sealed class AppUIFocusAuthoringRegion
    {
        public string RegionId = string.Empty;

        public string ParentRegionId = AppUIFocusDefinition.RootRegionId;

        public string DefaultGroupId = string.Empty;

        public bool AutoAdjacent;
    }

    [Serializable]
    public sealed class AppUIFocusAuthoringBoundary
    {
        public MoveDirection Direction;

        public AppUIFocusAuthoringBoundaryTarget Target;

        public string TargetId = string.Empty;

        private bool RequiresTargetId()
        {
            return Target != AppUIFocusAuthoringBoundaryTarget.ExitToParentRegion;
        }
    }

    [Serializable]
    public sealed class AppUIFocusAuthoringGroup
    {
        public string GroupId = string.Empty;

        public string RegionId = AppUIFocusDefinition.RootRegionId;

        public AppUIFocusGroupLayout Layout = AppUIFocusGroupLayout.Vertical;

        public AppUIFocusWrapPolicy WrapPolicy = AppUIFocusWrapPolicy.Stop;

        [Min(1)]
        public int GridColumnCount = 1;

        public AppUIFocusGridShortRowPolicy GridShortRowPolicy =
            AppUIFocusGridShortRowPolicy.Reject;

        public MonoBehaviour CustomLayoutResolver;

        public AppUIFocusEntryPolicy EntryPolicy =
            AppUIFocusEntryPolicy.LastFocusedOrFirst;

        public string EntryAnchorId = string.Empty;

        public bool OpenByDefault = true;

        public int Order;

        [Tooltip("可选。成功提交该 Group 的节点后，仅在目标越出 Viewport 时滚动。")]
        public ScrollRect ScrollRect;

        public List<AppUIFocusAuthoringBoundary> Boundaries =
            new List<AppUIFocusAuthoringBoundary>();

        private bool IsGrid()
        {
            return Layout == AppUIFocusGroupLayout.Grid;
        }

        private bool IsCustom()
        {
            return Layout == AppUIFocusGroupLayout.Custom;
        }

        private bool UsesEntryAnchor()
        {
            return EntryPolicy == AppUIFocusEntryPolicy.AnchorOrFirst;
        }
    }

    [Serializable]
    public sealed class AppUIFocusAuthoringNode
    {
        public string GroupId = string.Empty;

        public string NodeKey = string.Empty;

        public Selectable Selectable;

        public int Order;
    }

    [Serializable]
    public sealed class AppUIFocusAuthoringAnchor
    {
        public string AnchorId = string.Empty;

        public string GroupId = string.Empty;

        public string NodeKey = string.Empty;
    }

    /// <summary>
    /// 静态页面的 Prefab 焦点声明。运行时只把序列化配置转换为与代码 Builder 相同的
    /// AppUIFocusDefinition；动态节点、业务 Resolver 和虚拟列表继续由 Controller 负责。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AppUIFocusAuthoring :
        MonoBehaviour,
        IAppUIFocusDefinitionProvider,
        IAppUIDefaultFocusTargetProvider,
        IAppUIFocusAnchorTargetProvider
    {
        [Tooltip("留空时由 Scope 使用 PageId。")]
        public string ScopeId = string.Empty;

        [Tooltip("RootRegion 是否在显式路由未命中时启用空间自动邻接。")]
        public bool RootRegionAutoAdjacent;

        public List<AppUIFocusAuthoringRegion> Regions =
            new List<AppUIFocusAuthoringRegion>();

        public List<AppUIFocusAuthoringGroup> Groups =
            new List<AppUIFocusAuthoringGroup>();

        public List<AppUIFocusAuthoringNode> Nodes =
            new List<AppUIFocusAuthoringNode>();

        public List<AppUIFocusAuthoringAnchor> Anchors =
            new List<AppUIFocusAuthoringAnchor>();

        public bool RequireDefaultFocus = true;

        public string DefaultFocusGroupId = string.Empty;

        public string DefaultFocusNodeKey = string.Empty;

        [Tooltip("仅 Editor / Development 生效；启用固定容量 Trace 和只读 Game View Overlay。")]
        public bool DebugTrace;

        public AppUIFocusDefinition BuildFocusDefinition()
        {
            AppUIFocusChainBuilder chainBuilder = new AppUIFocusChainBuilder();
            for (int i = 0; i < Groups.Count; i++)
            {
                AppUIFocusAuthoringGroup group = Groups[i] ??
                    throw new InvalidOperationException(
                        "Focus Authoring contains a null Group entry.");
                AppUIFocusGroupRuleBuilder groupBuilder =
                    ConfigureGroup(chainBuilder, group);
                if (group.EntryPolicy == AppUIFocusEntryPolicy.AnchorOrFirst)
                {
                    groupBuilder.EnterWithAnchor(group.EntryAnchorId);
                }
                else
                {
                    groupBuilder.EnterWith(group.EntryPolicy);
                }

                for (int boundaryIndex = 0;
                     boundaryIndex < group.Boundaries.Count;
                     boundaryIndex++)
                {
                    AppUIFocusAuthoringBoundary boundary =
                        group.Boundaries[boundaryIndex] ??
                        throw new InvalidOperationException(
                            "Focus Authoring contains a null Boundary entry.");
                    groupBuilder.AtBoundary(
                        boundary.Direction,
                        CreateBoundaryAction(boundary));
                }
            }

            AppUIFocusDefinitionBuilder definitionBuilder =
                new AppUIFocusDefinitionBuilder(ScopeId)
                    .SetChain(chainBuilder.Build())
                    .SetAnchorTargetProvider(this)
                    .SetDebugTraceEnabled(DebugTrace)
                    .SetRegionAutoAdjacent(
                        AppUIFocusDefinition.RootRegionId,
                        RootRegionAutoAdjacent);
            for (int i = 0; i < Regions.Count; i++)
            {
                AppUIFocusAuthoringRegion region = Regions[i] ??
                    throw new InvalidOperationException(
                        "Focus Authoring contains a null Region entry.");
                definitionBuilder.AddRegion(
                    region.RegionId,
                    region.ParentRegionId,
                    region.DefaultGroupId);
                definitionBuilder.SetRegionAutoAdjacent(
                    region.RegionId,
                    region.AutoAdjacent);
            }

            for (int i = 0; i < Groups.Count; i++)
            {
                AppUIFocusAuthoringGroup group = Groups[i];
                definitionBuilder.AddGroup(
                    group.GroupId,
                    group.RegionId,
                    group.OpenByDefault,
                    group.Order);
                if (group.ScrollRect != null)
                {
                    definitionBuilder.SetGroupVisibilityAdapter(
                        group.GroupId,
                        new AppUIFocusScrollRectVisibilityAdapter(
                            group.ScrollRect));
                }
            }

            for (int i = 0; i < Nodes.Count; i++)
            {
                AppUIFocusAuthoringNode node = Nodes[i] ??
                    throw new InvalidOperationException(
                        "Focus Authoring contains a null Node entry.");
                definitionBuilder.AddNode(
                    node.GroupId,
                    new AppUIFocusNodeKey(node.NodeKey),
                    node.Selectable,
                    node.Order);
            }

            return definitionBuilder.Build();
        }

        public bool TryGetDefaultFocus(
            UIDefaultFocusReason reason,
            out AppUIFocusTarget target)
        {
            AppUIFocusNodeAddress address = CreateAddress(
                DefaultFocusGroupId,
                DefaultFocusNodeKey);
            target = AppUIFocusTarget.FromNodeAddress(address);
            return target.IsValid;
        }

        public bool TryGetFocusAnchor(
            string anchorId,
            out AppUIFocusTarget target)
        {
            if (!string.IsNullOrEmpty(anchorId))
            {
                for (int i = 0; i < Anchors.Count; i++)
                {
                    AppUIFocusAuthoringAnchor anchor = Anchors[i];
                    if (anchor != null &&
                        string.Equals(
                            anchor.AnchorId,
                            anchorId,
                            StringComparison.Ordinal))
                    {
                        target = AppUIFocusTarget.FromNodeAddress(
                            CreateAddress(anchor.GroupId, anchor.NodeKey));
                        return target.IsValid;
                    }
                }
            }

            target = default;
            return false;
        }

        public void ValidateFocus()
        {
            AppUIFocusValidationReport report = ValidateAuthoring();
            for (int i = 0; i < report.Errors.Count; i++)
            {
                Debug.LogError("<AppUIFocusAuthoring> " + report.Errors[i], this);
            }

            for (int i = 0; i < report.Warnings.Count; i++)
            {
                Debug.LogWarning("<AppUIFocusAuthoring> " + report.Warnings[i], this);
            }

            Debug.Log(
                "<AppUIFocusAuthoring> Validate Focus completed. Errors=" +
                report.Errors.Count +
                ", Warnings=" +
                report.Warnings.Count,
                this);
        }

        public void PrintFocusMap()
        {
            try
            {
                AppUIFocusNodeAddress defaultAddress = RequireDefaultFocus
                    ? CreateAddress(DefaultFocusGroupId, DefaultFocusNodeKey)
                    : default;
                AppUIFocusMap focusMap = AppUIFocusMapBuilder.Build(
                    BuildFocusDefinition(),
                    defaultAddress,
                    true);
                Debug.Log(focusMap.ToString(), this);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "<AppUIFocusAuthoring> Print Focus Map failed: " +
                    exception.Message,
                    this);
            }
        }

        public AppUIFocusValidationReport ValidateAuthoring()
        {
            AppUIFocusValidationReport report;
            AppUIFocusDefinition definition;
            try
            {
                definition = BuildFocusDefinition();
                AppUIFocusNodeAddress defaultAddress = RequireDefaultFocus
                    ? CreateAddress(DefaultFocusGroupId, DefaultFocusNodeKey)
                    : default;
                report = AppUIFocusDefinitionValidator.Validate(
                    definition,
                    defaultAddress,
                    true);
            }
            catch (Exception exception)
            {
                report = new AppUIFocusValidationReport();
                report.AddError("Focus Authoring build failed: " + exception.Message);
                return report;
            }

            ValidateDefaultFocus(report);
            ValidateAnchors(report);
            return report;
        }

        private static AppUIFocusGroupRuleBuilder ConfigureGroup(
            AppUIFocusChainBuilder builder,
            AppUIFocusAuthoringGroup group)
        {
            switch (group.Layout)
            {
                case AppUIFocusGroupLayout.Single:
                    return builder.SingleGroup(group.GroupId);
                case AppUIFocusGroupLayout.Vertical:
                    return builder.VerticalGroup(group.GroupId, group.WrapPolicy);
                case AppUIFocusGroupLayout.Horizontal:
                    return builder.HorizontalGroup(group.GroupId, group.WrapPolicy);
                case AppUIFocusGroupLayout.Grid:
                    return builder.GridGroup(
                        group.GroupId,
                        group.GridColumnCount,
                        group.GridShortRowPolicy);
                case AppUIFocusGroupLayout.Spatial:
                    return builder.SpatialGroup(group.GroupId);
                case AppUIFocusGroupLayout.Custom:
                    if (!(group.CustomLayoutResolver is IAppUIFocusLayoutResolver resolver))
                    {
                        throw new InvalidOperationException(
                            "Custom Focus Group requires a MonoBehaviour implementing " +
                            nameof(IAppUIFocusLayoutResolver) +
                            ": " +
                            group.GroupId);
                    }

                    return builder.CustomGroup(group.GroupId, resolver);
                case AppUIFocusGroupLayout.Legacy:
                default:
                    throw new InvalidOperationException(
                        "Focus Authoring only supports semantic Group layouts: " +
                        group.GroupId);
            }
        }

        private static AppUIFocusAction CreateBoundaryAction(
            AppUIFocusAuthoringBoundary boundary)
        {
            switch (boundary.Target)
            {
                case AppUIFocusAuthoringBoundaryTarget.FocusGroup:
                    return AppUIFocusAction.FocusGroup(boundary.TargetId);
                case AppUIFocusAuthoringBoundaryTarget.FocusGroupFirst:
                    return AppUIFocusAction.FocusGroupFirst(boundary.TargetId);
                case AppUIFocusAuthoringBoundaryTarget.FocusGroupLastFocused:
                    return AppUIFocusAction.FocusGroupLastFocused(boundary.TargetId);
                case AppUIFocusAuthoringBoundaryTarget.FocusAnchor:
                    return AppUIFocusAction.FocusAnchor(boundary.TargetId);
                case AppUIFocusAuthoringBoundaryTarget.FocusRegionDefault:
                    return AppUIFocusAction.FocusRegionDefault(boundary.TargetId);
                case AppUIFocusAuthoringBoundaryTarget.FocusRegionLastFocused:
                    return AppUIFocusAction.FocusRegionLastFocused(boundary.TargetId);
                case AppUIFocusAuthoringBoundaryTarget.ExitToParentRegion:
                    return AppUIFocusAction.ExitToParentRegion();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ValidateDefaultFocus(AppUIFocusValidationReport report)
        {
            if (!RequireDefaultFocus)
            {
                return;
            }

            AppUIFocusNodeAddress address = CreateAddress(
                DefaultFocusGroupId,
                DefaultFocusNodeKey);
            if (!address.IsValid || !ContainsNode(address))
            {
                report.AddError(
                    "Required default focus does not reference a static Node in this Scope: " +
                    address);
            }
        }

        private void ValidateAnchors(AppUIFocusValidationReport report)
        {
            HashSet<string> anchorIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Anchors.Count; i++)
            {
                AppUIFocusAuthoringAnchor anchor = Anchors[i];
                if (anchor == null || string.IsNullOrWhiteSpace(anchor.AnchorId))
                {
                    report.AddError("Focus Authoring contains an empty AnchorId.");
                    continue;
                }

                if (!anchorIds.Add(anchor.AnchorId))
                {
                    report.AddError(
                        "Focus Authoring contains duplicate AnchorId: " +
                        anchor.AnchorId);
                }

                AppUIFocusNodeAddress address = CreateAddress(
                    anchor.GroupId,
                    anchor.NodeKey);
                if (!address.IsValid || !ContainsNode(address))
                {
                    report.AddError(
                        "Focus Anchor does not reference a static Node in this Scope: " +
                        anchor.AnchorId +
                        " -> " +
                        address);
                }
            }

            for (int groupIndex = 0; groupIndex < Groups.Count; groupIndex++)
            {
                AppUIFocusAuthoringGroup group = Groups[groupIndex];
                if (group == null)
                {
                    continue;
                }

                if (group.EntryPolicy == AppUIFocusEntryPolicy.AnchorOrFirst &&
                    !anchorIds.Contains(group.EntryAnchorId))
                {
                    report.AddError(
                        "Focus Group entry references a missing Anchor: " +
                        group.GroupId +
                        " -> " +
                        group.EntryAnchorId);
                }

                for (int boundaryIndex = 0;
                     boundaryIndex < group.Boundaries.Count;
                     boundaryIndex++)
                {
                    AppUIFocusAuthoringBoundary boundary =
                        group.Boundaries[boundaryIndex];
                    if (boundary != null &&
                        boundary.Target ==
                        AppUIFocusAuthoringBoundaryTarget.FocusAnchor &&
                        !anchorIds.Contains(boundary.TargetId))
                    {
                        report.AddError(
                            "Focus boundary references a missing Anchor: " +
                            group.GroupId +
                            "/" +
                            boundary.Direction +
                            " -> " +
                            boundary.TargetId);
                    }
                }
            }
        }

        private bool ContainsNode(AppUIFocusNodeAddress address)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                AppUIFocusAuthoringNode node = Nodes[i];
                if (node != null &&
                    string.Equals(node.GroupId, address.GroupId, StringComparison.Ordinal) &&
                    string.Equals(
                        node.NodeKey,
                        address.NodeKey.Value,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static AppUIFocusNodeAddress CreateAddress(
            string groupId,
            string nodeKey)
        {
            AppUIFocusNodeKey key = new AppUIFocusNodeKey(nodeKey);
            return !string.IsNullOrEmpty(groupId) && key.IsValid
                ? new AppUIFocusNodeAddress(groupId, key)
                : default;
        }
    }
}
