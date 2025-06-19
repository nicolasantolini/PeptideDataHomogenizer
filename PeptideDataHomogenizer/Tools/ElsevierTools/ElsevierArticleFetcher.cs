using Entities;
using Microsoft.AspNetCore.Mvc;

namespace PeptideDataHomogenizer.Tools.ElsevierTools
{
    public interface IElsevierArticleFetcher
    {
        Task<List<Chapter>> GetFullTextByDoi(string doi, string format = "text/xml");
    }
    public class ElsevierArticleFetcher : IElsevierArticleFetcher
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public ElsevierArticleFetcher([FromServices] HttpClient _http)
        {
            _httpClient = _http;
            _apiKey = "0e70f027e08a066848325116931c83da";

            // Set default headers
            _httpClient.DefaultRequestHeaders.Add("X-ELS-APIKey", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/xml"); // Default to XML

        }

        public async Task<List<Chapter>> GetFullTextByDoi(string doi, string format = "text/xml")
        {
            var sections = new List<Chapter>();
            var apiUrl = $"https://api.elsevier.com/content/article/doi/{doi}";
            try
            {
                // Set the Accept header for this specific request
                _httpClient.DefaultRequestHeaders.Remove("Accept");
                _httpClient.DefaultRequestHeaders.Add("Accept", format);

                Console.WriteLine($"Fetching article from: {apiUrl}");
                var response = await _httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Elsevier Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    return sections;
                }

                if (format == "text/xml")
                {
                    ElsevierArticleXMLConverter articleContent = new ElsevierArticleXMLConverter();
                    var xmlContent = await response.Content.ReadAsStringAsync();
                    //print xml
                    //Console.WriteLine($"XML Content: {xmlContent}");
                    articleContent = ElsevierArticleXMLConverter.ParseArticleBody(xmlContent);


                    foreach (var section in articleContent.Sections)
                    {
                        var textContent = "";
                        
                        foreach (var para in section.Paragraphs)
                        {
                            textContent += para.Text + "\n";
                        }

                        if (section.Title != null)
                        {
                            sections.Add(new Chapter
                            {
                                Title = section.Title,
                                Content = textContent
                            });
                        }
                        else
                        {
                            sections.Add(new Chapter
                            {
                                Title = "No title",
                                Content = textContent
                            });
                        }
                    }
                }

                return sections;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching article: {ex.Message}");
                return sections;
            }
            finally
            {
                // Reset Accept header to default
                _httpClient.DefaultRequestHeaders.Remove("Accept");
                _httpClient.DefaultRequestHeaders.Add("Accept", "text/xml");
            }
        }
    }
}