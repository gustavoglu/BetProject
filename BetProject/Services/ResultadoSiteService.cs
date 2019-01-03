using BetProject.Configurations;
using BetProject.Enums;
using BetProject.Helpers;
using BetProject.Infra.Repositories;
using BetProject.Models;
using BetProject.ObjectValues;
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
    public class ResultadoSiteService
    {
        private readonly IWebDriver _driver;
        private readonly SeleniumConfiguration _configuration;
        private readonly IdContainerRepository _idContainerRepository;
        private readonly JogoRepository _jogoRepository;
        private readonly JogoService _jogoService;
        private readonly AnaliseService _analiseService;

        public bool CarregandoJogos { get; set; } = false;
        public IWebDriver Driver { get { return _driver; } }

        public ResultadoSiteService(IWebDriver driver)
        {
            _driver = driver;

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var seleniumConfigurations = new SeleniumConfiguration();
            new ConfigureFromConfigurationOptions<SeleniumConfiguration>(
                configuration.GetSection("SeleniumConfiguration"))
                    .Configure(seleniumConfigurations);
            _configuration = seleniumConfigurations;

            _idContainerRepository = new IdContainerRepository();
            _jogoRepository = new JogoRepository();
            _jogoService = new JogoService(_driver);
            _analiseService = new AnaliseService();
        }

        private void NavegarParaSite(string site)
        {
            _driver.Navigate().GoToUrl(site);
        }

        private void RemoveTodasTabelasTabela()
        {
            var head_abs = _driver.FindElements(By.ClassName("head_ab")).ToList();
            if (!head_abs.Any()) return;
            int count = head_abs.Count - 1;
            while (count >= 0)
            {
                var h = head_abs[count];
                bool tabela = false;
                try
                {
                    tabela = h.FindElement(By.ClassName("stats-link"))
                                .FindElement(By.ClassName("stats-draw")) != null;
                }
                catch { }
                try
                {
                    if (tabela) h.FindElement(By.ClassName("expand-collapse-icon")).Click();
                }
                catch { }
                count--;
            }
        }

        private void ExpandiTodasTabelas()
        {
            var expandLeagueLink = _driver.FindElements(By.ClassName("expand-league-link")).ToList();
            if (!expandLeagueLink.Any()) return;
            int count = expandLeagueLink.Count - 1;
            while (count >= 0)
            {
                try
                {
                    var h = expandLeagueLink[count];
                    h.Click();

                }
                catch { }
                count--;
            }

        }

        private bool ApostaBet(IWebElement tr)
        {
            bool bet = false;
            try { bet = tr.FindElement(By.ClassName("clive")) != null; } catch { }
            return bet;
        }

        private bool Srf(string classInfo)
        {
            return classInfo.Contains("no-service-info");
        }

        private bool JogoCanceladoAdiadoOuEncerrado(string classInfo)
        {

            return classInfo.Contains("stage-finished");
        }

        private bool JogoClassificacao(string idLi4)
        {
            return idLi4.Contains("li-match-standings");
        }


        public List<IdJogo> ListaDeJogos(bool amanha = false)
        {
            var container = amanha ? _idContainerRepository.TrazerIdContainerAmanha() : _idContainerRepository.TrazerIdContainerHoje();
            var jogos = _jogoRepository.TrazJogosPorIds(container.Ids.Where(i => i.Ignorar == false).Select(ji => ji.Id).ToArray());
            var top4IdsMedia = jogos.OrderByDescending(j => j.MediaGolsTotal).Take(4).ToList();
            var jogosConfirmados = jogos.Where(j => j.UmTimeFazMaisGolEOutroSofreMaisGol || top4IdsMedia.Exists(i => i.Id == j.Id)).ToList();
            var idsJogos = container.Ids.Where(i => jogosConfirmados.Exists(j => j.IdJogoBet == i.Id)).Distinct().ToList();
            return idsJogos;
        }

        public async Task<IdContainer> SalvaProximosJogos()
        {
            var idContainerHoje = _idContainerRepository.TrazerIdContainerHoje();
            if (idContainerHoje == null) return null;

            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.Principal);
            await Task.Delay(2000);

            _driver.FindElement(By.ClassName("ifmenu-odds")).Click();
            await Task.Delay(2000);

            ExpandiTodasTabelas();

            RemoveTodasTabelasTabela();

            var trsJogos = _driver.FindElements(By.ClassName("stage-scheduled")).ToList();

            if (!trsJogos.Any()) return idContainerHoje;

            foreach (var tr in trsJogos)
            {
                string classInfo = tr.GetAttribute("class");
                string id = tr.GetAttribute("id").Substring(4);
                if (!Srf(classInfo) && !JogoCanceladoAdiadoOuEncerrado(classInfo))
                {

                    string horaInicio = tr.FindElement(By.ClassName("cell_ad")).Text;
                    IdJogo idJogo = new IdJogo(id, DateTime.Parse(horaInicio));
                    idContainerHoje.Ids.Add(idJogo);
                }

                _idContainerRepository.Salvar(idContainerHoje);
            }
            return idContainerHoje;

        }

        public async Task<IdContainer> SalvaIdsLive()
        {
            Console.WriteLine($"Salvando Ids Live as {DateTime.Now}");
            var idContainerHoje = _idContainerRepository.TrazerIdContainerHoje();
            if (idContainerHoje == null) return null;

            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.Principal);
            await Task.Delay(2000);

            _driver.FindElement(By.ClassName("ifmenu-live")).Click();
            await Task.Delay(2000);

            ExpandiTodasTabelas();

            RemoveTodasTabelasTabela();

            var jogosAceitos = ListaDeJogos();

            var trsJogos = _driver.FindElements(By.ClassName("stage-live")).ToList();
            if (!trsJogos.Any()) return idContainerHoje;

            idContainerHoje.IdsLive.Clear();
            foreach (var tr in trsJogos)
            {
                string id = tr.GetAttribute("id").Substring(4);

                if (jogosAceitos.Exists(j => j.Id == id))
                {

                    if (ApostaBet(tr))
                    {

                        string status = tr.FindElement(By.ClassName("cell_aa")).Text;
                        if (status != "Encerrado" && status != "Adiado")
                        {
                            string horaInicio = tr.FindElement(By.ClassName("cell_ad")).Text;
                            IdJogo idJogo = new IdJogo(id, DateTime.Parse(horaInicio));
                            idContainerHoje.IdsLive.Add(idJogo);
                        }
                    }
                }
            }

            _idContainerRepository.Salvar(idContainerHoje);
            return idContainerHoje;
        }

        private async Task<IdContainer> SalvaJogosIds(bool amanha = false)
        {
            NavegarParaSite(_configuration.Sites.Resultado.Principal);

            if (amanha)
                _driver.FindElement(By.ClassName("tomorrow")).Click();

            await Task.Delay(5000);
            try
            {
                ExpandiTodasTabelas();
            }
            catch { }

            List<IdJogo> idJogos = new List<IdJogo> { };

            var trsJogos = _driver.FindElements(By.ClassName("stage-scheduled")).ToList();
            var trsJogosLive = _driver.FindElements(By.ClassName("stage-live")).ToList();
            if (trsJogosLive.Any()) trsJogosLive.ForEach(j => trsJogos.Add(j));

            foreach (var tr in trsJogos)
            {
                string classInfo = tr.GetAttribute("class");

                if (!Srf(classInfo) && !JogoCanceladoAdiadoOuEncerrado(classInfo))
                {
                    string horaInicio = tr.FindElement(By.ClassName("cell_ad")).Text;
                    IdJogo idJogo = new IdJogo(tr.GetAttribute("id").Substring(4), DateTime.Parse(horaInicio));
                    idJogos.Add(idJogo);
                }
            }

            var idContainerHoje = _idContainerRepository.TrazerIdContainerHoje();
            if (amanha)
            {
                idContainerHoje = new IdContainer(idJogos.ToList(), DateTime.Now.Date.AddDays(1));
            }
            else
            {
                if (idContainerHoje != null) idContainerHoje.Ids = idJogos;
                else idContainerHoje = new IdContainer(idJogos.ToList(), DateTime.Now.Date);
            }

            _idContainerRepository.Salvar(idContainerHoje);
            return idContainerHoje;
        }

        public async Task SalvaJogosDeAmanha(bool descending = false)
        {
            Console.WriteLine($"Salvando Jogos De Amanhã as {DateTime.Now}");
            ResultadosSiteHelper.CarregandoJogos = true;
            var amanha = _idContainerRepository.TrazerIdContainerAmanha();
            if (amanha == null) amanha = await SalvaJogosIds(true);
            var ids = descending ? amanha.Ids.OrderByDescending(id => id.DataInicio.TimeOfDay) :
                                   amanha.Ids.OrderBy(id => id.DataInicio.TimeOfDay);

            try
            {
                foreach (var i in ids) await CriarOuAtualizaInfosJogo(i.Id, true);
            }
            catch
            {
                foreach (var i in ids) await CriarOuAtualizaInfosJogo(i.Id, true);
            }

            ResultadosSiteHelper.CarregandoJogos = false;
        }

        public void ReanalisaJogosDeHoje()
        {
            var container = _idContainerRepository.TrazerIdContainerHoje();
            var jogos = _jogoRepository.TrazJogosPorIds(container.Ids.Select(i => i.Id).ToArray());
            foreach (var jogo in jogos)
            {
                _jogoService.PreencheCamposAnaliseJogo(jogo);
                _analiseService.AnalisaMediaGolsMenorQue25(jogo);
                _analiseService.AnalisaSeMelhorJogo(jogo);
            }
        }

        public async Task CriarOuAtualizaInfosJogo(string id, bool amanha = false)
        {
            var idContainer = !amanha ? _idContainerRepository.TrazerIdContainerHoje() :
                                        _idContainerRepository.TrazerIdContainerAmanha();

            var jogoId = idContainer.Ids.FirstOrDefault(i => i.Id == id) ??
                         idContainer.IdsLive.FirstOrDefault(i => i.Id == id);

            var jogo = _jogoRepository.TrazerJogoPorIdBet(jogoId.Id);

            if (jogo != null)
                try
                {

                    if (jogo.Ignorar) return;
                    await _jogoService.AtualizaInformacoesBasicasJogo(jogo);
                    _jogoService.PreencheCamposAnaliseJogo(jogo);
                    _jogoRepository.Salvar(jogo);
                }
                catch (Exception e)
                {
                    jogoId.ErrorMessage = e.Message;
                    idContainer = amanha ? _idContainerRepository.TrazerIdContainerAmanha() : _idContainerRepository.TrazerIdContainerHoje();
                    if (idContainer == null) idContainer = _idContainerRepository.TrazerIdContainerHoje();
                    idContainer.IdsComErro.Add(jogoId);
                    _idContainerRepository.Salvar(idContainer);

                }
            else
                try
                {
                    await _jogoService.CriaNovoJogo(id);
                }
                catch (Exception e)
                {
                    jogoId.ErrorMessage = e.Message;
                    idContainer = amanha ? _idContainerRepository.TrazerIdContainerAmanha() : _idContainerRepository.TrazerIdContainerHoje();
                    if (idContainer == null) idContainer = _idContainerRepository.TrazerIdContainerHoje();
                    idContainer.IdsComErro.Add(jogoId);
                    _idContainerRepository.Salvar(idContainer);
                }
        }

        public async Task CarregaJogosDoDia(bool descending = false, bool headless = false)
        {
            while (ResultadosSiteHelper.CarregandoJogos)
            {
                await Task.Delay(400000);
            }

            var idContainer = _idContainerRepository.TrazerIdContainerHoje();
            if (!(DateTime.Now >= new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 19, 00, 00)) ||
                !((idContainer == null || !idContainer.Ids.Any()) && DateTime.Now >= new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 00, 00, 00))) return;


            FirefoxOptions options = new FirefoxOptions();
            IWebDriver wd1 = new FirefoxDriver(_configuration.DriverFirefoxPath, options);
            wd1.Manage().Timeouts().PageLoad = new TimeSpan(10, 0, 0);
            ResultadoSiteService rs1 = new ResultadoSiteService(wd1);
            if (headless) options.AddArgument("--headless");

            if (DateTime.Now >= new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 19, 00, 00))
            {
                Console.WriteLine($"Salvando Jogos Do Dia as {DateTime.Now}");
                Task.Factory.StartNew(async () =>
                {
                    await SalvaJogosDeAmanha();
                });

                await rs1.SalvaJogosDeAmanha(true);
                ResultadosSiteHelper.CarregandoJogos = false;
            }


            if (DateTime.Now >= new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 00, 00, 00))
            {

                if (idContainer == null || !idContainer.Ids.Any())
                {
                    ResultadosSiteHelper.CarregandoJogos = true;
                    idContainer = await SalvaJogosIds();



                    var ids = idContainer.Ids.OrderBy(i => i.DataInicio.TimeOfDay);
                    var idsDesc = idContainer.Ids.OrderByDescending(i => i.DataInicio.TimeOfDay);

                    Task.Factory.StartNew(async () =>
                    {
                        try
                        {
                            foreach (var id in ids)
                            {
                                await CriarOuAtualizaInfosJogo(id.Id, false);
                            }
                        }
                        catch
                        {
                            foreach (var id in ids)
                            {
                                await CriarOuAtualizaInfosJogo(id.Id, false);
                            }
                        }

                    });

                    try
                    {
                        foreach (var id in idsDesc)
                        {
                            await rs1.CriarOuAtualizaInfosJogo(id.Id, false);
                        }
                    }
                    catch
                    {
                        foreach (var id in idsDesc)
                        {
                            await rs1.CriarOuAtualizaInfosJogo(id.Id, false);
                        }
                    }


                }
                wd1.Dispose();
                ResultadosSiteHelper.CarregandoJogos = false;
            }
        }

        public bool JogoIgnorado(string idBet)
        {
            var container = _idContainerRepository.TrazerIdContainerHoje();
            return container.Ids.Exists(i => i.Id == idBet && i.Ignorar);
        }

        public async Task StartAnaliseLive(bool descending = false)
        {
            while (true)
            {
                await CarregaJogosDoDia(descending);
                var idContainer = _idContainerRepository.TrazerIdContainerHoje();

                await SalvaIdsLive();

                if (!idContainer.IdsLive.Any()) await Task.Delay(180000);

                var ids = descending ? idContainer.IdsLive.OrderByDescending(i => i.DataInicio.TimeOfDay) :
                                         idContainer.IdsLive.OrderBy(i => i.DataInicio.TimeOfDay);

                List<Jogo> jogos = new List<Jogo>();

                foreach (var i in ids)
                {
                    try
                    {
                        Console.WriteLine($"Analisando {i.Id} as {DateTime.Now}");
                        await CriarOuAtualizaInfosJogo(i.Id);
                        var jogo = _jogoRepository.TrazerJogoPorIdBet(i.Id);
                        if (jogo != null)
                        {
                            bool jogoProntoParaAnalise = _jogoRepository.JogoProntoParaAnalise(i.Id);
                            if (jogoProntoParaAnalise) _analiseService.AnalisaJogoLive(jogo);
                        }
                    }
                    catch (Exception e)
                    {
                        var msg = e.Message;
                        Console.WriteLine("Erro: " + e.Message + " IdBet: " + i.Id);

                    }
                }
            }
        }
    }
}
