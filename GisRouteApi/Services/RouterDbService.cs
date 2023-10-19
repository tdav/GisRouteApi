using AsbtCore.UtilsV2;
using GisRouteApi.Models;
using Itinero;
using Itinero.Algorithms.Weights;
using Itinero.IO.Osm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Drawing;
using System.IO;


namespace GisRouteApi.Services
{
    public interface IRouterDbService
    {
        Answere<Response> Calculate(PointF cbegin, PointF cend);
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

        public Answere<Response> Calculate(PointF cbegin, PointF cend)
        {
            try
            {
                var router = new Router(routerDb);
                var profile = Itinero.Osm.Vehicles.Vehicle.Car.Fastest(); // the default OSM car profile.                

                var start = router.Resolve(profile, cbegin.X, cbegin.Y);// 41.259976f, 69.199349f);
                var end = router.Resolve(profile, cend.X, cend.Y); // 41.364306f, 69.264752f);

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
    }
}
