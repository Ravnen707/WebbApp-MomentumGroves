using Microsoft.Playwright;

namespace MomentumGroves.PlaywrightTests;

[TestFixture]
public class Tests : PageTest
{
    [Test]
    public async Task DashboardShowsNutritionRangesAndLetsTheUserCloseThem()
    {
        // Start on the running Momentum Groves dashboard. Set this environment variable
        // if the app is listening on a different URL.
        var baseUrl = Environment.GetEnvironmentVariable("MOMENTUM_GROVES_BASE_URL") ?? "http://localhost:5000";
        await Page.GotoAsync(baseUrl);
        await Expect(Page.GetByRole(AriaRole.Heading, new()
        {
            Name = "Your daily overview"
        })).ToBeVisibleAsync();

        // Wait for the dashboard to render its training section after loading its data.
        // The page wires up the nutrition ranges buttons in the same initialization step.
        await Expect(Page.Locator("#home-workouts")).Not.ToBeEmptyAsync();

        // Open the ranges from the Calories card. Each nutrient card has the same button label,
        // so scope the search to the card the user would click.
        var caloriesCard = Page.Locator(".dashboard-nutrition > section").Filter(new()
        {
            Has = Page.GetByText("FAT", new()
            {
                Exact = true
            })
        });

        var openRanges = caloriesCard.GetByRole(AriaRole.Button, new()
        {
            Name = "View nutrition ranges"
        });
        await openRanges.ClickAsync();

        // Verify that the dialog opened and shows the calorie range the user came to see.
        var ranges = Page.GetByRole(AriaRole.Dialog, new()
        {
            Name = "Your daily nutrition ranges"
        });
        await Expect(ranges).ToBeVisibleAsync();

        // Field in "your daily nutrition Ranges Modal it reads from
        var caloriesRow = ranges.GetByRole(AriaRole.Row).Filter(new()
        {
            Has = Page.GetByRole(AriaRole.Cell, new()
            {
                Name = "Fat", Exact = true
            })
        });

        // Field it reads based on the field above it its set to read from. 
        //"Under 60 g - 60–90 g - 91–105 g - Over 105 g" any one of these would work as it reads from the line of "Fat"
        await Expect(caloriesRow.GetByRole(AriaRole.Cell, new()
        {
            Name = "60–90 g", Exact = true
        })).ToBeVisibleAsync();

        // Close the dialog using its close button and confirm the dashboard control is visible again.
        await ranges.GetByRole(AriaRole.Button, new()
        {
            Name = "Close nutrition ranges"
        }).ClickAsync();

        await Expect(ranges).ToBeHiddenAsync();
        await Expect(openRanges).ToBeVisibleAsync();
    }
}