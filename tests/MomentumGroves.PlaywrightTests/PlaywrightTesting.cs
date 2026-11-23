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

    /// <summary>
    /// Testing To navigate to My Recipes => Create a recipe => Save it. Removes it in another test or it cannot be
    /// observed otherwise it works
    /// </summary>
    [Test]
    public async Task DashboardShowsNutritinRangesAndLetsTheUserCloseThem()
    {
        var baseUrl = Environment.GetEnvironmentVariable("MOMENTUM_GROVES_BASE_URL") ?? "http://localhost:5000";
        await Page.GotoAsync(baseUrl);

        // Making sure page is loaded in.
        await Expect(Page.GetByRole(AriaRole.Heading, new()
        {
            Name = "Your daily overview"
        })).ToBeVisibleAsync();

        // Readable and better named functionality names
        await ClickMenuAsync("Food Library");
        await ClickLinkAsync("My Recipes");
        await WriteDialogAsync("Recipe name", "Test");
        await WriteDialogAsync("Find an ingredient in My Foods", "olie", AriaRole.Searchbox);
        await ClickButtonAsync("Ekstra Jomfru Olivenolie (per 100 g)");
        await WriteDialogAsync("Meal weight (g)", "200");
        await ClickButtonAsync("Save recipe");

        await VerifyStatusAsync("Recipe saved.");
    }


    /* Past this comment is all helper classes used for testing. will be moved to a helper later*/
    
    /// <summary>
    /// To remove repeated code. and make test a bit more readable
    /// </summary>
    /// <param name="dialogName"></param>
    private async Task ClickDialogAsync(string dialogName)
    {
        await Page.GetByRole(AriaRole.Dialog, new()
        {
            Name = dialogName
        }).ClickAsync();
    }

    private async Task ClickButtonAsync(string buttonName)
    {
        await Page.GetByRole(AriaRole.Button, new()
        {
            Name = buttonName
        }).ClickAsync();
    }
    
    /*private async Task WritedialogAsync(string fieldName, string value)
    {
        await Page.GetByRole(AriaRole.Textbox, new()
        {
            Name = fieldName
        }).FillAsync(value);
    }*/

    private async Task ClickMenuAsync(string text)
    {
        await Page.GetByText(text, new()
        {
            Exact = true
        }).ClickAsync();
    }

    private async Task ClickLinkAsync(string link)
    {
        await Page.GetByRole(AriaRole.Link, new()
        {
            Name = link
        }).ClickAsync();
    }

    private async Task WriteDialogAsync(string fieldName, string value, AriaRole role = AriaRole.Textbox)
    {
        await Page.GetByRole(role, new()
        {
            Name = fieldName
        }).FillAsync(value);
    }
    
    private async Task VerifyStatusAsync(string message)
    {
        await Expect(Page.GetByRole(AriaRole.Status)).ToHaveTextAsync(message);
    }
}