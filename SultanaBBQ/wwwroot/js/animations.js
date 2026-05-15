// ============================================
// SULTANA BBQ — GSAP Scroll Animations
// Award-winning scroll-triggered animations
// ============================================

window.SultanaAnimations = (() => {

    function init() {
        if (typeof gsap === 'undefined' || typeof ScrollTrigger === 'undefined') {
            console.warn('GSAP or ScrollTrigger not loaded');
            return;
        }

        ScrollTrigger.getAll().forEach(t => t.kill());
        gsap.globalTimeline.clear();

        const particles = document.getElementById('hero-particles');
        if (particles) particles.innerHTML = '';

        gsap.registerPlugin(ScrollTrigger);

        // Scroll-triggered reveal animations
        animateReveals();

        // Parallax effects
        animateParallax();

        // Hero particles
        createHeroParticles();
    }

    function animateReveals() {
        // General reveal elements
        gsap.utils.toArray('.reveal').forEach(elem => {
            gsap.fromTo(elem,
                { opacity: 0, y: 60 },
                {
                    opacity: 1,
                    y: 0,
                    duration: 1,
                    ease: 'power3.out',
                    scrollTrigger: {
                        trigger: elem,
                        start: 'top 85%',
                        end: 'bottom 20%',
                        toggleActions: 'play none none reverse'
                    }
                }
            );
        });

        // Left reveal
        gsap.utils.toArray('.reveal-left').forEach(elem => {
            gsap.fromTo(elem,
                { opacity: 0, x: -60 },
                {
                    opacity: 1,
                    x: 0,
                    duration: 1,
                    ease: 'power3.out',
                    scrollTrigger: {
                        trigger: elem,
                        start: 'top 85%',
                        toggleActions: 'play none none reverse'
                    }
                }
            );
        });

        // Right reveal
        gsap.utils.toArray('.reveal-right').forEach(elem => {
            gsap.fromTo(elem,
                { opacity: 0, x: 60 },
                {
                    opacity: 1,
                    x: 0,
                    duration: 1,
                    ease: 'power3.out',
                    scrollTrigger: {
                        trigger: elem,
                        start: 'top 85%',
                        toggleActions: 'play none none reverse'
                    }
                }
            );
        });

        // Review cards staggered
        gsap.utils.toArray('.review-card').forEach((card, i) => {
            gsap.fromTo(card,
                { opacity: 0, y: 40, scale: 0.95 },
                {
                    opacity: 1,
                    y: 0,
                    scale: 1,
                    duration: 0.8,
                    delay: i * 0.15,
                    ease: 'power3.out',
                    scrollTrigger: {
                        trigger: card,
                        start: 'top 90%',
                        toggleActions: 'play none none reverse'
                    }
                }
            );
        });

        // Menu category buttons staggered
        gsap.utils.toArray('.menu-category-btn').forEach((btn, i) => {
            gsap.fromTo(btn,
                { opacity: 0, y: 20 },
                {
                    opacity: 1,
                    y: 0,
                    duration: 0.5,
                    delay: i * 0.08,
                    ease: 'power2.out',
                    scrollTrigger: {
                        trigger: '.menu-categories',
                        start: 'top 95%',
                        toggleActions: 'play none none reverse'
                    }
                }
            );
        });
    }

    function animateParallax() {
        // About decoration parallax
        if (document.querySelector('.about-decoration') && document.querySelector('.about')) {
            gsap.to('.about-decoration', {
                rotation: 360,
                ease: 'none',
                scrollTrigger: {
                    trigger: '.about',
                    start: 'top bottom',
                    end: 'bottom top',
                    scrub: 1
                }
            });
        }

        // Marquee speed change on scroll
        if (document.querySelector('.marquee-inner') && document.querySelector('.marquee')) {
            gsap.to('.marquee-inner', {
                x: '-=200',
                ease: 'none',
                scrollTrigger: {
                    trigger: '.marquee',
                    start: 'top bottom',
                    end: 'bottom top',
                    scrub: 0.5
                }
            });
        }
    }

    function createHeroParticles() {
        const container = document.getElementById('hero-particles');
        if (!container) return;

        // Create floating smoke/ember particles with CSS
        for (let i = 0; i < 20; i++) {
            const particle = document.createElement('div');
            particle.style.cssText = `
                position: absolute;
                width: ${2 + Math.random() * 4}px;
                height: ${2 + Math.random() * 4}px;
                background: radial-gradient(circle, rgba(212, 168, 83, ${0.3 + Math.random() * 0.4}), transparent);
                border-radius: 50%;
                left: ${Math.random() * 100}%;
                top: ${Math.random() * 100}%;
                pointer-events: none;
            `;
            container.appendChild(particle);

            // Animate with GSAP
            gsap.to(particle, {
                y: -(100 + Math.random() * 300),
                x: (Math.random() - 0.5) * 100,
                opacity: 0,
                duration: 3 + Math.random() * 4,
                repeat: -1,
                delay: Math.random() * 3,
                ease: 'power1.out'
            });
        }
    }

    return { init };
})();
