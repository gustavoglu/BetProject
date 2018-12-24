using BetProject.ObjectValues.Selenium;
using System.IO;

namespace BetProject.Configurations
{
    public class SeleniumConfiguration
    {
        public SeleniumConfiguration()
        {
            DriverFirefoxPath = Path.Combine(Directory.GetCurrentDirectory());
        }

        public string DriverFirefoxPath { get; set; }
        public int TimeOut { get; set; }
        public Sites Sites { get; set; }

    }
}
