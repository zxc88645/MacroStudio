using MacroNex.Domain.ValueObjects;

namespace MacroNex.Infrastructure.Utilities;

/// <summary>
/// Decodes binary protocol data from Arduino into events.
/// Protocol format: [EventType: 1 byte][DataLength: 2 bytes][Data: N bytes][Timestamp: 4 bytes][Checksum: 1 byte]
/// </summary>
public static class ArduinoProtocolDecoder
{
    private const int MinEventSize = 8; // 1 (type) + 2 (length) + 4 (timestamp) + 1 (checksum)

    /// <summary>
    /// Attempts to decode an event from the buffer.
    /// </summary>
    /// <param name="buffer">The buffer containing the event data.</param>
    /// <param name="offset">The offset to start reading from.</param>
    /// <param name="decodedEvent">The decoded event, if successful.</param>
    /// <returns>The number of bytes consumed, or 0 if not enough data is available.</returns>
    public static int TryDecodeEvent(byte[] buffer, int offset, out DecodedArduinoEvent? decodedEvent)
    {
        decodedEvent = null;

        if (buffer == null || offset < 0 || offset >= buffer.Length)
            return 0;

        var available = buffer.Length - offset;
        if (available < MinEventSize)
            return 0; // Not enough data

        var eventType = (ArduinoEventType)buffer[offset];
        var dataLength = (ushort)(buffer[offset + 1] | (buffer[offset + 2] << 8));

        var totalSize = MinEventSize + dataLength;
        if (available < totalSize)
            return 0; // Not enough data for complete event

        var dataStart = offset + 3;
        var timestampStart = dataStart + dataLength;
        var checksumPos = timestampStart + 4;

        // Extract data
        var data = new byte[dataLength];
        if (dataLength > 0)
        {
            Array.Copy(buffer, dataStart, data, 0, dataLength);
        }

        // Extract timestamp
        var timestamp = (uint)(
            buffer[timestampStart] |
            (buffer[timestampStart + 1] << 8) |
            (buffer[timestampStart + 2] << 16) |
            (buffer[timestampStart + 3] << 24)
        );

        // Verify checksum
        var checksum = buffer[checksumPos];
        var calculatedChecksum = ArduinoProtocolEncoder.CalculateChecksum(
            buffer.Skip(offset).Take(totalSize - 1).ToArray()
        );

        if (checksum != calculatedChecksum)
        {
            // Checksum mismatch - skip this byte and try again
            return 1;
        }

        decodedEvent = new DecodedArduinoEvent
        {
            EventType = eventType,
            Data = data,
            Timestamp = timestamp
        };

        return totalSize;
    }
}

/// <summary>
/// Represents a decoded event from Arduino.
/// </summary>
public sealed class DecodedArduinoEvent
{
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public ArduinoEventType EventType { get; init; }

    /// <summary>
    /// Gets the event data.
    /// </summary>
    public byte[] Data { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Gets the timestamp (milliseconds since Arduino boot).
    /// </summary>
    public uint Timestamp { get; init; }
}
