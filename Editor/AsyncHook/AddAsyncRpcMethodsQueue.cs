#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MonoFN.Cecil;



public interface IAddHookMethodsService
{
    void AddMethodToHookInto(Action<MethodDefinition, MethodDefinition> methodToHookInto);
    void BeginHooking(MethodDefinition preHookMethod, MethodDefinition postHookMethod);
}

/// <summary>
/// Queues up all hook calls.
/// Once the methods we want to hook have been found,
/// start adding the hooks. 
/// </summary>
public class AddAsyncRpcHookMethodsService : IAddHookMethodsService
{
    private Queue<Action<MethodDefinition, MethodDefinition>> _rpcsToHookQueue = new();
    private bool _hooksAvailable;
    private MethodDefinition _preHookMethod;
    private MethodDefinition _postHookMethod;
    public static AddAsyncRpcHookMethodsService Instance = new();

    void IAddHookMethodsService.AddMethodToHookInto(Action<MethodDefinition, MethodDefinition> methodToHookInto)
    {
        if (_hooksAvailable)
        {
            methodToHookInto?.Invoke(_preHookMethod, _postHookMethod);
        }
        else
        {
            _rpcsToHookQueue.Enqueue(methodToHookInto);
        }
    }

    // Add hooks once we find the necessary methods.
    // If there are any in the queue, start performing the hook injection on each.
    // Any new item added after this being called will just immediately inject the hooks.
    void IAddHookMethodsService.BeginHooking(MethodDefinition preHookMethod, MethodDefinition postHookMethod)
    {
        _hooksAvailable = true;
        _preHookMethod = preHookMethod;
        _postHookMethod = postHookMethod;
        foreach (var hook in _rpcsToHookQueue)
        {
            hook?.Invoke(_preHookMethod, _postHookMethod);
        }
    }
}
#endif