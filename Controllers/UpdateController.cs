using Microsoft.AspNetCore.Mvc;

namespace TauriUpdateServer.Controllers
{
    [ApiController]
    [Route("/")]
    public class UpdateController : ControllerBase
    {

        private readonly ILogger<UpdateController> _logger;

        public UpdateController(ILogger<UpdateController> logger)
        {
            _logger = logger;
        }

        [HttpGet("{name}/{target}/{arch}/{currentVersion}")]
        public IActionResult GetUpdate(string name, string target, string arch, string currentVersion)
        {
            var url = $"https://releases.myapp.com/{name}/{target}/{arch}/{currentVersion}";
            return Ok(new
            {
                version = "0.1.0",
                pub_date = DateTime.Now,
                url = url,
                signature = "",
                notes = ""
            });
        }
        [HttpPost("{name}/{target}/{arch}/{version}")]
        public IActionResult PostRelease(string name, string target, string arch, string version)
        {
            return Ok();
        }
        [HttpPost("SetS3")]
        public IActionResult SetS3(string endpoint, string bucket, string accessKey, string secretKey) 
        {
            return Ok();
        }
    }
}
