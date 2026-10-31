using System.Globalization;
using System.IO;
using PCL.Core.App;
using PCL.Core.App.Configuration;

namespace PCL;

/// <summary>
///     启动时自动备份启动器自身配置。
///     <para>
///         备份对象是磁盘上真正保存配置的文件本体，也就是“能用来自我还原”的那几个文件：
///         <list type="bullet">
///             <item>全局配置 <c>%AppData%\PCLCE\config.v1.json</c>（<see cref="ConfigService.SharedConfigPath" />）</item>
///             <item>本地配置 <c>&lt;启动器目录&gt;\PCL\config.v1.yml</c>（<see cref="ConfigService.LocalConfigPath" />）</item>
///             <item>加密配置所需的用户密钥 <c>%AppData%\PCLCE\UserKey.bin</c>，缺少它就无法解开全局配置里被加密的项</item>
///         </list>
///     </para>
///     <para>
///         备份产物统一放在 <c>&lt;启动器目录&gt;\PCL\Backup\</c>，文件名形如
///         <c>config-2026-10-31 15-30-00.json</c>，按修改时间倒序只保留最近 <see cref="KeepCount" /> 份。
///         本类中的所有失败都只写日志，不弹窗、不阻塞启动、不影响程序继续运行。
///     </para>
/// </summary>
internal static class ConfigBackup
{
    /// <summary>
    ///     每种备份文件保留的份数上限。
    /// </summary>
    public const int KeepCount = 10;

    /// <summary>
    ///     备份文件统一前缀。一次备份会写出同名的多个不同后缀文件（json / yml / bin）。
    /// </summary>
    private const string FilePrefix = "config-";

    /// <summary>
    ///     时间戳格式，与文件名示例保持一致（Windows 文件名不能含冒号，故用 -）。
    /// </summary>
    private const string TimeFormat = "yyyy-MM-dd HH-mm-ss";

    /// <summary>
    ///     受管理的备份文件后缀。清理时只处理这些后缀，绝不碰用户自己往目录里放的东西。
    /// </summary>
    private static readonly string[] KnownExtensions = [".json", ".yml", ".bin"];

    /// <summary>
    ///     用户密钥文件名。与 PCL.Core 内部 EncryptHelper.KeyFilePath 保持一致
    ///     （那边是 internal，这里只能按同样的规则拼）。
    /// </summary>
    private const string UserKeyFileName = "UserKey.bin";

    private static int _started;

    /// <summary>
    ///     备份目录：<c>&lt;启动器目录&gt;\PCL\Backup\</c>。
    /// </summary>
    public static string BackupFolder => Path.Combine(Paths.Data, "Backup");

    /// <summary>
    ///     执行一次启动时自动备份。<br />
    ///     重复调用只会生效一次；配置尚未加载完成时直接跳过（不消耗调用机会，留待之后）。<br />
    ///     真正的复制与清理在后台线程执行，不会拖慢启动。
    /// </summary>
    public static void TryBackupOnce()
    {
        try
        {
            if (Volatile.Read(ref _started) == 1) return;
            // 必须在配置加载完成之后备份，否则可能备份到半初始化状态
            if (!ConfigService.IsInitialized) return;
            if (Interlocked.Exchange(ref _started, 1) == 1) return;
            Task.Run(() =>
            {
                try
                {
                    BackupCore();
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "自动备份配置失败", ModBase.LogLevel.Developer);
                }
            });
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "自动备份配置失败", ModBase.LogLevel.Developer);
        }
    }

    private static void BackupCore()
    {
        // 能完整还原一份配置所需的全部磁盘文件
        var sources = new (string Source, string Extension)[]
        {
            (ConfigService.SharedConfigPath, "json"),
            (ConfigService.LocalConfigPath, "yml"),
            (Path.Combine(Paths.SharedData, UserKeyFileName), "bin")
        };

        // 目录不存在或不可写时直接抛出，由外层统一安静处理
        Directory.CreateDirectory(BackupFolder);

        var timestamp = DateTime.Now.ToString(TimeFormat, CultureInfo.InvariantCulture);
        var copied = 0;
        foreach (var (source, extension) in sources)
        {
            try
            {
                if (!File.Exists(source)) continue; // 例如从未保存过配置
                var target = Path.Combine(BackupFolder, $"{FilePrefix}{timestamp}.{extension}");
                // 同一秒内的重复备份（理论上不会发生）直接跳过，避免互相覆盖
                if (File.Exists(target)) continue;
                File.Copy(source, target, false);
                copied++;
            }
            catch (Exception ex)
            {
                // 单个文件失败不影响其余文件，最常见的是源文件正被占用
                ModBase.Log(ex, $"自动备份配置失败：{source}", ModBase.LogLevel.Developer);
            }
        }

        if (copied == 0) return; // 一份都没成功（例如目录不可写），安静退出，不无限重试
        ModBase.Log($"[Backup] 已自动备份 {copied} 个配置文件到 {BackupFolder}", ModBase.LogLevel.Developer);
        Cleanup();
    }

    /// <summary>
    ///     按修改时间倒序，删除每种备份文件第 <see cref="KeepCount" /> 份之后的多余备份。
    /// </summary>
    private static void Cleanup()
    {
        var groups = Directory.EnumerateFiles(BackupFolder, FilePrefix + "*")
            .Where(IsBackupFile)
            .GroupBy(Path.GetExtension, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var expired = group
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .Skip(KeepCount);
            foreach (var info in expired)
            {
                try
                {
                    info.Delete();
                }
                catch (Exception ex)
                {
                    // 单个旧备份删不掉（例如被占用）不应打断其余清理
                    ModBase.Log(ex, $"自动备份配置失败：清理旧备份 {info.FullName}", ModBase.LogLevel.Developer);
                }
            }
        }
    }

    /// <summary>
    ///     判断是否为本次备份产出的文件，避免误删用户放在备份目录里的其他文件。
    /// </summary>
    private static bool IsBackupFile(string path)
    {
        if (!KnownExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) return false;
        var name = Path.GetFileNameWithoutExtension(path);
        if (!name.StartsWith(FilePrefix, StringComparison.Ordinal)) return false;
        var stamp = name[FilePrefix.Length..];
        return DateTime.TryParseExact(stamp, TimeFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _);
    }
}