using BetProject.ObjectValues;
using System;
using System.Collections.Generic;

namespace BetProject.Models
{
    public class Jogo
    {
        public Jogo()
        {

        }
        public Jogo(string idBet ,DateTime dataInicio, string liga, string status, string minutos)
        {
            DataInicio = dataInicio;
            Liga = liga;
            Status = status;
            Minutos = minutos;
            this.IdJogoBet = idBet;

            OddOUs = new List<OddOU>();
            ResultadosTempo = new List<ResultadoTempo>();
            FimPrimeroTempo = dataInicio.AddMinutes(45);
            InicioSegundoTempo = dataInicio.AddMinutes(60);
            FimSegundoTempo = InicioSegundoTempo.AddMinutes(45);

        }


        public Jogo(string idBet ,DateTime dataInicio, Time time1, Time time2, string resultadoPrimeiroTempo, 
                    string resultadoFinal, string liga, string status, string minutos)
        {
            DataInicio = dataInicio;
            Time1 = time1;
            Time2 = time2;
            ResultadoPrimeiroTempo = resultadoPrimeiroTempo;
            ResultadoFinal = resultadoFinal;
            Liga = liga;
            status = Status;
            Minutos = minutos;
            this.IdJogoBet = idBet;

            OddOUs = new List<OddOU>();
            ResultadosTempo = new List<ResultadoTempo>();
            FimPrimeroTempo = dataInicio.AddMinutes(45);
            InicioSegundoTempo = dataInicio.AddMinutes(60);
            FimSegundoTempo = InicioSegundoTempo.AddMinutes(45);
        }

        public string Id { get; set; }
        public string IdJogoBet { get; set; }
        public string Observacoes { get; set; }
        public DateTime DataInicio  { get; set; }
        public Time Time1 { get; set; }
        public Time Time2 { get; set; }
        public string ResultadoPrimeiroTempo { get; set; }
        public string ResultadoFinal { get; set; }
        public string Liga { get; set; }
        public string Minutos { get; set; }
        public string Status { get; set; }
        public List<OddOU> OddOUs{ get; set; }
        public List<ResultadoTempo> ResultadosTempo { get; set; }
        public DateTime DataImportacao { get; set; }
        public bool AlertaEnviado { get; set; } = false;
        public int GolsTime1 { get; set; } = 0;
        public int GolsTime2 { get; set; } = 0;
        public int GolsTotal { get; set; } = 0;
        public int ClassificaoTotal { get; set; } = 0;
        public bool ClassifPerto { get; set; } = false;
        public bool GolsIrregulares { get; set; } = false;
        public bool OsDoisTimesSofremGols { get; set; } = false;
        public bool UmOuOsDoisTimesFazemMaisGols { get; set; } = false;
        public bool TimesComPoucosJogos { get; set; } = false;
        public bool UmTimeFazMaisGolQueOOutro { get; set; } = false;
        public bool UmTimeSofreMaisGolQueOOutro { get; set; } = false;
        public bool UmTimeFazMaisGolEOutroSofreMaisGol { get; set; } = false;
        public bool UmaDasMaioresMediasDoDia { get; set; } = false;
        public bool UmOuOsDoisTimesTemJogosOversMenorQue5 { get; set; }
        public bool JogoComTimeComODobroDeGols { get; set; }
        public double MediaGolsTotal { get; set; }
        public int SomaOvers05 { get; set; } = 0;
        public int SomaOvers15 { get; set; } = 0;
        public int SomaOvers25 { get; set; } = 0;
        public int SomaTotalOvers { get; set; }
        public DateTime FimPrimeroTempo { get; set; }
        public DateTime InicioSegundoTempo { get; set; }
        public DateTime FimSegundoTempo{ get; set; }
        public string LinkResultados { get; set; }
        public bool Ignorar { get; set; } = false;
    }
}
