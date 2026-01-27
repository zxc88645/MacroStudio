-- MacroNex Lua æ¸¬è©¦?³æœ¬
-- å±•ç¤ºè®Šæ•¸?ç?è¡“ã€è¿´?ˆã€æ?ä»¶åˆ¤?·è? API ?¼å«

-- å®šç¾©è®Šæ•¸
local startX = 100
local startY = 200
local stepSize = 50
local delayMs = 100

-- ç§»å??°èµ·å§‹ä?ç½?
move(startX, startY)
msleep(delayMs)

-- æ¸¬è©¦ç®—è??‹ç?ï¼šç•«ä¸€?‹æ­£?¹å½¢
for i = 1, 4 do
    local x = startX + (i * stepSize)
    local y = startY + (i * stepSize)
    move(x, y)
    msleep(delayMs)
end

-- æ¸¬è©¦æ¢ä»¶?¤æ–·
local clickCount = 3
if clickCount > 0 then
    for i = 1, clickCount do
        mouse_click('left')
        msleep(200)
    end
end

-- æ¸¬è©¦?‡å?è¼¸å…¥
type_text('Hello from Lua!')
msleep(500)

-- æ¸¬è©¦ sleepï¼ˆç?ï¼‰è? msleepï¼ˆæ¯«ç§’ï?
sleep(0.5)  -- 0.5 ç§?
msleep(250) -- 250 æ¯«ç?

-- æ¸¬è©¦æ»‘é??‰é?
mouse_down('left')
msleep(50)
mouse_release('left')

-- æ¸¬è©¦?µç›¤?‰éµ
key_down('VK_A')
msleep(50)
key_release('VK_A')

-- è¤‡é?ç¯„ä?ï¼šæ ¹?šè??¸æ±ºå®šè???
local mode = 1
if mode == 1 then
    -- æ¨¡å? 1ï¼šå¿«?Ÿé???
    for i = 1, 5 do
        mouse_click('left')
        msleep(100)
    end
elseif mode == 2 then
    -- æ¨¡å? 2ï¼šç§»?•æ¨¡å¼?
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

print('?³æœ¬?·è?å®Œæ?ï¼?)
