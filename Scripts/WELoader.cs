using Godot;
using Godot.Collections;
using Godot.NativeInterop;
using System;

public partial class WELoader : Node
{
    [Export]
    public string WEWorkshopPath = @"C:\Program Files (x86)\Steam\steamapps\workshop\content\431960";
    [Export]
    public string WEPrefsPath = @"C:\Program Files (x86)\Steam\steamapps\common\wallpaper_engine\config.json";
    [Export]
    Container WallpaperPreviewContainer;
    [Export]
    PackedScene WallpaperPreviewScene;

    [Signal]
    public delegate void WallpaperLoadedEventHandler(WallpaperPreview wallpaper);

    private static WELoader _instance;

    const string ProjectName = @"\project.json";

    public override void _Ready()
    {
        Globals.Preferences = LoadWEPrefs();
        LoadWorkshopItems();
        _instance = this;
    }

    public static string GetPrefsPath()
    {
        return _instance.WEPrefsPath;
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
            var file = FileAccess.Open(dir.GetCurrentDir() + "/" + relativeProjectPath, FileAccess.ModeFlags.Read);
            if (file is null)
            {
                GD.PrintErr($"Failed to open project file at {relativeProjectPath}, reason: " + FileAccess.GetOpenError());
                continue;
            }
            var project = file.GetAsText();
            file.Close();
            var info = WallpaperInfo.From(project, dir.GetCurrentDir() + "/" + folderName);
            if (info is null)
            {
                GD.PrintErr($"Invalid project file at {relativeProjectPath}");
                continue;
            }
            LoadCustomData(info);

            WallpaperPreview preview = WallpaperPreviewScene.Instantiate<WallpaperPreview>();
            preview.Info = info;
            Globals.LoadedWallpapers.Add(info);
            var texture = LoadPreviewImage(preview, dir.GetCurrentDir() + "/" + folderName);
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
    
    private string GetCurrentUser()
    {
        if (OS.HasFeature("windows"))
        {
            string path = OS.GetEnvironment("USERPROFILE");
            return path[(path.LastIndexOf('\\') + 1)..];
        }
        else
        {
            string path = OS.GetEnvironment("HOME");
            return path[(path.LastIndexOf('/') + 1)..];
        }
    }

    private Dictionary LoadWEPrefs()
    {
        var file = FileAccess.Open(WEPrefsPath, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PrintErr($"Failed to open config file at {WEPrefsPath}, reason: " + FileAccess.GetOpenError());
            return null; ;
        }
        var prefs = file.GetAsText();
        file.Close();
        Globals.PreferencesString = prefs;
        var parser = new Json();
        var err = parser.Parse(prefs);
        if (err != Error.Ok)
        {
            GD.PrintErr(parser.GetErrorMessage());
            return null;
        }
        var dict = parser.Data.AsGodotDictionary();
        var user = GetCurrentUser();
        if (!dict.ContainsKey(user))
        {
            GD.PrintErr($"User '{user}' not found in Wallpaper Engine preferences! Top level keys: {String.Join(",", dict.Keys)}");
            return null;
        }
        Globals.UserName = user;
        return dict;
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
