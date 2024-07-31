namespace JackBot
{
    internal class GlobalStateData
    {
        private Dictionary<long, string> _groupIdToPollId = new();
        private Dictionary<string, long> _pollIdToGroupId = new();

        private Dictionary<long, string> _groupIdToGuid = new();
        private Dictionary<string, long> _guidToGroupId = new();

        private Dictionary<long, string> _chatIdToGuid = new();
        private Dictionary<string, List<long>> _guidToChatIds = new();

        private Dictionary<string, Session> _sessions = new();
        private Dictionary<long, string> _playerIdToUsername = new();
        public Dictionary<string, long> StaticTotals = new();

        public bool TryRegisterPlayer(long playerId, string userName)
        {
            if (!_playerIdToUsername.ContainsKey(playerId))
            {
                _playerIdToUsername.Add(playerId, userName);
                return true;
            }

            return false;
        }

        public void UpdateStaticTotals(long userId, long score)
        {
            if (_playerIdToUsername.ContainsKey(userId))
            {
                var id = _playerIdToUsername[userId];
                if (StaticTotals.ContainsKey(id))
                {
                    StaticTotals[id] += score;
                }
                else
                {
                    StaticTotals.Add(id, score);
                }
            }
            else
            {
                throw new Exception("Username was not found!");
            }
        }

        public bool PlayerIsInSession(long playerId)
        {
            return _chatIdToGuid.ContainsKey(playerId);
        }

        public bool TryAddPlayerToSession(long playerId, string sessionGuid)
        {

            var added = _chatIdToGuid.TryAdd(playerId, sessionGuid);
            if (added)
            {
                if (_guidToChatIds.ContainsKey(sessionGuid))
                {
                    _guidToChatIds[sessionGuid].Add(playerId);
                }
                else
                {
                    _guidToChatIds.Add(sessionGuid, new List<long>{ playerId });
                }
            }
            return added;
        }

        public bool TryGetSessionByChatId(long chatId, out string sessionGuid)
        {
            if (_chatIdToGuid.ContainsKey(chatId))
            {
                _chatIdToGuid.TryGetValue(chatId, out sessionGuid);
                return true;
            }

            sessionGuid = default(string);
            return false;
        }

        public bool TryGetGroupId(string pollId, out long groupId)
        {
            return _pollIdToGroupId.TryGetValue(pollId, out groupId);
        }

        public Session GetSession(long groupId)
        {
            return _sessions[_groupIdToGuid[groupId]];
        }

        public Session GetSession(string sessionId)
        {
            return _sessions[sessionId];
        }

        public bool SessionExists(long groupId)
        {
            return _groupIdToGuid.ContainsKey(groupId);
        }

        public void CreateNewSession(long groupId)
        {
            var sessionId = Guid.NewGuid().ToString();
            var session = new Session(groupId, sessionId);
            _groupIdToGuid.Add(groupId, sessionId);
            _sessions.Add(sessionId, session);
        }

        public void Clear(long groupId)
        {
            if (_groupIdToPollId.ContainsKey(groupId))
            {
                var pollId = _groupIdToPollId[groupId];
                _pollIdToGroupId.Remove(pollId);
                _groupIdToPollId.Remove(groupId);
            }

            if (_groupIdToGuid.ContainsKey(groupId))
            {
                var guid = _groupIdToGuid[groupId];
                _guidToGroupId.Remove(guid);
                _groupIdToGuid.Remove(groupId);
                _sessions.Remove(guid);

                if (_guidToChatIds.ContainsKey(guid))
                {
                    foreach (var playerId in _guidToChatIds[guid])
                    {
                        _chatIdToGuid.Remove(playerId);
                    }
                    _guidToChatIds.Remove(guid);
                }
            }
        }

        public void AddPollToGroup(string pollId, long chatId)
        {
            _pollIdToGroupId.Add(pollId, chatId);
        }
    }
}
