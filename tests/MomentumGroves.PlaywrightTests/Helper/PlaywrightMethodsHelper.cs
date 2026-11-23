using Microsoft.Playwright;

namespace MomentumGroves.PlaywrightTests.Helper;

public static class PlaywrightMethodsHelper
{
    /// <summary>
    /// To remove repeated code. and make test a bit more readable
    /// </summary>
    /// <param name="dialogName"></param>
    public static async Task ClickDialogAsync(IPage page, string dialogName)
    {
        await page.GetByRole(AriaRole.Dialog, new()
        {
            Name = dialogName
        }).ClickAsync();
    }

    public static async Task ClickButtonAsync(IPage page, string buttonName)
    {
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = buttonName
        }).ClickAsync();
    }

    public static async Task ClickMenuAsync(IPage page, string text)
    {
        await page.GetByText(text, new()
        {
            Exact = true
        }).ClickAsync();
    }

    public static async Task ClickLinkAsync(IPage page, string link)
    {
        await page.GetByRole(AriaRole.Link, new()
        {
            Name = link
        }).ClickAsync();
    }

    public static async Task WriteDialogAsync(IPage page, string fieldName, string value, AriaRole role = AriaRole.Textbox)
    {
        await page.GetByRole(role, new()
        {
            Name = fieldName
        }).FillAsync(value);
    }
    
    public static async Task VerifyStatusAsync(IPage page, string message)
    {
        await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToHaveTextAsync(message);
    }
}