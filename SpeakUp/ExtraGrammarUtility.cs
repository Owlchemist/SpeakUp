﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.Grammar;

namespace SpeakUp
{
	public static class ExtraGrammarUtility
	{
        const string
            initPrefix = "INITIATOR_",
            reciPrefix = "RECIPIENT_";

        private static Func<SkillRecord, SkillRecord, SkillRecord> AccessHighestPassion = (A, B) =>
        {
            return (A.passion >= B.passion) ? A : B;
        };

        private static Func<SkillRecord, SkillRecord, SkillRecord> AccessHighestSkill = (A, B) =>
        {
            int a = A.levelInt;
            int b = B.levelInt;
            if (a == b) return (A.passion >= B.passion) ? A : B;
            return (a > b) ? A : B;
        };

        private static Func<SkillRecord, SkillRecord, SkillRecord> AccessWorstSkill = (A, B) =>
        {
            int a = A.levelInt;
            int b = B.levelInt;
            if (a == b) return (A.passion <= B.passion) ? A : B;
            return (a < b) ? A : B;
        };

        private static float lookRadius = 5f;
        private static ThingRequestGroup[] subjects = { ThingRequestGroup.Art, ThingRequestGroup.Plant };
        private static List<Rule_String> tempRules = new List<Rule_String>();
        private static IDictionary<int, string> bodyParts = new Dictionary<int, string>()
        {
            {0, "Torso"}, {1, "Rib"}, {2, "Sternum"}, {3, "Pelvis"}, {4, "Spine"}, {5, "Stomach"}, {6, "Heart"}, {7, "Left Lung"}, {8, "Right Lung"}, {9, "Left Kidney"}, {10, "Right Kidney"}, {11, "Liver"}, {12, "Neck"}, {13, "Head"}, {14, "Skull"}, {15, "Brain"},
            {16, "Left Eye"}, {17, "Right Eye"}, {18, "Left Ear"}, {19, "Right Ear"}, {20, "Nose"}, {21, "Jaw"}, {22, "Left Shoulder"}, {23, "Left Clavicle"}, {24, "Left Arm"}, {25, "Left Humerus"}, {26, "Left Radius"}, {27, "Left Hand"}, {28, "Left Pinky"},
            {29, "Left Ring Finger"}, {30, "Left Middle Finger"}, {31, "Left Index Finger"}, {32, "Left Thumb"}, {33, "Right Shoulder"}, {34, "Right Clavicle"}, {35, "Right Arm"}, {36, "Right Humerus"}, {37, "Right Radius"}, {38, "Right Hand"}, {39, "Right Pinky"},
            {40, "Right Ring Finger"}, {41, "Right Middle Finger"}, {42, "Right Index Finger"}, {43, "Right Thumb"}, {44, "Waist"}, {45, "Left Leg"}, {46, "Left Femur"}, {47, "Left Tibia"}, {48, "Left Foot"}, {49, "Left Little Toe"}, {50, "Left Fourth Toe"},
            {51, "Left Middle Toe"}, {52, "Left Second Toe"}, {53, "Left Big Toe"}, {54, "Right Leg"}, {55, "Right Femur"}, {56, "Right Tibia"}, {57, "Right Foot"}, {58, "Right Little Toe"}, {59, "Right Fourth Toe"}, {60, "Right Middle Toe"}, {61, "Right Second Toe"}, {62, "Right Big Toe"}
        };
        private static List<string> reversibleRelations = new List<string>()
                { "Bond", "Sibling", "Spouse", "Lover", "Fiance", "HalfSibling", "ExSpouse", "ExLover", "Cousin", "CousinOnceRemoved", "SecondCousin", "Kin"
                };
        public enum dayPeriod { morning, afternoon, evening, night }

        public static IEnumerable<Rule> ExtraRules()
        {
            Pawn initiator = DialogManager.Initiator;
            if (initiator == null || !initiator.RaceProps.Humanlike) return null;
            Pawn recipient = DialogManager.Recipient;
            tempRules = new List<Rule_String>();
            try
            {
                ExtraRulesForPawn(initPrefix, initiator, recipient);
                if (recipient.IsValid()) ExtraRulesForPawn(reciPrefix, recipient, initiator);
                ExtraRulesForTime(initiator);
                ExtraRulesForMap(initiator);
            }
            catch (Exception e)
            {
                StringBuilder msg = new StringBuilder();
                msg.Append($"[SpeakUp] Error processing extra rules: {e.Message}");
                msg.AppendInNewLine($"Initator: {initiator}, ");
                msg.Append(recipient.IsValid() ? $"recipient: {recipient}." : "invalid recipient.");
                msg.AppendInNewLine(tempRules.Count > 0 ? $"Last successful rule: {tempRules.Last()}" : "Zero rules processed.");
                Log.Warning(msg.ToString());
                return null;
            }
            return tempRules;
        }

        public static void ExtraRulesForPawn(string symbol, Pawn pawn, Pawn other = null)
        {
            //THE PAWN'S MINDSTATE:

            //mood
            MakeRule(symbol + "mood", pawn.needs.mood.CurLevel.ToString());

            //thoughts
            List<Thought> thoughts = new List<Thought>();
            pawn.needs.mood.thoughts.GetAllMoodThoughts(thoughts);
            List<string> texts = new List<string>();
            
            for (int i = thoughts.Count; i-- > 0;)
            {
                var thought = thoughts[i];
                MakeRule(symbol + "thoughtDefName", thought.def.defName);
                if (thought.CurStage != null)
                {
                    MakeRule(symbol + "thoughtLabel", thought.CurStage.label);
                    if (!thought.CurStage.description.NullOrEmpty()) texts.Add(thought.CurStage.description);
                }
            }
            MakeRule(symbol + "thoughtText", texts.RandomElement());

            //THE PAWN'S RELATIONS
            if (other != null)
            {
                //opinion
                MakeRule(symbol + "opinion", pawn.relations.OpinionOf(other).ToString());

                //relationships
                bool flag1 = true;

                foreach (PawnRelationDef relation in PawnRelationUtility.GetRelations(pawn, other))
                {
                    if (reversibleRelations.Contains(relation.defName))
                    {
                        MakeRule(symbol + "relationship", relation.defName);
                        flag1 = false;
                    }
                }

                foreach (PawnRelationDef relation in PawnRelationUtility.GetRelations(other, pawn))
                {
                    if (!reversibleRelations.Contains(relation.defName))
                    {
                        MakeRule(symbol + "relationship", relation.defName);
                        flag1 = false;
                    }
                }


                if (flag1)
                {
                    MakeRule(symbol + "relationship", "None");
                }
            }

            //THE PAWN'S BIO:

            //traits
            var allTraits = pawn.story.traits.allTraits;
            for (int i = allTraits.Count; i-- > 0;)
            {
                MakeRule(symbol + "trait", allTraits[i].Label);
            }

            //best skill
            var skills = pawn.skills.skills;
            MakeRule(symbol + "bestSkill", skills.Aggregate(AccessHighestSkill).def.skillLabel);

            //worst skill
            MakeRule(symbol + "worstSkill", skills.Aggregate(AccessWorstSkill).def.skillLabel);

            //higher passion
            MakeRule(symbol + "higherPassion", skills.Aggregate(AccessHighestPassion).def.skillLabel);

            //all skills
            for (int i = skills.Count; i-- > 0;)
            {
                var skill = skills[i];
                MakeRule(symbol + skill.def.label + "_level", skill.levelInt.ToString());
                MakeRule(symbol + skill.def.label + "_passion", skill.passion.ToString());
            }

            //childhood
            MakeRule(symbol + "childhood", pawn.story.Childhood?.title);

            //adulthood
            MakeRule(symbol + "adulthood", pawn.story.Adulthood?.title);

            //OTHER PAWN SITUATIONS

            //moving?
            MakeRule(symbol + "moving", pawn.pather.Moving.ToStringYesNo());

            //current activity
            var curJob = pawn.CurJob;
            if (curJob != null)
            {
                MakeRule(symbol + "jobDefName", curJob.def.defName);
                MakeRule(symbol + "jobText", curJob.GetReport(pawn));
            }

            //seated?
            if (pawn.Map != null) MakeRule(symbol + "seated", Seated(pawn).ToStringYesNo());

            //THE PAWNS PHYSICAL DETAILS ------------- add bodyparts

            //invenotry does not include worn items
            var innerContainer = pawn.inventory.innerContainer;
            for (int i = innerContainer.Count; i-- > 0;)
            {
                MakeRule(symbol + "inventory_item", innerContainer[i].def.defName);
            }

            //worn items
            var wornApparel = pawn.apparel.WornApparel;
            for (int i = wornApparel.Count; i-- > 0;)
            {
                Apparel apparel = wornApparel[i];
                //substring to elimante (quality) from label so XML is easier
                if (apparel.Label.LastIndexOf('(') != -1)
                {
                    MakeRule(symbol + "wearing", apparel.Label.Substring(0, apparel.Label.LastIndexOf("(") - 1));
                    continue;
                }
                else
                {
                    MakeRule(symbol + "wearing", apparel.Label);
                }
            }

            //needs tending
            MakeRule(symbol + "needs_tending", pawn.health.HasHediffsNeedingTend().ToStringYesNo());

            //injuries
            var hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count; i-- > 0;)
            {
                Hediff hediff = hediffs[i];

                //injuries and missing body parts
                if (hediff is Hediff_Injury)
                {
                    MakeRule(symbol + "injury", bodyParts.TryGetValue(hediff.Part.Index) + " " + hediff.Label);
                    continue;
                }
                else if (hediff is Hediff_MissingPart)
                {
                    MakeRule(symbol + "missing_part", bodyParts.TryGetValue(hediff.Part.Index) + " " + hediff.Label);
                    continue;
                }

                //any other health effect and what part it effects
                if (hediff.Part != null)
                {
                    MakeRule(symbol + "ailment", bodyParts.TryGetValue(hediff.Part.Index) + " " + hediff.Label);
                    continue;
                }
                else
                {
                    MakeRule(symbol + "ailment", hediff.Label);
                }
            }
        }

        private static string DayPeriod(Pawn p)
        {
            int hour = GenLocalDate.HourInteger(p);
            if (hour >= 5 && hour < 12) return dayPeriod.morning.ToString();
            if (hour >= 12 && hour < 18) return dayPeriod.afternoon.ToString();
            if (hour >= 18 && hour < 24) return dayPeriod.evening.ToString();
            else return dayPeriod.night.ToString();
        }

        private static void ExtraRulesForMap(Pawn initiator)
        {
            Map map = initiator.Map;
            if (map == null) return;
            IntVec3 pos = initiator.Position;

            //climate
            MakeRule("WEATHER", map.weatherManager.CurWeatherPerceived.label);

            //temperature
            MakeRule("TEMPERATURE", GenTemperature.GetTemperatureForCell(pos, map).ToString());

            //outdoor?
            MakeRule("OUTDOORS", pos.UsesOutdoorTemperature(map).ToStringYesNo());

            //nearest things
            var listerThings = map.listerThings;
            for (int i = subjects.Length; i-- > 0;)
            {
                var group = subjects[i];
                var thing = GenClosest.ClosestThing_Global(pos, listerThings.ThingsInGroup(group), lookRadius);
                if (thing != null) MakeRule($"NEAREST_{group.ToString().ToLower()}", $"{thing.def.label}");
            }
        }

        private static void ExtraRulesForTime(Pawn initiator)
        {
            MakeRule("HOUR", GenLocalDate.HourInteger(initiator).ToString());
            MakeRule("DAYPERIOD", DayPeriod(initiator));
        }

        private static bool IsValid(this Pawn pawn)
        {
            return
                pawn != null &&
                pawn.RaceProps?.Humanlike == true; //Restricted to humanlike, for now.
        }

        private static void MakeRule(string keyword, string output = null)
        {
            if (output.NullOrEmpty())
            {
                if (Prefs.DevMode && SpeakUpSettings.showGrammarDebug) Log.Message($"[SpeakUp] Couldn't process {keyword}. Moving on.");
                return;
            }
            tempRules.Add(new Rule_String(keyword, output));
        }

        private static bool Seated(Pawn p)
        {
            Building edifice = p.Position.GetEdifice(p.Map);
            return edifice != null && edifice.def.category == ThingCategory.Building && edifice.def.building.isSittable;
        }
    }
}
