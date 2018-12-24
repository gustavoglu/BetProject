using System;
using System.Collections.Generic;

namespace BetProject.ObjectValues
{
    public class IdJogo
    {
        public IdJogo(string id, DateTime dataInicio)
        {
            Id = id;
            DataInicio = dataInicio;
     
        }

        public string Id { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime DataInicio { get; set; }
        public bool Ignorar { get; set; } = false;

    }
}
