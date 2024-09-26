using System;
using System.Linq;
using System.Text.RegularExpressions;
using FishNet.Connection;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;

namespace CodeGenerating
{
    internal class FishnetHooksInjector : IHooksInjector
    {
        /// <summary>
        /// Will inject on start and on end hooks into our <see cref="AsyncRpcAttribute"/> rpcs.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="method"></param>
        /// <param name="typeDef"></param>
        /// <param name="preHookMethod"></param>
        /// <param name="postHookMethod"></param>
        /// <exception cref="Exception"></exception>
        void IHooksInjector.InjectOnLogicComplete(CodegenSession session, MethodDefinition method, TypeDefinition typeDef,
            MethodDefinition preHookMethod, MethodDefinition postHookMethod)
        {
            // Fishnet relies on generated methods. Any method that has any sort of RPC attribute, will be split
            // into multiple versions.
            // RpcWriter, RpcLogic, RpcReader.
            // RpcLogic is the main method itself.
            // And its always named RpcLogic___{method_name}
            string logicMethodPrefix = "RpcLogic___";
            string methodName = method.Name; // The name of the original method
            
            // Create a regex pattern to match methods like RpcLogic___methodname followed by anything
            string regexPattern = $@"^{logicMethodPrefix}{methodName}.*$";

            // Find logic methods that contain the method name.
            var logicMethods = typeDef.Methods
                .Where(x => x.Name.Contains(methodName)).ToList();
            if (!logicMethods.Any())
            {
                return;
            }
            MethodDefinition logicMethod = null;
            // Since the order of source generating is indeterminate and can not be specified,
            // we will either use RpcLogic___ prefixed methods if found (Which indicates fishnet source gen ran first)
            // or use the main method itself (Which indicates that we ran before fishnet source gen)
            
            foreach (var logicMd in logicMethods)
            {
                // Assign the currently found method to be the logic method.
                logicMethod = logicMd;
                
                // If we find RpcLogic___ prefix method, we will break out of the loop and use that to add our hooks.
                if (Regex.IsMatch(logicMd.Name, regexPattern))
                {
                    break;
                }
            }
            
            ILProcessor processor = logicMethod!.Body.GetILProcessor();

            MethodReference preHookMethodReference = method.Module.ImportReference(preHookMethod);
            MethodReference postHookMethodReference = method.Module.ImportReference(postHookMethod);
            // Get method parameters (NetworkConnection and int)
            var networkConnectionParam = method.Parameters.FirstOrDefault(p => p.ParameterType.FullName == typeof(NetworkConnection).FullName);
            var intParam = method.Parameters.FirstOrDefault(p => p.ParameterType.FullName == "System.Int32");
            if (networkConnectionParam is null || intParam is null)
            {
                throw new($"The AsyncRPC {method.Name} in Type {typeDef.Name} does not have the expected signature." +
                          $"\n Please make sure your AsyncRPC has the following signature: void AsyncRpc(int callerId, NetworkConnection connection = null)");
            }
            
            // Load parameters (ldarg) and call rpcStartMethod
            // Assuming networkConnectionParam is first (ldarg_1) and intParam is second (ldarg_2)
            processor.Body.Instructions.Insert(0, processor.Create(OpCodes.Ldarg, 1));                // Load int param
            processor.Body.Instructions.Insert(1, processor.Create(OpCodes.Ldarg, 2));  // Load NetworkConnection param
            processor.Body.Instructions.Insert(2, processor.Create(OpCodes.Call, preHookMethodReference));         // Call rpcStartedMethod

            // Find Ret instruction and replace it with rpcFinishMethod and Ret
            var retInstruction = processor.Body.Instructions.LastOrDefault(i => i.OpCode == OpCodes.Ret);
            if (retInstruction != null)
            {
                // Insert logic before Ret
                processor.InsertBefore(retInstruction, processor.Create(OpCodes.Ldarg, 1));                // Load int param
                processor.InsertBefore(retInstruction, processor.Create(OpCodes.Ldarg, 2));  // Load NetworkConnection param
                processor.InsertBefore(retInstruction, processor.Create(OpCodes.Call, postHookMethodReference));       // Call rpcFinishMethod
            }
            else
            {
                // In case there isn't an explicit Ret, add it manually
                processor.Emit(OpCodes.Ldarg, 1);                // Load int param
                processor.Emit(OpCodes.Ldarg, 2);  // Load NetworkConnection param
                processor.Emit(OpCodes.Call, postHookMethodReference);       // Call rpcFinishMethod
                processor.Emit(OpCodes.Ret);                                  // Return
            }
        }
    }
}