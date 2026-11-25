import { read, write, SICK_KEY, FIBER_START_KEY } from "../core/storage.js";
import { food } from "../food/food-diary.js";

    const sickDays = () => {
        const stored = read(SICK_KEY, {});
        return stored && typeof stored === 'object' && !Array.isArray(stored) ? stored : {};
    };
    const isSickDay = date => sickDays()[date] === true;
    const fiberActive = date => date >= read(FIBER_START_KEY, '9999-12-31');
    const fiberKnown = date => food().some(item => item.date === date && item.fiber !== null && item.fiber !== undefined
        && Number.isFinite(Number(item.fiber)));
    const fiberComplete = date => {
        const entries = food().filter(item => item.date === date);
        return entries.length > 0 && entries.every(item => item.fiber !== null && item.fiber !== undefined
            && Number.isFinite(Number(item.fiber)));
    };

    function setSickDay(date, enabled) {
        const flags = {...sickDays()};
        if (enabled) {
            flags[date] = true;
        } else {
            delete flags[date];
        }
        write(SICK_KEY, flags);
    }

    // Fixed daily nutrition zones. Personal goals still control the goal line and remaining amounts.


export { sickDays, isSickDay, fiberActive, fiberKnown, fiberComplete, setSickDay };
