using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class RepositoryDocumentationConventionTests
    {
        [Test]
        public void GivenTheRepository_WhenInspecting_ThenTheExpectedTopLevelDocumentsExist()
        {
            List<string> missing = [];

            foreach (string document in new[] { "README.md", "LICENSE" })
            {
                if (!File.Exists(Path.Combine(SourceRepository.Root, document)))
                {
                    missing.Add(document);
                }
            }

            Assert.That(missing, Is.Empty, "The repository must contain a README and a LICENSE.");
        }

        [Test]
        public void GivenEveryRepositoryMarkdownFile_WhenInspecting_ThenNoHtmlCommentRemains()
        {
            List<string> violations = [];
            Regex htmlComment = new(@"<!--.*?-->", RegexOptions.Singleline);

            foreach (string file in MarkdownFiles())
            {
                string content = File.ReadAllText(file);

                if (htmlComment.IsMatch(content))
                {
                    violations.Add($"{Relative(file)}: contains an HTML comment; address the text it carries and remove the comment.");
                }
            }

            AssertNoViolations(violations, "no HTML comments in repository Markdown");
        }

        [Test]
        public void GivenEveryRepositoryMarkdownLink_WhenInspecting_ThenEachRepositoryRelativeTargetExists()
        {
            List<string> violations = [];
            Regex linkPattern = new(@"\]\((?<target>[^)]+)\)");

            foreach (string file in MarkdownFiles())
            {
                string content = File.ReadAllText(file);

                foreach (Match match in linkPattern.Matches(content))
                {
                    string target = match.Groups["target"].Value.Trim();

                    if (ShouldSkipLink(target))
                    {
                        continue;
                    }

                    string pathPart = target.Split('#')[0];

                    if (pathPart.Length == 0)
                    {
                        continue;
                    }

                    string resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, pathPart));

                    if (!File.Exists(resolved) && !Directory.Exists(resolved))
                    {
                        violations.Add($"{Relative(file)}: link target '{target}' does not exist.");
                    }
                }
            }

            AssertNoViolations(violations, "verified repository-relative links");
        }

        [Test]
        public void GivenEveryRepositoryMarkdownFile_WhenInspecting_ThenHeadingsUseTitleCase()
        {
            List<string> violations = [];
            Regex headingPattern = new(@"^#{1,6}\s+(?<text>.+?)\s*$");
            string[] minorWords = ["a", "an", "the", "and", "or", "of", "to", "in", "on", "for", "with", "at", "by", "as", "is"];

            foreach (string file in MarkdownFiles())
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    Match match = headingPattern.Match(line);

                    if (!match.Success)
                    {
                        continue;
                    }

                    string text = match.Groups["text"].Value.Trim();
                    string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    for (int index = 0; index < words.Length; index += 1)
                    {
                        string word = words[index];
                        string bare = new(word.Where(char.IsLetter).ToArray());

                        if (bare.Length == 0)
                        {
                            continue;
                        }

                        bool isFirstOrLast = index == 0 || index == words.Length - 1;
                        bool isMinor = minorWords.Contains(bare, StringComparer.OrdinalIgnoreCase);
                        bool startsUpper = char.IsUpper(bare[0]);

                        if (!startsUpper && !(isMinor && !isFirstOrLast))
                        {
                            violations.Add($"{Relative(file)}: heading '{text}' is not in title case.");
                            break;
                        }
                    }
                }
            }

            AssertNoViolations(violations, "title case headings");
        }

        [Test]
        public void GivenEveryRepositoryMarkdownFile_WhenInspecting_ThenNoBlankLineSeparatesAColonIntroductionFromItsList()
        {
            List<string> violations = [];

            foreach (string file in MarkdownFiles())
            {
                string[] lines = File.ReadAllLines(file);

                for (int index = 0; index < lines.Length - 1; index += 1)
                {
                    string current = lines[index].TrimEnd();

                    if (!current.EndsWith(':'))
                    {
                        continue;
                    }

                    if (lines[index + 1].Trim().Length != 0)
                    {
                        continue;
                    }

                    int next = index + 2;

                    if (next < lines.Length && IsListItem(lines[next]))
                    {
                        violations.Add($"{Relative(file)}:{index + 1}: a blank line separates a colon introduction from its list; the first item must immediately follow.");
                    }
                }
            }

            AssertNoViolations(violations, "colon introduction follows directly by its list");
        }

        [Test]
        public void GivenEveryRepositoryMarkdownFile_WhenInspecting_ThenIndentationUsesNoTabs()
        {
            List<string> violations = [];

            foreach (string file in MarkdownFiles())
            {
                if (File.ReadAllText(file).Contains('\t'))
                {
                    violations.Add($"{Relative(file)}: contains tab characters; two spaces per indentation level are required.");
                }
            }

            AssertNoViolations(violations, "space-based Markdown indentation");
        }

        [Test]
        public void GivenTheReadme_WhenInspecting_ThenItsTableOfContentsAnchorsResolveWithinTheDocument()
        {
            List<string> violations = [];
            string readmePath = Path.Combine(SourceRepository.Root, "README.md");

            if (!File.Exists(readmePath))
            {
                Assert.Ignore("No README is present at the repository root.");
            }

            string[] lines = File.ReadAllLines(readmePath);
            HashSet<string> anchors = [];

            foreach (string line in lines)
            {
                if (!line.StartsWith('#'))
                {
                    continue;
                }

                string heading = line.TrimStart('#').Trim();
                anchors.Add(ToAnchor(heading));
            }

            foreach (Match match in Regex.Matches(File.ReadAllText(readmePath), @"\]\(#(?<anchor>[^)]+)\)"))
            {
                string anchor = match.Groups["anchor"].Value;

                if (!anchors.Contains(anchor))
                {
                    violations.Add($"README.md: anchor '#{anchor}' has no matching heading in the document.");
                }
            }

            AssertNoViolations(violations, "resolving table-of-contents anchors");
        }

        private static string ToAnchor(string heading)
        {
            string lowered = heading.ToLowerInvariant();
            string cleaned = new(lowered.Where(character => char.IsLetterOrDigit(character) || character is ' ' or '-' or '_').ToArray());

            return cleaned.Replace(' ', '-');
        }

        private static bool ShouldSkipLink(string target)
        {
            return target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                   target.StartsWith('#');
        }

        private static bool IsListItem(string line)
        {
            string trimmed = line.TrimStart();
            return trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ ") || Regex.IsMatch(trimmed, @"^\d+\.\s");
        }

        private static IEnumerable<string> MarkdownFiles()
        {
            return Directory
                .EnumerateFiles(SourceRepository.Root, "*.md", SearchOption.AllDirectories)
                .Where(file => !file.Contains("/bin/", StringComparison.Ordinal) &&
                    !file.Contains("/obj/", StringComparison.Ordinal) &&
                    !file.Contains("/node_modules/", StringComparison.Ordinal))
                .OrderBy(file => file, StringComparer.Ordinal);
        }

        private static string Relative(string file)
            => Path.GetRelativePath(SourceRepository.Root, file).Replace('\\', '/');

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
