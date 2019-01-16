using BetProject.ObjectValues;
using System;

namespace BetProject.Models
{
    public class JogoLiga
    {
        public string Id { get; set; }
        public string Liga { get; set; }
        public TimeLiga Time1 { get; set; }
        public TimeLiga Time2 { get; set; }
        public string TimeVencedor { get; set; }
        public string TimePerdedor { get; set; }
        public DateTime DataAtualizacao { get; set; }
    }
}
