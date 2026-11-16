import { FOOD_KEY, write } from "../core/storage.js";
import { food } from "./food-diary.js?v=20261101-food-nutrition";

    window.momentumOpenMealEditor = (item, refresh) => {
        if (typeof window.momentumMealEditorOpen === 'function') {
            window.momentumMealEditorOpen(item, updated => {
                const items = food();
                const index = items.findIndex(x => String(x.id) === String(item.id));
                if (index < 0) return;
                items[index] = {...items[index], ...updated, id: item.id, date: item.date};
                write(FOOD_KEY, items);
                refresh();
            });
        }
    };


export {  };
