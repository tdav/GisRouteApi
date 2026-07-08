using AsbtCore.UtilsV2;
using GisRouteApi.Models;
using Itinero;
using Itinero.Algorithms.Networks;
using Itinero.Algorithms.Weights;
using Itinero.IO.Osm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using NetTopologySuite.IO;
using NetTopologySuite.Geometries;
using Itinero.Exceptions;

namespace GisRouteApi.Services
{
    public interface IRouterDbService
    {
        Answere<Response> Calculate(Request<float> req);
        ValueTask<Answere<AddressModel>> GetAddressAsync(string lat, string lon);
        Answere<int> GetAreaIdByCoordinates(double longitude, double latitude);
        ValueTask<Answere<OsrmResponseModel>> GetRouteByOsrmAsync(Request<double> req);
    }

    public class RouterDbService : IRouterDbService
    {
        private readonly string RouterDbPath;
        private readonly RouterDb routerDb;
        private readonly ILogger<RouterDbService> logger;
        private readonly HttpClient client;
        private readonly GeometryFactory GFactory;
        private readonly string ShapefilePath;

        private readonly string Url;
        private readonly string AddressUrl;

        private readonly int StartRoadSearch;
        private readonly int EndRoadSearch;

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

                    //routerDb.Sort();
                    routerDb.OptimizeNetwork(0);
                    //routerDb.Compress();

                    //routerDb.Network.Sort();
                    //routerDb.Network.Compress();
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

            StartRoadSearch = Convert.ToInt32(conf["StartRoadSearch"]);
            EndRoadSearch = Convert.ToInt32(conf["EndRoadSearch"]);

            ShapefilePath = AppDomain.CurrentDomain.BaseDirectory + conf["ShapeFileUrl"];
            GFactory = new GeometryFactory();
        }

        public Answere<Response> Calculate(Request<float> req)
        {
            try
            {
                var profile = Itinero.Osm.Vehicles.Vehicle.Car.Fastest(); // the default OSM car profile                
                var router = new Router(routerDb);

                var start = router.Resolve(profile, req.Begin.Latitude, req.Begin.Longitude, StartRoadSearch);// 41.259976f, 69.199349f);               
                var end = router.Resolve(profile, req.End.Latitude, req.End.Longitude , EndRoadSearch); // 41.364306f, 69.264752f);
                var route = router.Calculate(profile, start, end);

                var json = route.ToGeoJson();

                var res = json.FromJson<Response>();
                res.TotalDistance = route.TotalDistance;

                return new Answere<Response>(1, "", "", res);
            }
            catch (RouteNotFoundException rnfEx)
            {
                logger.LogError("RouterDbService.Calculate(RouteNotFoundException) error: {0}", rnfEx.GetAllMessages());

                // Вычисление приблизительного расстояния по прямой линии
                var distance = CalculateStraightLineDistance(req.Begin.Latitude, req.Begin.Longitude, req.End.Latitude, req.End.Longitude);
                var res = new Response { TotalDistance = distance + 500 };

                return new Answere<Response>(1, "Маршрут не найден, возвращено приблизительное расстояние", "", res);
            }
            catch (ResolveFailedException re)
            {
                double minDistanceMeters = GetDistanceMeters(req.Begin.Latitude, req.Begin.Longitude, req.End.Latitude, req.End.Longitude);
                logger.LogError("RouterDbService.Calculate(ResolveFailedException) error: {0}", re.GetAllMessages());
                return new Answere<Response>(new Response { TotalDistance = minDistanceMeters.ToInt()});
            }
            catch (Exception ex)
            {
                logger.LogError("RouterDbService.Calculate error: {0} model: {1}", ex.GetAllMessages(), req.ToJson());
                return new Answere<Response>(0, "Ошибка при калькуляции", ex.Message);
            }
        }

        private float CalculateStraightLineDistance(float lat1, float lon1, float lat2, float lon2)
        {
            var R = 6371e3;
            var f1 = lat1 * Math.PI / 180;
            var f2 = lat2 * Math.PI / 180;
            var df = (lat2 - lat1) * Math.PI / 180;
            var dl = (lon2 - lon1) * Math.PI / 180;

            var a = Math.Sin(df / 2) * Math.Sin(df / 2) +
                    Math.Cos(f1) * Math.Cos(f2) *
                    Math.Sin(dl / 2) * Math.Sin(dl / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            var distance = (float)(R * c);

            return distance;
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

        static int FindAdministrativeNameFieldIndex(ShapefileDataReader reader, string fieldName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            throw new ArgumentException($"Поле с именем '{fieldName}' не найдено в схеме данных.");
        }

        public Answere<int> GetAreaIdByCoordinates(double longitude, double latitude)
        {
            try
            {
                using var ShDataReader = new ShapefileDataReader(ShapefilePath, GFactory);
                var point = GFactory.CreatePoint(new Coordinate(longitude, latitude));
                while (ShDataReader.Read())
                {
                    var administrativeArea = ShDataReader.Geometry;

                    if (administrativeArea.Contains(point))
                    {
                        int ID_1 = ShDataReader.GetString(FindAdministrativeNameFieldIndex(ShDataReader, "shapeID")).ToInt();
                        return new Answere<int>(ID_1);
                    }
                }

                int nearst = GetNearestArea(point);
                if (nearst > -1)
                    return new Answere<int>(nearst);

                return new Answere<int>(0, "Невозможно найти регион по переданным гео-данным");
            }
            catch (Exception ex)
            {
                logger.LogError("RouterDbService.GetOfflineAddress error: {0}", ex.GetAllMessages());
                return new Answere<int>(0, "Невозможно найти регион по переданным гео-данным");
            }
        }

        public int GetNearestArea(Point point)
        {
            try
            {
                var geometryFactory = new GeometryFactory();                                
                var ShDataReader = new ShapefileDataReader(ShapefilePath, geometryFactory);
                var administrativeNameField = FindAdministrativeNameFieldIndex(ShDataReader, "shapeID");

                double minDistance = double.MaxValue;
                string nearestAdministrativeId = null;
                NetTopologySuite.Geometries.Geometry nearestAdministrativeArea = null;

                while (ShDataReader.Read())
                {
                    var administrativeArea = ShDataReader.Geometry;
                    var administrativeName = ShDataReader.GetString(administrativeNameField);

                    double distance = point.Distance(administrativeArea);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestAdministrativeId = administrativeName;
                        nearestAdministrativeArea = administrativeArea;
                    }
                }

                const double maxDistanceMeters = 1000.0;
                double minDistanceMeters = ConvertDegreesToMeters(minDistance);

                if (minDistanceMeters > maxDistanceMeters)
                    return -1;
                if (nearestAdministrativeId is not null)
                    return nearestAdministrativeId.ToInt();

                return -1;
            }
            catch (Exception ex)
            {
                logger.LogError("RouterDbService.GetOfflineAddress error: {0}", ex.GetAllMessages());
                return -1;
            }
        }

        private static double ConvertDegreesToMeters(double degrees)
        {
            // Приблизительный радиус Земли в метрах
            const double earthRadius = 6371000.0;
            return degrees * (Math.PI / 180) * earthRadius;
        }

        private const double EarthRadius = 6371000;

        /// <summary>
        /// Возвращает расстояние между двумя точками (широта/долгота) в метрах
        /// </summary>
        public static double GetDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);

            lat1 = ToRadians(lat1);
            lat2 = ToRadians(lat2);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1) * Math.Cos(lat2) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadius * c; // расстояние в метрах
        }

        private static double ToRadians(double angle) => angle * Math.PI / 180.0;
    }
}
