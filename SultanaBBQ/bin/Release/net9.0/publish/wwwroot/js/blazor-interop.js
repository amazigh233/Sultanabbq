// ============================================
// SULTANA BBQ — Blazor JS Interop Bridge
// Handles communication between JS and Blazor
// ============================================

window.SultanaInterop = (() => {

    function hidePreloader() {
        const preloader = document.getElementById('preloader');
        if (preloader) {
            setTimeout(() => {
                preloader.classList.add('hidden');
                setTimeout(() => {
                    preloader.remove();
                }, 800);
            }, 1200);
        }
    }

    function initNavScroll(dotNetRef) {
        let lastScrolled = false;

        window.addEventListener('scroll', () => {
            const scrolled = window.scrollY > 80;
            if (scrolled !== lastScrolled) {
                lastScrolled = scrolled;
                dotNetRef.invokeMethodAsync('SetScrolled', scrolled);
            }
        }, { passive: true });
    }

    function scrollToElement(elementId) {
        const element = document.getElementById(elementId);
        if (element) {
            element.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }

    function openUrl(url) {
        window.open(url, '_blank', 'noopener,noreferrer');
    }

    function animateBuilderStep(selector) {
        const el = document.querySelector(selector);
        if (!el || typeof gsap === 'undefined') return;
        gsap.fromTo(el,
            { opacity: 0, y: 18 },
            { opacity: 1, y: 0, duration: 0.45, ease: 'power2.out' }
        );
    }

    return {
        hidePreloader,
        initNavScroll,
        scrollToElement,
        openUrl,
        animateBuilderStep
    };
})();
