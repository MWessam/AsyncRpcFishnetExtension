using MonoFN.Cecil;

namespace CodeGenerating
{
    internal interface IHookMethodAssemblyFinder
    {
        /// <summary>
        /// Checks if this assembly is the one that stores the required type with hook function.
        /// </summary>
        /// <param name="assemblyDefinition"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        bool IsHookAssembly(AssemblyDefinition assemblyDefinition, CodegenSession session);
    }
}