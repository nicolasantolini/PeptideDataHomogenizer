namespace PeptideDataHomogenizer.Tools.NotCurrentlyInUse
{
    using System;
    using System.IO;
    using System.Net;
    using System.Linq;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Formats.Tar;
    using System.IO.Compression;

    public class PMCArticleDownloader
    {
        private const string FtpHost = "ftp.ncbi.nlm.nih.gov";
        private const string FtpUsername = "anonymous";
        private const string FtpPassword = "your@email.com";
        private const string FileListPath = "/pub/pmc/oa_file_list.csv";
        private const string BaseFtpPath = "/pub/pmc";

        public void DownloadArticleByPmid(int pmid, string outputDirectory)
        {
            try
            {
                // Step 1: Download and parse the file list
                var articleInfo = FindArticleInFileList(pmid);

                if (articleInfo == null)
                {
                    Console.WriteLine($"Article with PMID {pmid} not found in PMC Open Access Subset.");
                    return;
                }

                Console.WriteLine($"Found article: {articleInfo.Value.Citation} (PMID: {articleInfo.Value.PMID})");

                // Step 2: Download the article package
                DownloadArticlePackage(articleInfo.Value.FtpPath, outputDirectory);

                Console.WriteLine($"Successfully downloaded article PMID {pmid} to {outputDirectory}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading article: {ex.Message}");
            }
        }

        private ArticleInfo? FindArticleInFileList(int pmid)
        {
            using (var client = new WebClient())
            {
                // Download the file list
                string fileListContent = client.DownloadString($"ftp://{FtpHost}{FileListPath}");

                // Parse CSV content
                var lines = fileListContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                // Skip header line (timestamp)
                foreach (var line in lines.Skip(1))
                {
                    var fields = ParseFileListLine(line);

                    if (fields.Length >= 6 && int.TryParse(fields[4], out int currentPmid) && currentPmid == pmid)
                    {
                        return new ArticleInfo
                        {
                            FtpPath = fields[0],
                            Citation = fields[1],
                            PMCID = fields[2],
                            LastUpdated = fields[3],
                            PMID = pmid,
                            License = fields[5],
                            IsRetracted = fields.Length > 6 ? fields[6] == "yes" : false
                        };
                    }
                }
            }

            return null;
        }

        private string[] ParseFileListLine(string line)
        {
            // Handle both CSV and tab-delimited formats
            if (line.Contains('\t'))
            {
                return line.Split('\t');
            }

            // For CSV, we need to handle quoted fields that might contain commas
            var pattern = ",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))";
            return Regex.Split(line, pattern)
                       .Select(f => f.Trim('"'))
                       .ToArray();
        }

        private void DownloadArticlePackage(string ftpRelativePath, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);

            string fileName = Path.GetFileName(ftpRelativePath);
            string localPath = Path.Combine(outputDirectory, fileName);
            string ftpPath = $"ftp://{FtpHost}{BaseFtpPath}/{ftpRelativePath.TrimStart('/')}";

            using (var client = new WebClient())
            {
                client.Credentials = new NetworkCredential(FtpUsername, FtpPassword);
                Console.WriteLine($"Downloading package from {ftpPath}...");
                client.DownloadFile(ftpPath, localPath);
            }

            // Extract the package
            Console.WriteLine($"Extracting package {fileName}...");
            ExtractTarGz(localPath, outputDirectory);

            // Optionally delete the archive after extraction
            File.Delete(localPath);
        }

        private void ExtractTarGz(string gzArchiveName, string destFolder)
        {
            string tempTarFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            try
            {
                // Decompress .gz to .tar
                using (var fs = new FileStream(gzArchiveName, FileMode.Open))
                using (var gzipStream = new GZipStream(fs, CompressionMode.Decompress))
                using (var tempTarStream = File.Create(tempTarFile))
                {
                    gzipStream.CopyTo(tempTarStream);
                }

                // Extract .tar
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
            // .NET 7+ has built-in tar support
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

    public struct ArticleInfo
    {
        public string FtpPath { get; set; }
        public string Citation { get; set; }
        public string PMCID { get; set; }
        public string LastUpdated { get; set; }
        public int PMID { get; set; }
        public string License { get; set; }
        public bool IsRetracted { get; set; }
    }

}
