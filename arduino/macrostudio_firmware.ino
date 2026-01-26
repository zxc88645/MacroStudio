/*
 * MacroStudio Arduino Firmware
 * 
 * This firmware enables Arduino Leonardo + USB Host Shield to:
 * 1. Record keyboard/mouse input via USB Host Shield
 * 2. Simulate keyboard/mouse input via Arduino's HID capabilities
 * 
 * Hardware Requirements:
 * - Arduino Leonardo (or compatible)
 * - USB Host Shield 2.0
 * 
 * Protocol:
 * - Commands from PC: [Type: 1 byte][Length: 2 bytes][Data: N bytes][Checksum: 1 byte]
 * - Events to PC: [Type: 1 byte][Length: 2 bytes][Data: N bytes][Timestamp: 4 bytes][Checksum: 1 byte]
 * 
 * Serial Configuration:
 * - Baud Rate: 115200
 * - Data Bits: 8
 * - Stop Bits: 1
 * - Parity: None
 */

#include <usbhub.h>
#include <hidboot.h>
#include <Keyboard.h>
#include <Mouse.h>

// USB Host Shield configuration
USB Usb;
USBHub Hub(&Usb);
HIDBoot<USB_HID_PROTOCOL_KEYBOARD> KeyboardHost(&Usb);
HIDBoot<USB_HID_PROTOCOL_MOUSE> MouseHost(&Usb);

// Protocol constants
const byte CMD_MOUSE_MOVE_ABS = 0x01;
const byte CMD_MOUSE_MOVE_REL = 0x02;
const byte CMD_MOUSE_CLICK = 0x03;
const byte CMD_KEYBOARD_TEXT = 0x04;
const byte CMD_KEY_PRESS = 0x05;
const byte CMD_DELAY = 0x06;
const byte CMD_START_RECORDING = 0x10;
const byte CMD_STOP_RECORDING = 0x11;
const byte CMD_STATUS_QUERY = 0x20;
const byte CMD_ERROR = 0xFF;

const byte EVT_MOUSE_MOVE = 0x01;
const byte EVT_MOUSE_CLICK = 0x02;
const byte EVT_KEYBOARD_INPUT = 0x03;
const byte EVT_STATUS_RESPONSE = 0x20;
const byte EVT_ERROR = 0xFF;

// Serial configuration
const unsigned long BAUD_RATE = 115200;
const int SERIAL_TIMEOUT = 1000;
const int MAX_DATA_LENGTH = 250;

// State
bool isRecording = false;
unsigned long bootTime = 0;
int mouseX = 0;
int mouseY = 0;
unsigned long lastHeartbeat = 0;
const unsigned long HEARTBEAT_INTERVAL = 5000; // 5 seconds

// Command buffer
byte cmdBuffer[256];
int cmdBufferPos = 0;
unsigned long lastCommandTime = 0;
const unsigned long COMMAND_TIMEOUT = 5000; // 5 seconds timeout for incomplete commands

// Mouse button states (for tracking)
bool leftButtonDown = false;
bool rightButtonDown = false;
bool middleButtonDown = false;

/*
 * HID Keyboard Report Parser
 * Converts HID keyboard events to VirtualKey codes and sends to PC
 */
class KbdRptParser : public KeyboardReportParser {
protected:
  void OnKeyDown(uint8_t mod, uint8_t key) override {
    if (isRecording && key != 0) {
      sendKeyboardEvent(key, mod, true);
    }
  }
  
  void OnKeyUp(uint8_t mod, uint8_t key) override {
    if (isRecording && key != 0) {
      sendKeyboardEvent(key, mod, false);
    }
  }
  
private:
  void sendKeyboardEvent(uint8_t hidKey, uint8_t modifiers, bool isDown) {
    // Convert HID key code to Virtual Key code
    uint16_t vk = mapHidKeyToVk(hidKey, modifiers);
    
    if (vk == 0) {
      return; // Unsupported key
    }
    
    byte data[3];
    data[0] = (byte)(vk & 0xFF);
    data[1] = (byte)((vk >> 8) & 0xFF);
    data[2] = isDown ? 1 : 0;
    
    sendEvent(EVT_KEYBOARD_INPUT, data, 3);
  }
  
  uint16_t mapHidKeyToVk(uint8_t hidKey, uint8_t modifiers) {
    // HID Usage Page 0x07 (Keyboard/Keypad) mapping to Windows Virtual Key codes
    // Reference: https://www.usb.org/sites/default/files/documents/hut1_12v2.pdf
    
    if (hidKey >= 4 && hidKey <= 29) {
      // Letters A-Z (HID: 4-29, VK: 0x41-0x5A)
      return 0x41 + (hidKey - 4);
    }
    
    if (hidKey >= 30 && hidKey <= 39) {
      // Numbers 1-9, 0 (HID: 30-39, VK: 0x31-0x39, 0x30)
      if (hidKey == 39) return 0x30; // 0
      return 0x31 + (hidKey - 30); // 1-9
    }
    
    // Special keys mapping
    switch (hidKey) {
      case 40: return 0x0D; // Enter -> VK_RETURN
      case 41: return 0x1B; // Escape -> VK_ESCAPE
      case 42: return 0x08; // Backspace -> VK_BACK
      case 43: return 0x09; // Tab -> VK_TAB
      case 44: return 0x20; // Space -> VK_SPACE
      case 45: return 0xBD; // - -> VK_OEM_MINUS
      case 46: return 0xBB; // = -> VK_OEM_PLUS
      case 47: return 0xDB; // [ -> VK_OEM_4
      case 48: return 0xDD; // ] -> VK_OEM_6
      case 49: return 0xDC; // \ -> VK_OEM_5
      case 50: return 0x00; // Non-US # and ~ (not mapped)
      case 51: return 0xBA; // ; -> VK_OEM_1
      case 52: return 0xDE; // ' -> VK_OEM_7
      case 53: return 0xC0; // ` -> VK_OEM_3
      case 54: return 0xBC; // , -> VK_OEM_COMMA
      case 55: return 0xBE; // . -> VK_OEM_PERIOD
      case 56: return 0xBF; // / -> VK_OEM_2
      case 57: return 0x14; // Caps Lock -> VK_CAPITAL
      
      // Function keys
      case 58: return 0x70; // F1 -> VK_F1
      case 59: return 0x71; // F2 -> VK_F2
      case 60: return 0x72; // F3 -> VK_F3
      case 61: return 0x73; // F4 -> VK_F4
      case 62: return 0x74; // F5 -> VK_F5
      case 63: return 0x75; // F6 -> VK_F6
      case 64: return 0x76; // F7 -> VK_F7
      case 65: return 0x77; // F8 -> VK_F8
      case 66: return 0x78; // F9 -> VK_F9
      case 67: return 0x79; // F10 -> VK_F10
      case 68: return 0x7A; // F11 -> VK_F11
      case 69: return 0x7B; // F12 -> VK_F12
      
      // Navigation keys
      case 73: return 0x2C; // Print Screen -> VK_SNAPSHOT
      case 74: return 0x91; // Scroll Lock -> VK_SCROLL
      case 75: return 0x13; // Pause -> VK_PAUSE
      case 76: return 0x24; // Insert -> VK_INSERT
      case 77: return 0x23; // Home -> VK_HOME
      case 78: return 0x21; // Page Up -> VK_PRIOR
      case 79: return 0x2E; // Delete -> VK_DELETE
      case 80: return 0x24; // End -> VK_END
      case 81: return 0x22; // Page Down -> VK_NEXT
      case 82: return 0x26; // Right Arrow -> VK_UP
      case 83: return 0x25; // Left Arrow -> VK_LEFT
      case 84: return 0x28; // Down Arrow -> VK_DOWN
      case 85: return 0x27; // Up Arrow -> VK_RIGHT
      
      // Numpad
      case 89: return 0x90; // Num Lock -> VK_NUMLOCK
      case 90: return 0x6F; // Numpad / -> VK_DIVIDE
      case 91: return 0x6A; // Numpad * -> VK_MULTIPLY
      case 92: return 0x6D; // Numpad - -> VK_SUBTRACT
      case 93: return 0x6B; // Numpad + -> VK_ADD
      case 94: return 0x0D; // Numpad Enter -> VK_RETURN
      case 95: return 0x60; // Numpad 1 -> VK_NUMPAD1
      case 96: return 0x61; // Numpad 2 -> VK_NUMPAD2
      case 97: return 0x62; // Numpad 3 -> VK_NUMPAD3
      case 98: return 0x63; // Numpad 4 -> VK_NUMPAD4
      case 99: return 0x64; // Numpad 5 -> VK_NUMPAD5
      case 100: return 0x65; // Numpad 6 -> VK_NUMPAD6
      case 101: return 0x66; // Numpad 7 -> VK_NUMPAD7
      case 102: return 0x67; // Numpad 8 -> VK_NUMPAD8
      case 103: return 0x68; // Numpad 9 -> VK_NUMPAD9
      case 104: return 0x69; // Numpad 0 -> VK_NUMPAD0
      case 105: return 0x6E; // Numpad . -> VK_DECIMAL
      
      default:
        return 0; // Unsupported key
    }
  }
};

/*
 * Mouse Report Parser
 * Converts HID mouse events to coordinates and button states, sends to PC
 */
class MouseRptParser : public MouseReportParser {
protected:
  void OnMouseMove(MOUSEINFO *mi) override {
    if (isRecording) {
      // Update relative position
      mouseX += mi->dX;
      mouseY += mi->dY;
      
      // Clamp to screen bounds (typical screen size, adjust as needed)
      if (mouseX < 0) mouseX = 0;
      if (mouseY < 0) mouseY = 0;
      if (mouseX > 65535) mouseX = 65535;
      if (mouseY > 65535) mouseY = 65535;
      
      byte data[4];
      data[0] = (byte)(mouseX & 0xFF);
      data[1] = (byte)((mouseX >> 8) & 0xFF);
      data[2] = (byte)(mouseY & 0xFF);
      data[3] = (byte)((mouseY >> 8) & 0xFF);
      
      sendEvent(EVT_MOUSE_MOVE, data, 4);
    }
  }
  
  void OnLeftButtonDown(MOUSEINFO *mi) override {
    if (isRecording && !leftButtonDown) {
      leftButtonDown = true;
      sendMouseClickEvent(0, 0); // Left button, Down
    }
  }
  
  void OnLeftButtonUp(MOUSEINFO *mi) override {
    if (isRecording && leftButtonDown) {
      leftButtonDown = false;
      sendMouseClickEvent(0, 1); // Left button, Up
    }
  }
  
  void OnRightButtonDown(MOUSEINFO *mi) override {
    if (isRecording && !rightButtonDown) {
      rightButtonDown = true;
      sendMouseClickEvent(1, 0); // Right button, Down
    }
  }
  
  void OnRightButtonUp(MOUSEINFO *mi) override {
    if (isRecording && rightButtonDown) {
      rightButtonDown = false;
      sendMouseClickEvent(1, 1); // Right button, Up
    }
  }
  
  void OnMiddleButtonDown(MOUSEINFO *mi) override {
    if (isRecording && !middleButtonDown) {
      middleButtonDown = true;
      sendMouseClickEvent(2, 0); // Middle button, Down
    }
  }
  
  void OnMiddleButtonUp(MOUSEINFO *mi) override {
    if (isRecording && middleButtonDown) {
      middleButtonDown = false;
      sendMouseClickEvent(2, 1); // Middle button, Up
    }
  }
  
private:
  void sendMouseClickEvent(byte button, byte clickType) {
    byte data[6];
    data[0] = (byte)(mouseX & 0xFF);
    data[1] = (byte)((mouseX >> 8) & 0xFF);
    data[2] = (byte)(mouseY & 0xFF);
    data[3] = (byte)((mouseY >> 8) & 0xFF);
    data[4] = button;
    data[5] = clickType;
    
    sendEvent(EVT_MOUSE_CLICK, data, 6);
  }
};

KbdRptParser kbdParser;
MouseRptParser mouseParser;

void setup() {
  bootTime = millis();
  lastHeartbeat = bootTime;
  lastCommandTime = bootTime;
  
  // Initialize serial
  Serial.begin(BAUD_RATE);
  Serial.setTimeout(SERIAL_TIMEOUT);
  
  // Wait for serial port to be ready (optional, for debugging)
  #ifdef DEBUG
  while (!Serial) {
    delay(10);
  }
  #endif
  
  // Initialize USB Host Shield
  if (Usb.Init() == -1) {
    sendError("USB Host Shield init failed");
    // Continue anyway - may recover later
  }
  
  // Set report parsers
  KeyboardHost.SetReportParser(0, &kbdParser);
  MouseHost.SetReportParser(0, &mouseParser);
  
  // Initialize Keyboard and Mouse libraries (for output)
  Keyboard.begin();
  Mouse.begin();
  
  delay(200);
}

void loop() {
  unsigned long currentTime = millis();
  
  // Process USB Host Shield (must be called regularly)
  Usb.Task();
  
  // Process serial commands
  processSerialCommands();
  
  // Send periodic heartbeat if connected
  if (currentTime - lastHeartbeat >= HEARTBEAT_INTERVAL) {
    lastHeartbeat = currentTime;
    // Heartbeat is handled by PC sending status queries
  }
  
  // Reset command buffer if timeout
  if (cmdBufferPos > 0 && (currentTime - lastCommandTime) > COMMAND_TIMEOUT) {
    cmdBufferPos = 0;
  }
  
  delay(1);
}

void processSerialCommands() {
  while (Serial.available() > 0) {
    byte b = Serial.read();
    lastCommandTime = millis();
    
    if (cmdBufferPos == 0) {
      // Start of new command - validate command type
      if (b == CMD_MOUSE_MOVE_ABS || b == CMD_MOUSE_MOVE_REL || 
          b == CMD_MOUSE_CLICK || b == CMD_KEYBOARD_TEXT || 
          b == CMD_KEY_PRESS || b == CMD_DELAY ||
          b == CMD_START_RECORDING || b == CMD_STOP_RECORDING ||
          b == CMD_STATUS_QUERY) {
        cmdBuffer[0] = b;
        cmdBufferPos = 1;
      }
      // Ignore invalid command bytes
    } else if (cmdBufferPos == 1) {
      // Data length low byte
      cmdBuffer[1] = b;
      cmdBufferPos = 2;
    } else if (cmdBufferPos == 2) {
      // Data length high byte
      cmdBuffer[2] = b;
      uint16_t dataLength = cmdBuffer[1] | (cmdBuffer[2] << 8);
      
      if (dataLength > MAX_DATA_LENGTH) {
        // Invalid length, reset
        cmdBufferPos = 0;
        sendError("Data length too large");
        continue;
      }
      
      cmdBufferPos = 3;
      
      // Read data and checksum
      int bytesToRead = dataLength + 1; // +1 for checksum
      int bytesRead = 0;
      unsigned long startWait = millis();
      
      // Wait for remaining bytes with timeout
      while (bytesRead < bytesToRead) {
        if (Serial.available() > 0) {
          cmdBuffer[3 + bytesRead] = Serial.read();
          bytesRead++;
          startWait = millis(); // Reset timeout on each byte
        } else if (millis() - startWait > 100) {
          // Timeout waiting for data
          cmdBufferPos = 0;
          return;
        }
        delay(1);
      }
      
      if (bytesRead == bytesToRead) {
        // Verify checksum
        byte checksum = calculateChecksum(cmdBuffer, 3 + dataLength);
        if (checksum == cmdBuffer[3 + dataLength]) {
          // Execute command
          executeCommand(cmdBuffer[0], cmdBuffer + 3, dataLength);
        } else {
          sendError("Checksum mismatch");
        }
      }
      
      cmdBufferPos = 0;
    }
  }
}

void executeCommand(byte cmdType, byte* data, uint16_t dataLength) {
  switch (cmdType) {
    case CMD_MOUSE_MOVE_ABS:
      if (dataLength >= 4) {
        int x = (int16_t)(data[0] | (data[1] << 8));
        int y = (int16_t)(data[2] | (data[3] << 8));
        
        // Clamp to valid range
        if (x < 0) x = 0;
        if (y < 0) y = 0;
        if (x > 32767) x = 32767;
        if (y > 32767) y = 32767;
        
        Mouse.moveTo(x, y);
        mouseX = x;
        mouseY = y;
      }
      break;
      
    case CMD_MOUSE_MOVE_REL:
      if (dataLength >= 4) {
        int dx = (int16_t)(data[0] | (data[1] << 8));
        int dy = (int16_t)(data[2] | (data[3] << 8));
        
        Mouse.move(dx, dy);
        mouseX += dx;
        mouseY += dy;
        
        // Clamp to valid range
        if (mouseX < 0) mouseX = 0;
        if (mouseY < 0) mouseY = 0;
        if (mouseX > 32767) mouseX = 32767;
        if (mouseY > 32767) mouseY = 32767;
      }
      break;
      
    case CMD_MOUSE_CLICK:
      if (dataLength >= 2) {
        byte button = data[0];
        byte clickType = data[1];
        
        // 0=Left, 1=Right, 2=Middle
        // clickType: 0=Down, 1=Up, 2=Click
        
        if (button == 0) { // Left
          if (clickType == 0) Mouse.press(MOUSE_LEFT);
          else if (clickType == 1) Mouse.release(MOUSE_LEFT);
          else if (clickType == 2) Mouse.click(MOUSE_LEFT);
        } else if (button == 1) { // Right
          if (clickType == 0) Mouse.press(MOUSE_RIGHT);
          else if (clickType == 1) Mouse.release(MOUSE_RIGHT);
          else if (clickType == 2) Mouse.click(MOUSE_RIGHT);
        } else if (button == 2) { // Middle
          if (clickType == 0) Mouse.press(MOUSE_MIDDLE);
          else if (clickType == 1) Mouse.release(MOUSE_MIDDLE);
          else if (clickType == 2) Mouse.click(MOUSE_MIDDLE);
        }
      }
      break;
      
    case CMD_KEYBOARD_TEXT:
      if (dataLength > 0) {
        // Send text character by character
        for (uint16_t i = 0; i < dataLength; i++) {
          Keyboard.write(data[i]);
          delay(10); // Small delay between characters
        }
      }
      break;
      
    case CMD_KEY_PRESS:
      if (dataLength >= 3) {
        uint16_t vk = data[0] | (data[1] << 8);
        bool isDown = data[2] != 0;
        
        // Convert VK to Arduino key code
        uint8_t key = mapVkToArduinoKey(vk);
        if (key != 0) {
          if (isDown) {
            Keyboard.press(key);
          } else {
            Keyboard.release(key);
          }
        }
      }
      break;
      
    case CMD_DELAY:
      if (dataLength >= 4) {
        uint32_t durationMs = (uint32_t)data[0] | 
                              ((uint32_t)data[1] << 8) | 
                              ((uint32_t)data[2] << 16) | 
                              ((uint32_t)data[3] << 24);
        
        // Limit maximum delay to prevent blocking
        if (durationMs > 60000) durationMs = 60000; // Max 60 seconds
        
        delay(durationMs);
      }
      break;
      
    case CMD_START_RECORDING:
      isRecording = true;
      mouseX = 0;
      mouseY = 0;
      leftButtonDown = false;
      rightButtonDown = false;
      middleButtonDown = false;
      break;
      
    case CMD_STOP_RECORDING:
      isRecording = false;
      break;
      
    case CMD_STATUS_QUERY:
      sendStatusResponse();
      break;
      
    default:
      sendError("Unknown command");
      break;
  }
}

void sendEvent(byte eventType, byte* data, uint16_t dataLength) {
  if (dataLength > MAX_DATA_LENGTH) {
    return; // Data too large
  }
  
  byte buffer[256];
  int pos = 0;
  
  // Event type
  buffer[pos++] = eventType;
  
  // Data length
  buffer[pos++] = (byte)(dataLength & 0xFF);
  buffer[pos++] = (byte)((dataLength >> 8) & 0xFF);
  
  // Data
  for (uint16_t i = 0; i < dataLength; i++) {
    buffer[pos++] = data[i];
  }
  
  // Timestamp (milliseconds since boot)
  uint32_t timestamp = millis() - bootTime;
  buffer[pos++] = (byte)(timestamp & 0xFF);
  buffer[pos++] = (byte)((timestamp >> 8) & 0xFF);
  buffer[pos++] = (byte)((timestamp >> 16) & 0xFF);
  buffer[pos++] = (byte)((timestamp >> 24) & 0xFF);
  
  // Checksum (calculated before adding checksum byte)
  byte checksum = calculateChecksum(buffer, pos);
  buffer[pos++] = checksum;
  
  // Send
  Serial.write(buffer, pos);
}

void sendStatusResponse() {
  byte data[1];
  data[0] = isRecording ? 1 : 0;
  sendEvent(EVT_STATUS_RESPONSE, data, 1);
}

void sendError(const char* message) {
  uint16_t len = strlen(message);
  if (len > MAX_DATA_LENGTH) len = MAX_DATA_LENGTH;
  
  byte data[len];
  memcpy(data, message, len);
  sendEvent(EVT_ERROR, data, len);
}

byte calculateChecksum(byte* data, int length) {
  byte checksum = 0;
  for (int i = 0; i < length; i++) {
    checksum ^= data[i];
  }
  return checksum;
}

/*
 * Maps Windows Virtual Key codes to Arduino Keyboard library key codes
 * Reference: Arduino Keyboard library and Windows VK codes
 */
uint8_t mapVkToArduinoKey(uint16_t vk) {
  // Letters A-Z
  if (vk >= 0x41 && vk <= 0x5A) {
    return (vk - 0x41) + 'a';
  }
  
  // Numbers 0-9
  if (vk >= 0x30 && vk <= 0x39) {
    return (vk - 0x30) + '0';
  }
  
  // Special keys
  switch (vk) {
    case 0x08: return KEY_BACKSPACE;
    case 0x09: return KEY_TAB;
    case 0x0D: return KEY_RETURN;
    case 0x1B: return KEY_ESC;
    case 0x20: return ' ';
    case 0x21: return KEY_PAGE_UP;
    case 0x22: return KEY_PAGE_DOWN;
    case 0x23: return KEY_END;
    case 0x24: return KEY_HOME;
    case 0x25: return KEY_LEFT_ARROW;
    case 0x26: return KEY_UP_ARROW;
    case 0x27: return KEY_RIGHT_ARROW;
    case 0x28: return KEY_DOWN_ARROW;
    case 0x2C: return KEY_PRINT_SCREEN;
    case 0x2D: return KEY_INSERT;
    case 0x2E: return KEY_DELETE;
    
    // Function keys
    case 0x70: return KEY_F1;
    case 0x71: return KEY_F2;
    case 0x72: return KEY_F3;
    case 0x73: return KEY_F4;
    case 0x74: return KEY_F5;
    case 0x75: return KEY_F6;
    case 0x76: return KEY_F7;
    case 0x77: return KEY_F8;
    case 0x78: return KEY_F9;
    case 0x79: return KEY_F10;
    case 0x7A: return KEY_F11;
    case 0x7B: return KEY_F12;
    
    // Modifier keys
    case 0x10: return KEY_LEFT_SHIFT; // Shift
    case 0x11: return KEY_LEFT_CTRL;  // Control
    case 0x12: return KEY_LEFT_ALT;   // Alt
    case 0xA0: return KEY_LEFT_SHIFT; // Left Shift
    case 0xA1: return KEY_RIGHT_SHIFT; // Right Shift
    case 0xA2: return KEY_LEFT_CTRL;  // Left Control
    case 0xA3: return KEY_RIGHT_CTRL; // Right Control
    case 0xA4: return KEY_LEFT_ALT;   // Left Alt
    case 0xA5: return KEY_RIGHT_ALT;  // Right Alt
    
    // Numpad
    case 0x60: return KEYPAD_0;
    case 0x61: return KEYPAD_1;
    case 0x62: return KEYPAD_2;
    case 0x63: return KEYPAD_3;
    case 0x64: return KEYPAD_4;
    case 0x65: return KEYPAD_5;
    case 0x66: return KEYPAD_6;
    case 0x67: return KEYPAD_7;
    case 0x68: return KEYPAD_8;
    case 0x69: return KEYPAD_9;
    case 0x6A: return KEYPAD_MULTIPLY;
    case 0x6B: return KEYPAD_PLUS;
    case 0x6D: return KEYPAD_MINUS;
    case 0x6E: return KEYPAD_PERIOD;
    case 0x6F: return KEYPAD_SLASH;
    
    // OEM keys (punctuation)
    case 0xBA: return ';'; // VK_OEM_1
    case 0xBB: return '='; // VK_OEM_PLUS
    case 0xBC: return ','; // VK_OEM_COMMA
    case 0xBD: return '-'; // VK_OEM_MINUS
    case 0xBE: return '.'; // VK_OEM_PERIOD
    case 0xBF: return '/'; // VK_OEM_2
    case 0xC0: return '`'; // VK_OEM_3
    case 0xDB: return '['; // VK_OEM_4
    case 0xDC: return '\\'; // VK_OEM_5
    case 0xDD: return ']'; // VK_OEM_6
    case 0xDE: return '\''; // VK_OEM_7
    
    default:
      return 0; // Unsupported key
  }
}
