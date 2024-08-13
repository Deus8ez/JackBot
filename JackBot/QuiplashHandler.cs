using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
                case "/new@darkhan_test_bot":
                case "/new":
                    await CreateNewGame(groupId);
                    break;
                case "/start@darkhan_test_bot":
                case "/start":
                    await StartGame(groupId, groupName);
                    break;
                case "/join@darkhan_test_bot":
                case "/join":
                    await JoinGame(groupId, message.From.Id, message.From.FirstName);
                    break;
                case "/vote@darkhan_test_bot":
                case "/vote":
                    await SendPoll(groupId);
                    break;
                case "/end@darkhan_test_bot":
                case "/end":
                    await EndGame(groupId, groupName);
                    break;
                case "/sessiontotals@darkhan_test_bot":
                case "/sessiontotals":
                    await ShowSessionTotals(groupId);
                    break;
                case "/overalltotals@darkhan_test_bot":
                case "/overalltotals":
                    await ShowOverallTotals(groupId);
                    break;
                case "/exit@darkhan_test_bot":
                case "/exit":
                    await Leave(groupId, message.From.Id, message.From.FirstName);
                    break;
                case "/setenglish@darkhan_test_bot":
                case "/setenglish":
                    await SetSessionPromptLanguage(groupId, "En");
                    break;
                case "/setrussian@darkhan_test_bot":
                case "/setrussian":
                    await SetSessionPromptLanguage(groupId, "Ru");
                    break;
                case "/getmetrics@darkhan_test_bot":
                case "/getmetrics":
                    await GetMetrics(groupId);
                    break;
                case "/getrandom@darkhan_test_bot":
                case "/getrandom":
                    await GetRandom(groupId);
                    break;
                case "/resetoveralltotals@darkhan_test_bot":
                case "/resetoveralltotals":
                    await ResetOverallTotals(groupId);
                    break;
                case "/getstatus@darkhan_test_bot":
                case "/getstatus":
                    await _botClient.SendTextMessageAsync(groupId, $"Prompts in queue: {_globalState.AsyncMatches.Count}");
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

        private async Task ResetOverallTotals(long groupId)
        {
            _globalState.ResetTotals();
            await _botClient.SendTextMessageAsync(groupId, "Stats reset");
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

            //fix
            if (_globalState.PollIdToMatch.TryGetValue(poll.Id, out var asyncMatch))
            {
                if (asyncMatch.IsExpired())
                {
                    await _botClient.SendTextMessageAsync(groupId, $"This match expired");
                    return;
                }
                else
                {
                    asyncMatch.Player1.MatchScore = poll.Options[0].VoterCount;
                    asyncMatch.Player2.MatchScore = poll.Options[1].VoterCount;
                    _globalState.UpdateStaticTotals(asyncMatch.Player1.Id, poll.Options[0].VoterCount);
                    _globalState.UpdateStaticTotals(asyncMatch.Player2.Id, poll.Options[1].VoterCount);
                }
            }

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

                _globalState.UpdateStaticTotals(match.Player1.Id, poll.Options[0].VoterCount);
                _globalState.UpdateStaticTotals(match.Player2.Id, poll.Options[1].VoterCount);

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
                    session.RemoveRevealedMatch(poll.Id);
                    return;
                }

                await _botClient.SendTextMessageAsync(groupId, $"Winner is {winner.Username} with the score of {winner.MatchScore}!\nLoser is {loser.Username} with the score of {loser.MatchScore} :( Click /vote");
                session.VotingEnded = true;
                session.RemoveRevealedMatch(poll.Id);
            }
        }

        async Task HandleGetPrompt(long chatId)
        {
            string prompt = "";
            foreach (var match in _globalState.AsyncMatches)
            {
                if (match.Item2.ResponseCount < 2 && match.Item2.Player1.Id != chatId)
                {
                    prompt = match.Item1;
                    break;
                }
            }

            if (prompt.Length == 0)
            {
                prompt = await _questionManager.GetRandomPrompt();
            }

            await _botClient.SendTextMessageAsync(chatId, prompt);
        }

        async Task HandleAnswerPrompt(long chatId, long userId, string userName, string prompt, string response, Message replyMessage)
        {
            SessionMatch match = new SessionMatch(prompt, null, null);
            if (_globalState.PromptToMatches.ContainsKey(prompt))
            {
                if (_globalState.PromptToMatches.TryGetValue(prompt, out var matches))
                {
                    if (matches.Count == 0)
                    {
                        matches = new Dictionary<string, SessionMatch>();
                        matches.Add(match.Guid.ToString(), match);
                        _globalState.PromptToMatches[prompt] = matches;
                    }
                    else
                    {
                        match = matches.First().Value;
                    }

                } 
            } else
            {
                var newDict = new Dictionary<string, SessionMatch>();
                newDict.Add(match.Guid.ToString(), match);
                _globalState.PromptToMatches.Add(prompt, newDict);
            }

            if (match.Player1 == null)
            {
                match.Player1 = new Player(userId, userName);
                match.Player1Response = response;
                match.ResponseCount++;
                _globalState.AsyncMatches.Enqueue((prompt, match));
                await _botClient.SendTextMessageAsync(chatId, $"(Async) Prompt: {prompt}, Your answer {response}");
                return;
            }

            if (match.Player2 == null)
            {
                match.Player2 = new Player(userId, userName);
                match.Player2Response = response;
                match.ResponseCount++;
                await _botClient.SendTextMessageAsync(chatId, $"(Async) Prompt: {prompt}, Your answer {response}");
                return;
            }

            _globalState.TryGetSessionByChatId(chatId, out var sessionId);
            if (sessionId == null)
            {
                await _botClient.SendTextMessageAsync(chatId, $"Session not found");
                return;
            }

            var session = _globalState.GetSession(sessionId);
            if (session.TryGetMatch(chatId, replyMessage.MessageId, out var sessionMatch))
            {
                if (sessionMatch.Player1.Id == chatId)
                {
                    if (sessionMatch.Player1Response != null)
                    {
                        await _botClient.SendTextMessageAsync(chatId, $"You already answered to: {replyMessage.Text}, Your answer was {sessionMatch.Player1Response}");
                        return;
                    }
                    sessionMatch.Player1Response = response;
                    sessionMatch.ResponseCount++;
                }


                if (sessionMatch.Player2.Id == chatId)
                {
                    if (sessionMatch.Player2Response != null)
                    {
                        await _botClient.SendTextMessageAsync(chatId, $"You already answered to: {replyMessage.Text}, Your answer was {sessionMatch.Player2Response}");
                        return;
                    }
                    sessionMatch.Player2Response = response;
                    sessionMatch.ResponseCount++;
                }

                await _botClient.SendTextMessageAsync(chatId, $"Prompt: {replyMessage.Text}, Your answer {response}");
            }
        }

        async Task HandlePrivateAsync(Message message)
        {
            var chatId = message.Chat.Id;
            var messageText = message.Text.ToLower();
            var userName = message.From.FirstName;
            var userId = message.From.Id;

            if (messageText[..2] == "ex")
            {
                await Execute(chatId, messageText[2..]);
                return;
            }

            switch (messageText)
            {
                case "/getprompt@darkhan_test_bot":
                case "/getprompt":
                    await HandleGetPrompt(chatId);
                    break;
            }

            if (message.ReplyToMessage != null)
            {
                var replyMessage = message.ReplyToMessage;

                await HandleAnswerPrompt(chatId,userId,userName,replyMessage.Text,messageText, replyMessage);

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
            if (_globalState.TryRegisterPlayer(playerId, playerName))
            {
                await _botClient.SendTextMessageAsync(groupId, $"Player {playerName} has been registered");
            }

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
            if (_globalState.AsyncMatches.TryPeek(out var firstAsyncPoll))
            {
                var match = firstAsyncPoll.Item2;

                if (match.ResponseCount >= 2)
                {
                    match.VoteTime = DateTime.Now;
                    _globalState.AsyncMatches.Dequeue();
                    var poll = await _botClient.SendPollAsync(
                        chatId: groupId,
                        isAnonymous: false,
                        question: match.Prompt,
                        options: new List<string> {
                            match.Player1Response,
                            match.Player2Response
                        });

                    if (_globalState.PromptToMatches.ContainsKey(match.Prompt))
                    {
                        _globalState.PromptToMatches[match.Prompt].Remove(match.Guid.ToString());
                        if (_globalState.PromptToMatches[match.Prompt].Count == 0)
                        {
                            _globalState.PromptToMatches.Remove(match.Prompt);
                        }
                    }

                    _globalState.AddPollToMatchId(poll.Poll.Id, match);
                    _globalState.AddPollToGroup(poll.Poll.Id, groupId);
                    await _botClient.SendTextMessageAsync(groupId, "Async vote sent");
                    return;
                }
            }

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

        async Task ShowSessionTotals(long groupId)
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

            await _botClient.SendTextMessageAsync(groupId, $"Session totals\n{sb}");
        }

        async Task ShowOverallTotals(long groupId)
        {
            var sb = new StringBuilder();
            foreach (var p in _globalState.StaticTotals)
            {
                sb.Append($"Player {p.Key}, Score {p.Value}");
                sb.AppendLine();
            }

            await _botClient.SendTextMessageAsync(groupId, $"Overall totals\n{sb}");
        }
    }
}
