using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using FishNet.Connection;
using FishNet.Object;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace CodeGenerating
{
    using System.Collections;
    using System.IO;
    using System.Linq;
    using Mono.Cecil;
    using UnityEngine;
    using Unity.CompilationPipeline.Common.ILPostProcessing;

    internal class AddAsyncRPCProcessor : CodegenBase
    {
        
    }
    public class AddAsyncRpcHook : ILPostProcessor
    {
        #region Const.

        internal const string RUNTIME_ASSEMBLY_NAME = "FishNet.Runtime";

        #endregion

        private AddAsyncRpcMethodsQueue _asyncRpcMethodsQueue => AddAsyncRpcMethodsQueue.Instance;

        // Start is called before the first frame update
        void Start()
        {
        }

        // Update is called once per frame
        void Update()
        {

        }

        public override ILPostProcessor GetInstance() => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            if (compiledAssembly.Name.StartsWith("Unity."))
                return false;
            if (compiledAssembly.Name.StartsWith("UnityEngine."))
                return false;
            if (compiledAssembly.Name.StartsWith("UnityEditor."))
                return false;
            if (compiledAssembly.Name.Contains("Editor"))
                return false;
            bool referencesFishNet = IsFishNetAssembly(compiledAssembly) || compiledAssembly.References.Any(filePath =>
                Path.GetFileNameWithoutExtension(filePath) == RUNTIME_ASSEMBLY_NAME);
            return referencesFishNet;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            AssemblyDefinition assemblyDef = ILCoreHelper.GetAssemblyDefinition(compiledAssembly);
            if (assemblyDef == null)
                return null;
            CodegenSession session = new CodegenSession();
            if (!session.Initialize(assemblyDef.MainModule))
                return null;
            bool fnAssembly = IsFishNetAssembly(compiledAssembly);
            if (fnAssembly) return null;
            
            AssemblyNameReference anr = session.Module.AssemblyReferences.FirstOrDefault<AssemblyNameReference>(x => x.FullName == session.Module.Assembly.FullName);
            if (anr != null)
                session.Module.AssemblyReferences.Remove(anr);
            if (assemblyDef.Name.Name.Contains("AsyncRpc"))
            {
                var asyncRpcManagerTypeDefinition =
                    session.Module.Types.FirstOrDefault(x => nameof(AsyncRPCCallManager) == x.Name);
                var onStartMethod = asyncRpcManagerTypeDefinition.GetMethod(nameof(AsyncRPCCallManager.StartServerRPC));
                var onEndMethod = asyncRpcManagerTypeDefinition.GetMethod(nameof(AsyncRPCCallManager.EndServerRPC));
                _asyncRpcMethodsQueue.BeginHooking(onStartMethod, onEndMethod);
                return null;
            }
            bool modified = false;

            modified |= ProcessNetworkBehaviours(session);
            MemoryStream pe = new MemoryStream();
            MemoryStream pdb = new MemoryStream();
            WriterParameters writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(),
                SymbolStream = pdb,
                WriteSymbols = true
            };
            assemblyDef.Write(pe, writerParameters);
            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), session.Diagnostics);
        }

        private bool ProcessNetworkBehaviours(CodegenSession session)
        {
            //Get all network behaviours to process.
            List<TypeDefinition> networkBehaviourTypeDefs = session.Module.Types
                .Where(td => td.IsSubclassOf(session, typeof(NetworkBehaviour).FullName))
                .ToList();
            
            /* Remove types which are inherited. This gets the child most networkbehaviours.
             * Since processing iterates upward from each child there is no reason
             * to include any inherited NBs. */
            RemoveInheritedTypeDefinitions(networkBehaviourTypeDefs);
            foreach (var typeDef in networkBehaviourTypeDefs)
            {
                var methods = typeDef.Methods;
                var asyncRpcMethods = methods.Where(x =>
                    x.CustomAttributes.Any(a => a.AttributeType.FullName == typeof(AsyncRpcAttribute).FullName));
                foreach (var method in asyncRpcMethods)
                {
                    _asyncRpcMethodsQueue.AddRpcToHookQueue((x,y) => InjectOnLogicCompleteMethod(session, method, typeDef,x,y));
                }
            }

            return true;
            void RemoveInheritedTypeDefinitions(List<TypeDefinition> tds)
            {
                HashSet<TypeDefinition> inheritedTds = new HashSet<TypeDefinition>();
                /* Remove any networkbehaviour typedefs which are inherited by
                 * another networkbehaviour typedef. */
                for (int i = 0; i < tds.Count; i++)
                {
                    /* Iterates all base types and
                     * adds them to inheritedTds so long
                     * as the base type is not a NetworkBehaviour. */
                    TypeDefinition copyTd = tds[i].GetNextBaseTypeDefinition(session);
                    string networkBehaviourFullName = typeof(NetworkBehaviour).FullName;
                    while (copyTd != null)
                    {
                        //Class is NB.
                        if (copyTd.FullName == networkBehaviourFullName)
                            break;

                        inheritedTds.Add(copyTd);
                        copyTd = copyTd.GetNextBaseTypeDefinition(session);
                    }
                }

                //Remove all inherited types.
                foreach (TypeDefinition item in inheritedTds)
                    tds.Remove(item);
            }

        }

        private void InjectOnLogicCompleteMethod(CodegenSession session, MethodDefinition method, TypeDefinition typeDef, MethodDefinition rpcStartedMethod, MethodDefinition rpcFinishMethod)
        {
            string logicMethodPrefix = "RpcLogic___";
            string methodName = method.Name; // The name of the original method
            // Create a regex pattern to match methods like RpcLogic___methodname followed by anything
            string regexPattern = $@"^{logicMethodPrefix}{methodName}.*$";

            // Use Regex to find the matching method
            var logicMethod = typeDef.Methods
                .FirstOrDefault(x => Regex.IsMatch(x.Name, regexPattern));
            if (logicMethod == null)
            {
                logicMethod = typeDef.Methods
                    .FirstOrDefault(x => x.Name == methodName);
                if (logicMethod is null)
                {
                    return;
                }
            }
            ILProcessor processor = logicMethod.Body.GetILProcessor();
            MethodReference rpcStartMethodReference = method.Module.ImportReference(rpcStartedMethod);
            MethodReference rpcFinishMethodReference = method.Module.ImportReference(rpcFinishMethod);
            processor.Body.Instructions.Insert(0, processor.Create(OpCodes.Call, rpcStartMethodReference));
            if (processor.Body.Instructions[^1].OpCode == OpCodes.Ret)
            {
                processor.Body.Instructions.RemoveAt(processor.Body.Instructions.Count - 1);
                processor.Emit(OpCodes.Call, rpcFinishMethodReference);
                processor.Emit(OpCodes.Ret);
            }

            // throw new(string.Join("\n", processor.Body.Instructions.Select(x => x.ToString())));
        }


        internal static bool IsFishNetAssembly(ICompiledAssembly assembly) =>
            (assembly.Name == RUNTIME_ASSEMBLY_NAME);
    }
}
 
public static class AssemblyUtility
{
    public static AssemblyDefinition GetAssemblyByName(string assemblyName)
    {
        // Get all loaded assemblies
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        // Find the assembly that matches the specified name
        foreach (var assembly in assemblies)
        {
            if (assembly.GetName().Name == assemblyName)
            {
                // Load the assembly definition using Mono.Cecil
                return AssemblyDefinition.ReadAssembly(assembly.Location);
            }
        }

        return null; // Return null if not found
    }
    public static AssemblyDefinition GetAssemblyDefinitionFromLoadedAssembly(Assembly assembly)
    {
        if (assembly != null)
        {
            // Use a MemoryStream to create a ModuleDefinition from the loaded assembly
            using (MemoryStream ms = new MemoryStream())
            {
                assembly.GetManifestResourceStream(assembly.GetName().Name + ".dll")?.CopyTo(ms);
                ms.Position = 0; // Reset stream position

                // Read the assembly using Mono.Cecil
                var assemblyDefinition = AssemblyDefinition.ReadAssembly(ms);
                return assemblyDefinition;
            }
        }
        else
        {
            throw new InvalidOperationException($"Assembly '{assembly.FullName}' not found in the current AppDomain.");
        }
    }
}