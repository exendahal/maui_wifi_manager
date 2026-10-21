using MauiWifiManager.Abstractions;

namespace DemoApp;

/// <summary>
/// Bar chart of networks per channel for one band. Tap (or hover on Windows) a bar to select it.
/// </summary>
public sealed class ChannelChart : IDrawable
{
    private const float AxisWidth = 24;   // room for y tick labels
    private const float LabelHeight = 18; // room for channel labels under the baseline
    private const float TopPadding = 16;  // room for the value label above the tallest bar
    private const float MaxBarWidth = 24;
    private const float CornerRadius = 4;

    private readonly List<RectF> _Slots = new();

    public ChannelChart(WifiFrequencyBand band, IEnumerable<WifiChannelUsage> usage)
    {
        var byChannel = usage.ToDictionary(u => u.ChannelNumber);
        // Show every 2.4 GHz channel so free ones are visible; other bands have too many, so only used channels.
        var channels = band == WifiFrequencyBand.Band2_4GHz
            ? Enumerable.Range(1, 13).Union(byChannel.Keys).OrderBy(c => c)
            : byChannel.Keys.OrderBy(c => c);
        Bars = channels
            .Select(c => byChannel.TryGetValue(c, out var u) ? u : new WifiChannelUsage { FrequencyBand = band, ChannelNumber = c })
            .ToList();
    }

    public List<WifiChannelUsage> Bars { get; }

    public int? SelectedIndex { get; set; }

    /// <summary>Index of the bar whose slot contains the point; the whole slot is the hit target, not just the bar.</summary>
    public int? HitTest(PointF point)
    {
        for (int i = 0; i < _Slots.Count; i++)
            if (point.X >= _Slots[i].Left && point.X < _Slots[i].Right)
                return i;
        return null;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        _Slots.Clear();
        if (Bars.Count == 0) return;

        var accent = ThemeColor.Get("Accent");
        var muted = ThemeColor.Get("TextMuted");
        var text = ThemeColor.Get("Text");
        var grid = ThemeColor.Get("Border");

        var plot = new RectF(AxisWidth, TopPadding, dirtyRect.Width - AxisWidth, dirtyRect.Height - TopPadding - LabelHeight);
        int max = Math.Max(1, Bars.Max(b => b.NetworkCount));
        int step = max <= 4 ? 1 : (int)Math.Ceiling(max / 4.0);
        int top = (int)Math.Ceiling(max / (double)step) * step;
        float Y(int value) => plot.Bottom - plot.Height * value / top;

        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontSize = 11;

        // Recessive horizontal grid with integer ticks.
        canvas.StrokeSize = 1;
        for (int v = step; v <= top; v += step)
        {
            canvas.StrokeColor = grid;
            canvas.DrawLine(plot.Left, Y(v), plot.Right, Y(v));
            canvas.FontColor = muted;
            canvas.DrawString(v.ToString(), 0, Y(v) - 7, AxisWidth - 6, 14, HorizontalAlignment.Right, VerticalAlignment.Center);
        }

        float slotWidth = plot.Width / Bars.Count;
        float barWidth = Math.Min(MaxBarWidth, slotWidth - 2); // keeps at least a 2px gap between bars
        int labelEvery = slotWidth >= 20 ? 1 : (int)Math.Ceiling(20 / slotWidth);
        int maxIndex = Bars.FindIndex(b => b.NetworkCount == max);

        for (int i = 0; i < Bars.Count; i++)
        {
            var bar = Bars[i];
            var slot = new RectF(plot.Left + i * slotWidth, plot.Top, slotWidth, plot.Height + LabelHeight);
            _Slots.Add(slot);
            float x = slot.Center.X - barWidth / 2;

            if (bar.NetworkCount > 0)
            {
                bool dimmed = SelectedIndex != null && SelectedIndex != i;
                canvas.FillColor = dimmed ? accent.WithAlpha(0.35f) : accent;
                float h = plot.Bottom - Y(bar.NetworkCount);
                // Rounded data end, square base anchored to the baseline.
                canvas.FillRoundedRectangle(x, Y(bar.NetworkCount), barWidth, h, CornerRadius, CornerRadius, 0, 0);
            }

            // Value label only on the busiest channel and the selected one.
            if (bar.NetworkCount > 0 && (i == maxIndex || i == SelectedIndex))
            {
                canvas.FontColor = text;
                canvas.DrawString(bar.NetworkCount.ToString(), slot.Left, Y(bar.NetworkCount) - 15, slotWidth, 14, HorizontalAlignment.Center, VerticalAlignment.Center);
            }

            if (i % labelEvery == 0 || i == SelectedIndex)
            {
                canvas.FontColor = i == SelectedIndex ? text : muted;
                canvas.DrawString(bar.ChannelNumber.ToString(), slot.Left, plot.Bottom + 2, slotWidth, LabelHeight - 2, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }

        canvas.StrokeColor = muted;
        canvas.DrawLine(plot.Left, plot.Bottom, plot.Right, plot.Bottom);
    }
}
