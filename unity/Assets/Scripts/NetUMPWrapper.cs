using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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

        [Header("NetworkSettings")]
        [SerializeField] private NetworkSettings networkSettings;

        #region Native Delegates

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void UMPMessageCallbackDelegate(
            IntPtr instance,
            IntPtr data,
            int length);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ConnectionEventCallbackDelegate(
            IntPtr instance,
            IntPtr endpointName,
            int nameLength);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void DisconnectionEventCallbackDelegate(
            IntPtr instance);

        #endregion

        #region DLL Imports

        private const string DLL_NAME = "netump";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr NetUMP_Create(
            string localEndpointName,
            string productInstanceId);

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
        private static extern bool NetUMP_SendUMP(
            IntPtr instance,
            byte[] data,
            int length);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_PollMessages(IntPtr instance);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_GetNextMessage(
            IntPtr instance,
            byte[] buffer,
            int bufferSize);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_GetSessionStatus(IntPtr instance);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool NetUMP_ReadAndResetConnectionLost(
            IntPtr instance);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void NetUMP_SetMessageCallback(
            IntPtr instance,
            UMPMessageCallbackDelegate callback);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void NetUMP_SetConnectionCallback(
            IntPtr instance,
            ConnectionEventCallbackDelegate callback);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void NetUMP_SetDisconnectionCallback(
            IntPtr instance,
            DisconnectionEventCallbackDelegate callback);

        #endregion

        #region Events

        public event Action<byte[]> OnUMPMessageReceived;
        public event Action<string> OnConnected;
        public event Action OnDisconnected;

        #endregion

        #region Config

        [Header("NetUMP Configuration")]
        [SerializeField] private string localEndpointName = "UnityNetUMP";
        [SerializeField] private string productInstanceId = "Unity_001";
        [SerializeField] private string remoteHost = "127.0.0.1";

        [SerializeField] private ushort localPort = 5555;
        [SerializeField] private ushort remotePort = 5556;

        public bool isInitiator = true;
        public bool autoInitialize = true;

        public bool isDebugging = false;

        #endregion

        #region Private Fields

        private IntPtr nativeLocalInstance = IntPtr.Zero;
        private bool isRunning = false;

        private readonly byte[] receiveBuffer = new byte[16];

        // IMPORTANT: static delegates
        private static UMPMessageCallbackDelegate messageCallbackDelegate;
        private static ConnectionEventCallbackDelegate connectionCallbackDelegate;
        private static DisconnectionEventCallbackDelegate disconnectionCallbackDelegate;

        // Map native pointers to wrapper instances
        private static readonly Dictionary<IntPtr, NetUMPWrapper> instances =
            new Dictionary<IntPtr, NetUMPWrapper>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            remoteHost = networkSettings.IpAddress;
            remotePort = Convert.ToUInt16(networkSettings.Port);

            // Create delegates once
            messageCallbackDelegate ??= OnNativeMessageReceived;
            connectionCallbackDelegate ??= OnNativeConnected;
            disconnectionCallbackDelegate ??= OnNativeDisconnected;
        }

        private void Start()
        {
            if (autoInitialize)
            {
                Initialize();
            }
        }

        public void InitiatorIs()
        {
            isInitiator = true;
        }

        public void InitializeIs()
        {
            // Prevent double init
            autoInitialize = false;

            Initialize();
        }

        private void Update()
        {
            if (nativeLocalInstance == IntPtr.Zero)
                return;

            // REQUIRED FOR SESSION PROGRESSION
            int messageCount = NetUMP_PollMessages(nativeLocalInstance);

            for (int i = 0; i < messageCount; i++)
            {
                int length = NetUMP_GetNextMessage(
                    nativeLocalInstance,
                    receiveBuffer,
                    receiveBuffer.Length);

                if (length > 0)
                {
                    byte[] message = new byte[length];
                    Array.Copy(receiveBuffer, message, length);

                    OnUMPMessageReceived?.Invoke(message);
                }
            }

            if (NetUMP_ReadAndResetConnectionLost(nativeLocalInstance))
            {
                Debug.LogWarning($"[{gameObject.name}] Connection lost");
                OnDisconnected?.Invoke();
            }

            int status = NetUMP_GetSessionStatus(nativeLocalInstance);

            if (isDebugging)
            {
                Debug.Log(
                $"[{gameObject.name}] Status={status} " +
                $"Ptr={nativeLocalInstance}");
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

        #region Public API

        public bool Initialize()
        {
            if (nativeLocalInstance != IntPtr.Zero)
            {
                Debug.LogWarning(
                    $"[{gameObject.name}] Already initialized");

                return true;
            }

            Debug.Log($"[{gameObject.name}] Creating NetUMP instance");

            nativeLocalInstance = NetUMP_Create(
                localEndpointName,
                productInstanceId);

            if (nativeLocalInstance == IntPtr.Zero)
            {
                Debug.LogError(
                    $"[{gameObject.name}] Failed to create instance");

                return false;
            }

            // Register instance
            instances[nativeLocalInstance] = this;

            NetUMP_SetMessageCallback(
                nativeLocalInstance,
                messageCallbackDelegate);

            NetUMP_SetConnectionCallback(
                nativeLocalInstance,
                connectionCallbackDelegate);

            NetUMP_SetDisconnectionCallback(
                nativeLocalInstance,
                disconnectionCallbackDelegate);

            Debug.Log(
                $"[{gameObject.name}] Starting session " +
                $"{remoteHost}:{remotePort}");

            int result = NetUMP_Start(
                nativeLocalInstance,
                remoteHost,
                localPort,
                remotePort,
                isInitiator);

            if (result < 0)
            {
                Debug.LogError(
                    $"[{gameObject.name}] Start failed: {result}");

                instances.Remove(nativeLocalInstance);

                NetUMP_Destroy(nativeLocalInstance);

                nativeLocalInstance = IntPtr.Zero;

                return false;
            }

            isRunning = true;

            Debug.Log($"[{gameObject.name}] NetUMP initialized");

            return true;
        }

        public void Shutdown()
        {
            if (nativeLocalInstance == IntPtr.Zero)
                return;

            Debug.Log($"[{gameObject.name}] Shutdown");

            if (isRunning)
            {
                NetUMP_Stop(nativeLocalInstance);
                isRunning = false;
            }

            instances.Remove(nativeLocalInstance);

            NetUMP_Destroy(nativeLocalInstance);

            nativeLocalInstance = IntPtr.Zero;
        }

        public bool SendUMP(byte[] data)
        {
            if (nativeLocalInstance == IntPtr.Zero)
            {
                Debug.LogWarning("Cannot send - not initialized");
                return false;
            }

            if (data == null || data.Length < 4 || data.Length > 16)
            {
                Debug.LogError("Invalid UMP message size");
                return false;
            }

            return NetUMP_SendUMP(
                nativeLocalInstance,
                data,
                data.Length);
        }

        public int GetSessionStatus()
        {
            if (nativeLocalInstance == IntPtr.Zero)
                return 0;

            return NetUMP_GetSessionStatus(nativeLocalInstance);
        }

        public string GetSessionStatusString()
        {
            return GetSessionStatus() switch
            {
                0 => "Closed",
                1 => "Inviting",
                3 => "Opened",
                _ => "Unknown"
            };
        }

        public bool IsInitialized =>
            nativeLocalInstance != IntPtr.Zero;

        public bool IsRunning => isRunning;

        #endregion

        #region Native Callbacks

        [AOT.MonoPInvokeCallback(typeof(UMPMessageCallbackDelegate))]
        private static void OnNativeMessageReceived(
            IntPtr instance,
            IntPtr data,
            int length)
        {
            if (!instances.TryGetValue(instance, out var wrapper))
                return;

            // Polling handles actual message retrieval
        }

        [AOT.MonoPInvokeCallback(typeof(ConnectionEventCallbackDelegate))]
        private static void OnNativeConnected(
            IntPtr instance,
            IntPtr endpointName,
            int nameLength)
        {
            if (!instances.TryGetValue(instance, out var wrapper))
                return;

            try
            {
                string name =
                    Marshal.PtrToStringAnsi(endpointName, nameLength);

                Debug.Log(
                    $"[{wrapper.gameObject.name}] Connected: {name}");

                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    wrapper.OnConnected?.Invoke(name);
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"Connection callback error: {e}");
            }
        }

        [AOT.MonoPInvokeCallback(typeof(DisconnectionEventCallbackDelegate))]
        private static void OnNativeDisconnected(
            IntPtr instance)
        {
            if (!instances.TryGetValue(instance, out var wrapper))
                return;

            Debug.Log(
                $"[{wrapper.gameObject.name}] Disconnected");

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                wrapper.OnDisconnected?.Invoke();
            });
        }



        #endregion

        #region Helper Methods

        /// <summary>
        /// Convert UMP message bytes to uint32 words
        /// </summary>
        public static uint[] BytesToWords(byte[] data)
        {
            if (data == null)
                return Array.Empty<uint>();

            int wordCount = data.Length / 4;

            uint[] words = new uint[wordCount];

            for (int i = 0; i < wordCount; i++)
            {
                words[i] = BitConverter.ToUInt32(data, i * 4);
            }

            return words;
        }

        /// <summary>
        /// Get UMP Message Type (MT)
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



    #region Main Thread Dispatcher

    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher instance;

        private static readonly Queue<Action> executionQueue =
            new Queue<Action>();

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (instance != null)
                return;

            GameObject go =
                new GameObject("UnityMainThreadDispatcher");

            instance =
                go.AddComponent<UnityMainThreadDispatcher>();

            DontDestroyOnLoad(go);
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