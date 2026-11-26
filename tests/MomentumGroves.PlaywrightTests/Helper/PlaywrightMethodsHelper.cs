// Playwright is reserved for the project owner to learn and practice.
// Coding agents must not add, edit, remove, or run Playwright tests.

using Microsoft.Playwright;

namespace MomentumGroves.PlaywrightTests.Helper;

public static class PlaywrightMethodsHelper
{
    /// <summary>
    /// Clicks a dialog identified by its accessible name.
    /// </summary>
    /// <param name="page">The page containing the dialog.</param>
    /// <param name="dialogName">The accessible name of the dialog.</param>
    /// <returns>A task that completes after the dialog is clicked.</returns>
    public static async Task ClickDialogAsync(IPage page, string dialogName)
    {
        await page.GetByRole(AriaRole.Dialog, new()
        {
            Name = dialogName
        }).ClickAsync();
    }

    /// <summary>
    /// Clicks a button identified by its accessible name.
    /// </summary>
    /// <param name="page">The page containing the button.</param>
    /// <param name="buttonName">The accessible name of the button.</param>
    /// <returns>A task that completes after the button is clicked.</returns>
    public static async Task ClickButtonAsync(IPage page, string buttonName)
    {
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = buttonName
        }).ClickAsync();
    }

    /// <summary>
    /// Clicks a menu entry identified by its exact visible text.
    /// </summary>
    /// <param name="page">The page containing the menu entry.</param>
    /// <param name="text">The exact text of the menu entry.</param>
    /// <returns>A task that completes after the menu entry is clicked.</returns>
    public static async Task ClickMenuAsync(IPage page, string text)
    {
        await page.GetByText(text, new()
        {
            Exact = true
        }).ClickAsync();
    }

    /// <summary>
    /// Clicks a link identified by its accessible name.
    /// </summary>
    /// <param name="page">The page containing the link.</param>
    /// <param name="link">The accessible name of the link.</param>
    /// <returns>A task that completes after the link is clicked.</returns>
    public static async Task ClickLinkAsync(IPage page, string link)
    {
        await page.GetByRole(AriaRole.Link, new()
        {
            Name = link
        }).ClickAsync();
    }

    /// <summary>
    /// Replaces the value of a field identified by its role and accessible name.
    /// </summary>
    /// <param name="page">The page containing the field.</param>
    /// <param name="fieldName">The accessible name of the field.</param>
    /// <param name="value">The value to enter.</param>
    /// <param name="role">The field's accessibility role. Defaults to a textbox.</param>
    /// <returns>A task that completes after the field is filled.</returns>
    public static async Task WriteDialogAsync(IPage page, string fieldName, string value, AriaRole role = AriaRole.Textbox)
    {
        await page.GetByRole(role, new()
        {
            Name = fieldName
        }).FillAsync(value);
    }

    /// <summary>
    /// Sets the checked state of a checkbox identified by its accessible name.
    /// </summary>
    /// <param name="page">The page containing the checkbox.</param>
    /// <param name="checkboxName">The exact accessible name of the checkbox.</param>
    /// <param name="isChecked">True to check the box or false to clear it.</param>
    /// <returns>A task that completes when the checkbox has the requested state.</returns>
    public static async Task SetCheckboxAsync(IPage page, string checkboxName, bool isChecked)
    {
        await page.GetByRole(AriaRole.Checkbox, new() { Name = checkboxName, Exact = true }).SetCheckedAsync(isChecked);
    }

    /// <summary>
    /// Selects an option by its visible label in a native dropdown.
    /// </summary>
    /// <param name="page">The page containing the dropdown.</param>
    /// <param name="dropdownName">The exact accessible name of the dropdown.</param>
    /// <param name="optionLabel">The visible label of the option to select.</param>
    /// <returns>A task that completes after the option is selected.</returns>
    public static async Task SelectDropdownOptionAsync(IPage page, string dropdownName, string optionLabel)
    {
        var dropdown = page.GetByRole(AriaRole.Combobox, new() { Name = dropdownName, Exact = true });
        await dropdown.SelectOptionAsync(new SelectOptionValue { Label = optionLabel });
    }

    /// <summary>
    /// Asserts that a heading with the specified accessible name is visible.
    /// </summary>
    /// <param name="page">The page expected to contain the heading.</param>
    /// <param name="headingName">The exact accessible name of the heading.</param>
    /// <returns>A task that completes when the heading is visible.</returns>
    public static async Task VerifyHeadingAsync(IPage page, string headingName)
    {
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = headingName, Exact = true })).ToBeVisibleAsync();
    }

    /// <summary>
    /// Asserts that a dialog with the specified accessible name is visible.
    /// </summary>
    /// <param name="page">The page expected to contain the dialog.</param>
    /// <param name="dialogName">The exact accessible name of the dialog.</param>
    /// <returns>A task that completes when the dialog is visible.</returns>
    public static async Task VerifyDialogAsync(IPage page, string dialogName)
    {
        await Assertions.Expect(page.GetByRole(AriaRole.Dialog, new() { Name = dialogName, Exact = true })).ToBeVisibleAsync();
    }

    /// <summary>
    /// Asserts that the status region displays the expected message.
    /// </summary>
    /// <param name="page">The page containing the status region.</param>
    /// <param name="message">The expected text of the status region.</param>
    /// <returns>A task that completes when the status text matches.</returns>
    public static async Task VerifyStatusAsync(IPage page, string message)
    {
        await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToHaveTextAsync(message);
    }

    /// <summary>
    /// Clicks an element by its role and exact accessible name.
    /// </summary>
    /// <param name="page">The page containing the element.</param>
    /// <param name="role">The accessibility role of the element.</param>
    /// <param name="name">The exact accessible name of the element.</param>
    /// <returns>A task that completes when the element is clicked.</returns>
    public static async Task ClickByRoleAsync(IPage page, AriaRole role, string name)
    {
        await page.GetByRole(role, new() { Name = name, Exact = true }).ClickAsync();
    }

    /// <summary>
    /// Replaces the value of a field located by its label.
    /// </summary>
    /// <param name="page">The page containing the field.</param>
    /// <param name="label">The exact label of the field.</param>
    /// <param name="value">The value to enter.</param>
    /// <returns>A task that completes when the field is filled.</returns>
    public static async Task FillFieldAsync(IPage page, string label, string value)
    {
        await page.GetByLabel(label, new() { Exact = true }).FillAsync(value);
    }

    /// <summary>
    /// Selects a radio button by its accessible name.
    /// </summary>
    /// <param name="page">The page containing the radio button.</param>
    /// <param name="radioName">The exact accessible name of the radio button.</param>
    /// <returns>A task that completes when the radio button is selected.</returns>
    public static async Task SelectRadioAsync(IPage page, string radioName)
    {
        await page.GetByRole(AriaRole.Radio, new() { Name = radioName, Exact = true }).CheckAsync();
    }

    /// <summary>
    /// Sends a key or shortcut to an element.
    /// </summary>
    /// <param name="element">The element that receives the key press.</param>
    /// <param name="key">The Playwright key or shortcut, such as Enter or Control+A.</param>
    /// <returns>A task that completes when the key is pressed.</returns>
    public static async Task PressKeyAsync(ILocator element, string key)
    {
        await element.PressAsync(key);
    }

    /// <summary>
    /// Moves the pointer over an element.
    /// </summary>
    /// <param name="element">The element to hover over.</param>
    /// <returns>A task that completes when the pointer is over the element.</returns>
    public static async Task HoverAsync(ILocator element)
    {
        await element.HoverAsync();
    }

    /// <summary>
    /// Assigns a local file to a file input.
    /// </summary>
    /// <param name="fileInput">The locator for the file input.</param>
    /// <param name="filePath">The path to the file to upload.</param>
    /// <returns>A task that completes when the file is assigned to the input.</returns>
    public static async Task UploadFileAsync(ILocator fileInput, string filePath)
    {
        await fileInput.SetInputFilesAsync(filePath);
    }

    /// <summary>
    /// Asserts that an element is visible.
    /// </summary>
    /// <param name="element">The element expected to be visible.</param>
    /// <returns>A task that completes when the element is visible.</returns>
    public static async Task VerifyVisibleAsync(ILocator element)
    {
        await Assertions.Expect(element).ToBeVisibleAsync();
    }

    /// <summary>
    /// Asserts that an element is hidden or absent from the page.
    /// </summary>
    /// <param name="element">The element expected to be hidden or absent.</param>
    /// <returns>A task that completes when the element is hidden or absent.</returns>
    public static async Task VerifyHiddenAsync(ILocator element)
    {
        await Assertions.Expect(element).ToBeHiddenAsync();
    }

    /// <summary>
    /// Asserts that a control is enabled.
    /// </summary>
    /// <param name="element">The control expected to be enabled.</param>
    /// <returns>A task that completes when the control is enabled.</returns>
    public static async Task VerifyEnabledAsync(ILocator element)
    {
        await Assertions.Expect(element).ToBeEnabledAsync();
    }

    /// <summary>
    /// Asserts that a control is disabled.
    /// </summary>
    /// <param name="element">The control expected to be disabled.</param>
    /// <returns>A task that completes when the control is disabled.</returns>
    public static async Task VerifyDisabledAsync(ILocator element)
    {
        await Assertions.Expect(element).ToBeDisabledAsync();
    }

    /// <summary>
    /// Asserts that a checkbox or radio button has the expected checked state.
    /// </summary>
    /// <param name="element">The checkbox or radio button to verify.</param>
    /// <param name="isChecked">True to expect a checked control or false to expect an unchecked control.</param>
    /// <returns>A task that completes when the checked state matches.</returns>
    public static async Task VerifyCheckedAsync(ILocator element, bool isChecked)
    {
        await Assertions.Expect(element).ToBeCheckedAsync(new() { Checked = isChecked });
    }

    /// <summary>
    /// Asserts that an element's text matches the expected text.
    /// </summary>
    /// <param name="element">The element whose text is checked.</param>
    /// <param name="text">The expected text of the element.</param>
    /// <returns>A task that completes when the text matches.</returns>
    public static async Task VerifyTextAsync(ILocator element, string text)
    {
        await Assertions.Expect(element).ToHaveTextAsync(text);
    }

    /// <summary>
    /// Asserts that an element's text contains the expected text.
    /// </summary>
    /// <param name="element">The element whose text is checked.</param>
    /// <param name="text">The text expected within the element.</param>
    /// <returns>A task that completes when the text is found.</returns>
    public static async Task VerifyContainsTextAsync(ILocator element, string text)
    {
        await Assertions.Expect(element).ToContainTextAsync(text);
    }

    /// <summary>
    /// Asserts that a form field has the expected value.
    /// </summary>
    /// <param name="field">The form field whose value is checked.</param>
    /// <param name="value">The expected field value.</param>
    /// <returns>A task that completes when the value matches.</returns>
    public static async Task VerifyValueAsync(ILocator field, string value)
    {
        await Assertions.Expect(field).ToHaveValueAsync(value);
    }

    /// <summary>
    /// Asserts that a locator matches the expected number of elements.
    /// </summary>
    /// <param name="elements">The locator for the elements to count.</param>
    /// <param name="count">The expected number of matching elements.</param>
    /// <returns>A task that completes when the count matches.</returns>
    public static async Task VerifyCountAsync(ILocator elements, int count)
    {
        await Assertions.Expect(elements).ToHaveCountAsync(count);
    }

    /// <summary>
    /// Asserts that the browser is at the expected URL.
    /// </summary>
    /// <param name="page">The page whose URL is checked.</param>
    /// <param name="url">The expected URL, which can be relative to the configured base URL.</param>
    /// <returns>A task that completes when the URL matches.</returns>
    public static async Task VerifyUrlAsync(IPage page, string url)
    {
        await Assertions.Expect(page).ToHaveURLAsync(url);
    }

    /// <summary>
    /// Asserts that the browser page has the expected title.
    /// </summary>
    /// <param name="page">The page whose title is checked.</param>
    /// <param name="title">The expected page title.</param>
    /// <returns>A task that completes when the title matches.</returns>
    public static async Task VerifyTitleAsync(IPage page, string title)
    {
        await Assertions.Expect(page).ToHaveTitleAsync(title);
    }
}
