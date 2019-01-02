using Raven.Client.Documents;
using System.Collections.Generic;
using System.Linq;

namespace BetProject.Infra.Repositories
{
    public class Repository
    {
        protected readonly DbContextRaven context;
        protected IDocumentStore DocumentStore { get { return context.DocumentStore; } }
        public Repository()
        {
            context = new DbContextRaven();
        }

        public void Salvar(object obj)
        {
            using (var s = context.DocumentStore.OpenSession())
            {
                s.Store(obj);
                s.SaveChanges();
            }
        }

        public List<T> TrazerTodos<T>()
        {
            using (var s = context.DocumentStore.OpenSession())
            {
                return s.Query<T>().ToList();
            }
        }
    }
}
