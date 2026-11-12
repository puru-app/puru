# AudioSwitch

一鍵切換 Windows 預設輸出／輸入裝置的小工具。常駐系統匣，快捷鍵呼出面板，點裝置就切換；點耳麥這類輸出時，會自動把同一支裝置的麥克風一併設為預設輸入。

<img src="design/icon-256.png" width="96" alt="AudioSwitch icon">

## 下載

免安裝版在本倉庫的 [Releases](https://github.com/puru-app/puru/releases)（檔名 `AudioSwitch-<版本>-portable-x64.zip`）。解壓後執行 `AudioSwitch.exe` 即可，內含 .NET 執行環境。

## 使用

- 預設快捷鍵 **Ctrl+Alt+A** 開面板（系統匣右鍵可改）；Esc 或點別處關閉
- 面板上半是輸出、下半是輸入，點一下即設為預設（Console／Multimedia／Communications 三個角色一起）
- 面板底部的「切輸出時一併切同裝置的麥克風」預設開啟；配對規則是裝置名稱括號內的型號相同（藍牙的 Stereo／Hands-Free 字尾會先剝掉）
- 系統匣右鍵：設定快捷鍵、開機自動啟動、關於、結束
- 設定檔 `hotkey.txt`、`link.txt` 存在 exe 旁邊

## 自行建置

需要 .NET 8 SDK。`build.bat` 產生需要 .NET 8 Desktop Runtime 的小型版；`build-portable.bat` 產生自帶執行環境的免安裝版並打包到 `Output\`。

## 設計

介面與圖示依 puru 系列 UI／UX 設計指南：暖白底、黑字、黑塊反白、6／8px 導角；圖示為圓形黑底白線條稿，紅色加號為強調色。

## 授權

MIT License，© 2026 masamoon。
