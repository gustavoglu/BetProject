using BetProject.Configurations;
using BetProject.Enums;
using BetProject.Infra;
using BetProject.Models;
using BetProject.ObjectValues;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BetProject.Services
{
    public class ResultadoSiteServices
    {
        private bool CarregandoJogos = false;
        private const double DIFERENCAGOLSIRREGULARES = 0.4;
        private const double DIFERENCATIMEFAZMAISGOLS = 0.4;
        private const double DIFERENCACLASSIFICACAOACEITAVEL = 0.35;
        private readonly SeleniumConfiguration _configuration;
        private readonly IWebDriver _driver;
        private readonly IWebDriver _driverParalelo;
        public List<Jogo> Jogos { get; set; }
        private readonly DbContextRaven context;
        TelegramService telegramService;
        public string IdAtual { get; set; }
        public ResultadoSiteServices(SeleniumConfiguration configuration)
        {

            _configuration = configuration;
            FirefoxOptions options = new FirefoxOptions();
            //options.AddArgument("--headless");

            _driver = new FirefoxDriver(_configuration.DriverFirefoxPath, options);
            _driverParalelo = new FirefoxDriver(_configuration.DriverFirefoxPath, options);
            _driverParalelo.Manage().Timeouts().PageLoad = new TimeSpan(10, 0, 0);
            _driver.Manage().Timeouts().PageLoad = new TimeSpan(10, 0, 0);
            Jogos = new List<Jogo>();
            context = new DbContextRaven();
            telegramService = new TelegramService();
        }

        public void NavegarParaSite(string site, bool driverParalelo = false)
        {
            var driver = driverParalelo ? _driverParalelo : _driver;
            driver.Navigate().GoToUrl(site);
        }

        public string GetLinkDbId(string id)
        {
            return "http://ec2-52-15-109-86.us-east-2.compute.amazonaws.com:8083/databases/bp_db/docs?id=" + id;
        }

        public int GolsSofridosConvert(string gols)
        {
            if (gols == null) return 0;
            int? golsS = null;
            var length = gols.Length;
            var indexDoisPontos = gols.IndexOf(":");
            if (indexDoisPontos == 1)

                golsS = length == 3 ? int.Parse(gols[2].ToString()) : int.Parse(string.Concat(gols[2].ToString(), gols[3].ToString()));


            if (indexDoisPontos == 2)

                golsS = length == 4 ? int.Parse(gols[3].ToString()) : int.Parse(string.Concat(gols[3].ToString(), gols[4].ToString()));

            return golsS.Value;

        }

        public int GolsRealizadosConvert(string gols)
        {
            if (gols == null) return 0;
            int? golsR = null;
            var length = gols.Length;
            var indexDoisPontos = gols.IndexOf(":");
            if (indexDoisPontos == 1)
            {
                golsR = int.Parse(gols[0].ToString());
            }

            if (indexDoisPontos == 2)
            {
                golsR = int.Parse(string.Concat(gols[0].ToString(), gols[1].ToString()));
            }
            return golsR.Value;
        }

        public bool TimeGolsIrregular(string gols)
        {
            int? golsS = null;
            int? golsR = null;
            var length = gols.Length;
            var indexDoisPontos = gols.IndexOf(":");
            if (indexDoisPontos == 1)
            {
                golsR = int.Parse(gols[0].ToString());
                golsS = length == 3 ? int.Parse(gols[2].ToString()) : int.Parse(string.Concat(gols[2].ToString(), gols[3].ToString()));
            }

            if (indexDoisPontos == 2)
            {
                golsR = int.Parse(string.Concat(gols[0].ToString(), gols[1].ToString()));
                golsS = length == 4 ? int.Parse(gols[3].ToString()) : int.Parse(string.Concat(gols[3].ToString(), gols[4].ToString()));
            }

            if (golsS.Value == golsR.Value) return false;
            int numeroMaior = golsS.Value > golsR.Value ? golsS.Value : golsR.Value;
            int numeroMenor = golsS.Value < golsR.Value ? golsS.Value : golsR.Value;
            double diferencaAceitavel = numeroMaior * DIFERENCAGOLSIRREGULARES;
            int diferenca = numeroMaior - numeroMenor;

            if (diferenca > diferencaAceitavel) return true;
            else return false;
        }

        public bool TimesPoucaDiferencaClassificacao(Jogo jogo)
        {
            int lugaresTotal = jogo.Time1.Classificacoes.FirstOrDefault().TotalLugares;
            int time1Lugar = jogo.Time1.Classificacoes.FirstOrDefault().Lugar;
            int time2Lugar = jogo.Time2.Classificacoes.FirstOrDefault().Lugar;
            int classMaior = time1Lugar > time2Lugar ? time1Lugar : time2Lugar;
            int classMenor = time1Lugar < time2Lugar ? time1Lugar : time2Lugar;

            int diferenca = classMaior - classMenor;
            double diferencaAceitavel = lugaresTotal * DIFERENCACLASSIFICACAOACEITAVEL;
            return diferenca < diferencaAceitavel;

        }

        public List<Jogo> TrazJogos()
        {
            using (var s = context.DocumentStore.OpenSession())
            {
                return s.Query<Jogo>().ToList();
            }
        }

        public async void GuardaJogosDoDia()
        {try
            {
                try
                {
                    Task.Factory.StartNew(async () => await StartAnaliseLive(true));
                    await Task.Delay(400000);
                    await StartAnaliseLive();
                    return;
                }
                catch
                {
                    Task.Factory.StartNew(async () => await StartAnaliseLive(true));
                    await Task.Delay(400000);
                    await StartAnaliseLive();
                    return;
                }
            }
            catch
            {
                telegramService.EnviaMensagemParaOGrupo("Erro App");
            }

        }
        public async Task SalvaJogosDeAmanha()
        {
            var amanha = TrazerIdContainerAmanha();
            if (amanha == null) await SalvaJogosIds(true);
            Task.Factory.StartNew(async () =>
            {
                foreach (var i in amanha.Ids.OrderByDescending(id => id.DataInicio.TimeOfDay))
                {
                    await CriarOuAtualizaInfosJogo(i.Id, true, true);
                }
            });

            foreach (var i in amanha.Ids.OrderBy(id => id.DataInicio.TimeOfDay))
            {
                await CriarOuAtualizaInfosJogo(i.Id, false, true);
            }

        }

        public void DeletaNotificacao(string idBet, string desc)
        {
            using (var s = context.DocumentStore.OpenSession())
            {
                var idContainerHoje = TrazerIdContainerHoje();
                idContainerHoje.Notificacoes.RemoveAll(n => n.IdBet == idBet && n.Notificacao == desc);
                s.Store(idContainerHoje);
                s.SaveChanges();
            }
        }

        public void SalvaEnvioDeNotificao(string idBet, string desc, int numero)
        {
            using (var s = context.DocumentStore.OpenSession())
            {
                var idContainerHoje = TrazerIdContainerHoje();
                idContainerHoje.Notificacoes.Add(new NotificacaoJogo(idBet, desc, numero, true));
                s.Store(idContainerHoje);
                s.SaveChanges();
            }
        }

        public bool NotificacaoJaEnviada(string idBet, string desc, int numero)
        {
            using (var s = context.DocumentStore.OpenSession())
            {
                var idContainerHoje = TrazerIdContainerHoje();
                return idContainerHoje.Notificacoes.Exists(n => n.IdBet == idBet && n.Notificacao == desc && n.Numero == numero);
            }
        }

        public Jogo TrazerJogoPorId(string idBet)
        {
            using (var s = context.DocumentStore.OpenSession())
                return s.Query<Jogo>().FirstOrDefault(j => j.IdJogoBet == idBet);

        }

        public async Task CriarOuAtualizaInfosJogo(string id, bool driverParalelo = false, bool amanha = false)
        {

            var idContainer = !amanha ? TrazerIdContainerHoje() : TrazerIdContainerAmanha();
            var jogoId = idContainer.Ids.FirstOrDefault(i => i.Id == id) ??
                         idContainer.IdsLive.FirstOrDefault(i => i.Id == id);


            var jogo = TrazerJogoPorId(jogoId.Id);

            var ids = idContainer.Ids;

            if (jogo != null)
                try
                {

                    if (jogo.Ignorar) return;
                    await AtualizaInformacoesBasicasJogo(jogo, driverParalelo);
                    CriaOuAtualizaJogo(jogo);
                }
                catch (Exception e)
                {
                    jogoId.ErrorMessage = e.Message;
                    using (var s = context.DocumentStore.OpenSession())
                    {
                        idContainer = amanha ? TrazerIdContainerAmanha() : TrazerIdContainerHoje();
                        idContainer.IdsComErro.Add(jogoId);
                        s.Store(idContainer);
                        s.SaveChanges();
                    }
                }
            else
                try
                {
                    await PegaResumoDoJogo(id, driverParalelo);
                }
                catch (Exception e)
                {
                    jogoId.ErrorMessage = e.Message;
                    using (var s = context.DocumentStore.OpenSession())
                    {
                        idContainer = amanha ? TrazerIdContainerAmanha() : TrazerIdContainerHoje();
                        idContainer.IdsComErro.Add(jogoId);
                        s.Store(idContainer);
                        s.SaveChanges();
                    }
                }
        }

        public async Task CarregaJogosDoDia()
        {
            while (CarregandoJogos)
            {
                await Task.Delay(400000);
            }

            var idContainer = TrazerIdContainerHoje();
            if (DateTime.Now > new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 05, 00, 00))
            {
                if (idContainer == null || !idContainer.Ids.Any())
                {
                    CarregandoJogos = true;
                    idContainer = await SalvaJogosIds();
                    try
                    {
                        foreach (var id in idContainer.Ids.OrderBy(i => i.DataInicio.TimeOfDay).ToList())
                        {
                            await CriarOuAtualizaInfosJogo(id.Id, false, false);
                        }
                    }
                    catch
                    {
                        foreach (var id in idContainer.Ids.OrderBy(i => i.DataInicio.TimeOfDay).ToList())
                        {
                            await CriarOuAtualizaInfosJogo(id.Id, false, false);
                        }
                    }
                }
                CarregandoJogos = false;
            }
        }

        public async Task StartAnaliseLive(bool driverParalelo = false)
        {
            while (true)
            {
                telegramService.EnviaMensagemParaOGrupo("Teste Run App");
                await CarregaJogosDoDia();
                var idContainer = TrazerIdContainerHoje();

                await SalvaIdsLive(driverParalelo);

                if (!idContainer.IdsLive.Any()) await Task.Delay(60000);

                List<Jogo> jogos = new List<Jogo>();
                using (var s = context.DocumentStore.OpenSession())
                {

                    foreach (var i in idContainer.IdsLive)
                    {

                        try
                        {
                            await CriarOuAtualizaInfosJogo(i.Id, driverParalelo);
                            var jogo = TrazerJogoPorId(i.Id);
                            if (jogo != null)
                            {
                                var jogoQuery = s.Query<Jogo>().FirstOrDefault(jq => jq.IdJogoBet == i.Id &&
                                                                jq.Minutos != null &&
                                                                jq.Time1.AcimaAbaixo.Any() &&
                                                                jq.Time2.AcimaAbaixo.Any()
                                                                );

                                if (jogoQuery != null)
                                    AnalisaJogoLive(jogoQuery);
                            }
                        }
                        catch (Exception e)
                        {
                            var msg = e.Message;
                        }

                    }
                }
            }
        }

        bool UmDosTimesFazMaisGol(Jogo jogo)
        {
            var time1TotalJogos = jogo.Time1.AcimaAbaixo.FirstOrDefault(aa => aa.Tipo == EClassificacaoTipo.Total).Overs[0].J;
            var time2TotalJogos = jogo.Time2.AcimaAbaixo.FirstOrDefault(aa => aa.Tipo == EClassificacaoTipo.Total).Overs[0].J;
            var time1GolsR = GolsRealizadosConvert(jogo.Time1.AcimaAbaixo.FirstOrDefault(aa => aa.Tipo == EClassificacaoTipo.Total).Overs[0].Gols);
            var time2GolsR = GolsRealizadosConvert(jogo.Time2.AcimaAbaixo.FirstOrDefault(aa => aa.Tipo == EClassificacaoTipo.Total).Overs[0].Gols);
            bool time1FazMaisGolsQueSofre = time1GolsR >= time1TotalJogos;
            bool time2FazMaisGolsQueSofre = time2GolsR >= time2TotalJogos;


            return time1FazMaisGolsQueSofre || time2FazMaisGolsQueSofre;
        }

        bool PoucosJogosTime(Time time, EClassificacaoTipo tipo)
        {
            // Time1
            var time_05_15_25_Overs = time.AcimaAbaixo
                            .Where(a => a.Tipo == tipo)
                            .SelectMany(a => a.Overs).ToList();

            var time_05_15_25_OversTotal = time.AcimaAbaixo
                    .Where(a => a.Tipo == EClassificacaoTipo.Total)
                    .SelectMany(a => a.Overs).ToList();

            var time1qtdJogosTotal = time.AcimaAbaixo.FirstOrDefault(aa => aa.Tipo == EClassificacaoTipo.Total).Overs[0].TotalUltimosJogos;
            var time1qtdJogos = time_05_15_25_Overs.FirstOrDefault().TotalUltimosJogos < 4 ? time1qtdJogosTotal :
                                   time_05_15_25_Overs.FirstOrDefault().TotalUltimosJogos;


            return time1qtdJogos < 4;
        }

        bool PoucosJogosTime2(Time time)
        {
            // Time2
            var time2_05_15_25_Overs = time.AcimaAbaixo
                       .Where(a => a.Tipo == EClassificacaoTipo.Fora)
                       .SelectMany(a => a.Overs)
                       .Where(o => o.Overs > 0).ToList();

            var time2_05_15_25_OversTotal = time.AcimaAbaixo
            .Where(a => a.Tipo == EClassificacaoTipo.Total)
            .SelectMany(a => a.Overs)
            .Where(o => o.Overs > 0).ToList();

            var time2qtdJogosTotal = time.AcimaAbaixo.FirstOrDefault(aa => aa.Tipo == EClassificacaoTipo.Total).Overs[0].TotalUltimosJogos;
            var time2qtdJogos = time2_05_15_25_Overs.FirstOrDefault().TotalUltimosJogos < 4 ? time2qtdJogosTotal :
                                   time2_05_15_25_Overs.FirstOrDefault().TotalUltimosJogos;

            return time2qtdJogos < 4;
        }

        private int ConvertMinutos(string minutos)
        {
            return minutos.IndexOf(":") >= 0 ?
                   minutos.IndexOf(":") == 1 ? int.Parse(minutos[0].ToString()) :
                   int.Parse(string.Concat(minutos[0], minutos[1])) :
                   int.Parse(minutos);
        }

        public double MediaGols(Time time, EClassificacaoTipo tipo)
        {
            var time_05_15_25_Overs = time.AcimaAbaixo.Where(a => a.Tipo == tipo)
                                                            .SelectMany(a => a.Overs)
                                                            .ToList();

            var time_05_15_25_OversTotal = time.AcimaAbaixo
                                            .Where(a => a.Tipo == EClassificacaoTipo.Total)
                                            .SelectMany(a => a.Overs).ToList();

            var time_mediaGols = double.Parse(time_05_15_25_Overs[0].GJ.Replace(".", ","));
            var time_mediaGolsTotal = double.Parse(time.AcimaAbaixo.FirstOrDefault(aa => aa.Tipo == EClassificacaoTipo.Total)
                                                                                                .Overs[0].GJ.Replace(".", ","));

            return time_05_15_25_Overs[0].TotalUltimosJogos < 4 &&
                         time_05_15_25_OversTotal[0].TotalUltimosJogos > 4 ?
                         time_mediaGolsTotal : time_mediaGols;
        }

        public int GetOvers(Time time, double valor, EClassificacaoTipo tipo)
        {
            var time_05_15_25_Overs = time.AcimaAbaixo.Where(a => a.Tipo == tipo)
                                                             .SelectMany(a => a.Overs)
                                                             .ToList();

            var time_05_15_25_OversTotal = time.AcimaAbaixo
                                            .Where(a => a.Tipo == EClassificacaoTipo.Total)
                                            .SelectMany(a => a.Overs).ToList();



            int time_overs = time_05_15_25_Overs.FirstOrDefault(o => o.Valor == valor)?.Overs ?? 0;
            int time_oversTotal = time_05_15_25_OversTotal.FirstOrDefault(o => o.Valor == valor)?.Overs ?? 0;

            time_overs = time_05_15_25_Overs[0].TotalUltimosJogos < 4 &&
                         time_05_15_25_OversTotal[0].TotalUltimosJogos > 4 ?
                         time_oversTotal : time_overs;

            return time_overs;

        }

        public void AnalisaJogoLive(Jogo jogo)
        {
            if (jogo.GolsTime1 + jogo.GolsTime2 > 2) return;
            if (jogo.Status == "Intervalo") return;
            if (jogo.Status == "Encerrado") return;

            int golsTotal = jogo.GolsTime1 + jogo.GolsTime2;

            int minutos = ConvertMinutos(jogo.Minutos);

            var time1_05_15_25_Overs = jogo.Time1.AcimaAbaixo.Where(a => a.Tipo == EClassificacaoTipo.Casa)
                                                          .SelectMany(a => a.Overs)
                                                          .ToList();

            var time2_05_15_25_Overs = jogo.Time2.AcimaAbaixo.Where(a => a.Tipo == EClassificacaoTipo.Fora)
                                                        .SelectMany(a => a.Overs)
                                                        .ToList();
            // Time1

            var time1_overs05 = GetOvers(jogo.Time1, 0.5, EClassificacaoTipo.Casa);
            var time1_overs15 = GetOvers(jogo.Time1, 1.5, EClassificacaoTipo.Casa);
            var time1_overs25 = GetOvers(jogo.Time1, 2.5, EClassificacaoTipo.Casa);

            // Time2

            var time2_overs05 = GetOvers(jogo.Time2, 0.5, EClassificacaoTipo.Fora);
            var time2_overs15 = GetOvers(jogo.Time2, 1.5, EClassificacaoTipo.Fora);
            var time2_overs25 = GetOvers(jogo.Time2, 2.5, EClassificacaoTipo.Fora);

            if (PoucosJogosTime(jogo.Time1, EClassificacaoTipo.Casa) || PoucosJogosTime(jogo.Time2, EClassificacaoTipo.Fora)) return;

            //MediaGols
            double time1_mediaGols = MediaGols(jogo.Time1, EClassificacaoTipo.Casa);
            double time2_mediaGols = MediaGols(jogo.Time2, EClassificacaoTipo.Fora);
            double mediaGols = (time1_mediaGols + time2_mediaGols) / 2;

            var m1 = time1_05_15_25_Overs.FirstOrDefault()?.Gols ?? "";
            var m2 = time2_05_15_25_Overs.FirstOrDefault()?.Gols ?? "";
            var classTime1 = jogo.Time1.Classificacoes.FirstOrDefault().Lugar;
            var classTime2 = jogo.Time2.Classificacoes.FirstOrDefault().Lugar;
            var classTotal = jogo.Time1.Classificacoes.FirstOrDefault().TotalLugares;
            bool golsIrregularTime1 = TimeGolsIrregular(jogo.Time1.AcimaAbaixo.FirstOrDefault().Overs.FirstOrDefault().Gols);
            bool golsIrregularTime2 = TimeGolsIrregular(jogo.Time2.AcimaAbaixo.FirstOrDefault().Overs.FirstOrDefault().Gols);
            bool jogoComTimeComGolsIrregulares = golsIrregularTime1 || golsIrregularTime2;
            bool timesComPoucaDiferencaClassificacao = TimesPoucaDiferencaClassificacao(jogo);
            bool umDosTimesFazMaisGol = UmDosTimesFazMaisGol(jogo);
            bool osDoisTimesFazemPoucosGols = OsDoisTimesFazemPoucosGols(jogo);


            if (minutos > 5 && minutos < 80)
            {

                if (golsTotal == 0 && (minutos >= 15 && minutos <= 21 || minutos >= 60))
                {

                    if ((time1_overs05 + time2_overs05) > 8 && 
                        (timesComPoucaDiferencaClassificacao || jogoComTimeComGolsIrregulares) && 
                        mediaGols > 2.4 && 
                        umDosTimesFazMaisGol && 
                        time1_mediaGols > 2.1 && 
                        time2_mediaGols > 2.1 &&
                        (time1_overs15 + time2_overs15)> 7)
                    {
                        if (!this.NotificacaoJaEnviada(jogo.IdJogoBet, "0.5", 1))
                        {
                            telegramService.EnviaMensagemParaOGrupo($"{jogo.Time1.Nome} - {jogo.Time2.Nome}\n" +
                                                                    $"{jogo.Liga} \n" +
                                                                    $"Média Gols: {time1_mediaGols} / {time2_mediaGols} \n Média Gols Total: {mediaGols}\n" +
                                                                    $"Gols: {m1} / {m2}\n" +
                                                                    $"Overs: {time1_overs05} / {time2_overs05} \n" +
                                                                    $"Class: {classTime1} / {classTime2} de {classTotal} \n" +
                                                                    $"Classif. Perto : {timesComPoucaDiferencaClassificacao} \n Gols Irregulares: {jogoComTimeComGolsIrregulares} \n" +
                                                                    $"Os dois times fazem poucos gols: { osDoisTimesFazemPoucosGols } \n" +
                                                                    $"Um ou os dois Times Fazem Mais Gols: { umDosTimesFazMaisGol } \n" +
                                                                    $"Over: 0.5 \n" +
                                                                    $"Boa Aposta");
                            SalvaEnvioDeNotificao(jogo.IdJogoBet, "0.5", 1);
                        }

                    }

                    if (minutos >= 60 &&
                        (time1_overs05 + time2_overs05) > 8 &&
                        (timesComPoucaDiferencaClassificacao || jogoComTimeComGolsIrregulares) &&
                        mediaGols > 2.4 &&
                        umDosTimesFazMaisGol &&
                        time1_mediaGols > 2.1 &&
                        time2_mediaGols > 2.1 &&
                        (time1_overs15 + time2_overs15) > 7)
                    {
                        if (!this.NotificacaoJaEnviada(jogo.IdJogoBet, "0.5", 2))
                        {
                            telegramService.EnviaMensagemParaOGrupo($"{jogo.Time1.Nome} - {jogo.Time2.Nome} \n" +
                                                                    $"{jogo.Liga} \n" +
                                                                    $"Média Gols: {time1_mediaGols} / {time2_mediaGols} \n" +
                                                                    $"Média Gols Total: {mediaGols} \n " +
                                                                    $"Gols: {m1} / {m2}  \n" +
                                                                    $"Overs: {time1_overs05} / {time2_overs05} \n" +
                                                                    $"Class: {classTime1} / {classTime2} de {classTotal} \n " +
                                                                    $"Classif. Perto : {timesComPoucaDiferencaClassificacao} \n " +
                                                                    $"Gols Irregulares: {jogoComTimeComGolsIrregulares} \n" +
                                                                    $"Os dois times fazem poucos gols: { osDoisTimesFazemPoucosGols } \n" +
                                                                    $"Um ou os dois Times Fazem Mais Gols: { umDosTimesFazMaisGol } \n" +
                                                                    $"Over: 0.5 \n" +
                                                                    $"Boa Aposta");
                            SalvaEnvioDeNotificao(jogo.IdJogoBet, "0.5", 2);
                        }
                    }
                }

                if (golsTotal == 1 && 
                    minutos <= 7 &&
                    mediaGols > 2.4)
                {
                    if (!this.NotificacaoJaEnviada(jogo.IdJogoBet, "1.5", 1))
                    {
                        if (time1_overs15 + time2_overs15 >= 8 && umDosTimesFazMaisGol && jogoComTimeComGolsIrregulares && timesComPoucaDiferencaClassificacao)
                            telegramService.EnviaMensagemParaOGrupo($" {jogo.Time1.Nome} - {jogo.Time2.Nome} \n {jogo.Liga} \n Média Gols: {time1_mediaGols} / {time2_mediaGols} \n Média Gols Total: {mediaGols} " +
                                                                    $"\n Gols: {m1} / {m2} \n Overs: {time1_overs15} / {time2_overs15} \n Class: {classTime1} / {classTime2} de {classTotal} \n " +
                                                                    $" Classif. Perto : {timesComPoucaDiferencaClassificacao} \n Gols Irregulares: {jogoComTimeComGolsIrregulares} \n" +
                                                                    $"Um ou os dois Times Fazem Mais Gols: { umDosTimesFazMaisGol } \n Over: 1.5 \n Boa Aposta");
                        SalvaEnvioDeNotificao(jogo.IdJogoBet, "1.5", 1);
                    }
                }

                if (golsTotal == 1 && 
                    minutos >= 62 &&
                    mediaGols > 2.4 &&
                    umDosTimesFazMaisGol &&
                    time1_mediaGols > 2.1 &&
                    time2_mediaGols > 2.1)
                {
                    if (!this.NotificacaoJaEnviada(jogo.IdJogoBet, "1.5", 2))
                    {
                        if (time1_overs15 + time2_overs15 >= 7)
                            telegramService.EnviaMensagemParaOGrupo($"{jogo.Time1.Nome} - {jogo.Time2.Nome} \n" +
                                                                    $"{jogo.Liga} \n" +
                                                                    $"Média Gols: {time1_mediaGols} / {time2_mediaGols} \n" +
                                                                    $"Média Gols Total: {mediaGols} \n " +
                                                                    $"Gols: {m1} / {m2}  \n" +
                                                                    $"Overs: {time1_overs15} / {time2_overs15} \n" +
                                                                    $"Class: {classTime1} / {classTime2} de {classTotal} \n " +
                                                                    $"Classif. Perto : {timesComPoucaDiferencaClassificacao} \n " +
                                                                    $"Gols Irregulares: {jogoComTimeComGolsIrregulares} \n" +
                                                                    $"Os dois times fazem poucos gols: { osDoisTimesFazemPoucosGols } \n" +
                                                                    $"Um ou os dois Times Fazem Mais Gols: { umDosTimesFazMaisGol } \n" +
                                                                    $"Over: 1.5 \n" +
                                                                    $"Boa Aposta");
                        SalvaEnvioDeNotificao(jogo.IdJogoBet, "1.5", 2);
                    }

                }

                if (golsTotal == 2 && minutos >= 62)
                {
                    if ((time1_overs25 + time2_overs25) >= 7 && 
                        mediaGols >= 3.2 &&
                        time1_mediaGols > 2.3 &&
                        time2_mediaGols > 2.3)
                    {
                        if (!this.NotificacaoJaEnviada(jogo.IdJogoBet, "2.5", 1))
                        {
                            telegramService.EnviaMensagemParaOGrupo($"{jogo.Time1.Nome} - {jogo.Time2.Nome} \n" +
                                                                    $"{jogo.Liga} \n" +
                                                                    $"Média Gols: {time1_mediaGols} / {time2_mediaGols} \n" +
                                                                    $"Média Gols Total: {mediaGols} \n " +
                                                                    $"Gols: {m1} / {m2}  \n" +
                                                                    $"Overs: {time1_overs25} / {time2_overs25} \n" +
                                                                    $"Class: {classTime1} / {classTime2} de {classTotal} \n " +
                                                                    $"Classif. Perto : {timesComPoucaDiferencaClassificacao} \n " +
                                                                    $"Gols Irregulares: {jogoComTimeComGolsIrregulares} \n" +
                                                                    $"Os dois times fazem poucos gols: { osDoisTimesFazemPoucosGols } \n" +
                                                                    $"Um ou os dois Times Fazem Mais Gols: { umDosTimesFazMaisGol } \n" +
                                                                    $"Over: 2.5 \n" +
                                                                    $"Boa Aposta");
                            SalvaEnvioDeNotificao(jogo.IdJogoBet, "2.5", 1);
                        }
                    }

                    if (!this.NotificacaoJaEnviada(jogo.IdJogoBet, "2.5", 2))
                    {
                        if (mediaGols > 3.2 && 
                            jogoComTimeComGolsIrregulares && 
                            umDosTimesFazMaisGol &&
                            time1_mediaGols > 2.4 &&
                            time2_mediaGols > 2.4)
                            telegramService.EnviaMensagemParaOGrupo($"{jogo.Time1.Nome} - {jogo.Time2.Nome} \n" +
                                                                    $"{jogo.Liga} \n" +
                                                                    $"Média Gols: {time1_mediaGols} / {time2_mediaGols} \n" +
                                                                    $"Média Gols Total: {mediaGols} \n " +
                                                                    $"Gols: {m1} / {m2}  \n" +
                                                                    $"Overs: {time1_overs25} / {time2_overs25} \n" +
                                                                    $"Class: {classTime1} / {classTime2} de {classTotal} \n " +
                                                                    $"Classif. Perto : {timesComPoucaDiferencaClassificacao} \n " +
                                                                    $"Gols Irregulares: {jogoComTimeComGolsIrregulares} \n" +
                                                                    $"Os dois times fazem poucos gols: { osDoisTimesFazemPoucosGols } \n" +
                                                                    $"Um ou os dois Times Fazem Mais Gols: { umDosTimesFazMaisGol } \n" +
                                                                    $"Over: 2.5 \n" +
                                                                    $"Boa Aposta");
                        SalvaEnvioDeNotificao(jogo.IdJogoBet, "2.5", 2);
                    }
                }

            }
        }

        public async Task TentaNovamenteIdsComErro(bool driverParalelo = false)
        {
            var idContainer = TrazerIdContainerHoje();
            var idsComErro = idContainer.IdsComErro.ToList();
            if (!idsComErro.Any()) return;
            foreach (var id in idsComErro)
            {
                try
                {
                    var jogo = TrazerJogoPorId(id.Id);
                    if (jogo == null)
                    {
                        await PegaResumoDoJogo(id.Id, driverParalelo);
                        using (var s = this.context.DocumentStore.OpenSession())
                        {
                            idContainer.IdsComErro.Remove(id);
                            s.Store(idContainer);
                            s.SaveChanges();
                        }
                    }
                }
                catch (Exception e)
                {
                    var msg = e.Message;
                }
            }


        }

        private IdContainer TrazerIdContainerAmanha()
        {
            using (var s = context.DocumentStore.OpenSession())
                return s.Query<IdContainer>().FirstOrDefault(ic => ic.Data == DateTime.Now.Date.AddDays(1));

        }

        private IdContainer TrazerIdContainerHoje()
        {
            using (var s = context.DocumentStore.OpenSession())
                return s.Query<IdContainer>().FirstOrDefault(ic => ic.Data == DateTime.Now.Date);

        }

        public void RemoviTodasTabelasTabela(IWebDriver driver)
        {
            var head_abs = driver.FindElements(By.ClassName("head_ab")).ToList();
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

        public void ExpandiTodasTabelas(IWebDriver driver)
        {
            var expandLeagueLink = driver.FindElements(By.ClassName("expand-league-link")).ToList();
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

        public bool ApostaBet(IWebElement tr, IWebDriver driver)
        {
            bool bet = false;
            try { bet = tr.FindElement(By.ClassName("clive")) != null; } catch { }
            return bet;
        }

        public bool Srf(IWebElement tr, IWebDriver driver)
        {
            bool srf = false;
            try
            {
                var srfTest = tr.FindElement(By.ClassName("cell_aa")).FindElement(By.ClassName("final_result_only"));
                srf = srfTest != null;
            }
            catch { }
            return srf;
        }

        public void SalvaContainer(IdContainer container)
        {
            using (var s = context.DocumentStore.OpenSession())
            {
                s.Store(container);
                s.SaveChanges();
            }
        }

        private async Task<IdContainer> SalvaProximosJogos(bool driverParalelo = false)
        {
            var driver = driverParalelo ? _driverParalelo : _driver;
            var idContainerHoje = TrazerIdContainerHoje();
            if (idContainerHoje == null) return null;

            driver.Navigate().GoToUrl(_configuration.Sites.Resultado.Principal);
            await Task.Delay(2000);

            driver.FindElement(By.ClassName("ifmenu-odds")).Click();
            await Task.Delay(2000);

            ExpandiTodasTabelas(driver);

            RemoviTodasTabelasTabela(driver);

            var trsJogos = driver.FindElements(By.ClassName("stage-scheduled")).ToList();
            if (!trsJogos.Any()) return idContainerHoje;

            foreach (var tr in trsJogos)
            {
                string id = tr.GetAttribute("id").Substring(4);
                if (!Srf(tr, driver) && !idContainerHoje.Ids.Exists(i => i.Id == id))
                {

                    string status = tr.FindElement(By.ClassName("cell_aa")).Text;
                    if (status != "Encerrado" && status != "Adiado")
                    {
                        string horaInicio = tr.FindElement(By.ClassName("cell_ad")).Text;
                        IdJogo idJogo = new IdJogo(id, DateTime.Parse(horaInicio));
                        idContainerHoje.Ids.Add(idJogo);
                    }

                }
            }
            SalvaContainer(idContainerHoje);
            return idContainerHoje;
        }

        private async Task<IdContainer> SalvaIdsLive(bool driverParalelo = false)
        {
            var driver = driverParalelo ? _driverParalelo : _driver;
            var idContainerHoje = TrazerIdContainerHoje();
            if (idContainerHoje == null) return null;

            driver.Navigate().GoToUrl(_configuration.Sites.Resultado.Principal);
            await Task.Delay(2000);

            driver.FindElement(By.ClassName("ifmenu-live")).Click();
            await Task.Delay(2000);

            ExpandiTodasTabelas(driver);

            RemoviTodasTabelasTabela(driver);

            var trsJogos = driver.FindElements(By.ClassName("stage-live")).ToList();
            if (!trsJogos.Any()) return idContainerHoje;

            idContainerHoje.IdsLive.Clear();
            foreach (var tr in trsJogos)
            {
                string id = tr.GetAttribute("id").Substring(4);

                if (ApostaBet(tr, driver))
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

            SalvaContainer(idContainerHoje);
            return idContainerHoje;
        }

        private async Task<IdContainer> SalvaJogosIds(bool amanha = false)
        {

            _driver.Navigate().GoToUrl(_configuration.Sites.Resultado.Principal);
            if (amanha)
                _driver.FindElement(By.ClassName("tomorrow")).Click();

            await Task.Delay(5000);
            try
            {
                ExpandiTodasTabelas(_driver);
            }
            catch { }

            try
            {
                RemoviTodasTabelasTabela(_driver);
            }
            catch { }

            List<IdJogo> idJogos = new List<IdJogo> { };
            //tr-first
            var trsJogos = _driver.FindElements(By.ClassName("stage-scheduled")).ToList();
            var trsJogosLive = _driver.FindElements(By.ClassName("stage-live")).ToList();
            if (trsJogosLive.Any()) trsJogosLive.ForEach(j => trsJogos.Add(j));

            foreach (var tr in trsJogos)
            {

                string status = tr.FindElement(By.ClassName("cell_aa")).Text;
                if (!Srf(tr, _driver) && status != "Encerrado" && status != "Adiado")
                {
                    string horaInicio = tr.FindElement(By.ClassName("cell_ad")).Text;
                    IdJogo idJogo = new IdJogo(tr.GetAttribute("id").Substring(4), DateTime.Parse(horaInicio));
                    idJogos.Add(idJogo);
                }
            }

            var idContainerHoje = TrazerIdContainerHoje();
            if (amanha)
            {
                idContainerHoje = new IdContainer(idJogos.ToList(), DateTime.Now.Date.AddDays(1));
            }
            else
            {
                if (idContainerHoje != null) idContainerHoje.Ids = idJogos;
                else idContainerHoje = new IdContainer(idJogos.ToList(), DateTime.Now.Date);
            }

            SalvaContainer(idContainerHoje);
            return idContainerHoje;
        }

        public void AtualizaOdds(string id, bool driverParalelo = false)
        {
            using (var s = context.DocumentStore.OpenSession())
            {
                //var jogo = s.Query<Jogo>().FirstOrDefault(j => j.Id == id);
                var jogo = TrazerJogoPorId(id);
                if (jogo == null) return;
                var odds = CriarOddsBet365(id, driverParalelo);
                jogo.OddOUs = odds;
                s.Store(jogo);
                s.SaveChanges();

            }
        }

        public void AtualizaOdds(bool driverParalelo = false)
        {
            using (var s = context.DocumentStore.OpenSession())
            {
                var jogos = s.Query<Jogo>().Where(j => j.DataImportacao == DateTime.Now.Date).ToList();
                foreach (var jogo in jogos)
                {
                    var odds = CriarOddsBet365(jogo.IdJogoBet, driverParalelo);
                    jogo.OddOUs = odds;
                    s.Store(jogo);
                    s.SaveChanges();
                }
            }
        }

        public List<Time> CriaTimes(string idBet, bool driverParalelo = false)
        {
            var driver = driverParalelo ? _driverParalelo : _driver;

            List<Time> times = new List<Time>();
            var timesNomes = driver.FindElements(By.ClassName("participant-imglink")).ToList();
            times.Add(new Time(timesNomes[1].Text));
            times.Add(new Time(timesNomes[3].Text));
            return times;
        }

        public async void CriaOuAtualizaJogo(Jogo jogo)
        {

            using (var s = context.DocumentStore.OpenSession())
            {
                try
                {
                    s.Store(jogo);
                    s.SaveChanges();
                }
                catch
                {
                    await Task.Delay(2000);
                    s.Store(jogo);
                    s.SaveChanges();
                }

            }
        }

        private bool OsDoisTimesFazemPoucosGols(Jogo jogo)
        {
            var time1_05_15_25_OversTotal = jogo.Time1.AcimaAbaixo
                       .Where(a => a.Tipo == EClassificacaoTipo.Total)
                       .SelectMany(a => a.Overs)
                       .Where(o => o.Overs > 0).ToList();

            var time1_05_15_25_Overs = jogo.Time1.AcimaAbaixo
                        .Where(a => a.Tipo == EClassificacaoTipo.Casa)
                        .SelectMany(a => a.Overs)
                        .Where(o => o.Overs > 0).ToList();

            var time2_05_15_25_OversTotal = jogo.Time2.AcimaAbaixo
                   .Where(a => a.Tipo == EClassificacaoTipo.Total)
                   .SelectMany(a => a.Overs)
                   .Where(o => o.Overs > 0).ToList();

            var time2_05_15_25_Overs = jogo.Time2.AcimaAbaixo
                        .Where(a => a.Tipo == EClassificacaoTipo.Fora)
                        .SelectMany(a => a.Overs)
                        .Where(o => o.Overs > 0).ToList();

            var time1_overs15 = time1_05_15_25_Overs.FirstOrDefault(o => o.Valor == 1.5)?.Gols;
            var time1_overs15Total = time1_05_15_25_OversTotal.FirstOrDefault(o => o.Valor == 1.5)?.Gols;
            var time2_overs15 = time2_05_15_25_Overs.FirstOrDefault(o => o.Valor == 1.5)?.Gols;
            var time2_overs15Total = time2_05_15_25_OversTotal.FirstOrDefault(o => o.Valor == 1.5)?.Gols;

            time1_overs15 = time1_05_15_25_Overs[0].TotalUltimosJogos < 4 && time1_05_15_25_OversTotal != null ? time1_overs15Total : time1_overs15;
            time2_overs15 = time2_05_15_25_Overs[0].TotalUltimosJogos < 4 && time2_05_15_25_OversTotal != null ? time2_overs15Total : time2_overs15;

            int time1GolsR = GolsRealizadosConvert(time1_overs15);
            int time1GolsS = GolsSofridosConvert(time1_overs15);
            int time2GolsR = GolsRealizadosConvert(time2_overs15);
            int time2GolsS = GolsSofridosConvert(time2_overs15);

            if (time1GolsR > time1GolsS || time2GolsR > time2GolsS) return false;

            bool time1FazPoucosGols = time1GolsR <= (time1GolsR - (time1GolsR * 0.4));
            bool time2FazPoucosGols = time2GolsR <= (time2GolsR - (time2GolsR * 0.4));

            return time1FazPoucosGols && time2FazPoucosGols;

        }

        private async Task PegaResumoDoJogo(string idBet, bool driverParalelo = false)
        {
            this.IdAtual = idBet;
            var driver = driverParalelo ? _driverParalelo : _driver;
            driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogo.Replace("ID", idBet));
            await Task.Delay(2000);
            List<Time> times = CriaTimes(idBet, driverParalelo);
            if (times == null) return;
            Jogo jogo = await CriaJogo(idBet, times, driverParalelo);
            if (jogo == null) return;

            await PegaInfosClassficacao(idBet, times, driverParalelo);

            await PegaInfosAcimaAbaixo(idBet, times, jogo.GolsTime1 + jogo.GolsTime2, driverParalelo);



            AnalisaMediaGolsMenorQueDois(jogo);
            AnalisaSeMelhorJogo(jogo);


            CriaOuAtualizaJogo(jogo);

        }

        public async Task AtualizaInformacoesBasicasJogo(Jogo jogo, bool driverParalelo = false)
        {
            var driver = driverParalelo ? _driverParalelo : _driver;
            this.IdAtual = jogo.IdJogoBet;

            driver.Navigate().GoToUrl(_configuration.Sites.Resultado.ResumoJogo.Replace("ID", jogo.IdJogoBet));
            await Task.Delay(2000);
            var status = driver.FindElement(By.ClassName("mstat")).FindElements(By.TagName("span")).Count > 0 ?
                                   driver.FindElement(By.ClassName("mstat")).FindElements(By.TagName("span"))[0]?.Text :
                                   "";

            if (status == "Intervalo" || status == "Encerrado") return;
            string minutos = null;

            int score1 = 0;
            int score2 = 0;
            try
            {
                var scores = driver.FindElements(By.ClassName("scoreboard"));
                score1 = int.Parse(scores[0]?.Text);
                score2 = int.Parse(scores[1]?.Text);
            }
            catch { };


            try
            {
                minutos = driver.FindElement(By.Id("atomclock"))
                  .FindElements(By.TagName("span"))[0].Text;
            }
            catch { };

            if (status == "Intervalo") minutos = "45";

            var divLiga = driver.FindElement(By.ClassName("fleft"));

            jogo.Status = status;
            jogo.Minutos = minutos;
            jogo.GolsTime1 = score1;
            jogo.GolsTime2 = score2;

        }

        private async Task<Jogo> CriaJogo(string idBet, List<Time> times, bool driverParalelo = false)
        {
            var driver = driverParalelo ? _driverParalelo : _driver;

            var data = driver.FindElement(By.Id("utime"))?.Text;
            var ligaTitulo = driver.FindElement(By.ClassName("fleft"))
                                .FindElements(By.TagName("span"))[1]?.Text;

            var jogo = new Jogo(idBet, DateTime.Parse(data), ligaTitulo, "", "");
            jogo.Time1 = times[0];
            jogo.Time2 = times[1];

            await AtualizaInformacoesBasicasJogo(jogo, driverParalelo);

            jogo.DataImportacao = DateTime.Now.Date;

            return jogo;
        }

        private List<OddOU> CriarOddsBet365(string id = null, bool driverParalelo = false)
        {
            var driver = driverParalelo ? _driverParalelo : _driver;
            List<OddOU> odds = new List<OddOU>();
            try
            {
                NavegarParaSite(_configuration.Sites.Resultado.OddsOU_Regulamentar.Replace("ID", id), driverParalelo);
                var divTables = driver.FindElement(By.Id("block-under-over-ft"));
                var tables = divTables.FindElements(By.TagName("table"));

                foreach (var table in tables)
                {
                    var trs = table.FindElements(By.ClassName("odd"));
                    foreach (var tr in trs)
                    {
                        bool bet365 = tr.FindElement(By.TagName("a")).GetAttribute("title") == "bet365";
                        if (bet365)
                        {
                            var total = tr.FindElements(By.TagName("td"))[1].Text.Replace(".", ",");

                            if (double.Parse(total) > 2.5) break;

                            var over = tr.FindElements(By.TagName("td"))[2].Text.Replace(".", ",");
                            odds.Add(new OddOU(EOddOUTipo.TempoRegulamentar, "Bet365", double.Parse(total), double.Parse(over)));

                        }
                    }
                }

                return odds;
            }
            catch (Exception e)
            {
                string errorMessage = e.Message;
                return new List<OddOU>();
            }

        }

        public void AnalisaSeMelhorJogo(Jogo jogo)
        {

            var time1_05_15_25_Overs = jogo.Time1.AcimaAbaixo.Where(a => a.Tipo == EClassificacaoTipo.Casa)
                                                          .SelectMany(a => a.Overs)
                                                          .ToList();

            var time2_05_15_25_Overs = jogo.Time2.AcimaAbaixo.Where(a => a.Tipo == EClassificacaoTipo.Fora)
                                                        .SelectMany(a => a.Overs)
                                                        .ToList();
            // Time1

            var time1_overs05 = GetOvers(jogo.Time1, 0.5, EClassificacaoTipo.Casa);
            var time1_overs15 = GetOvers(jogo.Time1, 1.5, EClassificacaoTipo.Casa);
            var time1_overs25 = GetOvers(jogo.Time1, 2.5, EClassificacaoTipo.Casa);

            // Time2

            var time2_overs05 = GetOvers(jogo.Time2, 0.5, EClassificacaoTipo.Fora);
            var time2_overs15 = GetOvers(jogo.Time2, 1.5, EClassificacaoTipo.Fora);
            var time2_overs25 = GetOvers(jogo.Time2, 2.5, EClassificacaoTipo.Fora);

            if (PoucosJogosTime(jogo.Time1, EClassificacaoTipo.Casa) || PoucosJogosTime(jogo.Time2, EClassificacaoTipo.Fora)) return;

            //MediaGols
            double time1_mediaGols = MediaGols(jogo.Time1, EClassificacaoTipo.Casa);
            double time2_mediaGols = MediaGols(jogo.Time2, EClassificacaoTipo.Fora);
            double mediaGols = (time1_mediaGols + time2_mediaGols) / 2;

            var m1 = time1_05_15_25_Overs.FirstOrDefault()?.Gols ?? "";
            var m2 = time2_05_15_25_Overs.FirstOrDefault()?.Gols ?? "";
            var classTime1 = jogo.Time1.Classificacoes.FirstOrDefault().Lugar;
            var classTime2 = jogo.Time2.Classificacoes.FirstOrDefault().Lugar;
            var classTotal = jogo.Time1.Classificacoes.FirstOrDefault().TotalLugares;
            bool golsIrregularTime1 = TimeGolsIrregular(jogo.Time1.AcimaAbaixo.FirstOrDefault().Overs.FirstOrDefault().Gols);
            bool golsIrregularTime2 = TimeGolsIrregular(jogo.Time2.AcimaAbaixo.FirstOrDefault().Overs.FirstOrDefault().Gols);
            bool jogoComTimeComGolsIrregulares = golsIrregularTime1 || golsIrregularTime2;
            bool timesComPoucaDiferencaClassificacao = TimesPoucaDiferencaClassificacao(jogo);
            bool umDosTimesFazMaisGols = UmDosTimesFazMaisGol(jogo);
            bool osDoisTimesFazemPoucosGols = OsDoisTimesFazemPoucosGols(jogo);
            var somaOvers05 = time1_overs05 + time2_overs05;
            var somaOvers15 = time1_overs15 + time2_overs15;
            var somaOvers25 = time1_overs25 + time2_overs25;

            if (mediaGols < 3.5 || !umDosTimesFazMaisGols || !(jogoComTimeComGolsIrregulares || timesComPoucaDiferencaClassificacao) || somaOvers05 < 9 || somaOvers15 < 9 || somaOvers25 > 8) return;

            telegramService.EnviaMensagemParaOGrupo($"OVER\n{jogo.Time1.Nome} - {jogo.Time2.Nome} \n" +
                                                                    $"{jogo.Liga} \n" +
                                                                    $"{jogo.DataInicio}\n" +
                                                                    $"Média Gols: {time1_mediaGols} / {time2_mediaGols} \n Média Gols Total: {mediaGols} \n" +
                                                                    $"Gols: {m1} / {m2}\n" +
                                                                    $"Overs: {time1_overs05} / {time2_overs05} \n" +
                                                                    $"Class: {classTime1} / {classTime2} de {classTotal} \n" +
                                                                    $"Classif. Perto : {timesComPoucaDiferencaClassificacao} \n Gols Irregulares: {jogoComTimeComGolsIrregulares} \n" +
                                                                    $"Os dois times fazem poucos gols: { osDoisTimesFazemPoucosGols } \n" +
                                                                    $"Um ou os dois Times Fazem Mais Gols: { umDosTimesFazMaisGols } \n" +
                                                                    $"Boa Aposta", true);
        }

        public void AnalisaMediaGolsMenorQueDois(Jogo jogo)
        {

            var time1_05_15_25_Overs = jogo.Time1.AcimaAbaixo.Where(a => a.Tipo == EClassificacaoTipo.Casa)
                                                          .SelectMany(a => a.Overs)
                                                          .ToList();

            var time2_05_15_25_Overs = jogo.Time2.AcimaAbaixo.Where(a => a.Tipo == EClassificacaoTipo.Fora)
                                                        .SelectMany(a => a.Overs)
                                                        .ToList();
            // Time1

            var time1_overs05 = GetOvers(jogo.Time1, 0.5, EClassificacaoTipo.Casa);
            var time1_overs15 = GetOvers(jogo.Time1, 1.5, EClassificacaoTipo.Casa);
            var time1_overs25 = GetOvers(jogo.Time1, 2.5, EClassificacaoTipo.Casa);

            // Time2

            var time2_overs05 = GetOvers(jogo.Time2, 0.5, EClassificacaoTipo.Fora);
            var time2_overs15 = GetOvers(jogo.Time2, 1.5, EClassificacaoTipo.Fora);
            var time2_overs25 = GetOvers(jogo.Time2, 2.5, EClassificacaoTipo.Fora);

            if (PoucosJogosTime(jogo.Time1, EClassificacaoTipo.Casa) || PoucosJogosTime(jogo.Time2, EClassificacaoTipo.Fora)) return;

            //MediaGols
            double time1_mediaGols = MediaGols(jogo.Time1, EClassificacaoTipo.Casa);
            double time2_mediaGols = MediaGols(jogo.Time2, EClassificacaoTipo.Fora);
            double mediaGols = (time1_mediaGols + time2_mediaGols) / 2;

            var m1 = time1_05_15_25_Overs.FirstOrDefault()?.Gols ?? "";
            var m2 = time2_05_15_25_Overs.FirstOrDefault()?.Gols ?? "";
            var classTime1 = jogo.Time1.Classificacoes.FirstOrDefault().Lugar;
            var classTime2 = jogo.Time2.Classificacoes.FirstOrDefault().Lugar;
            var classTotal = jogo.Time1.Classificacoes.FirstOrDefault().TotalLugares;
            bool golsIrregularTime1 = TimeGolsIrregular(jogo.Time1.AcimaAbaixo.FirstOrDefault().Overs.FirstOrDefault().Gols);
            bool golsIrregularTime2 = TimeGolsIrregular(jogo.Time2.AcimaAbaixo.FirstOrDefault().Overs.FirstOrDefault().Gols);
            bool jogoComTimeComGolsIrregulares = golsIrregularTime1 || golsIrregularTime2;
            bool timesComPoucaDiferencaClassificacao = TimesPoucaDiferencaClassificacao(jogo);
            bool umDosTimesFazMaisGols = UmDosTimesFazMaisGol(jogo);
            bool osDoisTimesFazemPoucosGols = OsDoisTimesFazemPoucosGols(jogo);

            if (time1_mediaGols > 1.9 || time2_mediaGols > 1.9 || (time1_overs25 + time2_overs25 > 3)) return;

            if (mediaGols < 2) telegramService.EnviaMensagemParaOGrupo($"UNDER\n{jogo.Time1.Nome} - {jogo.Time2.Nome} \n" +
                                                                    $"{jogo.Liga} \n" +
                                                                    $"{jogo.DataInicio}\n" +
                                                                    $"Média Gols: {time1_mediaGols} / {time2_mediaGols} \n Média Gols Total: {mediaGols} \n" +
                                                                    $"Gols: {time1_mediaGols} / {time2_mediaGols}\n" +
                                                                    $"Overs: {time1_overs15} / {time2_overs15} \n" +
                                                                    $"Class: {classTime1} / {classTime2} de {classTotal} \n" +
                                                                    $"Classif. Perto : {timesComPoucaDiferencaClassificacao} \n Gols Irregulares: {jogoComTimeComGolsIrregulares} \n" +
                                                                    $"Os dois times fazem poucos gols: { osDoisTimesFazemPoucosGols } \n" +
                                                                    $"Um ou os dois Times Fazem Mais Gols: { umDosTimesFazMaisGols } \n" +
                                                                    $"Over: 0.5 \n" +
                                                                    $"Boa Aposta", true); ;
        }

        private async Task PegaInfosAcimaAbaixo(string idBet, List<Time> times, int totalGols, bool driverParalelo = false)
        {
            var driver = driverParalelo ? _driverParalelo : _driver;

            // Total
            // 0.5
            NavegarParaSite(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Total_05.Replace("ID", idBet), driverParalelo);
            await Task.Delay(2000);
            var tabelaAcimaAbaixoTotal05 = driver.FindElement(By.Id("table-type-6-0.5"));
            CriaAcimaAbaixoTotal(0.5, tabelaAcimaAbaixoTotal05, EClassificacaoTipo.Total, times);

            // 1.5
            NavegarParaSite(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Total_15.Replace("ID", idBet), driverParalelo);
            await Task.Delay(2000);
            var tabelaAcimaAbaixoTotal15 = driver.FindElement(By.Id("table-type-6-1.5"));
            CriaAcimaAbaixoTotal(1.5, tabelaAcimaAbaixoTotal15, EClassificacaoTipo.Total, times);

            // 2.5
            NavegarParaSite(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Total_25.Replace("ID", idBet), driverParalelo);
            await Task.Delay(2000);
            var tabelaAcimaAbaixoTotal25 = driver.FindElement(By.Id("table-type-6-2.5"));
            CriaAcimaAbaixoTotal(2.5, tabelaAcimaAbaixoTotal25, EClassificacaoTipo.Total, times);

            // 2.5
            NavegarParaSite(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Casa_25.Replace("ID", idBet), driverParalelo);
            await Task.Delay(2000);
            var tabelaAcimaAbaixoCasa25 = driver.FindElement(By.Id("table-type-17-2.5"));
            CriaAcimaAbaixo(2.5, tabelaAcimaAbaixoCasa25, EClassificacaoTipo.Casa, times[0]);

            // 2.5
            NavegarParaSite(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Fora_25.Replace("ID", idBet), driverParalelo);
            await Task.Delay(2000);
            var tabelaAcimaAbaixoFora25 = driver.FindElement(By.Id("table-type-18-2.5"));
            CriaAcimaAbaixo(2.5, tabelaAcimaAbaixoFora25, EClassificacaoTipo.Fora, times[1]);

            // 0.5
            NavegarParaSite(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Casa_05.Replace("ID", idBet), driverParalelo);
            await Task.Delay(2000);
            var tabelaAcimaAbaixoCasa05 = driver.FindElement(By.Id("table-type-17-0.5"));
            CriaAcimaAbaixo(0.5, tabelaAcimaAbaixoCasa05, EClassificacaoTipo.Casa, times[0]);

            // 0.5
            NavegarParaSite(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Fora_05.Replace("ID", idBet), driverParalelo);
            await Task.Delay(2000);
            var tabelaAcimaAbaixoFora05 = driver.FindElement(By.Id("table-type-18-0.5"));
            CriaAcimaAbaixo(0.5, tabelaAcimaAbaixoFora05, EClassificacaoTipo.Fora, times[1]);

            //1.5
            NavegarParaSite(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Casa_15.Replace("ID", idBet), driverParalelo);
            await Task.Delay(2000);
            var tabelaAcimaAbaixoCasa15 = driver.FindElement(By.Id("table-type-17-1.5"));
            CriaAcimaAbaixo(1.5, tabelaAcimaAbaixoCasa15, EClassificacaoTipo.Casa, times[0]);

            //1.5
            NavegarParaSite(_configuration.Sites.Resultado.ResumoJogoClassificacaoAcimaAbaixo_Fora_15.Replace("ID", idBet), driverParalelo);
            await Task.Delay(2000);
            var tabelaAcimaAbaixoFora15 = driver.FindElement(By.Id("table-type-18-1.5"));
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

                Over o = new Over(overValor, gols, gj, overs, unders, overs + unders, GolsRealizadosConvert(gols), GolsSofridosConvert(gols));
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
                    var gols = tr.FindElement(By.ClassName("col_goals")).Text;
                    var gj = tr.FindElement(By.ClassName("col_avg_goals_match")).Text;
                    var asTag = tr.FindElement(By.ClassName("col_last_5"))
                                    .FindElements(By.TagName("a"));

                    var overs = tr.FindElements(By.ClassName("form-over")).Count;
                    var unders = tr.FindElements(By.ClassName("form-under")).Count;

                    Over o = new Over(overValor, gols, gj, overs, unders, overs + unders, GolsRealizadosConvert(gols), GolsSofridosConvert(gols));
                    var aa = new AcimaAbaixo(tipo);
                    aa.Overs.Add(o);
                    time.AcimaAbaixo.Add(aa);
                }
            }
        }

        private async Task PegaInfosClassficacao(string idBet, List<Time> times, bool driverParalelo = false)
        {
            var driver = driverParalelo ? _driverParalelo : _driver;
            //Classificação
            // Total
            NavegarParaSite(_configuration.Sites.Resultado.ResumoJogoClassificacao_Total.Replace("ID", idBet), driverParalelo);
            await Task.Delay(1000);
            driver.FindElement(By.Id("tabitem-table")).Click();
            var tabelaClassificacaoTotal = driver.FindElement(By.Id("table-type-1"));
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
    }
}
