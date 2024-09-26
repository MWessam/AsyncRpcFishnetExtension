using System;
using System.Collections.Concurrent;
using Cysharp.Threading.Tasks;
using FishNet.Connection;
using FishNet.Object;
/// <summary>
/// Used to signal that this rpc will be used for async purposes.
/// Automatically adds OnStartLogic and OnEndLogic hooks to notify the client.
/// </summary>
public class AsyncRpcAttribute : Attribute
{
    
}
/// <summary>
/// Singleton manager that handles async rpc calls.
/// Using <see cref="ExecuteRPC"/>, you will be able to await for any server rpc to complete its execution
/// as long as it follows <see cref="AsyncRPCAction"/> signature.
/// </summary>
public class AsyncRPCCallManager : NetworkBehaviour
{
    /// <summary>
    /// Has to be a singleton as you can't run an RPC on non network behaviour classes.
    /// </summary>
    public static AsyncRPCCallManager Instance { get; private set; }
    
    /// <summary>
    /// Concurrent Dictionary just in case of race conditions (Which is unlikely but better safe than sorry.)
    /// </summary>
    private ConcurrentDictionary<int, RPCCall> _rpcCalls = new();
    
    // Used to assign unique Ids
    private static int _lastCallId;
    
    /// <summary>
    /// Initialize the singleton.
    /// </summary>
    private void Awake()
    {
        if (Instance)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    /// <summary>
    /// Assigns a unique client side id and invokes your async rpc.
    /// Once invoked, the server will then renotify the client using that id once it completes its execution.
    /// </summary>
    /// <param name="asyncRPCAction"></param>
    public async UniTask ExecuteRPC(AsyncRPCAction asyncRPCAction)
    {
        int callId = ++_lastCallId;
        var rpcCall = new RPCCall(asyncRPCAction);
        _rpcCalls.TryAdd(callId, rpcCall);
        rpcCall.AsyncRPCAction?.Invoke(callId);
        
        // Request.Task is waiting for the client to be notified by the server.
        await rpcCall.Request.Task;
    }
    
    /// <summary>
    /// Notifies client that the RPC has been handled and that it can proceed.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="callId"></param>
    [TargetRpc]
    public void SendRPCResponse(NetworkConnection client, int callId)
    {
        if (_rpcCalls.TryRemove(callId, out var call))
        {
            call.Request.TrySetResult(true);
        }
    }
    /// <summary>
    /// Called at the start of the AsyncRPC execution.
    /// </summary>
    /// <param name="callerId"></param>
    /// <param name="networkConnection"></param>
    /// <returns></returns>
    public static void StartServerRPC(int callerId, NetworkConnection networkConnection)
    {
    }
    /// <summary>
    /// Called at the end of the AsyncRPC execution.
    /// </summary>
    /// <param name="callerId"></param>
    /// <param name="networkConnection"></param>
    public static void EndServerRPC(int callerId, NetworkConnection networkConnection)
    {
        Instance.SendRPCResponse(networkConnection, callerId);
    }
}
/// <summary>
/// The only supported AsyncRPC method signature.
/// </summary>
public delegate void AsyncRPCAction(int requestId, NetworkConnection connection = null);

/// <summary>
/// Wraps an AsyncRPC to contain its TaskCompletionSource.
/// This will be used to notify the client that the Server RPC has been handled.
/// </summary>
public class RPCCall
{
    public UniTaskCompletionSource<object> Request;
    public AsyncRPCAction AsyncRPCAction;

    public RPCCall(AsyncRPCAction asyncRPCAction)
    {
        AsyncRPCAction = asyncRPCAction;
        Request = new UniTaskCompletionSource<object>();
    }
}