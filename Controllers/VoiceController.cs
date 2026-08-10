
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Whisper.net;

namespace WeatherForecastAI.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class VoiceController : ControllerBase
    {

        public readonly Kernel _kernel;
        public readonly WhisperFactory _whisperFactory;

        public VoiceController(Kernel kernel, WhisperFactory whisperFactory)
        {
            _kernel = kernel;
            _whisperFactory = whisperFactory;
        }

        [HttpPost("summarize")]
        public async Task<IActionResult> Summarize(IFormFile audio)
        {
            if (audio == null || audio.Length == 0)
            {
                return BadRequest("No audio file uploaded.");
            }
            if (!audio.FileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Only .wav files are allowed.");
            }

            try
            {
                using var audioStream = audio.OpenReadStream();
                using var whisper = _whisperFactory.CreateBuilder().WithLanguage("auto").Build();
                var transcript = new StringBuilder();
                await foreach (var segment in whisper.ProcessAsync(audioStream))
                {
                    transcript.AppendLine(segment.Text);
                }
                var transcription = transcript.ToString();

                var summaryPrompt =
                    "Summarize the following voice note into a clear, structured summary with key points:\n\n" +
                    transcription;
                var summaryResult = await _kernel.InvokePromptAsync(summaryPrompt);

                return Ok(new { transcript = transcription, summary = summaryResult.ToString() });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "An unexpected error occurred while processing the audio file.",
                    details = ex.Message
                });
            }
        }
    }
}
