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
    public sealed class MethodContractConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoMethodHidesItsResultWithADiscardOnAValueReturningCall()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (AssignmentExpressionSyntax assignment in file.Root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    if (assignment.Right is not InvocationExpressionSyntax invocation)
                    {
                        continue;
                    }

                    string calledName = invocation.Expression switch
                    {
                        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
                        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                        _ => string.Empty
                    };

                    if (calledName.StartsWith("Try", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, assignment, $"the result of 'Try{calledName}' must be checked; discarding a Try* result hides failures."));
                    }
                }
            }

            AssertNoViolations(violations, "no discarded Try* results");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoBooleanComparisonIsMadeAgainstATrueOrFalseLiteral()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BinaryExpressionSyntax expression in file.Root.DescendantNodes().OfType<BinaryExpressionSyntax>())
                {
                    if (!expression.IsKind(SyntaxKind.EqualsExpression) && !expression.IsKind(SyntaxKind.NotEqualsExpression))
                    {
                        continue;
                    }

                    bool comparesBooleanLiteral =
                        expression.Left.IsKind(SyntaxKind.TrueLiteralExpression) ||
                        expression.Left.IsKind(SyntaxKind.FalseLiteralExpression) ||
                        expression.Right.IsKind(SyntaxKind.TrueLiteralExpression) ||
                        expression.Right.IsKind(SyntaxKind.FalseLiteralExpression);

                    if (comparesBooleanLiteral)
                    {
                        violations.Add(SyntaxInspections.Describe(file, expression, "a boolean must be used directly, never compared against true or false."));
                    }
                }
            }

            AssertNoViolations(violations, "direct boolean usage");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoNonShortCircuitLogicalOperatorIsUsedOnBooleans()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BinaryExpressionSyntax expression in file.Root.DescendantNodes().OfType<BinaryExpressionSyntax>())
                {
                    if (expression.IsKind(SyntaxKind.BitwiseAndExpression) || expression.IsKind(SyntaxKind.BitwiseOrExpression))
                    {
                        violations.Add(SyntaxInspections.Describe(file, expression, "use the short-circuit operators && and || for boolean logic."));
                    }
                }
            }

            AssertNoViolations(violations, "short-circuit boolean operators");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoReturnTypeIsExpressedAsATaskOfVoidOutsideAnEventHandler()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    string returnType = method.ReturnType.ToString();

                    if (returnType == "void" && method.Modifiers.Any(SyntaxKind.AsyncKeyword))
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"async void method '{method.Identifier.ValueText}' cannot be awaited; return Task instead unless it is an event handler."));
                    }
                }
            }

            AssertNoViolations(violations, "awaitable asynchronous methods");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoRedundantCastIsAppliedToAnAlreadyTypedExpression()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (CastExpressionSyntax cast in file.Root.DescendantNodes().OfType<CastExpressionSyntax>())
                {
                    if (cast.Expression is ObjectCreationExpressionSyntax creation &&
                        string.Equals(cast.Type.ToString(), creation.Type.ToString(), StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, cast, "a cast applied to a newly constructed instance of the same type is redundant."));
                    }
                }
            }

            AssertNoViolations(violations, "no redundant casts");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoAssignmentIsUsedAsAnExpressionInsideACondition()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (IfStatementSyntax statement in file.Root.DescendantNodes().OfType<IfStatementSyntax>())
                {
                    if (statement.Condition.DescendantNodesAndSelf().OfType<AssignmentExpressionSyntax>().Any())
                    {
                        violations.Add(SyntaxInspections.Describe(file, statement.Condition, "an assignment inside a condition obscures intent; compare or assign separately."));
                    }
                }

                foreach (WhileStatementSyntax statement in file.Root.DescendantNodes().OfType<WhileStatementSyntax>())
                {
                    if (statement.Condition.DescendantNodesAndSelf().OfType<AssignmentExpressionSyntax>().Any())
                    {
                        violations.Add(SyntaxInspections.Describe(file, statement.Condition, "an assignment inside a while condition obscures intent; assign separately."));
                    }
                }
            }

            AssertNoViolations(violations, "no assignments inside conditions");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoStringComparisonUsesTheDefaultCultureImplicitly()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    string text = invocation.ToString();

                    bool isCultureSensitive =
                        text.StartsWith("string.Compare(", StringComparison.Ordinal) ||
                        text.StartsWith("string.ToUpper(", StringComparison.Ordinal) ||
                        text.StartsWith("string.ToLower(", StringComparison.Ordinal) ||
                        text.Contains(".ToUpper()", StringComparison.Ordinal) ||
                        text.Contains(".ToLower()", StringComparison.Ordinal);

                    if (isCultureSensitive && !text.Contains("CultureInfo", StringComparison.Ordinal) &&
                        !text.Contains("Invariant", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, invocation, "culture-sensitive string operations must declare CultureInfo.InvariantCulture or an explicit culture."));
                    }
                }
            }

            AssertNoViolations(violations, "culture-independent string operations");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoUnusedParameterIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (method.Body is null && method.ExpressionBody is null)
                    {
                        continue;
                    }

                    if (method.Modifiers.Any(SyntaxKind.OverrideKeyword) ||
                        method.Modifiers.Any(SyntaxKind.VirtualKeyword) ||
                        method.Modifiers.Any(SyntaxKind.AbstractKeyword))
                    {
                        continue;
                    }

                    if (method.Parent is InterfaceDeclarationSyntax)
                    {
                        continue;
                    }

                    string body = method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? string.Empty;

                    foreach (ParameterSyntax parameter in method.ParameterList.Parameters)
                    {
                        string name = parameter.Identifier.ValueText;

                        if (name.Length == 0 || name == "_")
                        {
                            continue;
                        }

                        if (!body.Contains(name, StringComparison.Ordinal))
                        {
                            violations.Add(SyntaxInspections.Describe(file, parameter, $"parameter '{name}' is never used in method '{method.Identifier.ValueText}'."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "no unused parameters");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoNullForgivingOperatorIsUsedToSilenceWarnings()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (PostfixUnaryExpressionSyntax expression in file.Root.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>())
                {
                    if (expression.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                    {
                        violations.Add(SyntaxInspections.Describe(file, expression, "the null-forgiving operator silences the nullable analysis; restructure the code instead."));
                    }
                }
            }

            AssertNoViolations(violations, "no null-forgiving operators");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoDoWhileLoopIsUsedWhereAForEachWouldSuffice()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (DoStatementSyntax statement in file.Root.DescendantNodes().OfType<DoStatementSyntax>())
                {
                    if (statement.Condition is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.TrueLiteralExpression))
                    {
                        bool returnsInside = statement.Statement.DescendantNodes().OfType<ReturnStatementSyntax>().Any();

                        if (returnsInside)
                        {
                            violations.Add(SyntaxInspections.Describe(file, statement, "an unbounded do-while with a nested return should be expressed as a while loop for clarity."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "clear loop form");
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
