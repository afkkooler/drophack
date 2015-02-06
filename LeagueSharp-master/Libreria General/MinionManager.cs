﻿using System.Collections.Generic;
using System.Linq;
using SharpDX;

namespace LeagueSharp.Common
{
    public enum MinionOrderTypes
    {
        None,
        Health,
        MaxHealth
    }

    public enum MinionTeam
    {
        Neutral,
        Ally,
        Enemy,
        NotAlly,
        NotAllyForEnemy,
        All
    }

    public enum MinionTypes
    {
        Ranged,
        Melee,
        All,
        Wards
    }

    public static class MinionManager
    {
        public static List<Obj_AI_Base> GetMinions(Vector3 from, float range, MinionTypes type = MinionTypes.All, MinionTeam team = MinionTeam.Enemy, MinionOrderTypes order = MinionOrderTypes.Health)
        {
            var result = (from minion in ObjectManager.Get<Obj_AI_Minion>() where minion.IsValidTarget(range, false, @from) let minionTeam = minion.Team where team == MinionTeam.Neutral && minionTeam == GameObjectTeam.Neutral || team == MinionTeam.Ally && minionTeam == (ObjectManager.Player.Team == GameObjectTeam.Chaos ? GameObjectTeam.Chaos : GameObjectTeam.Order) || team == MinionTeam.Enemy && minionTeam == (ObjectManager.Player.Team == GameObjectTeam.Chaos ? GameObjectTeam.Order : GameObjectTeam.Chaos) || team == MinionTeam.NotAlly && minionTeam != ObjectManager.Player.Team || team == MinionTeam.NotAllyForEnemy && (minionTeam == ObjectManager.Player.Team || minionTeam == GameObjectTeam.Neutral) || team == MinionTeam.All where minion.IsMelee() && type == MinionTypes.Melee || !minion.IsMelee() && type == MinionTypes.Ranged || type == MinionTypes.All where IsMinion(minion) || minionTeam == GameObjectTeam.Neutral select minion).Cast<Obj_AI_Base>().ToList();
            switch (order)
            {
                case MinionOrderTypes.Health:
                    result = result.OrderBy(o => o.Health).ToList();
                break;

                case MinionOrderTypes.MaxHealth:
                    result = result.OrderBy(o => o.MaxHealth).Reverse().ToList();
                break;
            }
            return result;
        }

        public static List<Obj_AI_Base> GetMinions(float range, MinionTypes type = MinionTypes.All, MinionTeam team = MinionTeam.Enemy, MinionOrderTypes order = MinionOrderTypes.Health)
        {
            return GetMinions(ObjectManager.Player.ServerPosition, range, type, team, order);
        }

        public static bool IsMinion(Obj_AI_Minion minion, bool includeWards = false)
        {
            var name = minion.BaseSkinName.ToLower();
            return name.Contains("minion") || (includeWards && (name.Contains("ward") || name.Contains("trinket")));
        }

        public static FarmLocation GetBestCircularFarmLocation(List<Vector2> minionPositions, float width, float range, int useMECMax = 9)
        {
            var result = new Vector2();
            var minionCount = 0;
            var startPos = ObjectManager.Player.ServerPosition.To2D();
            range = range * range;
            if (minionPositions.Count == 0)
            {
                return new FarmLocation(result, minionCount);
            }
            if (minionPositions.Count <= useMECMax)
            {
                var subGroups = GetCombinations(minionPositions);
                foreach (var subGroup in subGroups)
                {
                    if (subGroup.Count > 0)
                    {
                        var circle = MEC.GetMec(subGroup);

                        if (circle.Radius <= width && circle.Center.Distance(startPos, true) <= range)
                        {
                            minionCount = subGroup.Count;
                            return new FarmLocation(circle.Center, minionCount);
                        }
                    }
                }
            }
            else
            {
                foreach (var pos in minionPositions)
                {
                    if (pos.Distance(startPos, true) <= range)
                    {
                        var count = minionPositions.Count(pos2 => pos.Distance(pos2, true) <= width * width);

                        if (count >= minionCount)
                        {
                            result = pos;
                            minionCount = count;
                        }
                    }
                }
            }
            return new FarmLocation(result, minionCount);
        }

        public static FarmLocation GetBestLineFarmLocation(List<Vector2> minionPositions, float width, float range)
        {
            var result = new Vector2();
            var minionCount = 0;
            var startPos = ObjectManager.Player.ServerPosition.To2D();
            var max = minionPositions.Count;
            for (var i = 0; i < max; i++)
            {
                for (var j = 0; j < max; j++)
                {
                    if (minionPositions[j] != minionPositions[i])
                    {
                        minionPositions.Add((minionPositions[j] + minionPositions[i]) / 2);
                    }
                }
            }
            foreach (var pos in minionPositions)
            {
                if (pos.Distance(startPos, true) <= range * range)
                {
                    var endPos = startPos + range * (pos - startPos).Normalized();

                    var count =
                        minionPositions.Count(pos2 => pos2.Distance(startPos, endPos, true, true) <= width * width);

                    if (count >= minionCount)
                    {
                        result = endPos;
                        minionCount = count;
                    }
                }
            }
            return new FarmLocation(result, minionCount);
        }

        public static List<Vector2> GetMinionsPredictedPositions(List<Obj_AI_Base> minions, float delay, float width, float speed, Vector3 from, float range, bool collision, SkillshotType stype, Vector3 rangeCheckFrom = new Vector3())
        {
            from = from.To2D().IsValid() ? from : ObjectManager.Player.ServerPosition;
            return (from minion in minions select Prediction.GetPrediction(
                    new PredictionInput
                    {
                        Unit = minion,
                        Delay = delay,
                        Radius = width,
                        Speed = speed,
                        From = @from,
                        Range = range,
                        Collision = collision,
                        Type = stype,
                        RangeCheckFrom = rangeCheckFrom
                     })
                     into pos
                     where pos.Hitchance >= HitChance.High
                     select pos.UnitPosition.To2D()).ToList();
        }

        private static List<List<Vector2>> GetCombinations(List<Vector2> allValues)
        {
            var collection = new List<List<Vector2>>();
            for (var counter = 0; counter < (1 << allValues.Count); ++counter)
            {
                var combination = allValues.Where((t, i) => (counter & (1 << i)) == 0).ToList();

                collection.Add(combination);
            }
            return collection;
        }

        public struct FarmLocation
        {
            public int MinionsHit;
            public Vector2 Position;

            public FarmLocation(Vector2 position, int minionsHit)
            {
                Position = position;
                MinionsHit = minionsHit;
            }
        }
    }
}