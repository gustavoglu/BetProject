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
        public List<AcimaAbaixo> AcimaAbaixo { get; set; }
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
        public bool? UltimoOverPositivo { get; set; } = false;
        public bool UsouOversTotal { get; set; } = false;
        public int QtdJogos { get; set; }
        public int QtdJogosTotal { get; set; }
        public int QtdTotalDeJogosOvers { get; set; }
        public List<H2HInfo> H2HInfos { get; set; }
        public decimal PercOverUltimosJogos { get; set; }
        public int GolsRealizadosH2H { get; set; }
        public int GolsSofridosH2H { get; set; }
        public int QtdJogosH2H0 { get; set; }
        public int QtdJogosH2H05 { get; set; }
        public int QtdJogosH2H15 { get; set; }
        public int QtdJogosH2H25 { get; set; }
        public int QtdJogosH2HOver15 { get; set; }
        public int QtdJogosH2HOver25 { get; set; }
        public int QtdJogosUnderH2H25 { get; set; }
        public int QtdJogosUnderH2H35 { get; set; }
        public int QtdJogosUnderH2H45 { get; set; }
    }
}
