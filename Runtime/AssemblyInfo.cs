using System.Runtime.CompilerServices;

// Exposes internal types (NavGate, NavEntry) to the EditMode test assembly so the
// transition gate and stack bookkeeping can be exercised directly.
[assembly: InternalsVisibleTo("KidzDev.Unity.SceneNavigator.Tests.Editor")]
[assembly: InternalsVisibleTo("KidzDev.Unity.SceneNavigator.Tests.Runtime")]
