using BetProject.Enums;
using BetProject.ObjectValues;
using System.Linq;

namespace BetProject.Validations
{
    public class TimeValidations
    {
        private const double DIFERENCAGOLSIRREGULARES = 0.4;

        public static bool TimeGolsIrregular(string gols)
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

        public static bool PoucosJogosTime(Time time, EClassificacaoTipo tipo)
        {
            var time_05_15_25_Overs = time.AcimaAbaixo
                            .Where(a => a.Tipo == tipo)
                            .SelectMany(a => a.Overs)
                            .ToList();

            var time_05_15_25_OversTotal = time.AcimaAbaixo
                    .Where(a => a.Tipo == EClassificacaoTipo.Total)
                    .SelectMany(a => a.Overs).ToList();

            if (!time_05_15_25_OversTotal.Any()) return time_05_15_25_Overs.FirstOrDefault().TotalUltimosJogos < 4;

            var time1qtdJogosTotal = time.AcimaAbaixo.FirstOrDefault(aa => aa.Tipo == EClassificacaoTipo.Total).Overs[0].TotalUltimosJogos;
            var time1qtdJogos = time_05_15_25_Overs.FirstOrDefault().TotalUltimosJogos < 4 ? time1qtdJogosTotal :
                                   time_05_15_25_Overs.FirstOrDefault().TotalUltimosJogos;


            return time1qtdJogos < 4;
        }

        public static bool UsarTotal(Time time, double valor, EClassificacaoTipo tipo)
        {
            var time_05_15_25_Overs = time.AcimaAbaixo.Where(a => a.Tipo == tipo)
                                                             .SelectMany(a => a.Overs)
                                                             .ToList();

            var time_05_15_25_OversTotal = time.AcimaAbaixo
                                            .Where(a => a.Tipo == EClassificacaoTipo.Total)
                                            .SelectMany(a => a.Overs).ToList();



            int time_overs = time_05_15_25_Overs.FirstOrDefault(o => o.Valor == valor)?.Overs ?? 0;
            int time_oversTotal = time_05_15_25_OversTotal.FirstOrDefault(o => o.Valor == valor)?.Overs ?? 0;

            if (time_oversTotal == 0) return false;
            time.UsouOversTotal = time_05_15_25_Overs[0].TotalUltimosJogos < 4 && time_05_15_25_OversTotal[0]?.TotalUltimosJogos > 4;
            return time_05_15_25_Overs[0].TotalUltimosJogos < 4 && time_05_15_25_OversTotal[0]?.TotalUltimosJogos > 4;

        }

    }

}
