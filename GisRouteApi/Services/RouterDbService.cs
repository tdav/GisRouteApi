using AsbtCore.UtilsV2;
using GisRouteApi.Models;
using Itinero;
using Itinero.Algorithms.Networks;
using Itinero.Algorithms.Search.Hilbert;
using Itinero.Algorithms.Weights;
using Itinero.IO.Osm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace GisRouteApi.Services
{
    public interface IRouterDbService
    {
        Answere<Response> Calculate(Request<float> req);
        ValueTask<Answere<OsrmResponseModel>> GetRouteByOsrmAsync(Request<double> req);
    }

    public class RouterDbService : IRouterDbService
    {
        private readonly string RouterDbPath;
        private readonly RouterDb routerDb;
        private readonly ILogger<RouterDbService> logger;

        public RouterDbService(IConfiguration conf, ILogger<RouterDbService> logger)
        {
            this.logger = logger;
            string mapPath = AppDomain.CurrentDomain.BaseDirectory + conf["MapName"];

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

                using var cl = new HttpClient();
                cl.DefaultRequestHeaders.Add("Accept", "application/json");

                string url = $"http://router.project-osrm.org/route/v1/driving/{y1},{y2};{x1},{x2}?overview=false";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                var res = await cl.SendAsync(request);
                var js = await res.Content.ReadAsStringAsync();

                var model = js.FromJson<OsrmResponseModel>();

                return new Answere<OsrmResponseModel>(model);
            }
            catch (Exception ex)
            {
                logger.LogError("RouterDbService.GetByOsrmAsync error: {0}", ex.GetAllMessages());
                return new Answere<OsrmResponseModel>(0, "Ошибка при калькуляции", ex.Message);

            }
        }        
    }
}
