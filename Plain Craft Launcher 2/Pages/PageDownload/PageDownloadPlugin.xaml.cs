using System.Collections;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

/// <summary>
///     「下载 → 插件」页：导入 <c>.pclne</c> 启动器插件、查看与卸载已安装的插件。
///     <para>
///         插件是声明式的，启动器只展示清单里声明的内容，不会执行插件内的代码。
///     </para>
/// </summary>
public partial class PageDownloadPlugin
{
    public PageDownloadPlugin()
    {
        InitializeComponent();
        Loaded += (_, _) => Init();
        BtnImport.Click += (_, _) => ImportPlugin();
        BtnFolder.Click += (_, _) => ModBase.OpenExplorer(ModPlugin.PluginsFolder);
    }

    private void Init()
    {
        PanBack.ScrollToHome();
        RefreshList();
    }

    /// <summary>
    ///     重新构建已安装插件列表。插件在运行时也可以通过宿主刷新它。
    /// </summary>
    public void RefreshList()
    {
        try
        {
            var plugins = ModPlugin.GetAll();
            PanMain.Children.Clear();

            if (!plugins.Any())
            {
                var empty = new MyCard
                {
                    Title = Lang.Text("Download.Plugin.ListTitle", 0),
                    Margin = new Thickness(0d, 0d, 0d, 15d)
                };
                var emptyStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top,
                    RenderTransform = new TranslateTransform()
                };
                empty.Children.Add(emptyStack);
                empty.SwapControl = emptyStack;
                empty.IsSwapped = true;
                empty.InstallMethod = stack => stack.Children.Add(new TextBlock
                {
                    Text = Lang.Text("Download.Plugin.Empty"),
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.75
                });
                PanMain.Children.Add(empty);
                return;
            }

            var card = new MyCard
            {
                Title = Lang.Text("Download.Plugin.ListTitle", plugins.Count),
                Margin = new Thickness(0d, 0d, 0d, 15d)
            };
            var listStack = new StackPanel
            {
                Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                VerticalAlignment = VerticalAlignment.Top,
                Tag = plugins
            };
            card.Children.Add(listStack);
            card.SwapControl = listStack;
            card.IsSwapped = true;
            card.InstallMethod = stack =>
            {
                foreach (var item in (IEnumerable)stack.Tag) AddPluginItem(stack, (ModPlugin.PluginInfo)item);
            };
            PanMain.Children.Add(card);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化插件列表失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Plugin.OperationFailed"));
        }
    }

    private static void AddPluginItem(Panel parent, ModPlugin.PluginInfo plugin)
    {
        var infoParts = new List<string>();
        if (plugin.IsCodePlugin)
            infoParts.Add(Lang.Text("Download.Plugin.CodeBadge"));
        if (!string.IsNullOrWhiteSpace(plugin.Author))
            infoParts.Add(plugin.Author);
        if (!string.IsNullOrWhiteSpace(plugin.Description))
            infoParts.Add(plugin.Description);

        var listItem = new MyListItem
        {
            Title = plugin.DisplayName,
            Info = string.Join("  |  ", infoParts),
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            SnapsToDevicePixels = true,
            Tag = plugin,
            // 插件自带 icon.png 就用它，否则用内置图标
            Logo = plugin.IconPath ?? ModBase.pathImage + "Blocks/CommandBlock.png"
        };
        // 左键：有主页就打开主页，否则打开插件目录
        listItem.Click += (sender, _) =>
        {
            var info = (ModPlugin.PluginInfo)((dynamic)sender).Tag;
            if (!string.IsNullOrWhiteSpace(info.Homepage))
                ModBase.OpenWebsite(info.Homepage);
            else
                ModBase.OpenExplorer(info.Folder);
        };
        listItem.ContentHandler = (sender, _) => BuildItemMenu((MyListItem)sender);
        parent.Children.Add(listItem);
    }

    private static void BuildItemMenu(MyListItem sender)
    {
        var plugin = (ModPlugin.PluginInfo)sender.Tag;
        var buttons = new List<MyIconButton>();

        if (!string.IsNullOrWhiteSpace(plugin.Homepage))
            buttons.Add(MakeButton("lucide/external-link", Lang.Text("Download.Plugin.OpenHomepage"), plugin,
                info => ModBase.OpenWebsite(info.Homepage)));

        buttons.Add(MakeButton("lucide/folder-open", Lang.Text("Download.Plugin.OpenFolder.Button"), plugin,
            info => ModBase.OpenExplorer(info.Folder)));

        buttons.Add(MakeButton("lucide/trash-2", Lang.Text("Download.Plugin.Uninstall"), plugin, UninstallPlugin));

        sender.Buttons = buttons.ToArray();
    }

    private static MyIconButton MakeButton(string svg, string tooltip, ModPlugin.PluginInfo plugin,
        Action<ModPlugin.PluginInfo> action)
    {
        var button = new MyIconButton
        {
            SvgIcon = svg, ToolTip = tooltip, Tag = plugin
        };
        ToolTipService.SetPlacement(button, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(button, 30d);
        ToolTipService.SetHorizontalOffset(button, 2d);
        button.Click += (_, _) => action(plugin);
        return button;
    }

    private static void UninstallPlugin(ModPlugin.PluginInfo plugin)
    {
        try
        {
            if (ModMain.MyMsgBox(
                    Lang.Text("Download.Plugin.Uninstall.Confirm.Message", plugin.DisplayName),
                    Lang.Text("Download.Plugin.Uninstall.Confirm.Title"),
                    Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel")) != 1)
                return;
            ModPlugin.Uninstall(plugin);
            HintService.Hint(Lang.Text("Download.Plugin.Uninstalled", plugin.DisplayName), HintType.Success);
            ModMain.frmDownloadPlugin?.RefreshList();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "卸载插件失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Plugin.OperationFailed"));
        }
    }

    /// <summary>
    ///     选一个 .pclne 并安装。
    /// </summary>
    private void ImportPlugin()
    {
        try
        {
            var path = SystemDialogs.SelectFile(Lang.Text("Download.Plugin.Import.FileFilter"),
                Lang.Text("Download.Plugin.Import.SelectTitle"));
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            // 先只看清单，确定 id 与是否需要覆盖
            ModPlugin.PluginInfo preview;
            try
            {
                preview = ModPlugin.Peek(path);
            }
            catch (InvalidDataException ex)
            {
                HintService.Hint(ex.Message, HintType.Error);
                return;
            }

            // 会执行代码的插件必须先把风险说清楚
            if (preview.IsCodePlugin &&
                ModMain.MyMsgBox(
                    Lang.Text("Download.Plugin.CodeWarning.Message", preview.DisplayName),
                    Lang.Text("Download.Plugin.CodeWarning.Title"),
                    Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel")) != 1)
                return;

            var overwrite = false;
            if (ModPlugin.IsInstalled(preview.Id))
            {
                if (ModMain.MyMsgBox(
                        Lang.Text("Download.Plugin.Import.AlreadyExists", preview.DisplayName),
                        Lang.Text("Download.Plugin.Import.AlreadyExists.Title"),
                        Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel")) != 1)
                    return;
                overwrite = true;
            }

            var installed = ModPlugin.Install(path, overwrite);
            // 代码插件立刻加载，这样导入后就能生效（无需重启）
            if (installed.IsCodePlugin)
                try
                {
                    PluginHost.Load(installed);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "加载代码插件失败：" + installed.Id, ModBase.LogLevel.Feedback,
                        userSummary: Lang.Text("Download.Plugin.Error.LoadFailed", installed.DisplayName));
                }

            HintService.Hint(Lang.Text("Download.Plugin.Imported", installed.DisplayName), HintType.Success);
            RefreshList();
        }
        catch (InvalidDataException ex)
        {
            HintService.Hint(ex.Message, HintType.Error);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "导入插件失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Plugin.Import.Failed"));
        }
    }
}