using System;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using SharpDX.Direct3D9;
using Color = System.Drawing.Color;

namespace Sharpshooter.Champions
{
    public static class Vayne
    {
        static Obj_AI_Hero Player { get { return ObjectManager.Player; } }
        static Orbwalking.Orbwalker Orbwalker { get { return SharpShooter.Orbwalker; } }

        static Spell Q, E;

        public static void Load()
        {
            Q = new Spell(SpellSlot.Q, 900f);
            E = new Spell(SpellSlot.E, 595f);

            E.SetTargetted(0.25f, 2200f);

            SharpShooter.Menu.SubMenu("Combo").AddItem(new MenuItem("comboUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Combo").AddItem(new MenuItem("comboUseE", "Use E", true).SetValue(true));

            SharpShooter.Menu.SubMenu("Harass").AddItem(new MenuItem("harassUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Harass").AddItem(new MenuItem("harassUseE", "Use E", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Harass").AddItem(new MenuItem("harassMana", "if Mana % >", true).SetValue(new Slider(50, 0, 100)));

            SharpShooter.Menu.SubMenu("Laneclear").AddItem(new MenuItem("laneclearUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Laneclear").AddItem(new MenuItem("laneclearMana", "if Mana % >", true).SetValue(new Slider(60, 0, 100)));

            SharpShooter.Menu.SubMenu("Jungleclear").AddItem(new MenuItem("jungleclearUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Jungleclear").AddItem(new MenuItem("jungleclearUseE", "Use E", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Jungleclear").AddItem(new MenuItem("jungleclearMana", "if Mana % >", true).SetValue(new Slider(20, 0, 100)));

            SharpShooter.Menu.SubMenu("Misc").AddItem(new MenuItem("antigapcloser", "Use Anti-Gapcloser", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Misc").AddItem(new MenuItem("autointerrupt", "Use Auto-Interrupt", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Misc").AddItem(new MenuItem("AutoRQ", "Autocast Q When Using Ultimate", true).SetValue(true));

            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingAA", "Real AA Range", true).SetValue(new Circle(true, Color.FromArgb(183,0,0))));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingP", "Passive Range", true).SetValue(new Circle(true, Color.FromArgb(183, 0, 0))));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingQ", "Q Range", true).SetValue(new Circle(true, Color.FromArgb(183, 0, 0))));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingE", "E Range", true).SetValue(new Circle(false, Color.FromArgb(183, 0, 0))));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingRtimer", "R Timer", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingCond", "E Crash Prediction", true).SetValue(new Circle(true, Color.Red)));
            
            Game.OnGameUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Interrupter.OnPossibleToInterrupt += Interrupter_OnPossibleToInterrupt;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
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
        }

        static void Drawing_OnDraw(EventArgs args)
        {
            if (Player.IsDead)
                return;

            var drawingAA = SharpShooter.Menu.Item("drawingAA", true).GetValue<Circle>();
            var drawingP = SharpShooter.Menu.Item("drawingP", true).GetValue<Circle>();
            var drawingQ = SharpShooter.Menu.Item("drawingQ", true).GetValue<Circle>();
            var drawingE = SharpShooter.Menu.Item("drawingE", true).GetValue<Circle>();
            var drawingCond = SharpShooter.Menu.Item("drawingCond", true).GetValue<Circle>();

            if (drawingAA.Active)
                Render.Circle.DrawCircle(Player.Position, Orbwalking.GetRealAutoAttackRange(Player), drawingAA.Color);

            if (drawingP.Active)
                Render.Circle.DrawCircle(Player.Position, 2000, drawingP.Color);

            if (drawingQ.Active && Q.IsReady())
                Render.Circle.DrawCircle(Player.Position, Q.Range, drawingQ.Color);

            if (drawingE.Active && E.IsReady())
                Render.Circle.DrawCircle(Player.Position, E.Range, drawingE.Color);

            if (drawingCond.Active)
            {
                foreach (var En in ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.IsEnemy && hero.IsValidTarget(1100)))
                {
                    var EPred = E.GetPrediction(En);
                    int pushDist = 460;

                    for (int i = 0; i < pushDist; i += (int)En.BoundingRadius)
                    {
                        Vector3 loc3 = EPred.UnitPosition.To2D().Extend(Player.Position.To2D(), -i).To3D();

                        if (loc3.IsWall())
                            Render.Circle.DrawCircle(loc3, En.BoundingRadius, drawingCond.Color);

                    }
                }
            }

            if (SharpShooter.Menu.Item("drawingRtimer", true).GetValue<Boolean>())
            {
                foreach (var buff in Player.Buffs)
                {
                    if (buff.Name == "vayneinquisition")
                    {
                        var targetpos = Drawing.WorldToScreen(Player.Position);
                        Drawing.DrawText(targetpos[0] - 10, targetpos[1], Color.Gold, "" + (buff.EndTime - Game.ClockTime));
                        break;
                    }
                }
            }

            if(Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.None && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.LastHit)
            {
                if(Q.IsReady())
                    Render.Circle.DrawCircle(Game.CursorPos, 50, Color.Red);
            }
        }


        static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!SharpShooter.Menu.Item("antigapcloser", true).GetValue<Boolean>() || Player.IsDead)
                return;

            if (gapcloser.Sender.IsValidTarget(1000))
            {
                Render.Circle.DrawCircle(gapcloser.Sender.Position, gapcloser.Sender.BoundingRadius, Color.Gold, 5);
                var targetpos = Drawing.WorldToScreen(gapcloser.Sender.Position);
                Drawing.DrawText(targetpos[0] - 40, targetpos[1] + 20, Color.Gold, "Gapcloser");
            }

            if (E.CanCast(gapcloser.Sender))
                E.Cast(gapcloser.Sender);
            else
                Q.Cast(Player.ServerPosition.Extend(gapcloser.Sender.ServerPosition, - 300));
        }

        static void Interrupter_OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!SharpShooter.Menu.Item("autointerrupt", true).GetValue<Boolean>() || Player.IsDead)
                return;

            if (unit.IsValidTarget(1000))
            {
                Render.Circle.DrawCircle(unit.Position, unit.BoundingRadius, Color.Gold, 5);
                var targetpos = Drawing.WorldToScreen(unit.Position);
                Drawing.DrawText(targetpos[0] - 40, targetpos[1] + 20, Color.Gold, "Interrupt");
            }

            if (E.CanCast(unit))
                E.Cast(unit);
        }

        static void Obj_AI_Hero_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (SharpShooter.Menu.Item("AutoRQ", true).GetValue<Boolean>())
            {
                if(sender.IsMe && args.SData.Name == "vayneinquisition" && Q.IsReady())
                    Q.Cast(Game.CursorPos);
            }
        }

        static bool isUnderAllyTurret(Vector3 Position)
        {
            foreach (var tur in ObjectManager.Get<Obj_AI_Turret>().Where(turr => turr.IsAlly && (turr.Health != 0)))
            {
                if (tur.Distance(Position) <= 975f) return true;
            }
            return false;
        }

        static bool isAllyFountain(Vector3 Position)
        {
            float fountainRange = 750;
            var map = Utility.Map.GetMap();
            if (map != null && map.Type == Utility.Map.MapType.SummonersRift)
            {
                fountainRange = 1050;
            }
            return
                ObjectManager.Get<GameObject>().Where(spawnPoint => spawnPoint is Obj_SpawnPoint && spawnPoint.IsAlly).Any(spawnPoint => Vector2.Distance(Position.To2D(), spawnPoint.Position.To2D()) < fountainRange);
        }

        static void Combo()
        {
            if (!Orbwalking.CanMove(1))
                return;

            if (SharpShooter.Menu.Item("comboUseQ", true).GetValue<Boolean>())
            {
                var Qtarget = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical, true);

                if(Q.CanCast(Qtarget))
                    Q.Cast(Game.CursorPos);
            }
                
            if (E.IsReady() && SharpShooter.Menu.Item("comboUseE", true).GetValue<Boolean>())
            {
                foreach (var En in ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.IsEnemy && hero.IsValidTarget(E.Range) && !hero.HasBuffOfType(BuffType.SpellShield) && !hero.HasBuffOfType(BuffType.SpellImmunity)))
                {
                    //Part of VayneHunterRework

                    var EPred = E.GetPrediction(En);
                    int pushDist = 460;
                    var FinalPosition = EPred.UnitPosition.To2D().Extend(Player.ServerPosition.To2D(), -pushDist).To3D();

                    for (int i = 1; i < pushDist; i += (int)En.BoundingRadius)
                    {
                        Vector3 loc3 = EPred.UnitPosition.To2D().Extend(Player.ServerPosition.To2D(), -i).To3D();

                        if (loc3.IsWall() || isUnderAllyTurret(FinalPosition) || isAllyFountain(FinalPosition))
                            E.Cast(En);
                    }
                }
            }

        }

        static void Harass()
        {
            if (!Orbwalking.CanMove(1) || !(Player.ManaPercentage() > SharpShooter.Menu.Item("harassMana", true).GetValue<Slider>().Value))
                return;

            var target = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical, true);

            if (Q.CanCast(target) && SharpShooter.Menu.Item("harassUseQ", true).GetValue<Boolean>())
                Q.Cast(Game.CursorPos);

            if (E.CanCast(target) && SharpShooter.Menu.Item("harassUseE", true).GetValue<Boolean>())
            {
                foreach (var buff in target.Buffs)
                {
                    if (buff.Name == "vaynesilvereddebuf" && buff.Count == 2)
                    {
                        E.Cast(target);
                        break;
                    }
                }
            }
        }

        static void Laneclear()
        {
            if (!Orbwalking.CanMove(1) || !(Player.ManaPercentage() > SharpShooter.Menu.Item("laneclearMana", true).GetValue<Slider>().Value))
                return;

            var Minions = MinionManager.GetMinions(Player.ServerPosition, Q.Range, MinionTypes.All, MinionTeam.Enemy);

            if (Minions.Count <= 0)
                return;

            if (Q.IsReady() && SharpShooter.Menu.Item("laneclearUseQ", true).GetValue<Boolean>())
                Q.Cast(Game.CursorPos);
        }

        static void Jungleclear()
        {
            if (!Orbwalking.CanMove(1) || !(Player.ManaPercentage() > SharpShooter.Menu.Item("jungleclearMana", true).GetValue<Slider>().Value))
                return;

            var Mobs = MinionManager.GetMinions(Player.ServerPosition, Q.Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);

            if (Mobs.Count <= 0)
                return;

            if (Q.CanCast(Mobs[0]) && SharpShooter.Menu.Item("jungleclearUseQ", true).GetValue<Boolean>())
                Q.Cast(Game.CursorPos);

            if (E.CanCast(Mobs[0]) && SharpShooter.Menu.Item("jungleclearUseE", true).GetValue<Boolean>())
                E.Cast(Mobs[0]);
        }
    }
}
