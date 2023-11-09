using AsbtCore.UtilsV2;
using GeocodingApi;
using GisRouteApi.Models;
using Itinero;
using Itinero.Algorithms.Networks;
using Itinero.Algorithms.Search.Hilbert;
using Itinero.Algorithms.Weights;
using Itinero.IO.Osm;
using Itinero.LocalGeo;
using Itinero.Profiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace GisRouteApi.Services
{
    public interface IRouterDbService
    {
        Answere<Response> Calculate(Request<float> req);
        ValueTask<Answere<AddressModel>> GetAddressAsync(string lat, string lon);
        ValueTask<Answere<OsrmResponseModel>> GetRouteByOsrmAsync(Request<double> req);
    }

    public class RouterDbService : IRouterDbService
    {
        private readonly string RouterDbPath;
        private readonly RouterDb routerDb;
        private readonly ILogger<RouterDbService> logger;
        private readonly HttpClient client;

        private readonly string Url;
        private readonly string AddressUrl;

        public RouterDbService(IConfiguration conf, ILogger<RouterDbService> logger, IHttpClientFactory clientFactory)
        {
            this.logger = logger;
            string mapPath = AppDomain.CurrentDomain.BaseDirectory + conf["MapName"];

            client = clientFactory.CreateClient("RouterDbService");

            routerDb = new RouterDb();
            RouterDbPath = $"{AppDomain.CurrentDomain.BaseDirectory}router_database.db";

            if (File.Exists(RouterDbPath))
            {
                using (var stream = new FileInfo(RouterDbPath).Open(FileMode.Open))
                {
                    routerDb = RouterDb.Deserialize(stream);

                    routerDb.Network.Sort();
                    routerDb.OptimizeNetwork();
                    routerDb.Network.Compress();
                }
            }
            else
            {
                using (var stream = new FileInfo(mapPath).OpenRead())
                {
                    routerDb.LoadOsmData(stream, Itinero.Osm.Vehicles.Vehicle.Car);
                }

                using (var stream = new FileInfo(RouterDbPath).Open(FileMode.Create))
                {
                    routerDb.Serialize(stream);
                }
            }

            Url = conf["Url"];
            AddressUrl = conf["AddressUrl"];
        }

        public Answere<Response> Calculate(Request<float> req)
        {
            try
            {
                var profile = Itinero.Osm.Vehicles.Vehicle.Car.Fastest(); // the default OSM car profile.             
                var router = new Router(routerDb);

                var start = router.Resolve(profile, req.Begin.Latitude, req.Begin.Longitude);// 41.259976f, 69.199349f);               
                var end = router.Resolve(profile, req.End.Latitude, req.End.Longitude); // 41.364306f, 69.264752f);
                var route = router.Calculate(profile, start, end);
                
                var json = route.ToGeoJson();

                var res = json.FromJson<Response>();
                res.TotalDistance = route.TotalDistance;

                return new Answere<Response>(1, "", "", res);
            }
            catch (Exception ex)
            {
                logger.LogError("RouterDbService.Calculate error: {0}", ex.GetAllMessages());
                return new Answere<Response>(0, "Ошибка при калькуляции", ex.Message);
            }
        }

        public async ValueTask<Answere<OsrmResponseModel>> GetRouteByOsrmAsync(Request<double> req)
        {
            try
            {
                string x1 = req.Begin.Longitude.ToInvariantString();
                string x2 = req.Begin.Latitude.ToInvariantString();
                string y1 = req.End.Longitude.ToInvariantString();
                string y2 = req.End.Latitude.ToInvariantString();

                string url = string.Format(Url, x1, x2, y1, y2);
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("Accept-Language", "ru-RU");

                var res = await client.SendAsync(request);
                var js = await res.Content.ReadAsStringAsync();

                var model = js.FromJson<OsrmResponseModel>();

                var startAdr = await GetAddressAsync(x2, x1);
                await Task.Delay(1000);
                var endAdr = await GetAddressAsync(y2, y1);

                model.StartAddress = startAdr.Data;
                model.EndAddress = endAdr.Data;

                return new Answere<OsrmResponseModel>(model);
            }
            catch (Exception ex)
            {
                logger.LogError("RouterDbService.GetByOsrmAsync error: {0}", ex.GetAllMessages());
                return new Answere<OsrmResponseModel>(0, "Ошибка при калькуляции", ex.Message);

            }
        }

        public async ValueTask<Answere<AddressModel>> GetAddressAsync(string lat, string lon)
        {
            try
            {
                string url = string.Format(AddressUrl, lat, lon);
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Accept", "*/*");
                request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                request.Headers.Add("Accept-Language", "ru-RU");
                request.Headers.Add("User-Agent", "C# App");

                var res = await client.SendAsync(request);
                var js = await res.Content.ReadAsStringAsync();

                var model = js.FromJson<AddressModel>();
                return new Answere<AddressModel>(1, "OK", "", model);
            }
            catch (Exception ex)
            {
                logger.LogError("RouterDbService.GetAddressAsync error: {0}", ex.GetAllMessages());
                return new Answere<AddressModel>(0, "Ошибка при получении адреса", ex.Message);
            }
        }
    }
}
