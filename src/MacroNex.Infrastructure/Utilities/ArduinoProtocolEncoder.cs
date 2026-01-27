using MacroNex.Domain.ValueObjects;

namespace MacroNex.Infrastructure.Utilities;

/// <summary>
/// Encodes Arduino commands into binary protocol format.
/// Protocol format: [CommandType: 1 byte][DataLength: 2 bytes][Data: N bytes][Checksum: 1 byte]
/// </summary>
public static class ArduinoProtocolEncoder
{
    /// <summary>
    /// Encodes a command into binary protocol format.
    /// </summary>
    /// <param name="command">The command to encode.</param>
    /// <returns>The encoded command as a byte array.</returns>
    public static byte[] EncodeCommand(ArduinoCommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        var data = command.Serialize();
        var dataLength = (ushort)data.Length;

        // Protocol: [CommandType: 1 byte][DataLength: 2 bytes][Data: N bytes][Checksum: 1 byte]
        var buffer = new List<byte>
        {
            (byte)command.CommandType,
            (byte)(dataLength & 0xFF),
            (byte)((dataLength >> 8) & 0xFF)
        };

        buffer.AddRange(data);

        // Calculate checksum (simple XOR of all bytes)
        byte checksum = 0;
        foreach (var b in buffer)
        {
            checksum ^= b;
        }
        buffer.Add(checksum);

        return buffer.ToArray();
    }

    /// <summary>
    /// Calculates the checksum for a byte array.
    /// </summary>
    /// <param name="data">The data to calculate checksum for.</param>
    /// <returns>The checksum byte.</returns>
    public static byte CalculateChecksum(byte[] data)
    {
        if (data == null || data.Length == 0)
            return 0;

        byte checksum = 0;
        foreach (var b in data)
        {
            checksum ^= b;
        }
        return checksum;
    }
}
