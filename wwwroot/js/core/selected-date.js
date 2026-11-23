import {localDate, shift} from './utils.js';

const storageKey = 'momentum.selected-date';
let pinnedDate = sessionStorage.getItem(storageKey) || '';

export const selectedDate = () => pinnedDate || localDate();

export function selectDate(date) {
    pinnedDate = date === localDate() ? '' : date;
    if (pinnedDate) {
        sessionStorage.setItem(storageKey, pinnedDate);
    } else {
        sessionStorage.removeItem(storageKey);
    }
    document.dispatchEvent(new Event('momentum-date-change'));
}

export function setupDatePicker() {
    const picker = document.querySelector('#selected-date');
    const todayButton = document.querySelector('#select-today');
    let displayedDate;
    let midnightTimer;

    function updatePicker() {
        displayedDate = selectedDate();
        picker.value = displayedDate;
        todayButton.disabled = !pinnedDate;
    }

    function followCalendar() {
        if (displayedDate !== selectedDate()) {
            document.dispatchEvent(new Event('momentum-date-change'));
        }
        clearTimeout(midnightTimer);
        const midnight = new Date(shift(localDate(), 1) + 'T00:00:00');
        midnightTimer = setTimeout(followCalendar, midnight.getTime() - Date.now());
    }

    picker.addEventListener('change', () => {
        if (picker.value && picker.validity.valid) selectDate(picker.value);
        else updatePicker();
    });
    todayButton.addEventListener('click', () => selectDate(''));
    document.addEventListener('momentum-date-change', updatePicker);
    document.addEventListener('visibilitychange', () => {
        if (!document.hidden) followCalendar();
    });
    window.addEventListener('pageshow', followCalendar);
    updatePicker();
    followCalendar();
}
