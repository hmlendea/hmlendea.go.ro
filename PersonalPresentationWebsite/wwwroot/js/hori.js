(function($) {
    "use strict"; // Start of use strict

    // Dynamic copyright year
    var el = document.getElementById('copyright-year');
    if (el) el.textContent = new Date().getFullYear();

    // Initialize WOW.js Scrolling Animations, with IntersectionObserver fallback
    var wowLoaded = false;
    try {
        if (typeof WOW !== 'undefined') {
            new WOW().init();
            wowLoaded = true;
        }
    } catch (e) {}

    if (!wowLoaded) {
        var wowEls = document.querySelectorAll('.wow');
        if ('IntersectionObserver' in window) {
            var io = new IntersectionObserver(function(entries, obs) {
                entries.forEach(function(entry) {
                    if (entry.isIntersecting) {
                        entry.target.style.visibility = 'visible';
                        obs.unobserve(entry.target);
                    }
                });
            }, { threshold: 0.1 });
            wowEls.forEach(function(el) { io.observe(el); });
        } else {
            wowEls.forEach(function(el) { el.style.visibility = 'visible'; });
        }
    }

})(jQuery); // End of use strict

function fetchGitHub(resource) {
    return fetch('/api/github/' + resource)
        .then(function(r) { return r.ok ? r.json() : null; })
        .catch(function() { return null; });
}

// GitHub Stars & Version Badges — fetched lazily, cached server-side
(function() {
    var repoMap = {
        '#portfolioModalNarivia':                [['hmlendea/narivia']],
        '#portfolioModalMoreCulturalNames':      [['hmlendea/more-cultural-names']],
        '#portfolioModalGeForceNowElectron':     [['hmlendea/gfn-electron']],
        '#portfolioModalPersonalLogManager':     [['hmlendea/personal-log-manager', 'API'], ['hmlendea/personal-log-manager-client', 'Client']],
        '#portfolioModalIptvPlaylistAggregator': [['hmlendea/iptv-playlist-aggregator']],
        '#portfolioModalUniversalNameGenerator': [['hmlendea/universal-name-generator']],
        '#portfolioModalDuolingoDesktop':        [['hmlendea/duolingo-desktop']],
        '#portfolioModalBitwardenVaultManager':  [['hmlendea/bitwarden-vault-manager']],
        '#portfolioModalTransliterationAPI':     [['hmlendea/transliteration-api']],
        '#portfolioModalMemoryBlocks':           [['hmlendea/memoryblocks']],
        '#portfolioModalSokoGrump':              [['hmlendea/sokogrump']],
        '#portfolioModalMinesweeper':            [['hmlendea/minesweeper']],
        '#portfolioModalNuciLog':                [['hmlendea/nucilog', 'NuciLog'], ['hmlendea/nucilog.core', 'NuciLog.Core']],
        '#portfolioModalBackgammon':             [['hmlendea/backgammon-by-horatiu']],
        '#portfolioModalNuciAPI':                [['hmlendea/nuciapi', 'NuciAPI'], ['hmlendea/nuciapi.client', 'Client'], ['hmlendea/nuciapi.controllers', 'Controllers'], ['hmlendea/nuciapi.middleware', 'Middleware'], ['hmlendea/nuciapi.middleware.exceptionhandling', 'ExceptionHandling'], ['hmlendea/nuciapi.middleware.logging', 'Logging'], ['hmlendea/nuciapi.middleware.security', 'Security']],
        '#portfolioModalNuciWebAutomation':      [['hmlendea/nuciweb.automation', 'NuciWeb.Automation'], ['hmlendea/nuciweb.automation.selenium', 'Selenium'], ['hmlendea/nuciweb.automation.playwright', 'Playwright']]
    };

    Object.keys(repoMap).forEach(function(modalSelector) {
        var modal = document.querySelector(modalSelector);
        if (!modal) return;
        var repos = repoMap[modalSelector];
        var repoSlugs = repos.map(function(r) { return r[0]; });
        var multiRepo = repos.length > 1;

        // Inject a shared badge row after the divider
        var divider = modal.querySelector('.divider-custom');
        if (!divider) return;
        var row = document.createElement('div');
        row.className = 'gh-badges-row';
        divider.insertAdjacentElement('afterend', row);

        // Stars badge
        var starsBadge = document.createElement('div');
        starsBadge.className = 'gh-stars-badge';
        starsBadge.innerHTML = '<i class="fas fa-star"></i> <span class="gh-stars-count">\u2026</span> stars on GitHub';
        row.appendChild(starsBadge);
        var starsCountEl = starsBadge.querySelector('.gh-stars-count');

        // Version badges — one per repo, labelled when multi-repo
        var versionBadges = repos.map(function(r) {
            var repo = r[0];
            var label = multiRepo ? (r[1] || repo.split('/')[1]) : '';
            var vbadge = document.createElement('div');
            vbadge.className = 'gh-version-badge';
            vbadge.innerHTML = '<i class="fas fa-tag"></i> <span>' + (label ? label + ': ' : '') + '\u2026</span>';
            row.appendChild(vbadge);
            return { repo: repo, badge: vbadge, label: label };
        });

        // Fetch everything on first open
        modal.addEventListener('show.bs.modal', function onFirstOpen() {
            modal.removeEventListener('show.bs.modal', onFirstOpen);

            // Stars
            Promise.all(repoSlugs.map(function(repo) {
                return fetchGitHub('repos/' + repo)
                    .then(function(d) { return (d && d.stargazers_count) || 0; });
            })).then(function(counts) {
                var total = counts.reduce(function(a, b) { return a + b; }, 0);
                if (total === 0) return;
                starsCountEl.textContent = total >= 1000 ? (total / 1000).toFixed(1) + 'k' : total;
                starsBadge.classList.add('loaded');
            });

            // Versions
            versionBadges.forEach(function(item) {
                fetchGitHub('repos/' + item.repo + '/releases/latest')
                    .then(function(d) {
                        if (d && d.tag_name) {
                            item.badge.querySelector('span').textContent = (item.label ? item.label + ': ' : '') + d.tag_name;
                            item.badge.classList.add('loaded');
                        } else {
                            item.badge.remove();
                        }
                    });
            });
        });
    });
})();

// Open Source Impact Stats — fetched lazily when section scrolls into view
(function() {
    function fmt(n) {
        if (n === null || n === undefined) return '?';
        return n >= 1000 ? (n / 1000).toFixed(1) + 'k' : String(n);
    }

    function loadImpactStats() {
        // User profile: repos + followers
        fetchGitHub('profile').then(function(u) {
            if (!u) return;
            document.getElementById('impact-repos').textContent = fmt(u.public_repos);
            document.getElementById('impact-followers').textContent = fmt(u.followers);
        });

        // Stars + forks across all repos
        fetchGitHub('repository-totals').then(function(totals) {
            if (!totals) return;
            document.getElementById('impact-stars').textContent = fmt(totals.stars);
            document.getElementById('impact-forks').textContent = fmt(totals.forks);
        });

        // Pull requests authored
        fetchGitHub('pull-requests').then(function(d) {
            if (d && d.total_count !== undefined)
                document.getElementById('impact-prs').textContent = fmt(d.total_count);
        });

        // Issues filed
        fetchGitHub('issues').then(function(d) {
            if (d && d.total_count !== undefined)
                document.getElementById('impact-issues').textContent = fmt(d.total_count);
        });

        // Total commits
        fetchGitHub('commits').then(function(d) {
            if (d && d.total_count !== undefined)
                document.getElementById('impact-commits').textContent = fmt(d.total_count);
        });
    }

    var section = document.getElementById('impact');
    if (!section) return;

    if ('IntersectionObserver' in window) {
        var observer = new IntersectionObserver(function(entries, obs) {
            if (entries[0].isIntersecting) {
                obs.disconnect();
                loadImpactStats();
            }
        }, { threshold: 0.1 });
        observer.observe(section);
    } else {
        loadImpactStats();
    }
})();
