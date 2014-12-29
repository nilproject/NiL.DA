using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using NiL.JS;
using NiL.JS.Core;
using NiL.JS.Core.BaseTypes;
using NiL.JS.Core.TypeProxing;

namespace DynamicAssmsBuilder
{
    public sealed class Assembly
    {
        private Dictionary<string, Function> classes = new Dictionary<string, Function>();
        private string code;

        public Assembly(string source)
        {
            code = source;
            var script = new Script(source);
            script.Context.DefineVariable("registerClass").Assign(TypeProxy.Proxy(new Action<Function>(registerClass)));
            script.Invoke();
        }

        private void registerClass(Function constructor)
        {
            classes[constructor.name] = constructor;
        }

        public System.Reflection.Assembly Save(string filename, string assemblyName)
        {
            var thisPrm = Expression.Parameter(typeof(DynamicRuntime.DynamicClass), "this");
            var valuePrm = Expression.Parameter(typeof(DynamicRuntime.DynamicClass), "value");
            var getProp = typeof(DynamicRuntime.DynamicClass).GetMethod("GetProperty", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var setProp = typeof(DynamicRuntime.DynamicClass).GetMethod("SetProperty", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var getField = typeof(DynamicRuntime.DynamicClass).GetMethod("GetField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var setField = typeof(DynamicRuntime.DynamicClass).GetMethod("SetField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var defaultPrms = new[] { typeof(object) };

            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new System.Reflection.AssemblyName(assemblyName), System.Reflection.Emit.AssemblyBuilderAccess.RunAndSave);
            var module = assembly.DefineDynamicModule(assemblyName + "Module", filename);
            var constructors = defineInitializator(module);
            foreach (var constructor in classes)
            {
                var type = module.DefineType(constructor.Key, System.Reflection.TypeAttributes.Class | System.Reflection.TypeAttributes.Public, typeof(DynamicRuntime.DynamicClass));
                var prototype = constructor.Value.prototype;
                foreach (var mname in prototype)
                {
                    var member = prototype[mname];
                    switch (member.ValueType)
                    {
                        case JSObjectType.Property:
                            {
                                var prop = type.DefineProperty(mname,
                                    System.Reflection.PropertyAttributes.None,
                                    typeof(object),
                                    Type.EmptyTypes);

                                var nameToken = module.GetStringConstant(mname);
                                var attributes = System.Reflection.MethodAttributes.Public;
                                if ((member.Attributes & JSObjectAttributes.NotConfigurable) == 0)
                                    attributes |= MethodAttributes.Virtual;
                                var gsp = member.Value as PropertyPair;

                                if (gsp.Getter != null)
                                {
                                    var getter = type.DefineMethod("get_" + mname, attributes, typeof(object), Type.EmptyTypes);
                                    var ilgen = getter.GetILGenerator();
                                    ilgen.Emit(OpCodes.Ldarg_0);
                                    ilgen.Emit(System.Reflection.Emit.OpCodes.Ldstr, nameToken.Token);
                                    ilgen.Emit(System.Reflection.Emit.OpCodes.Call, getProp);
                                    ilgen.Emit(OpCodes.Ret);
                                    prop.SetGetMethod(getter);
                                }

                                if (gsp.Setter != null)
                                {
                                    var setter = type.DefineMethod("set_" + mname, attributes, typeof(void), defaultPrms);
                                    var ilgen = setter.GetILGenerator();
                                    ilgen.Emit(OpCodes.Ldarg_0);
                                    ilgen.Emit(System.Reflection.Emit.OpCodes.Ldstr, nameToken.Token);
                                    ilgen.Emit(OpCodes.Ldarg_1);
                                    ilgen.Emit(System.Reflection.Emit.OpCodes.Call, setProp);
                                    ilgen.Emit(OpCodes.Ret);
                                    prop.SetSetMethod(setter);
                                }
                                break;
                            }
                        case JSObjectType.Function:
                            {
                                if (mname.StartsWith("get_") || mname.StartsWith("set_"))
                                    continue;
                                var nameToken = module.GetStringConstant(mname);
                                var func = member.Value as Function;
                                int len = (int)func.length.Value;
                                var method = type.DefineMethod(
                                    mname,
                                    (((member.Attributes & JSObjectAttributes.NotConfigurable) == 0) ? MethodAttributes.Virtual : MethodAttributes.Final) | MethodAttributes.Public,
                                    typeof(object),
                                    (from x in Enumerable.Range(0, len) select typeof(object)).ToArray());
                                var ilgen = method.GetILGenerator();
                                ilgen.Emit(OpCodes.Ldarg_0);
                                ilgen.Emit(OpCodes.Ldstr, nameToken.Token);
                                ilgen.Emit(OpCodes.Call, getField);
                                ilgen.Emit(OpCodes.Castclass, typeof(Function));
                                ilgen.Emit(OpCodes.Ldarg_0);
                                ilgen.Emit(OpCodes.Ldfld, typeof(DynamicRuntime.DynamicClass).GetField("implementation", BindingFlags.NonPublic | BindingFlags.Instance));
                                if (len > 0)
                                {
                                    ilgen.Emit(OpCodes.Newobj, typeof(Arguments).GetConstructor(Type.EmptyTypes));
                                    for (int i = 0; i < len; i++)
                                    {
                                        ilgen.Emit(OpCodes.Dup);
                                        ilgen.Emit(OpCodes.Ldarg_S, i);
                                        ilgen.Emit(OpCodes.Callvirt, typeof(Arguments).GetMethod("Add"));
                                    }
                                }
                                else
                                    ilgen.Emit(OpCodes.Ldnull);
                                ilgen.Emit(OpCodes.Callvirt, typeof(Function).GetMethod("Invoke", new[] { typeof(JSObject), typeof(Arguments) }));
                                ilgen.Emit(OpCodes.Ret);
                                break;
                            }
                        case JSObjectType.Bool:
                        case JSObjectType.Int:
                        case JSObjectType.Double:
                        case JSObjectType.String:
                            {
                                var prop = type.DefineProperty(mname, System.Reflection.PropertyAttributes.None, typeof(object), Type.EmptyTypes);

                                var nameToken = module.GetStringConstant(mname);

                                var getter = type.DefineMethod("get_" + mname, System.Reflection.MethodAttributes.Public, typeof(object), Type.EmptyTypes);
                                var ilgen = getter.GetILGenerator();
                                ilgen.Emit(OpCodes.Ldarg_0);
                                ilgen.Emit(System.Reflection.Emit.OpCodes.Ldstr, nameToken.Token);
                                ilgen.Emit(System.Reflection.Emit.OpCodes.Call, getField);
                                ilgen.Emit(OpCodes.Ret);
                                prop.SetGetMethod(getter);

                                if ((member.Attributes & JSObjectAttributes.NotConfigurable) == 0)
                                {
                                    var setter = type.DefineMethod("set_" + mname, System.Reflection.MethodAttributes.Public, typeof(void), defaultPrms);
                                    ilgen = setter.GetILGenerator();
                                    ilgen.Emit(OpCodes.Ldarg_0);
                                    ilgen.Emit(System.Reflection.Emit.OpCodes.Ldstr, nameToken.Token);
                                    ilgen.Emit(OpCodes.Ldarg_1);
                                    ilgen.Emit(System.Reflection.Emit.OpCodes.Call, setField);
                                    ilgen.Emit(OpCodes.Ret);
                                    prop.SetSetMethod(setter);
                                }
                                break;
                            }
                    }
                }
                var ctor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
                var ctorIlGen = ctor.GetILGenerator();
                ctorIlGen.Emit(OpCodes.Ldarg_0);
                //ctorIlGen.Emit(OpCodes.Ldsfld, constructors[constructor.Key]);
                //ctorIlGen.Emit(OpCodes.Call, typeof(System.Console).GetMethod("WriteLine", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(object) }, null));
                ctorIlGen.Emit(OpCodes.Ldsfld, constructors[constructor.Key]);
                ctorIlGen.Emit(OpCodes.Ldnull);
                //ctorIlGen.Emit(OpCodes.Newobj, typeof(Arguments).GetConstructor(Type.EmptyTypes));
                ctorIlGen.Emit(OpCodes.Call, typeof(DynamicRuntime.DynamicClass).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0]);
                ctorIlGen.Emit(OpCodes.Ret);
                type.CreateType();
            }
            var s = module.IsTransient();
            assembly.Save(filename);
            return assembly;
        }

        private Dictionary<string, FieldInfo> defineInitializator(System.Reflection.Emit.ModuleBuilder module)
        {
            var constructors = new Dictionary<string, FieldInfo>();

            var type = module.DefineType("<>Initializator", TypeAttributes.Abstract | TypeAttributes.Sealed);
            foreach (var jstype in classes)
                constructors[jstype.Key] = type.DefineField("_" + jstype.Key, typeof(Function), FieldAttributes.Static | FieldAttributes.Assembly);

            var registerClassMethod = type.DefineMethod("registerClass", MethodAttributes.Static | MethodAttributes.Private, typeof(void), new[] { typeof(Function) });
            var functionPrm = Expression.Parameter(typeof(Function), "constructor");
            var breakLabel = Expression.Label();
            Expression selector = Expression.Switch(Expression.Property(functionPrm, "name"), Expression.Empty(),
                (from c in classes select Expression.SwitchCase(Expression.Block(Expression.Assign(Expression.Field(null, constructors[c.Key]), functionPrm), Expression.Break(breakLabel)), Expression.Constant(c.Key))).ToArray());
            selector = Expression.Block(selector, Expression.Label(breakLabel));
            Expression.Lambda<Action<Function>>(selector, functionPrm).CompileToMethod(registerClassMethod);

            var constructor = type.DefineTypeInitializer();
            var ilgen = constructor.GetILGenerator();
            ilgen.Emit(OpCodes.Ldstr, module.GetStringConstant(code).Token);
            ilgen.Emit(OpCodes.Newobj, typeof(Script).GetConstructor(new[] { typeof(string) }));
            ilgen.Emit(OpCodes.Dup);
            ilgen.Emit(OpCodes.Call, typeof(Script).GetProperty("Context").GetMethod);
            ilgen.Emit(OpCodes.Ldstr, module.GetStringConstant("registerClass").Token);
            ilgen.Emit(OpCodes.Call, typeof(Context).GetMethod("DefineVariable"));
            ilgen.Emit(OpCodes.Ldnull);
            ilgen.Emit(OpCodes.Ldftn, registerClassMethod);
            ilgen.Emit(OpCodes.Newobj, typeof(Action<Function>).GetConstructors()[0]);
            ilgen.Emit(OpCodes.Call, new Func<object, object>(TypeProxy.Proxy).Method);
            ilgen.Emit(OpCodes.Callvirt, typeof(JSObject).GetMethod("Assign"));
            ilgen.Emit(OpCodes.Callvirt, typeof(Script).GetMethod("Invoke"));
            ilgen.Emit(OpCodes.Ret);
            type.CreateType();
            return constructors;
        }
    }
}
