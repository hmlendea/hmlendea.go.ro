using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;

using AngleSharp.Dom;

using Bunit;

using NUnit.Framework;

using PersonalPresentationWebsite.Components.Pages;
using PersonalPresentationWebsite.Configuration;

namespace PersonalPresentationWebsite.UnitTests.Components.Pages
{
    [TestFixture]
    public sealed class FundingTests
    {
        private static readonly string patreonUsername = "Angetenar";
        private static readonly string revolutUsername = "DummyUser";
        private static readonly string payPalHostedButtonId = "DummyUser";
        private static readonly string gitHubSponsorsUsername = "IlarionPintilie";
        private static readonly string liberaPayUsername = "solaire_of_astora";
        private static readonly string buyMeACoffeeUsername = "zezima";
        private static readonly string kofiUsername = "Angetenar";
        private static readonly string saltBankIban = "RO49AAAA1B31007593840000";
        private static readonly string saltBankSwift = "AAAAROBU";

        private static readonly IReadOnlyList<string> expectedChannelNames =
        [
            FundingChannelNames.Patreon, FundingChannelNames.Revolut, FundingChannelNames.PayPal,
            FundingChannelNames.GitHubSponsors, FundingChannelNames.LiberaPay,
            FundingChannelNames.BuyMeACoffee, FundingChannelNames.KoFi, FundingChannelNames.SaltBank
        ];

        private static readonly IReadOnlyList<string> expectedChannelAddresses =
        [
            $"https://patreon.com/{patreonUsername}",
            $"https://revolut.me/{revolutUsername}",
            $"https://paypal.com/donate?hosted_button_id={payPalHostedButtonId}",
            $"https://github.com/sponsors/{gitHubSponsorsUsername}",
            $"https://liberapay.com/{liberaPayUsername}/donate",
            $"https://buymeacoffee.com/{buyMeACoffeeUsername}",
            $"https://ko-fi.com/{kofiUsername}"
        ];

        private static readonly IReadOnlyList<string> expectedChannelImages =
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
                    Is.EqualTo(expectedChannelNames));
                Assert.That(
                    channels.Where(channel => !string.IsNullOrEmpty(channel.Url)).Select(channel => channel.Url),
                    Is.EqualTo(expectedChannelAddresses));
                Assert.That(
                    channels.Select(channel => channel.Image),
                    Is.EqualTo(expectedChannelImages));
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
                Assert.That(bankChannel.Name, Is.EqualTo(FundingChannelNames.SaltBank));
                Assert.That(bankChannel.Url, Is.Empty);
                Assert.That(bankChannel.Image, Is.EqualTo("../assets/img/funding/saltbank.png"));
                Assert.That(bankChannel.BankTransfer!.ModalId, Is.EqualTo("modal-saltbank"));
                Assert.That(bankChannel.BankTransfer!.Iban, Is.EqualTo(saltBankIban));
                Assert.That(bankChannel.BankTransfer!.Swift, Is.EqualTo(saltBankSwift));
            });
        }

        [Test]
        public void GivenNoFundingIdentifierConfigured_WhenDerivingChannels_ThenNoChannelIsAvailable()
            => Assert.That(new FundingSettings().Channels, Is.Empty);

        [Test]
        public void GivenASingleFundingIdentifierConfigured_WhenDerivingChannels_ThenOnlyThatChannelIsAvailable()
        {
            FundingSettings fundingSettings = new() { PatreonUsername = patreonUsername };

            Assert.That(
                fundingSettings.Channels.Select(channel => channel.Name),
                Is.EqualTo(
                [
                    FundingChannelNames.Patreon
                ]));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void GivenAMissingFundingIdentifier_WhenDerivingChannels_ThenTheChannelIsSkipped(string patreonUsername)
        {
            FundingSettings fundingSettings = new()
            {
                PatreonUsername = patreonUsername,
                KofiUsername = kofiUsername
            };

            Assert.That(
                fundingSettings.Channels.Select(channel => channel.Name),
                Is.EqualTo(
                [
                    FundingChannelNames.KoFi
                ]));
        }

        [Test]
        public void GivenNoBankAccountConfigured_WhenDerivingChannels_ThenTheBankTransferChannelIsSkipped()
        {
            FundingSettings fundingSettings = new() { PatreonUsername = patreonUsername };

            Assert.That(
                fundingSettings.Channels.Select(channel => channel.Name),
                Is.EqualTo(
                [
                    FundingChannelNames.Patreon
                ]));
        }

        [Test]
        public void GivenABankAccountWithoutAnIban_WhenDerivingChannels_ThenTheBankTransferChannelIsSkipped()
        {
            FundingSettings fundingSettings = new()
            {
                SaltBank = new BankAccountSettings { Swift = saltBankSwift }
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

            Assert.That(channelNames, Is.EqualTo(expectedChannelNames));
        }

        [Test]
        public void GivenTheFundingPage_WhenRendering_ThenEveryLinkedChannelPointsToItsDerivedAddress()
        {
            IRenderedComponent<Funding> component = RenderFunding(ConfiguredFundingSettings());

            IReadOnlyList<IElement> links = component.FindAll("#portfolio-gallery a");

            Assert.That(links.Count, Is.EqualTo(expectedChannelAddresses.Count));

            for (int index = 0; index < links.Count; index += 1)
            {
                Assert.That(
                    links[index].GetAttribute("href"),
                    Is.EqualTo(expectedChannelAddresses[index]));
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
                Assert.That(modalBody.TextContent, Does.Contain(saltBankIban));
                Assert.That(modalBody.TextContent, Does.Contain(saltBankSwift));
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
                PatreonUsername = patreonUsername,
                RevolutUsername = revolutUsername,
                PayPalHostedButtonId = payPalHostedButtonId,
                GitHubSponsorsUsername = gitHubSponsorsUsername,
                LiberaPayUsername = liberaPayUsername,
                BuyMeACoffeeUsername = buyMeACoffeeUsername,
                KofiUsername = kofiUsername,
                SaltBank = new BankAccountSettings
                {
                    Iban = saltBankIban,
                    Swift = saltBankSwift
                }
            };

        private IRenderedComponent<Funding> RenderFunding(FundingSettings fundingSettings)
        {
            testContext.Services.AddSingleton(fundingSettings);

            return testContext.Render<Funding>();
        }
    }
}
