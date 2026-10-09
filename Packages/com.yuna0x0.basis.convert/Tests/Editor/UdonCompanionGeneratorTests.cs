using System.Collections.Generic;
using NUnit.Framework;
using yuna0x0.Basis.Convert.Mapping;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Tests
{
    public class UdonCompanionGeneratorTests
    {
        static UdonScriptData Lock()
        {
            UdonScriptData data = new UdonScriptData
            {
                Name = "Lock",
                IsBehaviour = true,
                SyncMode = "Manual",
                OverridesInteract = true,
                OverridesPlayerTriggerEnter = true,
            };
            data.EventMethods.Add("Interact");
            data.EventMethods.Add("Refresh");
            data.DeclaredHooks.Add("Start");
            data.Fields.Add(new UdonFieldData { Name = "isLocked", TypeName = "bool", Wire = UdonWireKind.Bool, Synced = true });
            data.Fields.Add(new UdonFieldData { Name = "_label", TypeName = "string", Wire = UdonWireKind.String, Synced = true, CallbackProperty = "Label" });
            data.Fields.Add(new UdonFieldData { Name = "spots", TypeName = "global::UnityEngine.Vector3[]", Wire = UdonWireKind.Vector3, IsArray = true, Synced = true });
            data.Fields.Add(new UdonFieldData { Name = "icon", TypeName = "global::UnityEngine.GameObject", Wire = UdonWireKind.Unsupported });
            return data;
        }

        [Test]
        public void DispatchesMethodsAndBuiltInAliases()
        {
            string text = UdonCompanionGenerator.Generate(Lock(), "test", new List<ConversionDiagnostic>());
            StringAssert.Contains("case \"Refresh\": Refresh(); return true;", text);
            StringAssert.Contains("case \"_interact\": Interact(); return true;", text);
            StringAssert.Contains("case \"_start\": Start(); return true;", text);
            StringAssert.Contains("return false;", text);
        }

        [Test]
        public void TypeIdMatchesUdonSharp()
        {
            // Expected values computed independently: SHA-256 of the UTF-8 full name, first 8 bytes
            // as a little-endian long, as UdonSharpInternalUtility.GetTypeID does.
            Assert.AreEqual(1559466544549460938L, UdonCompanionGenerator.UdonTypeId("QvPen.UdonScript.QvPen_Pen"));
            Assert.AreEqual(6252210704562570902L, UdonCompanionGenerator.UdonTypeId("DoorSystem"));
        }

        [Test]
        public void BakesTypeIdAndName()
        {
            string text = UdonCompanionGenerator.Generate(Lock(), "test", new List<ConversionDiagnostic>());
            StringAssert.Contains("public override long __UdonTypeID() { return " + UdonCompanionGenerator.UdonTypeId("Lock") + "L; }", text);
            StringAssert.Contains("public override string __UdonTypeName() { return \"Lock\"; }", text);
        }

        [Test]
        public void RaisesFirstOnEnableFromAwake()
        {
            UdonScriptData s = Lock();
            s.DeclaredHooks.Add("OnEnable");
            string text = UdonCompanionGenerator.Generate(s, "test", new List<ConversionDiagnostic>());
            StringAssert.Contains("[System.NonSerialized] private bool __udonOnEnableRan;", text);
            StringAssert.Contains("    __UdonAwake();\n", text.Replace("\r", ""));
            StringAssert.Contains("if (!__udonOnEnableRan) OnEnable();", text);
        }

        [Test]
        public void DispatchesNetworkCallableWithArguments()
        {
            UdonScriptData s = Lock();
            UdonCallableMethod m = new UdonCallableMethod { Name = "PrintMessage", MaxEventsPerSecond = 2 };
            m.ParameterTypes.Add("string");
            m.EnumUnderlying.Add(null);
            m.ParameterTypes.Add("global::Mode");
            m.EnumUnderlying.Add("int");
            s.CallableMethods.Add(m);
            string text = UdonCompanionGenerator.Generate(s, "test", new List<ConversionDiagnostic>());
            StringAssert.Contains("case \"PrintMessage\": return 2;", text);
            StringAssert.Contains("PrintMessage(args.Length > 0 && args[0] != null ? (string)args[0] : default(string), args.Length > 1 && args[1] != null ? (global::Mode)(int)args[1] : default(global::Mode)); return true;", text);
        }

        [Test]
        public void CarriesExecutionOrder()
        {
            UdonScriptData s = Lock();
            s.ExecutionOrder = -50;
            string text = UdonCompanionGenerator.Generate(s, "test", new List<ConversionDiagnostic>());
            StringAssert.Contains("public override int __UdonUpdateOrder() { return -50; }", text);
        }

        [Test]
        public void RebuildsUrlFieldsInAwake()
        {
            UdonScriptData s = Lock();
            s.Fields.Add(new UdonFieldData { Name = "page", TypeName = "global::VRC.SDKBase.VRCUrl" });
            s.Fields.Add(new UdonFieldData { Name = "pages", TypeName = "global::VRC.SDKBase.VRCUrl[]", IsArray = true });
            string text = UdonCompanionGenerator.Generate(s, "test", new List<ConversionDiagnostic>());
            StringAssert.Contains("private string __udonUrl_page;", text);
            StringAssert.Contains("private string[] __udonUrl_pages;", text);
            StringAssert.Contains("if (__udonUrl_page != null) page = new global::VRC.SDKBase.VRCUrl(__udonUrl_page);", text);
            StringAssert.Contains("public override void __UdonAwake()", text);
        }

        [Test]
        public void ReportsAwakeThatBlocksFirstOnEnable()
        {
            UdonScriptData s = Lock();
            s.DeclaredHooks.Add("OnEnable");
            s.DeclaredHooks.Add("Awake");
            var diagnostics = new List<ConversionDiagnostic>();
            string text = UdonCompanionGenerator.Generate(s, "test", diagnostics);
            StringAssert.DoesNotContain("__udonOnEnableRan", text);
            Assert.IsTrue(diagnostics.Exists(d => d.Code == "script.awake.declared"));
        }

        [Test]
        public void SetVarGoesThroughFieldChangeCallbacks()
        {
            string text = UdonCompanionGenerator.Generate(Lock(), "test", new List<ConversionDiagnostic>());
            StringAssert.Contains("case \"_label\": if (!object.Equals(_label, value)) Label = (string)value; return true;", text);
            StringAssert.Contains("case \"icon\": icon = (global::UnityEngine.GameObject)value; return true;", text);
        }

        [Test]
        public void SerializesSyncedFieldsInOrder()
        {
            string text = UdonCompanionGenerator.Generate(Lock(), "test", new List<ConversionDiagnostic>());
            int a = text.IndexOf("writer.Write(isLocked);");
            int b = text.IndexOf("writer.Write(_label != null ? _label : \"\");");
            int c = text.IndexOf("writer.Write(spots.Length);");
            Assert.That(a >= 0 && b > a && c > b, "fields written in declaration order");
            StringAssert.Contains("string v0 = reader.ReadString();", text);
            StringAssert.Contains("if (!object.Equals(_label, v0)) Label = v0;", text);
            StringAssert.Contains("new global::UnityEngine.Vector3[n1]", text);
            StringAssert.DoesNotContain("writer.Write(icon", text);
        }

        [Test]
        public void WritesFlagsAndTriggerHook()
        {
            string text = UdonCompanionGenerator.Generate(Lock(), "test", new List<ConversionDiagnostic>());
            StringAssert.Contains("public override int __SyncMode() { return 3; }", text);
            StringAssert.Contains("public override bool __HasInteract() { return true; }", text);
            // Player trigger events come from the runtime's own volume test for every player (Unity
            // sends no trigger exit when a player is teleported out), so nothing is forwarded.
            StringAssert.DoesNotContain("__PlayerTrigger", text);
        }

        [Test]
        public void FirstConvertedClassStoresTheState()
        {
            string text = UdonCompanionGenerator.Generate(Lock(), "test", new List<ConversionDiagnostic>());
            StringAssert.Contains("private object __udonState;", text);
            StringAssert.Contains("public override object __GetState() { return __udonState; }", text);
            UdonScriptData sub = Lock();
            sub.UserBaseName = "BaseLock";
            StringAssert.DoesNotContain("__udonState", UdonCompanionGenerator.Generate(sub, "test", new List<ConversionDiagnostic>()));
        }

        [Test]
        public void SubclassChainsToItsBase()
        {
            UdonScriptData data = Lock();
            data.UserBaseName = "BaseLock";
            data.InheritedHooks.Add(new InheritedHook { Name = "Update", Virtual = true, Access = "public" });
            string text = UdonCompanionGenerator.Generate(data, "test", new List<ConversionDiagnostic>());
            StringAssert.Contains("return base.__Dispatch(eventName);", text);
            StringAssert.Contains("base.__Serialize(writer);", text);
            StringAssert.Contains("public override void Update() { base.Update(); }", text);
        }

        [Test]
        public void ReportsAnyModeWithSyncedFields()
        {
            UdonScriptData data = Lock();
            data.SyncMode = "Any";
            var diagnostics = new List<ConversionDiagnostic>();
            string text = UdonCompanionGenerator.Generate(data, "test", diagnostics);
            StringAssert.Contains("return 3;", text);
            Assert.IsTrue(diagnostics.HasCode("script.sync.anyAsManual"));
        }

        [Test]
        public void HelperClassGetsOnlyTheAttribute()
        {
            UdonScriptData data = new UdonScriptData { Namespace = "Pen", Name = "Util", IsStatic = true };
            string text = UdonCompanionGenerator.Generate(data, "test", new List<ConversionDiagnostic>());
            StringAssert.Contains("namespace Pen", text);
            StringAssert.Contains("[Cilboxable]", text);
            StringAssert.Contains("static partial class Util", text);
            StringAssert.DoesNotContain("public static partial class Util", text);
            StringAssert.DoesNotContain("__Dispatch", text);
        }
    }
}
