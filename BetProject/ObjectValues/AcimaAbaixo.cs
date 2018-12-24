using BetProject.Enums;
using System.Collections.Generic;

namespace BetProject.ObjectValues
{
    public class AcimaAbaixo
    {
        public AcimaAbaixo(EClassificacaoTipo tipo)
        {
            Tipo = tipo;
            Overs = new List<Over>();
        }

        public EClassificacaoTipo Tipo { get; set; }
        public List<Over> Overs  { get; set; }
    }
}
