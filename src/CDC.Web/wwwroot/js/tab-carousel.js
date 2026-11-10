// Horizontally-scrolling tab strip for the profile section pages, replicating the legacy
// Profiles.WebControls.TabStrip "view more tabs" left/right carousel controls.
(function () {
    'use strict';

    var SCROLL_STEP = 200;

    function updateOverflowState(carousel, scroller) {
        var isOverflowing = scroller.scrollWidth > scroller.clientWidth + 1;
        carousel.classList.toggle('app-tab-carousel--overflowing', isOverflowing);
        return isOverflowing;
    }

    // As the legacy TabButtonsShowHide() did, shows the selected tab when the tabs do not all fit.
    function revealSelectedTab(scroller) {
        var selected = scroller.querySelector('.govuk-tabs__list-item--selected');

        if (!selected) {
            return;
        }

        var scrollerBox = scroller.getBoundingClientRect();
        var selectedBox = selected.getBoundingClientRect();

        if (selectedBox.left < scrollerBox.left) {
            scroller.scrollLeft -= scrollerBox.left - selectedBox.left;
        } else if (selectedBox.right > scrollerBox.right) {
            scroller.scrollLeft += selectedBox.right - scrollerBox.right;
        }
    }

    function scrollTabs(scroller, direction) {
        var reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
        scroller.scrollBy({ left: direction * SCROLL_STEP, behavior: reduceMotion ? 'auto' : 'smooth' });
    }

    function initialiseTabCarousel(carousel) {
        var scroller = carousel.querySelector('.app-tab-carousel__scroll');
        var leftButton = carousel.querySelector('[data-tab-scroll="left"]');
        var rightButton = carousel.querySelector('[data-tab-scroll="right"]');

        if (!scroller) {
            return;
        }

        var wasOverflowing = false;

        function refresh() {
            var isOverflowing = updateOverflowState(carousel, scroller);

            if (isOverflowing && !wasOverflowing) {
                revealSelectedTab(scroller);
            }

            wasOverflowing = isOverflowing;
        }

        refresh();

        if (window.ResizeObserver) {
            new ResizeObserver(refresh).observe(scroller);
        } else {
            window.addEventListener('resize', refresh);
        }

        // The tabs get wider once the page font has loaded, which can tip them into overflowing.
        void document.fonts?.ready.then(function () {
            refresh();

            if (wasOverflowing) {
                revealSelectedTab(scroller);
            }
        });

        if (leftButton) {
            leftButton.addEventListener('click', function () {
                scrollTabs(scroller, -1);
            });
        }

        if (rightButton) {
            rightButton.addEventListener('click', function () {
                scrollTabs(scroller, 1);
            });
        }
    }

    function initialiseTabCarousels() {
        document.querySelectorAll('.app-tab-carousel').forEach(initialiseTabCarousel);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialiseTabCarousels);
    } else {
        initialiseTabCarousels();
    }
})();
