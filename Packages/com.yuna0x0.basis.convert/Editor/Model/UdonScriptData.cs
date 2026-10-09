using System.Collections.Generic;

namespace yuna0x0.Basis.Convert.Model
{
    /// <summary>
    /// A compiled UdonSharp class as the companion generator needs it: what can be reached by name,
    /// what is synced, and which events it overrides. Read from the compiled type, so it reflects
    /// what the compiler accepted rather than what the source text looks like.
    /// </summary>
    public sealed class UdonScriptData
    {
        public string Namespace;
        public string Name;

        /// <summary>The class's base when it is another converted script; null when it derives from the runtime's base.</summary>
        public string UserBaseName;

        public bool IsBehaviour;

        /// <summary>The class's [DefaultExecutionOrder], or null.</summary>
        public int? ExecutionOrder;
        public bool IsStatic;
        public bool IsAbstract;

        /// <summary>Public parameterless instance methods declared on this class, by name.</summary>
        public List<string> EventMethods = new List<string>();

        /// <summary>Methods marked [NetworkCallable], with their parameter types and rate.</summary>
        public List<UdonCallableMethod> CallableMethods = new List<UdonCallableMethod>();

        /// <summary>Unity lifecycle hooks declared on this class (any access), for the <c>_start</c>-style aliases.</summary>
        public List<string> DeclaredHooks = new List<string>();

        /// <summary>Hooks a converted base class declares and this class does not, which Cilbox would not call.</summary>
        public List<InheritedHook> InheritedHooks = new List<InheritedHook>();

        /// <summary>Instance fields declared on this class, for variable access by name.</summary>
        public List<UdonFieldData> Fields = new List<UdonFieldData>();

        /// <summary>One of None, NoVariableSync, Continuous, Manual or Any, as the attribute states it (inherited).</summary>
        public string SyncMode = "Any";

        public bool OverridesInteract;
        public bool OverridesPickupEvents;
        public bool OverridesPlayerTriggerEnter;
        public bool OverridesPlayerTriggerStay;
        public bool OverridesPlayerTriggerExit;

        /// <summary>Unity trigger messages this class or a converted base declares itself.</summary>
        public bool DeclaresTriggerEnter;
        public bool DeclaresTriggerStay;
        public bool DeclaresTriggerExit;

        public bool HasSyncedFields
        {
            get
            {
                foreach (UdonFieldData field in Fields)
                {
                    if (field.Synced)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    public sealed class UdonFieldData
    {
        public string Name;

        /// <summary>The type as C# source spells it, fully qualified where it is not a keyword.</summary>
        public string TypeName;

        /// <summary>How the value is written to and read from the network, or null when it cannot be synced.</summary>
        public UdonWireKind Wire;

        public bool IsArray;
        public bool Synced;

        /// <summary>Property named by <c>[FieldChangeCallback]</c>, or null.</summary>
        public string CallbackProperty;

        /// <summary>Underlying integer type of an enum field, as C# spells it, or null.</summary>
        public string EnumUnderlying;
    }

    public enum UdonWireKind
    {
        Unsupported,
        Bool,
        Char,
        Byte,
        SByte,
        Short,
        UShort,
        Int,
        UInt,
        Long,
        ULong,
        Float,
        Double,
        String,
        Vector2,
        Vector3,
        Vector4,
        Quaternion,
        Color,
        Color32,
        Enum,
    }

    public sealed class UdonCallableMethod
    {
        public string Name;

        /// <summary>Parameter types as C# source spells them.</summary>
        public List<string> ParameterTypes = new List<string>();

        /// <summary>Underlying integer type per parameter when it is an enum, else null.</summary>
        public List<string> EnumUnderlying = new List<string>();

        public int MaxEventsPerSecond = 5;
    }

    public sealed class InheritedHook
    {
        public string Name;

        /// <summary>The base declares it virtual, so the subclass can override it and call the base.</summary>
        public bool Virtual;

        /// <summary>The base declares it private, so the subclass cannot reach it.</summary>
        public bool Private;

        /// <summary>Access as C# spells it (public, protected, internal, protected internal), for the override.</summary>
        public string Access;
    }
}
