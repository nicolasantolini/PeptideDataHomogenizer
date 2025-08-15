using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace PeptideDataHomogenizer.Tools.WebScraper
{

    public class RobotsTxtChecker
    {
        private readonly HttpClient _httpClient;

        public RobotsTxtChecker([FromServices] HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<bool> IsPathScrapableAsync(
            string baseUrl,
            string contentPath,
            string userAgent)
        {
            try
            {
                // Normalize URLs (ensure no trailing slashes)
                baseUrl = baseUrl.TrimEnd('/');
                contentPath = contentPath.TrimStart('/');

                // Fetch robots.txt
                string robotsTxtUrl = $"{baseUrl}/robots.txt";
                string robotsTxtContent;

                try
                {
                    robotsTxtContent = await _httpClient.GetStringAsync(robotsTxtUrl);
                }
                catch (HttpRequestException)
                {
                    // If robots.txt doesn't exist, assume scraping is allowed
                    return true;
                }

                // Split into sections for each User-Agent
                var sections = robotsTxtContent.Split(
                    new[] { "User-agent:" },
                    StringSplitOptions.RemoveEmptyEntries
                );

                bool isAllowed = true;
                bool foundMatchingUserAgent = false;

                foreach (var section in sections)
                {
                    var lines = section.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length == 0) continue;

                    // Check if this section applies to our User-Agent
                    string sectionUserAgent = lines[0].Trim();
                    if (sectionUserAgent != "*" &&
                        !sectionUserAgent.Equals(userAgent, StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Skip if not for our User-Agent
                    }

                    foundMatchingUserAgent = true;

                    // Parse rules in this section
                    for (int i = 1; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // Check for "Disallow" or "Allow" rules
                        if (line.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
                        {
                            string disallowedPath = line.Substring("Disallow:".Length).Trim();
                            if (IsPathMatch(contentPath, disallowedPath))
                            {
                                isAllowed = false;
                            }
                        }
                        else if (line.StartsWith("Allow:", StringComparison.OrdinalIgnoreCase))
                        {
                            string allowedPath = line.Substring("Allow:".Length).Trim();
                            if (IsPathMatch(contentPath, allowedPath))
                            {
                                isAllowed = true;
                            }
                        }
                        // Ignore other directives (e.g., Crawl-delay)
                    }
                }

                // If no matching User-Agent section, assume allowed
                return foundMatchingUserAgent ? isAllowed : true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking robots.txt: {ex.Message}");
                return false; // Fail-safe: assume not allowed
            }
        }

        private static bool IsPathMatch(string requestPath, string rulePath)
        {
            if (string.IsNullOrEmpty(rulePath))
                return false;

            // Escape regex special chars and replace * with .*
            string regexPattern = "^" + Regex.Escape(rulePath)
                .Replace(@"\*", ".*") + ".*$";

            return Regex.IsMatch(requestPath, regexPattern, RegexOptions.IgnoreCase);
        }
    }
}
