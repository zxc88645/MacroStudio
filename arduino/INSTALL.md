# Arduino 固件安裝指南

## 前置要求

1. **硬件**
   - Arduino Leonardo（或兼容板）
   - USB Host Shield 2.0
   - USB 連接線
   - 跳線（用於連接 USB Host Shield）

2. **軟件**
   - Arduino IDE 1.8.x 或 2.x
   - USB Host Shield Library 2.0

## 安裝步驟

### 1. 安裝 Arduino IDE

從 [Arduino 官網](https://www.arduino.cc/en/software) 下載並安裝 Arduino IDE。

### 2. 安裝 USB Host Shield Library

#### 方法一：通過 Arduino IDE 庫管理器

1. 打開 Arduino IDE
2. 點擊 **工具** → **管理庫...**
3. 搜索 "USB Host Shield Library 2.0"
4. 找到由 **Circuits@Home** 開發的庫
5. 點擊 **安裝**

#### 方法二：手動安裝

1. 從 GitHub 下載：https://github.com/felis/USB_Host_Shield_2.0
2. 解壓縮到 Arduino 的 `libraries` 目錄
3. 重啟 Arduino IDE

### 3. 連接硬件

#### USB Host Shield 2.0 接線

將 USB Host Shield 2.0 連接到 Arduino Leonardo：

| USB Host Shield | Arduino Leonardo |
|----------------|------------------|
| MOSI           | Digital Pin 11   |
| MISO           | Digital Pin 12   |
| SCK            | Digital Pin 13   |
| SS             | Digital Pin 10   |
| GND            | GND              |
| 5V             | 5V               |

**注意**：
- 某些 USB Host Shield 版本可能使用不同的引腳配置
- 請參考您的 USB Host Shield 文檔確認引腳配置
- 確保電源連接正確，避免損壞設備

### 4. 上傳固件

1. 打開 `macrostudio_firmware.ino` 文件
2. 在 Arduino IDE 中選擇正確的板子和端口：
   - **工具** → **板子** → **Arduino Leonardo**
   - **工具** → **端口** → 選擇對應的 COM 端口（Windows）或 `/dev/ttyACM*`（Linux/Mac）
3. 點擊 **上傳** 按鈕（或按 `Ctrl+U`）

### 5. 驗證安裝

1. 打開 Arduino IDE 的串口監視器（**工具** → **串口監視器**）
2. 設置波特率為 **115200**
3. 如果看到錯誤消息，請檢查：
   - USB Host Shield 是否正確連接
   - 庫是否正確安裝
   - 板子選擇是否正確

## 故障排除

### 問題：上傳失敗

**解決方案**：
- 確認選擇了正確的板子（Arduino Leonardo）
- 確認選擇了正確的端口
- 嘗試按住 Reset 按鈕後再上傳
- 檢查 USB 連接線是否正常工作

### 問題：USB Host Shield 無法初始化

**解決方案**：
- 檢查接線是否正確
- 確認 USB Host Shield 電源供應充足
- 嘗試重新啟動 Arduino
- 檢查 USB Host Shield 是否損壞

### 問題：無法檢測到鍵盤/滑鼠

**解決方案**：
- 確認鍵盤/滑鼠已連接到 USB Host Shield
- 某些 USB 設備可能不兼容，嘗試使用不同的設備
- 檢查 USB Host Shield 庫版本是否正確
- 確認設備支持 USB HID 協議

### 問題：串口通信錯誤

**解決方案**：
- 確認波特率設置為 115200
- 檢查串口是否被其他程序佔用
- 嘗試重新連接 USB 線
- 確認驅動程序已正確安裝

## 測試固件

### 基本功能測試

1. **連接測試**：
   - 打開 MacroStudio 應用程序
   - 選擇硬件模式
   - 選擇對應的串口
   - 點擊連接

2. **錄製測試**：
   - 將鍵盤連接到 USB Host Shield
   - 在 MacroStudio 中開始錄製
   - 在鍵盤上輸入一些字符
   - 檢查是否能在應用程序中看到記錄的命令

3. **播放測試**：
   - 創建一個簡單的腳本
   - 選擇硬件模式執行
   - 檢查 Arduino 是否正確模擬輸入

## 注意事項

1. **電源**：確保 Arduino 和 USB Host Shield 有足夠的電源供應
2. **兼容性**：某些 USB 設備可能不完全兼容，建議使用標準 USB HID 設備
3. **性能**：硬件模式可能比軟件模式稍慢，取決於串口通信速度
4. **安全**：硬件模式繞過軟件級安全機制，使用時請注意安全

## 技術支持

如果遇到問題，請檢查：
- Arduino IDE 版本是否最新
- USB Host Shield Library 版本是否正確
- 硬件連接是否正確
- 串口設置是否正確

更多信息請參考 `README.md` 文件。
