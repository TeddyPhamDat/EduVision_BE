namespace EduVision.Models.Constants
{
    /// <summary>
    /// Template selection and naming constants.
    /// </summary>
    public static class TemplateConstants
    {
        /// <summary>
        /// Template ID enumeration for better type safety.
        /// </summary>
        public static class TemplateIds
        {
            public const int Default = 1;
            public const int Dark = 2;
            public const int Modern = 3;
            public const int Interactive = 4;
        }

        /// <summary>
        /// Template file names.
        /// </summary>
        public static class TemplateFiles
        {
            public const string Default = "RevealTemplate.html";
            public const string Dark = "RevealTemplateDark.html";
            public const string Modern = "RevealTemplateModern.html";
            public const string Interactive = "Template_1.html";
        }

        /// <summary>
        /// Gets the template file name for a given template ID.
        /// </summary>
        /// <param name="templateId">The template ID</param>
        /// <returns>The corresponding template file name</returns>
        public static string GetTemplateFileName(int templateId) => templateId switch
        {
            TemplateIds.Default => TemplateFiles.Default,
            TemplateIds.Dark => TemplateFiles.Dark,
            TemplateIds.Modern => TemplateFiles.Modern,
            TemplateIds.Interactive => TemplateFiles.Interactive,
            _ => TemplateFiles.Dark // Default fallback
        };
    }
}