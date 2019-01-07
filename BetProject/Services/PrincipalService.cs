﻿using BetProject.Configurations;
using BetProject.Helpers;
using BetProject.Infra.Repositories;
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
                var wd = new FirefoxDriver(_configuration.DriverFirefoxPath, options, TimeSpan.FromDays(1));
                wd.Manage().Timeouts().PageLoad = new TimeSpan(10, 0, 0);
                webDrivers.Add(wd);
            }
            return webDrivers;
        }


        public async Task SalvaJogosAmanha(int qtdWebDrivers = 1, bool headless = false)
        {

            FirefoxOptions options = new FirefoxOptions();
            IWebDriver wd1 = new FirefoxDriver(_configuration.DriverFirefoxPath, options, TimeSpan.FromDays(1));
            IWebDriver wd2 = new FirefoxDriver(_configuration.DriverFirefoxPath, options, TimeSpan.FromDays(1));
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

                await Task.Delay(TimeSpan.FromMinutes(15));
                await rs1.SalvaJogosDeAmanha(true);
                ResultadosSiteHelper.CarregandoJogos = false;
                wd1.Dispose();
                wd2.Dispose();

            }
            catch (Exception e)
            {
                _telegramService.EnviaMensagemParaOGrupo("Erro app: " + e.Message);
                wd1.Dispose();
                wd2.Dispose();
            }
        }

        public async Task Iniciar(int qtdWebDrivers = 1, bool headless = false)
        {
            try
            {
                var rs = new ResultadoSiteService();

                //IWebDriver wd1 = SeleniumHelper.CreateDefaultWebDriver(headless);
                //IWebDriver wd2 = SeleniumHelper.CreateDefaultWebDriver(headless);

                //ResultadoSiteService rs1 = new ResultadoSiteService(wd1);
                //ResultadoSiteService rs2 = new ResultadoSiteService(wd2);
                //Console.WriteLine($"Salvando Jogos De Amanhã as {DateTime.Now}");
                //Task.Factory.StartNew(async () =>
                //{
                //    await rs2.SalvaJogosDeHoje(false, wd2);
                //});

                //await Task.Delay(60000);
                //await rs1.SalvaJogosDeHoje(true, wd1);

                //ResultadosSiteHelper.CarregandoJogos = false;
                //wd1.Dispose();
                //wd2.Dispose();

                rs.ReanalisaJogosDeHoje(); return;
                await rs.StartAnaliseLive();
            }
            catch (Exception e)
            {
                _telegramService.EnviaMensagemParaOGrupo(e.Message);
            }
        }


        public void Dispose()
        {
            WebDriverCollectiont.ForEach(wd => wd.Dispose());
        }
    }
}
