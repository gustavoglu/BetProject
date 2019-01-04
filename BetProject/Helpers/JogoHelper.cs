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

        public static bool StatusJogo(string minutos)
        {
            if (minutos.IndexOf(":") >= 0) return false;
            int test = 0;
            return !(int.TryParse(minutos, out test));
        }

    }
}
