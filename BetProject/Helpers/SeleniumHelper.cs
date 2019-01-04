using BetProject.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using System;
using System.IO;

namespace BetProject.Helpers
{
    public class SeleniumHelper
    {
        public static IWebDriver CreateDefaultWebDriver(bool headless = false)
        {
            var configuration = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json")
               .Build();

            var seleniumConfigurations = new SeleniumConfiguration();
            new ConfigureFromConfigurationOptions<SeleniumConfiguration>(
                configuration.GetSection("SeleniumConfiguration"))
                    .Configure(seleniumConfigurations);

            FirefoxOptions options = new FirefoxOptions();
            if (headless) options.AddArgument("--headless");
            IWebDriver wd1 = new FirefoxDriver(seleniumConfigurations.DriverFirefoxPath, options, TimeSpan.FromDays(1));
            wd1.Manage().Timeouts().PageLoad = new TimeSpan(10, 0, 0);
            return wd1;
        }
    }
}
