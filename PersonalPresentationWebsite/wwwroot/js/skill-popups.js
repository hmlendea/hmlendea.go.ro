(function() {
    function adjustPopup(tag) {
        var popup = tag.querySelector('.skill-popup');
        if (!popup) return;
        popup.style.left = '';
        popup.style.transform = '';
        popup.style.removeProperty('--popup-caret');
        var tagRect = tag.getBoundingClientRect();
        var popupWidth = popup.offsetWidth || 215;
        var margin = 8;
        var vpWidth = window.innerWidth;
        var defaultCenter = tagRect.left + tagRect.width / 2;
        var leftEdge = defaultCenter - popupWidth / 2;
        var rightEdge = defaultCenter + popupWidth / 2;
        var newX = null;
        if (leftEdge < margin) {
            newX = margin - tagRect.left;
        } else if (rightEdge > vpWidth - margin) {
            newX = vpWidth - margin - popupWidth - tagRect.left;
        }
        if (newX !== null) {
            popup.style.left = newX + 'px';
            popup.style.transform = 'translateX(0)';
            var caretPct = (tagRect.left + tagRect.width / 2 - (tagRect.left + newX)) / popupWidth * 100;
            caretPct = Math.max(8, Math.min(92, caretPct));
            popup.style.setProperty('--popup-caret', caretPct + '%');
        }
    }

    function resetPopup(tag) {
        var popup = tag.querySelector('.skill-popup');
        if (!popup) return;
        popup.style.left = '';
        popup.style.transform = '';
        popup.style.removeProperty('--popup-caret');
    }

    document.querySelectorAll('.skill-tag').forEach(function(tag) {
        tag.addEventListener('mouseenter', function() { adjustPopup(tag); });
        tag.addEventListener('mouseleave', function() { resetPopup(tag); });
        tag.addEventListener('click', function(e) {
            var isActive = tag.classList.contains('active');
            document.querySelectorAll('.skill-tag.active').forEach(function(t) {
                t.classList.remove('active');
                resetPopup(t);
            });
            if (!isActive) {
                tag.classList.add('active');
                adjustPopup(tag);
                e.stopPropagation();
            }
        });
    });

    document.addEventListener('click', function() {
        document.querySelectorAll('.skill-tag.active').forEach(function(t) {
            t.classList.remove('active');
            resetPopup(t);
        });
    });
})();
