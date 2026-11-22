using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PersonalPresentationWebsite.ArchitectureTests.Infrastructure
{
    internal static class SyntaxInspections
    {
        internal static string Describe(SourceFile file, SyntaxNode node, string message)
        {
            int line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            return $"{file.RelativePath}:{line}: {message}";
        }

        internal static bool DeclaresAccessibility(SyntaxTokenList modifiers)
            => modifiers.Any(SyntaxKind.PublicKeyword) ||
               modifiers.Any(SyntaxKind.PrivateKeyword) ||
               modifiers.Any(SyntaxKind.ProtectedKeyword) ||
               modifiers.Any(SyntaxKind.InternalKeyword);

        internal static bool DeclaresTypeAccessibility(SyntaxTokenList modifiers)
            => modifiers.Any(SyntaxKind.PublicKeyword) ||
               modifiers.Any(SyntaxKind.InternalKeyword);

        internal static string? MemberName(MemberDeclarationSyntax member)
            => member switch
            {
                MethodDeclarationSyntax method => method.Identifier.ValueText,
                PropertyDeclarationSyntax property => property.Identifier.ValueText,
                FieldDeclarationSyntax field => field.Declaration.Variables.Count > 0
                    ? field.Declaration.Variables[0].Identifier.ValueText
                    : null,
                ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
                EventDeclarationSyntax eventMember => eventMember.Identifier.ValueText,
                EventFieldDeclarationSyntax eventField => eventField.Declaration.Variables.Count > 0
                    ? eventField.Declaration.Variables[0].Identifier.ValueText
                    : null,
                _ => null
            };

        internal static bool IsBlockControlFlowStatement(StatementSyntax statement)
            => statement is IfStatementSyntax ||
               statement is ForStatementSyntax ||
               statement is CommonForEachStatementSyntax ||
               statement is WhileStatementSyntax ||
               statement is DoStatementSyntax ||
               statement is SwitchStatementSyntax;

        internal static bool HasBlankLineBetween(SourceFile file, int startLine, int endLine)
        {
            for (int line = startLine + 1; line < endLine; line += 1)
            {
                if (file.Lines[line].Trim().Length == 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static void RecordBracelessBody(
            SourceFile file,
            StatementSyntax body,
            string keyword,
            List<string> violations)
        {
            if (body is BlockSyntax)
            {
                return;
            }

            violations.Add(Describe(file, body, $"control-flow '{keyword}' does not use braces."));
        }

        internal static void RecordJumpSpacing(
            SourceFile file,
            BlockSyntax block,
            List<string> violations)
        {
            List<StatementSyntax> statements = [.. block.Statements];

            for (int index = 1; index < statements.Count; index += 1)
            {
                StatementSyntax current = statements[index];

                if (current is not (ReturnStatementSyntax or ThrowStatementSyntax))
                {
                    continue;
                }

                StatementSyntax previous = statements[index - 1];

                if (IsBlockControlFlowStatement(previous))
                {
                    continue;
                }

                int previousEnd = previous.GetLocation().GetLineSpan().EndLinePosition.Line;
                int currentStart = current.GetLocation().GetLineSpan().StartLinePosition.Line;

                if (!HasBlankLineBetween(file, previousEnd, currentStart))
                {
                    violations.Add($"{file.RelativePath}:{currentStart + 1}: return or throw statement is not preceded by a blank line.");
                }
            }
        }

        internal static void RecordControlFlowSpacing(
            SourceFile file,
            BlockSyntax block,
            List<string> violations)
        {
            List<StatementSyntax> statements = [.. block.Statements];

            for (int index = 0; index < statements.Count; index += 1)
            {
                StatementSyntax statement = statements[index];

                if (!IsBlockControlFlowStatement(statement))
                {
                    continue;
                }

                if (index > 0 && !IsBlockControlFlowStatement(statements[index - 1]))
                {
                    int previousEnd = statements[index - 1].GetLocation().GetLineSpan().EndLinePosition.Line;
                    int start = statement.GetLocation().GetLineSpan().StartLinePosition.Line;

                    if (!HasBlankLineBetween(file, previousEnd, start))
                    {
                        violations.Add($"{file.RelativePath}:{start + 1}: block control-flow statement has no blank line above it.");
                    }
                }

                if (index < statements.Count - 1 && !IsBlockControlFlowStatement(statements[index + 1]))
                {
                    int end = statement.GetLocation().GetLineSpan().EndLinePosition.Line;
                    int nextStart = statements[index + 1].GetLocation().GetLineSpan().StartLinePosition.Line;

                    if (!HasBlankLineBetween(file, end, nextStart))
                    {
                        violations.Add($"{file.RelativePath}:{end + 1}: block control-flow statement has no blank line below it.");
                    }
                }
            }
        }

        internal static void RecordMemberSpacing(
            SourceFile file,
            TypeDeclarationSyntax declaration,
            List<string> violations)
        {
            List<MemberDeclarationSyntax> members = [.. declaration.Members];

            for (int index = 1; index < members.Count; index += 1)
            {
                if (members[index] is FieldDeclarationSyntax || members[index - 1] is FieldDeclarationSyntax)
                {
                    continue;
                }

                int previousEnd = members[index - 1].GetLocation().GetLineSpan().EndLinePosition.Line;
                int currentStart = members[index].GetLocation().GetLineSpan().StartLinePosition.Line;

                int blankCount = 0;

                for (int line = previousEnd + 1; line < currentStart; line += 1)
                {
                    if (file.Lines[line].Trim().Length == 0)
                    {
                        blankCount += 1;
                    }
                }

                if (blankCount != 1)
                {
                    violations.Add($"{file.RelativePath}:{currentStart + 1}: member '{MemberName(members[index])}' has {blankCount} blank lines above it instead of exactly one.");
                }
            }
        }

        internal static int FieldKindRank(FieldDeclarationSyntax field)
        {
            bool isStatic = field.Modifiers.Any(SyntaxKind.StaticKeyword) ||
                field.Modifiers.Any(SyntaxKind.ConstKeyword);
            bool isReadonly = field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) ||
                field.Modifiers.Any(SyntaxKind.ConstKeyword);

            if (isStatic && isReadonly)
            {
                return 0;
            }

            if (isStatic)
            {
                return 1;
            }

            if (isReadonly)
            {
                return 2;
            }

            return 3;
        }
    }
}
