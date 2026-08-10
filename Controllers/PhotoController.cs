
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WeatherForecastAI.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class PhotoController : ControllerBase
    {
        private static readonly HttpClient _httpClient = new();

        [HttpPost("analyze")]
        public async Task<IActionResult> Analyze(IFormFile image)
        {
            if (image == null || image.Length == 0)
            {
                return BadRequest("No image file uploaded.");
            }
            if (!image.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) && !image.FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) && !image.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !image.FileName.EndsWith(".heic", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Only .jpg, .jpeg, .png, and .heic files are allowed.");
            }
            if (!image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Uploaded file is not a valid image.");
            }

            try
            {
                using var memoryStream = new MemoryStream();
                await image.CopyToAsync(memoryStream);
                var base64Image = Convert.ToBase64String(memoryStream.ToArray());

                var description = await DescribeImageAsync(base64Image);

                return Ok(new { description });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "An unexpected error occurred while processing the image file.",
                    details = ex.Message
                });
            }
        }


        private static async Task<string> DescribeImageAsync(string base64Image)
        {
            var requestBody = new
            {
                model = "moondream",
                prompt = "Describe this image in detail.",
                images = new[] { base64Image },
                stream = false
            };

            var response = await _httpClient.PostAsJsonAsync("http://localhost:11434/api/generate", requestBody);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return result.GetProperty("response").GetString() ?? "No description available.";
        }

    }
}
