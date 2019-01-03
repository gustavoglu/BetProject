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
        private readonly IWebDriver _driver;
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

        

        public async Task AtualizaInformacoesBasicasJogo(Jogo jogo)
        {
            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogo.Replace("ID", jogo.IdJogoBet));
            await Task.Delay(2000);

            var status = PegaStatus();

            if (status == "Intervalo" || status == "Encerrado" || status == "Sem status") return;
            string minutos = null;

            int score1 = 0;
            int score2 = 0;
            try
            {
                var scores = _driver.FindElements(By.ClassName("scoreboard"));
                score1 = int.Parse(scores[0]?.Text);
                score2 = int.Parse(scores[1]?.Text);
            }
            catch { };


            try
            {
                minutos = _driver.FindElement(By.Id("atomclock"))
                  .FindElements(By.TagName("span"))[0].Text;
            }
            catch { };

            if (status == "Intervalo") minutos = "45";

            var divLiga = _driver.FindElement(By.ClassName("fleft"));

            jogo.Status = status;
            jogo.Minutos = minutos;
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

            await PegaInfosAcimaAbaixo(idBet, times, jogo.GolsTime1 + jogo.GolsTime2);

            PreencheCamposAnaliseJogo(jogo);

            if (jogo.TimesComPoucosJogos)
            {
                _idContainerRepository.IgnoraIdJogo(idBet);
                return;
            }

            _analiseService.AnalisaMediaGolsMenorQue25(jogo);
            _analiseService.AnalisaSeMelhorJogo(jogo);


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
            CriaClassificacao(tabelaClassificacaoTotal, EClassificacaoTipo.Total, times);

        }

        private bool CriaClassificacao(IWebElement tabelaClassificacao, EClassificacaoTipo tipo, List<Time> times)
        {
            var trsTotal = tabelaClassificacao.FindElements(By.ClassName("col_rank"));
            var trTimes = tabelaClassificacao.FindElements(By.ClassName("highlight"));
            foreach (var tr in trTimes)
            {
                var nomeTime = tr.FindElement(By.ClassName("team_name_span"))
                                    .FindElement(By.TagName("a")).Text;

                var lugar = int.Parse(tr.FindElement(By.ClassName("col_rank")).Text.Replace(".", ""));
                var gols = tr.FindElements(By.ClassName("col_goals"))[0].Text;

                var asTag = tr.FindElement(By.ClassName("form"))
                                .FindElements(By.TagName("a"));
                var empates = tr.FindElements(By.ClassName("form-d"));
                var derrotas = tr.FindElements(By.ClassName("form-l"));
                var vitorias = tr.FindElements(By.ClassName("form-w"));

                var qtdJogos = empates.Count + derrotas.Count + vitorias.Count;

                Classificacao classif = new Classificacao(tipo, vitorias.Count, empates.Count, derrotas.Count, qtdJogos,
                                                            lugar, trsTotal.Count - 1, gols);

                times.FirstOrDefault(t => t.Nome == nomeTime).Classificacoes.Add(classif);
            }
            return true;
        }

        private async Task PegaInfosAcimaAbaixo(string idBet, List<Time> times, int totalGols, bool driverParalelo = false)
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

        private void CriaAcimaAbaixoTotal(double overValor, IWebElement tabelaClassificacao, EClassificacaoTipo tipo, List<Time> times)
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
        }
    }
}
