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
        private readonly TelegramService _telegramService;
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
            _telegramService = new TelegramService();
        }

        public ResultadoSiteService()
        {
            _idContainerRepository = new IdContainerRepository();
            _jogoRepository = new JogoRepository();
            _jogoService = new JogoService(_driver);
            _analiseService = new AnaliseService();
            _telegramService = new TelegramService();
        }

        private void NavegarParaSite(string site)
        {
            _driver.Navigate().GoToUrl(site);
        }


        public async Task AnaliseH2H()
        {
            await CarregaJogosDeAmanha();
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
            //var expandLeagueLink = _driver.FindElements(By.ClassName("expand-league-link")).ToList();
            //var expandLeagueLink = _driver.FindElements(By.ClassName("collapse-league")).ToList();
           
            //if (!expandLeagueLink.Any()) return;
            //expandLeagueLink.ForEach(e => e.Click());


            var expandLeagueLink = _driver.FindElements(By.ClassName("expand-league")).ToList();
            if (!expandLeagueLink.Any()) return;
            int count = expandLeagueLink.Count - 1;
            while (count >= 0)
            {
                var e = expandLeagueLink[count];

                e.Click();
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

        public List<Jogo> ListaDeJogos(bool amanha = false)
        {
            var container = amanha ? _idContainerRepository.TrazerIdContainerAmanha() : _idContainerRepository.TrazerIdContainerHoje();
            var jogos = _jogoRepository.TrazJogosPorIds(container.Ids.Select(ji => ji.Id).ToArray());

            var jogosFSOuDobro = jogos.Where(j => j.UmTimeFazMaisGolEOutroSofreMaisGolTotal).Distinct().ToList();

            var top4IdsMedia = jogos.Where(j => !jogosFSOuDobro.Exists(fs => fs.IdJogoBet == j.IdJogoBet))
                                .Distinct()
                                .OrderByDescending(j => j.MediaGolsTotal)
                                .Where(j => j.MediaGolsTotal > 3)
                                .Take(4).ToList();

            top4IdsMedia.ForEach(j => j.Observacoes = "Top 4 Media Jogos");
            jogosFSOuDobro.ForEach(j => j.Observacoes = "Jogos FeS");

            var jogosFT0560 = jogos.Where(j => TempoDiferencaJogo(j) >= 50 && TempoDiferencaJogo(j) < 80 && j.GolsTotal == 0 && !j.UmTimeFazMaisGolEOutroSofreMaisGol && !top4IdsMedia.Exists(i => i.Id == j.Id))
                .Distinct()
                .ToList();

            jogosFT0560.ForEach(j => j.Observacoes = "0x0 FT 0.5");

            var jogosConfirmados = jogos.Where(j => jogosFSOuDobro.Exists(i => i.Id == j.Id) || top4IdsMedia.Exists(i => i.Id == j.Id) || jogosFT0560.Exists(i => i.Id == j.Id)).Distinct().ToList();
            return jogosConfirmados;
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

        private async Task VerificaJogosNãoCarregados(bool amanha = false)
        {
            NavegarParaSite(_configuration.Sites.Resultado.Principal);

            if (amanha)
                _driver.FindElement(By.ClassName("tomorrow")).Click();

            var container = amanha ? _idContainerRepository.TrazerIdContainerAmanha() : _idContainerRepository.TrazerIdContainerHoje();

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
                string id = tr.GetAttribute("id").Substring(4);
                if (!container.Ids.Exists(i => i.Id == id))
                {
                    string classInfo = tr.GetAttribute("class");

                    if (!Srf(classInfo) && !JogoCanceladoAdiadoOuEncerrado(classInfo))
                    {
                        DateTime dataInicio = DateTime.Parse(tr.FindElement(By.ClassName("cell_ad")).Text);
                        if (amanha) dataInicio = dataInicio.AddDays(1);
                        container.IdsComErro.Add(new IdJogo(id, dataInicio));
                    }
                }
            }

            _idContainerRepository.Salvar(container);
        }

        public async Task<IdContainer> SalvaJogosIds(bool amanha = false)
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

            var idContainer = _idContainerRepository.TrazerIdContainerHoje();

            if (amanha)
            {
                idContainer = new IdContainer(idJogos, DateTime.Now.Date.AddDays(1));
            }
            else
            {
                if (idContainer != null) idContainer.Ids = idJogos;
                else idContainer = new IdContainer(idJogos, DateTime.Now.Date);
            }

            _idContainerRepository.Salvar(idContainer);

            foreach (var tr in trsJogos)
            {
                string id = tr.GetAttribute("id").Substring(4);
                string classInfo = tr.GetAttribute("class");

                if (!Srf(classInfo) && !JogoCanceladoAdiadoOuEncerrado(classInfo))
                {
                    DateTime dataInicio = DateTime.Parse(tr.FindElement(By.ClassName("cell_ad")).Text);
                    if (amanha) dataInicio = dataInicio.AddDays(1);
                    IdJogo idJogo = new IdJogo(id, dataInicio);
                    idContainer.Ids.Add(idJogo);
                }
            }

            _idContainerRepository.Salvar(idContainer);
            await VerificaJogosNãoCarregados(amanha);
            idContainer = amanha ? _idContainerRepository.TrazerIdContainerAmanha() : _idContainerRepository.TrazerIdContainerHoje();
            return idContainer;
        }

        public async Task SalvaJogosDeHoje(IdContainer container, bool descending = false, IWebDriver driver = null)
        {
            Console.WriteLine($"Salvando Jogos De Hoje as {DateTime.Now}");

            ResultadosSiteHelper.CarregandoJogos = true;

            var ids = descending ? container.Ids.OrderByDescending(id => id.DataInicio.TimeOfDay) :
                                   container.Ids.OrderBy(id => id.DataInicio.TimeOfDay);

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

        public async Task SalvaJogosDeAmanha(IdContainer container, bool descending = false, IWebDriver driver = null)
        {
            if (driver != null) _driver = driver;
            Console.WriteLine($"Salvando Jogos De Amanhã as {DateTime.Now}");

            ResultadosSiteHelper.CarregandoJogos = true;

            var ids = descending ? container.Ids.OrderByDescending(id => id.DataInicio.TimeOfDay) :
                                   container.Ids.OrderBy(id => id.DataInicio.TimeOfDay);

            try
            {
                foreach (var i in ids) await CriarOuAtualizaInfosJogo(i.Id,container.Id, true);
            }
            catch (Exception e)
            {
                foreach (var i in ids) await CriarOuAtualizaInfosJogo(i.Id, container.Id, true);
            }

            ResultadosSiteHelper.CarregandoJogos = false;
        }

        public async Task SalvaJogosDeHojeH2H(IdContainer container, bool descending = false, IWebDriver driver = null)
        {
            if (driver != null) _driver = driver;
            Console.WriteLine($"Salvando Jogos De Hoje as {DateTime.Now}");

            ResultadosSiteHelper.CarregandoJogos = true;

            var ids = descending ? container.Ids.OrderByDescending(id => id.DataInicio.TimeOfDay) :
                                   container.Ids.OrderBy(id => id.DataInicio.TimeOfDay);

            try
            {
                foreach (var i in ids) await CriarOuAtualizaInfosJogoH2H(i.Id, container.Id, true);
            }
            catch (Exception e)
            {
                foreach (var i in ids) await CriarOuAtualizaInfosJogoH2H(i.Id, container.Id, true);
            }

            ResultadosSiteHelper.CarregandoJogos = false;
        }


        public async Task SalvaJogosDeAmanhaH2H(IdContainer container, bool descending = false, IWebDriver driver = null)
        {
            if (driver != null) _driver = driver;
            Console.WriteLine($"Salvando Jogos De Amanhã as {DateTime.Now}");

            ResultadosSiteHelper.CarregandoJogos = true;

            var ids = descending ? container.Ids.OrderByDescending(id => id.DataInicio.TimeOfDay) :
                                   container.Ids.OrderBy(id => id.DataInicio.TimeOfDay);

            try
            {
                foreach (var i in ids) await CriarOuAtualizaInfosJogoH2H(i.Id, container.Id, true);
            }
            catch (Exception e)
            {
                _telegramService.EnviaMensagemParaOGrupo($"Erro: {e.Message}");
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

        public async void AnalisaJogosH2H(IdContainer container)
        {
            //var jogot = _jogoRepository.TrazerJogoPorIdBet("KU067KaT");
            //_analiseService.AnalisaOverH2H(jogot);return;

            var ids = container.Ids.OrderBy(i => i.DataInicio.TimeOfDay).ToList();
            var jogos = _jogoRepository.TrazJogosPorIds(container.Ids.Select(i => i.Id).ToArray()).OrderBy(j => j.DataInicio.TimeOfDay).ToList();

            //foreach (var jo in jogos)
            //{

            //    jo.Time1.QtdJogosH2H05 = jo.Time1.H2HInfos.Count(j => j.TotalGols == 1);
            //    jo.Time1.QtdJogosH2H15 = jo.Time1.H2HInfos.Count(j => j.TotalGols == 2);
            //    jo.Time1.QtdJogosH2H25 = jo.Time1.H2HInfos.Count(j => j.TotalGols >= 3);
            //    jo.Time2.QtdJogosH2H05 = jo.Time2.H2HInfos.Count(j => j.TotalGols == 1);
            //    jo.Time2.QtdJogosH2H15 = jo.Time2.H2HInfos.Count(j => j.TotalGols == 2);
            //    jo.Time2.QtdJogosH2H25 = jo.Time2.H2HInfos.Count(j => j.TotalGols >= 3);
            //    jo.Time1.QtdJogosUnderH2H25 = jo.Time1.H2HInfos.Count(j => j.TotalGols <= 2);
            //    jo.Time1.QtdJogosUnderH2H35 = jo.Time1.H2HInfos.Count(j => j.TotalGols <= 3);
            //    jo.Time1.QtdJogosH2HOver15 = jo.Time1.H2HInfos.Count(j => j.TotalGols >= 2);
            //    jo.Time1.QtdJogosH2HOver25 = jo.Time1.H2HInfos.Count(j => j.TotalGols >= 3);
            //    jo.Time2.QtdJogosUnderH2H25 = jo.Time2.H2HInfos.Count(j => j.TotalGols <= 2);
            //    jo.Time2.QtdJogosUnderH2H35 = jo.Time2.H2HInfos.Count(j => j.TotalGols <= 3);
            //    jo.Time2.QtdJogosH2HOver15 = jo.Time2.H2HInfos.Count(j => j.TotalGols >= 2);
            //    jo.Time2.QtdJogosH2HOver25 = jo.Time2.H2HInfos.Count(j => j.TotalGols >= 3);

            //    _jogoRepository.Salvar(jo);
            //}

            for (int i = 0; i < jogos.Count; i++)
            {
                var j = jogos[i];
                _analiseService.AnalisaOverH2H(j);
                await Task.Delay(200);
                _analiseService.AnalisaUnderH2H(j);
                await Task.Delay(200);
            }
        }

        public async void ReanalisaJogosDeHoje()
        {

            var jogot = _jogoRepository.TrazerJogoPorIdBet("jcRjngTL");
            _analiseService.AnalisaUnderH2H(jogot);return;
            _jogoService.PegaInformacoesH2H(jogot);
            _jogoRepository.Salvar(jogot);
            return;

            var containerh = _idContainerRepository.TrazerIdContainerHoje();
            var jogost = _jogoRepository.TrazJogosPorIds(containerh.Ids.Select(i => i.Id).ToArray());
            foreach (var j in jogost)
            {
                await _jogoService.PegaInformacoesH2H(j); _jogoRepository.Salvar(j);
            }
            return;

            //_jogoService.PreencheCamposAnaliseJogo(jogot);
            //_analiseService.AnalisaMediaGolsMenorQue25_2(jogot);
            //return;
            //if (_driver == null) _driver = SeleniumHelper.CreateDefaultWebDriver();
            var container = _idContainerRepository.TrazerPorId("IdContainers/1281-A");

            //foreach (var i in container.Ids)
            //{
            //    try
            //    {
            //        // 1.5
            //        _driver.Navigate().GoToUrl("https://www.resultados.com/jogo/ID/#classificacao;over_under;overall;1.5".Replace("ID", i.Id));
            //        var jogo = _jogoRepository.TrazerJogoPorIdBet(i.Id);
            //        await Task.Delay(2000);
            //        var tabelaAcimaAbaixoTotal15 = _driver.FindElement(By.Id("table-type-6-1.5"));
            //        var jogolist = new List<Time> { jogo.Time1, jogo.Time2 };
            //        _jogoService.PegaUltimoOver(1.5, tabelaAcimaAbaixoTotal15, EClassificacaoTipo.Total, jogolist);
            //        jogo.Time1 = jogolist[0];
            //        jogo.Time2 = jogolist[1];
            //        _jogoRepository.Salvar(jogo);
            //    }
            //    catch { }

            //}
            //return;

            var jogos = _jogoRepository.TrazJogosPorIds(container.Ids.Select(i => i.Id).ToArray());
            foreach (var jogo in jogos)
            {
                _jogoService.PreencheCamposAnaliseJogo(jogo);
                //_analiseService.AnalisaMediaGolsMenorQue25(jogo);
                //_analiseService.AnalisaMediaGolsMenorQue25_2(jogo);
                _analiseService.AnalisaSeMelhorJogo_2(jogo);
                //_analiseService.AnalisaSeMelhorJogo(jogo);
            }
        }

        public async Task CriarOuAtualizaInfosJogo2(string id, IWebElement tr, bool amanha = false)
        {
            Console.WriteLine($"Criando ou Atualizando ID: {id} as {DateTime.Now}");
            var idContainer = !amanha ? _idContainerRepository.TrazerIdContainerHoje() :
                                        _idContainerRepository.TrazerIdContainerAmanha();

            var jogo = _jogoRepository.TrazerJogoPorIdBet(id);

            if (jogo != null)
                try
                {
                    await _jogoService.AtualizaInformacoesBasicasJogo(jogo);
                    _jogoService.PreencheCamposAnaliseJogo(jogo);
                    _jogoRepository.Salvar(jogo);
                }
                catch (Exception e)
                {
                    //jogoId.ErrorMessage = e.Message;
                    //idContainer = amanha ? _idContainerRepository.TrazerIdContainerAmanha() : _idContainerRepository.TrazerIdContainerHoje();
                    //if (idContainer == null) idContainer = _idContainerRepository.TrazerIdContainerHoje();
                    //idContainer.IdsComErro.Add(jogoId);
                    //_idContainerRepository.Salvar(idContainer);

                }
            else
                try
                {
                    await _jogoService.CriaNovoJogo(id);
                }
                catch (Exception e)
                {
                    //jogoId.ErrorMessage = e.Message;
                    //idContainer = amanha ? _idContainerRepository.TrazerIdContainerAmanha() : _idContainerRepository.TrazerIdContainerHoje();
                    //if (idContainer == null) idContainer = _idContainerRepository.TrazerIdContainerHoje();
                    //idContainer.IdsComErro.Add(jogoId);
                    //_idContainerRepository.Salvar(idContainer);
                }
        }

        public async Task CriarOuAtualizaInfosJogoH2H(string id, string idContainerId, bool amanha = false, bool ignorar = true)
        {
            Console.WriteLine($"Criando ou Atualizando ID: {id} as {DateTime.Now}");
            var idContainer = _idContainerRepository.TrazerPorId(idContainerId);

            var jogoId = idContainer.Ids.FirstOrDefault(i => i.Id == id) ??
                         idContainer.IdsLive.FirstOrDefault(i => i.Id == id);

            var jogo = _jogoRepository.TrazerJogoPorIdBet(jogoId.Id);

            if (jogo != null)
                try
                {
                    if (jogo.Ignorar && ignorar) return;
                    await _jogoService.AtualizaInformacoesBasicasJogo(jogo);
                    _jogoRepository.Salvar(jogo);
                }
                catch (Exception e)
                {
                    jogoId.ErrorMessage = e.Message;
                    idContainer = _idContainerRepository.TrazerPorId(idContainerId);
                    idContainer.IdsComErro.Add(jogoId);
                    _idContainerRepository.Salvar(idContainer);

                }
            else
                try
                {
                    await _jogoService.CriaNovoJogoH2H(id);
                }
                catch (Exception e)
                {
                    jogoId.ErrorMessage = e.Message;
                    idContainer = _idContainerRepository.TrazerPorId(idContainerId);
                    idContainer.IdsComErro.Add(jogoId);
                    _idContainerRepository.Salvar(idContainer);
                }
        }

        public async Task CriarOuAtualizaInfosJogo(string id, string idContainerId ,bool amanha = false, bool ignorar = true)
        {
            Console.WriteLine($"Criando ou Atualizando ID: {id} as {DateTime.Now}");
            var idContainer = _idContainerRepository.TrazerPorId(idContainerId);

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
                    idContainer = _idContainerRepository.TrazerPorId(idContainerId);
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
                    idContainer = _idContainerRepository.TrazerPorId(idContainerId);
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


        public async Task CarregaJogosDeHoje(bool descending = false, bool headless = false)
        {
            while (ResultadosSiteHelper.CarregandoJogos)
            {
                await Task.Delay(400000);
            }

            _telegramService.EnviaMensagemParaOGrupo($"Carregando Jogos de Hoje {DateTime.Now.Date}");

            var container = _idContainerRepository.TrazerIdContainerHoje();
            if (container == null || !container.Ids.Any())
            {
                IWebDriver wd3 = SeleniumHelper.CreateDefaultWebDriver(headless);
                ResultadoSiteService rs3 = new ResultadoSiteService(wd3);
                container = await rs3.SalvaJogosIds();
                wd3.Dispose();
                GC.Collect();
            }

            IWebDriver wd1 = SeleniumHelper.CreateDefaultWebDriver(headless);
            ResultadoSiteService rs1 = new ResultadoSiteService(wd1);
            IWebDriver wd2 = SeleniumHelper.CreateDefaultWebDriver(headless);
            ResultadoSiteService rs2 = new ResultadoSiteService(wd2);

            Task.Factory.StartNew(async () =>
            {
                await rs2.SalvaJogosDeHoje(container, false, wd2);
            });

            await rs1.SalvaJogosDeHoje(container, true, wd1);

            ResultadosSiteHelper.CarregandoJogos = false;

            wd1.Dispose();
            wd2.Dispose();

            var jogos = _jogoRepository.TrazJogosPorIds(container.Ids.Select(ji => ji.Id).ToArray());
            var jogosFSOuDobro = jogos.Where(j => j.UmTimeFazMaisGolEOutroSofreMaisGolTotal).Distinct().ToList();
            var primeiroJogo = jogosFSOuDobro.OrderBy(p => p.DataInicio.TimeOfDay).FirstOrDefault();
            _telegramService.EnviaMensagemParaOGrupo($"{jogosFSOuDobro.Count} jogo(s) sera(ão) analisado(s) dia {DateTime.Now.Date} com Inicio as {primeiroJogo.DataInicio.TimeOfDay}");
            // await TentaCarregarJogosComErroHoje();

        }


        public async Task CarregaJogosDeHojeH2H(bool descending = false, bool headless = false, bool ignorarHorario = false)
        {
            while (ResultadosSiteHelper.CarregandoJogos)
            {
                await Task.Delay(400000);
            }

            var idContainer = _idContainerRepository.TrazerIdContainerHoje();

            if (idContainer != null && !ignorarHorario) return;

            bool depoisDasSete = DateTime.Now >= new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 19, 00, 00);

            if (depoisDasSete || ignorarHorario)
            {
                var data = DateTime.Now.Date.AddDays(1).Date;
                _telegramService.EnviaMensagemParaOGrupo($"Carregando Jogos de Hoje {data}");

                var container = _idContainerRepository.TrazerIdContainerHoje();
                if (container == null || !container.Ids.Any())
                {
                    IWebDriver wd3 = SeleniumHelper.CreateDefaultWebDriver(headless);
                    ResultadoSiteService rs3 = new ResultadoSiteService(wd3);
                    container = await rs3.SalvaJogosIds(true);
                    wd3.Dispose();
                    GC.Collect();
                }

                IWebDriver wd1 = SeleniumHelper.CreateDefaultWebDriver(headless);
                ResultadoSiteService rs1 = new ResultadoSiteService(wd1);
                IWebDriver wd2 = SeleniumHelper.CreateDefaultWebDriver(headless);
                ResultadoSiteService rs2 = new ResultadoSiteService(wd2);

                await Task.Delay(5000);
                Console.WriteLine($"Salvando Jogos De Hoje as {DateTime.Now}");
                Task.Factory.StartNew(async () =>
                {
                    await rs2.SalvaJogosDeHojeH2H(container, false, wd2);
                });

                await rs1.SalvaJogosDeHojeH2H(container, true, wd1);

                AnalisaJogosH2H(container);

                ResultadosSiteHelper.CarregandoJogos = false;

                wd1.Dispose();
                wd2.Dispose();
            }
        }

        public async Task CarregaJogosDeAmanhaH2H(bool descending = false, bool headless = false, bool ignorarHorario = false)
        {
            while (ResultadosSiteHelper.CarregandoJogos)
            {
                await Task.Delay(400000);
            }

            var idContainer = _idContainerRepository.TrazerIdContainerAmanha();

            if (idContainer != null && !ignorarHorario) return;

            bool depoisDasSete = DateTime.Now >= new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 19, 00, 00);

            if (depoisDasSete || ignorarHorario)
            {
                var data = DateTime.Now.Date.AddDays(1).Date;
                _telegramService.EnviaMensagemParaOGrupo($"Carregando Jogos de Amanhã {data}");

                var container = _idContainerRepository.TrazerIdContainerAmanha();
                if (container == null || !container.Ids.Any())
                {
                    IWebDriver wd3 = SeleniumHelper.CreateDefaultWebDriver(headless);
                    ResultadoSiteService rs3 = new ResultadoSiteService(wd3);
                    container = await rs3.SalvaJogosIds(true);
                    wd3.Dispose();
                    GC.Collect();
                }

                IWebDriver wd1 = SeleniumHelper.CreateDefaultWebDriver(headless);
                ResultadoSiteService rs1 = new ResultadoSiteService(wd1);
                //IWebDriver wd2 = SeleniumHelper.CreateDefaultWebDriver(headless);
                //ResultadoSiteService rs2 = new ResultadoSiteService(wd2);

                await Task.Delay(5000);
                Console.WriteLine($"Salvando Jogos De Amanhã as {DateTime.Now}");
                //Task.Factory.StartNew(async () =>
                //{
                //    await rs2.SalvaJogosDeAmanhaH2H(container, false, wd2);
                //});

                await rs1.SalvaJogosDeAmanhaH2H(container, true, wd1);
                AnalisaJogosH2H(container);
                ResultadosSiteHelper.CarregandoJogos = false;

                wd1.Dispose();
                //wd2.Dispose();
            }
        }


        public async Task CarregaJogosDeAmanha(bool descending = false, bool headless = false, bool ignorarHorario = false)
        {
            while (ResultadosSiteHelper.CarregandoJogos)
            {
                await Task.Delay(400000);
            }

            var idContainer = _idContainerRepository.TrazerIdContainerAmanha();

            if (idContainer != null && !ignorarHorario) return;

            bool depoisDasSete = DateTime.Now >= new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 19, 00, 00);

            if (depoisDasSete || ignorarHorario)
            {
                var data =  DateTime.Now.Date.AddDays(1).Date;
                _telegramService.EnviaMensagemParaOGrupo($"Carregando Jogos de Amanhã {data}");

                var container = _idContainerRepository.TrazerIdContainerAmanha();
                if (container == null || !container.Ids.Any())
                {
                    IWebDriver wd3 = SeleniumHelper.CreateDefaultWebDriver(headless);
                    ResultadoSiteService rs3 = new ResultadoSiteService(wd3);
                    container = await rs3.SalvaJogosIds(true);
                    wd3.Dispose();
                    GC.Collect();
                }

                IWebDriver wd1 = SeleniumHelper.CreateDefaultWebDriver(headless);
                ResultadoSiteService rs1 = new ResultadoSiteService(wd1);
                IWebDriver wd2 = SeleniumHelper.CreateDefaultWebDriver(headless);
                ResultadoSiteService rs2 = new ResultadoSiteService(wd2);

                await Task.Delay(5000);
                Console.WriteLine($"Salvando Jogos De Amanhã as {DateTime.Now}");
                Task.Factory.StartNew(async () =>
                {
                    await rs2.SalvaJogosDeAmanha(container, false, wd2);
                });

                await rs1.SalvaJogosDeAmanha(container, true, wd1);

                ResultadosSiteHelper.CarregandoJogos = false;

                wd1.Dispose();
                wd2.Dispose();

                var jogos = _jogoRepository.TrazJogosPorIds(container.Ids.Select(ji => ji.Id).ToArray());
                var jogosFSOuDobro = jogos.Where(j => j.UmTimeFazMaisGolEOutroSofreMaisGolTotal).Distinct().ToList();
                var primeiroJogo = jogosFSOuDobro.OrderBy(p => p.DataInicio.TimeOfDay).FirstOrDefault();
                _telegramService.EnviaMensagemParaOGrupo($"{jogosFSOuDobro.Count} jogo(s) sera(ão) analisado(s) dia {data} com Inicio as {primeiroJogo.DataInicio.TimeOfDay}");

                await TentaCarregarJogosComErroHoje();
            }
        }

        public async Task TentaCarregarJogosComErroHoje(bool descending = false, bool headless = false)
        {
            while (ResultadosSiteHelper.CarregandoJogos)
            {
                await Task.Delay(400000);
            }

            var idContainer = _idContainerRepository.TrazerIdContainerHoje() ?? _idContainerRepository.TrazerIdContainerAmanha();

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
            diferenca = diferenca > 60 ? diferenca - 18 : diferenca;
            return diferenca;
        }

        double TempoDiferencaJogo(Jogo j)
        {
            var diferenca = (DateTime.Now - j.DataInicio).TotalMinutes;
            diferenca = diferenca >= 60 ? diferenca - 15 : diferenca;
            return Math.Ceiling(diferenca);
        }

        public void AtualizaJogosComErros(IdContainer container = null, bool amanha = false)
        {
            container = container == null ? amanha ? _idContainerRepository.TrazerIdContainerAmanha() :
                                                     _idContainerRepository.TrazerIdContainerHoje() :
                                                     container;

            JogoRepository jr = new JogoRepository();
            var idsJogosComErro = container.IdsComErro.Select(i => i.Id).Distinct().ToList();
            var jogos = jr.TrazJogosPorIds(idsJogosComErro.ToArray());
            foreach (var i in idsJogosComErro)
            {
                var id = container.IdsComErro.FirstOrDefault(j => j.Id == i);
                var jogoPronto = _jogoRepository.JogoProntoParaAnalise(i);
                if (jogoPronto) container.IdsComErro.Remove(id);
            }

            _idContainerRepository.Salvar(container);
        }

        public async Task StartAnaliseLive(bool descending = false)
        {
            while (true)
            {
                GC.Collect(); ;
                await CarregaJogosDeAmanha(descending, true);
                var jogos = ListaDeJogos().Where(i => TempoDiferencaJogo(i) > 1 && TempoDiferencaJogo(i) < 80).ToList();
                if (!jogos.Any())
                {
                    Console.WriteLine($"Nenhum Jogo Para Analisar no Momento as {DateTime.Now} Aguardando 3 Minutos...");
                    await Task.Delay(TimeSpan.FromMinutes(3));
                };

                if (jogos.Any())
                {
                    if (jogos.Count > 5)
                    {
                        if (this._driver != null)
                        {
                            this._driver.Dispose();
                            this._driver = SeleniumHelper.CreateDefaultWebDriver(true);
                        }
                    }

                    if(this._driver == null) this._driver = SeleniumHelper.CreateDefaultWebDriver(true);
                    _jogoService = new JogoService(_driver);
                    foreach (var i in jogos)
                    {
                        var minutagem = TempoDiferencaJogo(i);

                        try
                        {
                            Console.WriteLine($"Analisando ID: {i.IdJogoBet} as {DateTime.Now}");
                            await CriarOuAtualizaInfosJogo(i.IdJogoBet);
                            var jogo = _jogoRepository.TrazerJogoPorIdBet(i.IdJogoBet);
                            if (jogo != null)
                            {
                                jogo.Minutos = minutagem.ToString();
                                bool jogoProntoParaAnalise = _jogoRepository.JogoProntoParaAnalise(i.IdJogoBet);
                                if (jogoProntoParaAnalise)
                                {
                                    jogo.Observacoes = jogos.FirstOrDefault(j => j.IdJogoBet == jogo.IdJogoBet).Observacoes;
                                    _analiseService.AnalisaJogoLive(jogo);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            var msg = e.Message;
                            Console.WriteLine("Erro: " + e.Message + " IdBet: " + i.IdJogoBet);
                            _driver.Dispose();
                        }

                    }
                }
            }
        }
    }
}
