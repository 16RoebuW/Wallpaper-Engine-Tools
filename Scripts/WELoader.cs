using Godot;
using Godot.NativeInterop;
using System;

public partial class WELoader : Node
{
    [Export]
    public string WEWorkshopPath = @"C:\Program Files (x86)\Steam\steamapps\workshop\content\431960";
    [Export]
    Container WallpaperPreviewContainer;
    [Export]
    PackedScene WallpaperPreviewScene;

    [Signal]
    public delegate void WallpaperLoadedEventHandler(WallpaperPreview wallpaper);

    const string ProjectName = @"\project.json";

    public override void _Ready()
    {
        LoadWorkshopItems();
    }

    public void LoadWorkshopItems()
    {
        var dir = DirAccess.Open(WEWorkshopPath);
        if (dir is null)
        {
            GD.PrintErr("Failed to open workshop path: " + DirAccess.GetOpenError());
            return;
        }
        dir.ListDirBegin();
        string folderName = "";
        while ((folderName = dir.GetNext()) != "")
        {
            if (!dir.CurrentIsDir()) { continue; }
            string relativeProjectPath = folderName + ProjectName;
            if (!dir.FileExists(relativeProjectPath))
            {
                GD.Print("No project file in " + folderName);
                continue;
            }
            var file = FileAccess.Open(dir.GetCurrentDir() + "\\" + relativeProjectPath, FileAccess.ModeFlags.Read);
            if (file is null)
            {
                GD.PrintErr($"Failed to open project file at {relativeProjectPath}, reason: " + FileAccess.GetOpenError());
                continue;
            }
            var project = file.GetAsText();
            file.Close();
            var info = WallpaperInfo.From(project, folderName);
            if (info is null)
            {
                GD.PrintErr($"Invalid project file at {relativeProjectPath}");
                continue;
            }
            LoadCustomData(info);

            WallpaperPreview preview = WallpaperPreviewScene.Instantiate<WallpaperPreview>();
            preview.Info = info;
            Globals.LoadedWallpapers.Add(info);
            var texture = LoadPreviewImage(preview, dir.GetCurrentDir() + "\\" + folderName);
            if (texture is not null)
            {
                Globals.LoadedWallpaperTextures[info.Title] = texture;
                preview.PreviewImage = texture;
            }
            WallpaperPreviewContainer.AddChild(preview);
            EmitSignal(SignalName.WallpaperLoaded, preview);
        }
    }

    private void LoadCustomData(WallpaperInfo info) 
    {
        var data = ToolsDataManager.LoadCustomData(info.Identifier);
        info.AddCustomData(data);
    }

    private Texture2D LoadPreviewImage(WallpaperPreview preview, string absoluteDir)
    {
        var previewPath = preview.Info.PreviewPath;
        if (previewPath == null)
        {
            return null;
        }

        var path = absoluteDir + "\\" + previewPath;
        
        Texture2D texture = null;
        if (previewPath.EndsWith(".gif"))
        {
            byte[] data = FileAccess.GetFileAsBytes(path);
            if (FileAccess.GetOpenError() != Error.Ok)
            {
                GD.PrintErr($"Failed to load image file data ({path}): " + FileAccess.GetOpenError());
                return null;
            }
            texture = AnimatedTextureFromBuffer(data);
        } 
        else
        {
            var image = Image.LoadFromFile(path);
            if (image is null)
            {
                GD.PrintErr($"Failed to load image {path}");
                return null;
            }
            texture = ImageTexture.CreateFromImage(image);
        }
        if (texture is null)
        {
            GD.PrintErr("Failed to load image: " + path);
            return null;
        }
        GD.Print($"Successfully loaded '{preview.Info.Title}'");
        return texture;
    }

    private AnimatedTexture AnimatedTextureFromFile(string path, int maxFrames = 0)
    {
        var gifManager = GetTree().Root.GetNode("GifManagerWrapper");
        var res = gifManager.Call("animated_texture_from_file", path, maxFrames);
        return res.As<AnimatedTexture>();
    }

    private AnimatedTexture AnimatedTextureFromBuffer(byte[] data, int maxFrames = 0)
    {
        var gifManager = GetTree().Root.GetNode("GifManagerWrapper");
        var res = gifManager.Call("animated_texture_from_buffer", data, maxFrames);
        if (res.Obj is null) { return null; }
        return res.As<AnimatedTexture>();
    }
}
