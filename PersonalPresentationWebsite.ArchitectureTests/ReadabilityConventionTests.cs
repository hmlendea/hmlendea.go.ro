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
    public sealed class ReadabilityConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoMagicStringIsRepeatedAcrossMultipleUses()
        {
            List<string> violations = [];
            Dictionary<string, int> occurrenceCounts = new(StringComparer.Ordinal);

            foreach (SourceFile file in Files)
            {
                foreach (LiteralExpressionSyntax literal in file.Root.DescendantNodes().OfType<LiteralExpressionSyntax>())
                {
                    if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        continue;
                    }

                    string value = literal.Token.ValueText;

                    if (value.Length < 4 || value.Contains(' ') || value.StartsWith("http", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    occurrenceCounts[value] = occurrenceCounts.TryGetValue(value, out int count) ? count + 1 : 1;
                }
            }

            foreach (KeyValuePair<string, int> entry in occurrenceCounts)
            {
                if (entry.Value > 4)
                {
                    violations.Add($"The literal '{entry.Key}' is repeated {entry.Value} times and must be a named constant.");
                }
            }

            AssertNoViolations(violations, "no repeated magic strings");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoBooleanParameterIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    foreach (ParameterSyntax parameter in method.ParameterList.Parameters)
                    {
                        if (string.Equals(parameter.Type?.ToString(), "bool", StringComparison.Ordinal))
                        {
                            violations.Add(SyntaxInspections.Describe(file, parameter, $"boolean parameter '{parameter.Identifier.ValueText}' on method '{method.Identifier.ValueText}' creates a flag argument; split the method or pass an explicit options type."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "no boolean flag parameters");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoMethodDeclaresMoreThanThreeOutputParameters()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    bool passesMutableCollection = method.ParameterList.Parameters
                        .Any(parameter => parameter.Type?.ToString() is "List<FundingChannelSettings>" or "List<string>");

                    if (passesMutableCollection && method.ParameterList.Parameters.Count > 3)
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"method '{method.Identifier.ValueText}' passes a mutable collection beside {method.ParameterList.Parameters.Count - 1} other parameters; prefer returning a dedicated result."));
                    }
                }
            }

            AssertNoViolations(violations, "cohesive method outputs");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoNestedTernaryOrLongBooleanChainIsUsedInAnArgument()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ArgumentSyntax argument in file.Root.DescendantNodes().OfType<ArgumentSyntax>())
                {
                    if (argument.Expression is BinaryExpressionSyntax binary &&
                        (binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression)))
                    {
                        int clauseCount = binary.DescendantNodesAndSelf()
                            .OfType<BinaryExpressionSyntax>()
                            .Count(inner => inner.IsKind(SyntaxKind.LogicalAndExpression) || inner.IsKind(SyntaxKind.LogicalOrExpression));

                        if (clauseCount >= 3)
                        {
                            violations.Add(SyntaxInspections.Describe(file, argument, "a long boolean chain as a call argument is unreadable; extract a named helper."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "readable argument expressions");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoDeepMemberAccessChainIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (MemberAccessExpressionSyntax access in file.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
                {
                    int depth = access.DescendantNodesAndSelf()
                        .OfType<MemberAccessExpressionSyntax>()
                        .Count();

                    if (depth > 4)
                    {
                        violations.Add(SyntaxInspections.Describe(file, access, "a member access chain deeper than four steps is a Law of Demeter violation; introduce a well-named intermediate."));
                    }
                }
            }

            AssertNoViolations(violations, "shallow member access");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoVeryLongLineExceedsTheAbsoluteCeiling()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    if (file.Lines[index].Length > 180)
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: line of {file.Lines[index].Length} characters exceeds the absolute readability ceiling of 180.");
                    }
                }
            }

            AssertNoViolations(violations, "absolute line-length ceiling");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoMethodDeclaresMoreThanThreeNestedBlocks()
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

                    int maximumNesting = method.Body.DescendantNodes()
                        .OfType<StatementSyntax>()
                        .Select(statement => statement.Ancestors().OfType<BlockSyntax>().Count())
                        .DefaultIfEmpty(0)
                        .Max();

                    if (maximumNesting > 3)
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"method '{method.Identifier.ValueText}' nests blocks {maximumNesting} deep; extract helpers to flatten the control flow."));
                    }
                }
            }

            AssertNoViolations(violations, "shallow method nesting");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoSwitchStatementIsUsedInPlaceOfASwitchExpression()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (SwitchStatementSyntax statement in file.Root.DescendantNodes().OfType<SwitchStatementSyntax>())
                {
                    bool allSectionsReturnOrThrow = statement.Sections.All(section =>
                        section.Statements.Count > 0 &&
                        section.Statements[^1] is ReturnStatementSyntax or ThrowStatementSyntax or BreakStatementSyntax);

                    if (allSectionsReturnOrThrow)
                    {
                        violations.Add(SyntaxInspections.Describe(file, statement, "a switch statement whose sections all return or throw reads better as a switch expression."));
                    }
                }
            }

            AssertNoViolations(violations, "switch expression preference");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoCommentRestatesTheAdjacentCode()
        {
            List<string> violations = [];
            string[] redundantPhrases = ["// increment", "// loop", "// return", "// set the", "// get the", "// create a new"];

            foreach (SourceFile file in Files)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    string trimmed = file.Lines[index].TrimStart();
                    string lowered = trimmed.ToLowerInvariant();

                    foreach (string phrase in redundantPhrases)
                    {
                        if (lowered.StartsWith(phrase, StringComparison.Ordinal))
                        {
                            violations.Add($"{file.RelativePath}:{index + 1}: the comment restates the adjacent code; keep code self-explanatory.");
                        }
                    }
                }
            }

            AssertNoViolations(violations, "no redundant comments");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoEmptyRegionOrCommentedOutCodeRemains()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    string trimmed = file.Lines[index].TrimStart();

                    if (trimmed.StartsWith("// ", StringComparison.Ordinal) &&
                        (trimmed.EndsWith(';') || trimmed.EndsWith('}') || trimmed.EndsWith('{')))
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: commented-out code must be removed, not retained in comments.");
                    }
                }
            }

            AssertNoViolations(violations, "no commented-out code");
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
