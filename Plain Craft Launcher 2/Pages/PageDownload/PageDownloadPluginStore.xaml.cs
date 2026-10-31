using System.Collections;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using PCL.Core.App.Localization;

namespace PCL;

/// <summary>
///     「下载 → 插件商店」页：从 PCL NE 官方插件商店（本站 <c>/api/plugins</c>）浏览并一键安装插件。
///     <para>
///         商店索引由站点提供，插件包本体也在站点上；安装后立刻生效。代码插件会先确认风险。
///     </para>
/// </summary>
public partial class PageDownloadPluginStore
{
    public PageDownloadPluginStore()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
        BtnWeb.Click += (_, _) => ModBase.OpenWebsite(ModPlugin.StoreApi.Replace("/api/plugins", "/"));
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanMain, CardTip, ModPlugin.StoreLoader, _ => Load_OnFinish());
    }

    private void Init()
    {
        PanBack.ScrollToHome();
    }

    private void Load_OnFinish()
    {
        try
        {
            RefreshList();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化插件商店失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Plugin.Store.InstallFailed"));
        }
    }

    /// <summary>
    ///     重建商店列表（安装完成后也会被调用）。
    /// </summary>
    public void RefreshList()
    {
        var plugins = ModPlugin.StoreLoader.output ?? new List<ModPlugin.StoreEntry>();
        PanMain.Children.Clear();

        var card = new MyCard
        {
            Title = Lang.Text("Download.Plugin.Store.ListTitle", plugins.Count),
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
        var stack = new StackPanel
        {
            Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransform = new TranslateTransform(),
            Tag = plugins
        };
        card.Children.Add(stack);
        card.SwapControl = stack;
        card.IsSwapped = true;
        card.InstallMethod = target =>
        {
            if (!plugins.Any())
            {
                target.Children.Add(new TextBlock
                {
                    Text = Lang.Text("Download.Plugin.Store.Empty"),
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.75
                });
                return;
            }

            foreach (var item in (IEnumerable)target.Tag)
                AddStoreItem(target, (ModPlugin.StoreEntry)item);
        };
        PanMain.Children.Add(card);
    }

    private static void AddStoreItem(Panel parent, ModPlugin.StoreEntry entry)
    {
        var infoParts = new List<string>();
        if (entry.IsCodePlugin)
            infoParts.Add(Lang.Text("Download.Plugin.CodeBadge"));
        if (!string.IsNullOrWhiteSpace(entry.Author))
            infoParts.Add(entry.Author);
        if (!string.IsNullOrWhiteSpace(entry.Description))
            infoParts.Add(entry.Description);
        if (!string.IsNullOrWhiteSpace(entry.Tags))
            infoParts.Add(entry.Tags);
        if (entry.Size > 0L)
            infoParts.Add($"{entry.Size / 1024d:N1} KB");
        if (ModPlugin.IsInstalled(entry.Id))
            infoParts.Add(Lang.Text("Download.Plugin.Store.InstalledBadge"));

        var listItem = new MyListItem
        {
            Title = entry.DisplayName,
            Info = string.Join("  |  ", infoParts),
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            SnapsToDevicePixels = true,
            Tag = entry,
            Logo = ModBase.pathImage + "Blocks/CommandBlock.png"
        };
        listItem.Click += (sender, _) => Install((ModPlugin.StoreEntry)((dynamic)sender).Tag);
        listItem.ContentHandler = (sender, _) => BuildItemMenu((MyListItem)sender);
        parent.Children.Add(listItem);
    }

    private static void BuildItemMenu(MyListItem sender)
    {
        var entry = (ModPlugin.StoreEntry)sender.Tag;
        var buttons = new List<MyIconButton>
        {
            MakeButton("lucide/download", Lang.Text("Download.Plugin.Store.Install"), entry, Install)
        };
        if (!string.IsNullOrWhiteSpace(entry.Homepage))
            buttons.Add(MakeButton("lucide/external-link", Lang.Text("Download.Plugin.OpenHomepage"), entry,
                e => ModBase.OpenWebsite(e.Homepage)));
        sender.Buttons = buttons.ToArray();
    }

    private static MyIconButton MakeButton(string svg, string tooltip, ModPlugin.StoreEntry entry,
        Action<ModPlugin.StoreEntry> action)
    {
        var button = new MyIconButton { SvgIcon = svg, ToolTip = tooltip, Tag = entry };
        ToolTipService.SetPlacement(button, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(button, 30d);
        ToolTipService.SetHorizontalOffset(button, 2d);
        button.Click += (_, _) => action(entry);
        return button;
    }

    /// <summary>
    ///     安装一条商店插件：代码插件先确认，然后在后台线程下载 + 校验 + 安装。
    /// </summary>
    private static void Install(ModPlugin.StoreEntry entry)
    {
        try
        {
            if (entry.IsCodePlugin &&
                ModMain.MyMsgBox(
                    Lang.Text("Download.Plugin.CodeWarning.Message", entry.DisplayName),
                    Lang.Text("Download.Plugin.CodeWarning.Title"),
                    Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel")) != 1)
                return;

            HintService.Hint(Lang.Text("Download.Plugin.Store.Installing", entry.DisplayName));
            Task.Run(() =>
            {
                try
                {
                    var installed = ModPlugin.InstallFromStore(entry);
                    ModBase.RunInUi(() =>
                    {
                        HintService.Hint(Lang.Text("Download.Plugin.Store.Installed", installed.DisplayName),
                            HintType.Success);
                        ModMain.frmDownloadPluginStore?.RefreshList();
                        ModMain.frmDownloadPlugin?.RefreshList();
                    });
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "从商店安装插件失败：" + entry.Id, ModBase.LogLevel.Feedback,
                        userSummary: Lang.Text("Download.Plugin.Store.InstallFailed"));
                }
            });
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "从商店安装插件失败：" + entry.Id, ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Plugin.Store.InstallFailed"));
        }
    }
}