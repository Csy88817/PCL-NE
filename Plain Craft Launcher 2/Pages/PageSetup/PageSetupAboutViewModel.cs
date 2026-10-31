using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;
using PCL.Core.Utils;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面「关于」页的视图模型。</summary>
public partial class PageSetupAboutViewModel : ViewModelBase
{
    /// <summary>本仓库的 Release 页面，与 PCL.Core 的 BuildProvenance 校验目标保持一致。</summary>
    private const string ReleaseUrl = "https://github.com/Csy88817/PCL-NE/releases";

    private int _clickCount;

    private bool _initialized;

    private bool _buildInfoInitialized;

    [ObservableProperty] private string _aboutInfo = FormatVersionInfo();

    [ObservableProperty] private bool _showMcmod = true;

    [ObservableProperty] private bool _isLogoClickEnabled = true;

    #region 构建信息与自身 SHA-256

    [ObservableProperty] private string _buildInfoTitle = Localized("Setup.About.Build.Title", "构建信息");

    [ObservableProperty] private string _versionLabel = Localized("Setup.About.Build.Version", "启动器版本");

    [ObservableProperty] private string _commitLabel = Localized("Setup.About.Build.Commit", "构建来源");

    [ObservableProperty] private string _executableLabel = Localized("Setup.About.Build.Executable", "可执行文件");

    [ObservableProperty] private string _hashLabel = Localized("Setup.About.Build.Hash", "本程序 SHA-256");

    [ObservableProperty] private string _signatureTitle =
        Localized("Setup.About.Build.Signature.Title", "签名验证");

    [ObservableProperty] private string _copyButtonText = Localized("Setup.About.Build.Copy", "复制");

    [ObservableProperty] private string _openReleaseButtonText =
        Localized("Setup.About.Build.Signature.OpenRelease", "打开 Release 页面");

    [ObservableProperty] private string _versionInfo = "";

    [ObservableProperty] private string _commitInfo = "";

    [ObservableProperty] private string _executableInfo = "";

    /// <summary>分组显示用的哈希文本，或「计算中…」「不可用」。</summary>
    [ObservableProperty] private string _selfHashDisplay = Localized("Setup.About.Build.Hash.Computing", "计算中…");

    /// <summary>连续（不带空格）的完整哈希；不可用时为空。</summary>
    [ObservableProperty] private string? _selfHashPlain;

    [ObservableProperty] private bool _hasSelfHash;

    [ObservableProperty] private string _signatureInfo = "";

    #endregion

    public ObservableCollection<PageSetupAbout.GitHubContributor> Contributors { get; } = [];

    public void Initialize()
    {
        ShowMcmod = Lang.IsChineseMainland;
        if (!_buildInfoInitialized)
        {
            _buildInfoInitialized = true;
            LoadBuildInfoCommand.Execute(null);
        }

        if (_initialized) return;
        _initialized = true;
        LoadContributorsCommand.Execute(null);
    }

    [RelayCommand]
    public async Task LoadContributorsAsync()
    {
        var contributors = await RunAsync(ct => Task.Run(FetchContributorsAsync, ct));
        if (contributors is null) return;
        Contributors.Clear();
        foreach (var contributor in contributors)
            Contributors.Add(contributor);
    }

    [RelayCommand]
    public void RotateCommunityLogo(object? sender)
    {
        if (sender is null) return;
        ModAnimation.AniStart(new[] { ModAnimation.AaRotateTransform(sender, 360d) });
    }

    [RelayCommand]
    public async Task LogoClickAsync(object? sender)
    {
        if (!IsLogoClickEnabled || sender is null) return;
        try
        {
            if (_clickCount < 200)
            {
                _clickCount += 1;
                switch (_clickCount)
                {
                    case 5:
                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.NiceClick"));
                        break;
                    case 15:
                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.StillClicking"));
                        break;
                    case 25:
                        if (await ModMain.MyMsgBoxAsync(Lang.Text("Setup.About.EasterEgg.Bored.Message"),
                                Lang.Text("Setup.About.EasterEgg.Bored.Title"),
                                Lang.Text("Setup.About.EasterEgg.Bored.Yes"),
                                Lang.Text("Setup.About.EasterEgg.Bored.No")) == 2)
                            HintService.Hint(Lang.Text("Setup.About.EasterEgg.Bored.Response"));
                        break;
                    case 50:
                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.Encouragement"));
                        break;
                    case 75:
                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.HiddenTheme"));
                        break;
                    case 100:
                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.StillStaring"));
                        break;
                    case 130:
                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.NothingBehind"));
                        break;
                    case 150:
                        switch (await ModMain.MyMsgBoxAsync(Lang.Text("Setup.About.EasterEgg.Tired.Message1"),
                                    Lang.Text("Setup.About.EasterEgg.Tired.Title1"),
                                    Lang.Text("Setup.About.EasterEgg.Tired.Exhausted"),
                                    Lang.Text("Setup.About.EasterEgg.Tired.NotTired")))
                        {
                            case 1:
                                HintService.Hint(Lang.Text("Setup.About.EasterEgg.Tired.StopClicking"));
                                break;
                            case 2:
                                switch (await ModMain.MyMsgBoxAsync(
                                            Lang.Text("Setup.About.EasterEgg.Tired.Message2"),
                                            Lang.Text("Setup.About.EasterEgg.Tired.Title2"),
                                            Lang.Text("Setup.About.EasterEgg.Tired.Exhausted"),
                                            Lang.Text("Setup.About.EasterEgg.Tired.NotTired")))
                                {
                                    case 1:
                                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.Tired.StopClicking"));
                                        break;
                                    case 2:
                                        switch (await ModMain.MyMsgBoxAsync(
                                                    Lang.Text("Setup.About.EasterEgg.Tired.Message3"),
                                                    Lang.Text("Setup.About.EasterEgg.Tired.Title3"),
                                                    Lang.Text("Setup.About.EasterEgg.Tired.Exhausted"),
                                                    Lang.Text("Setup.About.EasterEgg.Tired.ReallyNotTired")))
                                        {
                                            case 1:
                                                HintService.Hint(
                                                    Lang.Text("Setup.About.EasterEgg.Tired.StopClicking"));
                                                break;
                                            case 2:
                                                HintService.Hint(
                                                    Lang.Text("Setup.About.EasterEgg.Tired.FinallyGiveUp"));
                                                break;
                                        }

                                        break;
                                }

                                break;
                        }

                        break;
                    case 200:
                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.ClickDisabled"));
                        IsLogoClickEnabled = false;
                        return;
                }

                var rand = new Random();
                var mx = rand.Next(-1, 1);
                if (mx == 0)
                    mx = 1;
                var my = rand.Next(-1, 1);
                if (my == 0)
                    my = 1;
                ModAnimation.AniStart(new[]
                {
                    ModAnimation.AaTranslateX(sender, mx, 0), ModAnimation.AaTranslateY(sender, my, 0),
                    ModAnimation.AaTranslateX(sender, -mx, 0, 100), ModAnimation.AaTranslateY(sender, -my, 0, 100)
                });
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "处理彩蛋点击时失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     填充构建信息，并在后台线程计算本次运行 exe 的 SHA-256。
    ///     任何失败都只会让哈希显示为「不可用」，不会抛出异常或弹窗。
    /// </summary>
    [RelayCommand]
    private async Task LoadBuildInfoAsync()
    {
        VersionInfo = Localized("Setup.About.Build.Version.Info", "版本 %VERSION%（内部代号 %VERSIONCODE%）")
            .Replace("%VERSION%", Basics.VersionName)
            .Replace("%VERSIONCODE%", Basics.VersionCode.ToString());
        CommitInfo = Localized("Setup.About.Build.Commit.Info", "分支 %BRANCH%，提交 %COMMIT%")
            .Replace("%BRANCH%", ModBase.versionBranchName)
            .Replace("%COMMIT%", ModBase.commitHashShort);

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            SelfHashDisplay = UnavailableText;
            return;
        }

        var exeName = Path.GetFileName(exePath);
        ExecutableInfo = exeName;
        SignatureInfo = Localized("Setup.About.Build.Signature.Info",
                "本程序可在 %URL% 的 Release 页面校验签名：签名文件名为 %SIGFILE%，可用 sigstore 客户端校验。")
            .Replace("%URL%", ReleaseUrl)
            .Replace("%SIGFILE%", exeName + ".sigstore.json");

        string? hash = null;
        try
        {
            hash = await Task.Run(() => ComputeSelfSha256(exePath)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "计算自身 SHA-256 失败", ModBase.LogLevel.Developer);
        }

        // 计算完成后回到 UI 线程回填界面控件
        void Apply()
        {
            if (string.IsNullOrEmpty(hash))
            {
                SelfHashPlain = null;
                SelfHashDisplay = UnavailableText;
                HasSelfHash = false;
                return;
            }

            SelfHashPlain = hash;
            SelfHashDisplay = FormatHashGroups(hash);
            HasSelfHash = true;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            Apply();
        else
            dispatcher.Invoke(Apply);
    }

    /// <summary>复制连续（不带空格）的完整哈希。</summary>
    [RelayCommand]
    private void CopySelfHash()
    {
        if (string.IsNullOrEmpty(SelfHashPlain)) return;
        try
        {
            System.Windows.Clipboard.SetText(SelfHashPlain);
            HintService.Hint(Localized("Setup.About.Build.Copy.Success", "SHA-256 已复制到剪贴板"));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "复制自身 SHA-256 失败", ModBase.LogLevel.Developer);
        }
    }

    private static string UnavailableText => Localized("Setup.About.Build.Hash.Unavailable", "不可用");

    /// <summary>流式计算自身可执行文件的 SHA-256，返回小写十六进制字符串。必须在后台线程调用。</summary>
    private static string ComputeSelfSha256(string exePath)
    {
        using var stream = new FileStream(exePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
    }

    /// <summary>把哈希按每 8 个字符一组分隔，便于人工比对。</summary>
    private static string FormatHashGroups(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return hex;
        var builder = new StringBuilder(hex.Length + hex.Length / 8);
        for (var i = 0; i < hex.Length; i++)
        {
            if (i > 0 && i % 8 == 0) builder.Append(' ');
            builder.Append(hex[i]);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     读取本地化文本；资源缺失时退回内置文案
    ///     （Debug 构建对缺失资源返回 !key!，发布构建返回键名本身）。
    /// </summary>
    private static string Localized(string key, string fallback)
    {
        var text = Lang.Text(key);
        if (string.IsNullOrWhiteSpace(text) || text == key) return fallback;
        if (text.Length >= 2 && text[0] == '!' && text[^1] == '!') return fallback;
        return text;
    }

    private static string FormatVersionInfo()
    {
        return Lang.Text("Setup.About.Version.Info").Replace("%VERSION%", ModBase.versionBaseName)
            .Replace("%VERSIONCODE%", ModBase.versionCode.ToString())
            .Replace("%BRANCH%", ModBase.versionBranchName)
            .Replace("%COMMIT_HASH%", ModBase.commitHashShort);
    }

    private static async Task<List<PageSetupAbout.GitHubContributor>> FetchContributorsAsync()
    {
        using var response = await HttpRequest
            .Create("https://api.github.com/repos/Csy88817/PCL-NE/contributors").SendAsync();
        response.EnsureSuccessStatusCode();
        return await response.AsJsonAsync<List<PageSetupAbout.GitHubContributor>>(JsonCompat.SerializerOptions) ?? [];
    }
}
