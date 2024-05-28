namespace JackBot
{
    internal class Player
    {
        public Player(long id, string username)
        {
            Id = id;
            Username = username;
            MatchScore = 0;
            TotalScore = 0;
        }

        public readonly long Id;
        public readonly string Username;
        public int MatchScore { get; set; }
        public int TotalScore { get; set; }
    }
}
