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
        public IResult Get(Request m)
        {
            //var res = service.Calculate(new PointF(41.259976f, 69.199349f), new PointF(41.364306f, 69.264752f));
            var res = service.Calculate(new PointF(m.Begin.Latitude, m.Begin.Longitude), new PointF(m.End.Latitude, m.End.Longitude));
            return Results.Content(res);
        }
    }
}