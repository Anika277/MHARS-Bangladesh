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
