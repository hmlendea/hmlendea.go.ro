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
    public sealed class ProjectAndSolutionConventionTests
    {
        [Test]
        public void GivenTheRepository_WhenInspecting_ThenExactlyOneSolutionFileExistsAndItIsSlnx()
        {
            string[] solutions = Directory
                .EnumerateFiles(SourceRepository.Root, "*.sln", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(SourceRepository.Root, "*.slnx", SearchOption.TopDirectoryOnly))
                .ToArray();

            Assert.That(solutions, Has.Length.EqualTo(1), "Exactly one solution file must exist at the repository root.");
            Assert.That(solutions[0], Does.EndWith(".slnx"), "The solution file must use the .slnx format.");
        }

        [Test]
        public void GivenTheSolutionFile_WhenInspecting_ThenItStartsAndEndsWithSolutionWithoutAttributes()
        {
            string content = File.ReadAllText(SourceRepository.SolutionFilePath).Trim();

            Assert.That(content.StartsWith("<Solution>", StringComparison.Ordinal), Is.True);
            Assert.That(content.EndsWith("</Solution>", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void GivenTheSolutionFile_WhenInspecting_ThenItDeclaresNoXmlDeclarationOrVersionElement()
        {
            string content = File.ReadAllText(SourceRepository.SolutionFilePath);

            Assert.That(content, Does.Not.Contain("<?xml"), "The .slnx file must not contain an XML declaration.");
            Assert.That(content, Does.Not.Contain("<Version>"), "The .slnx file must not contain a <Version> element.");
        }

        [Test]
        public void GivenTheSolutionFile_WhenInspecting_ThenEveryProjectElementDeclaresOnlyPath()
        {
            XDocument document = XDocument.Load(SourceRepository.SolutionFilePath);

            foreach (XElement element in document.Descendants("Project"))
            {
                string[] attributes = element.Attributes().Select(attribute => attribute.Name.LocalName).ToArray();

                Assert.That(
                    attributes,
                    Is.EqualTo(new[] { "Path" }),
                    $"Project element '{element.Attribute("Path")?.Value}' must declare only a Path attribute.");
            }
        }

        [Test]
        public void GivenTheSolutionFile_WhenInspecting_ThenTheRootElementDeclaresNoAttribute()
        {
            XDocument document = XDocument.Load(SourceRepository.SolutionFilePath);

            Assert.That(document.Root, Is.Not.Null);
            Assert.That(document.Root!.Attributes().Any(), Is.False, "The <Solution> element must declare no attributes.");
        }

        [Test]
        public void GivenEveryProject_WhenInspecting_ThenItResidesInASiblingFolderNamedAfterIt()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.Projects)
            {
                string expected = Path.Combine(SourceRepository.Root, project.Name);

                if (!string.Equals(project.Directory, expected, StringComparison.Ordinal))
                {
                    violations.Add($"{project.Name}: expected directory '{expected}', found '{project.Directory}'.");
                }
            }

            AssertNoViolations(violations, "project folder placement");
        }

        [Test]
        public void GivenTheRepository_WhenInspecting_ThenNoNestingSourceFolderExists()
        {
            Assert.That(
                Directory.Exists(Path.Combine(SourceRepository.Root, "src")),
                Is.False,
                "A 'src' nesting folder must not exist; projects must be siblings of the solution file.");
        }

        [Test]
        public void GivenTheSolutionProjects_WhenInspecting_ThenTheOrderIsMainSourceTest()
        {
            XDocument document = XDocument.Load(SourceRepository.SolutionFilePath);

            List<string> names = document
                .Descendants("Project")
                .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Path")!.Value))
                .ToList();

            List<string> expectedOrder = names
                .Where(name => !name.EndsWith("Tests", StringComparison.Ordinal))
                .Concat(names.Where(name => name.EndsWith("Tests", StringComparison.Ordinal)).OrderBy(name => name, StringComparer.Ordinal))
                .ToList();

            Assert.That(names, Is.EqualTo(expectedOrder), "Projects must be ordered main first, then source projects, then test projects alphabetically.");
        }

        [Test]
        public void GivenEveryProjectFile_WhenInspecting_ThenItTargetsNet10OrNewer()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.Projects)
            {
                XDocument document = LoadProject(project);

                foreach (XElement element in document.Descendants("TargetFramework"))
                {
                    if (!element.Value.StartsWith("net10.0", StringComparison.Ordinal) &&
                        !element.Value.StartsWith("net11.0", StringComparison.Ordinal))
                    {
                        violations.Add($"{project.Name}: targets '{element.Value}' instead of net10.0 or newer.");
                    }
                }
            }

            AssertNoViolations(violations, "target framework");
        }

        [Test]
        public void GivenEveryProjectFile_WhenInspecting_ThenImplicitUsingsAreDisabled()
        {
            foreach (ProjectSources project in SourceRepository.Projects)
            {
                XDocument document = LoadProject(project);
                string? value = document.Descendants("ImplicitUsings").FirstOrDefault()?.Value;

                Assert.That(value, Is.EqualTo("disable"), $"{project.Name} must declare <ImplicitUsings>disable</ImplicitUsings>.");
            }
        }

        [Test]
        public void GivenEveryProjectFile_WhenInspecting_ThenNullableReferencesAreEnabled()
        {
            foreach (ProjectSources project in SourceRepository.Projects)
            {
                XDocument document = LoadProject(project);
                string? value = document.Descendants("Nullable").FirstOrDefault()?.Value;

                Assert.That(value, Is.EqualTo("enable"), $"{project.Name} must declare <Nullable>enable</Nullable>.");
            }
        }

        [Test]
        public void GivenEveryProjectFile_WhenInspecting_ThenTheLatestLanguageVersionIsSelected()
        {
            foreach (ProjectSources project in SourceRepository.Projects)
            {
                XDocument document = LoadProject(project);
                string? value = document.Descendants("LangVersion").FirstOrDefault()?.Value;

                Assert.That(value, Is.EqualTo("latest"), $"{project.Name} must declare <LangVersion>latest</LangVersion>.");
            }
        }

        [Test]
        public void GivenEveryProjectFile_WhenInspecting_ThenPackabilityMatchesPublishingIntent()
        {
            foreach (ProjectSources project in SourceRepository.Projects)
            {
                XDocument document = LoadProject(project);
                string? value = document.Descendants("IsPackable").FirstOrDefault()?.Value;

                Assert.That(value, Is.EqualTo("false"), $"{project.Name} must declare <IsPackable>false</IsPackable> because no project is published as a NuGet package.");
            }
        }

        [Test]
        public void GivenEveryProjectFile_WhenInspecting_ThenPackageReferencesAreAlphabeticallyOrdered()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.Projects)
            {
                XDocument document = LoadProject(project);

                List<string> includes = document
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                    .ToList();

                List<string> sorted = [.. includes.OrderBy(include => include, StringComparer.OrdinalIgnoreCase)];

                if (!includes.SequenceEqual(sorted, StringComparer.OrdinalIgnoreCase))
                {
                    violations.Add($"{project.Name}: package references are not alphabetically ordered.");
                }
            }

            AssertNoViolations(violations, "package reference ordering");
        }

        [Test]
        public void GivenEveryTestProject_WhenInspecting_ThenItIsMarkedAsATestProjectAndExcludedFromCoverage()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.TestProjects)
            {
                XDocument document = LoadProject(project);

                if (document.Descendants("IsTestProject").FirstOrDefault()?.Value != "true")
                {
                    violations.Add($"{project.Name}: must declare <IsTestProject>true</IsTestProject>.");
                }

                if (document.Descendants("ExcludeFromCodeCoverage").FirstOrDefault()?.Value != "true")
                {
                    violations.Add($"{project.Name}: must declare <ExcludeFromCodeCoverage>true</ExcludeFromCodeCoverage>.");
                }
            }

            AssertNoViolations(violations, "test project flags");
        }

        [Test]
        public void GivenEveryUnitTestProject_WhenInspecting_ThenItIsNamedAfterTheMainProject()
        {
            ProjectSources? unitTests = SourceRepository.TestProjects
                .FirstOrDefault(project => project.Name.EndsWith(".UnitTests", StringComparison.Ordinal));

            Assert.That(unitTests, Is.Not.Null, "A unit test project must exist.");
            Assert.That(unitTests!.Name, Does.StartWith("PersonalPresentationWebsite."));
        }

        [Test]
        public void GivenEveryProductionProject_WhenInspecting_ThenItDeclaresNoInternalsVisibleTo()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.ProductionProjects)
            {
                foreach (string file in Directory.EnumerateFiles(project.Directory, "*.cs", SearchOption.AllDirectories))
                {
                    if (file.Contains("/bin/", StringComparison.Ordinal) ||
                        file.Contains("/obj/", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (File.ReadAllText(file).Contains("InternalsVisibleTo", StringComparison.Ordinal))
                    {
                        violations.Add($"{Path.GetFileName(file)}: declares InternalsVisibleTo.");
                    }
                }

                XDocument document = LoadProject(project);

                if (document.Descendants("InternalsVisibleTo").Any())
                {
                    violations.Add($"{project.Name}: declares an InternalsVisibleTo item.");
                }
            }

            AssertNoViolations(violations, "absence of InternalsVisibleTo");
        }

        private static XDocument LoadProject(ProjectSources project)
        {
            string projectFile = Path.Combine(project.Directory, project.Name + ".csproj");

            return XDocument.Load(projectFile);
        }

        private static void AssertNoViolations(IReadOnlyList<string> violations, string convention)
        {
            Assert.That(
                violations,
                Is.Empty,
                $"Violated {convention}:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
        }
    }
}
