using System;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using SharpDX.Direct3D9;
using Color = System.Drawing.Color;

namespace Sharpshooter.Champions
{
    public static class Kalista
    {
        static Obj_AI_Hero Player { get { return ObjectManager.Player; } }
        static Orbwalking.Orbwalker Orbwalker { get { return SharpShooter.Orbwalker; } }

        static Spell Q, W, E, R;

        public static void Load()
        {
            Q = new Spell(SpellSlot.Q, 1150f);
            W = new Spell(SpellSlot.W, 5500f);
            E = new Spell(SpellSlot.E, 1000f);
            R = new Spell(SpellSlot.R, 1200);

            Q.SetSkillshot(0.25f, 60f, 2000f, true, SkillshotType.SkillshotLine);

            SharpShooter.Menu.SubMenu("Combo").AddItem(new MenuItem("comboUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Combo").AddItem(new MenuItem("comboUseE", "Use E", true).SetValue(true));

            SharpShooter.Menu.SubMenu("Harass").AddItem(new MenuItem("harassUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Harass").AddItem(new MenuItem("harassUseE", "Use E", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Harass").AddItem(new MenuItem("harassMana", "if Mana % >", true).SetValue(new Slider(50, 0, 100)));

            SharpShooter.Menu.SubMenu("Laneclear").AddItem(new MenuItem("laneclearUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Laneclear").AddItem(new MenuItem("laneclearUseE", "Use E", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Laneclear").AddItem(new MenuItem("laneclearMana", "if Mana % >", true).SetValue(new Slider(50, 0, 100)));

            SharpShooter.Menu.SubMenu("Jungleclear").AddItem(new MenuItem("jungleclearUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Jungleclear").AddItem(new MenuItem("jungleclearUseE", "Use E", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Jungleclear").AddItem(new MenuItem("jungleclearMana", "if Mana % >", true).SetValue(new Slider(20, 0, 100)));

            SharpShooter.Menu.SubMenu("Misc").AddItem(new MenuItem("killsteal", "Use Killsteal", true).SetValue(true));

            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingAA", "Real AA Range", true).SetValue(new Circle(true, Color.FromArgb(0, 230, 255))));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingQ", "Q Range", true).SetValue(new Circle(true, Color.FromArgb(0, 230, 255))));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingW", "W Range", true).SetValue(new Circle(false, Color.FromArgb(0, 230, 255))));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingE", "E Range", true).SetValue(new Circle(true, Color.FromArgb(0, 230, 255))));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingR", "R Range", true).SetValue(new Circle(false, Color.FromArgb(0, 230, 255))));

            Game.OnGameUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Obj_AI_Hero.OnProcessSpellCast += Obj_AI_Hero_OnProcessSpellCast;
        }

        static void Game_OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead)
                return;

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
                Combo();

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed)
                Harass();

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
            {
                Laneclear();
                Jungleclear();
            }

            Killsteal();
        }

        static void Drawing_OnDraw(EventArgs args)
        {
            if (Player.IsDead)
                return;

            var drawingAA = SharpShooter.Menu.Item("drawingAA", true).GetValue<Circle>();
            var drawingQ = SharpShooter.Menu.Item("drawingQ", true).GetValue<Circle>();
            var drawingW = SharpShooter.Menu.Item("drawingW", true).GetValue<Circle>();
            var drawingE = SharpShooter.Menu.Item("drawingE", true).GetValue<Circle>();
            var drawingR = SharpShooter.Menu.Item("drawingR", true).GetValue<Circle>();

            if (drawingAA.Active)
                Render.Circle.DrawCircle(Player.Position, Orbwalking.GetRealAutoAttackRange(Player), drawingAA.Color);

            if (Q.IsReady() && drawingQ.Active)
                Render.Circle.DrawCircle(Player.Position, Q.Range, drawingQ.Color);

            if (W.IsReady() && drawingW.Active)
                Render.Circle.DrawCircle(Player.Position, W.Range, drawingW.Color);

            if (E.IsReady() && drawingE.Active)
                Render.Circle.DrawCircle(Player.Position, E.Range, drawingE.Color);

            if (R.IsReady() && drawingR.Active)
                Render.Circle.DrawCircle(Player.Position, R.Range, drawingR.Color);
        }

        static void Obj_AI_Hero_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe && args.SData.Name == "KalistaExpungeWrapper")
                Orbwalking.ResetAutoAttackTimer();
        }

        static void Killsteal()
        {
            if (!SharpShooter.Menu.Item("killsteal", true).GetValue<Boolean>())
                return;

            foreach (Obj_AI_Hero target in ObjectManager.Get<Obj_AI_Hero>().Where(x => x.IsValidTarget(E.Range) && x.IsEnemy && !x.HasBuffOfType(BuffType.Invulnerability) && !x.HasBuffOfType(BuffType.SpellShield)))
            {
                if (target != null)
                {
                    if (E.CanCast(target) && (target.Health + target.HPRegenRate) <= E.GetDamage(target))
                        E.Cast();
                }
            }
        }

        static void Combo()
        {
            if (!Orbwalking.CanMove(1))
                return;

            if (SharpShooter.Menu.Item("comboUseQ", true).GetValue<Boolean>())
            {
                var Qtarget = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical, true);
                var Qpred = Q.GetPrediction(Qtarget);

                if(Q.CanCast(Qtarget) && !Player.IsDashing() && Qpred.Hitchance >= HitChance.VeryHigh)
                    Q.Cast(Qtarget);
            }
                
            if (SharpShooter.Menu.Item("comboUseE", true).GetValue<Boolean>())
            {
                var Etarget = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical, true);

                if (E.CanCast(Etarget) && (Etarget.Health + Etarget.HPRegenRate) <= E.GetDamage(Etarget))
                    E.Cast();
            }

        }

        static void Harass()
        {
            if (!Orbwalking.CanMove(1) || !(Player.ManaPercentage() > SharpShooter.Menu.Item("harassMana", true).GetValue<Slider>().Value))
                return;

            if (SharpShooter.Menu.Item("harassUseQ", true).GetValue<Boolean>())
            {
                var Qtarget = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical, true);
                var Qpred = Q.GetPrediction(Qtarget);
                
                if (Q.CanCast(Qtarget) && !Player.IsDashing() && Qpred.Hitchance >= HitChance.VeryHigh)
                    Q.Cast(Qtarget);
            }

            if (SharpShooter.Menu.Item("harassUseE", true).GetValue<Boolean>())
            {
                var Etarget = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical, true);

                if (E.CanCast(Etarget) && (Etarget.Health + Etarget.HPRegenRate) <= E.GetDamage(Etarget))
                    E.Cast();
            }
        }

        static void Laneclear()
        {
            if (!Orbwalking.CanMove(1) || !(Player.ManaPercentage() > SharpShooter.Menu.Item("laneclearMana", true).GetValue<Slider>().Value))
                return;

            var Minions = MinionManager.GetMinions(Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.Enemy);

            if (Minions.Count <= 0)
                return;

            if (Q.IsReady() && SharpShooter.Menu.Item("laneclearUseQ", true).GetValue<Boolean>())
            {
                var Farmloc = Q.GetLineFarmLocation(Minions);

                if (Farmloc.MinionsHit >= 3)
                    Q.Cast(Farmloc.Position);
            }

            if (E.IsReady() && SharpShooter.Menu.Item("laneclearUseE", true).GetValue<Boolean>())
            {
                foreach (var Minion in Minions)
                {
                    if (Minion.Health <= E.GetDamage(Minion))
                    {
                        E.Cast();
                        break;
                    }
                }
            }
        }

        static void Jungleclear()
        {
            if (!Orbwalking.CanMove(1) || !(Player.ManaPercentage() > SharpShooter.Menu.Item("jungleclearMana", true).GetValue<Slider>().Value))
                return;

            var Mobs = MinionManager.GetMinions(Player.ServerPosition, Orbwalking.GetRealAutoAttackRange(Player) + 100, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);

            if (Mobs.Count < 1)
                return;

            if (Q.CanCast(Mobs[0]) && SharpShooter.Menu.Item("jungleclearUseQ", true).GetValue<Boolean>())
                Q.Cast(Mobs[0].Position);

            if (E.CanCast(Mobs[0]) && SharpShooter.Menu.Item("jungleclearUseE", true).GetValue<Boolean>())
            {
                if ((Mobs[0].Health + Mobs[0].HPRegenRate) <= E.GetDamage(Mobs[0]))
                    E.Cast();
            }
        }
    }
}
