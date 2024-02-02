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
        Answere<int> GetOfflineAddress(double longitude, double latitude);
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

                    routerDb.Sort();
                    routerDb.OptimizeNetwork(50);
                    routerDb.Compress();

                    routerDb.Network.Sort();
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

            StartRoadSearch = Convert.ToInt32(conf["StartRoadSearch"]);
            EndRoadSearch = Convert.ToInt32(conf["EndRoadSearch"]);

            ShapefilePath = AppDomain.CurrentDomain.BaseDirectory + conf["ShapeFileUrl"];
            GFactory = new GeometryFactory();
        }

        public Answere<Response> Calculate(Request<float> req)
        {
            try
            {
                var profile = Itinero.Osm.Vehicles.Vehicle.Car.Fastest(); // the default OSM car profile.             
                var router = new Router(routerDb);

                var start = router.Resolve(profile, req.Begin.Latitude, req.Begin.Longitude, StartRoadSearch);// 41.259976f, 69.199349f);               
                var end = router.Resolve(profile, req.End.Latitude, req.End.Longitude, EndRoadSearch); // 41.364306f, 69.264752f);
                var route = router.Calculate(profile, start, end);

                var json = route.ToGeoJson();

                var res = json.FromJson<Response>();
                res.TotalDistance = route.TotalDistance;

                return new Answere<Response>(1, "", "", res);
            }
            catch (ResolveFailedException re)
            {
                logger.LogError("RouterDbService.Calculate(ResolveFailedException) error: {0}", re.GetAllMessages());
                return new Answere<Response>(0, "Ошибка при калькуляции маршрута. Укажите более точные к дороге гео-данные");
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

        static int FindAdministrativeNameFieldIndex(ShapefileDataReader reader, string fieldName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            throw new ArgumentException($"Поле с именем '{fieldName}' не найдено в схеме данных.");
        }

        public Answere<int> GetOfflineAddress(double longitude, double latitude)
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
                        int ID_1 = ShDataReader.GetString(FindAdministrativeNameFieldIndex(ShDataReader, "ID_1")).ToInt();
                        return new Answere<int>(ID_1);
                        //string NAME_0 = ShDataReader.GetString(FindAdministrativeNameFieldIndex(ShDataReader, "NAME_0"));
                        //string NAME_1 = ShDataReader.GetString(FindAdministrativeNameFieldIndex(ShDataReader, "NAME_1"));
                        //string NAME_2 = ShDataReader.GetString(FindAdministrativeNameFieldIndex(ShDataReader, "NAME_2"));
                        //string TYPE_2 = ShDataReader.GetString(FindAdministrativeNameFieldIndex(ShDataReader, "TYPE_2"));

                        //if (NAME_1 == NAME_2) NAME_2 = string.Empty;

                        //if (NAME_1 == "Tashkent City")
                        //    NAME_1 = "Toshkent";

                        //if (TYPE_2 == "City")
                        //    NAME_1 = $"{NAME_1} Shahri";
                        //else
                        //    NAME_1 = $"{NAME_1} tumani";

                        //return NAME_1;
                    }
                }
                return new Answere<int>(0, "Невозможно найти регион по переданным гео-данным");
            }
            catch (Exception ex)
            {
                logger.LogError("RouterDbService.GetOfflineAddress error: {0}", ex.GetAllMessages());
                return new Answere<int>(0, "Невозможно найти регион по переданным гео-данным");
            }
        }
    }
}
