namespace PeptideDataHomogenizer.Tools.NotCurrentlyInUse
{

    using System;
    using System.IO;
    using System.Net;
    using System.IO.Compression;
    using System.Collections.Generic;
    using System.Linq;
    using System.Formats.Tar;

    public class PMCBulkDownloader
    {
        private readonly string _ftpHost = "ftp.ncbi.nlm.nih.gov";
        private readonly string _ftpUsername = "anonymous";
        private readonly string _ftpPassword = "your@email.com";

        // Base paths for different datasets
        private const string OABasePath = "/pub/pmc/oa_bulk";
        private const string ManuscriptBasePath = "/pub/pmc/manuscript";
        private const string HistoricalBasePath = "/pub/pmc/historical_ocr";

        public void DownloadBulkPackages(DownloadOptions options)
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.Credentials = new NetworkCredential(_ftpUsername, _ftpPassword);

                    // Determine base path based on dataset
                    string basePath = options.DatasetType switch
                    {
                        DatasetType.OA_Commercial => $"{OABasePath}/oa_comm",
                        DatasetType.OA_NonCommercial => $"{OABasePath}/oa_noncomm",
                        DatasetType.OA_Other => $"{OABasePath}/oa_other",
                        DatasetType.AuthorManuscript => ManuscriptBasePath,
                        DatasetType.HistoricalOCR => HistoricalBasePath,
                        _ => throw new ArgumentException("Invalid dataset type")
                    };

                    // Add content type subdirectory
                    string contentPath = $"{basePath}/{options.ContentType.ToString().ToLower()}";

                    // Get file list from FTP
                    var fileList = GetFileList(client, contentPath, options);

                    // Download files
                    foreach (var file in fileList)
                    {
                        DownloadFile(client, file, options.OutputDirectory);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private List<string> GetFileList(WebClient client, string contentPath, DownloadOptions options)
        {
            List<string> files = new List<string>();

            // Get directory listing
            string directoryUri = $"ftp://{_ftpHost}{contentPath}/";
            string[] directoryListing = client.DownloadString(directoryUri).Split(
                new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // Filter files based on options
            foreach (var entry in directoryListing)
            {
                string fileName = entry.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Last();

                // Skip file lists if not requested
                if (fileName.Contains("filelist") && !options.IncludeFileLists)
                    continue;

                // Filter by package type (baseline/incremental)
                if (options.PackageType == PackageType.Baseline && !fileName.Contains("baseline"))
                    continue;
                if (options.PackageType == PackageType.Incremental && !fileName.Contains("incr"))
                    continue;

                // Filter by date range
                if (options.StartDate.HasValue || options.EndDate.HasValue)
                {
                    DateTime? fileDate = ExtractDateFromFileName(fileName);
                    if (fileDate.HasValue)
                    {
                        if (options.StartDate.HasValue && fileDate < options.StartDate)
                            continue;
                        if (options.EndDate.HasValue && fileDate > options.EndDate)
                            continue;
                    }
                }

                // Filter by PMCID range if specified
                if (!string.IsNullOrEmpty(options.PMCIDRange) &&
                    fileName.Contains(options.PMCIDRange))
                    continue;

                files.Add($"{directoryUri}{fileName}");
            }

            return files;
        }

        private DateTime? ExtractDateFromFileName(string fileName)
        {
            try
            {
                // Extract date from patterns like "2021-09-16"
                var dateParts = fileName.Split('.')
                    .FirstOrDefault(part => part.Length == 10 && part.Contains('-'));

                if (dateParts != null && DateTime.TryParse(dateParts, out var date))
                {
                    return date;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private void DownloadFile(WebClient client, string fileUri, string outputDirectory)
        {
            string fileName = Path.GetFileName(fileUri);
            string localPath = Path.Combine(outputDirectory, fileName);

            Console.WriteLine($"Downloading {fileName}...");
            client.DownloadFile(fileUri, localPath);

            // Decompress if it's a .tar.gz file
            if (fileName.EndsWith(".tar.gz"))
            {
                Console.WriteLine($"Extracting {fileName}...");
                ExtractTarGz(localPath, outputDirectory);
            }
        }

        private void ExtractTarGz(string gzArchiveName, string destFolder)
        {
            // Create destination directory if it doesn't exist
            Directory.CreateDirectory(destFolder);

            // First decompress .gz to .tar
            string tempTarFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            try
            {
                using (var fs = new FileStream(gzArchiveName, FileMode.Open))
                using (var gzipStream = new GZipStream(fs, CompressionMode.Decompress))
                using (var tempTarStream = File.Create(tempTarFile))
                {
                    gzipStream.CopyTo(tempTarStream);
                }

                // Now extract the .tar file
                ExtractTar(tempTarFile, destFolder);
            }
            finally
            {
                if (File.Exists(tempTarFile))
                    File.Delete(tempTarFile);
            }
        }

        private void ExtractTar(string tarArchiveName, string destFolder)
        {
            // Note: .NET doesn't have built-in tar support until .NET 7
            // For earlier versions, you'd need a third-party library or custom implementation
            // This is a simplified example

            // In .NET 7+ you could use TarFile.ExtractToDirectory
            if (Environment.Version.Major >= 7)
            {
                TarFile.ExtractToDirectory(tarArchiveName, destFolder, true);
            }
            else
            {
                Console.WriteLine("Tar extraction requires .NET 7+ or a third-party library.");
                Console.WriteLine($"Please manually extract {tarArchiveName} to {destFolder}");
            }
        }
    }

    public class DownloadOptions
    {
        public DatasetType DatasetType { get; set; }
        public ContentType ContentType { get; set; }
        public PackageType PackageType { get; set; }
        public string OutputDirectory { get; set; }
        public bool IncludeFileLists { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string PMCIDRange { get; set; }
    }

    public enum DatasetType
    {
        OA_Commercial,
        OA_NonCommercial,
        OA_Other,
        AuthorManuscript,
        HistoricalOCR
    }

    public enum ContentType
    {
        XML,
        TXT
    }

    public enum PackageType
    {
        Baseline,
        Incremental,
        All
    }

    // Example usage
    class Program
    {
        static void Main()
        {
            
        }
    }
}
