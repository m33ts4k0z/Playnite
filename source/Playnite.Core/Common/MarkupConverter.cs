namespace Playnite.Common
{
    public class MarkupConverter : SDK.Data.IMarkupConverter
    {
        public string MarkdownToHtml(string markdown)
        {
            return Markdig.Markdown.ToHtml(markdown);
        }
    }
}
