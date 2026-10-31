using System.IO;
using System.Reflection;
using PCL.Core.App;
using PCL.Core.App.Localization;

namespace PCL;

/// <summary>
///     会执行代码的插件宿主。
///     <para>
///         清单里声明了 <c>"runtime": "dotnet"</c> 的 <c>.pclne</c> 插件，会在其包内放置一个 .NET 程序集。
///         宿主用反射加载该程序集、new 出 <c>entry</c> 指定的入口类型，并调用它的
///         <c>public void Load(object host)</c>。
///     </para>
///     <para>
///         插件侧不需要引用 PCL 的任何程序集：拿到的 <c>host</c> 是一个普通对象，
///         用 <c>dynamic</c> 或反射按名字调用它的公开方法即可。宿主提供的 API：
///         <list type="bullet">
///             <item><c>int ApiVersion</c>、<c>string PluginId</c>、<c>string LauncherVersionName</c>、<c>int LauncherVersionCode</c></item>
///             <item><c>void Log(string)</c>、<c>void LogError(string)</c>、<c>void Hint(string)</c></item>
///             <item><c>void OpenWebsite(string url)</c></item>
///             <item><c>void RegisterLink(string label, string url)</c> —— 给插件自己在列表里加一条链接</item>
///             <item><c>void On(string eventName, Delegate handler)</c> —— 注册事件钩子</item>
///         </list>
///     </para>
///     <para>
///         事件钩子（<c>On</c> 的第一个参数）：<c>LauncherReady</c>（无参）、
///         <c>GameLaunching</c>（<c>Action&lt;string&gt;</c>，实例名）、
///         <c>GameExited</c>（<c>Action&lt;int&gt;</c>，退出码）。
///     </para>
///     <para>
///         ⚠️ 代码插件是**完全信任**的：它以启动器自身权限运行，能读写文件、访问网络。
///         请不要安装来源不明的插件。另外 .NET 无法卸载已加载的程序集，
///         卸载插件后需要重启启动器才会真正停止它的代码。
///     </para>
/// </summary>
public static class PluginHost
{
    /// <summary>宿主 API 版本，插件可用它做兼容判断。</summary>
    public const int ApiVersion = 1;

    /// <summary>宿主门面对象的类型名，插件可以用反射按这个名字找它。</summary>
    public const string HostTypeName = "PCL.PluginHost+PluginHostApi";

    private static readonly List<LoadedPlugin> Loaded = new();
    private static readonly HashSet<string> LoadedIds = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object SyncRoot = new();

    /// <summary>
    ///     已加载的代码插件数量。
    /// </summary>
    public static int LoadedCount
    {
        get
        {
            lock (SyncRoot)
                return Loaded.Count;
        }
    }

    /// <summary>
    ///     加载全部「声明了 runtime 且已安装」的插件。启动时调用一次，导入插件后也可再调。
    ///     同一个 id 不会被加载两次。
    /// </summary>
    public static void LoadAll()
    {
        foreach (var info in ModPlugin.GetAll())
            try
            {
                Load(info);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "加载代码插件失败：" + info.Id, ModBase.LogLevel.Feedback,
                    userSummary: Lang.Text("Download.Plugin.Error.LoadFailed", info.DisplayName));
            }
    }

    /// <summary>
    ///     加载单个插件（不是代码插件则直接返回）。
    /// </summary>
    public static void Load(ModPlugin.PluginInfo info)
    {
        if (info is null || !info.IsCodePlugin)
            return;
        lock (SyncRoot)
        {
            if (!LoadedIds.Add(info.Id))
                return;
        }

        var assemblyPath = ResolveAssemblyPath(info);
        if (assemblyPath is null)
            throw new InvalidDataException(Lang.Text("Download.Plugin.Error.AssemblyMissing"));

        var assembly = Assembly.LoadFrom(assemblyPath);

        var entryType = ResolveEntryType(assembly, info.Entry);
        if (entryType is null)
            throw new InvalidDataException(Lang.Text("Download.Plugin.Error.EntryNotFound", info.Entry));

        var instance = Activator.CreateInstance(entryType)
                       ?? throw new InvalidDataException(Lang.Text("Download.Plugin.Error.EntryNotFound", info.Entry));
        var loadMethod = entryType.GetMethod("Load", BindingFlags.Public | BindingFlags.Instance, null,
            new[] { typeof(object) }, null);
        if (loadMethod is null)
            throw new InvalidDataException(Lang.Text("Download.Plugin.Error.EntryLoadMissing", info.Entry));

        var plugin = new LoadedPlugin { Info = info, Instance = instance, Assembly = assembly };
        lock (SyncRoot)
            Loaded.Add(plugin);

        // 把宿主门面交给插件。插件用 dynamic / 反射调用它的公开方法。
        loadMethod.Invoke(instance, new object[] { new PluginHostApi(plugin) });
        ModBase.Log($"[Plugin] 已加载代码插件 {info.Id} {info.Version}（入口 {entryType.FullName}）");
    }

    /// <summary>
    ///     只清掉事件钩子与宿主记录。.NET 无法卸载程序集，插件代码要重启后才真正停止。
    /// </summary>
    public static void UnloadAll()
    {
        lock (SyncRoot)
        {
            Loaded.Clear();
            LoadedIds.Clear();
        }
    }

    /// <summary>启动器初始化完成。同一次运行只触发一次。</summary>
    public static void RaiseLauncherReady()
    {
        lock (SyncRoot)
        {
            if (_launcherReadyRaised)
                return;
            _launcherReadyRaised = true;
        }

        Raise("LauncherReady", null);
    }

    private static bool _launcherReadyRaised;

    /// <summary>即将启动游戏。</summary>
    public static void RaiseGameLaunching(string instanceName)
    {
        Raise("GameLaunching", instanceName);
    }

    /// <summary>游戏进程已退出。</summary>
    public static void RaiseGameExited(int exitCode)
    {
        Raise("GameExited", exitCode);
    }

    /// <summary>
    ///     触发一个事件。任何插件的异常都会被吞掉并记日志，绝不影响启动器本身。
    /// </summary>
    public static void Raise(string eventName, object arg)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            return;
        List<(string Id, Delegate Handler)> handlers;
        lock (SyncRoot)
            handlers = Loaded
                .Where(plugin => plugin.Handlers.ContainsKey(eventName))
                .SelectMany(plugin => plugin.Handlers[eventName].Select(handler => (plugin.Info.Id, handler)))
                .ToList();

        foreach (var (id, handler) in handlers)
            try
            {
                var parameters = handler.Method.GetParameters();
                if (parameters.Length == 0)
                    handler.DynamicInvoke();
                else if (arg is not null && parameters.Length == 1)
                    handler.DynamicInvoke(arg);
                else
                    continue; // 参数个数对不上，跳过
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"插件 {id} 的事件处理出错（{eventName}）", ModBase.LogLevel.Feedback);
            }
    }

    private static string? ResolveAssemblyPath(ModPlugin.PluginInfo info)
    {
        var folder = info.Folder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return null;
        // entry 里逗号后面是程序集名（可省略扩展名）；找不到就退回插件目录下唯一的 dll
        var parts = (info.Entry ?? "").Split(',');
        if (parts.Length >= 2)
        {
            var name = parts[1].Trim();
            var candidate = Path.Combine(folder, name.EndsWithF(".dll", true) ? name : name + ".dll");
            if (File.Exists(candidate))
                return candidate;
        }

        var dlls = Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly);
        return dlls.Length == 1 ? dlls[0] : null;
    }

    private static Type? ResolveEntryType(Assembly assembly, string entry)
    {
        var typeName = (entry ?? "").Split(',')[0].Trim();
        if (string.IsNullOrWhiteSpace(typeName))
            return null;
        var type = assembly.GetType(typeName, false);
        if (type is not null)
            return type;
        // 允许省略命名空间的情形：按简单类名在所有公开类型里找
        return assembly.GetExportedTypes().FirstOrDefault(t =>
            t.Name.Equals(typeName, StringComparison.Ordinal) ||
            (t.FullName ?? "").Equals(typeName, StringComparison.Ordinal));
    }

    internal sealed class LoadedPlugin
    {
        public Assembly Assembly;
        public readonly Dictionary<string, List<Delegate>> Handlers = new(StringComparer.OrdinalIgnoreCase);
        public ModPlugin.PluginInfo Info;
        public object Instance;
    }

    /// <summary>
    ///     交给插件的宿主门面。这些公开方法就是插件的 API，改动等于破坏兼容性。
    /// </summary>
    public sealed class PluginHostApi
    {
        private readonly LoadedPlugin _plugin;

        internal PluginHostApi(LoadedPlugin plugin)
        {
            _plugin = plugin;
        }

        /// <summary>宿主 API 版本。</summary>
        public int ApiVersion => PluginHost.ApiVersion;

        /// <summary>本插件的 id。</summary>
        public string PluginId => _plugin.Info.Id;

        /// <summary>插件所在目录。</summary>
        public string PluginFolder => _plugin.Info.Folder;

        /// <summary>启动器版本名，如「2.15.2」。</summary>
        public string LauncherVersionName => Basics.VersionName;

        /// <summary>启动器版本代码。</summary>
        public int LauncherVersionCode => Basics.VersionCode;

        /// <summary>写一条普通日志。</summary>
        public void Log(string message)
        {
            ModBase.Log("[Plugin:" + _plugin.Info.Id + "] " + message);
        }

        /// <summary>写一条错误日志。</summary>
        public void LogError(string message)
        {
            ModBase.Log("[Plugin:" + _plugin.Info.Id + "] " + message, ModBase.LogLevel.Feedback);
        }

        /// <summary>给用户弹一条提示。</summary>
        public void Hint(string message)
        {
            HintService.Hint(message);
        }

        /// <summary>用默认浏览器打开一个 http/https 链接。</summary>
        public void OpenWebsite(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                ModBase.OpenWebsite(uri.ToString());
        }

        /// <summary>
        ///     给插件自己在列表里追加一条链接，并刷新插件页。
        /// </summary>
        public void RegisterLink(string label, string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                return;
            _plugin.Info.Links.Add(new ModPlugin.PluginLink
            {
                Label = string.IsNullOrWhiteSpace(label) ? uri.ToString() : label, Url = uri.ToString()
            });
            ModMain.frmDownloadPlugin?.RefreshList();
        }

        /// <summary>
        ///     注册一个事件钩子。事件名见 <see cref="PluginHost" /> 的说明，
        ///     无参事件用 <c>Action</c>，带参事件用 <c>Action&lt;string&gt;</c> / <c>Action&lt;int&gt;</c>。
        /// </summary>
        public void On(string eventName, Delegate handler)
        {
            if (string.IsNullOrWhiteSpace(eventName) || handler is null)
                return;
            if (!_plugin.Handlers.TryGetValue(eventName, out var list))
            {
                list = new List<Delegate>();
                _plugin.Handlers[eventName] = list;
            }

            list.Add(handler);
        }
    }
}