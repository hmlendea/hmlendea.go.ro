[![Latest Release](https://img.shields.io/github/v/release/hmlendea/hmlendea.go.ro)](https://github.com/hmlendea/hmlendea.go.ro/releases/latest)
[![Website](https://img.shields.io/badge/Website-Visit-blue)](https://hmlendea.go.ro)
[![License](https://img.shields.io/github/license/hmlendea/hmlendea.go.ro)](https://github.com/hmlendea/hmlendea.go.ro/blob/master/LICENSE)

# Personal Presentation Website

My personal presentation website.

## 📑 Table of Contents

- [Capabilities](#-capabilities)
- [Use Cases](#-use-cases)
- [Usage](#-usage)
- [Known Limitations](#-known-limitations)
- [Installation](#-installation)
  - [Manual Installation](#manual-installation)
  - [Verification](#verification)
- [Configuration](#-configuration)
  - [Configuration Files](#configuration-files)
  - [Settings](#settings)
  - [Precedence](#precedence)
  - [Reload Behaviour](#reload-behaviour)
  - [Profiles and Scopes](#profiles-and-scopes)
  - [Network Ports](#network-ports)
  - [Validation](#validation)
- [Compatibility](#-compatibility)
- [Integrations](#-integrations)
- [Privacy and Data](#-privacy-and-data)
- [Development](#-development)
  - [Requirements](#requirements)
  - [Setup](#setup)
  - [Build](#build)
  - [Run](#run)
  - [Release](#release)
  - [Dependencies](#dependencies)
- [Project Structure](#-project-structure)
  - [Directories](#directories)
- [Deployment](#-deployment)
- [Contributing](#-contributing)
- [Acknowledgements](#-acknowledgements)
- [Support](#-support)
- [Project Engagement](#-project-engagement)
- [License](#-license)

## ✨ Capabilities

- **Personal portfolio:** presents featured open-source projects in a filterable gallery grouped into highlights, games, mods, tools, and libraries.
- **Open-source impact figures:** displays stars earned, followers, repositories, forks, pull requests, filed issues, and commits, retrieved from GitHub.
- **Per-project metrics:** each project view shows the combined star count and the latest release tag of the repositories that make up that project.
- **Professional profile:** describes the author's background, skill inventory with proficiency popups, and career chronology.
- **Funding overview:** a dedicated page that gathers the supported channels for funding the work.
- **Discoverability:** publishes canonical links, Open Graph and Twitter metadata, `robots.txt`, and `sitemap.xml`.
- **Responsive presentation:** adapts to desktop and mobile viewports, with animated reveals while scrolling.
- **Legacy URL support:** redirects `/resume` to the hosted resume document and `/fund.html` to the funding page.

## 🎯 Use Cases

- **Recruiters and prospective clients:** review the professional profile, skill inventory, and career chronology in a single page.
- **Open-source users:** discover the featured projects and reach their repositories, star counts, and latest releases.
- **Supporters:** locate the available channels for funding the ongoing work.

## 🚀 Usage

Open [https://hmlendea.go.ro](https://hmlendea.go.ro) in a browser. The home page is one scrolling document organised into these sections:
- **Impact:** figures for the open-source activity since December 2014
- **About:** a short professional introduction
- **Portfolio:** a filterable gallery of featured projects, each opening a detailed view with star counts and the latest release tag
- **Skills:** grouped technologies, each with an experience popup
- **Timeline:** the professional chronology
- **Contact:** the available means of getting in touch

The pages are rendered on the server; client-side scripts provide the filtering, the popups, the animated reveals, and the typewriter effect on the masthead.

The user interface is fed by JSON endpoints that proxy the GitHub API:
| Endpoint | Description |
|----------|-------------|
| `/api/github/profile` | Profile of the GitHub account. |
| `/api/github/pull-requests` | Number of pull requests authored. |
| `/api/github/issues` | Number of issues authored. |
| `/api/github/commits` | Number of commits authored. |
| `/api/github/repository-totals` | Aggregate star and fork counts across all public repositories. |
| `/api/github/repos/hmlendea/{repository}` | Details of one of the featured repositories. |
| `/api/github/repos/hmlendea/{repository}/releases/latest` | Latest release of one of the featured repositories. |

## ⚠️ Known Limitations

- GitHub figures are fetched through the public GitHub API and cached on the server for at least 30 minutes, so they can lag behind GitHub.
- The interface depends on third-party content delivery networks for Bootstrap, Font Awesome, jQuery, Animate.css, and Google Fonts; if those are unreachable, the presentation degrades.
- Project cover images are loaded directly from the default branch of the respective repository on GitHub.

## 📦 Installation

[![Obtain it from GitHub](https://raw.githubusercontent.com/hmlendea/readme-assets/master/badges/stores/github.png)](https://github.com/hmlendea/hmlendea.go.ro/releases)

### Manual Installation

Download the archive that matches the target platform from [GitHub Releases](https://github.com/hmlendea/hmlendea.go.ro/releases), extract it, and launch the `PersonalPresentationWebsite` executable (`.exe` on Windows).

The archives are self-contained builds, so no separate .NET runtime installation is required, and the application reports the URL it listens on once it has started.

### Verification

After the application has started, the console reports the addresses it is listening on, and opening one of those addresses in a browser renders the home page.

## ⚙️ Configuration

### Configuration Files

| File | Scope | Purpose |
|------|-------|---------|
| `PersonalPresentationWebsite/appsettings.json` | Application | Defines the GitHub cache duration, the log levels, and the accepted host headers. |

### Settings

The subsequent settings are recognised:
| Section | Key | Type | Default | Required | Description |
|---------|-----|------|---------|----------|-------------|
| `GitHub` | `CacheDurationInMinutes` | `integer` | `30` | No | Number of minutes for which GitHub API responses are cached. |
| `Logging:LogLevel` | `Default` | `string` | `Information` | No | Minimum log level for all categories. |
| `Logging:LogLevel` | `Microsoft.AspNetCore` | `string` | `Warning` | No | Minimum log level for the ASP.NET Core categories. |
| — | `AllowedHosts` | `string` | `*` | No | Semicolon-separated list of accepted `Host` header values. |

### Precedence

Configuration is resolved through the standard ASP.NET Core providers. From highest to lowest precedence:
- Command-line arguments
- Environment variables
- `appsettings.{Environment}.json`
- `appsettings.json`

### Reload Behaviour

`GitHub:CacheDurationInMinutes` is read once while starting and determines the expiry of the output cache, so a change requires restarting the application.

### Profiles and Scopes

Local development relies on the launch profiles declared in `PersonalPresentationWebsite/Properties/launchSettings.json`:
| Profile | Environment | URLs |
|---------|-------------|------|
| `http` | `Development` | `http://localhost:5296` |
| `https` | `Development` | `https://localhost:7198` and `http://localhost:5296` |

Outside the development environment the application registers the exception handler, the HTTP Strict Transport Security middleware, and the HTTPS redirection.

### Network Ports

| Port | Protocol | Direction | Purpose | Required |
|------|----------|-----------|---------|----------|
| `5296` | HTTP | Inbound | Local development through the `http` launch profile. | No |
| `7198` | HTTPS | Inbound | Local development through the `https` launch profile. | No |

### Validation

A value below `30` for `GitHub:CacheDurationInMinutes` causes the application to throw `ArgumentOutOfRangeException` while starting, so the process refuses to run until the value is corrected.

## 🧩 Compatibility

| Component | Supported Versions | Notes |
|-----------|--------------------|-------|
| Linux | `linux-x64`, `linux-arm`, `linux-arm64` | Self-contained release archives. |
| macOS | `osx-x64`, `osx-arm64` | Self-contained release archives. |
| Windows | `win-x64`, `win-arm64` | Self-contained release archives. |
| Browsers | Any current browser with JavaScript enabled | JavaScript powers the portfolio filtering, the skill popups, the typewriter effect, and the scroll animations. |

## 🔌 Integrations

| Integration | Compatibility | Purpose | Required |
|-------------|---------------|---------|----------|
| [GitHub REST API](https://docs.github.com/rest) | `api.github.com`, media type `application/vnd.github+json` | Supplies the profile statistics, the repository details, and the latest release tags shown in the impact and portfolio sections. | Yes |

## 🛡️ Privacy and Data

The website sets no cookies, runs no analytics, and persists no information concerning its visitors.

The public GitHub figures shown in the interface are retrieved server-side and held in a memory cache in order to limit the load on the GitHub API. The pages additionally load assets from third-party services, so the browser contacts jsDelivr, cdnjs, Google Fonts, and GitHub directly for stylesheets, scripts, typefaces, and images.

| Data | Purpose | Storage | Retention | Optional |
|------|---------|---------|-----------|----------|
| Public GitHub account and repository statistics | Render the open-source impact and portfolio figures | Server memory | At least 30 minutes after each retrieval | No |

## 🛠️ Development

### Requirements

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Setup

```bash
git clone https://github.com/hmlendea/hmlendea.go.ro.git
cd hmlendea.go.ro
dotnet restore PersonalPresentationWebsite/PersonalPresentationWebsite.csproj
```

### Build

```bash
dotnet build PersonalPresentationWebsite/PersonalPresentationWebsite.csproj
```

The equivalent `build`, `publish`, and `watch` tasks are defined in `.vscode/tasks.json`.

### Run

```bash
dotnet run --project PersonalPresentationWebsite/PersonalPresentationWebsite.csproj
```

The `http` launch profile applies by default, so the site becomes available at `http://localhost:5296`. For an automatic restart after every change:

```bash
dotnet watch run --project PersonalPresentationWebsite/PersonalPresentationWebsite.csproj
```

### Release

The repository includes `release.sh`, which delegates to the upstream deployment script used by the project maintainer.

```bash
bash ./release.sh 2.0.0
```

This script downloads and executes an external release helper from `https://raw.githubusercontent.com/hmlendea/deployment-scripts/master/release/dotnet/10.0.sh`.

**Note:** Piping into `bash` is an intensely controversial topic. Please review any external scripts before running them in your environment!

### Dependencies

| Package | Version | Scope | Purpose |
|---------|---------|-------|---------|
| `NuciWeb.HTTP` | 1.7.3 | Runtime | Provides the HTTP client factory used to query the GitHub REST API. |
| `Bootstrap` | 5.3.3 | Runtime (front end) | Layout, components, and utility styles. |
| `Start Bootstrap Freelancer` | 7.0.7 | Runtime (front end) | Base theme of the presentation layout. |
| `Font Awesome` | 6.7.2 | Runtime (front end) | Iconography. |
| `jQuery` | 3.7.1 | Runtime (front end) | Client-side helper library. |
| `jQuery Easing` | 1.12.1 | Runtime (front end) | Easing functions for the scroll animations. |
| `Animate.css` | 4.1.1 | Runtime (front end) | Entry animations. |
| `WOW.js` | 1.1.2 | Runtime (front end) | Reveals animated elements while scrolling. |

## 🗂️ Project Structure

The repository hosts a single ASP.NET Core project, `PersonalPresentationWebsite`, alongside the solution file, the release helper, and the editor configuration.

### Directories

| Directory | Purpose |
|-----------|---------|
| `PersonalPresentationWebsite/` | The ASP.NET Core project that renders and serves the website. |
| `PersonalPresentationWebsite/Components/` | Razor components, where `App.razor` is the HTML document, `Routes.razor` the router, and `Pages/` the individual pages. |
| `PersonalPresentationWebsite/Endpoints/` | Minimal API endpoints that proxy the GitHub REST API. |
| `PersonalPresentationWebsite/Properties/` | Launch profiles used during local development. |
| `PersonalPresentationWebsite/wwwroot/` | Static assets: stylesheets, scripts, images, `robots.txt`, `sitemap.xml`, and the resume redirect. |
| `PersonalPresentationWebsite/wwwroot/assets/img/funding/` | Logos of the supported funding platforms. |
| `PersonalPresentationWebsite/wwwroot/css/` | Custom stylesheets layered on top of the theme. |
| `PersonalPresentationWebsite/wwwroot/js/` | Progressive-enhancement scripts for filtering, popups, typewriter, and animation. |
| `.vscode/` | Editor tasks and launch configuration for building, publishing, and running the site. |

## 🚢 Deployment

The website is served at [https://hmlendea.go.ro](https://hmlendea.go.ro). Every release attaches self-contained archives for the supported platforms, and the deployed instance runs the published output of the matching archive.

The application redirects HTTP requests to HTTPS and, outside the development environment, emits HTTP Strict Transport Security headers, so the host must terminate TLS.

## 🤝 Contributing

You are welcome to submit any suggestion, feedback, or modification to this project.

When doing so, please:
- Maintain cross-platform compatibility
- Submit focused pull requests that conform to the existing code style
- Maintain your branch synchronised with `master`
- Revise the documentation when functionality changes
- Raise a new [issue](https://github.com/hmlendea/hmlendea.go.ro/issues) for problems or suggestions

## 🙏 Acknowledgements

- [Bootstrap](https://getbootstrap.com/): layout, components, and utility styles.
- [Start Bootstrap Freelancer](https://startbootstrap.com/theme/freelancer): the theme the presentation layout derives from.
- [Font Awesome](https://fontawesome.com/): iconography.
- [jQuery](https://jquery.com/) and [jQuery Easing](https://github.com/gdsmith/jquery.easing): client-side helpers used by the scripts.
- [Animate.css](https://animate.style/) and [WOW.js](https://wowjs.uk/): entry and scroll animations.
- [Google Fonts](https://fonts.google.com/): the Montserrat and Lato typefaces.

## 💬 Support

For support, please [open an issue](https://github.com/hmlendea/hmlendea.go.ro/issues).

## 💝 Project Engagement

Discovered a problem with Personal Presentation Website or have a suggestion for it? [Open an issue](https://github.com/hmlendea/hmlendea.go.ro/issues)!

If you find this project useful, consider starring ⭐️ it on GitHub!

## 📄 License

This project is being distributed under the `GNU General Public License v3.0` or later.
See [LICENSE](./LICENSE) for additional information.
