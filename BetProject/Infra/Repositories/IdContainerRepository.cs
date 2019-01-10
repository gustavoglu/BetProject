using BetProject.Models;
using BetProject.ObjectValues;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BetProject.Infra.Repositories
{
    public class IdContainerRepository : Repository
    {
        public IdContainer TrazerIdContainerAmanha()
        {
            using (var s = context.DocumentStore.OpenSession())
                return s.Query<IdContainer>().FirstOrDefault(ic => ic.Data == DateTime.Now.Date.AddDays(1));

        }

        public IdContainer TrazerIdContainerHoje()
        {
            using (var s = context.DocumentStore.OpenSession())
                return s.Query<IdContainer>().FirstOrDefault(ic => ic.Data == DateTime.Now.Date);

        }


        public IdContainer TrazerPorId(string id)
        {
            using (var s = context.DocumentStore.OpenSession())
                return s.Load<IdContainer>(id);

        }

        public void SalvaEnvioDeNotificao(string idBet, string desc, int numero)
        {

            var idContainerHoje = TrazerIdContainerHoje();
            idContainerHoje.Notificacoes.Add(new NotificacaoJogo(idBet, desc, numero, true));
            Salvar(idContainerHoje);
        }

        public bool NotificacaoJaEnviada(string idBet, string desc, int numero)
        {
            var idContainerHoje = TrazerIdContainerHoje();
            return idContainerHoje.Notificacoes.Exists(n => n.IdBet == idBet && n.Notificacao == desc && n.Numero == numero);
        }

        public void IgnoraIdJogo(string idBet)
        {
            using (var s = context.DocumentStore.OpenSession())
            {
                var container = s.Query<IdContainer>().ToList()
                                .FirstOrDefault(ic => ic.Ids.Exists(i => i.Id == idBet));

                if (container == null) return;
                var idJogo = container.Ids.FirstOrDefault(i => i.Id == idBet);
                if (idJogo == null) return;
                container.Ids.FirstOrDefault(i => i.Id == idBet).Ignorar = true;
                s.Store(container);
                s.SaveChanges();
            }
        }
    }
}
