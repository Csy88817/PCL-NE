using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>工具测试页的视图模型。</summary>
public partial class PageToolsTestViewModel : ViewModelBase
{
    [ObservableProperty] private bool _achievementButtonsEnabled = true;

    [ObservableProperty] private bool _achievementImageVisible;

    [ObservableProperty] private bool _crashButtonVisible;

    [ObservableProperty] private bool _downloadOpenEnabled = true;

    [ObservableProperty] private bool _downloadStartEnabled = true;

    [ObservableProperty] private bool _openFolderHintVisible = true;

    [ObservableProperty] private bool _openInstanceFolderEnabled;

    [ObservableProperty] private bool _skinPreviewVisible;

    public PageToolsTestViewModel()
    {
#if DEBUG
        CrashButtonVisible = true;
#endif
    }

    public void RefreshAchievementButtons(bool blockValid, bool titleValid, bool string1Valid)
    {
        AchievementButtonsEnabled = blockValid && titleValid && string1Valid;
    }

    public void RefreshDownloadButtons(bool folderValid, bool urlValid, bool nameValid)
    {
        DownloadStartEnabled = folderValid && urlValid && nameValid;
        DownloadOpenEnabled = folderValid;
    }

    /// <summary>根据是否存在当前实例，刷新「一键打开目录」中依赖实例的按钮状态。</summary>
    public void RefreshOpenFolderButtons(bool hasInstance)
    {
        OpenInstanceFolderEnabled = hasInstance;
        OpenFolderHintVisible = !hasInstance;
    }
}
