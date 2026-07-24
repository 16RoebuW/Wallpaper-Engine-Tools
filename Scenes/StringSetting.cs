using Godot;
using System;

public partial class StringSetting : Container
{
    [Export]
    public Label Title;
    [Export]
    public Label Error;
    [Export]
    public DebouncedLineEdit Editor;
    [Export]
    public Button ResetButton;
}
