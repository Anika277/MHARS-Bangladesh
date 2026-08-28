// MHARS UI: theme toggle, mobile simulator, live clock

function toggleTheme() {
    const html = document.documentElement;
    const newTheme = (html.getAttribute('data-theme') || 'light') === 'dark' ? 'light' : 'dark';
    html.setAttribute('data-theme', newTheme);
    localStorage.setItem('mhars-theme', newTheme);

    const themeLabel = document.getElementById('themeLabel');
    const themePillIcon = document.getElementById('themePillIcon');

    if (newTheme === 'dark') {
        if (themeLabel) themeLabel.textContent = 'Light Mode';
        if (themePillIcon) themePillIcon.textContent = '☀️';
    } else {
        if (themeLabel) themeLabel.textContent = 'Dark Mode';
        if (themePillIcon) themePillIcon.textContent = '🌙';
    }
}

function toggleDeviceMode() {
    const wrapper = document.getElementById('viewWrapper');
    const btnText = document.getElementById('deviceBtnText');
    if (!wrapper) return;
    if (wrapper.classList.contains('mobile-mode')) {
        wrapper.classList.remove('mobile-mode');
        if (btnText) btnText.textContent = 'Mobile App Simulator';
    } else {
        wrapper.classList.add('mobile-mode');
        if (btnText) btnText.textContent = 'Desktop View';
    }
    if (window.map) {
        setTimeout(() => { window.map.invalidateSize(); }, 300);
    }
}

(function initTheme() {
    const saved = localStorage.getItem('mhars-theme');
    if (saved === 'dark') {
        document.documentElement.setAttribute('data-theme', 'dark');
        const themeLabel = document.getElementById('themeLabel');
        const themePillIcon = document.getElementById('themePillIcon');
        if (themeLabel) themeLabel.textContent = 'Light Mode';
        if (themePillIcon) themePillIcon.textContent = '☀️';
    }
})();

// Live Clock (BST UTC+6)
function updateClock() {
    const el = document.getElementById('liveClock');
    if (!el) return;
    const now = new Date();
    const utc = now.getTime() + (now.getTimezoneOffset() * 60000);
    const bstDate = new Date(utc + (3600000 * 6));
    const hours = String(bstDate.getHours()).padStart(2, '0');
    const minutes = String(bstDate.getMinutes()).padStart(2, '0');
    const seconds = String(bstDate.getSeconds()).padStart(2, '0');
    el.textContent = `${hours}:${minutes}:${seconds} BST (UTC+6)`;
}
setInterval(updateClock, 1000);
updateClock();

// ── MOTION LAYER: scroll reveals, count-ups, sticky nav reaction ──
// Animates real server-rendered markup via [data-reveal] / [data-reveal-group] /
// [data-countup]. Everything stays visible if GSAP is unavailable or the user
// prefers reduced motion.

function initMharsMotion() {
    const prefersReduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (!window.gsap || !window.ScrollTrigger) {
        return;
    }

    gsap.registerPlugin(ScrollTrigger);

    if (prefersReduced) {
        gsap.globalTimeline.timeScale(1000);
    }

    gsap.from('main', { opacity: 0, y: 10, duration: 0.55, ease: 'power2.out' });

    gsap.utils.toArray('[data-reveal]').forEach((el) => {
        const delay = parseFloat(el.getAttribute('data-reveal-delay') || '0') || 0;
        gsap.fromTo(el,
            { opacity: 0, y: 26 },
            {
                opacity: 1, y: 0, duration: 0.8, ease: 'power3.out', delay,
                scrollTrigger: { trigger: el, start: 'top 88%', once: true }
            });
    });

    gsap.utils.toArray('[data-reveal-group]').forEach((group) => {
        const items = Array.prototype.slice.call(group.children);
        items.forEach((el, i) => {
            gsap.fromTo(el,
                { opacity: 0, y: 18 },
                {
                    opacity: 1, y: 0, duration: 0.6, ease: 'power3.out', delay: (i % 6) * 0.07,
                    scrollTrigger: { trigger: el, start: 'top 90%', once: true }
                });
        });
    });

    gsap.utils.toArray('[data-countup]').forEach((el) => {
        const target = parseInt((el.textContent || '').replace(/[^\d]/g, ''), 10) || 0;
        const counter = { v: 0 };
        el.textContent = '0';
        gsap.to(counter, {
            v: target, duration: 1.8, ease: 'power2.out',
            onUpdate() {
                el.textContent = Math.round(counter.v).toLocaleString('en-US');
            },
            scrollTrigger: { trigger: el, start: 'top 90%', once: true }
        });
    });

    const nav = document.querySelector('.main-nav');
    const progress = document.getElementById('scrollProgress');
    if (nav) {
        const onNavScroll = () => {
            const scrolled = (window.scrollY || 0) > 8;
            nav.classList.toggle('nav-scrolled', scrolled);
            if (progress) {
                const doc = document.documentElement;
                const max = doc.scrollHeight - doc.clientHeight;
                progress.style.transform = 'scaleX(' + (max > 0 ? doc.scrollTop / max : 0) + ')';
            }
        };
        onNavScroll();
        window.addEventListener('scroll', onNavScroll, { passive: true });
    }

    ScrollTrigger.refresh();
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initMharsMotion);
} else {
    initMharsMotion();
}
