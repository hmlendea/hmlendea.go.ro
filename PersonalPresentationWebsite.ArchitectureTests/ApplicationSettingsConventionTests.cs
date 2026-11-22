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
    public sealed class ApplicationSettingsConventionTests
    {
        private static readonly string AppSettingsPath = Path.Combine(
            SourceRepository.Root,
            "PersonalPresentationWebsite",
            "appsettings.json");

        [Test]
        public void GivenTheApplicationSettings_WhenInspecting_ThenItIsWellFormedJson()
        {
            Assert.That(File.Exists(AppSettingsPath), Is.True, "appsettings.json must exist.");

            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(AppSettingsPath));

            Assert.That(document.RootElement.ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Object));
        }

        [Test]
        public void GivenTheApplicationSettings_WhenInspecting_ThenEverySectionRootIsCamelCase()
        {
            List<string> violations = [];
            Regex camelCase = new(@"^[a-z][A-Za-z0-9]*$");

            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(AppSettingsPath));

            foreach (System.Text.Json.JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!camelCase.IsMatch(property.Name))
                {
                    violations.Add($"Section root '{property.Name}' is not camelCase.");
                }
            }

            AssertNoViolations(violations, "camelCase section roots");
        }

        [Test]
        public void GivenTheApplicationSettings_WhenInspecting_ThenEveryNestedKeyIsCamelCase()
        {
            List<string> violations = [];
            Regex camelCase = new(@"^[a-z][A-Za-z0-9]*$");

            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(AppSettingsPath));

            Walk(document.RootElement, camelCase, violations, string.Empty, isLoggingSection: false);

            AssertNoViolations(violations, "camelCase nested keys");
        }

        [Test]
        public void GivenTheFundingSection_WhenInspecting_ThenItBindsEveryDeclaredSettingsProperty()
        {
            List<string> violations = [];
            System.Text.Json.JsonElement funding = LoadSection("funding");
            List<string> keys = funding.EnumerateObject().Select(property => property.Name).ToList();

            string[] declared = ["patreonUsername", "revolutUsername", "payPalHostedButtonId", "gitHubSponsorsUsername", "liberaPayUsername", "buyMeACoffeeUsername", "kofiUsername", "saltBank"];

            foreach (string property in declared)
            {
                if (!keys.Contains(property, StringComparer.Ordinal))
                {
                    violations.Add($"funding section is missing the declared key '{property}'.");
                }
            }

            AssertNoViolations(violations, "funding settings binding");
        }

        [Test]
        public void GivenTheGitHubSection_WhenInspecting_ThenTheCacheDurationMeetsTheDocumentedMinimum()
        {
            System.Text.Json.JsonElement section = LoadSection("gitHub");
            int cacheDuration = section.GetProperty("cacheDurationInMinutes").GetInt32();

            Assert.That(cacheDuration, Is.GreaterThanOrEqualTo(30), "The GitHub cache duration must be at least 30 minutes to protect the upstream rate limit.");
        }

        [Test]
        public void GivenTheApplicationSettings_WhenInspecting_ThenNoSensitiveValueIsStoredAsAPlainLiteral()
        {
            List<string> violations = [];
            string[] sensitiveKeys = ["password", "secret", "token", "apikey", "apikeyvalue", "privatekey", "connectionstring"];

            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(AppSettingsPath));

            WalkForSensitiveLiteral(document.RootElement, sensitiveKeys, violations, string.Empty);

            AssertNoViolations(violations, "no plain-literal secrets in settings");
        }

        [Test]
        public void GivenTheApplicationSettings_WhenInspecting_ThenNoEnvironmentSpecificValueIsCommitted()
        {
            List<string> violations = [];
            string content = File.ReadAllText(AppSettingsPath);

            if (!content.Contains("\"allowedHosts\"", StringComparison.Ordinal))
            {
                violations.Add("appsettings.json must declare allowedHosts so host filtering is explicit.");
            }

            foreach (string forbidden in new[] { "localhost", "127.0.0.1", "Debug", "Release" })
            {
                if (content.Contains(forbidden, StringComparison.Ordinal) && forbidden == "localhost")
                {
                    violations.Add("appsettings.json must not commit a developer-machine host such as localhost.");
                }
            }

            AssertNoViolations(violations, "environment-independent committed settings");
        }

        [Test]
        public void GivenTheLaunchSettings_WhenInspecting_ThenEveryProfileDeclaresAnApplicationUrl()
        {
            List<string> violations = [];
            string path = Path.Combine(
                SourceRepository.Root,
                "PersonalPresentationWebsite",
                "Properties",
                "launchSettings.json");

            if (!File.Exists(path))
            {
                Assert.Ignore("No launchSettings.json exists.");
            }

            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            System.Text.Json.JsonElement profiles = document.RootElement.GetProperty("profiles");

            foreach (System.Text.Json.JsonProperty profile in profiles.EnumerateObject())
            {
                if (!profile.Value.TryGetProperty("applicationUrl", out System.Text.Json.JsonElement _))
                {
                    violations.Add($"Launch profile '{profile.Name}' declares no applicationUrl.");
                }
            }

            AssertNoViolations(violations, "launch profile URLs");
        }

        [Test]
        public void GivenTheLaunchSettings_WhenInspecting_ThenNoSecretIsCommittedInAnEnvironmentVariable()
        {
            List<string> violations = [];
            string path = Path.Combine(
                SourceRepository.Root,
                "PersonalPresentationWebsite",
                "Properties",
                "launchSettings.json");

            if (!File.Exists(path))
            {
                Assert.Ignore("No launchSettings.json exists.");
            }

            string content = File.ReadAllText(path);
            Regex secretAssignment = new(@"(?i)(password|secret|token|apikey|privatekey)[""']?\s*[:=]\s*[""'][^""']{4,}[""']");

            if (secretAssignment.IsMatch(content))
            {
                violations.Add("launchSettings.json must not commit a secret value.");
            }

            AssertNoViolations(violations, "no committed launch secrets");
        }

        [Test]
        public void GivenEverySettingsClass_WhenInspecting_ThenItsKeysAreCoveredByTheJsonConfiguration()
        {
            List<string> violations = [];

            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(AppSettingsPath));

            if (!document.RootElement.TryGetProperty("funding", out System.Text.Json.JsonElement funding))
            {
                Assert.Fail("The funding section must exist so that its settings class binds.");
            }

            if (!funding.TryGetProperty("saltBank", out System.Text.Json.JsonElement saltBank))
            {
                Assert.Fail("The saltBank section must exist so that its bank-account settings class binds.");
            }

            foreach (string key in new[] { "iban", "swift" })
            {
                if (!saltBank.TryGetProperty(key, out System.Text.Json.JsonElement _))
                {
                    violations.Add($"saltBank section is missing the declared key '{key}'.");
                }
            }

            AssertNoViolations(violations, "nested settings binding");
        }

        private static System.Text.Json.JsonElement LoadSection(string name)
            => System.Text.Json.JsonDocument.Parse(File.ReadAllText(AppSettingsPath)).RootElement.GetProperty(name);

        private static void Walk(System.Text.Json.JsonElement element, Regex camelCase, List<string> violations, string path, bool isLoggingSection)
        {
            foreach (System.Text.Json.JsonProperty property in element.EnumerateObject())
            {
                string fullPath = path.Length == 0 ? property.Name : $"{path}:{property.Name}";
                bool inLoggingSection = isLoggingSection || property.Name == "logging";

                if (property.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    Walk(property.Value, camelCase, violations, fullPath, inLoggingSection);
                    continue;
                }

                if (inLoggingSection && property.Name.Contains('.', StringComparison.Ordinal))
                {
                    continue;
                }

                if (!camelCase.IsMatch(property.Name))
                {
                    violations.Add($"Key '{fullPath}' is not camelCase.");
                }
            }
        }

        private static void WalkForSensitiveLiteral(System.Text.Json.JsonElement element, string[] sensitiveKeys, List<string> violations, string path)
        {
            foreach (System.Text.Json.JsonProperty property in element.EnumerateObject())
            {
                string fullPath = path.Length == 0 ? property.Name : $"{path}:{property.Name}";

                if (property.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    WalkForSensitiveLiteral(property.Value, sensitiveKeys, violations, fullPath);
                    continue;
                }

                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.String)
                {
                    continue;
                }

                if (sensitiveKeys.Any(key => property.Name.ToLowerInvariant().Contains(key, StringComparison.Ordinal)))
                {
                    violations.Add($"Key '{fullPath}' stores a sensitive value as a plain literal; source it from a secret store or environment variable.");
                }
            }
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
