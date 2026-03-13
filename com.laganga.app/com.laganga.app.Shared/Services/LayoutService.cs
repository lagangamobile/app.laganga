using System;

namespace com.laganga.app.Shared.Services;

public class LayoutService
{
    public event Action? DrawerStateChanged;
    public event Action? StateChanged;
    private bool _isDrawerOpen;
    private string _title = "";

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value)
                return;
            _title = value;
            StateChanged?.Invoke();
        }
    }

    public bool IsDrawerOpen
    {
        get => _isDrawerOpen;
        set
        {
            if (_isDrawerOpen == value)
                return;
            _isDrawerOpen = value;
            DrawerStateChanged?.Invoke();
        }
    }

    public void ToggleDrawer() => IsDrawerOpen = !IsDrawerOpen;

    public void OpenDrawer() => IsDrawerOpen = true;

    public void CloseDrawer() => IsDrawerOpen = false;
}