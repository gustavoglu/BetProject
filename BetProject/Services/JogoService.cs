using BetProject.Configurations;
using BetProject.Enums;
using BetProject.Helpers;
using BetProject.Infra.Repositories;
using BetProject.Models;
using BetProject.ObjectValues;
using BetProject.Validations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BetProject.Services
{
    public class JogoService
    {
        private IWebDriver _driver;
        private readonly SeleniumConfiguration _configuration;
        private readonly IdContainerRepository _idContainerRepository;
        private readonly JogoRepository _jogoRepository;
        private readonly AnaliseService _analiseService;

        public JogoService(IWebDriver driver)
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
            _analiseService = new AnaliseService();
        }
        public string GetLinkResultadosId(string id)
        {
            return _configuration.Sites.Resultado.ResumoJogo.Replace("ID", id);
        }
        public string PegaStatus()
        {

            try
            {
                return _driver.FindElement(By.ClassName("mstat")).FindElements(By.TagName("span")).Count > 0 ?
                             _driver.FindElement(By.ClassName("mstat")).FindElements(By.TagName("span"))[0]?.Text :
                             "";
            }
            catch
            {

                return "Sem status";
            }


        }


        public async Task PegaInformacoesH2H(Jogo jogo)
        {
            //if (_driver == null) _driver = SeleniumHelper.CreateDefaultWebDriver();

            _driver.Navigate().GoToUrl($"https://www.resultados.com/jogo/{jogo.IdJogoBet}#h2h;overall");
            await Task.Delay(2000);
            var showMores = _driver.FindElements(By.ClassName("show_more")) ;
            showMores[0].Click();
            showMores[1].Click();

            //var tabelas = _driver.FindElements(By.ClassName("h2h-wrapper"));
            var trsTime1 = _driver.FindElement(By.ClassName("h2h_home"))
                                .FindElement(By.TagName("tbody"))
                                .FindElements(By.TagName("tr"));
            var trsTime2 = _driver.FindElement(By.ClassName("h2h_away"))
                                .FindElement(By.TagName("tbody"))
                                .FindElements(By.TagName("tr"));

            List<H2HInfo> h2hInfoListTime1 = new List<H2HInfo>();
            List<H2HInfo> h2hInfoListTime2 = new List<H2HInfo>();

            foreach (var tr in trsTime1.Take(10))
                h2hInfoListTime1.Add(CriarH2HInfo(tr));

            foreach (var tr in trsTime2.Take(10))
                h2hInfoListTime2.Add(CriarH2HInfo(tr));

            jogo.Time1.H2HInfos = h2hInfoListTime1;
            jogo.Time2.H2HInfos = h2hInfoListTime2;
            jogo.Time1.GolsRealizadosH2H = h2hInfoListTime1.Sum(j => j.GolsTime1);
            jogo.Time2.GolsRealizadosH2H = h2hInfoListTime2.Sum(j => j.GolsTime1);
            jogo.Time1.GolsSofridosH2H = h2hInfoListTime1.Sum(j => j.GolsTime2);
            jogo.Time2.GolsSofridosH2H = h2hInfoListTime2.Sum(j => j.GolsTime2);
            jogo.Time1.PercOverUltimosJogos = (new decimal(h2hInfoListTime1.Count(j => j.TotalGols > 2)) / new decimal (h2hInfoListTime1.Count)) * new decimal(100);
            jogo.Time2.PercOverUltimosJogos = (new decimal(h2hInfoListTime2.Count(j => j.TotalGols > 2)) / new decimal(h2hInfoListTime2.Count)) * new decimal(100);
        }

        private H2HInfo CriarH2HInfo(IWebElement tr)
        {
            var nomes = tr.FindElements(By.ClassName("name"));
            string t1Nome = nomes[0].Text;
            string t2Nome = nomes[1].Text;

            bool time1Principal = Time1PrincipalH2H(nomes);
            string score = tr.FindElement(By.ClassName("score"))
                            .FindElement(By.TagName("strong")).Text;
            H2HInfo i = new H2HInfo();
            i.Time1 = time1Principal ? t1Nome : t2Nome;
            i.Time2 = time1Principal ? t2Nome : t1Nome;
            i.GolsTime1 = time1Principal ? TimeHelper.GolsRealizadosConvert(score) :
                                           TimeHelper.GolsSofridosConvert(score);
            i.GolsTime2 = time1Principal ? TimeHelper.GolsSofridosConvert(score) :
                                           TimeHelper.GolsRealizadosConvert(score);

            i.Vencedor = i.GolsTime1 > i.GolsTime2;
            i.Empate = i.GolsTime1 == i.GolsTime2;
            i.TotalGols = i.GolsTime1 + i.GolsTime2;
            return i;
        }

        private bool Time1PrincipalH2H(ICollection<IWebElement> nomes)
        {

            string classInfo = nomes.ToArray()[0].GetAttribute("class");
            return classInfo.Contains("highTeam");

        }


        public void AtualizaInformacoesBasicasJogo2(IWebElement tr, Jogo jogo)
        {
            string minutos = "";
            string score = "";
            try
            {
                minutos = tr.FindElement(By.ClassName("cell_aa")).FindElement(By.TagName("span")).Text;
            }
            catch { }

            try
            {
                score = tr.FindElement(By.ClassName("cell_sa")).Text;
            }
            catch { }
            jogo.Status = JogoHelper.StatusJogo(minutos) ? minutos : "Sem Status";
            jogo.Minutos = JogoHelper.StatusJogo(minutos) ? "0" : minutos;
            jogo.GolsTime1 = TimeHelper.GolsScoreConvert(score, true);
            jogo.GolsTime2 = TimeHelper.GolsScoreConvert(score, false);
        }


        public async Task AtualizaInformacoesBasicasJogo(Jogo jogo)
        {
            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogo.Replace("ID", jogo.IdJogoBet));
            await Task.Delay(2000);

            int score1 = 0;
            int score2 = 0;
            try
            {
                var scores = _driver.FindElements(By.ClassName("scoreboard"));
                score1 = int.Parse(scores[0]?.Text);
                score2 = int.Parse(scores[1]?.Text);
            }
            catch { };

            jogo.GolsTime1 = score1;
            jogo.GolsTime2 = score2;
        }

        public async Task CriaNovoJogo(string idBet)
        {
            TimeServices ts = new TimeServices(_driver);
            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogo.Replace("ID", idBet));
            await Task.Delay(2000);

            if (!JogoClassificacao() || JogoSemJogosParaAnalise(idBet))
            {
                _idContainerRepository.IgnoraIdJogo(idBet);
                return;
            }

            List<Time> times = ts.CriaTimes(idBet);
            if (times == null) return;
            Jogo jogo = await CriaJogo(idBet, times);
            if (jogo == null) return;

            await PegaInfosClassficacao(idBet, times);

            if (!jogo.Time1.Classificacoes.Any() || !jogo.Time2.Classificacoes.Any())
            {
                _idContainerRepository.IgnoraIdJogo(idBet);
                return;
            }

            await PegaInfosAcimaAbaixo(idBet, times, jogo.GolsTime1 + jogo.GolsTime2);

            PreencheCamposAnaliseJogo(jogo);

            if (jogo.TimesComPoucosJogos)
            {
                _idContainerRepository.IgnoraIdJogo(idBet);
                return;
            }

            _analiseService.AnalisaMediaGolsMenorQue25(jogo);
            _analiseService.AnalisaMediaGolsMenorQue25_2(jogo);
            _analiseService.AnalisaSeMelhorJogo(jogo);
            _analiseService.AnalisaSeMelhorJogo_2(jogo);
            _jogoRepository.Salvar(jogo);

        }

        public bool JogoSemJogosParaAnalise(string idBet)
        {
            try
            {
                _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Total_05.Replace("ID", idBet));
                return _driver.FindElement(By.Id("tabitem-over_under-overall")) == null;
            }
            catch { return true; }
        }

        private bool JogoClassificacao()
        {
            try
            {
                string idLi4 = _driver.FindElement(By.ClassName("li4")).GetAttribute("id");

                return idLi4.Contains("li-match-standings");
            }
            catch { return false; }
        }

        private async Task<Jogo> CriaJogo(string idBet, List<Time> times)
        {
            var data = _driver.FindElement(By.Id("utime"))?.Text;
            var ligaTitulo = _driver.FindElement(By.ClassName("fleft"))
                                .FindElements(By.TagName("span"))[1]?.Text;

            var jogo = new Jogo(idBet, DateTime.Parse(data), ligaTitulo, "", "");
            jogo.Time1 = times[0];
            jogo.Time2 = times[1];

            await AtualizaInformacoesBasicasJogo(jogo);

            jogo.DataImportacao = DateTime.Now.Date;

            return jogo;
        }

        private async Task PegaInfosClassficacao(string idBet, List<Time> times)
        {
            //Classificação
            // Total
            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogoClassificacao_Total.Replace("ID", idBet));
            await Task.Delay(1000);
            _driver.FindElement(By.Id("tabitem-table")).Click();
            var tabelaClassificacaoTotal = _driver.FindElement(By.Id("table-type-1"));
            bool result = CriaClassificacao(tabelaClassificacaoTotal, EClassificacaoTipo.Total, times);

        }

        private bool CriaClassificacao(IWebElement tabelaClassificacao, EClassificacaoTipo tipo, List<Time> times)
        {
            var trsTotal = tabelaClassificacao.FindElements(By.ClassName("col_rank"));
            var trTimes = tabelaClassificacao.FindElements(By.ClassName("highlight"));
            foreach (var tr in trTimes)
            {
                var empates = tr.FindElements(By.ClassName("form-d"));
                var derrotas = tr.FindElements(By.ClassName("form-l"));
                var vitorias = tr.FindElements(By.ClassName("form-w"));

                var qtdJogos = empates.Count + derrotas.Count + vitorias.Count;

                if (qtdJogos < 4) return false;

                var nomeTime = tr.FindElement(By.ClassName("team_name_span"))
                                    .FindElement(By.TagName("a")).Text;

                var lugar = int.Parse(tr.FindElement(By.ClassName("col_rank")).Text.Replace(".", ""));
                var gols = tr.FindElements(By.ClassName("col_goals"))[0].Text;

                var asTag = tr.FindElement(By.ClassName("form"))
                                .FindElements(By.TagName("a"));


                Classificacao classif = new Classificacao(tipo, vitorias.Count, empates.Count, derrotas.Count, qtdJogos,
                                                            lugar, trsTotal.Count - 1, gols);

                times.FirstOrDefault(t => t.Nome == nomeTime).Classificacoes.Add(classif);
            }
            return true;
        }

        public async Task PegaInfosAcimaAbaixo(string idBet, List<Time> times, int totalGols, bool driverParalelo = false)
        {
            // Total
            // 0.5
            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Total_05.Replace("ID", idBet));
            await Task.Delay(2000);
            var tabelaAcimaAbaixoTotal05 = _driver.FindElement(By.Id("table-type-6-0.5"));
            CriaAcimaAbaixoTotal(0.5, tabelaAcimaAbaixoTotal05, EClassificacaoTipo.Total, times);

            // 1.5
            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Total_15.Replace("ID", idBet));
            await Task.Delay(2000);
            var tabelaAcimaAbaixoTotal15 = _driver.FindElement(By.Id("table-type-6-1.5"));
            CriaAcimaAbaixoTotal(1.5, tabelaAcimaAbaixoTotal15, EClassificacaoTipo.Total, times);

            // 2.5
            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Total_25.Replace("ID", idBet));
            await Task.Delay(2000);
            var tabelaAcimaAbaixoTotal25 = _driver.FindElement(By.Id("table-type-6-2.5"));
            CriaAcimaAbaixoTotal(2.5, tabelaAcimaAbaixoTotal25, EClassificacaoTipo.Total, times);

            // 2.5
            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Casa_25.Replace("ID", idBet));
            await Task.Delay(2000);
            var tabelaAcimaAbaixoCasa25 = _driver.FindElement(By.Id("table-type-17-2.5"));
            CriaAcimaAbaixo(2.5, tabelaAcimaAbaixoCasa25, EClassificacaoTipo.Casa, times[0]);

            // 2.5
            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Fora_25.Replace("ID", idBet));
            await Task.Delay(2000);
            var tabelaAcimaAbaixoFora25 = _driver.FindElement(By.Id("table-type-18-2.5"));
            CriaAcimaAbaixo(2.5, tabelaAcimaAbaixoFora25, EClassificacaoTipo.Fora, times[1]);

            // 0.5
            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Casa_05.Replace("ID", idBet));
            await Task.Delay(2000);
            var tabelaAcimaAbaixoCasa05 = _driver.FindElement(By.Id("table-type-17-0.5"));
            CriaAcimaAbaixo(0.5, tabelaAcimaAbaixoCasa05, EClassificacaoTipo.Casa, times[0]);

            // 0.5
            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Fora_05.Replace("ID", idBet));
            await Task.Delay(2000);
            var tabelaAcimaAbaixoFora05 = _driver.FindElement(By.Id("table-type-18-0.5"));
            CriaAcimaAbaixo(0.5, tabelaAcimaAbaixoFora05, EClassificacaoTipo.Fora, times[1]);

            //1.5
            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Casa_15.Replace("ID", idBet));
            await Task.Delay(2000);
            var tabelaAcimaAbaixoCasa15 = _driver.FindElement(By.Id("table-type-17-1.5"));
            CriaAcimaAbaixo(1.5, tabelaAcimaAbaixoCasa15, EClassificacaoTipo.Casa, times[0]);

            //1.5
            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Fora_15.Replace("ID", idBet));
            await Task.Delay(2000);
            var tabelaAcimaAbaixoFora15 = _driver.FindElement(By.Id("table-type-18-1.5"));
            CriaAcimaAbaixo(1.5, tabelaAcimaAbaixoFora15, EClassificacaoTipo.Fora, times[1]);

        }

        public void PegaUltimoOver(double overValor, IWebElement tabelaClassificacao, EClassificacaoTipo tipo, List<Time> times)
        {
            var trTimes = tabelaClassificacao.FindElements(By.ClassName("highlight"));
            foreach (var tr in trTimes)
            {
                var nomeTime = tr.FindElement(By.ClassName("team_name_span"))
                                    .FindElement(By.TagName("a")).Text;

                bool? ultimoOverPositivo;
                var underovers = tr.FindElement(By.ClassName("matches-5")).FindElements(By.TagName("a"));
                if (underovers.Count > 2)
                {

                    string classInfo = underovers[2].GetAttribute("class");
                    ultimoOverPositivo = classInfo.Contains("form-under") ? false : true;
                    times.FirstOrDefault(t => t.Nome == nomeTime).UltimoOverPositivo = ultimoOverPositivo;
                }

            }
        }

        public void CriaAcimaAbaixoTotal(double overValor, IWebElement tabelaClassificacao, EClassificacaoTipo tipo, List<Time> times)
        {
            var trTimes = tabelaClassificacao.FindElements(By.ClassName("highlight"));
            foreach (var tr in trTimes)
            {
                var nomeTime = tr.FindElement(By.ClassName("team_name_span"))
                                    .FindElement(By.TagName("a")).Text;

                var qtdJogos = tr.FindElement(By.ClassName("col_matches_played")).Text;
                var gols = tr.FindElement(By.ClassName("col_goals")).Text;
                var gj = tr.FindElement(By.ClassName("col_avg_goals_match")).Text;
                var asTag = tr.FindElement(By.ClassName("col_last_5"))
                                .FindElements(By.TagName("a"));


                bool? ultimoOverPositivo;
                var underovers = tr.FindElement(By.ClassName("matches-5")).FindElements(By.TagName("a"));
                if (underovers.Count > 1)
                {

                    string classInfo = underovers[1].GetAttribute("class");
                    ultimoOverPositivo = classInfo.Contains("form-under") ? false : true;
                    times.FirstOrDefault(t => t.Nome == nomeTime).UltimoOverPositivo = ultimoOverPositivo;
                }

                var overs = tr.FindElements(By.ClassName("form-over")).Count;
                var unders = tr.FindElements(By.ClassName("form-under")).Count;

                Over o = new Over(overValor, gols, gj, overs, unders, overs + unders, TimeHelper.GolsRealizadosConvert(gols), TimeHelper.GolsSofridosConvert(gols));
                o.J = int.Parse(qtdJogos);
                var aa = new AcimaAbaixo(tipo);
                aa.Overs.Add(o);
                times.FirstOrDefault(t => t.Nome == nomeTime).AcimaAbaixo.Add(aa);

            }
        }

        private void CriaAcimaAbaixo(double overValor, IWebElement tabelaClassificacao, EClassificacaoTipo tipo, Time time)
        {
            var trTimes = tabelaClassificacao.FindElements(By.ClassName("highlight"));
            foreach (var tr in trTimes)
            {
                var nomeTime = tr.FindElement(By.ClassName("team_name_span"))
                                    .FindElement(By.TagName("a")).Text;

                if (time.Nome == nomeTime)
                {
                    var qtdJogos = tr.FindElement(By.ClassName("col_matches_played")).Text;
                    var gols = tr.FindElement(By.ClassName("col_goals")).Text;
                    var gj = tr.FindElement(By.ClassName("col_avg_goals_match")).Text;
                    var asTag = tr.FindElement(By.ClassName("col_last_5"))
                                    .FindElements(By.TagName("a"));

                    var overs = tr.FindElements(By.ClassName("form-over")).Count;
                    var unders = tr.FindElements(By.ClassName("form-under")).Count;

                    Over o = new Over(overValor, gols, gj, overs, unders, overs + unders, TimeHelper.GolsRealizadosConvert(gols), TimeHelper.GolsSofridosConvert(gols));
                    o.J = int.Parse(qtdJogos);
                    var aa = new AcimaAbaixo(tipo);

                    aa.Overs.Add(o);
                    time.AcimaAbaixo.Add(aa);
                }
            }
        }

        public static void AnalisaGolsTotal(Jogo jogo)
        {
            // Total
            int time1_golsRealizados = jogo.Time1.AcimaAbaixo.Where(aa => aa.Tipo == EClassificacaoTipo.Total).ToList()[0].Overs[0].GolsRealizados;
            int time1_golsSofridos = jogo.Time1.AcimaAbaixo.Where(aa => aa.Tipo == EClassificacaoTipo.Total).ToList()[0].Overs[0].GolsSofridos;
            int time1_qtdJogos = jogo.Time1.AcimaAbaixo.Where(aa => aa.Tipo == EClassificacaoTipo.Total).ToList()[0].Overs[0].J;
            int time2_golsRealizados = jogo.Time2.AcimaAbaixo.Where(aa => aa.Tipo == EClassificacaoTipo.Total).ToList()[0].Overs[0].GolsRealizados;
            int time2_golsSofridos = jogo.Time2.AcimaAbaixo.Where(aa => aa.Tipo == EClassificacaoTipo.Total).ToList()[0].Overs[0].GolsSofridos;
            int time2_qtdJogos = jogo.Time2.AcimaAbaixo.Where(aa => aa.Tipo == EClassificacaoTipo.Total).ToList()[0].Overs[0].J;

            // Casa
            int time1_golsRealizados_Casa = jogo.Time1.AcimaAbaixo.Where(aa => aa.Tipo == EClassificacaoTipo.Casa).ToList()[0].Overs[0].GolsRealizados;
            int time1_golsSofridos_Casa = jogo.Time1.AcimaAbaixo.Where(aa => aa.Tipo == EClassificacaoTipo.Casa).ToList()[0].Overs[0].GolsSofridos;
            int time1_qtdJogos_Casa = jogo.Time1.AcimaAbaixo.Where(aa => aa.Tipo == EClassificacaoTipo.Casa).ToList()[0].Overs[0].J;

            // Fora
            int time2_golsRealizados_Fora = jogo.Time2.AcimaAbaixo.Where(aa => aa.Tipo == EClassificacaoTipo.Fora).ToList()[0].Overs[0].GolsRealizados;
            int time2_golsSofridos_Fora = jogo.Time2.AcimaAbaixo.Where(aa => aa.Tipo == EClassificacaoTipo.Fora).ToList()[0].Overs[0].GolsSofridos;
            int time2_qtdJogos_Fora = jogo.Time2.AcimaAbaixo.Where(aa => aa.Tipo == EClassificacaoTipo.Fora).ToList()[0].Overs[0].J;

            double time1_qtdAceitavel = (time1_qtdJogos * 0.4) + time1_qtdJogos;
            double time2_qtdAceitavel = (time2_qtdJogos * 0.4) + time2_qtdJogos;

            double time1_qtdAceitavel_Casa = (time1_qtdJogos_Casa * 0.4) + time1_qtdJogos_Casa;
            double time2_qtdAceitavel_Fora = (time2_qtdJogos_Fora * 0.4) + time2_qtdJogos_Fora;

            jogo.Time1SofreMaisGols_Total = time1_golsSofridos >= time1_qtdAceitavel;
            jogo.Time1RealizaMaisGols_Total = time1_golsRealizados >= time1_qtdAceitavel;
            jogo.Time2SofreMaisGols_Total = time2_golsSofridos >= time2_qtdAceitavel;
            jogo.Time2RealizaMaisGols_Total = time2_golsRealizados >= time2_qtdAceitavel;

            jogo.Time1SofreMaisGols_Casa = time1_golsSofridos_Casa >= time1_qtdAceitavel_Casa;
            jogo.Time1RealizaMaisGols_Casa = time1_golsRealizados_Casa >= time1_qtdAceitavel_Casa;

            jogo.Time2SofreMaisGols_Fora = time2_golsRealizados_Fora >= time2_qtdAceitavel_Fora;
            jogo.Time2RealizaMaisGols_Fora = time2_golsRealizados_Fora >= time2_qtdAceitavel_Fora;

            jogo.Time1.GolsRealizados = time1_golsRealizados_Casa;
            jogo.Time1.GolsSofridos = time1_golsSofridos_Casa;
            jogo.Time2.GolsRealizados = time2_golsRealizados_Fora;
            jogo.Time2.GolsSofridos = time2_golsSofridos_Fora;

            jogo.Time1.GolsRealizadosTotal = time1_golsRealizados;
            jogo.Time1.GolsSofridosTotal = time1_golsSofridos;
            jogo.Time2.GolsRealizadosTotal = time2_golsRealizados;
            jogo.Time2.GolsSofridosTotal = time2_golsSofridos;

            jogo.Time1.QtdJogosTotal = time1_qtdJogos;
            jogo.Time2.QtdJogosTotal = time2_qtdJogos;
            jogo.Time1.QtdJogos = time1_qtdJogos_Casa;
            jogo.Time2.QtdJogos = time2_qtdJogos_Fora;

            jogo.UmTimeFazMaisGolEOutroSofreMaisGol = jogo.Time1SofreMaisGols_Casa && jogo.Time2RealizaMaisGols_Fora || jogo.Time2SofreMaisGols_Fora && jogo.Time1RealizaMaisGols_Casa;
            jogo.UmTimeFazMaisGolEOutroSofreMaisGolTotal = jogo.Time1SofreMaisGols_Total && jogo.Time2RealizaMaisGols_Total || jogo.Time2SofreMaisGols_Total && jogo.Time1RealizaMaisGols_Total;
        }

        public void PreencheCamposAnaliseJogo(Jogo jogo)
        {
            var time1_05_15_25_Overs = jogo.Time1.AcimaAbaixo.Where(a => a.Tipo == EClassificacaoTipo.Casa)
                                                        .SelectMany(a => a.Overs)
                                                        .ToList();

            var time2_05_15_25_Overs = jogo.Time2.AcimaAbaixo.Where(a => a.Tipo == EClassificacaoTipo.Fora)
                                                        .SelectMany(a => a.Overs)
                                                        .ToList();
            // Time1

            var time1_overs05 = TimeHelper.GetOvers(jogo.Time1, 0.5, EClassificacaoTipo.Casa);
            var time1_overs15 = TimeHelper.GetOvers(jogo.Time1, 1.5, EClassificacaoTipo.Casa);
            var time1_overs25 = TimeHelper.GetOvers(jogo.Time1, 2.5, EClassificacaoTipo.Casa);

            // Time2

            var time2_overs05 = TimeHelper.GetOvers(jogo.Time2, 0.5, EClassificacaoTipo.Fora);
            var time2_overs15 = TimeHelper.GetOvers(jogo.Time2, 1.5, EClassificacaoTipo.Fora);
            var time2_overs25 = TimeHelper.GetOvers(jogo.Time2, 2.5, EClassificacaoTipo.Fora);

            //MediaGols
            double time1_mediaGols = TimeHelper.MediaGols(jogo.Time1, EClassificacaoTipo.Casa);
            double time2_mediaGols = TimeHelper.MediaGols(jogo.Time2, EClassificacaoTipo.Fora);
            double mediaGols = (time1_mediaGols + time2_mediaGols) / 2;

            var m1 = time1_05_15_25_Overs.FirstOrDefault()?.Gols ?? "";
            var m2 = time2_05_15_25_Overs.FirstOrDefault()?.Gols ?? "";
            var classTime1 = jogo.Time1.Classificacoes.FirstOrDefault().Lugar;
            var classTime2 = jogo.Time2.Classificacoes.FirstOrDefault().Lugar;
            var classTotal = jogo.Time1.Classificacoes.FirstOrDefault().TotalLugares;
            bool golsIrregularTime1 = TimeValidations.TimeGolsIrregular(jogo.Time1.AcimaAbaixo.FirstOrDefault().Overs.FirstOrDefault().Gols);
            bool golsIrregularTime2 = TimeValidations.TimeGolsIrregular(jogo.Time2.AcimaAbaixo.FirstOrDefault().Overs.FirstOrDefault().Gols);
            bool jogoComTimeComGolsIrregulares = golsIrregularTime1 || golsIrregularTime2;
            bool timesComPoucaDiferencaClassificacao = JogoValidations.TimesPoucaDiferencaClassificacao(jogo);
            bool umDosTimesFazMaisGol = JogoValidations.UmDosTimesFazMaisGol(jogo);
            bool osDoisTimesFazemPoucosGols = JogoValidations.OsDoisTimesFazemPoucosGols(jogo);
            int time1GolsR = TimeHelper.GolsRealizadosConvert(m1);
            int time1GolsS = TimeHelper.GolsSofridosConvert(m1);
            int time2GolsR = TimeHelper.GolsRealizadosConvert(m2);
            int time2GolsS = TimeHelper.GolsSofridosConvert(m2);
            var somaOvers05 = time1_overs05 + time2_overs05;
            var somaOvers15 = time1_overs15 + time2_overs15;
            var somaOvers25 = time1_overs25 + time2_overs25;

            jogo.Time1.MediaGols = time1_mediaGols;
            jogo.Time2.MediaGols = time2_mediaGols;

            jogo.Time1.Gols = m1;
            jogo.Time2.Gols = m2;

            jogo.MediaGolsTotal = mediaGols;

            jogo.Time1.Overs05 = time1_overs05;
            jogo.Time1.Overs15 = time1_overs15;
            jogo.Time1.Overs25 = time1_overs25;
            jogo.Time2.Overs05 = time2_overs05;
            jogo.Time2.Overs15 = time2_overs15;
            jogo.Time2.Overs25 = time2_overs25;

            jogo.SomaOvers05 = time1_overs05 + time2_overs05;
            jogo.SomaOvers15 = time1_overs15 + time2_overs15;
            jogo.SomaOvers25 = time1_overs25 + time2_overs25;
            jogo.SomaTotalOvers = jogo.SomaOvers05 + jogo.SomaOvers15 + jogo.SomaOvers25;
            jogo.UmTimeFazMaisGolQueOOutro = JogoValidations.UmDosTimesFazMaisGol(jogo);

            jogo.Time1.Classificacao = classTime1;
            jogo.Time2.Classificacao = classTime2;
            jogo.ClassificaoTotal = classTotal;

            jogo.ClassifPerto = timesComPoucaDiferencaClassificacao;
            jogo.UmOuOsDoisTimesFazemMaisGols = umDosTimesFazMaisGol;
            jogo.OsDoisTimesSofremGols = osDoisTimesFazemPoucosGols;
            jogo.GolsIrregulares = jogoComTimeComGolsIrregulares;
            jogo.TimesComPoucosJogos = TimeValidations.PoucosJogosTime(jogo.Time1, EClassificacaoTipo.Casa) ||
                                        TimeValidations.PoucosJogosTime(jogo.Time2, EClassificacaoTipo.Fora);

            jogo.LinkResultados = GetLinkResultadosId(jogo.IdJogoBet);

            jogo.Time1.GolsRealizados = time1GolsR;
            jogo.Time1.GolsSofridos = time1GolsS;
            jogo.Time2.GolsRealizados = time2GolsR;
            jogo.Time2.GolsSofridos = time2GolsS;
            jogo.GolsTotal = jogo.GolsTime1 + jogo.GolsTime2;
            jogo.UmTimeFazMaisGolEOutroSofreMaisGol = JogoValidations.UmTimeFazMaisGolEOutroSofreMaisGols(jogo);
            jogo.Time1.QtdTotalDeJogosOvers = TimeHelper.GetQtdJogos(jogo.Time1, EClassificacaoTipo.Casa);
            jogo.Time2.QtdTotalDeJogosOvers = TimeHelper.GetQtdJogos(jogo.Time2, EClassificacaoTipo.Fora);
            jogo.UmOuOsDoisTimesTemJogosOversMenorQue5 = (jogo.Time1.QtdTotalDeJogosOvers < 5 || jogo.Time2.QtdTotalDeJogosOvers < 5);
            jogo.JogoComTimeComODobroDeGols = JogoValidations.JogoComTimeFazODobroDeGols(jogo);
            AnalisaGolsTotal(jogo);
        }
    }
}
