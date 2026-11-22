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
    public sealed class DependencyInjectionConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.ProductionFiles;

        [Test]
        public void GivenTheApplicationStartup_WhenInspecting_ThenEveryServiceIsRegisteredWithAnExplicitLifetime()
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

                    if (!access.Name.Identifier.ValueText.StartsWith("Add", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string receiver = access.Expression.ToString();

                    if (!receiver.Contains("Services", StringComparison.Ordinal) &&
                        !receiver.Contains("builder.Services", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string name = access.Name.Identifier.ValueText;

                    bool declaresLifetime = name is "AddSingleton" or "AddScoped" or "AddTransient" or
                        "AddRazorComponents" or "AddOutputCache" or "AddAntiforgery" or
                        "AddHttpClient" or "AddRouting" or "AddControllers" or "AddOptions";

                    if (!declaresLifetime && !name.StartsWith("AddSingleton", StringComparison.Ordinal) &&
                        !name.StartsWith("AddScoped", StringComparison.Ordinal) &&
                        !name.StartsWith("AddTransient", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, invocation, $"service registration '{name}' must declare an explicit lifetime (Singleton, Scoped, or Transient)."));
                    }
                }
            }

            AssertNoViolations(violations, "explicit service lifetimes");
        }

        [Test]
        public void GivenEveryConfigurationBinding_WhenInspecting_ThenItUsesTheDeclaredSectionName()
        {
            List<string> violations = [];
            string[] sectionNameLiterals = ["\"gitHub\"", "\"funding\"", "\"logging\"", "\"allowedHosts\""];

            foreach (SourceFile file in Files)
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    string text = invocation.ToString();

                    foreach (string literal in sectionNameLiterals)
                    {
                        if (text.Contains($"GetSection({literal})", StringComparison.Ordinal) &&
                            !text.Contains("SectionName", StringComparison.Ordinal))
                        {
                            violations.Add(SyntaxInspections.Describe(file, invocation, $"configuration section '{literal}' must be resolved through its Settings class SectionName property."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "centralised configuration section names");
        }

        [Test]
        public void GivenEveryConfigurationStringKey_WhenInspecting_ThenItUsesAColonSeparatedPath()
        {
            List<string> violations = [];
            System.Text.RegularExpressions.Regex pattern = new(@"GetValue<[^>]+>\(\s*""(?<key>[^""]+)""");

            foreach (SourceFile file in Files)
            {
                foreach (System.Text.RegularExpressions.Match match in pattern.Matches(file.Root.ToString()))
                {
                    string key = match.Groups["key"].Value;

                    if (key.Contains('.', StringComparison.Ordinal))
                    {
                        violations.Add($"{file.RelativePath}: configuration key '{key}' uses a dot separator; configuration keys use a colon separator.");
                    }
                }
            }

            AssertNoViolations(violations, "colon-separated configuration keys");
        }

        [Test]
        public void GivenEverySingletonRegistration_WhenInspecting_ThenNoScopedServiceIsCapturedAsASingleton()
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

                    if (access.Name.Identifier.ValueText != "AddSingleton")
                    {
                        continue;
                    }

                    string text = invocation.ToString();

                    if (text.Contains("HttpContext", StringComparison.Ordinal) ||
                        text.Contains("IHttpContextAccessor", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, invocation, "a scoped HttpContext service must not be registered as a singleton."));
                    }
                }
            }

            AssertNoViolations(violations, "no captured scoped dependencies in singletons");
        }

        [Test]
        public void GivenEveryRegisteredSingleton_WhenInspecting_ThenItsImplementationIsConstructedViaAFactory()
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

                    if (!access.Name.Identifier.ValueText.StartsWith("AddSingleton", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (access.Name.Identifier.ValueText == "AddSingleton")
                    {
                        continue;
                    }

                    bool hasFactory = invocation.ArgumentList.Arguments
                        .Any(argument => argument.Expression is LambdaExpressionSyntax);

                    bool isConfigurationInstance = access.Name.Identifier.ValueText.Contains("FundingSettings", StringComparison.Ordinal) ||
                        invocation.ArgumentList.Arguments.Any(argument => argument.Expression is IdentifierNameSyntax);

                    if (!hasFactory && !isConfigurationInstance)
                    {
                        violations.Add(SyntaxInspections.Describe(file, invocation, "singleton registrations must construct their instance through an explicit factory or a bound configuration object."));
                    }
                }
            }

            AssertNoViolations(violations, "explicit singleton construction");
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
