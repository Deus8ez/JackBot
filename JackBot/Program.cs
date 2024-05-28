// See https://aka.ms/new-console-template for more information
using JackBot;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using System.Text.Json;
using File = System.IO.File;

var tokenInParam = args.FirstOrDefault();
string key = "";
if(tokenInParam != null)
{
    Console.WriteLine("Taking token from params");
    key = tokenInParam;
} else
{
    string jsonString = File.ReadAllText("appSettings.json");
    key = JsonDocument.Parse(jsonString).RootElement.GetProperty("key").GetString();
}

if(key == null || key.Length == 0)
{
    throw new Exception("Token not provided");
}

var botClient = new TelegramBotClient(key);
var me = await botClient.GetMeAsync();

using CancellationTokenSource cts = new();
ReceiverOptions receiverOptions = new()
{
    ThrowPendingUpdates = true,
    AllowedUpdates = Array.Empty<UpdateType>() // receive all update types except ChatMember related updates
};

var globalState = new GlobalStateData();
var quiplashHandler = new QuiplashHandler(globalState, botClient);
botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

Console.WriteLine($"Start listening for @{me.Username}");
while (true){}

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    try
    {
        await quiplashHandler.HandleRequest(update);
    }
    catch (Exception e)
    {
        if (update.Poll == null)
        {
            await botClient.SendTextMessageAsync(update.Message.Chat.Id, $"Exception occured: {e.Message}");
        }
    }
}

Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}