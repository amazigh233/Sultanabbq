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

    return {
        hidePreloader,
        initNavScroll,
        scrollToElement
    };
})();
