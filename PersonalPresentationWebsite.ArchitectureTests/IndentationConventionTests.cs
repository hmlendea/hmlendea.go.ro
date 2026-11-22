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
    public sealed class IndentationConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        [Test]
        public void GivenEveryMultiClauseCondition_WhenInspecting_ThenClausesAreSplitOnePerLineWhenExceedingTheThreshold()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (IfStatementSyntax statement in file.Root.DescendantNodes().OfType<IfStatementSyntax>())
                {
                    if (statement.Condition is not BinaryExpressionSyntax condition)
                    {
                        continue;
                    }

                    List<ExpressionSyntax> clauses = FlattenCondition(condition);

                    if (clauses.Count < 2)
                    {
                        continue;
                    }

                    int startLine = statement.Condition.GetLocation().GetLineSpan().StartLinePosition.Line;
                    int endLine = statement.Condition.GetLocation().GetLineSpan().EndLinePosition.Line;
                    string line = file.Lines[startLine];

                    int conditionLength = line.Length - line.IndexOf("if", StringComparison.Ordinal);

                    bool splitPerClause = clauses.All(clause => file.Lines[clause.GetLocation().GetLineSpan().StartLinePosition.Line].Trim().Length > 0);

                    if (conditionLength > 72 && (endLine - startLine) < clauses.Count - 1)
                    {
                        violations.Add($"{file.RelativePath}:{startLine + 1}: a multi-clause condition exceeding 72 characters must place one clause per line.");
                    }

                    if (splitPerClause && conditionLength <= 72 && (endLine - startLine) > 0)
                    {
                        violations.Add($"{file.RelativePath}:{startLine + 1}: a condition under the threshold must not be split across lines.");
                    }
                }
            }

            AssertNoViolations(violations, "conditional line splitting");
        }

        [Test]
        public void GivenEveryProperty_WhenInspecting_ThenAccessorBodiesOccupyTheirOwnLines()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (PropertyDeclarationSyntax property in file.Root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
                {
                    if (property.AccessorList is null)
                    {
                        continue;
                    }

                    foreach (AccessorDeclarationSyntax accessor in property.AccessorList.Accessors)
                    {
                        if (accessor.Body is null)
                        {
                            continue;
                        }

                        int accessorLine = accessor.GetLocation().GetLineSpan().StartLinePosition.Line;
                        int bodyStartLine = accessor.Body.GetLocation().GetLineSpan().StartLinePosition.Line;

                        if (accessorLine == bodyStartLine)
                        {
                            violations.Add(SyntaxInspections.Describe(file, accessor, $"accessor in property '{property.Identifier.ValueText}' must place its body on its own line."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "accessor body layout");
        }

        [Test]
        public void GivenEveryProperty_WhenInspecting_ThenItIsSeparatedFromAdjacentMembersByABlankLine()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (TypeDeclarationSyntax declaration in file.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    List<MemberDeclarationSyntax> members = [.. declaration.Members];

                    for (int index = 0; index < members.Count; index += 1)
                    {
                        if (members[index] is not PropertyDeclarationSyntax property)
                        {
                            continue;
                        }

                        if (index > 0 && !HasBlankLineAbove(file, property))
                        {
                            violations.Add(SyntaxInspections.Describe(file, property, $"property '{property.Identifier.ValueText}' must be separated from the preceding member by a blank line."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "property separation");
        }

        [Test]
        public void GivenEveryStatement_WhenInspecting_ThenIndentationIsAMultipleOfFourSpaces()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    string line = file.Lines[index];

                    if (line.Trim().Length == 0)
                    {
                        continue;
                    }

                    int indentation = 0;

                    while (indentation < line.Length && line[indentation] == ' ')
                    {
                        indentation += 1;
                    }

                    if (indentation % 4 != 0)
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: indentation of {indentation} spaces is not a multiple of four.");
                    }
                }
            }

            AssertNoViolations(violations, "four-space indentation");
        }

        [Test]
        public void GivenEveryOperator_WhenInspecting_ThenItTerminatesItsLineRatherThanStartingTheNext()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                for (int index = 1; index < file.Lines.Count; index += 1)
                {
                    string trimmed = file.Lines[index].TrimStart();

                    if (trimmed.StartsWith("&&", StringComparison.Ordinal) ||
                        trimmed.StartsWith("||", StringComparison.Ordinal))
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: logical operators must be placed at the end of the previous line.");
                    }
                }
            }

            AssertNoViolations(violations, "operator line placement");
        }

        [Test]
        public void GivenEveryLongParameterList_WhenInspecting_ThenEachParameterOccupiesItsOwnLine()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BaseMethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
                {
                    if (method.ParameterList is null || method.ParameterList.Parameters.Count < 2)
                    {
                        continue;
                    }

                    int signatureLine = method.GetLocation().GetLineSpan().StartLinePosition.Line;
                    string line = file.Lines[signatureLine];

                    int signatureLength = line.TrimStart().Length;

                    if (signatureLength <= 96)
                    {
                        continue;
                    }

                    bool allOnOwnLines = method.ParameterList.Parameters
                        .All(parameter => parameter.GetLocation().GetLineSpan().StartLinePosition.Line > signatureLine);

                    if (!allOnOwnLines)
                    {
                        violations.Add($"{file.RelativePath}:{signatureLine + 1}: a signature exceeding 96 characters must place every parameter on its own line.");
                    }
                }
            }

            AssertNoViolations(violations, "parameter list line splitting");
        }

        private static List<ExpressionSyntax> FlattenCondition(BinaryExpressionSyntax condition)
        {
            List<ExpressionSyntax> clauses = [];

            if (condition.Left is BinaryExpressionSyntax leftBinary &&
                (leftBinary.IsKind(SyntaxKind.LogicalAndExpression) || leftBinary.IsKind(SyntaxKind.LogicalOrExpression)))
            {
                clauses.AddRange(FlattenCondition(leftBinary));
            }
            else
            {
                clauses.Add(condition.Left);
            }

            if (condition.Right is BinaryExpressionSyntax rightBinary &&
                (rightBinary.IsKind(SyntaxKind.LogicalAndExpression) || rightBinary.IsKind(SyntaxKind.LogicalOrExpression)))
            {
                clauses.AddRange(FlattenCondition(rightBinary));
            }
            else
            {
                clauses.Add(condition.Right);
            }

            return clauses;
        }

        private static bool HasBlankLineAbove(SourceFile file, MemberDeclarationSyntax member)
        {
            int startLine = member.GetLocation().GetLineSpan().StartLinePosition.Line;

            if (startLine == 0)
            {
                return true;
            }

            return file.Lines[startLine - 1].Trim().Length == 0;
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
