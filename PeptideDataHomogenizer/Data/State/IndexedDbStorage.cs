namespace PeptideDataHomogenizer.Data.State
{
    using Microsoft.JSInterop;
    using System.Threading.Tasks;

    public class IndexedDbStorage
    {
        private readonly IJSRuntime _jsRuntime;

        public IndexedDbStorage(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task SaveAsync<T>(string key, T data)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("indexedDbStorage.save", key, data);
            }
            catch (JSException ex)
            {
                Console.WriteLine($"Failed to save to IndexedDB: {ex.Message}");
                throw;
            }
        }

        public async Task<T?> LoadAsync<T>(string key)
        {
            try
            {
                return await _jsRuntime.InvokeAsync<T?>("indexedDbStorage.load", key);
            }
            catch (JSException ex)
            {
                Console.WriteLine($"Failed to load from IndexedDB: {ex.Message}");
                return default;
            }
        }

        public async Task ClearAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("indexedDbStorage.clear");
            }
            catch (JSException ex)
            {
                Console.WriteLine($"Failed to clear IndexedDB: {ex.Message}");
                throw;
            }
        }
    }
}
