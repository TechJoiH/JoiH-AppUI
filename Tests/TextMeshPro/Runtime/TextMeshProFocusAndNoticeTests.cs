using System;
using System.Reflection;
using Joi.H.AppUI.Integrations.TextMeshPro;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI.Tests.TextMeshPro
{
    public sealed class TextMeshProFocusAndNoticeTests
    {
        private GameObject created;

        [TearDown]
        public void TearDown()
        {
            if (created != null) UnityEngine.Object.DestroyImmediate(created);
        }

        [Test]
        public void TextMeshProInputResolver_IdIsStable()
        {
            Assert.That(new TextMeshProInputFieldPolicyResolver().ResolverId,
                Is.EqualTo("joih.appui.tmp.input-field"));
        }

        [Test]
        public void TextMeshProInputResolver_MatchesOnlyTMPInputField()
        {
            TextMeshProInputFieldPolicyResolver resolver = new TextMeshProInputFieldPolicyResolver();
            TMP_InputField input = Create<TMP_InputField>();
            Assert.That(resolver.TryResolve(input, out IAppUIFocusControlPolicy policy), Is.True);
            Assert.That(policy, Is.Not.Null);

            UnityEngine.Object.DestroyImmediate(created);
            created = null;
            Button button = Create<Button>();
            Assert.That(resolver.TryResolve(button, out policy), Is.False);
            Assert.That(policy, Is.Null);
        }

        [Test]
        public void TextMeshProInputPolicy_FocusedCancelDeactivatesAndConsumes()
        {
            TMP_InputField input = Create<TMP_InputField>();
            SetPrivateField(input, "m_AllowInput", true);
            TextMeshProInputFieldPolicyResolver resolver = new TextMeshProInputFieldPolicyResolver();
            resolver.TryResolve(input, out IAppUIFocusControlPolicy policy);
            AppUIFocusCancelContext context = new AppUIFocusCancelContext(
                "main",
                new AppUIFocusNodeAddress("main", new AppUIFocusNodeKey("input")),
                input);

            Assert.That(policy.TryHandleCancel(context), Is.EqualTo(AppUIFocusCancelHandlingResult.Consumed));
            Assert.That(input.isFocused, Is.False);
        }

        [Test]
        public void TextMeshProInputPolicy_UnfocusedCancelContinues()
        {
            TMP_InputField input = Create<TMP_InputField>();
            TextMeshProInputFieldPolicyResolver resolver = new TextMeshProInputFieldPolicyResolver();
            resolver.TryResolve(input, out IAppUIFocusControlPolicy policy);
            AppUIFocusCancelContext context = new AppUIFocusCancelContext(
                "main",
                new AppUIFocusNodeAddress("main", new AppUIFocusNodeKey("input")),
                input);

            Assert.That(policy.TryHandleCancel(context), Is.EqualTo(AppUIFocusCancelHandlingResult.Continue));
        }

        [Test]
        public void TextMeshProDropdown_RequiresExplicitChildRegionId()
        {
            TMP_Dropdown dropdown = Create<TMP_Dropdown>();
            Assert.Throws<ArgumentException>(() =>
                new TextMeshProFocusDropdownControlPolicy(dropdown, string.Empty));
        }

        [Test]
        public void TextMeshProDropdown_ExpandCollapseSynchronizesRegion()
        {
            TMP_Dropdown dropdown = Create<TMP_Dropdown>();
            TextMeshProFocusDropdownControlPolicy policy =
                new TextMeshProFocusDropdownControlPolicy(dropdown, "options");
            RecordingScope scope = new RecordingScope();
            GameObject list = new GameObject("Dropdown List");
            SetPrivateField(dropdown, "m_Dropdown", list);

            policy.SynchronizeRegion(scope);
            Assert.That(scope.OpenCount, Is.EqualTo(1));

            SetPrivateField(dropdown, "m_Dropdown", null);
            policy.SynchronizeRegion(scope);
            Assert.That(scope.CloseCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(list);
        }

        [Test]
        public void TextMeshProDropdown_DisposeUnsubscribesEvents()
        {
            TMP_Dropdown dropdown = Create<TMP_Dropdown>();
            TextMeshProFocusDropdownControlPolicy policy =
                new TextMeshProFocusDropdownControlPolicy(dropdown, "options");
            RecordingScope scope = new RecordingScope();
            IDisposable binding = policy.Bind(scope);
            int beforeDispose = scope.CloseCount;

            binding.Dispose();
            dropdown.onValueChanged.Invoke(1);

            Assert.That(scope.CloseCount, Is.EqualTo(beforeDispose));
        }

        [Test]
        public void TextMeshProNoticeView_AppliesTextColorAndFontSize()
        {
            created = new GameObject(
                "Notice",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(TextMeshProUGUI),
                typeof(TextMeshProNoticeView));
            TMP_Text label = created.GetComponent<TMP_Text>();
            TextMeshProNoticeView view = created.GetComponent<TextMeshProNoticeView>();
            SetPrivateField(view, "label", label);
            Color color = new Color(0.2f, 0.3f, 0.4f, 1f);

            view.ApplyContent(new UINoticeContent("Hello", color, 32f));

            Assert.That(label.text, Is.EqualTo("Hello"));
            Assert.That(label.color, Is.EqualTo(color));
            Assert.That(label.fontSize, Is.EqualTo(32f));
        }

        private T Create<T>() where T : Component
        {
            created = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(T));
            return created.GetComponent<T>();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class RecordingScope : IAppUIFocusScopeHandle
        {
            public string ScopeId => "scope";
            public string ActiveRegionId => string.Empty;
            public AppUIFocusScopeStatus Status => AppUIFocusScopeStatus.Active;
            public AppUIFocusRegionStatus RootRegionStatus => AppUIFocusRegionStatus.Active;
            public int Revision => 0;
            public int OpenCount { get; private set; }
            public int CloseCount { get; private set; }

            public bool RegisterNode(string groupId, AppUIFocusNodeKey nodeKey, Selectable selectable, int order = 0) => true;
            public bool RegisterNode(string groupId, AppUIFocusNodeKey nodeKey, Selectable selectable, IAppUIFocusControlPolicy policy, int order = 0) => true;
            public bool UnregisterNode(string groupId, AppUIFocusNodeKey nodeKey) => true;
            public AppUIFocusGroupUpdateResult BeginGroupUpdate(string groupId, out AppUIFocusGroupUpdateTransaction transaction)
            {
                transaction = null;
                return AppUIFocusGroupUpdateResult.ScopeDisposed;
            }
            public bool ClearGroup(string groupId) => true;
            public bool OpenGroup(string groupId) => true;
            public bool CloseGroup(string groupId) => true;
            public bool IsGroupOpen(string groupId) => true;
            public AppUIFocusRegionStatus GetRegionStatus(string regionId) =>
                OpenCount > CloseCount ? AppUIFocusRegionStatus.Active : AppUIFocusRegionStatus.Closed;
            public AppUIFocusRequestResult OpenRegion(string regionId, AppUIFocusRegionEntryPolicy entryPolicy = AppUIFocusRegionEntryPolicy.LastFocusedOrDefault)
            {
                OpenCount++;
                return AppUIFocusRequestResult.Consumed;
            }
            public AppUIFocusRequestResult CloseRegion(string regionId)
            {
                CloseCount++;
                return AppUIFocusRequestResult.Consumed;
            }
            public AppUIFocusRequestResult FocusNode(AppUIFocusNodeAddress address, AppUIFocusChangeReason reason = AppUIFocusChangeReason.Programmatic) => AppUIFocusRequestResult.Focused;
            public AppUIFocusRequestResult FocusGroupFirst(string groupId, AppUIFocusChangeReason reason = AppUIFocusChangeReason.Programmatic) => AppUIFocusRequestResult.Focused;
            public bool TryResolveNode(AppUIFocusNodeAddress address, out Selectable selectable) { selectable = null; return false; }
            public bool TryGetNodeAddress(Selectable selectable, out AppUIFocusNodeAddress address) { address = default; return false; }
            public bool TryGetNodeAddress(GameObject selectedObject, out AppUIFocusNodeAddress address) { address = default; return false; }
        }
    }
}
