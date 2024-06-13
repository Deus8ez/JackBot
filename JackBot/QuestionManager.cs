using System.Text.Json;

namespace JackBot
{
    internal class PromptManager
    {
        private List<string> _questions = new();
        private Random random = new();
        public async Task<string> GetRandomPrompt(string lang = "Ru")
        {
            await Load(lang);
            var prompt = _questions[GetRandomNumber()];
            Clear();
            return prompt;
        }

        public async Task Load(string lang = "Ru")
        {
            string filePath = $"set{lang}.json";
            string jsonString = await File.ReadAllTextAsync(filePath);
            JsonDocument document = JsonDocument.Parse(jsonString);
            JsonElement root = document.RootElement;
            JsonElement contentArray = root.GetProperty("content");
            foreach (JsonElement item in contentArray.EnumerateArray())
            {
                string promptValue = item.GetProperty("prompt").GetString();
                _questions.Add(promptValue);
            }
        }

        public void Clear()
        {
            _questions.Clear();
        }

        public int GetRandomNumber()
        {
            return random.Next(0, _questions.Count);
        }

        public int GetQuestionCount()
        {
            return _questions.Count;
        }
    }
}
