// Horizontally-scrolling tab strip for EditProfileQuestions, replicating the legacy
// Profiles.WebControls.TabStrip "view more tabs" left/right carousel controls.
(function () {
    'use strict';

    function updateOverflowState(carousel, scroller) {
        var isOverflowing = scroller.scrollWidth > scroller.clientWidth + 1;
        carousel.classList.toggle('app-tab-carousel--overflowing', isOverflowing);
    }

    function initialiseTabCarousels() {
        document.querySelectorAll('.app-tab-carousel').forEach(function (carousel) {
            var scroller = carousel.querySelector('.app-tab-carousel__scroll');
            var leftButton = carousel.querySelector('[data-tab-scroll="left"]');
            var rightButton = carousel.querySelector('[data-tab-scroll="right"]');

            if (!scroller) {
                return;
            }

            updateOverflowState(carousel, scroller);
            window.addEventListener('resize', function () {
                updateOverflowState(carousel, scroller);
            });

            if (leftButton) {
                leftButton.addEventListener('click', function () {
                    scroller.scrollBy({ left: -200, behavior: 'smooth' });
                });
            }

            if (rightButton) {
                rightButton.addEventListener('click', function () {
                    scroller.scrollBy({ left: 200, behavior: 'smooth' });
                });
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialiseTabCarousels);
    } else {
        initialiseTabCarousels();
    }
})();
