namespace GisRouteApi.Models
{
    public class Request
    {
        public Coordinate Begin { get; set; }
        public Coordinate End { get; set; }
    }

    public class Coordinate
    {
        public float Latitude { get; set; }
        public float Longitude { get; set; }
    }

}
