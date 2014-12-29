using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NiL.JS.Core;
using NiL.JS.Core.BaseTypes;
using NiL.JS.Core.Functions;
using NiL.JS.Core.TypeProxing;

namespace DynamicRuntime
{
    public abstract class DynamicClass
    {
        protected readonly JSObject implementation;

        protected DynamicClass(Function constructor, Arguments args)
        {
            var impl = JSObject.CreateObject();
            impl.__proto__ = constructor.prototype;
            var r = constructor.Invoke(impl, args);
            if (r.ValueType >= JSObjectType.Object && r.Value != null)
                impl = r;
            implementation = impl;
            var members = this.GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            for (var i = 0; i < members.Length; i++)
            {
                if (members[i].DeclaringType.BaseType != typeof(DynamicClass)
                    && typeof(DynamicClass).IsAssignableFrom(members[i].DeclaringType.BaseType)) // элемент перегружен или добавлен
                {
                    if (members[i].Name.StartsWith("get_"))
                    {
                        var name = members[i].Name.Substring(4);
                        implementation.__defineGetter__(new Arguments { name, new MethodProxy(members[i], this) });
                    }
                    else if (members[i].Name.StartsWith("set_"))
                    {
                        var name = members[i].Name.Substring(4);
                        implementation.__defineSetter__(new Arguments { name, new MethodProxy(members[i], this) });
                    }
                    else
                    {
                        SetField(members[i].Name, new MethodProxy(members[i], this));
                    }
                }
            }
        }

        protected object GetField(string name)
        {
            return implementation.GetMember(name).Value;
        }

        protected void SetField(string name, object value)
        {
            implementation.DefineMember(name).Assign(TypeProxy.Proxy(value));
        }

        protected object GetProperty(string name)
        {
            return (implementation.GetMember(name).Value as PropertyPair).Getter.Invoke(implementation, null);
        }

        protected void SetProperty(string name, object value)
        {
            (implementation.GetMember(name).Value as PropertyPair).Setter.Invoke(implementation, new Arguments { TypeProxy.Proxy(value) });
        }
    }
}
