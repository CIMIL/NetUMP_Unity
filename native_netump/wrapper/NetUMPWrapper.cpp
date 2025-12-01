#include "NetUMPWrapper.h"
#include "NetUMP.h"
#include "choc_SingleReaderSingleWriterFIFO.h"
#include <thread>
#include <atomic>
#include <cstring>
#include <map>
#include <mutex>

#if defined(_WIN32) || defined(_WIN64)
#include <winsock2.h>
#include <ws2tcpip.h>
#else
#include <arpa/inet.h>
#endif

// UMP Message structure for the queue
struct UMPMessage
{
    uint8_t data[16]; // Max UMP message size (128 bits)
    uint8_t length;

    UMPMessage() : length(0)
    {
        memset(data, 0, sizeof(data));
    }
};

// UMP size table (from NetUMP.cpp)
static unsigned int UMPSize[16] = {1, 1, 1, 2, 2, 4, 1, 1, 2, 2, 2, 3, 3, 4, 4, 4};

// NetUMP Instance structure
struct NetUMPWrapper
{
    CNetUMPHandler *handler;
    std::thread sessionThread;
    std::atomic<bool> running;
    choc::fifo::SingleReaderSingleWriterFIFO<UMPMessage> receiveQueue;

    // Callbacks
    UMPMessageCallback messageCallback;
    ConnectionEventCallback connectionCallback;
    DisconnectionEventCallback disconnectionCallback;

    // Configuration
    char localEndpointName[MAX_UMP_ENDPOINT_NAME_LEN];
    char productInstanceId[MAX_UMP_PRODUCT_INSTANCE_ID_LEN];

    NetUMPWrapper()
        : handler(nullptr), running(false), messageCallback(nullptr), connectionCallback(nullptr), disconnectionCallback(nullptr)
    {
        receiveQueue.reset(1024);
        memset(localEndpointName, 0, sizeof(localEndpointName));
        memset(productInstanceId, 0, sizeof(productInstanceId));
    }
};

// Global registry of instances (protected by mutex) to support safe lookup and validation of the opaque
static std::map<NetUMPInstance, NetUMPWrapper *> g_instances;
static std::mutex g_instancesMutex;

// Helper to get wrapper safely
static NetUMPWrapper *GetWrapper(NetUMPInstance instance)
{
    std::lock_guard<std::mutex> lock(g_instancesMutex);
    auto it = g_instances.find(instance);
    return (it != g_instances.end()) ? it->second : nullptr;
}

// Native callback from NetUMP library
void NativeUMPCallback(void *userInstance, uint32_t *packet)
{
    NetUMPWrapper *wrapper = static_cast<NetUMPWrapper *>(userInstance);
    if (!wrapper)
        return;

    // Determine number of words from MT field
    unsigned int MT = (packet[0] >> 28) & 0xF;
    unsigned int numWords = UMPSize[MT];
    unsigned int numBytes = numWords * 4;

    UMPMessage msg;
    msg.length = static_cast<uint8_t>(numBytes);

    // Copy UMP data (keep native byte order for Unity to instance)
    memcpy(msg.data, packet, numBytes);

    // Try to push to queue
    if (!wrapper->receiveQueue.push(msg))
    {
        // Queue is full - message dropped
    }

    // Also call direct callback if set
    if (wrapper->messageCallback != nullptr)
    {
        wrapper->messageCallback(wrapper, msg.data, msg.length);
    }
}

// Connection callback wrapper
void NativeConnectionCallback(NetUMPWrapper *wrapper, const char *endpointName, unsigned int size)
{
    if (wrapper && wrapper->connectionCallback != nullptr)
    {
        wrapper->connectionCallback(wrapper, endpointName, static_cast<int>(size));
    }
}

// Disconnection callback wrapper
void NativeDisconnectionCallback(NetUMPWrapper *wrapper)
{
    if (wrapper && wrapper->disconnectionCallback != nullptr)
    {
        wrapper->disconnectionCallback(wrapper);
    }
}

// Session thread that calls RunSession every 1ms
void SessionThread(NetUMPWrapper *wrapper)
{
    // using clock = std::chrono::steady_clock;
    // auto nextTime = clock::now();

    while (wrapper->running.load(std::memory_order_acquire))
    {
        // nextTime += std::chrono::milliseconds(1);

        if (wrapper->handler)
        {
            wrapper->handler->RunSession();
        }

        // Sleep until next iteration
        // std::this_thread::sleep_until(nextTime);
        std::this_thread::sleep_for(std::chrono::milliseconds(1));
    }
}

// Create a new NetUMP handler wrapper
NETUMP_EXPORT NetUMPInstance NetUMP_Create(
    const char *localEndpointName,
    const char *productInstanceId)
{
    if (!localEndpointName || !productInstanceId)
    {
        return nullptr;
    }

    NetUMPWrapper *wrapper = new NetUMPWrapper();

    // Copy configuration
    strncpy(wrapper->localEndpointName, localEndpointName, MAX_UMP_ENDPOINT_NAME_LEN - 1);
    strncpy(wrapper->productInstanceId, productInstanceId, MAX_UMP_PRODUCT_INSTANCE_ID_LEN - 1);

    // Register wrapper
    {
        std::lock_guard<std::mutex> lock(g_instancesMutex);
        g_instances[wrapper] = wrapper;
    }

    return wrapper;
}

// Initialize and start the NetUMP session
NETUMP_EXPORT int NetUMP_Start(
    NetUMPInstance instance,
    const char *remoteHost,
    uint16_t localPort,
    uint16_t remotePort,
    bool isInitiator)
{
    NetUMPWrapper *wrapper = GetWrapper(instance);
    if (!wrapper || !remoteHost)
    {
        return -1;
    }

    // Stop if already running
    if (wrapper->running.load(std::memory_order_acquire))
    {
        NetUMP_Stop(instance);
    }

    // Create handler with callback
    wrapper->handler = new CNetUMPHandler(NativeUMPCallback, wrapper);
    if (!wrapper->handler)
    {
        return -2;
    }

    // Set endpoint name and product ID
    wrapper->handler->SetEndpointName(wrapper->localEndpointName);
    wrapper->handler->SetProductInstanceID(wrapper->productInstanceId);

    // Set connection/disconnection callbacks with lambda wrappers
    wrapper->handler->SetConnectionCallback(
        [](const char* endpointName, unsigned int size) {
            // Find which instance this callback belongs to by checking all handlers
            std::lock_guard<std::mutex> lock(g_instancesMutex);
            for (auto& pair : g_instances) {
                if (pair.second->handler) {
                    // We need to store instance pointer somewhere accessible
                    // For now, we'll call all registered callbacks
                    NativeConnectionCallback(pair.second, endpointName, size);
                }
            }
        }
    );
    
    wrapper->handler->SetDisconnectCallback(
        []() {
            // Similar issue - call all disconnection callbacks
            std::lock_guard<std::mutex> lock(g_instancesMutex);
            for (auto& pair : g_instances) {
                if (pair.second->handler) {
                    NativeDisconnectionCallback(pair.second);
                }
            }
        }
    );

    // Convert remote host to IP
    unsigned int destIP = ntohl(inet_addr(remoteHost));

    // Initiate session
    int result = wrapper->handler->InitiateSession(destIP, remotePort, localPort, isInitiator);
    if (result < 0)
    {
        delete wrapper->handler;
        wrapper->handler = nullptr;
        return -3;
    }

    // Start session thread
    wrapper->running.store(true, std::memory_order_release);
    wrapper->sessionThread = std::thread(SessionThread, wrapper);

    return 0;
}

// Stop the session
NETUMP_EXPORT void NetUMP_Stop(NetUMPInstance instance)
{
    NetUMPWrapper *wrapper = GetWrapper(instance);
    if (!wrapper)
        return;

    if (wrapper->running.load(std::memory_order_acquire))
    {
        wrapper->running.store(false, std::memory_order_release);

        if (wrapper->sessionThread.joinable())
        {
            wrapper->sessionThread.join();
        }
    }

    if (wrapper->handler)
    {
        wrapper->handler->CloseSession();
        delete wrapper->handler;
        wrapper->handler = nullptr;
    }

    wrapper->receiveQueue.reset();
}

// Destroy the handler wrapper
NETUMP_EXPORT void NetUMP_Destroy(NetUMPInstance instance)
{
    NetUMPWrapper *wrapper = GetWrapper(instance);
    if (!wrapper)
        return;

    // Stop session first
    NetUMP_Stop(instance);

    // Unregister wrapper
    {
        std::lock_guard<std::mutex> lock(g_instancesMutex);
        g_instances.erase(instance);
    }

    delete wrapper;
}

// Send UMP message
NETUMP_EXPORT bool NetUMP_SendUMP(NetUMPInstance instance, const uint8_t *data, int length)
{
    NetUMPWrapper *wrapper = GetWrapper(instance);
    if (!wrapper || !wrapper->handler || !data || length < 4 || length > 16)
    {
        return false;
    }

    // Copy to uint32_t array
    uint32_t umpData[4];
    memcpy(umpData, data, length);

    return wrapper->handler->SendUMPMessage(umpData);
}

// Poll for received messages
NETUMP_EXPORT int NetUMP_PollMessages(NetUMPInstance instance)
{
    NetUMPWrapper *wrapper = GetWrapper(instance);
    if (!wrapper)
        return 0;

    return static_cast<int>(wrapper->receiveQueue.getUsedSlots());
}

// Get next message from queue
NETUMP_EXPORT int NetUMP_GetNextMessage(NetUMPInstance instance, uint8_t *buffer, int bufferSize)
{
    NetUMPWrapper *wrapper = GetWrapper(instance);
    if (!wrapper || !buffer || bufferSize < 16)
    {
        return 0;
    }

    UMPMessage msg;
    if (wrapper->receiveQueue.pop(msg))
    {
        memcpy(buffer, msg.data, msg.length);
        return msg.length;
    }

    return 0;
}

// Get session status
NETUMP_EXPORT int NetUMP_GetSessionStatus(NetUMPInstance instance)
{
    NetUMPWrapper *wrapper = GetWrapper(instance);
    if (!wrapper || !wrapper->handler)
    {
        return 0;
    }
    return wrapper->handler->GetSessionStatus();
}

// Check if connection was lost
NETUMP_EXPORT bool NetUMP_ReadAndResetConnectionLost(NetUMPInstance instance)
{
    NetUMPWrapper *wrapper = GetWrapper(instance);
    if (!wrapper || !wrapper->handler)
    {
        return false;
    }
    return wrapper->handler->ReadAndResetConnectionLost();
}

// Set callback for received messages
NETUMP_EXPORT void NetUMP_SetMessageCallback(NetUMPInstance instance, UMPMessageCallback callback)
{
    NetUMPWrapper *wrapper = GetWrapper(instance);
    if (wrapper)
    {
        wrapper->messageCallback = callback;
    }
}

// Set callbacks for connection events
NETUMP_EXPORT void NetUMP_SetConnectionCallback(NetUMPInstance instance, ConnectionEventCallback callback)
{
    NetUMPWrapper *wrapper = GetWrapper(instance);
    if (wrapper)
    {
        wrapper->connectionCallback = callback;
    }
}

NETUMP_EXPORT void NetUMP_SetDisconnectionCallback(NetUMPInstance instance, DisconnectionEventCallback callback)
{
    NetUMPWrapper *wrapper = GetWrapper(instance);
    if (wrapper)
    {
        wrapper->disconnectionCallback = callback;
    }
}