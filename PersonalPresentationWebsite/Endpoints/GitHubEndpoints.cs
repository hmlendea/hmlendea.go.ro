using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace PersonalPresentationWebsite.Endpoints
{
    public static class GitHubEndpoints
    {
        public static string CachePolicyName => "GitHub";

        private static int RepositoryPageSize => 100;

        private static IEnumerable<string> Repositories =>
        [
            "narivia", "more-cultural-names", "gfn-electron", "personal-log-manager",
            "personal-log-manager-client", "iptv-playlist-aggregator", "universal-name-generator",
            "duolingo-desktop", "bitwarden-vault-manager", "transliteration-api", "memoryblocks",
            "sokogrump", "minesweeper", "nucilog", "nucilog.core", "backgammon-by-horatiu",
            "nuciapi", "nuciapi.client", "nuciapi.controllers", "nuciapi.middleware",
            "nuciapi.middleware.exceptionhandling", "nuciapi.middleware.logging",
            "nuciapi.middleware.security", "nuciweb.automation", "nuciweb.automation.selenium",
            "nuciweb.automation.playwright"
        ];

        public static void MapGitHubEndpoints(this WebApplication application)
        {
            MapResource(application, "profile", "users/hmlendea");
            MapResource(application, "pull-requests", "search/issues?q=author:hmlendea+type:pr&per_page=1");
            MapResource(application, "issues", "search/issues?q=author:hmlendea+type:issue&per_page=1");
            MapResource(application, "commits", "search/commits?q=author:hmlendea&per_page=1");
            application.MapGet("/api/github/repository-totals", GetRepositoryTotalsAsync)
                .CacheOutput(CachePolicyName);

            foreach (string repository in Repositories)
            {
                string resource = $"repos/hmlendea/{repository}";
                MapResource(application, resource, resource);
                MapResource(application, $"{resource}/releases/latest", $"{resource}/releases/latest");
            }
        }

        private static void MapResource(WebApplication application, string route, string resource)
        {
            application.MapGet($"/api/github/{route}", async (HttpClient client) =>
            {
                using HttpResponseMessage response = await client.GetAsync(resource);

                if (response.StatusCode is HttpStatusCode.NotFound)
                {
                    return Results.Json((object?)null);
                }

                if (!response.IsSuccessStatusCode)
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                return Results.Json(document.RootElement.Clone());
            }).CacheOutput(CachePolicyName);
        }

        private static async Task<IResult> GetRepositoryTotalsAsync(HttpClient client)
        {
            long stars = 0;
            long forks = 0;
            int repositoryCount;
            int page = 1;

            do
            {
                string pageNumber = page.ToString(CultureInfo.InvariantCulture);
                string resource = $"users/hmlendea/repos?per_page={RepositoryPageSize}&page={pageNumber}";
                using HttpResponseMessage response = await client.GetAsync(resource);

                if (!response.IsSuccessStatusCode)
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                repositoryCount = document.RootElement.GetArrayLength();

                foreach (JsonElement repository in document.RootElement.EnumerateArray())
                {
                    stars += repository.GetProperty("stargazers_count").GetInt64();
                    forks += repository.GetProperty("forks_count").GetInt64();
                }

                page++;
            }
            while (repositoryCount == RepositoryPageSize);

            return Results.Json(new { stars, forks });
        }
    }
}