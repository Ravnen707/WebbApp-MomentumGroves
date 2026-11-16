
    const parseDecimal = value => {
        const text = String(value).trim().replace(',', '.');
        return text && /^[-+]?(?:\d+(?:\.\d*)?|\.\d+)$/.test(text) ? Number(text) : NaN;
    };

    const fmt = value => Number((Number(value) || 0).toFixed(1)).toLocaleString('en-GB');
    const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#39;'
    }[c]));
    const setText = (selector, value) => {
        const el = document.querySelector(selector);
        if (el) el.textContent = value;
    };
    const setHTML = (selector, value) => {
        const el = document.querySelector(selector);
        if (el) el.innerHTML = value;
    };
    const localDate = (d = new Date()) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    const shift = (date, n) => {
        const d = new Date(date + 'T12:00:00');
        d.setDate(d.getDate() + n);
        return localDate(d);
    };
    const monday = date => shift(date, -((new Date(date + 'T12:00:00').getDay() + 6) % 7));


export { parseDecimal, fmt, esc, setText, setHTML, localDate, shift, monday };
