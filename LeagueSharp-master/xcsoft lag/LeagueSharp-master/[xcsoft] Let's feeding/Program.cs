using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;

namespace _xcsoft__Let_s_feeding
{
    internal class Program
    {
        static Obj_AI_Hero Player { get { return ObjectManager.Player; } }

        static readonly GameMapId SummonerRift = (GameMapId)11;
        static readonly Vector3 SummonersRift_PurpleFountain = new Vector3(14400f, 14376f, 171.9777f);
        static readonly Vector3 SummonersRift_BlueFountain = new Vector3(420f, 422f, 183.5748f);

        private static SpellSlot Revive;
        private static SpellSlot Ghost;
        private static SpellSlot Flash;

        private static Menu Menu;

        static float lastmove;

        static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        static void Game_OnGameLoad(EventArgs args)
        {
            if (Game.MapId != SummonerRift)
                return;

            Revive = Player.GetSpellSlot("SummonerRevive");
            Ghost = Player.GetSpellSlot("SummonerHaste");
            Flash = Player.GetSpellSlot("SummonerFlash");

            Game.OnGameUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;

            Menu = new Menu("[xcsoft] Let's Feeding", "xcoft_feeder", true);
            Menu.AddItem(new MenuItem("switch", "Switch").SetValue(false));
            Menu.Item("switch").ValueChanged += Enabled_ValueChanged;
            Menu.AddItem(new MenuItem("enjoy", "Enjoy!"));
            Menu.AddToMainMenu();

            if (Menu.Item("switch").GetValue<bool>())
            {
                var level = new AutoLevel(new[] { 1, 2, 3, 1, 2, 4, 3, 1, 2, 3, 4, 1, 2, 3, 1, 4, 1, 2 });
            }

            Game.PrintChat("<font color = \"#33CCCC\">[xcsoft] Let's feeding -</font> Loaded");
        }
        private static void Enabled_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            AutoLevel.Enabled(e.GetNewValue<bool>());
        }

        static void Game_OnGameUpdate(EventArgs args)
        {
            if (!Menu.Item("switch").GetValue<bool>() || Game.ClockTime < 40) return;

            if( (Player.InShop() || Player.IsDead) && Player.InventoryItems.Length < 6)
            {
                if (Player.Gold >= 475 && Player.InventoryItems.Any(i => i.Id == ItemId.Boots_of_Mobility))
                    Player.BuyItem(ItemId.Boots_of_Mobility_Enchantment_Homeguard);

                if (Player.Gold >= 475 && Player.InventoryItems.Any(i => i.Id == ItemId.Boots_of_Speed))
                    Player.BuyItem(ItemId.Boots_of_Mobility);

                if (Player.Gold >= 325 && !Player.InventoryItems.Any(i => i.Id == ItemId.Boots_of_Speed))
                    Player.BuyItem(ItemId.Boots_of_Speed);
            }

            UnconditionalCastSpell(Revive);
            UnconditionalCastSpell(Ghost);
            UnconditionalCastSpell(Flash);

            if (Player.ChampionName != "Xerath" && Player.ChampionName != "Vi" && Player.ChampionName != "Varus")
                UnconditionalCastSpell(SpellSlot.Q);

            UnconditionalCastSpell(SpellSlot.W);
            UnconditionalCastSpell(SpellSlot.E);
            UnconditionalCastSpell(SpellSlot.R);

            if (Player.IsDead) return;

            if (Game.ClockTime >= lastmove + 1)//lag free
            {
                Game.Say("/laugh");
                Player.IssueOrder(GameObjectOrder.MoveTo, Player.Team == GameObjectTeam.Chaos ? SummonersRift_BlueFountain : SummonersRift_PurpleFountain);//laugh motion cancel and move
                lastmove = Game.ClockTime;
            }
        }

        static void UnconditionalCastSpell(SpellSlot spell)
        {
            if (Player.Spellbook.CanUseSpell(spell) != SpellState.Ready)
                return;

            var enemyfountainpos = Player.Team == GameObjectTeam.Chaos ? SummonersRift_BlueFountain : SummonersRift_PurpleFountain;

            var castpos = Player.ServerPosition.Extend(enemyfountainpos, 1000);
            var castpos2 = Player.ServerPosition.Extend(enemyfountainpos, 300);

            Player.Spellbook.CastSpell(spell, castpos);

            if (Player.Spellbook.CanUseSpell(spell) == SpellState.Ready)
                Player.Spellbook.CastSpell(spell, castpos2);

            if (Player.Spellbook.CanUseSpell(spell) == SpellState.Ready)
                Player.Spellbook.CastSpell(spell, Player);
            
            if (Player.Spellbook.CanUseSpell(spell) == SpellState.Ready)
            {
                foreach (Obj_AI_Hero target in ObjectManager.Get<Obj_AI_Hero>().Where(x => x.IsValidTarget(1000) && x.IsEnemy && !x.IsDead && x.IsTargetable))
                {
                    Player.Spellbook.CastSpell(spell, target);
                }
            }
        }

        static void Drawing_OnDraw(EventArgs args)
        {
            if (!Menu.Item("switch").GetValue<bool>()) return;

            var centerpos = Drawing.WorldToScreen(new Vector3(7350f, 7400f, 53.96267f));
            Drawing.DrawText(centerpos[0], centerpos[1], Color.Gray, "Death road");

            if (Player.IsDead) return;

            var playerpos = Drawing.WorldToScreen(Player.Position);
            Drawing.DrawText(playerpos[0] - 80, playerpos[1] + 50, Color.Gold, "Feeding in progress..");
            Render.Circle.DrawCircle(Player.Position, Player.BoundingRadius, Color.Gold);
        }
    }
}
