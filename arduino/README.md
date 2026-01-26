# Arduino Leonardo + USB Host Shield 固件開發指南

本指南說明如何為 MacroStudio 配置 Arduino Leonardo 和 USB Host Shield，以實現硬件級鍵盤/滑鼠錄製與播放功能。

## 硬件要求

- **Arduino Leonardo** 開發板（或兼容板）
- **USB Host Shield 2.0**（或兼容版本）
- USB 連接線
- 跳線（用於連接 USB Host Shield）

## 接線說明

### USB Host Shield 2.0 連接

USB Host Shield 2.0 通過 SPI 接口連接到 Arduino Leonardo：

1. **MOSI** → Digital Pin 11
2. **MISO** → Digital Pin 12
3. **SCK** → Digital Pin 13
4. **SS** → Digital Pin 10
5. **GND** → GND
6. **5V** → 5V

**注意**：某些 USB Host Shield 版本可能使用不同的引腳配置，請參考您的 Shield 文檔。

## 所需庫

在 Arduino IDE 中安裝以下庫：

1. **USB Host Shield Library 2.0**
   - 在 Arduino IDE 中：工具 → 管理庫 → 搜索 "USB Host Shield Library 2.0"
   - 或從 GitHub: https://github.com/felis/USB_Host_Shield_2.0

2. **Keyboard** 和 **Mouse** 庫（Arduino Leonardo 內置）

## 通信協議

### 命令格式（PC → Arduino）

```
[命令類型: 1 byte][數據長度: 2 bytes][數據: N bytes][校驗和: 1 byte]
```

### 事件格式（Arduino → PC）

```
[事件類型: 1 byte][數據長度: 2 bytes][數據: N bytes][時間戳: 4 bytes][校驗和: 1 byte]
```

### 命令類型

- `0x01`: 滑鼠移動（絕對座標）
- `0x02`: 滑鼠移動（相對座標）
- `0x03`: 滑鼠點擊
- `0x04`: 鍵盤輸入（文本）
- `0x05`: 按鍵按下/釋放
- `0x06`: 延遲
- `0x10`: 開始錄製
- `0x11`: 停止錄製
- `0x20`: 心跳/狀態查詢
- `0xFF`: 錯誤響應

### 事件類型

- `0x01`: 滑鼠移動
- `0x02`: 滑鼠點擊
- `0x03`: 鍵盤輸入
- `0x20`: 狀態響應
- `0xFF`: 錯誤

## 串口配置

- **波特率**: 115200
- **數據位**: 8
- **停止位**: 1
- **校驗位**: 無
- **流控制**: 無

## 功能說明

### 錄製模式

當收到 `StartRecording` 命令時，Arduino 開始通過 USB Host Shield 監聽連接的鍵盤和滑鼠輸入，並將事件發送到 PC。

### 播放模式

當收到輸入模擬命令時，Arduino 使用內置的 Keyboard 和 Mouse 庫模擬鍵盤和滑鼠輸入。

## 注意事項

1. **權限要求**：某些操作系統可能需要管理員權限才能訪問串口
2. **驅動程序**：確保 Arduino Leonardo 的 USB 串口驅動已正確安裝
3. **兼容性**：USB Host Shield 需要與 Arduino Leonardo 兼容的版本
4. **性能**：硬件模式可能比軟件模式稍慢（取決於串口通信速度）
5. **安全性**：硬件模式繞過軟件級安全機制，需要額外的安全考慮

## 故障排除

### Arduino 無法連接

1. 檢查串口驅動是否正確安裝
2. 確認串口名稱正確（Windows: COM3, COM4 等）
3. 檢查 USB 連接線是否正常工作
4. 嘗試重新啟動 Arduino

### USB Host Shield 無法檢測設備

1. 檢查接線是否正確
2. 確認 USB Host Shield 電源供應充足
3. 檢查 USB Host Shield 庫是否正確安裝
4. 某些 USB 設備可能不兼容，嘗試使用不同的鍵盤/滑鼠

### 命令執行失敗

1. 檢查協議格式是否正確
2. 驗證校驗和是否正確
3. 確認串口通信是否穩定
4. 檢查 Arduino 是否有足夠的內存

## 開發建議

1. 使用串口監視器調試通信問題
2. 實現詳細的日誌記錄
3. 添加錯誤處理和恢復機制
4. 測試各種邊界情況
5. 優化協議以減少通信開銷
