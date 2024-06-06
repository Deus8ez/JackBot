namespace JackBot
{
    internal class Session
    {
        public string PromptLanguage = "Ru";
        public string SessionId;
        public readonly long GroupId;
        private readonly SessionStateData _stateData;
        public bool Playing;
        public bool VotingEnded;
        private Random _random;
        public Stack<string> CustomPrompts;
        public Session(long groupId, string sessionId)
        {
            CustomPrompts = new Stack<string>();
            _stateData = new SessionStateData();
            GroupId = groupId;
            _random = new Random();
            SessionId = sessionId;
            VotingEnded = true;
            Playing = false;
        }

        public void AddCustomPrompt(string prompt)
        {
            CustomPrompts.Push(prompt);
        }

        public string GetCustomPrompt()
        {
            return CustomPrompts.Pop();
        }

        public void SetPromptLanguage(string lang)
        {
            PromptLanguage = lang;
        }

        public Player GetAnotherRandomPlayer(long currentPlayerId)
        {
            var currentPlayer = _stateData.Players[currentPlayerId];
            _stateData.Players.Remove(currentPlayerId);
            var i = _random.Next(0, _stateData.Players.Count);
            var res = _stateData.Players.Values.ElementAt(i);
            _stateData.Players.Add(currentPlayerId, currentPlayer);
            return res;
        }

        public bool TryGetPlayer(long playerId, out Player value)
        {
            if (_stateData.Players.TryGetValue(playerId, out var player))
            {
                value = player;
                return true;
            }

            value = default(Player);
            return false;
        }

        public bool TryAddPlayer(long playerId, string userName)
        {
            return _stateData.Players.TryAdd(playerId, new Player(playerId, userName));
        }

        public int GetMatchCount()
        {
            return _stateData.ChatIdToMatches.Where(e => e.Value.ResponseCount < 2).GroupBy(e => e.Value.Guid).Count();
        }

        public void RevealMatch(SessionMatch match, string pollId)
        {
            var chatIds = _stateData.MatchIdToChats[match.Guid];
            foreach (var id in chatIds)
            {
                _stateData.ChatIdToMatches.Remove(id);
            }
            _stateData.MatchIdToChats.Remove(match.Guid);
            _stateData.RevealedMatches.Add(pollId, match);
        }

        public bool RemovePlayer(long playerId)
        {
            if (_stateData.Players.ContainsKey(playerId))
            {
                _stateData.Players.Remove(playerId);
                return true;
            }

            return false;
        }

        public List<Player> PlayerList()
        {
            return _stateData.Players.Select(e => e.Value).ToList();
        }

        public void AddPlayersToMatch(long player1ChatId, long player2ChatId, int player1MessageId, int player2MessageId, SessionMatch match)
        {
            var player1AndMsgId = player1ChatId + player1MessageId;
            var player2AndMsgId = player2ChatId + player2MessageId;
            _stateData.ChatIdToMatches.TryAdd(player1AndMsgId, match);
            _stateData.ChatIdToMatches.TryAdd(player2AndMsgId, match);
            _stateData.MatchIdToChats.Add(match.Guid, new List<long> { player1AndMsgId, player2AndMsgId });
        }

        public int PlayerCount()
        {
            return _stateData.Players.Count();
        }

        public bool ContainsPlayer(long playerId)
        {
            return _stateData.Players.ContainsKey(playerId);
        }

        public bool TryGetMatch(long chatId, int messageId, out SessionMatch value)
        {
            if (_stateData.ChatIdToMatches.ContainsKey(chatId + messageId))
            {
                value = _stateData.ChatIdToMatches[chatId + messageId];
                return true;
            }

            value = default(SessionMatch);
            return false;
        }

        public bool TryGetRevealedMatch(string pollId, out SessionMatch value)
        {
            if (_stateData.RevealedMatches.TryGetValue(pollId, out var match))
            {
                value = match;
                return true;
            }

            value = default(SessionMatch);
            return false;
        }

        public SessionMatch FirstMatureMatch()
        {
            return _stateData.ChatIdToMatches.FirstOrDefault(e => e.Value.ResponseCount == 2).Value;
        }

        public void RemoveRevealedMatch(string pollId)
        {
            if (_stateData.RevealedMatches.ContainsKey(pollId))
            {
                _stateData.RevealedMatches.Remove(pollId);
            }
        }

        public void ClearState()
        {
            Playing = false;
            _stateData.Clear();
        }
    }
}
