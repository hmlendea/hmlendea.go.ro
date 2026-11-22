using NUnit.Framework;

namespace PersonalPresentationWebsite.ArchitectureTests.Infrastructure
{
    internal static class RuleAssertions
    {
        internal static void EnsureSelected(int count, string description)
        {
            if (count == 0)
            {
                Assert.Fail(
                    $"The rule governing {description} selected no objects, so it would pass vacuously.");
            }
        }

        internal static void EnsureApplicable(int count, string description)
        {
            if (count == 0)
            {
                Assert.Ignore(
                    $"No {description} exist in the checked scope, so the rule is not applicable.");
            }
        }
    }
}
