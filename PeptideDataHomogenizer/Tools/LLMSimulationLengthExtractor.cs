namespace PeptideDataHomogenizer.Tools
{
    using System;
    using System.Threading.Tasks;
    using Ollama;

    public class LLMSimulationLengthExtractor : IDisposable
    {
        private readonly OllamaApiClient _ollama;
        private readonly Chat _chat;
        private bool _disposed = false;

        public class Duration
        {
            public List<int> Durations { get; set; } = new List<int>();
        }

        public LLMSimulationLengthExtractor()
        {
            _ollama = new OllamaApiClient();

            string modelName = "SmartSimulationTimeExtractor";
            _chat = _ollama.Chat(model: modelName);
        }

        public async Task<List<string>> ExtractSimulationTimeAsync(string inputText)
        {
            return new List<string>();
            if (string.IsNullOrWhiteSpace(inputText))
            {
                throw new ArgumentException("Input text cannot be null or empty", nameof(inputText));
            }

            var prompt = $"Extract the duration in nanoseconds from the following sentence:  \n{inputText}";

            var response = await _chat.SendAsync(prompt);
            //print response.Content.Trim(); // For debugging purposes
            if (response == null || string.IsNullOrWhiteSpace(response.Content))
            {
                return new List<string>();
            }
            Console.Write("PROMPT: " + prompt);
            Console.WriteLine("RESPONSE: "+response.Content.Trim());
            Duration duration = ParseResponse(response.Content.Trim());

            if (duration.Durations.Count == 0)
            {
                return new List<string>();
            }
            return duration.Durations.Select(d => d.ToString()).ToList();
        }

        private Duration ParseResponse(string ollamaResponse)
        {
            var result = new Duration();

            if (string.IsNullOrWhiteSpace(ollamaResponse))
                return result;

            var numberStrings = ollamaResponse.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var numStr in numberStrings)
            {
                if (int.TryParse(numStr.Trim(), out int duration))
                {
                    result.Durations.Add(duration);
                }
            }

            return result;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _ollama?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
