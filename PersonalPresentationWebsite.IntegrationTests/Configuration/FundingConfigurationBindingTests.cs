using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Configuration;

using NUnit.Framework;

using PersonalPresentationWebsite.Configuration;

namespace PersonalPresentationWebsite.IntegrationTests.Configuration
{
    [TestFixture]
    public sealed class FundingConfigurationBindingTests
    {
        private static readonly string[] PatreonKeyVariants =
        [
            "patreonUsername", "PatreonUsername", "PATREONUSERNAME"
        ];

        private static readonly string[] KoftKeyVariants =
        [
            "kofiUsername", "KofiUsername", "KOFIUSERNAME"
        ];

        private static readonly string[] IbanKeyVariants =
        [
            "saltBank:iban", "SaltBank:Iban", "SALTBANK:IBAN"
        ];

        [TestCaseSource(nameof(CasingVariants))]
        public void GivenFundingIdentifiersInAnyCasing_WhenBinding_ThenEveryChannelIsDerived(
            string patreonKey,
            string kofiKey,
            string ibanKey)
        {
            FundingSettings fundingSettings = BindFundingSettings(patreonKey, kofiKey, ibanKey);

            Assert.Multiple(() =>
            {
                Assert.That(
                    fundingSettings.PatreonUsername,
                    Is.EqualTo("Angetenar"));
                Assert.That(
                    fundingSettings.KofiUsername,
                    Is.EqualTo("zezima"));
                Assert.That(
                    fundingSettings.SaltBank.Iban,
                    Is.EqualTo("RO49AAAA1B31007593840000"));
                Assert.That(
                    fundingSettings.Channels.Select(channel => channel.Name),
                    Is.EqualTo(new[] { "Patreon", "Ko-fi", "Salt Bank" }));
            });
        }

        [TestCase("gitHub:cacheDurationInMinutes")]
        [TestCase("GitHub:CacheDurationInMinutes")]
        [TestCase("GITHUB:CACHEDURATIONINMINUTES")]
        public void GivenTheCacheDurationInAnyCasing_WhenBinding_ThenItIsRead(string configurationKey)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                [
                    new KeyValuePair<string, string?>(configurationKey, "45")
                ])
                .Build();

            Assert.That(
                configuration.GetValue<int>("gitHub:cacheDurationInMinutes", 30),
                Is.EqualTo(45));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void GivenAConfiguringIdentifierThatIsBlank_WhenBinding_ThenTheChannelIsSkipped(string kofiUsername)
        {
            FundingSettings fundingSettings = BindFundingSettings(
                "patreonUsername",
                "kofiUsername",
                "saltBank:iban",
                kofiUsername);

            Assert.That(
                fundingSettings.Channels.Select(channel => channel.Name),
                Is.EqualTo(new[] { "Patreon", "Salt Bank" }));
        }

        private static IEnumerable<TestCaseData> CasingVariants
        {
            get
            {
                for (int index = 0; index < PatreonKeyVariants.Length; index += 1)
                {
                    yield return new TestCaseData(
                        PatreonKeyVariants[index],
                        KoftKeyVariants[index],
                        IbanKeyVariants[index]);
                }
            }
        }

        private static FundingSettings BindFundingSettings(
            string patreonKey,
            string kofiKey,
            string ibanKey,
            string kofiUsername = "zezima")
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                [
                    new KeyValuePair<string, string?>($"funding:{patreonKey}", "Angetenar"),
                    new KeyValuePair<string, string?>($"funding:{kofiKey}", kofiUsername),
                    new KeyValuePair<string, string?>($"funding:{ibanKey}", "RO49AAAA1B31007593840000")
                ])
                .Build();

            FundingSettings fundingSettings = new();
            configuration
                .GetSection(FundingSettings.SectionName)
                .Bind(fundingSettings);

            return fundingSettings;
        }
    }
}
