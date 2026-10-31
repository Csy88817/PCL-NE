using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Network;

namespace PCL;

/// <summary>
///     PCL NE 的启动器插件（<c>.pclne</c>）。
///     <para>
///         <c>.pclne</c> 就是一个 zip 包，根目录必须有 <c>manifest.json</c>。
///         插件是**声明式**的：启动器只读取并展示清单里声明的内容（名称、作者、说明、链接、图标、说明文档），
///         <b>不会执行插件内的任何代码</b>。这样插件无法触碰账号、存档或启动流程。
///     </para>
///     <para>
///         清单格式（除 id / name / version 外都可省略）：
///         <code>
///         {
///           "id": "example.hello",
///           "name": "示例插件",
///           "version": "1.0.0",
///           "author": "作者名",
///           "description": "一句话说明",
///           "homepage": "https://example.com",
///           "minLauncherVersion": 525,
///           "links": [ { "label": "官网", "url": "https://example.com" } ]
///         }
///         </code>
///     </para>
/// </summary>
public static class ModPlugin
{
    /// <summary>清单文件名，必须位于包根目录。</summary>
    private const string ManifestName = "manifest.json";

    /// <summary>图标文件名（可选）。</summary>
    private const string IconName = "icon.png";

    /// <summary>说明文档文件名（可选）。</summary>
    private const string ReadmeName = "readme.md";

    /// <summary>暂存目录前缀，用于安装过程中的半成品；列表与卸载都会跳过它。</summary>
    private const string StagingPrefix = ".staging-";

    /// <summary>单个插件解压后的体积上限，防止 zip 炸弹。</summary>
    private const long MaxTotalBytes = 32L * 1024 * 1024;

    /// <summary>单个插件的文件数上限。</summary>
    private const int MaxEntryCount = 500;

    /// <summary>清单里 links 的条数上限。</summary>
    private const int MaxLinkCount = 20;

    /// <summary>
    ///     插件 id 允许的字符。与文件夹名共用一套规则，天然挡住路径穿越。
    /// </summary>
    private static readonly Regex IdPattern =
        new("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.Compiled);

    /// <summary>
    ///     插件安装目录：启动器目录下的 Plugins。
    /// </summary>
    public static string PluginsFolder => Path.Combine(ModBase.pathPure, "Plugins");

    /// <summary>插件清单里声明的一条链接。</summary>
    public sealed class PluginLink
    {
        public string Label;
        public string Url;
    }

    /// <summary>一个已安装（或待安装）插件的全部信息。</summary>
    public sealed class PluginInfo
    {
        public string Id = "";
        public string Name = "";
        public string Version = "";
        public string Author = "";
        public string Description = "";
        public string Homepage = "";
        public int MinLauncherVersion;

        /// <summary>
        ///     运行方式。为空表示纯声明式插件（不执行代码）；<c>dotnet</c> 表示会加载并执行包内的 .NET 程序集。
        /// </summary>
        public string Runtime = "";

        /// <summary>
        ///     入口类型，格式为「命名空间.类型名, 程序集名」，例如 <c>MyPlugin.Main, MyPlugin</c>。
        ///     只有 <see cref="IsCodePlugin" /> 为真时才有意义。
        /// </summary>
        public string Entry = "";

        public List<PluginLink> Links = new();

        /// <summary>是否是需要执行代码的插件。</summary>
        public bool IsCodePlugin => Runtime.Equals("dotnet", StringComparison.OrdinalIgnoreCase);

        /// <summary>安装目录的完整路径。</summary>
        public string Folder = "";

        /// <summary>图标文件完整路径；没有则为 Nothing。</summary>
        public string IconPath;

        /// <summary>说明文档完整路径；没有则为 Nothing。</summary>
        public string ReadmePath;

        /// <summary>列表里显示的标题，如“示例插件 1.0.0”。</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(Version) ? Name : Name + " " + Version;
    }

    /// <summary>
    ///     读取全部已安装插件。目录不存在或某个插件损坏时跳过它，不会抛出异常。
    /// </summary>
    public static List<PluginInfo> GetAll()
    {
        var result = new List<PluginInfo>();
        try
        {
            if (!Directory.Exists(PluginsFolder))
                return result;
            foreach (var folder in Directory.GetDirectories(PluginsFolder))
                try
                {
                    // 跳过安装过程中的暂存目录；顺手清掉超过 1 小时的残留（例如上次安装中途崩了）
                    if (Path.GetFileName(folder).StartsWithF(StagingPrefix, true))
                    {
                        try
                        {
                            if (Directory.GetLastWriteTime(folder) < DateTime.Now.AddHours(-1))
                                Directory.Delete(folder, true);
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, "清理插件暂存目录失败：" + folder, ModBase.LogLevel.Developer);
                        }

                        continue;
                    }
                    var info = ReadFromFolder(folder);
                    if (info is not null)
                        result.Add(info);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "读取插件失败，已跳过：" + folder, ModBase.LogLevel.Developer);
                }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "枚举插件目录失败", ModBase.LogLevel.Developer);
        }

        return result.OrderBy(info => info.Name, StringComparer.CurrentCulture).ToList();
    }

    /// <summary>
    ///     某个 id 是否已经装过。
    /// </summary>
    public static bool IsInstalled(string id)
    {
        if (!IsIdValid(id))
            return false;
        return Directory.Exists(Path.Combine(PluginsFolder, id));
    }

    #region 插件商店

    /// <summary>商店索引地址（本站 Worker 动态返回）。</summary>
    public const string StoreApi = "https://pcl-ne.simpplay.cc.cd/api/plugins";

    /// <summary>商店里的一条插件。</summary>
    public sealed class StoreEntry
    {
        public string Author = "";
        public string Description = "";
        public string File = "";
        public string Homepage = "";
        public string Id = "";
        public string Name = "";
        public string Runtime = "";
        public string Sha256 = "";
        public long Size;
        public string Tags = "";
        public string UpdatedAt = "";
        public string Version = "";

        public bool IsCodePlugin => Runtime.Equals("dotnet", StringComparison.OrdinalIgnoreCase);

        public string DisplayName => string.IsNullOrWhiteSpace(Version) ? Name : Name + " " + Version;
    }

    /// <summary>
    ///     商店列表加载器。用 PageLoaderInit 挂在页面上即可获得标准的加载动画与错误提示。
    /// </summary>
    public static ModLoader.LoaderTask<int, List<StoreEntry>> StoreLoader =
        new("ModPluginStore", loader => loader.output = FetchStore());

    /// <summary>
    ///     拉取商店索引。
    /// </summary>
    public static List<StoreEntry> FetchStore()
    {
        var json = (JsonObject)Requester.FetchJson(StoreApi, RequestParam.WithRetry);
        if (json["plugins"] is not JsonArray list)
            throw new Exception(Lang.Text("Download.Plugin.Store.Error.BadIndex"));

        var result = new List<StoreEntry>();
        foreach (var node in list)
        {
            if (node is not JsonObject item)
                continue;
            var entry = new StoreEntry
            {
                Id = StoreText(item, "id", 64),
                Name = StoreText(item, "name", 200),
                Version = StoreText(item, "version", 64),
                Author = StoreText(item, "author", 200),
                Description = StoreText(item, "description", 2000),
                Homepage = SanitizeUrl(StoreText(item, "homepage", 500)),
                Runtime = StoreText(item, "runtime", 32),
                File = StoreText(item, "file", 1000),
                Sha256 = StoreText(item, "sha256", 64),
                Tags = string.Join(" · ",
                    (item["tags"] as JsonArray)?.Select(t => t?.ToString() ?? "") ?? Array.Empty<string>()),
                UpdatedAt = StoreText(item, "updatedAt", 32)
            };
            if (long.TryParse(item["size"]?.ToString(), out var size))
                entry.Size = size;
            if (string.IsNullOrWhiteSpace(entry.Name))
                entry.Name = entry.Id;
            // 没有 id 或没有下载地址的条目直接跳过，避免列表里出现点了没反应的项
            if (!IsIdValid(entry.Id) || string.IsNullOrWhiteSpace(entry.File))
                continue;
            result.Add(entry);
        }

        return result;
    }

    /// <summary>
    ///     从商店安装：下载到临时目录 → 校验 sha256 → 安装 → 代码插件立刻加载。
    /// </summary>
    public static PluginInfo InstallFromStore(StoreEntry entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.File))
            throw new InvalidDataException(Lang.Text("Download.Plugin.Error.FileMissing"));

        var fileName = entry.File.Split('/').Last().Split('?')[0];
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = entry.Id + ".pclne";
        var target = Path.Combine(ModBase.pathTemp, "PluginStore", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        var checker = string.IsNullOrWhiteSpace(entry.Sha256)
            ? new ModBase.FileChecker(minSize: 256)
            : new ModBase.FileChecker(hash: entry.Sha256.ToLowerInvariant());
        if (checker.Check(target) is not null)
            ModNet.NetDownloadByLoader(new[] { entry.File }, target, check: checker);

        var installed = Install(target, true);
        if (installed.IsCodePlugin)
            PluginHost.Load(installed);
        ModBase.Log($"[Plugin] 已从商店安装 {installed.Id} {installed.Version}");
        return installed;
    }

    private static string StoreText(JsonObject json, string key, int maxLength)
    {
        var value = (json[key]?.ToString() ?? "").Trim();
        return value.Length > maxLength ? value.Substring(0, maxLength) : value;
    }

    #endregion

    /// <summary>
    ///     只读取包内的清单，不做安装。用于导入前判断 id 与是否需要覆盖。
    /// </summary>
    public static PluginInfo Peek(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
            throw new InvalidDataException(Lang.Text("Download.Plugin.Error.FileMissing"));

        using var archive = ZipFile.OpenRead(packagePath);
        var entry = archive.Entries.FirstOrDefault(e =>
            e.FullName.Replace('\\', '/').TrimStart('/').Equals(ManifestName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            throw new InvalidDataException(Lang.Text("Download.Plugin.Error.NoManifest"));

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        var info = ParseManifest(reader.ReadToEnd());
        if (!IsIdValid(info.Id))
            throw new InvalidDataException(Lang.Text("Download.Plugin.Error.InvalidId", info.Id ?? ""));
        return info;
    }

    /// <summary>
    ///     安装一个 .pclne 包。<paramref name="overwrite" /> 为 false 且同 id 已存在时抛 <see cref="InvalidOperationException" />。
    ///     校验失败一律抛 <see cref="InvalidDataException" />，调用方负责提示用户。
    /// </summary>
    public static PluginInfo Install(string packagePath, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
            throw new InvalidDataException(Lang.Text("Download.Plugin.Error.FileMissing"));

        Directory.CreateDirectory(PluginsFolder);

        // 先解到插件目录下的暂存文件夹，全部校验通过后再搬进 Plugins\<id>。
        // 暂存必须和最终目录在同一个卷上，否则 Directory.Move 会因跨卷失败。
        var tempFolder = Path.Combine(PluginsFolder, StagingPrefix + ModBase.GetUuid());
        try
        {
            Directory.CreateDirectory(tempFolder);
            ExtractSafely(packagePath, tempFolder);

            var manifestPath = Path.Combine(tempFolder, ManifestName);
            if (!File.Exists(manifestPath))
                throw new InvalidDataException(Lang.Text("Download.Plugin.Error.NoManifest"));

            var info = ParseManifest(ModBase.ReadFile(manifestPath));
            if (!IsIdValid(info.Id))
                throw new InvalidDataException(Lang.Text("Download.Plugin.Error.InvalidId", info.Id ?? ""));
            if (info.MinLauncherVersion > 0 && info.MinLauncherVersion > Basics.VersionCode)
                throw new InvalidDataException(
                    Lang.Text("Download.Plugin.Error.LauncherTooOld", info.MinLauncherVersion, Basics.VersionCode));

            var target = Path.Combine(PluginsFolder, info.Id);
            if (Directory.Exists(target))
            {
                if (!overwrite)
                    throw new InvalidOperationException(Lang.Text("Download.Plugin.Error.AlreadyExists", info.Name));
                SafeDeletePluginFolder(target);
            }

            Directory.Move(tempFolder, target);
            tempFolder = null; // 已经搬走了，不再清理

            var installed = ReadFromFolder(target);
            if (installed is null)
                throw new InvalidDataException(Lang.Text("Download.Plugin.Error.InvalidManifest"));
            ModBase.Log($"[Plugin] 已安装插件 {installed.Id} {installed.Version} 到 {target}");
            return installed;
        }
        finally
        {
            if (tempFolder is not null)
                try
                {
                    if (Directory.Exists(tempFolder))
                        Directory.Delete(tempFolder, true);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "清理插件临时目录失败", ModBase.LogLevel.Developer);
                }
        }
    }

    /// <summary>
    ///     卸载一个插件（只删除插件目录下它自己的文件夹）。
    /// </summary>
    public static void Uninstall(PluginInfo plugin)
    {
        if (plugin is null || string.IsNullOrWhiteSpace(plugin.Folder))
            return;
        SafeDeletePluginFolder(plugin.Folder);
        ModBase.Log($"[Plugin] 已卸载插件 {plugin.Id}");
    }

    /// <summary>
    ///     从插件目录读清单。
    /// </summary>
    private static PluginInfo? ReadFromFolder(string folder)
    {
        var manifestPath = Path.Combine(folder, ManifestName);
        if (!File.Exists(manifestPath))
            return null;
        var info = ParseManifest(ModBase.ReadFile(manifestPath));
        info.Folder = folder;
        var icon = Path.Combine(folder, IconName);
        info.IconPath = File.Exists(icon) ? icon : null;
        var readme = Path.Combine(folder, ReadmeName);
        info.ReadmePath = File.Exists(readme) ? readme : null;
        return info;
    }

    /// <summary>
    ///     解析清单文本。字段缺失或有明显问题时按「尽量可用」处理，不做严格拒绝。
    /// </summary>
    private static PluginInfo ParseManifest(string text)
    {
        JsonObject json;
        try
        {
            json = (JsonObject)ModBase.GetJson(text);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException(Lang.Text("Download.Plugin.Error.InvalidManifest"), ex);
        }

        if (json is null)
            throw new InvalidDataException(Lang.Text("Download.Plugin.Error.InvalidManifest"));

        var info = new PluginInfo
        {
            Id = Text(json, "id"),
            Name = Text(json, "name"),
            Version = Text(json, "version"),
            Author = Text(json, "author"),
            Description = Text(json, "description", 2000),
            Homepage = SanitizeUrl(Text(json, "homepage")),
            Runtime = Text(json, "runtime", 32),
            Entry = Text(json, "entry", 300)
        };
        if (int.TryParse(Text(json, "minLauncherVersion"), out var minVersion))
            info.MinLauncherVersion = minVersion;
        if (string.IsNullOrWhiteSpace(info.Name))
            info.Name = info.Id;

        if (json["links"] is JsonArray links)
            foreach (var node in links.Take(MaxLinkCount))
            {
                if (node is not JsonObject link)
                    continue;
                var url = SanitizeUrl(Text(link, "url"));
                if (string.IsNullOrWhiteSpace(url))
                    continue;
                var label = Text(link, "label", 80);
                info.Links.Add(new PluginLink
                {
                    Label = string.IsNullOrWhiteSpace(label) ? url : label, Url = url
                });
            }

        return info;
    }

    private static string Text(JsonObject json, string key, int maxLength = 200)
    {
        var value = json[key]?.ToString() ?? "";
        value = value.Trim();
        return value.Length > maxLength ? value.Substring(0, maxLength) : value;
    }

    /// <summary>
    ///     只允许 http/https 链接，其余一律丢掉（插件不能塞 file:// 之类的东西）。
    /// </summary>
    private static string SanitizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return "";
        return uri.Scheme is "http" or "https" ? uri.ToString() : "";
    }

    /// <summary>
    ///     解压到目标目录，逐项校验条目路径，拒绝绝对路径、盘符与 <c>..</c> 穿越。
    /// </summary>
    private static void ExtractSafely(string packagePath, string targetFolder)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        if (archive.Entries.Count > MaxEntryCount)
            throw new InvalidDataException(Lang.Text("Download.Plugin.Error.TooManyFiles", archive.Entries.Count));

        var root = Path.GetFullPath(targetFolder);
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        long total = 0L;

        foreach (var entry in archive.Entries)
        {
            total += entry.Length;
            if (total > MaxTotalBytes)
                throw new InvalidDataException(Lang.Text("Download.Plugin.Error.TooLarge"));

            // 目录条目只需要保证路径合法
            var relative = entry.FullName.Replace('\\', '/').TrimStart('/');
            if (relative.Length == 0)
                continue;
            if (relative.Split('/').Any(segment => segment is ".." or "."))
                throw new InvalidDataException(Lang.Text("Download.Plugin.Error.UnsafeEntry", entry.FullName));
            if (Path.IsPathRooted(relative) || relative.Contains(':'))
                throw new InvalidDataException(Lang.Text("Download.Plugin.Error.UnsafeEntry", entry.FullName));

            var destination = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(Lang.Text("Download.Plugin.Error.UnsafeEntry", entry.FullName));

            if (relative.EndsWithF("/"))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, true);
        }
    }

    /// <summary>
    ///     id 是否合法（同时也是安全的文件夹名）。
    /// </summary>
    private static bool IsIdValid(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && IdPattern.IsMatch(id);
    }

    /// <summary>
    ///     只允许删除 Plugins 目录下的子目录，避免任何越界删除。
    /// </summary>
    private static void SafeDeletePluginFolder(string folder)
    {
        var root = Path.GetFullPath(PluginsFolder);
        var target = Path.GetFullPath(folder);
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!target.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new IOException("拒绝删除插件目录之外的内容：" + target);
        if (Directory.Exists(target))
            ModBase.DeleteDirectory(target, true);
    }
}