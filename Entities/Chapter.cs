using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Entities
{
    public class Chapter
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("title")]
        [MaxLength(255)]
        public string Title { get; set; }

        [Required]
        [Column("content")]
        [MaxLength(int.MaxValue)]
        public string Content
        {
            get => DecompressContent(_content);
            set => _content = CompressContent(value);
        }

        private string _content;

        
        [Required]
        [Column("index")]
        public int Index { get; set; }

        [Required]
        [Column("article_doi")]
        public string ArticleDoi { get; set; }

        [ForeignKey("ArticleDoi")]
        [JsonIgnore]
        public Article Article { get; set; }

        private static string CompressContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            byte[] bytes = Encoding.UTF8.GetBytes(content);
            using (var memoryStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal))
                {
                    gzipStream.Write(bytes, 0, bytes.Length);
                }
                return Convert.ToBase64String(memoryStream.ToArray());
            }
        }

        private static string DecompressContent(string compressedContent)
        {
            if (string.IsNullOrEmpty(compressedContent))
                return compressedContent;

            byte[] bytes = Convert.FromBase64String(compressedContent);
            using (var memoryStream = new MemoryStream(bytes))
            using (var outputStream = new MemoryStream())
            {
                using (var decompressStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    decompressStream.CopyTo(outputStream);
                }
                return Encoding.UTF8.GetString(outputStream.ToArray());
            }
        }
    }



}
