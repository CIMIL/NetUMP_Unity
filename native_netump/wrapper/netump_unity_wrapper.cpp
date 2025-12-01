// netump_unity_wrapper.cpp
// Unity C++ wrapper for bbouchez's NetUMP library
// https://github.com/bbouchez/NetUMP

#include "netump_unity_wrapper.h"
#include "NetUMP.h"
#include <string>
#include <cstring>
#include <mutex>

#ifndef CALLBACK
  #ifdef _WIN32
    #define CALLBACK __stdcall
  #else
    #define CALLBACK
  #endif
#endif

// Wrapper structure to hold endpoint data
struct NetUMPEndpointWrapper
{
    CNetUMPHandler* handler;                    // NetUMP handler instance
    UMPPacketCallbackFunc packetCallback;       // User packet callback
    void* packetUserData;                       // User data for packet callback
    ConnectionCallbackFunc connectionCallback;  // Connection event callback
    void* connectionUserData;                   // User data for connection callback
    DisconnectionCallbackFunc disconnectCallback; // Disconnection event callback
    void* disconnectUserData;                   // User data for disconnect callback
    std::string lastError;                      // Last error message
    std::mutex mutex;                           // Thread safety
    
    NetUMPEndpointWrapper() 
        : handler(nullptr)
        , packetCallback(nullptr)
        , packetUserData(nullptr)
        , connectionCallback(nullptr)
        , connectionUserData(nullptr)
        , disconnectCallback(nullptr)
        , disconnectUserData(nullptr)
    {}
    
    ~NetUMPEndpointWrapper()
    {
        if (handler)
        {
            delete handler;
            handler = nullptr;
        }
    }
};

// Static callback adapters for NetUMP library

// UMP data callback adapter
static void CALLBACK UMPDataCallback(void* UserInstance, uint32_t* DataBlock)
{
    if (!UserInstance || !DataBlock)
        return;
    
    NetUMPEndpointWrapper* wrapper = static_cast<NetUMPEndpointWrapper*>(UserInstance);
    
    if (wrapper->packetCallback)
    {
        // Determine message size from MT (Message Type)
        unsigned int MT = DataBlock[0] >> 28;
        static const unsigned int UMPSize[16] = {1, 1, 1, 2, 2, 4, 1, 1, 2, 2, 2, 3, 3, 4, 4, 4};
        unsigned int messageSize = UMPSize[MT];
        
        wrapper->packetCallback(DataBlock, messageSize, wrapper->packetUserData);
    }
}

// Connection event callback adapter
static void ConnectionEventCallback(const char* EndpointName, unsigned int Size)
{
    // Note: This is a static function, we need to access the wrapper through global or other means
    // For now, we'll handle this differently - see SetConnectionCallback
}

// Disconnection event callback adapter
static void DisconnectionEventCallback()
{
    // Similar issue as above - see SetDisconnectionCallback
}

// Create endpoint
extern "C" EXPORT_API void* NetUMP_CreateEndpoint()
{
    try
    {
        NetUMPEndpointWrapper* wrapper = new NetUMPEndpointWrapper();
        
        // Create CNetUMPHandler instance with callback
        wrapper->handler = new CNetUMPHandler(UMPDataCallback, wrapper);
        
        if (!wrapper->handler)
        {
            delete wrapper;
            return nullptr;
        }
        
        return wrapper;
    }
    catch (...)
    {
        return nullptr;
    }
}

// Destroy endpoint
extern "C" EXPORT_API int NetUMP_DestroyEndpoint(void* handle)
{
    if (!handle)
        return -1;
    
    try
    {
        NetUMPEndpointWrapper* wrapper = static_cast<NetUMPEndpointWrapper*>(handle);
        delete wrapper;
        return 0;
    }
    catch (...)
    {
        return -1;
    }
}

// Helper function to convert IP string to uint32_t
static unsigned int IPStringToUInt(const char* ipStr)
{
    if (!ipStr)
        return 0;
    
    unsigned int a, b, c, d;
    if (sscanf(ipStr, "%u.%u.%u.%u", &a, &b, &c, &d) != 4)
        return 0;
    
    return (a << 24) | (b << 16) | (c << 8) | d;
}

// Initiate session
extern "C" EXPORT_API int NetUMP_InitiateSession(void* handle, const char* destIP, 
                                                  unsigned short destPort, unsigned short localPort,
                                                  bool isInitiator)
{
    if (!handle || !destIP)
        return -1;
    
    try
    {
        NetUMPEndpointWrapper* wrapper = static_cast<NetUMPEndpointWrapper*>(handle);
        std::lock_guard<std::mutex> lock(wrapper->mutex);
        
        if (!wrapper->handler)
            return -2;
        
        // Convert IP string to uint32
        unsigned int destIPInt = IPStringToUInt(destIP);
        if (destIPInt == 0)
        {
            wrapper->lastError = "Invalid IP address format";
            return -3;
        }
        
        // Call InitiateSession
        int result = wrapper->handler->InitiateSession(destIPInt, destPort, localPort, isInitiator);
        
        if (result != 0)
        {
            wrapper->lastError = "Failed to initiate session";
            return -1;
        }
        
        return 0;
    }
    catch (...)
    {
        return -1;
    }
}

// Close session
extern "C" EXPORT_API int NetUMP_CloseSession(void* handle)
{
    if (!handle)
        return -1;
    
    try
    {
        NetUMPEndpointWrapper* wrapper = static_cast<NetUMPEndpointWrapper*>(handle);
        std::lock_guard<std::mutex> lock(wrapper->mutex);
        
        if (!wrapper->handler)
            return -2;
        
        wrapper->handler->CloseSession();
        return 0;
    }
    catch (...)
    {
        return -1;
    }
}

// Run session (call every ~1ms)
extern "C" EXPORT_API int NetUMP_RunSession(void* handle)
{
    if (!handle)
        return -1;
    
    try
    {
        NetUMPEndpointWrapper* wrapper = static_cast<NetUMPEndpointWrapper*>(handle);
        
        // No lock here - this must be fast for real-time operation
        if (!wrapper->handler)
            return -2;
        
        wrapper->handler->RunSession();
        return 0;
    }
    catch (...)
    {
        return -1;
    }
}

// Send UMP packet
extern "C" EXPORT_API int NetUMP_SendPacket(void* handle, const void* data, int lengthWords)
{
    if (!handle || !data || lengthWords <= 0)
        return -1;
    
    try
    {
        NetUMPEndpointWrapper* wrapper = static_cast<NetUMPEndpointWrapper*>(handle);
        
        if (!wrapper->handler)
            return -2;
        
        // Cast to uint32_t array
        const uint32_t* umpData = static_cast<const uint32_t*>(data);
        
        // Validate UMP message size
        unsigned int MT = umpData[0] >> 28;
        static const unsigned int UMPSize[16] = {1, 1, 1, 2, 2, 4, 1, 1, 2, 2, 2, 3, 3, 4, 4, 4};
        unsigned int expectedSize = UMPSize[MT];
        
        if ((unsigned int)lengthWords != expectedSize)
        {
            wrapper->lastError = "Invalid UMP packet size for message type";
            return -3;
        }
        
        // SendUMPMessage expects a non-const pointer, so we need to copy
        uint32_t tempUMP[4];
        for (int i = 0; i < lengthWords && i < 4; i++)
        {
            tempUMP[i] = umpData[i];
        }
        
        bool success = wrapper->handler->SendUMPMessage(tempUMP);
        return success ? 0 : -1;
    }
    catch (...)
    {
        return -1;
    }
}

// Set endpoint name
extern "C" EXPORT_API int NetUMP_SetEndpointName(void* handle, const char* name)
{
    if (!handle || !name)
        return -1;
    
    try
    {
        NetUMPEndpointWrapper* wrapper = static_cast<NetUMPEndpointWrapper*>(handle);
        std::lock_guard<std::mutex> lock(wrapper->mutex);
        
        if (!wrapper->handler)
            return -2;
        
        // Cast away const - NetUMP API doesn't use const
        wrapper->handler->SetEndpointName(const_cast<char*>(name));
        return 0;
    }
    catch (...)
    {
        return -1;
    }
}

// Set product instance ID
extern "C" EXPORT_API int NetUMP_SetProductInstanceID(void* handle, const char* piid)
{
    if (!handle || !piid)
        return -1;
    
    try
    {
        NetUMPEndpointWrapper* wrapper = static_cast<NetUMPEndpointWrapper*>(handle);
        std::lock_guard<std::mutex> lock(wrapper->mutex);
        
        if (!wrapper->handler)
            return -2;
        
        wrapper->handler->SetProductInstanceID(const_cast<char*>(piid));
        return 0;
    }
    catch (...)
    {
        return -1;
    }
}

// Set receive callback
extern "C" EXPORT_API int NetUMP_SetReceiveCallback(void* handle, UMPPacketCallbackFunc callback, void* userData)
{
    if (!handle)
        return -1;
    
    try
    {
        NetUMPEndpointWrapper* wrapper = static_cast<NetUMPEndpointWrapper*>(handle);
        std::lock_guard<std::mutex> lock(wrapper->mutex);
        
        wrapper->packetCallback = callback;
        wrapper->packetUserData = userData;
        
        return 0;
    }
    catch (...)
    {
        return -1;
    }
}

// Set connection callback (simplified - see note below)
extern "C" EXPORT_API int NetUMP_SetConnectionCallback(void* handle, ConnectionCallbackFunc callback, void* userData)
{
    if (!handle)
        return -1;
    
    try
    {
        NetUMPEndpointWrapper* wrapper = static_cast<NetUMPEndpointWrapper*>(handle);
        std::lock_guard<std::mutex> lock(wrapper->mutex);
        
        wrapper->connectionCallback = callback;
        wrapper->connectionUserData = userData;
        
        // Note: CNetUMPHandler's SetConnectionCallback doesn't pass user data
        // You may need to modify NetUMP to support this, or use a global registry
        
        return 0;
    }
    catch (...)
    {
        return -1;
    }
}

// Set disconnection callback
extern "C" EXPORT_API int NetUMP_SetDisconnectionCallback(void* handle, DisconnectionCallbackFunc callback, void* userData)
{
    if (!handle)
        return -1;
    
    try
    {
        NetUMPEndpointWrapper* wrapper = static_cast<NetUMPEndpointWrapper*>(handle);
        std::lock_guard<std::mutex> lock(wrapper->mutex);
        
        wrapper->disconnectCallback = callback;
        wrapper->disconnectUserData = userData;
        
        return 0;
    }
    catch (...)
    {
        return -1;
    }
}

// Get session status
extern "C" EXPORT_API int NetUMP_GetSessionStatus(void* handle)
{
    if (!handle)
        return 0;
    
    try
    {
        NetUMPEndpointWrapper* wrapper = static_cast<NetUMPEndpointWrapper*>(handle);
        
        if (!wrapper->handler)
            return 0;
        
        return wrapper->handler->GetSessionStatus();
    }
    catch (...)
    {
        return 0;
    }
}

// Read and reset connection lost flag
extern "C" EXPORT_API int NetUMP_ReadAndResetConnectionLost(void* handle)
{
    if (!handle)
        return 0;
    
    try
    {
        NetUMPEndpointWrapper* wrapper = static_cast<NetUMPEndpointWrapper*>(handle);
        
        if (!wrapper->handler)
            return 0;
        
        return wrapper->handler->ReadAndResetConnectionLost() ? 1 : 0;
    }
    catch (...)
    {
        return 0;
    }
}

// Select error correction mode
extern "C" EXPORT_API int NetUMP_SelectErrorCorrectionMode(void* handle, unsigned int mode)
{
    if (!handle)
        return -1;
    
    try
    {
        NetUMPEndpointWrapper* wrapper = static_cast<NetUMPEndpointWrapper*>(handle);
        std::lock_guard<std::mutex> lock(wrapper->mutex);
        
        if (!wrapper->handler)
            return -2;
        
        wrapper->handler->SelectErrorCorrectionMode(mode);
        return 0;
    }
    catch (...)
    {
        return -1;
    }
}

// Get last error
extern "C" EXPORT_API const char* NetUMP_GetLastError(void* handle)
{
    if (!handle)
        return "Invalid handle";
    
    try
    {
        NetUMPEndpointWrapper* wrapper = static_cast<NetUMPEndpointWrapper*>(handle);
        
        if (wrapper->lastError.empty())
            return "No error";
        
        return wrapper->lastError.c_str();
    }
    catch (...)
    {
        return "Exception occurred";
    }
}