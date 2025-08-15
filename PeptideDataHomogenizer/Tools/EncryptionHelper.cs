using System.Security.Cryptography;
using System.Text;

namespace PeptideDataHomogenizer.Tools
{
    public class EncryptionHelper
    {
        private byte[] EncryptionKey;
        public EncryptionHelper(byte[] encryptionKey)
        {
            EncryptionKey = encryptionKey;
        }


        public string Encrypt(string plainText)
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

        public string Decrypt(string encryptedText)
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
