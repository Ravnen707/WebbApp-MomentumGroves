import {expect} from '@playwright/test';
import type {Locator, Page} from '@playwright/test';

type AriaRole = Parameters<Page['getByRole']>[0];

/**
 * Clicks a menu entry identified by its exact visible text.
 * @param page The page containing the menu entry.
 * @param text The exact text of the menu entry.
 * @returns A promise that resolves after the menu entry is clicked.
 */
export async function clickMenu(page: Page, text: string): Promise<void> {
    await page.getByText(text, {exact: true}).click();
}

/**
 * Clicks a dialog by accessible name.
 * @param page The page containing the dialog.
 * @param dialogName The accessible name of the dialog.
 * @returns A promise that resolves when the dialog is clicked.
 */
export async function clickDialog(page: Page, dialogName: string): Promise<void> {
    await page.getByRole('dialog', {name: dialogName}).click();
}

/**
 * Clicks a button by accessible name.
 * @param page The page containing the button.
 * @param buttonName The accessible name of the button.
 * @returns A promise that resolves when the button is clicked.
 */
export async function clickButton(page: Page, buttonName: string): Promise<void> {
    await page.getByRole('button', {name: buttonName}).click();
}

/**
 * Clicks a link by accessible name.
 * @param page The page containing the link.
 * @param link The accessible name of the link.
 * @returns A promise that resolves when the link is clicked.
 */
export async function clickLink(page: Page, link: string): Promise<void> {
    await page.getByRole('link', {name: link}).click();
}

/**
 * Clicks an element by role and exact accessible name.
 * @param page The page containing the element.
 * @param role The accessibility role of the element.
 * @param name The exact accessible name of the element.
 * @returns A promise that resolves when the element is clicked.
 */
export async function clickByRole(page: Page, role: AriaRole, name: string): Promise<void> {
    await page.getByRole(role, {name, exact: true}).click();
}

/**
 * Fills a field located by role and accessible name.
 * @param page The page containing the field.
 * @param fieldName The accessible name of the field.
 * @param value The value to enter.
 * @param role The accessibility role of the field.
 * @returns A promise that resolves when the field is filled.
 */
export async function writeDialog(page: Page, fieldName: string, value: string, role: AriaRole = 'textbox'): Promise<void> {
    await page.getByRole(role, {name: fieldName}).fill(value);
}

/**
 * Fills a field located by its exact label.
 * @param page The page containing the field.
 * @param label The exact label of the field.
 * @param value The value to enter.
 * @returns A promise that resolves when the field is filled.
 */
export async function fillField(page: Page, label: string, value: string): Promise<void> {
    await page.getByLabel(label, {exact: true}).fill(value);
}

/**
 * Sets a checkbox to the requested checked state.
 * @param page The page containing the checkbox.
 * @param checkboxName The exact accessible name of the checkbox.
 * @param isChecked Whether the checkbox should be checked.
 * @returns A promise that resolves when the checkbox has the requested state.
 */
export async function setCheckbox(page: Page, checkboxName: string, isChecked: boolean): Promise<void> {
    await page.getByRole('checkbox', {name: checkboxName, exact: true}).setChecked(isChecked);
}

/**
 * Selects an option by its visible label in a native dropdown.
 * @param page The page containing the dropdown.
 * @param dropdownName The exact accessible name of the dropdown.
 * @param optionLabel The visible label of the option to select.
 * @returns A promise that resolves when the option is selected.
 */
export async function selectDropdownOption(page: Page, dropdownName: string, optionLabel: string): Promise<void> {
    const dropdown = page.getByRole('combobox', {name: dropdownName, exact: true});
    await dropdown.selectOption({label: optionLabel});
}

/**
 * Selects a radio button by its accessible name.
 * @param page The page containing the radio button.
 * @param radioName The exact accessible name of the radio button.
 * @returns A promise that resolves when the radio button is selected.
 */
export async function selectRadio(page: Page, radioName: string): Promise<void> {
    await page.getByRole('radio', {name: radioName, exact: true}).check();
}

/**
 * Sends a key or shortcut to an element.
 * @param element The element that receives the key press.
 * @param key The Playwright key or shortcut, such as Enter or Control+A.
 * @returns A promise that resolves when the key is pressed.
 */
export async function pressKey(element: Locator, key: string): Promise<void> {
    await element.press(key);
}

/**
 * Moves the pointer over an element.
 * @param element The element to hover over.
 * @returns A promise that resolves when the pointer is over the element.
 */
export async function hover(element: Locator): Promise<void> {
    await element.hover();
}

/**
 * Assigns a local file to a file input.
 * @param fileInput The locator for the file input.
 * @param filePath The path to the file to upload.
 * @returns A promise that resolves when the file is assigned to the input.
 */
export async function uploadFile(fileInput: Locator, filePath: string): Promise<void> {
    await fileInput.setInputFiles(filePath);
}

/**
 * Asserts that a named heading is visible.
 * @param page The page expected to contain the heading.
 * @param headingName The exact accessible name of the heading.
 * @returns A promise that resolves when the heading is visible.
 */
export async function verifyHeading(page: Page, headingName: string): Promise<void> {
    await expect(page.getByRole('heading', {name: headingName, exact: true})).toBeVisible();
}

/**
 * Asserts that a named dialog is visible.
 * @param page The page expected to contain the dialog.
 * @param dialogName The exact accessible name of the dialog.
 * @returns A promise that resolves when the dialog is visible.
 */
export async function verifyDialog(page: Page, dialogName: string): Promise<void> {
    await expect(page.getByRole('dialog', {name: dialogName, exact: true})).toBeVisible();
}

/**
 * Asserts that the status region displays the expected message.
 * @param page The page containing the status region.
 * @param message The expected text of the status region.
 * @returns A promise that resolves when the status text matches.
 */
export async function verifyStatus(page: Page, message: string): Promise<void> {
    await expect(page.getByRole('status')).toHaveText(message);
}

/**
 * Asserts that an element is visible.
 * @param element The element expected to be visible.
 * @returns A promise that resolves when the element is visible.
 */
export async function verifyVisible(element: Locator): Promise<void> {
    await expect(element).toBeVisible();
}

/**
 * Asserts that an element is hidden or absent.
 * @param element The element expected to be hidden or absent.
 * @returns A promise that resolves when the element is hidden or absent.
 */
export async function verifyHidden(element: Locator): Promise<void> {
    await expect(element).toBeHidden();
}

/**
 * Asserts that a control is enabled.
 * @param element The control expected to be enabled.
 * @returns A promise that resolves when the control is enabled.
 */
export async function verifyEnabled(element: Locator): Promise<void> {
    await expect(element).toBeEnabled();
}

/**
 * Asserts that a control is disabled.
 * @param element The control expected to be disabled.
 * @returns A promise that resolves when the control is disabled.
 */
export async function verifyDisabled(element: Locator): Promise<void> {
    await expect(element).toBeDisabled();
}

/**
 * Asserts that a checkbox or radio button has the expected checked state.
 * @param element The checkbox or radio button to verify.
 * @param isChecked Whether the control should be checked.
 * @returns A promise that resolves when the checked state matches.
 */
export async function verifyChecked(element: Locator, isChecked: boolean): Promise<void> {
    await expect(element).toBeChecked({checked: isChecked});
}

/**
 * Asserts that an element's text matches the expected text.
 * @param element The element whose text is checked.
 * @param text The expected text of the element.
 * @returns A promise that resolves when the text matches.
 */
export async function verifyText(element: Locator, text: string): Promise<void> {
    await expect(element).toHaveText(text);
}

/**
 * Asserts that an element's text contains the expected text.
 * @param element The element whose text is checked.
 * @param text The text expected within the element.
 * @returns A promise that resolves when the text is found.
 */
export async function verifyContainsText(element: Locator, text: string): Promise<void> {
    await expect(element).toContainText(text);
}

/**
 * Asserts that a form field has the expected value.
 * @param field The form field whose value is checked.
 * @param value The expected field value.
 * @returns A promise that resolves when the value matches.
 */
export async function verifyValue(field: Locator, value: string): Promise<void> {
    await expect(field).toHaveValue(value);
}

/**
 * Asserts that a locator matches the expected number of elements.
 * @param elements The locator for the elements to count.
 * @param count The expected number of matching elements.
 * @returns A promise that resolves when the count matches.
 */
export async function verifyCount(elements: Locator, count: number): Promise<void> {
    await expect(elements).toHaveCount(count);
}

/**
 * Asserts that the browser is at the expected URL.
 * @param page The page whose URL is checked.
 * @param url The expected URL, which can be relative to the configured base URL.
 * @returns A promise that resolves when the URL matches.
 */
export async function verifyUrl(page: Page, url: string): Promise<void> {
    await expect(page).toHaveURL(url);
}

/**
 * Asserts that the browser page has the expected title.
 * @param page The page whose title is checked.
 * @param title The expected page title.
 * @returns A promise that resolves when the title matches.
 */
export async function verifyTitle(page: Page, title: string): Promise<void> {
    await expect(page).toHaveTitle(title);
}
