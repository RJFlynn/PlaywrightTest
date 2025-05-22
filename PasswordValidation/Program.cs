using Microsoft.Playwright;
using System;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PasswordValidation
{
    class Program
    {
        const string ConfigFilePath = "config.json";
        public static async Task Main()
        {
            var config = LoadConfig();

            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false // set true for headless mode
            });

            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            string username = config.Username;
            string password = config.Password;//"Password1!";//"Newpassword1";//

            string expectedError = "Password is not valid";

            // Step 1: Login
            await page.GotoAsync(config.Url);
            await Login(page,username,password);

            // Step 2: Navigate to 'Profile Settings'
            await page.ClickAsync("//a[contains(.,'Account')]"); // Replace with actual selector
            await page.ClickAsync("//a[@href='/account/profile']"); // Replace with actual selector

            // Step 3–4: Password without capital letters
            await ChangePassword(page, "newpassword1", expectedError);

            // Step 5–6: Password without numbers
            await ChangePassword(page, "Newpassword", expectedError);

            // Step 7–8: Password without lowercase
            await ChangePassword(page, "NEWPASSWORD1", expectedError);

            // Step 9–10: Valid password
            string newValidPassword = "Newpassword123";
            await ChangePassword(page, newValidPassword, null); // No error expected

            //save to config
            config.Password = newValidPassword;
            SaveConfig(config);
            password = config.Password;

            // Step 11: Logout
            await page.ClickAsync("//a[@id='logout']");

            // Step 12–13: Login with new password
            await Login(page,username,password);

            // Verify login success
            await page.WaitForSelectorAsync("//div[@class='dashboard-page']");

            Console.WriteLine("Test completed successfully.");
            await browser.CloseAsync();
        }

        private static async Task Login(IPage page, string username, string password)
        {
            await page.ClickAsync(".tertiary-btn");
            await page.FillAsync("//input[@class='ng-untouched ng-pristine ng-invalid']", username);
            await page.FillAsync("//input[@class='ng-untouched ng-pristine ng-valid']", password);
            await page.ClickAsync(".btn-primary");
        }

        private static async Task ChangePassword(IPage page, string newPassword, string? expectedError)
        {
            string passwordReset = "//div[@class='input-group-addon-container--append input-group-addon-container--append-password']/input[@class='ng-untouched ng-pristine ng-valid']";
            int count = await page.Locator(passwordReset).CountAsync();

            await page.Locator("change-password").ScrollIntoViewIfNeededAsync();

            if (count <= 0)
            {
                passwordReset = "//input[@class='ng-valid ng-dirty undefined has-error ng-touched']";
            }

            await page.Locator(passwordReset).FillAsync(newPassword);
            await page.ClickAsync("//button[contains(.,'Change Password')]");

            if (expectedError != null)
            {
                var error = await page.InnerTextAsync("//div[@class='alert alert-danger']/div[@class='alert__inner-container']");
                if (!error.Contains(expectedError))
                {
                    Console.WriteLine($"Expected error not found. Got: {error}");
                }
            }
            else
            {
                await page.WaitForSelectorAsync("//h4[@class='alert-heading alert__heading']");
                await page.ClickAsync("//button[contains(.,'Change Password')]");
            }
        }
        
        private static TestConfig LoadConfig()
        {
            string json = File.ReadAllText(ConfigFilePath);
            return JsonSerializer.Deserialize<TestConfig>(json)!;
        }

        private static void SaveConfig(TestConfig config)
        {
         var options = new JsonSerializerOptions { WriteIndented = true };
         File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(config, options));
        }
    }

}
