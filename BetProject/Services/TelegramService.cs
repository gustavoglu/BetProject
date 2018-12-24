using Telegram.Bot;

namespace BetProject.Services
{
    public class TelegramService
    {
        TelegramBotClient botClient;
        TelegramBotClient botClientPre;
        public string IdGrupo = "-378660997";
        public string IdGrupoPre = "-291034766";
        public TelegramService()
        {
            botClient = new TelegramBotClient("761193229:AAF5EfahOOEOZC7K539gXAUUVDmgqCFLZMQ");
            botClientPre = new TelegramBotClient("669898874:AAFYxJgvd5o0usoJ4sIs2lTAbKYZgr8xbZA");
        }

        public void EnviaMensagemParaOGrupo(string msg, bool pre = false)
        {
            if (pre)
                botClientPre.SendTextMessageAsync(IdGrupoPre, msg);
            else
                botClient.SendTextMessageAsync(IdGrupo, msg);
        }
    }
}
