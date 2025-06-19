using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Xml.Linq;
using System.Web;
using Azure;
using System.Xml.Serialization;
using static PeptideDataHomogenizer.Tools.HtmlTools.ArticleExtractorFromHtml;
using Entities;
using PeptideDataHomogenizer.Data;
using Microsoft.AspNetCore.Mvc;

namespace PeptideDataHomogenizer.Tools.PubMedSearch
{
    public interface IEUtilitiesService
    {
        Task<SearchResults> SearchArticlesAsync(string query, string db = "pubmed", int page = 1, int pageSize = 5, string apiKey = null);
        Task<List<ArticleDetail>> GetArticlesDetailAsync(List<string> ids, string db = "pubmed");
    }
    public class EUtilitiesService : IEUtilitiesService
    {
        private readonly HttpClient _http;
        private const string BaseUrl = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils/";
        private DateTime _lastRequestTime = DateTime.MinValue;
        private readonly object _lock = new object();
        private const int MinRequestIntervalMs = 333;

        private DatabaseDataHandler DatabaseDataHandler;

        public EUtilitiesService(HttpClient http, [FromServices] DatabaseDataHandler databaseDataHandler)
        {
            _http = http;
            _http.BaseAddress = new Uri(BaseUrl);
            if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _http.DefaultRequestHeaders.Add("User-Agent", "NCBIApiClient/1.0");
            }
            DatabaseDataHandler = databaseDataHandler;
        }

        public async Task<SearchResults> SearchArticlesAsync(
            string query,
            string db = "pubmed",
            int page = 1,
            int pageSize = 5,
            string apiKey = null)
        {
            EnsureRateLimit();

            var parameters = new Dictionary<string, string>
            {
                ["db"] = db,
                ["term"] = query,
                ["retstart"] = ((page - 1) * pageSize).ToString(),
                ["retmax"] = pageSize.ToString(),
                ["usehistory"] = "y",
                ["retmode"] = "json"
            };

            if (!string.IsNullOrEmpty(apiKey))
                parameters["api_key"] = apiKey;

            var response = await ExecuteWithRetry(() =>
                _http.GetAsync($"esearch.fcgi?{BuildQuery(parameters)}"));

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var result = doc.RootElement.GetProperty("esearchresult");
            var ids = result.GetProperty("idlist").EnumerateArray()
                    .Select(id => id.GetString())
                    .ToList();

            //print all ids
            foreach ( var id in ids )
                Console.WriteLine(id);

            //ids = await FilterArticleIdsApprovedOrDiscredited(ids.Where(id => id != null).ToList());

            Console.WriteLine("post filter:");
            foreach ( var id in ids )
                Console.WriteLine(id);
            var articles = new List<ArticleDetail>();
            articles.AddRange(await GetArticlesDetailAsync(ids, db));

            var searchResults = new SearchResults
            {
                Total = Convert.ToInt32(result.GetProperty("count").GetString()),
                Page = page,
                PageSize = pageSize
            };
            return searchResults;
        }


        public async Task<List<ArticleDetail>> GetArticlesDetailAsync(List<string> ids, string db = "pubmed")
        {
            var articles = new List<ArticleDetail>();

            var dbArticles = await DatabaseDataHandler.GetArticlesByPubMedIdsAsync(ids.Where(id => id != null).ToList());

            if (dbArticles != null && dbArticles.Count > 0)
            {
                articles.AddRange(dbArticles.Select(a => new ArticleDetail
                {
                    Id = a.PubMedId,
                    Title = a.Title,
                    Abstract = a.Abstract,
                    DOI = a.Doi,
                    ProteinRecords = a.ProteinData.ToList(),
                    FullText = a.Chapters.ToList()
                }).ToList());
            }

            ids = ids.Except(dbArticles.Select(a => a.PubMedId)).ToList();

            EnsureRateLimit();

            var parameters = new Dictionary<string, string>
            {
                ["db"] = db,
                ["id"] = string.Join(",", ids.Where(id => id != null)),
                ["retmode"] = "xml",
                ["rettype"] = "full"
            };

            var response = await ExecuteWithRetry(() =>
                _http.GetAsync($"efetch.fcgi?{BuildQuery(parameters)}"));

            var xmlResp = await response.Content.ReadAsStringAsync();
            var xml = XDocument.Parse(xmlResp);

            articles.AddRange(xml.Descendants("PubmedArticle")
                .Select(ArticleDetail.FromXml)
                .ToList());

            return articles;
        }

        private void EnsureRateLimit()
        {
            lock (_lock)
            {
                var timeSinceLastRequest = DateTime.Now - _lastRequestTime;
                var delayNeeded = MinRequestIntervalMs - timeSinceLastRequest.TotalMilliseconds;

                if (delayNeeded > 0)
                {
                    Task.Delay((int)delayNeeded).Wait();
                }
                _lastRequestTime = DateTime.Now;
            }
        }

        private async Task<HttpResponseMessage> ExecuteWithRetry(Func<Task<HttpResponseMessage>> operation, int maxRetries = 5)
        {
            int retryCount = 0;
            while (true)
            {
                try
                {
                    var response = await operation();

                    if ((int)response.StatusCode == 429 && retryCount < maxRetries)
                    {
                        var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                        await Task.Delay(retryAfter);
                        retryCount++;
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                    return response;
                }
                catch (HttpRequestException ex) when (retryCount < maxRetries)
                {
                    retryCount++;
                    await Task.Delay(1000 * retryCount);
                }
            }
        }

        private string BuildQuery(Dictionary<string, string> parameters)
        {
            return string.Join("&", parameters
                .Where(p => !string.IsNullOrEmpty(p.Value))
                .Select(p => $"{HttpUtility.UrlEncode(p.Key)}={EncodeValue(p.Key, p.Value)}"));
        }

        private string EncodeValue(string key, string value)
        {
            if (key == "term")
            {
                return HttpUtility.UrlEncode(value)
                    .Replace("%20", "+")
                    .Replace("%26", "&")
                    .Replace("%28", "(")
                    .Replace("%29", ")")
                    .Replace("%5B", "[")
                    .Replace("%5D", "]");
            }
            return HttpUtility.UrlEncode(value);
        }
    }

    
}