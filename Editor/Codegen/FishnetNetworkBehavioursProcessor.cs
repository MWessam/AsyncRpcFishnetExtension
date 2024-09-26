using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using MonoFN.Cecil;

namespace CodeGenerating
{
    internal class FishnetNetworkBehavioursProcessor : INetworkBehavioursProcessor
    {
        /// <summary>
        /// Processes all derived types of network behaviours defined by the user and adds hooks to all <see cref="AsyncRpcAttribute"/> methods.
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        bool INetworkBehavioursProcessor.ProcessNetworkBehaviours(CodegenSession session, IAddHookMethodsService addHookMethodsService, IHooksInjector hooksInjector)
        {
            //Get all network behaviours to process.
            List<TypeDefinition> networkBehaviourTypeDefs = session.Module.Types
                .Where(td => TypeDefinitionExtensions.IsSubclassOf(td, session, typeof(NetworkBehaviour).FullName))
                .ToList();
            /* Remove types which are inherited. This gets the child most networkbehaviours.
                 * Since processing iterates upward from each child there is no reason
                 * to include any inherited NBs. */
            INetworkBehavioursProcessor.RemoveInheritedTypeDefinitions(networkBehaviourTypeDefs, session);
            foreach (var typeDef in networkBehaviourTypeDefs)
            {
                var methods = typeDef.Methods;
                var asyncRpcMethods = methods.Where(x =>
                    x.CustomAttributes.Any(a => a.AttributeType.FullName == typeof(AsyncRpcAttribute).FullName));
                foreach (var method in asyncRpcMethods)
                {
                    // Store them in a static queue which shouldnt reset on each assembly (Only when the entire thing recompiles which is ideal for us)
                    // This way we can inject the required method from other assemblies once we find them as that is the only way I could workaround not having direct type references
                    // In the same assembly.
                    addHookMethodsService.AddMethodToHookInto((x,y) => hooksInjector.InjectOnLogicComplete(session, method, typeDef,x,y));
                }
            }

            return true;
        }
    }
}