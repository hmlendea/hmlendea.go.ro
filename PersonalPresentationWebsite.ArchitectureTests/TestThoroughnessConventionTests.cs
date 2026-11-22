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
    public sealed class TestThoroughnessConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> TestFiles = SourceWorkspace.TestFiles;

        [Test]
        public void GivenEveryTestClass_WhenInspecting_ThenItDeclaresNoDuplicateTestName()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles)
            {
                HashSet<string> names = [];

                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (!HasTestAttribute(method))
                    {
                        continue;
                    }

                    if (!names.Add(method.Identifier.ValueText))
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"duplicate test name '{method.Identifier.ValueText}' within the same fixture."));
                    }
                }
            }

            AssertNoViolations(violations, "unique test method names");
        }

        [Test]
        public void GivenEveryTestClass_WhenInspecting_ThenEveryTestCaseDeclaresAtLeastOneArgument()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles)
            {
                foreach (AttributeSyntax attribute in file.Root.DescendantNodes().OfType<AttributeSyntax>())
                {
                    if (attribute.Name.ToString() != "TestCase")
                    {
                        continue;
                    }

                    if (attribute.ArgumentList is null || attribute.ArgumentList.Arguments.Count == 0)
                    {
                        violations.Add(SyntaxInspections.Describe(file, attribute, "[TestCase] must declare at least one argument."));
                    }
                }
            }

            AssertNoViolations(violations, "non-empty parameterised cases");
        }

        [Test]
        public void GivenEveryParameterisedTest_WhenInspecting_ThenItsTestCaseArgumentCountMatchesTheParameterCount()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles)
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    List<AttributeSyntax> cases = method.AttributeLists
                        .SelectMany(list => list.Attributes)
                        .Where(attribute => attribute.Name.ToString() == "TestCase")
                        .ToList();

                    if (cases.Count == 0)
                    {
                        continue;
                    }

                    int parameterCount = method.ParameterList.Parameters.Count;

                    foreach (AttributeSyntax testCase in cases)
                    {
                        int argumentCount = testCase.ArgumentList?.Arguments.Count ?? 0;

                        if (argumentCount != parameterCount)
                        {
                            violations.Add(SyntaxInspections.Describe(file, testCase, $"[TestCase] supplies {argumentCount} arguments but method '{method.Identifier.ValueText}' declares {parameterCount} parameters."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "parameterised case arity");
        }

        [Test]
        public void GivenEveryTestClass_WhenInspecting_ThenSetUpConstructsItsDependenciesOrDeclaresNone()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    MethodDeclarationSyntax? setUp = declaration.Members
                        .OfType<MethodDeclarationSyntax>()
                        .FirstOrDefault(method => method.AttributeLists
                            .SelectMany(list => list.Attributes)
                            .Any(attribute => attribute.Name.ToString() == "SetUp"));

                    if (setUp is null)
                    {
                        continue;
                    }

                    if (setUp.Identifier.ValueText != "SetUp")
                    {
                        violations.Add(SyntaxInspections.Describe(file, setUp, "[SetUp] methods must be named exactly 'SetUp'."));
                    }
                }
            }

            AssertNoViolations(violations, "SetUp naming");
        }

        [Test]
        public void GivenEveryTestClass_WhenInspecting_ThenItDeclaresNoStaticTestStateOutsideReadonlyFields()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    foreach (FieldDeclarationSyntax field in declaration.Members.OfType<FieldDeclarationSyntax>())
                    {
                        bool isStatic = field.Modifiers.Any(SyntaxKind.StaticKeyword);
                        bool isReadonly = field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) || field.Modifiers.Any(SyntaxKind.ConstKeyword);

                        if (isStatic && !isReadonly)
                        {
                            violations.Add(SyntaxInspections.Describe(file, field, "mutable static test state leaks between test executions; use instance fields constructed in [SetUp]."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "no mutable static test state");
        }

        [Test]
        public void GivenEveryIntegrationTestClass_WhenInspecting_ThenItDisposesItsResources()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles.Where(candidate => candidate.Project.Name.EndsWith("IntegrationTests", StringComparison.Ordinal)))
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    bool declaresDisposableField = declaration.Members
                        .OfType<FieldDeclarationSyntax>()
                        .Any(field => field.Declaration.Type.ToString() is "HttpClient" or "PersonalPresentationWebsiteFactory" or "WebApplicationFactory");

                    if (!declaresDisposableField)
                    {
                        continue;
                    }

                    bool hasTearDown = declaration.Members
                        .OfType<MethodDeclarationSyntax>()
                        .Any(method => method.AttributeLists
                            .SelectMany(list => list.Attributes)
                            .Any(attribute => attribute.Name.ToString() is "TearDown" or "OneTimeTearDown"));

                    bool implementsDisposable = declaration.BaseList is not null &&
                        declaration.BaseList.Types.Any(type => type.Type.ToString() == "IDisposable");

                    if (!hasTearDown && !implementsDisposable)
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"integration fixture '{declaration.Identifier.ValueText}' holds disposable resources but declares no teardown."));
                    }
                }
            }

            AssertNoViolations(violations, "integration test teardown");
        }

        [Test]
        public void GivenEveryTestMethod_WhenInspecting_ThenNotEveryAssertionIsConditional()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles)
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (!HasTestAttribute(method))
                    {
                        continue;
                    }

                    if (method.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => attribute.Name.ToString() is "TestCaseSource"))
                    {
                        continue;
                    }

                    List<InvocationExpressionSyntax> assertions = method.DescendantNodes()
                        .OfType<InvocationExpressionSyntax>()
                        .Where(IsAssertionInvocation)
                        .ToList();

                    if (assertions.Count == 0)
                    {
                        continue;
                    }

                    bool allConditional = assertions.All(assertion =>
                        assertion.Ancestors().OfType<IfStatementSyntax>().Any());

                    if (allConditional)
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"test '{method.Identifier.ValueText}' places every assertion behind a condition, so it can pass whilst verifying nothing."));
                    }
                }
            }

            AssertNoViolations(violations, "unconditional assertions in every test");
        }

        [Test]
        public void GivenEveryTestMethod_WhenInspecting_ThenItContainsNoTryCatchThatHidesFailures()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles)
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (!HasTestAttribute(method))
                    {
                        continue;
                    }

                    foreach (CatchClauseSyntax catchClause in method.DescendantNodes().OfType<CatchClauseSyntax>())
                    {
                        bool assertsInCatch = catchClause.DescendantNodes()
                            .OfType<InvocationExpressionSyntax>()
                            .Any(invocation => invocation.ToString().StartsWith("Assert", StringComparison.Ordinal));

                        if (!assertsInCatch)
                        {
                            violations.Add(SyntaxInspections.Describe(file, catchClause, "a catch block inside a test must assert or rethrow; swallowing the exception hides the failure."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "no swallowed test failures");
        }

        [Test]
        public void GivenEveryTestClass_WhenInspecting_ThenItsNameEndsWithTestsAndIsPublicSealed()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (!HasTestAttribute(declaration))
                    {
                        continue;
                    }

                    if (!declaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"fixture '{declaration.Identifier.ValueText}' must be public."));
                    }

                    if (!declaration.Modifiers.Any(SyntaxKind.SealedKeyword))
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"fixture '{declaration.Identifier.ValueText}' must be sealed."));
                    }
                }
            }

            AssertNoViolations(violations, "public sealed fixtures");
        }

        [Test]
        public void GivenEveryTestClass_WhenInspecting_ThenItsMockFieldsArePrivate()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (!HasTestAttribute(declaration))
                    {
                        continue;
                    }

                    foreach (FieldDeclarationSyntax field in declaration.Members.OfType<FieldDeclarationSyntax>())
                    {
                        if (field.Modifiers.Any(SyntaxKind.PrivateKeyword))
                        {
                            continue;
                        }

                        if (field.Modifiers.Any(SyntaxKind.StaticKeyword))
                        {
                            continue;
                        }

                        violations.Add(SyntaxInspections.Describe(file, field, "instance fixture fields must be private."));
                    }
                }
            }

            AssertNoViolations(violations, "private fixture fields");
        }

        private static bool IsAssertionInvocation(InvocationExpressionSyntax invocation)
        {
            string text = invocation.ToString();

            return text.StartsWith("Assert.", StringComparison.Ordinal) ||
                text.StartsWith("AssertNoViolations(", StringComparison.Ordinal) ||
                text.StartsWith("RuleAssertions.", StringComparison.Ordinal) ||
                text.StartsWith("RuleAssertions.", StringComparison.Ordinal) ||
                text.Contains(".Verify(", StringComparison.Ordinal) ||
                text.Contains(".Should()", StringComparison.Ordinal);
        }

        private static bool HasTestAttribute(MemberDeclarationSyntax member)
            => member.AttributeLists
                .SelectMany(list => list.Attributes)
                .Any(attribute => attribute.Name.ToString() == "TestFixture" ||
                    attribute.Name.ToString() == "Test" ||
                    attribute.Name.ToString() == "TestCase" ||
                    attribute.Name.ToString() == "TestCaseSource");

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
