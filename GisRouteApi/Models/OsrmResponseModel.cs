namespace GisRouteApi.Models
{
    public class OsrmResponseModel
    {
        public string code { get; set; }
        public Route[] routes { get; set; }
        public Waypoint[] waypoints { get; set; }
    }

    public class Route
    {
        public Leg[] legs { get; set; }
        public string weight_name { get; set; }
        public float weight { get; set; }
        public float duration { get; set; }
        public double distance { get; set; }
    }

    public class Leg
    {
        public object[] steps { get; set; }
        public string summary { get; set; }
        public float weight { get; set; }
        public float duration { get; set; }
        public double distance { get; set; }
    }

    public class Waypoint
    {
        public string hint { get; set; }
        public float distance { get; set; }
        public string name { get; set; }
        public float[] location { get; set; }
    }

}
