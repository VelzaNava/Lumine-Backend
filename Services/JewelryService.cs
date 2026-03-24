using Lumine.Backend.Models;

namespace Lumine.Backend.Services
{
    // service na nag-hahandle ng lahat ng DB operations para sa jewelry catalog
    public class JewelryService
    {
        private readonly SupabaseService _supabaseService;

        // i-inject yung supabase service — dito kukuha ng client
        public JewelryService(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        // kunin lahat ng jewelry items galing sa Supabase — walang filter, buong catalog
        public async Task<List<Jewelry>> GetAllJewelryAsync()
        {
            var client = _supabaseService.GetClient();
            var response = await client
                .From<Jewelry>()
                .Get();

            return response.Models;
        }

        // hanapin yung isang jewelry by ID — mag-return ng null kung wala
        public async Task<Jewelry?> GetJewelryByIdAsync(Guid id)
        {
            var client = _supabaseService.GetClient();
            var response = await client
                .From<Jewelry>()
                .Where(x => x.Id == id)
                .Single();

            return response;
        }

        // mag-insert ng bagong jewelry sa DB tapos i-return yung created record
        public async Task<Jewelry> CreateJewelryAsync(Jewelry jewelry)
        {
            var client = _supabaseService.GetClient();
            var response = await client
                .From<Jewelry>()
                .Insert(jewelry);

            return response.Models.First();
        }

        // i-update yung existing jewelry record tapos i-return yung updated version
        public async Task<Jewelry> UpdateJewelryAsync(Jewelry jewelry)
        {
            var client = _supabaseService.GetClient();
            var response = await client
                .From<Jewelry>()
                .Update(jewelry);

            return response.Models.First();
        }

        // i-delete yung jewelry by ID — walang return, basta tanggalin na
        public async Task DeleteJewelryAsync(Guid id)
        {
            var client = _supabaseService.GetClient();
            await client
                .From<Jewelry>()
                .Where(x => x.Id == id)
                .Delete();
        }
    }
}
