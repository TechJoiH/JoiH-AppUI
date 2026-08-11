using System;
using System.Collections.Generic;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>页面 Controller 通过该接口向框架提供只读焦点定义。</summary>
    public interface IAppUIFocusDefinitionProvider
    {
        AppUIFocusDefinition BuildFocusDefinition();
    }

    /// <summary>
    /// 页面焦点定义。每个 Scope 都有一个隐式 RootRegion，子 Region 形成最大深度为 4
    /// 的有界父子栈。
    /// </summary>
    public sealed class AppUIFocusDefinition
    {
        public const string RootRegionId = "root";

        private readonly AppUIFocusGroupDefinition[] groups;
        private readonly AppUIFocusNodeDefinition[] nodes;
        private readonly AppUIFocusRegionDefinition[] regions;
        private readonly AppUIFocusRegionAdjacencyDefinition[] regionAdjacencies;

        internal AppUIFocusDefinition(
            string scopeId,
            AppUIFocusChain focusChain,
            IAppUIFocusAnchorTargetProvider anchorTargetProvider,
            IAppUIFocusMoveInputPolicy moveInputPolicy,
            bool debugTraceEnabled,
            IReadOnlyList<AppUIFocusRegionDefinition> regionDefinitions,
            IReadOnlyList<AppUIFocusRegionAdjacencyDefinition> adjacencyDefinitions,
            IReadOnlyList<AppUIFocusGroupDefinition> groupDefinitions,
            IReadOnlyList<AppUIFocusNodeDefinition> nodeDefinitions)
        {
            ScopeId = scopeId ?? string.Empty;
            FocusChain = focusChain;
            AnchorTargetProvider = anchorTargetProvider;
            MoveInputPolicy = moveInputPolicy;
            DebugTraceEnabled = debugTraceEnabled;

            int regionCount = regionDefinitions != null ? regionDefinitions.Count : 0;
            regions = new AppUIFocusRegionDefinition[regionCount];
            for (int i = 0; i < regionCount; i++)
            {
                regions[i] = regionDefinitions[i];
            }

            int adjacencyCount = adjacencyDefinitions != null
                ? adjacencyDefinitions.Count
                : 0;
            regionAdjacencies = new AppUIFocusRegionAdjacencyDefinition[adjacencyCount];
            for (int i = 0; i < adjacencyCount; i++)
            {
                regionAdjacencies[i] = adjacencyDefinitions[i];
            }

            int groupCount = groupDefinitions != null ? groupDefinitions.Count : 0;
            groups = new AppUIFocusGroupDefinition[groupCount];
            for (int i = 0; i < groupCount; i++)
            {
                groups[i] = groupDefinitions[i];
            }

            int nodeCount = nodeDefinitions != null ? nodeDefinitions.Count : 0;
            nodes = new AppUIFocusNodeDefinition[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                nodes[i] = nodeDefinitions[i];
            }
        }

        /// <summary>Scope 语义 ID；为空时框架使用 PageId。</summary>
        public string ScopeId { get; }

        /// <summary>页面内语义导航链；为空时只建立注册结构。</summary>
        public AppUIFocusChain FocusChain { get; }

        /// <summary>页面业务状态提供的 NodeAddress Anchor；为空时不解析 Anchor Action。</summary>
        public IAppUIFocusAnchorTargetProvider AnchorTargetProvider { get; }

        /// <summary>页面业务状态提供的 Move 输入消费策略；Scope 非 Active 时仍由框架优先阻断。</summary>
        public IAppUIFocusMoveInputPolicy MoveInputPolicy { get; }

        /// <summary>仅 Editor / Development 使用；启用固定容量运行时 Trace 与只读 Overlay。</summary>
        public bool DebugTraceEnabled { get; }

        internal int GroupCount
        {
            get { return groups.Length; }
        }

        internal int RegionCount
        {
            get { return regions.Length; }
        }

        internal int RegionAdjacencyCount
        {
            get { return regionAdjacencies.Length; }
        }

        internal AppUIFocusRegionDefinition GetRegion(int index)
        {
            return regions[index];
        }

        internal AppUIFocusRegionAdjacencyDefinition GetRegionAdjacency(int index)
        {
            return regionAdjacencies[index];
        }

        internal int NodeCount
        {
            get { return nodes.Length; }
        }

        internal AppUIFocusGroupDefinition GetGroup(int index)
        {
            return groups[index];
        }

        internal AppUIFocusNodeDefinition GetNode(int index)
        {
            return nodes[index];
        }
    }

    /// <summary>以构建期校验生成不可变页面焦点定义。</summary>
    public sealed class AppUIFocusDefinitionBuilder
    {
        private readonly string scopeId;
        private readonly List<AppUIFocusRegionDefinition> regions =
            new List<AppUIFocusRegionDefinition>(4);
        private readonly Dictionary<string, int> regionIndices =
            new Dictionary<string, int>(4, StringComparer.Ordinal);
        private readonly List<AppUIFocusRegionAdjacencyDefinition> regionAdjacencies =
            new List<AppUIFocusRegionAdjacencyDefinition>(8);
        private readonly List<AppUIFocusGroupDefinition> groups =
            new List<AppUIFocusGroupDefinition>(8);
        private readonly Dictionary<string, int> groupIndices =
            new Dictionary<string, int>(8, StringComparer.Ordinal);
        private readonly List<AppUIFocusNodeDefinition> nodes =
            new List<AppUIFocusNodeDefinition>(16);
        private readonly HashSet<AppUIFocusNodeAddress> nodeAddresses =
            new HashSet<AppUIFocusNodeAddress>();

        private AppUIFocusChain focusChain;
        private IAppUIFocusAnchorTargetProvider anchorTargetProvider;
        private IAppUIFocusMoveInputPolicy moveInputPolicy;
        private bool debugTraceEnabled;

        public AppUIFocusDefinitionBuilder(string focusScopeId = null)
        {
            scopeId = focusScopeId ?? string.Empty;
            regionIndices.Add(AppUIFocusDefinition.RootRegionId, 0);
            regions.Add(
                new AppUIFocusRegionDefinition(
                    AppUIFocusDefinition.RootRegionId,
                    string.Empty,
                    string.Empty,
                    null));
        }

        public AppUIFocusDefinitionBuilder SetChain(AppUIFocusChain chain)
        {
            focusChain = chain;
            return this;
        }

        public AppUIFocusDefinitionBuilder SetAnchorTargetProvider(
            IAppUIFocusAnchorTargetProvider provider)
        {
            anchorTargetProvider = provider;
            return this;
        }

        public AppUIFocusDefinitionBuilder SetMoveInputPolicy(
            IAppUIFocusMoveInputPolicy policy)
        {
            moveInputPolicy = policy;
            return this;
        }

        public AppUIFocusDefinitionBuilder SetDebugTraceEnabled(bool enabled = true)
        {
            debugTraceEnabled = enabled;
            return this;
        }

        public AppUIFocusDefinitionBuilder AddGroup(
            string groupId,
            bool openByDefault = true,
            int order = 0)
        {
            return AddGroup(
                groupId,
                AppUIFocusDefinition.RootRegionId,
                openByDefault,
                order);
        }

        public AppUIFocusDefinitionBuilder AddGroup(
            string groupId,
            string regionId,
            bool openByDefault = true,
            int order = 0)
        {
            ValidateId(groupId, nameof(groupId));
            ValidateId(regionId, nameof(regionId));
            if (groupIndices.ContainsKey(groupId))
            {
                throw new ArgumentException(
                    "Focus group id must be unique within a scope: " + groupId,
                    nameof(groupId));
            }

            groupIndices.Add(groupId, groups.Count);
            groups.Add(
                new AppUIFocusGroupDefinition(
                    groupId,
                    regionId,
                    openByDefault,
                    order));
            return this;
        }

        public AppUIFocusDefinitionBuilder AddRegion(string regionId)
        {
            return AddRegion(regionId, AppUIFocusDefinition.RootRegionId);
        }

        public AppUIFocusDefinitionBuilder AddRegion(
            string regionId,
            string parentRegionId,
            string defaultGroupId = null)
        {
            ValidateId(regionId, nameof(regionId));
            ValidateId(parentRegionId, nameof(parentRegionId));
            if (string.Equals(
                    regionId,
                    AppUIFocusDefinition.RootRegionId,
                    StringComparison.Ordinal) ||
                regionIndices.ContainsKey(regionId))
            {
                throw new ArgumentException(
                    "Focus region id must be unique within a scope: " + regionId,
                    nameof(regionId));
            }

            regionIndices.Add(regionId, regions.Count);
            regions.Add(
                new AppUIFocusRegionDefinition(
                    regionId,
                    parentRegionId,
                    defaultGroupId,
                    null));
            return this;
        }

        public AppUIFocusDefinitionBuilder SetRegionCancelHandler(
            string regionId,
            IAppUIFocusRegionCancelHandler cancelHandler)
        {
            ValidateId(regionId, nameof(regionId));
            if (cancelHandler == null)
            {
                throw new ArgumentNullException(nameof(cancelHandler));
            }

            if (string.Equals(
                    regionId,
                    AppUIFocusDefinition.RootRegionId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "RootRegion does not participate in the ChildRegion Cancel stage.",
                    nameof(regionId));
            }

            if (!regionIndices.TryGetValue(regionId, out int regionIndex))
            {
                throw new ArgumentException(
                    "Focus region is not declared: " + regionId,
                    nameof(regionId));
            }

            regions[regionIndex] = regions[regionIndex].WithCancelHandler(cancelHandler);
            return this;
        }

        public AppUIFocusDefinitionBuilder SetRegionDefaultGroup(
            string regionId,
            string groupId)
        {
            ValidateId(regionId, nameof(regionId));
            ValidateId(groupId, nameof(groupId));
            if (!regionIndices.TryGetValue(regionId, out int regionIndex))
            {
                throw new ArgumentException(
                    "Focus region is not declared: " + regionId,
                    nameof(regionId));
            }

            regions[regionIndex] = regions[regionIndex].WithDefaultGroup(groupId);
            return this;
        }

        public AppUIFocusDefinitionBuilder SetRegionAutoAdjacent(
            string regionId,
            bool enabled = true)
        {
            ValidateId(regionId, nameof(regionId));
            if (!regionIndices.TryGetValue(regionId, out int regionIndex))
            {
                throw new ArgumentException(
                    "Focus region is not declared: " + regionId,
                    nameof(regionId));
            }

            regions[regionIndex] =
                regions[regionIndex].WithAutoAdjacent(enabled);
            return this;
        }

        public AppUIFocusDefinitionBuilder AddRegionAdjacency(
            string regionId,
            string sourceGroupId,
            UnityEngine.EventSystems.MoveDirection moveDirection,
            string targetGroupId)
        {
            ValidateId(regionId, nameof(regionId));
            ValidateId(sourceGroupId, nameof(sourceGroupId));
            ValidateId(targetGroupId, nameof(targetGroupId));
            if (moveDirection == UnityEngine.EventSystems.MoveDirection.None)
            {
                throw new ArgumentException(
                    "Focus region adjacency requires a direction.",
                    nameof(moveDirection));
            }

            for (int i = 0; i < regionAdjacencies.Count; i++)
            {
                AppUIFocusRegionAdjacencyDefinition existing = regionAdjacencies[i];
                if (string.Equals(existing.RegionId, regionId, StringComparison.Ordinal) &&
                    string.Equals(
                        existing.SourceGroupId,
                        sourceGroupId,
                        StringComparison.Ordinal) &&
                    existing.MoveDirection == moveDirection)
                {
                    throw new ArgumentException(
                        "Focus region adjacency must be unique for a source direction: " +
                        regionId + "/" + sourceGroupId + "/" + moveDirection,
                        nameof(moveDirection));
                }
            }

            regionAdjacencies.Add(
                new AppUIFocusRegionAdjacencyDefinition(
                    regionId,
                    sourceGroupId,
                    moveDirection,
                    targetGroupId));
            return this;
        }

        public AppUIFocusDefinitionBuilder SetGroupVisibilityAdapter(
            string groupId,
            IAppUIFocusVisibilityAdapter adapter)
        {
            ValidateId(groupId, nameof(groupId));
            if (adapter == null)
            {
                throw new ArgumentNullException(nameof(adapter));
            }

            if (!groupIndices.TryGetValue(groupId, out int groupIndex))
            {
                throw new ArgumentException(
                    "Focus group is not declared: " + groupId,
                    nameof(groupId));
            }

            groups[groupIndex] = groups[groupIndex].WithVisibilityAdapter(adapter);
            return this;
        }

        public AppUIFocusDefinitionBuilder SetGroupVirtualizationAdapter(
            string groupId,
            IAppUIFocusVirtualizationAdapter adapter)
        {
            ValidateId(groupId, nameof(groupId));
            if (adapter == null)
            {
                throw new ArgumentNullException(nameof(adapter));
            }

            if (!groupIndices.TryGetValue(groupId, out int groupIndex))
            {
                throw new ArgumentException(
                    "Focus group is not declared: " + groupId,
                    nameof(groupId));
            }

            groups[groupIndex] = groups[groupIndex].WithVirtualizationAdapter(adapter);
            return this;
        }

        public AppUIFocusDefinitionBuilder AddNode(
            string groupId,
            AppUIFocusNodeKey nodeKey,
            Selectable selectable,
            int order = 0)
        {
            return AddNode(groupId, nodeKey, selectable, null, order);
        }

        public AppUIFocusDefinitionBuilder AddNode(
            string groupId,
            AppUIFocusNodeKey nodeKey,
            Selectable selectable,
            IAppUIFocusControlPolicy controlPolicy,
            int order = 0)
        {
            ValidateId(groupId, nameof(groupId));
            if (!groupIndices.ContainsKey(groupId))
            {
                throw new ArgumentException(
                    "Focus node references an undeclared group: " + groupId,
                    nameof(groupId));
            }

            if (!nodeKey.IsValid)
            {
                throw new ArgumentException("Focus node key cannot be empty.", nameof(nodeKey));
            }

            if (selectable == null)
            {
                throw new ArgumentNullException(nameof(selectable));
            }

            AppUIFocusNodeAddress address = new AppUIFocusNodeAddress(groupId, nodeKey);
            if (!nodeAddresses.Add(address))
            {
                throw new ArgumentException(
                    "Focus node address must be unique within a scope: " + address,
                    nameof(nodeKey));
            }

            nodes.Add(
                new AppUIFocusNodeDefinition(
                    address,
                    selectable,
                    controlPolicy,
                    order));
            return this;
        }

        public AppUIFocusDefinition Build()
        {
            return new AppUIFocusDefinition(
                scopeId,
                focusChain,
                anchorTargetProvider,
                moveInputPolicy,
                debugTraceEnabled,
                regions,
                regionAdjacencies,
                groups,
                nodes);
        }

        private static void ValidateId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Focus id cannot be empty.", parameterName);
            }
        }
    }

    internal readonly struct AppUIFocusGroupDefinition
    {
        public AppUIFocusGroupDefinition(
            string groupId,
            string regionId,
            bool openByDefault,
            int order,
            IAppUIFocusVisibilityAdapter visibilityAdapter = null,
            IAppUIFocusVirtualizationAdapter virtualizationAdapter = null)
        {
            GroupId = groupId;
            RegionId = regionId;
            OpenByDefault = openByDefault;
            Order = order;
            VisibilityAdapter = visibilityAdapter;
            VirtualizationAdapter = virtualizationAdapter;
        }

        public string GroupId { get; }

        public string RegionId { get; }

        public bool OpenByDefault { get; }

        public int Order { get; }

        public IAppUIFocusVisibilityAdapter VisibilityAdapter { get; }

        public IAppUIFocusVirtualizationAdapter VirtualizationAdapter { get; }

        public AppUIFocusGroupDefinition WithVisibilityAdapter(
            IAppUIFocusVisibilityAdapter adapter)
        {
            return new AppUIFocusGroupDefinition(
                GroupId,
                RegionId,
                OpenByDefault,
                Order,
                adapter,
                VirtualizationAdapter);
        }

        public AppUIFocusGroupDefinition WithVirtualizationAdapter(
            IAppUIFocusVirtualizationAdapter adapter)
        {
            return new AppUIFocusGroupDefinition(
                GroupId,
                RegionId,
                OpenByDefault,
                Order,
                VisibilityAdapter,
                adapter);
        }
    }

    internal readonly struct AppUIFocusNodeDefinition
    {
        public AppUIFocusNodeDefinition(
            AppUIFocusNodeAddress address,
            Selectable selectable,
            IAppUIFocusControlPolicy controlPolicy,
            int order)
        {
            Address = address;
            Selectable = selectable;
            ControlPolicy = controlPolicy;
            Order = order;
        }

        public AppUIFocusNodeAddress Address { get; }

        public Selectable Selectable { get; }

        public IAppUIFocusControlPolicy ControlPolicy { get; }

        public int Order { get; }
    }
}
