using Itinero;
using Itinero.IO.Osm;
using Itinero.Osm.Vehicles;
using System.Drawing;

namespace GisRouteApi.Services
{
    public interface IRouterDbService
    {
        string Calculate(PointF cbegin, PointF cend);
    }

    public class RouterDbService : IRouterDbService
    {
        private readonly string RouterDbPath;
        private readonly RouterDb routerDb;

        public RouterDbService()
        {
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
                using (var stream = new FileInfo(AppDomain.CurrentDomain.BaseDirectory + @"OsmMap\uzbekistan-latest.osm.pbf").OpenRead())
                {
                    routerDb.LoadOsmData(stream, Vehicle.Car);
                }

                using (var stream = new FileInfo(RouterDbPath).Open(FileMode.Create))
                {
                    routerDb.Serialize(stream);
                }
            }
        }

        public string Calculate(PointF cbegin, PointF cend)
        {
            var router = new Router(routerDb);
            var profile = Vehicle.Car.Fastest(); // the default OSM car profile.

            var start = router.Resolve(profile, cbegin.X, cbegin.Y);// 41.259976f, 69.199349f);
            var end = router.Resolve(profile, cend.X, cend.Y); // 41.364306f, 69.264752f);

            var route = router.Calculate(profile, start, end);
            return route.ToGeoJson();
        }
    }
}
