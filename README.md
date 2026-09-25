# InfoPanel MSI Afterburner (MAHM) プラグイン

[English](README_EN.md) | 日本語

[InfoPanel](https://infopanel.net) (バージョン 1.4 以降) 向けの **MSI Afterburner 共有メモリ連携プラグイン** です。
PC モニタリングのデファクトスタンダードである **MSI Afterburner** の共有メモリ（`MAHMSharedMemory`）から直接ハードウェア情報を取得し、InfoPanel 上に美しく安全にリアルタイム表示します。

> **NOTE: 「MAHM」とは？**  
> **MAHM** は **M**SI **A**fterburner **H**ardware **M**onitor の略称です。MSI Afterburner が外部アプリケーション向けにハードウェア監視テレメトリを公開している Windows 共有メモリ（`MAHMSharedMemory`）や SDK の公式内部識別子に由来しています。

---

## 開発の背景と本プラグインの強み

従来のプラグインでは、GPU 監視ライブラリ（NVML 等）のクラッシュにより InfoPanel 全体が巻き添えで落ちる問題がありました。
本プラグインは **MSI Afterburner が収集した集計データを Windows 共有メモリ経由でリードオンリー取得** するため、InfoPanel 本体をハードウェアの直接制御から完全に分離し、**100% の安定性と耐障害性** を実現しています。

---

## 主な特徴

- **完全クラッシュフリー設計**:
  ハードウェアへの直接アクセスを排し、Afterburner の共有メモリを読み取るだけの安全設計。
- **洗練された階層コンテナ分類（案1）**:
  Afterburner が公開する膨大なセンサー群を、プレフィックス重複のない美しい 6 つのコンテナに自動整理：
  - **`GPU`** : 使用率、VRAM使用量（`Memory usage` [MB]）、VRAM利用率（`FB usage (VRAM Usage)` [%]）、温度、ファン速度/回転数、コア/メモリクロック、消費電力、ステータス
  - **`GPU - Advanced & Limits`** : VID利用率、BUS利用率、Temp/Power等のサーマル・電力制限スロットリングフラグ
  - **`CPU`** : CPU 全体使用率、全体温度、動作周波数、消費電力、ステータス（※普段使いはここだけで完結）
  - **`CPU - Cores`** : 各コア（CPU1〜20等）ごとの個別温度・使用率・動作周波数
  - **`Memory`** : RAM 実メモリ使用量、コミットチャージ
  - **`Gaming (RTSS)`** : フレームレート (FPS)、描画フレーム時間 (Frametime)、FPS Min/Avg/Max、ゲーミング稼働ステータス
- **スマートな「N/A」表示とセンチネル値の無効化**:
  - ゲーム未起動時や未計測時、Afterburner/RTSS 特有の未計測センチネル値（`FLT_MAX` / `3.4028235E+38`）を自動検知して `float.NaN` および `"N/A"` へ正規化。
  - `Gaming Status` はゲーム起動中（`Framerate >= 1.0 FPS`）のみ `Active` となり、未起動時は `Idle (No Game Detected)` と判定。ゲーム非アクティブ時は Gaming 指標がすべて安全に `N/A` 表示となります。
- **FPS Min / Max の集計範囲**:
  - `Framerate Min / Max` は、現在のゲームプロセスが起動してから現在までのセッション全体（または Afterburner ベンチマークホットキーによる測定区間）での最小値・最大値を反映します。
- **堅牢な自動テストスイート完備**:
  - 共有メモリ破損・予期せぬ例外・部分欠損・極値境界・スレッドセーフティ、および実機ハードウェア結合テスト（全30件）を 100% パス。

> [!NOTE]
> **監視項目の増減に関するご注意**:
> InfoPanel のプラグイン仕様上、センサー一覧は InfoPanel の起動時にスキャン・確定されます。MSI Afterburner の設定画面（「モニタリング」タブ）で監視項目のチェックを増やしたり減らしたりした場合は、**変更を反映させるために InfoPanel を一度再起動**してください。

---

## インストール方法（超簡単 2ステップ）

### ステップ 1: プラグインフォルダの配置
リポジトリ内の [`release/InfoPanel.MAHM`](release/InfoPanel.MAHM) フォルダを、そのまま以下のパスにコピーしてください：

```text
C:\ProgramData\InfoPanel\plugins\
```

配置後のフォルダ構造：
```text
C:\ProgramData\InfoPanel\plugins\
└── InfoPanel.MAHM\
    ├── InfoPanel.MAHM.dll
    └── PluginInfo.ini
```

### ステップ 2: InfoPanel の再起動
InfoPanel を一度終了し、再起動します。
ダッシュボードの設定画面で、センサーソースとして **`MSI Afterburner`** が選択可能になります。

---

## 関連リンク
- [InfoPanel 公式サイト](https://infopanel.net)
- [InfoPanel GitHub リポジトリ](https://github.com/habibrehmansg/infopanel)
- [MSI Afterburner 公式サイト](https://www.msi.com/Landing/afterburner/graphics-cards)

---

## 開発・ビルド手順

### 必須環境
- .NET 8.0 SDK (Windows)
- InfoPanel 1.4 以降
- MSI Afterburner (モニタリング用)

### ビルド
```powershell
dotnet build InfoPanel.MAHM.sln -c Release
```

### 単体テストおよび実機結合テストの実行
```powershell
dotnet test InfoPanel.MAHM.sln -c Release
```

---

## ライセンス
MIT License
