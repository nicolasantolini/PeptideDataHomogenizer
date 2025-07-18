using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Entities;
using PeptideDataHomogenizer.Data;
using static PeptideDataHomogenizer.Components.Pages.Home;

namespace PeptideDataHomogenizer.Tools.PubMedSearch
{
    public interface IPubMedAPIConsumer
    {
        Task<SearchResults> SearchArticlesAsync(string query,AdditionalQueryParams additionalQueryParams, string db = "pubmed", int page = 1, int pageSize = 5, string apiKey = null);
        Task<List<ArticleDetail>> GetArticlesFromPubMedApi(List<string> ids, string db = "pubmed");
    }

    public class PubMedAPIConsumer : IPubMedAPIConsumer
    {
        private readonly HttpClient _http;
        private readonly DatabaseDataHandler _databaseDataHandler;
        private const string BaseUrl = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils/";
        private DateTime _lastRequestTime = DateTime.MinValue;
        private readonly object _lock = new();
        private const int MinRequestIntervalMs = 333;

        public PubMedAPIConsumer(HttpClient http, [FromServices] DatabaseDataHandler databaseDataHandler)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _http.BaseAddress = new Uri(BaseUrl);

            if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _http.DefaultRequestHeaders.Add("User-Agent", "NCBIApiClient/1.0");
            }

            _databaseDataHandler = databaseDataHandler ?? throw new ArgumentNullException(nameof(databaseDataHandler));
        }

        /// <summary>
        /// Searches for articles in PubMed using the provided query and returns a paginated result set.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="db"></param>
        /// <param name="page"></param>
        /// <param name="pageSize"></param>
        /// <param name="apiKey"></param>
        /// <returns></returns>
        public async Task<SearchResults> SearchArticlesAsync(
            string query,
            AdditionalQueryParams additionalQueryParams,
            string db = "pubmed",
            int page = 1,
            int pageSize = 5,
            string apiKey = null
            )
        {
            var (totalCount, allIds) = await PerformInitialSearch(query, db, page, pageSize, apiKey,additionalQueryParams);

            return new SearchResults
            {
                Total = totalCount,
                Page = page,
                PageSize = pageSize,
                ArticlesPubMedIds = allIds
            };
        }

        private async Task<(int totalCount, List<string> ids)> PerformInitialSearch(
            string query, string db, int page, int pageSize, string apiKey, AdditionalQueryParams additionalQueryParams)
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
            if (additionalQueryParams != null)
            {
                if (!string.IsNullOrEmpty(additionalQueryParams.Sort))
                    parameters["sort"] = additionalQueryParams.Sort;
                else
                    parameters["sort"] = "relevance";
                if (!string.IsNullOrEmpty(additionalQueryParams.Field))
                    parameters["field"] = additionalQueryParams.Field;
                if (additionalQueryParams.MinDate.HasValue && additionalQueryParams.MaxDate.HasValue)
                {
                    parameters["mindate"] = additionalQueryParams.MinDate.Value.ToString("yyyy/MM/dd");
                    parameters["maxdate"] = additionalQueryParams.MaxDate.Value.ToString("yyyy/MM/dd");
                }
                if(additionalQueryParams.RelDate>0)
                {
                    parameters["reldate"] = additionalQueryParams.RelDate.ToString();
                }
            }

            if (!string.IsNullOrEmpty(apiKey))
                parameters["api_key"] = apiKey;

            //print request uri
            Console.WriteLine($"PubMed API request: {BaseUrl}esearch.fcgi?{BuildQuery(parameters)}");

            var response = await ExecuteWithRetry(() =>
                _http.GetAsync($"esearch.fcgi?{BuildQuery(parameters)}"));

            Console.WriteLine($"PubMed API response: {response.RequestMessage.RequestUri}");

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var result = doc.RootElement.GetProperty("esearchresult");

            Console.WriteLine($"PubMed API result: {result}");

            var totalCount = Convert.ToInt32(result.GetProperty("count").GetString());
            var ids = result.GetProperty("idlist").EnumerateArray()
                .Select(id => id.GetString())
                .Where(id => id != null)
                .ToList();

            return (totalCount, ids);
        }


        /// <summary>
        /// Fetches full article details from PubMed API using a list of PubMed IDs without full text.
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        public async Task<List<ArticleDetail>> GetArticlesFromPubMedApi(List<string> ids, string db)
        {
            if (ids == null || ids.Count == 0)
                return new List<ArticleDetail>();
            EnsureRateLimit();

            var parameters = new Dictionary<string, string>
            {
                ["db"] = db,
                ["id"] = string.Join(",", ids),
                ["retmode"] = "xml",
                ["rettype"] = "full"
            };

            var response = await ExecuteWithRetry(() =>
                _http.GetAsync($"efetch.fcgi?{BuildQuery(parameters)}"));

            var xmlResp = await response.Content.ReadAsStringAsync();
            var xml = XDocument.Parse(xmlResp);


            return xml.Descendants("PubmedArticle")
                .Select(ArticleDetail.FromXml)
                .ToList();
        }


        /*
         * HELPER FUNCTIONS
         */

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
