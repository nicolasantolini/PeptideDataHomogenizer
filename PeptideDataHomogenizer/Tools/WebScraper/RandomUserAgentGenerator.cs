namespace PeptideDataHomogenizer.Tools.WebScraper
{
    public static class RandomUserAgentGenerator
    {
        private static readonly Random rand = new Random();
        private static readonly object syncLock = new object();
        private static readonly DateTime lastRefresh = DateTime.UtcNow;
        private static readonly string[] browserTypes = { "chrome", "firefox", "edge", "safari" };
        private static readonly string[] operatingSystems =
        {
            "Windows NT 10.0; Win64; x64",
            "X11; Ubuntu; Linux x86_64",
            "Macintosh; Intel Mac OS X 12_4",
            "iPhone; CPU iPhone OS 15_4 like Mac OS X",
            "Android 10; Mobile"
        };

        private static readonly Dictionary<string, string> userAgentTemplates = new()
        {
            { "chrome", "Mozilla/5.0 ({0}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{1} Safari/537.36" },
            { "firefox", "Mozilla/5.0 ({0}; rv:{1}.0) Gecko/20100101 Firefox/{1}.0" },
            { "edge", "Mozilla/5.0 ({0}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{1} Safari/537.36 Edg/{2}" },
            { "safari", "Mozilla/5.0 ({0}) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/{1} Safari/605.1.15" }
        };

        public static string GetRandomUserAgent()
        {
            lock (syncLock)
            {
                // Select random operating system
                var os = operatingSystems[rand.Next(operatingSystems.Length)];

                // If the OS is mobile, use safari. Otherwise, pick a random browser.
                string browser = os.Contains("iPhone")
                    ? "safari"
                    : browserTypes[rand.Next(browserTypes.Length)];

                var template = userAgentTemplates[browser];

                return browser switch
                {
                    "chrome" => GenerateChromeUserAgent(os, template),
                    "firefox" => GenerateFirefoxUserAgent(os, template),
                    "edge" => GenerateEdgeUserAgent(os, template),
                    "safari" => GenerateSafariUserAgent(os, template),
                    _ => throw new NotSupportedException($"Browser {browser} not supported")
                };
            }
        }

        private static string GenerateChromeUserAgent(string os, string template)
        {
            var version = rand.Next(110, 123); // Current Chrome versions
            var minor = 0;
            var patch = rand.Next(4950, 5162);
            var build = rand.Next(80, 212);
            var chromeVersion = $"{version}.{minor}.{patch}.{build}";
            return string.Format(template, os, chromeVersion);
        }

        private static string GenerateFirefoxUserAgent(string os, string template)
        {
            var version = rand.Next(110, 123);
            return string.Format(template, os, version);
        }

        private static string GenerateEdgeUserAgent(string os, string template)
        {
            var chromeVersion = rand.Next(110, 123);
            var edgeVersion = rand.Next(110, 123);
            return string.Format(template, os, chromeVersion, edgeVersion);
        }

        private static string GenerateSafariUserAgent(string os, string template)
        {
            var version = $"{rand.Next(14, 16)}.{rand.Next(0, 5)}";
            return string.Format(template, os, version);
        }
    }
}
