using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class ArchitectureSuiteIntegrityTests
    {
        private static readonly string[] RequiredFixtures =
        [
            "DependencyTests",
            "SourceFileConventionTests",
            "CodeConventionTests",
            "ConfigurationConventionTests",
            "NamespaceAndUsingConventionTests",
            "ProjectAndSolutionConventionTests",
            "TestSourceConventionTests",
            "MemberOrganisationConventionTests",
            "SpecialisedConventionTests",
            "TypeDeclarationConventionTests",
            "SelectorCoverageTests",
            "AnalyserEnforcementTests",
            "DocumentationTests",
            "NamingConventionTests",
            "CommonHygieneConventionTests",
            "SecurityConventionTests",
            "CollectionAndLinqConventionTests",
            "EndpointConventionTests",
            "WorkflowConventionTests",
            "RepositoryDocumentationConventionTests",
            "RepositoryHygieneConventionTests",
            "AssemblyIntegrityTests",
            "CodeSmellConventionTests",
            "MethodContractConventionTests",
            "IndentationConventionTests",
            "StructuralDesignConventionTests",
            "ExceptionSafetyConventionTests",
            "DependencyInjectionConventionTests",
            "CommentAndTextConventionTests",
            "AttributeAndEnumerationConventionTests",
            "ComponentAndAssetConventionTests",
            "ArchitectureSuiteIntegrityTests",
            "RazorComponentConventionTests",
            "StaticAssetConventionTests",
            "DependencyEnvelopeTests",
            "TestThoroughnessConventionTests",
            "ApplicationSettingsConventionTests",
            "AsyncCorrectnessConventionTests",
            "InvariantConventionTests",
            "ReadabilityConventionTests"
        ];

        [Test]
        public void GivenTheArchitectureProject_WhenInspecting_ThenEveryRequiredFixtureIsPresent()
        {
            HashSet<string> present = Directory
                .EnumerateFiles(ProjectDirectory, "*.cs", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .ToHashSet(StringComparer.Ordinal);

            List<string> missing = [.. RequiredFixtures.Where(fixture => !present.Contains(fixture))];

            Assert.That(missing, Is.Empty, "Every mandated architecture fixture must exist.");
        }

        [Test]
        public void GivenTheArchitectureProject_WhenInspecting_ThenNoProductionProjectGrantsInternalsVisibility()
        {
            foreach (ProjectSources project in SourceRepository.ProductionProjects)
            {
                foreach (string file in Directory.EnumerateFiles(project.Directory, "*.cs", SearchOption.AllDirectories))
                {
                    if (file.Contains("/bin/", StringComparison.Ordinal) || file.Contains("/obj/", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Assert.That(
                        File.ReadAllText(file),
                        Does.Not.Contain("InternalsVisibleTo"),
                        $"'{Path.GetFileName(file)}' must not declare InternalsVisibleTo.");
                }
            }
        }

        [Test]
        public void GivenTheArchitectureProject_WhenInspecting_ThenItCarriesTestAndCoverageFlags()
        {
            string projectFile = Path.Combine(ProjectDirectory, "PersonalPresentationWebsite.ArchitectureTests.csproj");
            string content = File.ReadAllText(projectFile);

            Assert.That(content, Does.Contain("<IsTestProject>true</IsTestProject>"));
            Assert.That(content, Does.Contain("<ExcludeFromCodeCoverage>true</ExcludeFromCodeCoverage>"));
            Assert.That(content, Does.Contain("<Nullable>enable</Nullable>"));
            Assert.That(content, Does.Contain("<ImplicitUsings>disable</ImplicitUsings>"));
        }

        [Test]
        public void GivenTheArchitectureProject_WhenInspecting_ThenItReferencesArchUnitAndRoslyn()
        {
            string projectFile = Path.Combine(ProjectDirectory, "PersonalPresentationWebsite.ArchitectureTests.csproj");
            string content = File.ReadAllText(projectFile);

            Assert.That(content, Does.Contain("TngTech.ArchUnitNET"), "Dependency rules require ArchUnit.NET.");
            Assert.That(content, Does.Contain("Microsoft.CodeAnalysis.CSharp"), "Source conventions require Roslyn.");
        }

        [Test]
        public void GivenEveryArchitectureTestFile_WhenInspecting_ThenItsTopLevelTypeMatchesTheFileName()
        {
            List<string> violations = [];

            foreach (SourceFile file in SourceWorkspace.Files.Where(candidate => candidate.Project.Name == "PersonalPresentationWebsite.ArchitectureTests"))
            {
                if (file.TopLevelTypeDeclarations.Count != 1)
                {
                    continue;
                }

                if (!string.Equals(file.TopLevelTypeDeclarations[0].Identifier.ValueText, file.FileNameStem, StringComparison.Ordinal))
                {
                    violations.Add($"{file.RelativePath}: type and file name differ.");
                }
            }

            Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
        }

        [Test]
        public void GivenEveryRuleFixture_WhenInspecting_ThenItUsesNUnitAndDeclaresAtLeastOneTest()
        {
            List<string> violations = [];

            foreach (SourceFile file in SourceWorkspace.Files.Where(candidate => candidate.Project.Name == "PersonalPresentationWebsite.ArchitectureTests"))
            {
                foreach (Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax declaration in file.TypeDeclarations)
                {
                    if (!declaration.Identifier.ValueText.EndsWith("Tests", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    bool hasFixture = declaration.AttributeLists
                        .SelectMany(list => list.Attributes)
                        .Any(attribute => attribute.Name.ToString() == "TestFixture");

                    if (!hasFixture)
                    {
                        violations.Add($"{file.RelativePath}: fixture '{declaration.Identifier.ValueText}' omits [TestFixture].");
                    }

                    bool hasTest = declaration.Members
                        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
                        .Any(method => method.AttributeLists
                            .SelectMany(list => list.Attributes)
                            .Any(attribute => attribute.Name.ToString() is "Test" or "TestCase" or "TestCaseSource"));

                    if (!hasTest)
                    {
                        violations.Add($"{file.RelativePath}: fixture '{declaration.Identifier.ValueText}' declares no test.");
                    }
                }
            }

            Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
        }

        private static string ProjectDirectory
            => Path.Combine(SourceRepository.Root, "PersonalPresentationWebsite.ArchitectureTests");
    }
}
