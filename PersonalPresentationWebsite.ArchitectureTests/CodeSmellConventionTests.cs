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
    public sealed class CodeSmellConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        private static readonly string[] MagicNumberExceptions = ["0", "1", "2", "-1", "0.0", "1.0"];

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoMagicNumberLiteralIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (BinaryExpressionSyntax expression in file.Root.DescendantNodes().OfType<BinaryExpressionSyntax>())
                {
                    if (!expression.IsKind(SyntaxKind.GreaterThanExpression) &&
                        !expression.IsKind(SyntaxKind.LessThanExpression) &&
                        !expression.IsKind(SyntaxKind.GreaterThanOrEqualExpression) &&
                        !expression.IsKind(SyntaxKind.LessThanOrEqualExpression))
                    {
                        continue;
                    }

                    foreach (ExpressionSyntax operand in new[] { expression.Left, expression.Right })
                    {
                        if (operand is not LiteralExpressionSyntax literal || !literal.IsKind(SyntaxKind.NumericLiteralExpression))
                        {
                            continue;
                        }

                        string text = literal.Token.ValueText;

                        if (MagicNumberExceptions.Contains(text, StringComparer.Ordinal))
                        {
                            continue;
                        }

                        violations.Add(SyntaxInspections.Describe(file, literal, $"threshold literal '{text}' is a magic value; use a named constant."));
                    }
                }
            }

            AssertNoViolations(violations, "named constants instead of magic numbers");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoRedundantElseFollowsAReturningIf()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (IfStatementSyntax statement in file.Root.DescendantNodes().OfType<IfStatementSyntax>())
                {
                    if (statement.Else is null)
                    {
                        continue;
                    }

                    StatementSyntax thenBody = statement.Statement;

                    bool thenAlwaysJumps = thenBody is ReturnStatementSyntax or ThrowStatementSyntax or ContinueStatementSyntax or BreakStatementSyntax ||
                        thenBody is BlockSyntax block && block.Statements.Count > 0 &&
                        block.Statements[^1] is ReturnStatementSyntax or ThrowStatementSyntax or ContinueStatementSyntax or BreakStatementSyntax;

                    if (thenAlwaysJumps)
                    {
                        violations.Add(SyntaxInspections.Describe(file, statement.Else, "an else clause after a returning or throwing if branch is redundant; flatten the control flow."));
                    }
                }
            }

            AssertNoViolations(violations, "no redundant else after a returning branch");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoExplicitThisQualificationIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ThisExpressionSyntax expression in file.Root.DescendantNodes().OfType<ThisExpressionSyntax>())
                {
                    if (expression.Parent is MemberAccessExpressionSyntax)
                    {
                        violations.Add(SyntaxInspections.Describe(file, expression, "explicit 'this.' qualification is redundant in this codebase."));
                    }
                }
            }

            AssertNoViolations(violations, "no redundant this qualification");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoEmptyMethodBodyIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (method.Modifiers.Any(SyntaxKind.AbstractKeyword) || method.Modifiers.Any(SyntaxKind.ExternKeyword))
                    {
                        continue;
                    }

                    if (method.Parent is InterfaceDeclarationSyntax)
                    {
                        continue;
                    }

                    if (method.Body is null || method.Body.Statements.Count != 0)
                    {
                        continue;
                    }

                    if (method.ExpressionBody is not null)
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, method, $"method '{method.Identifier.ValueText}' has an empty body and requires a concrete purpose."));
                }
            }

            AssertNoViolations(violations, "no empty method bodies");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoStringConcatenationBuildsALargeTextInALoop()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (AssignmentExpressionSyntax assignment in file.Root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    if (!assignment.IsKind(SyntaxKind.AddAssignmentExpression))
                    {
                        continue;
                    }

                    if (!assignment.Right.IsKind(SyntaxKind.StringLiteralExpression) &&
                        !assignment.Right.IsKind(SyntaxKind.InterpolatedStringExpression) &&
                        !assignment.Right.IsKind(SyntaxKind.AddExpression))
                    {
                        continue;
                    }

                    if (assignment.Right is BinaryExpressionSyntax binary && !binary.IsKind(SyntaxKind.AddExpression))
                    {
                        continue;
                    }

                    bool concatenatesText = assignment.Right is LiteralExpressionSyntax literal
                        ? literal.IsKind(SyntaxKind.StringLiteralExpression)
                        : assignment.Right is InterpolatedStringExpressionSyntax ||
                          assignment.Right is BinaryExpressionSyntax addExpression &&
                          (addExpression.Left.IsKind(SyntaxKind.StringLiteralExpression) ||
                           addExpression.Right.IsKind(SyntaxKind.StringLiteralExpression) ||
                           addExpression.Left.IsKind(SyntaxKind.InterpolatedStringExpression) ||
                           addExpression.Right.IsKind(SyntaxKind.InterpolatedStringExpression));

                    if (!concatenatesText)
                    {
                        continue;
                    }

                    bool insideLoop = assignment.Ancestors().Any(ancestor => ancestor is ForStatementSyntax ||
                        ancestor is CommonForEachStatementSyntax ||
                        ancestor is WhileStatementSyntax ||
                        ancestor is DoStatementSyntax);

                    if (!insideLoop)
                    {
                        continue;
                    }

                    if (assignment.Left.ToString().Contains("Builder", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, assignment, "text built by concatenation in a loop must use a StringBuilder."));
                }
            }

            AssertNoViolations(violations, "StringBuilder for repeated text building");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoDirectEnvironmentOrClockAccessIsScatteredInLogic()
        {
            List<string> violations = [];
            string[] ambientAccessors = ["DateTime.Now", "DateTime.UtcNow", "DateTimeOffset.Now", "DateTimeOffset.UtcNow", "Environment.TickCount", "Environment.TickCount64"];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    foreach (string accessor in ambientAccessors)
                    {
                        if (file.Lines[index].Contains(accessor, StringComparison.Ordinal))
                        {
                            violations.Add($"{file.RelativePath}:{index + 1}: ambient '{accessor}' access makes logic untestable; inject a clock or timestamp.");
                        }
                    }
                }
            }

            AssertNoViolations(violations, "no ambient clock access in logic");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoUnseededRandomIsConstructed()
        {
            List<string> violations = [];
            Regex pattern = new(@"new\s+Random\s*\(\s*\)");

            foreach (SourceFile file in Files)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    if (pattern.IsMatch(file.Lines[index]))
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: an unseeded Random is nondeterministic; inject a random source for testability.");
                    }
                }
            }

            AssertNoViolations(violations, "deterministic randomness");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoConsoleOutputIsUsedForApplicationLogging()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax access)
                    {
                        continue;
                    }

                    if (access.Expression.ToString() != "Console")
                    {
                        continue;
                    }

                    if (access.Name.Identifier.ValueText is not ("WriteLine" or "Write"))
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, invocation, "application logging must use the logging abstraction, not Console output."));
                }
            }

            AssertNoViolations(violations, "abstraction-based logging");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoUnusedPrivateFieldIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                HashSet<string> usedIdentifiers = file.Root
                    .DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Select(identifier => identifier.Identifier.ValueText)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (FieldDeclarationSyntax field in file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>())
                {
                    foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                    {
                        if (!field.Modifiers.Any(SyntaxKind.PrivateKeyword))
                        {
                            continue;
                        }

                        if (!usedIdentifiers.Contains(variable.Identifier.ValueText))
                        {
                            violations.Add(SyntaxInspections.Describe(file, variable, $"private field '{variable.Identifier.ValueText}' is never used and must be removed."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "no unused private fields");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenItsBodyDoesNotExceedTheLineBudget()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (method.Body is null)
                    {
                        continue;
                    }

                    int startLine = method.Body.GetLocation().GetLineSpan().StartLinePosition.Line;
                    int endLine = method.Body.GetLocation().GetLineSpan().EndLinePosition.Line;
                    int lineCount = endLine - startLine + 1;

                    if (lineCount > 40)
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"method '{method.Identifier.ValueText}' spans {lineCount} lines; extract cohesive helper methods to keep methods around 20-30 lines."));
                    }
                }
            }

            AssertNoViolations(violations, "method length budget");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoRedundantParenthesesAreUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ParenthesizedExpressionSyntax expression in file.Root.DescendantNodes().OfType<ParenthesizedExpressionSyntax>())
                {
                    if (IsRedundantParenthesisation(expression))
                    {
                        violations.Add(SyntaxInspections.Describe(file, expression, "redundant parentheses must be removed."));
                    }
                }
            }

            AssertNoViolations(violations, "no redundant parentheses");
        }

        private static bool IsRedundantParenthesisation(ParenthesizedExpressionSyntax expression)
        {
            bool wrapsSimpleOperand = expression.Expression is IdentifierNameSyntax or LiteralExpressionSyntax or ObjectCreationExpressionSyntax;

            if (!wrapsSimpleOperand)
            {
                return false;
            }

            return expression.Parent is ArgumentSyntax or ReturnStatementSyntax or EqualsValueClauseSyntax or AssignmentExpressionSyntax;
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
