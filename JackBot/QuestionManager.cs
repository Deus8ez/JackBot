using System.Text.Json;

namespace JackBot
{
    internal class PromptManager
    {
        private List<string> _questions = new();
        private Random random = new();
        public async Task<string> GetRandomPrompt(string lang = "Ru")
        {
            string filePath = $"set{lang}.json";
            string jsonString = await File.ReadAllTextAsync(filePath);
            JsonDocument document = JsonDocument.Parse(jsonString);
            JsonElement root = document.RootElement;
            JsonElement contentArray = root.GetProperty("content");
            foreach (JsonElement item in contentArray.EnumerateArray())
            {
                string promptValue = item.GetProperty("prompt").GetString();
                Console.WriteLine($"prompt: {promptValue}");
                _questions.Add(promptValue);
            }
            var prompt = _questions[random.Next(0, _questions.Count)];
            _questions.Clear();
            return prompt;
        }
    }
}
