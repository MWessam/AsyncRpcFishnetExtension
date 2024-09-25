using Mono.Cecil;

namespace CodeGenerating
{
    internal class PostProcessorReflectionImporterProvider : IReflectionImporterProvider
    {
        public IReflectionImporter GetReflectionImporter(ModuleDefinition moduleDef)
        {
            return new PostProcessorReflectionImporter(moduleDef);
        }
    }
}