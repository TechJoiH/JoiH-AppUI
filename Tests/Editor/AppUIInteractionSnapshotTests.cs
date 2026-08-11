using System.Collections.Generic;
using Joi.H.AppUI;
using NUnit.Framework;
using UnityEngine;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIInteractionSnapshotTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>(4);

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
        public void Registry_PreservesIdForSameInstanceAndAllocatesNewIdAfterRelease()
        {
            UIPageInstanceRegistry registry = new UIPageInstanceRegistry();
            UIPageInstance first = CreatePage("Page", 10);

            registry.Register(first);
            long firstId = first.RuntimeInstanceId;
            registry.Register(first);

            Assert.That(firstId, Is.GreaterThan(0));
            Assert.That(first.RuntimeInstanceId, Is.EqualTo(firstId));

            Assert.That(registry.Remove(first.PageId), Is.True);
            Assert.That(first.RuntimeInstanceId, Is.EqualTo(0));

            UIPageInstance replacement = CreatePage("Page", 11);
            registry.Register(replacement);

            Assert.That(replacement.RuntimeInstanceId, Is.GreaterThan(firstId));
        }

        [Test]
        public void Registry_RejectsHandleAfterOperationVersionChanges()
        {
            UIPageInstanceRegistry registry = new UIPageInstanceRegistry();
            UIPageInstance page = CreatePage("Page", 21);
            registry.Register(page);
            Assert.That(
                registry.TryCreateInteractionHandle(page, out UIPageInteractionHandle handle),
                Is.True);

            Assert.That(registry.TryResolve(handle, out UIPageInstance resolved), Is.True);
            Assert.That(resolved, Is.SameAs(page));

            page.OperationVersion++;

            Assert.That(registry.TryResolve(handle, out resolved), Is.False);
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void Snapshot_CopiesPageStatesFromMutableSource()
        {
            UIPageInteractionHandle firstHandle =
                new UIPageInteractionHandle("First", 1, 4);
            UIPageInteractionHandle replacementHandle =
                new UIPageInteractionHandle("Replacement", 2, 7);
            List<UIPageInteractionState> states = new List<UIPageInteractionState>
            {
                new UIPageInteractionState(firstHandle, true, 0, 0),
            };

            UIInteractionSnapshot snapshot =
                new UIInteractionSnapshot(5, firstHandle, states);
            states[0] = new UIPageInteractionState(replacementHandle, false, 2, 3);
            states.Clear();

            Assert.That(snapshot.StackRevision, Is.EqualTo(5));
            Assert.That(snapshot.PageStateCount, Is.EqualTo(1));
            Assert.That(snapshot.TopInteractivePage, Is.EqualTo(firstHandle));

            UIPageInteractionState captured = snapshot.GetPageState(0);
            Assert.That(captured.Page, Is.EqualTo(firstHandle));
            Assert.That(captured.StackVisible, Is.True);
            Assert.That(captured.PauseDepth, Is.Zero);
            Assert.That(captured.InputBlockDepth, Is.Zero);
        }

        [Test]
        public void PresentationCommit_PublishesOneRevisionAndResolvableTopPage()
        {
            UIPageInstanceRegistry registry = new UIPageInstanceRegistry();
            UIStackCoordinator stacks = new UIStackCoordinator();
            UIPageInstance page = CreatePage("Page", 31);
            registry.Register(page);
            stacks.Push(page);

            UIPresentationCoordinator presentation = new UIPresentationCoordinator(
                null,
                registry,
                new UILayerController(),
                stacks,
                new UIFocusService(),
                new UIInputBlocker(),
                new UISelectionInputAuthority());

            presentation.Commit();
            UIInteractionSnapshot first = presentation.CurrentInteractionSnapshot;

            Assert.That(first.StackRevision, Is.EqualTo(1));
            Assert.That(first.PageStateCount, Is.EqualTo(1));
            Assert.That(first.TopInteractivePage.IsValid, Is.True);
            Assert.That(
                registry.TryResolve(first.TopInteractivePage, out UIPageInstance resolved),
                Is.True);
            Assert.That(resolved, Is.SameAs(page));
            Assert.That(presentation.TryGetTopInteractivePage(out resolved), Is.True);
            Assert.That(resolved, Is.SameAs(page));

            presentation.Commit();
            UIInteractionSnapshot second = presentation.CurrentInteractionSnapshot;

            Assert.That(second.StackRevision, Is.EqualTo(2));
            Assert.That(second.TopInteractivePage, Is.EqualTo(first.TopInteractivePage));
        }

        private UIPageInstance CreatePage(string pageId, int operationVersion)
        {
            GameObject pageObject = new GameObject(pageId);
            createdObjects.Add(pageObject);
            return new UIPageInstance
            {
                PageId = pageId,
                LayerId = UILayerId.OverlayLayer,
                OperationVersion = operationVersion,
                GameObject = pageObject,
                State = UIPageState.Open,
                StackVisible = true,
            };
        }
    }
}
