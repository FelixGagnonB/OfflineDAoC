using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>One trained specialization line of an 8v8 build, at level 50.</summary>
    public readonly record struct PvpSpecLine(string Spec, int Level);

    /// <summary>
    /// A fixed level-50 build for the 8v8 mode: the class, the specialization lines it trains
    /// and the weapons it carries. Unlike the gamebot templates in <c>bots/specs</c> nothing
    /// in it is random, so two bots of the same build are identical.
    /// </summary>
    public sealed class PvpBuild
    {
        public string Id { get; }
        public eCharacterClass Class { get; }
        /// <summary>The existing gear and pet logic reads this to pick weapons and armour.</summary>
        public eSpecType SpecType { get; }
        public eObjectType WeaponOne { get; }
        public eObjectType WeaponTwo { get; }
        public bool TwoHanded { get; }
        public IReadOnlyList<PvpSpecLine> Lines { get; }

        public PvpBuild(string id, eCharacterClass characterClass, eSpecType specType, eObjectType weaponOne,
            eObjectType weaponTwo, bool twoHanded, params PvpSpecLine[] lines)
        {
            Id = id;
            Class = characterClass;
            SpecType = specType;
            WeaponOne = weaponOne;
            WeaponTwo = weaponTwo;
            TwoHanded = twoHanded;
            Lines = lines;
        }

        /// <summary>Specialization points the whole build costs.</summary>
        public int Cost => Lines.Sum(line => PvpBuildBudget.CostToTrain(line.Level));

        public BotSpec ToBotSpec() => new PvpPresetBotSpec(this);
    }

    /// <summary>Specialization point arithmetic, the same as <c>GameBot.SpendSpecPoints</c> uses.</summary>
    public static class PvpBuildBudget
    {
        public const int MaxLevel = 50;

        /// <summary>Points to raise one line from 1 to <paramref name="level"/>: 2 + 3 + ... + level.</summary>
        public static int CostToTrain(int level) => level <= 1 ? 0 : level * (level + 1) / 2 - 1;

        /// <summary>
        /// Points a character has earned by level 50, half levels included.
        /// <paramref name="multiplier"/> is the class value times ten (10 for casters, 20 for tanks).
        /// </summary>
        public static int TotalPoints(int multiplier)
        {
            int total = 0;
            for (int level = 2; level <= MaxLevel; level++)
            {
                total += level <= 5 ? level : multiplier * level / 10;
                if (level > 40)
                    total += multiplier * (level - 1) / 20;
            }
            return total;
        }
    }

    /// <summary>A <see cref="BotSpec"/> that trains exactly the lines of a <see cref="PvpBuild"/>.</summary>
    public sealed class PvpPresetBotSpec : BotSpec
    {
        public PvpBuild Build { get; }

        public PvpPresetBotSpec(PvpBuild build)
        {
            Build = build ?? throw new ArgumentNullException(nameof(build));
            SpecName = build.Id;
            SpecType = build.SpecType;
            WeaponOneType = build.WeaponOne;
            WeaponTwoType = build.WeaponTwo;
            Is2H = build.TwoHanded;
            // The ratio paces training while levelling; at level 50 every line ends on its cap.
            foreach (PvpSpecLine line in build.Lines)
                Add(line.Spec, (uint)line.Level, line.Level / (float)PvpBuildBudget.MaxLevel);
        }
    }
}
