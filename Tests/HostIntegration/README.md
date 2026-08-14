# Host Integration Contract Test Kit

This optional test assembly verifies project-owned AppUI adapters. Add
`com.joih.appui` to the consumer project's `testables` list, then reference
`Joi.H.AppUI.Tests.HostIntegration` from a test-only asmdef.

Derive the fixture matching each adapter or bridge:

- `AppUIOperationFactoryContractFixture`
- `AppUIAssetProviderContractFixture`
- `AppUIExecutionContextContractFixture`
- `AppUIHostLifecycleContractFixture`
- `AppUIInstanceStrategyContractFixture`

The assembly is constrained by `UNITY_INCLUDE_TESTS` and is not part of Player
builds.
