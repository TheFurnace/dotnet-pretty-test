namespace MockSelenium;

/// <summary>
/// Navigation smoke tests.
/// All tests are [Explicit] — they require Chrome + chromedriver on PATH and a running
/// target app. Run with: dotnet test --filter "cat=Selenium"
/// </summary>
[TestFixture]
[Category("Selenium")]
public class NavigationTests
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
    public void Navigation_HomeLink_ReturnsToRoot()
    {
        _driver!.Navigate().GoToUrl("https://example.com/about");
        _driver.FindElement(By.CssSelector("nav a[href='/']")).Click();

        Assert.That(_driver.Url, Is.EqualTo("https://example.com/"));
    }

    [Test, Explicit("Selenium: requires Chrome + chromedriver on PATH")]
    public void Navigation_BreadcrumbUpdates_OnPageChange()
    {
        _driver!.Navigate().GoToUrl("https://example.com/products/widget");

        var breadcrumb = _driver.FindElement(By.CssSelector(".breadcrumb"));
        Assert.That(breadcrumb.Text, Does.Contain("Products"));
        Assert.That(breadcrumb.Text, Does.Contain("Widget"));
    }

    [Test, Explicit("Selenium: requires Chrome + chromedriver on PATH")]
    public async Task Navigation_AllMainNavLinks_Return200()
    {
        _driver!.Navigate().GoToUrl("https://example.com");

        var navLinks = _driver.FindElements(By.CssSelector("nav a"));
        Assert.That(navLinks, Has.Count.GreaterThan(0));

        foreach (var link in navLinks)
        {
            var href = link.GetAttribute("href");
            if (string.IsNullOrEmpty(href) || href.StartsWith('#')) continue;

            _driver.Navigate().GoToUrl(href);
            await Task.Delay(200);   // brief settle

            Assert.That(
                _driver.FindElements(By.CssSelector("h1,h2,main")),
                Has.Count.GreaterThan(0),
                $"No main content found at {href}");
        }
    }

    [Test, Explicit("Selenium: requires Chrome + chromedriver on PATH")]
    public void Navigation_BackButton_ReturnsToCorrectPage()
    {
        _driver!.Navigate().GoToUrl("https://example.com");
        _driver.Navigate().GoToUrl("https://example.com/about");
        _driver.Navigate().Back();

        Assert.That(_driver.Url, Is.EqualTo("https://example.com/"));
    }
}
