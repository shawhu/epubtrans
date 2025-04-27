using VersOne.Epub;
using HtmlAgilityPack;
using TextCopy;


namespace EpubTrans
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("EPUB EBook Content Extractor/translator");
                Console.WriteLine("Author:  Harry Xiao (shawhu@gmail.com)");
                Console.WriteLine("Date:    27/4/2025");
                Console.WriteLine("");
                Console.WriteLine("Usage:");
                Console.WriteLine("EpubTrans <filename.epub>             # List chapters (filtered)");
                Console.WriteLine("EpubTrans <filename.epub> <n>         # Output chapter n (plain text)");
                Console.WriteLine("EpubTrans <filename.epub> -html <n>   # Output chapter n (HTML)");
                return;
            }

            string filePath = args[0];

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File '{filePath}' does not exist.");
                return;
            }

            EpubBook epubBook;
            try
            {
                epubBook = await EpubReader.ReadBookAsync(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading EPUB: {ex.Message}");
                return;
            }

            if (epubBook.Navigation == null || epubBook.Navigation.Count == 0)
            {
                Console.WriteLine("Error: No chapters or navigation found in this EPUB.");
                return;
            }

            // Flatten all chapters (including nested)
            var flatChapters = new List<EpubNavigationItem>();
            FlattenChapters(epubBook.Navigation, flatChapters);

            // Filter: only chapters whose title contains a digit
            var filteredChapters = ChapterFilter(flatChapters);

            if (filteredChapters.Count == 0)
            {
                Console.WriteLine("No chapters contain numbers in their titles.");
                return;
            }

            if (args.Length == 1)
            {
                Console.WriteLine($"Title: {epubBook.Title}");
                Console.WriteLine("Filtered Chapters (title contains a digit):");
                for (int i = 0; i < filteredChapters.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {filteredChapters[i].Title}");
                }
                return;
            }

            if (!int.TryParse(args[1], out int chapterIndex) || chapterIndex < 1 || chapterIndex > filteredChapters.Count)
            {
                Console.WriteLine("Error: Invalid chapter number.");
                return;
            }

            var navItem = filteredChapters[chapterIndex - 1];
            string navFilePath = navItem.Link?.ContentFilePath ?? "";

            var contentFile = !string.IsNullOrEmpty(navFilePath)
                ? epubBook.ReadingOrder.FirstOrDefault(f =>
                    string.Equals(f.FilePath.Replace('\\', '/'), navFilePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                : null;

            if (contentFile == null)
            {
                Console.WriteLine("Error: Chapter content not found in EPUB.");
                return;
            }

            bool asHtml = args.Length >= 3 && args.Any(arg => arg.Equals("-html", StringComparison.OrdinalIgnoreCase));

            string outputText = "";
            if (!asHtml)
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(contentFile.Content);
                var bodyNode = doc.DocumentNode.SelectSingleNode("//body");
                outputText = bodyNode?.InnerText.Trim() ?? "";
            }
            else
            {
                outputText = contentFile.Content.Trim();
            }
            Console.WriteLine(outputText);
            ClipboardService.SetText(outputText);
        }

        // Recursively flattens all chapters and subchapters into a single list
        static void FlattenChapters(IEnumerable<EpubNavigationItem> items, List<EpubNavigationItem> flatList)
        {
            foreach (var item in items)
            {
                flatList.Add(item);
                if (item.NestedItems != null && item.NestedItems.Count > 0)
                {
                    FlattenChapters(item.NestedItems, flatList);
                }
            }
        }

        // Keeps only chapters whose title contains a digit
        static List<EpubNavigationItem> ChapterFilter(List<EpubNavigationItem> chapters)
        {
            return chapters
                .Where(c =>
                    !string.IsNullOrEmpty(c.Title) &&
                    (c.Title.Any(char.IsDigit) ||
                    c.Title.Contains("epilogue", StringComparison.OrdinalIgnoreCase) ||
                    c.Title.Contains("prologue", StringComparison.OrdinalIgnoreCase)
                    )
                )
                .ToList();
        }
    }
}