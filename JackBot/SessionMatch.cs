namespace JackBot
{
    internal class SessionMatch
    {
        public Guid Guid { get; set; } = Guid.NewGuid();
        public readonly string Prompt;
        public readonly Player Player1;
        public string Player1Response;
        public readonly Player Player2;
        public string Player2Response;
        public SessionMatch(string prompt, Player player1, Player player2)
        {
            Prompt = prompt;
            Player1 = player1;
            Player2 = player2;
        }
        public int ResponseCount { get; set; }
    }
}
