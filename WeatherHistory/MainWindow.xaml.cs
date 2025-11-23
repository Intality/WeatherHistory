using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WeatherHistory
{
    public partial class MainWindow : Window
    {
        // Default: Loveland, CO
        private const double LAT = 40.386642;
        private const double LON = -105.084520;

        private static readonly DateTime ERA5_MIN = new DateTime(1940, 1, 1);

        // Animation state so we can stop/clear between plays
        private readonly List<DispatcherTimer> activeTimers = new();
        private readonly List<Storyboard> activeStoryboards = new();
        private readonly Random RNG = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        // ============================================================
        // MENU EVENTS
        // ============================================================
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void VersionHistory_Click(object sender, RoutedEventArgs e)
        {
            ShowVersionHistoryDialog();
        }

        // ============================================================
        // DEV TOOLS — FORCE WEATHER (ANIMATED)
        // ============================================================
        private void DevForceClear_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Dev: Forced Clear";
            DetailsText.Text = "Code: 0 (Clear)";
            PlayCartoonAnimation(0);
        }

        private void DevForceCloud_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Dev: Forced Cloudy";
            DetailsText.Text = "Code: 3 (Overcast)";
            PlayCartoonAnimation(3);
        }

        private void DevForceRain_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Dev: Forced Rain";
            DetailsText.Text = "Code: 61 (Rain)";
            PlayCartoonAnimation(61);
        }

        private void DevForceSnow_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Dev: Forced Snow";
            DetailsText.Text = "Code: 75 (Snow)";
            PlayCartoonAnimation(75);
        }

        private void DevForceStorm_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Dev: Forced Thunderstorm";
            DetailsText.Text = "Code: 95 (Thunderstorm)";
            PlayCartoonAnimation(95);
        }

        // ============================================================
        // FETCH BUTTON
        // ============================================================
        private async void FetchWeather_Click(object sender, RoutedEventArgs e)
        {
            DateTime? picked = DateBox.SelectedDate;

            if (picked == null)
            {
                StatusText.Text = "Select a date first.";
                return;
            }

            DateTime date = picked.Value.Date;

            // ---- 1. Future check (Easter egg) ----
            if (date > DateTime.Now.Date)
            {
                StatusText.Text = "Are you a time traveler? This date hasn't happened yet!";
                DetailsText.Text = "";
                ShowErrorAnimation();
                return;
            }

            // ---- 2. ERA5 minimum check ----
            if (date < ERA5_MIN)
            {
                StatusText.Text = "Please select a date AFTER Dec 31, 1939.\nERA5 begins in 1940.";
                DetailsText.Text = "";
                ShowErrorAnimation();
                return;
            }

            FetchButton.IsEnabled = false;
            StatusText.Text = $"Fetching {date:yyyy-MM-dd}…";
            DetailsText.Text = "";
            ShowLoadingAnimation();

            try
            {
                DayData? result = await WeatherServiceFetchAsync(date, LAT, LON);

                if (result == null)
                {
                    StatusText.Text = "API Error";
                    DetailsText.Text = "";
                    ShowErrorAnimation();
                    return;
                }

                if (result.isError)
                {
                    StatusText.Text = result.message;
                    DetailsText.Text = "";
                    ShowErrorAnimation();
                    return;
                }

                string desc = WeatherCodesDescribe(result.code);

                StatusText.Text = $"Weather on {date:yyyy-MM-dd}: {desc}";
                DetailsText.Text = string.Format(
                    "Code: {0} ({1})\nMax: {2:0.0}°C  Min: {3:0.0}°C\nPrecip: {4:0.00} mm\nSnow: {5:0.00} mm\nWind: {6:0.0} km/h",
                    result.code, desc,
                    result.maxTemp, result.minTemp,
                    result.precip, result.snow, result.wind
                );

                PlayCartoonAnimation(result.code);
            }
            catch (Exception ex)
            {
                StatusText.Text = "API Error";
                DetailsText.Text = ex.Message;
                ShowErrorAnimation();
            }
            finally
            {
                FetchButton.IsEnabled = true;
            }
        }

        // ============================================================
        // WEATHER SERVICE
        // ============================================================
        private class DayData
        {
            public int code;
            public double maxTemp;
            public double minTemp;
            public double precip;
            public double snow;
            public double wind;

            public bool isError;
            public string message = "";
        }

        private async Task<DayData?> WeatherServiceFetchAsync(DateTime date, double lat, double lon)
        {
            DayData d = new DayData();

            try
            {
                string day = date.ToString("yyyy-MM-dd");
                string url =
                    "https://archive-api.open-meteo.com/v1/era5" +
                    $"?latitude={lat:0.000000}&longitude={lon:0.000000}" +
                    $"&start_date={day}&end_date={day}" +
                    "&daily=weather_code,temperature_2m_max,temperature_2m_min," +
                    "precipitation_sum,snowfall_sum,wind_speed_10m_max&timezone=auto";

                using HttpClient client = new HttpClient();
                string json = await client.GetStringAsync(url);

                using JsonDocument doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("daily", out JsonElement daily))
                {
                    d.isError = true;
                    d.message = "Weather unavailable for this date.";
                    return d;
                }

                // Extract first values from daily arrays
                d.code = GetFirstInt(daily, "weather_code");
                d.maxTemp = GetFirstDouble(daily, "temperature_2m_max");
                d.minTemp = GetFirstDouble(daily, "temperature_2m_min");
                d.precip = GetFirstDouble(daily, "precipitation_sum");
                d.snow = GetFirstDouble(daily, "snowfall_sum");
                d.wind = GetFirstDouble(daily, "wind_speed_10m_max");

                // If arrays are empty or missing, treat as no-data day
                if (IsAnyArrayEmpty(daily))
                {
                    d.isError = true;
                    d.message = "Weather unavailable for this date.";
                }

                return d;
            }
            catch (Exception e)
            {
                d.isError = true;
                d.message = "API Error: " + e.Message;
                return d;
            }
        }

        private bool IsAnyArrayEmpty(JsonElement daily)
        {
            foreach (var prop in daily.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array &&
                    prop.Value.GetArrayLength() == 0)
                    return true;
            }
            return false;
        }

        private int GetFirstInt(JsonElement obj, string name)
        {
            if (obj.TryGetProperty(name, out JsonElement arr) &&
                arr.ValueKind == JsonValueKind.Array &&
                arr.GetArrayLength() > 0)
            {
                return arr[0].GetInt32();
            }
            return 0;
        }

        private double GetFirstDouble(JsonElement obj, string name)
        {
            if (obj.TryGetProperty(name, out JsonElement arr) &&
                arr.ValueKind == JsonValueKind.Array &&
                arr.GetArrayLength() > 0)
            {
                return arr[0].GetDouble();
            }
            return 0.0;
        }

        // ============================================================
        // WEATHER CODES
        // ============================================================
        private string WeatherCodesDescribe(int code)
        {
            return code switch
            {
                0 => "Clear",
                1 => "Mostly Clear",
                2 => "Partly Cloudy",
                3 => "Overcast",

                45 or 48 => "Fog",

                51 or 53 or 55 => "Drizzle",
                61 or 63 or 65 => "Rain",
                80 or 81 or 82 => "Rain Showers",

                71 or 73 or 75 or 77 => "Snow",
                85 or 86 => "Snow Showers",

                56 or 57 or 66 or 67 => "Freezing Rain / Ice",

                95 => "Thunderstorm",
                96 or 99 => "Thunderstorm w/ Hail",

                _ => "Unknown",
            };
        }

        private string WeatherCodesToType(int code)
        {
            if (code == 0 || code == 1 || code == 2) return "SUN";
            if (code == 3 || code == 45 || code == 48) return "CLOUD";
            if ((code >= 51 && code <= 67) || (code >= 80 && code <= 82)) return "RAIN";
            if ((code >= 71 && code <= 77) || code == 85 || code == 86) return "SNOW";
            if (code == 95 || code == 96 || code == 99) return "STORM";
            return "UNKNOWN";
        }

        // ============================================================
        // ANIMATION DISPATCH
        // ============================================================
        private void PlayCartoonAnimation(int code)
        {
            StopAllAnimations();
            AnimationCanvas.Children.Clear();

            string type = WeatherCodesToType(code);

            switch (type)
            {
                case "SUN":
                    SunAnimation();
                    break;
                case "CLOUD":
                    CloudAnimation();
                    break;
                case "RAIN":
                    RainAnimation();
                    break;
                case "SNOW":
                    SnowAnimation();
                    break;
                case "STORM":
                    StormAnimation();
                    break;
                default:
                    UnknownAnimation();
                    break;
            }
        }

        private void StopAllAnimations()
        {
            foreach (var t in activeTimers) t.Stop();
            activeTimers.Clear();

            foreach (var sb in activeStoryboards) sb.Stop();
            activeStoryboards.Clear();
        }

        // ============================================================
        // STATUS SCREENS
        // ============================================================
        private void ShowLoadingAnimation()
        {
            StopAllAnimations();
            AnimationCanvas.Children.Clear();

            TextBlock lbl = new TextBlock
            {
                Text = "Loading…",
                Foreground = Brushes.White,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold
            };
            Canvas.SetLeft(lbl, 180);
            Canvas.SetTop(lbl, 95);
            AnimationCanvas.Children.Add(lbl);

            ScaleTransform scale = new ScaleTransform(1, 1);
            lbl.RenderTransformOrigin = new Point(0.5, 0.5);
            lbl.RenderTransform = scale;

            DoubleAnimation pulse = new DoubleAnimation
            {
                From = 1.0,
                To = 1.15,
                Duration = TimeSpan.FromSeconds(1.3),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            Storyboard sb = new Storyboard();
            sb.Children.Add(pulse);
            Storyboard.SetTarget(pulse, lbl);
            Storyboard.SetTargetProperty(pulse, new PropertyPath("RenderTransform.ScaleX"));

            DoubleAnimation pulseY = pulse.Clone();
            sb.Children.Add(pulseY);
            Storyboard.SetTarget(pulseY, lbl);
            Storyboard.SetTargetProperty(pulseY, new PropertyPath("RenderTransform.ScaleY"));

            activeStoryboards.Add(sb);
            sb.Begin();
        }

        private void ShowErrorAnimation()
        {
            StopAllAnimations();
            AnimationCanvas.Children.Clear();

            TextBlock lbl = new TextBlock
            {
                Text = "API ERROR",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 102, 102)),
                FontSize = 20,
                FontWeight = FontWeights.SemiBold
            };
            Canvas.SetLeft(lbl, 175);
            Canvas.SetTop(lbl, 95);
            AnimationCanvas.Children.Add(lbl);
        }

        private void UnknownAnimation()
        {
            TextBlock lbl = new TextBlock
            {
                Text = "No Data",
                Foreground = Brushes.Gray,
                FontSize = 18
            };
            Canvas.SetLeft(lbl, 200);
            Canvas.SetTop(lbl, 100);
            AnimationCanvas.Children.Add(lbl);
        }

        // ============================================================
        // CARTOON ANIMATIONS
        // ============================================================

        // ☀️ SUN — glow + bounce
        private void SunAnimation()
        {
            Ellipse sun = new Ellipse
            {
                Width = 110,
                Height = 110,
                Fill = new SolidColorBrush(Color.FromRgb(255, 217, 61)),
                Stroke = new SolidColorBrush(Color.FromRgb(255, 179, 0)),
                StrokeThickness = 5
            };
            Canvas.SetLeft(sun, 185);
            Canvas.SetTop(sun, 60);
            AnimationCanvas.Children.Add(sun);

            Ellipse glow = new Ellipse
            {
                Width = 150,
                Height = 150,
                Fill = new SolidColorBrush(Color.FromArgb(60, 255, 224, 102))
            };
            Canvas.SetLeft(glow, 165);
            Canvas.SetTop(glow, 40);
            AnimationCanvas.Children.Add(glow);

            ScaleTransform scale = new ScaleTransform(1, 1);
            sun.RenderTransformOrigin = new Point(0.5, 0.5);
            sun.RenderTransform = scale;

            DoubleAnimation bounce = new DoubleAnimation
            {
                From = 1.0,
                To = 1.1,
                Duration = TimeSpan.FromSeconds(2),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            Storyboard sb = new Storyboard();
            sb.Children.Add(bounce);
            Storyboard.SetTarget(bounce, sun);
            Storyboard.SetTargetProperty(bounce, new PropertyPath("RenderTransform.ScaleX"));

            DoubleAnimation bounceY = bounce.Clone();
            sb.Children.Add(bounceY);
            Storyboard.SetTarget(bounceY, sun);
            Storyboard.SetTargetProperty(bounceY, new PropertyPath("RenderTransform.ScaleY"));

            activeStoryboards.Add(sb);
            sb.Begin();
        }

        // ☁️ CLOUDS — fluffy circles + drift
        private void CloudAnimation()
        {
            for (int i = 0; i < 3; i++)
            {
                Canvas cloud = CreateFluffyCloud();

                double baseX = 90 + i * 140;
                double baseY = 70 + RNG.Next(0, 25);

                Canvas.SetLeft(cloud, baseX);
                Canvas.SetTop(cloud, baseY);
                AnimationCanvas.Children.Add(cloud);

                TranslateTransform drift = new TranslateTransform();
                cloud.RenderTransform = drift;

                DoubleAnimation driftAnim = new DoubleAnimation
                {
                    From = -25,
                    To = 25,
                    Duration = TimeSpan.FromSeconds(7 + i * 2),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };

                Storyboard sb = new Storyboard();
                sb.Children.Add(driftAnim);
                Storyboard.SetTarget(driftAnim, cloud);
                Storyboard.SetTargetProperty(driftAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

                activeStoryboards.Add(sb);
                sb.Begin();
            }
        }

        private Canvas CreateFluffyCloud()
        {
            Canvas c = new Canvas { Width = 140, Height = 80 };

            Ellipse c1 = new Ellipse { Width = 80, Height = 60, Fill = Brushes.White };
            Ellipse c2 = new Ellipse { Width = 64, Height = 50, Fill = new SolidColorBrush(Color.FromRgb(242, 242, 242)) };
            Ellipse c3 = new Ellipse { Width = 56, Height = 45, Fill = Brushes.White };

            Canvas.SetLeft(c1, 30); Canvas.SetTop(c1, 10);
            Canvas.SetLeft(c2, 0); Canvas.SetTop(c2, 18);
            Canvas.SetLeft(c3, 72); Canvas.SetTop(c3, 26);

            c.Children.Add(c1);
            c.Children.Add(c2);
            c.Children.Add(c3);

            return c;
        }

        // 🌧️ RAIN — falling lines
        private void RainAnimation()
        {
            int dropCount = 90;

            for (int i = 0; i < dropCount; i++)
            {
                Line drop = new Line
                {
                    X1 = 0,
                    Y1 = 0,
                    X2 = 0,
                    Y2 = 15,
                    StrokeThickness = 3,
                    Stroke = new SolidColorBrush(Color.FromRgb(115, 194, 251))
                };

                double x = RNG.NextDouble() * 480;
                double y = RNG.NextDouble() * 240;

                Canvas.SetLeft(drop, x);
                Canvas.SetTop(drop, y);
                AnimationCanvas.Children.Add(drop);

                DispatcherTimer timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(24)
                };

                timer.Tick += (_, __) =>
                {
                    double ny = Canvas.GetTop(drop) + 14;
                    if (ny > 240)
                    {
                        ny = -20;
                        Canvas.SetLeft(drop, RNG.NextDouble() * 480);
                    }
                    Canvas.SetTop(drop, ny);
                };

                activeTimers.Add(timer);
                timer.Start();
            }
        }

        // ❄️ SNOW — floating dots + spin
        private void SnowAnimation()
        {
            int flakeCount = 50;

            for (int i = 0; i < flakeCount; i++)
            {
                Ellipse flake = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = Brushes.White,
                    Opacity = 0.9
                };

                double x = RNG.NextDouble() * 480;
                double y = RNG.NextDouble() * -240;

                Canvas.SetLeft(flake, x);
                Canvas.SetTop(flake, y);

                RotateTransform rot = new RotateTransform(0);
                flake.RenderTransformOrigin = new Point(0.5, 0.5);
                flake.RenderTransform = rot;

                AnimationCanvas.Children.Add(flake);

                DoubleAnimation fall = new DoubleAnimation
                {
                    From = -30,
                    To = 260,
                    Duration = TimeSpan.FromSeconds(4 + RNG.NextDouble() * 3),
                    RepeatBehavior = RepeatBehavior.Forever
                };

                DoubleAnimation spin = new DoubleAnimation
                {
                    From = 0,
                    To = 360,
                    Duration = TimeSpan.FromSeconds(2 + RNG.NextDouble()),
                    RepeatBehavior = RepeatBehavior.Forever
                };

                Storyboard sb = new Storyboard();

                sb.Children.Add(fall);
                Storyboard.SetTarget(fall, flake);
                Storyboard.SetTargetProperty(fall, new PropertyPath("(Canvas.Top)"));

                sb.Children.Add(spin);
                Storyboard.SetTarget(spin, flake);
                Storyboard.SetTargetProperty(spin, new PropertyPath("RenderTransform.Angle"));

                activeStoryboards.Add(sb);
                sb.Begin();
            }
        }

        // ⛈️ STORM — heavy rain + lightning flash
        private void StormAnimation()
        {
            // Heavy rain
            int dropCount = 120;

            for (int i = 0; i < dropCount; i++)
            {
                Line drop = new Line
                {
                    X1 = 0,
                    Y1 = 0,
                    X2 = 0,
                    Y2 = 18,
                    StrokeThickness = 3,
                    Stroke = new SolidColorBrush(Color.FromRgb(107, 183, 255))
                };

                double x = RNG.NextDouble() * 480;
                double y = RNG.NextDouble() * 240;

                Canvas.SetLeft(drop, x);
                Canvas.SetTop(drop, y);
                AnimationCanvas.Children.Add(drop);

                DispatcherTimer timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(20)
                };

                timer.Tick += (_, __) =>
                {
                    double ny = Canvas.GetTop(drop) + 16;
                    if (ny > 240) ny = -25;
                    Canvas.SetTop(drop, ny);
                };

                activeTimers.Add(timer);
                timer.Start();
            }

            // Lightning flash overlay
            Rectangle flash = new Rectangle
            {
                Width = 480,
                Height = 240,
                Fill = Brushes.White,
                Opacity = 0
            };
            Canvas.SetLeft(flash, 0);
            Canvas.SetTop(flash, 0);
            AnimationCanvas.Children.Add(flash);

            DispatcherTimer lightningTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.5)
            };

            lightningTimer.Tick += (_, __) =>
            {
                DoubleAnimation on = new DoubleAnimation
                {
                    From = 0,
                    To = 0.85,
                    Duration = TimeSpan.FromSeconds(0.01)
                };
                DoubleAnimation off = new DoubleAnimation
                {
                    From = 0.85,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.08),
                    BeginTime = TimeSpan.FromSeconds(0.02)
                };

                Storyboard sb = new Storyboard();
                sb.Children.Add(on);
                sb.Children.Add(off);

                Storyboard.SetTarget(on, flash);
                Storyboard.SetTargetProperty(on, new PropertyPath("Opacity"));
                Storyboard.SetTarget(off, flash);
                Storyboard.SetTargetProperty(off, new PropertyPath("Opacity"));

                activeStoryboards.Add(sb);
                sb.Begin();
            };

            activeTimers.Add(lightningTimer);
            lightningTimer.Start();
        }

        // ============================================================
        // VERSION HISTORY (C# v3 w/ December dates)
        // ============================================================
        private void ShowVersionHistoryDialog()
        {
            const string CHANGELOG =
@"WeatherApp – Version History

------------------------------------------------------------
v0.1.0   (July 18, 2025)
  • First proof-of-concept
  • Basic JavaFX window + date field
  • Hardcoded sample weather data for testing
  • No animations yet

v0.4.0   (August 2, 2025)
  • Added HTTP support using java.net.http
  • Connected to Open-Meteo (forecast only)
  • Displayed raw JSON output for debugging

v0.7.0   (September 10, 2025)
  • UI cleanup + better layout spacing
  • Added labels for temperature, precipitation, wind
  • Introduced simple “loading” and “error” screens

------------------------------------------------------------
v1.0.0   (October 1, 2025)
  • First stable release
  • Fetches actual daily weather data
  • Added minimal weather animations (sun + clouds)
  • Introduced WeatherCodes mapping

v1.1.0   (October 22, 2025)
  • Reworked weather code system
  • Added rain and snow animations
  • Improved missing-data handling
  • Polished UI theme and borders

------------------------------------------------------------
v1.3.0   (November 4, 2025)
  • Rewrote animation engine for smoother rendering
  • Added background-threaded API calls
  • Fixed UI freezing issues
  • Improved timezone and invalid-date logic

v1.4.0   (November 12, 2025)
  • Added ERA5 historical data support (pre-forecast dates)
  • Added future-date warning (“Are you a time traveler?”)
  • Added pre-ERA5 message (“Choose a date after Dec 31, 1939”)
  • Manual JSON parsing implemented — removed external libs

------------------------------------------------------------
v2.0.0   (November 20, 2025)
  • Complete visual overhaul — cartoon animation style
  • New animations: Sun, Clouds, Rain, Snow, Storm
  • All animations now properly clipped to window
  • Rain/Snow no longer spill beyond animation region
  • Big performance improvement across entire app
  • Version History dialog added

v2.1.0   (November 21, 2025)
  • UI refinement + spacing polish
  • Cleaner weather descriptions + safer default types
  • Better stability on slow or unstable connections
  • Minor layout + text readability adjustments

------------------------------------------------------------
v3.0.0   (December 12, 2025)
  • Major platform rewrite → C# WPF
  • New XAML interface + layout system
  • FoCo skyline background layer added
  • Rebuilt cartoon animations in C#
  • Added Dev Tools (force weather types)
  • Faster loading & better performance

v3.0.1   (December 14, 2025)
  • Improved text contrast + menu readability
  • Fixed animation canvas alignment
  • Minor stability + cleanup patch

------------------------------------------------------------";

            Window dialog = new Window
            {
                Title = "Version History",
                Width = 640,
                Height = 520,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brushes.White
            };

            TextBox area = new TextBox
            {
                Text = CHANGELOG,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Margin = new Thickness(10)
            };

            dialog.Content = area;
            dialog.ShowDialog();
        }
    }
}
