import { read, write, CHEAT_KEY } from "../core/storage.js";
import { food } from "../food/food-diary.js";

    const cheatDays = () => {
        const stored = read(CHEAT_KEY, {});
        return stored && typeof stored === 'object' && !Array.isArray(stored) ? stored : {};
    };
    const isCheatDay = date => cheatDays()[date] === true;

    function setCheatDay(date, enabled) {
        const flags = {...cheatDays()};
        if (enabled) {
            flags[date] = true;
        } else {
            delete flags[date];
        }
        write(CHEAT_KEY, flags);
    }

    // Sick-day flags are stored separately; food registrations remain unchanged.


export { cheatDays, isCheatDay, setCheatDay };
