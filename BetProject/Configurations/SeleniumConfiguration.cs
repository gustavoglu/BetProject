using BetProject.ObjectValues.Selenium;

namespace BetProject.Configurations
{
    public class SeleniumConfiguration
    {
        public string DriverFirefoxPath { get; set; }
        public int TimeOut { get; set; }
        public Sites Sites { get; set; }

    }
}
