using iTextSharp.text.pdf.parser;
using iTextSharp.text.pdf;
using System.Text;
using iTextSharp.text;

namespace PeptideDataHomogenizer.Tools.PdfTools
{
    public static class PdfCreationTools
    {

        public static string ConvertPdfToHtml(byte[] pdfBytes)
        {
            string randomOutputPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString() + ".pdf");
            try
            {
                CreatePDFFromByteArray(pdfBytes, randomOutputPath);

                var htmlContent = new StringBuilder();
                using (var reader = new PdfReader(randomOutputPath))
                {
                    for (int i = 1; i <= reader.NumberOfPages; i++)
                    {
                        var text = PdfTextExtractor.GetTextFromPage(reader, i);
                        htmlContent.AppendLine($"<h1>Page {i}</h1>");
                        htmlContent.AppendLine($"<p>{text}</p>");
                    }
                }
                return htmlContent.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing PDF: {ex.Message}");
                throw;
            }
            finally
            {
                if (File.Exists(randomOutputPath))
                {
                    try { File.Delete(randomOutputPath); } catch { /* Ignore deletion errors */ }
                }
            }
        }

        private static void CreatePDFFromByteArray(byte[] byteArray, string outputPath)
        {
            using var inputStream = new MemoryStream(byteArray);
            using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var document = new Document();
            var writer = PdfWriter.GetInstance(document, outputStream);

            if (!document.IsOpen())
            {
                document.Open();
            }

            using (var reader = new PdfReader(inputStream))
            {
                for (int pageNumber = 1; pageNumber <= reader.NumberOfPages; pageNumber++)
                {
                    var pageSize = reader.GetPageSize(pageNumber);
                    document.SetPageSize(pageSize);
                    document.NewPage();
                    var importedPage = writer.GetImportedPage(reader, pageNumber);
                    writer.DirectContent.AddTemplate(importedPage, 0, 0);
                }
            }
        }
    }
}
