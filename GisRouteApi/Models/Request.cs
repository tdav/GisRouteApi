namespace GisRouteApi.Models
{
    public class Request
    {
        public Coordinata Begin { get; set; }
        public Coordinata End { get; set; }
    }

    public class Coordinata
    {
        public float Latitude { get; set; }
        public float Longitude { get; set; }
    }

}
