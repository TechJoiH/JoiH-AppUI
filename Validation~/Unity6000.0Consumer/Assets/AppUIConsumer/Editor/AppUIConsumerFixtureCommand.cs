using System;
using System.Collections.Generic;
using Joi.H.AppUI.Editor.Binding;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Joi.H.AppUI.Validation.Consumer.Editor
{
    public static class AppUIConsumerFixtureCommand
    {
        [Serializable]
        private sealed class StageReport
        {
            public string schemaVersion;
            public string stage;
            public string unityVersion;
            public int generatedBindingCount;
            public string[] generatedFiles;
        }

        public static void ImportBasicIntegration()
        {
            AppUIConsumerBatchCommand.Run(ImportBasicIntegrationCore);
        }

        private static void ImportBasicIntegrationCore()
        {
            string version = GetExpectedPackageVersion();
            IEnumerable<Sample> samples = Sample.FindByPackage(
                "com.joih.appui", version);
            foreach (Sample sample in samples)
            {
                if (!string.Equals(
                        sample.displayName,
                        "Basic Integration",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                bool imported = sample.Import(
                    Sample.ImportOptions.OverridePreviousImports |
                    Sample.ImportOptions.HideImportWindow);
                if (!imported && !sample.isImported)
                {
                    throw new InvalidOperationException(
                        "Basic Integration sample import failed.");
                }

                return;
            }

            throw new InvalidOperationException(
                "Basic Integration sample was not found in com.joih.appui@" +
                version + ".");
        }

        public static void CreateFixturesAndGenerateBindings()
        {
            AppUIConsumerBatchCommand.Run(
                CreateFixturesAndGenerateBindingsCore);
        }

        private static void CreateFixturesAndGenerateBindingsCore()
        {
            ResetGeneratedRoot();
            UIPageDefinitionRegistry registry =
                CreateFixturesAndRegistry();
            CreateSettings(registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            registry = AssetDatabase.LoadAssetAtPath<
                UIPageDefinitionRegistry>(
                AppUIConsumerFixturePaths.Registry);
            if (registry == null)
            {
                throw new InvalidOperationException(
                    "Generated page registry could not be reloaded.");
            }

            CreateValidationScene(registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateSavedSceneReferences();

            string[] prefabPaths =
            {
                AppUIConsumerFixturePaths.BasicPrefab,
                AppUIConsumerFixturePaths.PopupPrefab,
                AppUIConsumerFixturePaths.BindingPrefab,
                AppUIConsumerFixturePaths.FocusPrefab,
            };
            List<string> generated = new List<string>(prefabPaths.Length);
            for (int i = 0; i < prefabPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPaths[i]);
                UIBindingScopeBase scope =
                    prefab != null
                        ? prefab.GetComponent<UIBindingScopeBase>()
                        : null;
                UIBindingGenerationResult result =
                    UIBindingGenerator.Generate(scope);
                if (!result.Success)
                {
                    throw new InvalidOperationException(
                        "Binding generation failed for " + prefabPaths[i] +
                        ": " + string.Join(" | ", result.Errors));
                }

                generated.Add(result.GeneratedFilePath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AppUIConsumerFixturePaths.WriteJson(
                "fixture-generate.json",
                new StageReport
                {
                    schemaVersion = "appui-consumer-fixture.v1",
                    stage = "Generated",
                    unityVersion = Application.unityVersion,
                    generatedBindingCount = generated.Count,
                    generatedFiles = generated.ToArray(),
                });
        }

        private static UIPageDefinitionRegistry CreateFixturesAndRegistry()
        {
            GameObject basic = CreatePagePrefab<ConsumerBasicPageController>(
                AppUIConsumerFixturePaths.BasicPrefab,
                false,
                false);
            GameObject popup = CreatePagePrefab<ConsumerPopupController>(
                AppUIConsumerFixturePaths.PopupPrefab,
                true,
                false);
            GameObject binding = CreatePagePrefab<
                ConsumerBindingPageController>(
                AppUIConsumerFixturePaths.BindingPrefab,
                false,
                true);
            GameObject focus = CreateFocusPrefab();
            CreateNoticePrefab();

            UIPageDefinition[] pages =
            {
                CreateDefinition(
                    "BasicPageDefinition",
                    ConsumerRuntimeInstaller.BasicPageId,
                    ConsumerRuntimeInstaller.BasicAssetId,
                    UILayerId.OverlayLayer,
                    UICanvasDomain.Overlay,
                    false,
                    false,
                    false),
                CreateDefinition(
                    "PopupDefinition",
                    ConsumerRuntimeInstaller.PopupPageId,
                    ConsumerRuntimeInstaller.PopupAssetId,
                    UILayerId.ModalLayer,
                    UICanvasDomain.Modal,
                    true,
                    true,
                    true),
                CreateDefinition(
                    "BindingPageDefinition",
                    ConsumerRuntimeInstaller.BindingPageId,
                    ConsumerRuntimeInstaller.BindingAssetId,
                    UILayerId.OverlayLayer,
                    UICanvasDomain.Overlay,
                    false,
                    false,
                    false),
                CreateDefinition(
                    "FocusPageDefinition",
                    ConsumerRuntimeInstaller.FocusPageId,
                    ConsumerRuntimeInstaller.FocusAssetId,
                    UILayerId.OverlayLayer,
                    UICanvasDomain.Overlay,
                    false,
                    false,
                    false),
            };

            UIPageDefinitionRegistry registry =
                ScriptableObject.CreateInstance<
                    UIPageDefinitionRegistry>();
            SetObjectReferenceList(registry, "m_Pages", pages);
            AssetDatabase.CreateAsset(
                registry, AppUIConsumerFixturePaths.Registry);
            registry.RebuildIndex();
            return registry;
        }

        private static GameObject CreatePagePrefab<TController>(
            string path,
            bool blocksInput,
            bool addBindingNodes)
            where TController : PanelBaseController
        {
            GameObject root = CreateRectObject(
                typeof(TController).Name,
                typeof(TController));
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            if (blocksInput)
            {
                Image background = root.AddComponent<Image>();
                background.color = new Color(0f, 0f, 0f, 0.75f);
                root.AddComponent<AppUIInputPolicyRoot>().SetDefaultPolicy(
                    AppUIInputZoneMode.BlockAll);
            }

            if (addBindingNodes)
            {
                GameObject title = CreateRectObject(
                    "B_TitleText", typeof(Text));
                title.transform.SetParent(root.transform, false);
                title.GetComponent<Text>().text = "Binding";
                GameObject confirm = CreateRectObject(
                    "B_ConfirmButton", typeof(Image), typeof(Button));
                confirm.transform.SetParent(root.transform, false);
            }

            EnsureAssetFolderForPath(path);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateFocusPrefab()
        {
            GameObject root = CreateRectObject(
                "ConsumerFocusListController",
                typeof(ConsumerFocusListController));
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject scrollObject = CreateRectObject(
                "FocusScroll", typeof(Image), typeof(ScrollRect));
            scrollObject.transform.SetParent(root.transform, false);
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            GameObject viewport = CreateRectObject(
                "Viewport", typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            GameObject content = CreateRectObject("Content");
            content.transform.SetParent(viewport.transform, false);
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = (RectTransform)content.transform;
            scroll.horizontal = false;
            CreateButton("FirstButton", content.transform, 80f);
            CreateButton("SecondButton", content.transform, -80f);

            EnsureAssetFolderForPath(AppUIConsumerFixturePaths.FocusPrefab);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                root, AppUIConsumerFixturePaths.FocusPrefab);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static UIPageDefinition CreateDefinition(
            string assetName,
            string pageId,
            string prefabPath,
            UILayerId layer,
            UICanvasDomain canvasDomain,
            bool blockLower,
            bool closeOnCancel,
            bool closeOnBackground)
        {
            UIPageDefinition definition =
                ScriptableObject.CreateInstance<UIPageDefinition>();
            definition.LayerId = layer;
            definition.CanvasDomain = canvasDomain;
            definition.Scope = UIPageScope.SceneScope;
            definition.OpenPolicy = UIOpenPolicy.RefreshExisting;
            definition.BlockLowerLayerInput = blockLower;
            definition.CloseOnCancel = closeOnCancel;
            definition.CloseOnBackgroundClick = closeOnBackground;
            definition.RequiresRaycaster = true;
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("m_DefinitionId").stringValue = pageId;
            serialized.FindProperty("m_PrefabAssetId").stringValue = prefabPath;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            string path = AppUIConsumerFixturePaths.Definitions + "/" +
                assetName + ".asset";
            EnsureAssetFolderForPath(path);
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        private static void CreateSettings(
            UIPageDefinitionRegistry registry)
        {
            UILayerSettings layers =
                ScriptableObject.CreateInstance<UILayerSettings>();
            AssetDatabase.CreateAsset(
                layers, AppUIConsumerFixturePaths.LayerSettings);
            AppUIRuntimeProfile profile =
                ScriptableObject.CreateInstance<AppUIRuntimeProfile>();
            SerializedObject profileSerialized =
                new SerializedObject(profile);
            profileSerialized.FindProperty("pageRegistry")
                .objectReferenceValue = registry;
            profileSerialized.FindProperty("layerSettings")
                .objectReferenceValue = layers;
            SerializedProperty toast = profileSerialized
                .FindProperty("noticeSettings")
                .FindPropertyRelative("toast");
            toast.FindPropertyRelative("enabled").boolValue = true;
            toast.FindPropertyRelative("prefabAssetId").stringValue =
                ConsumerRuntimeInstaller.NoticeAssetId;
            profileSerialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(
                profile, AppUIConsumerFixturePaths.RuntimeProfile);

            UIBindingSettings binding =
                ScriptableObject.CreateInstance<UIBindingSettings>();
            binding.EnableBuildPreprocess = false;
            binding.SelectedAssetIdResolverId =
                ConsumerEditorAssetIdResolver.Id;
            binding.PageDefinitionRegistry = registry;
            AssetDatabase.CreateAsset(
                binding, AppUIConsumerFixturePaths.BindingSettings);
        }

        private static void CreateValidationScene(
            UIPageDefinitionRegistry registry)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            GameObject eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            eventSystem.transform.SetParent(null);

            GameObject rootObject = CreateRectObject(
                "AppUIRuntime",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(AppUIManager),
                typeof(GlobalUIRoot),
                typeof(AppUIRuntimeHost),
                typeof(ConsumerRuntimeInstaller));
            Canvas canvas = rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            AppUIManager manager = rootObject.GetComponent<AppUIManager>();
            GlobalUIRoot globalRoot = rootObject.GetComponent<GlobalUIRoot>();
            AppUIRuntimeHost host = rootObject.GetComponent<AppUIRuntimeHost>();
            AppUIRuntimeProfile profile =
                AssetDatabase.LoadAssetAtPath<AppUIRuntimeProfile>(
                    AppUIConsumerFixturePaths.RuntimeProfile);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    "Generated runtime profile could not be loaded.");
            }

            Array values = Enum.GetValues(typeof(UILayerId));
            UILayerRoot[] layerRoots = new UILayerRoot[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                UILayerId layerId = (UILayerId)values.GetValue(i);
                GameObject layerObject = CreateRectObject(
                    layerId.ToString(), typeof(UILayerRoot));
                layerObject.transform.SetParent(rootObject.transform, false);
                RectTransform layerRect =
                    (RectTransform)layerObject.transform;
                layerRect.anchorMin = Vector2.zero;
                layerRect.anchorMax = Vector2.one;
                layerRect.offsetMin = Vector2.zero;
                layerRect.offsetMax = Vector2.zero;
                UICanvasDomain domain = ResolveCanvasDomain(layerId);
                UILayerRoot layerRoot =
                    layerObject.GetComponent<UILayerRoot>();
                layerRoot.Configure(layerId, domain, layerRect);
                layerRoots[i] = layerRoot;
            }

            globalRoot.Configure(new[] { canvas }, layerRoots, manager);
            SetHostReferences(
                host,
                manager,
                globalRoot,
                profile,
                registry,
                layerRoots);
            ConsumerRuntimeInstaller installer =
                rootObject.GetComponent<ConsumerRuntimeInstaller>();
            installer.Configure(
                host,
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    AppUIConsumerFixturePaths.BasicPrefab),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    AppUIConsumerFixturePaths.PopupPrefab),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    AppUIConsumerFixturePaths.BindingPrefab),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    AppUIConsumerFixturePaths.FocusPrefab),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    AppUIConsumerFixturePaths.NoticePrefab));

            EnsureAssetFolderForPath(AppUIConsumerFixturePaths.Scene);
            if (!EditorSceneManager.SaveScene(
                    scene, AppUIConsumerFixturePaths.Scene))
            {
                throw new InvalidOperationException(
                    "Failed to save validation scene.");
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(
                    AppUIConsumerFixturePaths.Scene, true),
            };
        }

        private static void SetHostReferences(
            AppUIRuntimeHost host,
            AppUIManager manager,
            GlobalUIRoot globalRoot,
            AppUIRuntimeProfile profile,
            UIPageDefinitionRegistry registry,
            UILayerRoot[] roots)
        {
            SerializedObject serialized = new SerializedObject(host);
            serialized.FindProperty("uiManager").objectReferenceValue = manager;
            serialized.FindProperty("globalRoot").objectReferenceValue =
                globalRoot;
            serialized.FindProperty("profile").objectReferenceValue = profile;
            serialized.FindProperty("pageRegistry").objectReferenceValue =
                registry;
            SerializedProperty array = serialized.FindProperty("layerRoots");
            array.arraySize = roots.Length;
            for (int i = 0; i < roots.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = roots[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(host);
        }

        private static void ValidateSavedSceneReferences()
        {
            EditorSceneManager.OpenScene(
                AppUIConsumerFixturePaths.Scene,
                OpenSceneMode.Single);
            AppUIRuntimeHost host =
                Object.FindFirstObjectByType<AppUIRuntimeHost>();
            ConsumerRuntimeInstaller installer =
                Object.FindFirstObjectByType<ConsumerRuntimeInstaller>();
            if (host == null || installer == null)
            {
                throw new InvalidOperationException(
                    "Saved validation scene is missing its runtime components.");
            }

            SerializedObject hostSerialized = new SerializedObject(host);
            SerializedObject installerSerialized =
                new SerializedObject(installer);
            AppUIRuntimeProfile profile = hostSerialized
                .FindProperty("profile").objectReferenceValue as
                AppUIRuntimeProfile;
            if ((profile == null || profile.PageRegistry == null) &&
                    hostSerialized.FindProperty("pageRegistry")
                        .objectReferenceValue == null ||
                installerSerialized.FindProperty("basicPagePrefab")
                    .objectReferenceValue == null ||
                installerSerialized.FindProperty("popupPagePrefab")
                    .objectReferenceValue == null ||
                installerSerialized.FindProperty("bindingPagePrefab")
                    .objectReferenceValue == null ||
                installerSerialized.FindProperty("focusPagePrefab")
                    .objectReferenceValue == null)
            {
                throw new InvalidOperationException(
                    "Saved validation scene lost a required asset reference.");
            }
        }

        private static void SetObjectReferenceList<T>(
            Object target,
            string propertyName,
            T[] values)
            where T : Object
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty array = serialized.FindProperty(propertyName);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateButton(
            string name,
            Transform parent,
            float y)
        {
            GameObject button = CreateRectObject(
                name, typeof(Image), typeof(Button));
            button.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(240f, 72f);
            rect.anchoredPosition = new Vector2(0f, y);
            return button;
        }

        private static void CreateNoticePrefab()
        {
            GameObject root = CreateRectObject(
                "ConsumerNotice",
                typeof(CanvasGroup),
                typeof(Text),
                typeof(ConsumerNoticeView));
            Text label = root.GetComponent<Text>();
            label.text = "Notice";
            SerializedObject view = new SerializedObject(
                root.GetComponent<ConsumerNoticeView>());
            view.FindProperty("label").objectReferenceValue = label;
            view.ApplyModifiedPropertiesWithoutUndo();
            EnsureAssetFolderForPath(AppUIConsumerFixturePaths.NoticePrefab);
            PrefabUtility.SaveAsPrefabAsset(root, AppUIConsumerFixturePaths.NoticePrefab);
            Object.DestroyImmediate(root);
        }

        private static GameObject CreateRectObject(
            string name,
            params Type[] components)
        {
            List<Type> types = new List<Type>(components.Length + 1)
            {
                typeof(RectTransform),
            };
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != typeof(RectTransform))
                {
                    types.Add(components[i]);
                }
            }

            return new GameObject(name, types.ToArray());
        }

        private static UICanvasDomain ResolveCanvasDomain(UILayerId layerId)
        {
            switch (layerId)
            {
                case UILayerId.HudLayer:
                    return UICanvasDomain.Hud;
                case UILayerId.OverlayLayer:
                case UILayerId.PopupLayer:
                    return UICanvasDomain.Overlay;
                case UILayerId.ModalLayer:
                    return UICanvasDomain.Modal;
                case UILayerId.NoticeLayer:
                    return UICanvasDomain.Notice;
                case UILayerId.GuideLayer:
                    return UICanvasDomain.Guide;
                case UILayerId.LoadingLayer:
                    return UICanvasDomain.Loading;
                case UILayerId.DebugLayer:
                    return UICanvasDomain.Debug;
                default:
                    return UICanvasDomain.System;
            }
        }

        private static void ResetGeneratedRoot()
        {
            if (AssetDatabase.IsValidFolder(
                    AppUIConsumerFixturePaths.GeneratedRoot) &&
                !AssetDatabase.DeleteAsset(
                    AppUIConsumerFixturePaths.GeneratedRoot))
            {
                throw new InvalidOperationException(
                    "Failed to reset " +
                    AppUIConsumerFixturePaths.GeneratedRoot + ".");
            }

            AssetDatabase.CreateFolder("Assets", "AppUIConsumerGenerated");
            EnsureAssetFolderForPath(
                AppUIConsumerFixturePaths.Prefabs + "/placeholder.asset");
            EnsureAssetFolderForPath(
                AppUIConsumerFixturePaths.Definitions + "/placeholder.asset");
            EnsureAssetFolderForPath(
                AppUIConsumerFixturePaths.Settings + "/placeholder.asset");
            EnsureAssetFolderForPath(
                AppUIConsumerFixturePaths.Scenes + "/placeholder.asset");
        }

        private static void EnsureAssetFolderForPath(string assetPath)
        {
            string directory = System.IO.Path.GetDirectoryName(assetPath)
                .Replace('\\', '/');
            string[] segments = directory.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private static string GetExpectedPackageVersion()
        {
            string version = Environment.GetEnvironmentVariable(
                "APPUI_EXPECTED_PACKAGE_VERSION");
            if (string.IsNullOrWhiteSpace(version))
            {
                UnityEditor.PackageManager.PackageInfo package =
                    UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(AppUIRuntimeHost).Assembly);
                version = package != null ? package.version : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new InvalidOperationException(
                    "Cannot resolve the installed AppUI package version.");
            }

            return version;
        }
    }
}
