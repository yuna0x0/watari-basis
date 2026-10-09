using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>
    /// Reads a compiled UdonSharp class into <see cref="UdonScriptData"/>. Attributes and base types
    /// are matched by full name, so Watari needs no reference to the runtime package that defines
    /// them.
    /// </summary>
    public static class UdonScriptTypeReader
    {
        public const string BehaviourBase = "UdonSharp.UdonSharpBehaviour";
        const string SyncedAttribute = "UdonSharp.UdonSyncedAttribute";
        const string SyncModeAttribute = "UdonSharp.UdonBehaviourSyncModeAttribute";
        const string CallbackAttribute = "UdonSharp.FieldChangeCallbackAttribute";

        static readonly string[] Hooks = { "Awake", "Start", "Update", "LateUpdate", "FixedUpdate", "OnEnable", "OnDisable", "OnDestroy" };
        static readonly string[] PickupEvents = { "OnPickup", "OnDrop", "OnPickupUseDown", "OnPickupUseUp" };

        const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static bool IsBehaviour(Type type)
        {
            for (Type t = type.BaseType; t != null; t = t.BaseType)
            {
                if (t.FullName == BehaviourBase)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Generic methods fail Cilbox's export of the whole scene, so a class declaring one cannot convert.</summary>
        public static string FindGenericMethod(Type type)
        {
            MethodInfo method = type.GetMethods(Declared | BindingFlags.Static).FirstOrDefault(m => m.IsGenericMethodDefinition);
            return method?.Name;
        }

        public static UdonScriptData Read(Type type)
        {
            UdonScriptData data = new UdonScriptData
            {
                Namespace = type.Namespace,
                Name = type.Name,
                IsBehaviour = IsBehaviour(type),
                IsStatic = type.IsAbstract && type.IsSealed,
                IsAbstract = type.IsAbstract && !type.IsSealed,
            };

            if (!data.IsBehaviour)
            {
                return data;
            }

            CustomAttributeData order = type.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.FullName == "UnityEngine.DefaultExecutionOrder");
            if (order != null && order.ConstructorArguments.Count > 0 && order.ConstructorArguments[0].Value is int value)
            {
                data.ExecutionOrder = value;
            }

            Type baseType = type.BaseType;
            if (baseType != null && baseType.Assembly == type.Assembly && IsBehaviour(baseType))
            {
                data.UserBaseName = baseType.Name;
            }

            foreach (MethodInfo m in type.GetMethods(Declared))
            {
                if (m.IsSpecialName || m.IsGenericMethodDefinition || m.Name.StartsWith("__", StringComparison.Ordinal))
                {
                    continue;
                }

                if (m.IsPublic && m.GetParameters().Length == 0)
                {
                    data.EventMethods.Add(m.Name);
                }

                if (m.GetParameters().Length == 0 && Hooks.Contains(m.Name))
                {
                    data.DeclaredHooks.Add(m.Name);
                }

                CustomAttributeData callable = m.GetCustomAttributesData()
                    .FirstOrDefault(a => a.AttributeType.FullName == "VRC.SDK3.UdonNetworkCalling.NetworkCallableAttribute");
                if (callable != null && m.IsPublic && !m.IsStatic && m.ReturnType == typeof(void))
                {
                    UdonCallableMethod method = new UdonCallableMethod { Name = m.Name };
                    if (callable.ConstructorArguments.Count > 0 && callable.ConstructorArguments[0].Value is int rate)
                    {
                        method.MaxEventsPerSecond = rate;
                    }

                    foreach (ParameterInfo p in m.GetParameters())
                    {
                        method.ParameterTypes.Add(CSharpName(p.ParameterType));
                        method.EnumUnderlying.Add(p.ParameterType.IsEnum ? CSharpName(Enum.GetUnderlyingType(p.ParameterType)) : null);
                    }

                    data.CallableMethods.Add(method);
                }
            }

            foreach (string hook in Hooks)
            {
                if (data.DeclaredHooks.Contains(hook))
                {
                    continue;
                }

                MethodInfo inherited = FindInUserBases(type, hook, 0);
                if (inherited != null)
                {
                    data.InheritedHooks.Add(new InheritedHook
                    {
                        Name = hook,
                        Virtual = inherited.IsVirtual && !inherited.IsFinal,
                        Private = inherited.IsPrivate,
                        Access = AccessOf(inherited),
                    });
                }
            }

            foreach (FieldInfo f in type.GetFields(Declared))
            {
                if (f.IsInitOnly || f.IsLiteral || f.Name.Contains("<") || f.Name.StartsWith("__", StringComparison.Ordinal))
                {
                    continue;
                }

                data.Fields.Add(ReadField(f));
            }

            data.SyncMode = ReadSyncMode(type);
            data.OverridesInteract = OverriddenByUser(type, "Interact", 0);
            data.OverridesPickupEvents = PickupEvents.Any(name => OverriddenByUser(type, name, 0));
            data.OverridesPlayerTriggerEnter = OverriddenByUser(type, "OnPlayerTriggerEnter", 1);
            data.OverridesPlayerTriggerStay = OverriddenByUser(type, "OnPlayerTriggerStay", 1);
            data.OverridesPlayerTriggerExit = OverriddenByUser(type, "OnPlayerTriggerExit", 1);
            data.DeclaresTriggerEnter = DeclaredInUserChain(type, "OnTriggerEnter");
            data.DeclaresTriggerStay = DeclaredInUserChain(type, "OnTriggerStay");
            data.DeclaresTriggerExit = DeclaredInUserChain(type, "OnTriggerExit");
            return data;
        }

        static UdonFieldData ReadField(FieldInfo f)
        {
            Type t = f.FieldType;
            Type element = t.IsArray ? t.GetElementType() : t;
            UdonFieldData field = new UdonFieldData
            {
                Name = f.Name,
                TypeName = CSharpName(t),
                IsArray = t.IsArray,
                Wire = WireOf(element),
            };

            if (element.IsEnum)
            {
                field.EnumUnderlying = CSharpName(Enum.GetUnderlyingType(element));
            }

            foreach (CustomAttributeData attribute in f.GetCustomAttributesData())
            {
                string name = attribute.AttributeType.FullName;
                if (name == SyncedAttribute)
                {
                    field.Synced = true;
                }
                else if (name == CallbackAttribute && attribute.ConstructorArguments.Count > 0)
                {
                    field.CallbackProperty = attribute.ConstructorArguments[0].Value as string;
                }
            }

            return field;
        }

        static string ReadSyncMode(Type type)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                foreach (CustomAttributeData attribute in t.GetCustomAttributesData())
                {
                    if (attribute.AttributeType.FullName != SyncModeAttribute || attribute.ConstructorArguments.Count == 0)
                    {
                        continue;
                    }

                    CustomAttributeTypedArgument argument = attribute.ConstructorArguments[0];
                    return argument.ArgumentType.IsEnum
                        ? Enum.GetName(argument.ArgumentType, argument.Value) ?? "Any"
                        : "Any";
                }
            }

            return "Any";
        }

        static bool OverriddenByUser(Type type, string name, int parameterCount)
        {
            for (Type t = type; t != null && t.Assembly == type.Assembly; t = t.BaseType)
            {
                if (t.GetMethods(Declared).Any(m => m.Name == name && m.GetParameters().Length == parameterCount))
                {
                    return true;
                }
            }

            return false;
        }

        static bool DeclaredInUserChain(Type type, string name)
        {
            for (Type t = type; t != null && t.Assembly == type.Assembly; t = t.BaseType)
            {
                if (t.GetMethods(Declared).Any(m => m.Name == name))
                {
                    return true;
                }
            }

            return false;
        }

        static MethodInfo FindInUserBases(Type type, string name, int parameterCount)
        {
            for (Type t = type.BaseType; t != null && t.Assembly == type.Assembly; t = t.BaseType)
            {
                MethodInfo m = t.GetMethods(Declared).FirstOrDefault(x => x.Name == name && x.GetParameters().Length == parameterCount);
                if (m != null)
                {
                    return m;
                }
            }

            return null;
        }

        static string AccessOf(MethodInfo m)
        {
            if (m.IsPublic) return "public";
            if (m.IsFamilyOrAssembly) return "protected internal";
            if (m.IsFamily) return "protected";
            if (m.IsAssembly) return "internal";
            return "private";
        }

        static UdonWireKind WireOf(Type t)
        {
            if (t.IsEnum) return UdonWireKind.Enum;
            switch (t.FullName)
            {
                case "System.Boolean": return UdonWireKind.Bool;
                case "System.Char": return UdonWireKind.Char;
                case "System.Byte": return UdonWireKind.Byte;
                case "System.SByte": return UdonWireKind.SByte;
                case "System.Int16": return UdonWireKind.Short;
                case "System.UInt16": return UdonWireKind.UShort;
                case "System.Int32": return UdonWireKind.Int;
                case "System.UInt32": return UdonWireKind.UInt;
                case "System.Int64": return UdonWireKind.Long;
                case "System.UInt64": return UdonWireKind.ULong;
                case "System.Single": return UdonWireKind.Float;
                case "System.Double": return UdonWireKind.Double;
                case "System.String": return UdonWireKind.String;
                case "UnityEngine.Vector2": return UdonWireKind.Vector2;
                case "UnityEngine.Vector3": return UdonWireKind.Vector3;
                case "UnityEngine.Vector4": return UdonWireKind.Vector4;
                case "UnityEngine.Quaternion": return UdonWireKind.Quaternion;
                case "UnityEngine.Color": return UdonWireKind.Color;
                case "UnityEngine.Color32": return UdonWireKind.Color32;
                default: return UdonWireKind.Unsupported;
            }
        }

        static readonly Dictionary<string, string> Keywords = new Dictionary<string, string>
        {
            { "System.Boolean", "bool" }, { "System.Char", "char" }, { "System.Byte", "byte" },
            { "System.SByte", "sbyte" }, { "System.Int16", "short" }, { "System.UInt16", "ushort" },
            { "System.Int32", "int" }, { "System.UInt32", "uint" }, { "System.Int64", "long" },
            { "System.UInt64", "ulong" }, { "System.Single", "float" }, { "System.Double", "double" },
            { "System.String", "string" }, { "System.Object", "object" }, { "System.Decimal", "decimal" },
        };

        /// <summary>The type as C# source spells it, qualified with <c>global::</c> so no using is needed.</summary>
        public static string CSharpName(Type t)
        {
            if (t.IsArray)
            {
                return CSharpName(t.GetElementType()) + "[" + new string(',', t.GetArrayRank() - 1) + "]";
            }

            if (t.FullName != null && Keywords.TryGetValue(t.FullName, out string keyword))
            {
                return keyword;
            }

            string name = t.Name;
            int tick = name.IndexOf('`');
            if (tick >= 0)
            {
                name = name.Substring(0, tick);
            }

            if (t.IsGenericType)
            {
                name += "<" + string.Join(", ", t.GetGenericArguments().Select(CSharpName)) + ">";
            }

            if (t.IsNested)
            {
                return CSharpName(t.DeclaringType) + "." + name;
            }

            return string.IsNullOrEmpty(t.Namespace) ? "global::" + name : "global::" + t.Namespace + "." + name;
        }
    }
}
