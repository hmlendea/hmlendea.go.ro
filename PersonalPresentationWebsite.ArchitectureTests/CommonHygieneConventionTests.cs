using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class CommonHygieneConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        private static readonly string[] CatchAllNamespaceSegments =
        [
            "Helpers", "Utils", "Common", "Misc", "Shared", "Extensions"
        ];

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenItEndsWithExactlyOneEmptyLine()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                byte[] bytes = File.ReadAllBytes(file.FilePath);

                if (bytes.Length == 0)
                {
                    violations.Add($"{file.RelativePath}: must end with an empty line (a trailing newline).");
                    continue;
                }

                if (bytes[^1] != (byte)'\n')
                {
                    violations.Add($"{file.RelativePath}: must end with an empty line (a trailing newline).");
                    continue;
                }

                if (bytes.Length > 1 && bytes[^2] == (byte)'\n')
                {
                    violations.Add($"{file.RelativePath}: must end with exactly one empty line, not several.");
                }
            }

            AssertNoViolations(violations, "trailing empty line");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenLineEndingsAreLfOnly()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                byte[] bytes = File.ReadAllBytes(file.FilePath);

                for (int index = 1; index < bytes.Length; index += 1)
                {
                    if (bytes[index - 1] == (byte)'\r' && bytes[index] == (byte)'\n')
                    {
                        violations.Add($"{file.RelativePath}: uses CRLF line endings; LF is required.");
                        break;
                    }
                }
            }

            AssertNoViolations(violations, "LF line endings");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenIndentationNeverUsesTabs()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                byte[] bytes = File.ReadAllBytes(file.FilePath);

                if (bytes.Any(b => b == (byte)'\t'))
                {
                    violations.Add($"{file.RelativePath}: uses tab characters; four spaces per indent level are required.");
                }
            }

            AssertNoViolations(violations, "space-based indentation");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenThereAreNoConsecutiveBlankLines()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                int consecutiveBlankLines = 0;

                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    if (file.Lines[index].Trim().Length == 0)
                    {
                        consecutiveBlankLines += 1;

                        if (consecutiveBlankLines >= 2)
                        {
                            violations.Add($"{file.RelativePath}:{index + 1}: two or more consecutive blank lines are prohibited.");
                            break;
                        }
                    }
                    else
                    {
                        consecutiveBlankLines = 0;
                    }
                }
            }

            AssertNoViolations(violations, "no consecutive blank lines");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNestedNamespaceNamesAreNotCatchAllModules()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BaseNamespaceDeclarationSyntax declaration in file.Root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
                {
                    string[] segments = declaration.Name.ToString().Split('.');

                    if (segments.Any(segment => CatchAllNamespaceSegments.Contains(segment)))
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"namespace '{declaration.Name}' contains a catch-all segment ({string.Join(", ", CatchAllNamespaceSegments)}); assign types to domain namespaces."));
                    }
                }
            }

            AssertNoViolations(violations, "no catch-all namespaces");
        }

        [Test]
        public void GivenEveryTypeDeclaration_WhenInspecting_ThenItIsNotAnEmptyPlaceholder()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BaseTypeDeclarationSyntax declaration in file.TopLevelTypeDeclarations)
                {
                    bool hasAnyMemberOrNested = declaration switch
                    {
                        ClassDeclarationSyntax classDeclaration => classDeclaration.Members.Count > 0,
                        InterfaceDeclarationSyntax interfaceDeclaration => interfaceDeclaration.Members.Count > 0,
                        StructDeclarationSyntax structDeclaration => structDeclaration.Members.Count > 0,
                        RecordDeclarationSyntax recordDeclaration => recordDeclaration.Members.Count > 0,
                        _ => true
                    };

                    if (!hasAnyMemberOrNested)
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"type '{declaration.Identifier.ValueText}' is an empty placeholder; every type requires a concrete purpose and meaningful members."));
                    }
                }
            }

            AssertNoViolations(violations, "no empty placeholder types");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenThereAreNoRedundantUsingDirectives()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                HashSet<string> referencedIdentifiers = file.Root
                    .DescendantNodes()
                    .SelectMany(node => node.DescendantTokens())
                    .Where(token => token.RawKind == (int)SyntaxKind.IdentifierToken)
                    .Select(token => token.ValueText)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (UsingDirectiveSyntax directive in file.Root.DescendantNodes().OfType<UsingDirectiveSyntax>())
                {
                    string? name = directive.Name?.ToString();

                    if (name is null)
                    {
                        continue;
                    }

                    string[] segments = name.Split('.');

                    foreach (string segment in segments)
                    {
                        if (referencedIdentifiers.Contains(segment))
                        {
                            goto nextDirective;
                        }
                    }

                    violations.Add(SyntaxInspections.Describe(file, directive, $"using directive '{name}' is redundant; manually add usings only to namespaces actually used."));
                    nextDirective:;
                }
            }

            AssertNoViolations(violations, "no redundant using directives");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenThereAreNoStatementsAfterAnUnconditionalJump()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BlockSyntax block in file.Root.DescendantNodes().OfType<BlockSyntax>())
                {
                    List<StatementSyntax> statements = [.. block.Statements];

                    for (int index = 0; index < statements.Count; index += 1)
                    {
                        StatementSyntax statement = statements[index];

                        if (!IsUnconditionalJumpStatement(statement))
                        {
                            continue;
                        }

                        if (index + 1 < statements.Count)
                        {
                            violations.Add(SyntaxInspections.Describe(file, statements[index + 1], "statements after an unconditional return, throw, continue, or break are unreachable."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "no unreachable statements after jumps");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoHardcodedPixelMeasurementsAreUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    string line = file.Lines[index];

                    if (line.Contains("px", StringComparison.OrdinalIgnoreCase))
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: layout measurements must be dynamic and relative; hardcoded pixels are prohibited.");
                    }
                }
            }

            AssertNoViolations(violations, "no hardcoded pixels");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenCommentMarkerIsFollowedByASpace()
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

                    string text = trivia.ToString();

                    if (text.Length <= 2)
                    {
                        continue;
                    }

                    char first = text[2];

                    if (first != ' ' && first != '/')
                    {
                        violations.Add($"{file.RelativePath}:{trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: place at least one space after '//' in comments.");
                    }
                }
            }

            AssertNoViolations(violations, "comment spacing");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenTodoCommentsFollowTheExactFormat()
        {
            List<string> violations = [];
            System.Text.RegularExpressions.Regex pattern = new(@"^//\s*TODO:\s*.+\.$");

            foreach (SourceFile file in Files)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    string line = file.Lines[index].TrimStart();

                    if (!line.Contains("TODO", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!pattern.IsMatch(line))
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: TODO comments must use the exact format '// TODO: Description.'.");
                    }
                }
            }

            AssertNoViolations(violations, "TODO comment format");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoUnsupportedPunctuationAppearsInComments()
        {
            List<string> violations = [];
            char[] forbidden = ['\u2013', '\u2014', '\u2026', '\u2192', '\u251C', '\u2514', '\u2500', '\u2502'];

            foreach (SourceFile file in Files)
            {
                foreach (SyntaxTrivia trivia in file.Root.DescendantTrivia())
                {
                    if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) &&
                        !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
                    {
                        continue;
                    }

                    string text = trivia.ToString();

                    if (forbidden.Any(character => text.Contains(character)))
                    {
                        violations.Add($"{file.RelativePath}:{trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: comments must use direct phrasing and ASCII punctuation, not em/en dashes, ellipsis, arrows, or box drawing characters.");
                    }
                }
            }

            AssertNoViolations(violations, "comment punctuation");
        }

        [Test]
        public void GivenEveryCatchBlock_WhenInspecting_ThenItLogsOrRethrows()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (CatchClauseSyntax catchClause in file.Root.DescendantNodes().OfType<CatchClauseSyntax>())
                {
                    string blockText = catchClause.Block.ToString();

                    bool rethrows = blockText.Contains("throw;");
                    bool logs = blockText.Contains("Log") || blockText.Contains("NuciLog");

                    if (!rethrows && !logs)
                    {
                        violations.Add(SyntaxInspections.Describe(file, catchClause, "a catch block must log the exception and rethrow it with a bare throw; otherwise the exception is swallowed."));
                    }
                }
            }

            AssertNoViolations(violations, "log-and-rethrow pattern");
        }

        private static bool IsUnconditionalJumpStatement(StatementSyntax statement)
            => statement is ReturnStatementSyntax or ThrowStatementSyntax or ContinueStatementSyntax or BreakStatementSyntax;

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
