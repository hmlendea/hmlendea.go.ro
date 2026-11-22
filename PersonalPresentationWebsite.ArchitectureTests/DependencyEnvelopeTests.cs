using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class DependencyEnvelopeTests
    {
        [Test]
        public void GivenTheProductionProject_WhenInspecting_ThenItDeclaresNoUnapprovedPackageReference()
        {
            string[] approved = ["NuciWeb.HTTP"];
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.ProductionProjects)
            {
                string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");
                XDocument document = XDocument.Load(projectFile);

                foreach (string reference in document
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value ?? string.Empty))
                {
                    if (!approved.Contains(reference, StringComparer.Ordinal))
                    {
                        violations.Add($"{project.Name}: package reference '{reference}' is not in the approved production dependency envelope.");
                    }
                }
            }

            AssertNoViolations(violations, "approved production dependencies");
        }

        [Test]
        public void GivenEveryTestProject_WhenInspecting_ThenItDeclaresOnlyApprovedTestPackages()
        {
            string[] approved =
            [
                "bunit", "coverlet.collector", "Microsoft.NET.Test.Sdk", "NUnit",
                "NUnit.Analyzers", "NUnit3TestAdapter", "Microsoft.AspNetCore.Mvc.Testing",
                "TngTech.ArchUnitNET", "TngTech.ArchUnitNET.NUnit", "Microsoft.CodeAnalysis.CSharp"
            ];

            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.TestProjects)
            {
                string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");
                XDocument document = XDocument.Load(projectFile);

                foreach (string reference in document
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value ?? string.Empty))
                {
                    if (!approved.Contains(reference, StringComparer.Ordinal))
                    {
                        violations.Add($"{project.Name}: test package '{reference}' is not in the approved test dependency envelope.");
                    }
                }
            }

            AssertNoViolations(violations, "approved test dependencies");
        }

        [Test]
        public void GivenEveryTestProject_WhenInspecting_ThenItUsesNFluentUnitAsItsFramework()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.TestProjects)
            {
                string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");
                XDocument document = XDocument.Load(projectFile);

                List<string> references = document
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                    .ToList();

                if (references.Any(reference => reference.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)))
                {
                    violations.Add($"{project.Name}: xUnit is not an approved framework; this solution uses NUnit.");
                }

                if (references.Any(reference => reference.StartsWith("MSTest", StringComparison.OrdinalIgnoreCase)))
                {
                    violations.Add($"{project.Name}: MSTest is not an approved framework; this solution uses NUnit.");
                }

                if (!references.Contains("NUnit", StringComparer.Ordinal))
                {
                    violations.Add($"{project.Name}: NUnit must be referenced as the testing framework.");
                }
            }

            AssertNoViolations(violations, "NUnit framework selection");
        }

        [Test]
        public void GivenEveryTestProject_WhenInspecting_ThenItReferencesATestAdapter()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.TestProjects)
            {
                string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");
                string content = File.ReadAllText(projectFile);

                if (!content.Contains("TestAdapter", StringComparison.Ordinal))
                {
                    violations.Add($"{project.Name}: must reference a test adapter so the runner can discover the tests.");
                }
            }

            AssertNoViolations(violations, "test adapter reference");
        }

        [Test]
        public void GivenEveryTestProject_WhenInspecting_ThenItReferencesTheTestSdk()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.TestProjects)
            {
                string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");
                string content = File.ReadAllText(projectFile);

                if (!content.Contains("Microsoft.NET.Test.Sdk", StringComparison.Ordinal))
                {
                    violations.Add($"{project.Name}: must reference Microsoft.NET.Test.Sdk.");
                }
            }

            AssertNoViolations(violations, "test SDK reference");
        }

        [Test]
        public void GivenEveryProductionProject_WhenInspecting_ThenItDeclaresNoTestFrameworkReference()
        {
            string[] testFrameworks = ["NUnit", "Moq", "Bunit", "xunit", "MSTest", "Microsoft.NET.Test.Sdk", "coverlet"];
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.ProductionProjects)
            {
                string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");
                XDocument document = XDocument.Load(projectFile);

                foreach (string reference in document
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value ?? string.Empty))
                {
                    if (testFrameworks.Any(framework => reference.StartsWith(framework, StringComparison.OrdinalIgnoreCase)))
                    {
                        violations.Add($"{project.Name}: production project references the test framework '{reference}'.");
                    }
                }
            }

            AssertNoViolations(violations, "no test framework in production");
        }

        [Test]
        public void GivenTheDependencyGraph_WhenInspecting_ThenNoProjectIsCyclic()
        {
            Dictionary<string, List<string>> references = [];

            foreach (ProjectSources project in SourceRepository.Projects)
            {
                string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");
                XDocument document = XDocument.Load(projectFile);

                references[project.Name] = document
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                    .Select(reference => Path.GetFileNameWithoutExtension(reference))
                    .ToList();
            }

            List<string> violations = [];

            foreach (string project in references.Keys)
            {
                HashSet<string> visited = [];
                Queue<string> queue = new(references[project]);

                while (queue.Count > 0)
                {
                    string current = queue.Dequeue();

                    if (current == project)
                    {
                        violations.Add($"Project reference cycle detected involving '{project}'.");
                        break;
                    }

                    if (!visited.Add(current) || !references.TryGetValue(current, out List<string>? next))
                    {
                        continue;
                    }

                    foreach (string neighbour in next)
                    {
                        queue.Enqueue(neighbour);
                    }
                }
            }

            AssertNoViolations(violations, "acyclic project references");
        }

        [Test]
        public void GivenEveryProjectReference_WhenInspecting_ThenTheTargetProjectExists()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.Projects)
            {
                string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");
                XDocument document = XDocument.Load(projectFile);

                foreach (string reference in document
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value ?? string.Empty))
                {
                    string resolved = Path.GetFullPath(Path.Combine(project.Directory, reference));

                    if (!File.Exists(resolved))
                    {
                        violations.Add($"{project.Name}: project reference '{reference}' points to a nonexistent project.");
                    }
                }
            }

            AssertNoViolations(violations, "existent project references");
        }

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
