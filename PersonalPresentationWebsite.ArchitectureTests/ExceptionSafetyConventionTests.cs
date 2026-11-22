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
    public sealed class ExceptionSafetyConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        private static readonly string[] AllowedBclExceptionTypes =
        [
            "ArgumentNullException", "ArgumentException", "ArgumentOutOfRangeException",
            "KeyNotFoundException", "AuthenticationException", "InvalidOperationException",
            "NotSupportedException", "InvalidCastException", "FormatException",
            "OverflowException", "ObjectDisposedException", "UnauthorizedAccessException",
            "IOException", "TimeoutException", "OperationCanceledException", "NotImplementedException"
        ];

        [Test]
        public void GivenEveryThrowStatement_WhenInspecting_ThenNoNonBclExceptionIsConstructed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ThrowStatementSyntax statement in file.Root.DescendantNodes().OfType<ThrowStatementSyntax>())
                {
                    if (statement.Expression is not ObjectCreationExpressionSyntax creation)
                    {
                        continue;
                    }

                    string typeName = creation.Type.ToString();

                    if (AllowedBclExceptionTypes.Contains(typeName, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    if (typeName.EndsWith("Exception", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, creation, $"exception type '{typeName}' is not a permitted BCL exception; prefer an existing BCL type."));
                    }
                }
            }

            AssertNoViolations(violations, "BCl exception usage");
        }

        [Test]
        public void GivenEveryThrowOfAnArgumentException_WhenInspecting_ThenItNamesTheOffendingParameterOrValue()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ThrowStatementSyntax statement in file.Root.DescendantNodes().OfType<ThrowStatementSyntax>())
                {
                    if (statement.Expression is not ObjectCreationExpressionSyntax creation)
                    {
                        continue;
                    }

                    string typeName = creation.Type.ToString();

                    if (typeName is not ("ArgumentException" or "ArgumentNullException" or "ArgumentOutOfRangeException"))
                    {
                        continue;
                    }

                    if (creation.ArgumentList is null || creation.ArgumentList.Arguments.Count == 0)
                    {
                        violations.Add(SyntaxInspections.Describe(file, creation, $"'{typeName}' must pass the offending parameter name and a descriptive message."));
                    }
                }
            }

            AssertNoViolations(violations, "parameterised argument exceptions");
        }

        [Test]
        public void GivenEveryUnsafeCast_WhenInspecting_ThenNoDirectCastOnAnUncertainRuntimeTypeIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (CastExpressionSyntax cast in file.Root.DescendantNodes().OfType<CastExpressionSyntax>())
                {
                    bool castsFromObject = cast.Expression is IdentifierNameSyntax identifier &&
                        identifier.Identifier.ValueText.Contains("object", StringComparison.OrdinalIgnoreCase);

                    if (castsFromObject)
                    {
                        violations.Add(SyntaxInspections.Describe(file, cast, "prefer 'as' over a direct cast when the runtime type is not guaranteed."));
                    }
                }
            }

            AssertNoViolations(violations, "safe type conversion");
        }

        [Test]
        public void GivenEveryCatchClause_WhenInspecting_ThenNoCatchWithoutAnExceptionTypeIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (CatchClauseSyntax catchClause in file.Root.DescendantNodes().OfType<CatchClauseSyntax>())
                {
                    if (catchClause.Declaration is null)
                    {
                        violations.Add(SyntaxInspections.Describe(file, catchClause, "a catch clause without an exception type swallows unexpected failures; catch a specific type."));
                    }
                }
            }

            AssertNoViolations(violations, "specific exception catching");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoExceptionIsThrownInsideAFinallyBlock()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (FinallyClauseSyntax finallyClause in file.Root.DescendantNodes().OfType<FinallyClauseSyntax>())
                {
                    foreach (ThrowStatementSyntax statement in finallyClause.Block.DescendantNodes().OfType<ThrowStatementSyntax>())
                    {
                        violations.Add(SyntaxInspections.Describe(file, statement, "an exception thrown inside a finally block masks the original exception."));
                    }
                }
            }

            AssertNoViolations(violations, "no throws inside finally");
        }

        [Test]
        public void GivenEveryExceptionFilterOrCondition_WhenInspecting_ThenNoExceptionIsCaughtAndIgnored()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (CatchClauseSyntax catchClause in file.Root.DescendantNodes().OfType<CatchClauseSyntax>())
                {
                    bool hasBody = catchClause.Block.Statements.Count > 0;
                    bool onlyComment = !hasBody && catchClause.Block.ContainsAnnotations is false;

                    if (!hasBody && catchClause.Filter is null)
                    {
                        violations.Add(SyntaxInspections.Describe(file, catchClause, "a catch clause with no body and no filter ignores the failure silently."));
                    }

                    if (onlyComment)
                    {
                        continue;
                    }
                }
            }

            AssertNoViolations(violations, "no silently ignored exceptions");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoDisposableIsConstructedWithoutUsingOrDisposal()
        {
            List<string> violations = [];
            string[] disposableTypes = ["HttpResponseMessage", "JsonDocument", "HttpClient", "StringReader", "StreamReader", "StreamWriter", "MemoryStream", "FileStream"];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (VariableDeclaratorSyntax variable in file.Root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
                {
                    if (variable.Initializer?.Value is not ObjectCreationExpressionSyntax creation)
                    {
                        continue;
                    }

                    string typeName = creation.Type.ToString();

                    if (!disposableTypes.Contains(typeName, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    bool disposed = variable.Parent?.Parent is LocalDeclarationStatementSyntax local &&
                        local.UsingKeyword.IsKind(SyntaxKind.UsingKeyword);

                    if (!disposed)
                    {
                        violations.Add(SyntaxInspections.Describe(file, variable, $"disposable '{typeName}' '{variable.Identifier.ValueText}' must be disposed with a using declaration."));
                    }
                }
            }

            AssertNoViolations(violations, "deterministic disposal");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoTaskIsFireAndForgotten()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (ExpressionStatementSyntax statement in file.Root.DescendantNodes().OfType<ExpressionStatementSyntax>())
                {
                    if (statement.Expression is not InvocationExpressionSyntax invocation)
                    {
                        continue;
                    }

                    string calledName = invocation.Expression switch
                    {
                        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
                        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                        _ => string.Empty
                    };

                    bool isTaskReturningLooking = calledName.EndsWith("Async", StringComparison.Ordinal);

                    if (isTaskReturningLooking && !statement.ToString().Contains("await", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, statement, $"call to '{calledName}' is not awaited; a fire-and-forget task hides failures."));
                    }
                }
            }

            AssertNoViolations(violations, "no fire-and-forget tasks");
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
