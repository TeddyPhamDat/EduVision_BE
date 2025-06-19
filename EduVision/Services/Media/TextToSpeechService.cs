using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using EduVision.Services.Storage;

namespace EduVision.Services.Media
{
    public class TextToSpeechService
    {
        private readonly AzureBlobStorageService _blobStorage;
        private readonly ILogger<TextToSpeechService> _logger;
        private readonly string _speechKey;
        private readonly string _speechRegion;

        public TextToSpeechService(
            AzureBlobStorageService blobStorage,
            IConfiguration configuration,
            ILogger<TextToSpeechService> logger)
        {
            _blobStorage = blobStorage;
            _speechKey = configuration["Azure:SpeechKey"] ?? throw new ArgumentNullException("Azure:SpeechKey is missing in configuration");
            _speechRegion = configuration["Azure:SpeechRegion"] ?? "southeastasia";
            _logger = logger;
        }

        public async Task<string> GenerateAudioAsync(string text, string blobName)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Empty text provided for speech synthesis");
                text = " "; // Provide a space to avoid API errors
            }

            try
            {
                var config = SpeechConfig.FromSubscription(_speechKey, _speechRegion);
                config.SpeechSynthesisVoiceName = "vi-VN-HoaiMyNeural";
                config.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Riff16Khz16BitMonoPcm);

                using var synthesizer = new SpeechSynthesizer(config, null);

                _logger.LogDebug("Synthesizing speech for text of length {Length}", text.Length);
                var result = await synthesizer.SpeakTextAsync(text);

                if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                {
                    _logger.LogInformation("Speech synthesis succeeded");
                    var audioData = result.AudioData;
                    if (audioData == null || audioData.Length < 44)
                    {
                        _logger.LogError("Audio data is empty or too short to be a valid WAV file.");
                        throw new InvalidDataException("Audio data is empty or too short to be a valid WAV file.");
                    }
                    // Check WAV header
                    if (System.Text.Encoding.ASCII.GetString(audioData, 0, 4) != "RIFF")
                    {
                        _logger.LogError("Audio file is not a valid WAV file. Header: {Header}", BitConverter.ToString(audioData, 0, 4));
                        throw new InvalidDataException("Audio file is not a valid WAV file.");
                    }
                    using var memoryStream = new MemoryStream(audioData);
                    var url = await _blobStorage.UploadAsync(blobName, memoryStream, "audio/wav");
                    return url;
                }
                else
                {
                    var errorDetails = $"Speech synthesis failed: {result.Reason}";
                    if (result.Reason == ResultReason.Canceled)
                    {
                        var cancellationDetails = SpeechSynthesisCancellationDetails.FromResult(result);
                        errorDetails += $", Error Code: {cancellationDetails.ErrorCode}, Error Details: {cancellationDetails.ErrorDetails}";
                    }
                    _logger.LogError(errorDetails);
                    throw new Exception(errorDetails);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audio from Azure TTS");
                throw;
            }
        }

        // Adapter to write Azure PushAudioOutputStream data to a MemoryStream
        private class MemoryStreamWriteAdapter : PushAudioOutputStreamCallback
        {
            private readonly Stream _stream;
            public MemoryStreamWriteAdapter(Stream stream) => _stream = stream;
            public override uint Write(byte[] dataBuffer) { _stream.Write(dataBuffer, 0, dataBuffer.Length); return (uint)dataBuffer.Length; }
            public override void Close() { _stream.Flush(); }
        }
    }
}