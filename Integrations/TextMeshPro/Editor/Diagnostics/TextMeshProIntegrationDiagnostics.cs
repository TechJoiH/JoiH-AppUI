using System;
using System.Collections.Generic;
using Joi.H.AppUI;
using Joi.H.AppUI.Editor.Binding;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Joi.H.AppUI.Integrations.TextMeshPro.Editor
{
    internal static class TextMeshProIntegrationDiagnostics
    {
        internal const string ProviderId = TextMeshProBindingRuleProvider.Id;
        internal const string ResolverId = TextMeshProInputFieldPolicyResolver.Id;

        internal static IReadOnlyList<TextMeshProIntegrationDiagnostic> Collect()
        {
            List<TextMeshProIntegrationDiagnostic> result =
                new List<TextMeshProIntegrationDiagnostic>(16);
            result.Add(Pass("APPUI_TMP_API", "TextMeshPro API is available to the optional assembly."));
            result.Add(Pass("APPUI_TMP_DEFINE", "JOIH_APPUI_TMP is active for the current build target."));
            AppendAssembly(result, "APPUI_TMP_RUNTIME_ASSEMBLY", "Joi.H.AppUI.Integrations.TextMeshPro.Runtime");
            AppendAssembly(result, "APPUI_TMP_EDITOR_ASSEMBLY", "Joi.H.AppUI.Integrations.TextMeshPro.Editor");

            string[] providerIds = UIBindingRuleProviderRegistry.GetRegisteredProviderIds();
            bool registered = Array.IndexOf(providerIds, ProviderId) >= 0;
            result.Add(EvaluateProviderRegistered(registered));
            if (UIBindingSettingsUtility.TryFindUniqueSettings(
                    out UIBindingSettings settings,
                    out _,
                    out string settingsError))
            {
                bool enabled = Contains(settings.EnabledRuleProviderIds, ProviderId);
                result.Add(EvaluateProviderEnabled(registered, enabled));
                bool valid = UIBindingRuleProviderRegistry.TryBuildSnapshot(
                    settings,
                    out _,
                    out string snapshotError);
                result.Add(EvaluateBindingRules(valid, snapshotError));
                AppendNoticeProfiles(result, settings);
            }
            else
            {
                result.Add(Failure(
                    "APPUI_TMP_PROVIDER_ENABLED",
                    settingsError,
                    "TMP binding rules cannot be resolved.",
                    "Create exactly one UIBindingSettings asset and explicitly select joih.appui.tmp."));
                result.Add(Failure(
                    "APPUI_TMP_BINDING_RULES",
                    "No unique UIBindingSettings snapshot is available.",
                    "Binding validation cannot prove TMP rule determinism.",
                    "Resolve the UIBindingSettings error first."));
                AppendNoticeProfiles(result, null);
            }

            AppendRuntimeHosts(result);
            return result;
        }

        internal static TextMeshProIntegrationDiagnostic EvaluateProviderRegistered(bool registered)
        {
            return registered
                ? Pass("APPUI_TMP_PROVIDER_REGISTERED", "joih.appui.tmp is registered.")
                : Failure(
                    "APPUI_TMP_PROVIDER_REGISTERED",
                    "joih.appui.tmp is not registered.",
                    "TMP components cannot be contributed to Binding snapshots.",
                    "Enable JOIH_APPUI_TMP and fix optional Editor assembly compilation errors.");
        }

        internal static TextMeshProIntegrationDiagnostic EvaluateProviderEnabled(bool registered, bool enabled)
        {
            return registered && enabled
                ? Pass("APPUI_TMP_PROVIDER_ENABLED", "joih.appui.tmp is explicitly selected.")
                : Failure(
                    "APPUI_TMP_PROVIDER_ENABLED",
                    enabled ? "The selected Provider is unavailable." : "joih.appui.tmp is not selected.",
                    "TMP components use no optional Binding rules.",
                    "Add joih.appui.tmp to UIBindingSettings.EnabledRuleProviderIds after registration succeeds.");
        }

        internal static TextMeshProIntegrationDiagnostic EvaluateBindingRules(bool valid, string error)
        {
            return valid
                ? Pass("APPUI_TMP_BINDING_RULES", "The frozen Binding rule snapshot is valid.")
                : Failure(
                    "APPUI_TMP_BINDING_RULES",
                    string.IsNullOrEmpty(error) ? "Binding rule snapshot is invalid." : error,
                    "Generate, Bind, and Validate are blocked before writes.",
                    "Resolve missing Providers or RuleId/component collisions.");
        }

        internal static TextMeshProIntegrationDiagnostic EvaluateNotice(
            bool enabled,
            bool prefabValid,
            string fact,
            UnityEngine.Object context = null)
        {
            if (!enabled)
            {
                return Pass("APPUI_TMP_NOTICE_PROFILE", fact + " is disabled; no TMP Notice prefab is required.", context);
            }

            return prefabValid
                ? Pass("APPUI_TMP_NOTICE_PROFILE", fact + " resolves to TextMeshProNoticeView.", context)
                : Failure(
                    "APPUI_TMP_NOTICE_PROFILE",
                    fact + " is enabled but does not resolve to an authored TextMeshProNoticeView prefab.",
                    "Notice initialization fails explicitly for this visual.",
                    "Assign a resolvable prefab containing CanvasGroup and TextMeshProNoticeView.",
                    context);
        }

        internal static TextMeshProIntegrationDiagnostic EvaluateHost(
            string hostName,
            bool initialized,
            IReadOnlyList<string> resolverIds,
            UnityEngine.Object context = null)
        {
            if (!initialized)
            {
                return new TextMeshProIntegrationDiagnostic(
                    "APPUI_TMP_RUNTIME_HOST",
                    TextMeshProIntegrationDiagnosticState.NotVerifiable,
                    hostName + " is not initialized.",
                    "The immutable runtime Configuration cannot be inspected yet.",
                    "Enter Play Mode and initialize the Host with its real composition.",
                    context);
            }

            bool found = Contains(resolverIds, ResolverId);
            return found
                ? Pass("APPUI_TMP_INPUT_RESOLVER", hostName + " contains " + ResolverId + ".", context)
                : new TextMeshProIntegrationDiagnostic(
                    "APPUI_TMP_INPUT_RESOLVER",
                    TextMeshProIntegrationDiagnosticState.Warning,
                    hostName + " does not contain " + ResolverId + ".",
                    "TMP InputField edit/Cancel semantics use framework-only behavior.",
                    "Inject TextMeshProInputFieldPolicyResolver into AppUIRuntimeConfiguration.",
                    context);
        }

        private static void AppendAssembly(
            List<TextMeshProIntegrationDiagnostic> result,
            string code,
            string assemblyName)
        {
            bool found = false;
            UnityEditor.Compilation.Assembly[] assemblies = CompilationPipeline.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (string.Equals(assemblies[i].name, assemblyName, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            result.Add(found
                ? Pass(code, assemblyName + " is compiled.")
                : Failure(code, assemblyName + " is absent.", "Optional integration code is unavailable.", "Fix Define and assembly compilation errors."));
        }

        private static void AppendNoticeProfiles(
            List<TextMeshProIntegrationDiagnostic> result,
            UIBindingSettings settings)
        {
            string[] guids = AssetDatabase.FindAssets("t:AppUIRuntimeProfile");
            if (guids.Length == 0)
            {
                result.Add(EvaluateNotice(false, true, "No Runtime Profile"));
                return;
            }

            IUIEditorAssetIdResolver resolver = null;
            string resolverError = string.Empty;
            if (settings != null)
            {
                UIEditorAssetIdResolverRegistry.TryGetSelected(settings, out resolver, out resolverError);
            }

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AppUIRuntimeProfile profile = AssetDatabase.LoadAssetAtPath<AppUIRuntimeProfile>(path);
                if (profile == null) continue;
                AppendNoticeVisual(result, profile, "Toast", profile.NoticeSettings.Toast, resolver, resolverError);
                AppendNoticeVisual(result, profile, "Tooltip", profile.NoticeSettings.Tooltip, resolver, resolverError);
                AppendNoticeVisual(result, profile, "FloatingText", profile.NoticeSettings.FloatingText, resolver, resolverError);
                AppendNoticeVisual(result, profile, "DamageNumber", profile.NoticeSettings.DamageNumber, resolver, resolverError);
            }
        }

        private static void AppendNoticeVisual(
            List<TextMeshProIntegrationDiagnostic> result,
            AppUIRuntimeProfile profile,
            string visualName,
            AppUINoticeVisualSettings visual,
            IUIEditorAssetIdResolver resolver,
            string resolverError)
        {
            string fact = profile.name + "/" + visualName;
            if (!visual.Enabled)
            {
                result.Add(EvaluateNotice(false, true, fact, profile));
                return;
            }

            bool valid = false;
            if (resolver != null &&
                resolver.TryResolveAssetPath(visual.PrefabAssetId, out string path, out _) &&
                AssetDatabase.LoadAssetAtPath<GameObject>(path) is GameObject prefab)
            {
                valid = prefab.GetComponent<TextMeshProNoticeView>() != null;
            }

            string resolvedFact = string.IsNullOrEmpty(resolverError) ? fact : fact + " (" + resolverError + ")";
            result.Add(EvaluateNotice(true, valid, resolvedFact, profile));
        }

        private static void AppendRuntimeHosts(List<TextMeshProIntegrationDiagnostic> result)
        {
            if (!EditorApplication.isPlaying)
            {
                result.Add(new TextMeshProIntegrationDiagnostic(
                    "APPUI_TMP_RUNTIME_HOST",
                    TextMeshProIntegrationDiagnosticState.NotVerifiable,
                    "Runtime Hosts are only verifiable in Play Mode.",
                    "No live immutable Configuration exists in Edit Mode.",
                    "Enter Play Mode and refresh this page."));
                return;
            }

            AppUIRuntimeHost[] hosts = UnityEngine.Object.FindObjectsByType<AppUIRuntimeHost>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (hosts.Length == 0)
            {
                result.Add(new TextMeshProIntegrationDiagnostic(
                    "APPUI_TMP_RUNTIME_HOST",
                    TextMeshProIntegrationDiagnosticState.NotVerifiable,
                    "No AppUIRuntimeHost exists in the active Play Mode scenes.",
                    "Runtime resolver composition cannot be checked.",
                    "Load the integration scene and initialize its Host."));
                return;
            }

            for (int i = 0; i < hosts.Length; i++)
            {
                AppUIRuntimeHost host = hosts[i];
                List<string> ids = new List<string>();
                if (host.Configuration != null)
                {
                    for (int j = 0; j < host.Configuration.FocusPolicyResolvers.Count; j++)
                    {
                        ids.Add(host.Configuration.FocusPolicyResolvers[j].ResolverId);
                    }
                }

                result.Add(EvaluateHost(host.name, host.IsInitialized, ids, host));
            }
        }

        private static bool Contains(IReadOnlyList<string> values, string expected)
        {
            if (values == null) return false;
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], expected, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        private static TextMeshProIntegrationDiagnostic Pass(string code, string fact, UnityEngine.Object context = null)
        {
            return new TextMeshProIntegrationDiagnostic(code, TextMeshProIntegrationDiagnosticState.Pass, fact, string.Empty, string.Empty, context);
        }

        private static TextMeshProIntegrationDiagnostic Failure(
            string code,
            string fact,
            string impact,
            string fix,
            UnityEngine.Object context = null)
        {
            return new TextMeshProIntegrationDiagnostic(code, TextMeshProIntegrationDiagnosticState.Failure, fact, impact, fix, context);
        }
    }
}
