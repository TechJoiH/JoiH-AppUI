using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    internal enum AppUIFocusMapActionTargetKind
    {
        Group = 0,
        Region = 1,
        Anchor = 2,
        ParentRegion = 3,
        TopGroup = 4,
    }

    internal readonly struct AppUIFocusMapActionTarget
    {
        public AppUIFocusMapActionTarget(
            AppUIFocusMapActionTargetKind kind,
            string id,
            string actionName)
        {
            Kind = kind;
            Id = id ?? string.Empty;
            ActionName = actionName ?? string.Empty;
        }

        public AppUIFocusMapActionTargetKind Kind { get; }

        public string Id { get; }

        public string ActionName { get; }
    }

    public enum AppUIFocusMapRouteKind
    {
        Action = 0,
        Boundary = 1,
        BoundaryResolver = 2,
        RegionAdjacency = 3,
        AutoAdjacent = 4,
    }

    public readonly struct AppUIFocusMapGroup
    {
        internal AppUIFocusMapGroup(
            string groupId,
            string regionId,
            AppUIFocusGroupLayout layout,
            bool openByDefault,
            int order,
            int nodeCount)
        {
            GroupId = groupId ?? string.Empty;
            RegionId = regionId ?? string.Empty;
            Layout = layout;
            OpenByDefault = openByDefault;
            Order = order;
            NodeCount = nodeCount;
        }

        public string GroupId { get; }

        public string RegionId { get; }

        public AppUIFocusGroupLayout Layout { get; }

        public bool OpenByDefault { get; }

        public int Order { get; }

        public int NodeCount { get; }
    }

    public readonly struct AppUIFocusMapNode
    {
        internal AppUIFocusMapNode(
            AppUIFocusNodeAddress address,
            string selectableName,
            int order)
        {
            Address = address;
            SelectableName = selectableName ?? string.Empty;
            Order = order;
        }

        public AppUIFocusNodeAddress Address { get; }

        public string SelectableName { get; }

        public int Order { get; }
    }

    public readonly struct AppUIFocusMapEdge
    {
        internal AppUIFocusMapEdge(
            string sourceGroupId,
            MoveDirection direction,
            AppUIFocusMapRouteKind routeKind,
            string actionName,
            string declaredTarget,
            string resolvedTargetGroupId,
            bool dynamicTarget)
        {
            SourceGroupId = sourceGroupId ?? string.Empty;
            Direction = direction;
            RouteKind = routeKind;
            ActionName = actionName ?? string.Empty;
            DeclaredTarget = declaredTarget ?? string.Empty;
            ResolvedTargetGroupId = resolvedTargetGroupId ?? string.Empty;
            DynamicTarget = dynamicTarget;
        }

        public string SourceGroupId { get; }

        public MoveDirection Direction { get; }

        public AppUIFocusMapRouteKind RouteKind { get; }

        public string ActionName { get; }

        public string DeclaredTarget { get; }

        public string ResolvedTargetGroupId { get; }

        public bool DynamicTarget { get; }
    }

    /// <summary>
    /// Definition 的只读结构快照。用于 Inspector、全量校验和运行时诊断，不参与导航决策。
    /// </summary>
    public sealed class AppUIFocusMap
    {
        internal AppUIFocusMap(
            string scopeId,
            string entryGroupId,
            AppUIFocusMapGroup[] groups,
            AppUIFocusMapNode[] nodes,
            AppUIFocusMapEdge[] edges,
            string[] warnings)
        {
            ScopeId = scopeId ?? string.Empty;
            EntryGroupId = entryGroupId ?? string.Empty;
            Groups = groups ?? Array.Empty<AppUIFocusMapGroup>();
            Nodes = nodes ?? Array.Empty<AppUIFocusMapNode>();
            Edges = edges ?? Array.Empty<AppUIFocusMapEdge>();
            Warnings = warnings ?? Array.Empty<string>();
        }

        public string ScopeId { get; }

        public string EntryGroupId { get; }

        public IReadOnlyList<AppUIFocusMapGroup> Groups { get; }

        public IReadOnlyList<AppUIFocusMapNode> Nodes { get; }

        public IReadOnlyList<AppUIFocusMapEdge> Edges { get; }

        public IReadOnlyList<string> Warnings { get; }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.Append("Focus Map: Scope=")
                .Append(ScopeId)
                .Append(", EntryGroup=")
                .AppendLine(EntryGroupId);
            builder.AppendLine("Groups:");
            for (int i = 0; i < Groups.Count; i++)
            {
                AppUIFocusMapGroup group = Groups[i];
                builder.Append("  ")
                    .Append(group.GroupId)
                    .Append(" [Region=")
                    .Append(group.RegionId)
                    .Append(", Layout=")
                    .Append(group.Layout)
                    .Append(", Open=")
                    .Append(group.OpenByDefault)
                    .Append(", Order=")
                    .Append(group.Order)
                    .Append(", Nodes=")
                    .Append(group.NodeCount)
                    .AppendLine("]");
            }

            builder.AppendLine("Nodes:");
            for (int i = 0; i < Nodes.Count; i++)
            {
                AppUIFocusMapNode node = Nodes[i];
                builder.Append("  ")
                    .Append(node.Address)
                    .Append(" [Order=")
                    .Append(node.Order)
                    .Append(", Selectable=")
                    .Append(node.SelectableName)
                    .AppendLine("]");
            }

            builder.AppendLine("Routes:");
            for (int i = 0; i < Edges.Count; i++)
            {
                AppUIFocusMapEdge edge = Edges[i];
                builder.Append("  ")
                    .Append(edge.SourceGroupId)
                    .Append(" --")
                    .Append(edge.Direction)
                    .Append('/')
                    .Append(edge.RouteKind)
                    .Append("--> ")
                    .Append(
                        edge.DynamicTarget
                            ? "dynamic:" + edge.DeclaredTarget
                            : edge.ResolvedTargetGroupId)
                    .Append(" [")
                    .Append(edge.ActionName)
                    .AppendLine("]");
            }

            if (Warnings.Count > 0)
            {
                builder.AppendLine("Warnings:");
                for (int i = 0; i < Warnings.Count; i++)
                {
                    builder.Append("  - ").AppendLine(Warnings[i]);
                }
            }

            return builder.ToString();
        }
    }

    public static class AppUIFocusMapBuilder
    {
        private static readonly MoveDirection[] Directions =
        {
            MoveDirection.Left,
            MoveDirection.Right,
            MoveDirection.Up,
            MoveDirection.Down,
        };

        public static AppUIFocusMap Build(AppUIFocusDefinition definition)
        {
            return Build(definition, default, false);
        }

        public static AppUIFocusMap Build(
            AppUIFocusDefinition definition,
            AppUIFocusNodeAddress defaultFocusAddress)
        {
            return Build(definition, defaultFocusAddress, false);
        }

        internal static AppUIFocusMap Build(
            AppUIFocusDefinition definition,
            AppUIFocusNodeAddress defaultFocusAddress,
            bool resolveAnchorTargets)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Dictionary<string, AppUIFocusGroupDefinition> groupDefinitions =
                new Dictionary<string, AppUIFocusGroupDefinition>(
                    definition.GroupCount,
                    StringComparer.Ordinal);
            Dictionary<string, AppUIFocusRegionDefinition> regionDefinitions =
                new Dictionary<string, AppUIFocusRegionDefinition>(
                    definition.RegionCount,
                    StringComparer.Ordinal);
            Dictionary<string, int> nodeCountByGroup =
                new Dictionary<string, int>(definition.GroupCount, StringComparer.Ordinal);
            for (int i = 0; i < definition.GroupCount; i++)
            {
                AppUIFocusGroupDefinition group = definition.GetGroup(i);
                if (!groupDefinitions.ContainsKey(group.GroupId))
                {
                    groupDefinitions.Add(group.GroupId, group);
                }
            }

            for (int i = 0; i < definition.RegionCount; i++)
            {
                AppUIFocusRegionDefinition region = definition.GetRegion(i);
                if (!regionDefinitions.ContainsKey(region.RegionId))
                {
                    regionDefinitions.Add(region.RegionId, region);
                }
            }

            List<AppUIFocusMapNode> nodes =
                new List<AppUIFocusMapNode>(definition.NodeCount);
            Dictionary<int, string> groupBySelectableId =
                new Dictionary<int, string>(definition.NodeCount);
            for (int i = 0; i < definition.NodeCount; i++)
            {
                AppUIFocusNodeDefinition node = definition.GetNode(i);
                string selectableName = node.Selectable != null
                    ? node.Selectable.name
                    : string.Empty;
                nodes.Add(
                    new AppUIFocusMapNode(
                        node.Address,
                        selectableName,
                        node.Order));
                if (nodeCountByGroup.TryGetValue(node.Address.GroupId, out int count))
                {
                    nodeCountByGroup[node.Address.GroupId] = count + 1;
                }
                else
                {
                    nodeCountByGroup.Add(node.Address.GroupId, 1);
                }

                if (node.Selectable != null)
                {
                    groupBySelectableId[node.Selectable.GetInstanceID()] =
                        node.Address.GroupId;
                }
            }

            List<AppUIFocusMapGroup> groups =
                new List<AppUIFocusMapGroup>(definition.GroupCount);
            for (int i = 0; i < definition.GroupCount; i++)
            {
                AppUIFocusGroupDefinition group = definition.GetGroup(i);
                AppUIFocusGroupLayout layout = AppUIFocusGroupLayout.Legacy;
                if (definition.FocusChain != null &&
                    definition.FocusChain.TryGetGroupRules(
                        group.GroupId,
                        out AppUIFocusGroupRules rules))
                {
                    layout = rules.Layout;
                }

                nodeCountByGroup.TryGetValue(group.GroupId, out int nodeCount);
                groups.Add(
                    new AppUIFocusMapGroup(
                        group.GroupId,
                        group.RegionId,
                        layout,
                        group.OpenByDefault,
                        group.Order,
                        nodeCount));
            }

            List<AppUIFocusMapEdge> edges = new List<AppUIFocusMapEdge>(16);
            AddChainEdges(
                definition,
                groupDefinitions,
                regionDefinitions,
                groupBySelectableId,
                resolveAnchorTargets,
                edges);
            AddRegionEdges(definition, groupDefinitions, regionDefinitions, edges);

            string entryGroupId = ResolveEntryGroup(
                definition,
                defaultFocusAddress,
                groupDefinitions,
                nodeCountByGroup);
            List<string> warnings = BuildReachabilityWarnings(
                entryGroupId,
                groupDefinitions,
                nodeCountByGroup,
                edges);
            return new AppUIFocusMap(
                definition.ScopeId,
                entryGroupId,
                groups.ToArray(),
                nodes.ToArray(),
                edges.ToArray(),
                warnings.ToArray());
        }

        private static void AddChainEdges(
            AppUIFocusDefinition definition,
            Dictionary<string, AppUIFocusGroupDefinition> groups,
            Dictionary<string, AppUIFocusRegionDefinition> regions,
            Dictionary<int, string> groupBySelectableId,
            bool resolveAnchorTargets,
            List<AppUIFocusMapEdge> edges)
        {
            AppUIFocusChain chain = definition.FocusChain;
            if (chain == null)
            {
                return;
            }

            List<AppUIFocusMapActionTarget> targets =
                new List<AppUIFocusMapActionTarget>(4);
            foreach (AppUIFocusGroupRules rules in chain.GroupRules)
            {
                if (rules == null)
                {
                    continue;
                }

                for (int directionIndex = 0;
                     directionIndex < Directions.Length;
                     directionIndex++)
                {
                    MoveDirection direction = Directions[directionIndex];
                    if (rules.TryGetAction(direction, out AppUIFocusAction action))
                    {
                        AddActionEdges(
                            definition,
                            rules.GroupId,
                            direction,
                            AppUIFocusMapRouteKind.Action,
                            action,
                            groups,
                            regions,
                            groupBySelectableId,
                            resolveAnchorTargets,
                            targets,
                            edges);
                    }

                    if (rules.TryGetBoundaryAction(
                            direction,
                            out AppUIFocusAction boundaryAction))
                    {
                        AddActionEdges(
                            definition,
                            rules.GroupId,
                            direction,
                            AppUIFocusMapRouteKind.Boundary,
                            boundaryAction,
                            groups,
                            regions,
                            groupBySelectableId,
                            resolveAnchorTargets,
                            targets,
                            edges);
                    }

                    if (rules.TryGetBoundaryResolver(direction, out _))
                    {
                        edges.Add(
                            new AppUIFocusMapEdge(
                                rules.GroupId,
                                direction,
                                AppUIFocusMapRouteKind.BoundaryResolver,
                                "CustomBoundaryResolver",
                                "resolver",
                                string.Empty,
                                true));
                    }
                }
            }
        }

        private static void AddActionEdges(
            AppUIFocusDefinition definition,
            string sourceGroupId,
            MoveDirection direction,
            AppUIFocusMapRouteKind routeKind,
            AppUIFocusAction action,
            Dictionary<string, AppUIFocusGroupDefinition> groups,
            Dictionary<string, AppUIFocusRegionDefinition> regions,
            Dictionary<int, string> groupBySelectableId,
            bool resolveAnchorTargets,
            List<AppUIFocusMapActionTarget> targets,
            List<AppUIFocusMapEdge> edges)
        {
            targets.Clear();
            action?.CollectMapTargets(targets);
            for (int i = 0; i < targets.Count; i++)
            {
                AppUIFocusMapActionTarget target = targets[i];
                string resolvedGroupId = ResolveActionTargetGroup(
                    definition,
                    sourceGroupId,
                    target,
                    groups,
                    regions,
                    groupBySelectableId,
                    resolveAnchorTargets);
                edges.Add(
                    new AppUIFocusMapEdge(
                        sourceGroupId,
                        direction,
                        routeKind,
                        target.ActionName,
                        target.Id,
                        resolvedGroupId,
                        string.IsNullOrEmpty(resolvedGroupId)));
            }
        }

        private static string ResolveActionTargetGroup(
            AppUIFocusDefinition definition,
            string sourceGroupId,
            AppUIFocusMapActionTarget target,
            Dictionary<string, AppUIFocusGroupDefinition> groups,
            Dictionary<string, AppUIFocusRegionDefinition> regions,
            Dictionary<int, string> groupBySelectableId,
            bool resolveAnchorTargets)
        {
            switch (target.Kind)
            {
                case AppUIFocusMapActionTargetKind.Group:
                    return groups.ContainsKey(target.Id) ? target.Id : string.Empty;
                case AppUIFocusMapActionTargetKind.Region:
                    return regions.TryGetValue(
                               target.Id,
                               out AppUIFocusRegionDefinition region)
                        ? region.DefaultGroupId
                        : string.Empty;
                case AppUIFocusMapActionTargetKind.ParentRegion:
                    if (groups.TryGetValue(
                            sourceGroupId,
                            out AppUIFocusGroupDefinition sourceGroup) &&
                        regions.TryGetValue(
                            sourceGroup.RegionId,
                            out AppUIFocusRegionDefinition sourceRegion) &&
                        regions.TryGetValue(
                            sourceRegion.ParentRegionId,
                            out AppUIFocusRegionDefinition parentRegion))
                    {
                        return parentRegion.DefaultGroupId;
                    }

                    return string.Empty;
                case AppUIFocusMapActionTargetKind.Anchor:
                    return resolveAnchorTargets
                        ? ResolveAnchorGroup(
                            definition.AnchorTargetProvider,
                            target.Id,
                            groups,
                            groupBySelectableId)
                        : string.Empty;
                case AppUIFocusMapActionTargetKind.TopGroup:
                default:
                    return string.Empty;
            }
        }

        private static string ResolveAnchorGroup(
            IAppUIFocusAnchorTargetProvider provider,
            string anchorId,
            Dictionary<string, AppUIFocusGroupDefinition> groups,
            Dictionary<int, string> groupBySelectableId)
        {
            if (provider == null)
            {
                return string.Empty;
            }

            try
            {
                if (!provider.TryGetFocusAnchor(
                        anchorId,
                        out AppUIFocusTarget target))
                {
                    return string.Empty;
                }

                if (target.Kind == AppUIFocusTargetKind.NodeAddress)
                {
                    return groups.ContainsKey(target.NodeAddress.GroupId)
                        ? target.NodeAddress.GroupId
                        : string.Empty;
                }

                if (target.Kind == AppUIFocusTargetKind.Selectable &&
                    target.Selectable != null &&
                    groupBySelectableId.TryGetValue(
                        target.Selectable.GetInstanceID(),
                        out string groupId))
                {
                    return groupId;
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static void AddRegionEdges(
            AppUIFocusDefinition definition,
            Dictionary<string, AppUIFocusGroupDefinition> groups,
            Dictionary<string, AppUIFocusRegionDefinition> regions,
            List<AppUIFocusMapEdge> edges)
        {
            for (int i = 0; i < definition.RegionAdjacencyCount; i++)
            {
                AppUIFocusRegionAdjacencyDefinition adjacency =
                    definition.GetRegionAdjacency(i);
                edges.Add(
                    new AppUIFocusMapEdge(
                        adjacency.SourceGroupId,
                        adjacency.MoveDirection,
                        AppUIFocusMapRouteKind.RegionAdjacency,
                        "RegionAdjacency",
                        adjacency.TargetGroupId,
                        groups.ContainsKey(adjacency.TargetGroupId)
                            ? adjacency.TargetGroupId
                            : string.Empty,
                        !groups.ContainsKey(adjacency.TargetGroupId)));
            }

            foreach (KeyValuePair<string, AppUIFocusRegionDefinition> pair in regions)
            {
                if (!pair.Value.AutoAdjacent)
                {
                    continue;
                }

                foreach (KeyValuePair<string, AppUIFocusGroupDefinition> source in groups)
                {
                    if (!source.Value.OpenByDefault ||
                        !string.Equals(
                            source.Value.RegionId,
                            pair.Key,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (KeyValuePair<string, AppUIFocusGroupDefinition> target in groups)
                    {
                        if (string.Equals(
                                source.Key,
                                target.Key,
                                StringComparison.Ordinal) ||
                            !target.Value.OpenByDefault ||
                            !string.Equals(
                                target.Value.RegionId,
                                pair.Key,
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        edges.Add(
                            new AppUIFocusMapEdge(
                                source.Key,
                                MoveDirection.None,
                                AppUIFocusMapRouteKind.AutoAdjacent,
                                "SpatialAutoAdjacent",
                                target.Key,
                                target.Key,
                                false));
                    }
                }
            }
        }

        private static string ResolveEntryGroup(
            AppUIFocusDefinition definition,
            AppUIFocusNodeAddress defaultFocusAddress,
            Dictionary<string, AppUIFocusGroupDefinition> groups,
            Dictionary<string, int> nodeCountByGroup)
        {
            if (defaultFocusAddress.IsValid &&
                groups.TryGetValue(
                    defaultFocusAddress.GroupId,
                    out AppUIFocusGroupDefinition defaultGroup) &&
                defaultGroup.OpenByDefault)
            {
                return defaultFocusAddress.GroupId;
            }

            for (int i = 0; i < definition.RegionCount; i++)
            {
                AppUIFocusRegionDefinition region = definition.GetRegion(i);
                if (string.Equals(
                        region.RegionId,
                        AppUIFocusDefinition.RootRegionId,
                        StringComparison.Ordinal) &&
                    !string.IsNullOrEmpty(region.DefaultGroupId) &&
                    groups.TryGetValue(
                        region.DefaultGroupId,
                        out AppUIFocusGroupDefinition regionDefault) &&
                    regionDefault.OpenByDefault)
                {
                    return region.DefaultGroupId;
                }
            }

            string bestGroupId = string.Empty;
            int bestOrder = int.MaxValue;
            for (int i = 0; i < definition.GroupCount; i++)
            {
                AppUIFocusGroupDefinition group = definition.GetGroup(i);
                nodeCountByGroup.TryGetValue(group.GroupId, out int nodeCount);
                if (!group.OpenByDefault || nodeCount == 0)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(bestGroupId) || group.Order < bestOrder)
                {
                    bestGroupId = group.GroupId;
                    bestOrder = group.Order;
                }
            }

            return bestGroupId;
        }

        private static List<string> BuildReachabilityWarnings(
            string entryGroupId,
            Dictionary<string, AppUIFocusGroupDefinition> groups,
            Dictionary<string, int> nodeCountByGroup,
            List<AppUIFocusMapEdge> edges)
        {
            List<string> warnings = new List<string>(8);
            if (string.IsNullOrEmpty(entryGroupId))
            {
                warnings.Add(
                    "Focus Map has no open static entry Group; reachability cannot be proven.");
                return warnings;
            }

            Dictionary<string, List<string>> graph =
                new Dictionary<string, List<string>>(groups.Count, StringComparer.Ordinal);
            for (int i = 0; i < edges.Count; i++)
            {
                AppUIFocusMapEdge edge = edges[i];
                if (edge.DynamicTarget ||
                    string.IsNullOrEmpty(edge.ResolvedTargetGroupId) ||
                    string.Equals(
                        edge.SourceGroupId,
                        edge.ResolvedTargetGroupId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!graph.TryGetValue(
                        edge.SourceGroupId,
                        out List<string> targets))
                {
                    targets = new List<string>(4);
                    graph.Add(edge.SourceGroupId, targets);
                }

                if (!targets.Contains(edge.ResolvedTargetGroupId))
                {
                    targets.Add(edge.ResolvedTargetGroupId);
                }
            }

            HashSet<string> reachable = Traverse(entryGroupId, graph);
            bool reachableDynamicRoute = false;
            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].DynamicTarget && reachable.Contains(edges[i].SourceGroupId))
                {
                    reachableDynamicRoute = true;
                    break;
                }
            }

            foreach (KeyValuePair<string, AppUIFocusGroupDefinition> pair in groups)
            {
                nodeCountByGroup.TryGetValue(pair.Key, out int nodeCount);
                if (!pair.Value.OpenByDefault || nodeCount == 0 || reachable.Contains(pair.Key))
                {
                    continue;
                }

                warnings.Add(
                    reachableDynamicRoute
                        ? "Focus Map cannot prove Group reachability because a dynamic route exists: " +
                          pair.Key
                        : "Focus Group is unreachable from entry Group " +
                          entryGroupId +
                          ": " +
                          pair.Key);
            }

            HashSet<string> oneWayWarnings =
                new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < edges.Count; i++)
            {
                AppUIFocusMapEdge edge = edges[i];
                if (edge.DynamicTarget ||
                    string.IsNullOrEmpty(edge.ResolvedTargetGroupId) ||
                    string.Equals(
                        edge.SourceGroupId,
                        edge.ResolvedTargetGroupId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                HashSet<string> reverseReachable =
                    Traverse(edge.ResolvedTargetGroupId, graph);
                if (reverseReachable.Contains(edge.SourceGroupId))
                {
                    continue;
                }

                string key = edge.SourceGroupId + "\n" + edge.ResolvedTargetGroupId;
                if (oneWayWarnings.Add(key))
                {
                    warnings.Add(
                        "Focus route is one-way with no return path: " +
                        edge.SourceGroupId +
                        " -> " +
                        edge.ResolvedTargetGroupId);
                }
            }

            return warnings;
        }

        private static HashSet<string> Traverse(
            string startGroupId,
            Dictionary<string, List<string>> graph)
        {
            HashSet<string> visited =
                new HashSet<string>(StringComparer.Ordinal);
            Queue<string> queue = new Queue<string>();
            visited.Add(startGroupId);
            queue.Enqueue(startGroupId);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (!graph.TryGetValue(current, out List<string> targets))
                {
                    continue;
                }

                for (int i = 0; i < targets.Count; i++)
                {
                    if (visited.Add(targets[i]))
                    {
                        queue.Enqueue(targets[i]);
                    }
                }
            }

            return visited;
        }
    }
}
