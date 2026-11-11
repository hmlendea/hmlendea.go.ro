using System;
using System.Net.Http;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using PersonalPresentationWebsite.Components;

namespace PersonalPresentationWebsite.IntegrationTests.Infrastructure
{
    internal sealed class PersonalPresentationWebsiteFactory : WebApplicationFactory<App>
    {
        private static string EnvironmentName => "IntegrationTesting";

        private readonly StubHttpMessageHandler gitHubHandler = new();

        internal StubHttpMessageHandler GitHubHandler => gitHubHandler;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(EnvironmentName);
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<HttpClient>();

                HttpClient client = new(gitHubHandler, disposeHandler: false)
                {
                    BaseAddress = new Uri("https://api.github.com/")
                };
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

                services.AddSingleton(client);
            });
        }
    }
}