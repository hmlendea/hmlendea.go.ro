using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class SelectorCoverageTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.Files;

        [Test]
        public void GivenTheProductionSelector_WhenApplied_ThenItMatchesAtLeastOneFile()
        {
            RuleAssertions.EnsureSelected(
                SourceWorkspace.ProductionFiles.Count,
                "production source files");
        }

        [Test]
        public void GivenTheTestSelector_WhenApplied_ThenItMatchesAtLeastOneFile()
        {
            RuleAssertions.EnsureSelected(
                SourceWorkspace.TestFiles.Count,
                "test source files");
        }

        [Test]
        public void GivenTheConfigurationSelector_WhenApplied_ThenItMatchesAtLeastOneType()
        {
            int count = Files
                .Where(file => !file.Project.IsTestProject)
                .SelectMany(file => file.TypeDeclarations)
                .Count(declaration => declaration.Ancestors()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .Any(ns => string.Equals(ns.Name.ToString(), ProductionNamespaces.Configuration, StringComparison.Ordinal)));

            RuleAssertions.EnsureSelected(count, "configuration types");
        }

        [Test]
        public void GivenTheEndpointSelector_WhenApplied_ThenItMatchesAtLeastOneType()
        {
            int count = Files
                .Where(file => !file.Project.IsTestProject)
                .SelectMany(file => file.TypeDeclarations)
                .Count(declaration => declaration.Ancestors()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .Any(ns => string.Equals(ns.Name.ToString(), ProductionNamespaces.Endpoints, StringComparison.Ordinal)));

            RuleAssertions.EnsureSelected(count, "endpoint types");
        }

        [Test]
        public void GivenTheTestClassSelector_WhenApplied_ThenItMatchesAtLeastOneClass()
        {
            int count = SourceWorkspace.TestFiles
                .SelectMany(file => file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                .Count();

            RuleAssertions.EnsureSelected(count, "test classes");
        }
    }
}
