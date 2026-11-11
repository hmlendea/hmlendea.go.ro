using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using NUnit.Framework;

using PersonalPresentationWebsite.IntegrationTests.Infrastructure;

namespace PersonalPresentationWebsite.IntegrationTests.Endpoints
{
    [TestFixture]
    public sealed class RepositoryEndpointTests
    {
        private const string Repository = "narivia";
        private const string RepositoryEndpoint = "/api/github/repos/hmlendea/narivia";
        private const string RepositoryResource = "repos/hmlendea/narivia";
        private const string LatestReleaseEndpoint = "/api/github/repos/hmlendea/narivia/releases/latest";
        private const string LatestReleaseResource = "repos/hmlendea/narivia/releases/latest";

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
        public async Task GivenARepositoryEndpoint_WhenGitHubReturnsTheRepository_ThenItIsProxied()
        {
            factory.GitHubHandler.EnqueueJsonResponse(
                RepositoryResource,
                """{"name":"narivia","stargazers_count":37,"forks_count":5}""");

            using HttpResponseMessage response = await client.GetAsync(RepositoryEndpoint);
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(document.RootElement.GetProperty("name").GetString(), Is.EqualTo("narivia"));
                Assert.That(document.RootElement.GetProperty("stargazers_count").GetInt32(), Is.EqualTo(37));
                Assert.That(factory.GitHubHandler.RequestedResources, Does.Contain(RepositoryResource));
            });
        }

        [Test]
        public async Task GivenALatestReleaseEndpoint_WhenGitHubReturnsTheRelease_ThenItIsProxied()
        {
            factory.GitHubHandler.EnqueueJsonResponse(
                LatestReleaseResource,
                """{"tag_name":"v1.4.2","name":"v1.4.2"}""");

            using HttpResponseMessage response = await client.GetAsync(LatestReleaseEndpoint);
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(document.RootElement.GetProperty("tag_name").GetString(), Is.EqualTo("v1.4.2"));
                Assert.That(factory.GitHubHandler.RequestedResources, Does.Contain(LatestReleaseResource));
            });
        }

        [Test]
        public async Task GivenARepositoryWithoutReleases_WhenRequestingTheLatestRelease_ThenAnEmptyPayloadIsReturned()
        {
            factory.GitHubHandler.EnqueueResponse(LatestReleaseResource, HttpStatusCode.NotFound);

            using HttpResponseMessage response = await client.GetAsync(LatestReleaseEndpoint);
            string responseBody = await response.Content.ReadAsStringAsync();

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(responseBody, Is.Empty);
            });
        }

        [Test]
        public async Task GivenTheRepositoryEndpoint_WhenGitHubIsUnavailable_ThenServiceUnavailableIsReturned()
        {
            factory.GitHubHandler.EnqueueResponse(RepositoryResource, HttpStatusCode.BadGateway);

            using HttpResponseMessage response = await client.GetAsync(RepositoryEndpoint);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        }

        [Test]
        public async Task GivenAnUnknownRepository_WhenRequestingIt_ThenNotNullIsReturned()
        {
            using HttpResponseMessage response = await client.GetAsync("/api/github/repos/hmlendea/unknown-repository");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }
    }
}