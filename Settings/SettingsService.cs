using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;

namespace KizaBit.Settings;

/// <summary>
/// アプリケーション設定の読み込み、保存、およびウィンドウ位置補正を提供するサービス
/// </summary>
public static class SettingsService
{
    private const string AppFolderName = "KizaBit";
    private const string SettingsFileName = "settings.json";

    /// <summary>
    /// 設定ファイルのフルパス (例: C:\Users\<User>\AppData\Local\KizaBit\settings.json)
    /// </summary>
    public static string SettingsFilePath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, AppFolderName, SettingsFileName);
        }
    }

    /// <summary>
    /// JSON シリアライザのオプション
    /// 日本語を Unicode エスケープせず可視テキストとして UTF-8 保存します。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 設定ファイルを読み込みます。ファイルが存在しない場合は既定値を返します。
    /// </summary>
    public static AppSettings LoadSettings()
    {
        try
        {
            var path = SettingsFilePath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            // 読み込み失敗時は既定設定を使用
            System.Diagnostics.Debug.WriteLine($"設定読み込みエラー: {ex.Message}");
        }

        return new AppSettings();
    }

    /// <summary>
    /// 設定ファイルを保存します。保存先フォルダが存在しない場合は自動作成します。
    /// </summary>
    public static void SaveSettings(AppSettings settings)
    {
        try
        {
            var path = SettingsFilePath;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"設定保存エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// 保存された設定値をもとに、ウィンドウの位置およびサイズを復元します。
    /// 画面外にはみ出している場合は、画面内に収まるよう補正します。
    /// </summary>
    public static void ApplyWindowPlacement(Window window, AppSettings settings)
    {
        if (settings.WindowWidth.HasValue && settings.WindowHeight.HasValue)
        {
            window.Width = Math.Max(settings.WindowWidth.Value, window.MinWidth > 0 ? window.MinWidth : 640);
            window.Height = Math.Max(settings.WindowHeight.Value, window.MinHeight > 0 ? window.MinHeight : 480);
        }

        if (settings.WindowLeft.HasValue && settings.WindowTop.HasValue)
        {
            var left = settings.WindowLeft.Value;
            var top = settings.WindowTop.Value;

            // 仮想画面の範囲を取得（マルチモニタ対応）
            var virtualLeft = SystemParameters.VirtualScreenLeft;
            var virtualTop = SystemParameters.VirtualScreenTop;
            var virtualWidth = SystemParameters.VirtualScreenWidth;
            var virtualHeight = SystemParameters.VirtualScreenHeight;

            // ウィンドウの一部が仮想画面領域内に収まっているか確認
            var isVisible = left + window.Width > virtualLeft + 50 &&
                            left < virtualLeft + virtualWidth - 50 &&
                            top + window.Height > virtualTop + 50 &&
                            top < virtualTop + virtualHeight - 50;

            if (isVisible)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = left;
                window.Top = top;
            }
            else
            {
                // 画面外の場合は画面中央に配置
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        // 最小化状態で終了した場合は通常状態で起動
        window.WindowState = settings.WindowState == WindowState.Minimized
            ? WindowState.Normal
            : settings.WindowState;
    }

    /// <summary>
    /// 現在のウィンドウ位置およびサイズを設定オブジェクトに記録します。
    /// </summary>
    public static void RecordWindowPlacement(Window window, AppSettings settings)
    {
        // 最小化・最大化前の通常時の位置・サイズを取得
        var bounds = window.RestoreBounds;

        if (window.WindowState == WindowState.Normal)
        {
            settings.WindowLeft = window.Left;
            settings.WindowTop = window.Top;
            settings.WindowWidth = window.ActualWidth;
            settings.WindowHeight = window.ActualHeight;
        }
        else
        {
            // 最大化または最小化されている場合は RestoreBounds を記録
            if (!bounds.IsEmpty)
            {
                settings.WindowLeft = bounds.Left;
                settings.WindowTop = bounds.Top;
                settings.WindowWidth = bounds.Width;
                settings.WindowHeight = bounds.Height;
            }
        }

        // 最小化されていた場合は次回通常で起動するよう記録
        settings.WindowState = window.WindowState == WindowState.Minimized
            ? WindowState.Normal
            : window.WindowState;
    }
}
