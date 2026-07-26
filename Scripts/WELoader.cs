using Godot;
using Godot.Collections;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;

public partial class WELoader : Node
{
    [Export]
    PackedScene WallpaperPreviewScene;
    [Export]
    Texture2D PlaceholderImageTexture;

    [Signal]
    public delegate void WallpaperLoadedEventHandler(WallpaperPreview wallpaper);

    private static WELoader _instance;

    const string ProjectName = @"\project.json";

    public override void _Ready()
    {
        Globals.Preferences = LoadWEPrefs();
        Globals.LoadedWallpapers.Clear();
        var wallpapers = LoadWallpapersFrom(ToolsDataManager.WEWorkshopPath);
        wallpapers.AddRange(LoadWallpapersFrom(ToolsDataManager.WELocalPath));
        _instance = this;
    }

    public static string GetPrefsPath()
    {
        return ToolsDataManager.WEPrefsPath;
    }

    public List<WallpaperPreview> LoadWallpapersFrom(string path)
    {
        List<WallpaperPreview> res = [];
        var dir = DirAccess.Open(path);
        if (dir is null)
        {
            GD.PrintErr("Failed to open workshop path: " + DirAccess.GetOpenError());
            return res;
        }
        dir.ListDirBegin();
        string folderName;
        while ((folderName = dir.GetNext()) != "")
        {
            if (!dir.CurrentIsDir()) { continue; }
            string relativeProjectPath = folderName + ProjectName;
            if (!dir.FileExists(relativeProjectPath))
            {
                GD.Print("No project file in " + folderName);
                continue;
            }
            var fullFilePath = dir.GetCurrentDir() + "/" + relativeProjectPath;
            var file = FileAccess.Open(fullFilePath, FileAccess.ModeFlags.Read);
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
            info.DateModified = FileAccess.GetModifiedTime(fullFilePath);
            info.DateCreated = Convert.ToUInt64((System.IO.File.GetCreationTimeUtc(fullFilePath).Ticks / 10_000_000) - 62135596800);

            WallpaperPreview preview = WallpaperPreviewScene.Instantiate<WallpaperPreview>();
            preview.Info = info;
            preview.Name = preview.Info.Identifier;
            Globals.LoadedWallpapers.Add(info);

            var texture = LoadPreviewImage(preview, dir.GetCurrentDir() + "/" + folderName);
            Globals.LoadedWallpaperTextures[info.Title] = texture;
            preview.PreviewImage = texture;

            res.Add(preview);
            EmitSignal(SignalName.WallpaperLoaded, preview);
        }
        return res;
    }
    public static ulong BytesToUInt64(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length > 8)
            throw new ArgumentException("Must be 8 elements or fewer", nameof(bytes));

        ulong result = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            result |= (ulong)bytes[i] << (i * 8);
        }

        return result;
    }

    private void LoadCustomData(WallpaperInfo info) 
    {
        var data = ToolsDataManager.LoadCustomData(info.Identifier);
        info.LoadCustomData(data);
    }

    private Texture2D LoadPreviewImage(WallpaperPreview preview, string absoluteDir)
    {
        var previewPath = preview.Info.PreviewPath;
        if (previewPath == null)
        {
            return PlaceholderImageTexture;
        }

        var path = absoluteDir + "\\" + previewPath;
        
        Texture2D texture = null;
        if (previewPath.EndsWith(".gif"))
        {
            byte[] data = FileAccess.GetFileAsBytes(path);
            if (FileAccess.GetOpenError() != Error.Ok)
            {
                GD.PrintErr($"Failed to load image file data ({path}): " + FileAccess.GetOpenError());
                return PlaceholderImageTexture;
            }
            texture = AnimatedTextureFromBuffer(data);
        } 
        else
        {
            var image = Image.LoadFromFile(path);
            if (image is null)
            {
                GD.PrintErr($"Failed to load image {path}");
                return PlaceholderImageTexture;
            }
            texture = ImageTexture.CreateFromImage(image);
        }
        if (texture is null)
        {
            GD.PrintErr("Failed to load image: " + path);
            return PlaceholderImageTexture;
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
        var file = FileAccess.Open(ToolsDataManager.WEPrefsPath, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PrintErr($"Failed to open config file at {ToolsDataManager.WEPrefsPath}, reason: " + FileAccess.GetOpenError());
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
