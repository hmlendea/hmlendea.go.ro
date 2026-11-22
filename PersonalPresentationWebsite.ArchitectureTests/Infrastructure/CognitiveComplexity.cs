using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PersonalPresentationWebsite.ArchitectureTests.Infrastructure
{
    internal static class CognitiveComplexity
    {
        private static readonly HashSet<SyntaxKind> NestingKinds =
        [
            SyntaxKind.IfStatement,
            SyntaxKind.ForStatement,
            SyntaxKind.ForEachStatement,
            SyntaxKind.ForEachVariableStatement,
            SyntaxKind.WhileStatement,
            SyntaxKind.DoStatement,
            SyntaxKind.SwitchStatement,
            SyntaxKind.CatchClause,
            SyntaxKind.ConditionalExpression,
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.AnonymousMethodExpression
        ];

        private static readonly HashSet<SyntaxKind> IncrementKinds =
        [
            SyntaxKind.IfStatement,
            SyntaxKind.ForStatement,
            SyntaxKind.ForEachStatement,
            SyntaxKind.ForEachVariableStatement,
            SyntaxKind.WhileStatement,
            SyntaxKind.DoStatement,
            SyntaxKind.SwitchStatement,
            SyntaxKind.CatchClause,
            SyntaxKind.ConditionalExpression,
            SyntaxKind.CoalesceExpression,
            SyntaxKind.SwitchExpression
        ];

        internal static int Measure(SyntaxNode body)
        {
            int complexity = 0;

            foreach (SyntaxNode node in body.DescendantNodes())
            {
                if (node is ElseClauseSyntax)
                {
                    continue;
                }

                if (IncrementKinds.Contains(node.Kind()))
                {
                    complexity += 1;
                    complexity += NestingDepth(node);
                }

                if (node is BinaryExpressionSyntax binary &&
                    (binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression)))
                {
                    complexity += 1;
                }

                if (node is GotoStatementSyntax or LabeledStatementSyntax)
                {
                    complexity += 1;
                }
            }

            return complexity;
        }

        private static int NestingDepth(SyntaxNode node)
        {
            int depth = 0;
            SyntaxNode? current = node.Parent;

            while (current is not null)
            {
                if (NestingKinds.Contains(current.Kind()) && current is not SwitchSectionSyntax)
                {
                    depth += 1;
                }

                current = current.Parent;
            }

            return depth;
        }
    }
}
