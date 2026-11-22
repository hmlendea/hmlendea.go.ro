using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class SourceFileConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        [Test]
        public void GivenTheCheckedProjects_WhenEnumerating_ThenEveryConfiguredProjectContributesSources()
        {
            List<string> emptyProjects = [];

            foreach (ProjectSources project in SourceRepository.Projects)
            {
                if (SourceWorkspace.FilesIn(project).Count == 0)
                {
                    emptyProjects.Add(project.Name);
                }
            }

            Assert.That(
                emptyProjects,
                Is.Empty,
                "Every checked project must contribute at least one eligible source file.");
        }

        [Test]
        public void GivenTheCheckedProjects_WhenEnumerating_ThenNoGeneratedOrBuildOutputFileIsIncluded()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                if (file.RelativePath.Contains("/obj/", StringComparison.Ordinal) ||
                    file.RelativePath.Contains("/bin/", StringComparison.Ordinal) ||
                    file.RelativePath.Contains("/generated/", StringComparison.Ordinal))
                {
                    violations.Add(file.RelativePath);
                }
            }

            Assert.That(violations, Is.Empty, "Generated and build-output files must be excluded.");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenItDeclaresExactlyOneTopLevelType()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                int count = file.TopLevelTypeDeclarations.Count + TopLevelDelegates(file).Count;

                if (count != 1)
                {
                    violations.Add($"{file.RelativePath}: declares {count} top-level types instead of exactly one.");
                }
            }

            AssertNoViolations(violations, "one top-level type per source file");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoNestedTypeIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BaseTypeDeclarationSyntax declaration in file.TypeDeclarations)
                {
                    if (declaration.Parent is not BaseNamespaceDeclarationSyntax)
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"nested type '{declaration.Identifier.ValueText}' must be extracted to its own file."));
                    }
                }

                foreach (DelegateDeclarationSyntax declaration in file.DelegateDeclarations)
                {
                    if (declaration.Parent is not BaseNamespaceDeclarationSyntax)
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"nested delegate '{declaration.Identifier.ValueText}' must be extracted to its own file."));
                    }
                }
            }

            AssertNoViolations(violations, "top-level type declarations");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenItsFileNameMatchesItsSingleTypeIdentifier()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                List<string> identifiers = file.TopLevelTypeDeclarations
                    .Select(declaration => declaration.Identifier.ValueText)
                    .ToList();

                identifiers.AddRange(TopLevelDelegates(file).Select(declaration => declaration.Identifier.ValueText));

                if (identifiers.Count != 1)
                {
                    continue;
                }

                if (!string.Equals(identifiers[0], file.FileNameStem, StringComparison.Ordinal))
                {
                    violations.Add($"{file.RelativePath}: type '{identifiers[0]}' does not match the file name stem '{file.FileNameStem}'.");
                }
            }

            AssertNoViolations(violations, "file name and type identifier correspondence");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenItDeclaresNoTopLevelStatement()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (GlobalStatementSyntax statement in file.Root.DescendantNodes().OfType<GlobalStatementSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, statement, "top-level statements are prohibited."));
                }
            }

            AssertNoViolations(violations, "absence of top-level statements");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenItDeclaresABlockNamespaceAndNoFileScopedNamespace()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                if (!file.Root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().Any())
                {
                    violations.Add($"{file.RelativePath}: declares no namespace.");
                }

                foreach (FileScopedNamespaceDeclarationSyntax declaration in file.Root
                    .DescendantNodes()
                    .OfType<FileScopedNamespaceDeclarationSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, declaration, "file-scoped namespaces are prohibited."));
                }
            }

            AssertNoViolations(violations, "block namespace declarations");
        }

        private static IReadOnlyList<DelegateDeclarationSyntax> TopLevelDelegates(SourceFile file)
            => file.DelegateDeclarations
                .Where(declaration => declaration.Parent is BaseNamespaceDeclarationSyntax)
                .ToList();

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
