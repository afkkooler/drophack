using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using SharpDX.Direct3D9;
using Color = System.Drawing.Color;
using Collision = LeagueSharp.Common.Collision;

namespace Sharpshooter.Champions
{
    public static class Jinx
    {
        static Obj_AI_Hero Player { get { return ObjectManager.Player; } }
        static Orbwalking.Orbwalker Orbwalker { get { return SharpShooter.Orbwalker; } }

        static Spell Q, W, E, R;

        static bool QisActive { get { return Player.HasBuff("JinxQ", true); } }

        static readonly int DefaultRange = 590;
        static float GetQActiveRange { get { return DefaultRange + ((25 * Q.Level) + 50); } }
        
        public static void Load()
        {
            Q = new Spell(SpellSlot.Q);
            W = new Spell(SpellSlot.W, 1450f);
            E = new Spell(SpellSlot.E, 900f);
            R = new Spell(SpellSlot.R, 2500f);

            W.SetSkillshot(0.65f, 60f, 1200f, true, SkillshotType.SkillshotLine);
            E.SetSkillshot(1.1f, 100f, 1750f, false, SkillshotType.SkillshotCircle);
            R.SetSkillshot(0.7f, 140f, 1700f, false, SkillshotType.SkillshotLine);

            SharpShooter.Menu.SubMenu("Combo").AddItem(new MenuItem("comboUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Combo").AddItem(new MenuItem("comboUseW", "Use W", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Combo").AddItem(new MenuItem("comboUseE", "Use E", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Combo").AddItem(new MenuItem("comboUseR", "Use R", true).SetValue(true));

            SharpShooter.Menu.SubMenu("Harass").AddItem(new MenuItem("harassUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Harass").AddItem(new MenuItem("harassUseW", "Use W", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Harass").AddItem(new MenuItem("harassMana", "if Mana % >", true).SetValue(new Slider(50, 0, 100)));

            SharpShooter.Menu.SubMenu("Laneclear").AddItem(new MenuItem("laneclearUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Laneclear").AddItem(new MenuItem("laneclearMana", "if Mana % >", true).SetValue(new Slider(60, 0, 100)));

            SharpShooter.Menu.SubMenu("Jungleclear").AddItem(new MenuItem("jungleclearUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Jungleclear").AddItem(new MenuItem("jungleclearUseW", "Use W", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Jungleclear").AddItem(new MenuItem("jungleclearMana", "if Mana % >", true).SetValue(new Slider(20, 0, 100)));

            SharpShooter.Menu.SubMenu("Misc").AddItem(new MenuItem("antigapcloser", "Use Anti-Gapcloser", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Misc").AddItem(new MenuItem("AutoE", "Autocast E On Immobile Targets", true).SetValue(true));

            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingAA", "Real AA Range", true).SetValue(new Circle(true, Color.HotPink)));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingW", "W Range", true).SetValue(new Circle(true, Color.HotPink)));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingE", "E Range", true).SetValue(new Circle(true, Color.HotPink)));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingR", "R Range", true).SetValue(new Circle(true, Color.HotPink)));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingPTimer", "Passive Timer", true).SetValue(true));

            Game.OnGameUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
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

            AutoE();
        }

        static void Drawing_OnDraw(EventArgs args)
        {
            if (Player.IsDead)
                return;

            var drawingAA = SharpShooter.Menu.Item("drawingAA", true).GetValue<Circle>();
            var drawingW = SharpShooter.Menu.Item("drawingW", true).GetValue<Circle>();
            var drawingE = SharpShooter.Menu.Item("drawingE", true).GetValue<Circle>();
            var drawingR = SharpShooter.Menu.Item("drawingR", true).GetValue<Circle>();

            if (drawingAA.Active)
                Render.Circle.DrawCircle(Player.Position, Orbwalking.GetRealAutoAttackRange(Player), drawingAA.Color);

            if (drawingW.Active && W.IsReady())
                Render.Circle.DrawCircle(Player.Position, W.Range, drawingW.Color);

            if (drawingE.Active && E.IsReady())
                Render.Circle.DrawCircle(Player.Position, E.Range, drawingE.Color);

            if (drawingR.Active && R.IsReady())
                Render.Circle.DrawCircle(Player.Position, R.Range, drawingR.Color);

            if (SharpShooter.Menu.Item("drawingPTimer", true).GetValue<Boolean>())
            {
                foreach (var buff in Player.Buffs)
                {
                    if (buff.Name == "jinxpassivekill")
                    {
                        var targetpos = Drawing.WorldToScreen(Player.Position);
                        Drawing.DrawText(targetpos[0] - 10, targetpos[1], Color.Gold, "" + (buff.EndTime - Game.ClockTime));
                        break;
                    }
                }
            }

            if (SharpShooter.Menu.Item("drawingTarget").GetValue<Boolean>())
            {
                var target = Orbwalker.GetTarget();

                if(target != null)
                {
                    if(QisActive)
                        Render.Circle.DrawCircle(target.Position, 160, Color.Red);
                }
            }

        }

        static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!SharpShooter.Menu.Item("antigapcloser", true).GetValue<Boolean>() || Player.IsDead)
                return;

            if (gapcloser.Sender.IsValidTarget(E.Range))
            {
                Render.Circle.DrawCircle(gapcloser.Sender.Position, gapcloser.Sender.BoundingRadius, Color.Gold, 5);
                var targetpos = Drawing.WorldToScreen(gapcloser.Sender.Position);
                Drawing.DrawText(targetpos[0] - 40, targetpos[1] + 20, Color.Gold, "Gapcloser");
            }

            if (E.CanCast(gapcloser.Sender) && E.GetPrediction(gapcloser.Sender).Hitchance >= HitChance.VeryHigh)
                E.Cast(Player);
        }

        static void QSwitchForUnit(AttackableUnit Unit)
        {
            if (Unit == null)
            {
                QSwitch(false);
                return;
            }

            if (Utility.CountEnemiesInRange(Unit.Position, 160) >= 2)
            {
                QSwitch(true);
                return;
            }

            if (Unit.IsValidTarget(DefaultRange))
                QSwitch(false);
            else
                QSwitch(true);

        }

        static void QSwitch(Boolean activate)
        {
            if (!Q.IsReady())
                return;

            if (QisActive && Player.IsWindingUp)
                return;

            if (activate && !QisActive)
                Q.Cast();
            else if (!activate && QisActive)
                Q.Cast();
        }

        static void AutoE()
        {
            if (!SharpShooter.Menu.Item("AutoE", true).GetValue<Boolean>())
                return;

            foreach (Obj_AI_Hero target in ObjectManager.Get<Obj_AI_Hero>().Where(x => x.IsEnemy && x.IsValidTarget(E.Range)))
            {
                if (E.CanCast(target) && UnitIsImmobileUntil(target) >= E.Delay)
                    E.Cast(target);
            }
        }

        static double UnitIsImmobileUntil(Obj_AI_Base unit)
        {
            var result =
                unit.Buffs.Where(
                    buff =>
                        buff.IsActive && Game.Time <= buff.EndTime &&
                        (buff.Type == BuffType.Charm || buff.Type == BuffType.Knockup || buff.Type == BuffType.Stun ||
                         buff.Type == BuffType.Suppression || buff.Type == BuffType.Snare))
                    .Aggregate(0d, (current, buff) => Math.Max(current, buff.EndTime));
            return (result - Game.Time);
        }

        static bool CollisionCheck(Obj_AI_Hero source, Vector3 targetpos, float width)
        {
            var input = new PredictionInput
            {
                Radius = width,
                Unit = source,
            };

            input.CollisionObjects[0] = CollisionableObjects.Heroes;

            return Collision.GetCollision(new List<Vector3> { targetpos }, input).Count() <= 1;
        }

        public static int CountEnemyMinionsInRange(this Vector3 point, float range)
        {
            return ObjectManager.Get<Obj_AI_Minion>().Count(h => h.IsValidTarget(range, true, point));
        }

        static void Combo()
        {
            if (!Orbwalking.CanMove(1))
                return;

            if (SharpShooter.Menu.Item("comboUseQ", true).GetValue<Boolean>())
                QSwitchForUnit(TargetSelector.GetTarget(GetQActiveRange + 30, TargetSelector.DamageType.Physical, true));

            if (SharpShooter.Menu.Item("comboUseW", true).GetValue<Boolean>())
            {
                var Wtarget = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Physical, true);

                if (W.CanCast(Wtarget) && !Wtarget.IsValidTarget(DefaultRange / 3) && W.GetPrediction(Wtarget).Hitchance >= HitChance.VeryHigh)
                    W.Cast(Wtarget);
            }

            if (SharpShooter.Menu.Item("comboUseE", true).GetValue<Boolean>())
            {
                var Etarget = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical, false);

                if (E.CanCast(Etarget) && !Etarget.HasBuffOfType(BuffType.SpellImmunity) && E.GetPrediction(Etarget).Hitchance >= HitChance.VeryHigh && Etarget.IsMoving)
                    E.Cast(Etarget);
            }

            if (SharpShooter.Menu.Item("comboUseR", true).GetValue<Boolean>())
            {
                foreach (Obj_AI_Hero Rtarget in ObjectManager.Get<Obj_AI_Hero>().Where(x => x.IsEnemy && x.IsValidTarget(R.Range) && !Player.HasBuffOfType(BuffType.SpellShield) && !Player.HasBuffOfType(BuffType.Invulnerability)))
                {
                    var Rpred = R.GetPrediction(Rtarget);

                    if (R.CanCast(Rtarget) && Rpred.Hitchance >= HitChance.VeryHigh && !Player.IsWindingUp)
                    {
                        var dis = Player.Distance(Rpred.CastPosition);
                        double predhealth = HealthPrediction.GetHealthPrediction(Rtarget, (int)(R.Delay + dis / R.Speed) * 1000) + Rtarget.HPRegenRate;

                        double RMinDamage = 75 + (50 * R.Level) + (Player.FlatPhysicalDamageMod * 0.5);
                        double RMaxDamage = RMinDamage * 2;

                        double RrangeDamage = RMaxDamage * ((dis / 1200) * 100);

                        if (RrangeDamage < RMinDamage) RrangeDamage = RMinDamage; else if (RrangeDamage > RMaxDamage) RrangeDamage = RMaxDamage;

                        double RbonusDamage = ((20 + (5 * R.Level)) / 100) * (Rtarget.MaxHealth - Rtarget.Health);

                        var RDamage = RrangeDamage + RbonusDamage;
                        var RCalcDamage = Damage.CalcDamage(Player, Rtarget, Damage.DamageType.Physical, RDamage);

                        //overkill check
                        if(Rtarget.IsValidTarget(DefaultRange))
                            predhealth -= Player.GetAutoAttackDamage(Rtarget, true) * 2;
                        else
                        if (Rtarget.IsValidTarget(GetQActiveRange - 50))
                            predhealth -= Player.GetAutoAttackDamage(Rtarget, true);
                        //--------------

                        if (CollisionCheck(Player, Rpred.UnitPosition, R.Width))
                        {
                            if (predhealth <= RCalcDamage)
                                R.Cast(Rtarget);
                        }
                        else
                        {
                            //can explosion kill check
                            if (predhealth <= RCalcDamage * 0.8)
                            {
                                foreach (Obj_AI_Hero ExplosionTarget in ObjectManager.Get<Obj_AI_Hero>().Where(x => x.IsEnemy && x.IsValidTarget(R.Range) && x.IsValidTarget(235, true, Rtarget.ServerPosition)))
                                {
                                    var pred = R.GetPrediction(ExplosionTarget);
                                    if (pred.Hitchance >= HitChance.VeryHigh && CollisionCheck(Player, pred.UnitPosition, R.Width))
                                        R.Cast(ExplosionTarget);

                                    Render.Circle.DrawCircle(ExplosionTarget.Position, 225, Color.Red);
                                    Render.Circle.DrawCircle(Rtarget.Position, Player.BoundingRadius, Color.Red);
                                }
                            }
                                
                        }
                    }
                }
            }
        }

        static void Harass()
        {
            if (!(Player.ManaPercentage() > SharpShooter.Menu.Item("harassMana", true).GetValue<Slider>().Value))
            {
                if (SharpShooter.Menu.Item("harassUseQ", true).GetValue<Boolean>())
                    QSwitch(false);

                return;
            }

            if (!Orbwalking.CanMove(1))
                return;

            if (SharpShooter.Menu.Item("harassUseQ", true).GetValue<Boolean>() && Q.IsReady())
                QSwitchForUnit(TargetSelector.GetTarget(GetQActiveRange + 30, TargetSelector.DamageType.Physical, true));

            if (SharpShooter.Menu.Item("harassUseW", true).GetValue<Boolean>() && W.IsReady())
            {
                var Wtarget = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Physical, true);

                if (W.CanCast(Wtarget) && W.GetPrediction(Wtarget).Hitchance >= HitChance.VeryHigh)
                    W.Cast(Wtarget);
            }
        }

        static void Laneclear()
        {
            if (!(Player.ManaPercentage() > SharpShooter.Menu.Item("laneclearMana", true).GetValue<Slider>().Value))
            {
                if (SharpShooter.Menu.Item("laneclearUseQ", true).GetValue<Boolean>()) 
                    QSwitch(false);

                return;
            }

            var Minions = MinionManager.GetMinions(Player.ServerPosition, GetQActiveRange, MinionTypes.All, MinionTeam.Enemy);

            if (Minions.Count <= 0)
                return;

            if (SharpShooter.Menu.Item("laneclearUseQ", true).GetValue<Boolean>())
            {
                var target = Orbwalker.GetTarget();

                if(target != null)
                    QSwitch((CountEnemyMinionsInRange(target.Position, 160) >= 2));
            }
                
        }

        static void Jungleclear()
        {
            if (!(Player.ManaPercentage() > SharpShooter.Menu.Item("jungleclearMana", true).GetValue<Slider>().Value))
            {
                if (SharpShooter.Menu.Item("jungleclearUseQ", true).GetValue<Boolean>())
                    QSwitch(false);

                return;
            }

            var Mobs = MinionManager.GetMinions(Player.ServerPosition, GetQActiveRange, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);

            if (Mobs.Count <= 0)
                return;

            if (SharpShooter.Menu.Item("jungleclearUseQ", true).GetValue<Boolean>() && Q.IsReady())
            {
                var target = Orbwalker.GetTarget();

                if (target != null)
                    QSwitch((CountEnemyMinionsInRange(target.Position, 160) >= 2));
            }

            if (!Orbwalking.CanMove(1))
                return;

            if (W.CanCast(Mobs[0]) && SharpShooter.Menu.Item("jungleclearUseW", true).GetValue<Boolean>())
                W.Cast(Mobs[0]);
        }

    }
}
