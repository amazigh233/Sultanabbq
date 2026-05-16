// ============================================
// SULTANA BBQ — Blazor JS Interop Bridge
// Handles communication between JS and Blazor
// ============================================

window.SultanaInterop = (() => {
    const loadedScripts = new Map();

    function whenIdle(callback) {
        if ('requestIdleCallback' in window) {
            window.requestIdleCallback(callback, { timeout: 1800 });
            return;
        }

        window.setTimeout(callback, 250);
    }

    function loadScript(src, id) {
        if (loadedScripts.has(src)) {
            return loadedScripts.get(src);
        }

        const existing = id ? document.getElementById(id) : document.querySelector(`script[src="${src}"]`);
        if (existing) {
            const promise = Promise.resolve();
            loadedScripts.set(src, promise);
            return promise;
        }

        const promise = new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = src;
            script.defer = true;
            if (id) script.id = id;
            script.onload = resolve;
            script.onerror = () => reject(new Error(`Could not load ${src}`));
            document.head.appendChild(script);
        });

        loadedScripts.set(src, promise);
        return promise;
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
        if (!el || !el.animate) return;

        el.animate(
            [
                { opacity: 0, transform: 'translateY(12px)' },
                { opacity: 1, transform: 'translateY(0)' }
            ],
            { duration: 220, easing: 'ease-out' }
        );
    }

    function initDeferredAnimations() {
        const start = () => whenIdle(async () => {
            if (!document.querySelector('.reveal, .reveal-left, .reveal-right, .review-card')) {
                return;
            }

            await loadScript('https://cdnjs.cloudflare.com/ajax/libs/gsap/3.12.5/gsap.min.js', 'gsap-lazy');
            await loadScript('https://cdnjs.cloudflare.com/ajax/libs/gsap/3.12.5/ScrollTrigger.min.js', 'scrolltrigger-lazy');
            await loadScript('js/animations.min.js', 'sultana-animations');

            if (window.SultanaAnimations) {
                window.SultanaAnimations.init();
            }
        });

        if (document.readyState === 'complete') {
            start();
            return;
        }

        window.addEventListener('load', start, { once: true });
    }

    async function initMenuScene(containerId, dotNetRef) {
        if (window.innerWidth <= 768) return;

        await loadScript('https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js', 'three-lazy');
        await loadScript('https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js', 'orbit-controls-lazy');
        await loadScript('js/scene.min.js', 'sultana-scene');

        if (window.SultanaScene) {
            window.SultanaScene.init(containerId, dotNetRef);
        }
    }

    return {
        initNavScroll,
        scrollToElement,
        openUrl,
        animateBuilderStep,
        initDeferredAnimations,
        initMenuScene
    };
})();
