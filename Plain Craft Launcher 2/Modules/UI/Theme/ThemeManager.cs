using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using PCL.Core.App;
using PCL.Core.UI.Theme;

namespace PCL;

public static class ThemeManager
{
    private static bool _contextMenuHandlerRegistered;

    public static bool IsDarkMode => ThemeService.IsDarkMode;

    public static ResourceDictionary AppResources => System.Windows.Application.Current.Resources;

    public static ModBase.MyColor colorGray1 = new(AppResources["ColorObjectGray1"]);
    public static ModBase.MyColor colorGray4 = new(AppResources["ColorObjectGray4"]);
    public static ModBase.MyColor colorGray5 = new(AppResources["ColorObjectGray5"]);
    public static ModBase.MyColor colorSemiTransparent = new(AppResources["ColorBrushSemiTransparent"]);

    public static void ThemeRefresh(int newTheme = -1)
    {
        // 首次调用时（启动阶段）按系统主题应用一次
        InitFollowSystemTheme();
        colorGray1 = new ModBase.MyColor(AppResources["ColorObjectGray1"]);
        colorGray4 = new ModBase.MyColor(AppResources["ColorObjectGray4"]);
        colorGray5 = new ModBase.MyColor(AppResources["ColorObjectGray5"]);
        colorSemiTransparent = new ModBase.MyColor(AppResources["ColorBrushSemiTransparent"]);
        ThemeRefreshMain();
    }

    public static void ThemeRefreshMain()
    {
        ModBase.RunInUi(() =>
        {
            if (!ModMain.frmMain.IsLoaded) return;
            RefreshBackground();
            RefreshAllContextMenuThemes();
        });
    }
    
    // 主页面背景
    private static void RefreshBackground()
    {
        if (Config.Preference.Background.BackgroundColorful)
        {
            var brush = new LinearGradientBrush
            {
                EndPoint = new Point(0.1, 1),
                StartPoint = new Point(0.9, 0)
            };

            var hue = ThemeService.GetCurrentThemeArgs().Hue;
            var hue1 = hue - 15;
            var hue2 = hue + 15;
            var tone = ThemeService.CurrentTone;
            var darkLight = IsDarkMode ? 0.2d : 1d;
            brush.GradientStops.Add(new GradientStop
                { Offset = -0.1d, Color = LabColor.FromLch(0.84d * darkLight, tone.C5, hue1) });
            brush.GradientStops.Add(new GradientStop
                { Offset = 0.4d, Color = LabColor.FromLch(0.96d * darkLight, tone.C7, hue) });
            brush.GradientStops.Add(new GradientStop
                { Offset = 1.1d, Color = LabColor.FromLch(0.84d * darkLight, tone.C5, hue2) });
            ModMain.frmMain.PanForm.Background = brush;
        }
        else
        {
            ModMain.frmMain.PanForm.Background = (Brush)System.Windows.Application.Current.Resources["ColorBrushBackground"];
        }

        ModMain.frmMain.PanForm.Background.Freeze();
    }

    // 通用ContextMenu主题刷新
    private static void RefreshAllContextMenuThemes()
    {
        try
        {
            if (!_contextMenuHandlerRegistered)
            {
                EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent,
                    new RoutedEventHandler(OnContextMenuOpened));
                _contextMenuHandlerRegistered = true;
            }

            foreach (Window window in System.Windows.Application.Current.Windows)
                RefreshContextMenusInElement(window);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新ContextMenu主题时出错");
        }
    }

    private static void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is ContextMenu contextMenu)
            {
                contextMenu.ClearValue(FrameworkElement.StyleProperty);
                contextMenu.UpdateDefaultStyle();
            }
        }
        catch
        {
            // 忽略个别错误
        }
    }

    private static void RefreshContextMenusInElement(DependencyObject element)
    {
        if (element is null)
            return;

        try
        {
            if (element is FrameworkElement { ContextMenu: not null } fe)
            {
                fe.ContextMenu.ClearValue(FrameworkElement.StyleProperty);
                fe.ContextMenu.UpdateDefaultStyle();
            }

            var childrenCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childrenCount; i++)
                RefreshContextMenusInElement(VisualTreeHelper.GetChild(element, i));
        }
        catch
        {
            // 忽略个别元素的错误，继续处理其他元素
        }
    }

    #region 主题颜色跟随系统

    private const string SystemThemeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

    private static bool _systemThemeWatcherRegistered;
    private static bool _followSystemThemeInitialized;
    private static bool? _lastAppliedSystemDark;

    /// <summary>
    /// 初始化「主题颜色跟随系统」：启动时按系统主题应用一次，并注册系统主题变化监听。<br/>
    /// 重复调用是安全的：监听器只会注册一次，启动阶段的主题应用也只会执行一次。
    /// </summary>
    public static void InitFollowSystemTheme()
    {
        EnsureSystemThemeWatcher();
        if (_followSystemThemeInitialized)
            return;
        _followSystemThemeInitialized = true;
        ApplySystemTheme(true);
    }

    /// <summary>
    /// 立即按当前系统主题应用一次主题（供设置页开关使用）。<br/>
    /// 配置项 <c>Config.Preference.Theme.FollowSystemTheme</c> 为关闭状态时不执行任何操作。
    /// </summary>
    public static void ApplyFollowSystemTheme()
    {
        ApplySystemTheme(true);
    }

    /// <summary>
    /// 注册系统主题变化监听。仅注册一次，退出时反注册，避免重复订阅造成泄漏。
    /// </summary>
    private static void EnsureSystemThemeWatcher()
    {
        if (_systemThemeWatcherRegistered)
            return;

        try
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            if (System.Windows.Application.Current is { } app)
                app.Exit += OnApplicationExit;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            _systemThemeWatcherRegistered = true;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Theme] 注册系统主题变化监听失败");
        }
    }

    /// <summary>
    /// 退出时反注册系统主题变化监听。
    /// </summary>
    private static void UnhookSystemThemeWatcher()
    {
        if (!_systemThemeWatcherRegistered)
            return;

        try
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            if (System.Windows.Application.Current is { } app)
                app.Exit -= OnApplicationExit;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        }
        catch
        {
            // 退出阶段无需处理异常
        }
        finally
        {
            _systemThemeWatcherRegistered = false;
        }
    }

    private static void OnApplicationExit(object? sender, ExitEventArgs e)
    {
        UnhookSystemThemeWatcher();
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        UnhookSystemThemeWatcher();
    }

    /// <summary>
    /// 系统偏好变化。该事件在 SystemEvents 的独立线程上触发，因此只做筛选后转发到 UI 线程处理。
    /// </summary>
    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        try
        {
            // 只关心与配色相关的类别（ImmersiveColorSet 属于 General），忽略字体、鼠标等无关变化
            if (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.Color)
                return;
            ApplySystemTheme(false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Theme] 处理系统主题变化失败");
        }
    }

    /// <summary>
    /// 按系统主题切换启动器的浅色/深色主题。<br/>
    /// 切换动作走既有路径：写入配色模式配置项 <c>Config.Preference.Theme.ColorMode</c>，再触发
    /// <see cref="ThemeService.RefreshColorMode" /> 与 <see cref="ThemeRefresh" /> 刷新资源。
    /// </summary>
    /// <param name="force">为 <c>true</c> 时即使系统主题未发生变化也重新应用一次。</param>
    private static void ApplySystemTheme(bool force)
    {
        ModBase.RunInUi(() =>
        {
            try
            {
                if (!Config.Preference.Theme.FollowSystemTheme)
                    return;

                var isSystemDark = ReadSystemDarkMode();
                if (!force && _lastAppliedSystemDark == isSystemDark)
                    return;
                _lastAppliedSystemDark = isSystemDark;

                var targetMode = isSystemDark ? ColorMode.Dark : ColorMode.Light;
                if (Config.Preference.Theme.ColorMode != targetMode)
                    Config.Preference.Theme.ColorMode = targetMode;

                ThemeService.RefreshColorMode();
                ThemeRefresh();
                ModBase.Log($"[Theme] 已跟随系统主题切换为{(isSystemDark ? "深色" : "浅色")}模式");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Theme] 跟随系统主题失败");
            }
        });
    }

    /// <summary>
    /// 读取系统当前的应用程序主题是否使用深色（AppsUseLightTheme：1 为浅色，0 为深色）。
    /// </summary>
    /// <returns>注册表不可用时按浅色处理。</returns>
    private static bool ReadSystemDarkMode()
    {
        using var registryKey = Registry.CurrentUser.OpenSubKey(SystemThemeRegistryPath);
        if (registryKey?.GetValue(AppsUseLightThemeValueName) is int value)
            return value == 0;
        return false;
    }

    #endregion
}