using PCL.Core.App.Region;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

internal static class RestrictedFeaturePolicy
{
    /// <summary>
    ///     区域限制总开关。<br />
    ///     上游 PCL CE 会在大陆地区或无法判定区域时屏蔽 Minecraft 下载、补全、安装与整合包功能；
    ///     PCL NE 默认关闭该限制，即不再按国家和地区屏蔽这些功能。
    /// </summary>
    private const bool RegionRestrictionEnabled = false;

    internal static RegionAssessment Assess() => RegionPolicy.Evaluate(RegionPolicy.ReadWindowsSignals());

    internal static bool IsRestricted => RegionRestrictionEnabled && Assess().IsRestricted;

    internal static bool Check(string feature, bool showHint = true)
    {
        if (!RegionRestrictionEnabled) return true;
        var assessment = Assess();
        if (!assessment.IsRestricted) return true;

        ModBase.Log($"[RegionPolicy] 已阻止功能 {feature}，判定原因：{assessment.Reason}");
        if (showHint)
        {
            HintService.Hint(Lang.Text(assessment.Classification == RegionClassification.Unknown
                ? "Region.Feature.Unknown"
                : "Region.Feature.Restricted"), HintType.Error);
        }
        return false;
    }

    internal static void ThrowIfRestricted(string feature)
    {
        if (Check(feature, false)) return;
        throw new ModBase.CancelledException();
    }
}
