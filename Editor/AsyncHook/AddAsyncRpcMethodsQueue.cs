#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MonoFN.Cecil;

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
            // throw new(_rpcsToHookQueue.Count.ToString());
        }
    }

    public void BeginHooking(MethodDefinition rpcStartMethodDefinition, MethodDefinition rpcEndMethodDefinition)
    {
        // throw new(_rpcsToHookQueue.Count.ToString());
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