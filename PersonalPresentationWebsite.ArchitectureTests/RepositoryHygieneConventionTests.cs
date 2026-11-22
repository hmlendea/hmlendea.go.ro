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
    public sealed class RepositoryHygieneConventionTests
    {
        [Test]
        public void GivenTheRepository_WhenInspecting_ThenAGitIgnoreExistsAndExcludesBuildOutput()
        {
            string gitIgnorePath = Path.Combine(SourceRepository.Root, ".gitignore");

            Assert.That(File.Exists(gitIgnorePath), Is.True, "A .gitignore must exist at the repository root.");

            string[] patterns = File.ReadAllLines(gitIgnorePath);

            Assert.That(patterns.Any(pattern => pattern.Contains("bin/", StringComparison.Ordinal)), Is.True, ".gitignore must exclude bin directories.");
            Assert.That(patterns.Any(pattern => pattern.Contains("obj/", StringComparison.Ordinal)), Is.True, ".gitignore must exclude obj directories.");
        }

        [Test]
        public void GivenTheRepository_WhenInspecting_ThenNoBuildOutputDirectoryIsTracked()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.Projects)
            {
                foreach (string directory in new[] { "bin", "obj" })
                {
                    string candidate = Path.Combine(project.Directory, directory);

                    if (Directory.Exists(candidate) && ContainsSourceFile(candidate))
                    {
                        violations.Add($"{project.Name}/{directory}: build output must not contain committed sources.");
                    }
                }
            }

            AssertNoViolations(violations, "no tracked build output");
        }

        [Test]
        public void GivenTheRepository_WhenInspecting_ThenExactlyOneLicenseExists()
        {
            string[] licenses = Directory
                .EnumerateFiles(SourceRepository.Root, "LICENSE*", SearchOption.TopDirectoryOnly)
                .ToArray();

            Assert.That(licenses, Has.Length.EqualTo(1), "Exactly one LICENSE file must exist at the repository root.");
        }

        [Test]
        public void GivenTheRepository_WhenInspecting_ThenTheWebProjectContainsNoGeneratedSources()
        {
            string generated = Path.Combine(SourceRepository.Root, "PersonalPresentationWebsite", "generated");

            if (!Directory.Exists(generated))
            {
                Assert.Pass("No generated directory is present.");
            }

            bool anyCsFile = Directory.EnumerateFiles(generated, "*.cs", SearchOption.AllDirectories).Any();

            Assert.That(anyCsFile, Is.False, "The generated directory must not contain committed hand-written C# sources.");
        }

        [Test]
        public void GivenEveryProjectFile_WhenInspecting_ThenItDeclaresNoAbsolutePath()
        {
            List<string> violations = [];
            System.Text.RegularExpressions.Regex absolutePath = new(@"<(?:Compile|Content|None|ProjectReference)\s+[^>]*Include\s*=\s*[""'][A-Za-z]:[\\/]|<(?:Compile|Content|None|ProjectReference)\s+[^>]*Include\s*=\s*[""']/");

            foreach (ProjectSources project in SourceRepository.Projects)
            {
                string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");
                string content = File.ReadAllText(projectFile);

                if (absolutePath.IsMatch(content))
                {
                    violations.Add($"{project.Name}: declares an absolute file path, which breaks portability.");
                }
            }

            AssertNoViolations(violations, "portable project paths");
        }

        [Test]
        public void GivenEveryProjectFile_WhenInspecting_ThenItDeclaresNoHardcodedEnvironmentSpecificPath()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.Projects)
            {
                string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");

                foreach (string line in File.ReadAllLines(projectFile))
                {
                    if (line.Contains("/home/", StringComparison.Ordinal) ||
                        line.Contains("C:\\", StringComparison.Ordinal) ||
                        line.Contains("Program Files", StringComparison.Ordinal))
                    {
                        violations.Add($"{project.Name}: declares an environment-specific path.");
                    }
                }
            }

            AssertNoViolations(violations, "environment-independent project files");
        }

        [Test]
        public void GivenEveryProjectFile_WhenInspecting_ThenItDeclaresNoDuplicateProjectReference()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.Projects)
            {
                string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");
                XDocument document = XDocument.Load(projectFile);

                List<string> references = document
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                    .ToList();

                if (references.Count != references.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                {
                    violations.Add($"{project.Name}: declares a duplicate project reference.");
                }
            }

            AssertNoViolations(violations, "no duplicate project references");
        }

        [Test]
        public void GivenEveryProjectFile_WhenInspecting_ThenItDeclaresNoDuplicatePackageReference()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.Projects)
            {
                string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");
                XDocument document = XDocument.Load(projectFile);

                List<string> references = document
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                    .ToList();

                if (references.Count != references.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                {
                    violations.Add($"{project.Name}: declares a duplicate package reference.");
                }
            }

            AssertNoViolations(violations, "no duplicate package references");
        }

        [Test]
        public void GivenEveryProjectFile_WhenInspecting_ThenNoProjectReferencesATestProject()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.ProductionProjects)
            {
                string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");
                XDocument document = XDocument.Load(projectFile);

                foreach (string reference in document
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value ?? string.Empty))
                {
                    if (reference.Contains("Tests", StringComparison.Ordinal))
                    {
                        violations.Add($"{project.Name}: production project references test project '{reference}'.");
                    }
                }
            }

            AssertNoViolations(violations, "production projects never reference test projects");
        }

        [Test]
        public void GivenEveryTestProject_WhenInspecting_ThenItReferencesTheProductionProject()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.TestProjects)
            {
                string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");
                XDocument document = XDocument.Load(projectFile);

                bool referencesProduction = document
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                    .Any(reference => reference.Contains("PersonalPresentationWebsite/", StringComparison.Ordinal) ||
                        reference.Contains("PersonalPresentationWebsite\\", StringComparison.Ordinal));

                if (!referencesProduction)
                {
                    violations.Add($"{project.Name}: test projects must reference the production project under test.");
                }
            }

            AssertNoViolations(violations, "test project production reference");
        }

        [Test]
        public void GivenTheRepository_WhenInspecting_ThenNoEditorSpecificFolderIsTracked()
        {
            List<string> violations = [];

            foreach (string directory in new[] { ".vs", ".idea", "node_modules" })
            {
                if (Directory.Exists(Path.Combine(SourceRepository.Root, directory)))
                {
                    violations.Add($"'{directory}': editor or dependency folder must not be tracked.");
                }
            }

            AssertNoViolations(violations, "no tracked editor folders");
        }

        private static readonly string[] GeneratedFileMarkers =
        [
            "AssemblyInfo", "AssemblyAttributes", "GlobalUsings", "EmbeddedAttribute",
            "ValidatableTypeAttribute", ".g.cs", ".g.i.cs", ".AssemblyInfo.cs"
        ];

        private static bool ContainsSourceFile(string directory)
            => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Any(file => !GeneratedFileMarkers.Any(marker => file.Contains(marker, StringComparison.Ordinal)));

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
