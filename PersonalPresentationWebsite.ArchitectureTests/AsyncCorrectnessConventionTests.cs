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
    public sealed class AsyncCorrectnessConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        [Test]
        public void GivenEveryAsyncMethod_WhenInspecting_ThenItReturnsATaskOfSomeResultOrTask()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword))
                    {
                        continue;
                    }

                    string returnType = method.ReturnType.ToString();

                    if (returnType is not ("Task" or "void") && !returnType.StartsWith("Task<", StringComparison.Ordinal) &&
                        !returnType.StartsWith("ValueTask", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"async method '{method.Identifier.ValueText}' returns '{returnType}'; asynchronous methods return Task or Task<T>."));
                    }
                }
            }

            AssertNoViolations(violations, "task-returning async methods");
        }

        [Test]
        public void GivenEveryAsyncMethod_WhenInspecting_ThenNoTaskDotResultIsAccessed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (MemberAccessExpressionSyntax access in file.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
                {
                    if (access.Name.Identifier.ValueText != "Result")
                    {
                        continue;
                    }

                    bool looksLikeTask = access.Expression.ToString().EndsWith("Async", StringComparison.Ordinal) ||
                        access.Expression is InvocationExpressionSyntax;

                    if (looksLikeTask)
                    {
                        violations.Add(SyntaxInspections.Describe(file, access, "accessing '.Result' blocks the thread and risks deadlock; await the task."));
                    }
                }
            }

            AssertNoViolations(violations, "no blocking Result access");
        }

        [Test]
        public void GivenEveryAsyncMethod_WhenInspecting_ThenNoTaskDotWaitOrDotGetAwaiterIsCalled()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax access)
                    {
                        continue;
                    }

                    if (access.Name.Identifier.ValueText is "Wait" or "GetAwaiter")
                    {
                        violations.Add(SyntaxInspections.Describe(file, invocation, $"calling '{access.Name.Identifier.ValueText}' blocks; await the task instead."));
                    }
                }
            }

            AssertNoViolations(violations, "no blocking waits");
        }

        [Test]
        public void GivenEveryAsyncMethod_WhenInspecting_ThenNoRedundantTaskDotFromResultIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax access)
                    {
                        continue;
                    }

                    if (access.Expression.ToString() != "Task")
                    {
                        continue;
                    }

                    if (access.Name.Identifier.ValueText != "FromResult")
                    {
                        continue;
                    }

                    bool insideAsyncMethod = invocation.Ancestors()
                        .OfType<MethodDeclarationSyntax>()
                        .Any(method => method.Modifiers.Any(SyntaxKind.AsyncKeyword));

                    if (insideAsyncMethod)
                    {
                        violations.Add(SyntaxInspections.Describe(file, invocation, "Task.FromResult inside an asynchronous method is redundant; return the value directly."));
                    }
                }
            }

            AssertNoViolations(violations, "no redundant Task.FromResult");
        }

        [Test]
        public void GivenEveryAsyncMethod_WhenInspecting_ThenNoAsyncVoidIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (method.Modifiers.Any(SyntaxKind.AsyncKeyword) &&
                        string.Equals(method.ReturnType.ToString(), "void", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"async void method '{method.Identifier.ValueText}' cannot be awaited and cannot propagate exceptions."));
                    }
                }
            }

            AssertNoViolations(violations, "no async void methods");
        }

        [Test]
        public void GivenEveryAwaitExpression_WhenInspecting_ThenItIsNotAppliedToANonTaskValue()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (AwaitExpressionSyntax awaitExpression in file.Root.DescendantNodes().OfType<AwaitExpressionSyntax>())
                {
                    string awaited = awaitExpression.Expression.ToString();

                    if (awaited.StartsWith("Task.Delay", StringComparison.Ordinal) ||
                        awaited.StartsWith("Task.WhenAll", StringComparison.Ordinal) ||
                        awaited.StartsWith("Task.WhenAny", StringComparison.Ordinal) ||
                        awaited.Contains("Async(", StringComparison.Ordinal) ||
                        awaited.Contains("GetAsync", StringComparison.Ordinal) ||
                        awaited.Contains("ReadAsStringAsync", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    bool looksLikeTask = awaited.EndsWith("Async()", StringComparison.Ordinal) ||
                        awaited.Contains("Task.", StringComparison.Ordinal);

                    if (!looksLikeTask)
                    {
                        violations.Add(SyntaxInspections.Describe(file, awaitExpression, $"await is applied to '{awaited}', which does not clearly represent an asynchronous operation."));
                    }
                }
            }

            AssertNoViolations(violations, "awaiting asynchronous operations");
        }

        [Test]
        public void GivenEveryUsingDeclaration_WhenInspecting_ThenNoDisposableIsAwaitableUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (VariableDeclaratorSyntax variable in file.Root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
                {
                    string? initializer = variable.Initializer?.Value.ToString();

                    if (initializer is null)
                    {
                        continue;
                    }

                    if (initializer.Contains("await using", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, variable, "awaited async disposal of a synchronous disposable is unnecessary in this codebase."));
                    }
                }
            }

            AssertNoViolations(violations, "no unnecessary async disposal");
        }

        [Test]
        public void GivenEveryMethodSignature_WhenInspecting_ThenCancellationTokenIsNotDeclaredWithoutAnAwaitableBody()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    bool declaresToken = method.ParameterList.Parameters
                        .Any(parameter => string.Equals(parameter.Type?.ToString(), "CancellationToken", StringComparison.Ordinal));

                    if (!declaresToken)
                    {
                        continue;
                    }

                    string returnType = method.ReturnType.ToString();

                    if (!returnType.StartsWith("Task", StringComparison.Ordinal) && !returnType.StartsWith("ValueTask", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"method '{method.Identifier.ValueText}' accepts a CancellationToken but is not asynchronous."));
                    }
                }
            }

            AssertNoViolations(violations, "cancellation on asynchronous methods only");
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
