namespace GisRouteApi.Models
{
    public class Request<T>
    {
        public Coordinata<T> Begin { get; set; }
        public Coordinata<T> End { get; set; }
    }

    public class Coordinata<T>
    {
        public T Latitude { get; set; }
        public T Longitude { get; set; }
    }

}
