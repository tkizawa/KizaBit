using System.Windows;

namespace KizaBit.Settings;

/// <summary>
/// アプリケーションの設定情報を保持するモデルクラス
/// </summary>
public class AppSettings
{
    /// <summary>
    /// ウィンドウの左位置 (Left)
    /// </summary>
    public double? WindowLeft { get; set; }

    /// <summary>
    /// ウィンドウの上位置 (Top)
    /// </summary>
    public double? WindowTop { get; set; }

    /// <summary>
    /// ウィンドウの幅 (Width)
    /// </summary>
    public double? WindowWidth { get; set; }

    /// <summary>
    /// ウィンドウの高さ (Height)
    /// </summary>
    public double? WindowHeight { get; set; }

    /// <summary>
    /// ウィンドウの状態 (通常 / 最大化)
    /// </summary>
    public WindowState WindowState { get; set; } = WindowState.Normal;

    /// <summary>
    /// 最後に実行したコマンド履歴などの補足情報（日本語文字列テスト用含む）
    /// </summary>
    public string Note { get; set; } = "設定ファイル";
}
