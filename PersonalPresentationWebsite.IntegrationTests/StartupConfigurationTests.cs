using System;

using NUnit.Framework;

using PersonalPresentationWebsite.Configuration;

namespace PersonalPresentationWebsite.IntegrationTests
{
    [TestFixture]
    public sealed class StartupConfigurationTests
    {
        [TestCase("gitHub:cacheDurationInMinutes", "29")]
        [TestCase("GitHub:CacheDurationInMinutes", "29")]
        [TestCase("gitHub:cacheDurationInMinutes", "0")]
        [TestCase("GitHub:CacheDurationInMinutes", "0")]
        [TestCase("gitHub:cacheDurationInMinutes", "-30")]
        [TestCase("GitHub:CacheDurationInMinutes", "-30")]
        public void GivenACacheDurationBelowTheMinimum_WhenStarting_ThenTheApplicationRefusesToStart(
            string configurationKey,
            string cacheDurationInMinutes)
        {
            string[] arguments = [$"--{configurationKey}={cacheDurationInMinutes}"];

            string paramNameProperty = nameof(ArgumentOutOfRangeException.ParamName);

            Assert.That(
                () => Program.Main(arguments),
                Throws.TypeOf<ArgumentOutOfRangeException>()
                    .With.Property(paramNameProperty)
                    .EqualTo(GitHubSettingsKeys.CacheDurationInMinutes));
        }
    }
}
