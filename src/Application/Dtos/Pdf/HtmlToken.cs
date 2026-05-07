namespace CitizenPortal.Application.Dtos;

public class HtmlToken
{
    public string Text { get; }
    public bool Bold { get; }
    public bool Underline { get; }
    public bool Italic { get; }
    public int FontSize { get; }

    public HtmlToken(string text, bool bold, bool underline, bool italic, int fontSize)
        => (Text, Bold, Underline, Italic, FontSize) = (text, bold, underline, italic, fontSize);
}
