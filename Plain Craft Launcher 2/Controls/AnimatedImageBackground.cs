using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;

namespace PCL;

/// <summary>
///     动图背景（GIF / 动画 WebP）的帧解码与循环播放。<br />
///     帧数不足 2 帧、或解码失败时 <see cref="TryPlay" /> 返回 <c>false</c>，调用方应继续使用原有的静态图片逻辑。
/// </summary>
public sealed class AnimatedImageBackground
{
    /// <summary>
    ///     帧延迟元数据缺失或过短时使用的默认帧间隔，约 10 FPS。
    /// </summary>
    private const int DefaultFrameDelayMs = 100;

    /// <summary>
    ///     低于该值的帧延迟（2/100 秒）会导致极高占用，按默认值处理。
    /// </summary>
    private const int MinFrameDelayMs = 20;

    /// <summary>
    ///     各格式的帧延迟元数据查询路径。GIF 位于 /grctlext/Delay（1/100 秒），动画 WebP 位于 /ANMF 下（毫秒）。
    /// </summary>
    private static readonly string[] DelayQueryPaths =
    [
        "/grctlext/Delay",
        "/ANMF/FrameDuration",
        "/ANMF/Duration",
        "/Graphics/FrameDelay",
        "/FrameDelay",
        "/Delay"
    ];

    private readonly List<int> _frameDelaysMs = [];
    private readonly List<BitmapSource> _frames = [];

    private ConfigObserver? _autoPauseObserver;
    private ImageBrush? _brush;
    private BitmapDecoder? _decoder; // 持有解码器，避免帧在解码器被回收后失效
    private int _frameIndex;
    private bool _isStopped;
    private MemoryStream? _stream; // 配合 BitmapCacheOption.OnLoad，保证帧数据始终可读
    private Canvas? _target;
    private DispatcherTimer? _timer;
    private Window? _window;

    private AnimatedImageBackground()
    {
    }

    /// <summary>
    ///     当前正在播放的动图背景；没有播放时为 <c>null</c>。
    /// </summary>
    public static AnimatedImageBackground? Current { get; private set; }

    /// <summary>
    ///     第一帧，用于在计时器启动前立即显示画面。
    /// </summary>
    public ImageSource FirstFrame => _frames[0];

    /// <summary>
    ///     总帧数。
    /// </summary>
    public int FrameCount => _frames.Count;

    /// <summary>
    ///     尝试把指定文件作为动图背景播放到指定画布上。<br />
    ///     返回 <c>false</c> 表示该文件不是动图（帧数不足 2 帧）或无法播放，调用方应改用静态图片逻辑。
    /// </summary>
    public static bool TryPlay(string address, Canvas target)
    {
        var animated = TryLoad(address);
        if (animated is null)
            return false;
        try
        {
            animated.Play(target);
            return true;
        }
        catch (Exception ex)
        {
            animated.Stop();
            ModBase.Log(ex, $"[UI] 动图背景播放失败，将按静态图片处理（{address}）", ModBase.LogLevel.Developer);
            return false;
        }
    }

    /// <summary>
    ///     停止当前正在播放的动图背景（若有）。
    /// </summary>
    public static void StopCurrent()
    {
        Current?.Stop();
    }

    /// <summary>
    ///     尝试把指定文件解码为动图。文件不是动图（帧数不足 2 帧）或解码失败时返回 <c>null</c>。
    /// </summary>
    private static AnimatedImageBackground? TryLoad(string address)
    {
        MemoryStream? stream = null;
        try
        {
            stream = new MemoryStream();
            // 与 MyBitmap 一致：以 FileShare.ReadWrite 打开，避免占用用户的图片文件
            using (var file = new FileStream(address, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                file.CopyTo(stream);
            }

            stream.Position = 0;
            var header = new byte[4];
            var headerLength = stream.Read(header, 0, header.Length);
            stream.Position = 0;
            var isGif = headerLength == 4 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 &&
                        header[3] == 0x38; // "GIF8"

            // GIF 走专用解码器，其余格式（动画 WebP 等）交给 WIC 自动选择
            BitmapDecoder decoder = isGif
                ? new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad)
                : BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count <= 1)
                return null; // 静态图片，交回原有逻辑

            var result = new AnimatedImageBackground
            {
                _decoder = decoder,
                _stream = stream
            };
            stream = null; // 所有权已交给实例，由 Stop 释放
            foreach (var frame in decoder.Frames)
            {
                result._frames.Add(frame);
                result._frameDelaysMs.Add(ReadFrameDelayMs(frame));
            }

            ModBase.Log($"[UI] 已解码动图背景，共 {result._frames.Count} 帧：{address}");
            return result;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"[UI] 动图背景解码失败，将按静态图片处理（{address}）", ModBase.LogLevel.Developer);
            return null;
        }
        finally
        {
            stream?.Dispose();
        }
    }

    /// <summary>
    ///     读取单帧的显示时长（毫秒），无法读取时返回默认值。
    /// </summary>
    private static int ReadFrameDelayMs(BitmapFrame frame)
    {
        if (frame.Metadata is not BitmapMetadata metadata)
            return DefaultFrameDelayMs;
        foreach (var path in DelayQueryPaths)
        {
            try
            {
                if (!metadata.ContainsQuery(path))
                    continue;
                if (metadata.GetQuery(path) is not { } value)
                    continue;
                if (!int.TryParse(value.ToString(), out var raw) || raw <= 0)
                    continue;
                // GIF 的延迟单位为 1/100 秒，其余格式（动画 WebP）为毫秒
                var delayMs = path == "/grctlext/Delay" ? raw * 10 : raw;
                return delayMs < MinFrameDelayMs ? DefaultFrameDelayMs : delayMs;
            }
            catch
            {
                // 该格式不支持该查询路径，继续尝试下一个
            }
        }

        return DefaultFrameDelayMs;
    }

    /// <summary>
    ///     在指定画布上开始循环播放。会先停止此前正在播放的动图背景。
    /// </summary>
    private void Play(Canvas target)
    {
        StopCurrent();

        if (_decoder is null || _frames.Count <= 1)
            throw new InvalidOperationException("动图背景尚未完成解码。");

        Current = this;
        _target = target;
        _frameIndex = 0;
        // 与原静态图片逻辑相同：使用默认属性的 ImageBrush，内容裁剪等仍由既有逻辑调整
        _brush = new ImageBrush(FirstFrame);
        target.Background = _brush;

        _timer = new DispatcherTimer(DispatcherPriority.Render, target.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(_frameDelaysMs[0])
        };
        _timer.Tick += TimerTick;

        // 最小化、失去焦点、游戏状态都会影响播放
        _window = Window.GetWindow(target);
        if (_window is not null)
        {
            _window.Activated += WindowStateRefresh;
            _window.Deactivated += WindowStateRefresh;
            _window.StateChanged += WindowStateRefresh;
            _window.Closed += WindowClosed;
        }

        target.IsVisibleChanged += TargetVisibilityChanged;
        ModVideoBack.GamingStateChanged += VideoBackStateChanged;
        ModVideoBack.ForcePlayChanged += VideoBackStateChanged;
        _autoPauseObserver = new ConfigObserver(ConfigEvent.Changed, _ => RefreshPlayState());
        Config.Preference.Background.AutoPauseVideoConfig.Observe(_autoPauseObserver);

        RefreshPlayState();
        ModBase.Log($"[UI] 已开始动图背景播放（共 {FrameCount} 帧）");
    }

    /// <summary>
    ///     停止播放，并释放计时器与所有事件订阅。
    /// </summary>
    private void Stop()
    {
        if (_isStopped)
            return;
        _isStopped = true;

        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= TimerTick;
            _timer = null;
        }

        if (_window is not null)
        {
            _window.Activated -= WindowStateRefresh;
            _window.Deactivated -= WindowStateRefresh;
            _window.StateChanged -= WindowStateRefresh;
            _window.Closed -= WindowClosed;
            _window = null;
        }

        if (_target is not null)
        {
            _target.IsVisibleChanged -= TargetVisibilityChanged;
            _target = null;
        }

        ModVideoBack.GamingStateChanged -= VideoBackStateChanged;
        ModVideoBack.ForcePlayChanged -= VideoBackStateChanged;

        if (_autoPauseObserver is not null)
        {
            Config.Preference.Background.AutoPauseVideoConfig.Unobserve(_autoPauseObserver);
            _autoPauseObserver = null;
        }

        _brush = null;
        _decoder = null;
        _stream?.Dispose();
        _stream = null;

        if (ReferenceEquals(Current, this))
            Current = null;
        ModBase.Log("[UI] 已停止动图背景播放");
    }

    /// <summary>
    ///     按当前窗口、焦点与游戏状态决定是否继续播放。可在任意线程调用。
    /// </summary>
    private void RefreshPlayState()
    {
        var dispatcher = _target?.Dispatcher;
        if (dispatcher is null)
            return;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(RefreshPlayState);
            return;
        }

        if (_isStopped || _timer is null)
            return;

        if (ShouldPause())
            _timer.Stop();
        else if (!_timer.IsEnabled)
            _timer.Start();
    }

    /// <summary>
    ///     是否应当暂停播放。
    /// </summary>
    private bool ShouldPause()
    {
        // 关闭「自动暂停」后便一直播放
        if (!Config.Preference.Background.AutoPauseVideo)
            return false;
        if (_target is null)
            return false;
        if (!_target.IsVisible)
            return true; // 背景已被隐藏
        if (_window is null)
            return false;
        if (_window.WindowState == WindowState.Minimized)
            return true;
        if (!_window.IsActive)
            return true; // 窗口失去焦点
        return ModVideoBack.IsGaming && !ModVideoBack.ForcePlay; // 游戏启动后暂停
    }

    private void TimerTick(object? sender, EventArgs e)
    {
        if (_brush is null || _frames.Count == 0 || _timer is null)
            return;

        // 背景已被换成别的画刷（例如刷新为静态图片）时自行退出，避免计时器空转
        if (_target?.Background is not ImageBrush current || !ReferenceEquals(current, _brush))
        {
            Stop();
            return;
        }

        _frameIndex = (_frameIndex + 1) % _frames.Count;
        _brush.ImageSource = _frames[_frameIndex];

        var delay = _frameDelaysMs[_frameIndex];
        if (Math.Abs(_timer.Interval.TotalMilliseconds - delay) > 0.5d)
            _timer.Interval = TimeSpan.FromMilliseconds(delay);

        if (ShouldPause())
            _timer.Stop();
    }

    private void WindowStateRefresh(object? sender, EventArgs e)
    {
        RefreshPlayState();
    }

    private void TargetVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        RefreshPlayState();
    }

    private void VideoBackStateChanged(object? sender, ModVideoBack.BooleanEventArgs e)
    {
        RefreshPlayState();
    }

    private void WindowClosed(object? sender, EventArgs e)
    {
        Stop();
    }
}