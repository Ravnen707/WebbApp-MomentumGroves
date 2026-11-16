# Body metrics and weight tracking

The two features share the existing SQLite `src/Data/momentum.db` database and the existing `/api/momentum-data` API.

- `BodyMetrics` is the BMI-related personal calculator and nutrition target configuration. It uses `sunhed.profile.v1` and `sunhed.goal.v1`.
- `WeightTracking` stores dated weigh-ins or explicit skipped days under `sunhed.weight-history.v1`. It uses the target weight from `sunhed.profile.v1` and does not duplicate profile information.

## Weight trend

The weight trend is an elapsed-day-aware exponential moving average calculated from actual weigh-ins only. For each measured weight `W` after `d` days, `T = previousT + (1 - 0.9^d) * (W - previousT)`. The first measurement initializes the trend. Skipped days create no points.

## Forecast

At least six measurements spanning 14 days are required. A least-squares linear regression is fitted to the last 60 days of calculated trend values. Only a clearly decreasing trend produces a target month. The result is an illustrative, non-medical estimate, not a promise.

## Data and removal

No new database is created. To delete a single weight entry, use **Slet valgt dato** on the Weight Tracking page. Deleting all weight entries requires clearing only the `sunhed.weight-history.v1` key through the existing data API or database; do not delete `momentum.db`, which also holds meals, workouts and settings. The calculator remains accessible at `/Calculator` via redirect to `/BodyMetrics`.

## Code layout

- `Views/BodyMetrics/Index.cshtml`: existing calculator view, moved from `Views/Calculator`.
- `Views/WeightTracking/Index.cshtml`: weight entry and graph page.
- `src/Controllers/BodyMetricsController.cs`: calculator route.
- `src/Controllers/WeightTrackingController.cs`: tracking route.
- `wwwroot/js/weight-tracking.js`: calculations, chart, SQLite persistence.
- `wwwroot/css/weight-tracking.css`: page styling.

## Weight milestones

`sunhed.weight-goals.v1` stores `{ "mode": "milestone" | "final", "milestones": [110, 105] }` in the same `momentum.db` AppState table. The final target remains in `sunhed.profile.v1` and is never duplicated. Users can add and remove milestones, and choose the target type. When milestone mode is selected, the next milestone below the latest **measured** weight is active. Reached milestones are skipped; when none remains, the final target is used if it is lower than the latest measured weight. In final mode, only the final target is considered. When all applicable targets have been reached, the chart and history continue without a target line or forecast. Changing goals never changes the EMA trend or historical measurements.
