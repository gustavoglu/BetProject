namespace BetProject.ObjectValues
{
    public class NotificacaoJogo
    {
        public NotificacaoJogo(string idBet, string notificacao,int numero, bool enviada)
        {
            IdBet = idBet;
            Notificacao = notificacao;
            Enviada = enviada;
            Numero = numero;
        }

        public string IdBet { get; set; }
        public string Notificacao { get; set; }
        public int Numero { get; set; }
        public bool Enviada { get; set; } = false;
    }
}
