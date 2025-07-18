namespace PeptideDataHomogenizer.Tools.WileyTools
{
    using Entities;
    using Microsoft.AspNetCore.Mvc;
    using PeptideDataHomogenizer.Tools.HtmlTools;
    using PeptideDataHomogenizer.Tools.PdfTools;
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;



    public class WileyArticleDownloader : IDisposable
    {
        private readonly string _clientToken;
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _rateLimiter;
        private DateTime _lastRequestTime = DateTime.MinValue;
        private readonly object _lock = new object();
        private const int MaxRequestsPerSecond = 3;
        private const int MaxRequestsPer10Minutes = 60;
        private const int DelayBetweenRequestsMs = 10000; // 10 seconds

        private readonly ArticleExtractorFromHtml _articleExtractor;

        public WileyArticleDownloader(string clientToken, [FromServices] ArticleExtractorFromHtml articleExtractorFromHtml)
        {
            if (string.IsNullOrWhiteSpace(clientToken))
            {
                throw new ArgumentException("Client token cannot be null or empty", nameof(clientToken));
            }

            if (articleExtractorFromHtml == null)
            {
                throw new ArgumentNullException(nameof(articleExtractorFromHtml), "Article extractor cannot be null");
            }
            _articleExtractor = articleExtractorFromHtml;
            _clientToken = clientToken;
            _httpClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = true // Equivalent to -L in curl
            });

            // Initialize rate limiter with initial count of 1 to ensure proper delays
            _rateLimiter = new SemaphoreSlim(1, 1);
        }

        public async Task<(List<Chapter>,List<ExtractedTable>,List<ImageHolder>)> DownloadArticleAsync(string articleDoi,string title, string headersOutputFilePath = null)
        {
            if (string.IsNullOrWhiteSpace(articleDoi))
            {
                throw new ArgumentException("Article DOI cannot be null or empty", nameof(articleDoi));
            }


            // Enforce rate limiting
            await _rateLimiter.WaitAsync();
            try
            {
                await EnforceRateLimitsAsync();

                var url = $"https://api.wiley.com/onlinelibrary/tdm/v1/articles/{Uri.EscapeDataString(articleDoi)}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Wiley-TDM-Client-Token", _clientToken);

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                // Save headers if requested
                if (!string.IsNullOrWhiteSpace(headersOutputFilePath))
                {
                    await SaveResponseHeadersAsync(response, headersOutputFilePath);
                }

                // Handle errors
                if (!response.IsSuccessStatusCode)
                {
                    await HandleErrorResponseAsync(response, articleDoi);
                    return (new List<Chapter>(), new List<ExtractedTable>(), new List<ImageHolder>());
                }

                // Save the PDF
                var pdfBytes = await response.Content.ReadAsByteArrayAsync();
                var content = PdfCreationTools.ConvertPdfToHtml(pdfBytes);
                Console.WriteLine("PDF successfully converted to HTML.");

                var extractedData = await _articleExtractor.ExtractChaptersImagesAndTables(content, title,"");

                return (extractedData.Item1,extractedData.Item2,extractedData.Item3);
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        private async Task EnforceRateLimitsAsync()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var timeSinceLastRequest = now - _lastRequestTime;

                // Enforce minimum delay between requests
                if (timeSinceLastRequest.TotalMilliseconds < DelayBetweenRequestsMs)
                {
                    var delayMs = (int)(DelayBetweenRequestsMs - timeSinceLastRequest.TotalMilliseconds);
                    Thread.Sleep(delayMs);
                }

                _lastRequestTime = DateTime.UtcNow;
            }
        }

        private async Task SaveResponseHeadersAsync(HttpResponseMessage response, string headersOutputFilePath)
        {
            try
            {
                var headersText = $"HTTP/1.1 {(int)response.StatusCode} {response.ReasonPhrase}\n";
                foreach (var header in response.Headers)
                {
                    headersText += $"{header.Key}: {string.Join(", ", header.Value)}\n";
                }

                foreach (var header in response.Content.Headers)
                {
                    headersText += $"{header.Key}: {string.Join(", ", header.Value)}\n";
                }

                await File.WriteAllTextAsync(headersOutputFilePath, headersText);
            }
            catch (Exception ex)
            {
                // Don't fail the download if we can't save headers
                Console.WriteLine($"Warning: Failed to save response headers: {ex.Message}");
            }
        }

        private async Task HandleErrorResponseAsync(HttpResponseMessage response, string articleDoi)
        {
            var errorMessage = await response.Content.ReadAsStringAsync();
            var statusCode = response.StatusCode;

            switch (statusCode)
            {
                case System.Net.HttpStatusCode.BadRequest:
                    throw new InvalidOperationException($"No TDM Client Token was found in the request for article {articleDoi}");

                case System.Net.HttpStatusCode.Forbidden:
                    throw new InvalidOperationException($"TDM Client Token is invalid and not registered for article {articleDoi}");

                case System.Net.HttpStatusCode.NotFound:
                    throw new InvalidOperationException(
                        $"No access to article {articleDoi}. You or your institution/organization does not have access to the content. " +
                        "Please check that you are requesting content for which you have full-text access.");

                case (System.Net.HttpStatusCode)429: // TooManyRequests
                    throw new InvalidOperationException(
                        $"Rate limit exceeded for article {articleDoi}. " +
                        "Please reduce the rate you are calling this API (max 3 articles per second and 60 per 10 minutes).");

                default:
                    throw new HttpRequestException(
                        $"Failed to download article {articleDoi}. Status code: {statusCode}. Message: {errorMessage}");
            }
        }

        public void Dispose()
        {
            _rateLimiter?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
