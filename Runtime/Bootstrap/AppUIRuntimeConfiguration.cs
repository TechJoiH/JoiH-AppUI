using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Immutable snapshot of optional host-owned AppUI strategies.
    /// Required runtime ports remain in <see cref="AppUIRuntimeDependencies"/>.
    /// </summary>
    public sealed class AppUIRuntimeConfiguration
    {
        private static readonly AppUIRuntimeConfiguration empty =
            new AppUIRuntimeConfiguration(null, null);

        private readonly ReadOnlyCollection<IUILoadStrategy> loadStrategies;
        private readonly ReadOnlyCollection<IUIPageInstanceStrategy>
            instanceStrategies;

        public AppUIRuntimeConfiguration(
            IEnumerable<IUILoadStrategy> loadStrategies,
            IEnumerable<IUIPageInstanceStrategy> instanceStrategies)
        {
            this.loadStrategies = new ReadOnlyCollection<IUILoadStrategy>(
                Copy(loadStrategies));
            this.instanceStrategies =
                new ReadOnlyCollection<IUIPageInstanceStrategy>(
                    Copy(instanceStrategies));
        }

        public static AppUIRuntimeConfiguration Empty
        {
            get { return empty; }
        }

        public IReadOnlyList<IUILoadStrategy> LoadStrategies
        {
            get { return loadStrategies; }
        }

        public IReadOnlyList<IUIPageInstanceStrategy> InstanceStrategies
        {
            get { return instanceStrategies; }
        }

        internal AppUIInitializationResult Validate(
            UIPageDefinitionRegistry registry)
        {
            HashSet<string> loadIds =
                new HashSet<string>(StringComparer.Ordinal);
            AppUIInitializationResult loadValidation = ValidateLoadStrategies(
                loadIds);
            if (!loadValidation.Success)
            {
                return loadValidation;
            }

            HashSet<string> instanceIds =
                new HashSet<string>(StringComparer.Ordinal);
            AppUIInitializationResult instanceValidation =
                ValidateInstanceStrategies(instanceIds);
            if (!instanceValidation.Success)
            {
                return instanceValidation;
            }

            if (registry == null)
            {
                return AppUIInitializationResult.Failure(
                    AppUIInitializationStatus.MissingRegistry);
            }

            for (int i = 0; i < registry.Pages.Count; i++)
            {
                UIPageDefinition definition = registry.Pages[i];
                if (definition == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(definition.LoadStrategyId) &&
                    !loadIds.Contains(definition.LoadStrategyId))
                {
                    return InvalidReference(
                        AppUIInitializationStatus
                            .UnknownDefinitionLoadStrategy,
                        definition,
                        definition.LoadStrategyId,
                        "load");
                }

                if (!string.IsNullOrEmpty(definition.InstanceStrategyId) &&
                    !instanceIds.Contains(definition.InstanceStrategyId))
                {
                    return InvalidReference(
                        AppUIInitializationStatus
                            .UnknownDefinitionInstanceStrategy,
                        definition,
                        definition.InstanceStrategyId,
                        "instance");
                }
            }

            return AppUIInitializationResult.Ok();
        }

        private AppUIInitializationResult ValidateLoadStrategies(
            HashSet<string> strategyIds)
        {
            for (int i = 0; i < loadStrategies.Count; i++)
            {
                IUILoadStrategy strategy = loadStrategies[i];
                if (strategy == null ||
                    string.IsNullOrWhiteSpace(strategy.StrategyId))
                {
                    return InvalidStrategy(
                        AppUIInitializationStatus.InvalidLoadStrategy,
                        "load",
                        i);
                }

                if (!strategyIds.Add(strategy.StrategyId))
                {
                    return DuplicateStrategy(
                        AppUIInitializationStatus.DuplicateLoadStrategyId,
                        "load",
                        strategy.StrategyId);
                }
            }

            return AppUIInitializationResult.Ok();
        }

        private AppUIInitializationResult ValidateInstanceStrategies(
            HashSet<string> strategyIds)
        {
            for (int i = 0; i < instanceStrategies.Count; i++)
            {
                IUIPageInstanceStrategy strategy = instanceStrategies[i];
                if (strategy == null ||
                    string.IsNullOrWhiteSpace(strategy.StrategyId))
                {
                    return InvalidStrategy(
                        AppUIInitializationStatus.InvalidInstanceStrategy,
                        "instance",
                        i);
                }

                if (!strategyIds.Add(strategy.StrategyId))
                {
                    return DuplicateStrategy(
                        AppUIInitializationStatus
                            .DuplicateInstanceStrategyId,
                        "instance",
                        strategy.StrategyId);
                }
            }

            return AppUIInitializationResult.Ok();
        }

        private static AppUIInitializationResult InvalidStrategy(
            AppUIInitializationStatus status,
            string category,
            int index)
        {
            return AppUIInitializationResult.Failure(
                status,
                new ArgumentException(
                    "AppUI " + category + " strategy at index " + index +
                    " is null or has an empty StrategyId."));
        }

        private static AppUIInitializationResult DuplicateStrategy(
            AppUIInitializationStatus status,
            string category,
            string strategyId)
        {
            return AppUIInitializationResult.Failure(
                status,
                new ArgumentException(
                    "Duplicate AppUI " + category + " StrategyId: " +
                    strategyId));
        }

        private static AppUIInitializationResult InvalidReference(
            AppUIInitializationStatus status,
            UIPageDefinition definition,
            string strategyId,
            string category)
        {
            string pageId = !string.IsNullOrEmpty(definition.PageId)
                ? definition.PageId
                : definition.name;
            return AppUIInitializationResult.Failure(
                status,
                new InvalidOperationException(
                    "UI page '" + pageId + "' references unknown " +
                    category + " StrategyId: " + strategyId));
        }

        private static List<T> Copy<T>(IEnumerable<T> source)
        {
            return source != null ? new List<T>(source) : new List<T>();
        }
    }
}
