using BetProject.Enums;
using BetProject.Helpers;
using BetProject.Models;
using System.Linq;

namespace BetProject.Validations
{
    public class JogoValidations
    {
        private const double DIFERENCACLASSIFICACAOACEITAVEL = 0.35;
        private const double DIFERENCAGOLSACIMAACEITAVEL = 0.4;
        private const double DIFERENCAPOUCOSGOLSACEITAVEL = 0.4;

        public static bool TimesPoucaDiferencaClassificacao(Jogo jogo)
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

        public static bool UmDosTimesFazMaisGol(Jogo jogo)
        {
            if (jogo.Time1.AcimaAbaixo.FirstOrDefault(aa => aa.Tipo == EClassificacaoTipo.Total) == null || jogo.Time2.AcimaAbaixo.FirstOrDefault(aa => aa.Tipo == EClassificacaoTipo.Total) == null) return false;

            var time1TotalJogos = jogo.Time1.AcimaAbaixo.FirstOrDefault(aa => aa.Tipo == EClassificacaoTipo.Total)?.Overs[0]?.J;
            var time2TotalJogos = jogo.Time2.AcimaAbaixo.FirstOrDefault(aa => aa.Tipo == EClassificacaoTipo.Total)?.Overs[0]?.J;

            var time1GolsR = TimeHelper.GolsRealizadosConvert(jogo.Time1.AcimaAbaixo.FirstOrDefault(aa => aa.Tipo == EClassificacaoTipo.Total).Overs[0].Gols);
            var time2GolsR = TimeHelper.GolsRealizadosConvert(jogo.Time2.AcimaAbaixo.FirstOrDefault(aa => aa.Tipo == EClassificacaoTipo.Total).Overs[0].Gols);
            bool time1FazMaisGolsQueSofre = time1GolsR >= time1TotalJogos;
            bool time2FazMaisGolsQueSofre = time2GolsR >= time2TotalJogos;


            return time1FazMaisGolsQueSofre || time2FazMaisGolsQueSofre;
        }

        public static bool UmTimeFazMaisGolEOutroSofreMaisGols(Jogo jogo)
        {
            var qtdAceitavelTime1 = jogo.Time1.QtdJogos + (jogo.Time1.QtdJogos * DIFERENCAGOLSACIMAACEITAVEL);
            var qtdAceitavelTime2 = jogo.Time2.QtdJogos + (jogo.Time2.QtdJogos * DIFERENCAGOLSACIMAACEITAVEL);

            bool time1FazMaisGols = jogo.Time1.GolsRealizados > jogo.Time1.QtdJogos ? jogo.Time1.GolsRealizados > qtdAceitavelTime1 : false;
            bool time2FazMaisGols = jogo.Time2.GolsRealizados > jogo.Time2.QtdJogos ? jogo.Time2.GolsRealizados > qtdAceitavelTime2 : false;
            bool time1SofreMaisGols = jogo.Time1.GolsSofridos > jogo.Time1.QtdJogos ? jogo.Time1.GolsSofridos > qtdAceitavelTime1 : false;
            bool time2SofreMaisGols = jogo.Time2.GolsSofridos > jogo.Time2.QtdJogos ? jogo.Time2.GolsSofridos > qtdAceitavelTime2 : false;

            if (!time1FazMaisGols && !time2FazMaisGols) return false;

            return time1FazMaisGols && time2SofreMaisGols || time2FazMaisGols && time1SofreMaisGols;
        }

        public static bool OsDoisTimesFazemPoucosGols(Jogo jogo)
        {
            var time1_05_15_25_OversTotal = jogo.Time1.AcimaAbaixo
                       .Where(a => a.Tipo == EClassificacaoTipo.Total)
                       .SelectMany(a => a.Overs).ToList();

            var time1_05_15_25_Overs = jogo.Time1.AcimaAbaixo
                        .Where(a => a.Tipo == EClassificacaoTipo.Casa)
                        .SelectMany(a => a.Overs).ToList();

            var time2_05_15_25_OversTotal = jogo.Time2.AcimaAbaixo
                   .Where(a => a.Tipo == EClassificacaoTipo.Total)
                   .SelectMany(a => a.Overs).ToList();

            var time2_05_15_25_Overs = jogo.Time2.AcimaAbaixo
                        .Where(a => a.Tipo == EClassificacaoTipo.Fora)
                        .SelectMany(a => a.Overs).ToList();

            var time1_overs15 = time1_05_15_25_Overs.FirstOrDefault(o => o.Valor == 1.5)?.Gols;
            var time1_overs15Total = time1_05_15_25_OversTotal.FirstOrDefault(o => o.Valor == 1.5)?.Gols;
            var time2_overs15 = time2_05_15_25_Overs.FirstOrDefault(o => o.Valor == 1.5)?.Gols;
            var time2_overs15Total = time2_05_15_25_OversTotal.FirstOrDefault(o => o.Valor == 1.5)?.Gols;

            time1_overs15 = TimeValidations.UsarTotal(jogo.Time1, 1.5, EClassificacaoTipo.Casa) ? time1_overs15Total : time1_overs15;
            time2_overs15 = TimeValidations.UsarTotal(jogo.Time2, 1.5, EClassificacaoTipo.Fora) ? time2_overs15Total : time2_overs15;

            int time1GolsR = TimeHelper.GolsRealizadosConvert(time1_overs15);
            int time1GolsS = TimeHelper.GolsSofridosConvert(time1_overs15);
            int time2GolsR = TimeHelper.GolsRealizadosConvert(time2_overs15);
            int time2GolsS = TimeHelper.GolsSofridosConvert(time2_overs15);

            if (time1GolsR > time1GolsS || time2GolsR > time2GolsS) return false;

            bool time1FazPoucosGols = time1GolsR <= (time1GolsR - (time1GolsR * DIFERENCAPOUCOSGOLSACEITAVEL));
            bool time2FazPoucosGols = time2GolsR <= (time2GolsR - (time2GolsR * DIFERENCAPOUCOSGOLSACEITAVEL));

            return time1FazPoucosGols && time2FazPoucosGols;

        }

    }
}
