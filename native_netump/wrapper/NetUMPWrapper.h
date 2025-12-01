#ifndef __NETUMP_WRAPPER_H__
#define __NETUMP_WRAPPER_H__

#include <stdint.h>

// Export macro for different platforms
#if defined(_WIN32) || defined(_WIN64)
    #define NETUMP_EXPORT extern "C" __declspec(dllexport)
#elif defined(__APPLE__) || defined(__linux__)
    #define NETUMP_EXPORT extern "C" __attribute__((visibility("default")))
#else
    #define NETUMP_EXPORT extern "C"
#endif

// Opaque instance type for NetUMPWrappers
typedef void* NetUMPInstance;

// Callback type for UMP messages received from network
typedef void (*UMPMessageCallback)(NetUMPInstance instance, const uint8_t* data, int length);

// Callback type for connection events
typedef void (*ConnectionEventCallback)(NetUMPInstance instance, const char* endpointName, int nameLength);

// Callback type for disconnection events
typedef void (*DisconnectionEventCallback)(NetUMPInstance instance);

// Create a new NetUMP handler instance
NETUMP_EXPORT NetUMPInstance NetUMP_Create(
    const char* localEndpointName,
    const char* productInstanceId);

// Initialize and start the NetUMP session
NETUMP_EXPORT int NetUMP_Start(
    NetUMPInstance instance,
    const char* remoteHost,
    uint16_t localPort,
    uint16_t remotePort,
    bool isInitiator);

// Stop the session (can be restarted with NetUMP_Start)
NETUMP_EXPORT void NetUMP_Stop(NetUMPInstance instance);

// Destroy the handler instance and free resources
NETUMP_EXPORT void NetUMP_Destroy(NetUMPInstance instance);

// Send UMP message (data should be in native byte order)
NETUMP_EXPORT bool NetUMP_SendUMP(NetUMPInstance instance, const uint8_t* data, int length);

// Poll for received messages (call from Unity main thread)
// Returns number of messages available
NETUMP_EXPORT int NetUMP_PollMessages(NetUMPInstance instance);

// Get next message from queue (call after PollMessages returns > 0)
// Returns actual length written to buffer, or 0 if no message
NETUMP_EXPORT int NetUMP_GetNextMessage(NetUMPInstance instance, uint8_t* buffer, int bufferSize);

// Get session status: 0=closed, 1=inviting, 3=opened
NETUMP_EXPORT int NetUMP_GetSessionStatus(NetUMPInstance instance);

// Check if connection was lost (flag is reset after reading)
NETUMP_EXPORT bool NetUMP_ReadAndResetConnectionLost(NetUMPInstance instance);

// Set callback for received messages (optional, alternative to polling)
NETUMP_EXPORT void NetUMP_SetMessageCallback(NetUMPInstance instance, UMPMessageCallback callback);

// Set callbacks for connection events
NETUMP_EXPORT void NetUMP_SetConnectionCallback(NetUMPInstance instance, ConnectionEventCallback callback);
NETUMP_EXPORT void NetUMP_SetDisconnectionCallback(NetUMPInstance instance, DisconnectionEventCallback callback);

#endif // __NETUMP_WRAPPER_H__