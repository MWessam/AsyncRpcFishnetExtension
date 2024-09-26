using MonoFN.Cecil;

namespace CodeGenerating
{
    internal interface IHooksInjector
    {
        void InjectOnLogicComplete(CodegenSession session, MethodDefinition method, TypeDefinition typeDef,
            MethodDefinition preHookMethod, MethodDefinition postHookMethod);
    }
}