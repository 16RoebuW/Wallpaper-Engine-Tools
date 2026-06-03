using Godot;
using System;

public partial class WallpaperPreview : PanelContainer
{
    [Export]
    public TextureRect Preview;
    [Export]
    public Label Label;
    public WallpaperInfo Info { get; set; }

    public Texture2D PreviewImage = null;

    public override void _Ready()
    {
        Label.Text = Info.Title;
        if (PreviewImage != null)
        {
            Preview.Texture = PreviewImage;
        }
    }
}
