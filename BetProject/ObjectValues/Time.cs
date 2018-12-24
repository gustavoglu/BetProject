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

    }
}
