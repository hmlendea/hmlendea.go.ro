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
    public sealed class RazorComponentConventionTests
    {
        private static readonly string ComponentsDirectory = Path.Combine(
            SourceRepository.Root,
            "PersonalPresentationWebsite",
            "Components");

        [Test]
        public void GivenEveryPageComponent_WhenInspecting_ThenItDeclaresExactlyOnePageRoute()
        {
            List<string> violations = [];

            foreach (string file in RazorFiles().Where(candidate => candidate.Contains("/Pages/", StringComparison.Ordinal)))
            {
                int routeCount = File.ReadAllLines(file)
                    .Count(line => line.TrimStart().StartsWith("@page ", StringComparison.Ordinal));

                if (routeCount != 1)
                {
                    violations.Add($"{Relative(file)}: declares {routeCount} @page routes; each page declares exactly one.");
                }
            }

            AssertNoViolations(violations, "single page route");
        }

        [Test]
        public void GivenEveryPageRoute_WhenInspecting_ThenItBeginsWithASlashAndUsesLowerCaseSegments()
        {
            List<string> violations = [];
            Regex route = new("@page\\s+\"(?<route>[^\"]*)\"");

            foreach (string file in RazorFiles())
            {
                foreach (Match match in route.Matches(File.ReadAllText(file)))
                {
                    string value = match.Groups["route"].Value;

                    if (!value.StartsWith('/'))
                    {
                        violations.Add($"{Relative(file)}: route '{value}' must begin with a slash.");
                    }

                    if (value.Any(character => char.IsUpper(character)))
                    {
                        violations.Add($"{Relative(file)}: route '{value}' must use lower-case segments.");
                    }
                }
            }

            AssertNoViolations(violations, "route formatting");
        }

        [Test]
        public void GivenEveryPageComponent_WhenInspecting_ThenItDeclaresAPageTitle()
        {
            List<string> violations = [];

            foreach (string file in RazorFiles().Where(candidate => candidate.Contains("/Pages/", StringComparison.Ordinal)))
            {
                if (!File.ReadAllText(file).Contains("<PageTitle>", StringComparison.Ordinal))
                {
                    violations.Add($"{Relative(file)}: every page must declare a <PageTitle>.");
                }
            }

            AssertNoViolations(violations, "page titles");
        }

        [Test]
        public void GivenEveryRazorFile_WhenInspecting_ThenNoLegacyCodeBlockUsesTheAtCodeFormWithoutABrace()
        {
            List<string> violations = [];
            Regex malformed = new(@"@code\s*[^{\s]");

            foreach (string file in RazorFiles())
            {
                if (malformed.IsMatch(File.ReadAllText(file)))
                {
                    violations.Add($"{Relative(file)}: an '@code' directive must be followed by a brace-opened block.");
                }
            }

            AssertNoViolations(violations, "well-formed code blocks");
        }

        [Test]
        public void GivenEveryRazorCodeBlock_WhenInspecting_ThenMembersDeclareExplicitAccessibility()
        {
            List<string> violations = [];
            Regex member = new(@"^\s{4}(?!public|private|protected|internal|\[|//|@)(string|bool|int|void|Task|IEnumerable|List)\b");

            foreach (string file in RazorFiles())
            {
                string[] lines = File.ReadAllLines(file);
                int codeStart = Array.FindIndex(lines, line => line.TrimStart().StartsWith("@code", StringComparison.Ordinal));

                if (codeStart < 0)
                {
                    continue;
                }

                for (int index = codeStart + 1; index < lines.Length; index += 1)
                {
                    if (member.IsMatch(lines[index]))
                    {
                        violations.Add($"{Relative(file)}:{index + 1}: member inside '@code' must declare an explicit accessibility.");
                    }
                }
            }

            AssertNoViolations(violations, "explicit accessibility in Razor code blocks");
        }

        [Test]
        public void GivenEveryRazorOverride_WhenInspecting_ThenLifecycleMethodsKeepTheirRequiredModifiers()
        {
            List<string> violations = [];
            Regex overridden = new(@"^\s*(public|protected|private)?\s*override\s+void\s+(?<name>On[A-Za-z]+)\(");

            foreach (string file in RazorFiles())
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    Match match = overridden.Match(line);

                    if (!match.Success)
                    {
                        continue;
                    }

                    string name = match.Groups["name"].Value;

                    if (name is "OnParametersSet" or "OnInitialized" or "OnAfterRender" && !line.Contains("protected", StringComparison.Ordinal))
                    {
                        violations.Add($"{Relative(file)}: lifecycle override '{name}' must be declared protected to satisfy the base contract.");
                    }
                }
            }

            AssertNoViolations(violations, "lifecycle override modifiers");
        }

        [Test]
        public void GivenEveryRazorCascadingParameter_WhenInspecting_ThenItIsNullableWhenOptional()
        {
            List<string> violations = [];
            Regex cascading = new(@"\[CascadingParameter\][\s\S]{0,80}?(?<declaration>(public|private|protected)\s+[\w<>?]+\s+\w+\s*\{)");

            foreach (string file in RazorFiles())
            {
                foreach (Match match in cascading.Matches(File.ReadAllText(file)))
                {
                    string declaration = match.Groups["declaration"].Value;

                    if (!declaration.Contains('?'))
                    {
                        violations.Add($"{Relative(file)}: a cascading parameter that may be absent must be declared nullable.");
                    }
                }
            }

            AssertNoViolations(violations, "nullable optional cascading parameters");
        }

        [Test]
        public void GivenEveryInjectedService_WhenInspecting_ThenItIsDeclaredThroughAnInjectDirective()
        {
            List<string> violations = [];
            Regex qualified = new(@"@inject\s+(?<service>[\w\.]+)\s+(?<name>[\w]+)");

            foreach (string file in RazorFiles())
            {
                foreach (Match match in qualified.Matches(File.ReadAllText(file)))
                {
                    string service = match.Groups["service"].Value;
                    string name = match.Groups["name"].Value;

                    if (service.Contains('.', StringComparison.Ordinal))
                    {
                        violations.Add($"{Relative(file)}: injected service '{service}' must be referenced by its short name through a using directive.");
                    }

                    if (!char.IsUpper(name[0]))
                    {
                        violations.Add($"{Relative(file)}: injected service field '{name}' must begin uppercase.");
                    }
                }
            }

            AssertNoViolations(violations, "injected service formatting");
        }

        [Test]
        public void GivenEveryRazorFile_WhenInspecting_ThenItDeclaresNoInlineStyleAttribute()
        {
            List<string> violations = [];
            Regex inlineStyle = new(@"<[a-zA-Z][^>]*\sstyle\s*=\s*""[^""]+""");

            foreach (string file in RazorFiles())
            {
                if (inlineStyle.IsMatch(File.ReadAllText(file)))
                {
                    violations.Add($"{Relative(file)}: inline 'style' attributes must be replaced with a stylesheet class.");
                }
            }

            AssertNoViolations(violations, "no inline styles");
        }

        [Test]
        public void GivenEveryRazorFile_WhenInspecting_ThenItDeclaresNoInlineEventAttribute()
        {
            List<string> violations = [];
            Regex inlineHandler = new(@"\son(click|change|submit|load|mouseover)\s*=\s*""");

            foreach (string file in RazorFiles())
            {
                if (inlineHandler.IsMatch(File.ReadAllText(file)))
                {
                    violations.Add($"{Relative(file)}: inline JavaScript event handlers must be replaced with delegated listeners in a script file.");
                }
            }

            AssertNoViolations(violations, "no inline event handlers");
        }

        [Test]
        public void GivenEveryRazorFile_WhenInspecting_ThenNoHtmlIdentifierIsDuplicatedWithinTheSameFile()
        {
            List<string> violations = [];
            Regex identifier = new(@"\sid=""(?<identifier>[^""]+)""");

            foreach (string file in RazorFiles())
            {
                List<string> identifiers = identifier
                    .Matches(File.ReadAllText(file))
                    .Select(match => match.Groups["identifier"].Value)
                    .ToList();

                foreach (string duplicate in identifiers
                    .GroupBy(value => value, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key))
                {
                    violations.Add($"{Relative(file)}: id '{duplicate}' is declared more than once in the same component.");
                }
            }

            AssertNoViolations(violations, "unique element identifiers");
        }

        private static IEnumerable<string> RazorFiles()
            => Directory
                .EnumerateFiles(ComponentsDirectory, "*.razor", SearchOption.AllDirectories)
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
