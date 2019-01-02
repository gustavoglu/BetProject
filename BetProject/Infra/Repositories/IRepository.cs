using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BetProject.Infra.Repositories
{
    public interface IRepository<T>
    {
        void CriarOuAtualizar(T obj);
    }
}
