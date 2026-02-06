using System;

namespace com.laganga.app.Shared.Services;

public class LayoutService
{
    public event Action? DrawerStateChanged;
    private bool _isDrawerOpen;
    
    public string Title { get; set; } = "";
    
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