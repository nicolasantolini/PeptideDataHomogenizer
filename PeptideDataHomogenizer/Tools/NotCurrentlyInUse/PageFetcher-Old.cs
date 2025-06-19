using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using PeptideDataHomogenizer.Data.State;
using PeptideDataHomogenizer.Tools.WebScraper;
using PuppeteerSharp;

namespace PeptideDataHomogenizer.Tools.NotCurrentlyInUse
{
    public static class PageFetcher
    {
        private static readonly SemaphoreSlim _browserSemaphore = new SemaphoreSlim(5, 5); // Limit concurrent browsers
        private static readonly ConcurrentBag<IBrowser> _browserPool = new ConcurrentBag<IBrowser>();
        private static bool _browserFetcherInitialized = false;
        private static readonly object _initLock = new object();

        


        public static async Task<string> GetFullPageContentAsync(string url, bool headless = true, int timeoutMs = 30000)
        {
            ControlledTimer.StartOperationTimer($"FULL PAGE FETCH: {url}");

            IBrowser browser = null;
            IPage page = null;

            try
            {
                // Get or create browser instance
                browser = await GetBrowserInstanceAsync(headless);
                while(browser == null)
                {
                    await Task.Delay(300);
                    browser = await GetBrowserInstanceAsync(headless);
                }
                while(page == null)
                {
                    await Task.Delay(300);
                    page = await browser.NewPageAsync();
                }
                page.DefaultNavigationTimeout = timeoutMs;
                page.DefaultTimeout = timeoutMs;

                // Navigation with retries
                var content = await NavigateAndGetContentWithRetries(page, url);

                ControlledTimer.StopOperationTimer($"FULL PAGE FETCH: {url}");
                return content;
            }
            finally
            {
                // Cleanup resources
                if (page != null)
                {
                    await page.CloseAsync();
                }

                if (browser != null)
                {
                    ReturnBrowserToPool(browser);
                }
            }
        }

        private static async Task<string> NavigateAndGetContentWithRetries(IPage page, string url)
        {
            int attempt = 0;
            int maxRetries = 3;
            TimeSpan delay = TimeSpan.FromMilliseconds(500);

            while (attempt < maxRetries)
            {
                attempt++;
                try
                {
                    ControlledTimer.StartOperationTimer($"NAVIGATION ATTEMPT {attempt}: {url}");

                    if (page == null)
                    {
                        page = await GetBrowserInstanceAsync(true).Result.NewPageAsync();
                        page.DefaultNavigationTimeout = 30000;
                        page.DefaultTimeout = 30000;
                    }
                    var userAgentTask = page.SetUserAgentAsync(RandomUserAgentGenerator.GetRandomUserAgent());
                    if (page == null)
                    {
                        page = await GetBrowserInstanceAsync(true).Result.NewPageAsync();
                        page.DefaultNavigationTimeout = 30000;
                        page.DefaultTimeout = 30000;
                    }
                    var navigationTask = page.GoToAsync(url);

                    await Task.WhenAll(userAgentTask, navigationTask);
                    var response = await navigationTask;

                    if (response != null && response.Ok)
                    {
                        ControlledTimer.StartOperationTimer($"SCROLLING: {url}");
                        await ScrollPageToBottom(page);
                        ControlledTimer.StopOperationTimer($"SCROLLING: {url}");

                        ControlledTimer.StartOperationTimer($"CONTENT EXTRACTION: {url}");
                        var content = await page.GetContentAsync();
                        ControlledTimer.StopOperationTimer($"CONTENT EXTRACTION: {url}");

                        ControlledTimer.StopOperationTimer($"NAVIGATION ATTEMPT {attempt}: {url}");
                        return content;
                    }
                }
                catch (Exception ex)
                {
                    ControlledTimer.StopOperationTimer($"NAVIGATION ATTEMPT {attempt}: {url} (FAILED)");
                    if (attempt == maxRetries)
                    {
                        Console.WriteLine($"Failed to fetch page after {maxRetries} attempts: {ex}");
                    }

                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
                    await Task.Delay(delay);
                }
            }

            return string.Empty;
        }

        private static async Task<IBrowser> GetBrowserInstanceAsync(bool headless)
        {
            // Initialize browser fetcher once
            if (!_browserFetcherInitialized)
            {
                lock (_initLock)
                {
                    if (!_browserFetcherInitialized)
                    {
                        new BrowserFetcher().DownloadAsync().Wait();
                        _browserFetcherInitialized = true;
                    }
                }
            }

            // Try to get browser from pool
            if (_browserPool.TryTake(out var browser))
            {
                return browser;
            }

            // Wait for semaphore if we're at max browsers
            await _browserSemaphore.WaitAsync();

            try
            {
                // Check pool again in case browser was returned while waiting
                if (_browserPool.TryTake(out browser))
                {
                    return browser;
                }

                // Create new browser instance
                var options = new LaunchOptions
                {
                    Headless = headless,
                    Args = new[]
                    {
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-accelerated-2d-canvas",
                        "--disable-gpu",
                        "--window-size=1920x1080"
                    },
                    Timeout = 60000
                };

                return await Puppeteer.LaunchAsync(options);
            }
            catch
            {
                _browserSemaphore.Release();
                throw;
            }
        }

        private static void ReturnBrowserToPool(IBrowser browser)
        {
            if (!browser.IsClosed)
            {
                _browserPool.Add(browser);
            }
            _browserSemaphore.Release();
        }


        private static async Task ScrollPageToBottom(IPage page)
        {
            // First try a single jump to bottom (works for many pages)
            try
            {
                await page.EvaluateExpressionAsync(@"
                window.scrollTo(0, document.body.scrollHeight);
            ");
                await Task.Delay(200); // Short wait after jump
                return;
            }
            catch { /* Fall through to incremental scroll if needed */ }

            // Only use incremental scroll if needed
            await page.EvaluateExpressionAsync(@"
            new Promise(resolve => {
                const distance = 500; // Bigger chunks
                const delay = 100;    // Reduced frequency
                let lastPosition = 0;
                let samePositionCount = 0;
                
                const timer = setInterval(() => {
                    const scrollHeight = document.body.scrollHeight;
                    const currentPosition = window.scrollY;
                    
                    // Stop if we're at bottom or stuck
                    if (currentPosition + window.innerHeight >= scrollHeight || 
                        samePositionCount > 3) {
                        clearInterval(timer);
                        resolve();
                        return;
                    }
                    
                    // Track if we're stuck
                    if (currentPosition === lastPosition) {
                        samePositionCount++;
                    } else {
                        samePositionCount = 0;
                    }
                    lastPosition = currentPosition;
                    
                    window.scrollBy(0, distance);
                }, delay);
            });
        ");
        }

        private static async Task<IBrowser> InitializeBrowserAsync(bool headless)
        {
            await new BrowserFetcher().DownloadAsync();

            var options = new LaunchOptions
            {
                Headless = headless,
                Args = new[]
                {
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-accelerated-2d-canvas",
                        "--disable-gpu",
                        "--window-size=1920x1080"
                    },
                Timeout = 60000
            };

            Console.WriteLine("Launching browser...");

            var browser = await Puppeteer.LaunchAsync(options);

            Console.WriteLine("Browser launched.");
            return browser;
        }
    }
}