#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MonoFN.Cecil;

/// <summary>
/// Queues up all hook calls.
/// Once the methods we want to hook have been found,
/// start adding the hooks. 
/// </summary>
public class AddAsyncRpcMethodsQueue
{
    private Queue<Action<MethodDefinition, MethodDefinition>> _rpcsToHookQueue = new();
    private bool _hookAvailable;
    private MethodDefinition _onRpcStart;
    private MethodDefinition _onRpcEnd;
    public static AddAsyncRpcMethodsQueue Instance = new();
    
    public void AddRpcToHookQueue(Action<MethodDefinition, MethodDefinition> rpcToHook)
    {
        
        if (_hookAvailable)
        {
            rpcToHook?.Invoke(_onRpcStart, _onRpcEnd);
        }
        else
        {
            _rpcsToHookQueue.Enqueue(rpcToHook);
        }
    }

    // Add hooks once we find the necessary methods.
    // If there are any in the queue, start performing the hook injection on each.
    // Any new item added after this being called will just immediately inject the hooks.
    public void BeginHooking(MethodDefinition rpcStartMethodDefinition, MethodDefinition rpcEndMethodDefinition)
    {
        _hookAvailable = true;
        _onRpcStart = rpcStartMethodDefinition;
        _onRpcEnd = rpcEndMethodDefinition;
        foreach (var hook in _rpcsToHookQueue)
        {
            hook?.Invoke(_onRpcStart, _onRpcEnd);
        }
    }
}
#endif