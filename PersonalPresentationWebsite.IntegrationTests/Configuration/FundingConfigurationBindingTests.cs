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
        private static readonly string[] patreonKeyVariants =
        [
            "patreonUsername", "PatreonUsername", "PATREONUSERNAME"
        ];

        private static readonly string[] kofiKeyVariants =
        [
            "kofiUsername", "KofiUsername", "KOFIUSERNAME"
        ];

        private static readonly string[] ibanKeyVariants =
        [
            "saltBank:iban", "SaltBank:Iban", "SALTBANK:IBAN"
        ];

        private static readonly string[] derivedChannelNames =
        [
            FundingChannelNames.Patreon, FundingChannelNames.KoFi, FundingChannelNames.SaltBank
        ];

        private static readonly string[] linkedChannelNames =
        [
            FundingChannelNames.Patreon, FundingChannelNames.SaltBank
        ];

        private static IEnumerable<TestCaseData> CasingVariants
        {
            get
            {
                for (int index = 0; index < patreonKeyVariants.Length; index += 1)
                {
                    yield return new TestCaseData(
                        patreonKeyVariants[index],
                        kofiKeyVariants[index],
                        ibanKeyVariants[index]);
                }
            }
        }

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
                    Is.EqualTo(derivedChannelNames));
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
                configuration.GetValue<int>(GitHubSettingsKeys.CacheDurationInMinutes, 30),
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
                Is.EqualTo(linkedChannelNames));
        }

        private static FundingSettings BindFundingSettings(
            string patreonKey,
            string kofiKey,
            string ibanKey)
            => BindFundingSettings(patreonKey, kofiKey, ibanKey, "zezima");

        private static FundingSettings BindFundingSettings(
            string patreonKey,
            string kofiKey,
            string ibanKey,
            string kofiUsername)
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
