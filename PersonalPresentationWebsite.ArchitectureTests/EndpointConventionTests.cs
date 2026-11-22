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
    public sealed class EndpointConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.ProductionFiles;

        [Test]
        public void GivenEveryEndpointMapper_WhenInspecting_ThenItIsAStaticExtensionOnWebApplication()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => candidate.Project.Name == "PersonalPresentationWebsite"))
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (!declaration.Identifier.ValueText.EndsWith("Endpoints", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!declaration.Modifiers.Any(SyntaxKind.StaticKeyword))
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"endpoint mapper '{declaration.Identifier.ValueText}' must be static."));
                    }

                    foreach (MethodDeclarationSyntax method in declaration.Members.OfType<MethodDeclarationSyntax>())
                    {
                        if (!method.Modifiers.Any(SyntaxKind.PublicKeyword))
                        {
                            continue;
                        }

                        if (!method.Identifier.ValueText.StartsWith("Map", StringComparison.Ordinal))
                        {
                            violations.Add(SyntaxInspections.Describe(file, method, $"public endpoint method '{method.Identifier.ValueText}' must be a Map* extension."));
                        }

                        bool hasWebApplicationReceiver = method.ParameterList.Parameters.Count > 0 &&
                            method.ParameterList.Parameters[0].Modifiers.Any(SyntaxKind.ThisKeyword) &&
                            method.ParameterList.Parameters[0].Type?.ToString() == "WebApplication";

                        if (!hasWebApplicationReceiver)
                        {
                            violations.Add(SyntaxInspections.Describe(file, method, $"endpoint method '{method.Identifier.ValueText}' must extend WebApplication."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "endpoint mapper shape");
        }

        [Test]
        public void GivenEveryEndpointMapper_WhenInspecting_ThenRouteLiteralsStartWithASlash()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => candidate.Project.Name == "PersonalPresentationWebsite"))
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax access)
                    {
                        continue;
                    }

                    if (access.Name.Identifier.ValueText != "MapGet")
                    {
                        continue;
                    }

                    if (invocation.ArgumentList.Arguments.Count == 0)
                    {
                        continue;
                    }

                    ExpressionSyntax route = invocation.ArgumentList.Arguments[0].Expression;

                    if (route is LiteralExpressionSyntax literal || route is InterpolatedStringExpressionSyntax)
                    {
                        string text = route.ToString().Trim('"');

                        if (!text.StartsWith('/') && !text.Contains("/api", StringComparison.Ordinal))
                        {
                            violations.Add(SyntaxInspections.Describe(file, invocation, $"route '{text}' should be an absolute path beginning with a slash."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "absolute endpoint routes");
        }

        [Test]
        public void GivenEveryCachedEndpoint_WhenInspecting_ThenItDeclaresACachePolicy()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => candidate.Project.Name == "PersonalPresentationWebsite"))
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax access)
                    {
                        continue;
                    }

                    if (access.Name.Identifier.ValueText != "MapGet")
                    {
                        continue;
                    }

                    string chain = invocation.Parent?.ToString() ?? string.Empty;

                    if (!chain.Contains("CacheOutput", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, invocation, "each public GitHub proxy endpoint must declare a CacheOutput policy to protect the upstream rate limit."));
                    }
                }
            }

            AssertNoViolations(violations, "cached endpoint policies");
        }

        [Test]
        public void GivenEveryEndpointHandler_WhenInspecting_ThenExternalFailuresMapTo503AndMissingResourcesTo404()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => candidate.Project.Name == "PersonalPresentationWebsite"))
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    string methodText = method.ToString();

                    if (!methodText.Contains("GetAsync(", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!methodText.Contains("Status503ServiceUnavailable", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"handler '{method.Identifier.ValueText}' must map upstream failure to 503 Service Unavailable."));
                    }

                    if (!methodText.Contains("NotFound", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"handler '{method.Identifier.ValueText}' must map a missing upstream resource explicitly."));
                    }
                }
            }

            AssertNoViolations(violations, "endpoint failure semantics");
        }

        [Test]
        public void GivenEveryEndpointHandler_WhenInspecting_ThenHttpResponsesAreDisposed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => candidate.Project.Name == "PersonalPresentationWebsite"))
            {
                foreach (VariableDeclaratorSyntax variable in file.Root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
                {
                    if (variable.Initializer?.Value.ToString().Contains("GetAsync(", StringComparison.Ordinal) is true)
                    {
                        bool disposed = variable.Parent?.Parent is LocalDeclarationStatementSyntax local &&
                            local.UsingKeyword.IsKind(SyntaxKind.UsingKeyword);

                        if (!disposed)
                        {
                            violations.Add(SyntaxInspections.Describe(file, variable, $"HTTP response '{variable.Identifier.ValueText}' must be disposed with a using declaration."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "HTTP response disposal");
        }

        [Test]
        public void GivenEveryEndpointHandler_WhenInspecting_ThenJsonDocumentsAreDisposed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => candidate.Project.Name == "PersonalPresentationWebsite"))
            {
                foreach (VariableDeclaratorSyntax variable in file.Root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
                {
                    if (variable.Initializer?.Value.ToString().Contains("JsonDocument.Parse(", StringComparison.Ordinal) is true)
                    {
                        bool disposed = variable.Parent?.Parent is LocalDeclarationStatementSyntax local &&
                            local.UsingKeyword.IsKind(SyntaxKind.UsingKeyword);

                        if (!disposed)
                        {
                            violations.Add(SyntaxInspections.Describe(file, variable, $"JsonDocument '{variable.Identifier.ValueText}' must be disposed with a using declaration."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "JsonDocument disposal");
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
