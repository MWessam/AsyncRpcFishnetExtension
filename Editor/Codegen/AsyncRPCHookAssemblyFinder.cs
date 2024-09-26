using MonoFN.Cecil;

namespace CodeGenerating
{
    internal class AsyncRPCHookAssemblyFinder : IHookMethodAssemblyFinder
    {
        public const string ASYNC_RPC_ASSEMBLY_NAME = "AsyncRpc";
        /// <summary>
        /// Since it's near impossible to get assembly definitions whenever needed,
        /// I am storing every hook subscription into a queue. <see cref="AddAsyncRpcMethodsQueue"/>
        /// Once I find the assembly of async rpc, ill inject the hooks into every queued up call.
        /// And if any other assembly is found after that, it will automatically inject the hooks.
        /// </summary>
        /// <param name="assemblyDefinition">Current assembly</param>
        /// <param name="session"></param>
        /// <returns></returns>
        bool IHookMethodAssemblyFinder.IsHookAssembly(AssemblyDefinition assemblyDefinition, CodegenSession session)
        {
            return assemblyDefinition.Name.Name.Contains(ASYNC_RPC_ASSEMBLY_NAME);
        }

    }
}