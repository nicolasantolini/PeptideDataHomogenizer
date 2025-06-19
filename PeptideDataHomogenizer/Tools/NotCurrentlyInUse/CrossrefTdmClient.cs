using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PeptideDataHomogenizer.Tools.NotCurrentlyInUse
{
    public class CrossrefTdmClient
    {
        private readonly HttpClient _httpClient;
        private readonly HashSet<string> _allowedLicenses = new()
            {
                "http://creativecommons.org/licenses/by/",
                "http://creativecommons.org/licenses/by-nc/"
            };

        public CrossrefTdmClient()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "ResearchBot/1.0 (mailto:your@email.com)");
        }

        public async Task<string> GetFullTextLinkViaHead(string doi)
        {
            Console.WriteLine($"[DEBUG] Sending HEAD request for DOI: {doi}");
            var headRequest = new HttpRequestMessage(HttpMethod.Head, $"https://doi.org/{doi}");
            var response = await _httpClient.SendAsync(headRequest);
            Console.WriteLine($"[DEBUG] Response Status Code: {response.StatusCode}");

            response.EnsureSuccessStatusCode();

            if (response.Headers.Contains("Link"))
            {
                var linkHeader = response.Headers.GetValues("Link");
                Console.WriteLine($"[DEBUG] Link Header: {string.Join(", ", linkHeader)}");
                return ParseLinkHeader(linkHeader);
            }
            return null;
        }

        public async Task<string> GetFullTextByDoi(string doi)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Fetching full text for DOI: {doi}");

                var (licenses, fullTextUrl) = await GetCrossrefMetadata(doi);

                Console.WriteLine($"[DEBUG] Licenses: {string.Join(", ", licenses.Select(l => l.Url))}");
                Console.WriteLine($"[DEBUG] Full Text URL: {fullTextUrl}");

                if (!IsCurrentLicenseAllowed(licenses))
                {
                    Console.WriteLine("[DEBUG] License not allowed for this article");
                    return "License not allowed for this article";
                }

                return await DownloadFullText(fullTextUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception occurred: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private async Task<(IEnumerable<LicenseData> licenses, string fullTextUrl)>
            GetCrossrefMetadata(string doi)
        {
            Console.WriteLine($"[DEBUG] Fetching metadata for DOI: {doi}");
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://doi.org/{doi}");
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.crossref.unixsd+xml"));

            var response = await _httpClient.SendAsync(request);
            Console.WriteLine($"[DEBUG] Metadata Response Status Code: {response.StatusCode}");

            response.EnsureSuccessStatusCode();

            var xml = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[DEBUG] Metadata Response Body: {xml}");

            var doc = XDocument.Parse(xml);
            var ns = doc.Root.GetDefaultNamespace();

            var licenseElements = doc.Root.Elements(ns + "license_ref");
            var licenses = new List<LicenseData>();
            foreach (var elem in licenseElements)
            {
                var startAttr = elem.Attribute("start_date")?.Value;
                DateTime? startDate = null;
                if (!string.IsNullOrEmpty(startAttr) && DateTime.TryParse(startAttr, out var dt))
                {
                    startDate = dt;
                }
                Console.WriteLine("[DEBUG] License: "+elem.Value);
                licenses.Add(new LicenseData
                {
                    Url = elem.Value,
                    StartDate = startDate
                });
            }

            var fullTextUrl = response.Headers.Contains("Link")
                ? ParseLinkHeader(response.Headers.GetValues("Link"))
                : doc.Root.Element(ns + "fulltext")?.Value;

            if (string.IsNullOrEmpty(fullTextUrl))
            {
                Console.WriteLine("[DEBUG] No full-text URL found in metadata");
                throw new Exception("No full-text URL found in metadata");
            }

            return (licenses, fullTextUrl);
        }

        private bool IsCurrentLicenseAllowed(IEnumerable<LicenseData> licenses)
        {
            Console.WriteLine("[DEBUG] Checking if any license is allowed");
            var now = DateTime.UtcNow;

            if (licenses == null || !licenses.Any())
            {
                Console.WriteLine("[DEBUG] No licenses found");
                return false;
            }

            foreach (var lic in licenses)
            {
                if (lic.StartDate == null || lic.StartDate <= now)
                {
                    foreach (var allowed in _allowedLicenses)
                    {
                        if (!string.IsNullOrEmpty(lic.Url) &&
                            lic.Url.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[DEBUG] Allowed license found: {lic.Url}");
                            return true;
                        }
                    }
                }
            }
            Console.WriteLine("[DEBUG] No allowed licenses found");
            return false;
        }

        private async Task<string> DownloadFullText(string url)
        {
            Console.WriteLine($"[DEBUG] Downloading full text from URL: {url}");
            var response = await _httpClient.GetAsync(url);

            if (response.Headers.Contains("CR-TDM-Rate-Limit-Remaining"))
            {
                var remaining = int.Parse(
                    response.Headers.GetValues("CR-TDM-Rate-Limit-Remaining").First());
                if (remaining <= 1)
                {
                    var reset = long.Parse(
                        response.Headers.GetValues("CR-TDM-Rate-Limit-Reset").First());
                    var resetTime = DateTimeOffset.FromUnixTimeSeconds(reset);
                    Console.WriteLine($"[DEBUG] Approaching rate limit. Resets at: {resetTime}");
                }
            }

            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType.MediaType;
            Console.WriteLine($"[DEBUG] Content Type: {contentType}");

            return contentType switch
            {
                "application/pdf" => "[PDF content - would need parsing]",
                "application/xml" or "text/xml" => await response.Content.ReadAsStringAsync(),
                "text/html" => await ExtractArticleText(await response.Content.ReadAsStringAsync()),
                _ => await response.Content.ReadAsStringAsync()
            };
        }

        private string ParseLinkHeader(IEnumerable<string> linkHeaders)
        {
            Console.WriteLine($"[DEBUG] Parsing Link Header: {string.Join(", ", linkHeaders)}");
            foreach (var header in linkHeaders)
            {
                if (header.Contains("rel=\"http://id.crossref.org/schema/fulltext\""))
                {
                    int start = header.IndexOf('<') + 1;
                    int end = header.IndexOf('>');
                    return header[start..end];
                }
            }
            return null;
        }

        private async Task<string> ExtractArticleText(string html)
        {
            Console.WriteLine("[DEBUG] Extracting article text from HTML");
            return html;
        }

        private class LicenseData
        {
            public string Url { get; set; }
            public DateTime? StartDate { get; set; }
        }
    }
}
