using GisRouteApi.Services;
using Itinero;
using Microsoft.AspNetCore.Mvc;
using System.Drawing;

namespace GisRouteApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RouterController : ControllerBase
    {

        private readonly ILogger<RouterController> logger;

        public RouterController(ILogger<RouterController> logger)
        {
            this.logger = logger;
        }

        [HttpPost]
        public IResult Get([FromServices] IRouterDbService service, PointF begin, PointF end)
        {
            //var res = service.Calculate(new PointF(41.259976f, 69.199349f), new PointF(41.364306f, 69.264752f));
            var res = service.Calculate(begin, end);
            return Results.Content(res);
        }
    }
}