using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class WorkflowConventionTests
    {
        private static readonly string[] CanonicalWorkflowNames =
        [
            "security.yml", "dotnet.yml", "android.yml", "bash.yml", "github-release.yml",
            "nexus-mods-release.yml", "nuget-release.yml", "release-assets.yml",
            "steam-workshop-release.yml"
        ];

        [Test]
        public void GivenTheRepository_WhenInspectingWorkflows_ThenADotNetWorkflowExists()
        {
            Assert.That(File.Exists(DotNetWorkflowPath), Is.True, "A .github/workflows/dotnet.yml workflow must exist.");
        }

        [Test]
        public void GivenEveryWorkflowFile_WhenInspecting_ThenItsNameIsCanonical()
        {
            List<string> violations = [];

            foreach (string file in WorkflowFiles())
            {
                string name = Path.GetFileName(file);

                if (!CanonicalWorkflowNames.Contains(name))
                {
                    violations.Add($"{name}: workflow file names outside the canonical set are prohibited.");
                }
            }

            AssertNoViolations(violations, "canonical workflow file names");
        }

        [Test]
        public void GivenTheDotNetWorkflow_WhenInspecting_ThenItDeclaresTheExpectedNameTriggersAndRunner()
        {
            List<string> violations = [];
            string content = File.ReadAllText(DotNetWorkflowPath);

            if (!content.Contains("name: .NET", StringComparison.Ordinal))
            {
                violations.Add("dotnet.yml must declare 'name: .NET'.");
            }

            if (!content.Contains("push:", StringComparison.Ordinal) ||
                !content.Contains("pull_request:", StringComparison.Ordinal))
            {
                violations.Add("dotnet.yml must trigger on push and pull_request.");
            }

            if (!Regex.IsMatch(content, @"branches:\s*\[\s*master\s*\]"))
            {
                violations.Add("dotnet.yml must target the master branch.");
            }

            if (!content.Contains("runs-on: ubuntu-latest", StringComparison.Ordinal))
            {
                violations.Add("dotnet.yml must run on ubuntu-latest.");
            }

            AssertNoViolations(violations, "dotnet workflow header");
        }

        [Test]
        public void GivenTheDotNetWorkflow_WhenInspecting_ThenItDerivesTheVersionFromTheProjectFile()
        {
            List<string> violations = [];
            string content = File.ReadAllText(DotNetWorkflowPath);

            if (!content.Contains("Determine the .NET version", StringComparison.Ordinal))
            {
                violations.Add("dotnet.yml must derive the .NET version from the primary project file with a 'Determine the .NET version' step.");
            }

            if (!content.Contains("steps.dotnet.outputs.version", StringComparison.Ordinal))
            {
                violations.Add("dotnet.yml must pass the derived version to actions/setup-dotnet as ${{ steps.dotnet.outputs.version }}.x.");
            }

            if (Regex.IsMatch(content, @"dotnet-version:\s*\d"))
            {
                violations.Add("dotnet.yml must not pin a literal dotnet-version; it must derive the version from the project file.");
            }

            AssertNoViolations(violations, "dotnet version derivation");
        }

        [Test]
        public void GivenTheDotNetWorkflow_WhenInspecting_ThenItRestoresBuildsAndTests()
        {
            List<string> violations = [];
            string content = File.ReadAllText(DotNetWorkflowPath);

            if (!content.Contains("dotnet restore", StringComparison.Ordinal))
            {
                violations.Add("dotnet.yml must restore dependencies.");
            }

            if (!content.Contains("dotnet build", StringComparison.Ordinal))
            {
                violations.Add("dotnet.yml must build the solution.");
            }

            if (!content.Contains("dotnet test", StringComparison.Ordinal))
            {
                violations.Add("dotnet.yml must run the tests.");
            }

            AssertNoViolations(violations, "dotnet workflow steps");
        }

        [Test]
        public void GivenTheDotNetWorkflow_WhenInspecting_ThenNoTemplatePlaceholderRemains()
        {
            List<string> violations = [];
            string content = File.ReadAllText(DotNetWorkflowPath);

            if (content.Contains("[[DOTNET_PROJECT_PATH]]", StringComparison.Ordinal) ||
                Regex.IsMatch(content, @"\[\[[A-Z_]+\]\]"))
            {
                violations.Add("dotnet.yml must not retain a template placeholder.");
            }

            if (content.Contains("# Only include this step", StringComparison.Ordinal))
            {
                violations.Add("dotnet.yml must not retain a guidance comment in the delivered output.");
            }

            AssertNoViolations(violations, "workflow placeholder removal");
        }

        [Test]
        public void GivenEveryWorkflowFile_WhenInspecting_ThenNoSecretOrCredentialIsExposed()
        {
            List<string> violations = [];
            Regex suspiciousLiteral = new(@"(?i)(password|passwd|secret|token|api[_-]?key|client[_-]?secret)\s*[:=]\s*[""'][^""']{6,}[""']");

            foreach (string file in WorkflowFiles())
            {
                for (int index = 0; index < File.ReadAllLines(file).Length; index += 1)
                {
                    string line = File.ReadAllLines(file)[index];

                    if (suspiciousLiteral.IsMatch(line) && !line.Contains("secrets.", StringComparison.Ordinal))
                    {
                        violations.Add($"{Path.GetFileName(file)}:{index + 1}: a secret or credential must be sourced from GitHub secrets, never embedded in a workflow.");
                    }
                }
            }

            AssertNoViolations(violations, "workflow secret hygiene");
        }

        [Test]
        public void GivenEveryWorkflowFile_WhenInspecting_ThenActionsUsePinnedMajorVersionsOrSha()
        {
            List<string> violations = [];
            Regex unpinnedAction = new(@"uses:\s*([\w\-\.]+/[\w\-\.]+)(?:\s|$)");

            foreach (string file in WorkflowFiles())
            {
                foreach (Match match in unpinnedAction.Matches(File.ReadAllText(file)))
                {
                    violations.Add($"{Path.GetFileName(file)}: action '{match.Groups[1].Value}' must be pinned to a version tag or commit SHA.");
                }
            }

            AssertNoViolations(violations, "pinned workflow actions");
        }

        private static string DotNetWorkflowPath
            => Path.Combine(SourceRepository.Root, ".github", "workflows", "dotnet.yml");

        private static IEnumerable<string> WorkflowFiles()
        {
            string directory = Path.Combine(SourceRepository.Root, ".github", "workflows");

            if (!Directory.Exists(directory))
            {
                return [];
            }

            return Directory
                .EnumerateFiles(directory)
                .Where(file => file.EndsWith(".yml", StringComparison.Ordinal) ||
                    file.EndsWith(".yaml", StringComparison.Ordinal))
                .OrderBy(file => file, StringComparer.Ordinal);
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
