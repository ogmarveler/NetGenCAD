namespace NetGenCAD.Core.Utility
{
    public static class UpdateFIle
    {
        public static bool UpdateFile(string filePath, string codeBlock)
        {
            try
            {
                string content = File.ReadAllText(filePath);
                if (!content.Contains(codeBlock, StringComparison.Ordinal)) return false;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }

        public static bool ChangeContentBlockFile(string filePath, string oldCodeBlock, string newCodeBlock)
        {
            try
            {
                string content = File.ReadAllText(filePath);
                if (!content.Contains(oldCodeBlock, StringComparison.Ordinal)) return false;
                var updatedContent = content.Replace(oldCodeBlock, newCodeBlock, StringComparison.Ordinal);
                if (updatedContent.Equals(content, StringComparison.Ordinal)) return true;
                File.WriteAllText(filePath, updatedContent);
                Console.WriteLine("Replacement successful.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }
    }
}
