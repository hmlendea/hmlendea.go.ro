using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class StaticAssetConventionTests
    {
        private static readonly string WebRoot = Path.Combine(
            SourceRepository.Root,
            "PersonalPresentationWebsite",
            "wwwroot");

        [Test]
        public void GivenTheRobotsFile_WhenInspecting_ThenItAllowsCrawlingAndDeclaresTheSitemap()
        {
            string path = Path.Combine(WebRoot, "robots.txt");

            Assert.That(File.Exists(path), Is.True, "robots.txt must exist.");

            string content = File.ReadAllText(path);

            Assert.That(content, Does.Contain("User-agent: *"), "robots.txt must declare a user-agent group.");
            Assert.That(content, Does.Contain("Sitemap: https://"), "robots.txt must declare an absolute HTTPS sitemap location.");
        }

        [Test]
        public void GivenTheSitemap_WhenInspecting_ThenEveryLocationIsUnique()
        {
            XDocument document = LoadSitemap();

            List<string> locations = document
                .Descendants()
                .Where(element => element.Name.LocalName == "loc")
                .Select(element => element.Value)
                .ToList();

            Assert.That(locations, Is.Not.Empty);
            Assert.That(locations, Is.Unique);
        }

        [Test]
        public void GivenTheSitemap_WhenInspecting_ThenEveryEntryDeclaresAValidChangeFrequencyAndPriority()
        {
            XDocument document = LoadSitemap();
            string[] validFrequencies = ["always", "hourly", "daily", "weekly", "monthly", "yearly", "never"];
            List<string> violations = [];

            foreach (XElement url in document.Descendants().Where(element => element.Name.LocalName == "url"))
            {
                XElement? changeFrequency = url.Elements().FirstOrDefault(element => element.Name.LocalName == "changefreq");
                XElement? priority = url.Elements().FirstOrDefault(element => element.Name.LocalName == "priority");

                if (changeFrequency is null || !validFrequencies.Contains(changeFrequency.Value))
                {
                    violations.Add("A sitemap entry declares an invalid or missing changefreq.");
                }

                if (priority is not null &&
                    (!double.TryParse(priority.Value, System.Globalization.CultureInfo.InvariantCulture, out double value) || value < 0.0 || value > 1.0))
                {
                    violations.Add($"A sitemap entry declares priority '{priority.Value}' outside the 0.0 to 1.0 range.");
                }
            }

            AssertNoViolations(violations, "sitemap entry metadata");
        }

        [Test]
        public void GivenEveryPublicPageRoute_WhenInspecting_ThenItAppearsInTheSitemap()
        {
            XDocument document = LoadSitemap();
            List<string> locations = document
                .Descendants()
                .Where(element => element.Name.LocalName == "loc")
                .Select(element => element.Value)
                .ToList();

            List<string> violations = [];
            Regex route = new("@page\\s+\"(?<route>[^\"]*)\"");

            foreach (string file in RazorFiles())
            {
                if (file.Contains("Error.razor", StringComparison.Ordinal) ||
                    file.Contains("NotFound.razor", StringComparison.Ordinal) ||
                    file.Contains("FundingRedirect.razor", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Match match in route.Matches(File.ReadAllText(file)))
                {
                    string value = match.Groups["route"].Value;

                    if (value is "/")
                    {
                        continue;
                    }

                    if (!locations.Any(location => location.EndsWith(value + "/", StringComparison.Ordinal)))
                    {
                        violations.Add($"{Relative(file)}: route '{value}' is not listed in the sitemap.");
                    }
                }
            }

            AssertNoViolations(violations, "sitemap coverage of public routes");
        }

        [Test]
        public void GivenEveryFundingImageReference_WhenInspecting_ThenTheAssetExists()
        {
            List<string> violations = [];
            Regex reference = new(@"\.\./assets/img/funding/(?<file>[a-z0-9\-]+\.png)");

            foreach (string file in Directory.EnumerateFiles(
                Path.Combine(SourceRepository.Root, "PersonalPresentationWebsite"),
                "*.cs",
                SearchOption.AllDirectories))
            {
                if (file.Contains("/bin/", StringComparison.Ordinal) || file.Contains("/obj/", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Match match in reference.Matches(File.ReadAllText(file)))
                {
                    string asset = Path.Combine(WebRoot, "assets", "img", "funding", match.Groups["file"].Value);

                    if (!File.Exists(asset))
                    {
                        violations.Add($"{Path.GetFileName(file)}: referenced funding image '{match.Groups["file"].Value}' does not exist.");
                    }
                }
            }

            AssertNoViolations(violations, "existing funding image assets");
        }

        [Test]
        public void GivenEveryFundingImageAsset_WhenInspecting_ThenItIsReferencedByTheCode()
        {
            List<string> violations = [];
            string fundingDirectory = Path.Combine(WebRoot, "assets", "img", "funding");

            if (!Directory.Exists(fundingDirectory))
            {
                Assert.Ignore("No funding asset directory exists.");
            }

            string codeText = string.Join(
                "\n",
                Directory
                    .EnumerateFiles(Path.Combine(SourceRepository.Root, "PersonalPresentationWebsite"), "*.cs", SearchOption.AllDirectories)
                    .Where(file => !file.Contains("/bin/", StringComparison.Ordinal) && !file.Contains("/obj/", StringComparison.Ordinal))
                    .Select(File.ReadAllText));

            bool composesImagePathsFromSlugs = codeText.Contains("assets/img/funding/", StringComparison.Ordinal) &&
                codeText.Contains("{slug}", StringComparison.Ordinal);

            foreach (string asset in Directory.EnumerateFiles(fundingDirectory, "*.png"))
            {
                string name = Path.GetFileName(asset);
                string slug = Path.GetFileNameWithoutExtension(asset);

                bool referencedDirectly = codeText.Contains(name, StringComparison.Ordinal);
                bool referencedAsSlug = codeText.Contains($"\"{slug}\"", StringComparison.Ordinal);

                if (referencedDirectly || (composesImagePathsFromSlugs && referencedAsSlug))
                {
                    continue;
                }

                violations.Add($"assets/img/funding/{name}: the asset is not referenced by any funding channel definition.");
            }

            AssertNoViolations(violations, "no orphan funding assets");
        }

        [Test]
        public void GivenTheResumeRedirect_WhenInspecting_ThenItDeclaresANonEmptyTargetThatResolvesWithinTheRepository()
        {
            string path = Path.Combine(WebRoot, "resume");

            if (!File.Exists(path))
            {
                Assert.Ignore("No resume redirect document exists.");
            }

            Match match = Regex.Match(File.ReadAllText(path), @"url=(?<target>[^\s""']+)");

            Assert.That(match.Success, Is.True, "The resume document must redirect to a target URL.");

            string target = match.Groups["target"].Value.Trim('"', '\'', '>', ';');

            if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string webRootCandidate = Path.GetFullPath(Path.Combine(WebRoot, target.TrimStart('/')));
            string repositoryCandidate = Path.GetFullPath(Path.Combine(SourceRepository.Root, target.TrimStart('/')));

            bool resolvable = File.Exists(webRootCandidate) || File.Exists(repositoryCandidate);

            Assert.That(
                resolvable,
                Is.True,
                $"The resume redirect target '{target}' does not resolve to a file within the web root or the repository.");
        }

        [Test]
        public void GivenEveryLocalStylesheetReference_WhenInspecting_ThenTheFileExists()
        {
            List<string> violations = [];
            Regex reference = new(@"href=""(?<href>/[^""]+\.css)""");

            foreach (string file in RazorFiles())
            {
                foreach (Match match in reference.Matches(File.ReadAllText(file)))
                {
                    if (!File.Exists(Path.Combine(WebRoot, match.Groups["href"].Value.TrimStart('/'))))
                    {
                        violations.Add($"{Relative(file)}: stylesheet '{match.Groups["href"].Value}' does not exist in the web root.");
                    }
                }
            }

            AssertNoViolations(violations, "existing local stylesheets");
        }

        [Test]
        public void GivenEveryLocalScriptReference_WhenInspecting_ThenTheFileExists()
        {
            List<string> violations = [];
            Regex reference = new(@"src=""(?<src>/[^""]+\.js)""");

            foreach (string file in RazorFiles())
            {
                foreach (Match match in reference.Matches(File.ReadAllText(file)))
                {
                    if (!File.Exists(Path.Combine(WebRoot, match.Groups["src"].Value.TrimStart('/'))))
                    {
                        violations.Add($"{Relative(file)}: script '{match.Groups["src"].Value}' does not exist in the web root.");
                    }
                }
            }

            AssertNoViolations(violations, "existing local scripts");
        }

        [Test]
        public void GivenEveryStaticWebFile_WhenInspecting_ThenItEndsWithANewline()
        {
            List<string> violations = [];

            foreach (string file in Directory.EnumerateFiles(WebRoot, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!File.ReadAllText(file).EndsWith('\n'))
                {
                    violations.Add($"{Relative(file)}: text-based static assets must end with a newline.");
                }
            }

            AssertNoViolations(violations, "static asset newline termination");
        }

        private static XDocument LoadSitemap()
        {
            string path = Path.Combine(WebRoot, "sitemap.xml");

            Assert.That(File.Exists(path), Is.True, "sitemap.xml must exist.");

            return XDocument.Load(path);
        }

        private static IEnumerable<string> RazorFiles()
            => Directory
                .EnumerateFiles(Path.Combine(SourceRepository.Root, "PersonalPresentationWebsite", "Components"), "*.razor", SearchOption.AllDirectories)
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
