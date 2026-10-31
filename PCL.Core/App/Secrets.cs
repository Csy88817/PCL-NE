using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;

namespace PCL.Core.App;

// ReSharper disable InconsistentNaming
public static class Secrets
{
    /// <summary>
    /// 微软 OAuth 的 Client ID
    /// </summary>
    public static string MSOAuthClientId => ResolveMsOAuthClientId();

    // 内置默认值：上游 PCL CE 注册的 Azure 应用 ID（公开标识符，不是密钥）
    private const string DefaultMsOAuthClientId = "c14b0370-8d75-42f8-b329-5b60d39e319f";

    // 取值顺序：编译期注入的密钥 -> 配置项 MsOAuthClientId -> 内置默认值
    private static string ResolveMsOAuthClientId()
    {
        var fromBuild = EnvironmentInterop.GetSecret("MS_CLIENT_ID", readEnvDebugOnly: true);
        if (!string.IsNullOrWhiteSpace(fromBuild)) return fromBuild;
        try
        {
            var fromConfig = Config.Launch.MsOAuthClientId;
            if (!string.IsNullOrWhiteSpace(fromConfig)) return fromConfig;
        }
        catch
        {
            // 配置系统尚未就绪时忽略，退回内置默认值
        }

        return DefaultMsOAuthClientId;
    }

    /// <summary>
    /// CurseForge API 的 Client ID
    /// </summary>
    public static string CurseForgeAPIKey { get; } = EnvironmentInterop.GetSecret("CURSEFORGE_API_KEY", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// 遥测密钥
    /// </summary>
    public static string TelemetryKey { get; } = EnvironmentInterop.GetSecret("TELEMETRY_KEY", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// Natayark ID OAuth 的 Client ID
    /// </summary>
    public static string NatayarkClientId { get; } = EnvironmentInterop.GetSecret("NAID_CLIENT_ID", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// Natayark ID OAuth 的 Client ID
    /// </summary>
    public static string NatayarkClientSecret { get; } = EnvironmentInterop.GetSecret("NAID_CLIENT_SECRET", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// 联机根服务器
    /// </summary>
    public static string[] LinkServers { get; } = EnvironmentInterop.GetSecret("LINK_SERVER_ROOT", readEnvDebugOnly: true).ReplaceNullOrEmpty().Split("|");

    /// <summary>
    /// 当前版本的 Git 提交 SHA
    /// </summary>
    public static string CommitHash { get; } = EnvironmentInterop.GetSecret("GITHUB_SHA", readEnvDebugOnly: true).ReplaceNullOrEmpty();
}
