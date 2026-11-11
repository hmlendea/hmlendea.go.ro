using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PersonalPresentationWebsite.IntegrationTests.Infrastructure
{
    internal sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private const string JsonMediaType = "application/json";

        private readonly Dictionary<string, Queue<HttpResponseMessage>> responsesByResource = [];
        private readonly List<string> requestedResources = [];

        internal IReadOnlyList<string> RequestedResources => requestedResources;

        internal void EnqueueJsonResponse(string resource, string content)
            => EnqueueResponse(
                resource,
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content, Encoding.UTF8, JsonMediaType)
                });

        internal void EnqueueResponse(string resource, HttpStatusCode statusCode)
            => EnqueueResponse(
                resource,
                new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent("{}", Encoding.UTF8, JsonMediaType)
                });

        internal void EnqueueResponse(string resource, HttpResponseMessage response)
        {
            if (!responsesByResource.TryGetValue(resource, out Queue<HttpResponseMessage>? responses))
            {
                responses = new Queue<HttpResponseMessage>();
                responsesByResource.Add(resource, responses);
            }

            responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string resource = request.RequestUri!.PathAndQuery.TrimStart('/');
            requestedResources.Add(resource);

            if (responsesByResource.TryGetValue(resource, out Queue<HttpResponseMessage>? responses) &&
                responses.Count > 0)
            {
                return Task.FromResult(responses.Dequeue());
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}