using Itinero;
using Itinero.IO.Osm;
using Itinero.Osm.Vehicles;
using Itinero.Profiles;
using OpenLR;
using OpenLR.Osm;
using OpenLR.Referenced.Locations;
using System;
using System.IO;

namespace GeocodingApi
{
    public class Geocoding
    {
        private readonly RouterDb routerDb;

        public Geocoding()
        {
            string mapPath = "C:\\works\\GisRouteApi\\GisRouteApi\\Maps\\uzbekistan-latest.osm.pbf";
            string RouterDbPath = $"{AppDomain.CurrentDomain.BaseDirectory}router_database.db";

            routerDb = new RouterDb();

            if (File.Exists(RouterDbPath))
            {
                using (var stream = new FileInfo(RouterDbPath).Open(FileMode.Open))
                {
                    routerDb = RouterDb.Deserialize(stream);

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

        public void Encode()
        {
            var coderProfile = new OsmCoderProfile();
            var coder = new Coder(routerDb, coderProfile);

            var line = coder.BuildLine(
    new Itinero.LocalGeo.Coordinate(41.285910f, 69.269177f),
    new Itinero.LocalGeo.Coordinate(41.378203f, 69.251803f));
            var edge = routerDb.Network.GetEdge(line.StartLocation.EdgeId);
            
            var attr = routerDb.GetProfileAndMeta(edge.Data.Profile, edge.Data.MetaId);
            var speed = Itinero.Osm.Vehicles.Vehicle.Car.Fastest().Speed(attr);
            
        }
    }
}
