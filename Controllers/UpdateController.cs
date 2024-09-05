using Microsoft.AspNetCore.Mvc;
using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.S3.Model;
using System.Threading.Tasks;
using Semver;
using System.Globalization;

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

        private string? s3EndPoint = Environment.GetEnvironmentVariable("S3_ENDPOINT");
        private string? s3AccessKey = Environment.GetEnvironmentVariable("S3_ACCESS_KEY");
        private string? s3SecretKey = Environment.GetEnvironmentVariable("S3_SECRET_KEY");
        private string? s3BucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME");
        private string? signature= Environment.GetEnvironmentVariable("SIGNATURE");
        [HttpGet("{name}/{target}/{arch}/{currentVersion}")]
        public async Task<IActionResult> GetUpdate(string name, string target, string arch, string currentVersion)
        {
            var s3Client = new AmazonS3Client(s3AccessKey, s3SecretKey, new AmazonS3Config { ServiceURL = s3EndPoint });

            // Construct the S3 prefix (directory path)
            var prefix = $"{name}/{target}/{arch}/";

            // List the directories (subdirectories named with SemVer)
            var request = new ListObjectsV2Request
            {
                BucketName = s3BucketName,
                Prefix = prefix,
                Delimiter = "/"
            };

            var response = await s3Client.ListObjectsV2Async(request);

            if (response.CommonPrefixes.Count == 0)
            {
                return NoContent(); // No subdirectories found
            }

            SemVersion currentSemVer;
            if (!SemVersion.TryParse(currentVersion, out currentSemVer))
            {
                return BadRequest("Invalid current version format");
            }

            SemVersion latestVersion = null;
            string latestDirectory = null;

            // Find the latest version greater than the current version
            foreach (var entry in response.CommonPrefixes)
            {
                // Get the version from the directory name (assuming the last segment is the version)
                var directory = entry.Replace(prefix, "").Trim('/');
                if (SemVersion.TryParse(directory, out var parsedVersion) && parsedVersion > currentSemVer)
                {
                    if (latestVersion == null || parsedVersion > latestVersion)
                    {
                        latestVersion = parsedVersion;
                        latestDirectory = directory;
                    }
                }
            }

            if (latestVersion == null)
            {
                return NoContent(); // No version greater than the current one found
            }

            var fileRequest = new ListObjectsV2Request
            {
                BucketName = s3BucketName,
                Prefix = $"{name}/{target}/{arch}/{latestVersion}/"
            };

            var fileResponse = await s3Client.ListObjectsV2Async(fileRequest);

            // Check if the directory contains exactly one file
            var file = fileResponse.S3Objects.FirstOrDefault();
            if (file == null)
            {
                return NoContent(); // No file found in the directory
            }

            // Build the latest version info including file URL and last modified time
            var latestVersionJson = new
            {
                version = latestVersion.ToString(),
                pub_date = file.LastModified.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture), // Use file's last modified time
                url = $"{s3EndPoint}/{s3BucketName}/{file.Key}",
                signature, 
                notes = ""
            };

            return Ok(latestVersionJson);
        }
        [HttpPost("{name}/{target}/{arch}/{version}")]
        [RequestSizeLimit(500 * 1024 * 1024)]
        public async Task<IActionResult> PostRelease(string name, string target, string arch, string version, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("File is required");
            }

            var filePath = Path.GetTempFileName();
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var s3Client = new AmazonS3Client(s3AccessKey, s3SecretKey, new AmazonS3Config { ServiceURL = s3EndPoint });

            var transferUtility = new TransferUtility(s3Client);

            var key = $"{name}/{target}/{arch}/{version}/{name}-{version}{Path.GetExtension(file.FileName)}";

            await transferUtility.UploadAsync(filePath, s3BucketName, key);

            return Ok();
        }
    }
}
