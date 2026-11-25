import {FOOD_KEY, write} from "../core/storage.js";
import {openEntryEditor} from "./edit-meal.js?v=meal-edit";

export function openMealEditor(item, openFoodModal, food, refresh) {
    if (item.type !== 'drink' && openFoodModal && (item.ingredientItems?.length ||
        item.entryMode === 'recipe' || item.entryMode === 'recipe-custom')) {
        openFoodModal(item, {edit: true});
        return;
    }

    openEntryEditor?.(item, updated => {
        const entries = [...food()];
        const index = entries.findIndex(entry => String(entry.id) === String(item.id));
        if (index < 0) return;
        entries[index] = {...entries[index], ...updated};
        write(FOOD_KEY, entries);
        refresh();
    });
}
