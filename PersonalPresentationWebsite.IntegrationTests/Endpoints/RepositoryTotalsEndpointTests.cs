using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using NUnit.Framework;

using PersonalPresentationWebsite.IntegrationTests.Infrastructure;

namespace PersonalPresentationWebsite.IntegrationTests.Endpoints
{
    [TestFixture]
    public sealed class RepositoryTotalsEndpointTests
    {
        private const string TotalsEndpoint = "/api/github/repository-totals";

        private PersonalPresentationWebsiteFactory factory = null!;
        private HttpClient client = null!;

        [SetUp]
        public void SetUp()
        {
            factory = new PersonalPresentationWebsiteFactory();
            client = factory.CreateClient();
        }

        [TearDown]
        public void TearDown()
        {
            client.Dispose();
            factory.Dispose();
        }

        [Test]
        public async Task GivenFewerRepositoriesThanAPage_WhenRequestingTheTotals_ThenTheyAreAggregated()
        {
            factory.GitHubHandler.EnqueueJsonResponse(
                BuildRepositoriesResource(1),
                """[{"stargazers_count":10,"forks_count":2},{"stargazers_count":5,"forks_count":1}]""");

            using HttpResponseMessage response = await client.GetAsync(TotalsEndpoint);
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(document.RootElement.GetProperty("stars").GetInt64(), Is.EqualTo(15));
                Assert.That(document.RootElement.GetProperty("forks").GetInt64(), Is.EqualTo(3));
                Assert.That(factory.GitHubHandler.RequestedResources, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task GivenExactlyOneFullPage_WhenRequestingTheTotals_ThenAnExtraPageIsRequested()
        {
            factory.GitHubHandler.EnqueueJsonResponse(BuildRepositoriesResource(1), BuildRepositoriesPage(100));
            factory.GitHubHandler.EnqueueJsonResponse(BuildRepositoriesResource(2), BuildRepositoriesPage(1));

            using HttpResponseMessage response = await client.GetAsync(TotalsEndpoint);
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(document.RootElement.GetProperty("stars").GetInt64(), Is.EqualTo(101));
                Assert.That(factory.GitHubHandler.RequestedResources, Has.Count.EqualTo(2));
                Assert.That(factory.GitHubHandler.RequestedResources, Does.Contain(BuildRepositoriesResource(2)));
            });
        }

        [Test]
        public async Task GivenNoPublicRepositories_WhenRequestingTheTotals_ThenZeroTotalsAreReturned()
        {
            factory.GitHubHandler.EnqueueJsonResponse(BuildRepositoriesResource(1), "[]");

            using HttpResponseMessage response = await client.GetAsync(TotalsEndpoint);
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Multiple(() =>
            {
                Assert.That(document.RootElement.GetProperty("stars").GetInt64(), Is.Zero);
                Assert.That(document.RootElement.GetProperty("forks").GetInt64(), Is.Zero);
            });
        }

        [Test]
        public async Task GivenTheTotalsEndpoint_WhenGitHubFails_ThenServiceUnavailableIsReturned()
        {
            factory.GitHubHandler.EnqueueResponse(BuildRepositoriesResource(1), HttpStatusCode.InternalServerError);

            using HttpResponseMessage response = await client.GetAsync(TotalsEndpoint);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        }

        [Test]
        public async Task GivenTheTotalsEndpoint_WhenRequestingItTwice_ThenTheResponseIsCached()
        {
            factory.GitHubHandler.EnqueueJsonResponse(BuildRepositoriesResource(1), BuildRepositoriesPage(2));

            using HttpResponseMessage firstResponse = await client.GetAsync(TotalsEndpoint);
            using HttpResponseMessage secondResponse = await client.GetAsync(TotalsEndpoint);

            Assert.Multiple(() =>
            {
                Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(factory.GitHubHandler.RequestedResources, Has.Count.EqualTo(1));
            });
        }

        private static string BuildRepositoriesResource(int page)
            => $"users/hmlendea/repos?per_page=100&page={page}";

        private static string BuildRepositoriesPage(int repositoryCount)
        {
            StringBuilder builder = new();
            builder.Append('[');

            for (int repositoryIndex = 0; repositoryIndex < repositoryCount; repositoryIndex += 1)
            {
                if (repositoryIndex > 0)
                {
                    builder.Append(',');
                }

                builder.Append("""{"stargazers_count":1,"forks_count":0}""");
            }

            builder.Append(']');

            return builder.ToString();
        }
    }
}