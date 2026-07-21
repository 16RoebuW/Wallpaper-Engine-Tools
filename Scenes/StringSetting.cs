using Godot;
using System;

public partial class StringSetting : Container
{
    [Export]
    public Label Title;
    [Export]
    public Label Error;
    [Export]
    public LineEdit Editor;
    [Export]
    public Button ResetButton;
    [Export]
    Timer debouncer;

    [Signal]
    public delegate void DebouncedTextChangedEventHandler(string newText);

    private void LineEditTextChanged(string newText)
    {
        debouncer.Start();
    }

    private void LineEditTextSubmitted(string newText)
    {
        EmitSignal(SignalName.DebouncedTextChanged, Editor.Text);
    }

    private void TimerTimeout()
    {
        EmitSignal(SignalName.DebouncedTextChanged, Editor.Text);
    }
}
