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
    public sealed class TestSourceConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> TestFiles = SourceWorkspace.TestFiles;

        [Test]
        public void GivenTheTestProjects_WhenEnumerating_ThenAtLeastOneTestSourceFileExists()
        {
            RuleAssertions.EnsureSelected(TestFiles.Count, "test source files");
        }

        [Test]
        public void GivenEveryTestClass_WhenInspecting_ThenItIsNamedAfterItsSubjectAndSuffixedWithTests()
        {
            List<string> violations = [];
            Regex pattern = new(@"^[A-Z][A-Za-z0-9]*Tests$");

            foreach (SourceFile file in TestClassFiles())
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (!HasAttribute(declaration, "TestFixture"))
                    {
                        continue;
                    }

                    if (!pattern.IsMatch(declaration.Identifier.ValueText))
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"test class '{declaration.Identifier.ValueText}' must follow 'SubjectTests'."));
                    }
                }
            }

            AssertNoViolations(violations, "test class naming");
        }

        [Test]
        public void GivenEveryTestFixtureClass_WhenInspecting_ThenItDeclaresTheTestFixtureAttribute()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestClassFiles())
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (!declaration.Identifier.ValueText.EndsWith("Tests", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!HasAttribute(declaration, "TestFixture"))
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"test class '{declaration.Identifier.ValueText}' omits [TestFixture]."));
                    }
                }
            }

            AssertNoViolations(violations, "test fixture attribute");
        }

        [Test]
        public void GivenEveryTestMethod_WhenInspecting_ThenItDeclaresATestOrTestCaseAttribute()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles)
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (method.Modifiers.Any(SyntaxKind.PublicKeyword) is false)
                    {
                        continue;
                    }

                    if (method.Identifier.ValueText.StartsWith("SetUp", StringComparison.Ordinal) ||
                        method.Identifier.ValueText.StartsWith("TearDown", StringComparison.Ordinal) ||
                        method.Identifier.ValueText.StartsWith("OneTime", StringComparison.Ordinal) ||
                        method.Identifier.ValueText.StartsWith("Build", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (HasAttribute(method, "Test") ||
                        HasAttribute(method, "TestCase") ||
                        HasAttribute(method, "TestCaseSource"))
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, method, $"test method '{method.Identifier.ValueText}' declares no test attribute."));
                }
            }

            AssertNoViolations(violations, "test method attributes");
        }

        [Test]
        public void GivenEveryUnitTestMethod_WhenInspecting_ThenItsNameFollowsTheGivenWhenThenPattern()
        {
            List<string> violations = [];
            Regex pattern = new(@"^Given\w+_When\w+_Then\w+$");

            foreach (SourceFile file in TestFiles.Where(candidate => candidate.Project.Name.EndsWith("UnitTests", StringComparison.Ordinal)))
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (!HasAttribute(method, "Test") &&
                        !HasAttribute(method, "TestCase") &&
                        !HasAttribute(method, "TestCaseSource"))
                    {
                        continue;
                    }

                    if (!pattern.IsMatch(method.Identifier.ValueText))
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"unit test '{method.Identifier.ValueText}' must follow Given_When_Then."));
                    }
                }
            }

            AssertNoViolations(violations, "unit test naming");
        }

        [Test]
        public void GivenEveryParameterisedTestMethod_WhenInspecting_ThenItDeclaresAtLeastTwoCases()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles)
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    int testCaseCount = method.AttributeLists
                        .SelectMany(list => list.Attributes)
                        .Count(attribute => string.Equals(attribute.Name.ToString(), "TestCase", StringComparison.Ordinal));

                    bool hasSource = HasAttribute(method, "TestCaseSource");

                    if (testCaseCount == 1 && !hasSource)
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"parameterised test '{method.Identifier.ValueText}' declares only one case."));
                    }
                }
            }

            AssertNoViolations(violations, "minimum two parameterised cases");
        }

        [Test]
        public void GivenEveryTestMethod_WhenInspecting_ThenItDoesNotUseLegacyClassicAssertions()
        {
            List<string> violations = [];
            string[] legacyAssertions =
            [
                "AreEqual", "AreNotEqual", "IsTrue", "IsFalse", "IsNull", "IsNotNull",
                "Greater", "Less", "Zero", "NotZero", "IsEmpty", "IsNotEmpty"
            ];

            foreach (SourceFile file in TestFiles)
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax access)
                    {
                        continue;
                    }

                    if (access.Expression.ToString() != "Assert")
                    {
                        continue;
                    }

                    if (legacyAssertions.Contains(access.Name.Identifier.ValueText))
                    {
                        violations.Add(SyntaxInspections.Describe(file, invocation, $"uses the classic assertion 'Assert.{access.Name.Identifier.ValueText}'; prefer the constraint model."));
                    }
                }
            }

            AssertNoViolations(violations, "NUnit constraint model");
        }

        [Test]
        public void GivenEveryBusinessTestMethod_WhenInspecting_ThenItContainsAnAssertionOrVerification()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestClassFiles())
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (!HasAttribute(method, "Test") &&
                        !HasAttribute(method, "TestCase") &&
                        !HasAttribute(method, "TestCaseSource"))
                    {
                        continue;
                    }

                    bool hasAssertion = method.DescendantNodes()
                        .OfType<InvocationExpressionSyntax>()
                        .Any(IsAssertionOrVerification);

                    if (!hasAssertion)
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"test '{method.Identifier.ValueText}' contains no assertion or verification."));
                    }
                }
            }

            AssertNoViolations(violations, "assertion presence");
        }

        [Test]
        public void GivenEveryUnitTestMethod_WhenInspecting_ThenItDoesNotUseReflection()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles.Where(candidate => candidate.Project.Name.EndsWith("UnitTests", StringComparison.Ordinal)))
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    string text = invocation.ToString();

                    if (text.StartsWith("typeof(", StringComparison.Ordinal) &&
                        (text.Contains(".GetMethod", StringComparison.Ordinal) ||
                         text.Contains(".GetProperty", StringComparison.Ordinal) ||
                         text.Contains(".GetField", StringComparison.Ordinal)))
                    {
                        violations.Add(SyntaxInspections.Describe(file, invocation, "unit tests must not use reflection to reach non-public code."));
                    }
                }
            }

            AssertNoViolations(violations, "reflection misuse");
        }

        [Test]
        public void GivenEveryUnitTestProject_WhenInspecting_ThenItDeclaresNoConfigurationOnlyDependencyOnFileSystems()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles)
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    string text = invocation.ToString();

                    if (text.StartsWith("Directory.GetCurrentDirectory(", StringComparison.Ordinal) ||
                        text.StartsWith("Environment.GetEnvironmentVariable(", StringComparison.Ordinal) ||
                        text.StartsWith("File.ReadAllText(\"", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, invocation, "test sources must not depend on the host filesystem or environment."));
                    }
                }
            }

            AssertNoViolations(violations, "host independent tests");
        }

        [Test]
        public void GivenEveryJsonSerializerOptionsUsage_WhenInspecting_ThenItIsNotConstructedPerCall()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles)
            {
                foreach (ObjectCreationExpressionSyntax creation in file.Root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                {
                    if (!string.Equals(creation.Type.ToString(), "JsonSerializerOptions", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    bool isFieldInitialiser = creation.Ancestors().OfType<FieldDeclarationSyntax>().Any();

                    if (!isFieldInitialiser)
                    {
                        violations.Add(SyntaxInspections.Describe(file, creation, "cache JsonSerializerOptions in a static readonly field."));
                    }
                }
            }

            AssertNoViolations(violations, "cached serializer options in tests");
        }

        [Test]
        public void GivenEveryTestDummyType_WhenInspecting_ThenItIsNotNestedInsideATestClass()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestClassFiles())
            {
                foreach (BaseTypeDeclarationSyntax declaration in file.TypeDeclarations)
                {
                    if (declaration.Parent is BaseNamespaceDeclarationSyntax)
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, declaration, $"dummy type '{declaration.Identifier.ValueText}' must be a top-level internal class in its own file."));
                }
            }

            AssertNoViolations(violations, "top-level test dummies");
        }

        [Test]
        public void GivenEveryIntegrationTestFile_WhenInspecting_ThenItResidesInAnIntegrationTestsProject()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles)
            {
                string namespaceName = file.Root
                    .DescendantNodes()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .FirstOrDefault()?.Name.ToString() ?? string.Empty;

                if (namespaceName.Contains(".IntegrationTests", StringComparison.Ordinal) &&
                    !file.Project.Name.EndsWith(".IntegrationTests", StringComparison.Ordinal))
                {
                    violations.Add($"{file.RelativePath}: integration-test namespaces must reside in an integration-test project.");
                }
            }

            AssertNoViolations(violations, "integration test placement");
        }

        private static IEnumerable<SourceFile> TestClassFiles()
            => TestFiles.Where(file => !file.Project.Name.EndsWith(".ArchitectureTests", StringComparison.Ordinal));

        private static bool HasAttribute(MemberDeclarationSyntax member, string attributeName)
            => member.AttributeLists
                .SelectMany(list => list.Attributes)
                .Any(attribute => string.Equals(attribute.Name.ToString(), attributeName, StringComparison.Ordinal));

        private static bool IsAssertionOrVerification(InvocationExpressionSyntax invocation)
        {
            string text = invocation.ToString();

            if (text.StartsWith("Assert.", StringComparison.Ordinal) ||
                text.StartsWith("AssertNoViolations(", StringComparison.Ordinal) ||
                text.StartsWith("RuleAssertions.", StringComparison.Ordinal) ||
                text.StartsWith("Verify(", StringComparison.Ordinal) ||
                text.Contains(".Verify(", StringComparison.Ordinal) ||
                text.Contains(".MarkupMatches(", StringComparison.Ordinal) ||
                text.StartsWith("MarkupMatches(", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
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
