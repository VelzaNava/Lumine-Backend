using Supabase;

namespace Lumine.Backend.Services
{
    // singleton service na nag-manage ng connection sa Supabase — shared sa buong app
    public class SupabaseService
    {
        private readonly Client _client;

        // i-setup yung Supabase client gamit yung URL at key galing sa config
        public SupabaseService(IConfiguration configuration)
        {
            var url = configuration["Supabase:Url"]
                ?? throw new Exception("Supabase URL not configured");

            var key = configuration["Supabase:Key"]
                ?? throw new Exception("Supabase Key not configured");

            // realtime hindi na kailangan dito — REST lang ang ginagamit natin
            var options = new SupabaseOptions
            {
                AutoConnectRealtime = false
            };

            _client = new Client(url, key, options);
        }

        // i-initialize yung connection sa Supabase — dapat tawagin sa startup ng app
        public async Task InitializeAsync()
        {
            await _client.InitializeAsync();
        }

        // i-return yung client instance — ginagamit ng ibang services
        public Client GetClient()
        {
            return _client;
        }

        // property shortcut para sa Client — para mas convenient ang access
        public Client Client => _client;
    }
}
