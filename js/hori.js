(function($) {
    "use strict"; // Start of use strict

    // Initialize WOW.js Scrolling Animations, with IntersectionObserver fallback
    var wowLoaded = false;
    try {
        if (typeof WOW !== 'undefined') {
            new WOW().init();
            wowLoaded = true;
        }
    } catch (e) {
        wowLoaded = false;
    }

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

$(".filter").not('.highlights').hide();

// GitHub Stars & Version Badges — fetched lazily on first modal open, cached in localStorage for 1 hour
(function() {
    var TTL = 60 * 60 * 1000;

    function ghFetch(url) {
        var key = 'gh_cache_' + url;
        try {
            var cached = JSON.parse(localStorage.getItem(key));
            if (cached && (Date.now() - cached.ts < TTL)) return Promise.resolve(cached.v);
        } catch (e) {}
        return fetch(url)
            .then(function(r) { return r.ok ? r.json() : null; })
            .then(function(data) {
                try { localStorage.setItem(key, JSON.stringify({ ts: Date.now(), v: data })); } catch (e) {}
                return data;
            })
            .catch(function() { return null; });
    }

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
            if (starsBadge) {
                Promise.all(repoSlugs.map(function(repo) {
                    return ghFetch('https://api.github.com/repos/' + repo)
                        .then(function(d) { return (d && d.stargazers_count) || 0; });
                })).then(function(counts) {
                    var total = counts.reduce(function(a, b) { return a + b; }, 0);
                    if (total === 0) return;
                    starsCountEl.textContent = total >= 1000 ? (total / 1000).toFixed(1) + 'k' : total;
                    starsBadge.classList.add('loaded');
                });
            }

            // Versions
            versionBadges.forEach(function(item) {
                ghFetch('https://api.github.com/repos/' + item.repo + '/releases/latest')
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
    var TTL = 60 * 60 * 1000;

    function impactFetch(url, headers) {
        var key = 'gh_impact_' + url;
        try {
            var cached = JSON.parse(localStorage.getItem(key));
            if (cached && (Date.now() - cached.ts < TTL)) return Promise.resolve(cached.v);
        } catch (e) {}
        return fetch(url, headers ? { headers: headers } : undefined)
            .then(function(r) { return r.ok ? r.json() : null; })
            .then(function(data) {
                try { localStorage.setItem(key, JSON.stringify({ ts: Date.now(), v: data })); } catch (e) {}
                return data;
            })
            .catch(function() { return null; });
    }

    function fmt(n) {
        if (n === null || n === undefined) return '?';
        return n >= 1000 ? (n / 1000).toFixed(1) + 'k' : String(n);
    }

    function fetchAllRepos() {
        var cacheKey = 'gh_impact_allrepos_totals';
        try {
            var cached = JSON.parse(localStorage.getItem(cacheKey));
            if (cached && (Date.now() - cached.ts < TTL)) return Promise.resolve(cached.v);
        } catch (e) {}
        function page(p, stars, forks) {
            return fetch('https://api.github.com/users/hmlendea/repos?per_page=100&page=' + p)
                .then(function(r) { return r.ok ? r.json() : []; })
                .then(function(repos) {
                    if (!repos || repos.length === 0) return { stars: stars, forks: forks };
                    repos.forEach(function(r) { stars += r.stargazers_count || 0; forks += r.forks_count || 0; });
                    return repos.length === 100 ? page(p + 1, stars, forks) : { stars: stars, forks: forks };
                })
                .catch(function() { return { stars: stars, forks: forks }; });
        }
        return page(1, 0, 0).then(function(totals) {
            try { localStorage.setItem(cacheKey, JSON.stringify({ ts: Date.now(), v: totals })); } catch (e) {}
            return totals;
        });
    }

    function loadImpactStats() {
        // User profile: repos + followers
        impactFetch('https://api.github.com/users/hmlendea').then(function(u) {
            if (!u) return;
            document.getElementById('impact-repos').textContent = fmt(u.public_repos);
            document.getElementById('impact-followers').textContent = fmt(u.followers);
        });

        // Stars + forks across all repos
        fetchAllRepos().then(function(totals) {
            document.getElementById('impact-stars').textContent = fmt(totals.stars);
            document.getElementById('impact-forks').textContent = fmt(totals.forks);
        });

        // Pull requests authored
        impactFetch('https://api.github.com/search/issues?q=author:hmlendea+type:pr&per_page=1').then(function(d) {
            if (d && d.total_count !== undefined)
                document.getElementById('impact-prs').textContent = fmt(d.total_count);
        });

        // Issues filed
        impactFetch('https://api.github.com/search/issues?q=author:hmlendea+type:issue&per_page=1').then(function(d) {
            if (d && d.total_count !== undefined)
                document.getElementById('impact-issues').textContent = fmt(d.total_count);
        });

        // Total commits
        impactFetch('https://api.github.com/search/commits?q=author:hmlendea&per_page=1', { 'Accept': 'application/vnd.github.cloak-preview' }).then(function(d) {
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
