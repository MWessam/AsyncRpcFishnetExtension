using System;
using System.Linq;
using MonoFN.Cecil;

namespace CodeGenerating
{
    internal class HookProvider : IHookProvider
    {
        private Type _hookClassType;
        private string _hookMethodName;

        public HookProvider(Type hookClassType, string hookMethodName)
        {
            _hookClassType = hookClassType;
            _hookMethodName = hookMethodName;
        }

        MethodDefinition IHookProvider.GetHookMethod(AssemblyDefinition assemblyDefinition, CodegenSession session)
        {
            var asyncRpcManagerTypeDefinition =
                session.Module.Types.FirstOrDefault(x => _hookClassType.Name == x.Name);
            var hookMethodDefinition = asyncRpcManagerTypeDefinition.GetMethod(_hookMethodName);
            return hookMethodDefinition;
        }
    }
}