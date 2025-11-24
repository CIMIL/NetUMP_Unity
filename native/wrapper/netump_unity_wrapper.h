// netump_unity_wrapper.h
// Unity C++ wrapper for bbouchez's NetUMP library
// https://github.com/bbouchez/NetUMP

#ifndef NETUMP_UNITY_WRAPPER_H
#define NETUMP_UNITY_WRAPPER_H

#ifdef _WIN32
    #define EXPORT_API __declspec(dllexport)
#else
    #define EXPORT_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

// Callback function pointer type for received UMP packets
typedef void (*UMPPacketCallbackFunc)(const void* data, int lengthWords, void* userData);

// Callback for connection events
typedef void (*ConnectionCallbackFunc)(const char* endpointName, unsigned int size, void* userData);

// Callback for disconnection events
typedef void (*DisconnectionCallbackFunc)(void* userData);

// Create a new NetUMP endpoint
// sessionMode: 0 = Initiator, 1 = Listener
// Returns: Handle to endpoint or NULL on failure
EXPORT_API void* NetUMP_CreateEndpoint();

// Destroy an endpoint and free resources
EXPORT_API int NetUMP_DestroyEndpoint(void* handle);

// Initialize and start a session
// isInitiator: true = session initiator, false = session listener
EXPORT_API int NetUMP_InitiateSession(void* handle, const char* destIP, unsigned short destPort, 
                                       unsigned short localPort, bool isInitiator);

// Close the session
EXPORT_API int NetUMP_CloseSession(void* handle);

// Run one session iteration (should call every ~1ms from high-priority thread)
EXPORT_API int NetUMP_RunSession(void* handle);

// Send a UMP packet (data is array of 32-bit words)
EXPORT_API int NetUMP_SendPacket(void* handle, const void* data, int lengthWords);

// Set endpoint name (must be called before InitiateSession)
EXPORT_API int NetUMP_SetEndpointName(void* handle, const char* name);

// Set product instance ID (must be called before InitiateSession)
EXPORT_API int NetUMP_SetProductInstanceID(void* handle, const char* piid);

// Set callback for receiving UMP packets
EXPORT_API int NetUMP_SetReceiveCallback(void* handle, UMPPacketCallbackFunc callback, void* userData);

// Set callback for connection events
EXPORT_API int NetUMP_SetConnectionCallback(void* handle, ConnectionCallbackFunc callback, void* userData);

// Set callback for disconnection events
EXPORT_API int NetUMP_SetDisconnectionCallback(void* handle, DisconnectionCallbackFunc callback, void* userData);

// Get session status: 0=closed, 1=inviting, 3=opened
EXPORT_API int NetUMP_GetSessionStatus(void* handle);

// Check if connection was lost (flag is reset after reading)
EXPORT_API int NetUMP_ReadAndResetConnectionLost(void* handle);

// Select error correction mode: 0=none, 1=FEC
EXPORT_API int NetUMP_SelectErrorCorrectionMode(void* handle, unsigned int mode);

// Get last error message
EXPORT_API const char* NetUMP_GetLastError(void* handle);

#ifdef __cplusplus
}
#endif

#endif // NETUMP_UNITY_WRAPPER_H