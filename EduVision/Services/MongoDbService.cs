using EduVision.Models.Config;
using EduVision.Models.DTO;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;

namespace EduVision.Services
{
    public class MongoDbService
    {
        private readonly IMongoDatabase _database;
        private readonly ILogger<MongoDbService> _logger;
        private readonly Dictionary<string, string> _collectionMappings;

        public MongoDbService(IOptions<MongoDbConfig> config, ILogger<MongoDbService> logger)
        {
            _logger = logger;
            _collectionMappings = config.Value.CollectionMappings ?? new Dictionary<string, string>();
            
            try
            {
                var client = new MongoClient(config.Value.ConnectionString);
                _database = client.GetDatabase(config.Value.DatabaseName);
                _logger.LogInformation("Successfully connected to MongoDB database: {DatabaseName}", config.Value.DatabaseName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to MongoDB");
                throw;
            }
        }

        /// <summary>
        /// Gets the collection name for a subject
        /// </summary>
        public string GetCollectionForSubject(string subject)
        {
            if (_collectionMappings.TryGetValue(subject, out var collection))
            {
                return collection;
            }
            
            // Default to the subject name + "12_pages" if mapping not found
            return $"{subject.ToLower()}12_pages";
        }

        /// <summary>
        /// Gets content by subject, chapter and optionally grade
        /// </summary>
        public async Task<string> GetContentAsync(string subject, string chapter, int? grade = null)
        {
            try
            {
                var collectionName = GetCollectionForSubject(subject);
                var collection = _database.GetCollection<BsonDocument>(collectionName);
                
                // Build filter for chapter and optionally grade
                var filterBuilder = Builders<BsonDocument>.Filter;
                var filter = filterBuilder.Eq("chapter", chapter);
                
                if (grade.HasValue)
                {
                    filter &= filterBuilder.Eq("grade", grade.Value);
                }

                var sort = Builders<BsonDocument>.Sort.Ascending("page");
                
                var documents = await collection.Find(filter)
                    .Sort(sort)
                    .ToListAsync();

                if (documents == null || !documents.Any())
                {
                    _logger.LogWarning("No documents found for subject: {Subject}, chapter: {Chapter}", subject, chapter);
                    return string.Empty;
                }

                StringBuilder contentBuilder = new StringBuilder();
                foreach (var doc in documents)
                {
                    if (doc.Contains("content") && doc["content"].IsString)
                    {
                        contentBuilder.AppendLine(doc["content"].AsString);
                    }
                }

                return contentBuilder.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching content for subject: {Subject}, chapter: {Chapter}", subject, chapter);
                throw;
            }
        }

        /// <summary>
        /// Gets metadata for an educational item by subject, chapter
        /// </summary>
        public async Task<EducationalItemMetadataDto?> GetMetadataAsync(string subject, string chapter, int? grade = null)
        {
            try
            {
                var collectionName = GetCollectionForSubject(subject);
                var collection = _database.GetCollection<BsonDocument>(collectionName);
                
                var filterBuilder = Builders<BsonDocument>.Filter;
                var filter = filterBuilder.Eq("chapter", chapter);
                
                if (grade.HasValue)
                {
                    filter &= filterBuilder.Eq("grade", grade.Value);
                }

                // Get just the first document to extract title and metadata
                var document = await collection.Find(filter)
                    .SortBy(doc => doc["page"])
                    .FirstOrDefaultAsync();

                if (document == null)
                {
                    return null;
                }

                var metadata = new EducationalItemMetadataDto
                {
                    Subject = document.Contains("subject") ? document["subject"].AsString : subject,
                    Grade = document.Contains("grade") ? document["grade"].AsInt32 : grade ?? 0,
                    Chapter = chapter,
                    Title = document.Contains("title") ? document["title"].AsString : chapter,
                    TotalPages = await collection.CountDocumentsAsync(filter)
                };

                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching metadata for subject: {Subject}, chapter: {Chapter}", subject, chapter);
                return null;
            }
        }

        /// <summary>
        /// Gets all available chapters for a particular subject
        /// </summary>
        public async Task<List<string>> GetChaptersAsync(string subject, int? grade = null)
        {
            try
            {
                var collectionName = GetCollectionForSubject(subject);
                if (!await CollectionExistsAsync(collectionName))
                {
                    _logger.LogWarning("Collection {Collection} for subject {Subject} does not exist", 
                        collectionName, subject);
                    return new List<string>();
                }

                var collection = _database.GetCollection<BsonDocument>(collectionName);

                // Build filter that optionally includes grade
                var filter = grade.HasValue 
                    ? Builders<BsonDocument>.Filter.Eq("grade", grade.Value) 
                    : FilterDefinition<BsonDocument>.Empty;

                var chapters = await collection.Distinct<string>("chapter", filter).ToListAsync();
                
                // Ensure we have content for each chapter
                var chaptersWithContent = new List<string>();
                foreach (var chapter in chapters)
                {
                    var chapterFilter = Builders<BsonDocument>.Filter.Eq("chapter", chapter);
                    if (grade.HasValue)
                    {
                        chapterFilter &= Builders<BsonDocument>.Filter.Eq("grade", grade.Value);
                    }
                    
                    // Check if there's at least one document with content
                    var documents = await collection.Find(chapterFilter)
                        .Project(Builders<BsonDocument>.Projection.Include("content"))
                        .ToListAsync();
                        
                    bool hasContent = documents.Any(doc => 
                        doc.Contains("content") && 
                        doc["content"].IsString && 
                        !string.IsNullOrWhiteSpace(doc["content"].AsString));
                        
                    if (hasContent)
                    {
                        chaptersWithContent.Add(chapter);
                        _logger.LogInformation("Found chapter with content: {Chapter} for subject {Subject}", 
                            chapter, subject);
                    }
                    else
                    {
                        _logger.LogWarning("Chapter {Chapter} for subject {Subject} has no content", 
                            chapter, subject);
                    }
                }
                
                return chaptersWithContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching chapters for subject: {Subject}", subject);
                return new List<string>();
            }
        }

        /// <summary>
        /// Gets all available subjects in the MongoDB database
        /// </summary>
        public async Task<List<string>> GetAvailableSubjectsAsync()
        {
            try
            {
                // First try to get subjects from collection mappings
                if (_collectionMappings.Any())
                {
                    var availableSubjects = new List<string>();
                    
                    // Only return subjects whose collections actually exist
                    foreach (var mapping in _collectionMappings)
                    {
                        if (await CollectionExistsAsync(mapping.Value))
                        {
                            availableSubjects.Add(mapping.Key);
                            _logger.LogInformation("Found available subject: {Subject} (collection: {Collection})", 
                                mapping.Key, mapping.Value);
                        }
                        else
                        {
                            _logger.LogWarning("Collection {Collection} for subject {Subject} does not exist",
                                mapping.Value, mapping.Key);
                        }
                    }
                    
                    return availableSubjects;
                }

                // If no mappings defined, try to infer from actual collections
                var collections = await _database.ListCollectionNamesAsync();
                var collectionNames = await collections.ToListAsync();
                
                _logger.LogInformation("Found {Count} collections in database", collectionNames.Count);
                
                // Try to extract subject names from collection naming pattern (e.g., gdcd12_pages -> GDCD)
                var subjects = new List<string>();
                foreach (var collection in collectionNames)
                {
                    _logger.LogInformation("Checking collection: {Collection}", collection);
                    
                    if (collection.EndsWith("12_pages"))
                    {
                        var subject = collection.Replace("12_pages", "").ToUpper();
                        subjects.Add(subject);
                        _logger.LogInformation("Inferred subject {Subject} from collection {Collection}", 
                            subject, collection);
                    }
                }
                
                return subjects;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching available subjects");
                return new List<string>();
            }
        }

        private async Task<bool> CollectionExistsAsync(string collectionName)
        {
            try
            {
                var filter = new BsonDocument("name", collectionName);
                var collections = await _database.ListCollectionsAsync(new ListCollectionsOptions { Filter = filter });
                return await collections.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if collection exists: {CollectionName}", collectionName);
                return false;
            }
        }
    }
}