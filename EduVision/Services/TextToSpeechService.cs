using System.Speech.Synthesis;
using System.IO;
using System.Threading.Tasks;

namespace EduVision.Services
{
    public class TextToSpeechService
    {
        private readonly AzureBlobStorageService _blobStorage;

        public TextToSpeechService(AzureBlobStorageService blobStorage)
        {
            _blobStorage = blobStorage;
        }

        public async Task<string> GenerateAudioAsync(string text, string blobName)
        {
            // Generate audio to a memory stream
            using var synth = new SpeechSynthesizer();
            synth.Volume = 100;
            synth.Rate = 0;

            using var memoryStream = new MemoryStream();
            synth.SetOutputToWaveStream(memoryStream);
            synth.Speak(text);

            memoryStream.Position = 0; // Reset stream position

            // Upload to Azure Blob Storage
            var url = await _blobStorage.UploadAsync(blobName, memoryStream, "audio/wav");
            return url;
        }
    }
}
