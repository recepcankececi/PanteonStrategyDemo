using System.Runtime.CompilerServices;

// Lets EditMode tests exercise internal helpers without widening the public API.
[assembly: InternalsVisibleTo("StrategyDemo.Tests.EditMode")]
