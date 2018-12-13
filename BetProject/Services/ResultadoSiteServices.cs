using BetProject.Configurations;
using BetProject.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BetProject.Services
{
    public class ResultadoSiteServices
    {
        private readonly SeleniumConfiguration _configuration;
        private readonly IWebDriver _driver;
        public ResultadoSiteServices(SeleniumConfiguration configuration)
        {
            _configuration = configuration;
            FirefoxOptions options = new FirefoxOptions();
            options.AddArgument("--headless");

            _driver = new FirefoxDriver(_configuration.DriverFirefoxPath, options);

        }


        public void AbrirPagina()
        {
            // _driver.Manage()
            //     .Timeouts()
            //     .PageLoad = new TimeSpan(0,0,10);

            _driver
                .Navigate()
                .GoToUrl(_configuration.Sites.Resultado.Principal);
        }

        public void GuardaJogosDoDia()
        {
            List<Jogo> Jogos = new List<Jogo>();
            var expands = _driver.FindElements(By.ClassName("expand-league-link"));
            expands.ToList().ForEach(e => e.Click());

            var ligas = _driver.FindElements(By.TagName("table"));
            var tbodys = ligas.SelectMany(l => l.FindElements(By.TagName("tbody"))).ToList();
            foreach (var tbody in tbodys)
            {
                var minutosJogo = tbody.FindElement(By.ClassName("cell_aa")).Text;
                bool srf = false;
                try { srf = tbody.FindElement(By.ClassName("final_result_only")).Text == "SRF"; } catch { }

                if (!srf || minutosJogo != "Encerrado" || minutosJogo != "Adiado")
                {
                    var dataInicio = tbody.FindElement(By.ClassName("cell_ad")).Text;
                    var time1 = tbody.FindElement(By.ClassName("padr")).Text;
                    var time2 = tbody.FindElement(By.ClassName("padl")).Text;
                    var resultadoPrimeiroTempo = tbody.FindElement(By.ClassName("cell_sb")).Text;
                    var resultadoFinal = tbody.FindElement(By.ClassName("cell_sa")).Text;

                    Jogo jogo = new Jogo(DateTime.Parse(dataInicio), time1, time2, resultadoPrimeiroTempo, resultadoFinal, minutosJogo);
                    Jogos.Add(jogo);
                }
            }

        }
    }
}
