using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App.Localization;

namespace PCL;

/// <summary>
///     MCJS（MC in JavaScript，Eaglercraft 的简中汉化整合站）页面。
///     这里只做「列出各版本与线路、点一下用浏览器打开」，不下载也不托管任何文件。
/// </summary>
public partial class PageDownloadMcjs
{
    /// <summary>MCJS 唯一官方网站。</summary>
    private const string OfficialSite = "https://mcjs.link";

    /// <summary>线路分组。</summary>
    private enum MirrorKind
    {
        /// <summary>正式版：1~7 号镜像站。</summary>
        Release,

        /// <summary>高版本测试版：高版本镜像站。</summary>
        Beta
    }

    /// <summary>一个可打开的 MCJS 版本。</summary>
    private sealed class McjsVersion
    {
        /// <summary>显示名，如“EaglercraftX 1.8.8 (WASM)”。</summary>
        public string Name;

        /// <summary>线路地址里的版本标识，如“1.8.8wasm”。</summary>
        public string VersionId;

        /// <summary>列表项右侧的说明。</summary>
        public string Info;

        /// <summary>线路分组。</summary>
        public MirrorKind Kind;
    }

    public PageDownloadMcjs()
    {
        InitializeComponent();
        Loaded += (_, _) => Init();
        BtnWeb.Click += (_, _) => ModBase.OpenWebsite(OfficialSite);
    }

    /// <summary>
    ///     界面上首次加载后构建列表。数据是静态的，不做任何联网。
    /// </summary>
    private void Init()
    {
        PanBack.ScrollToHome();
        PanMain.Children.Clear();

        AddCard("Download.Mcjs.Group.Release", "Download.Mcjs.Group.Release.Description", new[]
        {
            new McjsVersion { Name = "EaglercraftX 1.8.8", VersionId = "1.8.8", Kind = MirrorKind.Release,
                Info = "MC 1.8.8 · JS · 约 21.2 MB · 含简中 · 支持触屏与联机" },
            new McjsVersion { Name = "EaglercraftX 1.8.8 (WASM)", VersionId = "1.8.8wasm", Kind = MirrorKind.Release,
                Info = "MC 1.8.8 · WASM · 约 9.7 MB · 含简中 · 更流畅，需要新版 Chrome" },
            new McjsVersion { Name = "Eaglercraft 1.12.2", VersionId = "1.12.2", Kind = MirrorKind.Release,
                Info = "MC 1.12.2 · JS · 约 29.8 MB · 含简中 · 支持联机" },
            new McjsVersion { Name = "Eaglercraft 1.12.2 (WASM)", VersionId = "1.12.2wasm", Kind = MirrorKind.Release,
                Info = "MC 1.12.2 · WASM · 约 16.3 MB · 含简中 · 更流畅，需要新版 Chrome" }
        });

        AddCard("Download.Mcjs.Group.Beta", "Download.Mcjs.Group.Beta.Description", new[]
        {
            new McjsVersion { Name = "Eaglercraft 1.13.2 (WASM)", VersionId = "1.13.2", Kind = MirrorKind.Beta, Info = "MC 1.13.2 · 约 40 MB · 含简中" },
            new McjsVersion { Name = "Eaglercraft 1.14.4 (WASM)", VersionId = "1.14.4", Kind = MirrorKind.Beta, Info = "MC 1.14.4 · 约 25 MB · 已停更" },
            new McjsVersion { Name = "Eaglercraft 1.15.2 (WASM)", VersionId = "1.15.2", Kind = MirrorKind.Beta, Info = "MC 1.15.2 · 约 35 MB" },
            new McjsVersion { Name = "Eaglercraft 1.16.5 (WASM)", VersionId = "1.16.5", Kind = MirrorKind.Beta, Info = "MC 1.16.5 · 约 35 MB" },
            new McjsVersion { Name = "Eaglercraft 1.20.6 (WASM)", VersionId = "1.20.6", Kind = MirrorKind.Beta, Info = "MC 1.20.6 · 约 40 MB · 已停更" },
            new McjsVersion { Name = "Eaglercraft 1.21.11 (WASM)", VersionId = "1.21.11", Kind = MirrorKind.Beta, Info = "MC 1.21.11 · 约 40 MB · 暂无翻译" }
        });

        AddCard("Download.Mcjs.Group.Modpack", "Download.Mcjs.Group.Modpack.Description", new[]
        {
            new McjsVersion { Name = "1.6.4 Forge · Lite", VersionId = "modpack/lite", Kind = MirrorKind.Release, Info = "内置轻量版模组整合包 · 约 25.7 MB" },
            new McjsVersion { Name = "1.6.4 Forge · Tech", VersionId = "modpack/tech", Kind = MirrorKind.Release, Info = "内置科技模组整合包 · 约 28.3 MB" },
            new McjsVersion { Name = "1.6.4 Forge · Skyfactory", VersionId = "modpack/skyfactory", Kind = MirrorKind.Release, Info = "内置天空工厂整合包 · 约 26.5 MB" },
            new McjsVersion { Name = "1.6.4 Forge · Magic", VersionId = "modpack/magic", Kind = MirrorKind.Release, Info = "内置魔法模组整合包 · 约 30.3 MB" }
        });

        AddCard("Download.Mcjs.Group.Legacy", "Download.Mcjs.Group.Legacy.Description", new[]
        {
            new McjsVersion { Name = "Eaglercraft 1.6.4", VersionId = "1.6.4", Kind = MirrorKind.Release, Info = "MC 1.6.4 · 约 23.5 MB · 早期版本" },
            new McjsVersion { Name = "Eaglercraft 1.5.2", VersionId = "1.5.2", Kind = MirrorKind.Release, Info = "MC 1.5.2 · 约 20.2 MB · 早期版本" },
            new McjsVersion { Name = "Eaglercraft 1.2.5", VersionId = "1.2.5", Kind = MirrorKind.Release, Info = "MC 1.2.5 · 约 21.3 MB · 早期版本" },
            new McjsVersion { Name = "Eaglercraft Beta 1.7.3", VersionId = "legacy/beta1.7.3", Kind = MirrorKind.Release, Info = "Beta 1.7.3 · 约 15.6 MB" },
            new McjsVersion { Name = "Eaglercraft Beta 1.3", VersionId = "legacy/beta1.3", Kind = MirrorKind.Release, Info = "Beta 1.3 · 约 4.3 MB" },
            new McjsVersion { Name = "Eaglercraft Alpha 1.2.6", VersionId = "legacy/alpha1.2.6", Kind = MirrorKind.Release, Info = "Alpha 1.2.6 · 约 8.2 MB" }
        });
    }

    /// <summary>
    ///     往界面里追加一组卡片。
    /// </summary>
    private void AddCard(string titleKey, string descriptionKey, IReadOnlyList<McjsVersion> items)
    {
        var card = new MyCard
        {
            Title = Lang.Text(titleKey),
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
        var stack = new StackPanel
        {
            Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransform = new TranslateTransform(0d, 0d),
            Tag = items
        };
        card.Children.Add(stack);
        card.SwapControl = stack;
        card.IsSwapped = true;
        card.InstallMethod = target =>
        {
            target.Children.Add(new TextBlock
            {
                Text = Lang.Text(descriptionKey),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0d, 0d, 0d, 8d),
                Opacity = 0.75
            });
            foreach (var item in (IEnumerable)target.Tag) AddListItem(target, (McjsVersion)item);
        };
        PanMain.Children.Add(card);
    }

    /// <summary>
    ///     生成单个版本的可点击列表项。
    /// </summary>
    private static void AddListItem(Panel parent, McjsVersion version)
    {
        var listItem = new MyListItem
        {
            Title = version.Name,
            Info = version.Info,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            SnapsToDevicePixels = true,
            Tag = version,
            Logo = ModBase.pathImage + "Blocks/GrassPath.png"
        };
        listItem.Click += McjsOpen_Click;
        listItem.ContentHandler = (sender, _) => McjsContMenuBuild(sender);
        parent.Children.Add(listItem);
    }

    // 左键：直接打开首选线路（正式版走 1 号镜像，测试版走高版本镜像站）
    private static void McjsOpen_Click(object sender, RoutedEventArgs e)
    {
        var version = (McjsVersion)((dynamic)sender).Tag;
        ModBase.OpenWebsite(GetMirror(version, 1));
    }

    // 右键：给一个「其它线路」按钮，以及官网入口
    private static void McjsContMenuBuild(MyListItem sender)
    {
        var version = (McjsVersion)sender.Tag;
        var btnMirror = new MyIconButton
        {
            SvgIcon = "lucide/globe", ToolTip = Lang.Text("Download.Mcjs.Mirror.Tooltip"), Tag = version
        };
        btnMirror.Click += (_, _) => ModBase.OpenWebsite(GetMirror(version, 2));
        var btnWeb = new MyIconButton
        {
            SvgIcon = "lucide/external-link", ToolTip = Lang.Text("Download.Mcjs.OpenWebsite"), Tag = version
        };
        btnWeb.Click += (_, _) => ModBase.OpenWebsite(OfficialSite);
        sender.Buttons = [btnMirror, btnWeb];
    }

    /// <summary>
    ///     取某个版本的线路地址。<paramref name="index" /> 从 1 开始，超出实际线路数时回退到官网。
    /// </summary>
    private static string GetMirror(McjsVersion version, int index)
    {
        var id = version.VersionId ?? "";
        if (version.Kind == MirrorKind.Beta)
            return index switch
            {
                1 => "https://mcjs.144449.xyz/" + id,
                2 => "https://mcjs-beta.144449.xyz/" + id,
                3 => "https://mcjs-r2.pages.dev/assets/game?ver=" + id,
                _ => OfficialSite
            };
        return index switch
        {
            1 => "https://1.mcjslink.144449.xyz/" + id,
            2 => "https://2.mcjslink.144449.xyz/" + id,
            3 => "https://3.mcjslink.144449.xyz/" + id,
            4 => "https://play.mcjs.cc/" + id,
            5 => "https://playmcjscc.pages.dev/" + id,
            6 => "https://play.mcjs.144449.xyz/" + id,
            7 => "https://mcjs-mirror.144449.xyz/" + id,
            _ => OfficialSite
        };
    }
}