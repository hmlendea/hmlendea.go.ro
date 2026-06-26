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

// GitHub Stars Badges
(function() {
    var repoMap = {
        '#portfolioModalNarivia':                ['hmlendea/narivia'],
        '#portfolioModalMoreCulturalNames':      ['hmlendea/more-cultural-names'],
        '#portfolioModalGeForceNowElectron':     ['hmlendea/gfn-electron'],
        '#portfolioModalPersonalLogManager':     ['hmlendea/personal-log-manager', 'hmlendea/personal-log-manager-client'],
        '#portfolioModalIptvPlaylistAggregator': ['hmlendea/iptv-playlist-aggregator'],
        '#portfolioModalUniversalNameGenerator': ['hmlendea/universal-name-generator'],
        '#portfolioModalDuolingoDesktop':        ['hmlendea/duolingo-desktop'],
        '#portfolioModalBitwardenVaultManager':  ['hmlendea/bitwarden-vault-manager'],
        '#portfolioModalTransliterationAPI':     ['hmlendea/transliteration-api'],
        '#portfolioModalMemoryBlocks':           ['hmlendea/memoryblocks'],
        '#portfolioModalSokoGrump':              ['hmlendea/sokogrump'],
        '#portfolioModalMinesweeper':            ['hmlendea/minesweeper'],
        '#portfolioModalNuciLog':                ['hmlendea/nucilog', 'hmlendea/nucilog.core'],
        '#portfolioModalBackgammon':             ['hmlendea/backgammon-by-horatiu'],
        '#portfolioModalNuciAPI':                ['hmlendea/nuciapi', 'hmlendea/nuciapi.client', 'hmlendea/nuciapi.controllers', 'hmlendea/nuciapi.middleware', 'hmlendea/nuciapi.middleware.exceptionhandling', 'hmlendea/nuciapi.middleware.logging', 'hmlendea/nuciapi.middleware.security'],
        '#portfolioModalNuciWebAutomation':      ['hmlendea/nuciweb.automation', 'hmlendea/nuciweb.automation.selenium', 'hmlendea/nuciweb.automation.playwright']
    };

    Object.keys(repoMap).forEach(function(modalSelector) {
        var modal = document.querySelector(modalSelector);
        if (!modal) return;
        var repos = repoMap[modalSelector];

        var divider = modal.querySelector('.divider-custom');
        if (!divider) return;

        var badge = document.createElement('div');
        badge.className = 'gh-stars-badge';
        badge.innerHTML = '<i class="fas fa-star"></i> <span class="gh-stars-count">…</span> stars on GitHub';
        divider.insertAdjacentElement('afterend', badge);

        var countEl = badge.querySelector('.gh-stars-count');
        var promises = repos.map(function(repo) {
            return fetch('https://api.github.com/repos/' + repo)
                .then(function(r) { return r.ok ? r.json() : {}; })
                .then(function(d) { return d.stargazers_count || 0; })
                .catch(function() { return 0; });
        });

        Promise.all(promises).then(function(counts) {
            var total = counts.reduce(function(a, b) { return a + b; }, 0);
            countEl.textContent = total >= 1000 ? (total / 1000).toFixed(1) + 'k' : total;
            badge.classList.add('loaded');
        });
    });
})();