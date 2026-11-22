using System;
using System.Net.Http;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NuciWeb.HTTP;

using PersonalPresentationWebsite.Components;
using PersonalPresentationWebsite.Configuration;
using PersonalPresentationWebsite.Endpoints;

namespace PersonalPresentationWebsite
{
    public static class Program
    {
        private static int MinimumCacheDurationInMinutes => 30;

        private static int DefaultCacheDurationInMinutes => 30;

        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            int cacheDurationInMinutes = builder.Configuration.GetValue<int>(
                GitHubSettingsKeys.CacheDurationInMinutes,
                DefaultCacheDurationInMinutes
            );

            if (cacheDurationInMinutes < MinimumCacheDurationInMinutes)
            {
                throw new ArgumentOutOfRangeException(
                    GitHubSettingsKeys.CacheDurationInMinutes,
                    "GitHub cache duration must be at least 30 minutes."
                );
            }

            RegisterServices(builder, cacheDurationInMinutes);

            WebApplication app = builder.Build();

            ConfigurePipeline(app);

            app.Run();
        }

        private static void RegisterServices(
            WebApplicationBuilder builder,
            int cacheDurationInMinutes)
        {
            builder.Services.AddRazorComponents();
            builder.Services.AddOutputCache(options => options.AddPolicy(
                GitHubEndpoints.CachePolicyName,
                policy => policy.Expire(TimeSpan.FromMinutes(cacheDurationInMinutes)).SetVaryByQuery([])
            ));

            FundingSettings fundingSettings = new();
            builder.Configuration
                .GetSection(FundingSettings.SectionName)
                .Bind(fundingSettings);
            builder.Services.AddSingleton(fundingSettings);

            builder.Services.AddSingleton<HttpClient>(services =>
            {
                HttpClient client = HttpClientCreator.Create();
                client.BaseAddress = new Uri("https://api.github.com/");
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

                return client;
            });
        }

        private static void ConfigurePipeline(WebApplication app)
        {
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseAntiforgery();
            app.UseOutputCache();

            app.MapGitHubEndpoints();
            app.MapStaticAssets();
            app.MapRazorComponents<App>();
        }
    }
}
