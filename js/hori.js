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