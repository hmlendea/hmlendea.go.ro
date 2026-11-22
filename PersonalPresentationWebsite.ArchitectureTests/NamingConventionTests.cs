using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class NamingConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        private static readonly string[] AbbreviatedNames =
        [
            "sp", "sut", "ttl", "acc", "auth", "config", "def", "dmg", "ex",
            "len", "max", "min", "str", "svc", "tex", "msg", "btn", "img",
            "val", "obj", "mgr", "cfg", "pos", "dir", "ray"
        ];

        private static readonly char[] SingleLetterNames = ['e', 'i', 'j', 'm', 'n', 't', 'x'];

        private static readonly string[] MeaningRedundantSuffixes =
        [
            "Token", "Machine", "Number", "Id", "Idd", "IdentifierName"
        ];

        [Test]
        public void GivenEveryIdentifier_WhenInspecting_ThenNoAbbreviatedNameIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (IdentifierNameSyntax identifier in file.Root.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    string name = identifier.Identifier.ValueText;

                    if (AbbreviatedNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        violations.Add(SyntaxInspections.Describe(file, identifier, $"the abbreviated name '{name}' is forbidden; write the full descriptive name."));
                    }

                    if (name.Length == 1 && SingleLetterNames.Contains(name[0]))
                    {
                        violations.Add(SyntaxInspections.Describe(file, identifier, $"single-letter name '{name}' is forbidden; write the full descriptive name."));
                    }

                    if (IsMinOrMaxName(name))
                    {
                        violations.Add(SyntaxInspections.Describe(file, identifier, $"the name '{name}' is forbidden; use Minimum or Maximum instead."));
                    }
                }
            }

            AssertNoViolations(violations, "descriptive naming");
        }

        [Test]
        public void GivenEveryIdentifier_WhenInspecting_ThenNoSuffixRepeatsMeaningAlreadyImplied()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (IdentifierNameSyntax identifier in file.Root.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    string name = identifier.Identifier.ValueText;

                    foreach (string suffix in MeaningRedundantSuffixes)
                    {
                        if (!name.EndsWith(suffix, StringComparison.Ordinal) || name.Length <= suffix.Length)
                        {
                            continue;
                        }

                        int boundaryIndex = name.Length - suffix.Length - 1;

                        if (boundaryIndex < 0 || !IsUppercaseLetter(name[boundaryIndex]))
                        {
                            continue;
                        }

                        string prefix = name.Substring(0, name.Length - suffix.Length);

                        if (prefix.Length == 0)
                        {
                            continue;
                        }

                        violations.Add(SyntaxInspections.Describe(file, identifier, $"the name '{name}' repeats the meaning implied by '{suffix}'."));
                        break;
                    }
                }
            }

            AssertNoViolations(violations, "redundant name suffixes");
        }

        [Test]
        public void GivenEveryBooleanIdentifier_WhenInspecting_ThenItsNameUsesTheIsOrHasForm()
        {
            List<string> violations = [];
            System.Text.RegularExpressions.Regex flagPattern = new(@"\b(flag|check|result)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (SourceFile file in Files)
            {
                foreach (VariableDeclaratorSyntax variable in file.Root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
                {
                    string typeName = variable.Parent switch
                    {
                        FieldDeclarationSyntax fieldDeclaration => fieldDeclaration.Declaration.Type.ToString(),
                        LocalDeclarationStatementSyntax local => local.Declaration.Type.ToString(),
                        _ => null
                    };

                    if (!string.Equals(typeName, "bool", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string name = variable.Identifier.ValueText;

                    if (name.StartsWith("Is", StringComparison.Ordinal) ||
                        name.StartsWith("Has", StringComparison.Ordinal) ||
                        name.StartsWith("Does", StringComparison.Ordinal) ||
                        name.StartsWith("Are", StringComparison.Ordinal) ||
                        name.StartsWith("Was", StringComparison.Ordinal) ||
                        name.StartsWith("Were", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (flagPattern.IsMatch(name))
                    {
                        violations.Add(SyntaxInspections.Describe(file, variable, $"boolean name '{name}' must use an Is/Has/Does/Are/Were/Were form, never flag/check/result."));
                    }
                }
            }

            AssertNoViolations(violations, "boolean naming");
        }

        [Test]
        public void GivenEveryLocalOrLambdaName_WhenInspecting_ThenTheNameIsDescriptive()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (VariableDeclaratorSyntax variable in file.Root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
                {
                    string name = variable.Identifier.ValueText;

                    if (AbbreviatedNames.Contains(name, StringComparer.OrdinalIgnoreCase) ||
                        (name.Length == 1 && SingleLetterNames.Contains(name[0])) ||
                        IsMinOrMaxName(name))
                    {
                        violations.Add(SyntaxInspections.Describe(file, variable, $"local variable '{name}' is not a clear, descriptive name."));
                    }
                }

                foreach (SimpleLambdaExpressionSyntax lambda in file.Root.DescendantNodes().OfType<SimpleLambdaExpressionSyntax>())
                {
                    string name = lambda.Parameter.Identifier.ValueText;

                    if (AbbreviatedNames.Contains(name, StringComparer.OrdinalIgnoreCase) ||
                        SingleLetterNames.Contains(name[0]) ||
                        IsMinOrMaxName(name))
                    {
                        violations.Add(SyntaxInspections.Describe(file, lambda, $"lambda parameter '{name}' is not a clear, descriptive name."));
                    }
                }
            }

            AssertNoViolations(violations, "descriptive local and lambda names");
        }

        private static bool IsMinOrMaxName(string name)
        {
            return string.Equals(name, "Min", StringComparison.Ordinal) ||
                   string.Equals(name, "Max", StringComparison.Ordinal) ||
                   name.EndsWith("Min", StringComparison.Ordinal) &&
                   name.Length > 3 &&
                   IsUppercaseLetter(name[name.Length - 4]) ||
                   name.EndsWith("Max", StringComparison.Ordinal) &&
                   name.Length > 3 &&
                   IsUppercaseLetter(name[name.Length - 4]);
        }

        private static bool IsUppercaseLetter(char c)
            => c >= 'A' && c <= 'Z';

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
