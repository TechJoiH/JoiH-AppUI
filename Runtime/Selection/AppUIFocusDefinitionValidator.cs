using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    public sealed class AppUIFocusValidationReport
    {
        private readonly List<string> errors = new List<string>(8);
        private readonly List<string> warnings = new List<string>(8);

        public IReadOnlyList<string> Errors
        {
            get { return errors; }
        }

        public bool Success
        {
            get { return errors.Count == 0; }
        }

        public IReadOnlyList<string> Warnings
        {
            get { return warnings; }
        }

        internal void AddError(string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                errors.Add(error);
            }
        }

        internal void AddWarning(string warning)
        {
            if (!string.IsNullOrEmpty(warning))
            {
                warnings.Add(warning);
            }
        }
    }

    /// <summary>
    /// 只读检查 Definition、静态节点、Region 树和显式路由。
    /// </summary>
    public static class AppUIFocusDefinitionValidator
    {
        public static AppUIFocusValidationReport Validate(AppUIFocusDefinition definition)
        {
            return Validate(definition, default, false);
        }

        internal static AppUIFocusValidationReport Validate(
            AppUIFocusDefinition definition,
            AppUIFocusNodeAddress defaultFocusAddress,
            bool resolveAnchorTargets)
        {
            AppUIFocusValidationReport report = new AppUIFocusValidationReport();
            if (definition == null)
            {
                report.AddError("Focus definition is missing.");
                return report;
            }

            Dictionary<string, AppUIFocusRegionDefinition> regions =
                BuildAndValidateRegions(definition, report);

            Dictionary<string, AppUIFocusGroupDefinition> groups =
                new Dictionary<string, AppUIFocusGroupDefinition>(
                    definition.GroupCount,
                    StringComparer.Ordinal);
            for (int i = 0; i < definition.GroupCount; i++)
            {
                AppUIFocusGroupDefinition group = definition.GetGroup(i);
                if (string.IsNullOrWhiteSpace(group.GroupId))
                {
                    report.AddError("Focus definition contains an empty GroupId.");
                    continue;
                }

                if (groups.ContainsKey(group.GroupId))
                {
                    report.AddError("Focus definition contains duplicate GroupId: " + group.GroupId);
                    continue;
                }


                if (string.IsNullOrWhiteSpace(group.RegionId) ||
                    !regions.ContainsKey(group.RegionId))
                {
                    report.AddError(
                        "Focus Group references a missing Region: " +
                        group.GroupId + " -> " + (group.RegionId ?? string.Empty));
                }

                groups.Add(group.GroupId, group);
            }

            ValidateRegionDefaults(regions, groups, report);
            ValidateRegionAdjacencies(definition, regions, groups, report);

            HashSet<AppUIFocusNodeAddress> addresses =
                new HashSet<AppUIFocusNodeAddress>();
            Dictionary<int, AppUIFocusNodeAddress> selectableOwners =
                new Dictionary<int, AppUIFocusNodeAddress>(definition.NodeCount);
            Dictionary<int, AppUIFocusNodeAddress> gameObjectOwners =
                new Dictionary<int, AppUIFocusNodeAddress>(definition.NodeCount);
            for (int i = 0; i < definition.NodeCount; i++)
            {
                AppUIFocusNodeDefinition node = definition.GetNode(i);
                ValidateNode(
                    node,
                    groups,
                    addresses,
                    selectableOwners,
                    gameObjectOwners,
                    report);
            }

            ValidateRegionEntries(definition, regions, groups, report);
            ValidateChain(definition.FocusChain, groups, regions, report);
            if (report.Success)
            {
                AppUIFocusMap focusMap = AppUIFocusMapBuilder.Build(
                    definition,
                    defaultFocusAddress,
                    resolveAnchorTargets);
                for (int i = 0; i < focusMap.Warnings.Count; i++)
                {
                    report.AddWarning(focusMap.Warnings[i]);
                }
            }

            return report;
        }

        private static Dictionary<string, AppUIFocusRegionDefinition> BuildAndValidateRegions(
            AppUIFocusDefinition definition,
            AppUIFocusValidationReport report)
        {
            Dictionary<string, AppUIFocusRegionDefinition> regions =
                new Dictionary<string, AppUIFocusRegionDefinition>(
                    definition.RegionCount,
                    StringComparer.Ordinal);
            for (int i = 0; i < definition.RegionCount; i++)
            {
                AppUIFocusRegionDefinition region = definition.GetRegion(i);
                if (string.IsNullOrWhiteSpace(region.RegionId))
                {
                    report.AddError("Focus definition contains an empty RegionId.");
                    continue;
                }

                if (regions.ContainsKey(region.RegionId))
                {
                    report.AddError(
                        "Focus definition contains duplicate RegionId: " + region.RegionId);
                    continue;
                }

                regions.Add(region.RegionId, region);
            }

            if (!regions.TryGetValue(
                    AppUIFocusDefinition.RootRegionId,
                    out AppUIFocusRegionDefinition root))
            {
                report.AddError("Focus definition is missing RootRegion.");
                return regions;
            }

            if (!string.IsNullOrEmpty(root.ParentRegionId))
            {
                report.AddError("RootRegion cannot declare a parent Region.");
            }

            foreach (KeyValuePair<string, AppUIFocusRegionDefinition> pair in regions)
            {
                AppUIFocusRegionDefinition region = pair.Value;
                if (string.Equals(
                        region.RegionId,
                        AppUIFocusDefinition.RootRegionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(region.ParentRegionId) ||
                    !regions.ContainsKey(region.ParentRegionId))
                {
                    report.AddError(
                        "Focus Region references a missing parent: " +
                        region.RegionId + " -> " + region.ParentRegionId);
                    continue;
                }

                HashSet<string> path = new HashSet<string>(StringComparer.Ordinal);
                string currentRegionId = region.RegionId;
                int depth = 0;
                while (!string.Equals(
                           currentRegionId,
                           AppUIFocusDefinition.RootRegionId,
                           StringComparison.Ordinal))
                {
                    if (!path.Add(currentRegionId))
                    {
                        report.AddError(
                            "Focus Region parent cycle detected at: " + region.RegionId);
                        break;
                    }

                    if (!regions.TryGetValue(
                            currentRegionId,
                            out AppUIFocusRegionDefinition currentRegion) ||
                        string.IsNullOrEmpty(currentRegion.ParentRegionId))
                    {
                        break;
                    }

                    depth++;
                    if (depth > 4)
                    {
                        report.AddError(
                            "Focus Region depth exceeds the maximum of 4: " + region.RegionId);
                        break;
                    }

                    currentRegionId = currentRegion.ParentRegionId;
                }
            }

            return regions;
        }

        private static void ValidateRegionDefaults(
            Dictionary<string, AppUIFocusRegionDefinition> regions,
            Dictionary<string, AppUIFocusGroupDefinition> groups,
            AppUIFocusValidationReport report)
        {
            foreach (KeyValuePair<string, AppUIFocusRegionDefinition> pair in regions)
            {
                AppUIFocusRegionDefinition region = pair.Value;
                if (string.IsNullOrEmpty(region.DefaultGroupId))
                {
                    continue;
                }

                if (!groups.TryGetValue(
                        region.DefaultGroupId,
                        out AppUIFocusGroupDefinition group))
                {
                    report.AddError(
                        "Focus Region default entry references a missing Group: " +
                        region.RegionId + " -> " + region.DefaultGroupId);
                }
                else if (!string.Equals(
                             group.RegionId,
                             region.RegionId,
                             StringComparison.Ordinal))
                {
                    report.AddError(
                        "Focus Region default entry must belong to the same Region: " +
                        region.RegionId + " -> " + region.DefaultGroupId);
                }
                else if (!group.OpenByDefault)
                {
                    report.AddError(
                        "Focus Region default entry Group is closed by default: " +
                        region.DefaultGroupId);
                }
            }
        }

        private static void ValidateRegionAdjacencies(
            AppUIFocusDefinition definition,
            Dictionary<string, AppUIFocusRegionDefinition> regions,
            Dictionary<string, AppUIFocusGroupDefinition> groups,
            AppUIFocusValidationReport report)
        {
            HashSet<string> routeKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.RegionAdjacencyCount; i++)
            {
                AppUIFocusRegionAdjacencyDefinition adjacency =
                    definition.GetRegionAdjacency(i);
                string routeKey = adjacency.RegionId + "\n" +
                                  adjacency.SourceGroupId + "\n" +
                                  (int)adjacency.MoveDirection;
                if (!routeKeys.Add(routeKey))
                {
                    report.AddError(
                        "Focus Region contains duplicate adjacency: " + routeKey);
                }

                if (!regions.ContainsKey(adjacency.RegionId))
                {
                    report.AddError(
                        "Focus adjacency references a missing Region: " +
                        adjacency.RegionId);
                    continue;
                }

                if (!groups.TryGetValue(
                        adjacency.SourceGroupId,
                        out AppUIFocusGroupDefinition sourceGroup) ||
                    !groups.TryGetValue(
                        adjacency.TargetGroupId,
                        out AppUIFocusGroupDefinition targetGroup))
                {
                    report.AddError(
                        "Focus adjacency references a missing Group: " +
                        adjacency.SourceGroupId + " -> " + adjacency.TargetGroupId);
                    continue;
                }

                if (!string.Equals(
                        sourceGroup.RegionId,
                        adjacency.RegionId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        targetGroup.RegionId,
                        adjacency.RegionId,
                        StringComparison.Ordinal))
                {
                    report.AddError(
                        "Focus adjacency Groups must belong to Region: " +
                        adjacency.RegionId);
                }

                if (!targetGroup.OpenByDefault)
                {
                    report.AddError(
                        "Focus adjacency targets a Group that is closed by default: " +
                        adjacency.TargetGroupId);
                }
            }
        }

        private static void ValidateNode(
            AppUIFocusNodeDefinition node,
            Dictionary<string, AppUIFocusGroupDefinition> groups,
            HashSet<AppUIFocusNodeAddress> addresses,
            Dictionary<int, AppUIFocusNodeAddress> selectableOwners,
            Dictionary<int, AppUIFocusNodeAddress> gameObjectOwners,
            AppUIFocusValidationReport report)
        {
            if (!node.Address.IsValid)
            {
                report.AddError("Focus definition contains an invalid NodeAddress.");
                return;
            }

            if (!groups.ContainsKey(node.Address.GroupId))
            {
                report.AddError(
                    "Focus node references a missing Group: " + node.Address);
            }

            if (!addresses.Add(node.Address))
            {
                report.AddError("Focus definition contains duplicate NodeAddress: " + node.Address);
            }

            Selectable selectable = node.Selectable;
            if (selectable == null || selectable.gameObject == null)
            {
                report.AddError("Focus node has a missing Selectable: " + node.Address);
                return;
            }

            if (selectable.navigation.mode != Navigation.Mode.None)
            {
                report.AddError(
                    "Registered Selectable must use Navigation.Mode.None: " + node.Address);
            }

            if (selectable.transition == Selectable.Transition.None &&
                !HasFocusVisualHandler(selectable))
            {
                report.AddWarning(
                    "Focusable Selectable has no built-in transition or explicit select/deselect visual handler: " +
                    node.Address);
            }

            int selectableId = selectable.GetInstanceID();
            if (selectableOwners.TryGetValue(
                    selectableId,
                    out AppUIFocusNodeAddress selectableOwner))
            {
                report.AddError(
                    "Selectable is registered to multiple focus nodes: " +
                    selectableOwner +
                    " and " +
                    node.Address);
            }
            else
            {
                selectableOwners.Add(selectableId, node.Address);
            }

            int gameObjectId = selectable.gameObject.GetInstanceID();
            if (gameObjectOwners.TryGetValue(
                    gameObjectId,
                    out AppUIFocusNodeAddress gameObjectOwner))
            {
                report.AddError(
                    "GameObject is registered to multiple focus nodes: " +
                    gameObjectOwner +
                    " and " +
                    node.Address);
            }
            else
            {
                gameObjectOwners.Add(gameObjectId, node.Address);
            }
        }

        private static void ValidateRegionEntries(
            AppUIFocusDefinition definition,
            Dictionary<string, AppUIFocusRegionDefinition> regions,
            Dictionary<string, AppUIFocusGroupDefinition> groups,
            AppUIFocusValidationReport report)
        {
            foreach (KeyValuePair<string, AppUIFocusRegionDefinition> pair in regions)
            {
                bool hasOpenGroup = false;
                bool hasStaticOrVirtualizedEntry = false;
                foreach (KeyValuePair<string, AppUIFocusGroupDefinition> groupPair in groups)
                {
                    AppUIFocusGroupDefinition group = groupPair.Value;
                    if (!group.OpenByDefault ||
                        !string.Equals(
                            group.RegionId,
                            pair.Key,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    hasOpenGroup = true;
                    if (group.VirtualizationAdapter != null ||
                        HasStaticNode(definition, group.GroupId))
                    {
                        hasStaticOrVirtualizedEntry = true;
                    }
                }

                if (!hasOpenGroup)
                {
                    report.AddError(
                        "Focus Region has no Group that is open by default: " + pair.Key);
                    continue;
                }

                if (!hasStaticOrVirtualizedEntry)
                {
                    report.AddWarning(
                        "Focus Region has no static or virtualized entry Node; runtime registration must complete before activation: " +
                        pair.Key);
                }

                if (!string.Equals(
                        pair.Key,
                        AppUIFocusDefinition.RootRegionId,
                        StringComparison.Ordinal) &&
                    string.IsNullOrEmpty(pair.Value.DefaultGroupId))
                {
                    report.AddWarning(
                        "Child Focus Region does not declare a default Group; entry depends on ordered runtime fallback: " +
                        pair.Key);
                }
            }
        }

        private static bool HasStaticNode(
            AppUIFocusDefinition definition,
            string groupId)
        {
            for (int i = 0; i < definition.NodeCount; i++)
            {
                if (string.Equals(
                        definition.GetNode(i).Address.GroupId,
                        groupId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasFocusVisualHandler(Selectable selectable)
        {
            if (selectable.GetComponent<AppUISelectionVisualState>() != null)
            {
                return true;
            }

            MonoBehaviour[] behaviours = selectable.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null &&
                    behaviour is ISelectHandler &&
                    behaviour is IDeselectHandler &&
                    !(behaviour is AppUIFocusGroupNode))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateChain(
            AppUIFocusChain chain,
            Dictionary<string, AppUIFocusGroupDefinition> groups,
            Dictionary<string, AppUIFocusRegionDefinition> regions,
            AppUIFocusValidationReport report)
        {
            if (chain == null)
            {
                return;
            }

            HashSet<string> routeTargets = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> regionTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (AppUIFocusGroupRules rules in chain.GroupRules)
            {
                if (rules == null || string.IsNullOrWhiteSpace(rules.GroupId))
                {
                    report.AddError("Focus chain contains an empty GroupId.");
                    continue;
                }

                if (!groups.ContainsKey(rules.GroupId))
                {
                    report.AddError(
                        "Focus chain references a missing Group: " + rules.GroupId);
                }

                bool semanticGroup = rules.Layout != AppUIFocusGroupLayout.Legacy;
                if (semanticGroup && rules.HasLegacyActions)
                {
                    report.AddError(
                        "Semantic Group cannot use legacy On(...) actions: " + rules.GroupId);
                }
                else if (!semanticGroup && rules.HasSemanticConfiguration)
                {
                    report.AddError(
                        "Legacy Group cannot use semantic boundary, entry, or layout rules: " +
                        rules.GroupId);
                }

                if (rules.Layout == AppUIFocusGroupLayout.Custom &&
                    rules.LayoutResolver == null)
                {
                    report.AddError(
                        "Custom Group requires a layout resolver: " + rules.GroupId);
                }

                if (rules.Layout == AppUIFocusGroupLayout.Grid &&
                    rules.GridColumnCount < 1)
                {
                    report.AddError(
                        "Grid Group requires at least one column: " + rules.GroupId);
                }

                routeTargets.Clear();
                rules.CollectReferencedGroups(routeTargets);
                foreach (string targetGroupId in routeTargets)
                {
                    if (string.IsNullOrWhiteSpace(targetGroupId) ||
                        !groups.TryGetValue(
                            targetGroupId,
                            out AppUIFocusGroupDefinition targetGroup))
                    {
                        report.AddError(
                            "Focus route targets a missing Group: " +
                            (targetGroupId ?? string.Empty));
                    }
                    else if (!targetGroup.OpenByDefault)
                    {
                        report.AddError(
                            "Focus route targets a Group that is closed by default: " +
                            targetGroupId);
                    }
                    else if (groups.TryGetValue(
                                 rules.GroupId,
                                 out AppUIFocusGroupDefinition sourceGroup) &&
                             !string.Equals(
                                 sourceGroup.RegionId,
                                 targetGroup.RegionId,
                                 StringComparison.Ordinal))
                    {
                        report.AddError(
                            "Focus Group route cannot cross Region; use a Region route: " +
                            rules.GroupId + " -> " + targetGroupId);
                    }
                }

                regionTargets.Clear();
                rules.CollectReferencedRegions(regionTargets);
                foreach (string targetRegionId in regionTargets)
                {
                    if (string.IsNullOrWhiteSpace(targetRegionId) ||
                        !regions.ContainsKey(targetRegionId))
                    {
                        report.AddError(
                            "Focus route targets a missing Region: " +
                            (targetRegionId ?? string.Empty));
                    }
                }
            }
        }
    }
}
