using System.Collections.Generic;
using System.Linq;

using AngleSharp.Dom;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using NUnit.Framework;

using PersonalPresentationWebsite.Components.Pages;
using PersonalPresentationWebsite.Configuration;

namespace PersonalPresentationWebsite.UnitTests.Components.Pages
{
    [TestFixture]
    public sealed class FundingTests
    {
        private static readonly string PatreonUsername = "Angetenar";
        private static readonly string RevolutUsername = "DummyUser";
        private static readonly string PayPalHostedButtonId = "DummyUser";
        private static readonly string GitHubSponsorsUsername = "IlarionPintilie";
        private static readonly string LiberaPayUsername = "solaire_of_astora";
        private static readonly string BuyMeACoffeeUsername = "zezima";
        private static readonly string KofiUsername = "Angetenar";
        private static readonly string SaltBankIban = "RO49AAAA1B31007593840000";
        private static readonly string SaltBankSwift = "AAAAROBU";

        private BunitContext testContext = null!;

        [SetUp]
        public void SetUp() => testContext = new BunitContext();

        [TearDown]
        public void TearDown() => testContext.Dispose();

        [Test]
        public void GivenEveryFundingIdentifierConfigured_WhenDerivingChannels_ThenEveryChannelIsAvailable()
        {
            IReadOnlyList<FundingChannelSettings> channels = ConfiguredFundingSettings().Channels.ToList();

            Assert.Multiple(() =>
            {
                Assert.That(
                    channels.Select(channel => channel.Name),
                    Is.EqualTo(ExpectedChannelNames));
                Assert.That(
                    channels.Where(channel => !string.IsNullOrEmpty(channel.Url)).Select(channel => channel.Url),
                    Is.EqualTo(ExpectedChannelAddresses));
                Assert.That(
                    channels.Select(channel => channel.Image),
                    Is.EqualTo(ExpectedChannelImages));
            });
        }

        [Test]
        public void GivenEveryFundingIdentifierConfigured_WhenDerivingChannels_ThenTheBankTransferChannelIsAvailable()
        {
            FundingChannelSettings bankChannel = ConfiguredFundingSettings()
                .Channels
                .Single(channel => channel.BankTransfer is not null);

            Assert.Multiple(() =>
            {
                Assert.That(bankChannel.Name, Is.EqualTo("Salt Bank"));
                Assert.That(bankChannel.Url, Is.Empty);
                Assert.That(bankChannel.Image, Is.EqualTo("../assets/img/funding/saltbank.png"));
                Assert.That(bankChannel.BankTransfer!.ModalIdentifier, Is.EqualTo("modal-saltbank"));
                Assert.That(bankChannel.BankTransfer!.Iban, Is.EqualTo(SaltBankIban));
                Assert.That(bankChannel.BankTransfer!.Swift, Is.EqualTo(SaltBankSwift));
            });
        }

        [Test]
        public void GivenNoFundingIdentifierConfigured_WhenDerivingChannels_ThenNoChannelIsAvailable()
            => Assert.That(new FundingSettings().Channels, Is.Empty);

        [Test]
        public void GivenASingleFundingIdentifierConfigured_WhenDerivingChannels_ThenOnlyThatChannelIsAvailable()
        {
            FundingSettings fundingSettings = new() { PatreonUsername = PatreonUsername };

            Assert.That(
                fundingSettings.Channels.Select(channel => channel.Name),
                Is.EqualTo(new[] { "Patreon" }));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void GivenAMissingFundingIdentifier_WhenDerivingChannels_ThenTheChannelIsSkipped(string patreonUsername)
        {
            FundingSettings fundingSettings = new()
            {
                PatreonUsername = patreonUsername,
                KofiUsername = KofiUsername
            };

            Assert.That(
                fundingSettings.Channels.Select(channel => channel.Name),
                Is.EqualTo(new[] { "Ko-fi" }));
        }

        [Test]
        public void GivenNoBankAccountConfigured_WhenDerivingChannels_ThenTheBankTransferChannelIsSkipped()
        {
            FundingSettings fundingSettings = new() { PatreonUsername = PatreonUsername };

            Assert.That(
                fundingSettings.Channels.Select(channel => channel.Name),
                Is.EqualTo(new[] { "Patreon" }));
        }

        [Test]
        public void GivenABankAccountWithoutAnIban_WhenDerivingChannels_ThenTheBankTransferChannelIsSkipped()
        {
            FundingSettings fundingSettings = new()
            {
                SaltBank = new BankAccountSettings { Swift = SaltBankSwift }
            };

            Assert.That(fundingSettings.Channels, Is.Empty);
        }

        [Test]
        public void GivenTheFundingPage_WhenRendering_ThenEveryDerivedChannelIsListed()
        {
            IRenderedComponent<Funding> component = RenderFunding(ConfiguredFundingSettings());

            IEnumerable<string> channelNames = component
                .FindAll("#portfolio-gallery .portfolio-item-caption-content")
                .Select(caption => caption.TextContent.Trim());

            Assert.That(channelNames, Is.EqualTo(ExpectedChannelNames));
        }

        [Test]
        public void GivenTheFundingPage_WhenRendering_ThenEveryLinkedChannelPointsToItsDerivedAddress()
        {
            IRenderedComponent<Funding> component = RenderFunding(ConfiguredFundingSettings());

            IReadOnlyList<IElement> links = component.FindAll("#portfolio-gallery a");

            Assert.That(links.Count, Is.EqualTo(ExpectedChannelAddresses.Count));

            for (int index = 0; index < links.Count; index += 1)
            {
                Assert.That(
                    links[index].GetAttribute("href"),
                    Is.EqualTo(ExpectedChannelAddresses[index]));
            }
        }

        [Test]
        public void GivenTheFundingPage_WhenRendering_ThenTheBankTransferDetailsAreAvailable()
        {
            IRenderedComponent<Funding> component = RenderFunding(ConfiguredFundingSettings());

            IElement modalBody = component.Find("#modal-saltbank .modal-body");

            Assert.Multiple(() =>
            {
                Assert.That(component.Find("#modal-saltbank"), Is.Not.Null);
                Assert.That(modalBody.TextContent, Does.Contain(SaltBankIban));
                Assert.That(modalBody.TextContent, Does.Contain(SaltBankSwift));
                Assert.That(component.Find("[data-bs-target='#modal-saltbank']"), Is.Not.Null);
            });
        }

        [Test]
        public void GivenTheFundingPage_WhenNoFundingIdentifierIsConfigured_ThenNoChannelIsDisplayed()
        {
            IRenderedComponent<Funding> component = RenderFunding(new FundingSettings());

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("#portfolio-gallery > div"), Is.Empty);
                Assert.That(component.FindAll(".modal"), Is.Empty);
            });
        }

        private static FundingSettings ConfiguredFundingSettings()
            => new()
            {
                PatreonUsername = PatreonUsername,
                RevolutUsername = RevolutUsername,
                PayPalHostedButtonId = PayPalHostedButtonId,
                GitHubSponsorsUsername = GitHubSponsorsUsername,
                LiberaPayUsername = LiberaPayUsername,
                BuyMeACoffeeUsername = BuyMeACoffeeUsername,
                KofiUsername = KofiUsername,
                SaltBank = new BankAccountSettings
                {
                    Iban = SaltBankIban,
                    Swift = SaltBankSwift
                }
            };

        private IRenderedComponent<Funding> RenderFunding(FundingSettings fundingSettings)
        {
            testContext.Services.AddSingleton(fundingSettings);

            return testContext.Render<Funding>();
        }

        private static IReadOnlyList<string> ExpectedChannelNames =>
        [
            "Patreon", "Revolut", "PayPal", "GitHub Sponsors",
            "LiberaPay", "Buy Me a Coffee", "Ko-fi", "Salt Bank"
        ];

        private static IReadOnlyList<string> ExpectedChannelAddresses =>
        [
            $"https://patreon.com/{PatreonUsername}",
            $"https://revolut.me/{RevolutUsername}",
            $"https://paypal.com/donate?hosted_button_id={PayPalHostedButtonId}",
            $"https://github.com/sponsors/{GitHubSponsorsUsername}",
            $"https://liberapay.com/{LiberaPayUsername}/donate",
            $"https://buymeacoffee.com/{BuyMeACoffeeUsername}",
            $"https://ko-fi.com/{KofiUsername}"
        ];

        private static IReadOnlyList<string> ExpectedChannelImages =>
        [
            "../assets/img/funding/patreon.png",
            "../assets/img/funding/revolut.png",
            "../assets/img/funding/paypal.png",
            "../assets/img/funding/github.png",
            "../assets/img/funding/liberapay.png",
            "../assets/img/funding/buymeacoffee.png",
            "../assets/img/funding/kofi.png",
            "../assets/img/funding/saltbank.png"
        ];
    }
}
