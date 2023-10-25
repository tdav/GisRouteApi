using GisRouteApi.Models;
using GisRouteApi.Services;
using Itinero;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Drawing;
using System.Threading.Tasks;

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

        [HttpPost("Distance")]        
        public IActionResult GetDistance(Request<float> m)
        {
            var res = service.Calculate(m);
            if (res.AnswereId == 1)
                return Ok(res.Data.TotalDistance);

            return BadRequest($"{res.AnswereMessage}\n{res.AnswereComment}");
        }

        [HttpPost]
        public Answere<Response> Get(Request<float> m)
        {
            //var res = service.Calculate(new PointF(41.311577f, 69.289810f), new PointF(41.378203f, 69.251803f));
            var res = service.Calculate(m);
            return res;
        }

        [HttpPost("RouteByOsrm")]
        public ValueTask<Answere<OsrmResponseModel>> GetRouteByOsrmAsync(Request<double> request)
        {
            return service.GetRouteByOsrmAsync(request);
        }

        [HttpPost("DistanceByOsrm")]
        public async ValueTask<IActionResult> GetDistanceByOsrmAsync(Request<double> request)
        {
            var res = await service.GetRouteByOsrmAsync(request);
            if (res.AnswereId == 1)
            {
                var distance = res.Data.routes[0].distance;
                return Ok(distance);
            }

            return BadRequest($"{res.AnswereMessage}\n{res.AnswereComment}");
        }
    }
}