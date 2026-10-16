using System.Runtime.CompilerServices;

// An asmdef with no scripts is not compiled; this file keeps the module building until its first
// class arrives, and opens internals to tests should a later ADR let tests reference this module.
[assembly: InternalsVisibleTo("StrategyDemo.Tests.EditMode")]
