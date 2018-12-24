using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace BetProject.Infra
{
    public class DbContextRaven
    {
        private IDocumentStore _documentStore;
        private IDocumentSession _session;

        public IDocumentStore DocumentStore { get { return this._documentStore; } }
        public IDocumentSession Session { get { return this._session; } }

        public DbContextRaven()
        {
            _documentStore = new DocumentStore
            {
                Urls = new string[] { "http://ec2-52-15-109-86.us-east-2.compute.amazonaws.com:8083" },
                Database = "bp_db"
            };
            _documentStore.Initialize();
            _session = _documentStore.OpenSession();
            _session.Advanced.WaitForIndexesAfterSaveChanges();
        }
    }
}
