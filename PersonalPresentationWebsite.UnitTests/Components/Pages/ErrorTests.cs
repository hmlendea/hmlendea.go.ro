using System;
using System.Diagnostics;

using Bunit;

using Microsoft.AspNetCore.Http;

using NUnit.Framework;

using PersonalPresentationWebsite.Components.Pages;

namespace PersonalPresentationWebsite.UnitTests.Components.Pages
{
    [TestFixture]
    public sealed class ErrorTests
    {
        private BunitContext testContext = null!;

        [SetUp]
        public void SetUp()
        {
            testContext = new BunitContext();
            Activity.Current = null;
        }

        [TearDown]
        public void TearDown()
        {
            Activity.Current = null;
            testContext.Dispose();
        }

        [Test]
        public void GivenAnHttpContextWithATraceIdentifier_WhenRendering_ThenTheRequestIdentifierIsDisplayed()
        {
            DefaultHttpContext httpContext = new() { TraceIdentifier = "TraceIdentifier-20261111" };

            IRenderedComponent<Error> component = testContext.Render<Error>(parameters => parameters
                .AddCascadingValue(httpContext));

            Assert.Multiple(() =>
            {
                Assert.That(component.Find("code").TextContent, Is.EqualTo(httpContext.TraceIdentifier));
                Assert.That(component.Find("h1").TextContent, Is.EqualTo("Error."));
            });
        }

        [Test]
        public void GivenNeitherAnActivityNorAnHttpContext_WhenRendering_ThenNoRequestIdentifierIsDisplayed()
        {
            IRenderedComponent<Error> component = testContext.Render<Error>();

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("code"), Is.Empty);
                Assert.That(component.Markup, Does.Not.Contain("Request ID:"));
                Assert.That(component.Markup, Does.Contain("Development Mode"));
            });
        }

        [Test]
        public void GivenAnActiveActivity_WhenRendering_ThenTheActivityIdentifierTakesPrecedence()
        {
            using Activity activity = new("TestActivity");
            activity.Start();

            IRenderedComponent<Error> component = testContext.Render<Error>(parameters => parameters
                .AddCascadingValue(new DefaultHttpContext { TraceIdentifier = "TraceIdentifier-20261111" }));

            Assert.That(component.Find("code").TextContent, Is.EqualTo(activity.Id));
        }
    }
}