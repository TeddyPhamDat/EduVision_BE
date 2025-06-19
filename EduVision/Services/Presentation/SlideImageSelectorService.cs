using EduVision.DBContext;
using Microsoft.EntityFrameworkCore;

namespace EduVision.Services.Presentation
{
    public class SlideImageSelectorService
    {
        private readonly EduVisionContext _dbContext;

        public SlideImageSelectorService(EduVisionContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<string>?> GetBestImagesForSlidesAsync(
            string category, int? grade, string chapter, int slideCount)
        {
            string gradeStr = grade.HasValue ? $"Grade{grade}" : "None";
            string chapterStr = !string.IsNullOrWhiteSpace(chapter) ? chapter : "None";

            var images = await _dbContext.Images
                .Where(i => i.Category == category && i.Status == "Active" &&
                    (
                        i.Grade == gradeStr && i.Chapter == chapterStr
                        || i.Grade == "None" && i.Chapter == "None"
                    ))
                .ToListAsync();

            var rng = new Random();
            var specificImages = images
                .Where(i => i.Grade == gradeStr && i.Chapter == chapterStr)
                .Select(i => i.Url)
                .OrderBy(_ => rng.Next())
                .ToList();

            var defaultImages = images
                .Where(i => i.Grade == "None" && i.Chapter == "None")
                .Select(i => i.Url)
                .OrderBy(_ => rng.Next())
                .ToList();

            if (specificImages.Count == 0 && defaultImages.Count == 0)
                return null;

            var result = new List<string>();

            foreach (var url in specificImages)
            {
                if (result.Count < slideCount)
                    result.Add(url);
                else
                    break;
            }

            int defaultIndex = 0;
            while (result.Count < slideCount)
            {
                if (defaultImages.Count > 0)
                {
                    result.Add(defaultImages[defaultIndex % defaultImages.Count]);
                    defaultIndex++;
                }
                else
                {
                    result.Add(string.Empty);
                }
            }

            return result;
        }
    }
}