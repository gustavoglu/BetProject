namespace BetProject.ObjectValues
{
    public class ResultadoTempo
    {
        public ResultadoTempo(int tempo, int resultado)
        {
            Tempo = tempo;
            Resultado = resultado;
        }

        public int Tempo { get; set; } = 1;
        public string Time { get; set; }
        public int Resultado { get; set; } = 0;
    }
}
