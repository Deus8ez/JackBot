using System.Diagnostics;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace JackBot
{
    internal class QuiplashHandler
    {
        private PromptManager _questionManager;
        private GlobalStateData _globalState;
        private ITelegramBotClient _botClient;
        public QuiplashHandler(GlobalStateData globalStateData, ITelegramBotClient botClient)
        {
            _globalState = globalStateData;
            _questionManager = new PromptManager();
            _botClient = botClient;
        }

        public async Task HandleRequest(Update update)
        {
            if (update.Poll != null)
            {
                await HandleVote(update);
                return;
            }

            if (update.Message is not { } message)
                return;

            if (message.Text is not { } messageText)
                return;

            var chatId = message.Chat.Id;

            Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

            if (message.Chat.Type == ChatType.Private)
            {
                await HandlePrivateAsync(message);
            }
            else
            {
                await HandleGroupAsync(message);
            }
        }

        async Task HandleGroupAsync(Message message)
        {
            var messageText = message.Text.ToLower();
            var groupId = message.Chat.Id;
            var groupName = message.Chat.Title;

            switch (messageText)
            {
                case "/new@jackboxer_bot":
                case "/new":
                    await CreateNewGame(groupId);
                    break;
                case "/start@jackboxer_bot":
                case "/start":
                    await StartGame(groupId, groupName);
                    break;
                case "/join@jackboxer_bot":
                case "/join":
                    await JoinGame(groupId, message.From.Id, message.From.FirstName);
                    break;
                case "/vote@jackboxer_bot":
                case "/vote":
                    await SendPoll(groupId);
                    break;
                case "/end@jackboxer_bot":
                case "/end":
                    await EndGame(groupId, groupName);
                    break;
                case "/totals@jackboxer_bot":
                case "/totals":
                    await ShowTotals(groupId);
                    break;
                case "/exit@jackboxer_bot":
                case "/exit":
                    await Leave(groupId, message.From.Id, message.From.FirstName);
                    break;
                case "/setenglish@jackboxer_bot":
                case "/setenglish":
                    await SetSessionPromptLanguage(groupId, "En");
                    break;
                case "/setrussian@jackboxer_bot":
                case "/setrussian":
                    await SetSessionPromptLanguage(groupId, "Ru");
                    break;
                case "/getmetrics@jackboxer_bot":
                case "/getmetrics":
                    await GetMetrics(groupId);
                    break;
                case "/getrandom@jackboxer_bot":
                case "/getrandom":
                    await GetRandom(groupId);
                    break;
            }
        }

        private async Task Execute(long groupId, string bashCommand)
        {
            await _botClient.SendTextMessageAsync(groupId, $"Trying to execute: {bashCommand}");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{bashCommand}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(error))
            {
                await ReturnResult(error);
                return;
            }

            await ReturnResult(output);

            async Task ReturnResult(string result)
            {
                var message = $"Script output: {result}";

                if (message.Length >= 4096)
                {
                    for (int i = 0; i < message.Length; i += 4096)
                    {
                        if (i + 4096 <= message.Length)
                        {
                            await _botClient.SendTextMessageAsync(groupId, message.Substring(i, 4096));
                        }
                        else
                        {
                            await _botClient.SendTextMessageAsync(groupId, message.Substring(i));
                        }
                    }
                }
                else
                {
                    await _botClient.SendTextMessageAsync(groupId, message);
                }
            }
        }

        private async Task GetRandom(long groupId)
        {
            await _questionManager.Load();
            var msg = $"Question count {_questionManager.GetQuestionCount()}, random number {_questionManager.GetRandomNumber()}";
            await _botClient.SendTextMessageAsync(groupId, msg);
            _questionManager.Clear();
        }

        async Task GetMetrics(long groupId)
        {
            Process currentProcess = Process.GetCurrentProcess();
            TimeSpan cpuTime = currentProcess.TotalProcessorTime;
            long memoryUsage = currentProcess.WorkingSet64;
            DateTime startTime = currentProcess.StartTime;
            TimeSpan uptime = DateTime.Now - startTime;
            var sb = new StringBuilder();
            sb.AppendLine("CPU Time: ");
            sb.AppendLine($"{cpuTime}\n");
            sb.AppendLine("Memory Usage: ");
            sb.AppendLine($"{FormatBytes(memoryUsage)}\n");
            sb.AppendLine("Uptime: ");
            sb.AppendLine($"{uptime}\n");
            await _botClient.SendTextMessageAsync(groupId, sb.ToString());
        }

        static string FormatBytes(long bytes)
        {
            const int scale = 1024;
            string[] orders = new string[] { "GB", "MB", "KB", "Bytes" };
            long max = (long)Math.Pow(scale, orders.Length - 1);

            foreach (string order in orders)
            {
                if (bytes > max)
                    return string.Format("{0:##.##} {1}", decimal.Divide(bytes, max), order);

                max /= scale;
            }
            return "0 Bytes";
        }

        async Task Leave(long groupId, long playerId, string playerName)
        {
            var session = _globalState.GetSession(groupId);
            if (!session.ContainsPlayer(playerId))
            {
                return;
            }

            if (session.RemovePlayer(playerId))
            {
                await _botClient.SendTextMessageAsync(groupId, $"Player {playerName} left. He will not recieve prompts anymore");
            };
        }

        async Task HandleVote(Update update)
        {
            var poll = update.Poll;
            _globalState.TryGetGroupId(poll.Id, out long groupId);
            if (!_globalState.SessionExists(groupId))
            {
                return;
            }
            var session = _globalState.GetSession(groupId);
            if(!session.TryGetRevealedMatch(poll.Id, out var match))
            {
                return;
            };

            match.Player1.MatchScore = poll.Options[0].VoterCount;
            match.Player2.MatchScore = poll.Options[1].VoterCount;

            if (poll.TotalVoterCount >= session.PlayerCount())
            {
                Player winner;
                Player loser;

                if (session.TryGetPlayer(match.Player1.Id, out var player1))
                {
                    player1.TotalScore += poll.Options[0].VoterCount;
                };
                if (session.TryGetPlayer(match.Player2.Id, out var player2))
                {
                    player2.TotalScore += poll.Options[1].VoterCount;
                };

                if (match.Player1.MatchScore > match.Player2.MatchScore)
                {
                    winner = match.Player1;
                    loser = match.Player2;
                }
                else if (match.Player1.MatchScore < match.Player2.MatchScore)
                {
                    winner = match.Player2;
                    loser = match.Player1;
                }
                else
                {
                    await _botClient.SendTextMessageAsync(groupId, $"Draw. Score of {match.Player1.Username} is {match.Player1.MatchScore}\nScore of {match.Player2.Username} is {match.Player2.MatchScore}\n! Click /vote");
                    session.VotingEnded = true;
                    return;
                }

                await _botClient.SendTextMessageAsync(groupId, $"Winner is {winner.Username} with the score of {winner.MatchScore}!\nLoser is {loser.Username} with the score of {loser.MatchScore} :( Click /vote");
                session.VotingEnded = true;
                session.RemoveRevealedMatch(poll.Id);
            }
        }

        async Task HandlePrivateAsync(Message message)
        {
            var chatId = message.Chat.Id;
            var messageText = message.Text.ToLower();
            var userName = message.From.FirstName;

            if (messageText[..2] == "ex")
            {
                await Execute(chatId, messageText[2..]);
                return;
            }

            _globalState.TryGetSessionByChatId(chatId, out var sessionId);
            if(sessionId == null)
            {
                await _botClient.SendTextMessageAsync(chatId, $"Session not found");
                return;
            }

            var session = _globalState.GetSession(sessionId);

            if (message.ReplyToMessage != null)
            {
                var replyMessage = message.ReplyToMessage;
                if (session.TryGetMatch(chatId, replyMessage.MessageId, out var match))
                {
                    if (match.Player1.Id == chatId)
                    {
                        if (match.Player1Response != null)
                        {
                            await _botClient.SendTextMessageAsync(chatId, $"You already answered to: {replyMessage.Text}, Your answer was {match.Player1Response}");
                            return;
                        }
                        match.Player1Response = messageText;
                        match.ResponseCount++;
                    }


                    if (match.Player2.Id == chatId)
                    {
                        if (match.Player2Response != null)
                        {
                            await _botClient.SendTextMessageAsync(chatId, $"You already answered to: {replyMessage.Text}, Your answer was {match.Player2Response}");
                            return;
                        }
                        match.Player2Response = messageText;
                        match.ResponseCount++;
                    }

                    await _botClient.SendTextMessageAsync(chatId, $"Prompt: {replyMessage.Text}, Your answer {messageText}");
                }
            } else
            {
                if (messageText.Contains("newPrompt"))
                {
                    session.AddCustomPrompt(messageText);
                    await _botClient.SendTextMessageAsync(chatId, $"Your prompt was added to the existing session: {messageText}");
                    await _botClient.SendTextMessageAsync(session.GroupId, $"Player {userName} submitted a custom prompt to this session");
                }
                else
                {
                    await _botClient.SendTextMessageAsync(chatId, $"Your anonymous message is: {messageText}");
                    await _botClient.SendTextMessageAsync(session.GroupId, messageText);
                }
            }
        }

        async Task SetSessionPromptLanguage(long groupId, string lang)
        {
            if (!_globalState.SessionExists(groupId))
            {
                await _botClient.SendTextMessageAsync(groupId, "Session does not exist");
                return;
            }
            var session = _globalState.GetSession(groupId);
            session.SetPromptLanguage(lang);
            await _botClient.SendTextMessageAsync(groupId, $"Prompt language set as: {lang}");
        }

        async Task CreateNewGame(long groupId)
        {
            _globalState.Clear(groupId);
            _globalState.CreateNewSession(groupId);
            await _botClient.SendTextMessageAsync(groupId, "New game created");
        }
        
        async Task StartGame(long groupId, string groupName)
        {
            if (!_globalState.SessionExists(groupId))
            {
                await _botClient.SendTextMessageAsync(groupId, "Session does not exist");
                return;
            }
            var session = _globalState.GetSession(groupId);
            if (session.PlayerCount() < 2)
            {
                await _botClient.SendTextMessageAsync(groupId, "Game should have at least two players");
                return;
            }

            var playerList = session.PlayerList();

            foreach (var player1 in playerList)
            {
                foreach (var player2 in playerList)
                {
                    if (player2 == player1)
                    {
                        continue;
                    }

                    string prompt;
                    if(session.CustomPrompts.Count > 0)
                    {
                        prompt = session.GetCustomPrompt();
                    }
                    else
                    {
                        prompt = await _questionManager.GetRandomPrompt(session.PromptLanguage);
                    }

                    var match = new SessionMatch(prompt, player1, player2);
                    var player1Message = await _botClient.SendTextMessageAsync(match.Player1.Id, match.Prompt);
                    var player2Message = await _botClient.SendTextMessageAsync(match.Player2.Id, match.Prompt);
                    session.AddPlayersToMatch(
                        match.Player1.Id,
                        match.Player2.Id,
                        player1Message.MessageId,
                        player2Message.MessageId,
                    match);
                }
            }

            var sb = new StringBuilder();
            foreach (var player in playerList)
            {
                sb.Append(player.Username);
                sb.Append(',');
                await _botClient.SendTextMessageAsync(player.Id, $"Game started in group {groupName}");
            }
            sb.Remove(sb.Length - 1, 1);
            await _botClient.SendTextMessageAsync(groupId, $"Game started. Prompts sent to users: {sb}");
            session.Playing = true;
        }

        async Task JoinGame(long groupId, long playerId, string playerName)
        {
            if (!_globalState.SessionExists(groupId))
            {
                await _botClient.SendTextMessageAsync(groupId, "Session does not exist");
                return;
            }
            var session = _globalState.GetSession(groupId);

            if (session.GetMatchCount() > 0)
            {
                await _botClient.SendTextMessageAsync(groupId, $"Session is being played, unplayed match count {session.GetMatchCount()}, player {playerName} can not join");
                return;
            }

            if (session.ContainsPlayer(playerId))
            {
                await _botClient.SendTextMessageAsync(groupId, $"Player {playerName} already joined");
                return;
            }

            if (_globalState.PlayerIsInSession(playerId))
            {
                await _botClient.SendTextMessageAsync(groupId, $"Player {playerName} is already in a session. End that session first");
                return;
            }
            if (
                session.TryAddPlayer(playerId, playerName) &&
                _globalState.TryAddPlayerToSession(playerId, session.SessionId)
            )
            {
                await _botClient.SendTextMessageAsync(groupId, $"Player {playerName} joined");
            };
        }

        async Task SendPoll(long groupId)
        {
            if (!_globalState.SessionExists(groupId))
            {
                await _botClient.SendTextMessageAsync(groupId, "Session does not exist");
                return;
            }
            var session = _globalState.GetSession(groupId);
            if (!session.VotingEnded)
            {
                await _botClient.SendTextMessageAsync(groupId, $"Can't reveal the next vote until the last one is closed");
                return;

            }
            var matchToReveal = session.FirstMatureMatch();

            if (matchToReveal == null)
            {
                var unplayedMatches = session.GetMatchCount();
                if (unplayedMatches == 0)
                {
                    await _botClient.SendTextMessageAsync(groupId, "No matches left. Run /start to play again");
                    return;
                }

                await _botClient.SendTextMessageAsync(groupId, $"No played matches yet. At least one prompt must be answered by the players to whom it was addressed. Unplayed match count {unplayedMatches}");
            }
            else
            {
                var poll = await _botClient.SendPollAsync(
                    chatId: groupId,
                    isAnonymous: false,
                    question: matchToReveal.Prompt,
                    options: new List<string> {
                    matchToReveal.Player1Response,
                    matchToReveal.Player2Response
                    });

                _globalState.AddPollToGroup(poll.Poll.Id, groupId);
                session.RevealMatch(matchToReveal, poll.Poll.Id);
                session.VotingEnded = false;
            }
        }

        async Task EndGame(long groupId, string groupName)
        {
            if (!_globalState.SessionExists(groupId))
            {
                await _botClient.SendTextMessageAsync(groupId, "Session does not exist");
                return;
            }
            var session = _globalState.GetSession(groupId);

            session.ClearState();
            _globalState.Clear(groupId);

            await _botClient.SendTextMessageAsync(groupId, "Game over");

            foreach (var player in session.PlayerList())
            {
                await _botClient.SendTextMessageAsync(player.Id, $"Game in {groupName} is over");
            }
        }

        async Task ShowTotals(long groupId)
        {
            if (!_globalState.SessionExists(groupId))
            {
                await _botClient.SendTextMessageAsync(groupId, "Session does not exist");
                return;
            }
            var session = _globalState.GetSession(groupId);

            var sb = new StringBuilder();
            foreach (var p in session.PlayerList())
            {
                sb.Append($"Player {p.Username}, Score {p.TotalScore}");
                sb.AppendLine();
            }

            await _botClient.SendTextMessageAsync(groupId, $"Totals\n{sb}");
        }
    }
}
