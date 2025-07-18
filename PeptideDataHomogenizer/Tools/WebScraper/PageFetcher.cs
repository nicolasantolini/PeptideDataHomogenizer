using Entities;
using Microsoft.AspNetCore.Mvc;
using PeptideDataHomogenizer.Data;
using PeptideDataHomogenizer.Services;
using PeptideDataHomogenizer.Tools.ElsevierTools;
using PeptideDataHomogenizer.Tools.HtmlTools;
using PeptideDataHomogenizer.Tools.WebScraper;
using PeptideDataHomogenizer.Tools.WileyTools;
using PuppeteerExtraSharp;
using PuppeteerExtraSharp.Plugins.ExtraStealth;
using PuppeteerSharp;

public interface IPageFetcher
{
    Task<(List<Chapter>,List<ExtractedTable>,List<ImageHolder>)> GetFullPageContentAsync(string url, int projectId, CancellationToken ct = default, string title = "");
    Task<List<(string,string,string)>> ExtractClassificationOrganismAndMethodAsync(string pdbId, CancellationToken ct = default);
    void Dispose();
}

public class PageFetcher : IPageFetcher, IDisposable
{
    private const int MaxPages = 5;

    private IBrowser _sharedBrowser;
    private readonly SemaphoreSlim _pageSemaphore = new(MaxPages, MaxPages);
    private bool _disposed;
    private Task<IBrowser> _browserInitializationTask;
    private SemaphoreSlim _browserInitializationSemaphore = new(1, 1);

    private IElsevierArticleFetcher _elsevierArticleFetcher;
    private ArticleExtractorFromHtml _articleExtractor;
    private WileyArticleDownloader _wileyArticleDownloader;
    private PDBContextDataExtractor _pdfContextDataExtractor;
    private DatabaseDataHandler DatabaseDataHandler;
    private PublishersService  _discreditedPublisherService;
    private HttpClient _http;

    public PageFetcher([FromServices] IElsevierArticleFetcher elsevierArticleFetcher, [FromServices] ArticleExtractorFromHtml articleExtractor, [FromServices] HttpClient http, [FromServices] WileyArticleDownloader wileyArticleDownloader, [FromServices] PDBContextDataExtractor pDBContextDataExtractor, [FromServices] DatabaseDataHandler databaseDataHandler, [FromServices] PublishersService publishersService)
    {
        Console.WriteLine("PageFetcher initialized.");
        _elsevierArticleFetcher = elsevierArticleFetcher;
        _articleExtractor = articleExtractor;
        _wileyArticleDownloader = wileyArticleDownloader;
        _pdfContextDataExtractor = pDBContextDataExtractor;
        DatabaseDataHandler = databaseDataHandler;
        _discreditedPublisherService = publishersService;
        _http = http;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    private async Task<IBrowser> InitializeSharedBrowserAsync()
    {
        if (_sharedBrowser != null && !_sharedBrowser.IsClosed)
            return _sharedBrowser;

        await _browserInitializationSemaphore.WaitAsync();
        try
        {
            if (_sharedBrowser == null || _sharedBrowser.IsClosed)
            {
                // Reset the task if browser is closed/disposed
                _browserInitializationTask = null;

                if (_browserInitializationTask == null)
                {
                    _browserInitializationTask = Task.Run(async () =>
                    {
                        try
                        {
                            var extra = new PuppeteerExtra();
                            extra.Use(new StealthPlugin());
                            await new BrowserFetcher().DownloadAsync();
                            var browser = await extra.LaunchAsync(new LaunchOptions
                            {
                                Headless = true,
                                Args = new[]
                                {
                                    "--no-sandbox",
                                    "--disable-setuid-sandbox",
                                    "--disable-dev-shm-usage",
                                    "--disable-accelerated-2d-canvas",
                                    "--disable-gpu",
                                    "--window-size=1080,720",
                                    "--disable-dev-shm-usage",
                                    "--disable-features=site-per-process",
                                    "--auto-open-devtools-for-tabs",
                                    "--disable-notifications"
                                },
                                Timeout = 300000,
                                Browser = SupportedBrowser.Chrome,
                                ExecutablePath = "/usr/bin/google-chrome"
                            });
                            return browser;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to initialize shared browser: {ex}");
                            throw;
                        }
                    });
                }

                _sharedBrowser = await _browserInitializationTask;
            }
        }
        finally
        {
            _browserInitializationSemaphore.Release();
        }

        return _sharedBrowser;
    }

    public async Task<(List<Chapter>,List<ExtractedTable>,List<ImageHolder>)> GetFullPageContentAsync(string url, int projectId, CancellationToken ct = default, string title = "")
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentNullException(nameof(url));

        // Initialize browser (thread-safe)
        if (_sharedBrowser == null || _sharedBrowser.IsClosed || _disposed)
        {
            _sharedBrowser = await InitializeSharedBrowserAsync();
        }
        while (_sharedBrowser.IsClosed)
        {
            Console.WriteLine("Browser is closed, reinitializing...");
            _sharedBrowser = await InitializeSharedBrowserAsync();
        }

        if (_disposed) throw new ObjectDisposedException(nameof(PageFetcher));

        await _pageSemaphore.WaitAsync(ct); // Respects CancellationToken
        try
        {
            using var page = await _sharedBrowser.NewPageAsync();
            await page.SetUserAgentAsync(RandomUserAgentGenerator.GetRandomUserAgent());

            // Configure request interception (optional)
            await page.SetRequestInterceptionAsync(true);
            page.Request += (sender, e) =>
            {
                if (e.Request.ResourceType == ResourceType.Image || e.Request.ResourceType == ResourceType.Font)
                    e.Request.AbortAsync();
                else
                    e.Request.ContinueAsync();
            };

            // Navigate with timeout (fallback to WaitUntil.Load if Networkidle0 hangs)
            try
            {
                await page.GoToAsync(url, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Load }, // More reliable than Networkidle0
                    Timeout = 60000 // 60s timeout
                });
            }
            catch (TimeoutException)
            {
                Console.WriteLine($"Timeout loading {url}");
                return (new List<Chapter>(), new List<ExtractedTable>(), new List<ImageHolder>()); // Return empty lists on timeout
            }


            await ScrollToBottomWithRetryAsync(page);
            await Task.Delay(1000);
            var chapters = new List<Chapter>();
            var tables = new List<ExtractedTable>();
            var images = new List<ImageHolder>();

            var discreditedPublishers = await _discreditedPublisherService.GetDiscreditedPublishersAsync(projectId);


            // Route to Elsevier/Wiley or generic scraper
            var finalUrl = page.Url;
            if (discreditedPublishers.Any(dp => finalUrl.Contains(dp.Url, StringComparison.OrdinalIgnoreCase)))
            {
                chapters = new List<Chapter>
                {
                    new Chapter
                    {
                        Title = "Discredited Publisher",
                        Content = "This publisher is discredited and not supported."
                    }
                };

            }
            else if(finalUrl.StartsWith("https://www.sciencedirect.com")){
                (chapters,tables,images) = await _elsevierArticleFetcher.GetFullTextByDoi(url.Replace("https://doi.org/", ""));}
            else if (finalUrl.StartsWith("https://onlinelibrary.wiley.com/"))
                (chapters, tables, images) = await _wileyArticleDownloader.DownloadArticleAsync(url, title);
            else
            {
                var content = await page.GetContentAsync();
                (chapters, tables, images) = await _articleExtractor.ExtractChaptersImagesAndTables(content, title,finalUrl);


                if (chapters.Count==1 && chapters[0].Title=="Full Text" && chapters[0].Content== "HtmlAgilityPack.HtmlNode")
                {
                    (chapters, tables,images) = await _elsevierArticleFetcher.GetFullTextByDoi(url.Replace("https://doi.org/", ""));
                }
            }

            if (!page.IsClosed)
            {
                await page.CloseAsync(); // Ensure page is closed after use
            }


            return (chapters,tables,images) ;
        }
        finally
        {
            _pageSemaphore.Release();
        }

    }

    public async Task<List<(string,string,string)>> ExtractClassificationOrganismAndMethodAsync(string pdbId, CancellationToken ct= default)
    {
        string url = $"https://www.rcsb.org/structure/{pdbId}";

        if (string.IsNullOrEmpty(url))
            throw new ArgumentNullException(nameof(url));

        // Initialize browser (thread-safe)
        _sharedBrowser ??= await InitializeSharedBrowserAsync();

        if (_disposed) throw new ObjectDisposedException(nameof(PageFetcher));

        await _pageSemaphore.WaitAsync(ct); // Respects CancellationToken
        try
        {
            using var page = await _sharedBrowser.NewPageAsync();
            await page.SetUserAgentAsync(RandomUserAgentGenerator.GetRandomUserAgent());

            // Configure request interception (optional)
            await page.SetRequestInterceptionAsync(true);
            page.Request += (sender, e) =>
            {
                if (e.Request.ResourceType == ResourceType.Image || e.Request.ResourceType == ResourceType.Font)
                    e.Request.AbortAsync();
                else
                    e.Request.ContinueAsync();
            };

            // Navigate with timeout (fallback to WaitUntil.Load if Networkidle0 hangs)
            try
            {
                await page.GoToAsync(url, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Load }, // More reliable than Networkidle0
                    Timeout = 60000 // 60s timeout
                });
            }
            catch (TimeoutException)
            {
                Console.WriteLine($"Timeout loading {url}");
                return new List<(string,string,string)>(); // Return empty list on timeout
            }


            var content = await page.GetContentAsync();


            var pdbData = _pdfContextDataExtractor.ExtractInfoFromHtml(content);


            if (!page.IsClosed)
            {
                await page.CloseAsync(); // Ensure page is closed after use
            }


            return pdbData;
        }
        finally
        {
            _pageSemaphore.Release();
        }
    }

    private static async Task ScrollToBottomWithRetryAsync(IPage page, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await ScrollToBottomAsync(page);
                return;
            }
            catch (EvaluationFailedException ex) when (ex.InnerException is MessageException)
            {
                if (i == maxRetries - 1) throw;
                await Task.Delay(1000 * (i + 1));
            }
        }
    }

    private static async Task ScrollToBottomAsync(IPage page)
    {
        try
        {
            // Single jump first
            await page.EvaluateExpressionAsync("window.scrollTo(0, document.body.scrollHeight);");
            await Task.Delay(200);
        }
        catch
        {
            // Proceed to incremental scroll on failure
        }

        await page.EvaluateExpressionAsync(@"
            new Promise((resolve, reject) => {
                const distance = 500;
                const delay = 100;
                let lastPosition = 0;
                let samePositionCount = 0;
                const timer = setInterval(() => {
                    try {
                        const scrollHeight = document.body.scrollHeight;
                        const currentPosition = window.scrollY;
                        
                        if (currentPosition + window.innerHeight >= scrollHeight || samePositionCount > 3) {
                            clearInterval(timer);
                            resolve();
                            return;
                        }
                        
                        if (currentPosition === lastPosition) {
                            samePositionCount++;
                        } else {
                            samePositionCount = 0;
                            lastPosition = currentPosition;
                        }
                        
                        window.scrollBy(0, distance);
                    } catch (error) {
                        clearInterval(timer);
                        reject(error);
                    }
                }, delay);
            });
        ");
    }

    private void OnProcessExit(object sender, EventArgs e)
    {
        Dispose();
    }

    public void Dispose()
    {

        if (_sharedBrowser != null)
        {
            _sharedBrowser.CloseAsync().ConfigureAwait(false) ;
            _sharedBrowser = null;
        }

        Console.WriteLine("BROWSER disposed.");
    }
}
