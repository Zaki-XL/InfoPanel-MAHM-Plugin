# InfoPanel MAHM Plugin - セットアップ・導入ガイド (SETUP.md)

## 1. 前提条件 (Prerequisites)

* **Windows 10 / 11 (64-bit)**
* **.NET 8.0 SDK** (ビルド時に必要)
* **MSI Afterburner** (最新版が起動していること)
  * ※起動後、設定でハードウェア監視が有効になっていることを確認してください（デフォルトで有効）。
* **InfoPanel 1.4.x**
  * インストール先: `C:\Program Files (x86)\InfoPanel`

---

## 2. ビルド手順 (Build)

プロジェクトルートにて以下のコマンドを実行してビルドします：

```powershell
dotnet build -c Release
```

ビルドが完了すると、以下のフォルダに出力物が生成されます：
`src\InfoPanel.MAHM\bin\Release\net8.0-windows\`
* `InfoPanel.MAHM.dll`
* `PluginInfo.ini`

---

## 3. インストール・配置手順 (Deployment)

InfoPanel がプラグインを自動検出・ロードするディレクトリに配置します。

### 配置先ディレクトリ
```text
C:\ProgramData\InfoPanel\plugins\InfoPanel.MAHM\
```
または
```text
C:\Program Files (x86)\InfoPanel\plugins\InfoPanel.MAHM\
```

### 配置ファイル
以下の2つのファイルを上記フォルダにコピーします：
1. `InfoPanel.MAHM.dll`
2. `PluginInfo.ini`

---

## 4. InfoPanel でのセンサー割り当て

1. **InfoPanel を再起動** します。
2. InfoPanel の編集画面（Designer）を開き、対象のプロファイルを選択します。
3. **「GPU Core」** や **「GPU Memory」**、**「GPU Fan」** などの項目を選択します。
4. センサーのプロパティで：
   * **Sensor Type**: `Plugin`
   * **Plugin Sensor**:
     * GPU 利用率: `MSI Afterburner` ➔ `GPU usage`
     * GPU メモリ: `MSI Afterburner` ➔ `Memory usage`
     * GPU 温度: `MSI Afterburner` ➔ `GPU temperature`
     * GPU ファン: `MSI Afterburner` ➔ `Fan speed` / `Fan tachometer`
5. 保存して完了です。
