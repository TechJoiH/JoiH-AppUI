using System.Collections.Generic;
using Joi.H.AppUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIFocusAuthoringTests
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
        public void BuildFocusDefinition_ProducesValidatedSemanticDefinition()
        {
            AppUIFocusAuthoring authoring = CreateAuthoring();
            Button mainButton = CreateButton(authoring.gameObject, "MainButton");
            Button detailsButton = CreateButton(authoring.gameObject, "DetailsButton");
            authoring.ScopeId = "authored-scope";
            authoring.DebugTrace = true;
            authoring.Groups.Add(
                new AppUIFocusAuthoringGroup
                {
                    GroupId = "main",
                    Layout = AppUIFocusGroupLayout.Vertical,
                    Boundaries =
                    {
                        new AppUIFocusAuthoringBoundary
                        {
                            Direction = MoveDirection.Right,
                            Target = AppUIFocusAuthoringBoundaryTarget.FocusGroupFirst,
                            TargetId = "details",
                        },
                    },
                });
            authoring.Groups.Add(
                new AppUIFocusAuthoringGroup
                {
                    GroupId = "details",
                    Layout = AppUIFocusGroupLayout.Horizontal,
                    Order = 10,
                });
            authoring.Nodes.Add(CreateNode("main", "main-0", mainButton));
            authoring.Nodes.Add(CreateNode("details", "details-0", detailsButton));
            authoring.DefaultFocusGroupId = "main";
            authoring.DefaultFocusNodeKey = "main-0";
            authoring.Anchors.Add(
                new AppUIFocusAuthoringAnchor
                {
                    AnchorId = "details-entry",
                    GroupId = "details",
                    NodeKey = "details-0",
                });

            AppUIFocusDefinition definition = authoring.BuildFocusDefinition();
            AppUIFocusValidationReport report =
                AppUIFocusDefinitionValidator.Validate(definition);

            Assert.That(report.Success, Is.True, JoinErrors(report));
            Assert.That(definition.ScopeId, Is.EqualTo("authored-scope"));
            Assert.That(definition.GroupCount, Is.EqualTo(2));
            Assert.That(definition.NodeCount, Is.EqualTo(2));
            Assert.That(definition.DebugTraceEnabled, Is.True);
            Assert.That(definition.AnchorTargetProvider, Is.SameAs(authoring));
        }

        [Test]
        public void DefaultAndAnchorProviders_ReturnNodeAddresses()
        {
            AppUIFocusAuthoring authoring = CreateAuthoring();
            Button button = CreateButton(authoring.gameObject, "EntryButton");
            authoring.Groups.Add(
                new AppUIFocusAuthoringGroup
                {
                    GroupId = "main",
                    Layout = AppUIFocusGroupLayout.Single,
                });
            authoring.Nodes.Add(CreateNode("main", "entry", button));
            authoring.DefaultFocusGroupId = "main";
            authoring.DefaultFocusNodeKey = "entry";
            authoring.Anchors.Add(
                new AppUIFocusAuthoringAnchor
                {
                    AnchorId = "resume",
                    GroupId = "main",
                    NodeKey = "entry",
                });

            Assert.That(
                authoring.TryGetDefaultFocus(
                    UIDefaultFocusReason.PageOpened,
                    out AppUIFocusTarget defaultTarget),
                Is.True);
            Assert.That(
                defaultTarget.NodeAddress,
                Is.EqualTo(
                    new AppUIFocusNodeAddress(
                        "main",
                        new AppUIFocusNodeKey("entry"))));
            Assert.That(
                authoring.TryGetFocusAnchor("resume", out AppUIFocusTarget anchorTarget),
                Is.True);
            Assert.That(anchorTarget.NodeAddress, Is.EqualTo(defaultTarget.NodeAddress));
            Assert.That(authoring.TryGetFocusAnchor("missing", out _), Is.False);
        }

        [Test]
        public void ValidateAuthoring_ReportsMissingDefaultAndInvalidAnchors()
        {
            AppUIFocusAuthoring authoring = CreateAuthoring();
            Button button = CreateButton(authoring.gameObject, "OnlyButton");
            authoring.Groups.Add(
                new AppUIFocusAuthoringGroup
                {
                    GroupId = "main",
                    Layout = AppUIFocusGroupLayout.Single,
                    EntryPolicy = AppUIFocusEntryPolicy.AnchorOrFirst,
                    EntryAnchorId = "missing-entry",
                });
            authoring.Nodes.Add(CreateNode("main", "only", button));
            authoring.DefaultFocusGroupId = "main";
            authoring.DefaultFocusNodeKey = "missing";
            authoring.Anchors.Add(
                new AppUIFocusAuthoringAnchor
                {
                    AnchorId = "duplicate",
                    GroupId = "main",
                    NodeKey = "only",
                });
            authoring.Anchors.Add(
                new AppUIFocusAuthoringAnchor
                {
                    AnchorId = "duplicate",
                    GroupId = "main",
                    NodeKey = "missing",
                });

            AppUIFocusValidationReport report = authoring.ValidateAuthoring();

            Assert.That(report.Success, Is.False);
            Assert.That(ContainsError(report, "Required default focus"), Is.True);
            Assert.That(ContainsError(report, "duplicate AnchorId"), Is.True);
            Assert.That(ContainsError(report, "does not reference a static Node"), Is.True);
            Assert.That(ContainsError(report, "entry references a missing Anchor"), Is.True);
        }

        private AppUIFocusAuthoring CreateAuthoring()
        {
            GameObject root = new GameObject("FocusAuthoring", typeof(RectTransform));
            createdObjects.Add(root);
            return root.AddComponent<AppUIFocusAuthoring>();
        }

        private Button CreateButton(GameObject parent, string name)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent.transform, false);
            Button button = child.AddComponent<Button>();
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

        private static bool ContainsError(
            AppUIFocusValidationReport report,
            string expected)
        {
            for (int i = 0; i < report.Errors.Count; i++)
            {
                if (report.Errors[i].Contains(expected))
                {
                    return true;
                }
            }

            return false;
        }

        private static string JoinErrors(AppUIFocusValidationReport report)
        {
            return string.Join("\n", report.Errors);
        }
    }
}
