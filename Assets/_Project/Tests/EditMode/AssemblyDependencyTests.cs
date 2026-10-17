using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// Guards the module dependency rules (Core and Data at the bottom, App at the top): a wrong-direction reference
    /// must fail a test, not just a code review.
    /// </summary>
    /// <remarks>
    /// Reads the references declared in each .asmdef rather than the compiled assembly's list:
    /// Unity adds auto-referenced package assemblies (uGUI) to every compiled assembly, which a
    /// developer never declared and cannot remove.
    /// </remarks>
    public class AssemblyDependencyTests
    {
        private const string ModulePrefix = "StrategyDemo.";
        private const string GuidReferencePrefix = "GUID:";

        private static readonly Dictionary<string, string[]> AllowedModuleReferences = new Dictionary<string, string[]>
        {
            ["StrategyDemo.Data"] = new string[0],
            ["StrategyDemo.Core"] = new string[0],
            ["StrategyDemo.Gameplay"] = new[] { "StrategyDemo.Data", "StrategyDemo.Core" },
            ["StrategyDemo.UI"] = new[] { "StrategyDemo.Data", "StrategyDemo.Core", "StrategyDemo.Gameplay" },
            ["StrategyDemo.App"] = new[] { "StrategyDemo.Data", "StrategyDemo.Core", "StrategyDemo.Gameplay", "StrategyDemo.UI" },
            ["StrategyDemo.EditorTools"] = new[] { "StrategyDemo.Data", "StrategyDemo.Core", "StrategyDemo.Gameplay", "StrategyDemo.UI", "StrategyDemo.App" },
            ["StrategyDemo.Tests.EditMode"] = new[] { "StrategyDemo.Data", "StrategyDemo.Core", "StrategyDemo.Gameplay", "StrategyDemo.UI", "StrategyDemo.EditorTools" }
        };

        private static IEnumerable<string> ModuleNames => AllowedModuleReferences.Keys;

        [TestCaseSource(nameof(ModuleNames))]
        public void Module_Exists_AsCompiledAssembly(string moduleName)
        {
            bool compiled = CompilationPipeline.GetAssemblies(AssembliesType.Editor).Any(assembly => assembly.name == moduleName);

            Assert.That(compiled, Is.True, $"{moduleName}.asmdef is missing or has no scripts.");
        }

        [TestCaseSource(nameof(ModuleNames))]
        public void Module_DeclaresReferencesByName(string moduleName)
        {
            // GUID references would make the name-based rules below silently pass.
            Assert.That(DeclaredReferences(moduleName), Has.None.StartsWith(GuidReferencePrefix));
        }

        [TestCaseSource(nameof(ModuleNames))]
        public void Module_References_OnlyAllowedModules(string moduleName)
        {
            string[] moduleReferences = DeclaredReferences(moduleName).Where(name => name.StartsWith(ModulePrefix)).ToArray();

            Assert.That(moduleReferences, Is.SubsetOf(AllowedModuleReferences[moduleName]));
        }

        // Data and Core stay engine-only (no uGUI, Input System or other packages),
        // so they can be tested and reused without a scene.
        [TestCase("StrategyDemo.Data")]
        [TestCase("StrategyDemo.Core")]
        public void FoundationModule_References_NoOtherAssembly(string moduleName)
        {
            Assert.That(DeclaredReferences(moduleName), Is.Empty);
        }

        [Test]
        public void ProjectModules_AreAllListedInDependencyRules()
        {
            string[] unlisted = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .Select(assembly => assembly.name)
                .Where(name => name.StartsWith(ModulePrefix) && !AllowedModuleReferences.ContainsKey(name))
                .ToArray();

            Assert.That(unlisted, Is.Empty, "New modules need an entry here.");
        }

        private static string[] DeclaredReferences(string moduleName)
        {
            string asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(moduleName);
            Assert.That(asmdefPath, Is.Not.Null.And.Not.Empty, $"No .asmdef found for {moduleName}.");

            AssemblyDefinitionReferences definition = JsonUtility.FromJson<AssemblyDefinitionReferences>(File.ReadAllText(asmdefPath));
            return definition.references ?? Array.Empty<string>();
        }

        /// <summary>The only part of the .asmdef JSON these tests read; the field name must match the JSON key.</summary>
        [Serializable]
        private class AssemblyDefinitionReferences
        {
            public string[] references;
        }
    }
}
