using Microsoft.JSInterop;
using System.Security.Cryptography;
using System.Text;

namespace PeptideDataHomogenizer.State
{
    public static class ProjectCookieManager
    {
        private const string CookieName = "project";
        // Use a key for encryption/decryption. In production, store securely.
        private static readonly byte[] EncryptionKey = Encoding.UTF8.GetBytes("JCIYCL9nZTgY5OmPKWXNNoHUEmUDOSuV"); // 24+ chars for AES

        public static async Task SetProjectAsync(IJSRuntime jsRuntime, int organizationId, int days = 7)
        {
            var encryptedValue = Encrypt(organizationId.ToString());
            await jsRuntime.InvokeVoidAsync("methods.CreateCookie", CookieName, encryptedValue, days);
        }

        public static async Task<int?> GetProjectAsync(IJSRuntime jsRuntime)
        {
            var encryptedValue = await jsRuntime.InvokeAsync<string>("methods.GetCookie", CookieName);
            if (string.IsNullOrEmpty(encryptedValue))
                return null;
            try
            {
                var decrypted = Decrypt(encryptedValue);
                if (int.TryParse(decrypted, out var orgId))
                    return orgId;
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = EncryptionKey;
            aes.GenerateIV();
            var iv = aes.IV;

            using var encryptor = aes.CreateEncryptor(aes.Key, iv);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // Combine IV and encrypted bytes
            var result = new byte[iv.Length + encryptedBytes.Length];
            Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
            Buffer.BlockCopy(encryptedBytes, 0, result, iv.Length, encryptedBytes.Length);

            return Convert.ToBase64String(result);
        }

        private static string Decrypt(string encryptedText)
        {
            var fullCipher = Convert.FromBase64String(encryptedText);

            using var aes = Aes.Create();
            aes.Key = EncryptionKey;

            // Extract IV
            var iv = new byte[aes.BlockSize / 8];
            var cipher = new byte[fullCipher.Length - iv.Length];
            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            var decryptedBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

            return Encoding.UTF8.GetString(decryptedBytes);
        }
    }
}
