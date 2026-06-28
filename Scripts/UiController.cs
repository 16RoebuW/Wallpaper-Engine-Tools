using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Globalization;
using Dictionary = Godot.Collections.Dictionary;

public partial class UiController : Node
{
    [Export]
    PackedScene TagOptionScene;
    [Export]
    Container ItemTagContainer;
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
    [Export]
    Control PlaylistCreator;
    [Export]
    Container FilterTagContainer;
    [Export]
    PackedScene FoldableTagContainerScene;

    private WallpaperInfo selectedWallpaper;

    private List<TagButton> detailTags = [];
    private List<TagButton> filterTags = [];

    private HashSet<TagButton> filteredDetailTags = [];
    
    private HashSet<string> filter = [];

    private string oldText = "longer_than_blank";

    private Button fakeFocussedNode;

    public override void _Ready()
    {
        var prefs = Globals.Preferences[Globals.UserName].AsGodotDictionary();
        if (prefs is null)
        {
            GD.PrintErr("Failed to load preferences!!");
            return;
        }

        LoadBuiltinTags(prefs);

        foreach (var kv in Globals.GetBuiltinTags())
        {
            NewDetailsTagNode(kv.Key, true, kv.Value, "Built in");
            NewFilterTagNode(kv.Key, kv.Value, "Built in");
        }

        LoadCustomTags();

        foreach (var kv in Globals.GetCustomTags())
        {
            NewDetailsTagNode(kv.Key, colour: kv.Value, category: "Custom");
            NewFilterTagNode(kv.Key, kv.Value, "Custom");
        }

        TagSearchBox.Editable = false;
    }

    private void OnFilterClearedPressed()
    {
        filter.Clear();
    }

    private void OnCreatePlaylistPressed()
    {
        PlaylistCreator.Show();
    }

    private void LoadCustomTags()
    {
        Globals.SetCustomTags(ToolsDataManager.LoadCustomTags());
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
            filteredDetailTags.Add(node);
        }
    }

    private TagButton AddCustomTag(string tag, Color? colour = null)
    {
        var tagColour = colour ?? new Color(1, 1, 1);
        Globals.AddCustomTag(tag, tagColour);
        ToolsDataManager.SaveCustomTags(Globals.GetCustomTags());
        NewFilterTagNode(tag, tagColour, category: "Custom");
        return NewDetailsTagNode(tag, colour: tagColour, category: "Custom");
    }

    private void RemoveCustomTag(string tag)
    {
        Globals.RemoveCustomTag(tag);
        ToolsDataManager.SaveCustomTags(Globals.GetCustomTags());
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
        var customTags = Globals.GetCustomTags();
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
            var prev = GetPreviousVisibleTag(fakeFocussedNode.GetParent().GetIndex());
            if (prev is not null)
            {
                FakeFocus(prev.CheckBox);
            }
        }
        else if (Input.IsActionJustPressed("ui_down") || Input.IsActionJustPressed("ui_focus_next")) 
        {
            var next = GetNextVisibleTag(fakeFocussedNode.GetParent().GetIndex());
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
            var current = Globals.LoadFromUntrustworthyDict(prefs, ["general", "browser", "filterinfo", "installed", "tags"]);
            Godot.Collections.Dictionary<string, Color> tags = [];

            foreach (var tag in current.AsGodotDictionary().Keys)
            {
                tags.Add(tag.AsString(), new Color(1,1,1));
            }
            Globals.SetBuiltinTags(tags);
        }
        catch (KeyNotFoundException e)
        {
            GD.PrintErr($"Failed to get tags: {e.Message}");
            return;
        }
    }

    private void LoadSpecialTags(Dictionary prefs, string name, Func<string, WallpaperInfo, bool> tagCriteria)
    {
        throw new NotImplementedException();
        try
        {
            var current = Globals.LoadFromUntrustworthyDict(prefs, ["general", "browser", "filterinfo", "installed", name]);
            Godot.Collections.Dictionary<string, Color> tags = [];

            foreach (var tag in current.AsGodotDictionary().Keys)
            {
                tags.Add(tag.AsString(), new Color(1, 1, 1));
            }
            Globals.SetBuiltinTags(tags);
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
            var colour = Globals.LoadFromUntrustworthyDict(wallpaper.WEData, ["general", "properties", "schemecolor", "value"]);
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

    private TagButton NewDetailsTagNode(string tag, bool disabled=false, Color? colour = null, string category = null)
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
        if (category != null)
        {
            if (ItemTagContainer.HasNode(category))
            {
                ItemTagContainer.GetNode(category).GetChild(0).AddChild(node);
            } else {
                var newCategory = FoldableTagContainerScene.Instantiate<FoldableContainer>();
                newCategory.Title = category;
                newCategory.Name = category;
                ItemTagContainer.AddChild(newCategory);
                newCategory.GetChild(0).AddChild(node);
            }
        } else {
            ItemTagContainer.AddChild(node);
        }
        detailTags.Add(node);
        return node;
    }

    private TagButton NewFilterTagNode(string tag, Color? colour = null, string category = null)
    {
        TagButton node = TagOptionScene.Instantiate<TagButton>();
        node.CheckBox.Text = tag;
        node.Name = tag;
        node.CheckBox.Toggled += (toggledOn) => OnFilterTagToggled(node, toggledOn);
        if (colour is Color notNull)
        {
            node.ColorPicker.Color = notNull;
            node.Icon.SelfModulate = notNull;
        }
        node.ColorPicker.Hide();
        if (category != null)
        {
            if (FilterTagContainer.HasNode(category))
            {
                FilterTagContainer.GetNode(category).GetChild(0).AddChild(node);
            }
            else
            {
                var newCategory = FoldableTagContainerScene.Instantiate<FoldableContainer>();
                newCategory.Title = category;
                newCategory.Name = category;
                FilterTagContainer.AddChild(newCategory);
                newCategory.GetChild(0).AddChild(node);
            }
        }
        else
        {
            FilterTagContainer.AddChild(node);
        }
        filterTags.Add(node);
        return node;
    }

    private void OnFilterTagToggled(TagButton node, bool toggledOn)
    {

    }

    private void RemoveTagFromDetailsPane(string tag)
    {
        foreach (TagButton node in detailTags)
        {
            if (node.Name == tag)
            {
                node.QueueFree();
                return;
            }
        }
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
            bool found = false;
            foreach (var tagButton in detailTags)
            {
                tagButton.CheckBox.SetPressedNoSignal(pressed);
                found = true;
                break;
            }

            if (!found)
            {
                GD.PrintErr($"Failed to get node for tag {tag}");
                continue;
            }
        }
    }

    public void OnTagSearchTextChanged(string newText)
    {
        if (newText.Length < oldText.Length)
        {
            filteredDetailTags.Clear();
            foreach (TagButton tag in detailTags)
            {
                if (tag.Name.ToString().Contains(newText, StringComparison.InvariantCultureIgnoreCase))
                {
                    filteredDetailTags.Add(tag);
                    tag.Visible = true;
                }
                else
                {
                    tag.Visible = false;
                }
            }
        }
        else
        {
            foreach (TagButton tag in filteredDetailTags)
            {
                if (tag.Name.ToString().Contains(newText, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }
                filteredDetailTags.Remove(tag);
                tag.Visible = false;
            }
        }

        var topTag = GetNextVisibleTag(-1);
        if (topTag is not null)
        {
            FakeFocus(topTag.CheckBox);
        }

        oldText = newText;
    }

    private TagButton GetNextVisibleTag(int from)
    {
        for (int i = from + 1; i < detailTags.Count; i++)
        {
            var tag = detailTags[i];
            if (tag.Visible == true)
            {
                return tag;
            }
        }
        // Loop round
        for (int i = 0; i <= from; i++)
        {
            var tag = detailTags[i];
            if (tag.Visible == true)
            {
                return tag;
            }
        }
        return null;
    }
    
    private TagButton GetPreviousVisibleTag(int from)
    {
        for (int i = from - 1; i >= 0; i--)
        {
            var tag = detailTags[i];
            if (tag.Visible == true)
            {
                return tag;
            }
        }
        // Loop round
        for (int i = detailTags.Count - 1; i >= from; i--)
        {
            var tag = detailTags[i];
            if (tag.Visible == true)
            {
                return tag;
            }
        }
        return null;
    }
}
