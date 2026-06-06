using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System;

using Dictionary = Godot.Collections.Dictionary;

public partial class UiController : Node
{
    [Export]
    public string WEPrefsPath = @"C:\Program Files (x86)\Steam\steamapps\common\wallpaper_engine\config.json";
    [Export]
    PackedScene TagOptionScene;
    [Export]
    Container TagContainer;
    [Export]
    LineEdit TagSearchBox;
    [Export]
    PopupMenu TagSearchPopup;
    [Export]
    Label DetailsTitle;
    [Export]
    TextureRect DetailsPreview;

    private HashSet<string> builtinTags = [];
    private WallpaperInfo lastWallpaper;

    public override void _Ready()
    {
        var prefs = LoadPrefs();
        if (prefs is null)
        {
            GD.PrintErr("Failed to load preferences!!");
            return;
        }

        LoadBuiltinTags(prefs);

        foreach (var tag in builtinTags)
        {
            AddTagNodeToDetailsPane(tag, true);
        }
    }

    private void LoadBuiltinTags(Dictionary prefs)
    {
        string[] path = ["general", "browser", "filterinfo", "installed", "tags"];
        var current = prefs;
        foreach (var part in path) { 
            if (!current.ContainsKey(part))
            {
                GD.PrintErr($"Failed to get tags: key '{part}' not found");
                return;
            }
            current = current[part].AsGodotDictionary();
        };

        foreach (var tag in current.Keys) { 
            builtinTags.Add(tag.AsString());
        }
    }

    private void AddTagNodeToDetailsPane(string tag, bool disabled=false)
    {
        TagButton node = TagOptionScene.Instantiate<TagButton>();
        node.CheckBox.Text = tag;
        node.CheckBox.Disabled = disabled;
        node.Name = tag;
        TagContainer.AddChild(node);
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

    private Dictionary LoadPrefs()
    {
        var file = FileAccess.Open(WEPrefsPath, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PrintErr($"Failed to open config file at {WEPrefsPath}, reason: " + FileAccess.GetOpenError());
            return null; ;
        }
        var prefs = file.GetAsText();
        file.Close();
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
        return dict[user].AsGodotDictionary();
    }

    public void OnWallpaperLoaded(WallpaperPreview wallpaper)
    {
        wallpaper.Pressed += () => OnWallpaperSelected(wallpaper);
    }

    private void OnWallpaperSelected(WallpaperPreview wallpaper)
    {
        if (lastWallpaper is not null)
        {
            SetDetailsTagsState(lastWallpaper.Tags, false);
        }

        DetailsTitle.Text = wallpaper.Info.Title;
        DetailsPreview.Texture = wallpaper.PreviewImage;
        SetDetailsTagsState(wallpaper.Info.Tags, true);
        lastWallpaper = wallpaper.Info;
    }

    private void SetDetailsTagsState(HashSet<string> tags, bool pressed)
    {
        foreach (string tag in tags)
        {
            TagButton node = TagContainer.GetNodeOrNull<TagButton>(tag);
            if (node is null)
            {
                GD.PrintErr($"Failed to get node for tag {tag}");
                continue;
            }
            node.CheckBox.ButtonPressed = pressed;
        }
    }
}
