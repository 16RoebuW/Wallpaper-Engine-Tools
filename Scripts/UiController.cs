using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    Label DetailsTitle;
    [Export]
    TextureRect DetailsPreview;
    [Export]
    Theme MainTheme;
    [Export]
    Container WallpaperContainer;

    private Godot.Collections.Dictionary<string, Color> builtinTags = [];
    private Godot.Collections.Dictionary<string, Color> customTags = [];
    private WallpaperInfo selectedWallpaper;

    private HashSet<TagButton> filteredTags = [];
    private string oldText = "longer_than_blank";

    private Button fakeFocussedNode;

    public override void _Ready()
    {
        var prefs = LoadWEPrefs();
        if (prefs is null)
        {
            GD.PrintErr("Failed to load preferences!!");
            return;
        }

        LoadBuiltinTags(prefs);

        foreach (var kv in builtinTags)
        {
            AddTagNodeToDetailsPane(kv.Key, true, kv.Value);
        }

        LoadCustomTags();

        foreach (var kv in customTags)
        {
            AddTagNodeToDetailsPane(kv.Key, colour: kv.Value);
        }

        TagSearchBox.Editable = false;
    }

    private void LoadCustomTags()
    {
        customTags = ToolsDataManager.LoadCustomTags();
    }

    public override void _Process(double delta)
    {
        if (TagSearchBox.HasFocus() && fakeFocussedNode is not null)
        {
            HandleDetailsTagFilterInput();
        }
    }

    public void OnDetailsTagToggled(TagButton detailsTag, bool tagged)
    {
        if (selectedWallpaper is null)
        {
            return;
        }
        SetItemTag(selectedWallpaper, detailsTag.Name, tagged);
    }

    public void OnAddTagPressed()
    {
        if (TagSearchBox.Text.Length > 0)
        {
            string formatted = TagSearchBox.Text;
            if (TagSearchBox.Text.ToLower() == TagSearchBox.Text)
            {
                formatted = TagSearchBox.Text[..1].ToUpper() + TagSearchBox.Text[1..];
            }
            var node = AddCustomTag(formatted, GetSchemeColor(selectedWallpaper));
            SetItemTag(selectedWallpaper, formatted, true);
            filteredTags.Add(node);
        }
    }

    private TagButton AddCustomTag(string tag, Color? colour = null)
    {
        var tagColour = colour ?? new Color(1, 1, 1);
        customTags.Add(tag, tagColour);
        ToolsDataManager.SaveCustomTags(customTags);
        return AddTagNodeToDetailsPane(tag, colour: tagColour);
    }

    private void RemoveCustomTag(string tag)
    {
        customTags.Remove(tag);
        ToolsDataManager.SaveCustomTags(customTags);
        foreach (Node node in WallpaperContainer.GetChildren())
        {
            if (node is WallpaperPreview preview)
            {
                SetItemTag(preview.Info, tag, false);
                ToolsDataManager.SaveCustomData(preview.Info.Identifier, GetCustomWallpaperData(preview.Info));
            }
        }
        RemoveTagFromDetailsPane(tag);
    }

    private Dictionary GetCustomWallpaperData(WallpaperInfo info)
    {
        Dictionary data = [];
        Array<string> tags = [];
        foreach (var tag in info.Tags)
        {
            if (customTags.ContainsKey(tag))
            {
                tags.Add(tag);
            }
        }
        data.Add("tags", tags);
        return data;
    }

    private void HandleDetailsTagFilterInput()
    {
        // Order matters here because shift+tab (focus prev) means that tab is pressed as well (focus next)
        if (Input.IsActionJustPressed("ui_up") || Input.IsActionJustPressed("ui_focus_prev"))
        {
            var prev = GetPreviousVisibleTag(fakeFocussedNode.GetParent().GetIndex(), TagContainer);
            if (prev is not null)
            {
                FakeFocus(prev.CheckBox);
            }
        }
        else if (Input.IsActionJustPressed("ui_down") || Input.IsActionJustPressed("ui_focus_next")) 
        {
            var next = GetNextVisibleTag(fakeFocussedNode.GetParent().GetIndex(), TagContainer);
            if (next is not null)
            {
                FakeFocus(next.CheckBox);
            }
        }
        else if (Input.IsActionJustPressed("ui_accept") && fakeFocussedNode.Disabled == false)
        {
            SetItemTag(selectedWallpaper, fakeFocussedNode.GetParent().Name, !fakeFocussedNode.ButtonPressed);
        }
    }

    private void SetItemTag(WallpaperInfo item, string tag, bool tagged)
    {
        if (tagged)
        {
            item.Tags.Add(tag);
        } 
        else
        {
            item.Tags.Remove(tag);
        }

        ToolsDataManager.SaveCustomData(item.Identifier, GetCustomWallpaperData(item));

        if (selectedWallpaper == item)
        {
            SetDetailsTagsState([tag], tagged);
        }
    }

    private void FakeFocus(Button node)
    {
        fakeFocussedNode?.RemoveThemeStyleboxOverride("disabled");
        fakeFocussedNode = node;
        var focusBox = MainTheme.GetStylebox("CheckBox", "focus");
        fakeFocussedNode.AddThemeStyleboxOverride("normal", focusBox);
        fakeFocussedNode.AddThemeStyleboxOverride("disabled", focusBox);
        fakeFocussedNode.AddThemeStyleboxOverride("pressed", focusBox);
    }

    private void LoadBuiltinTags(Dictionary prefs)
    {
        try
        {
            var current = LoadFromUntrustworthyDict(prefs, ["general", "browser", "filterinfo", "installed", "tags"]);

            foreach (var tag in current.AsGodotDictionary().Keys)
            {
                builtinTags.Add(tag.AsString(), new Color(1,1,1));
            }
        }
        catch (KeyNotFoundException e)
        {
            GD.PrintErr($"Failed to get tags: {e.Message}");
            return;
        }
    }
    
    private Color? GetSchemeColor(WallpaperInfo wallpaper)
    {
        try
        {
            var colour = LoadFromUntrustworthyDict(wallpaper.WEData, ["general", "properties", "schemecolor", "value"]);
            // Colour is in string "float float float" form. (or doubles perhaps)
            var floatStrings = colour.AsString().Split(' ');
            if (floatStrings.Length != 3)
            {
                return null;
            }
            float[] parsed = new float[floatStrings.Length];
            for (int i = 0; i < 3; i++)
            {
                parsed[i] = float.Parse(floatStrings[i], CultureInfo.InvariantCulture.NumberFormat);
            }
            return new Color(parsed[0], parsed[1], parsed[2]);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
        catch (FormatException e)
        {
            GD.PrintErr($"Failed to parse float: {e.Message}");
            return null;
        }
    }

    private Variant LoadFromUntrustworthyDict(Dictionary dictionary, string[] path)
    {
        Variant current = dictionary;
        foreach (var part in path)
        {
            if (!current.AsGodotDictionary().ContainsKey(part))
            {
                throw new KeyNotFoundException($"key '{part}' not found");
            }
            current = current.AsGodotDictionary()[part];
        };
        return current;
    }

    private TagButton AddTagNodeToDetailsPane(string tag, bool disabled=false, Color? colour = null)
    {
        TagButton node = TagOptionScene.Instantiate<TagButton>();
        node.CheckBox.Text = tag;
        node.CheckBox.Disabled = disabled;
        node.Name = tag;
        if (colour is Color notNull)
        {
            node.ColorPicker.Color = notNull;
            node.Icon.SelfModulate = notNull;
        }
        node.CheckBox.Toggled += (toggledOn) => OnDetailsTagToggled(node, toggledOn);
        TagContainer.AddChild(node);
        return node;
    }

    private void RemoveTagFromDetailsPane(string tag)
    {
        TagButton node = TagContainer.GetNode<TagButton>(tag);
        node.QueueFree();
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
        TagSearchBox.Editable = true;

        if (selectedWallpaper is not null)
        {
            SetDetailsTagsState(selectedWallpaper.Tags, false);
        }
        selectedWallpaper = wallpaper.Info;

        DetailsTitle.Text = wallpaper.Info.Title;
        DetailsPreview.Texture = wallpaper.PreviewImage;
        SetDetailsTagsState(wallpaper.Info.Tags, true);
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
            node.CheckBox.SetPressedNoSignal(pressed);
        }
    }

    public void OnTagSearchTextChanged(string newText)
    {
        if (newText.Length < oldText.Length)
        {
            filteredTags.Clear();
            foreach (Node node in TagContainer.GetChildren())
            {
                if (node is TagButton tag)
                {
                    if (tag.Name.ToString().Contains(newText, StringComparison.InvariantCultureIgnoreCase))
                    {
                        filteredTags.Add(tag);
                        tag.Visible = true;
                    }
                    else
                    {
                        tag.Visible = false;
                    }
                }
            }
        }
        else
        {
            foreach (TagButton tag in filteredTags)
            {
                if (tag.Name.ToString().Contains(newText, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }
                filteredTags.Remove(tag);
                tag.Visible = false;
            }
        }

        var topTag = GetNextVisibleTag(-1, TagContainer);
        if (topTag is not null)
        {
            FakeFocus(topTag.CheckBox);
        }

        oldText = newText;
    }

    private TagButton GetNextVisibleTag(int from, Container tagContainer)
    {
        for (int i = from + 1; i < tagContainer.GetChildCount(); i++)
        {
            var tag = tagContainer.GetChild<TagButton>(i);
            if (tag.Visible == true)
            {
                return tag;
            }
        }
        // Loop round
        for (int i = 0; i <= from; i++)
        {
            var tag = tagContainer.GetChild<TagButton>(i);
            if (tag.Visible == true)
            {
                return tag;
            }
        }
        return null;
    }
    
    private TagButton GetPreviousVisibleTag(int from, Container tagContainer)
    {
        for (int i = from - 1; i >= 0; i--)
        {
            var tag = tagContainer.GetChild<TagButton>(i);
            if (tag.Visible == true)
            {
                return tag;
            }
        }
        // Loop round
        for (int i = GetChildCount() - 1; i >= from; i--)
        {
            var tag = tagContainer.GetChild<TagButton>(i);
            if (tag.Visible == true)
            {
                return tag;
            }
        }
        return null;
    }
}
