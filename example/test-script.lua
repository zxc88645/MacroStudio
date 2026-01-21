-- MacroStudio Lua 測試腳本
-- 展示變數、算術、迴圈、條件判斷與 API 呼叫

-- 定義變數
local startX = 100
local startY = 200
local stepSize = 50
local delayMs = 100

-- 移動到起始位置
move(startX, startY)
msleep(delayMs)

-- 測試算術運算：畫一個正方形
for i = 1, 4 do
    local x = startX + (i * stepSize)
    local y = startY + (i * stepSize)
    move(x, y)
    msleep(delayMs)
end

-- 測試條件判斷
local clickCount = 3
if clickCount > 0 then
    for i = 1, clickCount do
        mouse_click('left')
        msleep(200)
    end
end

-- 測試文字輸入
type_text('Hello from Lua!')
msleep(500)

-- 測試 sleep（秒）與 msleep（毫秒）
sleep(0.5)  -- 0.5 秒
msleep(250) -- 250 毫秒

-- 測試滑鼠按鈕
mouse_down('left')
msleep(50)
mouse_release('left')

-- 測試鍵盤按鍵
key_down('VK_A')
msleep(50)
key_release('VK_A')

-- 複雜範例：根據變數決定行為
local mode = 1
if mode == 1 then
    -- 模式 1：快速點擊
    for i = 1, 5 do
        mouse_click('left')
        msleep(100)
    end
elseif mode == 2 then
    -- 模式 2：移動模式
    local positions = {
        {x = 300, y = 300},
        {x = 400, y = 400},
        {x = 500, y = 500}
    }
    for _, pos in ipairs(positions) do
        move(pos.x, pos.y)
        msleep(200)
    end
end

print('腳本執行完成！')
