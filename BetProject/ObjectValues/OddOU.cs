using BetProject.Enums;

namespace BetProject.ObjectValues
{
    public class OddOU
    {
        public OddOU(EOddOUTipo tipo, string descricao, double total, double acima)
        {
            Tipo = tipo;
            Descricao = descricao;
            Total = total;
            Acima = acima;
        }

        public EOddOUTipo Tipo { get; set; }
        public string Descricao { get; set; }
        public double Total { get; set; }
        public double Acima { get; set; }
    }
}
