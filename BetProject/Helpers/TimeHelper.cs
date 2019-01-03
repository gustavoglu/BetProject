using BetProject.Enums;
using BetProject.ObjectValues;
using BetProject.Validations;
using System.Linq;

namespace BetProject.Helpers
{
    public class TimeHelper
    {
        public static int GolsSofridosConvert(string gols)
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

        public static int GolsRealizadosConvert(string gols)
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

        public static double MediaGols(Time time, EClassificacaoTipo tipo)
        {
            var time_05_15_25_Overs = time.AcimaAbaixo.Where(a => a.Tipo == tipo)
                                                            .SelectMany(a => a.Overs)
                                                            .ToList();

            var time_05_15_25_OversTotal = time.AcimaAbaixo
                                            .Where(a => a.Tipo == EClassificacaoTipo.Total)
                                            .SelectMany(a => a.Overs).ToList();

            var time_mediaGols = double.Parse(time_05_15_25_Overs[0]?.GJ?.Replace(".", ","));
            time.QtdJogos = time_05_15_25_Overs[0].J;

            if (time_05_15_25_OversTotal.Count == 0) return time_mediaGols;

            var time_mediaGolsTotal = double.Parse(time.AcimaAbaixo.FirstOrDefault(aa => aa.Tipo == EClassificacaoTipo.Total)
                                                                                                ?.Overs[0]?.GJ?.Replace(".", ","));

            time.UsouOversTotal = time_05_15_25_Overs[0].TotalUltimosJogos < 4 && time_05_15_25_OversTotal[0]?.TotalUltimosJogos > 4;

            if (time.UsouOversTotal) time.QtdJogos = time_05_15_25_OversTotal[0].J;

            return time_05_15_25_Overs[0].TotalUltimosJogos < 4 &&
                         time_05_15_25_OversTotal[0]?.TotalUltimosJogos > 4 ?
                         time_mediaGolsTotal : time_mediaGols;
        }

        public static int GetOvers(Time time, double valor, EClassificacaoTipo tipo)
        {
            var time_05_15_25_Overs = time.AcimaAbaixo.Where(a => a.Tipo == tipo)
                                                             .SelectMany(a => a.Overs)
                                                             .ToList();

            var time_05_15_25_OversTotal = time.AcimaAbaixo
                                            .Where(a => a.Tipo == EClassificacaoTipo.Total)
                                            .SelectMany(a => a.Overs).ToList();



            int time_overs = time_05_15_25_Overs.FirstOrDefault(o => o.Valor == valor)?.Overs ?? 0;
            int time_oversTotal = time_05_15_25_OversTotal.FirstOrDefault(o => o.Valor == valor)?.Overs ?? 0;

            if (time_oversTotal == 0) return time_overs;

            time.UsouOversTotal = TimeValidations.UsarTotal(time, valor, tipo);

            time_overs = time.UsouOversTotal ? time_oversTotal : time_overs;

            return time_overs;

        }

        public static int GetQtdJogos(Time time, EClassificacaoTipo tipo)
        {
            int? qtdJogosTime = time.UsouOversTotal ?
                                 time.AcimaAbaixo.FirstOrDefault(t => t.Tipo == EClassificacaoTipo.Total)?.Overs[0]?
                                 .TotalUltimosJogos :

                                 time.AcimaAbaixo.FirstOrDefault(t => t.Tipo == tipo)?.Overs[0]?.TotalUltimosJogos;

            return qtdJogosTime.HasValue ? qtdJogosTime.Value : time.AcimaAbaixo.FirstOrDefault(t => t.Tipo == tipo).Overs[0].TotalUltimosJogos;
        }
    }
}
