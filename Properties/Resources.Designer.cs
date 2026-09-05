#nullable enable
using System.Globalization;
using System.Resources;

namespace KizaBit.Properties;

/// <summary>
/// 多言語リソースへのアクセスを提供するクラス
/// </summary>
public static class Resources
{
    private static readonly ResourceManager resourceMan =
        new ResourceManager("KizaBit.Properties.Resources", typeof(Resources).Assembly);

    /// <summary>
    /// 現在のスレッドの CurrentUICulture をオーバーライドする場合に使用するカルチャ
    /// </summary>
    public static CultureInfo? Culture { get; set; }

    /// <summary>
    /// 指定されたキーに対応するリソース文字列を取得します。
    /// </summary>
    public static string GetString(string name)
    {
        return resourceMan.GetString(name, Culture ?? CultureInfo.CurrentUICulture) ?? name;
    }

    /// <summary>ウィンドウタイトル</summary>
    public static string WindowTitle => GetString("WindowTitle");

    /// <summary>アプリケーションタイトル</summary>
    public static string AppTitle => GetString("AppTitle");

    /// <summary>サブタイトル / アーキテクチャ説明</summary>
    public static string AppSubtitle => GetString("AppSubtitle");

    /// <summary>リセットボタン</summary>
    public static string ResetButton => GetString("ResetButton");

    /// <summary>デモ読み込みボタン</summary>
    public static string DemoButton => GetString("DemoButton");

    /// <summary>ステップボタン</summary>
    public static string StepButton => GetString("StepButton");

    /// <summary>実行ボタン</summary>
    public static string RunButton => GetString("RunButton");

    /// <summary>システムステータス見出し</summary>
    public static string SystemStatus => GetString("SystemStatus");

    /// <summary>キーボード見出し</summary>
    public static string Keyboard => GetString("Keyboard");

    /// <summary>キーボード説明文1</summary>
    public static string KeyboardHelp1 => GetString("KeyboardHelp1");

    /// <summary>キーボード説明文2</summary>
    public static string KeyboardHelp2 => GetString("KeyboardHelp2");
}
