using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace NetUMP
{
    /// <summary>
    /// Session mode for NetUMP endpoint
    /// </summary>
    public enum SessionMode
    {
        Initiator = 0,
        Listener = 1
    }

    /// <summary>
    /// Status codes returned by NetUMP operations
    /// </summary>
    public enum NetUMPStatus
    {
        Success = 0,
        Error = -1,
        InvalidHandle = -2,
        NotInitialized = -3,
        AlreadyRunning = -4,
        NetworkError = -5
    }

    /// <summary>
    /// Callback delegate for receiving UMP packets
    /// </summary>
    /// <param name="data">Pointer to packet data</param>
    /// <param name="length">Length of packet in bytes</param>
    /// <param name="timestamp">Packet timestamp</param>
    /// <param name="userData">User-defined context pointer</param>
    public delegate void UMPPacketCallback(IntPtr data, int length, long timestamp, IntPtr userData);

    /// <summary>
    /// Unity wrapper for NetUMP C++ library
    /// </summary>
    public class NetUMPEndpoint : IDisposable
    {
        #region Native Imports

#if UNITY_ANDROID && !UNITY_EDITOR
        private const string LIBRARY_NAME = "netump";
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const string LIBRARY_NAME = "netump";
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        private const string LIBRARY_NAME = "netump";
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private const string LIBRARY_NAME = "netump";
#else
        private const string LIBRARY_NAME = "netump";
#endif

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr NetUMP_CreateEndpoint();

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_DestroyEndpoint(IntPtr handle);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_InitiateSession(IntPtr handle, string destIP, ushort destPort, ushort localPort, bool isInitiator);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_CloseSession(IntPtr handle);
        
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_SetEndpointName(IntPtr handle, string name);
        
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_SetProductInstanceID(IntPtr handle, string piid);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_RunSession(IntPtr handle);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_SendPacket(IntPtr handle, IntPtr data, int length);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_SetReceiveCallback(IntPtr handle, UMPPacketCallback callback, IntPtr userData);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetUMP_IsConnected(IntPtr handle);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr NetUMP_GetLastError(IntPtr handle);

        #endregion

        #region Private Fields

        private IntPtr nativeHandle;
        private bool disposed = false;
        private UMPPacketCallback nativeCallback;
        private System.Threading.Thread sessionThread;
        private volatile bool isRunning = false;
        private Action<byte[], long> onPacketReceived;

        #endregion

        #region Public Properties

        public bool IsConnected
        {
            get
            {
                if (disposed || nativeHandle == IntPtr.Zero)
                    return false;
                return NetUMP_IsConnected(nativeHandle) != 0;
            }
        }

        public bool IsRunning => isRunning;

        #endregion

        #region Constructor / Destructor

        /// <summary>
        /// Creates a new NetUMP endpoint
        /// </summary>
        /// <param name="mode">Session mode (Initiator or Listener)</param>
        /// <param name="ipAddress">IP address for connection</param>
        /// <param name="port">Network port</param>
        public NetUMPEndpoint(SessionMode mode, string ipAddress, int port)
        {
            nativeHandle = NetUMP_CreateEndpoint((int)mode, ipAddress, port);
            
            if (nativeHandle == IntPtr.Zero)
            {
                throw new Exception("Failed to create NetUMP endpoint");
            }

            // Create native callback wrapper
            nativeCallback = new UMPPacketCallback(OnNativePacketReceived);
            NetUMP_SetReceiveCallback(nativeHandle, nativeCallback, IntPtr.Zero);
        }

        ~NetUMPEndpoint()
        {
            Dispose(false);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initialize the endpoint
        /// </summary>
        public void Initialize()
        {
            ThrowIfDisposed();
            
            int result = NetUMP_Initialize(nativeHandle);
            if (result != (int)NetUMPStatus.Success)
            {
                throw new Exception($"Failed to initialize NetUMP endpoint: {GetLastError()}");
            }
        }

        /// <summary>
        /// Start the session thread
        /// </summary>
        public void StartSession()
        {
            ThrowIfDisposed();

            if (isRunning)
            {
                Debug.LogWarning("Session is already running");
                return;
            }

            isRunning = true;
            sessionThread = new System.Threading.Thread(SessionThreadProc)
            {
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.Highest,
                Name = "NetUMP Session Thread"
            };
            sessionThread.Start();
        }

        /// <summary>
        /// Stop the session thread
        /// </summary>
        public void StopSession()
        {
            if (!isRunning)
                return;

            isRunning = false;
            
            if (sessionThread != null && sessionThread.IsAlive)
            {
                sessionThread.Join(2000); // Wait up to 2 seconds
            }
        }

        /// <summary>
        /// Send a UMP packet
        /// </summary>
        /// <param name="data">Packet data</param>
        public void SendPacket(byte[] data)
        {
            ThrowIfDisposed();

            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Packet data cannot be null or empty");
            }

            IntPtr unmanagedArray = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, unmanagedArray, data.Length);
                int result = NetUMP_SendPacket(nativeHandle, unmanagedArray, data.Length);
                
                if (result != (int)NetUMPStatus.Success)
                {
                    throw new Exception($"Failed to send packet: {GetLastError()}");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(unmanagedArray);
            }
        }

        /// <summary>
        /// Set callback for received packets
        /// </summary>
        /// <param name="callback">Callback action that receives packet data and timestamp</param>
        public void SetPacketReceivedCallback(Action<byte[], long> callback)
        {
            onPacketReceived = callback;
        }

        /// <summary>
        /// Get the last error message
        /// </summary>
        public string GetLastError()
        {
            if (nativeHandle == IntPtr.Zero)
                return "Invalid handle";

            IntPtr errorPtr = NetUMP_GetLastError(nativeHandle);
            if (errorPtr == IntPtr.Zero)
                return "No error";

            return Marshal.PtrToStringAnsi(errorPtr);
        }

        #endregion

        #region Private Methods

        private void SessionThreadProc()
        {
            // Set thread to high priority for accurate timing
            System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            long nextTick = 1; // 1ms interval

            while (isRunning)
            {
                // Call RunSession approximately every 1ms
                if (nativeHandle != IntPtr.Zero)
                {
                    NetUMP_RunSession(nativeHandle);
                }

                // Sleep until next millisecond
                long currentMs = stopwatch.ElapsedMilliseconds;
                long sleepTime = nextTick - currentMs;
                
                if (sleepTime > 0)
                {
                    System.Threading.Thread.Sleep((int)sleepTime);
                }

                nextTick = stopwatch.ElapsedMilliseconds + 1;
            }
        }

        private void OnNativePacketReceived(IntPtr data, int length, long timestamp, IntPtr userData)
        {
            if (onPacketReceived == null || data == IntPtr.Zero || length <= 0)
                return;

            try
            {
                // Copy unmanaged data to managed array
                byte[] managedData = new byte[length];
                Marshal.Copy(data, managedData, 0, length);

                // Invoke callback on Unity main thread if needed
                if (UnityEngine.Application.isPlaying)
                {
                    UnityMainThreadDispatcher.Instance.Enqueue(() => 
                    {
                        onPacketReceived?.Invoke(managedData, timestamp);
                    });
                }
                else
                {
                    onPacketReceived?.Invoke(managedData, timestamp);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in packet received callback: {ex.Message}");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(NetUMPEndpoint));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                StopSession();
            }

            if (nativeHandle != IntPtr.Zero)
            {
                NetUMP_Shutdown(nativeHandle);
                NetUMP_DestroyEndpoint(nativeHandle);
                nativeHandle = IntPtr.Zero;
            }

            disposed = true;
        }

        #endregion
    }

    /// <summary>
    /// Unity main thread dispatcher for handling callbacks
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher instance;
        private readonly System.Collections.Generic.Queue<Action> executionQueue = new System.Collections.Generic.Queue<Action>();

        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (instance == null)
                {
                    var obj = new GameObject("UnityMainThreadDispatcher");
                    instance = obj.AddComponent<UnityMainThreadDispatcher>();
                    DontDestroyOnLoad(obj);
                }
                return instance;
            }
        }

        public void Enqueue(Action action)
        {
            lock (executionQueue)
            {
                executionQueue.Enqueue(action);
            }
        }

        void Update()
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
}