# 品質・セキュリティ監査報告書 (QA & Security Review Report)

- **対象プロジェクト**: InfoPanel MSI Afterburner Plugin (`InfoPanel.MAHM`)
- **監査日**: 2026-09-25
- **バージョン**: v1.1.0 (Afterburner ポーリング周期自動同期対応)
- **監査担当**: QA & Security Role
- **総合判定**: **PASS (承認完了)**

---

## 1. 監査結果サマリー

| 監査項目 | 結果 | 判定 |
| :--- | :--- | :--- |
| 自動テスト通過率 | 44 / 44 件 (100%) | **PASS** |
| ビルドステータス | 0 Warning, 0 Error (Release) | **PASS** |
| ポーリング周期自動検知 | レジストリ探索・CFGパース・安全クランプ検証済み | **PASS** |
| センチネル値・異常値耐性 | `FLT_MAX` 正規化・N/A化多層防御検証済み | **PASS** |
| メトリクス分類・UI整合性 | `FB usage` メイン昇格・フレンドリーネーム検証済み | **PASS** |
| プロセス分離・安定性 | リードオンリー共有メモリ + ホスト分離 | **PASS** |

---

## 2. 実装変更点に対する監査詳細

### ① Afterburner ポーリング周期 (`HwPollPeriod`) 自動検知・動的同期
- **設計**: `AfterburnerConfigService` が起動時にレジストリ（`HKLM\SOFTWARE\WOW6432Node\MSI\Afterburner`）経由で `MSIAfterburner.cfg` を参照。
- **安全ガード**: パース値が 100ms 〜 10000ms の範囲外、またはファイル不在・構文異常時はデフォルト 1000ms へ安全フォールバック。
- **監査結果**: `AfterburnerConfigServiceTests`（12テストケース）および実機テストを含む全テストが 100% PASS。

### ② Framerate 異常値対策（センチネル正規化）
- **事象**: ゲーム未起動時に RTSS の無効値フラグ `3.4028235E+38` (`FLT_MAX`) が取得され、天文学的数値が表示されていた。
- **対策**: `MahmBufferParser` で `Math.Abs(val) >= 3.4e38f` を検知して `float.NaN` へ変換。`SensorFormatter.IsInvalid` にもセンチネル条件を追加し多層防御を実施。
- **監査結果**: 実機結合テスト `RealAfterburner_WhenNoGameRunning_FramerateIsNaAndStatusIsIdle` を含む全回帰テストが PASS することを確認。

### ③ VRAM 利用率 (`FB usage`) の適正化
- **事象**: VRAM 利用率（`FB usage` [%]）が詳細コンテナに隠れており、名称が分かりにくかった。
- **対策**: メインの `GPU` コンテナに配置変更し、名称を `FB usage (VRAM Usage)` に更新。
- **監査結果**: `PluginLogicTests` および `RealAfterburner_PopulatesProposal1ContainersCleanly` にて正常にメインコンテナに含まれることを確認。

---

## 3. 総合評価

すべてのセキュリティ・品質基準およびガードレールを満たしており、プロダクション利用に十分な品質であることを確認いたしました。
