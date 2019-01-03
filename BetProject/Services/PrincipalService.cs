using BetProject.Configurations;
using BetProject.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BetProject.Services
{
    public class PrincipalService
    {
        public List<IWebDriver> WebDriverCollectiont { get; set; }
        private readonly SeleniumConfiguration _configuration;
        private readonly TelegramService _telegramService;
        public PrincipalService()
        {
            var configuration = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json")
              .Build();

            var seleniumConfigurations = new SeleniumConfiguration();
            new ConfigureFromConfigurationOptions<SeleniumConfiguration>(
                configuration.GetSection("SeleniumConfiguration"))
                    .Configure(seleniumConfigurations);
            _configuration = seleniumConfigurations;
            _telegramService = new TelegramService();

        }

   

        public List<IWebDriver> WebDriverList(int qtdWebDrivers = 1, bool headless = false)
        {
            FirefoxOptions options = new FirefoxOptions();
            if (headless) options.AddArgument("--headless");
            List<IWebDriver> webDrivers = new List<IWebDriver>();
            for (int i = 0; i < qtdWebDrivers; i++)
            {
                var wd = new FirefoxDriver(_configuration.DriverFirefoxPath, options);
                wd.Manage().Timeouts().PageLoad = new TimeSpan(10, 0, 0);
                webDrivers.Add(wd);
            }
            return webDrivers;
        }


        public async Task SalvaJogosAmanha(int qtdWebDrivers = 1, bool headless = false)
        {
            
            FirefoxOptions options = new FirefoxOptions();
            IWebDriver wd1 = new FirefoxDriver(_configuration.DriverFirefoxPath, options);
            IWebDriver wd2 = new FirefoxDriver(_configuration.DriverFirefoxPath, options);
            wd1.Manage().Timeouts().PageLoad = new TimeSpan(10, 0, 0);
            wd2.Manage().Timeouts().PageLoad = new TimeSpan(10, 0, 0);
            try
            {
                ResultadosSiteHelper.CarregandoJogos = true;
                ResultadoSiteService rs1 = new ResultadoSiteService(wd1);
                ResultadoSiteService rs2 = new ResultadoSiteService(wd2);

                Task.Factory.StartNew(async () =>
                    {
                        await rs2.SalvaJogosDeAmanha();
                    });

                await rs1.SalvaJogosDeAmanha(true);
                ResultadosSiteHelper.CarregandoJogos = false;
                wd1.Dispose();
                wd2.Dispose();

            }
            catch (Exception e)
            {
                _telegramService.EnviaMensagemParaOGrupo("Erro app: "+ e.Message);
                wd1.Dispose();
                wd2.Dispose();
            }
        }

        public async Task Iniciar(int qtdWebDrivers = 1, bool headless = false)
        {
            WebDriverCollectiont = WebDriverList(qtdWebDrivers, headless);
            try
            {
                for (int i = 0; i < WebDriverCollectiont.Count; i++)
                {
                    var wd = WebDriverCollectiont[i];
                    var rs = new ResultadoSiteService(wd);
                    if (i + 1 == WebDriverCollectiont.Count)
                    {
                        await rs.StartAnaliseLive(i % 2 == 0);
                    }
                    else
                    {
                        Task.Factory.StartNew(() => rs.StartAnaliseLive(i % 2 == 0));
                        if (i == 0) await Task.Delay(5000);
                    }
                }
            }
            catch (Exception e)
            {
                _telegramService.EnviaMensagemParaOGrupo(e.Message);
                WebDriverCollectiont.ForEach(wd => wd.Dispose());
            }
        }


        public void Dispose()
        {
            WebDriverCollectiont.ForEach(wd => wd.Dispose());
        }
    }
}
