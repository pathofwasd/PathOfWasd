using System.Windows;
using System.Windows.Input;
using PathOfWASD.Internals;
using PathOfWASD.Managers.Cursor.Interfaces;
using PathOfWASD.Overlays.Cursor.Interfaces;
using PathOfWASD.Overlays.Settings.Models;
using WindowsInput;
using Application = System.Windows.Application;

namespace PathOfWASD.Managers.Cursor;

/// <summary>
/// Owns the virtual-cursor coordinates and the real-cursor lock and unlock flow.
/// </summary>
public class CursorManager
{
    private bool _startInputHandler = true;

    public bool UserControlsRealCursor = false;
    private RawMouseInputHandler _inputHandler;

    private readonly ICursorOverlay _overlayWindow;
    
    private readonly InputSimulator _sim = new();
    public ICursorState State { get; }
    
    /// <summary>
    /// Creates the cursor manager and waits for the renderer to create its native window.
    /// </summary>
    public CursorManager(
        ICursorOverlay overlay,
        ICursorState state)
    {
        _overlayWindow = overlay;
        State = state;
        
        _overlayWindow.SourceInitialized += OnSourceInitialized;
    }
    
    /// <summary>
    /// Starts raw mouse input once the renderer has created its native window.
    /// </summary>
    private void OnSourceInitialized(object sender, EventArgs e)
    {
        _inputHandler = new RawMouseInputHandler(OnMouseMoved);
        _inputHandler.Start();

        _overlayWindow.SourceInitialized -= OnSourceInitialized;
    }
    
    /// <summary>
    /// Moves the real cursor to the current virtual-cursor position.
    /// </summary>
    public async Task JumpToVirtualCursor()
    {
        int virtualPixelX = (int)Math.Round(State.VirtualX * State.DpiScaleX);
        int virtualPixelY = (int)Math.Round(State.VirtualY * State.DpiScaleY);
        Win32.SetCursorPos(virtualPixelX, virtualPixelY);
        

    }

    /// <summary>
    /// Returns control to the real cursor and hides the virtual cursor renderer.
    /// </summary>
    public async Task UnlockRealCursor(bool isClick = false)
    {
        await JumpToVirtualCursor();
        UserControlsRealCursor = true;
        
      Application.Current.Dispatcher.Invoke(() =>
      {
          _overlayWindow.HideOverlay(State.CursorMode, false, isClick);
      });
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Stops virtual-cursor mode and tears down the supporting hooks.
    /// </summary>
    public async Task StopUnlockRealCursor()
    {
        await JumpToVirtualCursor();
        UserControlsRealCursor = true;
        
          Application.Current.Dispatcher.Invoke(() =>
          {
              _overlayWindow.HideOverlay(CursorMode.ShowDuringSkills, true);
          });
          Application.Current.Dispatcher.Invoke(MouseHook.StopHook);
          if (_inputHandler != null)
          {
              _inputHandler.Stop();
          }
              

        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Enters virtual-cursor mode and shows the active renderer.
    /// </summary>
    public async Task LockRealCursor(bool leftMouse = false, bool rightMouse = false, bool fromToggle = false)
    {
        var initialVirtualX = 0.0;
        var initialVirtualY = 0.0;
        if (State.VirtualX == 0 || State.VirtualY == 0)
        {
            Win32.GetCursorPos(out Win32.POINT p);
            State.VirtualX = p.X / State.DpiScaleX;
            State.VirtualY = p.Y / State.DpiScaleY;
            initialVirtualX = State.VirtualX;
            initialVirtualY = State.VirtualY;
        }
        

        if (leftMouse)
        {
            _sim.Mouse.LeftButtonUp();
        }
        else if (rightMouse)
        {
            _sim.Mouse.RightButtonUp();
        }

     UserControlsRealCursor = false;
        Application.Current.Dispatcher.Invoke(MouseHook.StartHook);
        Application.Current.Dispatcher.Invoke(() =>
        {
            _overlayWindow.ShowOverlay(fromToggle ? CursorMode.ShowDuringSkills : State.CursorMode);
            if (initialVirtualX != 0.0 || initialVirtualY != 0.0)
            {
                _overlayWindow.UpdateCursorPosition(State.VirtualX, State.VirtualY, UserControlsRealCursor, State.XCursorCenterAdjustment, State.YCursorCenterAdjustment);
            }
        });
       _inputHandler.Start();

    }
    
    /// <summary>
    /// Moves the real cursor to the configured midpoint plus any active WASD offset.
    /// </summary>
    public async Task SetDirectionalCursorPosition(List<Key> wasdKeys)
    {
        double offset = State.Offset;

        double middleX = State.MidPoint.X;
        double middleY = State.MidPoint.Y;

        double targetX = middleX;
        double targetY = middleY;

        if (wasdKeys.Contains(Key.W))
        {
            targetY = middleY - offset;
        }
        if (wasdKeys.Contains(Key.S))
        {
            targetY = middleY + offset;
        }
        if (wasdKeys.Contains(Key.A))
        {
            targetX = middleX - offset;
        }
        if (wasdKeys.Contains(Key.D))
        {
            targetX = middleX + offset;
        }

        State.RealX = targetX;
        State.RealY = targetY;

        int realX = (int)Math.Round(State.RealX * State.DpiScaleX);
        int realY = (int)Math.Round(State.RealY * State.DpiScaleY);

        Win32.SetCursorPos((int)realX, (int)realY);
    }

    /// <summary>
    /// Applies raw mouse deltas to the virtual cursor and forwards the result to the renderer.
    /// </summary>
    private void OnMouseMoved(int deltaX, int deltaY)
    {
        if (_startInputHandler)
        {
            double dipDX = deltaX / State.DpiScaleX;
            double dipDY = deltaY / State.DpiScaleY;
        
            State.VirtualX += dipDX  * (State.Sensitivity / 1000);
            State.VirtualY += dipDY  * (State.Sensitivity / 1000);
        
            State.VirtualX = Math.Clamp(State.VirtualX, SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 1);

            State.VirtualY = Math.Clamp(State.VirtualY, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 1);

            Application.Current.Dispatcher.Invoke(() =>
            {
                _overlayWindow.UpdateCursorPosition(State.VirtualX, State.VirtualY, UserControlsRealCursor, State.XCursorCenterAdjustment, State.YCursorCenterAdjustment);
            });
        }
    }
}
