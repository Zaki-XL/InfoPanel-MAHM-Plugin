# InfoPanel.MAHM プラグイン アーキテクチャ設計書

本ドキュメントは、InfoPanel 1.4+ 向け **MSI Afterburner 共有メモリ連携プラグイン (`InfoPanel.MAHM`)** のシステム設計、データフロー、およびコンテナ・センチネル処理仕様を解説します。

---

## 1. 全体データフロー

```text
┌────────────────────────┐
│  Hardware Sensors      │ (GPU, CPU, VRAM, Fans, Sensors)
└───────────┬────────────┘
            │ 監視・制御 (ハードウェアアクセス)
┌───────────▼────────────┐
│    MSI Afterburner     │
└───────────┬────────────┘
            │ 集計結果を書き込み
┌───────────▼────────────┐
│  MAHMSharedMemory      │ ◄── Windows Memory-Mapped File (共有メモリ)
└───────────▲────────────┘
            │ 安全に読み取り (リードオンリー)
┌───────────┴────────────┐
│ InfoPanel.MAHM Plugin  │ ◄── 独立プロセス (InfoPanel.Plugins.Host.exe)
│ - MahmBufferParser     │     (FLT_MAX センチネル正規化)
│ - DynamicMetricPair    │     (階層コンテナ振り分け & フレンドリーネーム)
└───────────┬────────────┘
            │ Named Pipe (StreamJsonRpc)
┌───────────▼────────────┐
│     InfoPanel 本体     │ ◄── 絶対にクラッシュしない！
│  (USB液晶 / 画面描画)  │
└────────────────────────┘
```

---

## 2. 共有メモリ仕様とセンチネル正規化

### ヘッダー構造 (`MAHM_SHARED_MEMORY_HEADER`)
* `DWORD dwSignature`: `'MAHM'` (0x4D41484D)
* `DWORD dwVersion`: バージョン (0x00020000 等)
* `DWORD dwHeaderSize`: ヘッダーサイズ (sizeof)
* `DWORD dwNumEntries`: 有効なセンサーエントリ数
* `DWORD dwEntrySize`: 1エントリあたりのサイズ

### エントリ構造 (`MAHM_SHARED_MEMORY_ENTRY`)
* `char szSrcName[260]`: センサー識別名 (例: `"GPU usage"`, `"Memory usage"`, `"FB usage"`, `"Framerate"`)
* `char szSrcUnits[260]`: 単位 (例: `"%"`, `"MB"`, `"°C"`, `"FPS"`)
* `float data`: リアルタイム現在値
* `DWORD dwGpu`: 対象 GPU インデックス

### センチネル値（`FLT_MAX`）の正規化
- Afterburner / RTSS は、ゲーム未起動時や未計測センサーに対して `FLT_MAX` (`3.4028235E+38`) を格納します。
- `MahmBufferParser` は、`Math.Abs(entry.data) >= 3.4e38f` または非数を検知した時点で `float.NaN` に正規化します。
- これにより、未計測時の天文学的数値表示を完全に防ぎ、`SensorFormatter` で綺麗に `"N/A"` 表示します。

---

## 3. コンテナ設計（案1: スッキリ階層型）

プレフィックスの不要な重複を排除し、ユーザーが迷わず直感的に配置できる 6 つのコンテナを採用しています：

1. **`GPU` (`gpu-metrics`)**:
   - `GPU Status` (Connected / Disconnected)
   - `GPU usage`, `GPU temperature`, `Memory usage` (MB)
   - **`FB usage (VRAM Usage)`** (%) : ビデオメモリ利用率
   - `Fan speed`, `GPU clock`, `Memory clock`, `Power`
2. **`GPU - Advanced & Limits` (`gpu-advanced-metrics`)**:
   - `VID usage`, `BUS usage`, `Temp limit`, `Power limit`, `Voltage limit`
3. **`CPU` (`cpu-metrics`)**:
   - `CPU Status`
   - `CPU usage`, `CPU temperature`, `CPU clock`, `CPU power`
4. **`CPU - Cores` (`cpu-cores-metrics`)**:
   - `CPU1 usage`, `CPU1 temperature`, `CPU1 clock` ...
5. **`Memory` (`memory-metrics`)**:
   - `RAM usage`, `Commit charge`
6. **`Gaming (RTSS)` (`gaming-metrics`)**:
   - `Gaming Status` (Active / Idle (No Game Detected))
   - `Framerate`, `Frametime`, `Framerate Min`, `Framerate Avg`, `Framerate Max`
   - アイドル時は自動的に `N/A` にフォールバック。

---

## 4. ポーリング周期 (HwPollPeriod) 自動検知アーキテクチャ

`AfterburnerConfigService` は起動時に以下の順序で Afterburner の計測周期を検出し、プラグインの `UpdateInterval` に自動同期します：

```text
1. レジストリ照会: HKLM\SOFTWARE\WOW6432Node\MSI\Afterburner -> InstallDir
2. 設定ファイル探索: Profiles\MSIAfterburner.cfg
3. キー抽出: [Settings] セクション内の HwPollPeriod=(\d+) (ミリ秒単位)
4. 安全クランプ: 100ms <= 値 <= 10000ms の範囲内か検証
5. フォールバック: ファイル不在や異常値時は安全に 1000ms (1秒) を返却
```

これにより、ユーザーが Afterburner 側で高速モニタリング（例: 250ms/500ms）を設定している環境では自動で高頻度更新となり、低頻度（例: 2000ms）設定では無駄な CPU 負荷を抑える完全な自律同期を実現しています。

---

## 5. 品質保証と自動テスト戦略

- **単体テスト (`SensorFormatterTests`, `MahmBufferParserTests`, `AfterburnerConfigServiceTests`)**:
  正常系フォーマット、ゼロ除算防止、センチネル変換、破損バッファ耐性、HwPollPeriod パースと安全クランプを検証。
- **異常系・境界テスト (`BoundaryAndExceptionTests`)**:
  例外発生時のステータスハンドリング、動的欠損対応、20スレッド同時実行のスレッドセーフティを検証。
- **ロジックテスト (`PluginLogicTests`)**:
  案1の階層分類、`FB usage (VRAM Usage)` の GPU メイン配置、動的 `UpdateInterval` 同期を検証。
- **実機結合テスト (`RealAfterburnerIntegrationTests`)**:
  稼働中の Afterburner から実データを取得し、実機 CFG からの 1000ms 取得、ゲーム非稼働時の FPS `N/A` 判定、全コンテナ生成を検証。
