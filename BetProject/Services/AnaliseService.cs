using BetProject.Enums;
using BetProject.Helpers;
using BetProject.Infra.Repositories;
using BetProject.Models;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BetProject.Services
{
    public class AnaliseService
    {

        private readonly TelegramService _telegramService;
        private readonly IdContainerRepository _idContainerRepository;
        public AnaliseService()
        {
            _telegramService = new TelegramService();
            _idContainerRepository = new IdContainerRepository();
        }

        public string
            MensagemJogo(Jogo jogo, string topInfo = null, double? over = null)
        {
            topInfo = topInfo == null ? "" : topInfo + "\n";
            string overMsg = over.HasValue ? $"Over: {over.Value}\n" : "";
            return $"{topInfo}{jogo.Time1.Nome} - {jogo.Time2.Nome}\n" +
                                                                $"{jogo.Liga}\n" +
                                                                $"{jogo.DataInicio}\n" +
                                                                $"Obs: {jogo.Observacoes}\n" +
                                                                $"Placar: {jogo.GolsTime1} x {jogo.GolsTime2}\n" +
                                                                $"Gols Total Placar: {jogo.GolsTotal}\n" +
                                                                $"Média Gols: {jogo.Time1.MediaGols} / {jogo.Time2.MediaGols} | {jogo.MediaGolsTotal}\n" +
                                                                $"Gols: {jogo.Time1.Gols} / {jogo.Time2.Gols}\n" +
                                                                $"Overs 0.5: {jogo.Time1.Overs05} / {jogo.Time2.Overs05} | {jogo.SomaOvers05}\n" +
                                                                $"Overs 1.5: {jogo.Time1.Overs15} / {jogo.Time2.Overs15} | {jogo.SomaOvers15}\n" +
                                                                $"Overs 2.5: {jogo.Time1.Overs25} / {jogo.Time2.Overs25} | {jogo.SomaOvers25}\n" +
                                                                $"Soma Overs: {jogo.SomaTotalOvers}\n" +
                                                                $"Class: {jogo.Time1.Classificacao} / {jogo.Time2.Classificacao} de {jogo.ClassificaoTotal}\n " +
                                                                $"Classif. Perto : {jogo.ClassifPerto}\n " +
                                                                $"Gols Irregulares: {jogo.GolsIrregulares}\n" +
                                                                $"Os dois times fazem poucos gols: { jogo.OsDoisTimesSofremGols}\n" +
                                                                $"Um Time Faz mais Gols e outro Sofre Mais Gols: { jogo.UmTimeFazMaisGolEOutroSofreMaisGol }\n" +
                                                                $"Jogo Com Time Com o Dobro de Gols: { jogo.JogoComTimeComODobroDeGols }\n" +
                                                                overMsg +
                                                                $"Boa Aposta\n" +
                                                                jogo.LinkResultados;
        }

        public void AnalisaMediaGolsMenorQue25(Jogo jogo)
        {
            int somaOvers25 = jogo.Time1.Overs25 + jogo.Time2.Overs25;

            if (jogo.Time1.MediaGols > 1.9 ||
                jogo.Time2.MediaGols > 1.9 ||
                somaOvers25 > 3 ||
                jogo.MediaGolsTotal > 1.9
                ) return;

            _telegramService.EnviaMensagemParaOGrupo(MensagemJogo(jogo, "UNDER"), true); ;
        }

        public void AnalisaSeMelhorJogo(Jogo jogo)
        {
            if (jogo.MediaGolsTotal < 3.5 ||
                !jogo.UmTimeFazMaisGolQueOOutro ||
                !(jogo.GolsIrregulares || jogo.ClassifPerto) ||
                jogo.SomaOvers05 < 9 ||
                jogo.SomaOvers15 < 9 ||
                jogo.SomaOvers25 < 9) return;

            _telegramService.EnviaMensagemParaOGrupo(MensagemJogo(jogo, "OVER", null), true);
        }

        public bool ValidacaoBasica05(Jogo jogo)
        {
            return jogo.UmOuOsDoisTimesTemJogosOversMenorQue5 ? jogo.SomaOvers05 >= 8 : jogo.SomaOvers05 > 8 &&
                   jogo.SomaOvers15 >= 7 &&
                   jogo.MediaGolsTotal >= 2 &&
                   jogo.Time1.MediaGols > 1.9 &&
                   jogo.Time2.MediaGols > 1.9;
        }
        public bool ValidacaoBasica15(Jogo jogo)
        {

            return jogo.MediaGolsTotal > 2.4 &&
                   jogo.Time1.MediaGols > 1.9 &&
                   jogo.Time2.MediaGols > 1.9 &&
                   jogo.SomaOvers15 >= 7;
        }
        public bool ValidacaoBasica25(Jogo jogo)
        {

            return jogo.SomaOvers25 > 6 &&
                   jogo.MediaGolsTotal >= 2.9 &&
                   jogo.Time1.MediaGols > 2.4 &&
                   jogo.Time2.MediaGols > 2.4;
        }
        public bool ValidacaoBasica35(Jogo jogo)
        {

            int diferencaGols = jogo.GolsTime1 > jogo.GolsTime2 ? jogo.GolsTime1 - jogo.GolsTime2 : jogo.GolsTime2 - jogo.GolsTime1;

            return jogo.SomaOvers25 > 6 &&
                   jogo.MediaGolsTotal >= 2.9 &&
                   jogo.Time1.MediaGols > 2.4 &&
                   jogo.Time2.MediaGols > 2.4 &&
                   jogo.GolsTotal > 2 &&
                   diferencaGols < 3;;
        }

        public void AnalisaHT(Jogo jogo)
        {
            int minutos = JogoHelper.ConvertMinutos(jogo.Minutos);
            bool validacaoBasica05 = ValidacaoBasica05(jogo);
            bool validacaoBasica15 = ValidacaoBasica15(jogo);

            if (minutos > 22) return;

            if (minutos <= 8 && jogo.GolsTotal == 1 && validacaoBasica15) EnviaNotificacao(jogo, 1.5, "1.5", 1);
            if (minutos > 13 && jogo.GolsTotal == 0 && validacaoBasica15) EnviaNotificacao(jogo, 0.5, "0.5", 1);
        }

        public void AnalisaFT(Jogo jogo)
        {
            int minutos = JogoHelper.ConvertMinutos(jogo.Minutos);
            bool validacaoBasica05 = ValidacaoBasica05(jogo);
            bool validacaoBasica15 = ValidacaoBasica15(jogo);
            bool validacaoBasica25 = ValidacaoBasica25(jogo);
            bool validacaoBasica35 = ValidacaoBasica35(jogo);

            if (minutos < 55 || minutos > 75 ) return;

            bool ft05_0x0 = jogo.Observacoes == "0x0 FT 0.5";

            if (minutos >= 55 && jogo.GolsTotal == 0 && validacaoBasica05 && ft05_0x0) EnviaNotificacao(jogo, 0.5, "0.5", 3);
            if (minutos >= 60 && jogo.GolsTotal == 0 && validacaoBasica05) EnviaNotificacao(jogo, 0.5, "0.5", 2);
            if (minutos >= 60 && jogo.GolsTotal == 1 && validacaoBasica15) EnviaNotificacao(jogo, 1.5, "1.5", 2);
            if (minutos >= 60 && jogo.GolsTotal > 2 && validacaoBasica35) EnviaNotificacao(jogo, 2.5, "2.5", 1);
            if (minutos >= 60 && jogo.GolsTotal == 2 && validacaoBasica25) EnviaNotificacao(jogo, 3.5, "3.5", 1);
        }


        public void EnviaNotificacao(Jogo jogo, double valor, string desc, int numero)
        {
            if (!_idContainerRepository.NotificacaoJaEnviada(jogo.IdJogoBet, desc, numero))
            {
                _telegramService.EnviaMensagemParaOGrupo(MensagemJogo(jogo, null, valor));
                _idContainerRepository.SalvaEnvioDeNotificao(jogo.IdJogoBet, desc, numero);
                return;
            }
        }

        public void AnalisaJogoLive(Jogo jogo)
        {
            if (jogo.GolsTime1 + jogo.GolsTime2 > 3) return;
            if (jogo.Status == "Intervalo") return;
            if (jogo.Status == "Encerrado") return;

            int minutos = JogoHelper.ConvertMinutos(jogo.Minutos);

            AnalisaHT(jogo);
            AnalisaFT(jogo);
            //if (jogo.Observacoes == "0x0 FT 0.5" &&
            //    minutos > 55 &&
            //    jogo.GolsTotal == 0 &&
            //    jogo.SomaOvers05 > 8 &&
            //    jogo.SomaOvers15 >= 7 &&
            //    jogo.MediaGolsTotal >= 2 &&
            //    jogo.Time1.MediaGols > 1.9 &&
            //    jogo.Time2.MediaGols > 1.9)
            //{
            //    if (!_idContainerRepository.NotificacaoJaEnviada(jogo.IdJogoBet, "0.5", 2))
            //    {
            //        _telegramService.EnviaMensagemParaOGrupo(MensagemJogo(jogo, null, 0.5));
            //        _idContainerRepository.SalvaEnvioDeNotificao(jogo.IdJogoBet, "0.5", 2);
            //        return;
            //    }
            //}

            //if (minutos > 5 && minutos < 80)
            //{

            //    if (minutos >= 13 && minutos <= 22 &&
            //       jogo.GolsTotal == 0 &&
            //       jogo.UmOuOsDoisTimesTemJogosOversMenorQue5 &&
            //       jogo.SomaOvers05 >= 8 &&
            //       jogo.SomaOvers15 >= 7 &&
            //       jogo.MediaGolsTotal >= 2 &&
            //       jogo.Time1.MediaGols > 1.9 &&
            //       jogo.Time2.MediaGols > 1.9)
            //    {
            //        if (!_idContainerRepository.NotificacaoJaEnviada(jogo.IdJogoBet, "0.5", 1))
            //        {
            //            _telegramService.EnviaMensagemParaOGrupo(MensagemJogo(jogo, null, 0.5));

            //            _idContainerRepository.SalvaEnvioDeNotificao(jogo.IdJogoBet, "0.5", 1);
            //            return;
            //        }
            //    }



            //    if (minutos >= 13 &&
            //        minutos <= 22 &&
            //        jogo.GolsTotal == 0 &&
            //        jogo.SomaOvers05 > 8 &&
            //        jogo.SomaOvers15 >= 7 &&
            //        jogo.MediaGolsTotal >= 2 &&
            //        jogo.Time1.MediaGols > 1.9 &&
            //        jogo.Time2.MediaGols > 1.9)
            //    {
            //        if (!_idContainerRepository.NotificacaoJaEnviada(jogo.IdJogoBet, "0.5", 1))
            //        {
            //            _telegramService.EnviaMensagemParaOGrupo(MensagemJogo(jogo, null, 0.5));

            //            _idContainerRepository.SalvaEnvioDeNotificao(jogo.IdJogoBet, "0.5", 1);
            //            return;
            //        }
            //    }

            //    if (jogo.GolsTotal == 1 &&
            //            minutos <= 8 &&
            //            jogo.MediaGolsTotal >= 2 &&
            //            jogo.Time1.MediaGols > 1.9 &&
            //            jogo.Time2.MediaGols > 1.9 &&
            //            jogo.SomaOvers15 >= 7)
            //    {
            //        if (!_idContainerRepository.NotificacaoJaEnviada(jogo.IdJogoBet, "1.5", 1))
            //        {
            //            _telegramService.EnviaMensagemParaOGrupo(MensagemJogo(jogo, null, 1.5));

            //            _idContainerRepository.SalvaEnvioDeNotificao(jogo.IdJogoBet, "1.5", 1);
            //            return;
            //        }
            //    }
            //}

            //if (minutos >= 60 &&
            //    jogo.GolsTotal == 0 &&
            //    jogo.UmOuOsDoisTimesTemJogosOversMenorQue5 &&
            //    jogo.SomaOvers05 >= 8 &&
            //    jogo.SomaOvers15 >= 7 &&
            //    jogo.MediaGolsTotal >= 2 &&
            //    jogo.Time1.MediaGols > 1.9 &&
            //    jogo.Time2.MediaGols > 1.9)
            //{
            //    if (!_idContainerRepository.NotificacaoJaEnviada(jogo.IdJogoBet, "0.5", 2))
            //    {
            //        _telegramService.EnviaMensagemParaOGrupo(MensagemJogo(jogo, null, 0.5));

            //        _idContainerRepository.SalvaEnvioDeNotificao(jogo.IdJogoBet, "0.5", 2);
            //        return;
            //    }
            //}

            //if (minutos >= 60 &&
            //     jogo.GolsTotal == 0 &&
            //     jogo.SomaOvers05 > 8 &&
            //     jogo.SomaOvers15 >= 7 &&
            //     jogo.MediaGolsTotal >= 2 &&
            //     jogo.Time1.MediaGols > 1.9 &&
            //     jogo.Time2.MediaGols > 1.9)
            //{
            //    if (!_idContainerRepository.NotificacaoJaEnviada(jogo.IdJogoBet, "0.5", 2))
            //    {
            //        _telegramService.EnviaMensagemParaOGrupo(MensagemJogo(jogo, null, 0.5));

            //        _idContainerRepository.SalvaEnvioDeNotificao(jogo.IdJogoBet, "0.5", 2);
            //        return;
            //    }
            //}

            //if (minutos >= 60 &&
            //    jogo.GolsTotal == 1 &&
            //    jogo.MediaGolsTotal > 2.4 &&
            //    jogo.Time1.MediaGols > 1.9 &&
            //    jogo.Time2.MediaGols > 1.9 &&
            //    jogo.SomaOvers15 >= 7)
            //{
            //    if (!_idContainerRepository.NotificacaoJaEnviada(jogo.IdJogoBet, "1.5", 2))
            //    {
            //        _telegramService.EnviaMensagemParaOGrupo(MensagemJogo(jogo, null, 1.5));
            //        _idContainerRepository.SalvaEnvioDeNotificao(jogo.IdJogoBet, "1.5", 2);
            //        return;
            //    }
            //}

            //if (jogo.GolsTotal == 2 && minutos >= 60)
            //{
            //    if (jogo.SomaOvers25 >= 6 &&
            //        jogo.MediaGolsTotal >= 2.9 &&
            //        jogo.Time1.MediaGols > 2.4 &&
            //        jogo.Time2.MediaGols > 2.4)
            //    {
            //        if (!_idContainerRepository.NotificacaoJaEnviada(jogo.IdJogoBet, "2.5", 1))
            //        {
            //            _telegramService.EnviaMensagemParaOGrupo(MensagemJogo(jogo, null, 2.5));
            //            _idContainerRepository.SalvaEnvioDeNotificao(jogo.IdJogoBet, "2.5", 1);
            //            return;
            //        }
            //    }
            //}

            //int diferencaGols = jogo.GolsTime1 > jogo.GolsTime2 ? jogo.GolsTime1 - jogo.GolsTime2 : jogo.GolsTime2 - jogo.GolsTime1;

            //if (minutos >= 60 &&
            //    jogo.GolsTotal > 2 &&
            //    diferencaGols < 3)
            //{
            //    if (jogo.SomaOvers25 > 6 &&
            //        jogo.MediaGolsTotal >= 2.9 &&
            //        jogo.Time1.MediaGols > 2.4 &&
            //        jogo.Time2.MediaGols > 2.4)
            //    {
            //        if (!_idContainerRepository.NotificacaoJaEnviada(jogo.IdJogoBet, "3.5", 1))
            //        {
            //            _telegramService.EnviaMensagemParaOGrupo(MensagemJogo(jogo, null, 3.5));
            //            _idContainerRepository.SalvaEnvioDeNotificao(jogo.IdJogoBet, "3.5", 1);
            //            return;
            //        }
            //    }
            //}
        }
    }
}
