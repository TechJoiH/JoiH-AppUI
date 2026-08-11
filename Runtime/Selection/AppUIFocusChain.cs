using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    public interface IAppUIFocusAnchorProvider
    {
        bool TryGetFocusAnchor(string anchorId, out Selectable selectable);
    }

    internal interface IAppUIFocusTargetAnchorProvider
    {
        bool TryGetFocusAnchorTarget(
            string anchorId,
            out AppUIFocusTarget target);
    }

    public enum AppUIFocusGroupLayout
    {
        Legacy = 0,
        Single = 1,
        Vertical = 2,
        Horizontal = 3,
        Grid = 4,
        Custom = 5,
        Spatial = 6,
    }

    public enum AppUIFocusWrapPolicy
    {
        Stop = 0,
        Cycle = 1,
    }

    public enum AppUIFocusMoveStage
    {
        BeforeMove = 0,
        Layout = 1,
        Boundary = 2,
        ControlPolicy = 3,
    }

    public enum AppUIFocusMoveResult
    {
        ContinueDefault = 0,
        FocusTarget = 1,
        BoundaryReached = 2,
        Consumed = 3,
        Blocked = 4,
    }

    public enum AppUIFocusEntryPolicy
    {
        FirstUsable = 0,
        LastUsable = 1,
        LastFocusedOrFirst = 2,
        PreserveOrdinalOrClamp = 3,
        NearestOnEntryAxis = 4,
        AnchorOrFirst = 5,
    }

    /// <summary>
    /// A special focus rule returns a decision only. The navigator remains the sole owner of
    /// validating and committing EventSystem focus so group state cannot drift out of sync.
    /// </summary>
    public readonly struct AppUIFocusMoveDecision
    {
        private AppUIFocusMoveDecision(
            AppUIFocusMoveResult result,
            Selectable target)
        {
            Result = result;
            Target = target;
        }

        public AppUIFocusMoveResult Result { get; }

        public Selectable Target { get; }

        public static AppUIFocusMoveDecision ContinueDefault()
        {
            return new AppUIFocusMoveDecision(
                AppUIFocusMoveResult.ContinueDefault,
                null);
        }

        public static AppUIFocusMoveDecision Focus(Selectable target)
        {
            return new AppUIFocusMoveDecision(
                AppUIFocusMoveResult.FocusTarget,
                target);
        }

        public static AppUIFocusMoveDecision ReachBoundary()
        {
            return new AppUIFocusMoveDecision(
                AppUIFocusMoveResult.BoundaryReached,
                null);
        }

        public static AppUIFocusMoveDecision Consume()
        {
            return new AppUIFocusMoveDecision(
                AppUIFocusMoveResult.Consumed,
                null);
        }

        public static AppUIFocusMoveDecision Block()
        {
            return new AppUIFocusMoveDecision(
                AppUIFocusMoveResult.Blocked,
                null);
        }
    }

    public readonly struct AppUIFocusMoveContext
    {
        internal AppUIFocusMoveContext(
            string groupId,
            Selectable currentSelectable,
            MoveDirection moveDirection,
            AppUIFocusMoveStage stage,
            AppUIFocusGroupLayout layout,
            AppUIFocusWrapPolicy wrapPolicy,
            int gridColumnCount,
            AppUIFocusGridShortRowPolicy gridShortRowPolicy,
            int currentIndex,
            int nodeCount)
        {
            GroupId = groupId ?? string.Empty;
            CurrentSelectable = currentSelectable;
            MoveDirection = moveDirection;
            Stage = stage;
            Layout = layout;
            WrapPolicy = wrapPolicy;
            GridColumnCount = gridColumnCount;
            GridShortRowPolicy = gridShortRowPolicy;
            CurrentIndex = currentIndex;
            NodeCount = nodeCount;
        }

        public string GroupId { get; }

        public Selectable CurrentSelectable { get; }

        public MoveDirection MoveDirection { get; }

        public AppUIFocusMoveStage Stage { get; }

        public AppUIFocusGroupLayout Layout { get; }

        public AppUIFocusWrapPolicy WrapPolicy { get; }

        public int GridColumnCount { get; }

        public AppUIFocusGridShortRowPolicy GridShortRowPolicy { get; }

        public int CurrentIndex { get; }

        public int NodeCount { get; }
    }

    public readonly struct AppUIFocusEntryContext
    {
        internal AppUIFocusEntryContext(
            string scopeId,
            string sourceRegionId,
            string targetRegionId,
            string sourceGroupId,
            string targetGroupId,
            AppUIFocusNodeAddress sourceNodeAddress,
            Selectable sourceSelectable,
            MoveDirection moveDirection,
            AppUIFocusNodeAddress targetLastFocusedAddress,
            int targetLastIndex,
            int targetNodeCount)
        {
            ScopeId = scopeId ?? string.Empty;
            SourceRegionId = sourceRegionId ?? string.Empty;
            TargetRegionId = targetRegionId ?? string.Empty;
            SourceGroupId = sourceGroupId ?? string.Empty;
            TargetGroupId = targetGroupId ?? string.Empty;
            SourceNodeAddress = sourceNodeAddress;
            SourceSelectable = sourceSelectable;
            MoveDirection = moveDirection;
            TargetLastFocusedAddress = targetLastFocusedAddress;
            TargetLastIndex = targetLastIndex;
            TargetNodeCount = targetNodeCount;
        }

        public string ScopeId { get; }

        public string SourceRegionId { get; }

        public string TargetRegionId { get; }

        public string SourceGroupId { get; }

        public string TargetGroupId { get; }

        public AppUIFocusNodeAddress SourceNodeAddress { get; }

        public Selectable SourceSelectable { get; }

        public MoveDirection MoveDirection { get; }

        public AppUIFocusNodeAddress TargetLastFocusedAddress { get; }

        public int TargetLastIndex { get; }

        public int TargetNodeCount { get; }
    }

    public interface IAppUIFocusMoveRule
    {
        AppUIFocusMoveDecision Evaluate(in AppUIFocusMoveContext context);
    }

    public interface IAppUIFocusBoundaryResolver
    {
        AppUIFocusMoveDecision Resolve(in AppUIFocusMoveContext context);
    }

    public interface IAppUIFocusLayoutResolver
    {
        AppUIFocusMoveDecision Resolve(in AppUIFocusMoveContext context);
    }

    public interface IAppUIFocusEntryResolver
    {
        bool TryResolve(
            in AppUIFocusEntryContext context,
            out Selectable selectable);
    }

    public sealed class AppUIFocusChain
    {
        private readonly Dictionary<string, AppUIFocusGroupRules> groups;

        internal AppUIFocusChain(Dictionary<string, AppUIFocusGroupRules> groupRules)
        {
            groups = groupRules ?? new Dictionary<string, AppUIFocusGroupRules>(0);
        }

        internal bool TryGetAction(
            string groupId,
            MoveDirection moveDirection,
            out AppUIFocusAction action)
        {
            action = null;
            if (string.IsNullOrEmpty(groupId) ||
                !groups.TryGetValue(groupId, out AppUIFocusGroupRules rules))
            {
                return false;
            }

            return rules.TryGetAction(moveDirection, out action);
        }

        internal bool TryGetGroupRules(
            string groupId,
            out AppUIFocusGroupRules rules)
        {
            rules = null;
            return !string.IsNullOrEmpty(groupId) &&
                groups.TryGetValue(groupId, out rules);
        }

        internal IEnumerable<AppUIFocusGroupRules> GroupRules
        {
            get { return groups.Values; }
        }

        internal bool IsSemanticGroup(string groupId)
        {
            return TryGetGroupRules(groupId, out AppUIFocusGroupRules rules) &&
                   rules.Layout != AppUIFocusGroupLayout.Legacy;
        }
    }

    public sealed class AppUIFocusChainBuilder
    {
        private readonly Dictionary<string, AppUIFocusGroupRules> groups =
            new Dictionary<string, AppUIFocusGroupRules>(8);

        public AppUIFocusGroupRuleBuilder Group(string groupId)
        {
            AppUIFocusGroupRules rules = GetOrCreateGroup(groupId);
            return new AppUIFocusGroupRuleBuilder(this, rules);
        }

        public AppUIFocusGroupRuleBuilder SingleGroup(string groupId)
        {
            return ConfigureGroup(
                groupId,
                AppUIFocusGroupLayout.Single,
                AppUIFocusWrapPolicy.Stop,
                0,
                AppUIFocusGridShortRowPolicy.Reject);
        }

        public AppUIFocusGroupRuleBuilder VerticalGroup(
            string groupId,
            AppUIFocusWrapPolicy wrapPolicy = AppUIFocusWrapPolicy.Stop)
        {
            return ConfigureGroup(
                groupId,
                AppUIFocusGroupLayout.Vertical,
                wrapPolicy,
                0,
                AppUIFocusGridShortRowPolicy.Reject);
        }

        public AppUIFocusGroupRuleBuilder HorizontalGroup(
            string groupId,
            AppUIFocusWrapPolicy wrapPolicy = AppUIFocusWrapPolicy.Stop)
        {
            return ConfigureGroup(
                groupId,
                AppUIFocusGroupLayout.Horizontal,
                wrapPolicy,
                0,
                AppUIFocusGridShortRowPolicy.Reject);
        }

        public AppUIFocusGroupRuleBuilder GridGroup(
            string groupId,
            int columnCount,
            AppUIFocusGridShortRowPolicy shortRowPolicy =
                AppUIFocusGridShortRowPolicy.Reject)
        {
            return ConfigureGroup(
                groupId,
                AppUIFocusGroupLayout.Grid,
                AppUIFocusWrapPolicy.Stop,
                columnCount,
                shortRowPolicy);
        }

        public AppUIFocusGroupRuleBuilder SpatialGroup(string groupId)
        {
            return ConfigureGroup(
                groupId,
                AppUIFocusGroupLayout.Spatial,
                AppUIFocusWrapPolicy.Stop,
                0,
                AppUIFocusGridShortRowPolicy.Reject);
        }

        public AppUIFocusGroupRuleBuilder CustomGroup(
            string groupId,
            IAppUIFocusLayoutResolver layoutResolver)
        {
            AppUIFocusGroupRuleBuilder groupBuilder = ConfigureGroup(
                groupId,
                AppUIFocusGroupLayout.Custom,
                AppUIFocusWrapPolicy.Stop,
                0,
                AppUIFocusGridShortRowPolicy.Reject);
            return groupBuilder.ResolveLayoutWith(layoutResolver);
        }

        public AppUIFocusChain Build()
        {
            Dictionary<string, AppUIFocusGroupRules> builtGroups =
                new Dictionary<string, AppUIFocusGroupRules>(groups.Count);
            foreach (KeyValuePair<string, AppUIFocusGroupRules> pair in groups)
            {
                builtGroups.Add(pair.Key, pair.Value.Clone());
            }

            return new AppUIFocusChain(builtGroups);
        }

        private AppUIFocusGroupRules GetOrCreateGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                groupId = string.Empty;
            }

            if (!groups.TryGetValue(groupId, out AppUIFocusGroupRules rules))
            {
                rules = new AppUIFocusGroupRules(groupId);
                groups.Add(groupId, rules);
            }

            return rules;
        }

        private AppUIFocusGroupRuleBuilder ConfigureGroup(
            string groupId,
            AppUIFocusGroupLayout layout,
            AppUIFocusWrapPolicy wrapPolicy,
            int gridColumnCount,
            AppUIFocusGridShortRowPolicy gridShortRowPolicy)
        {
            AppUIFocusGroupRules rules = GetOrCreateGroup(groupId);
            rules.ConfigureLayout(
                layout,
                wrapPolicy,
                gridColumnCount,
                gridShortRowPolicy);
            return new AppUIFocusGroupRuleBuilder(this, rules);
        }
    }

    public sealed class AppUIFocusGroupRuleBuilder
    {
        private readonly AppUIFocusChainBuilder builder;
        private readonly AppUIFocusGroupRules rules;

        internal AppUIFocusGroupRuleBuilder(
            AppUIFocusChainBuilder chainBuilder,
            AppUIFocusGroupRules groupRules)
        {
            builder = chainBuilder;
            rules = groupRules;
        }

        public AppUIFocusRuleTargetBuilder On(MoveDirection moveDirection)
        {
            return new AppUIFocusRuleTargetBuilder(this, moveDirection, false);
        }

        public AppUIFocusGroupRuleBuilder On(MoveDirection moveDirection, AppUIFocusAction action)
        {
            rules.SetAction(moveDirection, action);
            return this;
        }

        public AppUIFocusRuleTargetBuilder AtBoundary(MoveDirection moveDirection)
        {
            return new AppUIFocusRuleTargetBuilder(this, moveDirection, true);
        }

        public AppUIFocusGroupRuleBuilder AtBoundary(
            MoveDirection moveDirection,
            AppUIFocusAction action)
        {
            rules.SetBoundaryAction(moveDirection, action);
            return this;
        }

        public AppUIFocusGroupRuleBuilder AtBoundary(
            MoveDirection moveDirection,
            IAppUIFocusBoundaryResolver resolver)
        {
            rules.SetBoundaryResolver(moveDirection, resolver);
            return this;
        }

        public AppUIFocusGroupRuleBuilder BeforeMove(IAppUIFocusMoveRule moveRule)
        {
            rules.SetBeforeMoveRule(moveRule);
            return this;
        }

        public AppUIFocusGroupRuleBuilder ResolveLayoutWith(
            IAppUIFocusLayoutResolver layoutResolver)
        {
            rules.SetLayoutResolver(layoutResolver);
            return this;
        }

        public AppUIFocusGroupRuleBuilder EnterWith(AppUIFocusEntryPolicy entryPolicy)
        {
            rules.SetEntryPolicy(entryPolicy);
            return this;
        }

        public AppUIFocusGroupRuleBuilder EnterWith(IAppUIFocusEntryResolver entryResolver)
        {
            rules.SetEntryResolver(entryResolver);
            return this;
        }

        public AppUIFocusGroupRuleBuilder EnterWithAnchor(string anchorId)
        {
            rules.SetEntryAnchor(anchorId);
            return this;
        }

        public AppUIFocusGroupRuleBuilder Group(string groupId)
        {
            return builder.Group(groupId);
        }

        public AppUIFocusGroupRuleBuilder SingleGroup(string groupId)
        {
            return builder.SingleGroup(groupId);
        }

        public AppUIFocusGroupRuleBuilder VerticalGroup(
            string groupId,
            AppUIFocusWrapPolicy wrapPolicy = AppUIFocusWrapPolicy.Stop)
        {
            return builder.VerticalGroup(groupId, wrapPolicy);
        }

        public AppUIFocusGroupRuleBuilder HorizontalGroup(
            string groupId,
            AppUIFocusWrapPolicy wrapPolicy = AppUIFocusWrapPolicy.Stop)
        {
            return builder.HorizontalGroup(groupId, wrapPolicy);
        }

        public AppUIFocusGroupRuleBuilder GridGroup(
            string groupId,
            int columnCount,
            AppUIFocusGridShortRowPolicy shortRowPolicy =
                AppUIFocusGridShortRowPolicy.Reject)
        {
            return builder.GridGroup(groupId, columnCount, shortRowPolicy);
        }

        public AppUIFocusGroupRuleBuilder SpatialGroup(string groupId)
        {
            return builder.SpatialGroup(groupId);
        }

        public AppUIFocusGroupRuleBuilder CustomGroup(
            string groupId,
            IAppUIFocusLayoutResolver layoutResolver)
        {
            return builder.CustomGroup(groupId, layoutResolver);
        }

        public AppUIFocusChain Build()
        {
            return builder.Build();
        }

        internal AppUIFocusGroupRuleBuilder Set(MoveDirection moveDirection, AppUIFocusAction action)
        {
            rules.SetAction(moveDirection, action);
            return this;
        }

        internal AppUIFocusGroupRuleBuilder SetBoundary(
            MoveDirection moveDirection,
            AppUIFocusAction action)
        {
            rules.SetBoundaryAction(moveDirection, action);
            return this;
        }

        internal AppUIFocusGroupRuleBuilder SetBoundaryResolver(
            MoveDirection moveDirection,
            IAppUIFocusBoundaryResolver resolver)
        {
            rules.SetBoundaryResolver(moveDirection, resolver);
            return this;
        }
    }

    public sealed class AppUIFocusRuleTargetBuilder
    {
        private readonly AppUIFocusGroupRuleBuilder groupBuilder;
        private readonly MoveDirection moveDirection;
        private readonly bool boundaryOnly;

        internal AppUIFocusRuleTargetBuilder(
            AppUIFocusGroupRuleBuilder builder,
            MoveDirection direction,
            bool isBoundaryOnly)
        {
            groupBuilder = builder;
            moveDirection = direction;
            boundaryOnly = isBoundaryOnly;
        }

        public AppUIFocusGroupRuleBuilder Do(AppUIFocusAction action)
        {
            return boundaryOnly
                ? groupBuilder.SetBoundary(moveDirection, action)
                : groupBuilder.Set(moveDirection, action);
        }

        public AppUIFocusGroupRuleBuilder Resolve(
            IAppUIFocusBoundaryResolver resolver)
        {
            if (!boundaryOnly)
            {
                throw new InvalidOperationException(
                    "A boundary resolver can only be configured through AtBoundary(...).Resolve(...).");
            }

            return groupBuilder.SetBoundaryResolver(moveDirection, resolver);
        }

        public AppUIFocusGroupRuleBuilder Move(int delta, bool wrap)
        {
            return Do(AppUIFocusAction.Move(delta, wrap));
        }

        public AppUIFocusGroupRuleBuilder MoveGrid(
            int columnCount,
            int columnDelta,
            int rowDelta)
        {
            return Do(AppUIFocusAction.MoveGrid(
                columnCount,
                columnDelta,
                rowDelta));
        }

        public AppUIFocusGroupRuleBuilder MoveGrid(
            int columnCount,
            int columnDelta,
            int rowDelta,
            AppUIFocusGridShortRowPolicy shortRowPolicy)
        {
            return Do(AppUIFocusAction.MoveGrid(
                columnCount,
                columnDelta,
                rowDelta,
                shortRowPolicy));
        }

        public AppUIFocusGroupRuleBuilder FocusGroupFirst(string groupId)
        {
            return Do(AppUIFocusAction.FocusGroupFirst(groupId));
        }

        public AppUIFocusGroupRuleBuilder FocusGroupLastFocused(string groupId)
        {
            return Do(AppUIFocusAction.FocusGroupLastFocused(groupId));
        }

        public AppUIFocusGroupRuleBuilder FocusGroup(string groupId)
        {
            return Do(AppUIFocusAction.FocusGroup(groupId));
        }

        public AppUIFocusGroupRuleBuilder FocusTopGroupLastFocused()
        {
            return Do(AppUIFocusAction.FocusTopGroupLastFocused());
        }

        public AppUIFocusGroupRuleBuilder FocusAnchor(string anchorId)
        {
            return Do(AppUIFocusAction.FocusAnchor(anchorId));
        }

        public AppUIFocusGroupRuleBuilder FocusRegionDefault(string regionId)
        {
            return Do(AppUIFocusAction.FocusRegionDefault(regionId));
        }

        public AppUIFocusGroupRuleBuilder FocusRegionLastFocused(string regionId)
        {
            return Do(AppUIFocusAction.FocusRegionLastFocused(regionId));
        }

        public AppUIFocusGroupRuleBuilder ExitToParentRegion()
        {
            return Do(AppUIFocusAction.ExitToParentRegion());
        }
    }

    public sealed class AppUIFocusAction
    {
        private enum ActionKind
        {
            None = 0,
            Move = 1,
            FocusGroupFirst = 2,
            FocusGroupLastFocused = 3,
            FocusTopGroupLastFocused = 4,
            FocusAnchor = 5,
            Fallback = 6,
            WhenCurrentIsAnchor = 7,
            MoveGrid = 8,
            FocusGroup = 9,
            FocusRegionDefault = 10,
            FocusRegionLastFocused = 11,
            ExitToParentRegion = 12,
        }

        private readonly ActionKind kind;
        private readonly string id;
        private readonly int delta;
        private readonly bool wrap;
        private readonly AppUIFocusAction primary;
        private readonly AppUIFocusAction fallback;
        private readonly int gridColumnCount;
        private readonly int gridColumnDelta;
        private readonly int gridRowDelta;
        private readonly AppUIFocusGridShortRowPolicy gridShortRowPolicy;

        private AppUIFocusAction(
            ActionKind actionKind,
            string actionId,
            int moveDelta,
            bool moveWrap,
            AppUIFocusAction primaryAction,
            AppUIFocusAction fallbackAction)
        {
            kind = actionKind;
            id = actionId ?? string.Empty;
            delta = moveDelta;
            wrap = moveWrap;
            primary = primaryAction;
            fallback = fallbackAction;
            gridColumnCount = 0;
            gridColumnDelta = 0;
            gridRowDelta = 0;
            gridShortRowPolicy = AppUIFocusGridShortRowPolicy.Reject;
        }

        private AppUIFocusAction(
            int columnCount,
            int columnDelta,
            int rowDelta,
            AppUIFocusGridShortRowPolicy shortRowPolicy)
        {
            kind = ActionKind.MoveGrid;
            id = string.Empty;
            delta = 0;
            wrap = false;
            primary = null;
            fallback = null;
            gridColumnCount = columnCount;
            gridColumnDelta = columnDelta;
            gridRowDelta = rowDelta;
            gridShortRowPolicy = shortRowPolicy;
        }

        public static AppUIFocusAction Move(int delta, bool wrap)
        {
            return new AppUIFocusAction(ActionKind.Move, string.Empty, delta, wrap, null, null);
        }

        public static AppUIFocusAction MoveGrid(
            int columnCount,
            int columnDelta,
            int rowDelta)
        {
            return new AppUIFocusAction(
                columnCount,
                columnDelta,
                rowDelta,
                AppUIFocusGridShortRowPolicy.Reject);
        }

        public static AppUIFocusAction MoveGrid(
            int columnCount,
            int columnDelta,
            int rowDelta,
            AppUIFocusGridShortRowPolicy shortRowPolicy)
        {
            return new AppUIFocusAction(
                columnCount,
                columnDelta,
                rowDelta,
                shortRowPolicy);
        }

        public static AppUIFocusAction FocusGroupFirst(string groupId)
        {
            return new AppUIFocusAction(ActionKind.FocusGroupFirst, groupId, 0, false, null, null);
        }

        public static AppUIFocusAction FocusGroupLastFocused(string groupId)
        {
            return new AppUIFocusAction(ActionKind.FocusGroupLastFocused, groupId, 0, false, null, null);
        }

        public static AppUIFocusAction FocusGroup(string groupId)
        {
            return new AppUIFocusAction(ActionKind.FocusGroup, groupId, 0, false, null, null);
        }

        public static AppUIFocusAction FocusTopGroupLastFocused()
        {
            return new AppUIFocusAction(ActionKind.FocusTopGroupLastFocused, string.Empty, 0, false, null, null);
        }

        public static AppUIFocusAction FocusAnchor(string anchorId)
        {
            return new AppUIFocusAction(ActionKind.FocusAnchor, anchorId, 0, false, null, null);
        }

        public static AppUIFocusAction FocusRegionDefault(string regionId)
        {
            return new AppUIFocusAction(
                ActionKind.FocusRegionDefault,
                regionId,
                0,
                false,
                null,
                null);
        }

        public static AppUIFocusAction FocusRegionLastFocused(string regionId)
        {
            return new AppUIFocusAction(
                ActionKind.FocusRegionLastFocused,
                regionId,
                0,
                false,
                null,
                null);
        }

        public static AppUIFocusAction ExitToParentRegion()
        {
            return new AppUIFocusAction(
                ActionKind.ExitToParentRegion,
                string.Empty,
                0,
                false,
                null,
                null);
        }

        public static AppUIFocusAction Fallback(
            AppUIFocusAction primaryAction,
            AppUIFocusAction fallbackAction)
        {
            return new AppUIFocusAction(
                ActionKind.Fallback,
                string.Empty,
                0,
                false,
                primaryAction,
                fallbackAction);
        }

        public static AppUIFocusAction WhenCurrentIsAnchor(
            string anchorId,
            AppUIFocusAction thenAction,
            AppUIFocusAction elseAction)
        {
            return new AppUIFocusAction(
                ActionKind.WhenCurrentIsAnchor,
                anchorId,
                0,
                false,
                thenAction,
                elseAction);
        }

        internal bool Execute(AppUIFocusActionContext context)
        {
            switch (kind)
            {
                case ActionKind.Move:
                    return context.Navigator.MoveWithinGroup(context.GroupId, delta, wrap);
                case ActionKind.MoveGrid:
                    return context.Navigator.MoveWithinGrid(
                        context.GroupId,
                        gridColumnCount,
                        gridColumnDelta,
                        gridRowDelta,
                        gridShortRowPolicy);
                case ActionKind.FocusGroupFirst:
                    return context.Navigator.FocusGroupFirst(
                        id,
                        AppUIFocusChangeReason.Navigation);
                case ActionKind.FocusGroupLastFocused:
                    return context.Navigator.FocusGroupLastFocused(
                        id,
                        AppUIFocusChangeReason.Navigation);
                case ActionKind.FocusGroup:
                    return context.Navigator.FocusGroup(
                        id,
                        context.GroupId,
                        context.CurrentSelectable,
                        context.MoveDirection);
                case ActionKind.FocusTopGroupLastFocused:
                    return context.Navigator.FocusTopGroup();
                case ActionKind.FocusAnchor:
                    return FocusAnchor(context, id);
                case ActionKind.FocusRegionDefault:
                    return FocusRegion(
                        context,
                        id,
                        AppUIFocusRegionEntryPolicy.Default);
                case ActionKind.FocusRegionLastFocused:
                    return FocusRegion(
                        context,
                        id,
                        AppUIFocusRegionEntryPolicy.LastFocusedOrDefault);
                case ActionKind.ExitToParentRegion:
                    return context.RegionGateway != null &&
                           context.RegionGateway.ExitToParentRegion(context.GroupId);
                case ActionKind.Fallback:
                    return ExecuteFallback(context);
                case ActionKind.WhenCurrentIsAnchor:
                    return ExecuteConditionalAnchor(context);
            }

            return false;
        }

        internal void CollectReferencedGroups(ISet<string> groupIds)
        {
            if (groupIds == null)
            {
                return;
            }

            switch (kind)
            {
                case ActionKind.FocusGroupFirst:
                case ActionKind.FocusGroupLastFocused:
                case ActionKind.FocusGroup:
                    groupIds.Add(id);
                    break;
                case ActionKind.Fallback:
                case ActionKind.WhenCurrentIsAnchor:
                    primary?.CollectReferencedGroups(groupIds);
                    fallback?.CollectReferencedGroups(groupIds);
                    break;
            }
        }

        internal void CollectReferencedRegions(ISet<string> regionIds)
        {
            if (regionIds == null)
            {
                return;
            }

            switch (kind)
            {
                case ActionKind.FocusRegionDefault:
                case ActionKind.FocusRegionLastFocused:
                    regionIds.Add(id);
                    break;
                case ActionKind.Fallback:
                case ActionKind.WhenCurrentIsAnchor:
                    primary?.CollectReferencedRegions(regionIds);
                    fallback?.CollectReferencedRegions(regionIds);
                    break;
            }
        }

        internal void CollectMapTargets(
            ICollection<AppUIFocusMapActionTarget> targets)
        {
            if (targets == null)
            {
                return;
            }

            switch (kind)
            {
                case ActionKind.FocusGroupFirst:
                    targets.Add(
                        new AppUIFocusMapActionTarget(
                            AppUIFocusMapActionTargetKind.Group,
                            id,
                            "FocusGroupFirst"));
                    break;
                case ActionKind.FocusGroupLastFocused:
                    targets.Add(
                        new AppUIFocusMapActionTarget(
                            AppUIFocusMapActionTargetKind.Group,
                            id,
                            "FocusGroupLastFocused"));
                    break;
                case ActionKind.FocusGroup:
                    targets.Add(
                        new AppUIFocusMapActionTarget(
                            AppUIFocusMapActionTargetKind.Group,
                            id,
                            "FocusGroup"));
                    break;
                case ActionKind.FocusTopGroupLastFocused:
                    targets.Add(
                        new AppUIFocusMapActionTarget(
                            AppUIFocusMapActionTargetKind.TopGroup,
                            string.Empty,
                            "FocusTopGroupLastFocused"));
                    break;
                case ActionKind.FocusAnchor:
                    targets.Add(
                        new AppUIFocusMapActionTarget(
                            AppUIFocusMapActionTargetKind.Anchor,
                            id,
                            "FocusAnchor"));
                    break;
                case ActionKind.FocusRegionDefault:
                    targets.Add(
                        new AppUIFocusMapActionTarget(
                            AppUIFocusMapActionTargetKind.Region,
                            id,
                            "FocusRegionDefault"));
                    break;
                case ActionKind.FocusRegionLastFocused:
                    targets.Add(
                        new AppUIFocusMapActionTarget(
                            AppUIFocusMapActionTargetKind.Region,
                            id,
                            "FocusRegionLastFocused"));
                    break;
                case ActionKind.ExitToParentRegion:
                    targets.Add(
                        new AppUIFocusMapActionTarget(
                            AppUIFocusMapActionTargetKind.ParentRegion,
                            string.Empty,
                            "ExitToParentRegion"));
                    break;
                case ActionKind.Fallback:
                case ActionKind.WhenCurrentIsAnchor:
                    primary?.CollectMapTargets(targets);
                    fallback?.CollectMapTargets(targets);
                    break;
            }
        }

        private bool ExecuteFallback(AppUIFocusActionContext context)
        {
            if (primary != null && primary.Execute(context))
            {
                return true;
            }

            return fallback != null && fallback.Execute(context);
        }

        private bool ExecuteConditionalAnchor(AppUIFocusActionContext context)
        {
            if (IsCurrentAnchor(context, id))
            {
                return primary != null && primary.Execute(context);
            }

            return fallback != null && fallback.Execute(context);
        }

        private static bool FocusAnchor(AppUIFocusActionContext context, string anchorId)
        {
            return context.Navigator.FocusAnchor(
                anchorId,
                AppUIFocusChangeReason.Navigation);
        }

        private static bool FocusRegion(
            AppUIFocusActionContext context,
            string regionId,
            AppUIFocusRegionEntryPolicy entryPolicy)
        {
            return context.RegionGateway != null &&
                   context.RegionGateway.FocusRegion(
                       regionId,
                       entryPolicy,
                       context.GroupId,
                       context.CurrentSelectable,
                       context.MoveDirection);
        }

        private static bool IsCurrentAnchor(AppUIFocusActionContext context, string anchorId)
        {
            return context.Navigator.IsCurrentAnchor(
                anchorId,
                context.CurrentSelectable);
        }
    }

    internal struct AppUIFocusActionContext
    {
        public readonly AppUIFocusGroupNavigator Navigator;
        public readonly IAppUIFocusAnchorProvider AnchorProvider;
        public readonly IAppUIFocusRegionNavigationGateway RegionGateway;
        public readonly string GroupId;
        public readonly Selectable CurrentSelectable;
        public readonly MoveDirection MoveDirection;

        public AppUIFocusActionContext(
            AppUIFocusGroupNavigator navigator,
            IAppUIFocusAnchorProvider anchorProvider,
            IAppUIFocusRegionNavigationGateway regionGateway,
            string groupId,
            Selectable currentSelectable,
            MoveDirection moveDirection)
        {
            Navigator = navigator;
            AnchorProvider = anchorProvider;
            RegionGateway = regionGateway;
            GroupId = groupId;
            CurrentSelectable = currentSelectable;
            MoveDirection = moveDirection;
        }
    }

    internal sealed class AppUIFocusGroupRules
    {
        private readonly Dictionary<MoveDirection, AppUIFocusAction> actions =
            new Dictionary<MoveDirection, AppUIFocusAction>(4);
        private readonly Dictionary<MoveDirection, AppUIFocusAction> boundaryActions =
            new Dictionary<MoveDirection, AppUIFocusAction>(4);
        private readonly Dictionary<MoveDirection, IAppUIFocusBoundaryResolver> boundaryResolvers =
            new Dictionary<MoveDirection, IAppUIFocusBoundaryResolver>(4);
        private IAppUIFocusMoveRule beforeMoveRule;
        private IAppUIFocusLayoutResolver layoutResolver;
        private IAppUIFocusEntryResolver entryResolver;
        private string entryAnchorId = string.Empty;

        public AppUIFocusGroupRules(string groupId)
        {
            GroupId = groupId ?? string.Empty;
        }

        public string GroupId { get; private set; }

        public AppUIFocusGroupLayout Layout { get; private set; }

        public AppUIFocusWrapPolicy WrapPolicy { get; private set; }

        public int GridColumnCount { get; private set; }

        public AppUIFocusGridShortRowPolicy GridShortRowPolicy { get; private set; }

        public IAppUIFocusMoveRule BeforeMoveRule => beforeMoveRule;

        public IAppUIFocusLayoutResolver LayoutResolver => layoutResolver;

        public AppUIFocusEntryPolicy EntryPolicy { get; private set; } =
            AppUIFocusEntryPolicy.LastFocusedOrFirst;

        public IAppUIFocusEntryResolver EntryResolver => entryResolver;

        public string EntryAnchorId => entryAnchorId;

        internal bool HasLegacyActions
        {
            get { return actions.Count > 0; }
        }

        internal bool HasSemanticConfiguration
        {
            get
            {
                return boundaryActions.Count > 0 ||
                       boundaryResolvers.Count > 0 ||
                       beforeMoveRule != null ||
                        layoutResolver != null ||
                        entryResolver != null ||
                        !string.IsNullOrEmpty(entryAnchorId) ||
                        EntryPolicy != AppUIFocusEntryPolicy.LastFocusedOrFirst;
            }
        }

        public void ConfigureLayout(
            AppUIFocusGroupLayout layout,
            AppUIFocusWrapPolicy wrapPolicy,
            int gridColumnCount,
            AppUIFocusGridShortRowPolicy gridShortRowPolicy)
        {
            Layout = layout;
            WrapPolicy = wrapPolicy;
            GridColumnCount = gridColumnCount;
            GridShortRowPolicy = gridShortRowPolicy;
        }

        public void SetAction(MoveDirection moveDirection, AppUIFocusAction action)
        {
            if (moveDirection == MoveDirection.None || action == null)
            {
                return;
            }

            actions[moveDirection] = action;
        }

        public bool TryGetAction(MoveDirection moveDirection, out AppUIFocusAction action)
        {
            return actions.TryGetValue(moveDirection, out action);
        }

        public void SetBoundaryAction(
            MoveDirection moveDirection,
            AppUIFocusAction action)
        {
            if (moveDirection == MoveDirection.None || action == null)
            {
                return;
            }

            boundaryActions[moveDirection] = action;
        }

        public bool TryGetBoundaryAction(
            MoveDirection moveDirection,
            out AppUIFocusAction action)
        {
            return boundaryActions.TryGetValue(moveDirection, out action);
        }

        public void SetBoundaryResolver(
            MoveDirection moveDirection,
            IAppUIFocusBoundaryResolver resolver)
        {
            if (moveDirection == MoveDirection.None || resolver == null)
            {
                return;
            }

            boundaryResolvers[moveDirection] = resolver;
        }

        public bool TryGetBoundaryResolver(
            MoveDirection moveDirection,
            out IAppUIFocusBoundaryResolver resolver)
        {
            return boundaryResolvers.TryGetValue(moveDirection, out resolver);
        }

        public void SetBeforeMoveRule(IAppUIFocusMoveRule moveRule)
        {
            beforeMoveRule = moveRule;
        }

        public void SetLayoutResolver(IAppUIFocusLayoutResolver resolver)
        {
            layoutResolver = resolver;
        }

        public void SetEntryPolicy(AppUIFocusEntryPolicy policy)
        {
            EntryPolicy = policy;
        }

        public void SetEntryResolver(IAppUIFocusEntryResolver resolver)
        {
            entryResolver = resolver;
        }

        public void SetEntryAnchor(string anchorId)
        {
            entryAnchorId = anchorId ?? string.Empty;
            EntryPolicy = AppUIFocusEntryPolicy.AnchorOrFirst;
        }

        public AppUIFocusGroupRules Clone()
        {
            AppUIFocusGroupRules clone = new AppUIFocusGroupRules(GroupId);
            clone.ConfigureLayout(
                Layout,
                WrapPolicy,
                GridColumnCount,
                GridShortRowPolicy);
            clone.SetBeforeMoveRule(beforeMoveRule);
            clone.SetLayoutResolver(layoutResolver);
            clone.SetEntryPolicy(EntryPolicy);
            clone.SetEntryResolver(entryResolver);
            clone.entryAnchorId = entryAnchorId;
            foreach (KeyValuePair<MoveDirection, AppUIFocusAction> pair in actions)
            {
                clone.actions.Add(pair.Key, pair.Value);
            }

            foreach (KeyValuePair<MoveDirection, AppUIFocusAction> pair in boundaryActions)
            {
                clone.boundaryActions.Add(pair.Key, pair.Value);
            }

            foreach (KeyValuePair<MoveDirection, IAppUIFocusBoundaryResolver> pair in boundaryResolvers)
            {
                clone.boundaryResolvers.Add(pair.Key, pair.Value);
            }

            return clone;
        }

        internal void CollectReferencedGroups(ISet<string> groupIds)
        {
            if (groupIds == null)
            {
                return;
            }

            foreach (KeyValuePair<MoveDirection, AppUIFocusAction> pair in actions)
            {
                pair.Value?.CollectReferencedGroups(groupIds);
            }

            foreach (KeyValuePair<MoveDirection, AppUIFocusAction> pair in boundaryActions)
            {
                pair.Value?.CollectReferencedGroups(groupIds);
            }
        }

        internal void CollectReferencedRegions(ISet<string> regionIds)
        {
            if (regionIds == null)
            {
                return;
            }

            foreach (KeyValuePair<MoveDirection, AppUIFocusAction> pair in actions)
            {
                pair.Value?.CollectReferencedRegions(regionIds);
            }

            foreach (KeyValuePair<MoveDirection, AppUIFocusAction> pair in boundaryActions)
            {
                pair.Value?.CollectReferencedRegions(regionIds);
            }
        }
    }
}
