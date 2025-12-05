using UnityEngine;
using NetUMP;

/// <summary>
/// Example usage of NetUMP in Unity
/// Demonstrates sending and receiving MIDI 2.0 UMP messages
/// </summary>
public class NetUMPFirstTest : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetUMPWrapper netUMP;

    [Header("Test Controls")]
    [SerializeField] private KeyCode sendNoteOnKey = KeyCode.Space;
    [SerializeField] private byte testNote = 60; // Middle C
    [SerializeField] private byte testVelocity = 100;
    [SerializeField] private byte testChannel = 0;

    private void Start()
    {
        // Get or create NetUMP wrapper
        if (netUMP == null)
        {
            // Each GameObject can have its own NetUMP connection
            netUMP = GetComponent<NetUMPWrapper>();
            if (netUMP == null)
            {
                netUMP = gameObject.AddComponent<NetUMPWrapper>();
            }
        }

        // Subscribe to events
        netUMP.OnUMPMessageReceived += HandleUMPMessage;
        netUMP.OnConnected += HandleConnected;
        netUMP.OnDisconnected += HandleDisconnected;
        
        Debug.Log($"NetUMP Example initialized on {gameObject.name}");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (netUMP != null)
        {
            netUMP.OnUMPMessageReceived -= HandleUMPMessage;
            netUMP.OnConnected -= HandleConnected;
            netUMP.OnDisconnected -= HandleDisconnected;
        }
    }

    private void Update()
    {
        // Test: Send note on when space is pressed
        if (Input.GetKeyDown(sendNoteOnKey))
        {
            SendNoteOn(testChannel, testNote, testVelocity);
        }

        // Test: Send note off when space is released
        if (Input.GetKeyUp(sendNoteOnKey))
        {
            SendNoteOff(testChannel, testNote);
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Session Status: {netUMP.GetSessionStatusString()}");
        GUILayout.Label($"Press {sendNoteOnKey} to send Note On/Off");
        GUILayout.EndArea();
    }

    #region Event Handlers

    private void HandleUMPMessage(byte[] data)
    {
        int mt = NetUMPWrapper.GetMessageType(data);
        Debug.Log($"Received UMP message - MT: {mt:X}, Length: {data.Length} bytes");

        // Parse based on message type
        switch (mt)
        {
            case 0x2: // MIDI 1.0 Channel Voice Message
                ParseMIDI1Message(data);
                break;
            case 0x4: // MIDI 2.0 Channel Voice Message
                ParseMIDI2Message(data);
                break;
            default:
                Debug.Log($"Unhandled message type: 0x{mt:X}");
                break;
        }
    }

    private void HandleConnected(string endpointName)
    {
        Debug.Log($"<color=green>Connected to: {endpointName}</color>");
    }

    private void HandleDisconnected()
    {
        Debug.Log("<color=red>Disconnected</color>");
    }

    #endregion

    #region Send MIDI Messages

    /// <summary>
    /// Send MIDI 2.0 Note On message
    /// </summary>
    public void SendNoteOn(byte channel, byte note, byte velocity)
    {
        byte[] ump = new byte[8];

        uint word1 = (4u << 28)          // MT = 4 (MIDI 2.0 Channel Voice)
                | (0u << 24)          // Group = 0 (can be changed if needed)
                | (0x9u << 20)        // Message Type = Note On (9)
                | ((uint)channel << 16) // Channel (0-15)
                | note;               // Note number (0-127)

        // Scale velocity from 7-bit to 16-bit by replicating bits:
        ushort velocity16 = (ushort)(velocity * 0x101);
        uint word2 = velocity16;         // Velocity in bits 0-15; rest 0

        BitConverter.GetBytes(word1).CopyTo(ump, 0);
        BitConverter.GetBytes(word2).CopyTo(ump, 4);

        if (netUMP.SendUMP(ump))
            Debug.Log($"Sent Note On: Ch={channel}, Note={note}, Vel={velocity}");
        else
            Debug.LogWarning("Failed to send Note On");
    }

    /// <summary>
    /// Send MIDI 2.0 Note Off message
    /// </summary>
    public void SendNoteOff(byte channel, byte note)
    {
        byte[] ump = new byte[8];

        uint word1 = (4u << 28)          // MT = 4
                | (0u << 24)          // Group = 0
                | (0x8u << 20)        // Message Type = Note Off (8)
                | ((uint)channel << 16)
                | note;

        uint word2 = 0;                 // Velocity = 0 for Note Off

        BitConverter.GetBytes(word1).CopyTo(ump, 0);
        BitConverter.GetBytes(word2).CopyTo(ump, 4);

        if (netUMP.SendUMP(ump))
            Debug.Log($"Sent Note Off: Ch={channel}, Note={note}");
        else
            Debug.LogWarning("Failed to send Note Off");
    }

    /// <summary>
    /// Send MIDI 2.0 Control Change message
    /// </summary>
    public void SendControlChange(byte channel, byte controller, uint value)
    {
        byte[] ump = new byte[8];

        uint word1 = (4u << 28)            // MT = 4
                | (0u << 24)            // Group = 0
                | (0xBu << 20)          // Message Type = Control Change (B)
                | ((uint)channel << 16)
                | controller;           // Controller number

        uint word2 = value;                // 32-bit 14-bit or 32-bit value depending on controller

        BitConverter.GetBytes(word1).CopyTo(ump, 0);
        BitConverter.GetBytes(word2).CopyTo(ump, 4);

        if (netUMP.SendUMP(ump))
            Debug.Log($"Sent Control Change: Ch={channel}, Ctrl={controller}, Value={value}");
        else
            Debug.LogWarning("Failed to send Control Change");
    }


    #endregion

    #region Parse MIDI Messages

    private void ParseMIDI1Message(byte[] data)
    {
        if (data.Length < 4) return;

        uint word = System.BitConverter.ToUInt32(data, 0);
        
        byte status = (byte)((word >> 8) & 0xFF);
        byte statusType = (byte)(status & 0xF0);
        byte channel = (byte)(status & 0x0F);
        byte data1 = (byte)(word & 0xFF);
        byte data2 = (byte)((word >> 16) & 0xFF);

        switch (statusType)
        {
            case 0x90: // Note On
                Debug.Log($"MIDI 1.0 Note On - Ch:{channel}, Note:{data1}, Vel:{data2}");
                break;
            case 0x80: // Note Off
                Debug.Log($"MIDI 1.0 Note Off - Ch:{channel}, Note:{data1}");
                break;
            case 0xB0: // Control Change
                Debug.Log($"MIDI 1.0 CC - Ch:{channel}, CC:{data1}, Val:{data2}");
                break;
        }
    }

    private void ParseMIDI2Message(byte[] data)
    {
        if (data.Length < 8) return;

        uint word1 = System.BitConverter.ToUInt32(data, 0);
        uint word2 = System.BitConverter.ToUInt32(data, 4);
        
        byte status = (byte)((word1 >> 8) & 0xFF);
        byte statusType = (byte)(status & 0xF0);
        byte channel = (byte)((word1 >> 16) & 0x0F);
        byte data1 = (byte)(word1 & 0xFF);

        switch (statusType)
        {
            case 0x90: // Note On
                ushort velocity = (ushort)(word2 >> 16);
                Debug.Log($"MIDI 2.0 Note On - Ch:{channel}, Note:{data1}, Vel:{velocity}");
                break;
            case 0x80: // Note Off
                Debug.Log($"MIDI 2.0 Note Off - Ch:{channel}, Note:{data1}");
                break;
            case 0xB0: // Control Change
                Debug.Log($"MIDI 2.0 CC - Ch:{channel}, CC:{data1}, Val:{word2}");
                break;
        }
    }

    #endregion
}