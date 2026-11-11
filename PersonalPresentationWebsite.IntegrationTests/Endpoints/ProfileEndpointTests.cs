using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using NUnit.Framework;

using PersonalPresentationWebsite.IntegrationTests.Infrastructure;

namespace PersonalPresentationWebsite.IntegrationTests.Endpoints
{
    [TestFixture]
    public sealed class ProfileEndpointTests
    {
        private const string ProfileEndpoint = "/api/github/profile";
        private const string ProfileResource = "users/hmlendea";

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
        public async Task GivenTheProfileEndpoint_WhenGitHubReturnsTheProfile_ThenItIsProxied()
        {
            factory.GitHubHandler.EnqueueJsonResponse(
                ProfileResource,
                """{"login":"hmlendea","id":10261133,"followers":120}""");

            using HttpResponseMessage response = await client.GetAsync(ProfileEndpoint);
            string responseBody = await response.Content.ReadAsStringAsync();

            using JsonDocument document = JsonDocument.Parse(responseBody);

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
                Assert.That(document.RootElement.GetProperty("login").GetString(), Is.EqualTo("hmlendea"));
                Assert.That(document.RootElement.GetProperty("id").GetInt32(), Is.EqualTo(10261133));
                Assert.That(factory.GitHubHandler.RequestedResources, Does.Contain(ProfileResource));
            });
        }

        [Test]
        public async Task GivenTheProfileEndpoint_WhenGitHubReturnsNotFound_ThenAnEmptyPayloadIsReturned()
        {
            factory.GitHubHandler.EnqueueResponse(ProfileResource, HttpStatusCode.NotFound);

            using HttpResponseMessage response = await client.GetAsync(ProfileEndpoint);
            string responseBody = await response.Content.ReadAsStringAsync();

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(responseBody, Is.Empty);
            });
        }

        [TestCase(HttpStatusCode.InternalServerError)]
        [TestCase(HttpStatusCode.Forbidden)]
        [TestCase(HttpStatusCode.Unauthorized)]
        public async Task GivenTheProfileEndpoint_WhenGitHubFails_ThenServiceUnavailableIsReturned(HttpStatusCode gitHubStatusCode)
        {
            factory.GitHubHandler.EnqueueResponse(ProfileResource, gitHubStatusCode);

            using HttpResponseMessage response = await client.GetAsync(ProfileEndpoint);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        }

        [Test]
        public async Task GivenTheProfileEndpoint_WhenRequestingItTwice_ThenGitHubIsQueriedOnce()
        {
            factory.GitHubHandler.EnqueueJsonResponse(ProfileResource, """{"login":"hmlendea"}""");

            using HttpResponseMessage firstResponse = await client.GetAsync(ProfileEndpoint);
            using HttpResponseMessage secondResponse = await client.GetAsync(ProfileEndpoint);

            Assert.Multiple(() =>
            {
                Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(
                    factory.GitHubHandler.RequestedResources,
                    Has.Exactly(1).EqualTo(ProfileResource));
            });
        }

        [TestCase("/api/github/pull-requests", "search/issues?q=author:hmlendea+type:pr&per_page=1")]
        [TestCase("/api/github/issues", "search/issues?q=author:hmlendea+type:issue&per_page=1")]
        [TestCase("/api/github/commits", "search/commits?q=author:hmlendea&per_page=1")]
        public async Task GivenASearchEndpoint_WhenGitHubReturnsTotals_ThenTheyAreProxied(string endpoint, string resource)
        {
            factory.GitHubHandler.EnqueueJsonResponse(resource, """{"total_count":42}""");

            using HttpResponseMessage response = await client.GetAsync(endpoint);
            string responseBody = await response.Content.ReadAsStringAsync();

            using JsonDocument document = JsonDocument.Parse(responseBody);

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(document.RootElement.GetProperty("total_count").GetInt32(), Is.EqualTo(42));
                Assert.That(factory.GitHubHandler.RequestedResources, Does.Contain(resource));
            });
        }
    }
}