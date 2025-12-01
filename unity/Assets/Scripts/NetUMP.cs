using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace NetUMP
{
    /// <summary>
    /// Unity wrapper for NetUMP library
    /// Handles MIDI 2.0 UMP (Universal MIDI Packet) networking
    /// Each instance can have its own independent connection
    /// </summary>
    public class NetUMPWrapper : MonoBehaviour
    {
        #region Native Function Delegates

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void UMPMessageCallbackDelegate(IntPtr instance, IntPtr data, int length);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ConnectionEventCallbackDelegate(IntPtr instance, IntPtr endpointName, int nameLength);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void DisconnectionEventCallbackDelegate(IntPtr instance);

        #endregion

        #region Native DLL Imports

        private const string DLL_NAME = "netump";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr NetUMP_Create(string localEndpointName, string productInstanceId);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_Start(
            IntPtr instance,
            string remoteHost,
            ushort localPort,
            ushort remotePort,
            bool isInitiator);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void NetUMP_Stop(IntPtr instance);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void NetUMP_Destroy(IntPtr instance);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool NetUMP_SendUMP(IntPtr instance, byte[] data, int length);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_PollMessages(IntPtr instance);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_GetNextMessage(IntPtr instance, byte[] buffer, int bufferSize);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_GetSessionStatus(IntPtr instance);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool NetUMP_ReadAndResetConnectionLost(IntPtr instance);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void NetUMP_SetMessageCallback(IntPtr instance, UMPMessageCallbackDelegate callback);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void NetUMP_SetConnectionCallback(IntPtr instance, ConnectionEventCallbackDelegate callback);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void NetUMP_SetDisconnectionCallback(IntPtr instance, DisconnectionEventCallbackDelegate callback);

        #endregion

        #region Public Events

        /// <summary>
        /// Fired when a UMP message is received from the network
        /// </summary>
        public event Action<byte[]> OnUMPMessageReceived;

        /// <summary>
        /// Fired when connection is established
        /// </summary>
        public event Action<string> OnConnected;

        /// <summary>
        /// Fired when connection is lost or closed
        /// </summary>
        public event Action OnDisconnected;

        #endregion

        #region Configuration

        [Header("NetUMP Configuration")]
        [SerializeField] private string localEndpointName = "UnityNetUMP";
        [SerializeField] private string productInstanceId = "Unity_001";
        [SerializeField] private string remoteHost = "127.0.0.1";
        [SerializeField] private ushort localPort = 5555;
        [SerializeField] private ushort remotePort = 5555;
        [SerializeField] private bool isInitiator = true;
        [SerializeField] private bool autoInitialize = true;

        #endregion

        #region Private Fields

        private IntPtr nativeHandle = IntPtr.Zero;
        private bool isRunning = false;
        private byte[] receiveBuffer = new byte[16]; // Max UMP message size

        // Keep delegates alive to prevent garbage collection
        private UMPMessageCallbackDelegate messageCallbackDelegate;
        private ConnectionEventCallbackDelegate connectionCallbackDelegate;
        private DisconnectionEventCallbackDelegate disconnectionCallbackDelegate;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Create and store delegates to prevent GC
            messageCallbackDelegate = OnNativeMessageReceived;
            connectionCallbackDelegate = OnNativeConnected;
            disconnectionCallbackDelegate = OnNativeDisconnected;
        }

        private void Start()
        {
            if (autoInitialize)
            {
                Initialize();
            }
        }

        private void Update()
        {
            if (nativeHandle == IntPtr.Zero)
                return;

            // Poll for messages from the native queue
            int messageCount = NetUMP_PollMessages(nativeHandle);
            for (int i = 0; i < messageCount; i++)
            {
                int length = NetUMP_GetNextMessage(nativeHandle, receiveBuffer, receiveBuffer.Length);
                if (length > 0)
                {
                    byte[] message = new byte[length];
                    Array.Copy(receiveBuffer, message, length);
                    OnUMPMessageReceived?.Invoke(message);
                }
            }

            // Check for connection loss
            if (NetUMP_ReadAndResetConnectionLost(nativeHandle))
            {
                Debug.LogWarning($"[{gameObject.name}] NetUMP: Connection lost");
                OnDisconnected?.Invoke();
            }
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initialize NetUMP connection
        /// </summary>
        public bool Initialize()
        {
            if (nativeHandle != IntPtr.Zero)
            {
                Debug.LogWarning($"[{gameObject.name}] NetUMP: Already initialized");
                return true;
            }

            Debug.Log($"[{gameObject.name}] NetUMP: Creating instance");

            // Create native instance
            nativeHandle = NetUMP_Create(localEndpointName, productInstanceId);
            if (nativeHandle == IntPtr.Zero)
            {
                Debug.LogError($"[{gameObject.name}] NetUMP: Failed to create native instance");
                return false;
            }

            // Set native callbacks
            NetUMP_SetMessageCallback(nativeHandle, messageCallbackDelegate);
            NetUMP_SetConnectionCallback(nativeHandle, connectionCallbackDelegate);
            NetUMP_SetDisconnectionCallback(nativeHandle, disconnectionCallbackDelegate);

            Debug.Log($"[{gameObject.name}] NetUMP: Starting connection to {remoteHost}:{remotePort}");

            // Start session
            int result = NetUMP_Start(
                nativeHandle,
                remoteHost,
                localPort,
                remotePort,
                isInitiator);

            if (result < 0)
            {
                Debug.LogError($"[{gameObject.name}] NetUMP: Start failed with code {result}");
                NetUMP_Destroy(nativeHandle);
                nativeHandle = IntPtr.Zero;
                return false;
            }

            isRunning = true;
            Debug.Log($"[{gameObject.name}] NetUMP: Initialized successfully");
            return true;
        }

        /// <summary>
        /// Shutdown NetUMP connection
        /// </summary>
        public void Shutdown()
        {
            if (nativeHandle == IntPtr.Zero)
                return;

            Debug.Log($"[{gameObject.name}] NetUMP: Shutting down");

            if (isRunning)
            {
                NetUMP_Stop(nativeHandle);
                isRunning = false;
            }

            NetUMP_Destroy(nativeHandle);
            nativeHandle = IntPtr.Zero;
        }

        /// <summary>
        /// Restart the connection (stop and start with current settings)
        /// </summary>
        public bool Restart()
        {
            if (nativeHandle == IntPtr.Zero)
            {
                return Initialize();
            }

            Debug.Log($"[{gameObject.name}] NetUMP: Restarting connection");

            NetUMP_Stop(nativeHandle);

            int result = NetUMP_Start(
                nativeHandle,
                remoteHost,
                localPort,
                remotePort,
                isInitiator);

            if (result < 0)
            {
                Debug.LogError($"[{gameObject.name}] NetUMP: Restart failed with code {result}");
                return false;
            }

            isRunning = true;
            return true;
        }

        /// <summary>
        /// Send a UMP message over the network
        /// </summary>
        /// <param name="data">UMP message data (4-16 bytes)</param>
        /// <returns>True if message was queued successfully</returns>
        public bool SendUMP(byte[] data)
        {
            if (nativeHandle == IntPtr.Zero)
            {
                Debug.LogWarning($"[{gameObject.name}] NetUMP: Cannot send - not initialized");
                return false;
            }

            if (data == null || data.Length < 4 || data.Length > 16)
            {
                Debug.LogError($"[{gameObject.name}] NetUMP: Invalid UMP message size: {data?.Length ?? 0}");
                return false;
            }

            return NetUMP_SendUMP(nativeHandle, data, data.Length);
        }

        /// <summary>
        /// Get current session status
        /// </summary>
        /// <returns>0=closed, 1=inviting, 2=waiting, 3=opened</returns>
        public int GetSessionStatus()
        {
            return (nativeHandle != IntPtr.Zero) ? NetUMP_GetSessionStatus(nativeHandle) : 0;
        }

        /// <summary>
        /// Get session status as string
        /// </summary>
        public string GetSessionStatusString()
        {
            switch (GetSessionStatus())
            {
                case 0: return "Closed";
                case 1: return "Inviting";
                case 2: return "Waiting";
                case 3: return "Opened";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Check if this instance is initialized
        /// </summary>
        public bool IsInitialized => nativeHandle != IntPtr.Zero;

        /// <summary>
        /// Check if session is running
        /// </summary>
        public bool IsRunning => isRunning;

        #endregion

        #region Native Callbacks

        private void OnNativeMessageReceived(IntPtr instance, IntPtr data, int length)
        {
            // Verify this callback is for our instance
            if (instance != nativeHandle)
                return;

            // This is called from native thread - do minimal work
            // Actual processing happens in Update() via polling
        }

        private void OnNativeConnected(IntPtr instance, IntPtr endpointName, int nameLength)
        {
            // Verify this callback is for our instance
            if (instance != nativeHandle)
                return;

            try
            {
                string name = Marshal.PtrToStringAnsi(endpointName, nameLength);
                Debug.Log($"[{gameObject.name}] NetUMP: Connected to endpoint: {name}");
                
                // Queue event for Unity main thread
                UnityMainThreadDispatcher.Enqueue(() => OnConnected?.Invoke(name));
            }
            catch (Exception e)
            {
                Debug.LogError($"[{gameObject.name}] NetUMP: Error in connection callback: {e}");
            }
        }

        private void OnNativeDisconnected(IntPtr instance)
        {
            // Verify this callback is for our instance
            if (instance != nativeHandle)
                return;

            Debug.Log($"[{gameObject.name}] NetUMP: Disconnected");
            
            // Queue event for Unity main thread
            UnityMainThreadDispatcher.Enqueue(() => OnDisconnected?.Invoke());
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Convert UMP message bytes to uint32 words for inspection
        /// </summary>
        public static uint[] BytesToWords(byte[] data)
        {
            int wordCount = data.Length / 4;
            uint[] words = new uint[wordCount];
            
            for (int i = 0; i < wordCount; i++)
            {
                words[i] = BitConverter.ToUInt32(data, i * 4);
            }
            
            return words;
        }

        /// <summary>
        /// Get the Message Type (MT) from UMP data
        /// </summary>
        public static int GetMessageType(byte[] data)
        {
            if (data == null || data.Length < 4)
                return -1;
                
            uint firstWord = BitConverter.ToUInt32(data, 0);
            return (int)((firstWord >> 28) & 0xF);
        }

        #endregion
    }

    #region Unity Main Thread Dispatcher

    /// <summary>
    /// Helper class to execute callbacks on Unity main thread
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher instance;
        private static readonly System.Collections.Generic.Queue<Action> executionQueue = 
            new System.Collections.Generic.Queue<Action>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (instance == null)
            {
                var go = new GameObject("UnityMainThreadDispatcher");
                instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
        }

        public static void Enqueue(Action action)
        {
            if (action == null)
                return;

            lock (executionQueue)
            {
                executionQueue.Enqueue(action);
            }
        }

        private void Update()
        {
            lock (executionQueue)
            {
                while (executionQueue.Count > 0)
                {
                    executionQueue.Dequeue()?.Invoke();
                }
            }
        }
    }

    #endregion
}