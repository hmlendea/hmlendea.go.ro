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
    public sealed class AnalyserEnforcementTests
    {
        [Test]
        public void GivenTheRepository_WhenInspecting_ThenAnEditorConfigDeclaresBuildAnalysers()
        {
            string editorConfigPath = Path.Combine(SourceRepository.Root, ".editorconfig");

            if (!File.Exists(editorConfigPath))
            {
                Assert.Ignore("No .editorconfig is present, so no analyser severity configuration can be verified.");
            }

            string content = File.ReadAllText(editorConfigPath);

            Assert.That(
                content.Contains("dotnet_analyzer_diagnostic", StringComparison.Ordinal),
                Is.True,
                "The .editorconfig must configure .NET analyser diagnostics to make the coding rules enforced.");
        }

        [Test]
        public void GivenEveryProductionProject_WhenInspecting_ThenItReferencesNoDisabledAnalyserSwitch()
        {
            List<string> violations = [];
            string[] disablingProperties =
            [
                "EnableNETAnalyzers", "RunAnalyzersDuringBuild", "EnforceCodeStyleInBuild"
            ];

            foreach (ProjectSources project in SourceRepository.ProductionProjects)
            {
                string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");
                XDocument document = XDocument.Load(projectFile);

                foreach (string property in disablingProperties)
                {
                    XElement? element = document.Descendants(property).FirstOrDefault();

                    if (element is null)
                    {
                        continue;
                    }

                    if (string.Equals(element.Value, "false", StringComparison.OrdinalIgnoreCase))
                    {
                        violations.Add($"{project.Name}: <{property}> is disabled.");
                    }
                }
            }

            Assert.That(
                violations,
                Is.Empty,
                $"Analysers must not be disabled:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoAnalyzerSuppressionOfCognitiveComplexityIsPresent()
        {
            List<string> violations = [];

            foreach (SourceFile file in SourceWorkspace.StyleCheckedFiles)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    string line = file.Lines[index];

                    if (line.Contains("SuppressMessage", StringComparison.Ordinal) &&
                        (line.Contains("CognitiveComplexity", StringComparison.Ordinal) ||
                         line.Contains("CA1069", StringComparison.Ordinal) ||
                         line.Contains("S3776", StringComparison.Ordinal)))
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: suppresses a coding-quality analyser rule without documented rationale.");
                    }
                }
            }

            AssertNoViolations(violations, "analyser suppression hygiene");
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
