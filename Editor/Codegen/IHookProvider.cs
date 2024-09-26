using MonoFN.Cecil;

namespace CodeGenerating
{
    internal interface IHookProvider
    {
        MethodDefinition GetHookMethod(AssemblyDefinition assemblyDefinition, CodegenSession session);
    }
}