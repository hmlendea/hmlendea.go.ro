using System.Collections.Generic;

using AngleSharp.Dom;

using Bunit;

using NUnit.Framework;

using PersonalPresentationWebsite.Components.Pages;

namespace PersonalPresentationWebsite.UnitTests.Components.Pages
{
    [TestFixture]
    public sealed class HomeTests
    {
        private static readonly string[] SectionIdentifiers =
        [
            "#impact", "#about", "#portfolio", "#skills", "#timeline", "#contact"
        ];

        private static readonly string[] ImpactFigureIdentifiers =
        [
            "#impact-stars", "#impact-followers", "#impact-repos", "#impact-forks",
            "#impact-prs", "#impact-issues", "#impact-commits"
        ];

        private BunitContext testContext = null!;

        [SetUp]
        public void SetUp() => testContext = new BunitContext();

        [TearDown]
        public void TearDown() => testContext.Dispose();

        [Test]
        public void GivenTheHomePage_WhenRendering_ThenEverySectionIsPresent()
        {
            IRenderedComponent<Home> component = testContext.Render<Home>();

            Assert.That(component.FindAll("section.page-section"), Has.Count.EqualTo(6));

            foreach (string sectionIdentifier in SectionIdentifiers)
            {
                Assert.That(component.Find(sectionIdentifier), Is.Not.Null);
            }
        }

        [Test]
        public void GivenTheHomePage_WhenRendering_ThenTheNavigationTargetsEverySection()
        {
            IRenderedComponent<Home> component = testContext.Render<Home>();

            Assert.That(component.FindAll("#mainNav .nav-link"), Has.Count.EqualTo(6));

            foreach (string sectionIdentifier in SectionIdentifiers)
            {
                Assert.That(
                    component.Find($"#mainNav .nav-link[href='{sectionIdentifier}']"),
                    Is.Not.Null);
            }
        }

        [Test]
        public void GivenTheHomePage_WhenRendering_ThenTheImpactFiguresStartUnresolved()
        {
            IRenderedComponent<Home> component = testContext.Render<Home>();

            foreach (string figureIdentifier in ImpactFigureIdentifiers)
            {
                Assert.That(component.Find(figureIdentifier).TextContent.Trim(), Is.EqualTo("—"));
            }
        }

        [Test]
        public void GivenTheHomePage_WhenRendering_ThenThePortfolioOffersEveryFilterAndProject()
        {
            IRenderedComponent<Home> component = testContext.Render<Home>();

            Assert.That(component.FindAll("#portfolio .filter-button"), Has.Count.EqualTo(6));
            Assert.That(component.FindAll("#portfolio-gallery .portfolio-item"), Has.Count.EqualTo(16));
            Assert.That(component.FindAll(".portfolio-modal"), Has.Count.EqualTo(16));
        }

        [Test]
        public void GivenTheHomePage_WhenRendering_ThenEveryProjectModalIsReachableFromTheGallery()
        {
            IRenderedComponent<Home> component = testContext.Render<Home>();

            foreach (IElement modal in component.FindAll(".portfolio-modal"))
            {
                Assert.That(
                    component.Find($"#portfolio-gallery [data-bs-target='#{modal.Id}']"),
                    Is.Not.Null);
                Assert.That(
                    component.Find($"#{modal.Id} .portfolio-modal-title").TextContent.Trim(),
                    Is.Not.Empty);
            }
        }
    }
}