using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace EduVision.Models
{
    public class MongoDbConfig
    {
        [Required]
        public string? ConnectionString { get; set; }
        
        [Required]
        public string? DatabaseName { get; set; }
        
        // Collection mappings for different subjects
        public Dictionary<string, string> CollectionMappings { get; set; } = new Dictionary<string, string>
        {
            { "GDCD", "gdcd12_pages" },
            // Future mappings can be added here or via configuration
            // { "Math", "math12_pages" },
            // { "Literature", "literature12_pages" },
        };
    }
}