using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PCL.Core.App;
using PCL.Core.App.Tools;
using PCL.Core.IO;
using PCL.Core.IO.Net;
using PCL.Core.UI;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using System.Globalization;

namespace PCL;

public partial class PageToolsTest : IPageView
{
    private readonly PageToolsTestViewModel _vm = new();
    private bool _refreshed;
    private Bitmap currentSkinBitmap;
    private Bitmap generatedHeadBitmap;

    private int headSize = 64;
    private string skinPath = "";

    public PageToolsTest()
    {
        InitializeComponent();
        DataContext = _vm;
        BtnSelectSkin.Click += BtnSelectSkin_Click;
        CmbHeadSize.SelectionChanged += CmbHeadSize_SelectionChanged;
        Loaded += (_, _) => RefreshOnNavigate();
        Unloaded += (_, _) => _refreshed = false;
    }

    public PageToolsTestViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
        RefreshOpenFolderButtons();
    }

    public void OnNavigatedFrom()
    {
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        MeLoaded();
    }

    private void MeLoaded()
    {
        _vm.DownloadStartEnabled = false;

        TextDownloadFolder.Text = States.Tool.DownloadFolder;
        TextDownloadFolder.Validate();

        if (!string.IsNullOrEmpty(TextDownloadFolder.ValidateResult) || string.IsNullOrEmpty(TextDownloadFolder.Text))
            TextDownloadFolder.Text = ModBase.exePath + @"PCL\MyDownload\";

        TextDownloadFolder.Validate();
        TextDownloadName.Validate();
        TextDownloadUrl.Validate();
        StartButtonRefresh();
    }

    private void StartButtonRefresh()
    {
        _vm.RefreshDownloadButtons(
            string.IsNullOrEmpty(TextDownloadFolder.ValidateResult),
            string.IsNullOrEmpty(TextDownloadUrl.ValidateResult),
            string.IsNullOrEmpty(TextDownloadName.ValidateResult));

        _vm.RefreshAchievementButtons(
            string.IsNullOrEmpty(AchievementBlockTextBox.ValidateResult),
            string.IsNullOrEmpty(AchievementTitleTextBox.ValidateResult),
            string.IsNullOrEmpty(AchievementString1TextBox.ValidateResult));
    }

    private void SaveCacheDownloadFolder(object sender, RoutedEventArgs e)
    {
        States.Tool.DownloadFolder = TextDownloadFolder.Text;
        TextDownloadName.Validate();
    }

    private static void DownloadState(ModLoader.LoaderCombo<int> loader)
    {
        try
        {
            switch (loader.State)
            {
                case ModBase.LoadState.Finished:
                {
                    HintService.Hint(Lang.Text("Tools.Test.CustomDownload.Finished", loader.name), HintType.Success);
                    Console.Beep();
                    break;
                }
                case ModBase.LoadState.Failed:
                {
                    ModBase.Log(
                        loader.Error,
                        $"{loader.name}失败",
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
                    Console.Beep();
                    break;
                }
                case ModBase.LoadState.Aborted:
                {
                    HintService.Hint(Lang.Text("Tools.Test.CustomDownload.Aborted", loader.name));
                    break;
                }
            }
        }
        catch (Exception ex)
        {
        }
    }

    public static void StartCustomDownload(string url, string fileName, string folder = null, string userAgent = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.CustomDownload.SelectLocation"), fileName);
                if (!folder.Contains(@"\")) return;
                if (folder.EndsWith(fileName)) folder = folder[..^fileName.Length];
            }

            folder = folder.Replace("/", @"\").TrimEnd(new[] { '\\' }) + @"\";
            try
            {
                Directory.CreateDirectory(folder);
                ModBase.CheckPermissionWithException(folder);
            }
            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    $"访问文件夹失败（{folder}）",
                    ModBase.LogLevel.Hint,
                    userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
                return;
            }

            ModBase.Log("[Download] 自定义下载文件名：" + fileName);
            ModBase.Log("[Download] 自定义下载文件目标：" + folder);
            var uuid = ModBase.GetUuid();
            ModLoader.LoaderBase loaderdownload;
            if (new HttpValidator().Validate(url).IsValid)
                loaderdownload = new LoaderDownload(Lang.Text("Tools.Test.CustomDownload.LoaderName", fileName),
                    new List<DownloadFile> { new(new[] { url }, folder + fileName, null, true, userAgent) });
            else // UNC 路径
                loaderdownload = new LoaderDownloadUnc(Lang.Text("Tools.Test.CustomDownload.LoaderName", fileName),
                    new Tuple<string, string>(url, folder + fileName));
            var loaderCombo = new ModLoader.LoaderCombo<int>(Lang.Text("Tools.Test.CustomDownload.LoaderTitle", uuid), new[] { loaderdownload })
                { OnStateChanged = a => DownloadState((ModLoader.LoaderCombo<int>)a) };
            loaderCombo.Start();
            ModLoader.LoaderTaskbarAdd(loaderCombo);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "开始自定义下载失败",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
        }
    }

    public static async void Jrrp()
    {
        try
        {
            var random = new Random(GenerateDailySeed());
            var luckValue = random.Next(0, 101);
            var rating = GetRating(luckValue);
            var title = Lang.Text("Tools.Test.Luck.MsgboxTitle", Lang.Date(DateTime.Now, "d"));

            if (luckValue >= 60)
                await ModMain.MyMsgBoxAsync(Lang.Text("Tools.Test.Luck.MessageGood", luckValue, rating), title);
            else
                await ModMain.MyMsgBoxAsync(Lang.Text("Tools.Test.Luck.MessageBad", luckValue, rating), title, isWarn: luckValue <= 30);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "显示今日人品弹窗失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
        }
    }

    public static void RubbishClear()
    {
        ModBase.RunInUi(() =>
        {
            if (ModMain.frmToolsTest is not null && ModMain.frmToolsTest.BtnClear is not null)
                ModMain.frmToolsTest.BtnClear.IsEnabled = false;
        });
        // 只有当没有运行中的Minecraft游戏且启动器不在加载状态时才能清理

        // 清理的文件数量
        // 所有 Minecraft 文件夹


        // 寻找所有 Minecraft 文件夹

        // 删除 Minecraft 的缓存
        // 删除日志和崩溃报告并计数

        // 删除 Natives 文件

        // 删除 PCL 的缓存

        ModBase.RunInNewThread(() =>
        {
            try
            {
                if (!ModWatcher.hasRunningMinecraft && ModLaunch.mcLaunchLoader.State != ModBase.LoadState.Loading)
                {
                    if (ModNet.HasDownloadingTask())
                    {
                        HintService.Hint(Lang.Text("Tools.Test.Clean.WaitForDownload"));
                        return;
                    }

                    if (!ModFolder.mcFolderList.Any()) ModFolder.mcFolderListLoader.Start();
                    if (States.Hint.CleanJunkFile <= 2)
                    {
                        if (ModMain.MyMsgBox(
                                Lang.Text("Tools.Test.Clean.ConfirmMessage"),
                                Lang.Text("Tools.Test.Clean.ConfirmTitle"),
                                Lang.Text("Common.Action.Confirm"),
                                Lang.Text("Common.Action.Cancel")
                            ) == 2) return;
                        States.Hint.CleanJunkFile += 1;
                    }

                    var num = 0;
                    var cleanMcFolderList = new List<DirectoryInfo>();
                    if (!ModFolder.mcFolderList.Any()) ModFolder.mcFolderListLoader.WaitForExit();
                    foreach (var mcFolder in ModFolder.mcFolderList)
                    {
                        cleanMcFolderList.Add(new DirectoryInfo(mcFolder.Location));
                        var dirInfo = new DirectoryInfo(mcFolder.Location + "versions");
                        if (dirInfo.Exists)
                            foreach (var item in dirInfo.EnumerateDirectories())
                                cleanMcFolderList.Add(item);
                    }

                    foreach (var dirInfo in cleanMcFolderList)
                    {
                        num += ModBase.DeleteDirectory(
                            dirInfo.FullName + (dirInfo.FullName.EndsWith(@"\") ? "" : @"\") + @"crash-reports\", true);
                        num += ModBase.DeleteDirectory(
                            dirInfo.FullName + (dirInfo.FullName.EndsWith(@"\") ? "" : @"\") + @"logs\", true);
                        foreach (var fileInfo in dirInfo.EnumerateFiles("*"))
                            if (fileInfo.Name.StartsWith("hs_err_pid") || fileInfo.Name.EndsWith(".log") ||
                                fileInfo.Name == "WailaErrorOutput.txt")
                            {
                                fileInfo.Delete();
                                num += 1;
                            }

                        foreach (var dirInfo2 in dirInfo.EnumerateDirectories())
                            if ((dirInfo2.Name ?? "") == (dirInfo2.Name + "-natives" ?? "") ||
                                dirInfo2.Name == "natives-windows-x86_64")
                                num += ModBase.DeleteDirectory(dirInfo2.FullName, true);
                    }

                    num += ModBase.DeleteDirectory(ModBase.pathTemp, true);
                    num += ModBase.DeleteDirectory(Path.Combine(SystemPaths.DriveLetter, "ProgramData", "PCL"), true);
                    if (num != 0)
                    {
                        ModMain.MyMsgBox(Lang.Text("Tools.Test.Clean.ClearedMessage", num),
                            Lang.Text("Tools.Test.Clean.Cleared"), Lang.Text("Common.Action.Confirm"), "", "", false, true, true);
                        Process.Start(new ProcessStartInfo(Basics.ExecutablePath));
                        FormMain.EndProgramForce();
                    }
                    else
                    {
                        HintService.Hint(Lang.Text("Tools.Test.Clean.NoFiles"));
                    }
                }
                else
                {
                    HintService.Hint(Lang.Text("Tools.Test.Clean.CloseGameFirst"));
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    "清理垃圾失败",
                    ModBase.LogLevel.Hint,
                    userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
            }
            finally
            {
                ModBase.RunInUiWait(() =>
                {
                    if (ModMain.frmToolsTest is not null && ModMain.frmToolsTest.BtnClear is not null)
                        ModMain.frmToolsTest.BtnClear.IsEnabled = true;
                });
            }
        }, "Rubbish Clear");
    }

    public static string GetRandomCave()
    {
        return Lang.Text("Tools.Test.CeNotice");
    }

    public static string GetRandomHint()
    {
        return Lang.Text("Tools.Test.CeNotice");
    }

    public static string GetRandomPresetHint()
    {
        return Lang.Text("Tools.Test.CeNotice");
    }

    private void TextDownloadUrl_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(TextDownloadName.Text) || string.IsNullOrEmpty(TextDownloadUrl.Text)) return;
            TextDownloadName.Text = ModBase.GetFileNameFromPath(WebUtility.UrlDecode(TextDownloadUrl.Text));
            StartButtonRefresh();
        }
        catch
        {
        }
    }

    private void MyTextButton_Click(object sender, EventArgs e)
    {
        var text = SystemDialogs.SelectFolder();
        if (!string.IsNullOrEmpty(text)) TextDownloadFolder.Text = text;
    }

    private void BtnDownloadOpen_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var text = TextDownloadFolder.Text;
            Directory.CreateDirectory(text);
            Basics.OpenPath(text);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "打开下载文件夹失败");
        }
    }

    #region 卡片：一键打开目录

    /// <summary>刷新「一键打开目录」中依赖当前实例的按钮状态。</summary>
    private void RefreshOpenFolderButtons()
    {
        _vm.RefreshOpenFolderButtons(ModInstanceList.McMcInstanceSelected is not null);
    }

    // 打开当前实例的 Mods 文件夹
    private void BtnOpenModsFolder_Click(object sender, MouseButtonEventArgs e)
    {
        OpenInstanceFolder(@"mods\");
    }

    // 打开当前实例的实例文件夹
    private void BtnOpenInstanceFolder_Click(object sender, MouseButtonEventArgs e)
    {
        OpenInstanceFolder();
    }

    // 打开启动器日志文件夹
    private void BtnOpenLogFolder_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            OpenFolder(LogService.Logger.Configuration.StoreFolder);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取启动器日志文件夹失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.OpenFolder.Failed"));
        }
    }

    // 打开 .minecraft 根目录
    private void BtnOpenMinecraftFolder_Click(object sender, MouseButtonEventArgs e)
    {
        OpenFolder(ModFolder.mcFolderSelected);
    }

    /// <summary>打开当前实例的目录；未选择实例时给出提示。</summary>
    /// <param name="subFolder">实例内的子文件夹（相对于版本隔离后的 Minecraft 根目录），留空则打开实例文件夹本身。</param>
    private void OpenInstanceFolder(string subFolder = "")
    {
        var instance = ModInstanceList.McMcInstanceSelected;
        if (instance is null)
        {
            HintService.Hint(Lang.Text("Tools.Test.OpenFolder.NoInstance"), HintType.Error);
            return;
        }

        try
        {
            OpenFolder(string.IsNullOrEmpty(subFolder) ? instance.PathInstance : instance.PathIndie + subFolder);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"获取实例文件夹失败（{instance.PathInstance}）", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.OpenFolder.Failed"));
        }
    }

    /// <summary>打开一个文件夹，不存在时先创建；路径为空时给出提示。</summary>
    private static void OpenFolder(string folderPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                HintService.Hint(Lang.Text("Tools.Test.OpenFolder.NoPath"), HintType.Error);
                return;
            }

            folderPath = folderPath.Replace("/", @"\").TrimEnd('\\') + @"\";
            Directory.CreateDirectory(folderPath);
            ModBase.OpenExplorer(folderPath);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"打开文件夹失败（{folderPath}）", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.OpenFolder.Failed"));
        }
    }

    #endregion

    private void BtnDownloadStart_Click(object sender, MouseButtonEventArgs e)
    {
        StartCustomDownload(TextDownloadUrl.Text, TextDownloadName.Text, TextDownloadFolder.Text);
        TextDownloadUrl.Text = "";
        TextDownloadUrl.Validate();
        TextDownloadUrl.ForceShowAsSuccess();
        TextDownloadName.Text = "";
        TextDownloadName.Validate();
        TextDownloadName.ForceShowAsSuccess();
        StartButtonRefresh();
    }

    private void TextDownloadUrl_ValidateChanged(object sender, RoutedEventArgs e)
    {
        StartButtonRefresh();
    }

    private void TextDownloadFolder_ValidateChanged(object sender, EventArgs e)
    {
        StartButtonRefresh();
    }

    private void TextDownloadName_ValidateChanged(object sender, EventArgs e)
    {
        StartButtonRefresh();
    }

    private void BtnClear_Click(object sender, MouseButtonEventArgs e)
    {
        RubbishClear();
    }

    // 下载正版玩家皮肤
    private void BtnSkinSave_Click(object sender, MouseButtonEventArgs e)
    {
        var id = TextSkinID.Text;
        HintService.Hint(Lang.Text("Tools.Test.Skin.Fetching"));
        ModBase.RunInNewThread(() =>
        {
            try
            {
                if (id.Length < 3)
                {
                    HintService.Hint(Lang.Text("Tools.Test.Skin.InvalidId"));
                }
                else
                {
                    var result = (string)ProfileUi.McLoginMojangUuid(id, true);
                    result = ModSkin.McSkinGetAddress(result, "Mojang");
                    result = ModSkin.McSkinDownload(result);
                    ModBase.RunInUi(() =>
                    {
                        var path = SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.Skin.Save"), $"{id}.png", Lang.Text("Tools.Test.Skin.FileFilter"));
                        ModBase.CopyFile(result, path);
                        HintService.Hint(Lang.Text("Tools.Test.Skin.Saved", id), HintType.Success);
                    });
                }
            }
            catch (Exception ex)
            {
                if (ex.ToString().Contains("429"))
                {
                    HintService.Hint(Lang.Text("Tools.Test.Skin.TooFrequent"), HintType.Error);
                    ModBase.Log($"获取正版皮肤失败（{id}）：获取皮肤太过频繁，请 5 分钟后再试！");
                }
                else
                {
                    ModBase.Log(ex, $"获取正版皮肤失败（{id}）");
                }
            }
        });
    }

    // 今日人品
    private void BtnLuck_Click(object sender, MouseButtonEventArgs e)
    {
        Jrrp();
    }

    public static int GenerateDailySeed()
    {
        var datePart = DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        return DJB2Hash(datePart + Identify.LauncherId);
    }

    private static int DJB2Hash(string str)
    {
        var hash = 5381L;
        var prime = 33L;
        foreach (var c in str)
        {
            long charValue = c;
            hash = (hash * prime + charValue) % 0x100000000L;
        }

        return (int)(hash & 0x7FFFFFFFL);
    }

    public static string GetRating(int luckValue)
    {
        var key = luckValue switch
        {
            100 => "Tools.Test.Luck.Rating100",
            >= 95 => "Tools.Test.Luck.Rating95",
            >= 90 => "Tools.Test.Luck.Rating90",
            >= 60 => "Tools.Test.Luck.Rating60",
            >= 40 => "Tools.Test.Luck.Rating40",
            >= 30 => "Tools.Test.Luck.Rating30",
            >= 10 => "Tools.Test.Luck.Rating10",
            _ => "Tools.Test.Luck.Rating0"
        };

        return Lang.Text(key);
    }

    private async void BtnCreateShortcut_Click(object sender, MouseButtonEventArgs e)
    {
        var ctl = sender as Control;
        if (ctl is not null) ctl.IsEnabled = false;
        try
        {
            var shortcutName = Lang.Text("Tools.Test.Shortcut.FileName", ".lnk");
            var desktopName = Lang.Text("Tools.Test.Shortcut.Desktop");
            var startName = Lang.Text("Tools.Test.Shortcut.StartMenu");
            var desktop = Paths.GetSpecialPath(Environment.SpecialFolder.Desktop, shortcutName);
            var start = Paths.GetSpecialPath(Environment.SpecialFolder.StartMenu, @"Programs\" + shortcutName);
            var choice =
                await ModMain.MyMsgBoxAsync(
                    Lang.Text("Tools.Test.Shortcut.ConfirmMessage", desktopName, desktop, startName, start),
                    Lang.Text("Tools.Test.Shortcut.SelectLocation"), Lang.Text("Common.Action.Cancel"), desktopName, startName);
            if (choice == 1)
                return;
            var shortcutPath = choice == 2 ? desktop : start;
            var locationName = choice == 2 ? desktopName : startName;
            Files.CreateShortcut(shortcutPath, Basics.ExecutablePath);
            HintService.Hint(Lang.Text("Tools.Test.Shortcut.Created", locationName), HintType.Success);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "创建快捷方式失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
        }
        finally
        {
            if (ctl is not null) ctl.IsEnabled = true;
        }
    }

    // 启动计数显示
    private async void BtnLaunchCount_Click(object sender, MouseButtonEventArgs e)
    {
        var ctl = sender as Control;
        if (ctl is not null) ctl.IsEnabled = false;
        try
        {
            await ModMain.MyMsgBoxAsync(Lang.Text("Tools.Test.LaunchCount.Message", States.System.LaunchCount), Lang.Text("Tools.Test.LaunchCount.Title"));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "显示启动次数弹窗失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
        }
        finally
        {
            if (ctl is not null) ctl.IsEnabled = true;
        }
    }

    private async void BtnAchievementPreview_Click(object sender, MouseButtonEventArgs e)
    {
        var url = GetAchievementUrl();
        ModBase.Log("[Net] 获取网络结果" + url);
        await LoadImageAsync(url);
    }

    private async Task LoadImageAsync(string imageUrl)
    {
        var client = NetworkService.GetClient();
        try
        {
            var response = await client.GetAsync(imageUrl);
            if (response.IsSuccessStatusCode)
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = stream;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    Dispatcher.Invoke(() =>
                    {
                        AchievementImage.Source = bitmapImage;
                        _vm.AchievementImageVisible = true;
                    });
                }
            else if (response.StatusCode == HttpStatusCode.NotFound)
                Dispatcher.Invoke(() =>
                {
                    ModBase.Log("获取成就图片失败（404）");
                    HintService.Hint(Lang.Text("Tools.Test.Achievement.FetchFailed"), HintType.Error);
                });

            else
                Dispatcher.Invoke(() => ModBase.Log("获取成就图片失败（" + (int)response.StatusCode + "）"));
        }

        catch (Exception ex)
        {
            Dispatcher.Invoke(() => ModBase.Log(ex, "获取成就图片失败"));
        }
    }

    private async void BtnAchievementSave_Click(object sender, MouseButtonEventArgs e)
    {
        var url = GetAchievementUrl();
        await DownloadImageToLocalAsync(url);
    }

    private async Task DownloadImageToLocalAsync(string imageUrl)
    {
        var savePath = ModBase.pathTemp + @"Download\" + ModBase.GetHash(imageUrl) + ".png";
        var client = NetworkService.GetClient();
        try
        {
            // 异步发送 GET 请求
            var response = await client.GetAsync(imageUrl);

            // 如果响应状态码是成功的，则继续
            if (response.IsSuccessStatusCode)
            {
                // 异步读取响应内容为字节流
                var imageBytes = await response.Content.ReadAsByteArrayAsync();

                // 将字节写入本地文件
                File.WriteAllBytes(savePath, imageBytes);

                var path =
                    SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.Achievement.Save"), AchievementTitleTextBox.Text + ".png", Lang.Text("Tools.Test.Achievement.FileFilter"));
                if (string.IsNullOrEmpty(path))
                {
                    ModBase.Log("用户取消了保存操作");
                    File.Delete(savePath);
                    return;
                }

                ModBase.CopyFile(savePath, path);
                File.Delete(savePath);
                HintService.Hint(Lang.Text("Tools.Test.Achievement.Saved"), HintType.Success);
            }
            // 下载成功，返回 True
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // 捕获 404 错误
                ModBase.Log("获取成就图片失败（404）");
                HintService.Hint(Lang.Text("Tools.Test.Achievement.FetchFailed"), HintType.Error);
            }
            else
            {
                // 处理其他非成功状态码
                ModBase.Log("获取成就图片失败（" + (int)response.StatusCode + "）");
            }
        }

        catch (Exception ex)
        {
            // 捕获所有其他异常（如网络连接问题）
            ModBase.Log(ex, "获取成就图片失败");
        }
    }

    private string GetAchievementUrl()
    {
        var block = AchievementBlockTextBox.Text.Trim();
        var title = AchievementTitleTextBox.Text.Replace(" ", "..");
        var str1 = AchievementString1TextBox.Text.Replace(" ", "..");
        var str2 = AchievementString2TextBox.Text.Replace(" ", "..");
        var url = $"https://minecraft-api.com/api/achivements/{block}/{title}/{str1}";
        if (!string.IsNullOrEmpty(str2)) url += $"/{str2}";
        return url;
    }

    private void BtnCrash_Click(object sender, MouseButtonEventArgs e)
    {
        throw new Exception(Lang.Text("Tools.Test.Crash.ManualCrash"));
    }

    private int GetHeadSize() => CmbHeadSize.SelectedIndex switch
    {
        0 => 64,
        1 => 96,
        2 => 128,
        _ => 64
    };

    private void BtnSelectSkin_Click(object sender, RoutedEventArgs e)
    {
        var filePath = SystemDialogs.SelectFile(Lang.Text("Tools.Test.Avatar.FileFilter"),
            Lang.Text("Tools.Test.Avatar.SelectSkinFile"));
        if (!string.IsNullOrEmpty(filePath)) LoadAndGenerateHead(filePath);
    }

    private void LoadAndGenerateHead(string skinPath)
    {
        try
        {
            using (var stream = new FileStream(skinPath, FileMode.Open, FileAccess.Read))
            {
                currentSkinBitmap = new Bitmap(stream);
            }

            this.skinPath = skinPath;

            if (currentSkinBitmap.Width != currentSkinBitmap.Height)
            {
                HintService.Hint(Lang.Text("Tools.Test.Avatar.InvalidSize"), HintType.Error);
                _vm.SkinPreviewVisible = false;
                return;
            }

            generatedHeadBitmap = GenerateHeadFromSkin(currentSkinBitmap);

            ImgFace.Source = BitmapToBitmapImage(generatedHeadBitmap);
            ImgHair.Source = null;

            _vm.SkinPreviewVisible = true;
            HintService.Hint(Lang.Text("Tools.Test.Avatar.Generated"), HintType.Success);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "生成头像失败");
            HintService.Hint(Lang.Text("Tools.Test.Avatar.GenerateFailed", ex.Message), HintType.Error);
            _vm.SkinPreviewVisible = false;
        }
    }

    private Bitmap GenerateHeadFromSkin(Bitmap skinBitmap)
    {
        var scale = skinBitmap.Width / 64;
        headSize = GetHeadSize();
        var headBitmap = new Bitmap(headSize, headSize);

        using (var g = Graphics.FromImage(headBitmap))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            DrawFaceLayer(g, skinBitmap, scale);
            if (skinBitmap.Width >= 64) DrawHairLayer(headBitmap, skinBitmap, scale);
        }

        return headBitmap;
    }

    private void DrawFaceLayer(Graphics g, Bitmap skinBitmap, int scale)
    {
        var faceRect = new Rectangle(8 * scale, 8 * scale, 8 * scale, 8 * scale);
        var faceSize = headSize - headSize / 8;
        var faceScaled = new Bitmap(faceSize, faceSize);

        using (var gFace = Graphics.FromImage(faceScaled))
        {
            gFace.InterpolationMode = InterpolationMode.NearestNeighbor;
            gFace.PixelOffsetMode = PixelOffsetMode.Half;
            gFace.DrawImage(skinBitmap, new Rectangle(0, 0, faceSize, faceSize), faceRect, GraphicsUnit.Pixel);
        }

        var offset = headSize / 16;
        g.DrawImage(faceScaled, offset, offset, faceSize, faceSize);
    }

    private void DrawHairLayer(Bitmap headBitmap, Bitmap skinBitmap, int scale)
    {
        var hairRect = new Rectangle(40 * scale, 8 * scale, 8 * scale, 8 * scale);
        var hairScaled = new Bitmap(headSize, headSize);

        using (var gHair = Graphics.FromImage(hairScaled))
        {
            gHair.InterpolationMode = InterpolationMode.NearestNeighbor;
            gHair.PixelOffsetMode = PixelOffsetMode.Half;
            gHair.DrawImage(skinBitmap, new Rectangle(0, 0, headSize, headSize), hairRect, GraphicsUnit.Pixel);
        }

        for (int x = 0, loopTo = headSize - 1; x <= loopTo; x++)
        for (int y = 0, loopTo1 = headSize - 1; y <= loopTo1; y++)
        {
            var pixel = hairScaled.GetPixel(x, y);
            if (pixel.A > 0) headBitmap.SetPixel(x, y, pixel);
        }
    }

    private void BtnSaveHead_Click(object sender, MouseButtonEventArgs e)
    {
        if (generatedHeadBitmap is null)
        {
            HintService.Hint(Lang.Text("Tools.Test.Avatar.SelectFirst"), HintType.Error);
            return;
        }

        var savePath = SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.Avatar.Save"), "Head.png");

        if (string.IsNullOrEmpty(savePath))
            return;

        generatedHeadBitmap.Save(savePath, ImageFormat.Png);
        HintService.Hint(Lang.Text("Tools.Test.Avatar.Saved"), HintType.Success);
    }

    private void CmbHeadSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (currentSkinBitmap is not null && skinPath is not null) LoadAndGenerateHead(skinPath);
    }

    private BitmapImage BitmapToBitmapImage(Bitmap bitmap)
    {
        using (var memoryStream = new MemoryStream())
        {
            bitmap.Save(memoryStream, ImageFormat.Png);
            memoryStream.Position = 0L;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = memoryStream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }
    }

    private void TextDownloadFolder_OnValidatedTextChanged(object sender, RoutedEventArgs e)
    {
        SaveCacheDownloadFolder(sender, e);
        TextDownloadName_ValidateChanged(sender, e);
    }

    #region 存储占用统计

    /// <summary>
    ///     统计 versions 目录下每个实例占用的空间（只读，不修改任何文件）。
    /// </summary>
    private void BtnCalcDiskUsage_Click(object sender, MouseButtonEventArgs e)
    {
        var root = Path.Combine(ModFolder.mcFolderSelected, "versions");
        if (!Directory.Exists(root))
        {
            LabDiskUsage.Text = Lang.Text("Tools.Test.DiskUsage.NoData");
            return;
        }

        LabDiskUsage.Text = Lang.Text("Tools.Test.DiskUsage.Working");
        Task.Run(() =>
        {
            try
            {
                var rows = new List<(string Name, long Bytes, int Files)>();
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var (bytes, files) = FolderSizeGet(dir);
                    rows.Add((Path.GetFileName(dir), bytes, files));
                }

                if (rows.Count == 0)
                {
                    ModBase.RunInUi(() => LabDiskUsage.Text = Lang.Text("Tools.Test.DiskUsage.NoData"));
                    return;
                }

                var total = rows.Sum(r => r.Bytes);
                var sb = new StringBuilder();
                sb.AppendLine(Lang.Text("Tools.Test.DiskUsage.Total", Lang.FileSize(total)));
                // 占用最大的排前面，最多列 10 条，避免列表过长
                foreach (var row in rows.OrderByDescending(r => r.Bytes).Take(10))
                    sb.AppendLine(Lang.Text("Tools.Test.DiskUsage.Row", row.Name,
                        Lang.FileSize(row.Bytes), row.Files));
                if (rows.Count > 10)
                    sb.AppendLine(Lang.Text("Tools.Test.DiskUsage.More", rows.Count - 10));

                var text = sb.ToString().TrimEnd();
                ModBase.RunInUi(() => LabDiskUsage.Text = text);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "统计实例占用失败", ModBase.LogLevel.Feedback,
                    userSummary: Lang.Text("Tools.Test.DiskUsage.Failed"));
            }
        });
    }

    private void BtnOpenVersionsFolder_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            OpenFolder(Path.Combine(ModFolder.mcFolderSelected, "versions"));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "打开 versions 文件夹失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.OpenFolder.Failed"));
        }
    }

    /// <summary>
    ///     递归统计一个目录的字节数与文件数。遇到无权限的子目录就跳过，不中断整体统计。
    /// </summary>
    private static (long Bytes, int Files) FolderSizeGet(string folder)
    {
        long bytes = 0L;
        var files = 0;
        try
        {
            foreach (var file in Directory.GetFiles(folder))
                try
                {
                    bytes += new FileInfo(file).Length;
                    files += 1;
                }
                catch
                {
                    // 单个文件读不到就跳过
                }

            foreach (var dir in Directory.GetDirectories(folder))
                try
                {
                    var (subBytes, subFiles) = FolderSizeGet(dir);
                    bytes += subBytes;
                    files += subFiles;
                }
                catch
                {
                    // 单个子目录失败就跳过
                }
        }
        catch
        {
            // 目录本身读不到就当作 0
        }

        return (bytes, files);
    }

    #endregion

    #region 世界备份

    private static string WorldBackupFolder =>
        Path.Combine(ModBase.pathPure, "Backup", "Worlds");

    /// <summary>
    ///     把当前实例的 saves 目录打包成一个 zip 放到 Backup/Worlds，文件名带时间。
    /// </summary>
    private void BtnBackupWorlds_Click(object sender, MouseButtonEventArgs e)
    {
        var instance = ModInstanceList.McMcInstanceSelected;
        if (instance is null)
        {
            HintService.Hint(Lang.Text("Tools.Test.OpenFolder.NoInstance"), HintType.Error);
            return;
        }

        var savesFolder = Path.Combine(instance.PathIndie, "saves");
        if (!Directory.Exists(savesFolder))
        {
            LabWorldBackup.Text = Lang.Text("Tools.Test.WorldBackup.NoSaves");
            return;
        }

        LabWorldBackup.Text = Lang.Text("Tools.Test.WorldBackup.Working");
        var instanceName = instance.Name;
        Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(WorldBackupFolder);
                var stamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
                var safeName = string.Join("_", instanceName.Split(Path.GetInvalidFileNameChars()));
                var target = Path.Combine(WorldBackupFolder, $"{safeName}-{stamp}.zip");

                ZipFile.CreateFromDirectory(savesFolder, target, CompressionLevel.Optimal, true);
                var size = new FileInfo(target).Length;
                ModBase.RunInUi(() => LabWorldBackup.Text =
                    Lang.Text("Tools.Test.WorldBackup.Done", Path.GetFileName(target), Lang.FileSize(size)));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "备份世界失败", ModBase.LogLevel.Feedback,
                    userSummary: Lang.Text("Tools.Test.WorldBackup.Failed"));
            }
        });
    }

    private void BtnOpenWorldBackupFolder_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            OpenFolder(WorldBackupFolder);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "打开世界备份文件夹失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.OpenFolder.Failed"));
        }
    }

    #endregion

    #region 日志摘要

    /// <summary>
    ///     扫描最近的启动器日志，统计 ERROR / WARN / Exception 等关键字出现次数（只读）。
    /// </summary>
    private void BtnAnalyzeLog_Click(object sender, MouseButtonEventArgs e)
    {
        LabLogSummary.Text = Lang.Text("Tools.Test.LogSummary.Working");
        Task.Run(() =>
        {
            try
            {
                string folder;
                try
                {
                    folder = LogService.Logger.Configuration.StoreFolder;
                }
                catch
                {
                    folder = "";
                }

                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    ModBase.RunInUi(() => LabLogSummary.Text = Lang.Text("Tools.Test.LogSummary.NoLogs"));
                    return;
                }

                var logs = Directory.GetFiles(folder, "*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Take(5)
                    .ToList();
                if (logs.Count == 0)
                {
                    ModBase.RunInUi(() => LabLogSummary.Text = Lang.Text("Tools.Test.LogSummary.NoLogs"));
                    return;
                }

                int error = 0, warn = 0, exception = 0;
                long lines = 0L;
                foreach (var log in logs)
                    using (var stream = log.OpenRead())
                    using (var reader = new StreamReader(stream))
                    {
                        string? line;
                        while ((line = reader.ReadLine()) is not null)
                        {
                            lines += 1;
                            if (line.Contains("Exception", StringComparison.OrdinalIgnoreCase)) exception += 1;
                            if (line.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase) ||
                                line.Contains("[FATAL]", StringComparison.OrdinalIgnoreCase)) error += 1;
                            if (line.Contains("[WARN]", StringComparison.OrdinalIgnoreCase)) warn += 1;
                        }
                    }

                var text = Lang.Text("Tools.Test.LogSummary.Result",
                    logs.Count, logs[0].LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                    lines, error, warn, exception);
                ModBase.RunInUi(() => LabLogSummary.Text = text);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "分析日志失败", ModBase.LogLevel.Feedback,
                    userSummary: Lang.Text("Tools.Test.LogSummary.Failed"));
            }
        });
    }

    #endregion
}
