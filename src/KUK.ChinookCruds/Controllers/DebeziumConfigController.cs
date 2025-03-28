using KUK.ChinookCruds.Debezium;
using Microsoft.AspNetCore.Mvc;
using System.Dynamic;

namespace KUK.ChinookCruds.Controllers
{
    [ApiController]
    [Route("api/debezium")]
    public class DebeziumConfigController : ControllerBase
    {
        private readonly DebeziumConfigService _configService;

        public DebeziumConfigController(DebeziumConfigService configService)
        {
            _configService = configService;
        }

        [HttpGet("config1")]
        public ActionResult<dynamic> GetConfig1()
        {
            return _configService.GetConfig1();
        }

        [HttpGet("config2")]
        public ActionResult<dynamic> GetConfig2()
        {
            return _configService.GetConfig2();
        }

        [HttpPost("config1")]
        public IActionResult UpdateConfig1([FromBody] ExpandoObject config)
        {
            _configService.UpdateConfig1(config);
            return Ok();
        }

        [HttpPost("config2")]
        public IActionResult UpdateConfig2([FromBody] ExpandoObject config)
        {
            _configService.UpdateConfig2(config);
            return Ok();
        }
    }

}
