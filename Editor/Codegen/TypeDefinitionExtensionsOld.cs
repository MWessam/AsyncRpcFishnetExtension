using System;
using System.Collections.Generic;
using CodeGenerating;
using MonoFN.Cecil;

namespace CodeGenerating
{
    public static class TypeDefinitionExtensions
    {
        internal static bool IsSubclassOf(this TypeDefinition typeDef,CodegenSession session, string ClassTypeFullName)
        {
            if (!typeDef.IsClass) return false;

            TypeReference baseTypeRef = typeDef.BaseType;
            while (baseTypeRef != null)
            {
                if (baseTypeRef.FullName == ClassTypeFullName)
                {
                    return true;
                }
                try
                {
                    baseTypeRef = baseTypeRef.CachedResolve(session).BaseType;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }
        /// <summary>
        /// Gets a Resolve favoring cached results first.
        /// </summary>
        internal static TypeDefinition CachedResolve(this TypeReference typeRef, CodegenSession session)
        {
            return session.GetClass<GeneralHelper>().GetTypeReferenceResolve(typeRef);
        }
        /// <summary>
        /// Returns the next base type.
        /// </summary>
        internal static TypeDefinition GetNextBaseTypeDefinition(this TypeDefinition typeDef, CodegenSession session)
        {
            return (typeDef.BaseType == null) ? null : typeDef.BaseType.CachedResolve(session);
        }
        /// <summary>
        /// Finds the first method by a given name.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <param name="methodName"></param>
        /// <returns></returns>
        internal static MethodDefinition GetMethod(this TypeDefinition typeDef, string methodName)
        {
            foreach (MethodDefinition md in typeDef.Methods)
            {
                if (md.Name == methodName)
                    return md;
            }

            return null;
        }
        internal static TypeDefinition GetBaseClassWithType<T>(this TypeDefinition typeDef, CodegenSession session)
        {
            TypeDefinition copyTd = typeDef;
            while (copyTd.BaseType.IsType(typeof(T)))
                copyTd = copyTd.BaseType.CachedResolve(session);

            return copyTd;
        }
        /// <summary>
        /// Returns if a typeRef is type.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsType(this TypeReference typeRef, Type type)
        {
            if (type.IsGenericType)
                return typeRef.GetElementType().FullName == type.FullName;
            else
                return typeRef.FullName == type.FullName;
        }
    }
}

internal class GeneralHelper : CodegenBase
{
    private Dictionary<TypeReference, TypeDefinition> _typeReferenceResolves = new Dictionary<TypeReference, TypeDefinition>();
    /// <summary>
    /// Gets a TypeDefinition for typeRef.
    /// </summary>
    public TypeDefinition GetTypeReferenceResolve(TypeReference typeRef)
    {
        TypeDefinition result;
        if (_typeReferenceResolves.TryGetValue(typeRef, out result))
        {
            return result;
        }
        else
        {
            result = typeRef.Resolve();
            AddTypeReferenceResolve(typeRef, result);
        }

        return result;
    }
    /// <summary>
    /// Adds a typeRef to TypeReferenceResolves.
    /// </summary>
    public void AddTypeReferenceResolve(TypeReference typeRef, TypeDefinition typeDef)
    {
        _typeReferenceResolves[typeRef] = typeDef;
    }
}