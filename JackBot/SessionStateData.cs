namespace JackBot
{
    internal class SessionStateData
    {
        internal Dictionary<Guid, List<long>> MatchIdToChats = new();
        internal Dictionary<long, SessionMatch> ChatIdToMatches = new();
        internal Dictionary<string, SessionMatch> RevealedMatches = new();
        internal Dictionary<long, Player> Players = new();
        public void Clear()
        {
            Players.Clear();
            MatchIdToChats.Clear();
            ChatIdToMatches.Clear();
            RevealedMatches.Clear();
        }
    }
}
