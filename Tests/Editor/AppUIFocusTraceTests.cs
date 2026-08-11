using System.Collections.Generic;
using System.Reflection;
using Joi.H.AppUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIFocusTraceTests
    {
        private readonly List<GameObject> createdObjects =
            new List<GameObject>(8);
        private EventSystem testEventSystem;

        [SetUp]
        public void SetUp()
        {
            AppUIFocusTrace.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            AppUIFocusTrace.ResetForTests();
            if (testEventSystem != null)
            {
                testEventSystem.SetSelectedGameObject(null);
                InvokeEventSystemLifecycle(testEventSystem, "OnDisable");
                testEventSystem = null;
            }

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
        public void DefaultOff_DoesNotStoreEntriesOrSnapshots()
        {
            AppUIFocusTrace.RegisterScope(10, "Page", "scope", false);
            AppUIFocusTrace.Record(
                10,
                AppUIFocusTraceStage.Scope,
                default,
                default,
                "ignored");
            List<AppUIFocusTraceEntry> entries =
                new List<AppUIFocusTraceEntry>();

            AppUIFocusTrace.CopyEntries(entries);

            Assert.That(AppUIFocusTrace.CanTrace(10), Is.False);
            Assert.That(entries, Is.Empty);
            Assert.That(AppUIFocusTrace.TryGetSnapshot(10, out _), Is.False);
        }

        [Test]
        public void RingBuffer_KeepsNewestEntriesInSequenceOrder()
        {
            AppUIFocusTrace.RegisterScope(20, "Page", "scope", true);
            int total = AppUIFocusTrace.Capacity + 7;
            for (int i = 0; i < total; i++)
            {
                AppUIFocusTrace.Record(
                    20,
                    AppUIFocusTraceStage.Move,
                    default,
                    default,
                    "entry-" + i);
            }

            List<AppUIFocusTraceEntry> entries =
                new List<AppUIFocusTraceEntry>();
            AppUIFocusTrace.CopyEntries(entries);

            Assert.That(entries.Count, Is.EqualTo(AppUIFocusTrace.Capacity));
            Assert.That(entries[0].Sequence, Is.EqualTo(8));
            Assert.That(entries[entries.Count - 1].Sequence, Is.EqualTo(total));
            Assert.That(entries[0].Message, Is.EqualTo("entry-7"));
            Assert.That(
                entries[entries.Count - 1].Message,
                Is.EqualTo("entry-" + (total - 1)));
        }

        [Test]
        public void DebugScope_RecordsCommitPublishesSnapshotAndRemovesOverlayState()
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            createdObjects.Add(eventSystemObject);
            testEventSystem = eventSystemObject.AddComponent<EventSystem>();
            InvokeEventSystemLifecycle(testEventSystem, "OnEnable");
            GameObject root = new GameObject("TracePage", typeof(RectTransform));
            createdObjects.Add(root);
            GameObject buttonObject = new GameObject("Entry", typeof(RectTransform));
            buttonObject.transform.SetParent(root.transform, false);
            Button button = buttonObject.AddComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            UIPageInstanceRegistry registry = new UIPageInstanceRegistry();
            UIPageInstance page = new UIPageInstance
            {
                PageId = "TracePage",
                GameObject = root,
                RectTransform = root.transform as RectTransform,
                OperationVersion = 1,
                State = UIPageState.Open,
                StackVisible = true,
            };
            registry.Register(page);
            UIFocusService service = new UIFocusService();
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder("trace-scope")
                    .SetDebugTraceEnabled()
                    .SetChain(
                        new AppUIFocusChainBuilder()
                            .SingleGroup("main")
                            .Build())
                    .AddGroup("main")
                    .AddNode(
                        "main",
                        new AppUIFocusNodeKey("entry"),
                        button,
                        5)
                    .Build();
            IAppUIFocusScopeHandle scope = service.AttachScope(page, definition);
            UIPageInteractionHandle handle = page.ToInteractionHandle();
            service.ApplyInteractionSnapshot(
                new UIInteractionSnapshot(
                    1,
                    handle,
                    new[]
                    {
                        new UIPageInteractionState(handle, true, 0, 0),
                    }));

            AppUIFocusRequestResult result = scope.FocusNode(
                new AppUIFocusNodeAddress(
                    "main",
                    new AppUIFocusNodeKey("entry")));
            List<AppUIFocusTraceEntry> entries =
                new List<AppUIFocusTraceEntry>();
            AppUIFocusTrace.CopyEntries(entries);

            Assert.That(result, Is.EqualTo(AppUIFocusRequestResult.Focused));
            Assert.That(ContainsStage(entries, AppUIFocusTraceStage.Commit), Is.True);
            Assert.That(
                AppUIFocusTrace.TryGetSnapshot(
                    page.RuntimeInstanceId,
                    out AppUIFocusDebugSnapshot snapshot),
                Is.True);
            Assert.That(
                snapshot.Current,
                Is.EqualTo(
                    new AppUIFocusNodeAddress(
                        "main",
                        new AppUIFocusNodeKey("entry"))));
            Assert.That(snapshot.CurrentOrder, Is.EqualTo(5));
            Assert.That(snapshot.Candidates, Does.Contain("entry#5"));
            Assert.That(root.GetComponent<AppUIFocusDebugOverlay>(), Is.Not.Null);

            service.DetachScope(page);
            Assert.That(AppUIFocusTrace.CanTrace(page.RuntimeInstanceId), Is.False);
            Assert.That(
                AppUIFocusTrace.TryGetSnapshot(page.RuntimeInstanceId, out _),
                Is.False);
        }

        private static bool ContainsStage(
            List<AppUIFocusTraceEntry> entries,
            AppUIFocusTraceStage stage)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Stage == stage)
                {
                    return true;
                }
            }

            return false;
        }

        private static void InvokeEventSystemLifecycle(
            EventSystem eventSystem,
            string methodName)
        {
            MethodInfo method = typeof(EventSystem).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(eventSystem, null);
        }
    }
}
