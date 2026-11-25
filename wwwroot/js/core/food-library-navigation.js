(() => {
    const menu = document.getElementById('food-library-nav');
    if (!menu) return;
    const saved = sessionStorage.getItem('momentum-food-library-nav');
    if (saved !== null) menu.open = saved === 'open';
    menu.addEventListener('toggle', () => {
        sessionStorage.setItem('momentum-food-library-nav', menu.open ? 'open' : 'closed');
    });
})();
