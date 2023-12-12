namespace Gpfm.Desktop.MarkupExtensions;

public enum IconName
{
    Add,
    Delete,
    Edit,
    OpenFolder,
}

public class Icon : IMarkupExtension<ImageSource>
{
    public required IconName Name { get; set; }
    public required string Size { get; set; } = "Default";

    private readonly FontSizeConverter _fontSizeConverter = new();

    public ImageSource ProvideValue(IServiceProvider serviceProvider)
    {
        return new FontImageSource
        {
            FontFamily = "FontAwesomeRegular",
            Glyph = Name switch
            {
                IconName.Add => "\u002b",
                IconName.Delete => "\uf2ed",
                IconName.Edit => "\uf044",
                IconName.OpenFolder => "\uf07c",
                _ => throw new InvalidOperationException(),
            },
            Size = (double)_fontSizeConverter.ConvertFrom(null, null, Size)!,
        };
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
    {
        return (this as IMarkupExtension<ImageSource>).ProvideValue(serviceProvider);
    }
}
