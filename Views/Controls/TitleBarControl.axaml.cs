using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace SyncDeck.Views.Controls;

public partial class TitleBarControl : UserControl
{
    public event EventHandler? MinimizeClicked;
    public event EventHandler? MaximizeClicked;
    public event EventHandler? CloseClicked;
    public event EventHandler? SettingsClicked;

    public TitleBarControl()
    {
        InitializeComponent();

        SettingsBtn.Click  += (_, _) => SettingsClicked?.Invoke(this, EventArgs.Empty);
        MinimizeBtn.Click  += (_, _) => MinimizeClicked?.Invoke(this, EventArgs.Empty);
        MaximizeBtn.Click  += (_, _) => MaximizeClicked?.Invoke(this, EventArgs.Empty);
        CloseBtn.Click     += (_, _) => CloseClicked?.Invoke(this, EventArgs.Empty);
    }
}
