using KUK.KafkaProcessor.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace KUK.KafkaProcessorApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class StatusController : ControllerBase
    {
        private readonly ILogger<StatusController> _logger;
        private readonly GlobalState _globalState;

        public StatusController(
            ILogger<StatusController> logger,
            GlobalState globalState)
        {
            _logger = logger;
            _globalState = globalState;
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok("OK");
        }

        [HttpGet("isinitialized")]
        public IActionResult IsInitialized()
        {
            return Ok(_globalState.IsInitialized);
        }

        [HttpGet("snapshotlastreceived")]
        public IActionResult SnapshotLastReceived()
        {
            return Ok(_globalState.SnapshotLastReceived);
        }

        [HttpGet("ispollyretrying")]
        public IActionResult IsPollyRetrying()
        {
            return Ok(_globalState.IsPollyRetrying);
        }
    }
}
