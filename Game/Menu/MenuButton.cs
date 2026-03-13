using System.Drawing;

namespace SlayInspiredPrototype;

public sealed class MenuButton
{
    public string Id { get; }
    public string Label { get; set; }
    public Rectangle Rect { get; set; }
    public bool Enabled { get; set; } = true;

    public MenuButton(string id, string label, Rectangle rect)
    {
        Id = id;
        Label = label;
        Rect = rect;
    }

    public bool Contains(Point p) => Enabled && Rect.Contains(p);
}
