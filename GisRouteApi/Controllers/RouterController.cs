using GisRouteApi.Models;
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
        private readonly IRouterDbService service;

        public RouterController(IRouterDbService service)
        {
            this.service = service;
        }

        [HttpPost]
        public Answere<Response> Get(Request m)
        {
            //var res = service.Calculate(new PointF(41.311577f, 69.289810f), new PointF(41.378203f, 69.251803f));
            var res = service.Calculate(new PointF(m.Begin.Latitude, m.Begin.Longitude), new PointF(m.End.Latitude, m.End.Longitude));
            return res;
        }
    }
}