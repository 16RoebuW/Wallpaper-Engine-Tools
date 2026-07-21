using Godot;
using System;

public partial class SettingsEditor : Control
{
    [Export]
    VBoxContainer settingsContainer;

    [Export]
    PackedScene stringSetting;

    public override void _Ready()
    {
        foreach (var setting in ToolsDataManager.GetUserSettings())
        {
            if (setting.PropertyType == typeof(string))
            {
                var node = stringSetting.Instantiate<StringSetting>();
                node.Title.Text = setting.HumanReadableName;
                node.Editor.Text = setting.Value as string;

                void updateSetting(string newText)
                {
                    node.Error.Text = "";
                    try
                    {
                        if (newText != setting.Value as string)
                        {
                            setting.Value = newText;
                        }
                    }
                    catch (Exception ex)
                    {
                        node.Error.Text = ex.InnerException.Message;
                    }
                }

                node.DebouncedTextChanged += updateSetting;
                node.ResetButton.Pressed += () =>
                {
                    string defaultValue = setting.DefaultValue as string;
                    node.Editor.Text = defaultValue;
                    updateSetting(defaultValue);
                };
                settingsContainer.AddChild(node);
            }
        }
    }

    public override void _ExitTree()
    {
        ToolsDataManager.SaveUserSettings();
    }

    private void DonePressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/wallpapers.tscn");
    }
}
