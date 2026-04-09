namespace MockSelenium;

/// <summary>
/// Login page smoke tests.
/// All tests are [Explicit] — they require Chrome + chromedriver on PATH and a running
/// target app. Run with: dotnet test --filter "cat=Selenium"
/// </summary>
[TestFixture]
[Category("Selenium")]
public class LoginPageTests
{
    private IWebDriver? _driver;

    [SetUp]
    public void SetUp()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        _driver = new ChromeDriver(options);
        _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
    }

    [TearDown]
    public void TearDown()
    {
        _driver?.Quit();
        _driver?.Dispose();
    }

    [Test, Explicit("Selenium: requires Chrome + chromedriver on PATH")]
    public void Login_WithValidCredentials_RedirectsToDashboard()
    {
        _driver!.Navigate().GoToUrl("https://example.com/login");

        _driver.FindElement(By.Id("username")).SendKeys("admin");
        _driver.FindElement(By.Id("password")).SendKeys("correct-password");
        _driver.FindElement(By.Id("login-btn")).Click();

        Assert.That(_driver.Url, Does.Contain("/dashboard"));
        Assert.That(_driver.Title, Does.Contain("Dashboard"));
    }

    [Test, Explicit("Selenium: requires Chrome + chromedriver on PATH")]
    public void Login_WithInvalidCredentials_ShowsErrorMessage()
    {
        _driver!.Navigate().GoToUrl("https://example.com/login");

        _driver.FindElement(By.Id("username")).SendKeys("admin");
        _driver.FindElement(By.Id("password")).SendKeys("wrong-password");
        _driver.FindElement(By.Id("login-btn")).Click();

        var error = _driver.FindElement(By.CssSelector(".error-message"));
        Assert.That(error.Text, Does.Contain("Invalid credentials"));
    }

    [Test, Explicit("Selenium: requires Chrome + chromedriver on PATH")]
    public void Login_ForgotPasswordLink_IsVisibleAndNavigates()
    {
        _driver!.Navigate().GoToUrl("https://example.com/login");

        var link = _driver.FindElement(By.LinkText("Forgot password?"));
        Assert.That(link.Displayed, Is.True);

        link.Click();
        Assert.That(_driver.Url, Does.Contain("/forgot-password"));
    }

    [Test, Explicit("Selenium: requires Chrome + chromedriver on PATH")]
    public void Login_EmptySubmit_ShowsValidationErrors()
    {
        _driver!.Navigate().GoToUrl("https://example.com/login");
        _driver.FindElement(By.Id("login-btn")).Click();

        var errors = _driver.FindElements(By.CssSelector(".field-error"));
        Assert.That(errors, Has.Count.GreaterThanOrEqualTo(2));
    }
}
