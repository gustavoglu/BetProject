using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BetProject.Models
{
    public class Jogo
    {
        public Jogo(DateTime dataInicio, string time1, string time2, string resultadoPrimeiroTempo, string resultadoFinal, string minutosJogo)
        {
            DataInicio = dataInicio;
            Time1 = time1;
            Time2 = time2;
            ResultadoPrimeiroTempo = resultadoPrimeiroTempo;
            ResultadoFinal = resultadoFinal;
            MinutosJogo = minutosJogo;
        }

        public DateTime DataInicio  { get; set; }
        public string MinutosJogo { get; set; }
        public string Time1 { get; set; }
        public string Time2 { get; set; }
        public string ResultadoPrimeiroTempo { get; set; }
        public string ResultadoFinal { get; set; }
    }
}
