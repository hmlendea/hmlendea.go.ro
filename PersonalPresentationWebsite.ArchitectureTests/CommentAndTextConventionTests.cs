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
    public sealed class CommentAndTextConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        [Test]
        public void GivenEveryComment_WhenInspecting_ThenInlineCommentsBeginUppercaseAndEndWithAPeriod()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (SyntaxTrivia trivia in file.Root.DescendantTrivia())
                {
                    if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
                    {
                        continue;
                    }

                    string text = trivia.ToString().TrimStart('/', ' ').TrimEnd();

                    if (text.Length == 0)
                    {
                        continue;
                    }

                    if (IsDirectiveComment(text))
                    {
                        continue;
                    }

                    if (!char.IsUpper(text[0]))
                    {
                        violations.Add($"{file.RelativePath}:{trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: comments must begin with an uppercase letter.");
                    }

                    if (!text.EndsWith('.') && !text.EndsWith(':') && !text.EndsWith(')'))
                    {
                        violations.Add($"{file.RelativePath}:{trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: comments must end with a period.");
                    }
                }
            }

            AssertNoViolations(violations, "comment capitalisation and punctuation");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoCommentBannerSeparatesMethodGroups()
        {
            List<string> violations = [];
            string[] bannerMarkers = ["// ---", "// ====", "// ====", "/* ----"];

            foreach (SourceFile file in Files)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    foreach (string banner in bannerMarkers)
                    {
                        if (file.Lines[index].TrimStart().StartsWith(banner, StringComparison.Ordinal))
                        {
                            violations.Add($"{file.RelativePath}:{index + 1}: comment banners grouping methods are prohibited; group by production method without banner comments.");
                        }
                    }
                }
            }

            AssertNoViolations(violations, "no comment banners");
        }

        [Test]
        public void GivenEveryTestMethod_WhenInspecting_ThenNoCommentBannerGroupsItsCases()
        {
            List<string> violations = [];

            foreach (SourceFile file in SourceWorkspace.TestFiles)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    string line = file.Lines[index].TrimStart();

                    if (line.StartsWith("// Arrange", StringComparison.Ordinal) ||
                        line.StartsWith("// Act", StringComparison.Ordinal) ||
                        line.StartsWith("// Assert", StringComparison.Ordinal))
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: Arrange/Act/Assert comments are prohibited; the test name and structure must convey the phases.");
                    }
                }
            }

            AssertNoViolations(violations, "no Arrange/Act/Assert comment banners");
        }

        [Test]
        public void GivenEveryComment_WhenInspecting_ThenNoAbbreviatedOrPlaceholderTextRemains()
        {
            List<string> violations = [];
            string[] placeholderMarkers = ["TODO", "FIXME", "HACK", "XXX", "WIP"];

            foreach (SourceFile file in Files)
            {
                foreach (SyntaxTrivia trivia in file.Root.DescendantTrivia())
                {
                    if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
                    {
                        continue;
                    }

                    string text = trivia.ToString();

                    foreach (string marker in placeholderMarkers)
                    {
                        if (marker == "TODO")
                        {
                            continue;
                        }

                        if (text.Contains(marker, StringComparison.Ordinal))
                        {
                            violations.Add($"{file.RelativePath}:{trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: placeholder comment marker '{marker}' must not remain in delivered code.");
                        }
                    }
                }
            }

            AssertNoViolations(violations, "no placeholder comment markers");
        }

        [Test]
        public void GivenEveryStringLiteral_WhenInspecting_ThenNoPlaceholderTextIsUsedInUserFacingStrings()
        {
            List<string> violations = [];
            string[] placeholders = ["lorem", "ipsum", "foo", "bar", "baz", "asdf", "test123", "changeme", "placeholder"];

            foreach (SourceFile file in Files)
            {
                foreach (LiteralExpressionSyntax literal in file.Root.DescendantNodes().OfType<LiteralExpressionSyntax>())
                {
                    if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        continue;
                    }

                    string value = literal.Token.ValueText.ToLowerInvariant();

                    foreach (string placeholder in placeholders)
                    {
                        if (value.Contains(placeholder, StringComparison.Ordinal))
                        {
                            violations.Add(SyntaxInspections.Describe(file, literal, $"string literal contains the placeholder text '{placeholder}'."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "no placeholder user strings");
        }

        [Test]
        public void GivenEveryUiString_WhenInspecting_ThenNoAllLowercaseSentenceIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (LiteralExpressionSyntax literal in file.Root.DescendantNodes().OfType<LiteralExpressionSyntax>())
                {
                    if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        continue;
                    }

                    string value = literal.Token.ValueText;

                    if (value.Length < 12 || !value.Contains(' '))
                    {
                        continue;
                    }

                    bool looksLikeSentence = value.Contains('.') || value.Contains('!') || value.Contains('?');

                    if (!looksLikeSentence)
                    {
                        continue;
                    }

                    if (char.IsLower(value[0]))
                    {
                        violations.Add(SyntaxInspections.Describe(file, literal, "a complete sentence must begin with an uppercase letter."));
                    }
                }
            }

            AssertNoViolations(violations, "complete grammar in user strings");
        }

        private static bool IsDirectiveComment(string text)
        {
            return text.StartsWith("TODO", StringComparison.Ordinal) ||
                text.StartsWith("pragma", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("region", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("end", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
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
