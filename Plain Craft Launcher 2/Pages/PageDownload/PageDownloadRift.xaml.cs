using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageDownloadRift
{
    public PageDownloadRift()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
        BtnWeb.Click += BtnWeb_Click;
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanMain, CardTip, ModDownload.dlRiftListLoader, _ => Load_OnFinish());
    }

    private void Init()
    {
        PanBack.ScrollToHome();
    }

    private void Load_OnFinish()
    {
        // 结果数据化
        try
        {
            // 归类：Rift 只能运行在 1.13.x 上，直接按它所依赖的 Minecraft 版本分组
            var dict = new Dictionary<string, List<ModDownload.DlRiftListEntry>>();
            foreach (var Version in ModDownload.dlRiftListLoader.output.Value)
            {
                var mainVersion = string.IsNullOrWhiteSpace(Version.Inherit)
                    ? McVersionComparer.UNKNOWN_VERSION_KEY
                    : Version.Inherit;
                if (!dict.ContainsKey(mainVersion))
                    dict.Add(mainVersion, new List<ModDownload.DlRiftListEntry>());
                dict[mainVersion].Add(Version);
            }

            // 按 Minecraft 版本从新到旧排列，未知版本放最后
            var orderedKeys = dict.Keys
                .Where(k => k != McVersionComparer.UNKNOWN_VERSION_KEY)
                .OrderByDescending(k => k,
                    Comparer<string>.Create((a, b) => McVersionComparer.CompareVersion(a, b)))
                .ToList();
            if (dict.ContainsKey(McVersionComparer.UNKNOWN_VERSION_KEY))
                orderedKeys.Add(McVersionComparer.UNKNOWN_VERSION_KEY);

            // 清空当前
            PanMain.Children.Clear();
            // 转化为 UI
            foreach (var key in orderedKeys)
            {
                var value = dict[key];
                if (!value.Any())
                    continue;
                // 增加卡片
                var newCard = new MyCard
                {
                    Title = (key == McVersionComparer.UNKNOWN_VERSION_KEY
                        ? Lang.Text("Minecraft.Version.Unknown")
                        : key) + " (" + value.Count + ")",
                    Margin = new Thickness(0d, 0d, 0d, 15d)
                };
                var newStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = value
                };
                newCard.Children.Add(newStack);
                newCard.SwapControl = newStack;
                newCard.IsSwapped = true;
                newCard.InstallMethod = stack =>
                {
                    stack.Tag = ((List<ModDownload.DlRiftListEntry>)stack.Tag).Sort((a, b) =>
                        McVersionComparer.CompareVersion(b.Version, a.Version) == 1);
                    foreach (var item in (IEnumerable)stack.Tag)
                        stack.Children.Add(ModDownloadLib.RiftDownloadListItem(
                            (ModDownload.DlRiftListEntry)item, DownloadStart, false));
                };
                PanMain.Children.Add(newCard);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "可视化 Rift 版本列表出错",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Error.OperationFailed"));
        }
    }

    private void DownloadStart(object sender, RoutedEventArgs e)
    {
        ModDownload.DlRiftListEntry version;
        if (((dynamic)sender).Tag is ModDownload.DlRiftListEntry)
            version = (ModDownload.DlRiftListEntry)((dynamic)sender).Tag;
        else
            version = (ModDownload.DlRiftListEntry)((dynamic)sender).Tag.Tag;
        ModDownloadLib.McDownloadRift(version);
    }

    private void BtnWeb_Click(object sender, EventArgs e)
    {
        ModBase.OpenWebsite("https://github.com/DimensionalDevelopment/Rift");
    }
}