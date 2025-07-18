using Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace PeptideDataHomogenizer.Tools.ElsevierTools
{
    public interface IElsevierArticleFetcher
    {
        Task<(List<Chapter>, List<ExtractedTable>,List<ImageHolder>)> GetFullTextByDoi(string doi, string format = "text/xml");
    }
    public class ElsevierArticleFetcher : IElsevierArticleFetcher
    {
        private readonly IWebHostEnvironment WebHostEnvironment;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public ElsevierArticleFetcher([FromServices] HttpClient _http,[FromServices] IWebHostEnvironment webHostEnvironment)
        {
            _httpClient = _http;
            _apiKey = "0e70f027e08a066848325116931c83da";

            // Set default headers
            _httpClient.DefaultRequestHeaders.Add("X-ELS-APIKey", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/xml"); // Default to XML
            WebHostEnvironment = webHostEnvironment;
        }

        public async Task<(List<Chapter>,List<ExtractedTable>,List<ImageHolder>)> GetFullTextByDoi(string doi, string format = "text/xml")
        {
            var sections = new List<Chapter>();
            var tables = new List<ExtractedTable>();
            var images = new List<ImageHolder>();
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
                    return (sections, tables,images);
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
                    foreach (var table in articleContent.Tables)
                    {
                        var extractedTable = new ExtractedTable
                        {
                            Caption = table.Caption,
                            ArticleDoi = doi,
                            Rows = table.Rows
                        };
                        tables.Add(extractedTable);
                    }

                    foreach (var image in articleContent.Images)
                    {
                        var downloadedImage = await DownloadAndSaveImageAsync(image.FileName, image.Caption);
                        if (downloadedImage != null)
                        {
                            images.Add(downloadedImage);
                        }
                    }
                }

                return (sections, tables,images);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching article: {ex.Message}");
                return (sections, tables,images);
            }
            finally
            {
                // Reset Accept header to default
                _httpClient.DefaultRequestHeaders.Remove("Accept");
                _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            }
        }

        private async Task<ImageHolder> DownloadAndSaveImageAsync(string imageUrl, string caption)
        {
            Console.WriteLine($"[DownloadAndSaveImageAsync] Called with imageUrl: {imageUrl}, caption: {caption}");

            if (string.IsNullOrEmpty(imageUrl))
            {
                Console.WriteLine("[DownloadAndSaveImageAsync] imageUrl is null or empty. Returning null.");
                return null;
            }

            try
            {
                _httpClient.DefaultRequestHeaders.Remove("Accept");
                _httpClient.DefaultRequestHeaders.Add("Accept", "image/*");
                Console.WriteLine($"[DownloadAndSaveImageAsync] Sending GET request to: {imageUrl}");

                var response = await _httpClient.GetAsync(imageUrl);
                Console.WriteLine($"[DownloadAndSaveImageAsync] Response status code: {response.StatusCode}");

                response.EnsureSuccessStatusCode();

                var fileExtension = response.Content.Headers.ContentType?.MediaType switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png",
                    _ => ".img"
                };
                var fileName = Guid.NewGuid().ToString() + fileExtension;
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";

                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                var base64Image = Convert.ToBase64String(imageBytes);

                Console.WriteLine($"[DownloadAndSaveImageAsync] fileName: {fileName}");
                Console.WriteLine($"[DownloadAndSaveImageAsync] contentType: {contentType}");

                return new ImageHolder
                {
                    Caption = caption,
                    FileName = fileName,
                    ContentType = contentType,
                    ImageData = System.Text.Encoding.UTF8.GetBytes(base64Image)
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DownloadAndSaveImageAsync] Error downloading image: {ex.Message}");
                return null;
            }
        }
    }
}