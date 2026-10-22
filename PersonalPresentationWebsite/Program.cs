using System;
using System.Net.Http;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NuciWeb.HTTP;

using PersonalPresentationWebsite.Components;
using PersonalPresentationWebsite.Endpoints;

namespace PersonalPresentationWebsite
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorComponents();
            builder.Services.AddOutputCache();
            builder.Services.AddSingleton<HttpClient>(services =>
            {
                HttpClient client = HttpClientCreator.Create();
                client.BaseAddress = new Uri("https://api.github.com/");
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

                return client;
            });

            WebApplication app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseAntiforgery();
            app.UseOutputCache();

            app.MapGitHubEndpoints();
            app.MapStaticAssets();
            app.MapRazorComponents<App>();

            app.Run();
        }
    }
}
