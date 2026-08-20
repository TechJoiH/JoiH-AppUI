using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Joi.H.AppUI.Validation.Consumer.Tests
{
    public sealed class AppUIConsumerPlayModeTests
    {
        private const string SceneName = "AppUIConsumerValidation";
        private ConsumerRuntimeInstaller installer;

        [UnitySetUp]
        public IEnumerator LoadValidationScene()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName);
            Assert.That(load, Is.Not.Null,
                "Run CreateFixturesAndGenerateBindings first.");
            yield return load;
            yield return null;
            installer = Object.FindFirstObjectByType<
                ConsumerRuntimeInstaller>();
            Assert.That(installer, Is.Not.Null);
            Assert.That(installer.Host.IsInitialized, Is.True);
        }

        [UnityTest]
        public IEnumerator BasicPage_CompletesLifecycleAndShutdownReleasesLease()
        {
            ConsumerBasicPageController.ResetDiagnostics();
            int releasesBefore = installer.AssetProvider.ReleaseCount;

            UIOpenResult opened = Complete(
                installer.Manager.Open(
                    ConsumerRuntimeInstaller.BasicPageId,
                    UIOpenArgs.FromExplicit("initial")
                        .WithSceneScopeId("scene-basic")));
            Assert.That(opened.Success, Is.True);
            Assert.That(Complete(installer.Manager.Refresh(
                ConsumerRuntimeInstaller.BasicPageId,
                "updated")).Success, Is.True);
            Assert.That(Complete(installer.Manager.Close(
                ConsumerRuntimeInstaller.BasicPageId)).Success, Is.True);
            Assert.That(ConsumerBasicPageController.CreateCount,
                Is.EqualTo(1));
            Assert.That(ConsumerBasicPageController.InitCount,
                Is.EqualTo(1));
            Assert.That(ConsumerBasicPageController.LastData,
                Is.EqualTo("updated"));
            Assert.That(ConsumerBasicPageController.DisposeCount,
                Is.EqualTo(1));
            Assert.That(installer.AssetProvider.ReleaseCount,
                Is.EqualTo(releasesBefore + 1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Popup_CancelBackgroundAndInputBlock_AreDefinitionDriven()
        {
            ConsumerPopupController.ResetDiagnostics();
            UIOpenResult opened = Complete(installer.Manager.Open(
                ConsumerRuntimeInstaller.PopupPageId));
            Assert.That(opened.Success, Is.True);
            Assert.That(opened.Handle.LayerId,
                Is.EqualTo(UILayerId.ModalLayer));
            yield return null;
            Assert.That(AppUIInputHitResolver.Shared.IsPointerBlocked(
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                AppUIInputChannel.ViewportPan), Is.True);

            UICancelResult cancel = Complete(installer.Manager.Cancel());
            Assert.That(cancel.Outcome, Is.EqualTo(UICancelOutcome.Closed));
            Assert.That(installer.Manager.IsOpen(
                ConsumerRuntimeInstaller.PopupPageId), Is.False);
            Assert.That(ConsumerPopupController.CancelCount,
                Is.EqualTo(1));

            Complete(installer.Manager.Open(
                ConsumerRuntimeInstaller.PopupPageId));
            yield return null;
            UIBackgroundClickHandler[] shields =
                Resources.FindObjectsOfTypeAll<UIBackgroundClickHandler>();
            UIBackgroundClickHandler shield = null;
            for (int i = 0; i < shields.Length; i++)
            {
                if (shields[i] != null &&
                    shields[i].gameObject.activeInHierarchy)
                {
                    shield = shields[i];
                    break;
                }
            }

            Assert.That(shield, Is.Not.Null);
            shield.OnPointerClick(new PointerEventData(EventSystem.current));
            Assert.That(installer.Manager.IsOpen(
                ConsumerRuntimeInstaller.PopupPageId), Is.False);
            Assert.That(AppUIInputHitResolver.Shared.IsPointerBlocked(
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                AppUIInputChannel.ViewportPan), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneScopeRelease_ClosesAndReleasesPage()
        {
            int releasesBefore = installer.AssetProvider.ReleaseCount;
            Complete(installer.Manager.Open(
                ConsumerRuntimeInstaller.BasicPageId,
                UIOpenArgs.None.WithSceneScopeId("scene-release")));

            UIScopeReleaseResult released = Complete(
                installer.Manager.ReleaseScope(
                    UIPageScope.SceneScope,
                    "scene-release"));

            Assert.That(released.Success, Is.True);
            Assert.That(installer.Manager.IsOpen(
                ConsumerRuntimeInstaller.BasicPageId), Is.False);
            Assert.That(installer.AssetProvider.ReleaseCount,
                Is.EqualTo(releasesBefore + 1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PendingLoad_LateSuccessAfterScopeRelease_DoesNotReopen()
        {
            installer.AssetProvider.CompleteLoadsImmediately = false;
            installer.AssetProvider.HonorCancellationOnPendingCompletion = false;
            int releasesBefore = installer.AssetProvider.ReleaseCount;
            IUIOperation<UIOpenResult> opening = installer.Manager.Open(
                ConsumerRuntimeInstaller.BasicPageId,
                UIOpenArgs.None.WithSceneScopeId("scene-late"));
            Assert.That(opening.IsTerminal, Is.False);

            Complete(installer.Manager.ReleaseScope(
                UIPageScope.SceneScope,
                "scene-late"));
            Assert.That(installer.AssetProvider.CompleteNextPending(), Is.True);

            Assert.That(installer.Manager.IsOpen(
                ConsumerRuntimeInstaller.BasicPageId), Is.False);
            Assert.That(installer.AssetProvider.ReleaseCount,
                Is.EqualTo(releasesBefore + 1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FocusList_ChangesRealEventSystemSelection()
        {
            Complete(installer.Manager.Open(
                ConsumerRuntimeInstaller.FocusPageId));
            yield return null;
            ConsumerFocusListController controller =
                Object.FindFirstObjectByType<ConsumerFocusListController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(controller.FirstButton.gameObject));

            Assert.That(controller.MoveDown(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(controller.SecondButton.gameObject));
        }

        [UnityTest]
        public IEnumerator AuthoredUGUINotice_IsLoadedAndShown()
        {
            ToastHandle handle = installer.Manager.Notices.Toast("Consumer notice");
            Assert.That(handle.IsValid, Is.True);
            yield return null;
        }

        private static TResult Complete<TResult>(
            IUIOperation<TResult> operation)
        {
            Assert.That(operation, Is.Not.Null);
            Assert.That(operation.IsTerminal, Is.True);
            Assert.That(operation.TryGetCompletion(out var completion),
                Is.True);
            Assert.That(completion.Status,
                Is.EqualTo(AppUIOperationStatus.Succeeded),
                completion.Exception != null
                    ? completion.Exception.ToString()
                    : string.Empty);
            return completion.Result;
        }
    }
}
