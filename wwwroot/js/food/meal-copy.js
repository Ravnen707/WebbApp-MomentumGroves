import { fmt, shift } from "../core/utils.js";
import { food } from "./food-diary.js?v=selected-date";

    function setupMealCopy(picker) {
        const dialog = document.querySelector('#copy-meal-dialog');
        const date = document.querySelector('#copy-date');
        const select = document.querySelector('#copy-meal');
        const button = document.querySelector('#copy-meal-button');
        const message = document.querySelector('#copy-message');
        if (!dialog || !date || !select || !button) {
            return;
        }
        function options() {
            const items = food().filter(item => item.date === date.value && item.type !== 'drink');
            select.replaceChildren();
            const placeholder = document.createElement('option');
            placeholder.value = '';
            placeholder.textContent = items.length ? 'Choose meal' : 'No meals on this day';
            select.append(placeholder);
            for (const item of items) {
                const option = document.createElement('option');
                option.value = item.id;
                option.textContent = item.name + ' · ' + fmt(item.kcal) + ' kcal';
                select.append(option);
            }
        }

        date.addEventListener('change', options);
        document.querySelector('#open-copy-meal').addEventListener('click', () => {
            date.value = shift(picker.value, -1);
            message.textContent = '';
            options();
            dialog.showModal();
        });
        document.querySelector('#close-copy-meal').addEventListener('click', () => dialog.close());
        document.querySelector('#cancel-copy-meal').addEventListener('click', () => dialog.close());
        button.addEventListener('click', () => {
            const item = food().find(entry => String(entry.id) === select.value && entry.date === date.value);
            if (!item) {
                message.textContent = 'Choose a previous meal.';
                return;
            }
            dialog.close();
            window.momentumOpenFoodModal?.(item);
        });
    }



export { setupMealCopy };
