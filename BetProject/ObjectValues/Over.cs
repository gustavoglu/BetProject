namespace BetProject.ObjectValues
{
    public class Over
    {
        public Over(double valor, string gols, string gJ, int overs, int unders, int totalUltimosJogos,int golsRealizados, int golsSofridos)
        {
            Valor = valor;
            Gols = gols;
            GJ = gJ;
            Overs = overs;
            Unders = unders;
            TotalUltimosJogos = totalUltimosJogos;
            GolsSofridos = golsSofridos;
            GolsRealizados = golsRealizados;
        }

        public int J { get; set; }
        public double Valor { get; set; }
        public int Acima { get; set; }
        public int Abaixo { get; set; }
        public string Gols { get; set; }
        public string GJ { get; set; }
        public int Overs { get; set; }
        public int Unders { get; set; }
        public int TotalUltimosJogos { get; set; }
        public int GolsSofridos { get; set; }
        public int GolsRealizados{ get; set; }
    }
}
