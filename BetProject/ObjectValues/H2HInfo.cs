using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BetProject.ObjectValues
{
    public class H2HInfo
    {
        public string Time1 { get; set; }
        public string Time2 { get; set; }
        public int GolsTime1 { get; set; }
        public int GolsTime2 { get; set; }
        public int TotalGols { get; set; }
        public bool Time1Principal { get; set; }
        public bool Vencedor { get; set; }
        public bool Empate { get; set; }
    }
}
