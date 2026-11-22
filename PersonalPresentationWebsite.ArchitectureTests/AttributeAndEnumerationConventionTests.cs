using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class AttributeAndEnumerationConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        [Test]
        public void GivenEveryEnumDeclaration_WhenInspecting_ThenNoNumericValueIsExplicitlyAssigned()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (EnumDeclarationSyntax declaration in file.TypeDeclarations.OfType<EnumDeclarationSyntax>())
                {
                    foreach (EnumMemberDeclarationSyntax member in declaration.Members)
                    {
                        if (member.EqualsValue is null)
                        {
                            continue;
                        }

                        violations.Add(SyntaxInspections.Describe(file, member, $"enum member '{member.Identifier.ValueText}' assigns an explicit numeric value; enumerations for categories must not encode persisted indices."));
                    }
                }
            }

            AssertNoViolations(violations, "no persisted enum indices");
        }

        [Test]
        public void GivenEveryEnumNumericContract_WhenInspecting_ThenValuesAreUniqueUnlessAliased()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (EnumDeclarationSyntax declaration in file.TypeDeclarations.OfType<EnumDeclarationSyntax>())
                {
                    List<long> values = [];

                    foreach (EnumMemberDeclarationSyntax member in declaration.Members)
                    {
                        if (member.EqualsValue?.Value is LiteralExpressionSyntax literal &&
                            literal.Token.Value is not null &&
                            long.TryParse(literal.Token.ValueText, out long value))
                        {
                            values.Add(value);
                        }
                    }

                    if (values.Count != values.Distinct().Count())
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"enum '{declaration.Identifier.ValueText}' declares duplicate numeric values without a documented alias."));
                    }
                }
            }

            AssertNoViolations(violations, "unique enum values");
        }

        [Test]
        public void GivenEveryAttribute_WhenInspecting_ThenItIsPlacedOnTheLineAboveTheDeclaration()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (AttributeListSyntax attributeList in file.Root.DescendantNodes().OfType<AttributeListSyntax>())
                {
                    int attributeLine = attributeList.GetLocation().GetLineSpan().StartLinePosition.Line;
                    SyntaxNode? declaration = attributeList.Parent;

                    if (declaration is null)
                    {
                        continue;
                    }

                    int declarationLine = declaration.GetLocation().GetLineSpan().StartLinePosition.Line;

                    if (attributeLine == declarationLine &&
                        declaration is PropertyDeclarationSyntax or FieldDeclarationSyntax)
                    {
                        violations.Add(SyntaxInspections.Describe(file, attributeList, "an attribute must be placed on the line above its declaration, not inline."));
                    }
                }
            }

            AssertNoViolations(violations, "attribute placement");
        }

        [Test]
        public void GivenEveryTestAttribute_WhenInspecting_ThenNoRedundantFixtureAndTestOnTheSameMethod()
        {
            List<string> violations = [];

            foreach (SourceFile file in SourceWorkspace.TestFiles)
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    List<string> attributeNames = method.AttributeLists
                        .SelectMany(list => list.Attributes)
                        .Select(attribute => attribute.Name.ToString())
                        .ToList();

                    if (attributeNames.Contains("TestFixture"))
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, "[TestFixture] must be applied to the class, never to a method."));
                    }

                    if (attributeNames.Contains("Test") && attributeNames.Contains("TestCase"))
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, "a method must not combine [Test] and [TestCase]."));
                    }

                    if (attributeNames.Count(name => name == "TestCase") > 0 && attributeNames.Contains("Test"))
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, "a parameterised case must not also carry [Test]."));
                    }
                }
            }

            AssertNoViolations(violations, "test attribute correctness");
        }

        [Test]
        public void GivenEveryTestFixture_WhenInspecting_ThenNoFixtureIsAppliedToATypeThatIsNotATestClass()
        {
            List<string> violations = [];

            foreach (SourceFile file in SourceWorkspace.TestFiles)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    bool hasFixture = declaration.AttributeLists
                        .SelectMany(list => list.Attributes)
                        .Any(attribute => attribute.Name.ToString() == "TestFixture");

                    bool hasTestMethods = declaration.Members
                        .OfType<MethodDeclarationSyntax>()
                        .Any(method => method.AttributeLists
                            .SelectMany(list => list.Attributes)
                            .Any(attribute => attribute.Name.ToString() is "Test" or "TestCase" or "TestCaseSource"));

                    if (hasFixture && !hasTestMethods)
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"fixture '{declaration.Identifier.ValueText}' declares no test method."));
                    }
                }
            }

            AssertNoViolations(violations, "fixtures declaring tests");
        }

        [Test]
        public void GivenEveryEnumDeclaration_WhenInspecting_ThenItsValueNamesAreSingularNouns()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (EnumDeclarationSyntax declaration in file.TypeDeclarations.OfType<EnumDeclarationSyntax>())
                {
                    if (!declaration.Identifier.ValueText.EndsWith("Type", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (EnumMemberDeclarationSyntax member in declaration.Members)
                    {
                        string name = member.Identifier.ValueText;

                        if (name.EndsWith('s') && !name.EndsWith("ss", StringComparison.Ordinal))
                        {
                            violations.Add(SyntaxInspections.Describe(file, member, $"enum member '{name}' appears plural but must be a singular category name."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "singular enum category names");
        }

        [Test]
        public void GivenEveryNullableAnnotation_WhenInspecting_ThenNoAnnotationIsRedundantWithTheDefault()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ParameterSyntax parameter in file.Root.DescendantNodes().OfType<ParameterSyntax>())
                {
                    if (parameter.Type is null)
                    {
                        continue;
                    }

                    if (parameter.Type is NullableTypeSyntax nullable &&
                        nullable.ElementType is IdentifierNameSyntax identifier &&
                        identifier.Identifier.ValueText is "string")
                    {
                        violations.Add(SyntaxInspections.Describe(file, parameter, $"parameter '{parameter.Identifier.ValueText}' uses an explicit nullable annotation; express nullability through the value contract instead."));
                    }
                }
            }

            AssertNoViolations(violations, "minimal nullable annotations");
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
