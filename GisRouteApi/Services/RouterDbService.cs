using AsbtCore.UtilsV2;
using GisRouteApi.Models;
using Itinero;
using Itinero.Exceptions;
using Itinero.IO.Osm;
using Itinero.Profiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using NetTopologySuite.IO.Esri.Shapefiles.Readers;
using NetTopologySuite.Operation.Distance;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

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
        private const string AreaIdFieldName = "shapeID";
        private const double MaxNearestAreaDistanceMeters = 1_000d;

        private readonly string routerDbPath;
        private readonly string shapefilePath;
        private readonly string url;
        private readonly string addressUrl;

        private readonly int startRoadSearch;
        private readonly int endRoadSearch;

        private readonly RouterDb routerDb;
        private readonly Router router;
        private readonly Profile profile;
        private readonly object routerSync = new object();

        private readonly ILogger<RouterDbService> logger;
        private readonly HttpClient client;
        private readonly GeometryFactory geometryFactory;
        private readonly AdministrativeArea[] administrativeAreas;

        public RouterDbService(IConfiguration configuration, ILogger<RouterDbService> logger, IHttpClientFactory clientFactory)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            if (clientFactory == null)
                throw new ArgumentNullException(nameof(clientFactory));

            client = clientFactory.CreateClient("RouterDbService");

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var mapPath = ResolveDataFilePath(GetRequiredSetting(configuration, "MapName"));
            var configuredShapefilePath = GetRequiredSetting(configuration, "ShapeFileUrl");
            shapefilePath = ResolveDataFilePath(Path.ChangeExtension(configuredShapefilePath, ".shp"));
            routerDbPath = Path.Combine(AppContext.BaseDirectory, "router_database.db");

            url = GetRequiredSetting(configuration, "Url");
            addressUrl = GetRequiredSetting(configuration, "AddressUrl");

            startRoadSearch = GetPositiveIntSetting(configuration, "StartRoadSearch");
            endRoadSearch = GetPositiveIntSetting(configuration, "EndRoadSearch");

            profile = Itinero.Osm.Vehicles.Vehicle.Car.Fastest();
            routerDb = LoadOrCreateRouterDb(mapPath);
            router = new Router(routerDb);

            geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            administrativeAreas = LoadAdministrativeAreas();
        }

        public Answere<Response> Calculate(Request<float> req)
        {
            try
            {
                Itinero.Route route;

                lock (routerSync)
                {
                    var start = router.Resolve(profile, req.Begin.Latitude, req.Begin.Longitude, startRoadSearch);
                    var end = router.Resolve(profile, req.End.Latitude, req.End.Longitude, endRoadSearch);
                    route = router.Calculate(profile, start, end);
                }

                var response = route.ToGeoJson().FromJson<Response>();
                if (response == null)
                    throw new InvalidDataException("Itinero вернул некорректный GeoJSON маршрута.");

                response.TotalDistance = route.TotalDistance;
                return new Answere<Response>(1, "", "", response);
            }
            catch (RouteNotFoundException ex)
            {
                logger.LogWarning(ex, "RouterDbService.Calculate: маршрут не найден. Request: {@Request}", req);
                var distance = CalculateStraightLineDistance(req.Begin.Latitude, req.Begin.Longitude, req.End.Latitude, req.End.Longitude);
                var response = new Response { TotalDistance = distance + 500 };
                return new Answere<Response>(1, "Маршрут не найден, возвращено приблизительное расстояние", "", response);
            }
            catch (ResolveFailedException ex)
            {
                logger.LogWarning(ex, "RouterDbService.Calculate: координаты не привязаны к дорожной сети. Request: {@Request}", req);
                var distance = CalculateStraightLineDistance(req.Begin.Latitude, req.Begin.Longitude, req.End.Latitude, req.End.Longitude);

                return new Answere<Response>(new Response { TotalDistance = distance.ToInt() });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RouterDbService.Calculate: ошибка расчёта маршрута. Request: {@Request}", req);
                var distance = CalculateStraightLineDistance(req.Begin.Latitude, req.Begin.Longitude, req.End.Latitude, req.End.Longitude);
                return new Answere<Response>(1, "Ошибка при калькуляции", ex.Message, new Response { TotalDistance = distance.ToInt() });
            }
        }

        public async ValueTask<Answere<OsrmResponseModel>> GetRouteByOsrmAsync(Request<double> req)
        {
            try
            {
                var beginLongitude = req.Begin.Longitude.ToInvariantString();
                var beginLatitude = req.Begin.Latitude.ToInvariantString();
                var endLongitude = req.End.Longitude.ToInvariantString();
                var endLatitude = req.End.Latitude.ToInvariantString();

                var url = string.Format(
                    CultureInfo.InvariantCulture,
                    this.url,
                    beginLongitude,
                    beginLatitude,
                    endLongitude,
                    endLatitude);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("Accept-Language", "ru-RU");

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var model = json.FromJson<OsrmResponseModel>();

                if (model == null)
                    throw new InvalidDataException("OSRM вернул пустой или некорректный JSON.");

                var startAddress = await GetAddressAsync(beginLatitude, beginLongitude);

                await Task.Delay(250);

                var endAddress = await GetAddressAsync(endLatitude, endLongitude);

                model.StartAddress = startAddress.Data;
                model.EndAddress = endAddress.Data;

                return new Answere<OsrmResponseModel>(model);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RouterDbService.GetRouteByOsrmAsync: ошибка расчёта маршрута. Request: {@Request}", req);
                return new Answere<OsrmResponseModel>(0, "Ошибка при калькуляции", ex.Message);
            }
        }

        public async ValueTask<Answere<AddressModel>> GetAddressAsync(string lat, string lon)
        {
            try
            {
                var url = string.Format(CultureInfo.InvariantCulture, addressUrl, lat, lon);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("Accept-Language", "ru-RU");
                request.Headers.UserAgent.ParseAdd("GisRouteApi/1.0");

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var model = json.FromJson<AddressModel>();

                if (model == null)
                    throw new InvalidDataException("Сервис геокодирования вернул пустой или некорректный JSON.");

                return new Answere<AddressModel>(1, "OK", "", model);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "RouterDbService.GetAddressAsync: ошибка получения адреса. Latitude: {Latitude}, Longitude: {Longitude}",
                    lat,
                    lon);

                return new Answere<AddressModel>(
                    0,
                    "Ошибка при получении адреса",
                    ex.Message);
            }
        }

        public Answere<int> GetAreaIdByCoordinates(double longitude, double latitude)
        {
            try
            {
                var point = geometryFactory.CreatePoint(new Coordinate(longitude, latitude));

                foreach (var area in administrativeAreas)
                {
                    if (area.Geometry.Covers(point))
                        return new Answere<int>(area.Id);
                }

                var nearestAreaId = FindNearestAreaId(point);
                if (nearestAreaId > -1)
                    return new Answere<int>(nearestAreaId);

                return new Answere<int>(0, "Невозможно найти регион по переданным гео-данным");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RouterDbService.GetAreaIdByCoordinates: ошибка поиска региона. Longitude: {Longitude}, Latitude: {Latitude}", longitude, latitude);
                return new Answere<int>(0, "Невозможно найти регион по переданным гео-данным");
            }
        }

        public int GetNearestArea(Point point)
        {
            try
            {
                if (point == null)
                    return -1;

                return FindNearestAreaId(point);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "RouterDbService.GetNearestArea: ошибка поиска ближайшего региона");

                return -1;
            }
        }

        private RouterDb LoadOrCreateRouterDb(string mapPath)
        {
            if (File.Exists(routerDbPath))
            {
                try
                {
                    using var stream = File.OpenRead(routerDbPath);
                    return RouterDb.Deserialize(stream);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Не удалось прочитать RouterDb {RouterDbPath}. База будет пересоздана из {MapPath}", routerDbPath, mapPath);
                }
            }

            if (!File.Exists(mapPath))
                throw new FileNotFoundException("OSM-файл карты не найден.", mapPath);

            var routerDb = new RouterDb();

            using (var stream = File.OpenRead(mapPath))
            {
                routerDb.LoadOsmData(
                    stream,
                    Itinero.Osm.Vehicles.Vehicle.Car);
            }

            SaveRouterDbAtomically(routerDb);
            return routerDb;
        }

        private void SaveRouterDbAtomically(RouterDb routerDb)
        {
            var temporaryPath = string.Concat(routerDbPath, ".", Guid.NewGuid().ToString("N"), ".tmp");

            try
            {
                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None))
                {
                    routerDb.Serialize(stream);
                    stream.Flush(true);
                }

                File.Move(temporaryPath, routerDbPath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private AdministrativeArea[] LoadAdministrativeAreas()
        {
            if (!File.Exists(shapefilePath))
                throw new FileNotFoundException("SHP-файл административных регионов не найден.", shapefilePath);

            var options = new ShapefileReaderOptions
            {
                Factory = geometryFactory,
                GeometryBuilderMode = GeometryBuilderMode.FixInvalidShapes
            };

            var features = Shapefile.ReadAllFeatures(shapefilePath, options);
            var areas = new List<AdministrativeArea>(features.Length);

            foreach (var feature in features)
            {
                if (feature.Geometry == null || feature.Geometry.IsEmpty)
                    continue;

                var areaId = GetAreaId(feature.Attributes);
                areas.Add(new AdministrativeArea(areaId, feature.Geometry));
            }

            if (areas.Count == 0)
            {
                throw new InvalidDataException($"В shapefile '{shapefilePath}' не найдено ни одного административного региона.");
            }

            logger.LogInformation("Загружено административных регионов: {AreaCount}. Shapefile: {ShapefilePath}", areas.Count, shapefilePath);
            return areas.ToArray();
        }

        private int FindNearestAreaId(Point point)
        {
            var nearestAreaId = -1;
            var minDistanceMeters = double.MaxValue;

            foreach (var area in administrativeAreas)
            {
                var nearestPoints = DistanceOp.NearestPoints(point, area.Geometry);
                if (nearestPoints == null || nearestPoints.Length < 2)
                    continue;

                var nearestPoint = nearestPoints[1];
                var distanceMeters = CalculateDistanceMeters(
                    point.Y,
                    point.X,
                    nearestPoint.Y,
                    nearestPoint.X);

                if (distanceMeters >= minDistanceMeters)
                    continue;

                minDistanceMeters = distanceMeters;
                nearestAreaId = area.Id;
            }

            return minDistanceMeters <= MaxNearestAreaDistanceMeters
                ? nearestAreaId
                : -1;
        }

        private static int GetAreaId(IAttributesTable attributes)
        {
            if (attributes == null)
                throw new InvalidDataException("В shapefile отсутствует таблица атрибутов.");

            var actualFieldName = attributes
                .GetNames()
                .FirstOrDefault(name => string.Equals(
                    name,
                    AreaIdFieldName,
                    StringComparison.OrdinalIgnoreCase));

            if (actualFieldName == null)
            {
                throw new InvalidDataException($"Поле '{AreaIdFieldName}' не найдено в DBF-файле shapefile.");
            }

            var rawValue = attributes[actualFieldName];
            if (rawValue == null || rawValue == DBNull.Value)
            {
                throw new InvalidDataException($"Поле '{actualFieldName}' содержит пустое значение.");
            }

            try
            {
                return Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException ||
                                       ex is InvalidCastException ||
                                       ex is OverflowException)
            {
                throw new InvalidDataException($"Значение '{rawValue}' поля '{actualFieldName}' невозможно преобразовать в Int32.", ex);
            }
        }

        private static float CalculateStraightLineDistance(
            float latitude1,
            float longitude1,
            float latitude2,
            float longitude2)
        {
            return (float)CalculateDistanceMeters(
                latitude1,
                longitude1,
                latitude2,
                longitude2);
        }

        private static double CalculateDistanceMeters(
            double latitude1,
            double longitude1,
            double latitude2,
            double longitude2)
        {
            const double earthRadiusMeters = 6_371_000d;

            var latitude1Radians = DegreesToRadians(latitude1);
            var latitude2Radians = DegreesToRadians(latitude2);
            var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
            var longitudeDelta = DegreesToRadians(longitude2 - longitude1);

            var a = Math.Sin(latitudeDelta / 2d) * Math.Sin(latitudeDelta / 2d) +
                    Math.Cos(latitude1Radians) * Math.Cos(latitude2Radians) *
                    Math.Sin(longitudeDelta / 2d) * Math.Sin(longitudeDelta / 2d);

            var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
            return earthRadiusMeters * c;
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180d;
        }

        private static string GetRequiredSetting(IConfiguration configuration, string key)
        {
            var value = configuration[key];

            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Не задан параметр конфигурации '{key}'.");

            return value;
        }

        private static int GetPositiveIntSetting(IConfiguration configuration, string key)
        {
            var value = GetRequiredSetting(configuration, key);

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ||
                result <= 0)
            {
                throw new InvalidOperationException($"Параметр конфигурации '{key}' должен быть положительным целым числом.");
            }

            return result;
        }

        private static string ResolveDataFilePath(string configuredPath)
        {
            if (Path.IsPathFullyQualified(configuredPath) && File.Exists(configuredPath))
                return configuredPath;

            var relativePath = configuredPath.TrimStart(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));
        }

        private sealed class AdministrativeArea
        {
            public AdministrativeArea(int id, NetTopologySuite.Geometries.Geometry geometry)
            {
                Id = id;
                Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            }

            public int Id { get; }
            public NetTopologySuite.Geometries.Geometry Geometry { get; }
        }
    }
}