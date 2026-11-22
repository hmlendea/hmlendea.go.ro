using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis.CSharp.Syntax;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class NamespaceAndUsingConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenAllUsingDirectivesAreOutsideTheNamespace()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (UsingDirectiveSyntax directive in file.Root.DescendantNodes().OfType<UsingDirectiveSyntax>())
                {
                    if (directive.Parent is CompilationUnitSyntax)
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, directive, "using directives must be declared above and outside the namespace."));
                }
            }

            AssertNoViolations(violations, "using directive placement");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoGlobalUsingDirectiveIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (UsingDirectiveSyntax directive in file.Root.DescendantNodes().OfType<UsingDirectiveSyntax>())
                {
                    if (string.Equals(directive.GlobalKeyword.ValueText, "global", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, directive, "global using directives are prohibited."));
                    }
                }
            }

            AssertNoViolations(violations, "absence of global usings");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenUsingGroupsFollowThePrescribedOrder()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                List<int> ranks = file.Root
                    .DescendantNodes()
                    .OfType<UsingDirectiveSyntax>()
                    .Where(directive => directive.Parent is CompilationUnitSyntax)
                    .Select(directive => UsingGroupRank(directive.Name?.ToString() ?? string.Empty))
                    .ToList();

                for (int index = 1; index < ranks.Count; index += 1)
                {
                    if (ranks[index] < ranks[index - 1])
                    {
                        violations.Add($"{file.RelativePath}: using groups must be ordered System, Microsoft, third-party, solution.");
                        break;
                    }
                }
            }

            AssertNoViolations(violations, "using group ordering");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenUsingsWithinAGroupAreAlphabetised()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                List<string> usings = file.Root
                    .DescendantNodes()
                    .OfType<UsingDirectiveSyntax>()
                    .Where(directive => directive.Parent is CompilationUnitSyntax)
                    .Select(directive => directive.Name?.ToString() ?? string.Empty)
                    .ToList();

                for (int index = 1; index < usings.Count; index += 1)
                {
                    if (UsingGroupRank(usings[index - 1]) != UsingGroupRank(usings[index]))
                    {
                        continue;
                    }

                    if (string.CompareOrdinal(usings[index - 1], usings[index]) > 0)
                    {
                        violations.Add($"{file.RelativePath}: '{usings[index]}' must precede '{usings[index - 1]}'.");
                    }
                }
            }

            AssertNoViolations(violations, "using alphabetisation");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenExactlyOneBlankLineSeparatesTheUsingsFromTheNamespace()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                if (!file.Root.DescendantNodes().OfType<UsingDirectiveSyntax>().Any())
                {
                    continue;
                }

                int namespaceLine = FindNamespaceLine(file);

                if (namespaceLine <= 0)
                {
                    continue;
                }

                int blankLines = CountPrecedingBlankLines(file, namespaceLine);

                if (blankLines != 1)
                {
                    violations.Add($"{file.RelativePath}:{namespaceLine}: expected exactly one blank line before the namespace, found {blankLines}.");
                }
            }

            AssertNoViolations(violations, "single blank line before the namespace");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenTheNamespaceMatchesTheFolder()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                BaseNamespaceDeclarationSyntax? declaration = file.Root
                    .DescendantNodes()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .FirstOrDefault();

                if (declaration is null)
                {
                    continue;
                }

                string? expected = ExpectedNamespace(file);

                if (expected is null)
                {
                    continue;
                }

                if (!string.Equals(declaration.Name.ToString(), expected, StringComparison.Ordinal))
                {
                    violations.Add($"{file.RelativePath}: namespace '{declaration.Name}' does not match the folder-derived '{expected}'.");
                }
            }

            AssertNoViolations(violations, "namespace and folder correspondence");
        }

        [Test]
        public void GivenEveryProductionSourceFile_WhenInspecting_ThenItsNamespaceIsWithinTheRootNamespace()
        {
            List<string> violations = [];

            foreach (SourceFile file in SourceWorkspace.ProductionFiles)
            {
                BaseNamespaceDeclarationSyntax? declaration = file.Root
                    .DescendantNodes()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .FirstOrDefault();

                if (declaration is null)
                {
                    continue;
                }

                string name = declaration.Name.ToString();

                if (!string.Equals(name, ProductionNamespaces.Root, StringComparison.Ordinal) &&
                    !name.StartsWith(ProductionNamespaces.Root + ".", StringComparison.Ordinal))
                {
                    violations.Add($"{file.RelativePath}: namespace '{name}' is outside the '{ProductionNamespaces.Root}' root.");
                }
            }

            AssertNoViolations(violations, "root namespace containment");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoNestedTypeIsDeclaredInsideAnotherType()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BaseTypeDeclarationSyntax declaration in file.TypeDeclarations)
                {
                    if (declaration.Parent is BaseNamespaceDeclarationSyntax)
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, declaration, $"type '{declaration.Identifier.ValueText}' is nested and must be extracted."));
                }
            }

            AssertNoViolations(violations, "colocated top-level types");
        }

        private static int UsingGroupRank(string name)
        {
            if (name.StartsWith("System", StringComparison.Ordinal))
            {
                return 0;
            }

            if (name.StartsWith("Microsoft", StringComparison.Ordinal))
            {
                return 1;
            }

            if (name.StartsWith("PersonalPresentationWebsite", StringComparison.Ordinal))
            {
                return 3;
            }

            return 2;
        }

        private static int FindNamespaceLine(SourceFile file)
        {
            for (int index = 0; index < file.Lines.Count; index += 1)
            {
                if (file.Lines[index].TrimStart().StartsWith("namespace ", StringComparison.Ordinal))
                {
                    return index + 1;
                }
            }

            return -1;
        }

        private static int CountPrecedingBlankLines(SourceFile file, int targetLine)
        {
            int blankLines = 0;

            for (int index = targetLine - 2; index >= 0; index -= 1)
            {
                if (file.Lines[index].Trim().Length > 0)
                {
                    break;
                }

                blankLines += 1;
            }

            return blankLines;
        }

        private static string? ExpectedNamespace(SourceFile file)
        {
            string relative = Path.GetRelativePath(file.Project.Directory, file.FilePath);
            string? directory = Path.GetDirectoryName(relative);

            if (string.IsNullOrEmpty(directory))
            {
                return file.Project.RootNamespace;
            }

            string suffix = directory.Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.');

            return $"{file.Project.RootNamespace}.{suffix}";
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
