namespace BetProject.Helpers
{
    public class JogoHelper
    {
        public static int ConvertMinutos(string minutos)
        {
            return minutos.IndexOf(":") >= 0 ?
                   minutos.IndexOf(":") == 1 ? int.Parse(minutos[0].ToString()) :
                   int.Parse(string.Concat(minutos[0], minutos[1])) :
                   int.Parse(minutos);
        }
    }
}
