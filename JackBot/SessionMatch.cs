namespace JackBot
{
    internal class SessionMatch
    {
        public Guid Guid { get; set; } = Guid.NewGuid();
        public readonly string Prompt;
        public Player Player1;
        public string Player1Response;
        public Player Player2;
        public string Player2Response;
        public DateTime VoteTime;

        public SessionMatch(string prompt, Player player1, Player player2)
        {
            Prompt = prompt;
            Player1 = player1;
            Player2 = player2;
        }

        public bool IsExpired()
        {
            if ((DateTime.Now - VoteTime).Days > 1)
            {
                return true;
            }
            return false;
        }

        public int ResponseCount { get; set; }
        public bool HasPlayers()
        {
            if(Player1 != null && Player2 != null)
            {
                return true;
            }

            return false;
        }
    }
}
