using BetProject.Enums;
using System.Collections.Generic;
using System.Linq;

namespace BetProject.ObjectValues
{
    public class Classificacao
    {
        public Classificacao(EClassificacaoTipo tipo, int totalVitorias, int totalEmpates, int totalDerrotas, 
            int qtdJogos, int lugar, int totalLugares, string gols)
        {
            Tipo = tipo;
            TotalVitorias = totalVitorias;
            TotalEmpates = totalEmpates;
            TotalDerrotas = totalDerrotas;
            QtdJogos = qtdJogos;
            Lugar = lugar;
            TotalLugares = totalLugares;
            Gols = gols;

        }

        public EClassificacaoTipo Tipo { get; set; }
        public int TotalVitorias { get; set; } = 0;
        public int TotalEmpates { get; set; } = 0;
        public int TotalDerrotas { get; set; } = 0;
        public int QtdJogos { get; set; } = 0;
        public int Lugar { get; set; } = 0;
        public int TotalLugares { get; set; } = 0;
        public string Gols { get; set; }
    }
}
