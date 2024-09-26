using System;
using System.IO;
using System.Reflection;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using System.Linq;
using CodeGenerating;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace CodeGenerating
{
    public class AddAsyncRpcHook : ILPostProcessor
    {
        #region Const.

#if FISHNET
        internal const string RUNTIME_ASSEMBLY_NAME = "FishNet.Runtime";
#endif
        #endregion

        #region Hook Stuff
        private IAddHookMethodsService AsyncRpcHookMethodsService => AddAsyncRpcHookMethodsService.Instance;
        private IHookProvider _onStartHookProvider = new HookProvider(typeof(AsyncRPCCallManager), nameof(AsyncRPCCallManager.StartServerRPC));
        private IHookProvider _onEndHookProvider =
            new HookProvider(typeof(AsyncRPCCallManager), nameof(AsyncRPCCallManager.EndServerRPC));
        private IHookMethodAssemblyFinder _hookMethodAssemblyFinder = new AsyncRPCHookAssemblyFinder();

        private IHooksInjector _hooksInjector;
        private INetworkBehavioursProcessor _networkBehavioursProcessor;
        #endregion
        
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
            if (!PreProcess(compiledAssembly, out AssemblyDefinition assemblyDef, out CodegenSession session)) return null;
            
            if (_hookMethodAssemblyFinder.IsHookAssembly(assemblyDef, session))
            {
                var onStartHook = _onStartHookProvider.GetHookMethod(assemblyDef,  session);
                var onEndHook = _onEndHookProvider.GetHookMethod(assemblyDef, session);
                AsyncRpcHookMethodsService.BeginHooking(onStartHook, onEndHook);
                return null;
            };
            _networkBehavioursProcessor.ProcessNetworkBehaviours(session, AsyncRpcHookMethodsService, _hooksInjector);
            return RewriteAssemblyDefinition(assemblyDef, session);
        }

        private void ResolveHookDependencies()
        {
#if FISHNET
        _onStartHookProvider = new HookProvider(typeof(AsyncRPCCallManager), nameof(AsyncRPCCallManager.StartServerRPC));
        _onEndHookProvider =
            new HookProvider(typeof(AsyncRPCCallManager), nameof(AsyncRPCCallManager.EndServerRPC));
        _hookMethodAssemblyFinder = new AsyncRPCHookAssemblyFinder();
        _networkBehavioursProcessor = new FishnetNetworkBehavioursProcessor();
        _hooksInjector = new FishnetHooksInjector();
#endif
        }

        private ILPostProcessResult RewriteAssemblyDefinition(AssemblyDefinition assemblyDef, CodegenSession session)
        {
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
        private bool PreProcess(ICompiledAssembly compiledAssembly, out AssemblyDefinition assemblyDef,
            out CodegenSession session)
        {
            assemblyDef = ILCoreHelper.GetAssemblyDefinition(compiledAssembly);
            session = new CodegenSession();
            if (assemblyDef == null)
                return false;
            
            if (!session.Initialize(assemblyDef.MainModule))
                return false;
            
            // If this is fishnet assembly, ignore as it won't have user defined network behaviours.
            bool fnAssembly = IsFishNetAssembly(compiledAssembly);
            if (fnAssembly) return false;
            
            // From fishnet source, but I believe this was to filter out duplicates? Not sure.
            var sessionCopy = session;
            AssemblyNameReference anr = session.Module.AssemblyReferences.FirstOrDefault<AssemblyNameReference>(x => x.FullName == sessionCopy.Module.Assembly.FullName);
            if (anr != null)
                session.Module.AssemblyReferences.Remove(anr);
            ResolveHookDependencies();
            return true;
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
