using Newtonsoft.Json;

namespace GisRouteApi.Models
{
    public class Response
    {
        public string type { get; set; }
        public Feature[] features { get; set; }
        public float TotalDistance { get; set; }
    }

    public class Feature
    {
        public string type { get; set; }
        public string name { get; set; }
        public Geometry geometry { get; set; }
        public Properties properties { get; set; }
        public string Shape { get; set; }
    }

    public class Geometry
    {
        public string type { get; set; }
        public object[] coordinates { get; set; }
    }

    public class Properties
    {
        public string name { get; set; }
        public string highway { get; set; }
        public string profile { get; set; }
        public double distance { get; set; }
        public double time { get; set; }
        public string bridge { get; set; }
        public string oneway { get; set; }
        public int? maxspeed { get; set; }
        public string tunnel { get; set; }
    }

}
