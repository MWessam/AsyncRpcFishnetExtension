using System.Collections.Generic;
using FishNet.Object;
using MonoFN.Cecil;

namespace CodeGenerating
{
    internal interface INetworkBehavioursProcessor
    {
        internal static void RemoveInheritedTypeDefinitions(List<TypeDefinition> tds, CodegenSession session)
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
        bool ProcessNetworkBehaviours(CodegenSession session, IAddHookMethodsService addHookMethodsService, IHooksInjector hooksInjector);
    }
}