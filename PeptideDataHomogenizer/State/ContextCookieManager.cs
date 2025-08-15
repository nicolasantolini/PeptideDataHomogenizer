using Microsoft.AspNetCore.Mvc;
using Microsoft.JSInterop;
using PeptideDataHomogenizer.Tools;
using System.Security.Cryptography;
using System.Text;

namespace PeptideDataHomogenizer.State
{
    public class ContextCookieManager
    {
        private const string CookieName = "context";

        private EncryptionHelper _encryptionHelper;

        public ContextCookieManager([FromServices] EncryptionHelper encryptionHelper)
        {
            _encryptionHelper = encryptionHelper;
        }

        public async Task SetContextAsync(IJSRuntime jsRuntime, int organizationId, int days = 7)
        {
            var encryptedValue = _encryptionHelper.Encrypt(organizationId.ToString());
            await jsRuntime.InvokeVoidAsync("methods.CreateCookie", CookieName, encryptedValue, days);
        }

        public async Task<int?> GetContextAsync(IJSRuntime jsRuntime)
        {
            var encryptedValue = await jsRuntime.InvokeAsync<string>("methods.GetCookie", CookieName);
            if (string.IsNullOrEmpty(encryptedValue))
                return null;
            try
            {
                var decrypted = _encryptionHelper.Decrypt(encryptedValue);
                if (int.TryParse(decrypted, out var orgId))
                    return orgId;
                return null;
            }
            catch
            {
                return null;
            }
        }

    }
}
