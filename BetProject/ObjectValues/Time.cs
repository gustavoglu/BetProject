using System.Collections.Generic;

namespace BetProject.ObjectValues
{
    public class Time
    {
        public Time(string nome)
        {
            Nome = nome;
            Classificacoes = new List<Classificacao>();
            AcimaAbaixo = new List<AcimaAbaixo>();
        }

        public string Nome { get; set; }
        public ICollection<Classificacao> Classificacoes { get; set; }
        public List<AcimaAbaixo> AcimaAbaixo  { get; set; }
        public double MediaGols { get; set; } = 0;
        public string Gols { get; set; }
        public int Classificacao { get; set; } = 0;
        public int GolsSofridos { get; set; } = 0;
        public int GolsRealizados { get; set; } = 0;
        public int GolsSofridosTotal { get; set; } = 0;
        public int GolsRealizadosTotal { get; set; } = 0;
        public int Overs05 { get; set; } = 0;
        public int Overs15 { get; set; } = 0;
        public int Overs25 { get; set; } = 0;
        public bool UsouOversTotal { get; set; } = false;
        public int QtdJogos { get; set; }
        public int QtdJogosTotal { get; set; }
        public int QtdTotalDeJogosOvers { get; set; }
    }
}
