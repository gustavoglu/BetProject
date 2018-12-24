using BetProject.ObjectValues;
using System;
using System.Collections.Generic;

namespace BetProject.Models
{
    public class IdContainer
    {
        public IdContainer(List<IdJogo> ids, DateTime data)
        {
            Ids = ids;
            Data = data;
            IdsComErro = new List<IdJogo>();
            IdsLive = new List<IdJogo>();
            Notificacoes = new List<NotificacaoJogo>();
        }

        public string Id { get; set; }
        public List<IdJogo> Ids { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime Data { get; set; }
        public List<IdJogo> IdsComErro { get; set; }
        public List<IdJogo> IdsLive{ get; set; }
        public List<NotificacaoJogo> Notificacoes { get; set; }

    }
}
