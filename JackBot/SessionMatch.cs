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
        public long GroupId;

        public SessionMatch(string prompt, Player player1, Player player2)
        {
            Prompt = prompt;
            Player1 = player1;
            Player2 = player2;
        }

        public bool IsExpired(int minutes)
        {
            if ((DateTime.Now - VoteTime).Minutes > minutes)
            {
                return true;
            }
            return false;
        }

        public int ResponseCount { get; set; }
    }
}
