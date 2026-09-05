using System.Globalization;
using System.Threading;
using System.Windows;

namespace KizaBit;

/// <summary>
/// アプリケーションのエントリポイントクラス
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// アプリケーション起動時の初期化処理
    /// 多言語（日本語・英語）設定およびメインウィンドウの起動を行います。
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Windowsの言語設定（表示言語）をスレッドのカルチャに適用
        var uiCulture = CultureInfo.CurrentUICulture;
        Thread.CurrentThread.CurrentUICulture = uiCulture;
        CultureInfo.DefaultThreadCurrentUICulture = uiCulture;

        var window = new MainWindow();
        window.Show();
    }
}
