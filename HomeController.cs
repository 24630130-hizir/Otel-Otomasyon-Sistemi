using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using projeotel.Models;
using System.Diagnostics;
using System.Text;

namespace projeotel.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index() => View();
        public IActionResult Hakkimizda() => View();
        public IActionResult Iletisim() => View();

        public async Task<IActionResult> OtelleriListele()
        {
            try
            {
                var client = new HttpClient();
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("https://hotels4.p.rapidapi.com/properties/v2/list"),
                    Headers =
                    {
                        { "x-rapidapi-key", "af9b9561a6msh12f452b04615d2ep16ab25jsn945e7362e18a" },
                        { "x-rapidapi-host", "hotels4.p.rapidapi.com" },
                    }
                };


                var jsonBody = "{\"destinations\":[{\"regionId\":\"6054451\"}],\"checkInDate\":{\"day\":10,\"month\":5,\"year\":2026},\"checkOutDate\":{\"day\":15,\"month\":5,\"year\":2026},\"rooms\":[{\"adults\":2}],\"resultsStartingIndex\":0,\"resultsSize\":20,\"sort\":\"PRICE_RELEVANT\"}";
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                using (var response = await client.SendAsync(request))
                {
                    response.EnsureSuccessStatusCode();
                    var body = await response.Content.ReadAsStringAsync();

                    
                    Debug.WriteLine("API Response: " + body);

                    var veri = JsonConvert.DeserializeObject<dynamic>(body);
                    var otelListesi = veri?.data?.propertySearch?.properties;

                    if (otelListesi == null)
                    {
                        ViewBag.Hata = "Maalesef þu an kriterlere uygun otel bulunamadý.";
                    }

                    return View(otelListesi);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"API Hatasý: {ex.Message}");
                ViewBag.Hata = "Baðlantý sýrasýnda bir hata oluþtu: " + ex.Message;
                return View(new List<dynamic>());
            }
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}