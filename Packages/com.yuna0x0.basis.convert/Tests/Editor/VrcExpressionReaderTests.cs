using System.Collections.Generic;
using NUnit.Framework;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Tests
{
    public class VrcExpressionReaderTests
    {
        private static readonly string FixturePath =
            LocalFixtures.Find(LocalFixtures.ShinanoPrefab);

        private static UnityYamlDocument Only(IEnumerable<string> lines)
        {
            List<UnityYamlDocument> documents = UnityYamlScanner.Scan(lines);
            Assert.That(documents.Count, Is.EqualTo(1));
            return documents[0];
        }

        [Test]
        public void ReadsControlsAndTellsTheTwoNameFieldsApart()
        {
            // A control has a name, and its nested parameter block has a field also called
            // name. Reading them without tracking which block is open swaps the two.
            VrcExpressionMenu menu = VrcExpressionReader.ReadMenu(Only(new[]
            {
                "--- !u!114 &11400000",
                "MonoBehaviour:",
                "  m_Name: Root",
                "  controls:",
                "  - name: Hat",
                "    icon: {fileID: 2800000, guid: 2e15f60602810d745866d8a8618ac313, type: 3}",
                "    type: 102",
                "    parameter:",
                "      name: HatToggle",
                "    value: 1",
                "    style: 0",
                "    subMenu: {fileID: 0}",
                "    subParameters: []",
                "    labels: []",
                "  - name: More",
                "    icon: {fileID: 0}",
                "    type: 103",
                "    parameter:",
                "      name: ",
                "    value: 1",
                "    style: 0",
                "    subMenu: {fileID: 11400000, guid: 3eea7643cb594aa4eb7ff8a5cef17bb8, type: 2}",
                "    subParameters: []",
                "    labels: []",
            }), "abc");

            Assert.That(menu.Name, Is.EqualTo("Root"));
            Assert.That(menu.Controls.Count, Is.EqualTo(2));

            VrcExpressionControl toggle = menu.Controls[0];
            Assert.That(toggle.Name, Is.EqualTo("Hat"));
            Assert.That(toggle.Parameter, Is.EqualTo("HatToggle"),
                "The parameter name comes from the nested block, not the control's own name.");
            Assert.That(toggle.Type, Is.EqualTo(VrcExpressionControlType.Toggle));
            Assert.That(toggle.Value, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(toggle.HasIcon, Is.True);

            VrcExpressionControl subMenu = menu.Controls[1];
            Assert.That(subMenu.Type, Is.EqualTo(VrcExpressionControlType.SubMenu));
            Assert.That(subMenu.Parameter, Is.Empty);
            Assert.That(subMenu.SubMenuGuid, Is.EqualTo("3eea7643cb594aa4eb7ff8a5cef17bb8"));
            Assert.That(subMenu.HasIcon, Is.False);
        }

        [Test]
        public void ReadsParameters()
        {
            List<VrcExpressionParameter> parameters = VrcExpressionReader.ReadParameters(Only(new[]
            {
                "--- !u!114 &11400000",
                "MonoBehaviour:",
                "  m_Name: Params",
                "  parameters:",
                "  - name: VRCEmote",
                "    valueType: 0",
                "    saved: 1",
                "    defaultValue: 0",
                "    networkSynced: 1",
                "  - name: HatToggle",
                "    valueType: 2",
                "    saved: 1",
                "    defaultValue: 1",
                "    networkSynced: 0",
            }));

            Assert.That(parameters.Count, Is.EqualTo(2));
            Assert.That(parameters[0].Name, Is.EqualTo("VRCEmote"));
            Assert.That(parameters[0].Type, Is.EqualTo(VrcExpressionParameterType.Int));
            Assert.That(parameters[1].Type, Is.EqualTo(VrcExpressionParameterType.Bool));
            Assert.That(parameters[1].DefaultValue, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(parameters[1].NetworkSynced, Is.False);
        }

        [Test]
        public void CountingIsPerControlAcrossEveryMenu()
        {
            VrcExpressionInventory inventory = new VrcExpressionInventory();
            inventory.Menus.Add(new VrcExpressionMenu
            {
                Controls =
                {
                    new VrcExpressionControl { Type = VrcExpressionControlType.Toggle },
                    new VrcExpressionControl { Type = VrcExpressionControlType.SubMenu },
                },
            });
            inventory.Menus.Add(new VrcExpressionMenu
            {
                Controls = { new VrcExpressionControl { Type = VrcExpressionControlType.Toggle } },
            });

            Assert.That(inventory.ControlCount, Is.EqualTo(3));
            Assert.That(inventory.CountOf(VrcExpressionControlType.Toggle), Is.EqualTo(2));
            Assert.That(inventory.CountOf(VrcExpressionControlType.Button), Is.Zero);
        }

        [Test]
        public void TheWholeMenuTreeOfARealAvatarIsWalked()
        {
            if (FixturePath == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoPrefab}.");
            }

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);
            VrcExpressionInventory inventory = plan.Expressions;

            TestContext.WriteLine($"menus:      {inventory.Menus.Count}");
            TestContext.WriteLine($"controls:   {inventory.ControlCount}");
            TestContext.WriteLine($"parameters: {inventory.Parameters.Count}");
            foreach (VrcExpressionMenu menu in inventory.Menus)
            {
                TestContext.WriteLine($"  {menu.Name}: {menu.Controls.Count} controls");
            }

            Assert.That(inventory.Menus.Count, Is.GreaterThan(1),
                "Submenus are separate assets and should have been followed.");
            Assert.That(inventory.ControlCount, Is.GreaterThan(0));
            Assert.That(inventory.Parameters.Count, Is.GreaterThan(0));

            foreach (VrcExpressionMenu menu in inventory.Menus)
            {
                foreach (VrcExpressionControl control in menu.Controls)
                {
                    if (control.Type == VrcExpressionControlType.Toggle)
                    {
                        Assert.That(control.Parameter, Is.Not.Empty,
                            $"Toggle '{control.Name}' drives no parameter, which cannot be right.");
                    }
                }
            }
        }
    }
}
