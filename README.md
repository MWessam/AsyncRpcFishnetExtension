# AsyncRpcFishnetExtension
DISCLAIMER:
Most of the SourceGen code has been copied from FirstGearGame's Fishnet directly. All credits goes to them for their wonderful system.
I have just extended it to be able to inject hook methods that deal directly with the ```AsyncRPCCallManager```



Adds an ```AsyncRPCCallManager``` Singleton that handles async calls to and from server.
To use you will need an ```[AsyncRPC]``` attribute on any ```[ServerRPC]``` with the following signature:
```
[AsyncRPC]
[ServerRPC(RequireOwnership = false)]
void AsyncRPC(int callId, NetworkConnection connection = null)
```
Then you will be able to execute this rpc via ```AsyncRPCCallManager``` using its ```ExecuteRPC``` method.
```
await AsyncRPCCallManager.Instance.ExecuteRPC(AsyncRPC);
```

You must have an item with the component AsyncRPCCallManager for it to work.


# Dependencies
[Fishnet](https://github.com/FirstGearGames/FishNet)
[Unitask](https://github.com/Cysharp/UniTask)
Mono.Cecil from Unity's Registry.
