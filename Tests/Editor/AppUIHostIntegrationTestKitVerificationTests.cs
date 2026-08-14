using Joi.H.AppUI.TestKit;
using UnityEngine;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIHostOperationFixtureVerificationTests :
        AppUIOperationFactoryContractFixture
    {
        protected override IUIOperationFactory CreateOperationFactory()
        {
            return new ManualUIOperationFactory();
        }
    }

    public sealed class AppUIHostInstanceFixtureVerificationTests :
        AppUIInstanceStrategyContractFixture
    {
        protected override AppUIInstanceStrategyContractContext
            CreateInstanceContext()
        {
            GameObject root = new GameObject(
                "InstanceContractRoot",
                typeof(RectTransform));
            GameObject prefab = new GameObject(
                "InstanceContractPrefab",
                typeof(RectTransform));
            UIPageDefinition definition =
                ScriptableObject.CreateInstance<UIPageDefinition>();
            return new AppUIInstanceStrategyContractContext(
                new DefaultUIPageInstanceStrategy(),
                definition,
                prefab,
                (RectTransform)root.transform,
                () =>
                {
                    Object.DestroyImmediate(root);
                    Object.DestroyImmediate(prefab);
                    Object.DestroyImmediate(definition);
                });
        }
    }
}
