import {fmt} from "../core/utils.js";

export const roundFoodAmount = value => Number(Number(value).toFixed(2));

export function nutritionSummary({kcal, protein, carbs, fat, fiber}, format = fmt) {
    return `${format(kcal)} kcal · P ${format(protein)} g · C ${format(carbs)} g · F ${format(fat)} g`
        + (fiber == null ? '' : ` · Fibre ${format(fiber)} g`);
}

export function ingredientRow(item, onRemove) {
    const row = document.createElement('div');
    row.className = 'food-ingredient-row';
    const details = document.createElement('div');
    details.className = 'food-ingredient-details';
    const name = document.createElement('strong');
    name.textContent = item.name;
    const nutrition = document.createElement('small');
    nutrition.textContent = nutritionSummary(item);
    details.append(name, nutrition);
    const amount = document.createElement('input');
    amount.type = 'text';
    amount.inputMode = 'decimal';
    amount.value = roundFoodAmount(item.grams);
    amount.className = 'food-row-amount';
    const unit = document.createElement('span');
    unit.textContent = item.unit || 'g';
    amount.setAttribute('aria-label', `Amount for ${item.name} (${unit.textContent})`);
    row.append(details, amount, unit);
    if (onRemove) {
        const remove = document.createElement('button');
        remove.type = 'button';
        remove.className = 'btn danger';
        remove.textContent = 'Remove';
        remove.addEventListener('click', onRemove);
        row.append(remove);
    }
    return {row, amount, nutrition};
}
