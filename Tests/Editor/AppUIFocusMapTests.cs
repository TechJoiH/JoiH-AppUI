using System.Collections.Generic;
using Joi.H.AppUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIFocusMapTests
    {
        private readonly List<GameObject> createdObjects =
            new List<GameObject>(8);

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void Build_ListsGroupsNodesAndBidirectionalRoutes()
        {
            Button left = CreateButton("Left");
            Button right = CreateButton("Right");
            AppUIFocusChain chain = new AppUIFocusChainBuilder()
                .SingleGroup("left")
                    .AtBoundary(MoveDirection.Right).FocusGroupFirst("right")
                .SingleGroup("right")
                    .AtBoundary(MoveDirection.Left).FocusGroupFirst("left")
                .Build();
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder("map-scope")
                    .AddGroup("left", order: 0)
                    .AddGroup("right", order: 10)
                    .AddNode("left", new AppUIFocusNodeKey("left-0"), left)
                    .AddNode("right", new AppUIFocusNodeKey("right-0"), right)
                    .SetChain(chain)
                    .Build();

            AppUIFocusMap map = AppUIFocusMapBuilder.Build(
                definition,
                new AppUIFocusNodeAddress(
                    "left",
                    new AppUIFocusNodeKey("left-0")));

            Assert.That(map.EntryGroupId, Is.EqualTo("left"));
            Assert.That(map.Groups.Count, Is.EqualTo(2));
            Assert.That(map.Nodes.Count, Is.EqualTo(2));
            Assert.That(map.Edges.Count, Is.EqualTo(2));
            Assert.That(map.Warnings, Is.Empty);
            Assert.That(map.ToString(), Does.Contain("left --Right/Boundary--> right"));
        }

        [Test]
        public void Validator_AddsNonBlockingUnreachableAndOneWayWarnings()
        {
            Button first = CreateButton("First");
            Button second = CreateButton("Second");
            Button isolated = CreateButton("Isolated");
            AppUIFocusChain chain = new AppUIFocusChainBuilder()
                .SingleGroup("first")
                    .AtBoundary(MoveDirection.Right).FocusGroupFirst("second")
                .SingleGroup("second")
                .SingleGroup("isolated")
                .Build();
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder("warning-scope")
                    .AddGroup("first")
                    .AddGroup("second")
                    .AddGroup("isolated")
                    .AddNode("first", new AppUIFocusNodeKey("first-0"), first)
                    .AddNode("second", new AppUIFocusNodeKey("second-0"), second)
                    .AddNode("isolated", new AppUIFocusNodeKey("isolated-0"), isolated)
                    .SetChain(chain)
                    .Build();

            AppUIFocusValidationReport report =
                AppUIFocusDefinitionValidator.Validate(definition);

            Assert.That(report.Success, Is.True);
            Assert.That(
                ContainsWarning(report, "unreachable", "isolated"),
                Is.True);
            Assert.That(
                ContainsWarning(report, "one-way", "first -> second"),
                Is.True);
        }

        [Test]
        public void AuthoringMap_ResolvesStaticAnchorTarget()
        {
            GameObject root = new GameObject("AuthoringMap", typeof(RectTransform));
            createdObjects.Add(root);
            AppUIFocusAuthoring authoring = root.AddComponent<AppUIFocusAuthoring>();
            Button main = CreateButton("Main", root.transform);
            Button details = CreateButton("Details", root.transform);
            AppUIFocusAuthoringGroup mainGroup = new AppUIFocusAuthoringGroup
            {
                GroupId = "main",
                Layout = AppUIFocusGroupLayout.Single,
            };
            mainGroup.Boundaries.Add(
                new AppUIFocusAuthoringBoundary
                {
                    Direction = MoveDirection.Right,
                    Target = AppUIFocusAuthoringBoundaryTarget.FocusAnchor,
                    TargetId = "details-entry",
                });
            authoring.Groups.Add(mainGroup);
            authoring.Groups.Add(
                new AppUIFocusAuthoringGroup
                {
                    GroupId = "details",
                    Layout = AppUIFocusGroupLayout.Single,
                });
            authoring.Nodes.Add(CreateNode("main", "main-0", main));
            authoring.Nodes.Add(CreateNode("details", "details-0", details));
            authoring.Anchors.Add(
                new AppUIFocusAuthoringAnchor
                {
                    AnchorId = "details-entry",
                    GroupId = "details",
                    NodeKey = "details-0",
                });
            authoring.DefaultFocusGroupId = "main";
            authoring.DefaultFocusNodeKey = "main-0";

            AppUIFocusMap map = AppUIFocusMapBuilder.Build(
                authoring.BuildFocusDefinition(),
                new AppUIFocusNodeAddress(
                    "main",
                    new AppUIFocusNodeKey("main-0")),
                true);

            Assert.That(
                ContainsResolvedEdge(map, "FocusAnchor", "details"),
                Is.True);
        }

        private Button CreateButton(string name, Transform parent = null)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            if (parent == null)
            {
                createdObjects.Add(gameObject);
            }
            else
            {
                gameObject.transform.SetParent(parent, false);
            }

            Button button = gameObject.AddComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            return button;
        }

        private static AppUIFocusAuthoringNode CreateNode(
            string groupId,
            string nodeKey,
            Selectable selectable)
        {
            return new AppUIFocusAuthoringNode
            {
                GroupId = groupId,
                NodeKey = nodeKey,
                Selectable = selectable,
            };
        }

        private static bool ContainsWarning(
            AppUIFocusValidationReport report,
            string first,
            string second)
        {
            for (int i = 0; i < report.Warnings.Count; i++)
            {
                if (report.Warnings[i].Contains(first) &&
                    report.Warnings[i].Contains(second))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsResolvedEdge(
            AppUIFocusMap map,
            string actionName,
            string targetGroupId)
        {
            for (int i = 0; i < map.Edges.Count; i++)
            {
                AppUIFocusMapEdge edge = map.Edges[i];
                if (edge.ActionName == actionName &&
                    edge.ResolvedTargetGroupId == targetGroupId &&
                    !edge.DynamicTarget)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
