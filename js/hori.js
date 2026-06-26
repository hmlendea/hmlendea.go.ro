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

    function impactFetch(url) {
        var key = 'gh_impact_' + url;
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

    function fmt(n) {
        if (n === null || n === undefined) return '?';
        return n >= 1000 ? (n / 1000).toFixed(1) + 'k' : String(n);
    }

    function fetchAllRepos() {
        function page(p, acc) {
            return impactFetch('https://api.github.com/users/hmlendea/repos?per_page=100&page=' + p)
                .then(function(repos) {
                    if (!repos || repos.length === 0) return acc;
                    acc = acc.concat(repos);
                    return repos.length === 100 ? page(p + 1, acc) : acc;
                });
        }
        return page(1, []);
    }

    function loadImpactStats() {
        // User profile: repos + followers
        impactFetch('https://api.github.com/users/hmlendea').then(function(u) {
            if (!u) return;
            document.getElementById('impact-repos').textContent = fmt(u.public_repos);
            document.getElementById('impact-followers').textContent = fmt(u.followers);
        });

        // Stars + forks across all repos
        fetchAllRepos().then(function(repos) {
            var stars = repos.reduce(function(s, r) { return s + (r.stargazers_count || 0); }, 0);
            var forks = repos.reduce(function(s, r) { return s + (r.forks_count || 0); }, 0);
            document.getElementById('impact-stars').textContent = fmt(stars);
            document.getElementById('impact-forks').textContent = fmt(forks);
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
        impactFetch('https://api.github.com/search/commits?q=author:hmlendea&per_page=1').then(function(d) {
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