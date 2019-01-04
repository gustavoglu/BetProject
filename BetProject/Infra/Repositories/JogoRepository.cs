using BetProject.Models;
using Raven.Client.Documents.Linq;
using System.Collections.Generic;
using System.Linq;

namespace BetProject.Infra.Repositories
{
    public class JogoRepository : Repository
    {
        public List<Jogo> TrazJogosPorIds(string[] ids)
        {
            using (var s = context.DocumentStore.OpenSession())
            {
                return s.Query<Jogo>().Where(j => j.IdJogoBet.In(ids)).ToList();
            }
        }

        public Jogo TrazerJogoPorIdBet(string idBet)
        {
            using (var s = context.DocumentStore.OpenSession())
                return s.Query<Jogo>().FirstOrDefault(j => j.IdJogoBet == idBet);

        }


        public Jogo TrazerJogoPorId(string idBet)
        {
            using (var s = context.DocumentStore.OpenSession())
                return s.Query<Jogo>().FirstOrDefault(j => j.IdJogoBet == idBet);

        }

        public bool JogoProntoParaAnalise(string idBet)
        {
            using (var s = context.DocumentStore.OpenSession())
                return s.Query<Jogo>().FirstOrDefault(jq => jq.IdJogoBet == idBet &&
                                                     jq.Time1.AcimaAbaixo.Any() &&
                                                     jq.Time2.AcimaAbaixo.Any() &&
                                                     jq.TimesComPoucosJogos == false) != null;
        }
    }
}
