using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class SecurityConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        private static readonly string[] SensitiveIdentifiers =
        [
            "password", "passphrase", "secret", "token", "apikey", "apikeyvalue",
            "credential", "privatekey", "connectionstring", "cardnumber", "pan",
            "cvv", "pin", "iban", "swift"
        ];

        [Test]
        public void GivenEveryStringInterpolation_WhenInspecting_ThenNoSensitiveIdentifierIsEmbeddedInASink()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (InterpolationSyntax interpolation in file.Root.DescendantNodes().OfType<InterpolationSyntax>())
                {
                    string expression = interpolation.Expression.ToString().ToLowerInvariant();

                    if (!SensitiveIdentifiers.Any(identifier => expression.Contains(identifier, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    if (!IsInsideLogOrMessageSink(interpolation))
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, interpolation, $"sensitive value '{interpolation.Expression}' must be masked before it reaches a log, exception message, or telemetry sink."));
                }
            }

            AssertNoViolations(violations, "sensitive data masking at logging sinks");
        }

        [Test]
        public void GivenEveryExceptionConstruction_WhenInspecting_ThenNoSensitiveValueIsPassedIntoTheMessage()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ObjectCreationExpressionSyntax creation in file.Root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                {
                    if (!creation.Type.ToString().EndsWith("Exception", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (ArgumentSyntax argument in creation.ArgumentList?.Arguments ?? [])
                    {
                        string text = argument.Expression.ToString().ToLowerInvariant();

                        if (SensitiveIdentifiers.Any(identifier => text.Contains(identifier, StringComparison.Ordinal)))
                        {
                            violations.Add(SyntaxInspections.Describe(file, argument, "a sensitive value must be masked before it is placed in an exception message."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "sensitive data exclusion from exception messages");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoLogStatementCarriesARawSensitiveIdentifier()
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

                    if (!access.Name.Identifier.ValueText.StartsWith("Log", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
                    {
                        string text = argument.Expression.ToString().ToLowerInvariant();

                        if (SensitiveIdentifiers.Any(identifier => text.Contains(identifier, StringComparison.Ordinal)))
                        {
                            violations.Add(SyntaxInspections.Describe(file, argument, "sensitive values must be registered as sensitive structured keys, not passed directly to a log call."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "sensitive log value handling");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoHardcodedCredentialLikeLiteralIsPresent()
        {
            List<string> violations = [];
            System.Text.RegularExpressions.Regex pattern = new(@"(?i)(password|secret|apikey|api_key|privatekey|token)\s*=\s*""[^""]{8,}""");

            foreach (SourceFile file in Files)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    if (pattern.IsMatch(file.Lines[index]))
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: a credential-like value must never be hardcoded; source it from configuration or a secret store.");
                    }
                }
            }

            AssertNoViolations(violations, "no hardcoded credentials");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoCultureDependentParsingIsPerformedWithoutAProvider()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    string line = file.Lines[index];

                    if (line.Contains("double.Parse(", StringComparison.Ordinal) ||
                        line.Contains("int.Parse(", StringComparison.Ordinal) ||
                        line.Contains("decimal.Parse(", StringComparison.Ordinal))
                    {
                        if (!line.Contains("CultureInfo", StringComparison.Ordinal))
                        {
                            violations.Add($"{file.RelativePath}:{index + 1}: numeric parsing must use CultureInfo.InvariantCulture or an explicit culture.");
                        }
                    }
                }
            }

            AssertNoViolations(violations, "culture-independent numeric parsing");
        }

        private static bool IsInsideLogOrMessageSink(SyntaxNode node)
        {
            foreach (SyntaxNode ancestor in node.Ancestors())
            {
                string text = ancestor switch
                {
                    InvocationExpressionSyntax invocation => invocation.Expression.ToString(),
                    _ => string.Empty
                };

                if (text.Contains("Log", StringComparison.Ordinal) ||
                    text.Contains("Message", StringComparison.Ordinal) ||
                    text.Contains("Telemetry", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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
