using System.Collections.Generic;
using System.Linq;

using AngleSharp.Dom;

using Bunit;

using NUnit.Framework;

using PersonalPresentationWebsite.Components.Pages;

namespace PersonalPresentationWebsite.UnitTests.Components.Pages
{
    [TestFixture]
    public sealed class FundingTests
    {
        private static readonly string[] FundingChannelNames =
        [
            "Patreon", "Revolut", "PayPal", "GitHub Sponsors",
            "LiberaPay", "Buy Me a Coffee", "Ko-fi", "Salt Bank"
        ];

        private BunitContext testContext = null!;

        [SetUp]
        public void SetUp() => testContext = new BunitContext();

        [TearDown]
        public void TearDown() => testContext.Dispose();

        [Test]
        public void GivenTheFundingPage_WhenRendering_ThenEveryChannelIsListed()
        {
            IRenderedComponent<Funding> component = testContext.Render<Funding>();

            IEnumerable<string> channelNames = component
                .FindAll("#portfolio-gallery .portfolio-item-caption-content")
                .Select(caption => caption.TextContent.Trim());

            Assert.That(channelNames, Is.EqualTo(FundingChannelNames));
        }

        [Test]
        public void GivenTheFundingPage_WhenRendering_ThenEveryLinkedChannelPointsToItsProvider()
        {
            IRenderedComponent<Funding> component = testContext.Render<Funding>();

            Assert.That(component.FindAll("#portfolio-gallery a"), Has.Count.EqualTo(7));

            foreach (IElement link in component.FindAll("#portfolio-gallery a"))
            {
                Assert.That(link.GetAttribute("href"), Does.StartWith("https://"));
            }
        }

        [Test]
        public void GivenTheFundingPage_WhenRendering_ThenTheBankTransferDetailsAreAvailable()
        {
            IRenderedComponent<Funding> component = testContext.Render<Funding>();

            Assert.That(component.Find("#modalSaltBank"), Is.Not.Null);
            Assert.That(component.Find("#modalSaltBank .modal-body").TextContent, Does.Contain("ROINROBU"));
            Assert.That(component.Find("[data-bs-target='#modalSaltBank']"), Is.Not.Null);
        }
    }
}