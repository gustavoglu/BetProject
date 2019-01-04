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
        private IWebDriver _driver;
        private readonly SeleniumConfiguration _configuration;
        private readonly IdContainerRepository _idContainerRepository;
        private readonly JogoRepository _jogoRepository;
        private JogoService _jogoService;
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

        public ResultadoSiteService()
        {
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
            var jogos = _jogoRepository.TrazJogosPorIds(container.Ids.Select(ji => ji.Id).ToArray());
            var jogosFS = jogos.Where(j => j.UmTimeFazMaisGolEOutroSofreMaisGol).ToList();

            var top4IdsMedia = jogos.Where(j => !jogosFS.Exists(fs => fs.IdJogoBet ==j.IdJogoBet))
                                .Distinct()
                                .OrderByDescending(j => j.MediaGolsTotal)
                                .Take(4).ToList();

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

        public async Task<IdContainer> SalvaIdsLive2()
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
                        var jogo = _jogoRepository.TrazerJogoPorIdBet(id);
                        string minutos = tr.FindElement(By.ClassName("cell_aa"))
                                            .FindElement(By.TagName("span")).Text;

                        string score = tr.FindElement(By.ClassName("cell_sa")).Text;

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
                        await CriarOuAtualizaInfosJogo2(id, tr);
                        var jogo = _jogoRepository.TrazerJogoPorIdBet(id);
                        if (jogo != null)
                        {
                            bool jogoProntoParaAnalise = _jogoRepository.JogoProntoParaAnalise(id);
                            if (jogoProntoParaAnalise) _analiseService.AnalisaJogoLive(jogo);
                        }


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
                    DateTime dataInicio = DateTime.Parse(tr.FindElement(By.ClassName("cell_ad")).Text);
                    if (amanha) dataInicio = dataInicio.AddDays(1);
                    IdJogo idJogo = new IdJogo(tr.GetAttribute("id").Substring(4), dataInicio);
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

        public async Task SalvaJogosDeHoje(bool descending = false, IWebDriver driver = null)
        {

            Console.WriteLine($"Salvando Jogos De Hoje as {DateTime.Now}");

            ResultadosSiteHelper.CarregandoJogos = true;

            var hoje = _idContainerRepository.TrazerIdContainerHoje();
            if (hoje == null) hoje = await SalvaJogosIds();
            var ids = descending ? hoje.Ids.OrderByDescending(id => id.DataInicio.TimeOfDay) :
                                   hoje.Ids.OrderBy(id => id.DataInicio.TimeOfDay);

            try
            {
                foreach (var i in ids) await CriarOuAtualizaInfosJogo(i.Id);
            }
            catch
            {
                foreach (var i in ids) await CriarOuAtualizaInfosJogo(i.Id);
            }

            ResultadosSiteHelper.CarregandoJogos = false;
        }

        public async Task SalvaJogosDeAmanha(bool descending = false, IWebDriver driver = null)
        {
            if (driver != null) _driver = driver;
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

        public async Task CriarOuAtualizaInfosJogo2(string id, IWebElement tr, bool amanha = false)
        {
            Console.WriteLine($"Criando ou Atualizando ID: {id} as {DateTime.Now}");
            var idContainer = !amanha ? _idContainerRepository.TrazerIdContainerHoje() :
                                        _idContainerRepository.TrazerIdContainerAmanha();

            var jogoId = idContainer.Ids.FirstOrDefault(i => i.Id == id) ??
                         idContainer.IdsLive.FirstOrDefault(i => i.Id == id);

            var jogo = _jogoRepository.TrazerJogoPorIdBet(jogoId.Id);

            if (jogo != null)
                try
                {
                    if (jogo.Ignorar) return;
                    _jogoService.AtualizaInformacoesBasicasJogo2(tr, jogo);
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

        public async Task CriarOuAtualizaInfosJogo(string id, bool amanha = false, bool ignorar = true)
        {
            Console.WriteLine($"Criando ou Atualizando ID: {id} as {DateTime.Now}");
            var idContainer = !amanha ? _idContainerRepository.TrazerIdContainerHoje() :
                                        _idContainerRepository.TrazerIdContainerAmanha();

            var jogoId = idContainer.Ids.FirstOrDefault(i => i.Id == id) ??
                         idContainer.IdsLive.FirstOrDefault(i => i.Id == id);

            var jogo = _jogoRepository.TrazerJogoPorIdBet(jogoId.Id);

            if (jogo != null)
                try
                {
                    if (jogo.Ignorar && ignorar) return;
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

        public async Task CarregaJogosDeAmanha(bool descending = false, bool headless = false)
        {
            while (ResultadosSiteHelper.CarregandoJogos)
            {
                await Task.Delay(400000);
            }

            var idContainer = _idContainerRepository.TrazerIdContainerAmanha();

            if (idContainer != null) return;

            bool depoisDasSete = DateTime.Now >= new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 19, 00, 00);

            if (depoisDasSete)
            {
                IWebDriver wd1 = SeleniumHelper.CreateDefaultWebDriver(headless);
                IWebDriver wd2 = SeleniumHelper.CreateDefaultWebDriver(headless);

                ResultadoSiteService rs1 = new ResultadoSiteService(wd1);
                ResultadoSiteService rs2 = new ResultadoSiteService(wd2);
                Console.WriteLine($"Salvando Jogos De Amanhã as {DateTime.Now}");
                Task.Factory.StartNew(async () =>
                {
                    await rs2.SalvaJogosDeAmanha(false, wd2);
                });

                await rs1.SalvaJogosDeAmanha(true, wd1);

                ResultadosSiteHelper.CarregandoJogos = false;
                wd1.Dispose();
                wd2.Dispose();
                await TentaCarregarJogosComErroHoje();
            }
        }

        public async Task TentaCarregarJogosComErroHoje(bool descending = false, bool headless = false)
        {
            while (ResultadosSiteHelper.CarregandoJogos)
            {
                await Task.Delay(400000);
            }

            var idContainer = _idContainerRepository.TrazerIdContainerHoje();

            if (idContainer == null) return;

            IWebDriver wd1 = SeleniumHelper.CreateDefaultWebDriver(headless);
            IWebDriver wd2 = SeleniumHelper.CreateDefaultWebDriver(headless);

            ResultadoSiteService rs1 = new ResultadoSiteService(wd1);
            ResultadoSiteService rs2 = new ResultadoSiteService(wd2);
            Console.WriteLine($"Tentando carregar novamente Jogos De Hoje as {DateTime.Now}");
            Task.Factory.StartNew(async () =>
            {

                try
                {
                    foreach (var i in idContainer.IdsComErro.OrderBy(i => i.DataInicio).Select(i => i.Id).Distinct()) await rs1.CriarOuAtualizaInfosJogo(i, false, false);
                }
                catch
                {
                    foreach (var i in idContainer.IdsComErro.OrderBy(i => i.DataInicio).Select(i => i.Id).Distinct()) await rs1.CriarOuAtualizaInfosJogo(i, false, false);
                }
            });

            try
            {
                foreach (var i in idContainer.IdsComErro.OrderByDescending(i => i.DataInicio).Select(i => i.Id).Distinct()) await rs2.CriarOuAtualizaInfosJogo(i, false, false);
            }
            catch
            {
                foreach (var i in idContainer.IdsComErro.OrderByDescending(i => i.DataInicio).Select(i => i.Id).Distinct()) await rs2.CriarOuAtualizaInfosJogo(i, false, false);
            }

            ResultadosSiteHelper.CarregandoJogos = false;
            wd1.Dispose();
            wd2.Dispose();

        }

        public bool JogoIgnorado(string idBet)
        {
            var container = _idContainerRepository.TrazerIdContainerHoje();
            return container.Ids.Exists(i => i.Id == idBet && i.Ignorar);
        }

        double TempoDiferencaJogo(IdJogo i)
        {
            var diferenca = DateTime.Now.Date > i.DataInicio.Date ? (DateTime.Now - i.DataInicio.AddDays(1)).TotalMinutes :
                                    (DateTime.Now - i.DataInicio).TotalMinutes;
            return diferenca;
        }

        public async Task StartAnaliseLive(bool descending = false)
        {
            while (true)
            {
                GC.Collect(); ;
                await CarregaJogosDeAmanha(descending);
                var jogos = ListaDeJogos().Where(i => TempoDiferencaJogo(i) > 1 && TempoDiferencaJogo(i) < 80 ).ToList();
                if (!jogos.Any())
                {
                    Console.WriteLine($"Nenhum Jogo Para Analisar no Momento as {DateTime.Now} Aguardando 3 Minutos...");
                    await Task.Delay(TimeSpan.FromMinutes(3));
                };

                if (jogos.Any())
                {
                    this._driver = SeleniumHelper.CreateDefaultWebDriver(true);
                    _jogoService = new JogoService(_driver);
                    foreach (var i in jogos)
                    {
                        var diferenca = TempoDiferencaJogo(i);
                        if (diferenca < 80 && diferenca > 1)
                        {
                            var minutagem = Math.Ceiling(diferenca > 60 ? diferenca - 18 : diferenca);

                            try
                            {
                                Console.WriteLine($"Analisando ID: {i.Id} as {DateTime.Now}");
                                await CriarOuAtualizaInfosJogo(i.Id);
                                var jogo = _jogoRepository.TrazerJogoPorIdBet(i.Id);
                                if (jogo != null)
                                {
                                    jogo.Minutos = minutagem.ToString();
                                    bool jogoProntoParaAnalise = _jogoRepository.JogoProntoParaAnalise(i.Id);
                                    if (jogoProntoParaAnalise) _analiseService.AnalisaJogoLive(jogo);
                                }
                            }
                            catch (Exception e)
                            {
                                var msg = e.Message;
                                Console.WriteLine("Erro: " + e.Message + " IdBet: " + i.Id);
                                _driver.Dispose();
                            }
                        }
                    }
                    _driver.Dispose();
                }
            }
        }
    }
}
