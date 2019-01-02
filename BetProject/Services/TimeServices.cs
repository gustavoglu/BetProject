using BetProject.ObjectValues;
using OpenQA.Selenium;
using System.Collections.Generic;
using System.Linq;

namespace BetProject.Services
{
    public class TimeServices
    {
        private readonly IWebDriver _driver; 
        public TimeServices(IWebDriver driver)
        {
            _driver = driver;
        }
        public List<Time> CriaTimes(string idBet)
        {
            List<Time> times = new List<Time>();
            var timesNomes = _driver.FindElements(By.ClassName("participant-imglink")).ToList();
            times.Add(new Time(timesNomes[1].Text));
            times.Add(new Time(timesNomes[3].Text));
            return times;
        }
    }
}
