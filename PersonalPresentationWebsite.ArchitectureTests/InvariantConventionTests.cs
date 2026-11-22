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
    public sealed class InvariantConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoDynamicKeywordIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (IdentifierNameSyntax identifier in file.Root.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    if (string.Equals(identifier.Identifier.ValueText, "dynamic", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, identifier, "the 'dynamic' keyword bypasses compile-time safety."));
                    }
                }

                foreach (PredefinedTypeSyntax predefined in file.Root.DescendantNodes().OfType<PredefinedTypeSyntax>())
                {
                    if (predefined.Keyword.Text == "dynamic")
                    {
                        violations.Add(SyntaxInspections.Describe(file, predefined, "the 'dynamic' type bypasses compile-time safety."));
                    }
                }
            }

            AssertNoViolations(violations, "static typing without dynamic");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoGotoStatementIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (GotoStatementSyntax statement in file.Root.DescendantNodes().OfType<GotoStatementSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, statement, "goto statements obstruct structured control flow."));
                }
            }

            AssertNoViolations(violations, "structured control flow");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoLabeledStatementIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (LabeledStatementSyntax statement in file.Root.DescendantNodes().OfType<LabeledStatementSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, statement, "labeled statements are prohibited."));
                }
            }

            AssertNoViolations(violations, "no labeled statements");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoUnsafeBlockOrPointerIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (UnsafeStatementSyntax statement in file.Root.DescendantNodes().OfType<UnsafeStatementSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, statement, "unsafe blocks are prohibited in application code."));
                }

                foreach (PointerTypeSyntax pointer in file.Root.DescendantNodes().OfType<PointerTypeSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, pointer, $"pointer type '{pointer}' is prohibited."));
                }
            }

            AssertNoViolations(violations, "memory-safe code");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoFixedStatementIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (FixedStatementSyntax statement in file.Root.DescendantNodes().OfType<FixedStatementSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, statement, "fixed statements pinning memory are prohibited."));
                }
            }

            AssertNoViolations(violations, "no pinned memory");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoPreprocessorConditionalCompilationIsUsedInLogic()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (SyntaxTrivia trivia in file.Root.DescendantTrivia())
                {
                    if (trivia.IsKind(SyntaxKind.IfDirectiveTrivia) ||
                        trivia.IsKind(SyntaxKind.ElseDirectiveTrivia) ||
                        trivia.IsKind(SyntaxKind.ElifDirectiveTrivia))
                    {
                        violations.Add($"{file.RelativePath}:{trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: conditional compilation directives fragment the logic; prefer configuration or separate builds.");
                    }
                }
            }

            AssertNoViolations(violations, "no conditional compilation");
        }

        [Test]
        public void GivenEveryTestMethod_WhenInspecting_ThenNoThreadSleepIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.ToString().StartsWith("Thread.Sleep", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, invocation, "Thread.Sleep makes tests slow and flaky; use a deterministic synchronisation primitive."));
                    }
                }
            }

            AssertNoViolations(violations, "no thread sleeping in tests");
        }

        [Test]
        public void GivenEveryTestMethod_WhenInspecting_ThenNoOrderedTestAttributeIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in TestFiles())
            {
                foreach (AttributeSyntax attribute in file.Root.DescendantNodes().OfType<AttributeSyntax>())
                {
                    string name = attribute.Name.ToString();

                    if (name is "Order" or "NonParallelizable" or "Parallelizable")
                    {
                        violations.Add(SyntaxInspections.Describe(file, attribute, $"[{name}] couples tests to execution order; keep every test independent."));
                    }
                }
            }

            AssertNoViolations(violations, "order-independent tests");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoCheckedOrUncheckedArithmeticBlockIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (CheckedExpressionSyntax checkedExpression in file.Root.DescendantNodes().OfType<CheckedExpressionSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, checkedExpression, "explicit checked or unchecked arithmetic obscures overflow semantics; handle overflow explicitly."));
                }
            }

            AssertNoViolations(violations, "explicit overflow handling");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoStackAllocIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.ToString().StartsWith("stackalloc", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, invocation, "stackalloc allocations are prohibited in application code."));
                    }
                }
            }

            AssertNoViolations(violations, "heap-managed allocation");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoExplicitDestructorIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (DestructorDeclarationSyntax destructor in file.Root.DescendantNodes().OfType<DestructorDeclarationSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, destructor, "finalizers are nondeterministic; implement IDisposable for deterministic cleanup."));
                }
            }

            AssertNoViolations(violations, "deterministic cleanup over finalizers");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoOperatorOverloadIsDeclaredOutsideAnEnumerationClass()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (OperatorDeclarationSyntax operatorDeclaration in file.Root.DescendantNodes().OfType<OperatorDeclarationSyntax>())
                {
                    bool equalityOperator = operatorDeclaration.OperatorToken.IsKind(SyntaxKind.EqualsEqualsToken) ||
                        operatorDeclaration.OperatorToken.IsKind(SyntaxKind.ExclamationEqualsToken);

                    if (equalityOperator)
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, operatorDeclaration, "operator overloads other than enumeration equality are prohibited."));
                }
            }

            AssertNoViolations(violations, "restricted operator overloading");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoConversionOperatorIsDeclaredOutsideAnEnumerationClass()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ConversionOperatorDeclarationSyntax conversion in file.Root.DescendantNodes().OfType<ConversionOperatorDeclarationSyntax>())
                {
                    bool implicitToString = conversion.ImplicitOrExplicitKeyword.IsKind(SyntaxKind.ImplicitKeyword) &&
                        conversion.Type.ToString() == "string";

                    if (implicitToString)
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, conversion, "conversion operators other than the enumeration implicit string conversion are prohibited."));
                }
            }

            AssertNoViolations(violations, "restricted conversion operators");
        }

        private static IEnumerable<SourceFile> TestFiles()
            => SourceWorkspace.TestFiles;

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
