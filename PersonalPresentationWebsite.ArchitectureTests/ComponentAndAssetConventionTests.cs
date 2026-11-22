using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class ComponentAndAssetConventionTests
    {
        private static readonly string WebRoot = Path.Combine(SourceRepository.Root, "PersonalPresentationWebsite", "wwwroot");

        [Test]
        public void GivenTheWebRoot_WhenInspecting_ThenRobotsAndSitemapArePresent()
        {
            List<string> missing = [];

            foreach (string file in new[] { "robots.txt", "sitemap.xml" })
            {
                if (!File.Exists(Path.Combine(WebRoot, file)))
                {
                    missing.Add(file);
                }
            }

            Assert.That(missing, Is.Empty, "The static web root must publish robots.txt and sitemap.xml.");
        }

        [Test]
        public void GivenTheSitemap_WhenInspecting_ThenItIsWellFormedXmlListingCanonicalUrls()
        {
            string path = Path.Combine(WebRoot, "sitemap.xml");

            if (!File.Exists(path))
            {
                Assert.Ignore("No sitemap.xml is present.");
            }

            System.Xml.Linq.XDocument document = System.Xml.Linq.XDocument.Load(path);
            List<string> locations = document.Descendants()
                .Where(element => element.Name.LocalName == "loc")
                .Select(element => element.Value)
                .ToList();

            Assert.That(locations, Is.Not.Empty, "The sitemap must list at least one location.");
            Assert.That(locations, Has.All.StartsWith("https://"), "Every sitemap location must use an absolute HTTPS URL.");
        }

        [Test]
        public void GivenEveryRazorComponent_WhenInspecting_ThenNoInlineCodeBlockDeclaresPublicApi()
        {
            List<string> violations = [];

            foreach (string file in RazorFiles())
            {
                string content = File.ReadAllText(file);

                if (Regex.IsMatch(content, @"@code\s*\{[^}]*public\s+[A-Za-z<>\[\]?]+\s+\w+\(", RegexOptions.Singleline))
                {
                    violations.Add($"{Relative(file)}: a Razor @code block must not declare a public API; move logic into a dedicated type.");
                }
            }

            AssertNoViolations(violations, "no public API in Razor code blocks");
        }

        [Test]
        public void GivenEveryRazorComponent_WhenInspecting_ThenItDeclaresNoEmptyCodeBlock()
        {
            List<string> violations = [];
            Regex emptyBlock = new(@"@code\s*\{\s*\}");

            foreach (string file in RazorFiles())
            {
                if (emptyBlock.IsMatch(File.ReadAllText(file)))
                {
                    violations.Add($"{Relative(file)}: an empty @code block must be removed.");
                }
            }

            AssertNoViolations(violations, "no empty Razor code blocks");
        }

        [Test]
        public void GivenEveryRazorComponent_WhenInspecting_ThenImportsAreCentralisedRatherThanDuplicated()
        {
            List<string> violations = [];

            foreach (string file in RazorFiles())
            {
                string fileName = Path.GetFileName(file);

                if (fileName == "_Imports.razor")
                {
                    continue;
                }

                int usingCount = File.ReadAllLines(file).Count(line => line.TrimStart().StartsWith("@using ", StringComparison.Ordinal));

                if (usingCount > 1)
                {
                    violations.Add($"{Relative(file)}: {usingCount} @using directives are declared; centralise them in _Imports.razor.");
                }
            }

            AssertNoViolations(violations, "centralised Razor imports");
        }

        [Test]
        public void GivenEveryCssFile_WhenInspecting_ThenItDeclaresNoImportantQualifierOveruse()
        {
            List<string> violations = [];

            foreach (string file in CssFiles())
            {
                string content = File.ReadAllText(file);
                int importantCount = Regex.Matches(content, @"!important").Count;

                if (importantCount > 3)
                {
                    violations.Add($"{Relative(file)}: uses '!important' {importantCount} times; excessive qualifier overrides indicate a specificity problem.");
                }
            }

            AssertNoViolations(violations, "CSS specificity hygiene");
        }

        [Test]
        public void GivenEveryJavaScriptFile_WhenInspecting_ThenNoConsoleLogRemains()
        {
            List<string> violations = [];
            Regex consoleCall = new(@"console\.(log|debug|info)\(");

            foreach (string file in JavaScriptFiles())
            {
                string[] lines = File.ReadAllLines(file);

                for (int index = 0; index < lines.Length; index += 1)
                {
                    if (consoleCall.IsMatch(lines[index]))
                    {
                        violations.Add($"{Relative(file)}:{index + 1}: stray console logging must not be shipped in client assets.");
                    }
                }
            }

            AssertNoViolations(violations, "no stray console logging");
        }

        [Test]
        public void GivenEveryJavaScriptFile_WhenInspecting_ThenNoStrictModeDirectiveIsDuplicated()
        {
            List<string> violations = [];

            foreach (string file in JavaScriptFiles())
            {
                string[] lines = File.ReadAllLines(file);
                int boundary = Math.Min(5, lines.Length);
                int strictCount = lines.Take(boundary).Count(line => line.Contains("\"use strict\"", StringComparison.Ordinal) || line.Contains("'use strict'", StringComparison.Ordinal));

                if (strictCount > 1)
                {
                    violations.Add($"{Relative(file)}: the 'use strict' directive is declared {strictCount} times.");
                }
            }

            AssertNoViolations(violations, "single strict mode directive");
        }

        [Test]
        public void GivenEveryRazorHeadContent_WhenInspecting_ThenTheCanonicalLinkUsesProductionHttpsUrl()
        {
            List<string> violations = [];
            Regex canonical = new(@"<link\s+rel=""canonical""\s+href=""(?<href>[^""]+)""");

            foreach (string file in RazorFiles())
            {
                foreach (Match match in canonical.Matches(File.ReadAllText(file)))
                {
                    string href = match.Groups["href"].Value;

                    if (!href.StartsWith("https://", StringComparison.Ordinal))
                    {
                        violations.Add($"{Relative(file)}: canonical link '{href}' must be an absolute HTTPS URL.");
                    }
                }
            }

            AssertNoViolations(violations, "absolute HTTPS canonical links");
        }

        private static IEnumerable<string> RazorFiles()
            => Directory
                .EnumerateFiles(Path.Combine(SourceRepository.Root, "PersonalPresentationWebsite"), "*.razor", SearchOption.AllDirectories)
                .OrderBy(file => file, StringComparer.Ordinal);

        private static IEnumerable<string> CssFiles()
            => Directory
                .EnumerateFiles(WebRoot, "*.css", SearchOption.AllDirectories)
                .OrderBy(file => file, StringComparer.Ordinal);

        private static IEnumerable<string> JavaScriptFiles()
            => Directory
                .EnumerateFiles(WebRoot, "*.js", SearchOption.AllDirectories)
                .OrderBy(file => file, StringComparer.Ordinal);

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
