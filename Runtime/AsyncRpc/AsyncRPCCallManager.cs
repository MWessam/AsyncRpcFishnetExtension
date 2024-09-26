using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public class AsyncRpcAttribute : Attribute
{
    
}
public class AsyncRPCCallManager : NetworkBehaviour
{
    public static AsyncRPCCallManager Instance { get; private set; }
    private ConcurrentDictionary<int, RPCCall> _rpcCalls = new();
    private int _lastCallId = 0;
    // private static Queue<int>
    private NetworkConnection _callingConnection;

    private void Awake()
    {
        if (Instance)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
    }

    public async UniTask AsyncRPCCall()
    {
        Debug.Log("Invoking Cmd");
        await ExecuteRPC(CmdTest);
        Debug.Log("Invoked!");
    }
    [AsyncRpc]
    [ServerRpc(RequireOwnership = false)]
    public void CmdTest(int callId, NetworkConnection networkConnection = null)
    {
        Debug.Log("Done something on server.");
        _callingConnection = networkConnection;
    }
    public async UniTask ExecuteRPC(RPCAction rpcAction)
    {
        int callId = ++_lastCallId;
        var rpcCall = new RPCCall(rpcAction);
        _rpcCalls.TryAdd(callId, rpcCall);
        RPC(callId, rpcCall);
        await rpcCall.Request.Task;
        Debug.Log("Finished executing.");
    }
    private void RPC(int callId, RPCCall rpcCall)
    {
        rpcCall.RPCAction?.Invoke(callId);
    }
    [TargetRpc]
    public void SendRPCResponse(NetworkConnection client, int callId)
    {
        if (_rpcCalls.TryRemove(callId, out var call))
        {
            call.Request.TrySetResult(true);
        }
    }

    public static void StartServerRPC(int callerId, NetworkConnection networkConnection)
    {
        Debug.Log($"Started Cmd {callerId} On Server!");
    }

    public static void EndServerRPC(int callerId, NetworkConnection networkConnection)
    {
        Debug.Log($"Ending Cmd {callerId}.");
        Instance.InitiateCallbackRequest(callerId, networkConnection);
    }
    [Server]
    private void InitiateCallbackRequest(int callerId, NetworkConnection networkConnection)
    {
        SendRPCResponse(networkConnection, callerId);
    }
}
public delegate void RPCAction(int requestId, NetworkConnection connection = null);

public class RPCCall
{
    public UniTaskCompletionSource<object> Request;
    public RPCAction RPCAction;

    public RPCCall(RPCAction rpcAction)
    {
        RPCAction = rpcAction;
        Request = new UniTaskCompletionSource<object>();
    }
}