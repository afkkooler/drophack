﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;

namespace Kiteo
{
    public static class Kiteo
    {
        public delegate void AfterAttackEvenH(AttackableUnit unit, AttackableUnit target);

        public delegate void BeforeAttackEvenH(BeforeAttackEventArgs args);

        public delegate void OnAttackEvenH(AttackableUnit unit, AttackableUnit target);

        public delegate void OnNonKillableMinionH(AttackableUnit minion);

        public delegate void OnTargetChangeH(AttackableUnit oldTarget, AttackableUnit newTarget);

        public enum OrbwalkingMode
        {
            LastHit,
            Mixed,
            LaneClear,
            Combo,
            None,
        }

        private static readonly string[] AttackResets =
        {
            "dariusnoxiantacticsonh", "fioraflurry", "garenq",
            "hecarimrapidslash", "jaxempowertwo", "jaycehypercharge", "leonashieldofdaybreak", "luciane", "lucianq",
            "monkeykingdoubleattack", "mordekaisermaceofspades", "nasusq", "nautiluspiercinggaze", "netherblade",
            "parley", "poppydevastatingblow", "powerfist", "renektonpreexecute", "rengarq", "shyvanadoubleattack",
            "sivirw", "takedown", "talonnoxiandiplomacy", "trundletrollsmash", "vaynetumble", "vie", "volibearq",
            "xenzhaocombotarget", "yorickspectral", "reksaiq"
        };

        private static readonly string[] NoAttacks =
        {
            "jarvanivcataclysmattack", "monkeykingdoubleattack",
            "shyvanadoubleattack", "shyvanadoubleattackdragon", "zyragraspingplantattack", "zyragraspingplantattack2",
            "zyragraspingplantattackfire", "zyragraspingplantattack2fire"
        };

        private static readonly string[] Attacks =
        {
            "caitlynheadshotmissile", "frostarrow", "garenslash2",
            "kennenmegaproc", "lucianpassiveattack", "masteryidoublestrike", "quinnwenhanced", "renektonexecute",
            "renektonsuperexecute", "rengarnewpassivebuffdash", "trundleq", "xenzhaothrust", "xenzhaothrust2",
            "xenzhaothrust3"
        };

        private static readonly string[] NoCancelChamps =
        {
            "Kalista"
        };

        public static int LastAATick;
        public static bool Attack = true;
        public static bool DisableNextAttack = false;
        public static bool Move = true;
        public static int LastMoveCommandT = 0;
        public static Vector3 LastMoveCommandPosition = Vector3.Zero;
        private static AttackableUnit _lastTarget;
        private static readonly Obj_AI_Hero Player;
        private static int _delay = 80;
        private static float _minDistance = 400;
        private static readonly Random _random = new Random(DateTime.Now.Millisecond);
        public static IEnumerable<Obj_AI_Hero> AllEnemys = ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.IsEnemy);
        public static IEnumerable<Obj_AI_Hero> AllAllys = ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.IsAlly);

        static Kiteo()
        {
            Player = ObjectManager.Player;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpell;
            GameObject.OnCreate += Obj_SpellMissile_OnCreate;
            Obj_AI_Hero.OnInstantStopAttack += ObjAiHeroOnOnInstantStopAttack;
        }

        private static void Obj_SpellMissile_OnCreate(GameObject sender, EventArgs args)
        {
            // Deny InvalidCastException
            if (sender is Obj_LampBulb)
            {
                return;
            }

            if (sender.IsValid<Obj_SpellMissile>())
            {
                var missile = (Obj_SpellMissile)sender;
                if (missile.SpellCaster.IsValid<Obj_AI_Hero>() && IsAutoAttack(missile.SData.Name))
                {
                    FireAfterAttack(missile.SpellCaster, _lastTarget);
                }
            }
        }

        public static event BeforeAttackEvenH BeforeAttack;

        public static event OnAttackEvenH OnAttack;

        public static event AfterAttackEvenH AfterAttack;

        public static event OnTargetChangeH OnTargetChange;

        public static event OnNonKillableMinionH OnNonKillableMinion;

        private static void FireBeforeAttack(AttackableUnit target)
        {
            if (BeforeAttack != null)
            {
                BeforeAttack(new BeforeAttackEventArgs { Target = target });
            }
            else
            {
                DisableNextAttack = false;
            }
        }

        private static void FireOnAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (OnAttack != null)
            {
                OnAttack(unit, target);
            }
        }

        private static void FireAfterAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (AfterAttack != null)
            {
                AfterAttack(unit, target);
            }
        }

        private static void FireOnTargetSwitch(AttackableUnit newTarget)
        {
            if (OnTargetChange != null && (!_lastTarget.IsValidTarget() || _lastTarget != newTarget))
            {
                OnTargetChange(_lastTarget, newTarget);
            }
        }

        private static void FireOnNonKillableMinion(AttackableUnit minion)
        {
            if (OnNonKillableMinion != null)
            {
                OnNonKillableMinion(minion);
            }
        }

        public static bool IsAutoAttackReset(string name)
        {
            return AttackResets.Contains(name.ToLower());
        }

        public static bool IsMelee(this Obj_AI_Base unit)
        {
            return unit.CombatType == GameObjectCombatType.Melee;
        }

        public static bool IsAutoAttack(string name)
        {
            return (name.ToLower().Contains("attack") && !NoAttacks.Contains(name.ToLower())) ||
                   Attacks.Contains(name.ToLower());
        }

         public static float GetRealAutoAttackRange(AttackableUnit target)
        {
            var result = Player.AttackRange + Player.BoundingRadius;
            if (target.IsValidTarget())
            {
                return result + target.BoundingRadius;
            }
            return result;
        }

        public static bool InAutoAttackRange(AttackableUnit target)
        {
            if (!target.IsValidTarget())
            {
                return false;
            }
            var myRange = GetRealAutoAttackRange(target);
            return
                Vector2.DistanceSquared((target is Obj_AI_Base) ? ((Obj_AI_Base)target).ServerPosition.To2D() : target.Position.To2D(), Player.ServerPosition.To2D()) <= myRange * myRange;
        }

        public static float GetMyProjectileSpeed()
        {
            return IsMelee(Player) ? float.MaxValue : Player.BasicAttack.MissileSpeed;
        }

        public static bool CanAttack()
        {
            if (LastAATick <= Environment.TickCount)
            {
                return Environment.TickCount + Game.Ping / 2 + 25 >= LastAATick + Player.AttackDelay * 1000 && Attack;
            }

            return false;
        }

        public static bool CanMove(float extraWindup)
        {
            if (LastAATick <= Environment.TickCount)
            {
                return Move && NoCancelChamps.Contains(Player.ChampionName) ? (Environment.TickCount - LastAATick > 250) : (Environment.TickCount + Game.Ping / 2 >= LastAATick + Player.AttackCastDelay * 1000 + extraWindup);
            }
            return false;
        }

        public static void SetMovementDelay(int delay)
        {
            _delay = delay;
        }

        public static void SetMinimumOrbwalkDistance(float d)
        {
            _minDistance = d;
        }

        public static float GetLastMoveTime()
        {
            return LastMoveCommandT;
        }

        public static Vector3 GetLastMovePosition()
        {
            return LastMoveCommandPosition;
        }

        private static void MoveTo(Vector3 position, float holdAreaRadius = 0, bool overrideTimer = false, bool useFixedDistance = true, bool randomizeMinDistance = true)
        {
            if (Environment.TickCount - LastMoveCommandT < _delay && !overrideTimer)
            {
                return;
            }

            LastMoveCommandT = Environment.TickCount;

            if (Player.ServerPosition.Distance(position) < holdAreaRadius)
            {
                if (Player.Path.Count() > 1)
                {
                    Player.IssueOrder(GameObjectOrder.HoldPosition, Player.ServerPosition);
                    LastMoveCommandPosition = Player.ServerPosition;
                }
                return;
            }

            var point = position;
            if (useFixedDistance)
            {
                point = Player.ServerPosition + (randomizeMinDistance ? (_random.NextFloat(0.6f, 1) + 0.2f) * _minDistance : _minDistance) * (position.To2D() - Player.ServerPosition.To2D()).Normalized().To3D();
            }
            else
            {
                if (randomizeMinDistance)
                {
                    point = Player.ServerPosition + (_random.NextFloat(0.6f, 1) + 0.2f) * _minDistance * (position.To2D() - Player.ServerPosition.To2D()).Normalized().To3D();
                }
                else if (Player.ServerPosition.Distance(position) > _minDistance)
                {
                    point = Player.ServerPosition + _minDistance * (position.To2D() - Player.ServerPosition.To2D()).Normalized().To3D();
                }
            }
            Player.IssueOrder(GameObjectOrder.MoveTo, point);
            LastMoveCommandPosition = point;
        }

        public static void Orbwalk(AttackableUnit target, Vector3 position, float extraWindup = 80, float holdAreaRadius = 0, bool useFixedDistance = true, bool randomizeMinDistance = true)
        {
            try
            {
                if (target.IsValidTarget() && CanAttack())
                {
                    DisableNextAttack = false;
                    FireBeforeAttack(target);
                    if (!DisableNextAttack)
                    {
                        Player.IssueOrder(GameObjectOrder.AttackUnit, target);
                        if (_lastTarget != null && _lastTarget.IsValid && _lastTarget != target)
                        {
                            LastAATick = Environment.TickCount + Game.Ping / 2;
                        }
                        _lastTarget = target;
                        return;
                    }
                }

                if (CanMove(extraWindup))
                {
                    MoveTo(position, holdAreaRadius, false, useFixedDistance, randomizeMinDistance);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static void ResetAutoAttackTimer()
        {
            LastAATick = 0;
        }

        private static void ObjAiHeroOnOnInstantStopAttack(Obj_AI_Base sender, GameObjectInstantStopAttackEventArgs args)
        {
            if (sender.IsValid && sender.IsMe && (args.BitData & 1) == 0 && ((args.BitData >> 4) & 1) == 1)
            {
                ResetAutoAttackTimer();
            }
        }


        private static void OnProcessSpell(Obj_AI_Base unit, GameObjectProcessSpellCastEventArgs Spell)
        {
            try
            {
                var spellName = Spell.SData.Name;
                if (IsAutoAttackReset(spellName) && unit.IsMe)
                {
                    Utility.DelayAction.Add(250, ResetAutoAttackTimer);
                }
                if (!IsAutoAttack(spellName))
                {
                    return;
                }
                if (unit.IsMe && Spell.Target is Obj_AI_Base)
                {
                    LastAATick = Environment.TickCount - Game.Ping / 2;
                    var target = (Obj_AI_Base)Spell.Target;
                    if (target.IsValid)
                    {
                        FireOnTargetSwitch(target);
                        _lastTarget = target;
                    }
                    if (unit.IsMelee())
                    {
                        Utility.DelayAction.Add((int)(unit.AttackCastDelay * 1000 + 40), () => FireAfterAttack(unit, _lastTarget));
                    }
                }
                FireOnAttack(unit, _lastTarget);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public class BeforeAttackEventArgs
        {
            public AttackableUnit Target;
            public Obj_AI_Base Unit = ObjectManager.Player;
            private bool _process = true;

            public bool Process
            {
                get { return _process; }
                set
                {
                    DisableNextAttack = !value;
                    _process = value;
                }
            }
        }

        public class Orbwalker
        {
            private const float LaneClearWaitTimeMod = 2f;
            private static Menu _config;
            private Obj_AI_Base _forcedTarget;
            private OrbwalkingMode _mode = OrbwalkingMode.None;
            private Vector3 _orbwalkingPoint;
            private Obj_AI_Minion _prevMinion;
            private readonly Obj_AI_Hero Player;

            public Orbwalker(Menu attachToMenu)
            {
                _config = attachToMenu;

                /* Drawings submenu */
                var drawings = new Menu("Señalizaciones", "Señalizaciones");
                drawings.AddItem(new MenuItem("AACircle", "Mi Rango Ataque Basico").SetShared().SetValue(new Circle(true, Color.FloralWhite)));
                drawings.AddItem(new MenuItem("AACircle2", "Rango Ataque Basico Enemigo").SetShared().SetValue(new Circle(true, Color.Pink)));
                drawings.AddItem(new MenuItem("Matar_Minion", "Señalizar Matar Minion").SetValue(new Circle(true, Color.Lime)));
                _config.AddSubMenu(drawings);

                /* Misc options */
                var misc = new Menu("Misc", "Misc");
                misc.AddItem(new MenuItem("HoldPosRadius", "Hold Position Radius").SetShared().SetValue(new Slider(0, 150, 0)));
                misc.AddItem(new MenuItem("PriorizeFarm", "Priorize farm over harass").SetShared().SetValue(true));
                _config.AddSubMenu(misc);


                /* Delay sliders */
                _config.AddItem(new MenuItem("ExtraWindup", "Extra windup time").SetShared().SetValue(new Slider(80, 200, 0)));
                _config.AddItem(new MenuItem("FarmDelay", "Farm delay").SetShared().SetValue(new Slider(0, 200, 0)));

                /*Load the menu*/
                _config.AddItem(new MenuItem("LastHit", "Last hit").SetShared().SetValue(new KeyBind('X', KeyBindType.Press)));
                _config.AddItem(new MenuItem("Farm", "Mixed").SetShared().SetValue(new KeyBind('C', KeyBindType.Press)));
                _config.AddItem(new MenuItem("LaneClear", "LaneClear").SetShared().SetValue(new KeyBind('V', KeyBindType.Press)));
                _config.AddItem(new MenuItem("Orbwalk", "Combo").SetShared().SetValue(new KeyBind(32, KeyBindType.Press)));

                Player = ObjectManager.Player;
                Game.OnGameUpdate += GameOnOnGameUpdate;
                Drawing.OnDraw += DrawingOnOnDraw;
            }

            private int FarmDelay
            {
                get 
                { 
                    return _config.Item("FarmDelay").GetValue<Slider>().Value; 
                }
            }

            public OrbwalkingMode ActiveMode
            {
                get
                {
                    if (_mode != OrbwalkingMode.None)
                    {
                        return _mode;
                    }

                    if (_config.Item("Orbwalk").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.Combo;
                    }

                    if (_config.Item("LaneClear").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.LaneClear;
                    }

                    if (_config.Item("Farm").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.Mixed;
                    }

                    if (_config.Item("LastHit").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.LastHit;
                    }

                    return OrbwalkingMode.None;
                }
                set 
                { 
                    _mode = value; 
                }
            }

            public void SetAttack(bool b)
            {
                Attack = b;
            }

            public void SetMovement(bool b)
            {
                Move = b;
            }

            public void ForceTarget(Obj_AI_Base target)
            {
                _forcedTarget = target;
            }

            public void SetOrbwalkingPoint(Vector3 point)
            {
                _orbwalkingPoint = point;
            }

            private bool ShouldWait()
            {
                return ObjectManager.Get<Obj_AI_Minion>().Any(minion =>minion.IsValidTarget() && minion.Team != GameObjectTeam.Neutral && InAutoAttackRange(minion) && HealthPrediction.LaneClearHealthPrediction(minion, (int) ((Player.AttackDelay * 1000) * LaneClearWaitTimeMod), FarmDelay) <=Player.GetAutoAttackDamage(minion));
            }

            public AttackableUnit GetTarget()
            {
                AttackableUnit result = null;

                if ((ActiveMode == OrbwalkingMode.Mixed || ActiveMode == OrbwalkingMode.LaneClear) &&
                    !_config.Item("PriorizeFarm").GetValue<bool>())
                {
                    var target = TargetSelector.GetTarget(-1, TargetSelector.DamageType.Physical);
                    if (target != null)
                    {
                        return target;
                    }
                }

                /*Killable Minion*/
                if (ActiveMode == OrbwalkingMode.LaneClear || ActiveMode == OrbwalkingMode.Mixed ||ActiveMode == OrbwalkingMode.LastHit)
                {
                    foreach (var minion in ObjectManager.Get<Obj_AI_Minion>().Where(minion => minion.IsValidTarget() && InAutoAttackRange(minion) && minion.Health < 2 * (ObjectManager.Player.BaseAttackDamage + ObjectManager.Player.FlatPhysicalDamageMod)))
                    {
                        var t = (int)(Player.AttackCastDelay * 1000) - 100 + Game.Ping / 2 + 1000 * (int)Player.Distance(minion) / (int)GetMyProjectileSpeed();
                        var predHealth = HealthPrediction.GetHealthPrediction(minion, t, FarmDelay);
                        if (minion.Team != GameObjectTeam.Neutral && MinionManager.IsMinion(minion, true))
                        {
                            if (predHealth <= 0)
                            {
                                FireOnNonKillableMinion(minion);
                            }
                            if (predHealth > 0 && predHealth <= Player.GetAutoAttackDamage(minion, true))
                            {
                                return minion;
                            }
                        }
                    }
                }

                //Forced target
                if (_forcedTarget.IsValidTarget() && InAutoAttackRange(_forcedTarget))
                {
                    return _forcedTarget;
                }

                /* turrets / inhibitors / nexus */
                if (ActiveMode == OrbwalkingMode.LaneClear)
                {
                    /* turrets */
                    foreach (var turret in ObjectManager.Get<Obj_AI_Turret>().Where(t => t.IsValidTarget() && InAutoAttackRange(t)))
                    {
                        return turret;
                    }

                    /* inhibitor */
                    foreach (var turret in ObjectManager.Get<Obj_BarracksDampener>().Where(t => t.IsValidTarget() && InAutoAttackRange(t)))
                    {
                        return turret;
                    }

                    /* nexus */
                    foreach (var nexus in ObjectManager.Get<Obj_HQ>().Where(t => t.IsValidTarget() && InAutoAttackRange(t)))
                    {
                        return nexus;
                    }
                }

                /*Champions*/
                if (ActiveMode != OrbwalkingMode.LastHit)
                {
                    var target = TargetSelector.GetTarget(-1, TargetSelector.DamageType.Physical);
                    if (target.IsValidTarget())
                    {
                        return target;
                    }
                }

                /*Jungle minions*/
                if (ActiveMode == OrbwalkingMode.LaneClear || ActiveMode == OrbwalkingMode.Mixed)
                {
                    result = ObjectManager.Get<Obj_AI_Minion>().Where(mob => mob.IsValidTarget() && InAutoAttackRange(mob) && mob.Team == GameObjectTeam.Neutral).MaxOrDefault(mob => mob.MaxHealth);
                    if (result != null)
                    {
                        return result;
                    }
                }

                /*Lane Clear minions*/
                if (ActiveMode == OrbwalkingMode.LaneClear)
                {
                    if (!ShouldWait())
                    {
                        if (_prevMinion.IsValidTarget() && InAutoAttackRange(_prevMinion))
                        {
                            var predHealth = HealthPrediction.LaneClearHealthPrediction(_prevMinion, (int)((Player.AttackDelay * 1000) * LaneClearWaitTimeMod), FarmDelay);
                            if (predHealth >= 2 * Player.GetAutoAttackDamage(_prevMinion) || Math.Abs(predHealth - _prevMinion.Health) < float.Epsilon)
                            {
                                return _prevMinion;
                            }
                        }
                        result = (from minion in ObjectManager.Get<Obj_AI_Minion>().Where(minion => minion.IsValidTarget() && InAutoAttackRange(minion)) let predHealth = HealthPrediction.LaneClearHealthPrediction(minion, (int)((Player.AttackDelay * 1000) * LaneClearWaitTimeMod), FarmDelay) where predHealth >= 2 * Player.GetAutoAttackDamage(minion) || Math.Abs(predHealth - minion.Health) < float.Epsilon select minion).MaxOrDefault(m => m.Health);
                        if (result != null)
                        {
                            _prevMinion = (Obj_AI_Minion)result;
                        }
                    }
                }
                return result;
            }

            private void GameOnOnGameUpdate(EventArgs args)
            {
                try
                {
                    if (ActiveMode == OrbwalkingMode.None)
                    {
                        return;
                    }
                    if (Player.IsChannelingImportantSpell())
                    {
                        return;
                    }
                    var target = GetTarget();
                    Orbwalk(target, (_orbwalkingPoint.To2D().IsValid()) ? _orbwalkingPoint : Game.CursorPos, _config.Item("ExtraWindup").GetValue<Slider>().Value, _config.Item("HoldPosRadius").GetValue<Slider>().Value);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            private void DrawingOnOnDraw(EventArgs args)
            {
                if (_config.Item("AACircle").GetValue<Circle>().Active)
                {
                    Render.Circle.DrawCircle(Player.Position, GetRealAutoAttackRange(null) + 65, _config.Item("AACircle").GetValue<Circle>().Color);
                }

                if (_config.Item("AACircle2").GetValue<Circle>().Active)
                {
                    foreach (var enemy in AllEnemys.Where(enemy => enemy.IsValidTarget(1500)))
                    {
                        Render.Circle.DrawCircle(enemy.Position, GetRealAutoAttackRange(enemy), _config.Item("AACircle2").GetValue<Circle>().Color);
                    }
                }

                if (_config.Item("Matar_Minion").GetValue<Circle>().Active)
                {
                    var minionList = MinionManager.GetMinions(Player.Position, GetRealAutoAttackRange(null) + 500, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.MaxHealth);
                    foreach (var minion in minionList.Where(minion => minion.IsValidTarget(GetRealAutoAttackRange(null) + 500)))
                    {
                        if (_config.Item("Matar_Minion").GetValue<Circle>().Active && minion.Health <= Player.GetAutoAttackDamage(minion, true))
                        {
                            Render.Circle.DrawCircle(minion.Position, minion.BoundingRadius, _config.Item("Matar_Minion").GetValue<Circle>().Color);
                        }
                    }
                }
            }
        }
    }
}