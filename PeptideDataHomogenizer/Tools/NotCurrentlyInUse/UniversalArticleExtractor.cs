using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.JSInterop;

namespace PeptideDataHomogenizer.Tools.NotCurrentlyInUse
{
    

    public class UniversalArticleExtractor
    {
        private readonly HttpClient _httpClient;
        private readonly HashSet<string> _academicDomains = new HashSet<string>
    {
        "springer.com", "nature.com", "sciencedirect.com", "tandfonline.com",
        "wiley.com", "plos.org", "biomedcentral.com", "ieeexplore.ieee.org"
    };
        private readonly IJSRuntime myWebView;

        public UniversalArticleExtractor(IJSRuntime myWebView)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AcademicBot/1.0");
            this.myWebView = myWebView;
        }

        // SimulationMethod 1: Fetch full-text by DOI
        public async Task<Dictionary<string, string>> GetFullTextByDoi(string doi)
        {
            string landingUrl = $"https://doi.org/{doi}";
            Console.WriteLine($"Fetching article from: {landingUrl}");
            return await ExtractFromLandingPage(landingUrl);
        }

        public async Task<string> GetFullTextUnformattedByDoi(string doi)
        {
            string landingUrl = $"https://doi.org/{doi}";
            Console.WriteLine($"Fetching article from: {landingUrl}");
            return await GetFullTextByUrl(landingUrl);
        }


        // SimulationMethod 3: Fetch full-text by URL
        public async Task<string> GetFullTextByUrl(string url)
        {
            Console.WriteLine($"Fetching article from: {url}");
            string html = await myWebView.InvokeAsync<string>("eval", new string[] { "document.documentElement.outerHTML;" });
            var text = html;
            return text;
        }        
        // SimulationMethod 2: Fetch full-text by PMID
        public async Task<Dictionary<string, string>> GetFullTextByPmid(string pmid)
        {
            string landingUrl = $"https://pubmed.ncbi.nlm.nih.gov/{pmid}/";
            return await ExtractFromLandingPage(landingUrl);
        }

        private async Task<Dictionary<string, string>> ExtractFromLandingPage(string url)
        {
            var articleSections = new Dictionary<string, string>();

            try
            {
                // Step 1: Download HTML
                string html = await _httpClient.GetStringAsync(url);
                Console.WriteLine("HTML: "+html);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Step 2: Try academic journal-specific selectors
                if (IsAcademicDomain(url))
                {
                    articleSections = ParseAcademicHtml(doc);
                }

                // Step 3: Fallback to generic extraction
                if (articleSections.Count == 0)
                {
                    articleSections = ParseGenericHtml(doc);
                }

                return articleSections;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting from {url}: {ex.Message}");
                return articleSections;
            }
        }

        private Dictionary<string, string> ParseAcademicHtml(HtmlDocument doc)
        {
            var sections = new Dictionary<string, string>();

            // Common selectors for academic papers
            var selectors = new Dictionary<string, string>
        {
            { "Abstract", "//div[contains(@class,'abstract')]//p" },
            { "Introduction", "//div[h2[contains(.,'Introduction')]]" },
            { "Methods", "//div[h2[contains(.,'Methods')]]" },
            { "Results", "//div[h2[contains(.,'Results')]]" },
            { "References", "//div[contains(@class,'references')]" }
        };

            foreach (var (section, xpath) in selectors)
            {
                var node = doc.DocumentNode.SelectSingleNode(xpath);
                if (node != null)
                {
                    sections.Add(section, CleanText(node.InnerText));
                }
            }

            return sections;
        }

        private Dictionary<string, string> ParseGenericHtml(HtmlDocument doc)
        {
            var sections = new Dictionary<string, string>();

            // Heuristic: Find the longest text block (likely main content)
            var paragraphs = doc.DocumentNode.SelectNodes("//p");
            if (paragraphs != null)
            {
                string fullText = string.Join("\n\n", paragraphs.Select(p => CleanText(p.InnerText)));
                sections.Add("FullText", fullText);
            }

            return sections;
        }

        private string CleanText(string text)
        {
            return System.Web.HttpUtility.HtmlDecode(text)
                .Replace("\n", " ")
                .Replace("\t", " ")
                .Trim();
        }

        private bool IsAcademicDomain(string url)
        {
            foreach (var domain in _academicDomains)
            {
                if (url.Contains(domain)) return true;
            }
            return false;
        }
    }
}
