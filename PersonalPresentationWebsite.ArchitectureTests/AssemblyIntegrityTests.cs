using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class AssemblyIntegrityTests
    {
        private static readonly Assembly Production = typeof(Program).Assembly;

        [Test]
        public void GivenTheProductionAssembly_WhenInspecting_ThenItExposesNoTestNamedType()
        {
            List<string> violations = [];

            foreach (Type type in ExportedTypes())
            {
                if (type.Name.EndsWith("Tests", StringComparison.Ordinal))
                {
                    violations.Add($"production type '{type.FullName}' carries a test-only name.");
                }
            }

            AssertNoViolations(violations, "no test types in production");
        }

        [Test]
        public void GivenTheProductionAssembly_WhenInspecting_ThenNoTypeNameRepeatsItsNamespace()
        {
            List<string> violations = [];

            foreach (Type type in ExportedTypes())
            {
                string? namespaceName = type.Namespace;

                if (namespaceName is null)
                {
                    continue;
                }

                string lastSegment = namespaceName.Split('.').Last();

                if (type.Name == lastSegment)
                {
                    violations.Add($"type '{type.FullName}' repeats its namespace segment '{lastSegment}'.");
                }
            }

            AssertNoViolations(violations, "no namespace repetition in type names");
        }

        [Test]
        public void GivenTheProductionAssembly_WhenInspecting_ThenEveryPublicTypeResidesInTheRootNamespace()
        {
            List<string> violations = [];

            foreach (Type type in ExportedTypes())
            {
                if (type.IsNested)
                {
                    continue;
                }

                if (type.Namespace is null ||
                    (!string.Equals(type.Namespace, "PersonalPresentationWebsite", StringComparison.Ordinal) &&
                     !type.Namespace.StartsWith("PersonalPresentationWebsite.", StringComparison.Ordinal)))
                {
                    violations.Add($"public type '{type.FullName}' resides outside the root namespace.");
                }
            }

            AssertNoViolations(violations, "root namespace containment");
        }

        [Test]
        public void GivenTheProductionAssembly_WhenInspecting_ThenNoTypeIsInternalOutsideTheIntendedVisibility()
        {
            List<string> violations = [];

            foreach (Type type in Production.GetTypes())
            {
                if (type.IsNested)
                {
                    continue;
                }

                if (type.Namespace is null ||
                    !type.Namespace.StartsWith("PersonalPresentationWebsite", StringComparison.Ordinal))
                {
                    continue;
                }

                if (type.IsPublic)
                {
                    continue;
                }

                if (type.Name.Contains("<>", StringComparison.Ordinal) ||
                    type.Name.Contains("EmbeddedAttribute", StringComparison.Ordinal) ||
                    type.Name.Contains("RefSafetyRules", StringComparison.Ordinal))
                {
                    continue;
                }

                violations.Add($"type '{type.FullName}' is not public; application types form the presentation or configuration contract.");
            }

            AssertNoViolations(violations, "public application types");
        }

        [Test]
        public void GivenTheProductionAssembly_WhenInspecting_ThenTheProgramEntryPointHasTheExpectedSignature()
        {
            MethodInfo? entryPoint = Production.EntryPoint;

            Assert.That(entryPoint, Is.Not.Null, "The production assembly must expose a Main entry point.");
            Assert.That(entryPoint!.Name, Is.EqualTo("Main"));
            Assert.That(entryPoint.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(entryPoint.GetParameters().Length, Is.EqualTo(1));
            Assert.That(entryPoint.GetParameters()[0].ParameterType, Is.EqualTo(typeof(string[])));
        }

        [Test]
        public void GivenTheProductionAssembly_WhenInspecting_ThenNoAssemblyLevelInternalsVisibleToIsDeclared()
        {
            string[] attributes = Production
                .GetCustomAttributes<System.Runtime.CompilerServices.InternalsVisibleToAttribute>()
                .Select(attribute => attribute.AssemblyName)
                .ToArray();

            Assert.That(attributes, Is.Empty, "production code must not grant internal visibility to other assemblies.");
        }

        [Test]
        public void GivenTheProductionAssembly_WhenInspecting_ThenItReferencesNoTestOrEditorAssembly()
        {
            List<string> violations = [];

            foreach (AssemblyName reference in Production.GetReferencedAssemblies())
            {
                string name = reference.Name ?? string.Empty;

                if (name.Contains("Tests", StringComparison.Ordinal) ||
                    name.Contains("NUnit", StringComparison.Ordinal) ||
                    name.Contains("Moq", StringComparison.Ordinal) ||
                    name.Contains("Bunit", StringComparison.Ordinal))
                {
                    violations.Add($"production assembly references '{name}'.");
                }
            }

            AssertNoViolations(violations, "production assembly references");
        }

        [Test]
        public void GivenEveryArchitectureTestFixture_WhenInspecting_ThenItIsSealed()
        {
            List<string> violations = [];

            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.Namespace is null ||
                    !type.Namespace.Equals("PersonalPresentationWebsite.ArchitectureTests", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!type.Name.EndsWith("Tests", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!type.IsSealed)
                {
                    violations.Add($"architecture test fixture '{type.Name}' must be sealed.");
                }
            }

            AssertNoViolations(violations, "sealed architecture test fixtures");
        }

        [Test]
        public void GivenTheArchitectureAssembly_WhenInspecting_ThenItRetainsItsRepositoryRootMetadata()
        {
            string? root = Assembly
                .GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => attribute.Key == "RepositoryRoot")
                ?.Value;

            Assert.That(root, Is.Not.Null, "The architecture test assembly must carry its repository root metadata.");
        }

        private static IEnumerable<Type> ExportedTypes()
            => Production.GetExportedTypes();

        private static void AssertNoViolations(IReadOnlyList<string> violations, string convention)
        {
            if (violations.Count == 0)
            {
                return;
            }

            Assert.Fail(
                $"Violated {convention} with {violations.Count} finding(s):{Environment.NewLine}" +
                string.Join(Environment.NewLine, violations.Take(50)));
        }
    }
}
