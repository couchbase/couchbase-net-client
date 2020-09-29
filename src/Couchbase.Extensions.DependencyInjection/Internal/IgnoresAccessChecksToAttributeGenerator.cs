using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    /// <summary>
    /// .NET Core applications won't respect the strong name signature added to the dynamic assembly for accessing protected
    /// members of Couchbase.Extensions.DependencyInjection via the InternalsVisibleTo attribute. Therefore, we instead add
    /// a custom attribute named IgnoresAccessChecksToAttribute to the dynamic assembly, naming the main assembly. This acts
    /// like InternalsVisibleTo but in the opposite direction. However, this "hidden" attribute recognized by the CLR is not
    /// distributed with .NET Core so we need to define the entire attribute.
    /// </summary>
    internal static class IgnoresAccessChecksToAttributeGenerator
    {
        public static void Generate(AssemblyBuilder assemblyBuilder, ModuleBuilder moduleBuilder)
        {
            var typeBuilder = moduleBuilder.DefineType("System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute",
                TypeAttributes.Class | TypeAttributes.NotPublic,
                typeof(Attribute));

            var fieldBuilder = typeBuilder.DefineField("_assemblyName", typeof(string),
                FieldAttributes.Private | FieldAttributes.InitOnly);

            var getMethodBuilder = typeBuilder.DefineMethod("get_AssemblyName",
                MethodAttributes.SpecialName | MethodAttributes.Public, CallingConventions.Standard | CallingConventions.HasThis,
                typeof(string), null);
            var ilGenerator = getMethodBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0); // Load this
            ilGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
            ilGenerator.Emit(OpCodes.Ret);

            var propertyBuilder = typeBuilder.DefineProperty("AssemblyName", PropertyAttributes.None, CallingConventions.Standard | CallingConventions.HasThis,
                typeof(string), null);
            propertyBuilder.SetGetMethod(getMethodBuilder);

            var baseConstructor = typeof(Attribute).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
                Type.EmptyTypes, null);

            var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public,
                CallingConventions.Standard | CallingConventions.HasThis, new[] {typeof(string)});
            ilGenerator = constructorBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0); // push "this"
            ilGenerator.Emit(OpCodes.Call, baseConstructor!);
            ilGenerator.Emit(OpCodes.Ldarg_0); // push "this"
            ilGenerator.Emit(OpCodes.Ldarg_1); // push assemblyName
            ilGenerator.Emit(OpCodes.Stfld, fieldBuilder);

            var attributeType = typeBuilder.CreateTypeInfo()!.AsType();
            var attributeConstructor = attributeType.GetConstructor(new[] {typeof(string)});

            var attributeBuilder = new CustomAttributeBuilder(attributeConstructor!,
                new object[] {Assembly.GetExecutingAssembly().GetName().Name});
            assemblyBuilder.SetCustomAttribute(attributeBuilder);
        }
    }
}
