using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PeptideDataHomogenizer.Tools.NotCurrentlyInUse
{
    public class PythonRegexProteinDataExtractor
    {
        private readonly HttpClient _httpClient;

        public PythonRegexProteinDataExtractor([FromServices] HttpClient httpClient)
        {
            _httpClient = httpClient;
            // Set the base address for the HttpClient
            _httpClient.BaseAddress = new Uri("http://127.0.0.1:5034/");
            // Clear and set default headers once during initialization
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.Timeout = new TimeSpan(0,30,0);
        }

        // SimulationMethod to extract protein data using the Python API
        public async Task<List<ProteinDataResponse>> GetArticleMetadataAsync(string text)
        {
            try
            {
                // Create the request body with proper serialization options
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                // Create a strongly-typed request object
                var requestBody = new ProteinDataRequest { Text = text };

                // Send the POST request to the Python API using JsonContent
                using var content = JsonContent.Create(requestBody, MediaTypeHeaderValue.Parse("application/json"), options);
                var response = await _httpClient.PostAsync("extract_md_data", content);

                // Check if the response is successful
                if (response.IsSuccessStatusCode)
                {
                    //print response
                    Console.WriteLine($"Response: {await response.Content.ReadAsStringAsync()}");

                    // Read and deserialize the response content
                    var responseData = await response.Content.ReadFromJsonAsync<ProteinDataApiResponse>(options);
                    return responseData.Data;
                }
                else
                {
                    // Handle error response with detailed logging
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error: {response.StatusCode}");
                    Console.WriteLine($"Response: {errorContent}");
                    throw new HttpRequestException($"Request failed with status code {response.StatusCode}. Details: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in GetArticleMetadataAsync: {ex.Message}");
                throw;
            }
        }
    }

    public class ProteinDataRequest
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class ProteinDataApiResponse
    {
        [JsonPropertyName("data")]
        public List<ProteinDataResponse> Data { get; set; } = new();

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    public class ProteinDataResponse
    {
        [JsonPropertyName("software_name")]
        public string SoftwareName { get; set; } = string.Empty;

        [JsonPropertyName("software_version")]
        public string SoftwareVersion { get; set; } = string.Empty;

        [JsonPropertyName("water_model")]
        public string WaterModel { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }
}
